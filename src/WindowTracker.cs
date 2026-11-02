using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace CodexUsageOverlay
{
    public sealed class WindowTracker : IWindowTracker
    {
        readonly Dispatcher dispatcher;
        readonly DispatcherTimer timer;
        readonly NativeMethods.WinEventProc callback;
        readonly List<IntPtr> hooks = new List<IntPtr>();
        bool queued, disposed;
        public event EventHandler<TargetStateEventArgs> Changed;
        public TargetState Current { get; private set; }
        public WindowTracker(Dispatcher ui)
        {
            dispatcher = ui; Current = new TargetState(); callback = OnEvent;
            hooks.Add(NativeMethods.SetWinEventHook(3, 3, IntPtr.Zero, callback, 0, 0, 2));
            hooks.Add(NativeMethods.SetWinEventHook(0x16, 0x17, IntPtr.Zero, callback, 0, 0, 2));
            hooks.Add(NativeMethods.SetWinEventHook(0x8000, 0x800B, IntPtr.Zero, callback, 0, 0, 2));
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; timer.Tick += delegate { Reconcile(); }; timer.Start();
        }
        void OnEvent(IntPtr hook, uint kind, IntPtr hwnd, int objectId, int childId, uint thread, uint time)
        {
            if (disposed || queued || (kind >= 0x8000 && (objectId != 0 || childId != 0 || hwnd != Current.Hwnd))) return;
            queued = true; dispatcher.BeginInvoke(new Action(delegate { queued = false; if (!disposed) Reconcile(); }));
        }
        static bool Eligible(IntPtr hwnd, out int pid)
        {
            pid = 0;
            if (!NativeMethods.IsWindow(hwnd) || NativeMethods.GetWindow(hwnd, 4) != IntPtr.Zero || (NativeMethods.GetWindowLongPtr(hwnd, -20).ToInt64() & 0x80) != 0) return false;
            if (!NativeMethods.IsCodexGui(hwnd, out pid)) return false;
            if (NativeMethods.Class(hwnd) != "Chrome_WidgetWin_1") return false;
            string caption = NativeMethods.Text(hwnd);
            if (caption != "ChatGPT" && caption != "Codex") return false;
            Rect bounds = NativeMethods.ClientBounds(hwnd); double scale = NativeMethods.Scale(hwnd);
            return !bounds.IsEmpty && bounds.Width >= 480 * scale && bounds.Height >= 360 * scale;
        }
        public void Reconcile()
        {
            if (disposed) return;
            IntPtr foreground = NativeMethods.GetForegroundWindow(); int pid;
            IntPtr chosen = IntPtr.Zero;
            if (Eligible(foreground, out pid)) chosen = foreground;
            else if (Eligible(Current.Hwnd, out pid)) chosen = Current.Hwnd;
            else
            {
                var candidates = new List<IntPtr>();
                NativeMethods.EnumWindows(delegate(IntPtr hwnd, IntPtr unused) { int processId; if (NativeMethods.IsWindowVisible(hwnd) && Eligible(hwnd, out processId)) candidates.Add(hwnd); return true; }, IntPtr.Zero);
                if (candidates.Count == 1) chosen = candidates[0];
            }
            var state = new TargetState();
            if (chosen != IntPtr.Zero && Eligible(chosen, out pid))
            {
                state.Hwnd = chosen; state.ProcessId = pid; state.Verified = true; state.MainWindow = true;
                state.Visible = NativeMethods.IsWindowVisible(chosen); state.Minimized = NativeMethods.IsIconic(chosen);
                state.Cloaked = NativeMethods.Cloaked(chosen); state.Foreground = foreground == chosen;
                state.BlockedByDialog = !NativeMethods.IsWindowEnabled(chosen);
                state.ClientPixels = NativeMethods.ClientBounds(chosen); state.DpiScale = NativeMethods.Scale(chosen);
            }
            Current = state; var handler = Changed; if (handler != null) handler(this, new TargetStateEventArgs(state));
        }
        public void Dispose() { if (disposed) return; disposed = true; timer.Stop(); foreach (var hook in hooks) if (hook != IntPtr.Zero) NativeMethods.UnhookWinEvent(hook); }
        public static object Probe()
        {
            var windows = new List<object>();
            NativeMethods.EnumWindows(delegate(IntPtr hwnd, IntPtr unused)
            {
                int pid; if (!NativeMethods.IsCodexGui(hwnd, out pid)) return true;
                Rect r = NativeMethods.ClientBounds(hwnd); int eligiblePid;
                windows.Add(new { hwnd = hwnd.ToInt64(), processId = pid, caption = NativeMethods.Text(hwnd), windowClass = NativeMethods.Class(hwnd), eligible = Eligible(hwnd, out eligiblePid), visible = NativeMethods.IsWindowVisible(hwnd), minimized = NativeMethods.IsIconic(hwnd), cloaked = NativeMethods.Cloaked(hwnd), scale = NativeMethods.Scale(hwnd), client = r.IsEmpty ? "empty" : r.ToString(System.Globalization.CultureInfo.InvariantCulture) });
                return true;
            }, IntPtr.Zero);
            return windows;
        }
    }
}
