using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
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
            AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbeddedAssembly;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var (mode, hwnd) = ParseArgs(args);
            Native.SetDpiAwareness(perMonitor: mode == 's' || mode == 'p');
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
                    ShowSettings(settings, hwnd);
                    break;
            }
        }

        /// <summary>Kept out of Main so the WPF settings window's types are only resolved
        /// after <see cref="ResolveEmbeddedAssembly"/> is hooked up.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ShowSettings(Settings settings, IntPtr owner)
        {
            // A real Application (rather than Window.ShowDialog) so that hiding the window during
            // "Test full screen" doesn't end the message loop and quit.
            var app = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
            var window = new SettingsWindow(settings);
            window.Closed += (s, e) => app.Shutdown();
            if (owner != IntPtr.Zero) new System.Windows.Interop.WindowInteropHelper(window).Owner = owner;
            app.Run(window);
        }

        /// <summary>The theme library is embedded in this executable so the .scr stays a single
        /// file; load it from resources when the CLR asks for it.</summary>
        private static Assembly ResolveEmbeddedAssembly(object sender, ResolveEventArgs args)
        {
            string name = new AssemblyName(args.Name).Name + ".dll";
            using (var stream = typeof(Program).Assembly.GetManifestResourceStream(name))
            {
                if (stream == null) return null;
                var bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);
                return Assembly.Load(bytes);
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
