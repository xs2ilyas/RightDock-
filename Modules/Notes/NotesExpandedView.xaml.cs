using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using RightDock.Data;

namespace RightDock.Modules.Notes
{
    public partial class NotesExpandedView : UserControl
    {
        private static readonly Brush RowTitleBrush = ModuleUtil.Brush("#EDEDED");
        private static readonly Brush RowMetaBrush = ModuleUtil.Brush("#8C8C8C");
        private static readonly Brush PinBrush = ModuleUtil.Brush("#9CC7E9");
        private static readonly Brush SelectedRowBrush = ModuleUtil.Brush("#16FFFFFF");
        private static readonly Brush DividerBrush = ModuleUtil.Brush("#1AFFFFFF");

        private readonly NotesModule _module;
        private string _editingId;
        private bool _editingArchived;
        private bool _deleteArmed;
        private bool _syncing;

        internal NotesExpandedView(NotesModule module)
        {
            _module = module;
            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _module.Changed += Refresh;

            SearchBox.Text = _module.SearchText;

            string pending = _module.TakePendingEdit();
            if (!string.IsNullOrWhiteSpace(pending))
            {
                LoadEditor(pending);
            }
            else
            {
                ClearEditor();
            }

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
                BuildCategoryFilters();

                ArchivedChip.IsChecked = _module.ShowArchived;
                int archivedCount = _module.ArchivedCount();
                ArchiveCountText.Text = archivedCount == 0
                    ? "No archived notes"
                    : archivedCount + (archivedCount == 1 ? " archived note" : " archived notes");

                List<NoteItem> notes = _module.Query();
                ListCountText.Text = notes.Count == 0
                    ? string.Empty
                    : notes.Count + " note" + (notes.Count == 1 ? string.Empty : "s")
                        + (_module.ShowArchived ? " in archive" : string.Empty);
                EmptyText.Visibility = notes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                EmptyText.Text = _module.ShowArchived
                    ? "No archived notes."
                    : !string.IsNullOrWhiteSpace(_module.CategoryFilter)
                        ? "No notes in this category."
                        : "No notes yet.";

                NotePanel.Children.Clear();
                foreach (NoteItem note in notes)
                {
                    NotePanel.Children.Add(BuildRow(note));
                }

                if (!string.IsNullOrEmpty(_editingId) && _module.Get(_editingId) == null)
                {
                    ClearEditor();
                }
                else if (!string.IsNullOrEmpty(_editingId))
                {
                    SyncArchiveState();
                }
                else
                {
                    ArchiveButton.IsEnabled = false;
                    ArchiveButton.Content = "Archive";
                }
            }
            finally
            {
                _syncing = false;
            }
        }

        private void BuildCategoryFilters()
        {
            CategoryPanel.Children.Clear();

            CategoryPanel.Children.Add(BuildCategoryChip(string.Empty, "All categories",
                string.IsNullOrWhiteSpace(_module.CategoryFilter)));

            foreach (string category in _module.Categories())
            {
                CategoryPanel.Children.Add(BuildCategoryChip(category, category,
                    string.Equals(category, _module.CategoryFilter, StringComparison.OrdinalIgnoreCase)));
            }
        }

        private FrameworkElement BuildCategoryChip(string value, string label, bool selected)
        {
            string display = label.Length <= 20 ? label : label.Substring(0, 19) + "…";

            var chip = new ToggleButton
            {
                Style = (Style)FindResource("MdChip"),
                Content = display,
                IsChecked = selected,
                Tag = value,
                ToolTip = label,
            };
            AutomationProperties.SetName(chip, value.Length == 0
                ? "All note categories"
                : "Note category " + value);

            chip.Click += (_, __) =>
            {
                if (!_syncing) _module.SetCategory(value);
            };

            return chip;
        }

