using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CodexUsageOverlay
{
    public static class CodexLocator
    {
        public static string Resolve()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin");
            if (!Directory.Exists(root)) throw new FileNotFoundException("Official Codex CLI not found");
            string found = Directory.GetDirectories(root).Select(d => Path.Combine(d, "codex.exe")).Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (found == null) throw new FileNotFoundException("Official Codex CLI not found");
            return found;
        }
        public static ProcessStartInfo StartInfo(string executable, string arguments)
        {
            return new ProcessStartInfo(executable, arguments) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        }
        public static string Version(string executable)
        {
            using (var p = new Process { StartInfo = StartInfo(executable, "--version") })
            {
                p.Start();
                Task<string> output = p.StandardOutput.ReadLineAsync();
                Task.Run(async delegate { var b = new char[1024]; try { while (await p.StandardError.ReadAsync(b, 0, b.Length).ConfigureAwait(false) > 0) { } } catch { } });
                if (!p.WaitForExit(5000) || !output.Wait(1000)) { try { p.Kill(); } catch { } throw new IOException("Codex version check timed out"); }
                string version = output.Result ?? "";
                if (p.ExitCode != 0 || !version.StartsWith("codex-cli ", StringComparison.Ordinal) || version.Length > 128) throw new IOException("Unsupported Codex CLI");
                return version;
            }
        }
        public static IRpcConnection Connect()
        {
            string file = Resolve(); Version(file);
            var p = new Process { StartInfo = StartInfo(file, "app-server --listen stdio://") };
            try { p.Start(); return new ProtocolClient(p, 10000, 1024 * 1024); }
            catch { p.Dispose(); throw; }
        }
    }
}
