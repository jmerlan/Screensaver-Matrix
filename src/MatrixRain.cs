using System;
using System.Collections.Generic;
using System.Drawing;

namespace MatrixScreensaver
{
    /// <summary>
    /// Simulates the falling code on a character grid and renders it into a <see cref="DibSurface"/>.
    /// Only cells whose appearance changed since the previous frame are redrawn, which keeps
    /// full-screen 4K rendering cheap.
    /// </summary>
    internal sealed unsafe class MatrixRain
    {
        private const int Levels = 32;               // brightness steps for trail cells (1..Levels)
        private const byte HeadLevel = Levels + 1;   // special level for the leading character
        private const byte Unknown = byte.MaxValue;  // forces a redraw

        private sealed class Drop
        {
            public int Column;
            public float Head;      // fractional row of the leading character
            public float Speed;     // rows per second
            public int LastRow = -1;
        }

        private readonly IPixelSurface _surface;
        private readonly int _stride;
        private readonly GlyphAtlas _atlas;
        private readonly int _cols, _rows;
        private readonly Random _rng = new Random();

        // Per-cell state (row-major).
        private readonly byte[] _glyph;
        private readonly float[] _bright;  // 0..1
        private readonly float[] _fade;    // brightness lost per second
        private readonly bool[] _isHead;
        private readonly bool[] _dirty;
        private readonly byte[] _drawnLevel;

        private readonly List<Drop> _drops = new List<Drop>();
        private readonly Drop[] _lastDropInColumn;

        private readonly int _fontPx;
        private readonly int[] _rowScale; // per pixel row, 0..256 brightness multiplier (CRT scanlines)
        private int[][] _palettes; // per column: colour for each level
        private float _baseSpeed, _spawnRate, _trailSeconds, _flickerAccumulator;

        // Hidden image: per-cell -1..+1 map and how strongly it currently shows (0..1).
        private float[] _imageMap;
        private float _imageStrength, _imageInfluence;

        public int Columns => _cols;
        public int Rows => _rows;
        public int CellWidth => _atlas.CellW;
        public int CellHeight => _atlas.CellH;

        public MatrixRain(Settings settings, IPixelSurface surface, int fontPx)
        {
            _surface = surface;
            _stride = surface.Stride;
            _fontPx = fontPx;
            _rowScale = new int[surface.Height];
            // Stroke weight 10 adds an outline ~1/8 of the character size.
            float strokePx = settings.StrokeWeight * fontPx * 0.0125f;
            _atlas = new GlyphAtlas(fontPx, settings.CharWidth / 100f, settings.ColumnSpacing / 100f, strokePx);
            _cols = (surface.Width + _atlas.CellW - 1) / _atlas.CellW;
            _rows = (surface.Height + _atlas.CellH - 1) / _atlas.CellH;

            int cells = _cols * _rows;
            _glyph = new byte[cells];
            _bright = new float[cells];
            _fade = new float[cells];
            _isHead = new bool[cells];
            _dirty = new bool[cells];
            _drawnLevel = new byte[cells];
            _lastDropInColumn = new Drop[_cols];

            ApplySettings(settings);
        }

        /// <summary>Applies colour/speed/density/trail changes without resetting the animation.</summary>
        public void ApplySettings(Settings s)
        {
            _baseSpeed = 4f + s.Speed * 0.45f;                  // ~4.5 .. 49 rows per second
            _trailSeconds = s.TrailTime / 10f;
            _imageStrength = s.ImageStrength / 100f;
            // Spawn rate per column per second, normalised by how long a drop takes to cross the
            // screen so the perceived density is the same on any monitor height.
            _spawnRate = (0.03f + s.Density / 100f * 1.4f) * _baseSpeed / Math.Max(1, _rows);

            _palettes = new int[_cols][];
            if (s.Rainbow)
            {
                for (int c = 0; c < _cols; c++)
                    _palettes[c] = BuildPalette(FromHsv(360f * c / _cols, 1f, 1f), s.GlowHead);
            }
            else
            {
                var p = BuildPalette(s.Color, s.GlowHead);
                for (int c = 0; c < _cols; c++) _palettes[c] = p;
            }

            // Scanlines: darken the last row(s) of every period. The period follows the character
            // size so every glyph is crossed by ~6 lines at any resolution.
            int period = Math.Max(2, (int)Math.Round(_fontPx / 6f));
            int darkRows = Math.Max(1, period / 3);
            int dark = 256 - 256 * s.ScanlineStrength / 100;
            for (int y = 0; y < _rowScale.Length; y++)
                _rowScale[y] = s.Scanlines && y % period >= period - darkRows ? dark : 256;

            for (int i = 0; i < _drawnLevel.Length; i++) _drawnLevel[i] = Unknown;
        }

