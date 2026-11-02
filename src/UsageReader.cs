using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CodexUsageOverlay
{
    public sealed class UsageReader : IUsageReader
    {
        readonly object gate = new object();
        readonly Func<IRpcConnection> factory;
        readonly int pollMs, idleMs, debounceMs, retryMs;
        readonly Timer timer;
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        IRpcConnection connection;
        QuotaSnapshot snapshot;
        DisplayState state = DisplayState.Loading;
        bool active, manual, busy, disposed;
        long generation;
        double nextDue, inactiveAt, capturedAt;
        int failures;
        public event EventHandler<QuotaViewEventArgs> Changed;
        public QuotaView CurrentView { get { lock (gate) { return ViewLocked(Clock); } } }
        static double Clock { get { return Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency; } }
        public UsageReader() : this(CodexLocator.Connect, 60000, 60000, 500, 5000) { }
        internal UsageReader(Func<IRpcConnection> connect, int poll, int idle, int debounce, int retry)
        {
            factory = connect; pollMs = poll; idleMs = idle; debounceMs = debounce; retryMs = retry;
            inactiveAt = Clock; timer = new Timer(Tick, null, 20, Math.Min(250, Math.Max(10, poll / 2)));
        }
        public void SetActive(bool value)
        {
            lock (gate)
            {
                if (disposed || active == value) return;
                active = value; if (value) nextDue = Clock; else inactiveAt = Clock;
            }
        }
        public void RefreshNow() { lock (gate) { if (disposed) return; manual = true; nextDue = Clock; } }
        void Tick(object ignored)
        {
            IRpcConnection stop = null; bool run = false; QuotaView view;
            lock (gate)
            {
                if (disposed) return;
                double now = Clock;
                if (!active && !manual && now - inactiveAt >= idleMs && connection != null)
                { stop = connection; connection = null; generation++; snapshot = null; state = DisplayState.Loading; }
                if ((active || manual) && !busy && now >= nextDue) { busy = true; manual = false; run = true; }
                view = ViewLocked(now);
            }
            if (stop != null) stop.Dispose();
            Publish(view);
            if (run) Task.Run((Func<Task>)Cycle);
        }
        QuotaView ViewLocked(double now)
        {
            DateTimeOffset projected = snapshot == null ? DateTimeOffset.UtcNow : snapshot.CheckedAt.AddMilliseconds(Math.Max(0, now - capturedAt));
            return QuotaPolicy.Project(snapshot, state, projected);
        }
        async Task<SourceIdentity> Account(IRpcConnection c)
        {
            var response = await c.RequestAsync("account/read", new { refreshToken = false }, lifetime.Token).ConfigureAwait(false);
            var account = JsonValue.Dict(JsonValue.Get(response, "account"));
            if (account == null) throw new ProtocolException(true);
            string type = JsonValue.Text(JsonValue.Get(account, "type"));
            if (type == "apiKey" || type == "apikey" || type == "amazonBedrock") throw new ProtocolException(true);
            string email = JsonValue.Text(JsonValue.Get(account, "email"));
            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(email)) throw new InvalidOperationException("Source unknown");
            string accountId = JsonValue.Text(JsonValue.Get(JsonValue.Dict(JsonValue.Get(response, "workspaceRouting")), "chatgptAccountId"));
            if (string.IsNullOrWhiteSpace(accountId)) throw new InvalidOperationException("Stable source identity unavailable");
            string workspace = JsonValue.Text(JsonValue.Get(account, "workspaceName"));
            string label = email.Trim() + (string.IsNullOrWhiteSpace(workspace) ? "" : "\n" + workspace);
            return new SourceIdentity(type + "\n" + email.Trim().ToLowerInvariant() + "\n" + accountId, label, accountId);
        }
        async Task Cycle()
        {
            IRpcConnection local = null; long ticket = 0; bool verifying = true;
            try
            {
                lock (gate) { if (disposed) return; ticket = generation; local = connection; }
                if (local == null)
                {
                    local = factory();
                    lock (gate) { if (disposed || ticket != generation) { local.Dispose(); return; } connection = local; }
                    local.Notification += OnNotification;
                    await local.RequestAsync("initialize", new { clientInfo = new { name = "codex_usage_overlay_windows", title = "Local Windows usage overlay", version = "1.0.0" } }, lifetime.Token).ConfigureAwait(false);
                    await local.InitializeNotification(lifetime.Token).ConfigureAwait(false);
                }
                SourceIdentity before = await Account(local).ConfigureAwait(false);
                lock (gate)
                {
                    if (ticket != generation || disposed) return;
                    if (snapshot != null && snapshot.Source.SessionKey != before.SessionKey) { snapshot = null; state = DisplayState.Loading; }
                }
                verifying = false;
                var quota = await local.RequestAsync("account/rateLimits/read", new { excludeResetCreditDetails = true }, lifetime.Token).ConfigureAwait(false);
                verifying = true;
                SourceIdentity after = await Account(local).ConfigureAwait(false);
                string quotaAccount = JsonValue.Text(JsonValue.Get(quota, "accountId"));
                lock (gate)
                {
                    if (disposed || ticket != generation) return;
                    if (before.SessionKey != after.SessionKey || string.IsNullOrWhiteSpace(quotaAccount) || after.AccountId != quotaAccount) { generation++; snapshot = null; state = DisplayState.SourceUnknown; nextDue = Clock + debounceMs; return; }
                    snapshot = QuotaPolicy.Normalize(quota, after, generation, DateTimeOffset.UtcNow);
                    capturedAt = Clock; state = DisplayState.Current; failures = 0;
                    double delay = pollMs;
                    foreach (var w in new[] { snapshot.FiveHour, snapshot.Weekly }) if (w.ResetsAt.HasValue)
                    { double until = (w.ResetsAt.Value - DateTimeOffset.UtcNow).TotalMilliseconds; if (until > 0) delay = Math.Min(delay, until + 500); }
                    nextDue = Clock + Math.Max(20, delay);
                }
            }
            catch (Exception e)
            {
                IRpcConnection close = null;
                lock (gate)
                {
                    if (disposed || ticket != generation) return;
                    var protocol = e as ProtocolException;
                    bool auth = protocol != null && protocol.Authentication;
                    if (auth || verifying) snapshot = null;
                    state = auth ? DisplayState.SignInNeeded : verifying ? DisplayState.SourceUnknown : DisplayState.Retry;
                    close = connection; connection = null; generation++;
                    failures = Math.Min(5, failures + 1); nextDue = Clock + Math.Min(60000, retryMs * Math.Pow(2, failures - 1));
                }
                if (close != null) close.Dispose();
            }
            finally
            {
                QuotaView view;
                lock (gate) { busy = false; view = ViewLocked(Clock); }
                Publish(view);
            }
        }
        void OnNotification(object sender, ProtocolNotification notification)
        {
            lock (gate)
            {
                if (disposed || !object.ReferenceEquals(sender, connection)) return;
                if (notification.Method == "account/updated") { generation++; snapshot = null; state = DisplayState.SourceUnknown; }
                nextDue = Clock + debounceMs;
            }
        }
        void Publish(QuotaView view)
        {
            var handler = Changed; if (handler != null && !disposed) handler(this, new QuotaViewEventArgs(view));
        }
        public void Dispose()
        {
            IRpcConnection close;
            lock (gate) { if (disposed) return; disposed = true; active = false; generation++; snapshot = null; close = connection; connection = null; }
            timer.Dispose(); lifetime.Cancel(); if (close != null) close.Dispose();
        }
    }
}
