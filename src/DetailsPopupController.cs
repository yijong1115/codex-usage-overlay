using System;

namespace CodexUsageOverlay
{
    public sealed class DetailsPopupController : IDisposable
    {
        readonly Func<bool, DetailsWindow> create;
        public DetailsWindow Current { get; private set; }
        public DetailsPopupController(Func<bool, DetailsWindow> factory) { create = factory; }
        public void Toggle(bool fiveHour)
        {
            if (Current != null)
            {
                bool same = Current.FiveHour == fiveHour; Close(); if (same) return;
            }
            var popup = create(fiveHour); Current = popup;
            if (popup != null) popup.Closed += delegate { if (object.ReferenceEquals(Current, popup)) Current = null; };
        }
        public void Close() { var popup = Current; Current = null; if (popup != null) popup.Close(); }
        public void Dispose() { Close(); }
    }
}
