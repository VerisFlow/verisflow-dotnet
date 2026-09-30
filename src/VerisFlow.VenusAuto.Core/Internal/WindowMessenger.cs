// Copyright (c) VerisFlow. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;

namespace VerisFlow.VenusAuto.Core.Internal;

/// <summary>
/// Win32 window queries and messages used to drive Run Control. All reads and writes of window text use
/// SendMessageTimeout, so a hung Run Control never blocks the caller.
/// </summary>
internal interface IWindowMessenger
{
    bool IsWindow(IntPtr hwnd);

    bool IsWindowVisible(IntPtr hwnd);

    bool IsWindowEnabled(IntPtr hwnd);

    /// <summary>True when the window expects Unicode text (otherwise text is converted with the system code page).</summary>
    bool IsUnicodeWindow(IntPtr hwnd);

    string GetClassName(IntPtr hwnd);

    int GetControlId(IntPtr hwnd);

    int GetStyle(IntPtr hwnd);

    IntPtr GetOwner(IntPtr hwnd);

    /// <summary>Process that owns the window; 0 when the window no longer exists.</summary>
    int GetProcessId(IntPtr hwnd);

    /// <summary>Text of a window or control, also of controls in other processes. Empty when unavailable.</summary>
    string GetText(IntPtr hwnd);

    bool SetText(IntPtr hwnd, string text);

    /// <summary>BM_GETCHECK of a check box or radio button: 0 unchecked, 1 checked, 2 indeterminate; null on failure.</summary>
    int? GetCheckState(IntPtr hwnd);

    /// <summary>Top-level windows of the process, topmost first.</summary>
    IReadOnlyList<IntPtr> GetTopLevelWindows(int processId);

    /// <summary>All descendants of the window, in window order.</summary>
    IReadOnlyList<IntPtr> GetDescendants(IntPtr parent);

    /// <summary>First descendant with the class (null for any class) and control ID (negative for any ID).</summary>
    IntPtr FindDescendant(IntPtr parent, string? className, int controlId);

    /// <summary>Posts a menu/toolbar command (WM_COMMAND with no control), as a menu or accelerator would.</summary>
    bool PostCommand(IntPtr window, int commandId);

    /// <summary>Presses an enabled button of a dialog by posting the notification the button itself would send.</summary>
    bool ClickButton(IntPtr dialog, int buttonId);

    /// <summary>Whether a toolbar button of the window is enabled; null when no toolbar has the command.</summary>
    bool? IsCommandEnabled(IntPtr mainWindow, int commandId);

    /// <summary>The window at a point given in client coordinates of <paramref name="window"/>.</summary>
    IntPtr WindowAtClientPoint(IntPtr window, int x, int y);
}

/// <inheritdoc />
internal sealed class WindowMessenger : IWindowMessenger
{
    private const uint TimeoutMilliseconds = 1000;
    private const int MaxTextLength = 8192;
    private const string ButtonClass = "Button";
    private const string ToolbarClass = "ToolbarWindow32";

    public bool IsWindow(IntPtr hwnd) => hwnd != IntPtr.Zero && NativeMethods.IsWindow(hwnd);

    public bool IsWindowVisible(IntPtr hwnd) => hwnd != IntPtr.Zero && NativeMethods.IsWindowVisible(hwnd);

    public bool IsWindowEnabled(IntPtr hwnd) => hwnd != IntPtr.Zero && NativeMethods.IsWindowEnabled(hwnd);

    public bool IsUnicodeWindow(IntPtr hwnd) => hwnd != IntPtr.Zero && NativeMethods.IsWindowUnicode(hwnd);

    public string GetClassName(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return string.Empty;
        }

