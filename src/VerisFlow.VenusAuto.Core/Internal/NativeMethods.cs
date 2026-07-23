// Copyright (c) VerisFlow. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Runtime.InteropServices;
using System.Text;

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
    /// <param name="hWnd">A handle to a top-level window.</param>
    /// <param name="lParam">The application-defined value given in window enumeration functions.</param>
    /// <returns><c>true</c> to continue enumeration; <c>false</c> to stop enumeration.</returns>
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    /// <summary>Enumerates top-level windows across the active desktop session.</summary>
    /// <param name="lpEnumFunc">A pointer to an application-defined callback function.</param>
    /// <param name="lParam">An application-defined value to be passed to the callback function.</param>
    /// <returns><c>true</c> if successful; otherwise, <c>false</c>.</returns>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    /// <summary>Enumerates non-child windows created by the specified thread ID.</summary>
    /// <param name="dwThreadId">The identifier of the thread whose windows are to be enumerated.</param>
    /// <param name="lpfn">A pointer to an application-defined callback function.</param>
    /// <param name="lParam">An application-defined value to be passed to the callback function.</param>
    /// <returns><c>true</c> if successful; otherwise, <c>false</c>.</returns>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumThreadWindows(uint dwThreadId, EnumWindowsProc lpfn, IntPtr lParam);

    /// <summary>Enumerates child windows belonging to the specified parent window handle.</summary>
    /// <param name="hWndParent">A handle to the parent window whose child windows are to be enumerated.</param>
    /// <param name="lpEnumFunc">A pointer to an application-defined callback function.</param>
    /// <param name="lParam">An application-defined value to be passed to the callback function.</param>
    /// <returns><c>true</c> if successful; otherwise, <c>false</c>.</returns>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    /// <summary>Retrieves thread and process IDs associated with the specified window handle.</summary>
    /// <param name="hWnd">A handle to the window.</param>
    /// <param name="lpdwProcessId">A pointer to a variable that receives the process identifier.</param>
    /// <returns>The identifier of the thread that created the window.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    /// <summary>Determines whether the specified window is visible.</summary>
    /// <param name="hWnd">A handle to the window to be tested.</param>
    /// <returns><c>true</c> if the specified window, its parent window, and its grandparent window have the WS_VISIBLE style; otherwise, <c>false</c>.</returns>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    /// <summary>Posts an asynchronous window message to the specified window queue.</summary>
    /// <param name="hWnd">A handle to the window whose window procedure is to receive the message.</param>
    /// <param name="Msg">The message to be posted.</param>
    /// <param name="wParam">Additional message-specific information.</param>
    /// <param name="lParam">Additional message-specific information.</param>
    /// <returns><c>true</c> if the function succeeds; otherwise, <c>false</c>.</returns>
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    /// <summary>Copies title bar or control text from the specified window into a character array buffer.</summary>
    /// <param name="hWnd">A handle to the window or control containing the text.</param>
    /// <param name="lpString">The buffer that will receive the text.</param>
    /// <param name="nMaxCount">The maximum number of characters to copy to the buffer, including the null character.</param>
    /// <returns>The length, in characters, of the copied string, not including the terminating null character.</returns>
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetWindowText(IntPtr hWnd, [Out] char[] lpString, int nMaxCount);

    /// <summary>Retrieves the class name of the specified window control.</summary>
    /// <param name="hWnd">A handle to the window and, indirectly, the class to which the window belongs.</param>
    /// <param name="lpString">The buffer that receives the class name string.</param>
    /// <param name="nMaxCount">The length of the buffer, in characters.</param>
    /// <returns>The number of characters copied to the buffer.</returns>
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hWnd, [Out] char[] lpString, int nMaxCount);

    /// <summary>Converts client area coordinates of a point to absolute screen coordinates.</summary>
    /// <param name="hWnd">A handle to the window whose client area is used for transformation.</param>
    /// <param name="lpPoint">A pointer to a <see cref="POINT"/> structure containing client coordinates to convert.</param>
    /// <returns><c>true</c> if conversion succeeds; otherwise, <c>false</c>.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    /// <summary>Converts absolute screen coordinates of a point to client area coordinates.</summary>
    /// <param name="hWnd">A handle to the window whose client area is used for transformation.</param>
    /// <param name="lpPoint">A pointer to a <see cref="POINT"/> structure containing screen coordinates to convert.</param>
    /// <returns><c>true</c> if conversion succeeds; otherwise, <c>false</c>.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    /// <summary>Retrieves the handle to the window that contains the specified point.</summary>
    /// <param name="Point">The point to be checked.</param>
    /// <returns>The handle to the window containing the point, or <see cref="IntPtr.Zero"/> if no window exists at the given point.</returns>
    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT Point);

    /// <summary>Retrieves a window handle related to the target window based on command flags.</summary>
    /// <param name="hWnd">A handle to the window whose relationship is to be retrieved.</param>
    /// <param name="uCmd">The relationship between the specified window and the window to be retrieved.</param>
    /// <returns>The handle to the requested window, or <see cref="IntPtr.Zero"/> if none exists.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    /// <summary>Retrieves the handle to the specified window's ancestor.</summary>
    /// <param name="hwnd">A handle to the window whose ancestor is to be retrieved.</param>
    /// <param name="gaFlags">The ancestor flag specifying which ancestor to retrieve.</param>
    /// <returns>The handle to the ancestor window.</returns>
    [DllImport("user32.dll")]
    public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    /// <summary>Brings the specified window to the foreground and activates it.</summary>
    /// <param name="hWnd">A handle to the window that should be activated and brought to the foreground.</param>
    /// <returns><c>true</c> if the window was brought to the foreground; otherwise, <c>false</c>.</returns>
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>Sets the display state of the specified window (e.g., maximize, restore).</summary>
    /// <param name="hWnd">A handle to the window.</param>
    /// <param name="nCmdShow">Controls how the window is to be shown.</param>
    /// <returns><c>true</c> if the window was previously visible; otherwise, <c>false</c>.</returns>
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    /// <summary>Sends a synchronous message to a window control.</summary>
    /// <param name="hWnd">A handle to the window whose window procedure receives the message.</param>
    /// <param name="Msg">The message to be sent.</param>
    /// <param name="wParam">Additional message-specific information.</param>
    /// <param name="lParam">Additional message-specific information.</param>
    /// <returns>The result of the message processing.</returns>
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam);

    /// <summary>Synthesizes low-level keyboard input events.</summary>
    /// <param name="bVk">A virtual-key code.</param>
    /// <param name="bScan">A hardware scan code for the key.</param>
    /// <param name="dwFlags">Flags specifying function options (e.g., <see cref="KEYEVENTF_KEYUP"/>).</param>
    /// <param name="dwExtraInfo">An additional value associated with the key stroke.</param>
    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    /// <summary>Checks whether the specified window handle is minimized.</summary>
    /// <param name="hWnd">A handle to the window to be tested.</param>
    /// <returns><c>true</c> if the window is minimized; otherwise, <c>false</c>.</returns>
    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    /// <summary>Retrieves the parent or owner window handle of the specified window.</summary>
    /// <param name="hWnd">A handle to the window whose parent window handle is to be retrieved.</param>
    /// <returns>A handle to the parent window or owner window.</returns>
    [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr GetParent(IntPtr hWnd);

    /// <summary>Changes the location and dimensions of the specified window.</summary>
    /// <param name="hWnd">A handle to the window.</param>
    /// <param name="X">The new position of the left side of the window.</param>
    /// <param name="Y">The new position of the top side of the window.</param>
    /// <param name="nWidth">The new width of the window.</param>
    /// <param name="nHeight">The new height of the window.</param>
    /// <param name="bRepaint">Indicates whether the window is to be repainted.</param>
    /// <returns><c>true</c> if the window position was changed; otherwise, <c>false</c>.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    /// <summary>Queries or configures system-wide parameters.</summary>
    /// <param name="uiAction">The system-wide parameter to be queried or set.</param>
    /// <param name="uiParam">A parameter whose usage depends on the system parameter being queried or set.</param>
    /// <param name="pvParam">A parameter whose usage depends on the system parameter being queried or set.</param>
    /// <param name="fWinIni">If a system parameter is set, specifies whether the user profile is updated.</param>
    /// <returns><c>true</c> if successful; otherwise, <c>false</c>.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);

    /// <summary>Retrieves a handle to the display monitor nearest to the specified window.</summary>
    /// <param name="hwnd">A handle to the window of interest.</param>
    /// <param name="dwFlags">Determines the return value if the window does not intersect any display monitor.</param>
    /// <returns>A handle to the display monitor.</returns>
    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    /// <summary>Retrieves detailed information about a display monitor.</summary>
    /// <param name="hMonitor">A handle to the display monitor of interest.</param>
    /// <param name="lpmi">A pointer to a <see cref="MONITORINFO"/> structure that receives information about the monitor.</param>
    /// <returns><c>true</c> if successful; otherwise, <c>false</c>.</returns>
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    /// <summary>Restores the window to its original size and position after being minimized or maximized.</summary>
    public const int SW_RESTORE = 9;

    /// <summary>Maximizes the specified window.</summary>
    public const int SW_MAXIMIZE = 3;

    /// <summary>Flag for keybd_event indicating that the key is being released.</summary>
    public const uint KEYEVENTF_KEYUP = 0x0002;

    /// <summary>SystemParametersInfo action to retrieve the size of the work area on the primary display monitor.</summary>
    public const uint SPI_GETWORKAREA = 0x0030;

    /// <summary>Returns a handle to the display monitor nearest to the window if it does not intersect any monitor.</summary>
    public const uint MONITOR_DEFAULTTONEAREST = 2;

    /// <summary>Window message sent to set the text of a window or control.</summary>
    public const uint WM_SETTEXT = 0x000C;

    /// <summary>Virtual key code for the CTRL key.</summary>
    public const int VK_CONTROL = 0x11;

    /// <summary>Virtual key code for the 'O' key.</summary>
    public const int VK_O = 0x4F;

    /// <summary>Flag for GetAncestor to retrieve the root window by walking the chain of parent windows.</summary>
    public const uint GA_ROOT = 2;

    /// <summary>Flag for GetWindow to retrieve the owner window handle.</summary>
    public const uint GW_OWNER = 4;

    /// <summary>Window message posted when the user presses the left mouse button in the client area.</summary>
    public const uint WM_LBUTTONDOWN = 0x0201;

    /// <summary>Window message posted when the user releases the left mouse button in the client area.</summary>
    public const uint WM_LBUTTONUP = 0x0202;

    /// <summary>Indicates that the left mouse button is pressed.</summary>
    public const int MK_LBUTTON = 0x0001;

    /// <summary>Button message sent to simulate clicking a button control.</summary>
    public const uint BM_CLICK = 0x00F5;

    /// <summary>Window message posted when a non-system key is pressed.</summary>
    public const uint WM_KEYDOWN = 0x0100;

    /// <summary>Window message posted when a non-system key is released.</summary>
    public const uint WM_KEYUP = 0x0101;

    /// <summary>Virtual key code for the ENTER key.</summary>
    public const int VK_RETURN = 0x0D;

    /// <summary>
    /// The standard Win32 window class name assigned to modal dialog boxes.
    /// </summary>
    public const string DialogClassName = "#32770";
}