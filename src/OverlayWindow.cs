using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Data;
using Microsoft.Win32;

namespace CodexUsageOverlay
{
    public sealed class Palette
    {
        public Brush Background, Text, Muted, Track, Five, Week, Surface, Outline, Hover, Accent;
        public bool Dark;
        static string previousKey;
        static Palette previous;
        public static Palette Resolve(OverlaySettings settings)
        {
            bool dark = settings.Theme == "Dark";
            if (settings.Theme == "System")
            {
                try { dark = Convert.ToInt32(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1)) == 0; } catch { }
            }
            string key = dark + "|" + settings.BackgroundColor + "|" + settings.TextColor + "|" + settings.FiveHourColor + "|" + settings.WeeklyColor + "|" + SystemParameters.HighContrast + "|" + SystemColors.WindowColor + "|" + SystemColors.WindowTextColor;
            if (key == previousKey) return previous;
            var palette = new Palette { Dark = dark,
                Background = Color(settings.BackgroundColor == "Auto" ? dark ? "#F02C2C2E" : "#EEF9FAFC" : settings.BackgroundColor),
                Text = Color(settings.TextColor == "Auto" ? dark ? "#F5F5F7" : "#242428" : settings.TextColor),
                Muted = Color(dark ? "#B9B9C1" : "#62646D"), Track = Color(dark ? "#30FFFFFF" : "#1C000000"),
                Surface = Color(dark ? "#303034" : "#FFFFFF"), Outline = Color(dark ? "#38FFFFFF" : "#22000000"),
                Hover = Color(dark ? "#3C3C42" : "#E9EBF0"), Accent = Color(dark ? "#286FD6" : "#0066CC"),
                Five = Color(settings.FiveHourColor), Week = Color(settings.WeeklyColor) };
            if (SystemParameters.HighContrast)
            {
                palette.Background = palette.Surface = SystemColors.WindowBrush; palette.Text = palette.Muted = SystemColors.WindowTextBrush;
                palette.Track = palette.Outline = SystemColors.WindowTextBrush; palette.Accent = SystemColors.HighlightBrush; palette.Hover = SystemColors.ControlBrush;
            }
            previousKey = key; previous = palette; return palette;
        }
        public static Brush Color(string value) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)); b.Freeze(); return b; }
    }
    public sealed class QuotaRing : FrameworkElement
    {
        public double? Remaining;
        public Brush Track, Stroke, Ink;
        public QuotaRing() { Width = 34; Height = 34; }
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc); double radius = 14.5; var center = new Point(17, 17);
            dc.DrawEllipse(null, new Pen(Track ?? Brushes.LightGray, 2.7), center, radius, radius);
            if (Remaining.HasValue && Remaining.Value > 0)
            {
                var pen = new Pen(Stroke ?? Brushes.SeaGreen, 2.7) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                if (Remaining.Value >= 100) dc.DrawEllipse(null, pen, center, radius, radius);
                else
                {
                    double radians = Remaining.Value / 100 * 2 * Math.PI - Math.PI / 2;
                    var geometry = new StreamGeometry();
                    using (var g = geometry.Open()) { g.BeginFigure(new Point(17, 17 - radius), false, false); g.ArcTo(new Point(17 + radius * Math.Cos(radians), 17 + radius * Math.Sin(radians)), new Size(radius, radius), 0, Remaining.Value > 50, SweepDirection.Clockwise, true, false); }
                    geometry.Freeze(); dc.DrawGeometry(null, pen, geometry);
                }
            }
            string label = Remaining.HasValue ? Math.Round(Remaining.Value, MidpointRounding.AwayFromZero).ToString(CultureInfo.CurrentCulture) + "%" : "—";
            var text = new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(WidgetAppearance.Font, FontStyles.Normal, FontWeights.Medium, FontStretches.Normal), 10.5, Ink ?? Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(text, new Point((34 - text.Width) / 2, (34 - text.Height) / 2));
        }
    }
    public sealed class OverlayWindow : Window
    {
        readonly Border border;
        readonly Image image;
        readonly TextBlock symbol, fiveLabel, weekLabel;
        readonly QuotaRing five, week;
        readonly string project;
        string imageKey;
        Palette appliedPalette;
        string fiveStroke, weekStroke;
        IntPtr handle;
        bool dragging;
        double grabX, grabY;
        public bool Repositioning;
        public event Action<bool> DetailsRequested;
        public event Action ImageSettingsRequested;
        public event Action SettingsRequested, RefreshRequested, RepositionRequested, PauseRequested, QuitRequested;
        public event Action<Point> Repositioned;
        public event Action RepositionFinished;
        public bool MenuOpen { get { return ContextMenu != null && ContextMenu.IsOpen; } }
        public OverlayWindow(string projectRoot)
        {
            project = projectRoot; Title = "Codex Usage Overlay"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = false; ShowActivated = false;
            Width = 44; Height = 152; UseLayoutRounding = true; SnapsToDevicePixels = true; FontFamily = WidgetAppearance.Font;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            var stack = new StackPanel { Width = 44, Height = 152, VerticalAlignment = VerticalAlignment.Center };
            image = new Image { Width = 44, Height = 30, Stretch = Stretch.Uniform, IsHitTestVisible = false };
            symbol = new TextBlock { Text = "✦", FontSize = 19, HorizontalAlignment = HorizontalAlignment.Center, Height = 28, Visibility = Visibility.Collapsed };
            var imageContent = new Grid(); imageContent.Children.Add(image); imageContent.Children.Add(symbol);
            var imageButton = SurfaceButton(imageContent, "ImageSettingsButton", "Change image and appearance");
            imageButton.Width = 44; imageButton.Height = 30; imageButton.Margin = new Thickness(0, 3, 0, -4);
            imageButton.Click += delegate { if (!Repositioning && ImageSettingsRequested != null) ImageSettingsRequested(); };
            stack.Children.Add(imageButton);
            five = new QuotaRing(); week = new QuotaRing();
            fiveLabel = AddQuota(stack, five, "5h", true); weekLabel = AddQuota(stack, week, "Week", false);
            border = new Border { CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(.75), Child = new Viewbox { Stretch = Stretch.Uniform, Child = stack } };
            Content = border;
            ContextMenu = new ContextMenu();
            AddMenu("Settings…", delegate { if (SettingsRequested != null) SettingsRequested(); });
            AddMenu("Refresh now", delegate { if (RefreshRequested != null) RefreshRequested(); });
            AddMenu("Reposition card", delegate { if (RepositionRequested != null) RepositionRequested(); });
            AddMenu("Pause / resume", delegate { if (PauseRequested != null) PauseRequested(); });
            ContextMenu.Items.Add(new Separator()); AddMenu("Quit", delegate { if (QuitRequested != null) QuitRequested(); });
            SourceInitialized += delegate
            {
                handle = new WindowInteropHelper(this).Handle;
                NativeMethods.SetWindowLongPtr(handle, -20, new IntPtr(NativeMethods.GetWindowLongPtr(handle, -20).ToInt64() | 0x80 | 0x08000000));
            };
            PreviewMouseLeftButtonDown += OnDown; PreviewMouseMove += OnMove; PreviewMouseLeftButtonUp += OnUp;
        }
        TextBlock AddQuota(StackPanel stack, QuotaRing ring, string name, bool primary)
        {
            var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 0), Background = Brushes.Transparent };
            panel.Children.Add(ring);
            var label = new TextBlock { Text = name, FontSize = 10.5, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 1, 0, 0) };
            panel.Children.Add(label);
            var button = SurfaceButton(panel, primary ? "FiveHourUsageButton" : "WeeklyUsageButton", primary ? "Five-hour usage details" : "Weekly usage details");
            button.Width = 44; stack.Children.Add(button);
            System.Windows.Automation.AutomationProperties.SetName(panel, primary ? "Five-hour usage" : "Weekly usage");
            button.Click += delegate { if (!Repositioning && DetailsRequested != null) DetailsRequested(primary); };
            return label;
        }
        static Button SurfaceButton(object content, string id, string label)
        {
            var button = new Button { Content = content, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), Cursor = Cursors.Hand };
            var borderFactory = new FrameworkElementFactory(typeof(Border)); borderFactory.Name = "Surface"; borderFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(9)); borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1)); borderFactory.SetValue(Border.BorderBrushProperty, Brushes.Transparent);
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetBinding(ContentPresenter.ContentProperty, new Binding("Content") { RelativeSource = RelativeSource.TemplatedParent });
            presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            borderFactory.AppendChild(presenter); var template = new ControlTemplate(typeof(Button)) { VisualTree = borderFactory };
            var hover = new Trigger { Property = Button.IsMouseOverProperty, Value = true }; hover.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension("Hover"), "Surface")); template.Triggers.Add(hover);
            // WPF's focus adorner follows keyboard navigation only. An IsKeyboardFocused
            // trigger also fires after mouse clicks and leaves a false selection outline.
            var focusBorder = new FrameworkElementFactory(typeof(Border));
            focusBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            focusBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            focusBorder.SetValue(FrameworkElement.MarginProperty, new Thickness(1));
            focusBorder.SetResourceReference(Border.BorderBrushProperty, SystemColors.HighlightBrushKey);
            var focusStyle = new Style(typeof(Control));
            focusStyle.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(Control)) { VisualTree = focusBorder }));
            button.FocusVisualStyle = focusStyle;
            button.Template = template; WidgetMotion.PressFeedback(button);
            System.Windows.Automation.AutomationProperties.SetAutomationId(button, id);
            System.Windows.Automation.AutomationProperties.SetName(button, label); return button;
        }
        void AddMenu(string title, Action action) { var item = new MenuItem { Header = title }; item.Click += delegate { action(); }; ContextMenu.Items.Add(item); }
        void OnDown(object sender, MouseButtonEventArgs e)
        {
            if (!Repositioning) return;
            NativeMethods.Point p; NativeMethods.GetCursorPos(out p); Rect r = NativeMethods.ClientBounds(handle);
            grabX = p.X - r.X; grabY = p.Y - r.Y; dragging = true; CaptureMouse(); e.Handled = true;
        }
        void OnMove(object sender, MouseEventArgs e)
        {
            Cursor = Repositioning ? Cursors.SizeAll : Cursors.Arrow;
            if (!dragging) return; NativeMethods.Point p; NativeMethods.GetCursorPos(out p);
            if (Repositioned != null) Repositioned(new Point(p.X - grabX, p.Y - grabY));
        }
        void OnUp(object sender, MouseButtonEventArgs e) { if (!dragging) return; dragging = false; ReleaseMouseCapture(); Repositioning = false; Cursor = Cursors.Arrow; e.Handled = true; if (RepositionFinished != null) RepositionFinished(); }
        public void Apply(QuotaView view, OverlaySettings settings, PlacementResult placement)
        {
            var colors = Palette.Resolve(settings); bool paletteChanged = colors != appliedPalette;
            if (paletteChanged)
            {
                appliedPalette = colors; border.Background = colors.Background; border.BorderBrush = colors.Outline;
                fiveLabel.Foreground = weekLabel.Foreground = symbol.Foreground = colors.Muted;
                Resources["Hover"] = colors.Hover; Resources["Accent"] = colors.Accent;
                five.Track = week.Track = colors.Track; five.Ink = week.Ink = colors.Text;
            }
            double? fiveRemaining = view != null && view.Current != null ? view.Current.FiveHour.RemainingPercent : null;
            double? weekRemaining = view != null && view.Current != null ? view.Current.Weekly.RemainingPercent : null;
            string fiveColour = RingColourPolicy.Choose(fiveRemaining, settings, true), weekColour = RingColourPolicy.Choose(weekRemaining, settings, false);
            bool fiveChanged = paletteChanged || five.Remaining != fiveRemaining || fiveStroke != fiveColour;
            bool weekChanged = paletteChanged || week.Remaining != weekRemaining || weekStroke != weekColour;
            if (fiveStroke != fiveColour) { fiveStroke = fiveColour; five.Stroke = Palette.Color(fiveColour); }
            if (weekStroke != weekColour) { weekStroke = weekColour; week.Stroke = Palette.Color(weekColour); }
            five.Remaining = fiveRemaining; week.Remaining = weekRemaining;
            if (fiveChanged) five.InvalidateVisual(); if (weekChanged) week.InvalidateVisual();
            System.Windows.Automation.AutomationProperties.SetName(five, "Five-hour remaining " + PresentationText.Percent(view, true));
            System.Windows.Automation.AutomationProperties.SetName(week, "Weekly remaining " + PresentationText.Percent(view, false));
            string path = settings.ImageMode == "Cat" ? Path.Combine(project, "assets", "sleeping-cat.png") : settings.ImageMode == "Custom" ? SettingsStore.ImageFullPath(project, settings.ImagePath) : "";
            string key = settings.ImageMode + path;
            if (key != imageKey)
            {
                imageKey = key; image.Source = null; image.Visibility = Visibility.Collapsed; symbol.Visibility = Visibility.Collapsed;
                if (settings.ImageMode == "Built-in") symbol.Visibility = Visibility.Visible;
                else if (path.Length > 0 && File.Exists(path))
                {
                    try { var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = 256; bitmap.UriSource = new Uri(path); bitmap.EndInit(); bitmap.Freeze(); image.Source = bitmap; image.Visibility = Visibility.Visible; } catch { symbol.Visibility = Visibility.Visible; }
                }
            }
            ToolTip = null;
            if (!placement.Show)
            {
                if (IsVisible) Hide();
                // Keep the above-window status scoped to an eligible foreground app.
                if (handle != IntPtr.Zero) NativeMethods.SetWindowPos(handle, new IntPtr(-2), 0, 0, 0, 0, 0x0010 | 0x0001 | 0x0002);
                return;
            }
            Width = settings.WidthDip; Height = settings.HeightDip;
            if (!IsVisible) Show();
            Rect b = placement.BoundsPixels;
            NativeMethods.SetWindowPos(handle, new IntPtr(-1), (int)Math.Round(b.X), (int)Math.Round(b.Y), (int)Math.Round(b.Width), (int)Math.Round(b.Height), 0x0010 | 0x0040);
        }
    }
}
