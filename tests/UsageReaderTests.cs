using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CodexUsageOverlay.Tests
{
    public sealed class FakeConnection : IRpcConnection
    {
        public event EventHandler<ProtocolNotification> Notification;
        public string Account = "a@example.test";
        public string AccountId = "workspace-a";
        public int QuotaCalls, Disposals, InFlight, Maximum;
        public bool SwitchDuringQuota, SwitchWorkspaceDuringQuota, WrongQuotaAccount, MissingAccount, FailQuota, InitialAccountNotice, OmitRouting, OmitQuotaIdentity, NullQuotaIdentity, EmptyQuotaIdentity;
        public readonly TaskCompletionSource<bool> Release = new TaskCompletionSource<bool>();
        public bool Block;
        public Task InitializeNotification(CancellationToken c) { return Task.FromResult(true); }
        public async Task<IDictionary<string, object>> RequestAsync(string method, object parameters, CancellationToken cancellation)
        {
            int n = Interlocked.Increment(ref InFlight); Maximum = Math.Max(Maximum, n);
            try
            {
                if (method == "initialize") { if (InitialAccountNotice) Notify("account/updated"); return Assert.Json("{\"userAgent\":\"test\"}"); }
                if (method == "account/read") return MissingAccount ? Assert.Json("{\"account\":null}") : Assert.Json("{\"account\":{\"type\":\"chatgpt\",\"email\":\"" + Account + "\",\"planType\":\"plus\"}" + (OmitRouting ? "" : ",\"workspaceRouting\":{\"chatgptAccountId\":" + new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(AccountId) + "}") + "}");
                if (method != "account/rateLimits/read") throw new Exception("Unexpected method");
                Interlocked.Increment(ref QuotaCalls);
                string quotaAccount = NullQuotaIdentity ? null : EmptyQuotaIdentity ? "" : WrongQuotaAccount ? "unrelated" : AccountId;
                if (Block) await Release.Task.ConfigureAwait(false);
                if (SwitchDuringQuota) { Account = "b@example.test"; SwitchDuringQuota = false; }
                if (SwitchWorkspaceDuringQuota) { AccountId = "workspace-b"; SwitchWorkspaceDuringQuota = false; }
                if (FailQuota) throw new System.IO.IOException("offline");
                return Assert.Json("{" + (OmitQuotaIdentity ? "" : "\"accountId\":" + new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(quotaAccount) + ",") + "\"rateLimits\":{\"limitId\":\"codex\",\"primary\":{\"usedPercent\":17,\"windowDurationMins\":300},\"secondary\":{\"usedPercent\":60,\"windowDurationMins\":10080}}}");
            }
            finally { Interlocked.Decrement(ref InFlight); }
        }
        public void Notify(string method) { var h = Notification; if (h != null) h(this, new ProtocolNotification { Method = method }); }
        public void Dispose() { Interlocked.Increment(ref Disposals); Release.TrySetResult(true); }
    }
    public static class UsageReaderTests
    {
        [Case("Reader")] public static void DelayedUiDeliveryReadsAuthoritativeState()
        {
            var fake = new FakeConnection(); Action delayed = null; QuotaView applied = null;
            using (var reader = new UsageReader(() => fake, 10000, 50, 30, 10))
            using (var dispatch = new UsageViewDispatcher(reader, action => { var current = reader.CurrentView.Current; if (current != null && current.Source.AccountId == "workspace-a") delayed = action; }, value => applied = value))
            {
                reader.SetActive(true); Assert.True(SpinWait.SpinUntil(() => delayed != null, 2000), "No delayed A delivery");
                Action oldDelivery = delayed; fake.Account = "b@example.test"; fake.AccountId = "workspace-b"; fake.Notify("account/updated");
                oldDelivery(); Assert.True(applied.Current == null && applied.LastKnown == null, "A reappeared after invalidation");
                reader.RefreshNow(); Assert.True(SpinWait.SpinUntil(() => reader.CurrentView.Current != null && reader.CurrentView.Current.Source.AccountId == "workspace-b", 2000), "No B snapshot");
                oldDelivery(); Assert.Equal("workspace-b", applied.Current.Source.AccountId);
            }
        }
        [Case("Reader")] public static void MissingStableWorkspaceIdentityStaysUnknown()
        {
            foreach (int mode in new[] { 0, 1, 2 })
            {
                var fake = new FakeConnection { OmitRouting = mode == 0, AccountId = mode == 1 ? null : mode == 2 ? "" : "workspace-a" }; bool unsafeCurrent = false, rejected = false;
                using (var reader = new UsageReader(() => fake, 10000, 50, 30, 10))
                {
                    reader.Changed += delegate(object s, QuotaViewEventArgs e) { if (e.View.Current != null) unsafeCurrent = true; if (e.View.State == DisplayState.SourceUnknown) rejected = true; };
                    reader.SetActive(true); Assert.True(SpinWait.SpinUntil(() => rejected || unsafeCurrent, 2000), "No identity result"); Assert.True(rejected && !unsafeCurrent, "Missing/null/empty stable workspace ID accepted");
                }
            }
        }
        [Case("Reader")] public static void MissingQuotaIdentityStaysUnknown()
        {
            foreach (int mode in new[] { 0, 1, 2 })
            {
                var fake = new FakeConnection { OmitQuotaIdentity = mode == 0, NullQuotaIdentity = mode == 1, EmptyQuotaIdentity = mode == 2 }; bool unsafeCurrent = false, rejected = false;
                using (var reader = new UsageReader(() => fake, 10000, 50, 30, 10))
                {
                    reader.Changed += delegate(object s, QuotaViewEventArgs e) { if (e.View.Current != null) unsafeCurrent = true; if (fake.QuotaCalls > 0 && e.View.State == DisplayState.SourceUnknown) rejected = true; };
                    reader.SetActive(true); Assert.True(SpinWait.SpinUntil(() => rejected || unsafeCurrent, 2000), "No quota result"); Assert.True(rejected && !unsafeCurrent, "Missing/null/empty quota account ID accepted");
                }
            }
        }
        [Case("Reader")] public static void WorkspaceChangeWithSameEmailFencesLateResponse()
        {
            var fake = new FakeConnection { SwitchWorkspaceDuringQuota = true }; bool wrong = false, right = false;
            using (var reader = new UsageReader(() => fake, 50, 50, 30, 10))
            {
                reader.Changed += delegate(object s, QuotaViewEventArgs e) { if (e.View.Current != null) { if (e.View.Current.Source.SessionKey.Contains("workspace-a")) wrong = true; if (e.View.Current.Source.SessionKey.Contains("workspace-b")) right = true; } };
                reader.SetActive(true); Assert.True(SpinWait.SpinUntil(() => right || wrong, 2000), "Workspace identity absent"); Assert.True(right && !wrong, "Same-email workspace changed during read");
            }
        }
        [Case("Reader")] public static void WrongQuotaAccountNeverBecomesCurrent()
        {
            var fake = new FakeConnection { WrongQuotaAccount = true }; bool unsafeCurrent = false, rejected = false;
            using (var reader = new UsageReader(() => fake, 50, 50, 30, 10))
            {
                reader.Changed += delegate(object s, QuotaViewEventArgs e) { if (e.View.Current != null) unsafeCurrent = true; if (fake.QuotaCalls > 0 && e.View.State == DisplayState.SourceUnknown) rejected = true; };
                reader.SetActive(true); Assert.True(SpinWait.SpinUntil(() => rejected || unsafeCurrent, 2000), "No result"); Assert.True(rejected && !unsafeCurrent, "Quota account mismatch accepted");
            }
        }
        [Case("Reader")] public static void InitialAccountNotificationRecovers()
        {
            var fake = new FakeConnection { InitialAccountNotice = true }; bool current = false;
            using (var reader = new UsageReader(() => fake, 10000, 50, 30, 10))
            {
                reader.Changed += delegate(object s, QuotaViewEventArgs e) { if (e.View.Current != null) current = true; };
                reader.SetActive(true); Assert.True(SpinWait.SpinUntil(() => current, 2000), "Initial notification prevented recovery");
            }
        }
        [Case("Reader")] public static void OnlyOneQuotaReadInFlight()
        {
            var fake = new FakeConnection { Block = true };
            using (var reader = new UsageReader(() => fake, 40, 50, 30, 10))
            using (var ready = new ManualResetEventSlim())
            {
                reader.Changed += delegate(object s, QuotaViewEventArgs e) { if (e.View.Current != null) ready.Set(); };
                reader.SetActive(true); reader.RefreshNow(); reader.RefreshNow();
                Assert.True(SpinWait.SpinUntil(() => fake.QuotaCalls == 1, 2000), "No quota request");
                reader.RefreshNow(); fake.Release.SetResult(true);
                Assert.True(ready.Wait(2000), "No snapshot"); Assert.Equal(1, fake.Maximum);
            }
        }
        [Case("Reader")] public static void SparseNotificationDebouncesFullRead()
        {
            var fake = new FakeConnection(); int current = 0;
            using (var reader = new UsageReader(() => fake, 10000, 50, 30, 10))
            {
                reader.Changed += delegate(object s, QuotaViewEventArgs e) { if (e.View.Current != null && e.View.State == DisplayState.Current) Interlocked.Increment(ref current); };
                reader.SetActive(true); Assert.True(SpinWait.SpinUntil(() => current > 0, 2000), "No initial snapshot");
                int baseline = fake.QuotaCalls;
                fake.Notify("account/rateLimits/updated"); fake.Notify("account/rateLimits/updated");
                Assert.True(SpinWait.SpinUntil(() => fake.QuotaCalls > baseline, 2000), "Sparse update did not refetch");
                Assert.Equal(1, fake.Maximum);
            }
        }
        [Case("Reader")] public static void IdentityChangeFencesLateResponse()
        {
            var fake = new FakeConnection { SwitchDuringQuota = true }; bool wrong = false, right = false;
            using (var reader = new UsageReader(() => fake, 50, 50, 30, 10))
            {
                reader.Changed += delegate(object s, QuotaViewEventArgs e) { if (e.View.Current != null) { if (e.View.Current.Source.LocalLabel == "a@example.test") wrong = true; if (e.View.Current.Source.LocalLabel == "b@example.test") right = true; } };
                reader.SetActive(true); Assert.True(SpinWait.SpinUntil(() => right, 3000), "New account never received quota"); Assert.True(!wrong, "Late old-account quota published");
            }
        }
        [Case("Reader")] public static void UnknownIdentityDropsSnapshot()
        {
            var fake = new FakeConnection(); bool had = false, cleared = false;
            using (var reader = new UsageReader(() => fake, 40, 50, 30, 10))
            {
                reader.Changed += delegate(object s, QuotaViewEventArgs e) { if (e.View.Current != null) had = true; if (had && e.View.Current == null && (e.View.State == DisplayState.SignInNeeded || e.View.State == DisplayState.SourceUnknown)) cleared = true; };
                reader.SetActive(true); Assert.True(SpinWait.SpinUntil(() => had, 2000), "No initial quota");
                fake.MissingAccount = true; reader.RefreshNow(); Assert.True(SpinWait.SpinUntil(() => cleared, 2000), "Uncertain identity retained quota");
            }
        }
        [Case("Reader")] public static void InactiveStopsOnlyOwnedConnection()
        {
            var fake = new FakeConnection(); bool had = false;
            using (var reader = new UsageReader(() => fake, 10000, 30, 20, 10))
            {
                reader.Changed += delegate(object s, QuotaViewEventArgs e) { if (e.View.Current != null) had = true; };
                reader.SetActive(true); Assert.True(SpinWait.SpinUntil(() => had, 2000), "No initial quota");
                reader.SetActive(false); Assert.True(SpinWait.SpinUntil(() => fake.Disposals == 1, 2000), "Owned child remained alive");
            }
        }
    }
}
