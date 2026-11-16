using System;
using System.Collections.Generic;
using System.Windows;

namespace RightDock.Services
{
    internal sealed class NotificationService : IDisposable
    {
        private readonly AppSettings _settings;
        private readonly List<ToastWindow> _active = new List<ToastWindow>();
        private bool _disposed;

        internal NotificationService(AppSettings settings)
        {
            _settings = settings;
        }

        internal bool Show(string title, string message, Action onClick = null)
        {
            if (_disposed || _settings == null || !_settings.NotificationsEnabled) return false;

            Application app = Application.Current;
            if (app == null) return false;

            if (!app.Dispatcher.CheckAccess())
            {
                app.Dispatcher.BeginInvoke(new Action(() => Show(title, message, onClick)));
                return true;
            }

            try
            {
                if (_active.Count >= 4) _active[0].BeginClose();

                var toast = new ToastWindow(title, message, onClick);
                toast.Closed += (_, __) =>
                {
                    _active.Remove(toast);
                    Reposition();
                };
                _active.Add(toast);

                toast.Loaded += (_, __) => Reposition();
                toast.ShowToast();
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Could not show a notification.", ex);
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (ToastWindow toast in _active.ToArray())
            {
                try
                {
                    toast.Close();
                }
                catch
                {
                    // Shutdown cleanup is best-effort.
                }
            }

            _active.Clear();
        }

        private void Reposition()
        {
            Rect work = SystemParameters.WorkArea;
            double right = work.Right - 10;
            double bottom = work.Bottom - 10;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                ToastWindow toast = _active[i];
                if (!toast.IsLoaded) continue;

                double height = toast.ActualHeight;
                toast.Left = right - toast.ActualWidth;
                toast.Top = bottom - height;
                bottom -= height + 8;
            }
        }
    }
}
