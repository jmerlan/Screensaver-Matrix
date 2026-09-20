using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Linq;

namespace MatrixScreensaver
{
    /// <summary>
    /// Pre-rasterises every glyph once into an 8-bit coverage mask so the renderer
    /// can composite characters with plain integer math instead of GDI+ text calls.
    /// </summary>
    internal sealed class GlyphAtlas
    {
        public readonly int CellW;
        public readonly int CellH;
        public readonly int Count;
        /// <summary>Count masks of CellW*CellH bytes, stored back to back.</summary>
        public readonly byte[] Masks;

        private const string Symbols = "0123456789Z:.\"=*+-<>|";
        private const string LatinFallback = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const string SmallKana = "ィゥェォッャュョヮ"; // small ィゥェォッャュョヮ

        private static readonly Lazy<(string Name, bool HasKatakana)> Font = new Lazy<(string, bool)>(PickFont);

        /// <param name="widthScale">Horizontal stretch of each glyph (1 = the font's natural shape).</param>
        /// <param name="columnGap">Empty space between columns, in ems.</param>
        /// <param name="strokePx">Extra outline thickness in pixels (0 = the font's own weight).</param>
        public GlyphAtlas(int fontPx, float widthScale, float columnGap, float strokePx)
        {
            var (fontName, hasKatakana) = Font.Value;

            // Full-width katakana are ~1em square; the Latin fallback is ~0.6em wide.
            float glyphEm = hasKatakana ? 1f : 0.6f;
            CellW = Math.Max(4, (int)Math.Round(fontPx * (glyphEm * widthScale + columnGap)));
            CellH = Math.Max(4, fontPx); // rows sit tight, like the film

            // (character, mirrored?) — the film shows katakana mirrored left/right.
            var glyphs = new List<(char, bool)>();
            if (hasKatakana)
            {
                for (char c = 'ア'; c <= 'ン'; c++) // ア .. ン
                {
                    if (SmallKana.IndexOf(c) < 0) glyphs.Add((c, true));
                }
            }
            else
            {
                foreach (char c in LatinFallback) glyphs.Add((c, true));
            }
            foreach (char c in Symbols) glyphs.Add((c, false));

            Count = glyphs.Count;
            int maskSize = CellW * CellH;
            Masks = new byte[Count * maskSize];

            using (var bmp = new Bitmap(CellW, CellH, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(bmp))
            using (var family = new FontFamily(fontName))
            using (var pen = new Pen(Color.White, Math.Max(0.01f, strokePx)) { LineJoin = LineJoin.Round })
            using (var format = new StringFormat(StringFormat.GenericDefault))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                // Scale horizontally about the cell centre (negated for mirrored glyphs).
                float cx = CellW / 2f;
                var normal = new Matrix(widthScale, 0, 0, 1, cx - widthScale * cx, 0);
                var mirror = new Matrix(-widthScale, 0, 0, 1, cx + widthScale * cx, 0);

                for (int i = 0; i < Count; i++)
                {
                    var (ch, mirrored) = glyphs[i];
                    g.ResetTransform();
                    g.Clear(Color.Black);
                    g.Transform = mirrored ? mirror : normal;
                    // Lay out in unscaled space so the scaled result lands centred in the cell.
                    var layout = new RectangleF(cx - cx / widthScale, 0, CellW / widthScale, CellH);
                    // Draw as a path so strokes can be thickened by outlining with a pen.
                    using (var path = new GraphicsPath())
                    {
                        path.AddString(ch.ToString(), family, (int)FontStyle.Regular, fontPx, layout, format);
                        g.FillPath(Brushes.White, path);
                        if (strokePx > 0) g.DrawPath(pen, path);
                    }
                    CopyCoverage(bmp, Masks, i * maskSize);
                }
            }
        }

        private unsafe void CopyCoverage(Bitmap bmp, byte[] dest, int offset)
        {
            var data = bmp.LockBits(new Rectangle(0, 0, CellW, CellH), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < CellH; y++)
                {
                    byte* row = (byte*)data.Scan0 + y * data.Stride;
                    for (int x = 0; x < CellW; x++)
                        dest[offset + y * CellW + x] = row[x * 4 + 1]; // green channel of white-on-black
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }

        /// <summary>Finds an installed font that actually contains katakana.</summary>
        private static (string, bool) PickFont()
        {
            HashSet<string> installed;
            using (var fonts = new InstalledFontCollection())
                installed = new HashSet<string>(fonts.Families.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);

            string[] candidates = { "MS Gothic", "Yu Gothic", "Meiryo", "MS UI Gothic", "Yu Gothic UI", "Noto Sans JP", "Arial Unicode MS" };
            foreach (var name in candidates)
            {
                if (installed.Contains(name) && HasGlyph(name, 'ア'))
                    return (name, true);
            }
            return (installed.Contains("Consolas") ? "Consolas" : "Courier New", false);
        }

        private static bool HasGlyph(string family, char ch)
        {
            try
            {
                using (var font = new Font(family, 20, GraphicsUnit.Pixel))
                {
                    IntPtr dc = Native.CreateCompatibleDC(IntPtr.Zero);
                    IntPtr hFont = font.ToHfont();
                    IntPtr old = Native.SelectObject(dc, hFont);
                    try
                    {
                        var indices = new ushort[1];
                        uint result = Native.GetGlyphIndicesW(dc, ch.ToString(), 1, indices, Native.GGI_MARK_NONEXISTING_GLYPHS);
                        return result != uint.MaxValue && indices[0] != 0xFFFF;
                    }
                    finally
                    {
                        Native.SelectObject(dc, old);
                        Native.DeleteObject(hFont);
                        Native.DeleteDC(dc);
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
