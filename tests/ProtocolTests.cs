using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace CodexUsageOverlay.Tests
{
    public static class ProtocolTests
    {
        static ProtocolClient Client(string mode, int timeout, int maxLine)
        {
            var p = new Process { StartInfo = new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, "--fake-server " + mode) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true } };
            p.Start(); return new ProtocolClient(p, timeout, maxLine);
        }
        [Case("Protocol")] public static void ResponsesMatchIdsWithInterleavedNotification()
        {
            using (var c = Client("reverse", 2000, 8192))
            {
                int events = 0; c.Notification += delegate { Interlocked.Increment(ref events); };
                var a = c.RequestAsync("account/read", new { label = "first" }, CancellationToken.None);
                var b = c.RequestAsync("account/read", new { label = "second" }, CancellationToken.None);
                Task.WaitAll(a, b);
                Assert.Equal("first", JsonValue.Text(JsonValue.Get(JsonValue.Dict(JsonValue.Get(a.Result, "echo")), "label")));
                Assert.Equal("second", JsonValue.Text(JsonValue.Get(JsonValue.Dict(JsonValue.Get(b.Result, "echo")), "label")));
                Assert.Equal(1, events);
            }
        }
        [Case("Protocol")] public static void MalformedOversizedAndEofRejectPending()
        {
            foreach (string mode in new[] { "malformed", "oversized", "eof" })
            using (var c = Client(mode, 1500, 1024))
                Assert.Throws(() => c.RequestAsync("account/read", null, CancellationToken.None).GetAwaiter().GetResult());
        }
        [Case("Protocol")] public static void TimeoutAndCancellationAreBounded()
        {
            using (var c = Client("timeout", 200, 4096))
            {
                var clock = Stopwatch.StartNew(); Assert.Throws(() => c.RequestAsync("account/read", null, CancellationToken.None).GetAwaiter().GetResult());
                Assert.True(clock.ElapsedMilliseconds < 2000, "Timeout was unbounded");
                using (var cancel = new CancellationTokenSource()) { cancel.Cancel(); Assert.Throws(() => c.RequestAsync("account/read", null, cancel.Token).GetAwaiter().GetResult()); }
            }
        }
        [Case("Protocol")] public static void MutatingMethodsAreRejected()
        {
            using (var c = Client("normal", 1000, 8192)) Assert.Throws(() => c.RequestAsync("account/logout", null, CancellationToken.None).GetAwaiter().GetResult());
        }
    }
}
