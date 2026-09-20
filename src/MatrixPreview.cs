using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace MatrixScreensaver
{
    /// <summary>WPF live preview of the rain, drawn straight into a <see cref="BitmapSurface"/>.
    /// A plain drawing element rather than an <see cref="Image"/>: an Image with no source
    /// arranges to zero, so it could never report the size the bitmap needs to be.</summary>
    internal sealed class MatrixPreview : FrameworkElement
    {
        private readonly DispatcherTimer _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        private Settings _settings;
        private BitmapSurface _surface;
        private MatrixRain _rain;
        private HiddenImage _hiddenImage;
        private double _lastTime;

        public MatrixPreview(Settings settings)
        {
            _settings = settings.Clone();
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);

            Loaded += (s, e) => { Rebuild(); _timer.Start(); };
            Unloaded += (s, e) => _timer.Stop();
            SizeChanged += (s, e) => Rebuild();
            _timer.Tick += OnTick;
        }

        public void ApplySettings(Settings settings)
        {
            bool sizeChanged = settings.CharSize != _settings.CharSize || settings.CharWidth != _settings.CharWidth
                || settings.ColumnSpacing != _settings.ColumnSpacing || settings.StrokeWeight != _settings.StrokeWeight;
            bool imageChanged = settings.HiddenImage != _settings.HiddenImage || settings.ImagePath != _settings.ImagePath;
            _settings = settings.Clone();

            if (sizeChanged || _rain == null) Rebuild();
            else
            {
                _rain.ApplySettings(_settings);
                if (imageChanged) CreateHiddenImage();
            }
        }

        private double DpiScale =>
            PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;

        private void Rebuild()
        {
            _rain = null;
            _surface = null;
            if (ActualWidth <= 0 || ActualHeight <= 0) return;

            double scale = DpiScale;
            _surface = new BitmapSurface((int)Math.Round(ActualWidth * scale), (int)Math.Round(ActualHeight * scale), scale);

            int fontPx = Math.Max(6, (int)Math.Round(_settings.CharSize * scale));
            _rain = new MatrixRain(_settings, _surface, fontPx);
            _rain.Prewarm();
            CreateHiddenImage();
            _lastTime = _clock.Elapsed.TotalSeconds;
            Draw();
            InvalidateVisual();
        }

        private void CreateHiddenImage()
        {
            // Always-on in the preview so the strength slider can be judged.
            _hiddenImage = _rain != null && _settings.HiddenImage && HiddenImage.HasImages(_settings.ImagePath)
                ? new HiddenImage(_settings.ImagePath, _rain.Columns, _rain.Rows, _rain.CellWidth, _rain.CellHeight, alwaysOn: true)
                : null;
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (_rain == null) return;

            double now = _clock.Elapsed.TotalSeconds;
            float dt = (float)Math.Min(0.1, now - _lastTime);
            _lastTime = now;

            _hiddenImage?.Update(dt);
            _rain.SetHiddenImage(_hiddenImage?.Map, _hiddenImage?.Amount ?? 0f);
            _rain.Update(dt);
            Draw();
        }

        protected override void OnRender(DrawingContext dc)
        {
            var area = new Rect(RenderSize);
            dc.DrawRectangle(Brushes.Black, null, area);
            if (_surface != null) dc.DrawImage(_surface.Bitmap, area);
        }

        private void Draw()
        {
            _surface.BeginWrite();
            try { _rain.Render(); }
            finally { _surface.EndWrite(); }
        }
    }
}
