using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MatrixScreensaver
{
    /// <summary>A WPF <see cref="WriteableBitmap"/> the renderer can draw into directly.</summary>
    internal sealed unsafe class BitmapSurface : IPixelSurface
    {
        public WriteableBitmap Bitmap { get; }

        public int Width { get; }
        public int Height { get; }
        public int Stride { get; }
        public int* Bits => (int*)Bitmap.BackBuffer;

        /// <param name="dpiScale">Device pixels per DIP, so the bitmap renders 1:1 on screen.</param>
        public BitmapSurface(int pixelWidth, int pixelHeight, double dpiScale)
        {
            Width = Math.Max(1, pixelWidth);
            Height = Math.Max(1, pixelHeight);
            Bitmap = new WriteableBitmap(Width, Height, 96 * dpiScale, 96 * dpiScale, PixelFormats.Bgr32, null);
            Stride = Bitmap.BackBufferStride / 4;
        }

        /// <summary>Locks the bitmap so the renderer can write into <see cref="Bits"/>.</summary>
        public void BeginWrite() => Bitmap.Lock();

        /// <summary>Publishes everything written since <see cref="BeginWrite"/>.</summary>
        public void EndWrite()
        {
            Bitmap.AddDirtyRect(new Int32Rect(0, 0, Width, Height));
            Bitmap.Unlock();
        }
    }
}
