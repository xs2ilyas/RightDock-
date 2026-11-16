using System;
using System.Collections.Generic;
using System.Globalization;
using RightDock.Modules;

namespace RightDock.Data
{
    internal sealed class NoteItem
    {
        internal string Id { get; set; }

        internal string Title { get; set; }

        internal string Body { get; set; } = string.Empty;

        internal string Category { get; set; }

        internal bool IsPinned { get; set; }

        internal DateTime CreatedUtc { get; set; }

        internal DateTime UpdatedUtc { get; set; }

        internal string DisplayTitle =>
            string.IsNullOrWhiteSpace(Title)
                ? (string.IsNullOrWhiteSpace(Body) ? "Untitled note" : FirstLine())
                : Title;

        private string FirstLine()
        {
            string text = Body.Trim();
            int newline = text.IndexOf('\n');
            return newline < 0 ? text : text.Substring(0, newline).Trim();
        }
    }

    internal sealed class NoteStore
    {
        private readonly Database _database;

        internal NoteStore(Database database)
        {
            _database = database;
        }

        internal List<NoteItem> Query(string search)
        {
            var items = new List<NoteItem>();
            if (_database == null || !_database.IsAvailable) return items;

            string filter = " WHERE IsArchived = 0";
            var parameters = new List<Microsoft.Data.Sqlite.SqliteParameter>();

            if (!string.IsNullOrWhiteSpace(search))
            {
                filter += @" AND (IFNULL(Title, '') LIKE @search ESCAPE '\' OR Body LIKE @search ESCAPE '\'
                    OR IFNULL(Category, '') LIKE @search ESCAPE '\')";
                parameters.Add(Database.Param("@search", "%" + ModuleUtil.EscapeLike(search.Trim()) + "%"));
            }

            try
            {
                foreach (Dictionary<string, object> row in _database.Query(
                    @"SELECT NoteId, Title, Body, Category, IsPinned, CreatedUtc, UpdatedUtc
                      FROM Notes" + filter + " ORDER BY IsPinned DESC, UpdatedUtc DESC LIMIT 300",
                    parameters.ToArray()))
                {
                    items.Add(Read(row));
                }
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not query notes.", ex);
            }

            return items;
        }

        internal NoteItem Get(string noteId)
        {
            if (string.IsNullOrWhiteSpace(noteId) || _database == null || !_database.IsAvailable) return null;

            try
            {
                List<Dictionary<string, object>> rows = _database.Query(
                    @"SELECT NoteId, Title, Body, Category, IsPinned, CreatedUtc, UpdatedUtc
                      FROM Notes WHERE NoteId = @id",
                    Database.Param("@id", noteId));
                return rows.Count == 0 ? null : Read(rows[0]);
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not load a note.", ex);
                return null;
            }
        }

        internal string Add(NoteItem note)
        {
            if (note == null || _database == null || !_database.IsAvailable) return null;

            string id = ModuleUtil.NewId();
            DateTime now = DateTime.UtcNow;

            try
            {
                _database.Execute(
                    @"INSERT INTO Notes (NoteId, Title, Body, Category, IsPinned, IsArchived, CreatedUtc, UpdatedUtc)
                      VALUES (@id, @title, @body, @category, @pinned, 0, @now, @now);",
                    Database.Param("@id", id),
                    Database.Param("@title", string.IsNullOrWhiteSpace(note.Title) ? null : note.Title.Trim()),
                    Database.Param("@body", note.Body ?? string.Empty),
                    Database.Param("@category", string.IsNullOrWhiteSpace(note.Category) ? null : note.Category.Trim()),
                    Database.Param("@pinned", note.IsPinned ? 1 : 0),
                    Database.Param("@now", ModuleUtil.Iso(now)));

                return id;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not add a note.", ex);
                return null;
            }
        }

        internal bool Update(NoteItem note)
        {
            if (note == null || string.IsNullOrWhiteSpace(note.Id)
                || _database == null || !_database.IsAvailable)
            {
                return false;
            }

            try
            {
                return _database.Execute(
                    @"UPDATE Notes SET Title = @title, Body = @body, Category = @category,
                        IsPinned = @pinned, UpdatedUtc = @now
                      WHERE NoteId = @id;",
                    Database.Param("@title", string.IsNullOrWhiteSpace(note.Title) ? null : note.Title.Trim()),
                    Database.Param("@body", note.Body ?? string.Empty),
                    Database.Param("@category", string.IsNullOrWhiteSpace(note.Category) ? null : note.Category.Trim()),
                    Database.Param("@pinned", note.IsPinned ? 1 : 0),
                    Database.Param("@now", ModuleUtil.Iso(DateTime.UtcNow)),
                    Database.Param("@id", note.Id)) > 0;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not update a note.", ex);
                return false;
            }
        }

        internal bool SetPinned(string noteId, bool pinned)
        {
            if (string.IsNullOrWhiteSpace(noteId) || _database == null || !_database.IsAvailable) return false;

            try
            {
                return _database.Execute(
                    "UPDATE Notes SET IsPinned = @pinned, UpdatedUtc = @now WHERE NoteId = @id;",
                    Database.Param("@pinned", pinned ? 1 : 0),
                    Database.Param("@now", ModuleUtil.Iso(DateTime.UtcNow)),
                    Database.Param("@id", noteId)) > 0;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not pin a note.", ex);
                return false;
            }
        }

        internal bool Delete(string noteId)
        {
            if (string.IsNullOrWhiteSpace(noteId) || _database == null || !_database.IsAvailable) return false;

            try
            {
                return _database.Execute(
                    "DELETE FROM Notes WHERE NoteId = @id;",
                    Database.Param("@id", noteId)) > 0;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not delete a note.", ex);
                return false;
            }
        }

        private static NoteItem Read(Dictionary<string, object> row)
        {
            return new NoteItem
            {
                Id = row["NoteId"] as string,
                Title = row["Title"] as string,
                Body = row["Body"] as string ?? string.Empty,
                Category = row["Category"] as string,
                IsPinned = Convert.ToInt64(row["IsPinned"], CultureInfo.InvariantCulture) != 0,
                CreatedUtc = ModuleUtil.FromIso(row["CreatedUtc"]) ?? DateTime.UtcNow,
                UpdatedUtc = ModuleUtil.FromIso(row["UpdatedUtc"]) ?? DateTime.UtcNow,
            };
        }
    }
}
