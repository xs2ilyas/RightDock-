using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using RightDock.Data;

namespace RightDock.Modules.Medication
{
    public partial class MedicationExpandedView : UserControl
    {
        private static readonly Brush RowTitleBrush = ModuleUtil.Brush("#EDEDED");
        private static readonly Brush RowMetaBrush = ModuleUtil.Brush("#8C8C8C");
        private static readonly Brush SelectedRowBrush = ModuleUtil.Brush("#16FFFFFF");
        private static readonly Brush DividerBrush = ModuleUtil.Brush("#1AFFFFFF");

        private readonly MedicationModule _module;
        private string _editingId;
        private string _days = MedicationDays.EveryDay;
        private string _view = "today";
        private bool _deleteArmed;
        private bool _syncing;

        internal MedicationExpandedView(MedicationModule module)
        {
            _module = module;
            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _module.Changed += Refresh;
            ClearEditor();
            Refresh();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _module.Changed -= Refresh;
        }

        private void Refresh()
        {
            _syncing = true;
            try
            {
                ViewToday.IsChecked = _view == "today";
                ViewWeek.IsChecked = _view == "week";

                DosePanel.Children.Clear();

                if (_view == "week")
                {
                    BuildWeek();
                }
                else
                {
                    BuildToday();
                }

                BuildMedications();

                if (_editingId != null && _module.Medications.All(item => item.Id != _editingId))
                {
                    ClearEditor();
                }
            }
            finally
            {
                _syncing = false;
            }
        }

        private void BuildToday()
        {
            List<MedicationDose> doses = _module.TodayDoses.ToList();

            ListCountText.Text = doses.Count == 0
                ? string.Empty
                : _module.TakenToday + " of " + doses.Count + " taken";
            EmptyText.Text = "No doses scheduled today.";
            EmptyText.Visibility = doses.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            foreach (MedicationDose dose in doses)
            {
                DosePanel.Children.Add(BuildDoseRow(dose, true));
            }
        }

        private void BuildWeek()
        {
            List<MedicationDose> doses = _module.WeekDoses.ToList();

            ListCountText.Text = doses.Count == 0
                ? string.Empty
                : doses.Count + (doses.Count == 1 ? " dose" : " doses");
            EmptyText.Text = "No doses in the last 7 days.";
            EmptyText.Visibility = doses.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            foreach (IGrouping<DateTime, MedicationDose> day in doses
                .GroupBy(dose => dose.LocalTime.Date)
                .OrderByDescending(group => group.Key))
            {
                DosePanel.Children.Add(new TextBlock
                {
                    Text = day.Key == DateTime.Today ? "Today" : day.Key.ToString("ddd, MMM d"),
                    Foreground = RowMetaBrush,
                    FontSize = 10.5,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(2, 10, 0, 4),
                });

                foreach (MedicationDose dose in day.OrderBy(item => item.ScheduledUtc))
                {
                    DosePanel.Children.Add(BuildDoseRow(dose, false));
                }
            }
        }

