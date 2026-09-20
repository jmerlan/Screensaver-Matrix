using System;
using System.Drawing;
using System.Windows.Forms;

namespace MatrixScreensaver
{
    /// <summary>One borderless window per monitor (full-screen mode), or a child of the
    /// Control Panel's little monitor picture (preview mode).</summary>
    internal sealed class ScreenSaverForm : Form
    {
        private const int MouseMoveThreshold = 8;

        private readonly bool _isPreview;
        private readonly IntPtr _previewParent;
        private Point? _mouseOrigin;

        /// <summary>Raised on keyboard/mouse activity in full-screen mode.</summary>
        public event Action ExitRequested;

        /// <summary>Full-screen window covering one monitor.</summary>
        public ScreenSaverForm(Rectangle bounds, Settings settings, bool showRain)
        {
            InitCommon();
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            TopMost = true;

            if (showRain) AddView(new MatrixView(settings));

            KeyPreview = true;
            KeyDown += (s, e) => Exit();
            WireMouse(this);
        }

        /// <summary>Preview hosted inside the Screen Saver Settings dialog.</summary>
        public ScreenSaverForm(IntPtr previewParent, Settings settings)
        {
            InitCommon();
            _isPreview = true;
            _previewParent = previewParent;
            AddView(new MatrixView(settings, sizeMultiplier: 0.35f));

            var watchdog = new Timer { Interval = 500 };
            watchdog.Tick += (s, e) => { if (!Native.IsWindow(_previewParent)) Application.Exit(); };
            watchdog.Start();
        }

        private void InitCommon()
        {
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            BackColor = Color.Black;
            Text = "Matrix";
        }

        private void AddView(MatrixView view)
        {
            view.Dock = DockStyle.Fill;
            Controls.Add(view);
            if (!_isPreview) WireMouse(view);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!_isPreview) return;

            // Re-parent into the preview window and fill its client area.
            Native.SetParent(Handle, _previewParent);
            int style = Native.GetWindowLong(Handle, Native.GWL_STYLE);
            Native.SetWindowLong(Handle, Native.GWL_STYLE, (style | Native.WS_CHILD) & ~Native.WS_POPUP);
            Native.GetClientRect(_previewParent, out var rect);
            Bounds = new Rectangle(0, 0, rect.Width, rect.Height);
        }

        private void WireMouse(Control control)
        {
            control.MouseDown += (s, e) => Exit();
            control.MouseWheel += (s, e) => Exit();
            control.MouseMove += (s, e) =>
            {
                // Windows sends a MouseMove as soon as the window appears, so only exit
                // once the cursor has genuinely travelled some distance.
                var pos = Cursor.Position;
                if (_mouseOrigin == null)
                {
                    _mouseOrigin = pos;
                    return;
                }
                if (Math.Abs(pos.X - _mouseOrigin.Value.X) > MouseMoveThreshold ||
                    Math.Abs(pos.Y - _mouseOrigin.Value.Y) > MouseMoveThreshold)
                {
                    Exit();
                }
            };
        }

        private void Exit()
        {
            if (!_isPreview) ExitRequested?.Invoke();
        }

        /// <summary>Opens one full-screen window per monitor. Any input closes them all and
        /// then calls <paramref name="onClosed"/>.</summary>
        public static void ShowFullScreen(Settings settings, Action onClosed)
        {
            var forms = new System.Collections.Generic.List<ScreenSaverForm>();
            bool closing = false;
            void CloseAll()
            {
                if (closing) return;
                closing = true;
                foreach (var f in forms) f.Close();
                Cursor.Show();
                Native.timeEndPeriod(1);
                onClosed?.Invoke();
            }

            Native.timeBeginPeriod(1);
            Cursor.Hide();
            // One window per monitor; each runs its own independent rain.
            foreach (var screen in Screen.AllScreens)
            {
                var form = new ScreenSaverForm(screen.Bounds, settings, settings.AllMonitors || screen.Primary);
                form.ExitRequested += CloseAll;
                forms.Add(form);
                form.Show();
            }
            forms[0].Activate();
        }
    }
}
