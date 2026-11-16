using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using RightDock.Data;
using RightDock.Modules;

namespace RightDock
{
    public partial class SettingsWindow : Window
    {
        public event Action<bool> AutoHideChanged;
        public event Action<Color> IconColorChanged;

        private static readonly Color[] Presets =
        {
            Color.FromRgb(0xA9, 0xA9, 0xA9),
            Color.FromRgb(0xF5, 0xF5, 0xF5),
            Color.FromRgb(0x5A, 0xB0, 0xF2),
            Color.FromRgb(0x39, 0xC2, 0xB6),
            Color.FromRgb(0x63, 0xC1, 0x74),
            Color.FromRgb(0xE6, 0xC8, 0x4F),
            Color.FromRgb(0xF2, 0x99, 0x4A),
            Color.FromRgb(0xEF, 0x6B, 0x6B),
            Color.FromRgb(0xE5, 0x6B, 0xA8),
            Color.FromRgb(0xA8, 0x8B, 0xEB),
        };

        private readonly DockServices _services;
        private readonly AppSettings _settings;
        private readonly ModuleManager _modules;
        private readonly List<Button> _swatchButtons = new List<Button>();
        private Button _customSwatch;
        private Brush _iconBrush;
        private Color _currentColor;
        private readonly DataBackupService _backup;
        private string _diagnostics;
        private bool _loading = true;

        internal SettingsWindow(DockServices services)
        {
            InitializeComponent();

            _services = services;
            _settings = services.Settings;
            _modules = services.Modules;
            _backup = services.Backup;
            _currentColor = ParseColor(_settings.IconColor, Presets[0]);
            _iconBrush = CreateFrozenBrush(_currentColor);

            BuildSwatches();
            RebuildModulesPage();
            SelectTab(0);

            AutoHideToggle.IsChecked = _settings.AutoHide;
            NotificationsToggle.IsChecked = _settings.NotificationsEnabled;
            StartupToggle.IsChecked = StartupRegistration.IsEnabled();
            StartupPathRun.Text = StartupRegistration.ExecutablePath;

            DataPathText.Text = _services.Data.Path;
            AboutText.Text = "RightDock " + DataBackupService.AppVersion + " · .NET 5 · schema v"
                + _services.Data.SchemaVersion;
            RefreshDataSummary();

            AutoHideToggle.Checked += AutoHideToggled;
            AutoHideToggle.Unchecked += AutoHideToggled;
            NotificationsToggle.Checked += NotificationsToggled;
            NotificationsToggle.Unchecked += NotificationsToggled;
            StartupToggle.Checked += StartupToggled;
            StartupToggle.Unchecked += StartupToggled;

            KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) Close();
            };

