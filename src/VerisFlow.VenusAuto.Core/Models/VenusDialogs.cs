// Copyright (c) VerisFlow. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;

namespace VerisFlow.VenusAuto.Core.Models;

/// <summary>
/// What a Run Control dialog is, recognized by its button and control IDs (independent of the UI language).
/// </summary>
public enum VenusDialogKind
{
    /// <summary>Not one of the dialogs below; read its text and buttons.</summary>
    Unknown,

    /// <summary>The "Execution paused" dialog.</summary>
    Paused,

    /// <summary>The confirmation asked before a run is aborted.</summary>
    AbortConfirmation,

    /// <summary>The common file Open dialog.</summary>
    FileOpen,

    /// <summary>A custom window rendered by UI framework without standard Win32 dialog class.</summary>
    Custom
}

/// <summary>A push button of a dialog.</summary>
/// <param name="Id">Control ID; used to press the button.</param>
/// <param name="Text">Caption as shown (mnemonic ampersands removed), in the UI language.</param>
public record VenusDialogButton(int Id, string Text, bool Enabled, bool IsDefault);

/// <summary>A check box or radio button of a dialog.</summary>
public record VenusDialogOption(int Id, string Text, bool Checked, bool Enabled);

/// <summary>An input control (edit box, path selector, or number input) of a dialog.</summary>
/// <param name="Key">Identifier key (AutomationId, control ID, or index-based identifier).</param>
/// <param name="Label">Associated label or description text, if available.</param>
/// <param name="Value">Current text or numeric value.</param>
/// <param name="IsReadOnly">True if the input is read-only.</param>
public record VenusDialogInput(string Key, string Label, string Value, bool IsReadOnly);

/// <summary>A dropdown selection control (combo box) of a dialog.</summary>
/// <param name="Key">Identifier key (AutomationId or index-based identifier).</param>
/// <param name="Label">Associated label or description text, if available.</param>
/// <param name="SelectedItem">Currently selected option text.</param>
/// <param name="Options">Available selectable option values.</param>
public record VenusDialogDropdown(string Key, string Label, string SelectedItem, IReadOnlyList<string> Options);

/// <summary>
/// Encapsulates responses to dialog inputs, options, dropdowns, and button actions.
/// </summary>
public class VenusDialogSubmission
{
    /// <summary>Gets or sets values to write to text/numeric inputs by input Key or Label.</summary>
    public IDictionary<string, string> Inputs { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets states to apply to check boxes or radio buttons by option ID or caption.</summary>
    public IDictionary<string, bool> Options { get; set; } = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets options to select in dropdowns by dropdown Key or Label.</summary>
    public IDictionary<string, string> Dropdowns { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets the ID of the button to click after applying inputs.</summary>
    public int? ButtonId { get; set; }

    /// <summary>Gets or sets the text or key of the button to click (fallback if ButtonId is null).</summary>
    public string? ButtonName { get; set; }
}

/// <summary>
/// A snapshot of an open Run Control dialog. Nothing in this library answers dialogs on its own; callers show this
/// information to a person and respond with <c>RespondToDialogAsync</c>.
/// </summary>
/// <param name="Handle">Window handle.</param>
/// <param name="OwnerHandle">Owner window handle (the main window, or another dialog for nested dialogs); 0 if none.</param>
/// <param name="Kind">Recognized kind.</param>
/// <param name="Title">Window title.</param>
/// <param name="Message">Text of the dialog's static and read-only text controls, one per line.</param>
/// <param name="Buttons">Push buttons, in window order.</param>
/// <param name="Options">Check boxes and radio buttons, in window order.</param>
/// <param name="BlocksMainWindow">True when the dialog is modal for the main window (Run Control waits for it).</param>
/// <param name="Fingerprint">Identifies this dialog instance; a response is only accepted with a matching fingerprint.</param>
public record VenusDialogInfo(
    long Handle,
    long OwnerHandle,
    VenusDialogKind Kind,
    string Title,
    string Message,
    IReadOnlyList<VenusDialogButton> Buttons,
    IReadOnlyList<VenusDialogOption> Options,
    bool BlocksMainWindow,
    string Fingerprint)
{
    /// <summary>Input controls (edit boxes, number fields, file paths) in window order.</summary>
    public IReadOnlyList<VenusDialogInput> Inputs { get; init; } = Array.Empty<VenusDialogInput>();

    /// <summary>Dropdown controls (combo boxes) in window order.</summary>
    public IReadOnlyList<VenusDialogDropdown> Dropdowns { get; init; } = Array.Empty<VenusDialogDropdown>();

    /// <summary>Indicates whether this dialog is a customized UI-Automation driven window.</summary>
    public bool IsCustom { get; init; }
}

/// <summary>Outcome of loading a method.</summary>
/// <param name="Loaded">True when Run Control shows the method in its title.</param>
/// <param name="LoadedMethodName">Method name from the window title, if any.</param>
/// <param name="PendingDialogs">Dialogs that appeared and were not answered; a person decides how to respond.</param>
/// <param name="Detail">Explanation of the outcome.</param>
public record VenusLoadResult(bool Loaded, string? LoadedMethodName, IReadOnlyList<VenusDialogInfo> PendingDialogs, string Detail);

/// <summary>Outcome of an abort request.</summary>
/// <param name="Aborted">True when the abort was confirmed.</param>
/// <param name="PendingConfirmation">The open confirmation dialog when the abort was not confirmed.</param>
/// <param name="Detail">Explanation of the outcome.</param>
public record VenusAbortResult(bool Aborted, VenusDialogInfo? PendingConfirmation, string Detail);

/// <summary>Outcome of pressing a dialog button.</summary>
/// <param name="DialogClosed">True when the dialog closed after the button was pressed.</param>
/// <param name="OpenDialogs">Dialogs open afterwards, e.g. one opened by the button.</param>
public record VenusDialogResponse(bool DialogClosed, IReadOnlyList<VenusDialogInfo> OpenDialogs);

/// <summary>
/// Thrown when an operation cannot run because a dialog waits for a person to respond.
/// </summary>
public class VenusDialogPendingException : InvalidOperationException
{
    public VenusDialogPendingException(string message, IReadOnlyList<VenusDialogInfo> dialogs)
        : base(message)
    {
        Dialogs = dialogs;
    }

    /// <summary>The dialogs that need a response.</summary>
    public IReadOnlyList<VenusDialogInfo> Dialogs { get; }
}
