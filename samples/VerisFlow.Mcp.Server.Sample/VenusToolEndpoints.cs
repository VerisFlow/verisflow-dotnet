using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VerisFlow.Mcp.Server;

namespace VerisFlow.Mcp.Server.Sample;

/// <summary>
/// Configures Minimal API endpoints for interacting with the local Hamilton robotics hardware.
/// </summary>
public static class VenusToolEndpoints
{
    private static readonly string[] ValidCommands = ["start", "pause", "resume", "abort", "shutdown"];

    public static void MapHamiltonToolEndpoints(this WebApplication app)
    {
        app.MapPost("/api/tools/hamilton/ensure_started", async (
            [FromServices] IHubContext<McpRelayHub, IMcpAgentClient> hubContext,
            [FromServices] McpCoordinator coordinator,
            CancellationToken ct) =>
        {
            string requestId = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            coordinator.Register(requestId, tcs);

            await hubContext.Clients.All.ExecuteToolAsync(requestId, "hamilton_ensure_started", "{}");

            try
            {
                var result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
                return result.IsError ? Results.BadRequest(result.Data) : Results.Ok(result.Data);
            }
            catch (TimeoutException)
            {
                return Results.StatusCode(StatusCodes.Status504GatewayTimeout);
            }
        })
        .WithName("HamiltonEnsureStarted")
        .WithDescription("Ensures the Hamilton Run Control process is running.")
        .RequireAuthorization();

        app.MapPost("/api/tools/hamilton/status", async (
            [FromServices] IHubContext<McpRelayHub, IMcpAgentClient> hubContext,
            [FromServices] McpCoordinator coordinator,
            CancellationToken ct) =>
        {
            string requestId = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            coordinator.Register(requestId, tcs);

            await hubContext.Clients.All.ExecuteToolAsync(requestId, "hamilton_get_status", "{}");

            try
            {
                var result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
                return result.IsError ? Results.BadRequest(result.Data) : Results.Ok(result.Data);
            }
            catch (TimeoutException)
            {
                return Results.StatusCode(StatusCodes.Status504GatewayTimeout);
            }
        })
        .WithName("HamiltonGetStatus")
        .WithDescription("Gets the current execution state of the Hamilton system, including the currently loaded method name.")
        .RequireAuthorization();

        app.MapPost("/api/tools/hamilton/arrange_window", async (
            [FromBody] ArrangeVenusWindowRequest request,
            [FromServices] IHubContext<McpRelayHub, IMcpAgentClient> hubContext,
            [FromServices] McpCoordinator coordinator,
            CancellationToken ct) =>
        {
            string requestId = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            coordinator.Register(requestId, tcs);

            string argsJson = System.Text.Json.JsonSerializer.Serialize(request);
            await hubContext.Clients.All.ExecuteToolAsync(requestId, "hamilton_arrange_window", argsJson);

            try
            {
                var result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
                return result.IsError ? Results.BadRequest(result.Data) : Results.Ok(result.Data);
            }
            catch (TimeoutException)
            {
                return Results.StatusCode(StatusCodes.Status504GatewayTimeout);
            }
        })
        .WithName("HamiltonArrangeWindow")
        .WithDescription("Arranges the Hamilton Run Control window layout.")
        .RequireAuthorization();

        app.MapPost("/api/tools/hamilton/load_method", async (
            [FromBody] LoadVenusMethodRequest request,
            [FromServices] IHubContext<McpRelayHub, IMcpAgentClient> hubContext,
            [FromServices] McpCoordinator coordinator,
            CancellationToken ct) =>
        {
            string requestId = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            coordinator.Register(requestId, tcs);

            string argsJson = System.Text.Json.JsonSerializer.Serialize(new { methodPath = request.MethodPath });
            await hubContext.Clients.All.ExecuteToolAsync(requestId, "hamilton_load_method", argsJson);

            try
            {
                var result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(45), ct);
                return result.IsError ? Results.BadRequest(result.Data) : Results.Ok(result.Data);
            }
            catch (TimeoutException)
            {
                return Results.StatusCode(StatusCodes.Status504GatewayTimeout);
            }
        })
        .WithName("HamiltonLoadMethod")
        .WithDescription("Silently loads a .hsl method file into the Hamilton Run Control.")
        .RequireAuthorization();

        // Explicitly allow empty body payloads so callers can trigger directory scanning without passing JSON arguments.
        app.MapPost("/api/tools/hamilton/scan_methods", async (
            [Microsoft.AspNetCore.Mvc.FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] ScanVenusMethodsRequest? request,
            [FromServices] IHubContext<McpRelayHub, IMcpAgentClient> hubContext,
            [FromServices] McpCoordinator coordinator,
            CancellationToken ct) =>
        {
            string requestId = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            coordinator.Register(requestId, tcs);

            string argsJson = request != null && !string.IsNullOrWhiteSpace(request.DirectoryPath)
                ? System.Text.Json.JsonSerializer.Serialize(new { directoryPath = request.DirectoryPath })
                : "{}";

            await hubContext.Clients.All.ExecuteToolAsync(requestId, "hamilton_scan_methods", argsJson);

            try
            {
                var result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
                return result.IsError ? Results.BadRequest(result.Data) : Results.Ok(result.Data);
            }
            catch (TimeoutException)
            {
                return Results.StatusCode(StatusCodes.Status504GatewayTimeout);
            }
        })
        .WithName("HamiltonScanMethods")
        .WithDescription("Scans configured directories on the local agent for complete .med and .hsl methods.")
        .RequireAuthorization();

        // Route RESTful command parameters dynamically to specific Hamilton agent tool handlers.
        app.MapPost("/api/tools/hamilton/action/{command}", async (
            string command,
            [FromServices] IHubContext<McpRelayHub, IMcpAgentClient> hubContext,
            [FromServices] McpCoordinator coordinator,
            CancellationToken ct) =>
        {
            // Validate incoming route command against strictly permitted machine actions.
            if (!ValidCommands.Contains(command.ToLowerInvariant()))
            {
                return Results.BadRequest("Invalid command. Use start, pause, resume, abort, or shutdown.");
            }

            // Map standard control action verbs to internal agent tool function names.
            string toolName = command.ToLowerInvariant() switch
            {
                "start" => "hamilton_start_run",
                "pause" => "hamilton_pause_run",
                "resume" => "hamilton_resume_run",
                "abort" => "hamilton_abort_run",
                "shutdown" => "hamilton_graceful_shutdown",
                _ => throw new InvalidOperationException()
            };

            string requestId = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            coordinator.Register(requestId, tcs);

            await hubContext.Clients.All.ExecuteToolAsync(requestId, toolName, "{}");

            try
            {
                var result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(20), ct);
                return result.IsError ? Results.BadRequest(result.Data) : Results.Ok(result.Data);
            }
            catch (TimeoutException)
            {
                return Results.StatusCode(StatusCodes.Status504GatewayTimeout);
            }
        })
        .WithName("HamiltonAction")
        .WithDescription("Executes a basic control action (start, pause, resume, abort, shutdown) on the Hamilton machine.")
        .RequireAuthorization();
    }
}