        /// <summary>Runs the simulation for a few seconds so the screen starts already full of rain.</summary>
        public void Prewarm()
        {
            const float step = 1f / 30f;
            float seconds = Math.Min(30f, _rows / _baseSpeed + _trailSeconds);
            for (float t = 0; t < seconds; t += step) Update(step);
        }

        /// <summary>Sets the hidden image map (or null) and its current fade-in amount (0..1).</summary>
        public void SetHiddenImage(float[] map, float amount)
        {
            _imageMap = map;
            _imageInfluence = map == null ? 0f : _imageStrength * amount;
        }

        public void Update(float dt)
        {
            int cells = _bright.Length;

            // 1. Fade every lit trail cell. Where a hidden image is bright, trails linger a little
            //    longer (and fade faster where it's dark), so the picture builds up statistically.
            float k = _imageInfluence;
            var map = _imageMap;
            for (int i = 0; i < cells; i++)
            {
                if (_bright[i] > 0f && !_isHead[i])
                {
                    float rate = k > 0f ? _fade[i] * (1f - 0.6f * k * map[i]) : _fade[i];
                    _bright[i] -= rate * dt;
                    if (_bright[i] < 0f) _bright[i] = 0f;
                }
            }

            // 2. Advance drops; each row a head enters gets a fresh random glyph at full brightness.
            for (int d = _drops.Count - 1; d >= 0; d--)
            {
                var drop = _drops[d];
                drop.Head += drop.Speed * dt;
                int target = (int)drop.Head;
                while (drop.LastRow < target && drop.LastRow < _rows)
                {
                    if (drop.LastRow >= 0) _isHead[drop.LastRow * _cols + drop.Column] = false;
                    drop.LastRow++;
                    if (drop.LastRow < _rows)
                    {
                        int i = drop.LastRow * _cols + drop.Column;
                        _glyph[i] = (byte)_rng.Next(_atlas.Count);
                        _bright[i] = 1f;
                        _fade[i] = 1f / _trailSeconds;
                        _isHead[i] = true;
                        _dirty[i] = true;
                    }
                }
                if (drop.LastRow >= _rows)
                {
                    _drops.RemoveAt(d);
                    if (_lastDropInColumn[drop.Column] == drop) _lastDropInColumn[drop.Column] = null;
                }
            }

            // 3. Spawn new drops at the top.
            float spawnChance = _spawnRate * dt;
            // New drops may fall through an older drop's lingering trail, but keep heads apart.
            float minGap = 3f + _rows * 0.1f;
            for (int c = 0; c < _cols; c++)
            {
                if (_rng.NextDouble() >= spawnChance) continue;
                var previous = _lastDropInColumn[c];
                if (previous != null && previous.Head < minGap) continue;

                float speed = _baseSpeed * (0.6f + (float)_rng.NextDouble() * 0.7f);
                if (previous != null) speed = Math.Min(speed, previous.Speed); // never overtake
                var drop = new Drop { Column = c, Head = 0f, Speed = speed };
                _drops.Add(drop);
                _lastDropInColumn[c] = drop;
            }

            // 4. Randomly mutate a few glyphs in the trails for that shimmering look.
            _flickerAccumulator += cells * 0.01f * dt;
            int flickers = (int)_flickerAccumulator;
            _flickerAccumulator -= flickers;
            for (int n = 0; n < flickers; n++)
            {
                int i = _rng.Next(cells);
                if (_bright[i] > 0.05f && !_isHead[i])
                {
                    _glyph[i] = (byte)_rng.Next(_atlas.Count);
                    _dirty[i] = true;
                }
            }
        }

