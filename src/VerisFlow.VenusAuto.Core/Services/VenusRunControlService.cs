// Copyright (c) VerisFlow. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using VerisFlow.VenusAuto.Core.Contracts;
using VerisFlow.VenusAuto.Core.Internal;
using VerisFlow.VenusAuto.Core.Models;

namespace VerisFlow.VenusAuto.Core.Services;

/// <summary>
/// Core implementation of <see cref="IVenusRunControlService"/> providing end-to-end automation control for Venus Run Control software.
/// </summary>
internal sealed partial class VenusRunControlService : IVenusRunControlService
{
    private readonly VenusAutoOptions _options;
    private readonly IWindowOrchestrator _orchestrator;
    private readonly ISilentSimulator _simulator;
    private readonly IDialogGuard _dialogGuard;
    private readonly ILogger<VenusRunControlService> _logger;

    public VenusRunControlService(
        IOptionsSnapshot<VenusAutoOptions> options,
        IWindowOrchestrator orchestrator,
        ISilentSimulator simulator,
        IDialogGuard dialogGuard,
        ILogger<VenusRunControlService> logger)
    {
        _options = options.Value;
        _orchestrator = orchestrator;
        _simulator = simulator;
        _dialogGuard = dialogGuard;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartRunAsync(CancellationToken cancellationToken = default)
    {
        Log.StartingExecutionSilently(_logger);

        IntPtr hwnd = await _orchestrator.FindInteractiveWindowAsync(_options.RunControlProcessName, cancellationToken);
        if (hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Cannot start run. Target window '{_options.RunControlProcessName}' not found.");
        }

        var btnCoords = _options.RunControlUI.StartButton;
        await _simulator.ClickRelativeAsync(hwnd, btnCoords.X, btnCoords.Y);
    }

    /// <inheritdoc />
    public async Task PauseRunAsync(CancellationToken cancellationToken = default)
    {
        Log.AttemptingToPause(_logger);

        IntPtr hwnd = await _orchestrator.FindInteractiveWindowAsync(_options.RunControlProcessName, cancellationToken);
        if (hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("Cannot pause. Main window not found.");
        }

        var btnCoords = _options.RunControlUI.PauseButton;
        await _simulator.ClickRelativeAsync(hwnd, btnCoords.X, btnCoords.Y);
    }

    /// <inheritdoc />
    public async Task ResumeRunAsync(CancellationToken cancellationToken = default)
    {
        Log.AttemptingToResume(_logger);

        var processes = Process.GetProcessesByName(_options.RunControlProcessName);
        if (processes.Length == 0) return;

        int mainProcessId = processes[0].Id;

        IntPtr dialogHwnd = await _dialogGuard.WaitForDialogAsync(mainProcessId, "Execution paused", TimeSpan.FromSeconds(2), cancellationToken);

        if (dialogHwnd != IntPtr.Zero)
        {
            Log.PauseDialogFound(_logger);
            await _simulator.ClickButtonByTextAsync(dialogHwnd, "Resume");
        }
        else
        {
            Log.PauseDialogNotFound(_logger);
        }
    }

    /// <inheritdoc />
    public async Task AbortRunAsync(CancellationToken cancellationToken = default)
    {
        Log.AttemptingToAbort(_logger);

        var processes = Process.GetProcessesByName(_options.RunControlProcessName);
        if (processes.Length == 0) return;

        int mainProcessId = processes[0].Id;

        IntPtr mainHwnd = await _orchestrator.FindInteractiveWindowAsync(_options.RunControlProcessName, cancellationToken);
        if (mainHwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("Cannot abort. Main window not found.");
        }

        var btnCoords = _options.RunControlUI.AbortButton;
        await _simulator.ClickRelativeAsync(mainHwnd, btnCoords.X, btnCoords.Y);

        Log.WaitingForAbortConfirmation(_logger);
        IntPtr dialogHwnd = await _dialogGuard.WaitForDialogAsync(mainProcessId, "Are you sure", TimeSpan.FromSeconds(5), cancellationToken);

        if (dialogHwnd != IntPtr.Zero)
        {
            Log.AbortConfirmationFound(_logger);
            await _simulator.ClickButtonByTextAsync(dialogHwnd, "Abort");
        }
        else
        {
            Log.AbortConfirmationNotFound(_logger);
        }
    }

    /// <inheritdoc />
    public async Task<VenusSystemStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var processes = Process.GetProcessesByName(_options.RunControlProcessName);
        if (processes.Length == 0)
        {
            return new VenusSystemStatus(RunState.Unknown, "Process not running", false, null, null);
        }

        int mainProcessId = processes[0].Id;

        IntPtr mainHwnd = await _orchestrator.FindInteractiveWindowAsync(_options.RunControlProcessName, cancellationToken);
        string? loadedMethodName = null;

        if (mainHwnd != IntPtr.Zero)
        {
            char[] titleBuffer = ArrayPool<char>.Shared.Rent(1024);
            try
            {
                int titleLength = NativeMethods.GetWindowText(mainHwnd, titleBuffer, titleBuffer.Length);
                if (titleLength > 0)
                {
                    string windowTitle = new string(titleBuffer, 0, titleLength);
                    int separatorIndex = windowTitle.IndexOf(" - ", StringComparison.Ordinal);
                    if (separatorIndex >= 0)
                    {
                        loadedMethodName = windowTitle.Substring(separatorIndex + 3).Trim();
                    }
                }
            }
            finally
            {
                ArrayPool<char>.Shared.Return(titleBuffer);
            }
        }

        var (hasError, isPaused, errorMessage) = await _dialogGuard.CheckAndHandleDialogsAsync(mainProcessId, cancellationToken);

        if (hasError)
        {
            return new VenusSystemStatus(RunState.Error, "Halted by dialog", true, errorMessage, loadedMethodName);
        }

        if (isPaused)
        {
            return new VenusSystemStatus(RunState.Paused, "Execution Paused", false, errorMessage, loadedMethodName);
        }

        if (mainHwnd == IntPtr.Zero)
        {
            return new VenusSystemStatus(RunState.Unknown, "Main window hidden or inaccessible", false, null, null);
        }

        NativeMethods.POINT pt = new NativeMethods.POINT
        {
            X = _options.RunControlUI.StatusWindow.X,
            Y = _options.RunControlUI.StatusWindow.Y
        };

        NativeMethods.ClientToScreen(mainHwnd, ref pt);
        IntPtr statusControlHwnd = NativeMethods.WindowFromPoint(pt);

        if (statusControlHwnd == IntPtr.Zero)
        {
            return new VenusSystemStatus(RunState.Unknown, "Status control not found at coordinates", false, null, loadedMethodName);
        }

        string rawStatus = string.Empty;
        char[] statusBuffer = ArrayPool<char>.Shared.Rent(1024);
        try
        {
            int statusLength = NativeMethods.GetWindowText(statusControlHwnd, statusBuffer, statusBuffer.Length);
            if (statusLength > 0)
            {
                rawStatus = new string(statusBuffer, 0, statusLength).Trim();
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(statusBuffer);
        }

        RunState state = RunState.Running;
        if (string.IsNullOrWhiteSpace(rawStatus))
        {
            state = RunState.Unknown;
        }
        else if (rawStatus.Contains("Idle", StringComparison.OrdinalIgnoreCase))
        {
            state = RunState.Idle;
        }
        else if (rawStatus.Contains("Error", StringComparison.OrdinalIgnoreCase) || rawStatus.Contains("Stopped", StringComparison.OrdinalIgnoreCase))
        {
            state = RunState.Error;
        }

        return new VenusSystemStatus(state, rawStatus, false, null, loadedMethodName);
    }

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
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task LoadMethodAsync(string methodPath, CancellationToken cancellationToken = default)
    {
        Log.AttemptingToLoadMethod(_logger, methodPath);

        IntPtr mainHwnd = await _orchestrator.FindInteractiveWindowAsync(_options.RunControlProcessName, cancellationToken);
        if (mainHwnd == IntPtr.Zero) throw new InvalidOperationException("Main window not found.");

        if (NativeMethods.IsIconic(mainHwnd))
        {
            NativeMethods.ShowWindow(mainHwnd, NativeMethods.SW_RESTORE);
            await Task.Delay(300, cancellationToken);
        }

        NativeMethods.SetForegroundWindow(mainHwnd);
        await Task.Delay(200, cancellationToken);

        await _simulator.SendCtrlShortcutAsync(mainHwnd, NativeMethods.VK_O);

        var processes = Process.GetProcessesByName(_options.RunControlProcessName);
        if (processes.Length == 0) return;

        Log.WaitingForOpenDialog(_logger);
        IntPtr dialogHwnd = await _dialogGuard.WaitForDialogAsync(processes[0].Id, "Open", TimeSpan.FromSeconds(30), cancellationToken);

        if (dialogHwnd != IntPtr.Zero)
        {
            await Task.Delay(800, cancellationToken);
            Log.InjectingMethodPath(_logger);
            await _simulator.SetControlTextAsync(dialogHwnd, "Edit", methodPath);
            await Task.Delay(200, cancellationToken);

            Log.SubmittingOpenDialog(_logger);
            NativeMethods.PostMessage(dialogHwnd, NativeMethods.WM_KEYDOWN, (IntPtr)NativeMethods.VK_RETURN, IntPtr.Zero);
            NativeMethods.PostMessage(dialogHwnd, NativeMethods.WM_KEYUP, (IntPtr)NativeMethods.VK_RETURN, IntPtr.Zero);
        }
        else
        {
            Log.OpenDialogNotFound(_logger);
            throw new TimeoutException("The Open dialog did not appear after pressing Ctrl+O.");
        }
    }

    /// <inheritdoc />
    public async Task EnsureProcessStartedAsync(CancellationToken cancellationToken = default)
    {
        Log.CheckingProcessState(_logger, _options.RunControlProcessName);

        var processes = Process.GetProcessesByName(_options.RunControlProcessName);
        if (processes.Length > 0)
        {
            Log.ProcessAlreadyRunning(_logger, processes[0].Id);
            return;
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

            var process = Process.Start(startInfo);
            if (process != null)
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
    }

    /// <inheritdoc />
    public async Task ArrangeWindowAsync(WindowLayoutPreset preset, int customX = 0, int customY = 0, int customWidth = 0, int customHeight = 0, CancellationToken cancellationToken = default)
    {
        Log.ArrangingWindow(_logger, preset);

        IntPtr mainHwnd = await _orchestrator.FindInteractiveWindowAsync(_options.RunControlProcessName, cancellationToken);
        if (mainHwnd == IntPtr.Zero) throw new InvalidOperationException("Main window not found.");

        if (preset == WindowLayoutPreset.Maximize)
        {
            NativeMethods.ShowWindow(mainHwnd, NativeMethods.SW_MAXIMIZE);
            return;
        }

        NativeMethods.ShowWindow(mainHwnd, NativeMethods.SW_RESTORE);

        IntPtr hMonitor = NativeMethods.MonitorFromWindow(mainHwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);

        NativeMethods.MONITORINFO monitorInfo = new NativeMethods.MONITORINFO();
        monitorInfo.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>();

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
        await Task.Delay(200, cancellationToken);
    }

    /// <summary>
    /// Contains compile-time high-performance structured logging source-generated delegates.
    /// </summary>
    internal static partial class Log
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Attempting to start execution process silently.")]
        public static partial void StartingExecutionSilently(ILogger logger);

        [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Attempting to pause execution.")]
        public static partial void AttemptingToPause(ILogger logger);

        [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Attempting to resume execution.")]
        public static partial void AttemptingToResume(ILogger logger);

        [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Pause dialog found. Sending click to the Resume button.")]
        public static partial void PauseDialogFound(ILogger logger);

        [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Execution paused dialog was not found.")]
        public static partial void PauseDialogNotFound(ILogger logger);

        [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "Attempting to abort execution.")]
        public static partial void AttemptingToAbort(ILogger logger);

        [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "Waiting for the confirmation dialog to appear.")]
        public static partial void WaitingForAbortConfirmation(ILogger logger);

        [LoggerMessage(EventId = 8, Level = LogLevel.Information, Message = "Confirmation dialog found. Sending click to the Abort button.")]
        public static partial void AbortConfirmationFound(ILogger logger);

        [LoggerMessage(EventId = 9, Level = LogLevel.Warning, Message = "Confirmation dialog did not appear within the timeout period.")]
        public static partial void AbortConfirmationNotFound(ILogger logger);

        [LoggerMessage(EventId = 10, Level = LogLevel.Information, Message = "Attempting graceful shutdown of PID {Pid}")]
        public static partial void ShutdownAttempt(ILogger logger, int pid);

        [LoggerMessage(EventId = 11, Level = LogLevel.Warning, Message = "Graceful shutdown failed for PID {Pid}")]
        public static partial void ShutdownFailed(ILogger logger, Exception ex, int pid);

        [LoggerMessage(EventId = 12, Level = LogLevel.Information, Message = "Attempting to load method: {Path}")]
        public static partial void AttemptingToLoadMethod(ILogger logger, string path);

        [LoggerMessage(EventId = 13, Level = LogLevel.Information, Message = "Waiting for Open dialog.")]
        public static partial void WaitingForOpenDialog(ILogger logger);

        [LoggerMessage(EventId = 14, Level = LogLevel.Information, Message = "Injecting method path into dialog.")]
        public static partial void InjectingMethodPath(ILogger logger);

        [LoggerMessage(EventId = 15, Level = LogLevel.Information, Message = "Submitting the dialog via Enter key.")]
        public static partial void SubmittingOpenDialog(ILogger logger);

        [LoggerMessage(EventId = 16, Level = LogLevel.Warning, Message = "Open dialog did not appear.")]
        public static partial void OpenDialogNotFound(ILogger logger);

        [LoggerMessage(EventId = 17, Level = LogLevel.Information, Message = "Checking if {ProcessName} is running.")]
        public static partial void CheckingProcessState(ILogger logger, string processName);

        [LoggerMessage(EventId = 18, Level = LogLevel.Information, Message = "Process is already running (PID: {Pid}).")]
        public static partial void ProcessAlreadyRunning(ILogger logger, int pid);

        [LoggerMessage(EventId = 19, Level = LogLevel.Information, Message = "Process not found. Starting from: {Path}")]
        public static partial void StartingProcess(ILogger logger, string path);

        [LoggerMessage(EventId = 20, Level = LogLevel.Information, Message = "Process started successfully.")]
        public static partial void ProcessStartedSuccessfully(ILogger logger);

        [LoggerMessage(EventId = 21, Level = LogLevel.Error, Message = "Failed to start execution process.")]
        public static partial void ProcessStartFailed(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 22, Level = LogLevel.Information, Message = "Arranging window to preset: {Preset}")]
        public static partial void ArrangingWindow(ILogger logger, WindowLayoutPreset preset);
    }
}