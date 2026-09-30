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
    FileOpen
}

/// <summary>A push button of a dialog.</summary>
/// <param name="Id">Control ID; used to press the button.</param>
/// <param name="Text">Caption as shown (mnemonic ampersands removed), in the UI language.</param>
public record VenusDialogButton(int Id, string Text, bool Enabled, bool IsDefault);

/// <summary>A check box or radio button of a dialog.</summary>
public record VenusDialogOption(int Id, string Text, bool Checked, bool Enabled);

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
    string Fingerprint);

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