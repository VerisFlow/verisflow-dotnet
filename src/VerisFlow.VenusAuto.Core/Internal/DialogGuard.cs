// Copyright (c) VerisFlow. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using VerisFlow.VenusAuto.Core.Models;

namespace VerisFlow.VenusAuto.Core.Internal;

/// <summary>
/// Reads Run Control dialogs. Never answers a dialog; callers decide.
/// </summary>
internal interface IDialogGuard
{
    /// <summary>Visible dialogs of the process, topmost first.</summary>
    IReadOnlyList<VenusDialogInfo> GetDialogs(int processId, IntPtr mainWindow);

    /// <summary>The dialog, or null when it is gone, not visible, not a dialog, or not owned by the process.</summary>
    VenusDialogInfo? Inspect(IntPtr dialog, int processId, IntPtr mainWindow);

    /// <summary>Waits for a dialog matching <paramref name="predicate"/>; null on timeout.</summary>
    Task<VenusDialogInfo?> WaitForDialogAsync(
        int processId,
        IntPtr mainWindow,
        Func<VenusDialogInfo, bool> predicate,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>Waits until the dialog is closed or hidden; false on timeout.</summary>
    Task<bool> WaitForCloseAsync(IntPtr dialog, TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>Clicks a button on a custom UIA dialog.</summary>
    bool ClickCustomButton(IntPtr dialog, int buttonId);

    /// <summary>Submits values and triggers a button on a dialog.</summary>
    bool SubmitDialog(IntPtr dialog, VenusDialogSubmission submission);
}

/// <inheritdoc />
internal sealed class DialogGuard : IDialogGuard
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(150);

    private const string StaticClass = "Static";
    private const string EditClass = "Edit";
    private const string ButtonClass = "Button";

    // Button types (style & BS_TYPEMASK).
    private const int BsDefPushButton = 0x1;
    private const int BsCheckBox = 0x2;
    private const int BsAutoCheckBox = 0x3;
    private const int BsRadioButton = 0x4;
    private const int Bs3State = 0x5;
    private const int BsAuto3State = 0x6;
    private const int BsGroupBox = 0x7;
    private const int BsAutoRadioButton = 0x9;
    private const int BsDefSplitButton = 0xD;
    private const int BsDefCommandLink = 0xF;

    private readonly IWindowMessenger _messenger;
    private readonly IUiaDialogDriver _uiaDriver;
    private readonly RunControlIdentifiers _ids;

    public DialogGuard(IWindowMessenger messenger, IUiaDialogDriver uiaDriver, IOptions<VenusAutoOptions> options)
    {
        _messenger = messenger;
        _uiaDriver = uiaDriver;
        _ids = options.Value.RunControlIds;
    }

    public IReadOnlyList<VenusDialogInfo> GetDialogs(int processId, IntPtr mainWindow)
    {
        var dialogs = new List<VenusDialogInfo>();

        foreach (var hwnd in _messenger.GetTopLevelWindows(processId))
        {
            if (!_messenger.IsWindow(hwnd) || !_messenger.IsWindowVisible(hwnd))
            {
                continue;
            }

            if (hwnd == mainWindow)
            {
                continue;
            }

            if (IsNativeDialog(hwnd))
            {
                dialogs.Add(CaptureNative(hwnd, mainWindow));
            }
            else if (_uiaDriver.IsCustomDialog(hwnd, processId, mainWindow))
            {
                var custom = _uiaDriver.CaptureCustomDialog(hwnd, mainWindow, processId);
                if (custom != null)
                {
                    dialogs.Add(custom);
                }
            }
        }

        return dialogs;
    }

    public VenusDialogInfo? Inspect(IntPtr dialog, int processId, IntPtr mainWindow)
    {
        if (!_messenger.IsWindow(dialog) || _messenger.GetProcessId(dialog) != processId || !_messenger.IsWindowVisible(dialog))
        {
            return null;
        }

        if (dialog == mainWindow)
        {
            return null;
        }

        if (IsNativeDialog(dialog))
        {
            return CaptureNative(dialog, mainWindow);
        }

        return _uiaDriver.CaptureCustomDialog(dialog, mainWindow, processId);
    }

    public async Task<VenusDialogInfo?> WaitForDialogAsync(
        int processId,
        IntPtr mainWindow,
        Func<VenusDialogInfo, bool> predicate,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (true)
        {
            var match = GetDialogs(processId, mainWindow).FirstOrDefault(predicate);
            if (match is not null)
            {
                return match;
            }

            if (DateTime.UtcNow >= deadline)
            {
                return null;
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<bool> WaitForCloseAsync(IntPtr dialog, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (_messenger.IsWindow(dialog) && _messenger.IsWindowVisible(dialog))
        {
            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    public bool ClickCustomButton(IntPtr dialog, int buttonId)
    {
        if (IsNativeDialog(dialog))
        {
            return _messenger.ClickButton(dialog, buttonId);
        }

        return _uiaDriver.ClickButton(dialog, buttonId);
    }

    public bool SubmitDialog(IntPtr dialog, VenusDialogSubmission submission)
    {
        if (submission == null) return false;

        if (IsNativeDialog(dialog))
        {
            if (submission.Inputs != null && submission.Inputs.Count > 0)
            {
                var edits = _messenger.GetDescendants(dialog)
                    .Where(c => string.Equals(_messenger.GetClassName(c), EditClass, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var edit in edits)
                {
                    var id = _messenger.GetControlId(edit).ToString(CultureInfo.InvariantCulture);
                    if (submission.Inputs.TryGetValue(id, out var val))
                    {
                        _messenger.SetText(edit, val);
                    }
                    else if (submission.Inputs.TryGetValue("0", out val) || submission.Inputs.TryGetValue("Edit_0", out val))
                    {
                        _messenger.SetText(edit, val);
                    }
                }
            }

            if (submission.ButtonId.HasValue)
            {
                return _messenger.ClickButton(dialog, submission.ButtonId.Value);
            }

            return true;
        }

        return _uiaDriver.Submit(dialog, submission);
    }

    private bool IsNativeDialog(IntPtr hwnd)
        => _messenger.IsWindowVisible(hwnd)
           && string.Equals(_messenger.GetClassName(hwnd), NativeMethods.DialogClassName, StringComparison.Ordinal);

    private VenusDialogInfo CaptureNative(IntPtr dialog, IntPtr mainWindow)
    {
        var title = _messenger.GetText(dialog).Trim();
        var owner = _messenger.GetOwner(dialog);
        var messageLines = new List<string>();
        var buttons = new List<VenusDialogButton>();
        var options = new List<VenusDialogOption>();
        var inputs = new List<VenusDialogInput>();

        foreach (var child in _messenger.GetDescendants(dialog))
        {
            if (!_messenger.IsWindowVisible(child))
            {
                continue;
            }

            var className = _messenger.GetClassName(child);
            var style = _messenger.GetStyle(child);

            if (string.Equals(className, StaticClass, StringComparison.OrdinalIgnoreCase))
            {
                AddLine(messageLines, _messenger.GetText(child));
            }
            else if (string.Equals(className, EditClass, StringComparison.OrdinalIgnoreCase))
            {
                // Read-only edits show text; editable ones are inputs.
                if ((style & NativeMethods.ES_READONLY) != 0)
                {
                    AddLine(messageLines, _messenger.GetText(child));
                }
                else
                {
                    var id = _messenger.GetControlId(child);
                    var text = _messenger.GetText(child);
                    inputs.Add(new VenusDialogInput(id.ToString(CultureInfo.InvariantCulture), string.Empty, text, false));
                }
            }
            else if (string.Equals(className, ButtonClass, StringComparison.OrdinalIgnoreCase))
            {
                var type = style & NativeMethods.BS_TYPEMASK;
                var id = _messenger.GetControlId(child);
                var text = RemoveMnemonics(_messenger.GetText(child));
                var enabled = _messenger.IsWindowEnabled(child);

                switch (type)
                {
                    case BsGroupBox:
                        break;

                    case BsCheckBox:
                    case BsAutoCheckBox:
                    case BsRadioButton:
                    case Bs3State:
                    case BsAuto3State:
                    case BsAutoRadioButton:
                        options.Add(new VenusDialogOption(id, text, _messenger.GetCheckState(child) == 1, enabled));
                        break;

                    default:
                        // Push, default push, owner-drawn, split, and command-link buttons.
                        var isDefault = type == BsDefPushButton || type == BsDefSplitButton || type == BsDefCommandLink;
                        buttons.Add(new VenusDialogButton(id, text, enabled, isDefault));
                        break;
                }
            }
        }

        var blocksMainWindow = mainWindow != IntPtr.Zero
            && (owner == mainWindow || !_messenger.IsWindowEnabled(mainWindow));

        return new VenusDialogInfo(
            dialog.ToInt64(),
            owner.ToInt64(),
            Classify(dialog, buttons),
            title,
            string.Join("\n", messageLines),
            buttons,
            options,
            blocksMainWindow,
            ComputeFingerprint(dialog, title, buttons.Select(b => b.Id).Concat(options.Select(o => o.Id))))
        {
            Inputs = inputs,
            IsCustom = false
        };
    }

    /// <summary>
    /// Recognizes a dialog by its button and control IDs, which do not depend on the UI language.
    /// </summary>
    private VenusDialogKind Classify(IntPtr dialog, IReadOnlyList<VenusDialogButton> buttons)
    {
        bool Has(int id) => id > 0 && buttons.Any(button => button.Id == id);

        if (Has(_ids.PausedResumeButton) && Has(_ids.PausedAbortButton))
        {
            return VenusDialogKind.Paused;
        }

        if (Has(_ids.AbortConfirmButton) && Has(_ids.AbortCancelButton))
        {
            return VenusDialogKind.AbortConfirmation;
        }

        if (_ids.FileNameControl > 0 && _messenger.FindDescendant(dialog, null, _ids.FileNameControl) != IntPtr.Zero)
        {
            return VenusDialogKind.FileOpen;
        }

        return VenusDialogKind.Unknown;
    }

    private static void AddLine(List<string> lines, string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length > 0)
        {
            lines.Add(trimmed);
        }
    }

    /// <summary>"&amp;Abort" becomes "Abort"; "&amp;&amp;" stays a literal ampersand.</summary>
    private static string RemoveMnemonics(string text)
    {
        var result = new StringBuilder(text.Length);

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '&')
            {
                if (i + 1 < text.Length && text[i + 1] == '&')
                {
                    result.Append('&');
                    i++;
                }

                continue;
            }

            result.Append(text[i]);
        }

        return result.ToString().Trim();
    }

    /// <summary>
    /// Handle, title, and control IDs; the message is left out because dialogs may update it (e.g. countdowns).
    /// The same 16 uppercase hex characters on every target framework.
    /// </summary>
    private static string ComputeFingerprint(IntPtr dialog, string title, IEnumerable<int> controlIds)
    {
        var source = dialog.ToInt64().ToString(CultureInfo.InvariantCulture)
            + "|" + title
            + "|" + string.Join(",", controlIds.OrderBy(id => id).Select(id => id.ToString(CultureInfo.InvariantCulture)));

        var bytes = Encoding.UTF8.GetBytes(source);

#if NET5_0_OR_GREATER
        return Convert.ToHexString(SHA256.HashData(bytes), 0, 8);
#else
        using (var sha = SHA256.Create())
        {
            var hash = sha.ComputeHash(bytes);
            return BitConverter.ToString(hash, 0, 8).Replace("-", string.Empty);
        }
#endif
    }
}
