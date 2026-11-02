using System;

namespace CodexUsageOverlay.Tests
{
    public static class PresentationStateTests
    {
        [Case("Presentation")] public static void AutomaticRingColoursFollowDisplayedRemaining()
        {
            var settings = new OverlaySettings();
            foreach (double value in new[] { 100.0, 70.0, 69.5 }) Assert.Equal(settings.HighRemainingColor, RingColourPolicy.Choose(value, settings, true));
            foreach (double value in new[] { 69.0, 30.0, 29.6 }) Assert.Equal(settings.MediumRemainingColor, RingColourPolicy.Choose(value, settings, true));
            foreach (double value in new[] { 29.0, 0.0 }) Assert.Equal(settings.LowRemainingColor, RingColourPolicy.Choose(value, settings, false));
            Assert.Equal("#969BA3", RingColourPolicy.Choose(null, settings, true));
            Assert.Equal("#969BA3", RingColourPolicy.Choose(double.NaN, settings, true));
        }
        [Case("Presentation")] public static void ManualRingColoursRemainAvailable()
        {
            var settings = new OverlaySettings { AutomaticRingColours = false, FiveHourColor = "#123456", WeeklyColor = "#654321" };
            Assert.Equal("#123456", RingColourPolicy.Choose(10, settings, true)); Assert.Equal("#654321", RingColourPolicy.Choose(95, settings, false));
        }
        [Case("Presentation")] public static void StaleAndUnknownNeverDisplayCurrentPercentages()
        {
            var snapshot = new QuotaSnapshot { Source = new SourceIdentity("a", "test@example.test"), CheckedAt = DateTimeOffset.UtcNow };
            snapshot.FiveHour.RemainingPercent = 82.6;
            var current = QuotaPolicy.Project(snapshot, DisplayState.Current, snapshot.CheckedAt);
            Assert.Equal("83%", PresentationText.Percent(current, true));
            var stale = QuotaPolicy.Project(snapshot, DisplayState.Current, snapshot.CheckedAt.AddSeconds(150));
            Assert.Equal("—", PresentationText.Percent(stale, true));
            Assert.True(PresentationText.Details(stale, true, snapshot.CheckedAt.AddSeconds(150)).Contains("Last known"), "Stale details lose last-known label");
            var unknown = QuotaPolicy.Project(snapshot, DisplayState.SourceUnknown, snapshot.CheckedAt);
            Assert.True(!PresentationText.Details(unknown, true, snapshot.CheckedAt).Contains("83%"), "Unknown account leaked percentage");
        }
        [Case("Presentation")] public static void MissingResetAndUnverifiedSourceAreExplicit()
        {
            var snapshot = new QuotaSnapshot { Source = new SourceIdentity("a", "test@example.test"), CheckedAt = DateTimeOffset.UtcNow };
            snapshot.FiveHour.RemainingPercent = 83;
            var view = QuotaPolicy.Project(snapshot, DisplayState.Current, snapshot.CheckedAt);
            string text = PresentationText.Details(view, true, snapshot.CheckedAt);
            Assert.True(text.Contains("Reset time unavailable"), "Missing reset fabricated");
            Assert.True(text.Contains("Local Codex account") && text.Contains("unverified"), "Source match overstated");
            snapshot.FiveHour.ResetsAt = snapshot.CheckedAt.AddSeconds(-1);
            Assert.True(PresentationText.Details(view, true, snapshot.CheckedAt).Contains("Awaiting confirmation"), "Reset assumed replenishment");
        }
    }
}
