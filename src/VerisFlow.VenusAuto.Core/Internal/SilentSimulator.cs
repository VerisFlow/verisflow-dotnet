// Copyright (c) VerisFlow. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Buffers;
using System.Threading.Tasks;

namespace VerisFlow.VenusAuto.Core.Internal;

/// <summary>
/// Internal contract for executing background UI interaction events without taking control of the physical mouse cursor.
/// </summary>
internal interface ISilentSimulator
{
    /// <summary>
    /// Posts relative mouse click messages to a target window at specific client coordinates.
    /// </summary>
    /// <param name="mainWindowHwnd">The primary parent window handle.</param>
    /// <param name="mainWindowRelativeX">The relative horizontal pixel coordinate within the client area.</param>
    /// <param name="mainWindowRelativeY">The relative vertical pixel coordinate within the client area.</param>
    Task ClickRelativeAsync(IntPtr mainWindowHwnd, int mainWindowRelativeX, int mainWindowRelativeY);

    /// <summary>
    /// Searches child controls of a parent window for a Button matching specified text and posts a BM_CLICK message.
    /// </summary>
    /// <param name="parentHwnd">The parent window or dialog handle.</param>
    /// <param name="buttonText">The text string contained in the button caption.</param>
    Task ClickButtonByTextAsync(IntPtr parentHwnd, string buttonText);

    /// <summary>
    /// Synthesizes a key combination pressing Ctrl plus the specified virtual key code.
    /// </summary>
    /// <param name="hwnd">Target window handle receiving focus context.</param>
    /// <param name="key">The virtual key code to press alongside Ctrl.</param>
    Task SendCtrlShortcutAsync(IntPtr hwnd, int key);

    /// <summary>
    /// Sets text in a designated child input control (such as an Edit box) and posts an Enter keystroke.
    /// </summary>
    /// <param name="parentHwnd">The parent container window handle.</param>
    /// <param name="controlClass">The target child control class name (e.g. "Edit").</param>
    /// <param name="text">The string content to assign to the control.</param>
    Task SetControlTextAsync(IntPtr parentHwnd, string controlClass, string text);
}

/// <summary>
/// Provides non-intrusive UI automation mechanisms using low-level Win32 message posting techniques.
/// </summary>
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
        await Task.Delay(50);
        NativeMethods.PostMessage(targetHwnd, NativeMethods.WM_LBUTTONUP, IntPtr.Zero, lParam);
    }

    /// <inheritdoc />
    public Task ClickButtonByTextAsync(IntPtr parentHwnd, string buttonText)
    {
        if (parentHwnd == IntPtr.Zero) return Task.CompletedTask;

        IntPtr targetButtonHwnd = IntPtr.Zero;

        // Rent buffers from the shared pool to avoid GC allocations
        char[] classNameBuffer = ArrayPool<char>.Shared.Rent(256);
        char[] textBuffer = ArrayPool<char>.Shared.Rent(256);

        try
        {
            NativeMethods.EnumChildWindows(parentHwnd, (hwnd, lParam) =>
            {
                // Capture the actual length of the written string
                int classLength = NativeMethods.GetClassName(hwnd, classNameBuffer, classNameBuffer.Length);

                if (classLength > 0)
                {
                    // Create a string using only the valid characters, ignoring trailing nulls
                    string className = new string(classNameBuffer, 0, classLength);

                    if (className == "Button")
                    {
                        int textLength = NativeMethods.GetWindowText(hwnd, textBuffer, textBuffer.Length);

                        if (textLength > 0)
                        {
                            string windowText = new string(textBuffer, 0, textLength);

                            if (windowText.Contains(buttonText, StringComparison.OrdinalIgnoreCase))
                            {
                                targetButtonHwnd = hwnd;
                                return false; // Stop enumerating once found
                            }
                        }
                    }
                }
                return true;
            }, IntPtr.Zero);
        }
        finally
        {
            // Crucial: Always return the rented arrays to the pool to prevent memory leaks
            ArrayPool<char>.Shared.Return(classNameBuffer);
            ArrayPool<char>.Shared.Return(textBuffer);
        }

        if (targetButtonHwnd != IntPtr.Zero)
        {
            NativeMethods.PostMessage(targetButtonHwnd, NativeMethods.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SendCtrlShortcutAsync(IntPtr hwnd, int key)
    {
        if (hwnd == IntPtr.Zero) return;

        NativeMethods.keybd_event((byte)NativeMethods.VK_CONTROL, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event((byte)key, 0, 0, UIntPtr.Zero);

        await Task.Delay(50);

        NativeMethods.keybd_event((byte)key, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        NativeMethods.keybd_event((byte)NativeMethods.VK_CONTROL, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    /// <inheritdoc />
    public Task SetControlTextAsync(IntPtr parentHwnd, string controlClass, string text)
    {
        if (parentHwnd == IntPtr.Zero) return Task.CompletedTask;

        IntPtr targetHwnd = IntPtr.Zero;

        // Rent buffers from the shared pool
        char[] classNameBuffer = ArrayPool<char>.Shared.Rent(256);
        char[] parentClassBuffer = ArrayPool<char>.Shared.Rent(256);

        try
        {
            NativeMethods.EnumChildWindows(parentHwnd, (hwnd, lParam) =>
            {
                int classLength = NativeMethods.GetClassName(hwnd, classNameBuffer, classNameBuffer.Length);

                if (classLength > 0)
                {
                    string className = new string(classNameBuffer, 0, classLength);

                    if (className.Equals(controlClass, StringComparison.OrdinalIgnoreCase))
                    {
                        IntPtr parent = NativeMethods.GetParent(hwnd);
                        int parentClassLength = NativeMethods.GetClassName(parent, parentClassBuffer, parentClassBuffer.Length);

                        if (parentClassLength > 0)
                        {
                            string parentClassName = new string(parentClassBuffer, 0, parentClassLength);

                            if (parentClassName.Contains("ComboBox", StringComparison.OrdinalIgnoreCase))
                            {
                                targetHwnd = hwnd;
                                return false; // Found the exact target, stop searching
                            }
                        }

                        // Fallback, keep searching only if a target hasn't been found yet
                        if (targetHwnd == IntPtr.Zero)
                        {
                            targetHwnd = hwnd;
                        }
                    }
                }
                return true;
            }, IntPtr.Zero);
        }
        finally
        {
            // Return buffers to the pool
            ArrayPool<char>.Shared.Return(classNameBuffer);
            ArrayPool<char>.Shared.Return(parentClassBuffer);
        }

        if (targetHwnd != IntPtr.Zero)
        {
            NativeMethods.SendMessage(targetHwnd, NativeMethods.WM_SETTEXT, IntPtr.Zero, text);

            NativeMethods.PostMessage(targetHwnd, NativeMethods.WM_KEYDOWN, (IntPtr)NativeMethods.VK_RETURN, IntPtr.Zero);
            NativeMethods.PostMessage(targetHwnd, NativeMethods.WM_KEYUP, (IntPtr)NativeMethods.VK_RETURN, IntPtr.Zero);
        }

        return Task.CompletedTask;
    }
}