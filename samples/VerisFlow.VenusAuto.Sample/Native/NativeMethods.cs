using System;
using System.Runtime.InteropServices;

namespace VerisFlow.VenusAuto.Sample.Native
{
    /// <summary>
    /// Win32 functions for window probing, raw input tests, and hotkey registration.
    /// </summary>
    internal static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(POINT Point);

        /// <summary>
        /// Child window of <paramref name="hwndParent"/> at a point in its client coordinates; unlike WindowFromPoint,
        /// also returns disabled windows.
        /// </summary>
        [DllImport("user32.dll")]
        public static extern IntPtr ChildWindowFromPointEx(IntPtr hwndParent, POINT pt, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, [Out] char[] lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetWindowText(IntPtr hWnd, [Out] char[] lpString, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        public static extern int GetDlgCtrlID(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public const uint GA_ROOT = 2;
        public const uint GW_OWNER = 4;
        public const int GWL_STYLE = -16;

        /// <summary>ChildWindowFromPointEx: skip invisible and transparent windows (disabled ones are kept).</summary>
        public const uint CWP_SKIPINVISIBLE = 0x0001;
        public const uint CWP_SKIPTRANSPARENT = 0x0004;

        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_NONE = 0x0000;
        public const uint VK_F2 = 0x71;

        public const uint WM_KEYDOWN = 0x0100;
        public const uint WM_KEYUP = 0x0101;
        public const int VK_F5 = 0x76;

        public const uint WM_LBUTTONDOWN = 0x0201;
        public const uint WM_LBUTTONUP = 0x0202;
        public const int MK_LBUTTON = 0x0001;
    }
}