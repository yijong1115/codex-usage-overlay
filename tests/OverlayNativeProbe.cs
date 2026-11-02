using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;

namespace CodexUsageOverlay.Tests
{
    public static class OverlayNativeProbe
    {
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out NativeMethods.NativeRect rect);
        [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(NativeMethods.Point point);
        public static int TestForegroundHide()
        {
            NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4));
            bool activated = false, found = false, hidden = false, normalOrder = false;
            using (var form = new System.Windows.Forms.Form())
            using (var timer = new System.Windows.Forms.Timer { Interval = 1000 })
            {
                form.Text = "Checking usage card visibility"; form.Width = 400; form.Height = 150;
                form.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
                form.Controls.Add(new System.Windows.Forms.Label { Dock = System.Windows.Forms.DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleCenter, Text = "Checking that the usage card hides.\nThis window closes automatically." });
                form.Shown += delegate { NativeMethods.SetForegroundWindow(form.Handle); timer.Start(); };
                timer.Tick += delegate
                {
                    timer.Stop(); activated = NativeMethods.GetForegroundWindow() == form.Handle;
                    string executable = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CodexUsageOverlay.exe"));
                    NativeMethods.EnumWindows(delegate(IntPtr hwnd, IntPtr unused)
                    {
                        if (NativeMethods.Text(hwnd) != "Codex Usage Overlay") return true;
                        uint pid; NativeMethods.GetWindowThreadProcessId(hwnd, out pid); IntPtr process = NativeMethods.OpenProcess(0x1000, false, pid);
                        if (process == IntPtr.Zero) return true;
                        try
                        {
                            uint size = 32768; var path = new StringBuilder((int)size);
                            if (!NativeMethods.QueryFullProcessImageName(process, 0, path, ref size) || !string.Equals(path.ToString(), executable, StringComparison.OrdinalIgnoreCase)) return true;
                            found = true; hidden = !NativeMethods.IsWindowVisible(hwnd); normalOrder = (NativeMethods.GetWindowLongPtr(hwnd, -20).ToInt64() & 8) == 0;
                        }
                        finally { NativeMethods.CloseHandle(process); }
                        return true;
                    }, IntPtr.Zero);
                    form.Close();
                };
                System.Windows.Forms.Application.Run(form);
            }
            Console.WriteLine(new JavaScriptSerializer().Serialize(new { activated = activated, cardFound = found, hidden = hidden, topmostCleared = normalOrder }));
            Console.WriteLine(activated && found && hidden && normalOrder ? "PASS ForeignForegroundHidesAndClearsTopmost" : "FAIL ForeignForegroundHidesAndClearsTopmost");
            return activated && found && hidden && normalOrder ? 0 : 1;
        }
        public static int Run()
        {
            NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4));
            string executable = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CodexUsageOverlay.exe"));
            var found = new List<object>(); int z = 0; bool overlayFound = false, overlayHit = false;
            NativeMethods.EnumWindows(delegate(IntPtr hwnd, IntPtr unused)
            {
                z++; uint pid; NativeMethods.GetWindowThreadProcessId(hwnd, out pid);
                IntPtr process = NativeMethods.OpenProcess(0x1000, false, pid); if (process == IntPtr.Zero) return true;
                bool own = false;
                try { uint size = 32768; var path = new StringBuilder((int)size); own = NativeMethods.QueryFullProcessImageName(process, 0, path, ref size) && string.Equals(path.ToString(), executable, StringComparison.OrdinalIgnoreCase); }
                finally { NativeMethods.CloseHandle(process); }
                int targetPid; bool target = NativeMethods.IsCodexGui(hwnd, out targetPid) && NativeMethods.IsWindowVisible(hwnd) && NativeMethods.Text(hwnd) != "";
                if (!own && !target) return true;
                NativeMethods.NativeRect rect; GetWindowRect(hwnd, out rect);
                var point = new NativeMethods.Point { X = rect.Left + (rect.Right - rect.Left) / 2, Y = rect.Top + (rect.Bottom - rect.Top) / 2 };
                IntPtr hit = WindowFromPoint(point); uint hitPid; NativeMethods.GetWindowThreadProcessId(hit, out hitPid);
                if (own && NativeMethods.Text(hwnd) == "Codex Usage Overlay" && NativeMethods.IsWindowVisible(hwnd)) { overlayFound = true; overlayHit = hitPid == pid; }
                found.Add(new { hwnd = hwnd.ToInt64(), pid = pid, kind = own ? "overlay" : "codex", caption = NativeMethods.Text(hwnd), z = z, visible = NativeMethods.IsWindowVisible(hwnd), cloaked = NativeMethods.Cloaked(hwnd), exStyle = NativeMethods.GetWindowLongPtr(hwnd, -20).ToInt64().ToString("X"), left = rect.Left, top = rect.Top, width = rect.Right - rect.Left, height = rect.Bottom - rect.Top, centerHitOwn = hitPid == pid, centerHitClass = NativeMethods.Class(hit), foreground = NativeMethods.GetForegroundWindow() == hwnd });
                return true;
            }, IntPtr.Zero);
            Console.WriteLine(new JavaScriptSerializer().Serialize(found));
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--assert-visible") >= 0) { Console.WriteLine(overlayFound && overlayHit ? "PASS NativeCardIsVisibleAboveCodex" : "FAIL NativeCardIsVisibleAboveCodex"); return overlayFound && overlayHit ? 0 : 1; }
            return 0;
        }
    }
}
