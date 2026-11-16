using System;
using System.Collections.Generic;
using System.Globalization;
using RightDock.Modules;

namespace RightDock.Data
{
    internal sealed class TaskItem
    {
        internal string Id { get; set; }

        internal string Title { get; set; }

        internal string Notes { get; set; }

        internal int Priority { get; set; } = 2;

        internal DateTime? DueUtc { get; set; }

        internal string CategoryId { get; set; }

        internal string CategoryName { get; set; }

        internal string ProjectId { get; set; }

        internal string ProjectName { get; set; }

        internal bool IsCompleted { get; set; }

        internal DateTime? CompletedUtc { get; set; }

        internal string Recurrence { get; set; } = "none";

        internal DateTime CreatedUtc { get; set; }

        internal bool IsOverdue => !IsCompleted && DueUtc.HasValue && DueUtc.Value < DateTime.UtcNow;
    }

    internal sealed class TaskStore
    {
        private const string BaseSelect = @"
SELECT t.TaskId, t.Title, t.Description, t.Priority, t.DueUtc, t.CategoryId,
       c.Name AS CategoryName, t.ProjectId, p.Name AS ProjectName,
       t.IsCompleted, t.CompletedUtc, t.RecurrenceRule, t.CreatedUtc
FROM Tasks t
LEFT JOIN TaskCategories c ON c.CategoryId = t.CategoryId
LEFT JOIN Projects p ON p.ProjectId = t.ProjectId
WHERE t.ArchivedUtc IS NULL";

        private readonly Database _database;

        internal TaskStore(Database database)
        {
            _database = database;
        }

        internal List<TaskItem> Query(string view, string search)
        {
            var items = new List<TaskItem>();
            if (_database == null || !_database.IsAvailable) return items;

            string filter;
            string order;
            switch (view)
            {
                case "upcoming":
                    filter = " AND t.IsCompleted = 0 AND t.DueUtc IS NOT NULL AND t.DueUtc >= @tomorrow";
                    order = " ORDER BY t.DueUtc, t.Priority DESC";
                    break;
                case "completed":
                    filter = " AND t.IsCompleted = 1";
                    order = " ORDER BY t.CompletedUtc DESC LIMIT 200";
                    break;
                case "all":
                    filter = string.Empty;
                    order = " ORDER BY t.IsCompleted, CASE WHEN t.DueUtc IS NULL THEN 1 ELSE 0 END, t.DueUtc, t.Priority DESC";
                    break;
                default:
                    filter = " AND t.IsCompleted = 0 AND (t.DueUtc IS NULL OR t.DueUtc < @tomorrow)";
                    order = " ORDER BY CASE WHEN t.DueUtc IS NULL THEN 1 ELSE 0 END, t.DueUtc, t.Priority DESC, t.CreatedUtc DESC";
                    break;
            }

            var parameters = new List<Microsoft.Data.Sqlite.SqliteParameter>
            {
                Database.Param("@tomorrow", ModuleUtil.Iso(DateTime.Today.AddDays(1))),
            };

            string searchFilter = string.Empty;
            if (!string.IsNullOrWhiteSpace(search))
            {
                searchFilter = @" AND (t.Title LIKE @search ESCAPE '\' OR IFNULL(t.Description, '') LIKE @search ESCAPE '\'
                    OR IFNULL(c.Name, '') LIKE @search ESCAPE '\' OR IFNULL(p.Name, '') LIKE @search ESCAPE '\')";
                parameters.Add(Database.Param("@search", "%" + ModuleUtil.EscapeLike(search.Trim()) + "%"));
            }

            try
            {
                foreach (Dictionary<string, object> row in _database.Query(
                    BaseSelect + filter + searchFilter + order, parameters.ToArray()))
                {
                    items.Add(Read(row));
                }
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not query tasks.", ex);
            }

            return items;
        }

        internal TaskItem Get(string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId) || _database == null || !_database.IsAvailable) return null;

