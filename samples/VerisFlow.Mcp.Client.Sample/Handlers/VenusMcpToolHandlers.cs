using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using VerisFlow.VenusAuto.Core.Contracts;
using VerisFlow.VenusAuto.Core.Models;

namespace VerisFlow.Mcp.Client.Sample;

/// <summary>
/// Scans specified or configured directories to discover complete Hamilton method file packages.
/// </summary>
public class HamiltonScanMethodsHandler : McpToolHandlerBase
{
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="HamiltonScanMethodsHandler"/> class.
    /// </summary>
    /// <param name="configuration">Application configuration instance to read default scan paths.</param>
    public HamiltonScanMethodsHandler(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <inheritdoc />
    public override string Name => "hamilton_scan_methods";

    /// <summary>
    /// Scans target directories and groups files to ensure required companion files are present before returning valid method definitions.
    /// </summary>
    /// <param name="arguments">JSON arguments containing optional directoryPath parameter.</param>
    /// <returns>A JSON response listing validated Hamilton method files.</returns>
    protected override Task<(string ResultJson, bool IsError)> ExecuteCoreAsync(JsonElement arguments)
    {
        string? targetDirectory = arguments.TryGetProperty("directoryPath", out var dirElement) ? dirElement.GetString() : null;
        string[] directories;

        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            directories = new[] { targetDirectory };
        }
        else
        {
            directories = _configuration.GetSection("VenusAutomation:MethodScanDirectories")
                                            .GetChildren()
                                            .Select(c => c.Value)
                                            .OfType<string>()
                                            .ToArray();
        }

        var validMethods = new List<object>();

        foreach (var dirPath in directories)
        {
            if (!Directory.Exists(dirPath)) continue;

            var allFiles = Directory.GetFiles(dirPath, "*.*", SearchOption.AllDirectories);

            // Group files by full path omitting extension to evaluate method dependencies
            var groupedFiles = allFiles.GroupBy(f =>
            {
                string directory = Path.GetDirectoryName(f) ?? string.Empty;
                string name = Path.GetFileNameWithoutExtension(f);
                return Path.Combine(directory, name);
            }, StringComparer.OrdinalIgnoreCase);

            foreach (var group in groupedFiles)
            {
                var extensions = group.Select(f => Path.GetExtension(f).ToLowerInvariant()).ToHashSet();

                string fullPathWithoutExtension = group.Key;
                string actualDirectory = Path.GetDirectoryName(fullPathWithoutExtension) ?? string.Empty;
                string baseName = Path.GetFileName(fullPathWithoutExtension);

                // Check for standard MED method package (.med, .stp, .hsl, .sub)
                if (extensions.Contains(".med") && extensions.Contains(".stp") && extensions.Contains(".hsl") && extensions.Contains(".sub"))
                {
                    validMethods.Add(new
                    {
                        MethodName = baseName,
                        MethodType = "MED",
                        Directory = actualDirectory,
                        FullPath = fullPathWithoutExtension + ".med"
                    });
                }
                // Check for standalone HSL method package (.hsl, .sub)
                else if (extensions.Contains(".hsl") && extensions.Contains(".sub"))
                {
                    validMethods.Add(new
                    {
                        MethodName = baseName,
                        MethodType = "HSL",
                        Directory = actualDirectory,
                        FullPath = fullPathWithoutExtension + ".hsl"
                    });
                }
            }
        }

        var response = new
        {
            status = "success",
            scannedDirectories = directories.Length,
            methodsFound = validMethods.Count,
            methods = validMethods
        };

        return Task.FromResult((JsonSerializer.Serialize(response), false));
    }
}

/// <summary>
/// Handler for verifying and ensuring that the Hamilton Venus process is running.
/// </summary>
public class VenusEnsureStartedHandler : McpToolHandlerBase
{
    private readonly IVenusRunControlService _venusService;

    /// <summary>
    /// Initializes a new instance of the <see cref="VenusEnsureStartedHandler"/> class.
    /// </summary>
    /// <param name="venusService">The Venus run control automation service.</param>
    public VenusEnsureStartedHandler(IVenusRunControlService venusService)
    {
        _venusService = venusService;
    }

    /// <inheritdoc />
    public override string Name => "hamilton_ensure_started";

    /// <summary>
    /// Initiates process verification for the Hamilton software stack.
    /// </summary>
    /// <param name="arguments">JSON arguments passed to the tool.</param>
    /// <returns>A JSON status response confirming process verification.</returns>
    protected override async Task<(string ResultJson, bool IsError)> ExecuteCoreAsync(JsonElement arguments)
    {
        await _venusService.EnsureProcessStartedAsync();
        return (JsonSerializer.Serialize(new { status = "success", message = "Hamilton process verified." }), false);
    }
}

/// <summary>
/// Handler for rearranging the window layout of the Hamilton Venus application.
/// </summary>
public class VenusArrangeWindowHandler : McpToolHandlerBase
{
    private readonly IVenusRunControlService _venusService;

