using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TripleZeroLabs.Os2;
using WinForms = System.Windows.Forms;

namespace MatrixScreensaver
{
    /// <summary>Settings dialog, themed with TripleZeroLabs.Os2, with a live preview of the rain.</summary>
    internal sealed class SettingsWindow : Window
    {
        private static readonly (string Name, System.Drawing.Color Color)[] Presets =
        {
            ("Matrix green", Settings.DefaultColor),
            ("Cyan", System.Drawing.Color.FromArgb(0, 220, 255)),
            ("Blue", System.Drawing.Color.FromArgb(40, 110, 255)),
            ("Purple", System.Drawing.Color.FromArgb(180, 70, 255)),
            ("Red", System.Drawing.Color.FromArgb(255, 40, 40)),
            ("Amber", System.Drawing.Color.FromArgb(255, 170, 0)),
            ("White", System.Drawing.Color.FromArgb(230, 230, 230)),
        };

        private readonly Settings _settings;
        private readonly MatrixPreview _preview;

        private readonly Border _colorSwatch;
        private readonly StackPanel _colorPanel;
        private readonly CheckBox _rainbow, _glow, _scanlines, _hiddenImage, _allMonitors;
        private readonly TextBox _imagePath;
        private readonly TextBlock _imagePathHint;
        private readonly Panel _imagePathPanel;
        private readonly System.Collections.Generic.Dictionary<string, RadioButton> _imageSources =
            new System.Collections.Generic.Dictionary<string, RadioButton>();
        private FrameworkElement _imageSourceRow;
        private readonly Slider _speed, _size, _width, _spacing, _weight, _density, _trail, _scanlineStrength, _imageStrength;
        private bool _loading;

        public SettingsWindow(Settings initial)
        {
            _settings = initial.Clone();

            Title = "Matrix Screensaver Settings";
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Os2Theme.Apply(this);
            SetResourceReference(BackgroundProperty, Os2ResourceKey.Brush.Background);

            var options = new StackPanel { Width = 520 };

            options.Children.Add(Heading("Appearance"));

            _colorSwatch = new Border
            {
                Width = 46,
                Height = 26,
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand,
                ToolTip = "Pick a custom color",
                Background = Brush(_settings.Color),
            };
            _colorSwatch.SetResourceReference(Border.BorderBrushProperty, Os2ResourceKey.Brush.Border);
            _colorSwatch.MouseLeftButtonUp += (s, e) => PickCustomColor();

            _colorPanel = new StackPanel { Orientation = Orientation.Horizontal };
            _colorPanel.Children.Add(_colorSwatch);
            foreach (var (name, color) in Presets)
            {
                var swatch = new Border
                {
                    Width = 26,
                    Height = 26,
                    CornerRadius = new CornerRadius(4),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 4, 0),
                    Cursor = Cursors.Hand,
                    ToolTip = name,
                    Background = Brush(color),
                };
                swatch.SetResourceReference(Border.BorderBrushProperty, Os2ResourceKey.Brush.Border);
                swatch.MouseLeftButtonUp += (s, e) => { _settings.Color = color; OnChanged(); };
                _colorPanel.Children.Add(swatch);
            }
            options.Children.Add(Row("Color:", _colorPanel));

            _rainbow = Check("Rainbow (a different hue for every column)", v => _settings.Rainbow = v);
            options.Children.Add(_rainbow);

            _speed = AddSlider(options, "Speed:", Settings.MinSpeed, Settings.MaxSpeed, v => $"{v}", v => _settings.Speed = v);
            _size = AddSlider(options, "Character size:", Settings.MinCharSize, Settings.MaxCharSize, v => $"{v} px", v => _settings.CharSize = v);
            _width = AddSlider(options, "Character width:", Settings.MinCharWidth, Settings.MaxCharWidth, v => $"{v}%", v => _settings.CharWidth = v);
            _spacing = AddSlider(options, "Column spacing:", Settings.MinColumnSpacing, Settings.MaxColumnSpacing, v => $"{v}%", v => _settings.ColumnSpacing = v);
            _weight = AddSlider(options, "Stroke weight:", Settings.MinStrokeWeight, Settings.MaxStrokeWeight, v => v == 0 ? "Thin" : $"{v}", v => _settings.StrokeWeight = v);
            _density = AddSlider(options, "Density:", Settings.MinDensity, Settings.MaxDensity, v => $"{v}%", v => _settings.Density = v);
            _trail = AddSlider(options, "Trail duration:", Settings.MinTrailTime, Settings.MaxTrailTime, v => $"{v / 10.0:0.0} s", v => _settings.TrailTime = v,
                "How long each character stays on screen after the leading character passes.");