            try
            {
                List<Dictionary<string, object>> rows = _database.Query(
                    BaseSelect + " AND t.TaskId = @id", Database.Param("@id", taskId));
                return rows.Count == 0 ? null : Read(rows[0]);
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not load a task.", ex);
                return null;
            }
        }

        internal List<TaskItem> ActiveForFocus(int limit)
        {
            var items = new List<TaskItem>();
            if (_database == null || !_database.IsAvailable) return items;

            try
            {
                foreach (Dictionary<string, object> row in _database.Query(
                    BaseSelect + " AND t.IsCompleted = 0 ORDER BY CASE WHEN t.DueUtc IS NULL THEN 1 ELSE 0 END, t.DueUtc, t.Priority DESC LIMIT @limit",
                    Database.Param("@limit", Math.Max(1, limit))))
                {
                    items.Add(Read(row));
                }
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not load focus tasks.", ex);
            }

            return items;
        }

        internal string Add(TaskItem task)
        {
            if (task == null || _database == null || !_database.IsAvailable) return null;

            string id = ModuleUtil.NewId();
            DateTime now = DateTime.UtcNow;

            try
            {
                _database.Execute(
                    @"INSERT INTO Tasks (TaskId, Title, Description, Priority, DueUtc, CategoryId, ProjectId,
                        IsCompleted, RecurrenceRule, CreatedUtc, UpdatedUtc)
                      VALUES (@id, @title, @notes, @priority, @due, @category, @project,
                        0, @recurrence, @created, @created);",
                    Database.Param("@id", id),
                    Database.Param("@title", task.Title.Trim()),
                    Database.Param("@notes", EmptyToNull(task.Notes)),
                    Database.Param("@priority", task.Priority),
                    Database.Param("@due", task.DueUtc.HasValue ? (object)ModuleUtil.Iso(task.DueUtc.Value) : null),
                    Database.Param("@category", ResolveCategory(task.CategoryName)),
                    Database.Param("@project", ResolveProject(task.ProjectName)),
                    Database.Param("@recurrence", string.IsNullOrWhiteSpace(task.Recurrence) ? "none" : task.Recurrence),
                    Database.Param("@created", ModuleUtil.Iso(now)));

                return id;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not add a task.", ex);
                return null;
            }
        }

        internal bool Update(TaskItem task)
        {
            if (task == null || string.IsNullOrWhiteSpace(task.Id) || _database == null || !_database.IsAvailable)
            {
                return false;
            }

            try
            {
                return _database.Execute(
                    @"UPDATE Tasks SET Title = @title, Description = @notes, Priority = @priority,
                        DueUtc = @due, CategoryId = @category, ProjectId = @project,
                        RecurrenceRule = @recurrence, UpdatedUtc = @now
                      WHERE TaskId = @id;",
                    Database.Param("@title", task.Title.Trim()),
                    Database.Param("@notes", EmptyToNull(task.Notes)),
                    Database.Param("@priority", task.Priority),
                    Database.Param("@due", task.DueUtc.HasValue ? (object)ModuleUtil.Iso(task.DueUtc.Value) : null),
                    Database.Param("@category", ResolveCategory(task.CategoryName)),
                    Database.Param("@project", ResolveProject(task.ProjectName)),
                    Database.Param("@recurrence", string.IsNullOrWhiteSpace(task.Recurrence) ? "none" : task.Recurrence),
                    Database.Param("@now", ModuleUtil.Iso(DateTime.UtcNow)),
                    Database.Param("@id", task.Id)) > 0;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not update a task.", ex);
                return false;
            }
        }

        internal TaskItem SetCompleted(string taskId, bool completed)
        {
            TaskItem task = Get(taskId);
            if (task == null || _database == null || !_database.IsAvailable) return null;

            try
            {
                _database.Execute(
                    "UPDATE Tasks SET IsCompleted = @done, CompletedUtc = @when, UpdatedUtc = @now WHERE TaskId = @id;",
                    Database.Param("@done", completed ? 1 : 0),
                    Database.Param("@when", completed ? (object)ModuleUtil.Iso(DateTime.UtcNow) : null),
                    Database.Param("@now", ModuleUtil.Iso(DateTime.UtcNow)),
                    Database.Param("@id", taskId));

                if (!completed) return null;

                string rule = task.Recurrence;
                DateTime baseDue = task.DueUtc ?? DateTime.UtcNow;
                DateTime? next = ModuleUtil.NextOccurrence(rule, baseDue);
                if (!next.HasValue) return null;

                var repeat = new TaskItem
                {
                    Title = task.Title,
                    Notes = task.Notes,
                    Priority = task.Priority,
                    DueUtc = next,
                    CategoryName = task.CategoryName,
                    ProjectName = task.ProjectName,
                    Recurrence = rule,
                };

                string newId = Add(repeat);
                return newId == null ? null : Get(newId);
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not complete a task.", ex);
                return null;
            }
        }

        internal bool Delete(string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId) || _database == null || !_database.IsAvailable) return false;

            try
            {
                return _database.Execute("DELETE FROM Tasks WHERE TaskId = @id;", Database.Param("@id", taskId)) > 0;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not delete a task.", ex);
                return false;
            }
        }

        internal string ResolveCategory(string name)
        {
            return ResolveNamed("TaskCategories", "CategoryId", name);
        }

        internal string ResolveProject(string name)
        {
            return ResolveNamed("Projects", "ProjectId", name);
        }

        private string ResolveNamed(string table, string idColumn, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (_database == null || !_database.IsAvailable) return null;

            name = name.Trim();

            try
            {
                object existing = _database.Scalar(
                    "SELECT " + idColumn + " FROM " + table + " WHERE Name = @name COLLATE NOCASE LIMIT 1;",
                    Database.Param("@name", name));
                if (existing != null && existing != DBNull.Value) return Convert.ToString(existing, CultureInfo.InvariantCulture);

                string id = ModuleUtil.NewId();
                string now = ModuleUtil.Iso(DateTime.UtcNow);

                if (table == "Projects")
                {
                    _database.Execute(
                        "INSERT INTO Projects (ProjectId, Name, CreatedUtc) VALUES (@id, @name, @now);",
                        Database.Param("@id", id), Database.Param("@name", name),
                        Database.Param("@now", now));
                }
                else
                {
                    _database.Execute(
                        "INSERT INTO TaskCategories (CategoryId, Name, SortOrder) VALUES (@id, @name, 0);",
                        Database.Param("@id", id), Database.Param("@name", name));
                }

                return id;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not resolve " + table + ".", ex);
                return null;
            }
        }

        private static object EmptyToNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : (object)value.Trim();
        }

        private static TaskItem Read(Dictionary<string, object> row)
        {
            return new TaskItem
            {
                Id = row["TaskId"] as string,
                Title = row["Title"] as string ?? string.Empty,
                Notes = row["Description"] as string,
                Priority = Convert.ToInt32(row["Priority"], CultureInfo.InvariantCulture),
                DueUtc = ModuleUtil.FromIso(row["DueUtc"]),
                CategoryId = row["CategoryId"] as string,
                CategoryName = row["CategoryName"] as string,
                ProjectId = row["ProjectId"] as string,
                ProjectName = row["ProjectName"] as string,
                IsCompleted = Convert.ToInt64(row["IsCompleted"], CultureInfo.InvariantCulture) != 0,
                CompletedUtc = ModuleUtil.FromIso(row["CompletedUtc"]),
                Recurrence = row["RecurrenceRule"] as string ?? "none",
                CreatedUtc = ModuleUtil.FromIso(row["CreatedUtc"]) ?? DateTime.UtcNow,
            };
        }
    }
}
