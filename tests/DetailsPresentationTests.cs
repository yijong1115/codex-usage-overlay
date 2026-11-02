using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace CodexUsageOverlay.Tests
{
    public static class DetailsPresentationTests
    {
        static TextBlock Find(DependencyObject parent, string id)
        {
            if (AutomationProperties.GetAutomationId(parent) == id) return parent as TextBlock;
            foreach (object child in LogicalTreeHelper.GetChildren(parent))
            {
                var dependency = child as DependencyObject; if (dependency == null) continue;
                var found = Find(dependency, id); if (found != null) return found;
            }
            return null;
        }
        [Case("Presentation")] public static void StructuredDetailsKeepAccountDisclosureAndResetInformation()
        {
            DateTimeOffset now = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);
            var snapshot = new QuotaSnapshot { Source = new SourceIdentity("sample", "sample@example.test"), CheckedAt = now };
            snapshot.FiveHour.RemainingPercent = 83; snapshot.FiveHour.ResetsAt = now.AddHours(2);
            var popup = new DetailsWindow(true);
            try
            {
                popup.Apply(QuotaPolicy.Project(snapshot, DisplayState.Current, now), new OverlaySettings(), now);
                var remaining = Find(popup, "DetailsRemaining");
                Assert.True(remaining != null, "Usage details have no independently readable percentage");
                Assert.Equal("83%", remaining.Text);
                Assert.True(Find(popup, "DetailsReset").Text.Contains("2h"), "Reset countdown was lost");
                Assert.Equal("sample@example.test", Find(popup, "DetailsAccount").Text);
                Assert.True(Find(popup, "DetailsDisclosure").Text.Contains("unverified"), "Desktop account match was overstated");
            }
            finally { popup.Close(); }
        }
        [Case("Presentation")] public static void DetailsClearPreviousAccountAndMarkStaleValues()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var snapshot = new QuotaSnapshot { Source = new SourceIdentity("sample", "sample@example.test"), CheckedAt = now };
            snapshot.FiveHour.RemainingPercent = 83;
            var popup = new DetailsWindow(true);
            try
            {
                popup.Apply(QuotaPolicy.Project(snapshot, DisplayState.Current, now), new OverlaySettings(), now);
                popup.Apply(QuotaPolicy.Project(snapshot, DisplayState.Current, now.AddSeconds(150)), new OverlaySettings(), now.AddSeconds(150));
                var remaining = Find(popup, "DetailsRemaining");
                Assert.True(remaining != null, "Missing accessible percentage"); Assert.Equal("—", remaining.Text);
                Assert.True(Find(popup, "DetailsStatus").Text.Contains("Stale"), "Stale state is not visible");
                popup.Apply(QuotaPolicy.Project(snapshot, DisplayState.SourceUnknown, now), new OverlaySettings(), now);
                Assert.Equal("—", remaining.Text);
                Assert.True(!Find(popup, "DetailsAccount").Text.Contains("sample@example.test"), "Old account remained visible after source became unknown");
            }
            finally { popup.Close(); }
        }
    }
}