        var buffer = new char[256];
        var length = NativeMethods.GetClassName(hwnd, buffer, buffer.Length);
        return length > 0 ? new string(buffer, 0, length) : string.Empty;
    }

    public int GetControlId(IntPtr hwnd) => hwnd == IntPtr.Zero ? 0 : NativeMethods.GetDlgCtrlID(hwnd);

    public int GetStyle(IntPtr hwnd) => hwnd == IntPtr.Zero ? 0 : NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_STYLE);

    public IntPtr GetOwner(IntPtr hwnd) => hwnd == IntPtr.Zero ? IntPtr.Zero : NativeMethods.GetWindow(hwnd, NativeMethods.GW_OWNER);

    public int GetProcessId(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return 0;
        }

        // A thread ID of 0 means the window no longer exists; the process ID is then meaningless.
        return NativeMethods.GetWindowThreadProcessId(hwnd, out var processId) == 0 ? 0 : (int)processId;
    }

    public string GetText(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return string.Empty;
        }

        if (!Send(hwnd, NativeMethods.WM_GETTEXTLENGTH, IntPtr.Zero, out var lengthResult))
        {
            return string.Empty;
        }

        var length = lengthResult.ToInt64();
        if (length <= 0)
        {
            return string.Empty;
        }

        var capacity = (int)Math.Min(length, MaxTextLength) + 1;
        var buffer = new char[capacity];

        if (NativeMethods.SendMessageTimeoutText(
                hwnd, NativeMethods.WM_GETTEXT, new IntPtr(capacity), buffer,
                NativeMethods.SMTO_ABORTIFHUNG, TimeoutMilliseconds, out var copiedResult) == IntPtr.Zero)
        {
            return string.Empty;
        }

        var copied = (int)Math.Min(copiedResult.ToInt64(), capacity - 1);
        return copied > 0 ? new string(buffer, 0, copied) : string.Empty;
    }

    public bool SetText(IntPtr hwnd, string text)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        return NativeMethods.SendMessageTimeoutString(
                   hwnd, NativeMethods.WM_SETTEXT, IntPtr.Zero, text,
                   NativeMethods.SMTO_ABORTIFHUNG, TimeoutMilliseconds, out var result) != IntPtr.Zero
               && result != IntPtr.Zero;
    }

    public int? GetCheckState(IntPtr hwnd)
        => Send(hwnd, NativeMethods.BM_GETCHECK, IntPtr.Zero, out var result) ? (int)result.ToInt64() : (int?)null;

    public IReadOnlyList<IntPtr> GetTopLevelWindows(int processId)
    {
        var windows = new List<IntPtr>();

        NativeMethods.EnumWindows((hwnd, _) =>
        {
            // Skip windows that closed during enumeration (thread ID 0).
            if (NativeMethods.GetWindowThreadProcessId(hwnd, out var windowProcessId) != 0
                && (int)windowProcessId == processId)
            {
                windows.Add(hwnd);
            }

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    public IReadOnlyList<IntPtr> GetDescendants(IntPtr parent)
    {
        var children = new List<IntPtr>();
        if (parent == IntPtr.Zero)
        {
            return children;
        }

        NativeMethods.EnumChildWindows(parent, (hwnd, _) =>
        {
            children.Add(hwnd);
            return true;
        }, IntPtr.Zero);

        return children;
    }

    public IntPtr FindDescendant(IntPtr parent, string? className, int controlId)
    {
        foreach (var child in GetDescendants(parent))
        {
            if (controlId >= 0 && GetControlId(child) != controlId)
            {
                continue;
            }

            if (className is not null && !string.Equals(GetClassName(child), className, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return child;
        }

        return IntPtr.Zero;
    }

    public bool PostCommand(IntPtr window, int commandId)
    {
        if (window == IntPtr.Zero || commandId <= 0)
        {
            return false;
        }

        // High word 0 and no control handle: handled like a menu command. MFC then checks the command's
        // update handler first and ignores commands that are currently disabled, as the UI would.
        return NativeMethods.PostMessage(window, NativeMethods.WM_COMMAND, new IntPtr(commandId & 0xFFFF), IntPtr.Zero);
    }

    public bool ClickButton(IntPtr dialog, int buttonId)
    {
        var button = FindDescendant(dialog, ButtonClass, buttonId);
        if (button == IntPtr.Zero || !IsWindowEnabled(button))
        {
            return false;
        }

        var parent = NativeMethods.GetParent(button);
        if (parent == IntPtr.Zero)
        {
            parent = dialog;
        }

        // The notification a real click sends to the parent: WM_COMMAND, BN_CLICKED (0) in the high word, the button handle.
        return NativeMethods.PostMessage(parent, NativeMethods.WM_COMMAND, new IntPtr(buttonId & 0xFFFF), button);
    }

    public bool? IsCommandEnabled(IntPtr mainWindow, int commandId)
    {
        if (mainWindow == IntPtr.Zero || commandId <= 0)
        {
            return null;
        }

        foreach (var child in GetDescendants(mainWindow))
        {
            if (!string.Equals(GetClassName(child), ToolbarClass, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Only integer arguments, so these messages are safe across processes.
            if (!Send(child, NativeMethods.TB_COMMANDTOINDEX, new IntPtr(commandId), out var index)
                || unchecked((int)index.ToInt64()) < 0)
            {
                continue;
            }

            return Send(child, NativeMethods.TB_ISBUTTONENABLED, new IntPtr(commandId), out var enabled)
                ? enabled != IntPtr.Zero
                : (bool?)null;
        }

        return null;
    }

    public IntPtr WindowAtClientPoint(IntPtr window, int x, int y)
    {
        if (window == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var point = new NativeMethods.POINT { X = x, Y = y };
        return NativeMethods.ClientToScreen(window, ref point) ? NativeMethods.WindowFromPoint(point) : IntPtr.Zero;
    }

    private static bool Send(IntPtr hwnd, uint message, IntPtr wParam, out IntPtr result)
    {
        result = IntPtr.Zero;
        return hwnd != IntPtr.Zero
            && NativeMethods.SendMessageTimeout(
                hwnd, message, wParam, IntPtr.Zero, NativeMethods.SMTO_ABORTIFHUNG, TimeoutMilliseconds, out result) != IntPtr.Zero;
    }
}