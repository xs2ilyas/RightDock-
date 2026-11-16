using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using RightDock.Services;

namespace RightDock.Modules
{
    internal sealed class PopupHost : IDisposable
    {
        private static readonly Brush HeaderIconBrush = CreateFrozenBrush(Color.FromRgb(0xD0, 0xD0, 0xD0));

        private readonly Dictionary<string, ModulePopupWindow> _compact =
            new Dictionary<string, ModulePopupWindow>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, ModulePopupWindow> _expanded =
            new Dictionary<string, ModulePopupWindow>(StringComparer.OrdinalIgnoreCase);

        private bool _disposed;

        internal event Action<int> OpenCountChanged;

        internal int OpenCount => _compact.Count + _expanded.Count;

        internal bool IsOpen(string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId)) return false;
            return _compact.ContainsKey(moduleId) || _expanded.ContainsKey(moduleId);
        }

        internal void ShowCompact(IProductivityModule module, FrameworkElement view, FrameworkElement anchor)
        {
            if (_disposed || module == null || view == null) return;

            Close(module.Id);

            ModulePopupWindow window = CreateWindow(module, view, expanded: false);
            window.SetAnchor(anchor);
            window.Closed += (_, __) => Remove(_compact, module.Id, window);

            _compact[module.Id] = window;
            RaiseChanged();

            ShowWindow(window);
            window.Activate();

            if (anchor != null)
            {
                window.PlaceBeside(anchor);
            }
        }

        internal void ShowExpanded(IProductivityModule module, FrameworkElement view)
        {
            if (_disposed || module == null || view == null) return;

            Close(module.Id);

            ModulePopupWindow window = CreateWindow(module, view, expanded: true);
            window.Closed += (_, __) => Remove(_expanded, module.Id, window);

            _expanded[module.Id] = window;
            RaiseChanged();

            ShowWindow(window);
            window.Activate();
            window.CenterOnWorkArea();
        }

        internal void Close(string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId)) return;

            if (_compact.TryGetValue(moduleId, out ModulePopupWindow compact))
            {
                compact.Close();
            }

            if (_expanded.TryGetValue(moduleId, out ModulePopupWindow expanded))
            {
                expanded.Close();
            }
        }

        internal void CloseAll()
        {
            if (_disposed) return;

            foreach (string moduleId in _compact.Keys.Concat(_expanded.Keys).Distinct().ToList())
            {
                Close(moduleId);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            CloseAll();
            _disposed = true;
            _compact.Clear();
            _expanded.Clear();
        }

        private ModulePopupWindow CreateWindow(IProductivityModule module, FrameworkElement view, bool expanded)
        {
            var window = new ModulePopupWindow(module.Name, module.IconKey, HeaderIconBrush, expanded)
            {
                Owner = Application.Current?.MainWindow,
            };

            window.SetContent(view);
            return window;
        }

        private static void ShowWindow(Window window)
        {
            try
            {
                window.Show();
            }
            catch (Exception ex)
            {
                Log.Error("Could not show a module popup.", ex);
                window.Close();
            }
        }

        private void Remove(Dictionary<string, ModulePopupWindow> host, string moduleId, ModulePopupWindow window)
        {
            if (host.TryGetValue(moduleId, out ModulePopupWindow current) && ReferenceEquals(current, window))
            {
                host.Remove(moduleId);
                Log.Info("Popup closed: " + moduleId);
                RaiseChanged();
            }
        }

        private void RaiseChanged()
        {
            OpenCountChanged?.Invoke(OpenCount);
        }

        private static Brush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
