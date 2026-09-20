using System;

namespace MatrixScreensaver
{
    /// <summary>
    /// A 32-bit top-down GDI DIB section we can write pixels into directly and BitBlt to a window.
    /// </summary>
    internal sealed unsafe class DibSurface : IPixelSurface, IDisposable
    {
        public int Width { get; }
        public int Height { get; }
        public int Stride => Width; // DIB sections created here are 32bpp with no row padding
        public int* Bits { get; }

        private IntPtr _dc;
        private IntPtr _bitmap;
        private IntPtr _oldBitmap;

        public DibSurface(int width, int height)
        {
            Width = width;
            Height = height;

            var header = new Native.BITMAPINFOHEADER
            {
                biSize = sizeof(Native.BITMAPINFOHEADER),
                biWidth = width,
                biHeight = -height, // negative = top-down rows
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0,  // BI_RGB
            };

            _dc = Native.CreateCompatibleDC(IntPtr.Zero);
            _bitmap = Native.CreateDIBSection(_dc, ref header, 0, out IntPtr bits, IntPtr.Zero, 0);
            if (_bitmap == IntPtr.Zero)
            {
                Native.DeleteDC(_dc);
                throw new OutOfMemoryException($"Could not allocate a {width}x{height} drawing surface.");
            }
            _oldBitmap = Native.SelectObject(_dc, _bitmap);
            Bits = (int*)bits; // DIB sections are zero-initialised, i.e. black.
        }

        public void BlitTo(IntPtr destDc) =>
            Native.BitBlt(destDc, 0, 0, Width, Height, _dc, 0, 0, Native.SRCCOPY);

        public void Dispose()
        {
            if (_dc == IntPtr.Zero) return;
            Native.SelectObject(_dc, _oldBitmap);
            Native.DeleteObject(_bitmap);
            Native.DeleteDC(_dc);
            _dc = IntPtr.Zero;
            GC.SuppressFinalize(this);
        }

        ~DibSurface() => Dispose();
    }
}
