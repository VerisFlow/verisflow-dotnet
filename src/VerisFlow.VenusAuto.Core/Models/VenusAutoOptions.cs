// Copyright (c) VerisFlow. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;

namespace VerisFlow.VenusAuto.Core.Models;

/// <summary>
/// Represents configuration settings for Hamilton Venus application automation, including process names, executable paths,
/// command and control identifiers, and (as a fallback) UI control coordinates.
/// </summary>
public class VenusAutoOptions
{
    /// <summary>
    /// Default configuration section key used when binding options from configuration providers.
    /// </summary>
    public const string SectionName = "VenusAutomation";

    /// <summary>
    /// Gets or sets the target process name for the Venus Run Control executable (without extension). Defaults to <c>HxRun</c>.
    /// </summary>
    public string RunControlProcessName { get; set; } = "HxRun";

    /// <summary>
    /// Gets or sets the target process name for the Venus Method Editor executable (without extension). Defaults to <c>HxHSLMetEd</c>.
    /// </summary>
    public string MethodEditorProcessName { get; set; } = "HxHSLMetEd";

    /// <summary>
    /// Gets or sets the full filesystem path to the Run Control executable binary.
    /// </summary>
    public string RunControlExecutablePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the command and control identifiers of Run Control. They do not depend on the UI language, DPI, or
    /// window position and are preferred over <see cref="RunControlUI"/> coordinates. An identifier of 0 disables it.
    /// </summary>
    public RunControlIdentifiers RunControlIds { get; set; } = new();

    /// <summary>
    /// Gets or sets the relative coordinate map for interactive UI elements in the Run Control window.
    /// Used only when the corresponding identifier in <see cref="RunControlIds"/> is 0.
    /// </summary>
    public AppCoordinates RunControlUI { get; set; } = new();

    /// <summary>
    /// Gets or sets the relative coordinate map for interactive UI elements in the Method Editor window.
    /// </summary>
    public AppCoordinates MethodEditorUI { get; set; } = new();
}

/// <summary>
/// Menu and toolbar command IDs (sent as WM_COMMAND) and control IDs of Run Control and its dialogs.
/// Defaults were measured on Venus Run Control; an identifier of 0 disables it.
/// </summary>
public class RunControlIdentifiers
{
    /// <summary>File &gt; Open (MFC ID_FILE_OPEN).</summary>
    public int OpenFileCommand { get; set; } = 0xE101;

    /// <summary>Toolbar Start.</summary>
    public int StartCommand { get; set; } = 32795;

    /// <summary>Toolbar Pause.</summary>
    public int PauseCommand { get; set; } = 32796;

    /// <summary>Toolbar Single Step.</summary>
    public int SingleStepCommand { get; set; } = 32797;

    /// <summary>Toolbar Abort.</summary>
    public int AbortCommand { get; set; } = 32798;

    /// <summary>Static control in the main window showing the run status text.</summary>
    public int StatusControl { get; set; } = 0x8021;

    /// <summary>File name box of the common Open dialog (cmb13).</summary>
    public int FileNameControl { get; set; } = 0x47C;

    /// <summary>Open button of the file dialog (IDOK).</summary>
    public int FileDialogOkButton { get; set; } = 1;

    /// <summary>Cancel button of the file dialog (IDCANCEL).</summary>
    public int FileDialogCancelButton { get; set; } = 2;

    /// <summary>Resume button of the "Execution paused" dialog.</summary>
    public int PausedResumeButton { get; set; } = 217;

    /// <summary>Abort button of the "Execution paused" dialog.</summary>
    public int PausedAbortButton { get; set; } = 211;

    /// <summary>Confirm button of the abort confirmation dialog.</summary>
    public int AbortConfirmButton { get; set; } = 220;

    /// <summary>Cancel button of the abort confirmation dialog (IDCANCEL).</summary>
    public int AbortCancelButton { get; set; } = 2;
}

