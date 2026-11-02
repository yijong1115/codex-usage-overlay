using System;
using System.Windows;

namespace CodexUsageOverlay
{
    public sealed class TargetState
    {
        public IntPtr Hwnd;
        public int ProcessId;
        public bool Verified, Visible, Minimized, Cloaked, Foreground, MainWindow, BlockedByDialog, DesktopTransition;
        public Rect ClientPixels;
        public double DpiScale = 1;
        public Rect? ProfilePixels;
    }
    public sealed class TargetStateEventArgs : EventArgs
    {
        public TargetState State;
        public TargetStateEventArgs(TargetState state) { State = state; }
    }
    public interface IWindowTracker : IDisposable
    {
        event EventHandler<TargetStateEventArgs> Changed;
        TargetState Current { get; }
        void Reconcile();
    }
    public sealed class PlacementResult
    {
        public bool Show;
        public Rect BoundsPixels;
        public string Status;
    }
    public static class PlacementPolicy
    {
        public static PlacementResult Calculate(TargetState target, OverlaySettings settings, bool ownInteraction)
        {
            var result = new PlacementResult { Status = "Waiting for an active Codex window" };
            if (target == null || !target.Verified || !target.MainWindow || !target.Visible || target.Minimized || target.Cloaked || target.BlockedByDialog || target.DesktopTransition || (!target.Foreground && !ownInteraction)) return result;
            double s = target.DpiScale;
            if (double.IsNaN(s) || double.IsInfinity(s) || s < 0.5 || s > 8) return result;
            double width = settings.WidthDip * s, height = settings.HeightDip * s;
            if (width <= 0 || height <= 0 || double.IsNaN(width) || double.IsNaN(height) || double.IsInfinity(width) || double.IsInfinity(height)) return result;
            var rect = new Rect(target.ClientPixels.Left + settings.OffsetXDip * s, target.ClientPixels.Bottom - settings.OffsetBottomDip * s - height, width, height);
            if (!target.ClientPixels.Contains(rect)) { result.Status = "Placement needs adjusting — card is outside the app"; return result; }
            if (target.ProfilePixels.HasValue)
            {
                Rect protectedArea = target.ProfilePixels.Value; protectedArea.Inflate(8 * s, 8 * s);
                if (rect.IntersectsWith(protectedArea)) { result.Status = "Placement needs adjusting — profile clearance"; return result; }
            }
            result.Show = true; result.BoundsPixels = rect; result.Status = "Window-relative placement"; return result;
        }
    }
}
