// Copyright (c) VerisFlow. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using VerisFlow.VenusAuto.Core.Models;

namespace VerisFlow.VenusAuto.Core.Contracts;

/// <summary>
/// Service contract for automating and monitoring Hamilton Venus Run Control process operations.
/// </summary>
public interface IVenusRunControlService
{
    /// <summary>
    /// Verifies that the Venus Run Control process is currently running, launching the configured executable if missing.
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous start operation.</returns>
    Task EnsureProcessStartedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves and resizes the primary Venus application window according to the requested layout preset.
    /// </summary>
    /// <param name="preset">The layout preset specifying screen placement strategy.</param>
    /// <param name="customX">The X position offset when <paramref name="preset"/> is <see cref="WindowLayoutPreset.Custom"/>.</param>
    /// <param name="customY">The Y position offset when <paramref name="preset"/> is <see cref="WindowLayoutPreset.Custom"/>.</param>
    /// <param name="customWidth">The window width when <paramref name="preset"/> is <see cref="WindowLayoutPreset.Custom"/>.</param>
    /// <param name="customHeight">The window height when <paramref name="preset"/> is <see cref="WindowLayoutPreset.Custom"/>.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous window positioning operation.</returns>
    Task ArrangeWindowAsync(WindowLayoutPreset preset, int customX = 0, int customY = 0, int customWidth = 0, int customHeight = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins execution of the currently loaded method by simulating a click on the Start control button.
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous start command operation.</returns>
    /// <exception cref="System.InvalidOperationException">Thrown when the target process window cannot be found.</exception>
    Task StartRunAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses active method execution by simulating a click on the Pause control button.
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous pause command operation.</returns>
    /// <exception cref="System.InvalidOperationException">Thrown when the target application window is unavailable.</exception>
    Task PauseRunAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a paused execution run by confirming the execution pause modal dialog.
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous resume operation.</returns>
    Task ResumeRunAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Aborts an active execution run by clicking the Abort button and automatically confirming the confirmation prompt.
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous abort operation.</returns>
    /// <exception cref="System.InvalidOperationException">Thrown when the target main window cannot be found.</exception>
    Task AbortRunAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates the active application state, checking for blocking error dialogs, window status, and loaded method details.
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A snapshot record containing system status information.</returns>
    Task<VenusSystemStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Initiates a graceful shutdown request by closing the main window of all active target processes.
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous shutdown operation.</returns>
    Task GracefulShutdownAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Automates loading a method file (.hsl) into Venus Run Control using standard keyboard shortcuts and file dialog injection.
    /// </summary>
    /// <param name="methodPath">The absolute path to the method file to load.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous method loading operation.</returns>
    /// <exception cref="System.InvalidOperationException">Thrown when the main application window is unreachable.</exception>
    /// <exception cref="System.TimeoutException">Thrown when the file dialog fails to display within the designated timeout.</exception>
    Task LoadMethodAsync(string methodPath, CancellationToken cancellationToken = default);
}