using System;
using System.Drawing;
using System.Windows.Forms;

namespace MatrixScreensaver
{
    /// <summary>Configuration dialog with a live preview of the current settings.</summary>
    internal sealed class SettingsForm : Form
    {
        private static readonly (string Name, Color Color)[] Presets =
        {
            ("Matrix green", Settings.DefaultColor),
            ("Cyan", Color.FromArgb(0, 220, 255)),
            ("Blue", Color.FromArgb(40, 110, 255)),
            ("Purple", Color.FromArgb(180, 70, 255)),
            ("Red", Color.FromArgb(255, 40, 40)),
            ("Amber", Color.FromArgb(255, 170, 0)),
            ("White", Color.FromArgb(230, 230, 230)),
        };

        private readonly Settings _settings;
        private readonly MatrixView _preview;
        private readonly ToolTip _tips = new ToolTip();

        private readonly Button _colorButton;
        private readonly FlowLayoutPanel _presetPanel;
        private readonly CheckBox _rainbow, _glow, _scanlines, _hiddenImage, _allMonitors;
        private readonly TextBox _imagePath;
        private readonly FlowLayoutPanel _imagePathPanel;
        private readonly TrackBar _speed, _size, _width, _spacing, _weight, _density, _trail, _scanlineStrength, _imageStrength;
        private bool _loading;

