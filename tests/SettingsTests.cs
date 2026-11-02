using System;
using System.IO;

namespace CodexUsageOverlay.Tests
{
    public static class SettingsTests
    {
        static string Root() { string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(p); return p; }
        [Case("Settings")] public static void ExistingSettingsGainAutomaticColoursWithoutLosingAppearance()
        {
            string root = Root(); Directory.CreateDirectory(Path.Combine(root, "data"));
            File.WriteAllText(Path.Combine(root, "data", "settings.json"), "{\"Theme\":\"Dark\",\"FiveHourColor\":\"#112233\"}");
            var s = SettingsStore.Load(root); Assert.True(s.AutomaticRingColours, "Existing settings did not gain automatic colours"); Assert.Equal("Dark", s.Theme); Assert.Equal("#112233", s.FiveHourColor);
        }
        [Case("Settings")] public static void CorruptSettingsFallBackSafely()
        {
            string root = Root(); Directory.CreateDirectory(Path.Combine(root, "data")); File.WriteAllText(Path.Combine(root, "data", "settings.json"), "{broken");
            var s = SettingsStore.Load(root); Assert.Equal(44.0, s.WidthDip); Assert.Equal("Cat", s.ImageMode);
        }
        [Case("Settings")] public static void InvalidSizesColoursAndPathsAreRejected()
        {
            var s = new OverlaySettings { WidthDip = double.NaN }; Assert.True(!SettingsStore.Validate(s), "NaN size accepted");
            s = new OverlaySettings { FiveHourColor = "Red; anything" }; Assert.True(!SettingsStore.Validate(s), "Invalid color accepted");
            s = new OverlaySettings { ImageMode = "Custom", ImagePath = "../../outside.png" }; Assert.True(!SettingsStore.Validate(s), "Traversal accepted");
            Assert.Equal("", SettingsStore.ImageFullPath(Root(), "../outside.png"));
        }
        [Case("Settings")] public static void AtomicSettingsRoundTripContainsNoQuotaOrIdentity()
        {
            string root = Root(); var s = new OverlaySettings { Theme = "Dark", OffsetXDip = 8 };
            SettingsStore.Save(root, s); s.OffsetXDip = 12; SettingsStore.Save(root, s);
            Assert.Equal(12.0, SettingsStore.Load(root).OffsetXDip);
            string json = File.ReadAllText(Path.Combine(root, "data", "settings.json"));
            Assert.True(!json.Contains("Source") && !json.Contains("account") && !json.Contains("Quota") && !json.Contains("Percent"), "Private quota/account persisted");
            Assert.Equal(1, Directory.GetFiles(Path.Combine(root, "data")).Length);
        }
        [Case("Settings")] public static void UninstallClearsOnlyOwnedSettingsAndCopiedImages()
        {
            string root = Root(); string images = Path.Combine(root, "data", "images"); Directory.CreateDirectory(images);
            string owned = Path.Combine(images, "e92cc7722e154bc2a3925ae0fbe87f43.png"), other = Path.Combine(images, "my-original.png");
            File.WriteAllText(owned, "copied image"); File.WriteAllText(other, "unrecognized user file");
            SettingsStore.Save(root, new OverlaySettings()); Directory.CreateDirectory(Path.Combine(root, "assets")); File.WriteAllText(Path.Combine(root, "assets", "sleeping-cat.png"), "source asset");
            SettingsStore.ClearOwnedPreferences(root);
            Assert.True(!File.Exists(Path.Combine(root, "data", "settings.json")) && !File.Exists(owned), "Owned data remained");
            Assert.True(File.Exists(other) && File.Exists(Path.Combine(root, "assets", "sleeping-cat.png")), "Unrelated or source file deleted");
        }
    }
}
