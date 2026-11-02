using System;
using System.Collections.Generic;

namespace CodexUsageOverlay
{
    public static class JsonValue
    {
        public static object Get(IDictionary<string, object> d, string key) { object v; return d != null && d.TryGetValue(key, out v) ? v : null; }
        public static IDictionary<string, object> Dict(object value) { return value as IDictionary<string, object>; }
        public static string Text(object value) { return value as string; }
        public static double? Number(object value)
        {
            if (!(value is int || value is long || value is double || value is float || value is decimal)) return null;
            double n = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
            return double.IsNaN(n) || double.IsInfinity(n) ? null : (double?)n;
        }
    }
    public static class QuotaPolicy
    {
        public static QuotaSnapshot Normalize(IDictionary<string, object> response, SourceIdentity source, long generation, DateTimeOffset checkedAt)
        {
            var snapshot = new QuotaSnapshot { Source = source, Generation = generation, CheckedAt = checkedAt };
            var all = JsonValue.Dict(JsonValue.Get(response, "rateLimitsByLimitId"));
            var bucket = JsonValue.Dict(JsonValue.Get(all, "codex"));
            if (bucket == null)
            {
                bucket = JsonValue.Dict(JsonValue.Get(response, "rateLimits"));
                if (JsonValue.Text(JsonValue.Get(bucket, "limitId")) != "codex") bucket = null;
            }
            foreach (string key in new[] { "primary", "secondary" })
            {
                var raw = JsonValue.Dict(JsonValue.Get(bucket, key));
                double? duration = JsonValue.Number(JsonValue.Get(raw, "windowDurationMins"));
                double? used = JsonValue.Number(JsonValue.Get(raw, "usedPercent"));
                if (!used.HasValue) continue;
                QuotaWindow window = duration == 300 ? snapshot.FiveHour : duration == 10080 ? snapshot.Weekly : null;
                if (window == null) continue;
                window.RemainingPercent = Math.Max(0, Math.Min(100, 100 - used.Value));
                double? reset = JsonValue.Number(JsonValue.Get(raw, "resetsAt"));
                if (reset.HasValue) window.ResetsAt = TimeExtensions.FromUnix(reset.Value);
            }
            return snapshot;
        }
        public static QuotaView Project(QuotaSnapshot snapshot, DisplayState state, DateTimeOffset now)
        {
            var view = new QuotaView { State = state, Status = Label(state) };
            if (state == DisplayState.SignInNeeded || state == DisplayState.SourceUnknown || snapshot == null) return view;
            if ((now - snapshot.CheckedAt).TotalSeconds >= 150)
            {
                view.State = DisplayState.Stale; view.Status = "Stale — waiting for a fresh check"; view.LastKnown = snapshot; return view;
            }
            if (state == DisplayState.Current || state == DisplayState.Retry)
            {
                view.Current = snapshot;
                if (!snapshot.FiveHour.RemainingPercent.HasValue && !snapshot.Weekly.RemainingPercent.HasValue)
                { view.State = DisplayState.Unavailable; view.Status = "Quota windows unavailable"; }
            }
            return view;
        }
        public static string Label(DisplayState state)
        {
            switch (state)
            {
                case DisplayState.Current: return "Current";
                case DisplayState.Retry: return "Retrying — showing last successful check";
                case DisplayState.SignInNeeded: return "Sign-in needed in Codex";
                case DisplayState.SourceUnknown: return "Local Codex account could not be verified";
                case DisplayState.Stale: return "Stale";
                case DisplayState.Unavailable: return "Unavailable";
                default: return "Checking usage…";
            }
        }
    }
}
