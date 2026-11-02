using System;
using System.Collections;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CodexUsageOverlay.Tests
{
    public static class InteractionTests
    {
        static T Find<T>(DependencyObject parent, string id) where T : DependencyObject
        {
            if (AutomationProperties.GetAutomationId(parent) == id) return (T)parent;
            foreach (object child in LogicalTreeHelper.GetChildren(parent))
            {
                var dependency = child as DependencyObject; if (dependency == null) continue;
                var match = Find<T>(dependency, id); if (match != null) return match;
            }
            return null;
        }
        static OverlayWindow Card()
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
            var card = new OverlayWindow(root);
            card.Apply(new QuotaView { State = DisplayState.Loading, Status = "Checking usage" }, new OverlaySettings(), new PlacementResult());
            return card;
        }
        [Case("Interaction")] public static void ImageClickOpensCustomizationWithoutQuotaDetails()
        {
            var card = Card(); int settings = 0, details = 0;
            card.ImageSettingsRequested += delegate { settings++; };
            card.DetailsRequested += delegate { details++; };
            var image = Find<Button>(card, "ImageSettingsButton"); Assert.True(image != null, "Image has no accessible click target");
            image.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(1, settings); Assert.Equal(0, details); card.Close();
        }
        [Case("Interaction")] public static void RingHoverDoesNothingButClickRequestsDetails()
        {
            var card = Card(); int count = 0; bool selected = false;
            card.DetailsRequested += delegate(bool five) { count++; selected = five; };
            var ring = Find<Button>(card, "FiveHourUsageButton"); Assert.True(ring != null, "Five-hour click target missing");
            ring.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseEnterEvent });
            Assert.Equal(0, count); Assert.True(card.ToolTip == null && ring.ToolTip == null, "Hover still has a popup");
            ring.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.Equal(1, count); Assert.True(selected, "Wrong ring requested"); card.Close();
        }
        [Case("Interaction")] public static void SameRingClickClosesAndOtherRingSwitches()
        {
            int created = 0; bool firstClosed = false, weekClosed = false;
            using (var popups = new DetailsPopupController(five => { created++; return new DetailsWindow(five); }))
            {
                popups.Toggle(true); var first = popups.Current; first.Closed += delegate { firstClosed = true; };
                popups.Toggle(true); Assert.True(popups.Current == null && firstClosed, "Same ring reopened instead of closing"); Assert.Equal(1, created);
                popups.Toggle(false); popups.Current.Closed += delegate { weekClosed = true; };
                popups.Toggle(true); Assert.True(weekClosed && popups.Current.FiveHour, "Other ring did not replace details"); Assert.Equal(3, created);
            }
        }
        [Case("Interaction")] public static void ClosingDetailsDoesNotPaintMouseFocusAsSelection()
        {
            var card = Card(); var ring = Find<Button>(card, "WeeklyUsageButton");
            ((Panel)ring.Parent).Children.Remove(ring);
            var host = new Window { Content = ring, Width = 80, Height = 120, Left = -10000, Top = -10000, ShowInTaskbar = false };
            host.Resources["Accent"] = Brushes.Blue; host.Resources["Hover"] = Brushes.LightGray;
            try
            {
                host.Show(); host.Activate(); ring.Focus(); host.UpdateLayout();
                Assert.True(ring.IsKeyboardFocused, "Could not reproduce retained focus on the usage button");
                using (var popups = new DetailsPopupController(five => new DetailsWindow(five)))
                {
                    ring.Click += delegate { popups.Toggle(false); };
                    ring.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); ring.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.True(popups.Current == null, "Second click did not close the details");
                    var surface = (Border)ring.Template.FindName("Surface", ring);
                    Assert.Equal((byte)0, ((SolidColorBrush)surface.BorderBrush).Color.A);
                    Assert.True(ring.Focusable && ring.FocusVisualStyle != null, "Keyboard navigation lost its focus cue");
                }
            }
            finally { host.Close(); card.Close(); }
        }
    }
}
