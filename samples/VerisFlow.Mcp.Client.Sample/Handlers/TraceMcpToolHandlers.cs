using Microsoft.Extensions.Options;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using TraceLogic.Core.Enums;
using TraceLogic.Core.Interfaces;
using TraceLogic.Core.Options;

namespace VerisFlow.Mcp.Client.Sample;

/// <summary>
/// Handler for parsing trace logs to extract high-level liquid transfer summary events.
/// </summary>
public class ParseTraceSummaryHandler : McpToolHandlerBase
{
    private readonly ITraceFileParser _traceParser;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParseTraceSummaryHandler"/> class.
    /// </summary>
    /// <param name="traceParser">The trace file parsing service.</param>
    public ParseTraceSummaryHandler(ITraceFileParser traceParser)
    {
        _traceParser = traceParser;
    }

    /// <inheritdoc />
    public override string Name => "parse_trace_summary";

    /// <summary>
    /// Parses the trace file specified in the arguments and returns liquid transfer summary data.
    /// </summary>
    /// <param name="arguments">JSON arguments containing the target file path.</param>
    /// <returns>A JSON response summarizing liquid transfers or reporting execution errors.</returns>
    protected override Task<(string ResultJson, bool IsError)> ExecuteCoreAsync(JsonElement arguments)
    {
        var filePath = arguments.TryGetProperty("filePath", out var el) ? el.GetString() ?? string.Empty : string.Empty;
        var analysisResult = _traceParser.Parse(filePath);

        if (analysisResult.Errors.Count > 0)
        {
            var errorResponse = new { error = "Parsing completed with errors", details = analysisResult.Errors };
            return Task.FromResult((JsonSerializer.Serialize(errorResponse), true));
        }

        var summary = new
        {
            FileName = analysisResult.FileName,
            TotalTransfers = analysisResult.LiquidTransfers.Count,
            Transfers = analysisResult.LiquidTransfers
        };
        return Task.FromResult((JsonSerializer.Serialize(summary), false));
    }
}

/// <summary>
/// Handler for parsing trace logs to extract granular pipetting steps and hardware actions.
/// </summary>
public class ParseTraceDetailsHandler : McpToolHandlerBase
{
    private readonly ITraceFileParser _traceParser;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParseTraceDetailsHandler"/> class.
    /// </summary>
    /// <param name="traceParser">The trace file parsing service.</param>
    public ParseTraceDetailsHandler(ITraceFileParser traceParser)
    {
        _traceParser = traceParser;
    }

    /// <inheritdoc />
    public override string Name => "parse_trace_details";

    /// <summary>
    /// Parses the trace file specified in the arguments and returns detailed pipetting steps.
    /// </summary>
    /// <param name="arguments">JSON arguments containing the target file path.</param>
    /// <returns>A JSON response detailing individual pipetting steps or reporting execution errors.</returns>
    protected override Task<(string ResultJson, bool IsError)> ExecuteCoreAsync(JsonElement arguments)
    {
        var filePath = arguments.TryGetProperty("filePath", out var el) ? el.GetString() ?? string.Empty : string.Empty;
        var analysisResult = _traceParser.Parse(filePath);

        if (analysisResult.Errors.Count > 0)
        {
            var errorResponse = new { error = "Parsing completed with errors", details = analysisResult.Errors };
            return Task.FromResult((JsonSerializer.Serialize(errorResponse), true));
        }

        var details = new
        {
            FileName = analysisResult.FileName,
            TotalSteps = analysisResult.PipettingSteps.Count,
            Steps = analysisResult.PipettingSteps
        };
        return Task.FromResult((JsonSerializer.Serialize(details), false));
    }
}

/// <summary>
/// Handler for locating trace log files based on directory paths and time filtering parameters.
/// </summary>
public class ListTraceFilesHandler : McpToolHandlerBase
{
    private readonly ITraceLocator _traceLocator;
    private readonly TraceLocatorOptions _traceLocatorOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListTraceFilesHandler"/> class.
    /// </summary>
    /// <param name="traceLocator">The trace locator service.</param>
    /// <param name="traceLocatorOptions">Options providing default directory and locator configurations.</param>
    public ListTraceFilesHandler(ITraceLocator traceLocator, IOptions<TraceLocatorOptions> traceLocatorOptions)
    {
        _traceLocator = traceLocator;
        _traceLocatorOptions = traceLocatorOptions.Value;
    }

    /// <inheritdoc />
    public override string Name => "list_trace_files";

    /// <summary>
    /// Locates matching trace log files using provided filters such as time range and directory path.
    /// </summary>
    /// <param name="arguments">JSON arguments containing search criteria including directoryPath, timeFilter, startTime, and endTime.</param>
    /// <returns>A JSON response listing all discovered trace files.</returns>
    protected override Task<(string ResultJson, bool IsError)> ExecuteCoreAsync(JsonElement arguments)
    {
        string? providedDirectory = arguments.TryGetProperty("directoryPath", out var dirElement) ? dirElement.GetString() : null;

        string filterString = arguments.TryGetProperty("timeFilter", out var filterElement) ? filterElement.GetString() ?? "all" : "all";
        if (!Enum.TryParse<TimeFilterType>(filterString, true, out var timeFilterType))
        {
            timeFilterType = TimeFilterType.All;
        }

        DateTime? startTime = arguments.TryGetProperty("startTime", out var startEl) && DateTime.TryParse(startEl.GetString(), out var s) ? s : null;
        DateTime? endTime = arguments.TryGetProperty("endTime", out var endEl) && DateTime.TryParse(endEl.GetString(), out var e) ? e : null;

        var files = _traceLocator.FindFiles(timeFilterType, providedDirectory, startTime, endTime);

        var response = new
        {
            TargetDirectory = string.IsNullOrWhiteSpace(providedDirectory) ? _traceLocatorOptions.DefaultLogDirectory : providedDirectory,
            FilterApplied = timeFilterType.ToString(),
            FilesFound = files.Count,
            Files = files
        };

        return Task.FromResult((JsonSerializer.Serialize(response), false));
    }
}