        public void Render()
        {
            float k = _imageInfluence;
            var map = _imageMap;
            int* bits = _surface.Bits;
            fixed (byte* masks = _atlas.Masks)
            {
                for (int r = 0; r < _rows; r++)
                {
                    int rowStart = r * _cols;
                    for (int c = 0; c < _cols; c++)
                    {
                        int i = rowStart + c;
                        byte level;
                        if (_isHead[i]) level = HeadLevel;
                        else if (_bright[i] <= 0f) level = 0;
                        else
                        {
                            // Dark parts of the hidden image dim the rain; bright parts lift it a little.
                            float b = _bright[i];
                            if (k > 0f) b *= map[i] < 0f ? 1f + 0.75f * k * map[i] : 1f + 0.3f * k * map[i];
                            level = (byte)Math.Max(1, Math.Min(Levels, (int)Math.Ceiling(b * Levels)));
                        }

                        if (level == _drawnLevel[i] && !_dirty[i]) continue;
                        _drawnLevel[i] = level;
                        _dirty[i] = false;

                        byte* mask = level == 0 ? null : masks + _glyph[i] * _atlas.CellW * _atlas.CellH;
                        DrawCell(bits, c, r, mask, _palettes[c][level]);
                    }
                }
            }
        }

        private void DrawCell(int* bits, int col, int row, byte* mask, int color)
        {
            int cellW = _atlas.CellW, cellH = _atlas.CellH;
            int x0 = col * cellW, y0 = row * cellH;
            int w = Math.Min(cellW, _surface.Width - x0);
            int h = Math.Min(cellH, _surface.Height - y0);
            if (w <= 0 || h <= 0) return;

            int stride = _stride;
            int* dst = bits + y0 * stride + x0;

            if (mask == null)
            {
                for (int y = 0; y < h; y++, dst += stride)
                    for (int x = 0; x < w; x++) dst[x] = 0;
                return;
            }

            int r0 = (color >> 16) & 0xFF, g0 = (color >> 8) & 0xFF, b0 = color & 0xFF;
            for (int y = 0; y < h; y++, dst += stride)
            {
                int rs = _rowScale[y0 + y];
                int cr = r0 * rs >> 8, cg = g0 * rs >> 8, cb = b0 * rs >> 8;
                byte* m = mask + y * cellW;
                for (int x = 0; x < w; x++)
                {
                    int a = m[x];
                    dst[x] = a == 0 ? 0 : ((cr * a >> 8) << 16) | ((cg * a >> 8) << 8) | (cb * a >> 8);
                }
            }
        }

        private static int[] BuildPalette(Color baseColor, bool glowHead)
        {
            var p = new int[HeadLevel + 1];
            for (int l = 1; l <= Levels; l++)
            {
                float k = (float)Math.Pow(l / (float)Levels, 1.1); // gentle falloff so trails linger visibly
                p[l] = Rgb(baseColor.R * k, baseColor.G * k, baseColor.B * k);
            }
            const float white = 0.75f;
            p[HeadLevel] = glowHead
                ? Rgb(baseColor.R + (255 - baseColor.R) * white, baseColor.G + (255 - baseColor.G) * white, baseColor.B + (255 - baseColor.B) * white)
                : Rgb(baseColor.R, baseColor.G, baseColor.B);
            return p;
        }

        private static int Rgb(float r, float g, float b) =>
            ((int)r << 16) | ((int)g << 8) | (int)b;

        private static Color FromHsv(float hue, float sat, float val)
        {
            float c = val * sat;
            float x = c * (1 - Math.Abs(hue / 60f % 2 - 1));
            float m = val - c;
            float r, g, b;
            if (hue < 60) { r = c; g = x; b = 0; }
            else if (hue < 120) { r = x; g = c; b = 0; }
            else if (hue < 180) { r = 0; g = c; b = x; }
            else if (hue < 240) { r = 0; g = x; b = c; }
            else if (hue < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }
            return Color.FromArgb((int)((r + m) * 255), (int)((g + m) * 255), (int)((b + m) * 255));
        }
    }
}
