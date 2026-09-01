using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace VerisFlow.Mcp.Client;

/// <summary>
/// Defines protocol method constants used for SignalR communication with Cloud Relay.
/// </summary>
internal static class McpProtocolMethods
{
    /// <summary>
    /// SignalR method name invoked by the server to request tool execution on the client.
    /// </summary>
    public const string ExecuteToolAsync = "ExecuteToolAsync";

    /// <summary>
    /// SignalR method name invoked by the client to return tool execution results back to the server.
    /// </summary>
    public const string SubmitToolResultAsync = "SubmitToolResultAsync";
}

/// <summary>
/// Manages the SignalR connection lifecycle and handles incoming MCP tool execution calls from Cloud Relay.
/// </summary>
public partial class McpClientService : IAsyncDisposable
{
    private readonly HubConnection _connection;
    private readonly IMcpToolDispatcher _dispatcher;
    private readonly ILogger<McpClientService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpClientService"/> class.
    /// </summary>
    /// <param name="relayUrl">The target SignalR hub endpoint URL for Cloud Relay.</param>
    /// <param name="dispatcher">The tool dispatcher responsible for routing and executing tool calls.</param>
    /// <param name="logger">The logger instance for operational and diagnostic logging.</param>
    /// <param name="accessTokenProvider">An optional delegate to retrieve an authentication access token asynchronously.</param>
    public McpClientService(
        string relayUrl,
        IMcpToolDispatcher dispatcher,
        ILogger<McpClientService> logger,
        Func<Task<string?>>? accessTokenProvider = null)
    {
        if (string.IsNullOrWhiteSpace(relayUrl))
            throw new ArgumentException("Relay URL cannot be null or whitespace.", nameof(relayUrl));

        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var hubBuilder = new HubConnectionBuilder();

        if (accessTokenProvider != null)
        {
            hubBuilder.WithUrl(relayUrl, options =>
            {
                options.AccessTokenProvider = accessTokenProvider;
            });
        }
        else
        {
            hubBuilder.WithUrl(relayUrl);
        }

        _connection = hubBuilder
            .WithAutomaticReconnect()
            .Build();

        // Bind incoming tool execution calls to the generic dispatcher
        _connection.On<string, string, string>(McpProtocolMethods.ExecuteToolAsync, async (requestId, toolName, argsJson) =>
        {
            LogAiRequestedToolExecution(toolName);

            string resultJson;
            bool isError;

            try
            {
                (resultJson, isError) = await _dispatcher.DispatchAsync(toolName, argsJson);
            }
            catch (Exception ex)
            {
                // Ensure unhandled dispatcher failures still yield a valid JSON error payload back to the relay
                var errorPayload = new { error = ex.Message, stackTrace = ex.StackTrace };
                resultJson = JsonSerializer.Serialize(errorPayload);
                isError = true;
            }

            LogToolExecutionFinished();

            try
            {
                await _connection.InvokeAsync(McpProtocolMethods.SubmitToolResultAsync, requestId, resultJson, isError);
            }
            catch (Exception ex)
            {
                LogFailedToSubmitToolExecutionResult(ex, requestId);
            }
        });
    }

    /// <summary>
    /// Establishes the SignalR hub connection to Cloud Relay.
    /// </summary>
    /// <returns>A task representing the asynchronous connect operation.</returns>
    public async Task StartAsync()
    {
        await _connection.StartAsync();
        LogConnectedSuccessfully();
    }

    /// <summary>
    /// Disconnects the SignalR hub connection from Cloud Relay.
    /// </summary>
    /// <returns>A task representing the asynchronous disconnect operation.</returns>
    public async Task StopAsync()
    {
        await _connection.StopAsync();
        LogDisconnected();
    }

    /// <summary>
    /// Asynchronously disposes the SignalR hub connection resources.
    /// </summary>
    /// <returns>A value task representing the asynchronous disposal operation.</returns>
    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "AI requested tool execution for {ToolName}.")]
    private partial void LogAiRequestedToolExecution(string toolName);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Tool execution finished. Submitting result to Cloud Relay.")]
    private partial void LogToolExecutionFinished();

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Connected to Cloud Relay successfully.")]
    private partial void LogConnectedSuccessfully();

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Disconnected from Cloud Relay.")]
    private partial void LogDisconnected();

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Failed to submit tool execution result for request ID {RequestId}.")]
    private partial void LogFailedToSubmitToolExecutionResult(Exception ex, string requestId);
}