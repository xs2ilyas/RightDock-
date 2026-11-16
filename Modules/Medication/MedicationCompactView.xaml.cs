using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using RightDock.Data;

namespace RightDock.Modules.Medication
{
    public partial class MedicationCompactView : UserControl
    {
        private static readonly Brush RowTitleBrush = ModuleUtil.Brush("#EDEDED");
        private static readonly Brush RowMetaBrush = ModuleUtil.Brush("#8C8C8C");
        private static readonly Brush DividerBrush = ModuleUtil.Brush("#1AFFFFFF");

        private readonly MedicationModule _module;
        private bool _syncing;

        internal MedicationCompactView(MedicationModule module)
        {
            _module = module;
            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _module.Changed += Refresh;
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
                TakenValue.Text = _module.TakenToday + " of " + _module.PlannedToday;

                MedicationDose next = _module.NextDose;
                NextValue.Text = next == null ? "—" : next.TimeLabel;
                NextName.Text = next != null
                    ? next.MedicationName
                    : _module.PlannedToday == 0
                        ? "No doses today"
                        : "All doses handled";

                DosePanel.Children.Clear();
                EmptyText.Visibility = _module.TodayDoses.Count == 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                List<MedicationDose> doses = _module.TodayDoses.Take(5).ToList();
                for (int i = 0; i < doses.Count; i++)
                {
                    DosePanel.Children.Add(BuildRow(doses[i], i == doses.Count - 1));
                }
            }
            finally
            {
                _syncing = false;
            }
        }

        private FrameworkElement BuildRow(MedicationDose dose, bool last)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var time = new TextBlock
            {
                Text = dose.TimeLabel,
                Foreground = RowTitleBrush,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var text = new StackPanel
            {
                Margin = new Thickness(6, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            text.Children.Add(new TextBlock
            {
                Text = dose.MedicationName,
                Foreground = RowTitleBrush,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            text.Children.Add(new TextBlock
            {
                Text = BuildMeta(dose),
                Foreground = MedicationFormat.StatusBrush(dose),
                FontSize = 10.5,
                Margin = new Thickness(0, 1, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            Grid.SetColumn(text, 1);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
            };

            if (dose.IsActionable)
            {
                var take = new Button
                {
                    Style = (Style)FindResource("MdGhostButton"),
                    Content = "Take",
                    FontSize = 10.5,
                    Padding = new Thickness(3, 1, 3, 1),
                };
                AutomationProperties.SetName(take, "Take " + dose.MedicationName);
                take.Click += (_, __) =>
                {
                    if (!_syncing) _module.Take(dose.LogId);
                };

                var skip = new Button
                {
                    Style = (Style)FindResource("MdGhostButton"),
                    Content = "Skip",
                    FontSize = 10.5,
                    Padding = new Thickness(3, 1, 3, 1),
                    Foreground = MedicationFormat.SkippedBrush,
                };
                AutomationProperties.SetName(skip, "Skip " + dose.MedicationName);
                skip.Click += (_, __) =>
                {
                    if (!_syncing) _module.Skip(dose.LogId);
                };

                actions.Children.Add(take);
                actions.Children.Add(skip);
            }

            Grid.SetColumn(actions, 2);

            grid.Children.Add(time);
            grid.Children.Add(text);
            grid.Children.Add(actions);

            return new Border
            {
                BorderBrush = DividerBrush,
                BorderThickness = last ? new Thickness(0) : new Thickness(0, 0, 0, 1),
                Padding = new Thickness(2, 5, 2, 5),
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

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            _module.OpenDashboard();
        }
    }
}
