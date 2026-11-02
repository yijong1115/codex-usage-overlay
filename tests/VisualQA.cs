using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CodexUsageOverlay.Tests
{
    public static class VisualQA
    {
        public static int RenderSettings(string section, string theme = "Light")
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
            var window = new SettingsWindow(root, new OverlaySettings { Theme = theme }, section);
            var surface = (FrameworkElement)window.Content;
            var panel = surface as System.Windows.Controls.Panel; if (panel != null) panel.Background = window.Background;
            surface.Measure(new Size(514, 591)); surface.Arrange(new Rect(0, 0, 514, 591)); surface.UpdateLayout();
            var bitmap = new RenderTargetBitmap(643, 739, 120, 120, PixelFormats.Pbgra32); bitmap.Render(surface);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            string file = Path.Combine(root, "out", "settings-" + section + (theme == "Light" ? "" : "-" + theme) + ".png"); using (var stream = File.Create(file)) encoder.Save(stream);
            window.Close(); Console.WriteLine(file); return 0;
        }
        public static int RenderCard(string theme = "Light")
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
            var snapshot = new QuotaSnapshot { Source = new SourceIdentity("sample", "Preview sample"), CheckedAt = DateTimeOffset.UtcNow };
            snapshot.FiveHour.RemainingPercent = 83; snapshot.Weekly.RemainingPercent = 40;
            var view = QuotaPolicy.Project(snapshot, DisplayState.Current, snapshot.CheckedAt);
            var window = new OverlayWindow(root); window.Apply(view, new OverlaySettings { Theme = theme }, new PlacementResult());
            var surface = (FrameworkElement)window.Content; surface.Measure(new Size(44, 152)); surface.Arrange(new Rect(0, 0, 44, 152)); surface.UpdateLayout();
            var bitmap = new RenderTargetBitmap(176, 608, 384, 384, PixelFormats.Pbgra32); bitmap.Render(surface);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            string file = Path.Combine(root, "out", "native-card-preview" + (theme == "Light" ? "" : "-" + theme) + ".png"); using (var stream = File.Create(file)) encoder.Save(stream);
            window.Close(); Console.WriteLine(file); return 0;
        }
        public static int RenderDetails(string theme = "Light")
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
            DateTimeOffset now = DateTimeOffset.Now;
            var snapshot = new QuotaSnapshot { Source = new SourceIdentity("sample", "you@example.com"), CheckedAt = now.AddSeconds(-28) };
            snapshot.FiveHour.RemainingPercent = 83; snapshot.FiveHour.ResetsAt = now.AddMinutes(144);
            var window = new DetailsWindow(true); window.Apply(QuotaPolicy.Project(snapshot, DisplayState.Current, now), new OverlaySettings { Theme = theme }, now);
            var surface = (FrameworkElement)window.Content; surface.Measure(new Size(310, 480));
            double height = Math.Ceiling(surface.DesiredSize.Height); surface.Arrange(new Rect(0, 0, 310, height)); surface.UpdateLayout();
            var bitmap = new RenderTargetBitmap(465, (int)Math.Ceiling(height * 1.5), 144, 144, PixelFormats.Pbgra32); bitmap.Render(surface);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            string file = Path.Combine(root, "out", "details-" + theme + ".png"); using (var stream = File.Create(file)) encoder.Save(stream);
            window.Close(); Console.WriteLine(file); return 0;
        }
    }
}
