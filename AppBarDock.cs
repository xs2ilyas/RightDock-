using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RightDock
{
    internal sealed class AppBarDock
    {
        private const int WidthInDips = 50;

        private readonly IntPtr _hwnd;
        private readonly int _callbackMessage;
        private bool _registered;

        public AppBarDock(IntPtr hwnd, int callbackMessage)
        {
            _hwnd = hwnd;
            _callbackMessage = callbackMessage;
        }

        public bool IsRegistered => _registered;

        public NativeMethods.RECT ReservedRect { get; private set; }

        public NativeMethods.RECT MonitorRect { get; private set; }

        public long LastReserveTicks { get; private set; }

        public bool Reserve(double dpiScale)
        {
            var data = CreateData();
            if (!_registered)
            {
                NativeMethods.SHAppBarMessage(NativeMethods.ABM_NEW, ref data);
                _registered = true;
            }

            IntPtr hMonitor = NativeMethods.MonitorFromPoint(
                new NativeMethods.POINT { X = 0, Y = 0 }, NativeMethods.MONITOR_DEFAULTTONEAREST);

            var monitor = new NativeMethods.MONITORINFO
            {
                cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>(),
            };
            if (!NativeMethods.GetMonitorInfo(hMonitor, ref monitor)) return false;

            var proposed = CreateData();
            proposed.rc = monitor.rcMonitor;
            NativeMethods.SHAppBarMessage(NativeMethods.ABM_QUERYPOS, ref proposed);

            int width = (int)Math.Round(WidthInDips * dpiScale);
            proposed.rc.Left = proposed.rc.Right - width;
            NativeMethods.SHAppBarMessage(NativeMethods.ABM_SETPOS, ref proposed);

            MonitorRect = monitor.rcMonitor;
            ReservedRect = proposed.rc;
            LastReserveTicks = Stopwatch.GetTimestamp();
            return true;
        }

        public void Unregister()
        {
            if (!_registered) return;

            var data = CreateData();
            NativeMethods.SHAppBarMessage(NativeMethods.ABM_REMOVE, ref data);
            _registered = false;
        }

        private NativeMethods.APPBARDATA CreateData()
        {
            return new NativeMethods.APPBARDATA
            {
                cbSize = Marshal.SizeOf<NativeMethods.APPBARDATA>(),
                hWnd = _hwnd,
                uCallbackMessage = _callbackMessage,
                uEdge = NativeMethods.ABE_RIGHT,
            };
        }
    }
}
