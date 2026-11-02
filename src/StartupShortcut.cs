using System;
using System.IO;
using System.Runtime.InteropServices;

namespace CodexUsageOverlay
{
    public enum StartupKind { Enabled, Disabled, Broken, Foreign }
    public sealed class StartupStatus
    {
        public StartupKind Kind;
        public string ShortcutPath, TargetPath;
    }
    public sealed class ShortcutRecord
    {
        public string Target, WorkingDirectory, Arguments, Description;
    }
    public interface IShortcutStore
    {
        ShortcutRecord Read(string path);
        void Write(string path, ShortcutRecord record);
        void Delete(string path);
    }
    public sealed class ShellShortcutStore : IShortcutStore
    {
        public ShortcutRecord Read(string path)
        {
            if (!File.Exists(path)) return null;
            object shell = null, shortcut = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")); shortcut = ((dynamic)shell).CreateShortcut(path); dynamic link = shortcut;
                return new ShortcutRecord { Target = link.TargetPath, WorkingDirectory = link.WorkingDirectory, Arguments = link.Arguments, Description = link.Description };
            }
            catch { return new ShortcutRecord { Description = "Unreadable shortcut" }; }
            finally { if (shortcut != null) Marshal.FinalReleaseComObject(shortcut); if (shell != null) Marshal.FinalReleaseComObject(shell); }
        }
        public void Write(string path, ShortcutRecord record)
        {
            object shell = null, shortcut = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")); shortcut = ((dynamic)shell).CreateShortcut(path); dynamic link = shortcut;
                link.TargetPath = record.Target; link.WorkingDirectory = record.WorkingDirectory; link.Arguments = record.Arguments; link.Description = record.Description; link.IconLocation = record.Target + ",0"; link.WindowStyle = 7; link.Save();
            }
            finally { if (shortcut != null) Marshal.FinalReleaseComObject(shortcut); if (shell != null) Marshal.FinalReleaseComObject(shell); }
        }
        public void Delete(string path) { File.Delete(path); }
    }
    public sealed class ShortcutManager
    {
        readonly string root, owner, path;
        readonly IShortcutStore store;
        const string Marker = "Codex Usage Overlay owner:";
        public ShortcutManager(string projectRoot, string ownerId, IShortcutStore shortcutStore, string shortcutPath) { root = Path.GetFullPath(projectRoot); owner = ownerId; store = shortcutStore; path = shortcutPath; }
        public StartupStatus Inspect()
        {
            string target = Path.Combine(root, "out", "CodexUsageOverlay.exe"); var record = store.Read(path);
            StartupKind kind = StartupKind.Disabled;
            if (record != null)
            {
                if (string.IsNullOrWhiteSpace(owner) || record.Description != Marker + owner) kind = StartupKind.Foreign;
                else if (!Same(record.Target, target) || !Same(record.WorkingDirectory, root) || !string.IsNullOrEmpty(record.Arguments) || !File.Exists(target)) kind = StartupKind.Broken;
                else kind = StartupKind.Enabled;
            }
            return new StartupStatus { Kind = kind, ShortcutPath = path, TargetPath = target };
        }
        static bool Same(string a, string b) { try { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); } catch { return false; } }
        public StartupStatus SetEnabled(bool enabled)
        {
            var status = Inspect(); if (status.Kind == StartupKind.Foreign) throw new InvalidOperationException("Another shortcut already uses this name; it was left untouched");
            if (enabled)
            {
                if (string.IsNullOrWhiteSpace(owner) || !File.Exists(status.TargetPath)) throw new InvalidOperationException("Build the overlay before enabling startup");
                store.Write(path, new ShortcutRecord { Target = status.TargetPath, WorkingDirectory = root, Arguments = "", Description = Marker + owner });
            }
            else if (status.Kind != StartupKind.Disabled) store.Delete(path);
            return Inspect();
        }
    }
    public static class StartupShortcut
    {
        static string Owner(string root)
        {
            string file = Path.Combine(root, "data", "startup-owner.txt");
            try { if (!File.Exists(file) || new FileInfo(file).Length > 100) return ""; Guid value; string text = File.ReadAllText(file).Trim(); return Guid.TryParse(text, out value) ? text : ""; } catch { return ""; }
        }
        static ShortcutManager Manager(string root) { return new ShortcutManager(root, Owner(root), new ShellShortcutStore(), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Codex Usage Overlay.lnk")); }
        public static StartupStatus Inspect(string root) { return Manager(root).Inspect(); }
        public static StartupStatus SetEnabled(string root, bool enabled)
        {
            if (enabled && Owner(root).Length == 0)
            {
                if (Inspect(root).Kind == StartupKind.Foreign) throw new InvalidOperationException("Existing foreign startup shortcut was left untouched");
                string data = Path.Combine(root, "data"); Directory.CreateDirectory(data); string file = Path.Combine(data, "startup-owner.txt");
                using (var writer = new StreamWriter(new FileStream(file, FileMode.CreateNew, FileAccess.Write))) writer.Write(Guid.NewGuid().ToString("D"));
            }
            return Manager(root).SetEnabled(enabled);
        }
    }
}