    /// <summary>
    /// Initializes a new instance of the <see cref="VenusArrangeWindowHandler"/> class.
    /// </summary>
    /// <param name="venusService">The Venus run control automation service.</param>
    public VenusArrangeWindowHandler(IVenusRunControlService venusService)
    {
        _venusService = venusService;
    }

    /// <inheritdoc />
    public override string Name => "hamilton_arrange_window";

    /// <summary>
    /// Repositions and resizes the Hamilton window based on preset configurations or custom coordinates.
    /// </summary>
    /// <param name="arguments">JSON arguments containing layout preset and coordinate values (customX, customY, customWidth, customHeight).</param>
    /// <returns>A JSON response confirming layout application.</returns>
    protected override async Task<(string ResultJson, bool IsError)> ExecuteCoreAsync(JsonElement arguments)
    {
        string presetString = arguments.TryGetProperty("preset", out var presetEl) ? presetEl.GetString() ?? "Center" : "Center";
        if (!Enum.TryParse<WindowLayoutPreset>(presetString, true, out var preset))
        {
            preset = WindowLayoutPreset.Center;
        }

        int x = arguments.TryGetProperty("customX", out var xEl) ? xEl.GetInt32() : 0;
        int y = arguments.TryGetProperty("customY", out var yEl) ? yEl.GetInt32() : 0;
        int width = arguments.TryGetProperty("customWidth", out var wEl) ? wEl.GetInt32() : 0;
        int height = arguments.TryGetProperty("customHeight", out var hEl) ? hEl.GetInt32() : 0;

        await _venusService.ArrangeWindowAsync(preset, x, y, width, height);
        return (JsonSerializer.Serialize(new { status = "success", layout = preset.ToString() }), false);
    }
}

/// <summary>
/// Handler for retrieving current runtime status and error indicators from Hamilton Venus.
/// </summary>
public class VenusGetStatusHandler : McpToolHandlerBase
{
    private readonly IVenusRunControlService _venusService;

    /// <summary>
    /// Initializes a new instance of the <see cref="VenusGetStatusHandler"/> class.
    /// </summary>
    /// <param name="venusService">The Venus run control automation service.</param>
    public VenusGetStatusHandler(IVenusRunControlService venusService)
    {
        _venusService = venusService;
    }

    /// <inheritdoc />
    public override string Name => "hamilton_get_status";

    /// <summary>
    /// Queries current run state, dialog prompts, and error information from the Hamilton software.
    /// </summary>
    /// <param name="arguments">JSON arguments passed to the tool.</param>
    /// <returns>A JSON response containing full status parameters.</returns>
    protected override async Task<(string ResultJson, bool IsError)> ExecuteCoreAsync(JsonElement arguments)
    {
        var status = await _venusService.GetStatusAsync();

        var response = new
        {
            state = status.State.ToString(),
            rawStatusText = status.RawStatusText,
            hasErrorDialog = status.HasErrorDialog,
            errorMessage = status.ErrorMessage,
            loadedMethodName = status.LoadedMethodName
        };

        return (JsonSerializer.Serialize(response), false);
    }
}

/// <summary>
/// Handler for triggering method execution start sequence in Hamilton Venus.
/// </summary>
public class VenusStartRunHandler : McpToolHandlerBase
{
    private readonly IVenusRunControlService _venusService;

    /// <summary>
    /// Initializes a new instance of the <see cref="VenusStartRunHandler"/> class.
    /// </summary>
    /// <param name="venusService">The Venus run control automation service.</param>
    public VenusStartRunHandler(IVenusRunControlService venusService)
    {
        _venusService = venusService;
    }

    /// <inheritdoc />
    public override string Name => "hamilton_start_run";

    /// <summary>
    /// Initiates the run sequence in the underlying Hamilton control system.
    /// </summary>
    /// <param name="arguments">JSON arguments passed to the tool.</param>
    /// <returns>A JSON response confirming command invocation.</returns>
    protected override async Task<(string ResultJson, bool IsError)> ExecuteCoreAsync(JsonElement arguments)
    {
        await _venusService.StartRunAsync();
        return (JsonSerializer.Serialize(new { status = "success", message = "Start sequence initiated." }), false);
    }
}

/// <summary>
/// Handler for initiating pause sequence in an ongoing Hamilton run.
/// </summary>
public class VenusPauseRunHandler : McpToolHandlerBase
{
    private readonly IVenusRunControlService _venusService;

    /// <summary>
    /// Initializes a new instance of the <see cref="VenusPauseRunHandler"/> class.
    /// </summary>
    /// <param name="venusService">The Venus run control automation service.</param>
    public VenusPauseRunHandler(IVenusRunControlService venusService)
    {
        _venusService = venusService;
    }

    /// <inheritdoc />
    public override string Name => "hamilton_pause_run";

