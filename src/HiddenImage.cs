using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MatrixScreensaver
{
    /// <summary>
    /// Picks images (the built-in skull, a file, or a random file from a folder), converts each
    /// into a per-cell brightness map, and slowly fades it in and out. The rain dims where the
    /// map is dark and lingers where it is bright, so a picture emerges only if you stare.
    /// </summary>
    internal sealed class HiddenImage
    {
        /// <summary>Prefix marking a "file name" that is really an embedded resource.</summary>
        private const string BuiltInPrefix = "built-in:";

        private static readonly string[] Extensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff" };

        private const float FadeInSeconds = 10f;
        private const float HoldSeconds = 25f;
        private const float FadeOutSeconds = 10f;

        private enum Phase { Waiting, FadingIn, Holding, FadingOut }

        private readonly string[] _files;
        private readonly int _cols, _rows, _cellW, _cellH;
        private readonly bool _alwaysOn;
        private readonly Random _rng = new Random();

        private Task<float[]> _loading;
        private Phase _phase = Phase.Waiting;
        private float _phaseTime, _phaseLength;

        /// <summary>Per cell: -1 (dark in the image) .. +1 (bright). Null until an image is loaded.</summary>
        public float[] Map { get; private set; }

        /// <summary>0..1 fade envelope for the current image.</summary>
        public float Amount { get; private set; }

        public static bool HasImages(Settings settings) => FindFiles(settings).Length > 0;

        public HiddenImage(Settings settings, int cols, int rows, int cellW, int cellH, bool alwaysOn)
        {
            _files = FindFiles(settings);
            _cols = cols;
            _rows = rows;
            _cellW = cellW;
            _cellH = cellH;
            _alwaysOn = alwaysOn;
            _phaseLength = 3f + (float)_rng.NextDouble() * 5f; // first reveal comes fairly soon
            StartLoading();
        }

        public void Update(float dt)
        {
            if (_alwaysOn)
            {
                if (Map == null && TryTakeLoaded(out var first)) Map = first;
                Amount = Map == null ? 0f : 1f;
                return;
            }

            _phaseTime += dt;
            switch (_phase)
            {
                case Phase.Waiting:
                    Amount = 0f;
                    if (_phaseTime >= _phaseLength && TryTakeLoaded(out var next))
                    {
                        Map = next;
                        Enter(Phase.FadingIn, FadeInSeconds);
                        StartLoading(); // prepare the following image in the background
                    }
                    break;
                case Phase.FadingIn:
                    Amount = SmoothStep(_phaseTime / _phaseLength);
                    if (_phaseTime >= _phaseLength) Enter(Phase.Holding, HoldSeconds);
                    break;
                case Phase.Holding:
                    Amount = 1f;
                    if (_phaseTime >= _phaseLength) Enter(Phase.FadingOut, FadeOutSeconds);
                    break;
                case Phase.FadingOut:
                    Amount = 1f - SmoothStep(_phaseTime / _phaseLength);
                    if (_phaseTime >= _phaseLength) Enter(Phase.Waiting, 20f + (float)_rng.NextDouble() * 40f);
                    break;
            }
        }

        private void Enter(Phase phase, float length)
        {
            _phase = phase;
            _phaseTime = 0f;
            _phaseLength = length;
        }

        private void StartLoading()
        {
            if (_files.Length == 0) return;
            string file = _files[_rng.Next(_files.Length)];
            int cols = _cols, rows = _rows, cellW = _cellW, cellH = _cellH;
            _loading = Task.Run(() => BuildMap(file, cols, rows, cellW, cellH));
        }

        private bool TryTakeLoaded(out float[] map)
        {
            map = null;
            if (_loading == null || !_loading.IsCompleted) return false;
            if (_loading.Status == TaskStatus.RanToCompletion) map = _loading.Result;
            _loading = null;
            if (map == null) StartLoading(); // unreadable file: try another one
            return map != null;
        }

        private static string[] FindFiles(Settings settings)
        {
            try
            {
                if (settings.ImageSource != Settings.SourceCustom)
                    return new[] { BuiltInPrefix + settings.ImageSource };

                string path = settings.ImagePath;
                if (string.IsNullOrWhiteSpace(path)) return new string[0];
                if (File.Exists(path)) return new[] { path };
                if (Directory.Exists(path))
                {
                    return Directory.EnumerateFiles(path)
                        .Where(f => Extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                        .ToArray();
                }
            }
            catch (Exception)
            {
                // Inaccessible path: behave as if no images were chosen.
            }
            return new string[0];
        }

        /// <summary>Fits the image inside the screen, samples it once per character cell and
        /// normalises contrast. Cells outside the image take the value of the image's border,
        /// with a feathered transition, so there is no visible frame.</summary>
        private static float[] BuildMap(string file, int cols, int rows, int cellW, int cellH)
        {
            try
            {
                using (var stream = file.StartsWith(BuiltInPrefix)
                    ? OpenBuiltIn(file.Substring(BuiltInPrefix.Length))
                    : new MemoryStream(File.ReadAllBytes(file)))
                using (var image = Image.FromStream(stream))
                {
                    ApplyExifRotation(image);

                    // Fit the whole image on screen, in pixel space, then convert to cells.
                    float screenW = cols * cellW, screenH = rows * cellH;
                    float scale = Math.Min(screenW / image.Width, screenH / image.Height);
                    float fitW = image.Width * scale, fitH = image.Height * scale;
                    int c0 = (int)Math.Round((screenW - fitW) / 2 / cellW);
                    int r0 = (int)Math.Round((screenH - fitH) / 2 / cellH);
                    int w = Math.Max(1, (int)Math.Round(fitW / cellW));
                    int h = Math.Max(1, (int)Math.Round(fitH / cellH));

                    var lum = new float[w * h];
                    using (var small = new Bitmap(w, h, PixelFormat.Format32bppArgb))
                    {
                        using (var g = Graphics.FromImage(small))
                        using (var attributes = new ImageAttributes())
                        {
                            attributes.SetWrapMode(WrapMode.TileFlipXY);
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.Clear(Color.Black);
                            g.DrawImage(image, new Rectangle(0, 0, w, h), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
                        }
                        for (int y = 0; y < h; y++)
                            for (int x = 0; x < w; x++)
                            {
                                var p = small.GetPixel(x, y);
                                lum[y * w + x] = (0.299f * p.R + 0.587f * p.G + 0.114f * p.B) / 255f;
                            }
                    }

                    // Stretch contrast between the 3rd and 97th percentiles so dim photos still register.
                    var sorted = lum.OrderBy(v => v).ToArray();
                    float lo = sorted[(int)(sorted.Length * 0.03)];
                    float hi = sorted[(int)(sorted.Length * 0.97)];
                    float range = Math.Max(0.05f, hi - lo);

                    // Everything outside the picture takes the value of the picture's own border,
                    // so a subject on a dark background blends seamlessly into the rest of the screen.
                    double borderSum = 0;
                    int borderCount = 0;
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                            if (x == 0 || y == 0 || x == w - 1 || y == h - 1)
                            {
                                borderSum += Clamp01((lum[y * w + x] - lo) / range) * 2f - 1f;
                                borderCount++;
                            }
                    float background = (float)(borderSum / borderCount);

                    var map = new float[cols * rows];
                    for (int i = 0; i < map.Length; i++) map[i] = background;
                    float feather = Math.Max(2f, Math.Min(w, h) * 0.12f);
                    for (int y = 0; y < h; y++)
                    {
                        int row = r0 + y;
                        if (row < 0 || row >= rows) continue;
                        for (int x = 0; x < w; x++)
                        {
                            int col = c0 + x;
                            if (col < 0 || col >= cols) continue;
                            float v = Clamp01((lum[y * w + x] - lo) / range) * 2f - 1f;
                            float edge = Math.Min(Math.Min(x + 0.5f, w - x - 0.5f), Math.Min(y + 0.5f, h - y - 0.5f));
                            float t = SmoothStep(edge / feather);
                            map[row * cols + col] = background + (v - background) * t;
                        }
                    }
                    return map;
                }
            }
            catch (Exception)
            {
                return null; // not an image, corrupt, or unreadable
            }
        }

        private static Stream OpenBuiltIn(string name)
        {
            var copy = new MemoryStream();
            using (var resource = typeof(HiddenImage).Assembly.GetManifestResourceStream(name + ".png"))
                resource.CopyTo(copy);
            copy.Position = 0;
            return copy;
        }

        private static void ApplyExifRotation(Image image)
        {
            const int OrientationId = 0x0112;
            if (!image.PropertyIdList.Contains(OrientationId)) return;
            switch (image.GetPropertyItem(OrientationId).Value[0])
            {
                case 3: image.RotateFlip(RotateFlipType.Rotate180FlipNone); break;
                case 6: image.RotateFlip(RotateFlipType.Rotate90FlipNone); break;
                case 8: image.RotateFlip(RotateFlipType.Rotate270FlipNone); break;
            }
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        private static float SmoothStep(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
