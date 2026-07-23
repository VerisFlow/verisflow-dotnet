using System;
using System.Runtime.InteropServices;
using System.Text;

namespace VerisFlow.VenusAuto.Sample.Native
{
    // Encapsulates Win32 API functions for UI probing and hotkey registration.
    internal static class NativeMethods
    {
        /// <summary>
        /// Represents an x- and y-coordinate pair that defines a point in a two-dimensional plane.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            /// <summary>
            /// The x-coordinate of the point.
            /// </summary>
            public int X;

            /// <summary>
            /// The y-coordinate of the point.
            /// </summary>
            public int Y;
        }

        /// <summary>
        /// Retrieves the position of the mouse cursor, in screen coordinates.
        /// </summary>
        /// <param name="lpPoint">A pointer to a <see cref="POINT"/> structure that receives the screen coordinates of the cursor.</param>
        /// <returns><c>true</c> if successful; otherwise, <c>false</c>.</returns>
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        /// <summary>
        /// Retrieves a handle to the window that contains the specified point.
        /// </summary>
        /// <param name="Point">The point to be checked.</param>
        /// <returns>The handle to the window that contains the point, or <see cref="IntPtr.Zero"/> if no window exists at the given point.</returns>
        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(POINT Point);

        /// <summary>
        /// Converts the screen coordinates of a specified point on the screen to client-area coordinates.
        /// </summary>
        /// <param name="hWnd">A handle to the window whose client area is used for the transformation.</param>
        /// <param name="lpPoint">A pointer to a <see cref="POINT"/> structure that contains the screen coordinates to be converted.</param>
        /// <returns><c>true</c> if successful; otherwise, <c>false</c>.</returns>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        /// <summary>
        /// Retrieves the name of the class to which the specified window belongs.
        /// </summary>
        /// <param name="hWnd">A handle to the window and, indirectly, the class to which the window belongs.</param>
        /// <param name="lpClassName">The buffer that is to receive the class name string.</param>
        /// <param name="nMaxCount">The length of the <paramref name="lpClassName"/> buffer, in characters.</param>
        /// <returns>The number of characters copied to the buffer, not including the terminating null character.</returns>
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, [Out] char[] lpClassName, int nMaxCount);

        /// <summary>
        /// Copies the text of the specified window's title bar or control into a buffer.
        /// </summary>
        /// <param name="hWnd">A handle to the window or control containing the text.</param>
        /// <param name="lpString">The buffer that will receive the text.</param>
        /// <param name="nMaxCount">The maximum number of characters to copy to the buffer, including the null character.</param>
        /// <returns>The length of the copied string in characters, not including the terminating null character.</returns>
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetWindowText(IntPtr hWnd, [Out] char[] lpString, int nMaxCount);

        /// <summary>
        /// Retrieves a handle to the specified window's parent or owner.
        /// </summary>
        /// <param name="hWnd">A handle to the window whose parent window handle is to be retrieved.</param>
        /// <returns>A handle to the parent window, owner window, or <see cref="IntPtr.Zero"/> if the window has no parent or owner.</returns>
        [DllImport("user32.dll")]
        public static extern IntPtr GetParent(IntPtr hWnd);

        /// <summary>
        /// Retrieves the handle to the ancestor of the specified window.
        /// </summary>
        /// <param name="hwnd">A handle to the window whose ancestor is to be retrieved.</param>
        /// <param name="gaFlags">The ancestor flag to be retrieved.</param>
        /// <returns>The handle to the ancestor window.</returns>
        [DllImport("user32.dll")]
        public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        /// <summary>
        /// Flag for GetAncestor to retrieve the root window by walking the chain of parent windows.
        /// </summary>
        public const uint GA_ROOT = 2;

        /// <summary>
        /// Retrieves a handle to a device context (DC) for the client area of a specified window or for the entire screen.
        /// </summary>
        /// <param name="hWnd">A handle to the window whose DC is to be retrieved. If <see cref="IntPtr.Zero"/>, retrieves the DC for the entire screen.</param>
        /// <returns>The handle to the device context for the specified window's client area.</returns>
        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        /// <summary>
        /// Retrieves the device context (DC) for the entire window, including title bar, menus, and scroll bars.
        /// </summary>
        /// <param name="hWnd">A handle to the window whose DC is to be retrieved.</param>
        /// <returns>The handle to the device context for the specified window.</returns>
        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowDC(IntPtr hWnd);

        /// <summary>
        /// Releases a device context (DC), freeing it for use by other applications.
        /// </summary>
        /// <param name="hWnd">A handle to the window whose DC is to be released.</param>
        /// <param name="hdc">A handle to the DC to be released.</param>
        /// <returns>1 if the DC was released; 0 if the DC was not released.</returns>
        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);

        /// <summary>
        /// Retrieves the red, green, blue (RGB) color value of the pixel at the specified coordinates.
        /// </summary>
        /// <param name="hdc">A handle to the device context.</param>
        /// <param name="nXPos">The x-coordinate, in logical units, of the pixel to be examined.</param>
        /// <param name="nYPos">The y-coordinate, in logical units, of the pixel to be examined.</param>
        /// <returns>The RGB color value of the pixel.</returns>
        [DllImport("gdi32.dll")]
        public static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);

        // Global hotkey registration APIs

        /// <summary>
        /// Defines a system-wide hot key.
        /// </summary>
        /// <param name="hWnd">A handle to the window that will receive WM_HOTKEY messages generated by the hot key.</param>
        /// <param name="id">The identifier of the hot key.</param>
        /// <param name="fsModifiers">The keys that must be pressed in combination with the key specified by <paramref name="vk"/>.</param>
        /// <param name="vk">The virtual-key code of the hot key.</param>
        /// <returns><c>true</c> if the hot key is registered successfully; otherwise, <c>false</c>.</returns>
        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        /// <summary>
        /// Frees a hot key previously registered by the calling thread.
        /// </summary>
        /// <param name="hWnd">A handle to the window associated with the hot key to be freed.</param>
        /// <param name="id">The identifier of the hot key to be freed.</param>
        /// <returns><c>true</c> if successful; otherwise, <c>false</c>.</returns>
        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // --- NEW API ADDITIONS FOR ACTION TESTING ---

        /// <summary>
        /// Places (posts) a message in the message queue associated with the thread that created the specified window.
        /// </summary>
        /// <param name="hWnd">A handle to the window whose window procedure is to receive the message.</param>
        /// <param name="Msg">The message to be posted.</param>
        /// <param name="wParam">Additional message-specific information.</param>
        /// <param name="lParam">Additional message-specific information.</param>
        /// <returns><c>true</c> if the function succeeds; otherwise, <c>false</c>.</returns>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Posted when the user presses a hot key registered by the <see cref="RegisterHotKey"/> function.
        /// </summary>
        public const int WM_HOTKEY = 0x0312;

        /// <summary>
        /// Indicates that no modifier key (e.g., ALT, CTRL, SHIFT) is required for a hotkey.
        /// </summary>
        public const uint MOD_NONE = 0x0000;

        /// <summary>
        /// Virtual key code for the F2 key.
        /// </summary>
        public const uint VK_F2 = 0x71;

        // Keyboard Constants

        /// <summary>
        /// Posted to the window with the keyboard focus when a non-system key is pressed.
        /// </summary>
        public const uint WM_KEYDOWN = 0x0100;

        /// <summary>
        /// Posted to the window with the keyboard focus when a non-system key is released.
        /// </summary>
        public const uint WM_KEYUP = 0x0101;

        /// <summary>
        /// Virtual key code for the F5 key.
        /// </summary>
        public const int VK_F5 = 0x76;

        // Mouse Constants

        /// <summary>
        /// Posted when the user presses the left mouse button while the cursor is in the client area of a window.
        /// </summary>
        public const uint WM_LBUTTONDOWN = 0x0201;

        /// <summary>
        /// Posted when the user releases the left mouse button while the cursor is in the client area of a window.
        /// </summary>
        public const uint WM_LBUTTONUP = 0x0202;

        /// <summary>
        /// Indicates that the left mouse button is down.
        /// </summary>
        public const int MK_LBUTTON = 0x0001;
    }
}