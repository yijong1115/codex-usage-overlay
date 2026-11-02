using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace CodexUsageOverlay
{
    public sealed class ProtocolNotification : EventArgs
    {
        public string Method;
        public IDictionary<string, object> Parameters;
    }
    public sealed class ProtocolException : Exception
    {
        public readonly bool Authentication;
        public ProtocolException(bool auth) : base(auth ? "Authentication required" : "Codex request failed") { Authentication = auth; }
    }
    public interface IRpcConnection : IDisposable
    {
        event EventHandler<ProtocolNotification> Notification;
        Task<IDictionary<string, object>> RequestAsync(string method, object parameters, CancellationToken cancellation);
        Task InitializeNotification(CancellationToken cancellation);
    }
    public sealed class ProtocolClient : IRpcConnection
    {
        readonly Process process;
        readonly int timeoutMs, maxBytes;
        readonly Dictionary<long, TaskCompletionSource<IDictionary<string, object>>> pending = new Dictionary<long, TaskCompletionSource<IDictionary<string, object>>>();
        readonly SemaphoreSlim writes = new SemaphoreSlim(1, 1);
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        long nextId;
        bool disposed, ended;
        public event EventHandler<ProtocolNotification> Notification;
        public ProtocolClient(Process ownedProcess, int requestTimeoutMs, int maximumLineBytes)
        {
            process = ownedProcess; timeoutMs = requestTimeoutMs; maxBytes = maximumLineBytes;
            Task.Run((Func<Task>)ReadLoop);
            Task.Run(async delegate { try { var buffer = new char[2048]; while (await process.StandardError.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false) > 0) { } } catch { } });
        }
        public async Task<IDictionary<string, object>> RequestAsync(string method, object parameters, CancellationToken cancellation)
        {
            if (method != "initialize" && method != "account/read" && method != "account/rateLimits/read") throw new InvalidOperationException("Method not allowed");
            long id = Interlocked.Increment(ref nextId);
            var completion = new TaskCompletionSource<IDictionary<string, object>>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (pending)
            {
                if (disposed || ended) throw new IOException("Codex connection closed");
                if (pending.Count >= 8) throw new IOException("Too many pending requests");
                pending.Add(id, completion);
            }
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation, lifetime.Token))
            {
                timeout.CancelAfter(timeoutMs);
                using (timeout.Token.Register(delegate { completion.TrySetCanceled(); }))
                {
                    try
                    {
                        await Write(new { method = method, id = id, @params = parameters }, timeout.Token).ConfigureAwait(false);
                        return await completion.Task.ConfigureAwait(false);
                    }
                    finally { lock (pending) { pending.Remove(id); } }
                }
            }
        }
        public Task InitializeNotification(CancellationToken cancellation) { return Write(new { method = "initialized", @params = new { } }, cancellation); }
        async Task Write(object message, CancellationToken cancellation)
        {
            string line = new JavaScriptSerializer().Serialize(message);
            if (line.Length > 65536) throw new IOException("Request too large");
            await writes.WaitAsync(cancellation).ConfigureAwait(false);
            try { cancellation.ThrowIfCancellationRequested(); await process.StandardInput.WriteLineAsync(line).ConfigureAwait(false); await process.StandardInput.FlushAsync().ConfigureAwait(false); }
            finally { writes.Release(); }
        }
        async Task ReadLoop()
        {
            try
            {
                var part = new StringBuilder(); var buffer = new char[4096]; int bytes = 0, count;
                while ((count = await process.StandardOutput.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                {
                    int start = 0;
                    for (int i = 0; i < count; i++) if (buffer[i] == '\n')
                    {
                        string chunk = new string(buffer, start, i - start); bytes += Encoding.UTF8.GetByteCount(chunk);
                        if (bytes > maxBytes) throw new IOException("Codex response too large");
                        part.Append(chunk); string line = part.ToString().TrimEnd('\r'); part.Clear(); bytes = 0; start = i + 1;
                        if (line.Length > 0) HandleLine(line);
                    }
                    if (start < count)
                    {
                        string chunk = new string(buffer, start, count - start); bytes += Encoding.UTF8.GetByteCount(chunk);
                        if (bytes > maxBytes) throw new IOException("Codex response too large"); part.Append(chunk);
                    }
                }
                throw new IOException("Codex connection ended");
            }
            catch { FailAll(); }
        }
        void HandleLine(string line)
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = maxBytes, RecursionLimit = 64 };
            var message = serializer.DeserializeObject(line) as IDictionary<string, object>;
            if (message == null) throw new IOException("Invalid Codex response");
            string method = JsonValue.Text(JsonValue.Get(message, "method"));
            if (method != null)
            {
                if (method == "account/rateLimits/updated" || method == "account/updated")
                {
                    var handler = Notification;
                    if (handler != null) handler(this, new ProtocolNotification { Method = method, Parameters = JsonValue.Dict(JsonValue.Get(message, "params")) });
                }
                return;
            }
            double? rawId = JsonValue.Number(JsonValue.Get(message, "id")); if (!rawId.HasValue) return;
            TaskCompletionSource<IDictionary<string, object>> tcs;
            lock (pending) { if (!pending.TryGetValue((long)rawId.Value, out tcs)) return; }
            var error = JsonValue.Dict(JsonValue.Get(message, "error"));
            if (error != null)
            {
                string text = (JsonValue.Text(JsonValue.Get(error, "message")) ?? "").ToLowerInvariant();
                tcs.TrySetException(new ProtocolException(text.Contains("auth") || text.Contains("sign in") || text.Contains("login") || text.Contains("not logged")));
            }
            else
            {
                var result = JsonValue.Dict(JsonValue.Get(message, "result"));
                if (result == null) tcs.TrySetException(new IOException("Invalid Codex result")); else tcs.TrySetResult(result);
            }
        }
        void FailAll()
        {
            lock (pending)
            {
                ended = true;
                foreach (var item in pending.Values) item.TrySetException(new IOException("Codex connection closed"));
                pending.Clear();
            }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; lifetime.Cancel(); FailAll();
            try { process.StandardInput.Close(); } catch { }
            try { if (!process.WaitForExit(500)) { process.Kill(); process.WaitForExit(1000); } } catch { }
            process.Dispose(); lifetime.Dispose();
        }
    }
}