        private FrameworkElement BuildRow(NoteItem note)
        {
            bool selected = string.Equals(note.Id, _editingId, StringComparison.Ordinal);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var pin = new Border
            {
                Width = 6,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = note.IsPinned ? PinBrush : Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
            };
            if (note.IsPinned) pin.ToolTip = "Pinned";

            var text = new StackPanel { Margin = new Thickness(8, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock
            {
                Text = note.DisplayTitle,
                Foreground = RowTitleBrush,
                FontSize = 12.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });

            string meta = BuildMeta(note);
            if (!string.IsNullOrEmpty(meta))
            {
                text.Children.Add(new TextBlock
                {
                    Text = meta,
                    Foreground = RowMetaBrush,
                    FontSize = 10.5,
                    Margin = new Thickness(0, 2, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                });
            }
            Grid.SetColumn(text, 1);

            var pinButton = new Button
            {
                Style = (Style)FindResource("MdGhostButton"),
                Content = note.IsPinned ? "Unpin" : "Pin",
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = note.IsPinned ? "Unpin this note" : "Pin this note",
            };
            AutomationProperties.SetName(pinButton, "Pin note " + note.DisplayTitle);
            pinButton.Click += (_, __) =>
            {
                if (!_syncing) _module.SetPinned(note.Id, !note.IsPinned);
            };
            Grid.SetColumn(pinButton, 2);

            grid.Children.Add(pin);
            grid.Children.Add(text);
            grid.Children.Add(pinButton);

            var row = new Button
            {
                Style = (Style)FindResource("MdRowButton"),
                Content = grid,
                Background = selected ? SelectedRowBrush : Brushes.Transparent,
                Tag = note.Id,
            };
            AutomationProperties.SetName(row, "Edit note " + note.DisplayTitle);
            row.Click += (_, __) =>
            {
                if (_syncing) return;
                LoadEditor(note.Id);
                Refresh();
            };

            return new Border
            {
                BorderBrush = DividerBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = row,
            };
        }

        private static string BuildMeta(NoteItem note)
        {
            var parts = new List<string> { ModuleUtil.FormatHistoryTime(note.UpdatedUtc) };
            if (!string.IsNullOrWhiteSpace(note.Category)) parts.Add(note.Category);
            if (note.IsPinned) parts.Add("Pinned");
            if (note.IsArchived) parts.Add("Archived");

            string snippet = Snippet(note.Body);
            if (!string.IsNullOrEmpty(snippet)) parts.Add(snippet);

            return string.Join("  ·  ", parts);
        }

        private static string Snippet(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;

            string text = body.Trim().Replace("\r", " ").Replace("\n", " ");
            while (text.Contains("  ")) text = text.Replace("  ", " ");
            return text.Length <= 60 ? text : text.Substring(0, 59) + "…";
        }

        private void LoadEditor(string noteId)
        {
            NoteItem note = _module.Get(noteId);
            if (note == null)
            {
                ClearEditor();
                return;
            }

            _editingId = note.Id;
            _editingArchived = note.IsArchived;
            EditorTitle.Text = "Edit note";
            TitleInput.Text = note.Title ?? string.Empty;
            CategoryInput.Text = note.Category ?? string.Empty;
            BodyInput.Text = note.Body ?? string.Empty;
            PinChip.IsChecked = note.IsPinned;
            ArchiveButton.Content = note.IsArchived ? "Restore" : "Archive";

            SetExistingState(true);
            StatusText.Text = note.IsArchived ? "Archived note" : string.Empty;
            HideError();
        }

        private void ClearEditor()
        {
            _editingId = null;
            _editingArchived = false;
            EditorTitle.Text = "New note";
            TitleInput.Text = string.Empty;
            CategoryInput.Text = string.Empty;
            BodyInput.Text = string.Empty;
            PinChip.IsChecked = false;
            ArchiveButton.Content = "Archive";

            SetExistingState(false);
            StatusText.Text = string.Empty;
            HideError();
        }

        private void SetExistingState(bool existing)
        {
            DeleteButton.IsEnabled = existing;
            ArchiveButton.IsEnabled = existing;
            ToTaskButton.IsEnabled = existing;
            ToReminderButton.IsEnabled = existing;
            _deleteArmed = false;
            DeleteButton.Content = "Delete";
        }

        private void SyncArchiveState()
        {
            if (string.IsNullOrEmpty(_editingId))
            {
                ArchiveButton.IsEnabled = false;
                ArchiveButton.Content = "Archive";
                return;
            }

            NoteItem note = _module.Get(_editingId);
            if (note == null)
            {
                ArchiveButton.IsEnabled = false;
                return;
            }

            _editingArchived = note.IsArchived;
            ArchiveButton.IsEnabled = true;
            ArchiveButton.Content = note.IsArchived ? "Restore" : "Archive";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            HideError();

            string title = TitleInput.Text.Trim();
            string body = BodyInput.Text.Trim();
            if (title.Length == 0 && body.Length == 0)
            {
                ShowError("Write a title or some text before saving.");
                return;
            }

            var note = new NoteItem
            {
                Id = _editingId,
                Title = title,
                Body = body,
                Category = CategoryInput.Text.Trim(),
                IsPinned = PinChip.IsChecked == true,
            };

            string id = _module.Save(note);
            if (id == null)
            {
                ShowError("Could not save the note.");
                return;
            }

            _editingId = id;
            EditorTitle.Text = "Edit note";
            SetExistingState(true);
            SyncArchiveState();
            StatusText.Text = "Saved";
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            ClearEditor();
            TitleInput.Focus();
        }

        private void New_Click(object sender, RoutedEventArgs e)
        {
            ClearEditor();
            if (!string.IsNullOrWhiteSpace(_module.CategoryFilter))
            {
                CategoryInput.Text = _module.CategoryFilter;
            }
            TitleInput.Focus();
        }

        private void Archive_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_editingId)) return;

            bool archive = !_editingArchived;
            _module.SetArchived(_editingId, archive);
            StatusText.Text = archive ? "Note archived" : "Note restored";
            HideError();
        }

        private void Archived_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            _module.SetShowArchived(ArchivedChip.IsChecked == true);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_editingId)) return;

            if (!_deleteArmed)
            {
                _deleteArmed = true;
                DeleteButton.Content = "Confirm delete";
                StatusText.Text = "Click delete again to remove this note.";
                return;
            }

            _module.Delete(_editingId);
            ClearEditor();
            StatusText.Text = "Note deleted";
        }

        private void Pin_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = string.Empty;
        }

        private void ToTask_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_editingId)) return;

            string message = _module.ConvertToTask(_editingId);
            if (message == null)
            {
                ShowError("Could not create a task from this note.");
                return;
            }

            StatusText.Text = message;
            HideError();
        }

        private void ToReminder_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_editingId)) return;

            string message = _module.ConvertToReminder(_editingId);
            if (message == null)
            {
                ShowError("Could not create a reminder from this note.");
                return;
            }

            StatusText.Text = message;
            HideError();
        }

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncing) return;
            _module.SetSearch(SearchBox.Text);
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
