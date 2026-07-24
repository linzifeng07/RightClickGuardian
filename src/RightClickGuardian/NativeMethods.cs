using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace RightClickGuardian
{
    public static class NativeMethods
    {
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;
        private const uint SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNF_IDLIST = 0x0000;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(
            uint eventId, uint flags, IntPtr item1, IntPtr item2);

        public static void ApplyRoundedCorners(Window window)
        {
            try
            {
                WindowInteropHelper helper = new WindowInteropHelper(window);
                int preference = DWMWCP_ROUND;
                DwmSetWindowAttribute(helper.Handle, DWMWA_WINDOW_CORNER_PREFERENCE,
                    ref preference, Marshal.SizeOf(typeof(int)));
            }
            catch { }
        }

        public static void NotifyShellChanged()
        {
            try { SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero); }
            catch { }
        }
    }
}
