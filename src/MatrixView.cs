using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace MatrixScreensaver
{
    /// <summary>A control that animates the digital rain. Used full-screen, in the tiny
    /// Control Panel preview, and as the live preview in the settings dialog.</summary>
    internal sealed class MatrixView : Control
    {
        private readonly float _sizeMultiplier;
        private readonly Timer _timer = new Timer { Interval = 15 };
        private readonly Stopwatch _clock = new Stopwatch();
        private Settings _settings;
        private DibSurface _surface;
        private MatrixRain _rain;
        private HiddenImage _hiddenImage;
        private double _lastTime;

        /// <summary>Show the hidden image continuously instead of fading it in and out
        /// (used by the settings preview so the strength can be judged).</summary>
        public bool HiddenImageAlwaysOn { get; set; }

        public MatrixView(Settings settings, float sizeMultiplier = 1f)
        {
            _settings = settings.Clone();
            _sizeMultiplier = sizeMultiplier;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);
            SetStyle(ControlStyles.Selectable, false);
            BackColor = Color.Black;
            _timer.Tick += OnTick;
        }

        public void ApplySettings(Settings settings)
        {
            bool sizeChanged = settings.CharSize != _settings.CharSize || settings.CharWidth != _settings.CharWidth
                || settings.ColumnSpacing != _settings.ColumnSpacing || settings.StrokeWeight != _settings.StrokeWeight;
            bool imageChanged = settings.HiddenImage != _settings.HiddenImage || settings.ImagePath != _settings.ImagePath
                || settings.ImageSource != _settings.ImageSource;
            _settings = settings.Clone();
            if (sizeChanged || _rain == null) Rebuild();
            else
            {
                _rain.ApplySettings(_settings);
                if (imageChanged) CreateHiddenImage();
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Rebuild();
            _clock.Start();
            _timer.Start();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            _timer.Stop();
            base.OnHandleDestroyed(e);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (IsHandleCreated) Rebuild();
        }

        private void Rebuild()
        {
            _surface?.Dispose();
            _surface = null;
            _rain = null;

            var size = ClientSize;
            if (size.Width <= 0 || size.Height <= 0) return;

            int fontPx = Math.Max(6, (int)Math.Round(_settings.CharSize * Native.GetDpiScale(Handle) * _sizeMultiplier));
            _surface = new DibSurface(size.Width, size.Height);
            _rain = new MatrixRain(_settings, _surface, fontPx);
            _rain.Prewarm();
            CreateHiddenImage();
            _lastTime = _clock.Elapsed.TotalSeconds;
            Invalidate();
        }

        private void CreateHiddenImage()
        {
            _hiddenImage = null;
            if (_rain == null || !_settings.HiddenImage || _sizeMultiplier < 1f) return; // too small to see in the mini preview
            if (!HiddenImage.HasImages(_settings)) return;
            _hiddenImage = new HiddenImage(_settings, _rain.Columns, _rain.Rows, _rain.CellWidth, _rain.CellHeight, HiddenImageAlwaysOn);
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (_rain == null) return;

            double now = _clock.Elapsed.TotalSeconds;
            float dt = (float)Math.Min(0.1, now - _lastTime); // don't jump after a stall
            _lastTime = now;

            _hiddenImage?.Update(dt);
            _rain.SetHiddenImage(_hiddenImage?.Map, _hiddenImage?.Amount ?? 0f);
            _rain.Update(dt);
            _rain.Render();

            IntPtr dc = Native.GetDC(Handle);
            try { _surface.BlitTo(dc); }
            finally { Native.ReleaseDC(Handle, dc); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_surface == null)
            {
                e.Graphics.Clear(Color.Black);
                return;
            }
            IntPtr dc = e.Graphics.GetHdc();
            try { _surface.BlitTo(dc); }
            finally { e.Graphics.ReleaseHdc(dc); }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Dispose();
                _surface?.Dispose();
                _surface = null;
            }
            base.Dispose(disposing);
        }
    }
}
