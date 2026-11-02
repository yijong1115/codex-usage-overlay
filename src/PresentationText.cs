using System;
using System.Globalization;
using System.Text;

namespace CodexUsageOverlay
{
    public static class PresentationText
    {
        public static string Percent(QuotaView view, bool fiveHour)
        {
            QuotaSnapshot s = view == null ? null : view.Current;
            double? remaining = s == null ? null : (fiveHour ? s.FiveHour : s.Weekly).RemainingPercent;
            return remaining.HasValue ? Math.Round(remaining.Value, MidpointRounding.AwayFromZero).ToString(CultureInfo.CurrentCulture) + "%" : "—";
        }
        public static string Details(QuotaView view, bool fiveHour, DateTimeOffset now)
        {
            string title = fiveHour ? "Five-hour allowance" : "Weekly allowance";
            if (view == null) return title + "\nChecking usage…";
            var text = new StringBuilder(title).Append("\n");
            var snapshot = view.Current ?? view.LastKnown;
            if (view.Current != null) text.Append(Percent(view, fiveHour)).Append(" remaining\n");
            text.Append(view.Status ?? QuotaPolicy.Label(view.State)).Append("\n");
            if (snapshot != null)
            {
                QuotaWindow window = fiveHour ? snapshot.FiveHour : snapshot.Weekly;
                if (view.Current == null && window.RemainingPercent.HasValue) text.Append("Last known: ").Append(Math.Round(window.RemainingPercent.Value)).Append("% remaining\n");
                if (window.ResetsAt.HasValue)
                {
                    text.Append("Resets ").Append(window.ResetsAt.Value.ToLocalTime().ToString("ddd d MMM, h:mm tt", CultureInfo.CurrentCulture)).Append(" (local)\n");
                    TimeSpan wait = window.ResetsAt.Value - now;
                    text.Append(wait.TotalSeconds > 0 ? "In " + (int)wait.TotalHours + "h " + wait.Minutes + "m" : "Awaiting confirmation of reset").Append("\n");
                }
                else text.Append("Reset time unavailable\n");
                text.Append("Last check: ").Append(snapshot.CheckedAt.ToLocalTime().ToString("h:mm:ss tt", CultureInfo.CurrentCulture));
                text.Append(" · ").Append(Math.Max(0, (int)(now - snapshot.CheckedAt).TotalSeconds)).Append("s ago\n\n");
                text.Append("Local Codex account\n").Append(snapshot.Source.LocalLabel).Append("\nDesktop account match: unverified");
            }
            else text.Append("Open Codex or use Refresh now.\nLocal Codex account is unverified.");
            return text.ToString();
        }
    }
}