            options.Children.Add(Heading("Effects"));
            _glow = Check("Bright white leading character", v => _settings.GlowHead = v);
            options.Children.Add(_glow);
            _scanlines = Check("CRT scanlines (old TV look)", v => _settings.Scanlines = v);
            options.Children.Add(_scanlines);
            _scanlineStrength = AddSlider(options, "Scanline strength:", Settings.MinScanlineStrength, Settings.MaxScanlineStrength, v => $"{v}%", v => _settings.ScanlineStrength = v);

            options.Children.Add(Heading("Hidden image"));
            _hiddenImage = Check("Hidden image (subtle — stare into the rain to see it)", v => _settings.HiddenImage = v);
            _hiddenImage.ToolTip = "The rain dims where the image is dark. Images fade in and out every minute or so.\n"
                + "High-contrast pictures (a bright subject on a dark background) work best.";
            options.Children.Add(_hiddenImage);

            _imagePath = new TextBox { Width = 130, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Used when Image is set to Custom." };
            _imagePath.TextChanged += (s, e) =>
            {
                _imagePathHint.Visibility = _imagePath.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                if (_loading) return;
                _settings.ImagePath = _imagePath.Text.Trim();
                OnChanged();
            };
            _imagePathHint = new TextBlock
            {
                Text = "Pick a file or folder",
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
            };
            _imagePathHint.SetResourceReference(TextBlock.ForegroundProperty, Os2ResourceKey.Brush.TextDisabled);

            var sourcePanel = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var (key, label) in new[]
            {
                (Settings.Source1730, "1730"),
                (Settings.SourceSkull, "Skull"),
                (Settings.SourceAlien, "Alien"),
                (Settings.SourceTripleZero, "Triple Zero"),
                (Settings.SourceCustom, "Custom…"),
            })
            {
                string source = key;
                var radio = new RadioButton
                {
                    Content = label,
                    GroupName = "HiddenImageSource",
                    Margin = new Thickness(0, 0, 14, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                radio.Checked += (s, e) =>
                {
                    if (_loading) return;
                    _settings.ImageSource = source;
                    OnChanged();
                };
                _imageSources[source] = radio;
                sourcePanel.Children.Add(radio);
            }
            _imageSourceRow = Row("Image:", sourcePanel);
            options.Children.Add(_imageSourceRow);

            _imagePathPanel = new StackPanel { Orientation = Orientation.Horizontal };
            _imagePathPanel.Children.Add(new Grid { Children = { _imagePath, _imagePathHint }, Margin = new Thickness(0, 0, 6, 0) });
            _imagePathPanel.Children.Add(SmallButton("Image…", BrowseImageFile, Os2ResourceKey.Button.Secondary));
            _imagePathPanel.Children.Add(SmallButton("Folder…", BrowseImageFolder, Os2ResourceKey.Button.Secondary));
            _imagePathPanel.Children.Add(SmallButton("Clear", () => _imagePath.Text = "", Os2ResourceKey.Button.Flat, "Go back to the built-in skull."));
            options.Children.Add(Row("Custom file/folder:", _imagePathPanel));

            _imageStrength = AddSlider(options, "Image strength:", Settings.MinImageStrength, Settings.MaxImageStrength, v => $"{v}%", v => _settings.ImageStrength = v);

            options.Children.Add(Heading("Monitors"));
            _allMonitors = Check($"Show rain on all monitors ({WinForms.Screen.AllScreens.Length} detected)", v => _settings.AllMonitors = v);
            _allMonitors.ToolTip = "When unchecked, only the primary monitor shows the rain and the others go black.";
            options.Children.Add(_allMonitors);

            // ---- Preview ---------------------------------------------------------------------
            // Explicit size: an Image with no Source measures to zero, so the preview
            // would never get a size to build its bitmap from.
            _preview = new MatrixPreview(_settings) { Width = 438, Height = 328 };
            var previewCard = new Border
            {
                Width = 440,
                Height = 330,
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Background = Brushes.Black,
                Margin = new Thickness(20, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
                ClipToBounds = true,
                Child = _preview,
            };
            previewCard.SetResourceReference(Border.BorderBrushProperty, Os2ResourceKey.Brush.Border);

            var previewColumn = new StackPanel();
            previewColumn.Children.Add(new TextBlock
            {
                Text = "Live preview (actual size)",
                Margin = new Thickness(20, 0, 0, 6),
            });
            previewColumn.Children.Add(previewCard);

            // ---- Buttons ---------------------------------------------------------------------
            var leftButtons = new StackPanel { Orientation = Orientation.Horizontal };
            leftButtons.Children.Add(SmallButton("Restore defaults",
                () => LoadControls(new Settings { AllMonitors = _settings.AllMonitors, ImagePath = _settings.ImagePath }),
                Os2ResourceKey.Button.Flat));
            leftButtons.Children.Add(SmallButton("Test full screen", TestFullScreen, Os2ResourceKey.Button.Secondary,
                "Run the screensaver with these settings (not saved yet). Move the mouse or press a key to return."));

            var rightButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = SmallButton("Cancel", Close, Os2ResourceKey.Button.Flat);
            cancel.IsCancel = true;
            cancel.MinWidth = 88;
            var ok = SmallButton("OK", Save, null);
            ok.IsDefault = true;
            ok.MinWidth = 88;
            rightButtons.Children.Add(cancel);
            rightButtons.Children.Add(ok);

            var buttonRow = new Grid { Margin = new Thickness(0, 20, 0, 0) };
            buttonRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(rightButtons, 1);
            buttonRow.Children.Add(leftButtons);
            buttonRow.Children.Add(rightButtons);

            var body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(previewColumn, 1);
            body.Children.Add(options);
            body.Children.Add(previewColumn);

            var root = new Grid { Margin = new Thickness(20) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(buttonRow, 1);
            root.Children.Add(body);
            root.Children.Add(buttonRow);
            Content = root;

            LoadControls(_settings);
        }

        // ---- Control builders ----------------------------------------------------------------

        private TextBlock Heading(string text)
        {
            var heading = new TextBlock { Text = text, Margin = new Thickness(0, 16, 0, 8) };
            heading.SetResourceReference(StyleProperty, Os2ResourceKey.Text.Heading);
            return heading;
        }

        private static FrameworkElement Row(string label, FrameworkElement content)
        {
            var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(content, 1);
            grid.Children.Add(text);
            grid.Children.Add(content);
            return grid;
        }

        private CheckBox Check(string text, Action<bool> set)
        {
            var box = new CheckBox { Content = text, Margin = new Thickness(0, 6, 0, 6) };
            box.Checked += (s, e) => { set(true); OnChanged(); };
            box.Unchecked += (s, e) => { set(false); OnChanged(); };
            return box;
        }

        private Button SmallButton(string text, Action click, string styleKey, string tooltip = null)
        {
            var button = new Button { Content = text, Margin = new Thickness(0, 0, 8, 0), ToolTip = tooltip };
            if (styleKey != null) button.SetResourceReference(StyleProperty, styleKey);
            button.Click += (s, e) => click();
            return button;
        }

        private Slider AddSlider(Panel parent, string label, int min, int max, Func<int, string> format, Action<int> set, string tooltip = null)
        {
            var slider = new Slider
            {
                Minimum = min,
                Maximum = max,
                IsSnapToTickEnabled = true,
                TickFrequency = 1,
                Width = 230,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = tooltip,
            };
            var value = new TextBlock { Width = 60, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            value.SetResourceReference(TextBlock.ForegroundProperty, Os2ResourceKey.Brush.TextSecondary);

            slider.ValueChanged += (s, e) =>
            {
                int v = (int)Math.Round(slider.Value);
                value.Text = format(v);
                if (_loading) return;
                set(v);
                OnChanged();
            };

            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(slider);
            panel.Children.Add(value);
            parent.Children.Add(Row(label, panel));
            return slider;
        }

        // ---- Behaviour -----------------------------------------------------------------------

        private void LoadControls(Settings s)
        {
            _loading = true;
            try
            {
                _settings.Color = s.Color;
                _settings.Rainbow = s.Rainbow;
                _settings.Speed = s.Speed;
                _settings.CharSize = s.CharSize;
                _settings.CharWidth = s.CharWidth;
                _settings.ColumnSpacing = s.ColumnSpacing;
                _settings.StrokeWeight = s.StrokeWeight;
                _settings.Density = s.Density;
                _settings.TrailTime = s.TrailTime;
                _settings.GlowHead = s.GlowHead;
                _settings.Scanlines = s.Scanlines;
                _settings.ScanlineStrength = s.ScanlineStrength;
                _settings.HiddenImage = s.HiddenImage;
                _settings.ImagePath = s.ImagePath;
                _settings.ImageSource = s.ImageSource;
                _settings.ImageStrength = s.ImageStrength;
                _settings.AllMonitors = s.AllMonitors;

                _rainbow.IsChecked = s.Rainbow;
                _glow.IsChecked = s.GlowHead;
                _scanlines.IsChecked = s.Scanlines;
                _hiddenImage.IsChecked = s.HiddenImage;
                _allMonitors.IsChecked = s.AllMonitors;
                _imagePath.Text = s.ImagePath ?? "";
                _imageSources[_imageSources.ContainsKey(s.ImageSource ?? "") ? s.ImageSource : Settings.Source1730].IsChecked = true;

                SetSlider(_speed, s.Speed);
                SetSlider(_size, s.CharSize);
                SetSlider(_width, s.CharWidth);
                SetSlider(_spacing, s.ColumnSpacing);
                SetSlider(_weight, s.StrokeWeight);
                SetSlider(_density, s.Density);
                SetSlider(_trail, s.TrailTime);
                SetSlider(_scanlineStrength, s.ScanlineStrength);
                SetSlider(_imageStrength, s.ImageStrength);
            }
            finally
            {
                _loading = false;
            }
            OnChanged();
        }

        private static void SetSlider(Slider slider, int value)
        {
            // Nudge so ValueChanged always fires and the value label refreshes.
            slider.Value = value == slider.Minimum ? slider.Maximum : slider.Minimum;
            slider.Value = value;
        }

        private void OnChanged()
        {
            if (_loading) return;
            _colorSwatch.Background = Brush(_settings.Color);
            _colorPanel.IsEnabled = !_settings.Rainbow;
            _scanlineStrength.IsEnabled = _settings.Scanlines;
            _imageSourceRow.IsEnabled = _imageStrength.IsEnabled = _settings.HiddenImage;
            _imagePathPanel.IsEnabled = _settings.HiddenImage && _settings.ImageSource == Settings.SourceCustom;
            _preview.ApplySettings(_settings);
        }

        private void PickCustomColor()
        {
            if (_settings.Rainbow) return;
            using (var dialog = new WinForms.ColorDialog { Color = _settings.Color, FullOpen = true, AnyColor = true })
            {
                if (dialog.ShowDialog() != WinForms.DialogResult.OK) return;
                _settings.Color = dialog.Color;
                OnChanged();
            }
        }

        private void BrowseImageFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Choose a hidden image",
                Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff|All files|*.*",
            };
            if (dialog.ShowDialog(this) == true) SetCustomPath(dialog.FileName);
        }

        private void BrowseImageFolder()
        {
            using (var dialog = new WinForms.FolderBrowserDialog { Description = "Choose a folder of images — one is picked at random each time." })
            {
                if (dialog.ShowDialog() == WinForms.DialogResult.OK) SetCustomPath(dialog.SelectedPath);
            }
        }

        private void SetCustomPath(string path)
        {
            _imagePath.Text = path;
            _imageSources[Settings.SourceCustom].IsChecked = true;
        }

        private void TestFullScreen()
        {
            Hide();
            ScreenSaverForm.ShowFullScreen(_settings.Clone(), () => { Show(); Activate(); });
        }

        private void Save()
        {
            try
            {
                _settings.Save();
                Close();
            }
            catch (Exception ex)
            {
                Os2MessageBox.Show(this, "Could not save settings:\n" + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static SolidColorBrush Brush(System.Drawing.Color color) =>
            new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
    }
}