    /// <summary>
    /// Sends a pause command to halt execution safely.
    /// </summary>
    /// <param name="arguments">JSON arguments passed to the tool.</param>
    /// <returns>A JSON response confirming command invocation.</returns>
    protected override async Task<(string ResultJson, bool IsError)> ExecuteCoreAsync(JsonElement arguments)
    {
        await _venusService.PauseRunAsync();
        return (JsonSerializer.Serialize(new { status = "success", message = "Pause sequence initiated." }), false);
    }
}

/// <summary>
/// Handler for resuming a paused Hamilton execution.
/// </summary>
public class VenusResumeRunHandler : McpToolHandlerBase
{
    private readonly IVenusRunControlService _venusService;

    /// <summary>
    /// Initializes a new instance of the <see cref="VenusResumeRunHandler"/> class.
    /// </summary>
    /// <param name="venusService">The Venus run control automation service.</param>
    public VenusResumeRunHandler(IVenusRunControlService venusService)
    {
        _venusService = venusService;
    }

    /// <inheritdoc />
    public override string Name => "hamilton_resume_run";

    /// <summary>
    /// Sends a resume command to continue execution.
    /// </summary>
    /// <param name="arguments">JSON arguments passed to the tool.</param>
    /// <returns>A JSON response confirming command invocation.</returns>
    protected override async Task<(string ResultJson, bool IsError)> ExecuteCoreAsync(JsonElement arguments)
    {
        await _venusService.ResumeRunAsync();
        return (JsonSerializer.Serialize(new { status = "success", message = "Resume sequence initiated." }), false);
    }
}

/// <summary>
/// Handler for aborting active method execution in Hamilton Venus.
/// </summary>
public class VenusAbortRunHandler : McpToolHandlerBase
{
    private readonly IVenusRunControlService _venusService;

    /// <summary>
    /// Initializes a new instance of the <see cref="VenusAbortRunHandler"/> class.
    /// </summary>
    /// <param name="venusService">The Venus run control automation service.</param>
    public VenusAbortRunHandler(IVenusRunControlService venusService)
    {
        _venusService = venusService;
    }

    /// <inheritdoc />
    public override string Name => "hamilton_abort_run";

    /// <summary>
    /// Sends an abort command to immediately stop the current method execution.
    /// </summary>
    /// <param name="arguments">JSON arguments passed to the tool.</param>
    /// <returns>A JSON response confirming command invocation.</returns>
    protected override async Task<(string ResultJson, bool IsError)> ExecuteCoreAsync(JsonElement arguments)
    {
        await _venusService.AbortRunAsync();
        return (JsonSerializer.Serialize(new { status = "success", message = "Abort sequence initiated." }), false);
    }
}

/// <summary>
/// Handler for loading a specified method file into Hamilton Venus.
/// </summary>
public class VenusLoadMethodHandler : McpToolHandlerBase
{
    private readonly IVenusRunControlService _venusService;

    /// <summary>
    /// Initializes a new instance of the <see cref="VenusLoadMethodHandler"/> class.
    /// </summary>
    /// <param name="venusService">The Venus run control automation service.</param>
    public VenusLoadMethodHandler(IVenusRunControlService venusService)
    {
        _venusService = venusService;
    }

    /// <inheritdoc />
    public override string Name => "hamilton_load_method";

    /// <summary>
    /// Loads a method file into Venus using the path provided in arguments.
    /// </summary>
    /// <param name="arguments">JSON arguments containing the methodPath property.</param>
    /// <returns>A JSON response confirming the method was loaded.</returns>
    /// <exception cref="ArgumentException">Thrown when methodPath parameter is missing or empty.</exception>
    protected override async Task<(string ResultJson, bool IsError)> ExecuteCoreAsync(JsonElement arguments)
    {
        var methodPath = arguments.GetProperty("methodPath").GetString() ?? throw new ArgumentException("Method path missing.");
        await _venusService.LoadMethodAsync(methodPath);
        return (JsonSerializer.Serialize(new { status = "success", message = $"Method loaded: {methodPath}" }), false);
    }
}

/// <summary>
/// Handler for executing a graceful shutdown sequence for the Hamilton software suite.
/// </summary>
public class VenusGracefulShutdownHandler : McpToolHandlerBase
{
    private readonly IVenusRunControlService _venusService;

    /// <summary>
    /// Initializes a new instance of the <see cref="VenusGracefulShutdownHandler"/> class.
    /// </summary>
    /// <param name="venusService">The Venus run control automation service.</param>
    public VenusGracefulShutdownHandler(IVenusRunControlService venusService)
    {
        _venusService = venusService;
    }

    /// <inheritdoc />
    public override string Name => "hamilton_graceful_shutdown";

    /// <summary>
    /// Initiates a safe, graceful shutdown of the Venus control application.
    /// </summary>
    /// <param name="arguments">JSON arguments passed to the tool.</param>
    /// <returns>A JSON response confirming shutdown initiation.</returns>
    protected override async Task<(string ResultJson, bool IsError)> ExecuteCoreAsync(JsonElement arguments)
    {
        await _venusService.GracefulShutdownAsync();
        return (JsonSerializer.Serialize(new { status = "success", message = "Shutdown sequence executed." }), false);
    }
}