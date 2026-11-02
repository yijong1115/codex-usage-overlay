using System;

namespace CodexUsageOverlay
{
    public static class RingColourPolicy
    {
        public static string Choose(double? remaining, OverlaySettings settings, bool fiveHour)
        {
            if (!remaining.HasValue || double.IsNaN(remaining.Value) || double.IsInfinity(remaining.Value)) return "#969BA3";
            if (!settings.AutomaticRingColours) return fiveHour ? settings.FiveHourColor : settings.WeeklyColor;
            double displayed = Math.Round(Math.Max(0, Math.Min(100, remaining.Value)), MidpointRounding.AwayFromZero);
            return displayed >= 70 ? settings.HighRemainingColor : displayed >= 30 ? settings.MediumRemainingColor : settings.LowRemainingColor;
        }
    }
}
