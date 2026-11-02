using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CodexUsageOverlay
{
    // Shared WPF controls retain their native automation and keyboard behavior.
    public static class WidgetAppearance
    {
        public static readonly FontFamily Font = new FontFamily("Segoe UI Variable, Segoe UI");
        public static void Install(Window window, Palette palette)
        {
            window.FontFamily = Font; window.FontSize = 13;
            window.UseLayoutRounding = true; window.SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(window, TextFormattingMode.Display);
            window.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(Styles));
            Apply(window, palette);
        }
        public static void Apply(Window window, Palette palette)
        {
            window.Resources["Ink"] = palette.Text; window.Resources["Muted"] = palette.Muted;
            window.Resources["Surface"] = palette.Surface; window.Resources["Outline"] = palette.Outline;
            window.Resources["Hover"] = palette.Hover; window.Resources["Accent"] = palette.Accent;
            window.Resources["AccentInk"] = SystemParameters.HighContrast ? SystemColors.HighlightTextBrush : Brushes.White;
            window.Resources["Error"] = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : Palette.Color(palette.Dark ? "#FF9696" : "#B42332");
            window.Foreground = palette.Text;
            window.Background = SystemParameters.HighContrast ? SystemColors.WindowBrush : Palette.Color(palette.Dark ? "#202124" : "#F5F5F7");
        }
        const string Styles = @"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
  <Style TargetType='{x:Type Button}'>
    <Setter Property='MinHeight' Value='32'/><Setter Property='Padding' Value='12,6'/>
    <Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Background' Value='{DynamicResource Surface}'/>
    <Setter Property='BorderBrush' Value='{DynamicResource Outline}'/><Setter Property='BorderThickness' Value='1'/>
    <Setter Property='Cursor' Value='Hand'/><Setter Property='HorizontalContentAlignment' Value='Center'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type Button}'>
      <Border x:Name='Chrome' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='9' Padding='{TemplateBinding Padding}'>
        <ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center' RecognizesAccessKey='True'/>
      </Border>
      <ControlTemplate.Triggers>
        <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Chrome' Property='Background' Value='{DynamicResource Hover}'/></Trigger>
        <Trigger Property='IsPressed' Value='True'><Setter TargetName='Chrome' Property='Opacity' Value='0.82'/></Trigger>
        <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Chrome' Property='BorderBrush' Value='{DynamicResource Accent}'/></Trigger>
        <Trigger Property='IsEnabled' Value='False'><Setter TargetName='Chrome' Property='Opacity' Value='0.45'/></Trigger>
      </ControlTemplate.Triggers>
    </ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style x:Key='PrimaryButton' TargetType='{x:Type Button}' BasedOn='{StaticResource {x:Type Button}}'>
    <Setter Property='Background' Value='{DynamicResource Accent}'/><Setter Property='Foreground' Value='{DynamicResource AccentInk}'/>
    <Setter Property='BorderBrush' Value='{DynamicResource Accent}'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type Button}'>
      <Border x:Name='Chrome' CornerRadius='9' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' Padding='{TemplateBinding Padding}'>
        <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' RecognizesAccessKey='True'/>
      </Border>
      <ControlTemplate.Triggers>
        <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Chrome' Property='Opacity' Value='0.9'/></Trigger>
        <Trigger Property='IsPressed' Value='True'><Setter TargetName='Chrome' Property='Opacity' Value='0.8'/></Trigger>
        <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Chrome' Property='BorderBrush' Value='{DynamicResource Ink}'/></Trigger>
        <Trigger Property='IsEnabled' Value='False'><Setter TargetName='Chrome' Property='Opacity' Value='0.45'/></Trigger>
      </ControlTemplate.Triggers>
    </ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style TargetType='{x:Type TextBox}'>
    <Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Background' Value='{DynamicResource Surface}'/>
    <Setter Property='CaretBrush' Value='{DynamicResource Ink}'/><Setter Property='BorderBrush' Value='{DynamicResource Outline}'/><Setter Property='BorderThickness' Value='1'/>
    <Setter Property='MinHeight' Value='32'/><Setter Property='Padding' Value='8,6'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type TextBox}'>
      <Border x:Name='Chrome' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='7'>
        <ScrollViewer x:Name='PART_ContentHost' Margin='{TemplateBinding Padding}'/>
      </Border>
      <ControlTemplate.Triggers><Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='Chrome' Property='BorderBrush' Value='{DynamicResource Accent}'/></Trigger></ControlTemplate.Triggers>
    </ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style TargetType='{x:Type ComboBox}'>
    <Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Background' Value='{DynamicResource Surface}'/>
    <Setter Property='BorderBrush' Value='{DynamicResource Outline}'/><Setter Property='MinHeight' Value='32'/>
    <Setter Property='ScrollViewer.HorizontalScrollBarVisibility' Value='Disabled'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ComboBox}'>
      <Grid>
        <Border x:Name='Chrome' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='8'/>
        <ContentPresenter Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}' Margin='10,6,30,6' VerticalAlignment='Center' IsHitTestVisible='False'/>
        <ToggleButton Focusable='False' IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}' ClickMode='Press'>
          <ToggleButton.Template><ControlTemplate TargetType='{x:Type ToggleButton}'><Border Background='Transparent'><Path Data='M 0 0 L 3.5 3.5 L 7 0' Stroke='{DynamicResource Muted}' StrokeThickness='1.4' HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,12,0'/></Border></ControlTemplate></ToggleButton.Template>
        </ToggleButton>
        <Popup x:Name='PART_Popup' Placement='Bottom' IsOpen='{TemplateBinding IsDropDownOpen}' AllowsTransparency='True' Focusable='False' PopupAnimation='None'>
          <Border Background='{DynamicResource Surface}' BorderBrush='{DynamicResource Outline}' BorderThickness='1' CornerRadius='9' Padding='4' Margin='0,4,0,0' MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}'>
            <ScrollViewer MaxHeight='260' CanContentScroll='True'><ItemsPresenter KeyboardNavigation.DirectionalNavigation='Contained'/></ScrollViewer>
          </Border>
        </Popup>
      </Grid>
      <ControlTemplate.Triggers>
        <Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='Chrome' Property='BorderBrush' Value='{DynamicResource Accent}'/></Trigger>
        <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.45'/></Trigger>
      </ControlTemplate.Triggers>
    </ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style TargetType='{x:Type ComboBoxItem}'>
    <Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Padding' Value='9,7'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ComboBoxItem}'>
      <Border x:Name='Chrome' CornerRadius='6' Background='Transparent' Padding='{TemplateBinding Padding}'><ContentPresenter/></Border>
      <ControlTemplate.Triggers>
        <Trigger Property='IsSelected' Value='True'><Setter TargetName='Chrome' Property='Background' Value='{DynamicResource Hover}'/></Trigger>
        <Trigger Property='IsHighlighted' Value='True'><Setter TargetName='Chrome' Property='Background' Value='{DynamicResource Accent}'/><Setter Property='Foreground' Value='{DynamicResource AccentInk}'/></Trigger>
      </ControlTemplate.Triggers>
    </ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style TargetType='{x:Type TabControl}'>
    <Setter Property='Background' Value='{DynamicResource Surface}'/><Setter Property='BorderBrush' Value='{DynamicResource Outline}'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type TabControl}'>
      <DockPanel>
        <Border DockPanel.Dock='Top' Background='{DynamicResource Hover}' CornerRadius='11' Padding='3' Margin='0,0,0,14'><TabPanel IsItemsHost='True'/></Border>
        <Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='14' Padding='{TemplateBinding Padding}'>
          <ContentPresenter x:Name='PART_SelectedContentHost' ContentSource='SelectedContent'/>
        </Border>
      </DockPanel>
    </ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style TargetType='{x:Type TabItem}'>
    <Setter Property='Foreground' Value='{DynamicResource Muted}'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type TabItem}'>
      <Border x:Name='Chrome' CornerRadius='8' BorderThickness='1' BorderBrush='Transparent' Padding='{TemplateBinding Padding}' Margin='1,0'>
        <ContentPresenter ContentSource='Header' HorizontalAlignment='Center' VerticalAlignment='Center' RecognizesAccessKey='True'/>
      </Border>
      <ControlTemplate.Triggers>
        <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Chrome' Property='Background' Value='{DynamicResource Surface}'/></Trigger>
        <Trigger Property='IsSelected' Value='True'><Setter TargetName='Chrome' Property='Background' Value='{DynamicResource Surface}'/><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='FontWeight' Value='SemiBold'/></Trigger>
        <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Chrome' Property='BorderBrush' Value='{DynamicResource Accent}'/></Trigger>
      </ControlTemplate.Triggers>
    </ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style TargetType='{x:Type CheckBox}'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='VerticalContentAlignment' Value='Center'/></Style>
  <Style TargetType='{x:Type Expander}'><Setter Property='Foreground' Value='{DynamicResource Ink}'/></Style>
