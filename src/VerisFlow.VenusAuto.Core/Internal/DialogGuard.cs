// Copyright (c) VerisFlow. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace VerisFlow.VenusAuto.Core.Internal;

/// <summary>
/// Internal contract for identifying and resolving blocking modal dialogs spawned by target process threads.
/// </summary>
internal interface IDialogGuard
{
    /// <summary>
    /// Scans thread windows of the specified process to check for modal dialogs, auto-dismissing recoverable warnings when encountered.
    /// </summary>
    /// <param name="processId">The operating system process identifier to inspect.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A tuple containing flags indicating critical errors, paused states, and extracted dialog message text.</returns>
    Task<(bool HasError, bool IsPaused, string? DialogMessage)> CheckAndHandleDialogsAsync(int processId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously waits until a modal dialog containing the expected window title or message text appears.
    /// </summary>
    /// <param name="processId">The operating system process identifier to search.</param>
    /// <param name="expectedText">The text string to match within window title or child static controls.</param>
    /// <param name="timeout">The maximum time period allowed for searching.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>The window handle (<see cref="IntPtr"/>) of the detected dialog, or <see cref="IntPtr.Zero"/> if not found within the timeout.</returns>
    Task<IntPtr> WaitForDialogAsync(int processId, string expectedText, TimeSpan timeout, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation for inspecting thread windows, capturing standard Win32 dialogs (#32770), and auto-responding to benign prompts.
/// </summary>
internal sealed partial class DialogGuard : IDialogGuard
{
    private readonly ILogger<DialogGuard> _logger;

    public DialogGuard(ILogger<DialogGuard> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<(bool HasError, bool IsPaused, string? DialogMessage)> CheckAndHandleDialogsAsync(int processId, CancellationToken cancellationToken = default)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            char[] classNameBuffer = ArrayPool<char>.Shared.Rent(256);

            try
            {
                foreach (ProcessThread thread in process.Threads)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    bool isCriticalError = false;
                    bool isPaused = false;
                    string? dialogMessage = null;

                    // Enumerate windows associated with individual process threads to locate unmanaged dialog handles
                    NativeMethods.EnumThreadWindows((uint)thread.Id, (hwnd, lParam) =>
                    {
                        int classLength = NativeMethods.GetClassName(hwnd, classNameBuffer, classNameBuffer.Length);

                        if (classLength > 0)
                        {
                            string className = new string(classNameBuffer, 0, classLength);

                            if (className == NativeMethods.DialogClassName)
                            {
                                dialogMessage = ExtractDialogText(hwnd);
                                LogDialogDetected(thread.Id, dialogMessage);

                                if (IsRecoverableWarning(dialogMessage))
                                {
                                    LogRecoverableWarning();
                                    // Synthesize Enter keypress to automatically bypass non-blocking modal warnings
                                    NativeMethods.PostMessage(hwnd, NativeMethods.WM_KEYDOWN, (IntPtr)NativeMethods.VK_RETURN, IntPtr.Zero);
                                    NativeMethods.PostMessage(hwnd, NativeMethods.WM_KEYUP, (IntPtr)NativeMethods.VK_RETURN, IntPtr.Zero);
                                }
                                else if (dialogMessage.Contains("Execution paused", StringComparison.OrdinalIgnoreCase))
                                {
                                    isPaused = true;
                                    LogPausedState();
                                }
                                else
                                {
                                    LogCriticalDialog();
                                    isCriticalError = true;
                                }
                                return false;
                            }
                        }
                        return true;
                    }, IntPtr.Zero);

                    if (isCriticalError || isPaused)
                    {
                        return Task.FromResult((isCriticalError, isPaused, dialogMessage));
                    }
                }
            }
            finally
            {
                ArrayPool<char>.Shared.Return(classNameBuffer);
            }
        }
        catch (ArgumentException)
        {
            LogProcessExited(processId);
        }

        return Task.FromResult((false, false, (string?)null));
    }

    /// <inheritdoc />
    public async Task<IntPtr> WaitForDialogAsync(int processId, string expectedText, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutTokenSource.CancelAfter(timeout);
        var token = timeoutTokenSource.Token;

        char[] classNameBuffer = ArrayPool<char>.Shared.Rent(256);
        char[] windowTitleBuffer = ArrayPool<char>.Shared.Rent(256);

        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var process = Process.GetProcessById(processId);
                    IntPtr foundDialogHwnd = IntPtr.Zero;

                    foreach (ProcessThread thread in process.Threads)
                    {
                        if (token.IsCancellationRequested) break;

                        NativeMethods.EnumThreadWindows((uint)thread.Id, (hwnd, lParam) =>
                        {
                            int classLength = NativeMethods.GetClassName(hwnd, classNameBuffer, classNameBuffer.Length);

                            if (classLength > 0)
                            {
                                string className = new string(classNameBuffer, 0, classLength);

                                if (className == NativeMethods.DialogClassName)
                                {
                                    // NEW: Standard dialogs like "Open" use the Window Title.
                                    int titleLength = NativeMethods.GetWindowText(hwnd, windowTitleBuffer, windowTitleBuffer.Length);
                                    string windowTitle = titleLength > 0 ? new string(windowTitleBuffer, 0, titleLength) : string.Empty;

                                    string dialogText = ExtractDialogText(hwnd);

                                    if (windowTitle.Contains(expectedText, StringComparison.OrdinalIgnoreCase) ||
                                        dialogText.Contains(expectedText, StringComparison.OrdinalIgnoreCase))
                                    {
                                        foundDialogHwnd = hwnd;
                                        return false;
                                    }
                                }
                            }
                            return true;
                        }, IntPtr.Zero);

                        if (foundDialogHwnd != IntPtr.Zero) return foundDialogHwnd;
                    }
                }
                catch (ArgumentException) { break; }

                await Task.Delay(200, token);
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(classNameBuffer);
            ArrayPool<char>.Shared.Return(windowTitleBuffer);
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Traverses child controls of a dialog window to aggregate text content from all Static text elements.
    /// </summary>
    private static string ExtractDialogText(IntPtr dialogHwnd)
    {
        var textParts = new List<string>();
        // Pin memory handle to pass collection context safely into unmanaged EnumChildWindows callback
        var listHandle = GCHandle.Alloc(textParts);

        char[] childClassBuffer = ArrayPool<char>.Shared.Rent(256);
        char[] childTextBuffer = ArrayPool<char>.Shared.Rent(2048);

        try
        {
            NativeMethods.EnumChildWindows(dialogHwnd, (childHwnd, lParam) =>
            {
                int classLength = NativeMethods.GetClassName(childHwnd, childClassBuffer, childClassBuffer.Length);

                if (classLength > 0)
                {
                    string childClass = new string(childClassBuffer, 0, classLength);

                    if (childClass == "Static")
                    {
                        int textLength = NativeMethods.GetWindowText(childHwnd, childTextBuffer, childTextBuffer.Length);

                        if (textLength > 0)
                        {
                            string childText = new string(childTextBuffer, 0, textLength);
                            var currentList = GCHandle.FromIntPtr(lParam).Target as List<string>;
                            currentList?.Add(childText);
                        }
                    }
                }
                return true;
            }, GCHandle.ToIntPtr(listHandle));
        }
        finally
        {
            if (listHandle.IsAllocated) listHandle.Free();
            ArrayPool<char>.Shared.Return(childClassBuffer);
            ArrayPool<char>.Shared.Return(childTextBuffer);
        }

        return string.Join(" | ", textParts).Trim();
    }

    /// <summary>
    /// Evaluates extracted dialog text to classify whether the message represents an auto-dismissible warning.
    /// </summary>
    private static bool IsRecoverableWarning(string text)
    {
        var lowerText = text.ToLowerInvariant();
        return lowerText.Contains("warning") ||
               lowerText.Contains("tip") ||
               lowerText.Contains("overwrite") ||
               lowerText.Contains("success");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Dialog detected on thread {ThreadId}. Text: '{Text}'")]
    private partial void LogDialogDetected(int threadId, string text);

    [LoggerMessage(Level = LogLevel.Information, Message = "Classified as recoverable warning. Sending silent ENTER to dismiss.")]
    private partial void LogRecoverableWarning();

    [LoggerMessage(Level = LogLevel.Information, Message = "Classified as paused state. Leaving dialog open.")]
    private partial void LogPausedState();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Classified as CRITICAL blocking dialog.")]
    private partial void LogCriticalDialog();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Process {ProcessId} exited while checking for dialogs.")]
    private partial void LogProcessExited(int processId);
}