        private FrameworkElement BuildDoseRow(MedicationDose dose, bool showActions)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var time = new TextBlock
            {
                Text = dose.LocalTime.ToString("HH:mm"),
                Foreground = RowTitleBrush,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var text = new StackPanel
            {
                Margin = new Thickness(6, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            text.Children.Add(new TextBlock
            {
                Text = dose.MedicationName,
                Foreground = RowTitleBrush,
                FontSize = 12.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            text.Children.Add(new TextBlock
            {
                Text = BuildMeta(dose),
                Foreground = MedicationFormat.StatusBrush(dose),
                FontSize = 10.5,
                Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            Grid.SetColumn(text, 1);

            if (showActions && dose.IsActionable)
            {
                var actions = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                };

                var take = new Button
                {
                    Style = (Style)FindResource("MdGhostButton"),
                    Content = "Take",
                    FontSize = 10.5,
                    Padding = new Thickness(3, 1, 3, 1),
                };
                AutomationProperties.SetName(take, "Take " + dose.MedicationName + " at " + dose.TimeLabel);
                take.Click += (_, __) =>
                {
                    if (!_syncing) _module.Take(dose.LogId);
                };

                var snooze = new Button
                {
                    Style = (Style)FindResource("MdGhostButton"),
                    Content = "Snooze",
                    FontSize = 10.5,
                    Padding = new Thickness(3, 1, 3, 1),
                };
                AutomationProperties.SetName(snooze, "Snooze " + dose.MedicationName + " at " + dose.TimeLabel);
                snooze.Click += (_, __) => OpenSnoozeMenu(snooze, dose);

                var skip = new Button
                {
                    Style = (Style)FindResource("MdGhostButton"),
                    Content = "Skip",
                    FontSize = 10.5,
                    Padding = new Thickness(3, 1, 3, 1),
                    Foreground = MedicationFormat.SkippedBrush,
                };
                AutomationProperties.SetName(skip, "Skip " + dose.MedicationName + " at " + dose.TimeLabel);
                skip.Click += (_, __) =>
                {
                    if (!_syncing) _module.Skip(dose.LogId);
                };

                actions.Children.Add(take);
                actions.Children.Add(snooze);
                actions.Children.Add(skip);
                Grid.SetColumn(actions, 2);
                grid.Children.Add(actions);
            }

            grid.Children.Add(time);
            grid.Children.Add(text);

            return new Border
            {
                BorderBrush = DividerBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(2, 6, 2, 6),
                Child = grid,
            };
        }

        private static string BuildMeta(MedicationDose dose)
        {
            string meta = MedicationFormat.StatusLabel(dose);
            if (!string.IsNullOrWhiteSpace(dose.Dosage))
            {
                meta = dose.Dosage + "  ·  " + meta;
            }

            return meta;
        }

        private void BuildMedications()
        {
            MedPanel.Children.Clear();

            List<MedicationItem> items = _module.Medications.ToList();
            MedCountText.Text = items.Count == 0 ? string.Empty : items.Count.ToString();
            EmptyMeds.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            foreach (MedicationItem item in items)
            {
                bool selected = string.Equals(item.Id, _editingId, StringComparison.Ordinal);

                var text = new StackPanel();
                text.Children.Add(new TextBlock
                {
                    Text = item.Name,
                    Foreground = RowTitleBrush,
                    FontSize = 12,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                });
                text.Children.Add(new TextBlock
                {
                    Text = (string.IsNullOrWhiteSpace(item.Dosage) ? string.Empty : item.Dosage + "  ·  ")
                        + item.ScheduleSummary,
                    Foreground = RowMetaBrush,
                    FontSize = 10.5,
                    Margin = new Thickness(0, 1, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                });

                var row = new Button
                {
                    Style = (Style)FindResource("MdRowButton"),
                    Content = text,
                    Background = selected ? SelectedRowBrush : Brushes.Transparent,
                };
                AutomationProperties.SetName(row, "Edit medication " + item.Name);
                row.Click += (_, __) =>
                {
                    if (_syncing) return;
                    LoadEditor(item.Id);
                    Refresh();
                };

                MedPanel.Children.Add(new Border
                {
                    BorderBrush = DividerBrush,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Child = row,
                });
            }
        }

        private void OpenSnoozeMenu(FrameworkElement anchor, MedicationDose dose)
        {
            var menu = new ContextMenu();
            foreach ((string label, int minutes) in new[]
            {
                ("10 minutes", 10), ("15 minutes", 15), ("30 minutes", 30), ("1 hour", 60),
            })
            {
                var entry = new MenuItem { Header = label };
                entry.Click += (_, __) => _module.Snooze(dose.LogId, minutes);
                menu.Items.Add(entry);
            }

            menu.PlacementTarget = anchor;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        private void LoadEditor(string medicationId)
        {
            MedicationItem item = _module.Get(medicationId);
            if (item == null)
            {
                ClearEditor();
                return;
            }

            _editingId = item.Id;
            EditorTitle.Text = "Edit medication";
            NameInput.Text = item.Name ?? string.Empty;
            DosageInput.Text = item.Dosage ?? string.Empty;
            NotesInput.Text = item.Notes ?? string.Empty;
            TimesInput.Text = string.Join(", ", item.Schedules.Select(schedule => schedule.TimeOfDay));
            _days = item.Schedules.Count > 0 ? item.Schedules[0].DaysOfWeek : MedicationDays.EveryDay;

            UpdateDaysChips();
            DisarmDelete();
            DeleteButton.IsEnabled = true;
            StatusText.Text = string.Empty;
            HideError();
        }

        private void ClearEditor()
        {
            _editingId = null;
            EditorTitle.Text = "New medication";
            NameInput.Text = string.Empty;
            DosageInput.Text = string.Empty;
            NotesInput.Text = string.Empty;
            TimesInput.Text = string.Empty;
            _days = MedicationDays.EveryDay;

            UpdateDaysChips();
            DisarmDelete();
            DeleteButton.IsEnabled = false;
            StatusText.Text = string.Empty;
            HideError();
        }

        private void UpdateDaysChips()
        {
            DaysEvery.IsChecked = _days == MedicationDays.EveryDay;
            DaysWeekdays.IsChecked = _days == MedicationDays.Weekdays;
            DaysWeekends.IsChecked = _days == MedicationDays.Weekends;
        }

        private void Days_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string days)
            {
                _days = days;
                UpdateDaysChips();
                StatusText.Text = string.Empty;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            HideError();

            string name = NameInput.Text.Trim();
            if (name.Length == 0)
            {
                ShowError("Give the medication a name.");
                return;
            }

            if (!TryParseTimes(TimesInput.Text, out List<string> times))
            {
                ShowError("Enter at least one time as HH:mm, for example 08:00, 20:00.");
                return;
            }

            var medication = new MedicationItem
            {
                Id = _editingId,
                Name = name,
                Dosage = DosageInput.Text,
                Notes = NotesInput.Text,
            };

            string id = _module.Save(medication, times, _days);
            if (id == null)
            {
                ShowError("Could not save the medication.");
                return;
            }

            _editingId = id;
            EditorTitle.Text = "Edit medication";
            DeleteButton.IsEnabled = true;
            DisarmDelete();
            StatusText.Text = "Saved";
        }

        private static bool TryParseTimes(string text, out List<string> times)
        {
            times = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return false;

            foreach (string part in text.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!TimeSpan.TryParse(part, CultureInfo.InvariantCulture, out TimeSpan parsed)
                    || parsed < TimeSpan.Zero || parsed >= TimeSpan.FromDays(1))
                {
                    return false;
                }

                string normalized = ((int)parsed.TotalHours).ToString("00") + ":"
                    + parsed.Minutes.ToString("00");
                if (!times.Contains(normalized)) times.Add(normalized);
            }

            times.Sort(StringComparer.Ordinal);
            return times.Count > 0;
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            ClearEditor();
            NameInput.Focus();
        }

        private void New_Click(object sender, RoutedEventArgs e)
        {
            ClearEditor();
            NameInput.Focus();
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_editingId)) return;

            if (!_deleteArmed)
            {
                _deleteArmed = true;
                DeleteButton.Content = "Confirm delete";
                StatusText.Text = "Click delete again to remove this medication and its history.";
                return;
            }

            _module.Delete(_editingId);
            ClearEditor();
            StatusText.Text = "Medication deleted";
        }

        private void DisarmDelete()
        {
            _deleteArmed = false;
            DeleteButton.Content = "Delete";
        }

        private void View_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            if (sender is FrameworkElement element && element.Tag is string view)
            {
                _view = view;
                Refresh();
            }
        }

        private void Editor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncing) return;
            StatusText.Text = string.Empty;
            HideError();
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void HideError()
        {
            ErrorText.Visibility = Visibility.Collapsed;
        }
    }
}
