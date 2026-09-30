// Copyright (c) VerisFlow. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VerisFlow.VenusAuto.Core.Contracts;
using VerisFlow.VenusAuto.Core.Internal;
using VerisFlow.VenusAuto.Core.Models;

namespace VerisFlow.VenusAuto.Core.Services;

/// <summary>
/// Drives Venus Run Control through menu/toolbar command IDs and dialog control IDs, which do not depend on the
/// UI language, DPI, window position, or focus. Coordinates and keyboard input remain as fallbacks for IDs set to 0.
/// </summary>
/// <remarks>
/// Only dialogs opened by this service's own command are answered (the Open dialog while loading, the pause dialog
/// on resume, the abort confirmation when confirmation was requested). Every other dialog is reported to the caller.
/// </remarks>
internal sealed partial class VenusRunControlService : IVenusRunControlService
{
    private const string StaticClass = "Static";
    private const string EditClass = "Edit";

    private static readonly TimeSpan DialogAppearTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ResumeDialogTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan OpenDialogTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan DialogCloseTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LoadConfirmationTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    private readonly VenusAutoOptions _options;
    private readonly RunControlIdentifiers _ids;
    private readonly IWindowOrchestrator _orchestrator;
    private readonly ISilentSimulator _simulator;
    private readonly IWindowMessenger _messenger;
    private readonly IDialogGuard _dialogGuard;
    private readonly ILogger<VenusRunControlService> _logger;

    public VenusRunControlService(
        IOptionsSnapshot<VenusAutoOptions> options,
        IWindowOrchestrator orchestrator,
        ISilentSimulator simulator,
        IWindowMessenger messenger,
        IDialogGuard dialogGuard,
        ILogger<VenusRunControlService> logger)
    {
        _options = options.Value;
        _ids = _options.RunControlIds ?? new RunControlIdentifiers();
        _orchestrator = orchestrator;
        _simulator = simulator;
        _messenger = messenger;
        _dialogGuard = dialogGuard;
        _logger = logger;
    }

    // ------------------------------------------------------------------ Run commands

