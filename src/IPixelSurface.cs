namespace MatrixScreensaver
{
    /// <summary>
    /// A 32-bit BGRA pixel buffer the renderer can write into: a GDI DIB section for the
    /// screensaver windows, or a WPF bitmap for the settings preview.
    /// </summary>
    internal unsafe interface IPixelSurface
    {
        int Width { get; }
        int Height { get; }
        /// <summary>Distance between rows, in pixels (not bytes).</summary>
        int Stride { get; }
        int* Bits { get; }
    }
}
