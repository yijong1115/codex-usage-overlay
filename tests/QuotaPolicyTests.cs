using System;
using System.Collections.Generic;

namespace CodexUsageOverlay.Tests
{
    public static class QuotaPolicyTests
    {
        static readonly DateTimeOffset Now = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
        static readonly SourceIdentity Source = new SourceIdentity("local-a", "Local test account");
        const string Both = "{\"rateLimits\":{\"limitId\":\"codex\",\"planType\":\"pro\",\"primary\":{\"usedPercent\":60,\"windowDurationMins\":10080,\"resetsAt\":1793776359},\"secondary\":{\"usedPercent\":17,\"windowDurationMins\":300,\"resetsAt\":1793547522}}}";
        static QuotaSnapshot Sample() { return QuotaPolicy.Normalize(Assert.Json(Both), Source, 1, Now); }
        [Case("Quota")] public static void WindowsFollowDurationNotPlanOrOrder()
        {
            var s = Sample(); Assert.Equal(83.0, s.FiveHour.RemainingPercent.Value); Assert.Equal(40.0, s.Weekly.RemainingPercent.Value);
            Assert.Equal(1793547522L, s.FiveHour.ResetsAt.Value.ToUnixSeconds());
        }
        [Case("Quota")] public static void CodexBucketWins()
        {
            var s = QuotaPolicy.Normalize(Assert.Json("{\"rateLimits\":{\"limitId\":\"other\"},\"rateLimitsByLimitId\":{\"other\":{\"primary\":{\"usedPercent\":99,\"windowDurationMins\":300}},\"codex\":{\"primary\":{\"usedPercent\":12,\"windowDurationMins\":300}}}}"), Source, 1, Now);
            Assert.Equal(88.0, s.FiveHour.RemainingPercent.Value); Assert.True(!s.Weekly.RemainingPercent.HasValue, "Missing weekly is unavailable");
        }
        [Case("Quota")] public static void MissingIsNotZero()
        {
            var s = QuotaPolicy.Normalize(Assert.Json("{\"rateLimits\":{\"limitId\":\"codex\",\"primary\":null}}"), Source, 1, Now);
            Assert.True(!s.FiveHour.RemainingPercent.HasValue && !s.Weekly.RemainingPercent.HasValue, "Missing windows became full/empty quota");
        }
        [Case("Quota")] public static void InvalidNumbersAreUnavailable()
        {
            foreach (string raw in new[] { "null", "\"17\"", "true" })
            {
                var s = QuotaPolicy.Normalize(Assert.Json("{\"rateLimits\":{\"limitId\":\"codex\",\"primary\":{\"usedPercent\":" + raw + ",\"windowDurationMins\":300}}}"), Source, 1, Now);
                Assert.True(!s.FiveHour.RemainingPercent.HasValue, "Invalid percent accepted");
            }
            var d = Assert.Json(Both); var b = (IDictionary<string, object>)d["rateLimits"]; var w = (IDictionary<string, object>)b["secondary"];
            w["usedPercent"] = double.NaN; Assert.True(!QuotaPolicy.Normalize(d, Source, 1, Now).FiveHour.RemainingPercent.HasValue, "NaN accepted");
            w["usedPercent"] = double.PositiveInfinity; Assert.True(!QuotaPolicy.Normalize(d, Source, 1, Now).FiveHour.RemainingPercent.HasValue, "Infinity accepted");
        }
        [Case("Quota")] public static void RemainingClamps()
        {
            foreach (int used in new[] { -5, 120 })
            {
                var s = QuotaPolicy.Normalize(Assert.Json("{\"rateLimits\":{\"limitId\":\"codex\",\"primary\":{\"usedPercent\":" + used + ",\"windowDurationMins\":300}}}"), Source, 1, Now);
                Assert.Equal(used < 0 ? 100.0 : 0.0, s.FiveHour.RemainingPercent.Value);
            }
        }
        [Case("Quota")] public static void WrongBucketAndDurationStayUnavailable()
        {
            var s = QuotaPolicy.Normalize(Assert.Json("{\"rateLimits\":{\"limitId\":\"different\",\"primary\":{\"usedPercent\":1,\"windowDurationMins\":300}}}"), Source, 1, Now);
            Assert.True(!s.FiveHour.RemainingPercent.HasValue, "Unrelated bucket substituted");
            s = QuotaPolicy.Normalize(Assert.Json("{\"rateLimits\":{\"limitId\":\"codex\",\"primary\":{\"usedPercent\":1,\"windowDurationMins\":20160}}}"), Source, 1, Now);
            Assert.True(!s.Weekly.RemainingPercent.HasValue, "Two-week window labelled weekly");
        }
        [Case("Quota")] public static void StaleAt150Seconds()
        {
            Assert.Equal(DisplayState.Current, QuotaPolicy.Project(Sample(), DisplayState.Current, Now.AddSeconds(149)).State);
            var stale = QuotaPolicy.Project(Sample(), DisplayState.Current, Now.AddSeconds(150));
            Assert.Equal(DisplayState.Stale, stale.State); Assert.True(stale.Current == null && stale.LastKnown != null, "Stale shown as current");
        }
        [Case("Quota")] public static void FailureRetainsOnlyLabelledLastKnown()
        {
            var retry = QuotaPolicy.Project(Sample(), DisplayState.Retry, Now.AddSeconds(50)); Assert.Equal(DisplayState.Retry, retry.State);
            var stale = QuotaPolicy.Project(Sample(), DisplayState.Retry, Now.AddSeconds(151)); Assert.True(stale.Current == null, "Retry extended freshness");
        }
        [Case("Quota")] public static void ResetDoesNotReplenish()
        {
            var s = Sample(); s.FiveHour.ResetsAt = Now.AddSeconds(1);
            Assert.Equal(83.0, QuotaPolicy.Project(s, DisplayState.Current, Now.AddSeconds(10)).Current.FiveHour.RemainingPercent.Value);
        }
        [Case("Quota")] public static void SourceUnknownClearsCurrent()
        {
            var v = QuotaPolicy.Project(Sample(), DisplayState.SourceUnknown, Now); Assert.True(v.Current == null && v.LastKnown == null, "Source uncertainty leaked quota");
        }
        [Case("Quota")] public static void AccountChangeClearsLastKnown()
        {
            var v = QuotaPolicy.Project(Sample(), DisplayState.SignInNeeded, Now); Assert.True(v.Current == null && v.LastKnown == null, "Old account details leaked");
        }
    }
}
