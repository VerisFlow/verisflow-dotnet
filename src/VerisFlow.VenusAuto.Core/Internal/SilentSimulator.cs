// Copyright (c) VerisFlow. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;

namespace VerisFlow.VenusAuto.Core.Internal;

/// <summary>
/// Input simulation used only as a fallback when no command ID is configured.
/// </summary>
internal interface ISilentSimulator
{
    /// <summary>
    /// Posts mouse click messages at client coordinates of the main window, to the window found at that point.
    /// The point must not be covered by another window.
    /// </summary>
    Task ClickRelativeAsync(IntPtr mainWindowHwnd, int mainWindowRelativeX, int mainWindowRelativeY);

    /// <summary>
    /// Presses Ctrl plus the key with global keyboard input; the target window must be in the foreground.
    /// </summary>
    Task SendCtrlShortcutAsync(IntPtr hwnd, int key);
}

/// <inheritdoc />
internal sealed class SilentSimulator : ISilentSimulator
{
    /// <inheritdoc />
    public async Task ClickRelativeAsync(IntPtr mainWindowHwnd, int mainWindowRelativeX, int mainWindowRelativeY)
    {
        if (mainWindowHwnd == IntPtr.Zero) return;

        var pt = new NativeMethods.POINT { X = mainWindowRelativeX, Y = mainWindowRelativeY };
        NativeMethods.ClientToScreen(mainWindowHwnd, ref pt);

        IntPtr targetHwnd = NativeMethods.WindowFromPoint(pt);
        if (targetHwnd == IntPtr.Zero) targetHwnd = mainWindowHwnd;

        NativeMethods.ScreenToClient(targetHwnd, ref pt);

        // Pack Y coordinate into high 16-bit word and X coordinate into low 16-bit word for WM_LBUTTON messages
        IntPtr lParam = (IntPtr)((pt.Y << 16) | (pt.X & 0xFFFF));

        NativeMethods.PostMessage(targetHwnd, NativeMethods.WM_LBUTTONDOWN, (IntPtr)NativeMethods.MK_LBUTTON, lParam);
        await Task.Delay(50).ConfigureAwait(false);
        NativeMethods.PostMessage(targetHwnd, NativeMethods.WM_LBUTTONUP, IntPtr.Zero, lParam);
    }

    /// <inheritdoc />
    public async Task SendCtrlShortcutAsync(IntPtr hwnd, int key)
    {
        if (hwnd == IntPtr.Zero) return;

        NativeMethods.keybd_event((byte)NativeMethods.VK_CONTROL, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event((byte)key, 0, 0, UIntPtr.Zero);

        await Task.Delay(50).ConfigureAwait(false);

        NativeMethods.keybd_event((byte)key, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        NativeMethods.keybd_event((byte)NativeMethods.VK_CONTROL, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
    }
}