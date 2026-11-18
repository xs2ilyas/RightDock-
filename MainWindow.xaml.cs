using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using RightDock.Data;
using RightDock.Modules;

namespace RightDock
{
    public partial class MainWindow : Window
    {
        private static readonly Color DefaultIconColor = Color.FromRgb(0xA9, 0xA9, 0xA9);
        private static readonly Brush FallbackTextBrush = CreateFrozenBrush(Color.FromArgb(0xE6, 0xFF, 0xFF, 0xFF));

        private const int HideDelayMs = 650;
        private const int SlideMs = 170;
        private const int EdgeRevealPixels = 5;
        private const int RevealMargin = 4;

        private readonly DockServices _services;
        private readonly AppSettings _settings;
        private readonly ModuleManager _modules;
        private readonly List<Pin> _pins = PinStore.Load();
        private readonly DispatcherTimer _hoverTimer;
        private readonly DispatcherTimer _slideTimer;

        private AppBarDock _appBar;
        private HwndSource _hwndSource;
        private IntPtr _hwnd;
        private int _appBarCallbackMessage;

        private bool _dockShown = true;
        private bool _suppressedByFullscreen;
        private bool _closing;
        private bool _hiddenByTray;
        private bool _keepShownUntilHover;
        private int _iconSize = AppSettings.DefaultIconSize;
        private DateTime _fullscreenSince = DateTime.MinValue;
        private bool _slideHides;
        private int _modalDepth;
        private int _popupCount;
        private DateTime _hideDeadline = DateTime.MaxValue;
        private double _dpiScale = 1;
        private Color _iconColor;
        private Brush _iconBrush = FallbackTextBrush;

        private int _currentLeft;
        private int _currentTop;
        private int _currentWidth;
        private int _currentHeight;
        private int _slideStartLeft;
        private int _slideTargetLeft;
        private int _slideTop;
        private int _slideWidth;
        private int _slideHeight;
        private DateTime _slideStarted;

        internal MainWindow(DockServices services)
        {
            InitializeComponent();

            _services = services;
            _settings = services?.Settings ?? SettingsStore.Load();
            _modules = services?.Modules;

            _iconColor = ParseIconColor(_settings.IconColor);
            _iconBrush = CreateFrozenBrush(_iconColor);
            _iconSize = AppSettings.NormalizeIconSize(_settings.IconSize);
            RebuildButtons();
            RebuildModuleButtons();

            if (_modules != null)
            {
                _modules.DockItemsChanged += RebuildModuleButtons;
                _modules.PopupCountChanged += OnPopupCountChanged;
            }

            _hoverTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromMilliseconds(110),
            };
            _hoverTimer.Tick += OnHoverTick;

            _slideTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(16),
            };
            _slideTimer.Tick += OnSlideTick;

            Loaded += OnLoaded;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            _hwnd = new WindowInteropHelper(this).Handle;

