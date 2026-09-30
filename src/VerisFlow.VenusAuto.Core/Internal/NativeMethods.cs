// Copyright (c) VerisFlow. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Runtime.InteropServices;

namespace VerisFlow.VenusAuto.Core.Internal;

/// <summary>
/// Provides unmanaged Win32 P/Invoke interop method signatures, data structures, and message constants for window manipulation.
/// </summary>
internal static class NativeMethods
{
    /// <summary>
    /// Defines a point structure representing 2D screen coordinates.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        /// <summary>The x-coordinate of the point.</summary>
        public int X;

        /// <summary>The y-coordinate of the point.</summary>
        public int Y;
    }

    /// <summary>
    /// Defines a rectangle structure for window boundaries and screen work areas.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        /// <summary>The x-coordinate of the upper-left corner of the rectangle.</summary>
        public int Left;

        /// <summary>The y-coordinate of the upper-left corner of the rectangle.</summary>
        public int Top;

        /// <summary>The x-coordinate of the lower-right corner of the rectangle.</summary>
        public int Right;

        /// <summary>The y-coordinate of the lower-right corner of the rectangle.</summary>
        public int Bottom;

        /// <summary>Gets the total width calculated from the right and left boundaries.</summary>
        public int Width => Right - Left;

        /// <summary>Gets the total height calculated from the bottom and top boundaries.</summary>
        public int Height => Bottom - Top;
    }

    /// <summary>
    /// Contains information about a display monitor boundary and usable work area.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        /// <summary>The size of the structure, in bytes.</summary>
        public uint cbSize;

        /// <summary>The display monitor rectangle, specified in screen coordinates.</summary>
        public RECT rcMonitor;

        /// <summary>The work area rectangle of the display monitor, specified in screen coordinates.</summary>
        public RECT rcWork;

        /// <summary>A set of flags that specify attributes of the display monitor.</summary>
        public uint dwFlags;
    }

    /// <summary>
    /// Callback delegate used during window enumeration calls.
    /// </summary>
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumThreadWindows(uint dwThreadId, EnumWindowsProc lpfn, IntPtr lParam);

    /// <summary>Enumerates all descendants (recursively) of the specified parent window.</summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowEnabled(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowUnicode(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetWindowText(IntPtr hWnd, [Out] char[] lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hWnd, [Out] char[] lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern int GetDlgCtrlID(IntPtr hWnd);

    /// <summary>Reads a 32-bit window value such as the style (valid for both 32- and 64-bit processes).</summary>
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT Point);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam);

    /// <summary>Sends a message with integer arguments; gives up after the timeout or when the target hangs.</summary>
    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    /// <summary>WM_GETTEXT into a buffer; the system marshals the text across processes.</summary>
    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr SendMessageTimeoutText(IntPtr hWnd, uint Msg, IntPtr wParam, [Out] char[] lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    /// <summary>WM_SETTEXT with a string; the system marshals the text across processes.</summary>
    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr SendMessageTimeoutString(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    public const int SW_RESTORE = 9;
    public const int SW_MAXIMIZE = 3;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint SPI_GETWORKAREA = 0x0030;
    public const uint MONITOR_DEFAULTTONEAREST = 2;

    public const uint WM_SETTEXT = 0x000C;
    public const uint WM_GETTEXT = 0x000D;
    public const uint WM_GETTEXTLENGTH = 0x000E;
    public const uint WM_COMMAND = 0x0111;
    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_KEYUP = 0x0101;
    public const uint WM_LBUTTONDOWN = 0x0201;
    public const uint WM_LBUTTONUP = 0x0202;

    public const uint BM_GETCHECK = 0x00F0;
    public const uint BM_CLICK = 0x00F5;

    /// <summary>TB_ISBUTTONENABLED (WM_USER + 9): wParam is the command ID.</summary>
    public const uint TB_ISBUTTONENABLED = 0x0409;

    /// <summary>TB_COMMANDTOINDEX (WM_USER + 25): wParam is the command ID; -1 when the toolbar has no such button.</summary>
    public const uint TB_COMMANDTOINDEX = 0x0419;

    /// <summary>SendMessageTimeout: return without waiting when the target thread is hung.</summary>
    public const uint SMTO_ABORTIFHUNG = 0x0002;

    public const int GWL_STYLE = -16;

    /// <summary>Button style type bits (BS_PUSHBUTTON, BS_CHECKBOX, ...).</summary>
    public const int BS_TYPEMASK = 0x000F;

    public const int ES_READONLY = 0x0800;

    public const int VK_CONTROL = 0x11;
    public const int VK_O = 0x4F;
    public const int VK_RETURN = 0x0D;
    public const int MK_LBUTTON = 0x0001;

    public const uint GA_ROOT = 2;
    public const uint GW_OWNER = 4;

    /// <summary>
    /// The standard Win32 window class name assigned to modal dialog boxes.
    /// </summary>
    public const string DialogClassName = "#32770";
}