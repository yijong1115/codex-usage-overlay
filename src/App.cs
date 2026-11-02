using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace CodexUsageOverlay
{
    public static class Program
    {
        public static string ProjectRoot { get { return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..")); } }
        public static string InstanceKey(string root)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(root).ToUpperInvariant()))).Replace("-", "").Substring(0, 24);
        }
        static bool Signal(string name) { try { using (var e = EventWaitHandle.OpenExisting(name)) return e.Set(); } catch (WaitHandleCannotBeOpenedException) { return false; } }
        [STAThread]
        public static int Main(string[] args)
        {
            string root = ProjectRoot, key = InstanceKey(root), mode = args.Length == 0 ? "" : args[0];
            try
            {
                NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4));
                AppContext.SetSwitch("Switch.System.Windows.DoNotScaleForDpiChanges", false);
                if (mode == "--probe") { Console.WriteLine(new JavaScriptSerializer().Serialize(WindowTracker.Probe())); return 0; }
                if (mode == "--read-once") return ReadOnce();
                if (mode == "--startup-status" || mode == "--enable-startup" || mode == "--disable-startup" || mode == "--uninstall")
                {
                    StartupStatus status = mode == "--startup-status" ? StartupShortcut.Inspect(root) : StartupShortcut.SetEnabled(root, mode == "--enable-startup");
                    if (mode == "--uninstall")
                    {
                        Signal("Local\\CodexUsageOverlay.Quit." + key);
                        if (Array.IndexOf(args, "--remove-settings") >= 0)
                        {
                            using (var stopped = new Mutex(false, "Local\\CodexUsageOverlay.Instance." + key))
                            {
                                bool acquired; try { acquired = stopped.WaitOne(5000); } catch (AbandonedMutexException) { acquired = true; }
                                if (!acquired) throw new IOException("Close the overlay before removing preferences");
                                try { SettingsStore.ClearOwnedPreferences(root); } finally { stopped.ReleaseMutex(); }
                            }
                        }
                    }
                    Console.WriteLine(new JavaScriptSerializer().Serialize(status)); return 0;
                }
                if (mode == "--quit") { Signal("Local\\CodexUsageOverlay.Quit." + key); return 0; }
                if (mode == "--settings" && Signal("Local\\CodexUsageOverlay.Settings." + key)) return 0;
                bool created;
                using (var mutex = new Mutex(true, "Local\\CodexUsageOverlay.Instance." + key, out created))
                {
                    if (!created) return 0;
                    using (var quit = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\CodexUsageOverlay.Quit." + key))
                    using (var settings = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\CodexUsageOverlay.Settings." + key))
                    { var app = new OverlayApplication(root, quit, settings, mode == "--settings"); app.Run(); }
                    mutex.ReleaseMutex();
                }
                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine(new JavaScriptSerializer().Serialize(new { error = e.GetType().Name, status = "The overlay could not complete this operation" })); return 1;
            }
        }
        static int ReadOnce()
        {
            QuotaView result = null;
            using (var done = new ManualResetEventSlim())
            using (var reader = new UsageReader())
            {
                reader.Changed += delegate(object sender, QuotaViewEventArgs e)
                {
                    QuotaView latest = reader.CurrentView;
                    if (latest.Current != null || latest.State == DisplayState.SignInNeeded)
                    { result = latest; done.Set(); }
                };
                reader.SetActive(true); done.Wait(25000);
            }
            var snapshot = result == null ? null : result.Current;
            Console.WriteLine(new JavaScriptSerializer().Serialize(new { state = result == null ? "Timeout" : result.State.ToString(), fiveHourRemaining = snapshot == null ? null : snapshot.FiveHour.RemainingPercent, weeklyRemaining = snapshot == null ? null : snapshot.Weekly.RemainingPercent, fiveHourReset = snapshot == null || !snapshot.FiveHour.ResetsAt.HasValue ? (long?)null : snapshot.FiveHour.ResetsAt.Value.ToUnixSeconds(), weeklyReset = snapshot == null || !snapshot.Weekly.ResetsAt.HasValue ? (long?)null : snapshot.Weekly.ResetsAt.Value.ToUnixSeconds() }));
            return snapshot == null ? 1 : 0;
        }
    }
    public sealed class OverlayApplication : Application
    {
        readonly string project;
        readonly EventWaitHandle quitEvent, settingsEvent;
        readonly bool showSettings;
        OverlaySettings settings;
        OverlayWindow overlay;
        DetailsPopupController detailPopups;
        DetailsWindow details { get { return detailPopups == null ? null : detailPopups.Current; } }
        SettingsWindow preferences;
        WindowTracker tracker;
        UsageReader reader;
        UsageViewDispatcher usageDispatch;
        TrayController tray;
        DispatcherTimer timer;
        QuotaView view = new QuotaView { State = DisplayState.Loading, Status = "Checking usage…" };
        bool paused, shuttingDown, removePreferencesOnExit;
        DateTime nextStatus, nextStartupCheck;
        StartupKind startupKind;
        public OverlayApplication(string root, EventWaitHandle quit, EventWaitHandle openSettings, bool open)
        {
            project = root; quitEvent = quit; settingsEvent = openSettings; showSettings = open; ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e); settings = SettingsStore.Load(project);
            overlay = new OverlayWindow(project); MainWindow = overlay;
            detailPopups = new DetailsPopupController(CreateDetails);
            tray = new TrayController(); tracker = new WindowTracker(Dispatcher); reader = new UsageReader();
            usageDispatch = new UsageViewDispatcher(reader, action => { if (!shuttingDown && !Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(action); }, value => { if (!shuttingDown) { view = value; Render(); } });
            tracker.Changed += delegate { Render(); };
            overlay.DetailsRequested += ShowDetails;
            overlay.ImageSettingsRequested += delegate { OpenSettings("Image"); };
            overlay.SettingsRequested += OpenSettings; overlay.RefreshRequested += reader.RefreshNow; overlay.RepositionRequested += Reposition;
            overlay.PauseRequested += TogglePause; overlay.QuitRequested += Quit;
            overlay.Repositioned += delegate(Point point)
            {
                var target = tracker.Current; if (!target.Verified) return;
                var candidate = settings.Copy(); candidate.OffsetXDip = (point.X - target.ClientPixels.Left) / target.DpiScale;
                candidate.OffsetBottomDip = (target.ClientPixels.Bottom - point.Y) / target.DpiScale - candidate.HeightDip;
                if (SettingsStore.Validate(candidate) && PlacementPolicy.Calculate(target, candidate, OwnForeground()).Show) { settings = candidate; Render(); }
            };
            overlay.RepositionFinished += delegate { try { SettingsStore.Save(project, settings); } catch { tray.Notify("The position could not be saved. Check folder permissions."); } };
            tray.Details += delegate { ShowDetails(true); }; tray.Settings += OpenSettings; tray.Refresh += reader.RefreshNow;
            tray.Reposition += Reposition; tray.Restore += delegate { settings.OffsetXDip = 6; settings.OffsetBottomDip = 54; try { SettingsStore.Save(project, settings); } catch { tray.Notify("The position could not be saved."); } Render(); };
            tray.TogglePause += TogglePause; tray.Quit += Quit;
            tray.Uninstall += delegate
            {
                var answer = MessageBox.Show("Remove saved appearance and copied images too?\n\nYes: remove preferences. No: keep preferences.\nSource files and Codex are left untouched.", "Uninstall usage overlay", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.No);
                if (answer == MessageBoxResult.Cancel) return;
                try { StartupShortcut.SetEnabled(project, false); removePreferencesOnExit = answer == MessageBoxResult.Yes; Quit(); }
                catch { tray.Notify("Uninstall could not remove the owned startup entry. Nothing else was removed."); }
            };
            tray.ToggleStartup += delegate { try { StartupShortcut.SetEnabled(project, StartupShortcut.Inspect(project).Kind != StartupKind.Enabled); nextStartupCheck = DateTime.MinValue; Render(); } catch { tray.Notify("Startup was not changed. Check the existing shortcut or folder permissions."); } };
            DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs error) { error.Handled = true; if (tray != null) tray.Notify("The display encountered a problem. Try Refresh now or restart the overlay."); };
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            timer.Tick += delegate { if (quitEvent.WaitOne(0)) { Quit(); return; } if (settingsEvent.WaitOne(0)) OpenSettings(); Render(); }; timer.Start();
            tracker.Reconcile(); if (showSettings) OpenSettings();
        }
        bool OwnForeground()
        {
            uint pid; NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out pid);
            return pid == (uint)Process.GetCurrentProcess().Id && (preferences != null || details != null || overlay.MenuOpen || overlay.Repositioning);
        }
        void BringTarget()
        {
            if (tracker.Current.Verified)
            {
                if (NativeMethods.IsIconic(tracker.Current.Hwnd)) NativeMethods.ShowWindow(tracker.Current.Hwnd, 9);
                NativeMethods.SetForegroundWindow(tracker.Current.Hwnd); tracker.Reconcile();
            }
        }
        void Render()
        {
            if (shuttingDown || overlay == null) return;
            // Read foreground state at the point of display, including between tracker events.
            tracker.Current.Foreground = tracker.Current.Verified && NativeMethods.GetForegroundWindow() == tracker.Current.Hwnd;
            bool own = OwnForeground(); var placement = PlacementPolicy.Calculate(tracker.Current, settings, own);
            reader.SetActive(!paused && placement.Show);
            if (paused || view.State == DisplayState.SignInNeeded || view.State == DisplayState.SourceUnknown) placement.Show = false;
            overlay.Apply(view, settings, placement);
            if (details != null)
            {
                if (!placement.Show) detailPopups.Close(); else { details.Apply(view, settings, DateTimeOffset.UtcNow); PositionDetails(details, placement); }
            }
            if (DateTime.UtcNow >= nextStartupCheck)
            {
                try { startupKind = StartupShortcut.Inspect(project).Kind; } catch { startupKind = StartupKind.Broken; }
                nextStartupCheck = DateTime.UtcNow.AddSeconds(5);
            }
            string message = paused ? "Paused" : !placement.Show ? (view.State == DisplayState.SignInNeeded || view.State == DisplayState.SourceUnknown ? view.Status : placement.Status) : view.Status;
            tray.Update(message, paused, startupKind);
            if (DateTime.UtcNow >= nextStatus)
            {
                nextStatus = DateTime.UtcNow.AddSeconds(1);
                try
                {
                    string path = Path.Combine(project, "out", "status.json"), temp = path + ".tmp";
                    File.WriteAllText(temp, new JavaScriptSerializer().Serialize(new { running = true, overlayVisible = overlay.IsVisible, paused = paused, quotaState = view.State.ToString(), targetVerified = tracker.Current.Verified, foreground = tracker.Current.Foreground, minimized = tracker.Current.Minimized, cloaked = tracker.Current.Cloaked, scale = tracker.Current.DpiScale, placement = placement.Status, utc = DateTime.UtcNow.ToString("o") }));
                    if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
                }
                catch { }
            }
        }
        void TogglePause() { paused = !paused; Render(); }
        void Reposition()
        {
            tracker.Reconcile(); var target = tracker.Current;
            if (!target.Verified) { tray.Notify("Open the Codex main window first."); return; }
            if (NativeMethods.IsIconic(target.Hwnd)) NativeMethods.ShowWindow(target.Hwnd, 9);
            NativeMethods.SetForegroundWindow(target.Hwnd); paused = false; overlay.Repositioning = true; tracker.Reconcile(); Render();
            tray.Notify("Drag the usage card to a clear spot. Release to save the position.");
        }
        void OpenSettings() { OpenSettings("Appearance"); }
        void OpenSettings(string section)
        {
            if (preferences != null) { preferences.SelectSection(section); preferences.Activate(); return; }
            detailPopups.Close();
            BringTarget();
            preferences = new SettingsWindow(project, settings, section);
            preferences.PreviewRequested += delegate(OverlaySettings value) { settings = value.Copy(); Render(); };
            preferences.Saved += delegate(OverlaySettings value) { settings = value.Copy(); Render(); };
            preferences.RepositionRequested += Reposition;
            preferences.Closed += delegate { preferences = null; nextStartupCheck = DateTime.MinValue; Render(); };
            preferences.Show();
        }
        void ShowDetails(bool five)
        {
            if (overlay.Repositioning) return;
            BringTarget(); detailPopups.Toggle(five);
        }
        DetailsWindow CreateDetails(bool five)
        {
            var placement = PlacementPolicy.Calculate(tracker.Current, settings, OwnForeground());
            if (!placement.Show || !overlay.IsVisible) { tray.Notify("Usage details are not available yet. Try Refresh now."); return null; }
            var popup = new DetailsWindow(five);
            popup.Owner = overlay; popup.Apply(view, settings, DateTimeOffset.UtcNow);
            PositionDetails(popup, placement); popup.Show();
            double anchorY = placement.BoundsPixels.Top / tracker.Current.DpiScale + settings.HeightDip * (five ? .36 : .73);
            popup.AnimateFrom(new Point(popup.Left > placement.BoundsPixels.Left / tracker.Current.DpiScale ? 0 : 1, Math.Max(0, Math.Min(1, (anchorY - popup.Top) / Math.Max(1, popup.ActualHeight)))));
            return popup;
        }
        void PositionDetails(DetailsWindow popup, PlacementResult placement)
        {
            var work = System.Windows.Forms.Screen.FromHandle(tracker.Current.Hwnd).WorkingArea;
            double scale = tracker.Current.DpiScale;
            double left = work.Left / scale + 8, top = work.Top / scale + 8, right = work.Right / scale - 8, bottom = work.Bottom / scale - 8;
            popup.Width = Math.Max(1, Math.Min(310, right - left)); popup.MaxHeight = Math.Max(1, Math.Min(480, bottom - top));
            var content = (FrameworkElement)popup.Content; content.Measure(new Size(popup.Width, popup.MaxHeight));
            double height = Math.Min(popup.MaxHeight, content.DesiredSize.Height);
            double desiredLeft = placement.BoundsPixels.Right / scale + 8;
            if (desiredLeft + popup.Width > right) desiredLeft = placement.BoundsPixels.Left / scale - popup.Width - 8;
            popup.Left = Math.Max(left, Math.Min(right - popup.Width, desiredLeft));
            popup.Top = Math.Max(top, Math.Min(bottom - height, placement.BoundsPixels.Top / scale));
        }
        void Quit() { if (!shuttingDown) Shutdown(); }
        protected override void OnExit(ExitEventArgs e)
        {
            shuttingDown = true; if (timer != null) timer.Stop(); if (usageDispatch != null) usageDispatch.Dispose(); if (tracker != null) tracker.Dispose(); if (reader != null) reader.Dispose();
            if (detailPopups != null) detailPopups.Dispose(); if (preferences != null) preferences.Close(); if (overlay != null) overlay.Close(); if (tray != null) tray.Dispose();
            try { string path = Path.Combine(project, "out", "status.json"); if (File.Exists(path)) File.Delete(path); } catch { }
            if (removePreferencesOnExit) { try { SettingsStore.ClearOwnedPreferences(project); } catch { } }
            base.OnExit(e);
        }
    }
}