        public SettingsForm(Settings initial)
        {
            _settings = initial.Clone();

            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont;
            Text = "Matrix Screensaver Settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);
            BackColor = SystemColors.Window;

            var root = new TableLayoutPanel { ColumnCount = 2, RowCount = 2, AutoSize = true, Dock = DockStyle.Fill };
            Controls.Add(root);

            // ---- Left column: options -------------------------------------------------------
            var options = new TableLayoutPanel { ColumnCount = 3, AutoSize = true, Margin = new Padding(0, 0, 16, 0) };
            options.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
            root.Controls.Add(options, 0, 0);

            // Colour
            _colorButton = new Button
            {
                Text = "Custom…",
                FlatStyle = FlatStyle.Flat,
                Size = new Size(90, 28),
                Margin = new Padding(0, 3, 8, 3),
            };
            _colorButton.Click += (s, e) => PickCustomColor();

            _presetPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            _presetPanel.Controls.Add(_colorButton);
            foreach (var (name, color) in Presets)
            {
                var swatch = new Button
                {
                    BackColor = color,
                    FlatStyle = FlatStyle.Flat,
                    Size = new Size(26, 26),
                    Margin = new Padding(2, 4, 2, 4),
                };
                swatch.FlatAppearance.BorderColor = Color.Gray;
                _tips.SetToolTip(swatch, name);
                swatch.Click += (s, e) => { _settings.Color = color; OnChanged(); };
                _presetPanel.Controls.Add(swatch);
            }
            AddRow(options, "Color:", _presetPanel, span: 2);

            _rainbow = new CheckBox { Text = "Rainbow (a different hue for every column)", AutoSize = true };
            _rainbow.CheckedChanged += (s, e) => { _settings.Rainbow = _rainbow.Checked; OnChanged(); };
            AddRow(options, "", _rainbow, span: 2);

            _speed = AddSlider(options, "Speed:", Settings.MinSpeed, Settings.MaxSpeed, v => $"{v}", v => _settings.Speed = v);
            _size = AddSlider(options, "Character size:", Settings.MinCharSize, Settings.MaxCharSize, v => $"{v} px", v => _settings.CharSize = v);
            _width = AddSlider(options, "Character width:", Settings.MinCharWidth, Settings.MaxCharWidth, v => $"{v}%", v => _settings.CharWidth = v);
            _spacing = AddSlider(options, "Column spacing:", Settings.MinColumnSpacing, Settings.MaxColumnSpacing, v => $"{v}%", v => _settings.ColumnSpacing = v);
            _weight = AddSlider(options, "Stroke weight:", Settings.MinStrokeWeight, Settings.MaxStrokeWeight, v => v == 0 ? "Thin" : $"{v}", v => _settings.StrokeWeight = v);
            _density = AddSlider(options, "Density:", Settings.MinDensity, Settings.MaxDensity, v => $"{v}%", v => _settings.Density = v);
            _trail = AddSlider(options, "Trail duration:", Settings.MinTrailTime, Settings.MaxTrailTime, v => $"{v / 10.0:0.0} s", v => _settings.TrailTime = v);
            _tips.SetToolTip(_trail, "How long each character stays on screen after the leading character passes.");

            _glow = new CheckBox { Text = "Bright white leading character", AutoSize = true, Margin = new Padding(3, 8, 3, 3) };
            _glow.CheckedChanged += (s, e) => { _settings.GlowHead = _glow.Checked; OnChanged(); };
            AddRow(options, "", _glow, span: 2);

            _scanlines = new CheckBox { Text = "CRT scanlines (old TV look)", AutoSize = true, Margin = new Padding(3, 8, 3, 3) };
            _scanlines.CheckedChanged += (s, e) => { _settings.Scanlines = _scanlines.Checked; OnChanged(); };
            AddRow(options, "", _scanlines, span: 2);
            _scanlineStrength = AddSlider(options, "Scanline strength:", Settings.MinScanlineStrength, Settings.MaxScanlineStrength, v => $"{v}%", v => _settings.ScanlineStrength = v);

            _hiddenImage = new CheckBox { Text = "Hidden image (subtle — stare into the rain to see it)", AutoSize = true, Margin = new Padding(3, 8, 3, 3) };
            _tips.SetToolTip(_hiddenImage, "The rain dims where the image is dark. Images fade in and out every minute or so.\nHigh-contrast pictures (a bright subject on a dark background) work best.");
            _hiddenImage.CheckedChanged += (s, e) => { _settings.HiddenImage = _hiddenImage.Checked; OnChanged(); };
            AddRow(options, "", _hiddenImage, span: 2);

            _imagePath = new TextBox { Width = 170, Margin = new Padding(0, 4, 4, 3) };
            _imagePath.HandleCreated += (s, e) => SendMessage(_imagePath.Handle, EM_SETCUEBANNER, (IntPtr)1, "Built-in skull");
            _tips.SetToolTip(_imagePath, "Leave empty to use the built-in skull.");
            _imagePath.TextChanged += (s, e) => { if (!_loading) { _settings.ImagePath = _imagePath.Text.Trim(); OnChanged(); } };
            var browseFile = new Button { Text = "Image…", AutoSize = true };
            browseFile.Click += (s, e) => BrowseImageFile();
            var browseFolder = new Button { Text = "Folder…", AutoSize = true };
            browseFolder.Click += (s, e) => BrowseImageFolder();
            _imagePathPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            var clearPath = new Button { Text = "Clear", AutoSize = true };
            _tips.SetToolTip(clearPath, "Go back to the built-in skull.");
            clearPath.Click += (s, e) => _imagePath.Text = "";
            _imagePathPanel.Controls.AddRange(new Control[] { _imagePath, browseFile, browseFolder, clearPath });
            AddRow(options, "Image or folder:", _imagePathPanel, span: 2);
            _imageStrength = AddSlider(options, "Image strength:", Settings.MinImageStrength, Settings.MaxImageStrength, v => $"{v}%", v => _settings.ImageStrength = v);

            _allMonitors = new CheckBox { Text = $"Show rain on all monitors ({Screen.AllScreens.Length} detected)", AutoSize = true };
            _tips.SetToolTip(_allMonitors, "When unchecked, only the primary monitor shows the rain and the others go black.");
            _allMonitors.CheckedChanged += (s, e) => { _settings.AllMonitors = _allMonitors.Checked; };
            AddRow(options, "", _allMonitors, span: 2);

            // ---- Right column: live preview -------------------------------------------------
            var previewBox = new GroupBox { Text = "Live preview (actual size)", Size = new Size(440, 330), Padding = new Padding(8) };
            _preview = new MatrixView(_settings) { Dock = DockStyle.Fill, HiddenImageAlwaysOn = true };
            previewBox.Controls.Add(_preview);
            root.Controls.Add(previewBox, 1, 0);

            // ---- Buttons --------------------------------------------------------------------
            var buttons = new TableLayoutPanel { ColumnCount = 5, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 0) };
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var defaults = new Button { Text = "Restore defaults", AutoSize = true };
            defaults.Click += (s, e) => LoadControls(new Settings { AllMonitors = _settings.AllMonitors, ImagePath = _settings.ImagePath });
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Size = new Size(88, 28) };
            ok.Click += (s, e) => Save();
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Size = new Size(88, 28) };
            var test = new Button { Text = "Test full screen", AutoSize = true };
            _tips.SetToolTip(test, "Run the screensaver with these settings (not saved yet). Move the mouse or press a key to return.");
            test.Click += (s, e) =>
            {
                Hide();
                ScreenSaverForm.ShowFullScreen(_settings.Clone(), () => { Show(); Activate(); });
            };
            buttons.Controls.Add(defaults, 0, 0);
            buttons.Controls.Add(test, 1, 0);
            buttons.Controls.Add(ok, 3, 0);
            buttons.Controls.Add(cancel, 4, 0);
            root.Controls.Add(buttons, 0, 1);
            root.SetColumnSpan(buttons, 2);

            AcceptButton = ok;
            CancelButton = cancel;

            LoadControls(_settings);
            ResumeLayout(false);
            PerformLayout();
        }

        private static void AddRow(TableLayoutPanel table, string label, Control control, int span = 1)
        {
            int row = table.RowCount++;
            table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 8, 3) }, 0, row);
            table.Controls.Add(control, 1, row);
            if (span > 1) table.SetColumnSpan(control, span);
        }

        private TrackBar AddSlider(TableLayoutPanel table, string label, int min, int max, Func<int, string> format, Action<int> set)
        {
            var bar = new TrackBar
            {
                Minimum = min,
                Maximum = max,
                Width = 250,
                TickStyle = TickStyle.None,
                LargeChange = Math.Max(1, (max - min) / 10),
                AutoSize = false,
                Height = 32,
            };
            var value = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 3, 3) };
            bar.ValueChanged += (s, e) =>
            {
                value.Text = format(bar.Value);
                if (_loading) return;
                set(bar.Value);
                OnChanged();
            };
            AddRow(table, label, bar);
            table.Controls.Add(value, 2, table.RowCount - 1);
            return bar;
        }

        private void LoadControls(Settings s)
        {
            _loading = true;
            try
            {
                _settings.Color = s.Color;
                _settings.Rainbow = s.Rainbow;
                _settings.GlowHead = s.GlowHead;
                _settings.AllMonitors = s.AllMonitors;
                _settings.Speed = s.Speed;
                _settings.CharSize = s.CharSize;
                _settings.CharWidth = s.CharWidth;
                _settings.ColumnSpacing = s.ColumnSpacing;
                _settings.StrokeWeight = s.StrokeWeight;
                _settings.Scanlines = s.Scanlines;
                _settings.ScanlineStrength = s.ScanlineStrength;
                _settings.HiddenImage = s.HiddenImage;
                _settings.ImagePath = s.ImagePath;
                _settings.ImageStrength = s.ImageStrength;
                _settings.Density = s.Density;
                _settings.TrailTime = s.TrailTime;

                _rainbow.Checked = s.Rainbow;
                _glow.Checked = s.GlowHead;
                _scanlines.Checked = s.Scanlines;
                SetSlider(_weight, s.StrokeWeight);
                SetSlider(_scanlineStrength, s.ScanlineStrength);
                _allMonitors.Checked = s.AllMonitors;
                SetSlider(_speed, s.Speed);
                SetSlider(_size, s.CharSize);
                SetSlider(_width, s.CharWidth);
                SetSlider(_spacing, s.ColumnSpacing);
                SetSlider(_density, s.Density);
                _hiddenImage.Checked = s.HiddenImage;
                _imagePath.Text = s.ImagePath;
                SetSlider(_imageStrength, s.ImageStrength);
                SetSlider(_trail, s.TrailTime);
            }
            finally
            {
                _loading = false;
            }
            OnChanged();
        }

        private static void SetSlider(TrackBar bar, int value)
        {
            // Nudge so ValueChanged always fires and the value label is refreshed.
            bar.Value = value == bar.Minimum ? bar.Maximum : bar.Minimum;
            bar.Value = value;
        }

        private const int EM_SETCUEBANNER = 0x1501;
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private void BrowseImageFile()
        {
            using (var dialog = new OpenFileDialog
            {
                Title = "Choose a hidden image",
                Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff|All files|*.*",
            })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK) _imagePath.Text = dialog.FileName;
            }
        }

        private void BrowseImageFolder()
        {
            using (var dialog = new FolderBrowserDialog { Description = "Choose a folder of images — one is picked at random each time." })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK) _imagePath.Text = dialog.SelectedPath;
            }
        }

        private void PickCustomColor()
        {
            using (var dialog = new ColorDialog { Color = _settings.Color, FullOpen = true, AnyColor = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                _settings.Color = dialog.Color;
                OnChanged();
            }
        }

        private void OnChanged()
        {
            if (_loading) return;
            _colorButton.BackColor = _settings.Color;
            _colorButton.ForeColor = _settings.Color.GetBrightness() > 0.55f ? Color.Black : Color.White;
            _presetPanel.Enabled = !_settings.Rainbow;
            _scanlineStrength.Enabled = _settings.Scanlines;
            _imagePathPanel.Enabled = _imageStrength.Enabled = _settings.HiddenImage;
            _preview.ApplySettings(_settings);
        }

        private void Save()
        {
            try
            {
                _settings.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not save settings:\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.None;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tips.Dispose();
            base.Dispose(disposing);
        }
    }
}
