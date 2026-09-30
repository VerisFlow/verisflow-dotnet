// Copyright (c) VerisFlow. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VerisFlow.VenusAuto.Core.Models;

namespace VerisFlow.VenusAuto.Core.Contracts;

/// <summary>
/// Service contract for automating and monitoring Hamilton Venus Run Control process operations.
/// </summary>
/// <remarks>
/// Commands are sent by command and control IDs (see <see cref="RunControlIdentifiers"/>), so they do not depend on the
/// UI language, window position, or focus. The service never answers dialogs it did not open itself: unexpected dialogs
/// are reported to the caller (<see cref="VenusDialogInfo"/>), and a person decides how to respond.
/// </remarks>
public interface IVenusRunControlService
{
    /// <summary>
    /// Verifies that the Venus Run Control process is currently running, launching the configured executable if missing.
    /// </summary>
    Task EnsureProcessStartedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves and resizes the primary Venus application window according to the requested layout preset.
    /// </summary>
    Task ArrangeWindowAsync(WindowLayoutPreset preset, int customX = 0, int customY = 0, int customWidth = 0, int customHeight = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the loaded method.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">Run Control is not running, or Start is not available.</exception>
    /// <exception cref="VenusDialogPendingException">A dialog waits for a response.</exception>
    Task StartRunAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses the running method.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">Run Control is not running, or Pause is not available.</exception>
    /// <exception cref="VenusDialogPendingException">A dialog waits for a response.</exception>
    Task PauseRunAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a paused run by pressing Resume in the pause dialog.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">Run Control is not paused.</exception>
    Task ResumeRunAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests an abort (through the toolbar, or the pause dialog while paused). Without <paramref name="confirm"/>,
    /// the confirmation dialog is left open and returned, so a person can decide; with it, the abort is confirmed.
    /// </summary>
    Task<VenusAbortResult> AbortRunAsync(bool confirm = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the run state from open dialogs, toolbar command availability, and the status text.
    /// </summary>
    Task<VenusSystemStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the open Run Control dialogs, topmost first.
    /// </summary>
    Task<IReadOnlyList<VenusDialogInfo>> GetDialogsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Presses a button of an open dialog, after checking that it is still the dialog the caller read.
    /// </summary>
    /// <param name="dialogHandle">Handle from <see cref="VenusDialogInfo.Handle"/>.</param>
    /// <param name="buttonId">ID from <see cref="VenusDialogButton.Id"/>.</param>
    /// <param name="fingerprint">Fingerprint from <see cref="VenusDialogInfo.Fingerprint"/>.</param>
    /// <exception cref="System.InvalidOperationException">The dialog is gone or changed, or the button is disabled.</exception>
    /// <exception cref="System.ArgumentException">The dialog has no such button.</exception>
    Task<VenusDialogResponse> RespondToDialogAsync(long dialogHandle, int buttonId, string fingerprint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Initiates a graceful shutdown request by closing the main window of all active target processes.
    /// </summary>
    Task GracefulShutdownAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a method through the Open dialog. Dialogs that appear other than the Open dialog are not answered;
    /// they are returned in <see cref="VenusLoadResult.PendingDialogs"/>.
    /// </summary>
    /// <param name="methodPath">The absolute path to the method file to load.</param>
    /// <exception cref="System.InvalidOperationException">Run Control is not running, or a method is running.</exception>
    /// <exception cref="System.ArgumentException">The path is empty or cannot be entered into this Run Control.</exception>
    /// <exception cref="System.TimeoutException">The Open dialog did not appear.</exception>
    Task<VenusLoadResult> LoadMethodAsync(string methodPath, CancellationToken cancellationToken = default);
}