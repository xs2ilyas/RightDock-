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
                    "SELECT ModuleId, IsEnabled, IsVisible, SortOrder, ConfigJson FROM ModuleSettings;");

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
                    "INSERT INTO ModuleSettings (ModuleId, IsEnabled, IsVisible, SortOrder, ConfigJson, UpdatedUtc) " +
                    "VALUES ($id, $enabled, $visible, $sort, $config, $updated) " +
                    "ON CONFLICT(ModuleId) DO UPDATE SET " +
                    "IsEnabled = excluded.IsEnabled, " +
                    "IsVisible = excluded.IsVisible, " +
                    "SortOrder = excluded.SortOrder, " +
                    "ConfigJson = excluded.ConfigJson, " +
                    "UpdatedUtc = excluded.UpdatedUtc;",
                    Database.Param("$id", state.ModuleId),
                    Database.Param("$enabled", state.IsEnabled ? 1 : 0),
                    Database.Param("$visible", state.IsVisible ? 1 : 0),
                    Database.Param("$sort", state.SortOrder),
                    Database.Param("$config", state.ConfigJson ?? "{}"),
                    Database.Param("$updated", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
            }
            catch (Exception ex)
            {
                Log.Error("Could not save module settings for " + state.ModuleId + ".", ex);
            }
        }
    }
}
