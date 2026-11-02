using System;
using System.Windows;

namespace CodexUsageOverlay.Tests
{
    public static class PlacementTests
    {
        static TargetState Target(double scale)
        {
            return new TargetState { Hwnd = new IntPtr(1), Verified = true, MainWindow = true, Foreground = true, Visible = true, DpiScale = scale, ClientPixels = new Rect(-1000, 100, 800 * scale, 600 * scale) };
        }
        [Case("Placement")] public static void NegativeOriginPreservesAnchor()
        {
            var p = PlacementPolicy.Calculate(Target(1), new OverlaySettings(), false);
            Assert.True(p.Show, p.Status); Assert.Equal(-994.0, p.BoundsPixels.X); Assert.Equal(494.0, p.BoundsPixels.Y);
        }
        [Case("Placement")] public static void DpiRoundTrip125And200()
        {
            foreach (double scale in new[] { 1.25, 1.5, 2.0 })
            {
                var p = PlacementPolicy.Calculate(Target(scale), new OverlaySettings(), false);
                Assert.Equal(44 * scale, p.BoundsPixels.Width); Assert.Equal(152 * scale, p.BoundsPixels.Height);
                Assert.Equal(-1000 + 6 * scale, p.BoundsPixels.Left); Assert.Equal(100 + (600 - 54 - 152) * scale, p.BoundsPixels.Top);
            }
        }
        [Case("Placement")] public static void ResizeKeepsBottomGap()
        {
            var t = Target(1); var a = PlacementPolicy.Calculate(t, new OverlaySettings(), false); t.ClientPixels = new Rect(-1000, 100, 1200, 900);
            var b = PlacementPolicy.Calculate(t, new OverlaySettings(), false); Assert.Equal(300.0, b.BoundsPixels.Y - a.BoundsPixels.Y);
        }
        [Case("Placement")] public static void OversizedCardHides()
        {
            var t = Target(1); t.ClientPixels = new Rect(0, 0, 40, 100); Assert.True(!PlacementPolicy.Calculate(t, new OverlaySettings(), false).Show, "Card leaves client area");
        }
        [Case("Placement")] public static void ProfileCollisionHides()
        {
            var t = Target(1); t.ProfilePixels = new Rect(-994, 500, 28, 28); Assert.True(!PlacementPolicy.Calculate(t, new OverlaySettings(), false).Show, "Profile covered");
        }
        [Case("Placement")] public static void ForeignForegroundHides()
        {
            var t = Target(1); t.Foreground = false; Assert.True(!PlacementPolicy.Calculate(t, new OverlaySettings(), false).Show, "Card remains over another app");
        }
        [Case("Placement")] public static void OwnedPopupKeepsEligibleTarget()
        {
            var t = Target(1); t.Foreground = false; Assert.True(PlacementPolicy.Calculate(t, new OverlaySettings(), true).Show, "Own popup hides eligible target");
            t.Minimized = true; Assert.True(!PlacementPolicy.Calculate(t, new OverlaySettings(), true).Show, "Own popup overrides minimization");
        }
        [Case("Placement")] public static void MinimizedCloakedOrUnknownHides()
        {
            var t = Target(1); t.Cloaked = true; Assert.True(!PlacementPolicy.Calculate(t, new OverlaySettings(), false).Show, "Cloaked window shown");
            t.Cloaked = false; t.Verified = false; Assert.True(!PlacementPolicy.Calculate(t, new OverlaySettings(), false).Show, "Unverified window shown");
            t.Verified = true; t.BlockedByDialog = true; Assert.True(!PlacementPolicy.Calculate(t, new OverlaySettings(), true).Show, "Dialog ignored");
        }
    }
}
