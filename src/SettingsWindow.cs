using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace CodexUsageOverlay
{
    public sealed class SettingsWindow : Window
    {
        readonly string project;
        readonly OverlaySettings original;
        readonly ComboBox theme, preset, imageMode;
        readonly TextBox width, height, five, week, high, medium, low, background, text, x, bottom;
        readonly TextBlock message, startupStatus, imageName;
        readonly CheckBox startup, automaticColours;
        readonly TabControl tabs;
        readonly StackPanel dimensions;
        readonly Image previewImage;
        readonly TextBlock previewSymbol;
        string imagePath, chosenName;
        bool saved, ready;
        public event Action<OverlaySettings> PreviewRequested, Saved;
        public event Action RepositionRequested;
        public SettingsWindow(string root, OverlaySettings settings, string section = "Appearance")
        {
            project = root; original = settings.Copy(); imagePath = settings.ImagePath;
            Title = "Usage Overlay Settings"; Width = 530; Height = 630; MinWidth = 490; MinHeight = 500;
            MaxHeight = Math.Max(440, SystemParameters.WorkArea.Height - 40); WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WidgetAppearance.Install(this, Palette.Resolve(new OverlaySettings { Theme = settings.Theme }));
            var layout = new Grid { Margin = new Thickness(24) };
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); layout.RowDefinitions.Add(new RowDefinition());
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); Content = layout;
            var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
            heading.Children.Add(new TextBlock { Text = "Make it yours", FontSize = 25, FontWeight = FontWeights.SemiBold });
            heading.Children.Add(Hint("Choose a look, change your image, or move the card.")); layout.Children.Add(heading);
            tabs = new TabControl { Padding = new Thickness(17) };
            System.Windows.Automation.AutomationProperties.SetAutomationId(tabs, "SettingsSections"); Grid.SetRow(tabs, 1); layout.Children.Add(tabs);

            var appearance = Page("Appearance");
            theme = Choice(appearance, "Card theme", new[] { "System", "Light", "Dark" }, settings.Theme);
            theme.SelectionChanged += delegate { WidgetAppearance.Apply(this, Palette.Resolve(new OverlaySettings { Theme = (string)theme.SelectedItem })); };
            appearance.Children.Add(Hint("System follows your Windows appearance."));
            preset = Choice(appearance, "Card size", new[] { "Slim", "Readable", "Custom" }, settings.SizePreset);
            dimensions = new StackPanel(); width = Field(dimensions, "Width", settings.WidthDip); height = Field(dimensions, "Height", settings.HeightDip); appearance.Children.Add(dimensions);
            automaticColours = new CheckBox { Content = "Automatic ring colours", IsChecked = settings.AutomaticRingColours, Margin = new Thickness(0, 12, 0, 2) }; appearance.Children.Add(automaticColours);
            appearance.Children.Add(Hint("70–100% green · 30–69% yellow · Below 30% red"));
            var colours = new StackPanel(); var automaticPalette = new StackPanel(); var manualPalette = new StackPanel(); colours.Children.Add(automaticPalette); colours.Children.Add(manualPalette);
            high = Colour(automaticPalette, "70–100% remaining", settings.HighRemainingColor, false); medium = Colour(automaticPalette, "30–69% remaining", settings.MediumRemainingColor, false); low = Colour(automaticPalette, "Below 30%", settings.LowRemainingColor, false);
            five = Colour(manualPalette, "Five-hour ring", settings.FiveHourColor, false); week = Colour(manualPalette, "Weekly ring", settings.WeeklyColor, false);
            Action updatePalette = delegate { automaticPalette.Visibility = automaticColours.IsChecked == true ? Visibility.Visible : Visibility.Collapsed; manualPalette.Visibility = automaticColours.IsChecked == true ? Visibility.Collapsed : Visibility.Visible; };
            automaticColours.Checked += delegate { updatePalette(); }; automaticColours.Unchecked += delegate { updatePalette(); }; updatePalette();
            background = Colour(colours, "Background", settings.BackgroundColor, true); text = Colour(colours, "Text", settings.TextColor, true);
            appearance.Children.Add(new Expander { Header = "Custom colours", Content = colours, Margin = new Thickness(0, 14, 0, 10) });
            startup = new CheckBox { Content = "Start at Windows sign-in", Margin = new Thickness(0, 12, 0, 4) }; startupStatus = Hint(""); appearance.Children.Add(startup); appearance.Children.Add(startupStatus);

            var imagePage = Page("Image");
            previewImage = new Image { Width = 113, Height = 80, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left };
            previewSymbol = new TextBlock { Text = "✦", FontSize = 38, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
            var imageTile = new Grid(); imageTile.Children.Add(previewImage); imageTile.Children.Add(previewSymbol);
            var imageHeader = new Grid(); imageHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(158) }); imageHeader.ColumnDefinitions.Add(new ColumnDefinition());
            var imageBackdrop = new Border { CornerRadius = new CornerRadius(13), Padding = new Thickness(12), Child = imageTile, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 12, 10) };
            imageBackdrop.SetResourceReference(Border.BackgroundProperty, "Hover"); imageHeader.Children.Add(imageBackdrop);
            var sizeGuide = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            sizeGuide.Children.Add(new TextBlock { Text = "Recommended: 300 × 200 px", FontSize = 12, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            sizeGuide.Children.Add(Hint("Transparent PNG works best.\nOther sizes resize to fit."));
            sizeGuide.Children.Add(Hint("Maximum: 4096 × 4096 px\nFile size: up to 10 MB"));
            Grid.SetColumn(sizeGuide, 1); imageHeader.Children.Add(sizeGuide); imagePage.Children.Add(imageHeader);
            imageName = Hint(""); imageName.TextTrimming = TextTrimming.CharacterEllipsis; imagePage.Children.Add(imageName);
            var choose = Button("Choose an image…"); choose.HorizontalAlignment = HorizontalAlignment.Left; choose.FontWeight = FontWeights.SemiBold; choose.Margin = new Thickness(0, 6, 0, 10);
            System.Windows.Automation.AutomationProperties.SetAutomationId(choose, "ChooseImageButton"); imagePage.Children.Add(choose);
            imageMode = Choice(imagePage, "Show", new[] { "Cat", "Custom", "Built-in", "None" }, settings.ImageMode);
            imagePage.Children.Add(Hint("None hides the image. Right-click the card to open Settings."));
            imagePage.Children.Add(Hint("PNG, JPEG or ICO · Your original stays untouched."));
            choose.Click += delegate
            {
                var dialog = new OpenFileDialog { Title = "Choose your card image", Filter = "Images|*.png;*.jpg;*.jpeg;*.ico", CheckFileExists = true };
                if (dialog.ShowDialog(this) != true) return;
                try { imagePath = SettingsStore.ImportImage(project, dialog.FileName); chosenName = Path.GetFileName(dialog.FileName); imageMode.SelectedItem = "Custom"; UpdateImage(); Status("Image selected. Choose Save to keep it.", false); }
                catch { Status("Choose a valid image up to 10 MB and 4096 × 4096 pixels.", true); }
            };

            var position = Page("Position");
            position.Children.Add(new TextBlock { Text = "Find a good spot", FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
            position.Children.Add(Hint("Save your changes, then drag the card to a clear spot. Release to keep its position."));
            var move = Button("Save & move card"); move.HorizontalAlignment = HorizontalAlignment.Left; move.FontWeight = FontWeights.SemiBold; move.Margin = new Thickness(0, 14, 0, 7); position.Children.Add(move);
            move.Click += delegate { if (Commit() && RepositionRequested != null) RepositionRequested(); };
            var reset = Button("Reset position"); reset.HorizontalAlignment = HorizontalAlignment.Left; position.Children.Add(reset);
            var offsets = new StackPanel(); x = Field(offsets, "From left", settings.OffsetXDip); bottom = Field(offsets, "Gap from bottom", settings.OffsetBottomDip);
            position.Children.Add(new Expander { Header = "Fine-tune position", Content = offsets, Margin = new Thickness(0, 20, 0, 9) });
            position.Children.Add(Hint("Reset position brings the card back above your profile."));
            reset.Click += delegate { x.Text = "6"; bottom.Text = "54"; Preview(); };

            message = Hint("Save keeps your changes. Cancel restores the previous look."); message.Margin = new Thickness(0, 16, 0, 12); message.MaxHeight = 48;
            Grid.SetRow(message, 2); layout.Children.Add(message);
            var footer = new Grid(); footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var preview = Button("Preview on card"); preview.HorizontalAlignment = HorizontalAlignment.Left; preview.Click += delegate { Preview(); }; footer.Children.Add(preview);
            var actions = new StackPanel { Orientation = Orientation.Horizontal }; Grid.SetColumn(actions, 1); footer.Children.Add(actions);
            var cancel = Button("Cancel"); cancel.IsCancel = true; cancel.Click += delegate { Close(); }; actions.Children.Add(cancel);
            var save = Button("Save"); save.SetResourceReference(FrameworkElement.StyleProperty, "PrimaryButton"); save.IsDefault = true; save.FontWeight = FontWeights.SemiBold; save.Margin = new Thickness(8, 0, 0, 0); save.Click += delegate { Commit(); }; actions.Children.Add(save);
            System.Windows.Automation.AutomationProperties.SetAutomationId(save, "SaveSettingsButton"); Grid.SetRow(footer, 3); layout.Children.Add(footer);
            preset.SelectionChanged += delegate { UpdateSize(); }; imageMode.SelectionChanged += delegate { UpdateImage(); };
            startup.Click += delegate { try { StartupShortcut.SetEnabled(project, startup.IsChecked == true); } catch { Status("Startup could not be changed. Check the existing shortcut or folder permissions.", true); } UpdateStartup(); };
            Closed += delegate { if (!saved && PreviewRequested != null) PreviewRequested(original.Copy()); };
            ready = true; UpdateSize(); UpdateImage(); UpdateStartup(); SelectSection(section);
        }
        public void SelectSection(string section) { foreach (TabItem tab in tabs.Items) if ((string)tab.Tag == section) { tabs.SelectedItem = tab; return; } tabs.SelectedIndex = 0; }
        void UpdateSize()
        {
            if (!ready) return; string value = (string)preset.SelectedItem;
            if (value == "Slim") { width.Text = "44"; height.Text = "152"; } else if (value == "Readable") { width.Text = "52"; height.Text = "176"; }
            dimensions.Visibility = value == "Custom" ? Visibility.Visible : Visibility.Collapsed;
        }
        void UpdateImage()
        {
            if (!ready) return; string mode = (string)imageMode.SelectedItem;
            string path = mode == "Cat" ? Path.Combine(project, "assets", "sleeping-cat.png") : mode == "Custom" ? SettingsStore.ImageFullPath(project, imagePath) : "";
            previewImage.Source = null;
            previewSymbol.Visibility = mode == "Built-in" ? Visibility.Visible : Visibility.Collapsed;
            imageName.Text = mode == "Cat" ? "Sleeping cat" : mode == "Custom" ? chosenName ?? "Your selected image" : mode == "None" ? "Image hidden — use right-click to open Settings." : "Simple icon";
            if (path.Length == 0 || !File.Exists(path)) return;
            try { var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = 300; bitmap.UriSource = new Uri(path); bitmap.EndInit(); bitmap.Freeze(); previewImage.Source = bitmap; } catch { Status("This image could not be previewed. Choose another image.", true); }
        }
        void UpdateStartup() { try { var status = StartupShortcut.Inspect(project); startup.IsChecked = status.Kind == StartupKind.Enabled; startupStatus.Text = "Applies immediately · " + status.Kind; } catch { startup.IsChecked = false; startupStatus.Text = "Startup status unavailable"; } }
        OverlaySettings Candidate()
        {
            var value = original.Copy(); value.Theme = (string)theme.SelectedItem; value.SizePreset = (string)preset.SelectedItem;
            value.WidthDip = double.Parse(width.Text, CultureInfo.InvariantCulture); value.HeightDip = double.Parse(height.Text, CultureInfo.InvariantCulture);
            value.FiveHourColor = five.Text.Trim(); value.WeeklyColor = week.Text.Trim(); value.BackgroundColor = background.Text.Trim(); value.TextColor = text.Text.Trim();
            value.AutomaticRingColours = automaticColours.IsChecked == true; value.HighRemainingColor = high.Text.Trim(); value.MediumRemainingColor = medium.Text.Trim(); value.LowRemainingColor = low.Text.Trim();
            value.OffsetXDip = double.Parse(x.Text, CultureInfo.InvariantCulture); value.OffsetBottomDip = double.Parse(bottom.Text, CultureInfo.InvariantCulture); value.ImageMode = (string)imageMode.SelectedItem; value.ImagePath = imagePath;
            if (!SettingsStore.Validate(value)) throw new ArgumentException("Check settings"); return value;
        }
        void Preview() { try { var value = Candidate(); if (PreviewRequested != null) PreviewRequested(value); Status("Preview applied. Save to keep it, or Cancel to restore it.", false); } catch { Status("Choose an image and check your size, colour or position values.", true); } }
        bool Commit() { try { var value = Candidate(); SettingsStore.Save(project, value); saved = true; if (Saved != null) Saved(value); Close(); return true; } catch { Status("Could not save. Check your image, values and folder permissions.", true); return false; } }
        void Status(string value, bool error) { message.Text = value; message.SetResourceReference(TextBlock.ForegroundProperty, error ? "Error" : "Muted"); }
        StackPanel Page(string name) { var panel = new StackPanel { Margin = new Thickness(0, 5, 0, 0) }; tabs.Items.Add(new TabItem { Header = name, Tag = name, Padding = new Thickness(14, 8, 14, 8), Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 0, 5, 0) } }); return panel; }
        static TextBlock Hint(string value) { var hint = new TextBlock { Text = value, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 6) }; hint.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); return hint; }
        static Button Button(string label) { var button = new Button { Content = label, MinWidth = 68, Padding = new Thickness(12, 7, 12, 7) }; WidgetMotion.PressFeedback(button); return button; }
        static TextBox Field(Panel parent, string label, double value)
        {
            var row = Row(parent, label); var input = new TextBox { Text = value.ToString(CultureInfo.InvariantCulture), Padding = new Thickness(6), VerticalContentAlignment = VerticalAlignment.Center };
            System.Windows.Automation.AutomationProperties.SetName(input, label); Grid.SetColumn(input, 1); row.Children.Add(input); return input;
        }
        static ComboBox Choice(Panel parent, string label, string[] choices, string selected) { var row = Row(parent, label); var choice = new ComboBox { ItemsSource = choices, SelectedItem = selected, Padding = new Thickness(6) }; System.Windows.Automation.AutomationProperties.SetName(choice, label); Grid.SetColumn(choice, 1); row.Children.Add(choice); return choice; }
        static Grid Row(Panel parent, string label) { var row = new Grid { Margin = new Thickness(0, 6, 0, 6) }; row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) }); row.Children.Add(new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center }); parent.Children.Add(row); return row; }
        static TextBox Colour(Panel parent, string label, string value, bool auto)
        {
            var row = Row(parent, label); var controls = new StackPanel { Orientation = Orientation.Horizontal }; Grid.SetColumn(controls, 1); row.Children.Add(controls);
            var field = new TextBox { Text = value, Width = 80, Padding = new Thickness(5), VerticalContentAlignment = VerticalAlignment.Center }; controls.Children.Add(field);
            var pick = Button("Pick"); pick.MinWidth = 0; pick.Padding = new Thickness(7, 5, 7, 5); pick.Margin = new Thickness(5, 0, 0, 0); controls.Children.Add(pick);
            pick.Click += delegate { using (var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true }) { try { dialog.Color = System.Drawing.ColorTranslator.FromHtml(field.Text); } catch { } if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) field.Text = "#" + dialog.Color.R.ToString("X2") + dialog.Color.G.ToString("X2") + dialog.Color.B.ToString("X2"); } };
            if (auto) { var reset = Button("Auto"); reset.MinWidth = 0; reset.Padding = new Thickness(5, 5, 5, 5); reset.Margin = new Thickness(4, 0, 0, 0); reset.Click += delegate { field.Text = "Auto"; }; controls.Children.Add(reset); }
            return field;
        }
    }
}
