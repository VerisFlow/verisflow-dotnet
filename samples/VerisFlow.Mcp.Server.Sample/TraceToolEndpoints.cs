using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading;
using System.Threading.Tasks;
using VerisFlow.Mcp.Server;

namespace VerisFlow.Mcp.Server.Sample;

/// <summary>
/// Configures Minimal API endpoints for triggering trace parsing and file system tools.
/// </summary>
public static class TraceToolEndpoints
{
    public static void MapTraceToolEndpoints(this WebApplication app)
    {
        app.MapPost("/api/tools/parse_trace", async (
            [FromBody] ParseTraceRequest request,
            [FromServices] IHubContext<McpRelayHub, IMcpAgentClient> hubContext,
            [FromServices] McpCoordinator coordinator,
            CancellationToken ct) =>
        {
            string requestId = Guid.NewGuid().ToString();
            // Register a correlated TaskCompletionSource to await the SignalR agent execution result.
            // RunContinuationsAsynchronously prevents completion callbacks from executing inline on the SignalR transport thread.
            var tcs = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            coordinator.Register(requestId, tcs);

            string argsJson = System.Text.Json.JsonSerializer.Serialize(new { filePath = request.FilePath });
            await hubContext.Clients.All.ExecuteToolAsync(requestId, "parse_trace_summary", argsJson);

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
        .WithName("ParseTraceLog")
        .WithDescription("Instructs the local agent to parse a .trc file and return all liquid transfer events.")
        .RequireAuthorization();

        app.MapPost("/api/tools/parse_details", async (
            [FromBody] ParseTraceRequest request,
            [FromServices] IHubContext<McpRelayHub, IMcpAgentClient> hubContext,
            [FromServices] McpCoordinator coordinator,
            CancellationToken ct) =>
        {
            string requestId = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            coordinator.Register(requestId, tcs);

            string argsJson = System.Text.Json.JsonSerializer.Serialize(new { filePath = request.FilePath });
            await hubContext.Clients.All.ExecuteToolAsync(requestId, "parse_trace_details", argsJson);

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
        .WithName("ParseTraceDetails")
        .WithDescription("Instructs the local agent to parse a .trc file and return granular pipetting steps and channel actions.")
        .RequireAuthorization();

        app.MapPost("/api/tools/list_traces", async (
            [FromBody] ListTracesRequest request,
            [FromServices] IHubContext<McpRelayHub, IMcpAgentClient> hubContext,
            [FromServices] McpCoordinator coordinator,
            CancellationToken ct) =>
        {
            string requestId = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            coordinator.Register(requestId, tcs);

            string argsJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                directoryPath = request.DirectoryPath,
                timeFilter = request.TimeFilter,
                startTime = request.StartTime,
                endTime = request.EndTime
            });

            await hubContext.Clients.All.ExecuteToolAsync(requestId, "list_trace_files", argsJson);

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
        .WithName("ListTraceLogs")
        .WithDescription("Instructs the local agent to search for .trc files. Supported filters: latest, today, this_week, this_month, custom, all.")
        .RequireAuthorization();
    }
}