    /// <inheritdoc />
    public async Task StartRunAsync(CancellationToken cancellationToken = default)
    {
        Log.StartingRun(_logger);

        var (processId, mainWindow) = await RequireRunControlAsync(cancellationToken).ConfigureAwait(false);
        EnsureNoBlockingDialog(processId, mainWindow, "a start command");
        EnsureCommandAvailable(mainWindow, _ids.StartCommand, "Start", "no method is loaded, or a run is already in progress");

        await ExecuteCommandAsync(mainWindow, _ids.StartCommand, _options.RunControlUI.StartButton, "Start").ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task PauseRunAsync(CancellationToken cancellationToken = default)
    {
        Log.PausingRun(_logger);

        var (processId, mainWindow) = await RequireRunControlAsync(cancellationToken).ConfigureAwait(false);
        EnsureNoBlockingDialog(processId, mainWindow, "a pause command");
        EnsureCommandAvailable(mainWindow, _ids.PauseCommand, "Pause", "no run is in progress");

        await ExecuteCommandAsync(mainWindow, _ids.PauseCommand, _options.RunControlUI.PauseButton, "Pause").ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ResumeRunAsync(CancellationToken cancellationToken = default)
    {
        Log.ResumingRun(_logger);

        var (processId, mainWindow) = await RequireRunControlAsync(cancellationToken).ConfigureAwait(false);

        var paused = _dialogGuard.GetDialogs(processId, mainWindow).FirstOrDefault(d => d.Kind == VenusDialogKind.Paused)
            ?? await _dialogGuard.WaitForDialogAsync(processId, mainWindow, d => d.Kind == VenusDialogKind.Paused, ResumeDialogTimeout, cancellationToken).ConfigureAwait(false);

        if (paused is null)
        {
            throw new InvalidOperationException("Run Control is not paused.");
        }

        var dialog = new IntPtr(paused.Handle);
        if (!_messenger.ClickButton(dialog, _ids.PausedResumeButton))
        {
            throw new InvalidOperationException("The Resume button of the pause dialog is not available.");
        }

        await _dialogGuard.WaitForCloseAsync(dialog, DialogCloseTimeout, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<VenusAbortResult> AbortRunAsync(bool confirm = false, CancellationToken cancellationToken = default)
    {
        Log.AbortingRun(_logger, confirm);

        var (processId, mainWindow) = await RequireRunControlAsync(cancellationToken).ConfigureAwait(false);
        var dialogs = _dialogGuard.GetDialogs(processId, mainWindow);
        var confirmation = dialogs.FirstOrDefault(d => d.Kind == VenusDialogKind.AbortConfirmation);

        if (confirmation is null)
        {
            // Another dialog waits for a person (e.g. an error): the abort would not reach Run Control.
            var other = dialogs.Where(d => d.BlocksMainWindow && d.Kind != VenusDialogKind.Paused).ToList();
            if (other.Count > 0)
            {
                throw new VenusDialogPendingException(
                    $"Run Control is waiting for a response to '{Describe(other[0])}'. Respond to it before aborting.", other);
            }

            var paused = dialogs.FirstOrDefault(d => d.Kind == VenusDialogKind.Paused);
            if (paused is not null)
            {
                if (!_messenger.ClickButton(new IntPtr(paused.Handle), _ids.PausedAbortButton))
                {
                    throw new InvalidOperationException("The Abort button of the pause dialog is not available.");
                }
            }
            else
            {
                EnsureCommandAvailable(mainWindow, _ids.AbortCommand, "Abort", "no run is in progress");
                await ExecuteCommandAsync(mainWindow, _ids.AbortCommand, _options.RunControlUI.AbortButton, "Abort").ConfigureAwait(false);
            }

            confirmation = await _dialogGuard.WaitForDialogAsync(
                processId, mainWindow, d => d.Kind == VenusDialogKind.AbortConfirmation, DialogAppearTimeout, cancellationToken).ConfigureAwait(false);

            if (confirmation is null)
            {
                Log.AbortConfirmationMissing(_logger);
                return new VenusAbortResult(false, null,
                    "Run Control did not ask for confirmation. Check the status to see whether the run stopped.");
            }
        }

        if (!confirm)
        {
            Log.AbortConfirmationPending(_logger);
            return new VenusAbortResult(false, confirmation,
                "Run Control asks for confirmation. The run continues until the abort is confirmed.");
        }

        var dialog = new IntPtr(confirmation.Handle);
        if (!_messenger.ClickButton(dialog, _ids.AbortConfirmButton))
        {
            throw new InvalidOperationException("The confirm button of the abort confirmation is not available.");
        }

        await _dialogGuard.WaitForCloseAsync(dialog, DialogCloseTimeout, cancellationToken).ConfigureAwait(false);
        Log.AbortConfirmed(_logger);

        return new VenusAbortResult(true, null, "The abort was confirmed.");
    }

    // ------------------------------------------------------------------ Status and dialogs

    /// <inheritdoc />
    public async Task<VenusSystemStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var processId = FindProcessId();
        if (processId is null)
        {
            return new VenusSystemStatus(RunState.Unknown, "Process not running", false, null, null);
        }

        var mainWindow = await _orchestrator.FindInteractiveWindowAsync(_options.RunControlProcessName, cancellationToken).ConfigureAwait(false);
        var dialogs = _dialogGuard.GetDialogs(processId.Value, mainWindow);

        if (mainWindow == IntPtr.Zero)
        {
            return new VenusSystemStatus(
                dialogs.Count > 0 ? RunState.WaitingForUser : RunState.Unknown,
                "Main window hidden or inaccessible",
                dialogs.Count > 0,
                dialogs.Count > 0 ? Describe(dialogs[0]) : null,
                null)
            { Dialogs = dialogs };
        }

        var loadedMethodName = ReadLoadedMethodName(mainWindow);
        var commands = ReadCommandState(mainWindow);
        var statusText = ReadStatusText(mainWindow);

        var pending = dialogs.Where(d => d.BlocksMainWindow && d.Kind != VenusDialogKind.Paused).ToList();

        RunState state;
        if (pending.Count > 0)
        {
            state = RunState.WaitingForUser;
        }
        else if (dialogs.Any(d => d.Kind == VenusDialogKind.Paused))
        {
            state = RunState.Paused;
        }
        else
        {
            state = Combine(FromCommands(commands), FromStatusText(statusText));
        }

        return new VenusSystemStatus(
            state,
            statusText,
            pending.Count > 0,
            pending.Count > 0 ? Describe(pending[0]) : null,
            loadedMethodName)
        {
            Dialogs = dialogs,
            Commands = commands
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VenusDialogInfo>> GetDialogsAsync(CancellationToken cancellationToken = default)
    {
        var processId = FindProcessId();
        if (processId is null)
        {
            return Array.Empty<VenusDialogInfo>();
        }

        var mainWindow = await _orchestrator.FindInteractiveWindowAsync(_options.RunControlProcessName, cancellationToken).ConfigureAwait(false);
        return _dialogGuard.GetDialogs(processId.Value, mainWindow);
    }

    /// <inheritdoc />
    public async Task<VenusDialogResponse> RespondToDialogAsync(long dialogHandle, int buttonId, string fingerprint, CancellationToken cancellationToken = default)
    {
        var processId = FindProcessId()
            ?? throw new InvalidOperationException($"Run Control ('{_options.RunControlProcessName}') is not running.");

        var mainWindow = await _orchestrator.FindInteractiveWindowAsync(_options.RunControlProcessName, cancellationToken).ConfigureAwait(false);
        var hwnd = new IntPtr(dialogHandle);

        var dialog = _dialogGuard.Inspect(hwnd, processId, mainWindow)
            ?? throw new InvalidOperationException("The dialog is no longer open. Read the dialogs again.");

        if (!string.Equals(dialog.Fingerprint, fingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The dialog changed since it was read. Read the dialogs again before responding.");
        }

        var button = dialog.Buttons.FirstOrDefault(b => b.Id == buttonId)
            ?? throw new ArgumentException($"The dialog '{dialog.Title}' has no button with ID {buttonId}.", nameof(buttonId));

        if (!button.Enabled)
        {
            throw new InvalidOperationException($"The button '{button.Text}' is disabled.");
        }

        Log.RespondingToDialog(_logger, dialog.Title, button.Text);

        if (!_messenger.ClickButton(hwnd, buttonId))
        {
            throw new InvalidOperationException($"Pressing '{button.Text}' failed.");
        }

        var closed = await _dialogGuard.WaitForCloseAsync(hwnd, DialogCloseTimeout, cancellationToken).ConfigureAwait(false);

        // Give a follow-up dialog (opened by the button) a moment to appear.
        await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);

        return new VenusDialogResponse(closed, _dialogGuard.GetDialogs(processId, mainWindow));
    }

    // ------------------------------------------------------------------ Loading

    /// <inheritdoc />
    public async Task<VenusLoadResult> LoadMethodAsync(string methodPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(methodPath))
        {
            throw new ArgumentException("A method path is required.", nameof(methodPath));
        }

        Log.LoadingMethod(_logger, methodPath);

        var (processId, mainWindow) = await RequireRunControlAsync(cancellationToken).ConfigureAwait(false);

        var before = _dialogGuard.GetDialogs(processId, mainWindow);
        var blocking = before.Where(d => d.BlocksMainWindow).ToList();
        if (blocking.Count > 0)
        {
            return new VenusLoadResult(false, ReadLoadedMethodName(mainWindow), blocking,
                "Run Control is waiting for a response to a dialog. Respond to it before loading a method.");
        }

        if (_ids.AbortCommand > 0 && _messenger.IsCommandEnabled(mainWindow, _ids.AbortCommand) == true)
        {
            throw new InvalidOperationException("A method is running. Abort it before loading another method.");
        }

        var known = new HashSet<long>(before.Select(d => d.Handle));

        await OpenFileDialogAsync(mainWindow, cancellationToken).ConfigureAwait(false);

        var dialogInfo = await _dialogGuard.WaitForDialogAsync(
            processId, mainWindow, d => !known.Contains(d.Handle), OpenDialogTimeout, cancellationToken).ConfigureAwait(false);

        if (dialogInfo is null)
        {
            throw new TimeoutException("The Open dialog of Run Control did not appear.");
        }

        if (dialogInfo.Kind != VenusDialogKind.FileOpen)
        {
            Log.LoadInterruptedByDialog(_logger, dialogInfo.Title);
            return new VenusLoadResult(false, ReadLoadedMethodName(mainWindow), new[] { dialogInfo },
                "Run Control showed a dialog instead of the Open dialog. Respond to it, then load the method again.");
        }

        Log.OpenDialogShown(_logger);
        var fileDialog = new IntPtr(dialogInfo.Handle);
        EnterPath(fileDialog, methodPath);

        if (!_messenger.ClickButton(fileDialog, _ids.FileDialogOkButton))
        {
            CancelFileDialog(fileDialog);
            throw new InvalidOperationException("The Open button of the file dialog is not available.");
        }

        known.Add(dialogInfo.Handle);
        var expectedName = Path.GetFileNameWithoutExtension(methodPath);
        var deadline = DateTime.UtcNow + LoadConfirmationTimeout;

        // Wait until the title shows the method. Any new dialog stops the wait and is returned unanswered.
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);

            var dialogs = _dialogGuard.GetDialogs(processId, mainWindow);
            var fresh = dialogs.Where(d => !known.Contains(d.Handle)).ToList();

            if (fresh.Count > 0)
            {
                Log.LoadInterruptedByDialog(_logger, fresh[0].Title);
                return new VenusLoadResult(false, ReadLoadedMethodName(mainWindow), fresh,
                    "Run Control showed a dialog while loading the method. It was not answered; respond to it to continue.");
            }

            if (dialogs.Any(d => d.Handle == dialogInfo.Handle))
            {
                continue;
            }

            var loaded = ReadLoadedMethodName(mainWindow);
            if (loaded is not null && loaded.ContainsIgnoreCase(expectedName))
            {
                Log.MethodLoaded(_logger, loaded);
                return new VenusLoadResult(true, loaded, Array.Empty<VenusDialogInfo>(), "The method was loaded.");
            }
        }

        var stillOpen = _dialogGuard.Inspect(fileDialog, processId, mainWindow);
        if (stillOpen is not null)
        {
            return new VenusLoadResult(false, ReadLoadedMethodName(mainWindow), new[] { stillOpen },
                "The Open dialog did not accept the path and is still open.");
        }

        Log.LoadNotConfirmed(_logger);
        return new VenusLoadResult(false, ReadLoadedMethodName(mainWindow), Array.Empty<VenusDialogInfo>(),
            $"Loading was not confirmed within {LoadConfirmationTimeout.TotalSeconds:0} s: the window title does not show the method.");
    }

    private async Task OpenFileDialogAsync(IntPtr mainWindow, CancellationToken cancellationToken)
    {
        if (_ids.OpenFileCommand > 0)
        {
            if (!_messenger.PostCommand(mainWindow, _ids.OpenFileCommand))
            {
                throw new InvalidOperationException("The Open command could not be sent to Run Control.");
            }

            return;
        }

        // Fallback: keyboard shortcut, which needs Run Control in the foreground.
        Log.UsingInputFallback(_logger, "Open");

        if (NativeMethods.IsIconic(mainWindow))
        {
            NativeMethods.ShowWindow(mainWindow, NativeMethods.SW_RESTORE);
            await Task.Delay(300, cancellationToken).ConfigureAwait(false);
        }

        NativeMethods.SetForegroundWindow(mainWindow);
        await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        await _simulator.SendCtrlShortcutAsync(mainWindow, NativeMethods.VK_O).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the path into the file name box (by control ID) and reads it back. Cancels the dialog on failure.
    /// </summary>
    private void EnterPath(IntPtr fileDialog, string methodPath)
    {
        var edit = FindFileNameEdit(fileDialog);
        if (edit == IntPtr.Zero)
        {
            CancelFileDialog(fileDialog);
            throw new InvalidOperationException("The file name box of the Open dialog was not found.");
        }

        // Text sent to an ANSI window is converted with the system code page, which loses other characters.
        if (!_messenger.IsUnicodeWindow(edit) && methodPath.Any(c => c > 127))
        {
            CancelFileDialog(fileDialog);
            throw new ArgumentException(
                "This Run Control accepts only ASCII paths. Move the method to a folder whose path contains only ASCII characters.",
                nameof(methodPath));
        }

        if (!_messenger.SetText(edit, methodPath)
            || !string.Equals(_messenger.GetText(edit), methodPath, StringComparison.OrdinalIgnoreCase))
        {
            CancelFileDialog(fileDialog);
            throw new InvalidOperationException("The path could not be entered into the Open dialog.");
        }
    }

    /// <summary>
    /// The edit box of the file name control: the control itself (old dialogs) or the Edit inside its combo box.
    /// </summary>
    private IntPtr FindFileNameEdit(IntPtr fileDialog)
    {
        var control = _messenger.FindDescendant(fileDialog, null, _ids.FileNameControl);
        if (control == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        return string.Equals(_messenger.GetClassName(control), EditClass, StringComparison.OrdinalIgnoreCase)
            ? control
            : _messenger.FindDescendant(control, EditClass, -1);
    }

    private void CancelFileDialog(IntPtr fileDialog)
    {
        if (_ids.FileDialogCancelButton > 0)
        {
            _messenger.ClickButton(fileDialog, _ids.FileDialogCancelButton);
        }
    }

    // ------------------------------------------------------------------ Process and window

    /// <inheritdoc />
    public Task GracefulShutdownAsync(CancellationToken cancellationToken = default)
    {
        var processes = Process.GetProcessesByName(_options.RunControlProcessName);
        foreach (var process in processes)
        {
            try
            {
                Log.ShutdownAttempt(_logger, process.Id);
                process.CloseMainWindow();
            }
            catch (Exception ex)
            {
                Log.ShutdownFailed(_logger, ex, process.Id);
            }
            finally
            {
                process.Dispose();
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task EnsureProcessStartedAsync(CancellationToken cancellationToken = default)
    {
        Log.CheckingProcessState(_logger, _options.RunControlProcessName);

        var runningId = FindProcessId();
        if (runningId is not null)
        {
            Log.ProcessAlreadyRunning(_logger, runningId.Value);
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(_options.RunControlExecutablePath))
        {
            throw new InvalidOperationException("Executable path is not configured.");
        }

        Log.StartingProcess(_logger, _options.RunControlExecutablePath);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _options.RunControlExecutablePath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal
            };

            using var process = Process.Start(startInfo);
            if (process is not null)
            {
                process.WaitForInputIdle(10000);
                Log.ProcessStartedSuccessfully(_logger);
            }
        }
        catch (Exception ex)
        {
            Log.ProcessStartFailed(_logger, ex);
            throw;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task ArrangeWindowAsync(WindowLayoutPreset preset, int customX = 0, int customY = 0, int customWidth = 0, int customHeight = 0, CancellationToken cancellationToken = default)
    {
        Log.ArrangingWindow(_logger, preset);

        IntPtr mainHwnd = await _orchestrator.FindInteractiveWindowAsync(_options.RunControlProcessName, cancellationToken).ConfigureAwait(false);
        if (mainHwnd == IntPtr.Zero) throw new InvalidOperationException("Main window not found.");

        if (preset == WindowLayoutPreset.Maximize)
        {
            NativeMethods.ShowWindow(mainHwnd, NativeMethods.SW_MAXIMIZE);
            return;
        }

        NativeMethods.ShowWindow(mainHwnd, NativeMethods.SW_RESTORE);

        IntPtr hMonitor = NativeMethods.MonitorFromWindow(mainHwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);

        var monitorInfo = new NativeMethods.MONITORINFO
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>()
        };

        NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo);
        NativeMethods.RECT workArea = monitorInfo.rcWork;

        int targetX = customX;
        int targetY = customY;
        int targetWidth = customWidth;
        int targetHeight = customHeight;

        switch (preset)
        {
            case WindowLayoutPreset.RightHalf:
                targetWidth = workArea.Width / 2;
                targetHeight = workArea.Height;
                targetX = workArea.Left + targetWidth;
                targetY = workArea.Top;
                break;

            case WindowLayoutPreset.LeftHalf:
                targetWidth = workArea.Width / 2;
                targetHeight = workArea.Height;
                targetX = workArea.Left;
                targetY = workArea.Top;
                break;

            case WindowLayoutPreset.Center:
                targetWidth = (int)(workArea.Width * 0.8);
                targetHeight = (int)(workArea.Height * 0.8);
                targetX = workArea.Left + (workArea.Width - targetWidth) / 2;
                targetY = workArea.Top + (workArea.Height - targetHeight) / 2;
                break;

            case WindowLayoutPreset.Custom:
                break;
        }

        NativeMethods.MoveWindow(mainHwnd, targetX, targetY, targetWidth, targetHeight, true);
        await Task.Delay(200, cancellationToken).ConfigureAwait(false);
    }

    // ------------------------------------------------------------------ Helpers

    private int? FindProcessId()
    {
        var processes = Process.GetProcessesByName(_options.RunControlProcessName);
        try
        {
            return processes.Length == 0 ? (int?)null : processes[0].Id;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private async Task<(int ProcessId, IntPtr MainWindow)> RequireRunControlAsync(CancellationToken cancellationToken)
    {
        var processId = FindProcessId()
            ?? throw new InvalidOperationException($"Run Control ('{_options.RunControlProcessName}') is not running.");

        var mainWindow = await _orchestrator.FindInteractiveWindowAsync(_options.RunControlProcessName, cancellationToken).ConfigureAwait(false);
        if (mainWindow == IntPtr.Zero)
        {
            throw new InvalidOperationException("The Run Control main window was not found.");
        }

        return (processId, mainWindow);
    }

    private void EnsureNoBlockingDialog(int processId, IntPtr mainWindow, string action)
    {
        var blocking = _dialogGuard.GetDialogs(processId, mainWindow).Where(d => d.BlocksMainWindow).ToList();
        if (blocking.Count > 0)
        {
            throw new VenusDialogPendingException(
                $"Run Control is waiting for a response to '{Describe(blocking[0])}' and cannot accept {action}. Respond to the dialog first.",
                blocking);
        }
    }

    /// <summary>
    /// Refuses a command whose toolbar button is disabled, with the reason a person would see. Unknown state passes.
    /// </summary>
    private void EnsureCommandAvailable(IntPtr mainWindow, int commandId, string name, string reason)
    {
        if (commandId > 0 && _messenger.IsCommandEnabled(mainWindow, commandId) == false)
        {
            throw new InvalidOperationException($"{name} is not available in Run Control right now: {reason}.");
        }
    }

    private async Task ExecuteCommandAsync(IntPtr mainWindow, int commandId, RelativePoint fallback, string name)
    {
        if (commandId > 0)
        {
            if (!_messenger.PostCommand(mainWindow, commandId))
            {
                throw new InvalidOperationException($"The {name} command could not be sent to Run Control.");
            }

            return;
        }

        Log.UsingInputFallback(_logger, name);
        await _simulator.ClickRelativeAsync(mainWindow, fallback.X, fallback.Y).ConfigureAwait(false);
    }

    private string? ReadLoadedMethodName(IntPtr mainWindow)
    {
        var title = _messenger.GetText(mainWindow);
        var separator = title.IndexOf(" - ", StringComparison.Ordinal);
        if (separator < 0)
        {
            return null;
        }

        var name = title.Substring(separator + 3).Trim();
        return name.Length > 0 ? name : null;
    }

    private VenusCommandState? ReadCommandState(IntPtr mainWindow)
    {
        if (_ids.StartCommand <= 0 && _ids.PauseCommand <= 0 && _ids.SingleStepCommand <= 0 && _ids.AbortCommand <= 0)
        {
            return null;
        }

        bool? Query(int id) => id > 0 ? _messenger.IsCommandEnabled(mainWindow, id) : null;

        return new VenusCommandState(Query(_ids.StartCommand), Query(_ids.PauseCommand), Query(_ids.SingleStepCommand), Query(_ids.AbortCommand));
    }

    private string ReadStatusText(IntPtr mainWindow)
    {
        var control = _ids.StatusControl > 0 ? _messenger.FindDescendant(mainWindow, StaticClass, _ids.StatusControl) : IntPtr.Zero;

        if (control == IntPtr.Zero)
        {
            var point = _options.RunControlUI.StatusWindow;
            if (point.X != 0 || point.Y != 0)
            {
                control = _messenger.WindowAtClientPoint(mainWindow, point.X, point.Y);
            }
        }

        return control == IntPtr.Zero ? string.Empty : _messenger.GetText(control).Trim();
    }

    /// <summary>
    /// State from toolbar availability: Pause only while running; Abort without Pause while busy; Start when idle.
    /// </summary>
    private static RunState FromCommands(VenusCommandState? commands)
    {
        if (commands is null)
        {
            return RunState.Unknown;
        }

        if (commands.CanPause == true)
        {
            return RunState.Running;
        }

        if (commands.CanAbort == true)
        {
            return RunState.Busy;
        }

        if (commands.CanStart == true)
        {
            return RunState.Idle;
        }

        return RunState.Unknown;
    }

    /// <summary>
    /// State from the (English) status text; null when the text is not recognized, e.g. in another UI language.
    /// </summary>
    private static RunState? FromStatusText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (text.ContainsIgnoreCase("Error") || text.ContainsIgnoreCase("Stopped") || text.ContainsIgnoreCase("Abort"))
        {
            return RunState.Error;
        }

        if (text.ContainsIgnoreCase("Idle"))
        {
            return RunState.Idle;
        }

        if (text.ContainsIgnoreCase("Pause"))
        {
            return RunState.Paused;
        }

        if (text.ContainsIgnoreCase("Running"))
        {
            return RunState.Running;
        }

        return null;
    }

    /// <summary>
    /// The toolbar decides; the text only fills in what the toolbar cannot tell (unknown state, or an error after a run).
    /// </summary>
    private static RunState Combine(RunState fromCommands, RunState? fromText)
    {
        if (fromCommands == RunState.Unknown)
        {
            return fromText ?? RunState.Unknown;
        }

        if (fromCommands == RunState.Idle && fromText == RunState.Error)
        {
            return RunState.Error;
        }

        return fromCommands;
    }

    private static string Describe(VenusDialogInfo dialog)
        => dialog.Message.Length > 0 ? $"{dialog.Title}: {dialog.Message}" : dialog.Title;

    /// <summary>
    /// Contains compile-time high-performance structured logging source-generated delegates.
    /// </summary>
    internal static partial class Log
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Starting the run.")]
        public static partial void StartingRun(ILogger logger);

        [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Pausing the run.")]
        public static partial void PausingRun(ILogger logger);

        [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Resuming the run.")]
        public static partial void ResumingRun(ILogger logger);

        [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Aborting the run (confirm: {Confirm}).")]
        public static partial void AbortingRun(ILogger logger, bool confirm);

        [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "The abort confirmation is open and waits for a decision.")]
        public static partial void AbortConfirmationPending(ILogger logger);

        [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "The abort was confirmed.")]
        public static partial void AbortConfirmed(ILogger logger);

        [LoggerMessage(EventId = 7, Level = LogLevel.Warning, Message = "Run Control did not show the abort confirmation.")]
        public static partial void AbortConfirmationMissing(ILogger logger);

        [LoggerMessage(EventId = 8, Level = LogLevel.Information, Message = "Loading method: {Path}")]
        public static partial void LoadingMethod(ILogger logger, string path);

        [LoggerMessage(EventId = 9, Level = LogLevel.Information, Message = "The Open dialog is shown; entering the path.")]
        public static partial void OpenDialogShown(ILogger logger);

        [LoggerMessage(EventId = 10, Level = LogLevel.Information, Message = "Method loaded: {Name}")]
        public static partial void MethodLoaded(ILogger logger, string name);

        [LoggerMessage(EventId = 11, Level = LogLevel.Warning, Message = "Loading stopped at dialog '{Title}'; it was left for the user.")]
        public static partial void LoadInterruptedByDialog(ILogger logger, string title);

        [LoggerMessage(EventId = 12, Level = LogLevel.Warning, Message = "Loading was not confirmed by the window title.")]
        public static partial void LoadNotConfirmed(ILogger logger);

        [LoggerMessage(EventId = 13, Level = LogLevel.Information, Message = "Responding to dialog '{Title}' with '{Button}'.")]
        public static partial void RespondingToDialog(ILogger logger, string title, string button);

        [LoggerMessage(EventId = 14, Level = LogLevel.Information, Message = "Attempting graceful shutdown of PID {Pid}")]
        public static partial void ShutdownAttempt(ILogger logger, int pid);

        [LoggerMessage(EventId = 15, Level = LogLevel.Warning, Message = "Graceful shutdown failed for PID {Pid}")]
        public static partial void ShutdownFailed(ILogger logger, Exception ex, int pid);

        [LoggerMessage(EventId = 16, Level = LogLevel.Information, Message = "Checking if {ProcessName} is running.")]
        public static partial void CheckingProcessState(ILogger logger, string processName);

        [LoggerMessage(EventId = 17, Level = LogLevel.Information, Message = "Process is already running (PID: {Pid}).")]
        public static partial void ProcessAlreadyRunning(ILogger logger, int pid);

        [LoggerMessage(EventId = 18, Level = LogLevel.Information, Message = "Process not found. Starting from: {Path}")]
        public static partial void StartingProcess(ILogger logger, string path);

        [LoggerMessage(EventId = 19, Level = LogLevel.Information, Message = "Process started successfully.")]
        public static partial void ProcessStartedSuccessfully(ILogger logger);

        [LoggerMessage(EventId = 20, Level = LogLevel.Error, Message = "Failed to start execution process.")]
        public static partial void ProcessStartFailed(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 21, Level = LogLevel.Information, Message = "Arranging window to preset: {Preset}")]
        public static partial void ArrangingWindow(ILogger logger, WindowLayoutPreset preset);

        [LoggerMessage(EventId = 22, Level = LogLevel.Debug, Message = "No command ID configured for {Command}; using input simulation.")]
        public static partial void UsingInputFallback(ILogger logger, string command);
    }
}