</ResourceDictionary>";
    }
    public static class WidgetMotion
    {
        static bool Allowed { get { return SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast; } }
        public static void PressFeedback(Button button)
        {
            button.RenderTransformOrigin = new Point(.5, .5);
            button.PreviewMouseLeftButtonDown += delegate { Scale(button, .975); };
            button.PreviewMouseLeftButtonUp += delegate { Scale(button, 1); };
            button.LostMouseCapture += delegate { Scale(button, 1); };
            button.MouseLeave += delegate { Scale(button, 1); };
        }
        static void Scale(FrameworkElement element, double target)
        {
            var scale = element.RenderTransform as ScaleTransform;
            if (!Allowed)
            {
                if (scale != null) { scale.BeginAnimation(ScaleTransform.ScaleXProperty, null); scale.BeginAnimation(ScaleTransform.ScaleYProperty, null); scale.ScaleX = scale.ScaleY = 1; }
                return;
            }
            if (scale == null) { scale = new ScaleTransform(1, 1); element.RenderTransform = scale; }
            Animate(scale, ScaleTransform.ScaleXProperty, scale.ScaleX, target, 100);
            Animate(scale, ScaleTransform.ScaleYProperty, scale.ScaleY, target, 100);
        }
        static void Animate(Animatable owner, DependencyProperty property, double from, double to, int milliseconds)
        {
            owner.SetValue(property, to); owner.BeginAnimation(property, Transition(from, to, milliseconds), HandoffBehavior.SnapshotAndReplace);
        }
        static DoubleAnimationUsingKeyFrames Transition(double from, double to, int milliseconds)
        {
            var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(milliseconds), FillBehavior = FillBehavior.Stop };
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new SplineDoubleKeyFrame(to, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(milliseconds)), new KeySpline(.23, 1, .32, 1)));
            return animation;
        }
        public static void Enter(FrameworkElement surface, Point origin)
        {
            if (!Allowed || !(InputManager.Current.MostRecentInputDevice is MouseDevice)) return;
            surface.RenderTransformOrigin = origin; var scale = new ScaleTransform(1, 1); surface.RenderTransform = scale;
            Animate(scale, ScaleTransform.ScaleXProperty, .98, 1, 160); Animate(scale, ScaleTransform.ScaleYProperty, .98, 1, 160);
            surface.BeginAnimation(UIElement.OpacityProperty, Transition(0, 1, 160), HandoffBehavior.SnapshotAndReplace);
        }
    }
}
