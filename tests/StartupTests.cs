using System;
using System.IO;

namespace CodexUsageOverlay.Tests
{
    public sealed class MemoryShortcutStore : IShortcutStore
    {
        public ShortcutRecord Record;
        public int Deletes, Writes;
        public ShortcutRecord Read(string path) { return Record; }
        public void Write(string path, ShortcutRecord record) { Record = record; Writes++; }
        public void Delete(string path) { Record = null; Deletes++; }
    }
    public static class StartupTests
    {
        static string Root() { string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-data", "startup")); Directory.CreateDirectory(Path.Combine(root, "out")); File.WriteAllText(Path.Combine(root, "out", "CodexUsageOverlay.exe"), "test target, never executed"); return root; }
        [Case("Startup")] public static void OwnedShortcutCanDisable()
        {
            var store = new MemoryShortcutStore(); var manager = new ShortcutManager(Root(), "test-owner", store, "test-startup.lnk");
            manager.SetEnabled(true); Assert.Equal(StartupKind.Enabled, manager.Inspect().Kind);
            manager.SetEnabled(false); Assert.Equal(1, store.Deletes); Assert.Equal(StartupKind.Disabled, manager.Inspect().Kind);
        }
        [Case("Startup")] public static void ForeignShortcutUntouched()
        {
            var store = new MemoryShortcutStore { Record = new ShortcutRecord { Description = "Other program", Target = "C:\\Other.exe" } };
            var manager = new ShortcutManager(Root(), "test-owner", store, "test-startup.lnk");
            Assert.Equal(StartupKind.Foreign, manager.Inspect().Kind);
            Assert.Throws(() => manager.SetEnabled(false)); Assert.Throws(() => manager.SetEnabled(true));
            Assert.Equal(0, store.Deletes); Assert.Equal(0, store.Writes);
        }
        [Case("Startup")] public static void MovedTargetIsBrokenAndRepairable()
        {
            var store = new MemoryShortcutStore(); var manager = new ShortcutManager(Root(), "test-owner", store, "test-startup.lnk"); manager.SetEnabled(true);
            store.Record.Target = "C:\\old\\out\\CodexUsageOverlay.exe";
            Assert.Equal(StartupKind.Broken, manager.Inspect().Kind); manager.SetEnabled(true);
            Assert.Equal(Path.Combine(Root(), "out", "CodexUsageOverlay.exe"), store.Record.Target);
        }
        [Case("Startup")] public static void EnableReportsExactPaths()
        {
            var store = new MemoryShortcutStore(); var manager = new ShortcutManager(Root(), "test-owner", store, "test-startup.lnk"); var result = manager.SetEnabled(true);
            Assert.Equal(Path.Combine(Root(), "out", "CodexUsageOverlay.exe"), result.TargetPath); Assert.Equal("test-startup.lnk", result.ShortcutPath);
            Assert.Equal(Root(), store.Record.WorkingDirectory); Assert.Equal("", store.Record.Arguments);
        }
    }
}