/// <summary>
/// Encapsulates relative pixel coordinates for interactive control elements within an application window.
/// </summary>
public class AppCoordinates
{
    /// <summary>Gets or sets the relative coordinates for the Start execution button.</summary>
    public RelativePoint StartButton { get; set; } = new();

    /// <summary>Gets or sets the relative coordinates for the Pause execution button.</summary>
    public RelativePoint PauseButton { get; set; } = new();

    /// <summary>Gets or sets the relative coordinates for the Abort execution button.</summary>
    public RelativePoint AbortButton { get; set; } = new();

    /// <summary>Gets or sets the relative coordinates for the status readout control area.</summary>
    public RelativePoint StatusWindow { get; set; } = new();

    /// <summary>Gets or sets the relative coordinates for the Save method button.</summary>
    public RelativePoint SaveButton { get; set; } = new();

    /// <summary>Gets or sets the relative coordinates for the Validate method button.</summary>
    public RelativePoint ValidateButton { get; set; } = new();

    /// <summary>Gets or sets the relative coordinates for the Run method button.</summary>
    public RelativePoint RunButton { get; set; } = new();
}

/// <summary>
/// Represents a two-dimensional pixel offset relative to the upper-left corner of a target window client area.
/// </summary>
public class RelativePoint
{
    /// <summary>Gets or sets the horizontal pixel offset.</summary>
    public int X { get; set; }

    /// <summary>Gets or sets the vertical pixel offset.</summary>
    public int Y { get; set; }
}

/// <summary>
/// Represents operational status states for the automated Venus run engine.
/// </summary>
public enum RunState
{
    /// <summary>The state is uninitialized or cannot be determined.</summary>
    Unknown,

    /// <summary>The execution engine is idle and ready to receive instructions.</summary>
    Idle,

    /// <summary>A method is currently executing.</summary>
    Running,

    /// <summary>The engine is busy processing hardware or system initialization tasks.</summary>
    Busy,

    /// <summary>Execution has been temporarily paused.</summary>
    Paused,

    /// <summary>Execution encountered an error or was stopped by a critical exception.</summary>
    Error,

    /// <summary>A dialog blocks Run Control and waits for a person to respond. See <see cref="VenusSystemStatus.Dialogs"/>.</summary>
    WaitingForUser
}

/// <summary>
/// Which toolbar commands Run Control currently allows. Null when the state could not be read.
/// </summary>
public record VenusCommandState(bool? CanStart, bool? CanPause, bool? CanSingleStep, bool? CanAbort);

/// <summary>
/// Represents an immutable snapshot of the Venus system runtime status.
/// </summary>
/// <param name="State">The current execution state.</param>
/// <param name="RawStatusText">The status text shown by Run Control, unparsed (language dependent).</param>
/// <param name="HasErrorDialog">Indicates whether a dialog other than the pause dialog blocks Run Control.</param>
/// <param name="ErrorMessage">The text of that dialog, if any.</param>
/// <param name="LoadedMethodName">The filename or identifier of the method currently loaded in memory.</param>
public record VenusSystemStatus(
    RunState State,
    string RawStatusText,
    bool HasErrorDialog,
    string? ErrorMessage,
    string? LoadedMethodName
)
{
    /// <summary>All open Run Control dialogs, topmost first.</summary>
    public IReadOnlyList<VenusDialogInfo> Dialogs { get; init; } = Array.Empty<VenusDialogInfo>();

    /// <summary>Toolbar command availability, or null when it could not be read.</summary>
    public VenusCommandState? Commands { get; init; }
}

/// <summary>
/// Defines display presets for positioning and sizing process windows on screen.
/// </summary>
public enum WindowLayoutPreset
{
    /// <summary>Use custom pixel coordinates and dimensions supplied by the caller.</summary>
    Custom,

    /// <summary>Maximize the window to fill the active monitor.</summary>
    Maximize,

    /// <summary>Snap the window to the left half of the active monitor work area.</summary>
    LeftHalf,

    /// <summary>Snap the window to the right half of the active monitor work area.</summary>
    RightHalf,

    /// <summary>Center the window on the active monitor using standard screen proportions.</summary>
    Center
}