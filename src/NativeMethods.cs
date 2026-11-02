using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace CodexUsageOverlay
{
    internal static class NativeMethods
    {
        internal delegate bool EnumWindowProc(IntPtr hwnd, IntPtr parameter);
        internal delegate void WinEventProc(IntPtr hook, uint kind, IntPtr hwnd, int objectId, int childId, uint thread, uint time);
        [StructLayout(LayoutKind.Sequential)] internal struct NativeRect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
        [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumWindowProc callback, IntPtr parameter);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool IsWindowEnabled(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool GetClientRect(IntPtr hwnd, out NativeRect rect);
        [DllImport("user32.dll")] internal static extern bool ClientToScreen(IntPtr hwnd, ref Point point);
        [DllImport("user32.dll")] internal static extern IntPtr GetWindow(IntPtr hwnd, uint command);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int size);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassName(IntPtr hwnd, StringBuilder text, int size);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] internal static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] internal static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr hwnd, int command);
        [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool SetProcessDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] internal static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventProc callback, uint processId, uint threadId, uint flags);
        [DllImport("user32.dll")] internal static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
        [DllImport("kernel32.dll")] internal static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);
        [DllImport("kernel32.dll")] internal static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern int GetPackageFamilyName(IntPtr process, ref uint length, StringBuilder name);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder path, ref uint size);
        internal static string Text(IntPtr hwnd) { var b = new StringBuilder(512); GetWindowText(hwnd, b, b.Capacity); return b.ToString(); }
        internal static string Class(IntPtr hwnd) { var b = new StringBuilder(256); GetClassName(hwnd, b, b.Capacity); return b.ToString(); }
        internal static Rect ClientBounds(IntPtr hwnd)
        {
            NativeRect r; var p = new Point();
            if (!GetClientRect(hwnd, out r) || !ClientToScreen(hwnd, ref p) || r.Right <= 0 || r.Bottom <= 0) return Rect.Empty;
            return new Rect(p.X, p.Y, r.Right, r.Bottom);
        }
        internal static bool IsCodexGui(IntPtr hwnd, out int pid)
        {
            uint raw; GetWindowThreadProcessId(hwnd, out raw); pid = (int)raw;
            IntPtr handle = OpenProcess(0x1000, false, raw); if (handle == IntPtr.Zero) return false;
            try
            {
                uint length = 0;
                if (GetPackageFamilyName(handle, ref length, null) != 122 || length > 512) return false;
                var family = new StringBuilder((int)length);
                if (GetPackageFamilyName(handle, ref length, family) != 0 || family.ToString() != "OpenAI.Codex_2p2nqsd0c76g0") return false;
                uint size = 32768; var path = new StringBuilder((int)size);
                return QueryFullProcessImageName(handle, 0, path, ref size) && path.ToString().EndsWith("\\app\\ChatGPT.exe", StringComparison.OrdinalIgnoreCase);
            }
            finally { CloseHandle(handle); }
        }
        internal static double Scale(IntPtr hwnd) { try { uint n = GetDpiForWindow(hwnd); return n == 0 ? 1 : n / 96.0; } catch (EntryPointNotFoundException) { return 1; } }
        internal static bool Cloaked(IntPtr hwnd) { int value; return DwmGetWindowAttribute(hwnd, 14, out value, 4) == 0 && value != 0; }
    }
}
