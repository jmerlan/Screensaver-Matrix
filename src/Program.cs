using System;
using System.Windows.Forms;

namespace MatrixScreensaver
{
    internal static class Program
    {
        /// <summary>
        /// Windows launches a .scr with one of:
        ///   /s            run full-screen
        ///   /p &lt;hwnd&gt;     render a preview inside the given window
        ///   /c[:hwnd]     show the settings dialog (optionally owned by hwnd)
        ///   (no args)     settings dialog
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var (mode, hwnd) = ParseArgs(args);
            var settings = Settings.Load();

            switch (mode)
            {
                case 's':
                    ScreenSaverForm.ShowFullScreen(settings, Application.Exit);
                    Application.Run();
                    break;
                case 'p':
                    if (hwnd != IntPtr.Zero) Application.Run(new ScreenSaverForm(hwnd, settings));
                    break;
                default:
                    using (var form = new SettingsForm(settings))
                    {
                        if (hwnd != IntPtr.Zero) form.ShowDialog(new Win32Window(hwnd));
                        else Application.Run(form);
                    }
                    break;
            }
        }

        private static (char Mode, IntPtr Hwnd) ParseArgs(string[] args)
        {
            if (args.Length == 0) return ('c', IntPtr.Zero);

            // Accept "/p 1234", "/p:1234", "-s", "/S" …
            string first = args[0].Trim().TrimStart('/', '-');
            if (first.Length == 0) return ('c', IntPtr.Zero);

            char mode = char.ToLowerInvariant(first[0]);
            string handleText = first.Length > 2 && first[1] == ':' ? first.Substring(2)
                              : args.Length > 1 ? args[1]
                              : null;

            IntPtr hwnd = IntPtr.Zero;
            if (handleText != null && long.TryParse(handleText, out long value)) hwnd = new IntPtr(value);

            return (mode == 's' || mode == 'p') ? (mode, hwnd) : ('c', hwnd);
        }
    }
}
