// Copyright (c) VerisFlow. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace VerisFlow.VenusAuto.Core.Internal;

/// <summary>
/// Internal contract for finding top-level interactive window handles based on target process names.
/// </summary>
internal interface IWindowOrchestrator
{
    /// <summary>
    /// Scans desktop windows for a visible, top-level window owned by the target process name.
    /// </summary>
    /// <param name="processName">The executable process name without extension.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>The window handle (<see cref="IntPtr"/>) if located; otherwise, <see cref="IntPtr.Zero"/>.</returns>
    Task<IntPtr> FindInteractiveWindowAsync(string processName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves interactive top-level application windows by evaluating thread process identity and window owner properties.
/// </summary>
internal sealed class WindowOrchestrator : IWindowOrchestrator
{
    /// <inheritdoc />
    public Task<IntPtr> FindInteractiveWindowAsync(string processName, CancellationToken cancellationToken = default)
    {
        var processes = Process.GetProcessesByName(processName);
        if (processes.Length == 0) return Task.FromResult(IntPtr.Zero);

        var validProcessIds = new HashSet<int>(processes.Select(p => p.Id));
        IntPtr targetHwnd = IntPtr.Zero;

        NativeMethods.EnumWindows((hwnd, lParam) =>
        {
            if (cancellationToken.IsCancellationRequested) return false;

            _ = NativeMethods.GetWindowThreadProcessId(hwnd, out uint windowProcessId);

            if (validProcessIds.Contains((int)windowProcessId) && NativeMethods.IsWindowVisible(hwnd))
            {
                // Verify window has no owner (GW_OWNER) to distinguish top-level application windows from owned popups
                IntPtr ownerHwnd = NativeMethods.GetWindow(hwnd, NativeMethods.GW_OWNER);

                if (ownerHwnd == IntPtr.Zero)
                {
                    targetHwnd = hwnd;
                    return false;
                }
            }
            return true;
        }, IntPtr.Zero);

        return Task.FromResult(targetHwnd);
    }
}