using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RightDock
{
    internal static class Icons
    {
        public static ImageSource Load(string path)
        {
            IntPtr hIcon = Extract(path);
            if (hIcon == IntPtr.Zero) return null;

            try
            {
                BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(
                    hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally
            {
                NativeMethods.DestroyIcon(hIcon);
            }
        }

        private static IntPtr Extract(string path)
        {
            uint structSize = (uint)Marshal.SizeOf<NativeMethods.SHFILEINFO>();

            var info = new NativeMethods.SHFILEINFO();
            NativeMethods.SHGetFileInfo(path, 0, ref info, structSize,
                NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON);
            if (info.hIcon != IntPtr.Zero) return info.hIcon;

            if (File.Exists(path))
            {
                info = new NativeMethods.SHFILEINFO();
                NativeMethods.SHGetFileInfo(".exe", 0, ref info, structSize,
                    NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON | NativeMethods.SHGFI_USEFILEATTRIBUTES);
            }

            return info.hIcon;
        }
    }
}
