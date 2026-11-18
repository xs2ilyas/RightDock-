using System;
using System.Collections.Generic;
using System.Globalization;
using RightDock.Services;

namespace RightDock.Data
{
    internal sealed class ModuleState
    {
        internal string ModuleId { get; set; }

        internal bool IsEnabled { get; set; } = true;

        internal bool IsVisible { get; set; } = true;

        internal int SortOrder { get; set; }

        internal string ConfigJson { get; set; } = "{}";

        internal string IconKey { get; set; }

        internal string IconColor { get; set; }

        internal int IconSize { get; set; }
    }

    internal sealed class ModuleStateStore
    {
        private readonly Database _database;

        internal ModuleStateStore(Database database)
        {
            _database = database;
        }

        internal Dictionary<string, ModuleState> LoadAll()
        {
            var states = new Dictionary<string, ModuleState>(StringComparer.OrdinalIgnoreCase);

            if (_database == null || !_database.IsAvailable) return states;

            try
            {
                List<Dictionary<string, object>> rows = _database.Query(
                    "SELECT ModuleId, IsEnabled, IsVisible, SortOrder, ConfigJson, IconKey, IconColor, IconSize FROM ModuleSettings;");

                foreach (Dictionary<string, object> row in rows)
                {
                    string id = row["ModuleId"] as string;
                    if (string.IsNullOrWhiteSpace(id)) continue;

                    states[id] = new ModuleState
                    {
                        ModuleId = id,
                        IsEnabled = Convert.ToInt64(row["IsEnabled"], CultureInfo.InvariantCulture) != 0,
                        IsVisible = Convert.ToInt64(row["IsVisible"], CultureInfo.InvariantCulture) != 0,
                        SortOrder = Convert.ToInt32(row["SortOrder"], CultureInfo.InvariantCulture),
                        ConfigJson = row["ConfigJson"] as string ?? "{}",
                        IconKey = row["IconKey"] as string,
                        IconColor = row["IconColor"] as string,
                        IconSize = Convert.ToInt32(row["IconSize"], CultureInfo.InvariantCulture),
                    };
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not load module settings.", ex);
            }

            return states;
        }

        internal void Save(ModuleState state)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.ModuleId)) return;
            if (_database == null || !_database.IsAvailable) return;

            try
            {
                _database.Execute(
                    "INSERT INTO ModuleSettings (ModuleId, IsEnabled, IsVisible, SortOrder, ConfigJson, IconKey, IconColor, IconSize, UpdatedUtc) " +
                    "VALUES ($id, $enabled, $visible, $sort, $config, $icon, $color, $size, $updated) " +
                    "ON CONFLICT(ModuleId) DO UPDATE SET " +
                    "IsEnabled = excluded.IsEnabled, " +
                    "IsVisible = excluded.IsVisible, " +
                    "SortOrder = excluded.SortOrder, " +
                    "ConfigJson = excluded.ConfigJson, " +
                    "IconKey = excluded.IconKey, " +
                    "IconColor = excluded.IconColor, " +
                    "IconSize = excluded.IconSize, " +
                    "UpdatedUtc = excluded.UpdatedUtc;",
                    Database.Param("$id", state.ModuleId),
                    Database.Param("$enabled", state.IsEnabled ? 1 : 0),
                    Database.Param("$visible", state.IsVisible ? 1 : 0),
                    Database.Param("$sort", state.SortOrder),
                    Database.Param("$config", state.ConfigJson ?? "{}"),
                    Database.Param("$icon", state.IconKey),
                    Database.Param("$color", state.IconColor),
                    Database.Param("$size", state.IconSize),
                    Database.Param("$updated", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
            }
            catch (Exception ex)
            {
                Log.Error("Could not save module settings for " + state.ModuleId + ".", ex);
            }
        }
    }
}
