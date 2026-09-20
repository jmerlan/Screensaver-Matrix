using System;
using System.Runtime.InteropServices;

namespace MatrixScreensaver
{
    internal static class Native
    {
        public const int GWL_STYLE = -16;
        public const int WS_CHILD = 0x40000000;
        public const int WS_POPUP = unchecked((int)0x80000000);
        public const uint SRCCOPY = 0x00CC0020;
        public const uint GGI_MARK_NONEXISTING_GLYPHS = 0x0001;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }

        // user32
        [DllImport("user32.dll")] public static extern IntPtr SetParent(IntPtr child, IntPtr newParent);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int index);
        [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr hWnd, int index, int value);
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);
        [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hWnd);

        // gdi32
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bmi, uint usage,
            out IntPtr bits, IntPtr section, uint offset);
        [DllImport("gdi32.dll")]
        public static extern bool BitBlt(IntPtr dest, int x, int y, int w, int h, IntPtr src, int sx, int sy, uint rop);
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        public static extern uint GetGlyphIndicesW(IntPtr hdc, string text, int count, [Out] ushort[] indices, uint flags);

        // winmm – raises timer resolution so the animation runs at a steady ~60 fps
        [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint period);
        [DllImport("winmm.dll")] public static extern uint timeEndPeriod(uint period);

        private static readonly IntPtr DpiAwarenessPerMonitorV2 = new IntPtr(-4);
        private static readonly IntPtr DpiAwarenessSystem = new IntPtr(-2);

        [DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();

        /// <summary>
        /// Per-monitor awareness lets the screensaver render at each monitor's native resolution.
        /// The WPF settings window gets system awareness instead: .NET Framework WPF lays windows
        /// out at 96 DPI under per-monitor awareness, which clips them on scaled displays.
        /// Must be called before any window exists.
        /// </summary>
        public static void SetDpiAwareness(bool perMonitor)
        {
            try
            {
                if (SetProcessDpiAwarenessContext(perMonitor ? DpiAwarenessPerMonitorV2 : DpiAwarenessSystem)) return;
            }
            catch (EntryPointNotFoundException)
            {
                // Windows 8.1 or older: fall through to the legacy call.
            }
            try { SetProcessDPIAware(); } catch (EntryPointNotFoundException) { }
        }

        public static float GetDpiScale(IntPtr hWnd)
        {
            try
            {
                uint dpi = GetDpiForWindow(hWnd);
                return dpi > 0 ? dpi / 96f : 1f;
            }
            catch (EntryPointNotFoundException)
            {
                return 1f;
            }
        }
    }

    internal sealed class Win32Window : System.Windows.Forms.IWin32Window
    {
        public Win32Window(IntPtr handle) => Handle = handle;
        public IntPtr Handle { get; }
    }
}
