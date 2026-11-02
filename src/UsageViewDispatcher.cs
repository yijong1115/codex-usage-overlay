using System;

namespace CodexUsageOverlay
{
    public sealed class UsageViewDispatcher : IDisposable
    {
        readonly IUsageReader reader;
        readonly Action<Action> post;
        readonly Action<QuotaView> apply;
        volatile bool disposed;
        public UsageViewDispatcher(IUsageReader source, Action<Action> dispatch, Action<QuotaView> consume)
        {
            reader = source; post = dispatch; apply = consume; reader.Changed += OnChanged;
        }
        void OnChanged(object sender, QuotaViewEventArgs e)
        {
            if (disposed) return;
            // A notification invalidates the UI; its payload may already be obsolete
            // by the time the dispatcher runs. Never carry quota across that boundary.
            post(delegate { if (!disposed) apply(reader.CurrentView); });
        }
        public void Dispose() { disposed = true; reader.Changed -= OnChanged; }
    }
}
