using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace VerisFlow.Mcp.Server;

/// <summary>
/// Represents the result of a Model Context Protocol (MCP) tool execution request.
/// </summary>
public class ToolResult
{
    /// <summary>
    /// Gets or sets the raw JSON result data returned by the tool execution.
    /// </summary>
    public string Data { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the tool execution encountered an error.
    /// </summary>
    public bool IsError { get; set; }
}

/// <summary>
/// Coordinates pending asynchronous tool execution requests between server callers and client agents.
/// </summary>
public class McpCoordinator
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ToolResult>> _pendingRequests = new();

    /// <summary>
    /// Registers a pending request with a <see cref="TaskCompletionSource{TResult}"/> to track its completion.
    /// </summary>
    /// <param name="requestId">The unique identifier for the execution request.</param>
    /// <param name="tcs">The task completion source used to await the tool result.</param>
    public void Register(string requestId, TaskCompletionSource<ToolResult> tcs)
    {
        _pendingRequests.TryAdd(requestId, tcs);
    }

    /// <summary>
    /// Attempts to complete a pending tool execution request with the provided execution result.
    /// </summary>
    /// <param name="requestId">The unique identifier of the pending request.</param>
    /// <param name="resultJson">The raw JSON output produced by the executed tool.</param>
    /// <param name="isError">Indicates whether the execution failed or threw an error.</param>
    /// <returns><c>true</c> if the pending request was successfully found and completed; otherwise, <c>false</c>.</returns>
    public bool TryCompleteRequest(string requestId, string resultJson, bool isError)
    {
        // Atomically retrieve and remove the pending request upon result callback
        if (_pendingRequests.TryRemove(requestId, out var tcs))
        {
            var result = new ToolResult
            {
                Data = resultJson,
                IsError = isError
            };
            return tcs.TrySetResult(result);
        }

        return false;
    }
}