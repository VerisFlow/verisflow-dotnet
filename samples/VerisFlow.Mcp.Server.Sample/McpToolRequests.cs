using System;

namespace VerisFlow.Mcp.Server.Sample;

/// <summary>
/// Payload for configuring and positioning the Hamilton software window layout.
/// </summary>
public class ArrangeVenusWindowRequest
{
    /// <summary>
    /// Predefined window layout position (e.g., "Center", "Fill", "Custom").
    /// </summary>
    public string Preset { get; set; } = "Center";

    /// <summary>
    /// Target X coordinate applied when using custom window placement.
    /// </summary>
    public int CustomX { get; set; }

    /// <summary>
    /// Target Y coordinate applied when using custom window placement.
    /// </summary>
    public int CustomY { get; set; }

    /// <summary>
    /// Target window width applied when using custom window placement.
    /// </summary>
    public int CustomWidth { get; set; }

    /// <summary>
    /// Target window height applied when using custom window placement.
    /// </summary>
    public int CustomHeight { get; set; }
}

/// <summary>
/// Data Transfer Object representing the JSON payload sent by ChatGPT to search for files.
/// </summary>
public class ListTracesRequest
{
    /// <summary>
    /// Optional. Defaults to C:\Program Files (x86)\HAMILTON\LogFiles if left null or empty.
    /// </summary>
    public string? DirectoryPath { get; set; }

    /// <summary>
    /// Supported values: latest, today, this_week, this_month, custom, all
    /// </summary>
    public string TimeFilter { get; set; } = "all";

    /// <summary>
    /// Only required if TimeFilter is 'custom'. Format: yyyy-MM-ddTHH:mm:ss
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// Only required if TimeFilter is 'custom'. Format: yyyy-MM-ddTHH:mm:ss
    /// </summary>
    public DateTime? EndTime { get; set; }
}

/// <summary>
/// Payload for loading a specific Hamilton method file into the workspace.
/// </summary>
public class LoadVenusMethodRequest
{
    /// <summary>
    /// Absolute file path to the target Hamilton method file.
    /// </summary>
    public required string MethodPath { get; set; }
}

/// <summary>
/// Data Transfer Object representing the JSON payload sent by ChatGPT.
/// </summary>
public class ParseTraceRequest
{
    /// <summary>
    /// Absolute file path to the target trace file to be parsed.
    /// </summary>
    public required string FilePath { get; set; }
}

// Defines the request payload for scanning Hamilton methods.
// The DirectoryPath is optional; if null, default configurations are used.
public record ScanVenusMethodsRequest(string? DirectoryPath);