            HeaderBar.MouseLeftButtonDown += (_, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed) DragMove();
            };

            _loading = false;
        }

        private void BuildSwatches()
        {
            foreach (Color color in Presets)
            {
                Color captured = color;
                var button = new Button
                {
                    Style = (Style)FindResource("Swatch"),
                    Background = new SolidColorBrush(captured),
                };
                button.Click += (_, __) => ApplyColor(captured);

                SwatchPanel.Children.Add(button);
                _swatchButtons.Add(button);
            }

            _customSwatch = new Button
            {
                Style = (Style)FindResource("Swatch"),
                Background = (Brush)FindResource("Rainbow"),
                ToolTip = "Custom color",
            };
            _customSwatch.Click += (_, __) => PickCustomColor();
            SwatchPanel.Children.Add(_customSwatch);

            UpdateSwatches();
        }

        private void UpdateSwatches()
        {
            for (int i = 0; i < Presets.Length; i++)
            {
                SetSwatchSelected(_swatchButtons[i], Presets[i] == _currentColor,
                    IsLight(Presets[i]) ? Brushes.Black : Brushes.White);
            }

            bool custom = Array.IndexOf(Presets, _currentColor) < 0;
            SetSwatchSelected(_customSwatch, custom, custom ? Brushes.Black : Brushes.White);

            ColorValueText.Text = "Current color   " + ToHex(_currentColor);
        }

        private static void SetSwatchSelected(Button button, bool selected, Brush checkBrush)
        {
            button.Tag = selected ? "1" : null;
            button.BorderBrush = selected ? Brushes.White : Brushes.Transparent;
            button.Foreground = checkBrush;
        }

        private void RebuildModulesPage()
        {
            ModulesList.Children.Clear();

            List<ModuleEntry> entries = _modules?.OrderedEntries() ?? new List<ModuleEntry>();
            ModulesEmptyText.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            foreach (ModuleEntry entry in entries)
            {
                ModulesList.Children.Add(BuildModuleRow(entry));
            }
        }

        private UIElement BuildModuleRow(ModuleEntry entry)
        {
            var row = new Grid { Margin = new Thickness(2, 0, 2, 14) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconHost = new ContentControl
            {
                Width = 20,
                Height = 20,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 10, 0),
            };

            FrameworkElement icon = IconRepository.CreateIconElement(entry.Module.IconKey, 20, _iconBrush);
            if (icon != null) iconHost.Content = icon;

            row.Children.Add(iconHost);

            var text = new StackPanel { Opacity = entry.State.IsEnabled ? 1.0 : 0.55 };

            text.Children.Add(new TextBlock
            {
                Text = entry.Module.Name,
                Foreground = new SolidColorBrush(Color.FromRgb(0xED, 0xED, 0xED)),
                FontSize = 12.5,
            });

            if (!string.IsNullOrWhiteSpace(entry.Module.Description))
            {
                text.Children.Add(new TextBlock
                {
                    Text = entry.Module.Description,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x8C, 0x8C, 0x8C)),
                    FontSize = 10.5,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 3, 12, 0),
                });
            }

            var visibility = new Button
            {
                Style = (Style)FindResource("LinkButton"),
                Content = entry.State.IsVisible ? "Hide from dock" : "Show in dock",
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 4, 0, 0),
            };

            visibility.Click += (_, __) =>
            {
                _modules.SetVisible(entry.Module.Id, !entry.State.IsVisible);
                RebuildModulesPage();
            };

            text.Children.Add(visibility);

            Grid.SetColumn(text, 1);
            row.Children.Add(text);

            var toggle = new ToggleButton
            {
                Style = (Style)FindResource("ToggleSwitch"),
                VerticalAlignment = VerticalAlignment.Top,
                IsChecked = entry.State.IsEnabled,
            };

            toggle.Checked += (_, __) =>
            {
                _modules.SetEnabled(entry.Module.Id, true);
                RebuildModulesPage();
            };

            toggle.Unchecked += (_, __) =>
            {
                _modules.SetEnabled(entry.Module.Id, false);
                RebuildModulesPage();
            };

            Grid.SetColumn(toggle, 2);
            row.Children.Add(toggle);

            return row;
        }

        private void ApplyColor(Color color)
        {
            _currentColor = Color.FromRgb(color.R, color.G, color.B);
            _iconBrush = CreateFrozenBrush(_currentColor);
            _settings.IconColor = ToHex(_currentColor);
            SettingsStore.Save(_settings);

            UpdateSwatches();
            RebuildModulesPage();
            IconColorChanged?.Invoke(_currentColor);
        }

        private void PickCustom_Click(object sender, RoutedEventArgs e)
        {
            PickCustomColor();
        }

        private void PickCustomColor()
        {
            using (var dialog = new System.Windows.Forms.ColorDialog
            {
                FullOpen = true,
                Color = System.Drawing.Color.FromArgb(_currentColor.R, _currentColor.G, _currentColor.B),
            })
            {
                var owner = new Win32Window(new WindowInteropHelper(this).Handle);
                if (dialog.ShowDialog(owner) == System.Windows.Forms.DialogResult.OK)
                {
                    ApplyColor(Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B));
                }
            }
        }

        private void AutoHideToggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;

            bool enabled = AutoHideToggle.IsChecked == true;
            _settings.AutoHide = enabled;
            SettingsStore.Save(_settings);
            AutoHideChanged?.Invoke(enabled);
        }

        private void NotificationsToggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;

            _settings.NotificationsEnabled = NotificationsToggle.IsChecked == true;
            SettingsStore.Save(_settings);
            NotificationStatus.Visibility = Visibility.Collapsed;
        }

        private void TestNotification_Click(object sender, RoutedEventArgs e)
        {
            bool shown = _services?.Notifications.Show(
                "RightDock", "Notifications are working. This is a test message.") == true;

            NotificationStatus.Text = shown ? "Test notification sent." : "Notifications are disabled.";
            NotificationStatus.Foreground = new SolidColorBrush(shown
                ? Color.FromRgb(0x63, 0xC1, 0x74)
                : Color.FromRgb(0xFF, 0x8A, 0x8A));
            NotificationStatus.Visibility = Visibility.Visible;
        }

        private void StartupToggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;

            bool enabled = StartupToggle.IsChecked == true;

            try
            {
                StartupRegistration.SetEnabled(enabled);
                StartupError.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                StartupError.Text = "Could not update Windows startup: " + ex.Message;
                StartupError.Visibility = Visibility.Visible;

                _loading = true;
                StartupToggle.IsChecked = !enabled;
                _loading = false;
            }
        }

        private void Tab_Click(object sender, RoutedEventArgs e)
        {
            if (sender == TabGeneral) SelectTab(0);
            else if (sender == TabAppearance) SelectTab(1);
            else if (sender == TabModules) SelectTab(2);
            else if (sender == TabNotifications) SelectTab(3);
            else if (sender == TabData) SelectTab(4);
            else SelectTab(5);
        }

        private void SelectTab(int index)
        {
            PageGeneral.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
            PageAppearance.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
            PageModules.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
            PageNotifications.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
            PageData.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;
            PageStartup.Visibility = index == 5 ? Visibility.Visible : Visibility.Collapsed;

            TabGeneral.Tag = index == 0 ? "1" : null;
            TabAppearance.Tag = index == 1 ? "1" : null;
            TabModules.Tag = index == 2 ? "1" : null;
            TabNotifications.Tag = index == 3 ? "1" : null;
            TabData.Tag = index == 4 ? "1" : null;
            TabStartup.Tag = index == 5 ? "1" : null;
        }

        private void RefreshDataSummary()
        {
            try
            {
                BackupInfo info = _backup.GetCurrentInfo();
                DataSummaryText.Text = info.SchemaVersion > 0
                    ? "SQLite · schema v" + info.SchemaVersion + " · " + BackupInfo.FormatBytes(info.DatabaseBytes)
                        + " · " + info.TotalRows.ToString("N0") + " rows"
                    : "Database unavailable" + (string.IsNullOrWhiteSpace(_services.Data.LastError)
                        ? "."
                        : ": " + _services.Data.LastError);
            }
            catch (Exception ex)
            {
                DataSummaryText.Text = "Could not read the database: " + ex.Message;
            }
        }

        private void RunDiagnostics_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BackupInfo info = _backup.GetCurrentInfo();
                bool ok = string.Equals(info.Integrity, "ok", StringComparison.OrdinalIgnoreCase);

                SetStatus(DataStatusText,
                    ok
                        ? "Integrity check passed · schema v" + info.SchemaVersion + " · "
                            + BackupInfo.FormatBytes(info.DatabaseBytes) + " · "
                            + info.TotalRows.ToString("N0") + " rows"
                        : "Integrity check failed: " + info.Integrity,
                    ok);

                _diagnostics = BuildDiagnosticsReport(info);
                RefreshDataSummary();
            }
            catch (Exception ex)
            {
                SetStatus(DataStatusText, "Diagnostics failed: " + ex.Message, false);
            }
        }

        private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(PinStore.DataFolder);
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + PinStore.DataFolder + "\"")
                {
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                SetStatus(DataStatusText, "Could not open the data folder: " + ex.Message, false);
            }
        }

        private void CreateBackup_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Create RightDock backup",
                Filter = "RightDock backup (*.zip)|*.zip|All files (*.*)|*.*",
                DefaultExt = ".zip",
                AddExtension = true,
                FileName = "RightDock-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            };

            if (dialog.ShowDialog(this) != true) return;

            try
            {
                BackupInfo info = _backup.CreateBackup(dialog.FileName);
                SetStatus(BackupStatusText,
                    "Backup created · " + info.Summary + " · " + Path.GetFileName(dialog.FileName), true);
            }
            catch (Exception ex)
            {
                SetStatus(BackupStatusText, "Backup failed: " + ex.Message, false);
            }
        }

        private void RestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Restore RightDock backup",
                Filter = "RightDock backup (*.zip)|*.zip|All files (*.*)|*.*",
                CheckFileExists = true,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            };

            if (dialog.ShowDialog(this) != true) return;

            try
            {
                BackupInfo info = _backup.InspectBackup(dialog.FileName);

                MessageBoxResult result = MessageBox.Show(this,
                    "Restore this backup?\n\n"
                    + info.Summary + "\nCreated "
                    + info.CreatedUtc.ToLocalTime().ToString("MMM d, yyyy HH:mm") + "\n\n"
                    + "RightDock will restart and replace your current data. A safety copy of the "
                    + "current database is kept in the backup folder.",
                    "Restore RightDock data", MessageBoxButton.OKCancel, MessageBoxImage.Warning);

                if (result != MessageBoxResult.OK) return;

                _backup.StageRestore(dialog.FileName);
                SetStatus(RestoreStatusText, "Restore prepared. Restarting RightDock…", true);
                RestartForRestore();
            }
            catch (Exception ex)
            {
                SetStatus(RestoreStatusText, "Restore failed: " + ex.Message, false);
            }
        }

        private void CopyDiagnostics_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_diagnostics))
                {
                    _diagnostics = BuildDiagnosticsReport(_backup.GetCurrentInfo());
                }

                Clipboard.SetText(_diagnostics);
                SetStatus(DataStatusText, "Diagnostics copied to the clipboard.", true);
            }
            catch (Exception ex)
            {
                SetStatus(DataStatusText, "Could not copy diagnostics: " + ex.Message, false);
            }
        }

        private string BuildDiagnosticsReport(BackupInfo info)
        {
            var report = new StringBuilder();
            report.AppendLine("RightDock " + DataBackupService.AppVersion);
            report.AppendLine("OS: " + Environment.OSVersion.VersionString);
            report.AppendLine("Framework: .NET " + Environment.Version);
            report.AppendLine("Database: " + (info.SourcePath ?? PinStore.DataFolder));
            report.AppendLine("Schema: v" + info.SchemaVersion);
            report.AppendLine("Integrity: " + info.Integrity);
            report.AppendLine("Size: " + BackupInfo.FormatBytes(info.DatabaseBytes));
            report.AppendLine("Rows: " + info.TotalRows.ToString("N0"));

            if (_modules != null)
            {
                int enabled = 0;
                int visible = 0;
                foreach (ModuleEntry entry in _modules.Entries)
                {
                    if (entry.State.IsEnabled) enabled++;
                    if (entry.State.IsEnabled && entry.State.IsVisible) visible++;
                }

                report.AppendLine("Modules: " + _modules.Entries.Count + " registered, "
                    + enabled + " enabled, " + visible + " in dock");
            }

            foreach (KeyValuePair<string, long> count in info.Counts)
            {
                report.AppendLine(count.Key + ": " + count.Value);
            }

            return report.ToString().TrimEnd();
        }

        private void RestartForRestore()
        {
            string executable = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(executable))
            {
                SetStatus(RestoreStatusText,
                    "Restore is staged. Close and reopen RightDock to apply it.", false);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(executable, "--restart") { UseShellExecute = true });
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                SetStatus(RestoreStatusText,
                    "Restore is staged. Close and reopen RightDock to apply it. " + ex.Message, false);
            }
        }

        private static void SetStatus(TextBlock block, string text, bool success)
        {
            block.Text = text;
            block.Foreground = new SolidColorBrush(success
                ? Color.FromRgb(0x63, 0xC1, 0x74)
                : Color.FromRgb(0xFF, 0x8A, 0x8A));
            block.Visibility = Visibility.Visible;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private static string ToHex(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        private static bool IsLight(Color color)
        {
            return 0.299 * color.R + 0.587 * color.G + 0.114 * color.B > 150;
        }

        private static Brush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static Color ParseColor(string value, Color fallback)
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
                // Fall through to the fallback color.
            }

            return fallback;
        }

        private sealed class Win32Window : System.Windows.Forms.IWin32Window
        {
            public Win32Window(IntPtr handle)
            {
                Handle = handle;
            }

            public IntPtr Handle { get; }
        }
    }
}
