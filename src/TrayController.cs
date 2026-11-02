using System;
using System.Drawing;
using Forms = System.Windows.Forms;

namespace CodexUsageOverlay
{
    public sealed class TrayController : IDisposable
    {
        readonly Forms.NotifyIcon icon;
        readonly Forms.ToolStripMenuItem status, pause, startup;
        public event Action Details, Settings, Refresh, Reposition, Restore, TogglePause, ToggleStartup, Uninstall, Quit;
        public TrayController()
        {
            var menu = new Forms.ContextMenuStrip(); status = new Forms.ToolStripMenuItem("Checking usage") { Enabled = false }; menu.Items.Add(status); menu.Items.Add(new Forms.ToolStripSeparator());
            Add(menu, "Usage details", delegate { if (Details != null) Details(); });
            Add(menu, "Settings…", delegate { if (Settings != null) Settings(); });
            Add(menu, "Refresh now", delegate { if (Refresh != null) Refresh(); });
            Add(menu, "Move card…", delegate { if (Reposition != null) Reposition(); });
            Add(menu, "Restore default position", delegate { if (Restore != null) Restore(); });
            pause = Add(menu, "Pause display", delegate { if (TogglePause != null) TogglePause(); });
            startup = Add(menu, "Start at Windows sign-in", delegate { if (ToggleStartup != null) ToggleStartup(); });
            menu.Items.Add(new Forms.ToolStripSeparator()); Add(menu, "Uninstall…", delegate { if (Uninstall != null) Uninstall(); }); Add(menu, "Quit", delegate { if (Quit != null) Quit(); });
            icon = new Forms.NotifyIcon { Icon = SystemIcons.Information, Text = "Codex Usage Overlay", ContextMenuStrip = menu, Visible = true };
            icon.MouseClick += delegate(object sender, Forms.MouseEventArgs e) { if (e.Button == Forms.MouseButtons.Left && Details != null) Details(); };
        }
        static Forms.ToolStripMenuItem Add(Forms.ContextMenuStrip menu, string text, Action action) { var item = new Forms.ToolStripMenuItem(text); item.Click += delegate { action(); }; menu.Items.Add(item); return item; }
        public void Update(string message, bool paused, StartupKind startupKind)
        {
            status.Text = message; pause.Text = paused ? "Resume display" : "Pause display"; startup.Checked = startupKind == StartupKind.Enabled;
            startup.Text = "Start at Windows sign-in (" + startupKind + ")";
            string tooltip = "Codex Usage Overlay — " + message; icon.Text = tooltip.Length > 63 ? tooltip.Substring(0, 63) : tooltip;
        }
        public void Notify(string text) { icon.ShowBalloonTip(4000, "Codex Usage Overlay", text, Forms.ToolTipIcon.Info); }
        public void Dispose() { icon.Visible = false; icon.ContextMenuStrip.Dispose(); icon.Dispose(); }
    }
}
