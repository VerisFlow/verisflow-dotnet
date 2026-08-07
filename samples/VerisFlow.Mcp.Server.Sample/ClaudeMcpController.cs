using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using VerisFlow.Mcp.Server;

namespace VerisFlow.Mcp.Server.Sample;

/// <summary>
/// API controller serving as an MCP (Model Context Protocol) gateway.
/// Handles OAuth2 proxying, SSE session streaming, and bridges JSON-RPC calls to WPF clients via SignalR.
/// </summary>
[ApiController]
[Route("mcp")]
public class ClaudeMcpController : ControllerBase
{
    private readonly McpSseConnectionManager _connectionManager;
    private readonly McpCoordinator _coordinator;
    private readonly IHubContext<McpRelayHub, IMcpAgentClient> _hubContext;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public ClaudeMcpController(
        McpSseConnectionManager connectionManager,
        McpCoordinator coordinator,
        IHubContext<McpRelayHub, IMcpAgentClient> hubContext,
        IConfiguration config,
        IHttpClientFactory httpClientFactory)
    {
        _connectionManager = connectionManager;
        _coordinator = coordinator;
        _hubContext = hubContext;
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Intercepts OAuth authorization requests and ensures the required Azure AD API scope is appended before redirecting to identity provider.
    /// </summary>
    [HttpGet("/authorize")]
    [AllowAnonymous]
    public IActionResult RedirectToAzureAuth()
    {
        var tenantId = _config["AzureAd:TenantId"];
        var clientId = _config["AzureAd:ClientId"];
        var scope = $"api://{clientId}/access_as_user";

        var queryString = Request.QueryString.Value ?? "";
        if (!queryString.Contains("scope=", StringComparison.OrdinalIgnoreCase))
        {
            var separator = queryString.Contains('?') ? "&" : "?";
            queryString += $"{separator}scope={Uri.EscapeDataString(scope)}";
        }

        var azureAuthUrl = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/authorize{queryString}";
        return Redirect(azureAuthUrl);
    }

    /// <summary>
    /// Proxies token requests to Azure AD, automatically injecting default scope configuration when missing from client form payloads.
    /// Filters incoming form data against a strict whitelist to prevent open proxy abuse.
    /// </summary>
    [HttpPost("/token")]
    [AllowAnonymous]
    public async Task<IActionResult> ProxyTokenRequest()
    {
        var tenantId = _config["AzureAd:TenantId"];
        var clientId = _config["AzureAd:ClientId"];
        var scope = $"api://{clientId}/access_as_user";

        var azureTokenUrl = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";

        // Whitelist of permitted parameters for the proxy to forward.
        // Ensure client_secret is included in the whitelist to support standard confidential clients.
        var allowedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "client_id", "client_secret", "grant_type", "code", "redirect_uri", "code_verifier", "scope"
    };

        var formDict = Request.Form
            .Where(x => allowedKeys.Contains(x.Key))
            .ToDictionary(x => x.Key, x => x.Value.ToString());

        if (!formDict.ContainsKey("scope"))
        {
            formDict["scope"] = scope;
        }

        var client = _httpClientFactory.CreateClient();
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, azureTokenUrl)
        {
            Content = new FormUrlEncodedContent(formDict)
        };

        // Forward the Authorization header if provided by the client (e.g., HTTP Basic Auth)
        if (Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            requestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
        }

        var response = await client.SendAsync(requestMessage);
        var responseData = await response.Content.ReadAsStringAsync();

        return Content(responseData, "application/json");
    }

    /// <summary>
    /// Establishes an HTTP Server-Sent Events (SSE) stream and asynchronously dispatches queued MCP messages from the session channel to the client.
    /// Enforces strict policy-based authorization.
    /// </summary>
    [HttpGet("sse")]
    [Authorize(Policy = "RequireApiScope")]
    public async Task ConnectSse()
    {
        // Set standard HTTP headers required for persistent SSE response streaming.
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        var sessionId = _connectionManager.CreateConnection(out var reader);

        await Response.WriteAsync($"event: endpoint\ndata: /mcp/messages?sessionId={sessionId}\n\n");
        await Response.Body.FlushAsync();

        try
        {
            await foreach (var message in reader.ReadAllAsync(HttpContext.RequestAborted))
            {
                await Response.WriteAsync($"event: message\ndata: {message}\n\n");
                await Response.Body.FlushAsync();
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _connectionManager.RemoveConnection(sessionId);
        }
    }

    /// <summary>
    /// Processes inbound MCP JSON-RPC protocol messages and routes execution commands to connected local agents.
    /// Enforces strict policy-based authorization.
    /// </summary>
    [HttpPost("messages")]
    [Authorize(Policy = "RequireApiScope")]
    public async Task<IActionResult> HandleMessage([FromQuery] string sessionId, [FromBody] JsonElement request)
    {
        if (string.IsNullOrEmpty(sessionId)) return BadRequest("Missing sessionId");

        string? method = request.TryGetProperty("method", out var methodProp) ? methodProp.GetString() : null;
        object? id = null;

        if (request.TryGetProperty("id", out var idProp))
        {
            id = idProp.ValueKind == JsonValueKind.Number ? idProp.GetInt64() : idProp.GetString();
        }

        if (method == "initialize")
        {
            var response = new
            {
                jsonrpc = "2.0",
                id = id,
                result = new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new { tools = new { } },
                    serverInfo = new { name = "VerisFlow.Mcp.Relay", version = "1.0.0" }
                }
            };
            await _connectionManager.SendMessageAsync(sessionId, JsonSerializer.Serialize(response));
        }
        else if (method == "tools/list")
        {
            var response = new
            {
                jsonrpc = "2.0",
                id = id,
                result = McpToolRegistry.GetToolsDefinition()
            };
            await _connectionManager.SendMessageAsync(sessionId, JsonSerializer.Serialize(response));
        }
        else if (method == "tools/call")
        {
            var paramsEl = request.GetProperty("params");
            var toolName = paramsEl.GetProperty("name").GetString();
            var argumentsJson = paramsEl.TryGetProperty("arguments", out var args) ? args.GetRawText() : "{}";

            // Map incoming MCP tool identifiers to internal agent endpoint names.
            string targetToolName = toolName switch
            {
                "list_traces" => "list_trace_files",
                "parse_trace" => "parse_trace_details",
                _ => toolName!
            };

            // Register a TaskCompletionSource to correlate the asynchronous SignalR execution response with the current HTTP JSON-RPC request.
            var requestId = Guid.NewGuid().ToString("N");
            var tcs = new TaskCompletionSource<ToolResult>();
            _coordinator.Register(requestId, tcs);

            await _hubContext.Clients.All.ExecuteToolAsync(requestId, targetToolName, argumentsJson);

            // Await execution result pushed back from WPF Agent via SignalR.
            var toolResult = await tcs.Task;

            Console.WriteLine($"\n[DEBUG] Raw data from WPF Agent for tool {toolName}: \n{toolResult.Data}\n");

            var response = new
            {
                jsonrpc = "2.0",
                id = id,
                result = new
                {
                    content = new object[] { new { type = "text", text = toolResult.Data } },
                    isError = toolResult.IsError
                }
            };

            await _connectionManager.SendMessageAsync(sessionId, JsonSerializer.Serialize(response));
        }

        return Accepted();
    }
}