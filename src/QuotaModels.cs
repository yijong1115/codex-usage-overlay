using System;

namespace CodexUsageOverlay
{
    public sealed class SourceIdentity
    {
        public readonly string SessionKey;
        public readonly string LocalLabel;
        public readonly string AccountId;
        public SourceIdentity(string key, string label) : this(key, label, null) { }
        public SourceIdentity(string key, string label, string accountId) { SessionKey = key; LocalLabel = label; AccountId = accountId; }
    }
    public sealed class QuotaWindow
    {
        public int DurationMinutes;
        public double? RemainingPercent;
        public DateTimeOffset? ResetsAt;
        public QuotaWindow(int duration) { DurationMinutes = duration; }
    }
    public sealed class QuotaSnapshot
    {
        public long Generation;
        public SourceIdentity Source;
        public QuotaWindow FiveHour = new QuotaWindow(300);
        public QuotaWindow Weekly = new QuotaWindow(10080);
        public DateTimeOffset CheckedAt;
    }
    public enum DisplayState { Loading, Current, Retry, Stale, Unavailable, SignInNeeded, SourceUnknown }
    public sealed class QuotaView
    {
        public DisplayState State;
        public QuotaSnapshot Current;
        public QuotaSnapshot LastKnown;
        public string Status;
    }
    public sealed class QuotaViewEventArgs : EventArgs
    {
        public QuotaView View;
        public QuotaViewEventArgs(QuotaView view) { View = view; }
    }
    public interface IUsageReader : IDisposable
    {
        event EventHandler<QuotaViewEventArgs> Changed;
        QuotaView CurrentView { get; }
        void SetActive(bool active);
        void RefreshNow();
    }
    public static class TimeExtensions
    {
        static readonly DateTimeOffset Epoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public static long ToUnixSeconds(this DateTimeOffset value) { return (long)(value - Epoch).TotalSeconds; }
        public static DateTimeOffset? FromUnix(double value)
        {
            try { return Epoch.AddSeconds(value); } catch (ArgumentOutOfRangeException) { return null; }
        }
    }
}
