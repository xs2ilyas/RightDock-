using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RightDock.Data;

namespace RightDock.Modules.Notes
{
    public partial class NotesCompactView : UserControl
    {
        private static readonly Brush RowTitleBrush = ModuleUtil.Brush("#EDEDED");
        private static readonly Brush RowMetaBrush = ModuleUtil.Brush("#8C8C8C");
        private static readonly Brush PinBrush = ModuleUtil.Brush("#9CC7E9");
        private static readonly Brush DividerBrush = ModuleUtil.Brush("#1AFFFFFF");

        private readonly NotesModule _module;
        private bool _syncing;

        internal NotesCompactView(NotesModule module)
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
                List<NoteItem> notes = _module.QueryAll();
                CountText.Text = notes.Count == 0 ? string.Empty : notes.Count + " saved";
                EmptyText.Visibility = notes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                NotePanel.Children.Clear();
                foreach (NoteItem note in notes.Take(5))
                {
                    NotePanel.Children.Add(BuildRow(note));
                }
            }
            finally
            {
                _syncing = false;
            }
        }

        private FrameworkElement BuildRow(NoteItem note)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var pin = new Border
            {
                Width = 6,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = note.IsPinned ? PinBrush : Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 7, 0),
            };
            if (note.IsPinned) pin.ToolTip = "Pinned";

            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock
            {
                Text = note.DisplayTitle,
                Foreground = RowTitleBrush,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });

            var parts = new List<string> { ModuleUtil.FormatHistoryTime(note.UpdatedUtc) };
            if (!string.IsNullOrWhiteSpace(note.Category)) parts.Add(note.Category);
            text.Children.Add(new TextBlock
            {
                Text = string.Join("  ·  ", parts),
                Foreground = RowMetaBrush,
                FontSize = 10,
                Margin = new Thickness(0, 1, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            Grid.SetColumn(text, 1);

            grid.Children.Add(pin);
            grid.Children.Add(text);

            var row = new Button
            {
                Style = (Style)FindResource("MdRowButton"),
                Content = grid,
                Tag = note.Id,
                Margin = new Thickness(-4, 0, -4, 0),
            };
            AutomationProperties.SetName(row, "Edit note " + note.DisplayTitle);
            row.Click += (_, __) =>
            {
                if (!_syncing) _module.EditFromCompact(note.Id);
            };

            return new Border
            {
                BorderBrush = DividerBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = row,
            };
        }

        private void Capture_Click(object sender, RoutedEventArgs e)
        {
            SubmitCapture();
        }

        private void Capture_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true;
                SubmitCapture();
            }
        }

        private void SubmitCapture()
        {
            string body = CaptureInput.Text;
            if (string.IsNullOrWhiteSpace(body)) return;

            _module.Capture(body);
            CaptureInput.Text = string.Empty;
            CaptureInput.Focus();
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            _module.OpenDashboard();
        }
    }
}
