using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace CodexUsageOverlay
{
    public sealed class DetailsWindow : Window
    {
        readonly TextBlock title, remaining, status, reset, checkedAt, account, disclosure, accountHint;
        readonly Border border, resetCard;
        readonly StackPanel accountSection;
        Palette appliedPalette;
        public readonly bool FiveHour;
        public DetailsWindow(bool fiveHour)
        {
            FiveHour = fiveHour; Title = "Codex usage details";
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; ShowActivated = true;
            SizeToContent = SizeToContent.Height; Width = 310; MaxHeight = 480;
            FontFamily = WidgetAppearance.Font; FontSize = 12; UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            var body = new StackPanel();
            title = Label(fiveHour ? "Five-hour allowance" : "Weekly allowance", 12, FontWeights.SemiBold); body.Children.Add(title);
            var usage = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 2) };
            remaining = Label("—", 34, FontWeights.SemiBold, "DetailsRemaining");
            Typography.SetNumeralAlignment(remaining, FontNumeralAlignment.Tabular); usage.Children.Add(remaining);
            var suffix = Label("remaining", 12, FontWeights.Normal); suffix.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); suffix.VerticalAlignment = VerticalAlignment.Bottom; suffix.Margin = new Thickness(8, 0, 0, 8); usage.Children.Add(suffix); body.Children.Add(usage);
            status = Label("", 12, FontWeights.Medium, "DetailsStatus"); status.Margin = new Thickness(0, 0, 0, 14); body.Children.Add(status);
            reset = Label("", 13, FontWeights.Normal, "DetailsReset");
            resetCard = new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 10, 12, 10), Child = reset }; body.Children.Add(resetCard);
            checkedAt = Label("", 11, FontWeights.Normal); checkedAt.Margin = new Thickness(0, 9, 0, 15); checkedAt.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); body.Children.Add(checkedAt);
            accountSection = new StackPanel();
            var sourceTitle = Label("Local Codex account", 11, FontWeights.SemiBold); sourceTitle.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); accountSection.Children.Add(sourceTitle);
            account = Label("", 12, FontWeights.Medium, "DetailsAccount"); account.Margin = new Thickness(0, 3, 0, 7); accountSection.Children.Add(account);
            disclosure = Label("Desktop account match: unverified", 11, FontWeights.Normal, "DetailsDisclosure"); disclosure.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); accountSection.Children.Add(disclosure);
            accountHint = Label("Compare with the account selected in Codex.", 11, FontWeights.Normal); accountHint.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); accountSection.Children.Add(accountHint);
            body.Children.Add(accountSection);
            var footer = Label("Click the same ring to close · Esc", 11, FontWeights.Normal); footer.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); footer.Margin = new Thickness(0, 16, 0, 0); body.Children.Add(footer);
            border = new Border { BorderThickness = new Thickness(.75), CornerRadius = new CornerRadius(17), Padding = new Thickness(18), Child = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled } };
            Content = border; Background = Brushes.Transparent; AllowsTransparency = true;
            KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) Close(); };
            SourceInitialized += delegate
            {
                IntPtr h = new WindowInteropHelper(this).Handle;
                long style = NativeMethods.GetWindowLongPtr(h, -20).ToInt64() | 0x80;
                NativeMethods.SetWindowLongPtr(h, -20, new IntPtr(style));
            };
        }
        static TextBlock Label(string value, double size, FontWeight weight, string id = null)
        {
            var label = new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap, FontSize = size, FontWeight = weight, LineStackingStrategy = LineStackingStrategy.BlockLineHeight };
            label.SetResourceReference(TextBlock.ForegroundProperty, "Ink");
            if (id != null) System.Windows.Automation.AutomationProperties.SetAutomationId(label, id); return label;
        }
        public void AnimateFrom(Point origin) { WidgetMotion.Enter(border, origin); }
        public void Apply(QuotaView view, OverlaySettings settings, DateTimeOffset now)
        {
            var colors = Palette.Resolve(settings);
            if (colors != appliedPalette)
            {
                appliedPalette = colors; Resources["Ink"] = colors.Text; Resources["Muted"] = colors.Muted;
                border.Background = colors.Background; border.BorderBrush = colors.Outline; resetCard.Background = colors.Hover;
            }
            remaining.Text = PresentationText.Percent(view, FiveHour);
            status.Text = view == null ? "Checking usage…" : view.Status ?? QuotaPolicy.Label(view.State);
            var snapshot = view == null ? null : view.Current ?? view.LastKnown;
            if (snapshot == null)
            {
                reset.Text = "Reset time unavailable"; checkedAt.Text = "Waiting for a successful usage check";
                account.Text = "Local Codex account is unverified."; disclosure.Text = "Desktop account match: unverified"; accountHint.Visibility = Visibility.Collapsed;
            }
            else
            {
                var window = FiveHour ? snapshot.FiveHour : snapshot.Weekly;
                if (window.ResetsAt.HasValue)
                {
                    TimeSpan wait = window.ResetsAt.Value - now;
                    reset.Text = "Resets " + window.ResetsAt.Value.ToLocalTime().ToString("ddd d MMM, h:mm tt") + " (local)\n" +
                        (wait.TotalSeconds > 0 ? "In " + (int)wait.TotalHours + "h " + wait.Minutes + "m" : "Awaiting confirmation of reset");
                }
                else reset.Text = "Reset time unavailable";
                checkedAt.Text = "Checked " + snapshot.CheckedAt.ToLocalTime().ToString("h:mm:ss tt") + " · " + Math.Max(0, (int)(now - snapshot.CheckedAt).TotalSeconds) + "s ago";
                if (view.Current == null && window.RemainingPercent.HasValue) status.Text += " · Last known " + Math.Round(window.RemainingPercent.Value) + "%";
                account.Text = snapshot.Source.LocalLabel; disclosure.Text = "Desktop account match: unverified"; accountHint.Visibility = Visibility.Visible;
            }
            System.Windows.Automation.AutomationProperties.SetName(border, PresentationText.Details(view, FiveHour, now));
        }
    }
}
