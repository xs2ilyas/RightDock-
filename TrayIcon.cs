using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace RightDock
{
    internal sealed class TrayIcon : IDisposable
    {
        private readonly NotifyIcon _icon;
        private readonly ContextMenuStrip _menu;
        private readonly ToolStripMenuItem _openItem;
        private readonly ToolStripMenuItem _minimizeItem;
        private readonly MainWindow _window;

        internal TrayIcon(MainWindow window)
        {
            _window = window;

            _menu = new ContextMenuStrip
            {
                Renderer = new ToolStripProfessionalRenderer(new DarkColorTable()),
                BackColor = Theme.Back,
                ForeColor = Theme.Text,
                ShowImageMargin = false,
                Padding = new Padding(0, 4, 0, 4),
                Font = new Font("Segoe UI", 9f),
            };

            _openItem = new ToolStripMenuItem("Open Right Dock");
            _openItem.Click += (_, __) => _window.RestoreFromTray();

            _minimizeItem = new ToolStripMenuItem("Minimize to tray");
            _minimizeItem.Click += (_, __) => _window.MinimizeToTray();

            var restartItem = new ToolStripMenuItem("Restart Right Dock");
            restartItem.Click += (_, __) => Restart();

            var exitItem = new ToolStripMenuItem("Exit Right Dock");
            exitItem.Click += (_, __) => System.Windows.Application.Current.Shutdown();

            _menu.Items.Add(_openItem);
            _menu.Items.Add(_minimizeItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(restartItem);
            _menu.Items.Add(exitItem);

            _menu.Opening += (_, __) => UpdateState();

            _icon = new NotifyIcon
            {
                Icon = LoadAppIcon(),
                Text = "Right Dock",
                Visible = true,
                ContextMenuStrip = _menu,
            };
            _icon.MouseClick += (_, e) =>
            {
                if (e.Button == MouseButtons.Left) _window.RestoreFromTray();
            };
        }

        private void UpdateState()
        {
            _openItem.Enabled = !_window.IsDockVisible;
            _minimizeItem.Enabled = _window.CanHideToTray;
        }

        private static void Restart()
        {
            string executable = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(executable)) return;

            try
            {
                Process.Start(new ProcessStartInfo(executable, "--restart") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not restart RightDock from the tray.", ex);
                return;
            }

            System.Windows.Application.Current.Shutdown();
        }

        private static Icon LoadAppIcon()
        {
            try
            {
                string executable = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(executable))
                {
                    Icon loaded = Icon.ExtractAssociatedIcon(executable);
                    if (loaded != null) return loaded;
                }
            }
            catch
            {
                // Fall back to the generic application icon.
            }

            return SystemIcons.Application;
        }

        public void Dispose()
        {
            _icon.Visible = false;
            _icon.Dispose();
            _menu.Dispose();
        }

        private static class Theme
        {
            internal static readonly Color Back = Color.FromArgb(0x1C, 0x1C, 0x1C);
            internal static readonly Color Text = Color.FromArgb(0xE6, 0xFF, 0xFF, 0xFF);
            internal static readonly Color Hover = Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF);
            internal static readonly Color Edge = Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF);
            internal static readonly Color Divider = Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF);
        }

        private sealed class DarkColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => Theme.Back;
            public override Color MenuBorder => Theme.Edge;
            public override Color MenuItemBorder => Theme.Hover;
            public override Color MenuItemSelected => Theme.Hover;
            public override Color MenuItemSelectedGradientBegin => Theme.Hover;
            public override Color MenuItemSelectedGradientEnd => Theme.Hover;
            public override Color MenuItemPressedGradientBegin => Theme.Back;
            public override Color MenuItemPressedGradientEnd => Theme.Back;
            public override Color ImageMarginGradientBegin => Theme.Back;
            public override Color ImageMarginGradientMiddle => Theme.Back;
            public override Color ImageMarginGradientEnd => Theme.Back;
            public override Color SeparatorDark => Theme.Divider;
            public override Color SeparatorLight => Theme.Divider;
        }
    }
}
