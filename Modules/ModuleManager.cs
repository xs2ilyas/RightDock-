using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using RightDock.Data;
using RightDock.Services;

namespace RightDock.Modules
{
    internal sealed class ModuleEntry
    {
        internal ModuleEntry(IProductivityModule module, ModuleState state)
        {
            Module = module;
            State = state;
        }

        internal IProductivityModule Module { get; }

        internal ModuleState State { get; }

        internal bool Initialized { get; set; }

        internal bool Active { get; set; }
    }

    internal sealed class ModuleManager : IDisposable
    {
        private readonly List<ModuleEntry> _entries = new List<ModuleEntry>();
        private readonly Dictionary<string, ModuleState> _savedStates;
        private readonly ModuleStateStore _store;
        private readonly ModuleContext _context;
        private readonly PopupHost _popups;

        private bool _started;
        private bool _disposed;

        internal ModuleManager(Database database, Scheduler scheduler, NotificationService notifications,
            AppSettings settings)
        {
            _store = new ModuleStateStore(database);
            _savedStates = _store.LoadAll();
            _context = new ModuleContext(settings, database, scheduler, notifications);
            _popups = new PopupHost();
            _popups.OpenCountChanged += count => PopupCountChanged?.Invoke(count);
        }

        internal event Action DockItemsChanged;

        internal event Action<int> PopupCountChanged;

        internal IReadOnlyList<ModuleEntry> Entries => _entries;

        internal void Register(IProductivityModule module)
        {
            if (_disposed || module == null || string.IsNullOrWhiteSpace(module.Id)) return;

            bool duplicate = _entries.Any(entry =>
                string.Equals(entry.Module.Id, module.Id, StringComparison.OrdinalIgnoreCase));
            if (duplicate) return;

            if (!_savedStates.TryGetValue(module.Id, out ModuleState state))
            {
                state = new ModuleState
                {
                    ModuleId = module.Id,
                    IsEnabled = true,
                    IsVisible = module.ShowInDockByDefault,
                    SortOrder = _entries.Count,
                };
            }

            _entries.Add(new ModuleEntry(module, state));
            Log.Info("Registered module: " + module.Id);
        }

        internal void Start()
        {
            if (_started || _disposed) return;
            _started = true;

            foreach (ModuleEntry entry in OrderedEntries())
            {
                if (entry.State.IsEnabled) EnsureInitialized(entry);
            }

            DockItemsChanged?.Invoke();
        }

        internal List<ModuleEntry> GetDockEntries()
        {
            return _entries
                .Where(entry => entry.State.IsEnabled && entry.State.IsVisible)
                .OrderBy(entry => entry.State.SortOrder)
                .ToList();
        }

        internal List<ModuleEntry> OrderedEntries()
        {
            return _entries.OrderBy(entry => entry.State.SortOrder).ToList();
        }

        internal ModuleEntry Find(string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId)) return null;

            return _entries.FirstOrDefault(entry =>
                string.Equals(entry.Module.Id, moduleId, StringComparison.OrdinalIgnoreCase));
        }

        internal void SetEnabled(string moduleId, bool enabled)
        {
            ModuleEntry entry = Find(moduleId);
            if (entry == null || entry.State.IsEnabled == enabled) return;

            entry.State.IsEnabled = enabled;
            _store.Save(entry.State);

            if (enabled)
            {
                EnsureInitialized(entry);
            }
            else
            {
                _popups.Close(moduleId);
                Deactivate(entry);
            }

            DockItemsChanged?.Invoke();
        }

        internal void SetVisible(string moduleId, bool visible)
        {
            ModuleEntry entry = Find(moduleId);
            if (entry == null || entry.State.IsVisible == visible) return;

            entry.State.IsVisible = visible;
            _store.Save(entry.State);
            DockItemsChanged?.Invoke();
        }

        internal void ToggleCompact(ModuleEntry entry, FrameworkElement anchor)
        {
            if (entry == null || !entry.State.IsEnabled) return;

            if (_popups.IsOpen(entry.Module.Id))
            {
                _popups.Close(entry.Module.Id);
                return;
            }

            EnsureInitialized(entry);

            try
            {
                FrameworkElement view = entry.Module.CreateCompactView();
                if (view == null) return;
                _popups.ShowCompact(entry.Module, view, anchor);
            }
            catch (Exception ex)
            {
                Log.Error("Could not open the compact view for " + entry.Module.Id + ".", ex);
            }
        }

        internal void OpenExpanded(ModuleEntry entry)
        {
            if (entry == null || !entry.State.IsEnabled) return;

            EnsureInitialized(entry);

            try
            {
                FrameworkElement view = entry.Module.CreateExpandedView();
                if (view == null) return;
                _popups.ShowExpanded(entry.Module, view);
            }
            catch (Exception ex)
            {
                Log.Error("Could not open the expanded view for " + entry.Module.Id + ".", ex);
            }
        }

        internal void ClosePopups()
        {
            _popups.CloseAll();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _popups.CloseAll();

            foreach (ModuleEntry entry in _entries)
            {
                Deactivate(entry);
            }

            _popups.Dispose();
        }

        private void EnsureInitialized(ModuleEntry entry)
        {
            if (!entry.Initialized)
            {
                try
                {
                    entry.Module.Initialize(_context);
                    entry.Initialized = true;
                }
                catch (Exception ex)
                {
                    Log.Error("Module initialization failed: " + entry.Module.Id, ex);
                    return;
                }
            }

            if (!entry.Active)
            {
                try
                {
                    entry.Module.Activate();
                    entry.Active = true;
                }
                catch (Exception ex)
                {
                    Log.Error("Module activation failed: " + entry.Module.Id, ex);
                }
            }
        }

        private void Deactivate(ModuleEntry entry)
        {
            if (!entry.Active) return;

            try
            {
                entry.Module.Deactivate();
            }
            catch (Exception ex)
            {
                Log.Error("Module deactivation failed: " + entry.Module.Id, ex);
            }

            entry.Active = false;
        }
    }
}
