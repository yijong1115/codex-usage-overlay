using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Media.Imaging;

namespace CodexUsageOverlay
{
    public sealed class OverlaySettings
    {
        public string Theme { get; set; }
        public string FiveHourColor { get; set; }
        public string WeeklyColor { get; set; }
        public bool AutomaticRingColours { get; set; }
        public string HighRemainingColor { get; set; }
        public string MediumRemainingColor { get; set; }
        public string LowRemainingColor { get; set; }
        public string BackgroundColor { get; set; }
        public string TextColor { get; set; }
        public string SizePreset { get; set; }
        public double WidthDip { get; set; }
        public double HeightDip { get; set; }
        public double OffsetXDip { get; set; }
        public double OffsetBottomDip { get; set; }
        public string ImageMode { get; set; }
        public string ImagePath { get; set; }
        public OverlaySettings()
        {
            Theme = "System"; FiveHourColor = "#15875E"; WeeklyColor = "#376CC5";
            AutomaticRingColours = true; HighRemainingColor = "#15875E"; MediumRemainingColor = "#D1A000"; LowRemainingColor = "#D94B47";
            BackgroundColor = "Auto"; TextColor = "Auto"; SizePreset = "Slim";
            WidthDip = 44; HeightDip = 152; OffsetXDip = 6; OffsetBottomDip = 54;
            ImageMode = "Cat"; ImagePath = "";
        }
        public OverlaySettings Copy() { return (OverlaySettings)MemberwiseClone(); }
    }
    public static class SettingsStore
    {
        static bool Finite(double n) { return !double.IsNaN(n) && !double.IsInfinity(n); }
        static bool Color(string s, bool auto) { return (auto && s == "Auto") || (s != null && Regex.IsMatch(s, "^#[0-9a-fA-F]{6}$")); }
        public static bool Validate(OverlaySettings s)
        {
            if (s == null || (s.Theme != "System" && s.Theme != "Light" && s.Theme != "Dark") || !Color(s.FiveHourColor, false) || !Color(s.WeeklyColor, false) || !Color(s.BackgroundColor, true) || !Color(s.TextColor, true)) return false;
            if (!Color(s.HighRemainingColor, false) || !Color(s.MediumRemainingColor, false) || !Color(s.LowRemainingColor, false)) return false;
            if (!Finite(s.WidthDip) || !Finite(s.HeightDip) || s.WidthDip < 36 || s.WidthDip > 120 || s.HeightDip < 128 || s.HeightDip > 320) return false;
            if (!Finite(s.OffsetXDip) || !Finite(s.OffsetBottomDip) || s.OffsetXDip < 0 || s.OffsetXDip > 10000 || s.OffsetBottomDip < 0 || s.OffsetBottomDip > 10000) return false;
            if (s.SizePreset != "Slim" && s.SizePreset != "Readable" && s.SizePreset != "Custom") return false;
            if (s.ImageMode != "Cat" && s.ImageMode != "None" && s.ImageMode != "Built-in" && s.ImageMode != "Custom") return false;
            if (s.ImageMode == "Custom" && !SafeRelativeImage(s.ImagePath)) return false;
            return true;
        }
        static bool SafeRelativeImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length > 512 || Path.IsPathRooted(path) || path.Contains(":")) return false;
            string normalized = path.Replace('/', '\\');
            if (!normalized.StartsWith("data\\images\\", StringComparison.OrdinalIgnoreCase) || normalized.Contains("..")) return false;
            string ext = Path.GetExtension(path).ToLowerInvariant(); return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".ico";
        }
        public static string ImageFullPath(string root, string relative)
        {
            if (!SafeRelativeImage(relative)) return "";
            try
            {
                string prefix = Path.GetFullPath(Path.Combine(root, "data", "images")) + Path.DirectorySeparatorChar;
                string full = Path.GetFullPath(Path.Combine(root, relative));
                return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? full : "";
            }
            catch { return ""; }
        }
        public static OverlaySettings Load(string root)
        {
            try
            {
                string file = Path.Combine(root, "data", "settings.json"); if (!File.Exists(file) || new FileInfo(file).Length > 65536) return new OverlaySettings();
                var s = new JavaScriptSerializer { MaxJsonLength = 65536 }.Deserialize<OverlaySettings>(File.ReadAllText(file));
                return Validate(s) ? s : new OverlaySettings();
            }
            catch { return new OverlaySettings(); }
        }
        public static void Save(string root, OverlaySettings settings)
        {
            if (!Validate(settings)) throw new ArgumentException("Settings need valid dimensions, colors and a local image");
            string data = Path.Combine(root, "data"); Directory.CreateDirectory(data);
            string file = Path.Combine(data, "settings.json"), temp = Path.Combine(data, ".settings-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(temp, new JavaScriptSerializer().Serialize(settings), new UTF8Encoding(false));
                if (File.Exists(file)) File.Replace(temp, file, null); else File.Move(temp, file);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public static string ImportImage(string root, string source)
        {
            var info = new FileInfo(source);
            string extension = info.Extension.ToLowerInvariant();
            if (!info.Exists || info.Length > 10 * 1024 * 1024 || (extension != ".png" && extension != ".jpg" && extension != ".jpeg" && extension != ".ico")) throw new ArgumentException("Choose a PNG, JPEG or ICO up to 10 MB");
            using (var stream = File.OpenRead(source))
            {
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                if (decoder.Frames.Count == 0 || decoder.Frames.Count > 64 || decoder.Frames[0].PixelWidth > 4096 || decoder.Frames[0].PixelHeight > 4096) throw new ArgumentException("Image must be at most 4096 × 4096 pixels");
            }
            string relative = "data/images/" + Guid.NewGuid().ToString("N") + extension;
            string target = ImageFullPath(root, relative); Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(source, target);
            return relative;
        }
        public static void ClearOwnedPreferences(string root)
        {
            string data = Path.GetFullPath(Path.Combine(root, "data"));
            if (!Directory.Exists(data)) return;
            if ((File.GetAttributes(data) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked data directory was left untouched");
            string images = Path.Combine(data, "images");
            if (Directory.Exists(images))
            {
                if ((File.GetAttributes(images) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked image directory was left untouched");
                foreach (string file in Directory.GetFiles(images, "*", SearchOption.TopDirectoryOnly))
                {
                    Guid id; if (Guid.TryParseExact(Path.GetFileNameWithoutExtension(file), "N", out id) && SafeRelativeImage("data/images/" + Path.GetFileName(file))) File.Delete(file);
                }
                if (Directory.GetFileSystemEntries(images).Length == 0) Directory.Delete(images);
            }
            foreach (string name in new[] { "settings.json", "startup-owner.txt" }) { string file = Path.Combine(data, name); if (File.Exists(file)) File.Delete(file); }
        }
    }
}