            long exStyle = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
            NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE,
                new IntPtr(exStyle | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE));

            int noRound = NativeMethods.DWMWCP_DONOTROUND;
            NativeMethods.DwmSetWindowAttribute(_hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref noRound, sizeof(int));

            _appBarCallbackMessage = NativeMethods.RegisterWindowMessage("RightDock_AppBarCallback");
            _hwndSource = HwndSource.FromHwnd(_hwnd);
            _hwndSource.AddHook(WndProc);
            _appBar = new AppBarDock(_hwnd, _appBarCallbackMessage);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _dpiScale = GetDpiScale();

            if (_appBar.Reserve(_dpiScale))
            {
                NativeMethods.RECT r = _appBar.ReservedRect;
                PlaceWindow(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
                _dockShown = true;
                if (_settings.AutoHide) _hideDeadline = DateTime.UtcNow.AddMilliseconds(1600);

                NativeMethods.RECT monitor = _appBar.MonitorRect;
                Services.Log.Info("Dock placed: DPI " + Math.Round(_dpiScale * 100) + "%, monitor "
                    + monitor.Left + "," + monitor.Top + " " + (monitor.Right - monitor.Left) + "x"
                    + (monitor.Bottom - monitor.Top) + ", dock " + r.Left + "," + r.Top + " "
                    + (r.Right - r.Left) + "x" + (r.Bottom - r.Top) + ".");
            }

            _hoverTimer.Start();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _closing = true;
            _hoverTimer.Stop();
            _slideTimer.Stop();
            _appBar?.Unregister();

            if (_modules != null)
            {
                _modules.DockItemsChanged -= RebuildModuleButtons;
                _modules.PopupCountChanged -= OnPopupCountChanged;
                _modules.ClosePopups();
            }

            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_hwndSource != null)
            {
                _hwndSource.RemoveHook(WndProc);
                _hwndSource = null;
            }
            base.OnClosed(e);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == _appBarCallbackMessage)
            {
                switch (wParam.ToInt32())
                {
                    case NativeMethods.ABN_POSCHANGED:
                        if (!WithinRecentReserve()) RefreshGeometry();
                        break;
                    case NativeMethods.ABN_FULLSCREENAPP:
                        OnFullscreenChanged(lParam != IntPtr.Zero);
                        break;
                }

                handled = true;
                return IntPtr.Zero;
            }

            if (msg == NativeMethods.WM_DISPLAYCHANGE)
            {
                RefreshGeometry();
            }
            else if (msg == NativeMethods.WM_DPICHANGED)
            {
                _dpiScale = (wParam.ToInt32() & 0xFFFF) / 96.0;
                RefreshGeometry();
            }
            else if (msg == NativeMethods.WM_POWERBROADCAST)
            {
                int reason = wParam.ToInt32();
                if (reason == NativeMethods.PBT_APMRESUMEAUTOMATIC
                    || reason == NativeMethods.PBT_APMRESUMESUSPEND)
                {
                    Services.Log.Info("System resumed; reconciling scheduled work.");
                    _services?.Scheduler.Reconcile();
                    RefreshGeometry();
                }
            }
            else if (msg == NativeMethods.WM_TIMECHANGE)
            {
                Services.Log.Info("System clock changed; reconciling scheduled work.");
                _services?.Scheduler.Reconcile();
            }

            return IntPtr.Zero;
        }

        private bool WithinRecentReserve()
        {
            if (_appBar == null) return false;
            long elapsed = Stopwatch.GetTimestamp() - _appBar.LastReserveTicks;
            return elapsed * 1000.0 / Stopwatch.Frequency < 250.0;
        }

        private void RefreshGeometry()
        {
            if (_closing || !_dockShown) return;
            if (!_appBar.Reserve(_dpiScale)) return;

            NativeMethods.RECT r = _appBar.ReservedRect;
            SlideTo(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top, hides: false, animate: false);
        }

        private void OnFullscreenChanged(bool isFullscreen)
        {
            Services.Log.Info("Fullscreen suppression: " + isFullscreen);
            _suppressedByFullscreen = isFullscreen;
            _fullscreenSince = isFullscreen ? DateTime.UtcNow : DateTime.MinValue;
            _keepShownUntilHover = false;

            if (isFullscreen)
            {
                HideDock();
            }
            else
            {
                _hideDeadline = DateTime.UtcNow.AddMilliseconds(HideDelayMs);
            }
        }

        private void OnHoverTick(object sender, EventArgs e)
        {
            if (_closing || _hiddenByTray || _modalDepth > 0 || _popupCount > 0) return;

            if (_suppressedByFullscreen)
            {
                bool settled = (DateTime.UtcNow - _fullscreenSince).TotalMilliseconds > 700;
                if (!settled || IsFullscreenOnDockMonitor())
                {
                    if (_dockShown) HideDock();
                    return;
                }

                Services.Log.Info("Fullscreen app ended; releasing dock suppression.");
                _suppressedByFullscreen = false;
            }

            if (!_settings.AutoHide)
            {
                _hideDeadline = DateTime.MaxValue;
                if (!_dockShown) ShowDock();
                return;
            }

            NativeMethods.RECT monitor = _appBar?.MonitorRect ?? default;
            if (monitor.Right == 0) return;
            if (!NativeMethods.GetCursorPos(out NativeMethods.POINT pt)) return;

            bool inBand = pt.Y >= monitor.Top && pt.Y < monitor.Bottom;
            bool atEdge = inBand && pt.X >= monitor.Right - EdgeRevealPixels;
            bool overDock = _dockShown && inBand
                && pt.X >= _currentLeft - RevealMargin
                && pt.X <= _currentLeft + _currentWidth + RevealMargin
                && pt.Y >= _currentTop - RevealMargin
                && pt.Y <= _currentTop + _currentHeight + RevealMargin;

            bool wantShow = atEdge || overDock;

            if (_keepShownUntilHover)
            {
                if (!wantShow)
                {
                    _hideDeadline = DateTime.MaxValue;
                    return;
                }

                _keepShownUntilHover = false;
            }

            if (wantShow)
            {
                _hideDeadline = DateTime.MaxValue;
                if (!_dockShown) ShowDock();
            }
            else if (_dockShown)
            {
                if (_hideDeadline == DateTime.MaxValue) _hideDeadline = DateTime.UtcNow.AddMilliseconds(HideDelayMs);
                if (DateTime.UtcNow >= _hideDeadline) HideDock();
            }
        }

        private void ShowDock()
        {
            if (_dockShown) return;
            if (!_appBar.Reserve(_dpiScale)) return;

            _dockShown = true;
            _hideDeadline = DateTime.MaxValue;

            NativeMethods.RECT r = _appBar.ReservedRect;
            SlideTo(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top, hides: false, animate: true);
        }

        private void HideDock()
        {
            if (!_dockShown) return;

            _dockShown = false;
            _hideDeadline = DateTime.MaxValue;

            int left = _appBar.MonitorRect.Right;
            int top = _currentHeight > 0 ? _currentTop : _appBar.MonitorRect.Top;
            int width = _currentWidth > 0 ? _currentWidth : (int)Math.Round(50 * _dpiScale);
            int height = _currentHeight > 0 ? _currentHeight : _appBar.MonitorRect.Bottom - _appBar.MonitorRect.Top;

            SlideTo(left, top, width, height, hides: true, animate: true);
        }

        private void PlaceWindow(int left, int top, int width, int height)
        {
            if (width <= 0 || height <= 0) return;

            NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, left, top, width, height,
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

            _currentLeft = left;
            _currentTop = top;
            _currentWidth = width;
            _currentHeight = height;
        }

        private void SlideTo(int targetLeft, int top, int width, int height, bool hides, bool animate)
        {
            _slideHides = hides;
            _slideTop = top;
            _slideWidth = width;
            _slideHeight = height;

            if (!animate)
            {
                _slideTimer.Stop();
                PlaceWindow(targetLeft, top, width, height);
                if (hides && !_dockShown) CompleteHide();
                return;
            }

            _slideStartLeft = _currentWidth > 0 ? _currentLeft : targetLeft;
            _slideTargetLeft = targetLeft;
            _slideStarted = DateTime.UtcNow;

            _slideTimer.Stop();
            _slideTimer.Start();
        }

        private void OnSlideTick(object sender, EventArgs e)
        {
            double t = (DateTime.UtcNow - _slideStarted).TotalMilliseconds / SlideMs;
            if (t >= 1) t = 1;

            double eased = 1 - Math.Pow(1 - t, 3);
            int left = _slideStartLeft + (int)Math.Round((_slideTargetLeft - _slideStartLeft) * eased);
            PlaceWindow(left, _slideTop, _slideWidth, _slideHeight);

            if (t >= 1)
            {
                _slideTimer.Stop();
                if (_slideHides && !_dockShown) CompleteHide();
            }
        }

        private void CompleteHide()
        {
            _appBar?.Unregister();
        }

        private double GetDpiScale()
        {
            return VisualTreeHelper.GetDpi(this).DpiScaleX;
        }

        private bool IsFullscreenOnDockMonitor()
        {
            IntPtr foreground = NativeMethods.GetForegroundWindow();
            if (foreground == IntPtr.Zero || foreground == _hwnd) return false;

            NativeMethods.GetWindowThreadProcessId(foreground, out uint pid);
            if (pid == (uint)Environment.ProcessId) return false;

            var className = new StringBuilder(64);
            NativeMethods.GetClassName(foreground, className, className.Capacity);
            string name = className.ToString();
            if (name == "Progman" || name == "WorkerW"
                || name == "Shell_TrayWnd" || name == "Shell_SecondaryTrayWnd")
            {
                return false;
            }

            // Fullscreen videos and players are borderless; maximized windows keep a caption.
            long style = NativeMethods.GetWindowLongPtr(foreground, NativeMethods.GWL_STYLE).ToInt64();
            if ((style & NativeMethods.WS_CAPTION) != 0) return false;

            if (!NativeMethods.GetWindowRect(foreground, out NativeMethods.RECT rect)) return false;

            IntPtr monitor = NativeMethods.MonitorFromWindow(foreground, NativeMethods.MONITOR_DEFAULTTONEAREST);
            if (monitor != NativeMethods.MonitorFromWindow(_hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST))
            {
                return false;
            }

            var info = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
            if (!NativeMethods.GetMonitorInfo(monitor, ref info)) return false;

            const int tolerance = 2;
            NativeMethods.RECT bounds = info.rcMonitor;
            return rect.Left <= bounds.Left + tolerance
                && rect.Top <= bounds.Top + tolerance
                && rect.Right >= bounds.Right - tolerance
                && rect.Bottom >= bounds.Bottom - tolerance;
        }

        private void RebuildButtons()
        {
            AppsPanel.Children.Clear();

            foreach (Pin pin in _pins)
            {
                var button = new Button
                {
                    Style = (Style)FindResource("DockButton"),
                    ToolTip = pin.Name,
                    Margin = new Thickness(0, 2, 0, 2),
                };
                ToolTipService.SetPlacement(button, PlacementMode.Left);
                AutomationProperties.SetName(button, pin.Name);

                button.Content = BuildPinVisual(pin);

                Pin captured = pin;
                button.Click += (_, __) => Launch(captured);
                button.ContextMenu = BuildAppMenu(captured);

                AppsPanel.Children.Add(button);
            }
        }

        private void RebuildModuleButtons()
        {
            ModulesPanel.Children.Clear();

            List<ModuleEntry> entries = _modules?.GetDockEntries() ?? new List<ModuleEntry>();

            foreach (ModuleEntry entry in entries)
            {
                var button = new Button
                {
                    Style = (Style)FindResource("DockButton"),
                    ToolTip = entry.Module.Name,
                    Margin = new Thickness(0, 2, 0, 2),
                };
                ToolTipService.SetPlacement(button, PlacementMode.Left);
                AutomationProperties.SetName(button, entry.Module.Name);

                string iconKey = string.IsNullOrWhiteSpace(entry.State.IconKey)
                    ? entry.Module.IconKey
                    : entry.State.IconKey;
                Brush iconBrush = string.IsNullOrWhiteSpace(entry.State.IconColor)
                    ? _iconBrush
                    : CreateFrozenBrush(ParseIconColor(entry.State.IconColor));
                int iconSize = entry.State.IconSize > 0
                    ? AppSettings.NormalizeIconSize(entry.State.IconSize)
                    : _iconSize;

                FrameworkElement icon = IconRepository.CreateIconElement(iconKey, iconSize, iconBrush);
                button.Content = icon ?? (object)new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(entry.Module.Name)
                        ? "?"
                        : entry.Module.Name.Substring(0, 1).ToUpperInvariant(),
                    FontSize = Math.Max(12, Math.Round(iconSize * 0.62)),
                    FontWeight = FontWeights.SemiBold,
                    Foreground = FallbackTextBrush,
                };

                ModuleEntry captured = entry;
                button.Click += (_, __) => _modules.ToggleCompact(captured, button);
                button.ContextMenu = BuildModuleMenu(captured, button);

                ModulesPanel.Children.Add(button);
            }

            ModulesDivider.Visibility = entries.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private ContextMenu BuildModuleMenu(ModuleEntry entry, Button anchor)
        {
            var openItem = new MenuItem { Header = "Open" };
            openItem.Click += (_, __) => _modules.ToggleCompact(entry, anchor);

            var customizeItem = new MenuItem { Header = "Customize icon…" };
            customizeItem.Click += (_, __) => ShowSettings(entry.Module.Id);

            var hideItem = new MenuItem { Header = "Hide from dock" };
            hideItem.Click += (_, __) => _modules.SetVisible(entry.Module.Id, false);

            var menu = new ContextMenu();
            menu.Items.Add(openItem);
            menu.Items.Add(customizeItem);
            menu.Items.Add(hideItem);
            menu.Opened += (_, __) => _modalDepth++;
            menu.Closed += (_, __) => _modalDepth = Math.Max(0, _modalDepth - 1);
            return menu;
        }

        private void OnPopupCountChanged(int count)
        {
            _popupCount = count;

            if (count > 0)
            {
                _hideDeadline = DateTime.MaxValue;
            }
            else if (_settings.AutoHide && _dockShown)
            {
                _hideDeadline = DateTime.UtcNow.AddMilliseconds(HideDelayMs);
            }
        }

        private UIElement BuildPinVisual(Pin pin)
        {
            string key = IconRepository.ResolveKey(pin.Icon);
            if (key != IconCatalog.AppIconKey)
            {
                UIElement lineIcon = IconRepository.CreateIconElement(key, _iconSize, _iconBrush);
                if (lineIcon != null) return lineIcon;
            }

            ImageSource icon = Icons.Load(pin.Path);
            if (icon != null)
            {
                var image = new Image
                {
                    Source = icon,
                    Width = _iconSize,
                    Height = _iconSize,
                    Stretch = Stretch.Uniform,
                };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                return image;
            }

            string label = string.IsNullOrWhiteSpace(pin.Name)
                ? "?"
                : pin.Name.Substring(0, 1).ToUpperInvariant();

            return new TextBlock
            {
                Text = label,
                FontSize = Math.Max(12, Math.Round(_iconSize * 0.62)),
                FontWeight = FontWeights.SemiBold,
                Foreground = FallbackTextBrush,
            };
        }

        private ContextMenu BuildAppMenu(Pin pin)
        {
            var changeItem = new MenuItem { Header = "Change icon…" };
            changeItem.Click += (_, __) => ChangeIcon(pin);

            var removeItem = new MenuItem { Header = "Remove from dock" };
            removeItem.Click += (_, __) => RemovePin(pin);

            var menu = new ContextMenu();
            menu.Items.Add(changeItem);
            menu.Items.Add(removeItem);
            menu.Opened += (_, __) => _modalDepth++;
            menu.Closed += (_, __) => _modalDepth = Math.Max(0, _modalDepth - 1);
            return menu;
        }

        private void ChangeIcon(Pin pin)
        {
            ImageSource appIcon = Icons.Load(pin.Path);

            var picker = new IconPickerWindow(pin.Icon, pin.Name, appIcon, _iconColor)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.Manual,
            };

            Rect work = SystemParameters.WorkArea;
            picker.Left = Math.Max(work.Left + 8, Left - picker.Width - 14);
            picker.Top = Math.Max(work.Top + 8,
                Math.Min(Top + (ActualHeight - picker.Height) / 2, work.Bottom - picker.Height - 8));

            _modalDepth++;
            try
            {
                if (picker.ShowDialog() == true)
                {
                    pin.Icon = picker.SelectedKey;
                    PinStore.Save(_pins);
                    RebuildButtons();
                }
            }
            finally
            {
                _modalDepth = Math.Max(0, _modalDepth - 1);
            }
        }

        private void RemovePin(Pin pin)
        {
            _pins.Remove(pin);
            PinStore.Save(_pins);
            RebuildButtons();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Add app to dock",
                Filter = "Apps and shortcuts (*.exe;*.lnk;*.bat;*.cmd;*.msc;*.appref-ms)|*.exe;*.lnk;*.bat;*.cmd;*.msc;*.appref-ms|All files (*.*)|*.*",
                Multiselect = true,
                CheckFileExists = true,
            };

            _modalDepth++;
            bool? result;
            try
            {
                result = dialog.ShowDialog(this);
            }
            finally
            {
                _modalDepth = Math.Max(0, _modalDepth - 1);
            }

            if (result != true) return;

            foreach (string path in dialog.FileNames)
            {
                if (_pins.Exists(p => string.Equals(p.Path, path, StringComparison.OrdinalIgnoreCase)))
                    continue;

                _pins.Add(new Pin
                {
                    Name = Path.GetFileNameWithoutExtension(path),
                    Path = path,
                });
            }

            PinStore.Save(_pins);
            RebuildButtons();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowSettings(null);
        }

        private void ShowSettings(string moduleId)
        {
            if (_services == null) return;

            var settings = new SettingsWindow(_services)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.Manual,
                InitialModuleId = moduleId,
            };

            Rect work = SystemParameters.WorkArea;
            settings.Left = Math.Max(work.Left + 8, Left - settings.Width - 14);
            settings.Top = Math.Max(work.Top + 8,
                Math.Min(Top + (ActualHeight - settings.Height) / 2, work.Bottom - settings.Height - 8));

            settings.AutoHideChanged += enabled =>
            {
                if (enabled)
                {
                    _hideDeadline = _dockShown
                        ? DateTime.UtcNow.AddMilliseconds(HideDelayMs)
                        : DateTime.MaxValue;
                }
                else
                {
                    _hideDeadline = DateTime.MaxValue;
                    if (!_dockShown) ShowDock();
                }
            };

            settings.IconColorChanged += color =>
            {
                _iconColor = color;
                _iconBrush = CreateFrozenBrush(color);
                RebuildButtons();
                RebuildModuleButtons();
            };

            settings.IconSizeChanged += size =>
            {
                _iconSize = AppSettings.NormalizeIconSize(size);
                RebuildButtons();
                RebuildModuleButtons();
            };

            _modalDepth++;
            try
            {
                settings.ShowDialog();
            }
            finally
            {
                _modalDepth = Math.Max(0, _modalDepth - 1);
            }
        }

        internal bool IsDockVisible => IsVisible && _dockShown;

        internal bool CanHideToTray => !_hiddenByTray && _modalDepth == 0;

        internal void MinimizeToTray()
        {
            if (_closing || !CanHideToTray) return;

            _hiddenByTray = true;
            _keepShownUntilHover = false;
            _dockShown = false;
            _hideDeadline = DateTime.MaxValue;
            _slideTimer.Stop();
            _modules?.ClosePopups();
            _appBar?.Unregister();
            Hide();
        }

        internal void RestoreFromTray()
        {
            if (_closing) return;

            if (_suppressedByFullscreen)
            {
                if (IsFullscreenOnDockMonitor())
                {
                    Services.Log.Info("Tray restore skipped; a fullscreen app is active.");
                    return;
                }

                _suppressedByFullscreen = false;
            }

            _hiddenByTray = false;
            if (!IsVisible) Show();

            if (!_dockShown) ShowDock();
            if (!_dockShown) return;

            _keepShownUntilHover = true;
            _hideDeadline = DateTime.MaxValue;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Launch(Pin pin)
        {
            try
            {
                var startInfo = new ProcessStartInfo(pin.Path) { UseShellExecute = true };
                string directory = Path.GetDirectoryName(pin.Path);
                if (!string.IsNullOrEmpty(directory)) startInfo.WorkingDirectory = directory;

                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not start \"{pin.Name}\".\n\n{ex.Message}",
                    "Right Dock", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static Color ParseIconColor(string value)
        {
            try
            {
                if (ColorConverter.ConvertFromString(value) is Color parsed)
                {
                    return Color.FromRgb(parsed.R, parsed.G, parsed.B);
                }
            }
            catch
            {
                // Fall through to the default color.
            }

            return DefaultIconColor;
        }

        private static Brush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
