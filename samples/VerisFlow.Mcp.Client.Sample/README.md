# VerisFlow.Mcp.Client.Sample

**VerisFlow.Mcp.Client.Sample is a WPF desktop agent application that connects to the VerisFlow Cloud Relay. It executes Model Context Protocol (MCP) commands locally to control Hamilton Venus automation hardware and analyze liquid handling trace logs.**

Target Frameworks: `.NET 8.0` | `.NET 9.0` | Windows Presentation Foundation (WPF)

![System Architecture](assets/mcp-flowchart.png)

---

## Overview

The WPF Client Agent operates within the local lab execution environment. It maintains an outbound WebSocket connection to the Cloud Relay Server via SignalR. When an AI assistant issues commands through the cloud gateway, this client receives execution requests, dispatches them to appropriate local tool handlers (such as hardware drivers or trace file parsers), and returns serialized execution results back to the cloud.

---

## Key Features

### MSAL Azure AD Interactive & Silent Authentication
Integrates Microsoft Authentication Library (MSAL) to handle token acquisition. Supports silent token refreshing from cache with automatic fallback to interactive browser prompts.

### Dynamic Environment Switching
Includes a custom WPF environment toggle switch to alternate between **Development** and **Production** relay endpoints seamlessly without requiring application restarts.

### Comprehensive Local Tool Handler Suite
Extends `McpToolHandlerBase` to provide concrete local handlers for:
* **Trace File Parsing**: Locating, parsing summaries, and extracting granular pipetting steps from `.trc` log files.
* **Hamilton Venus Automation**: Verifying process state, adjusting window layouts, loading `.hsl` / `.med` methods, scanning local method directories, and driving execution (`start`, `pause`, `resume`, `abort`, `shutdown`).

### Custom WPF Logging & Visual Interface
Features a dark-themed UI with animated status indicators, interactive connection controls, and an inline log viewer powered by a custom `WpfLoggerProvider`.

---

## Architecture & Execution Pipeline

```text
+-------------------------------------------------------------------+
|                           Relay Gateway                           |
|                    (ASP.NET Core / SignalR Hub)                   |
+---------------------------------+---------------------------------+
                                  |
                                  | WebSocket (Bearer Token)
                                  v
+---------------------------------+---------------------------------+
|                        McpClientService                           |
|           (Listens for ExecuteToolAsync over SignalR)             |
+---------------------------------+---------------------------------+
                                  |
                                  v
+---------------------------------+---------------------------------+
|                       McpToolDispatcher                           |
|       (Routes command string to registered IMcpToolHandler)       |
+-----------------+---------------------------------+---------------+
                  |                                 |
                  v                                 v
+-----------------+---------------+ +---------------+---------------+
|       Trace Tool Handlers       | |      Venus Automation Handlers |
| (ParseTraceSummary, ListFiles)  | | (EnsureStarted, LoadMethod)   |
+-----------------+---------------+ +---------------+---------------+
                  |                                 |
                  v                                 v
+-----------------+---------------+ +---------------+---------------+
|         Trace Logic Core        | |    Venus Automation Service   |
|   (Local .trc Log Files Engine) | |   (Win32 Process / Hardware)  |
+---------------------------------+ +-------------------------------+

```

---

## Configuration

Update `appsettings.json` with client credentials and relay server endpoints:

```json
{
  "McpConfig": {
    "Environment": "Dev",
    "ClientId": "YOUR_AZURE_CLIENT_ID",
    "TenantId": "YOUR_AZURE_TENANT_ID",
    "DevRelayUrl": "https://localhost:7216/mcphub",
    "ProdRelayUrl": "[https://your-production-server.com/mcphub](https://your-production-server.com/mcphub)"
  },
  "VenusAutomation": {
    "MethodScanDirectories": [
      "C:\\Program Files (x86)\\HAMILTON\\Methods",
      "C:\\Hamilton\\Methods"
    ]
  }
}

```

---

## Code Examples

### Implementing a Custom MCP Tool Handler

This snippet demonstrates how tool handlers inherit from `McpToolHandlerBase` to parse input parameters and return JSON execution payloads.

```csharp
public class VenusLoadMethodHandler : McpToolHandlerBase
{
    private readonly IVenusRunControlService _venusService;

    public VenusLoadMethodHandler(IVenusRunControlService venusService)
    {
        _venusService = venusService;
    }

    // Unique tool identifier matching MCP tool registration
    public override string Name => "hamilton_load_method";

    protected override async Task<(string ResultJson, bool IsError)> ExecuteCoreAsync(JsonElement arguments)
    {
        var methodPath = arguments.GetProperty("methodPath").GetString() 
            ?? throw new ArgumentException("Method path missing.");

        // Execute local hardware driver command
        await _venusService.LoadMethodAsync(methodPath);

        var response = new { status = "success", message = $"Method loaded: {methodPath}" };
        return (JsonSerializer.Serialize(response), false);
    }
}

```

### Acquiring Access Tokens via MSAL

This snippet demonstrates silent token acquisition with automatic interactive prompt fallback.

```csharp
private async Task<string?> GetAccessTokenAsync()
{
    var accounts = await _msalClient.GetAccountsAsync();

    try
    {
        // Attempt silent acquisition from cache
        var result = await _msalClient.AcquireTokenSilent(_scopes, accounts.FirstOrDefault())
            .ExecuteAsync();
        return result.AccessToken;
    }
    catch (MsalUiRequiredException)
    {
        try
        {
            // Fall back to interactive prompt if cache misses
            var result = await _msalClient.AcquireTokenInteractive(_scopes)
                .WithPrompt(Prompt.SelectAccount)
                .ExecuteAsync();
            return result.AccessToken;
        }
        catch (Exception ex)
        {
            LogToUI($"[Auth Error] {ex.Message}");
            return null;
        }
    }
}

```

---

## Supported Tool Handlers Reference

`parse_trace_summary`
Parses `.trc` files and returns top-level liquid transfer events.

`parse_trace_details`
Extracts channel-by-channel pipetting steps and hardware actions.

`list_trace_files`
Locates trace log files using time filters (`latest`, `today`, `this_week`, `custom`, `all`).

`hamilton_scan_methods`
Scans local directories to validate complete Hamilton method packages (`.med`, `.hsl`, `.stp`, `.sub`).

`hamilton_ensure_started`
Verifies that Hamilton Run Control is active.

`hamilton_get_status`
Queries system execution state, active errors, and current method name.

`hamilton_load_method`
Silently loads a target `.hsl` or `.med` method into the execution environment.

`hamilton_start_run` / `hamilton_pause_run` / `hamilton_resume_run` / `hamilton_abort_run`
Triggers real-time run control lifecycle actions against the robotics engine.

---

## License

This project is licensed under the MIT License.
