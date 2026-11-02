using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace CodexUsageOverlay.Tests
{
    public static class SettingsAppearanceTests
    {
        static IEnumerable<DependencyObject> All(DependencyObject root)
        {
            yield return root;
            foreach (object child in LogicalTreeHelper.GetChildren(root))
            {
                var item = child as DependencyObject; if (item == null) continue;
                foreach (var descendant in All(item)) yield return descendant;
            }
        }
        static double Linear(byte value) { double n = value / 255.0; return n <= .04045 ? n / 12.92 : Math.Pow((n + .055) / 1.055, 2.4); }
        static double Light(Color c) { return .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B); }
        static double Contrast(Brush foreground, Brush background)
        {
            double a = Light(((SolidColorBrush)foreground).Color), b = Light(((SolidColorBrush)background).Color);
            return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
        }
        [Case("Interaction")] public static void ValidationErrorsStayReadableWhenSettingsThemeChanges()
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
            var window = new SettingsWindow(root, new OverlaySettings { Theme = "Dark", SizePreset = "Custom" });
            try
            {
                All(window).OfType<TextBox>().First(t => AutomationProperties.GetName(t) == "Width").Text = "-1";
                All(window).OfType<Button>().First(b => object.Equals(b.Content, "Preview on card")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var error = All(window).OfType<TextBlock>().First(t => t.Text.Contains("check your size"));
                Assert.True(Contrast(error.Foreground, window.Background) >= 4.5, "Dark-mode validation error is too faint to read");
                All(window).OfType<ComboBox>().First(c => AutomationProperties.GetName(c) == "Card theme").SelectedItem = "Light";
                Assert.True(Contrast(error.Foreground, window.Background) >= 4.5, "Error colour did not follow the settings theme");
            }
            finally { window.Close(); }
        }
    }
}
