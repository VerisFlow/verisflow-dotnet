// Copyright (c) VerisFlow. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace VerisFlow.VenusAuto.Core.Models;

/// <summary>
/// Represents configuration settings for Hamilton Venus application automation, including process names, executable paths, and UI control coordinates.
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
    /// Gets or sets the relative coordinate map for interactive UI elements in the Run Control window.
    /// </summary>
    public AppCoordinates RunControlUI { get; set; } = new();

    /// <summary>
    /// Gets or sets the relative coordinate map for interactive UI elements in the Method Editor window.
    /// </summary>
    public AppCoordinates MethodEditorUI { get; set; } = new();
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
    Error
}

/// <summary>
/// Represents an immutable snapshot of the Venus system runtime status.
/// </summary>
/// <param name="State">The current execution state.</param>
/// <param name="RawStatusText">The exact unparsed status message extracted from the UI control.</param>
/// <param name="HasErrorDialog">Indicates whether a blocking error dialog is actively present.</param>
/// <param name="ErrorMessage">The text message extracted from an active error dialog, if any.</param>
/// <param name="LoadedMethodName">The filename or identifier of the method currently loaded in memory.</param>
public record VenusSystemStatus(
    RunState State,
    string RawStatusText,
    bool HasErrorDialog,
    string? ErrorMessage,
    string? LoadedMethodName
);

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