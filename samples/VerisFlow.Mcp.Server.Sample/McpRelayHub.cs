using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using VerisFlow.Mcp.Server;

namespace VerisFlow.Mcp.Server.Sample;

/// <summary>
/// The WebSocket hub where the local desktop Agent connects.
/// Ensures defense-in-depth authorization is applied directly to the class.
/// </summary>
[Authorize(Policy = "RequireApiScope")]
public class McpRelayHub(McpCoordinator coordinator) : Hub<IMcpAgentClient>, IMcpRelayHub
{
    public override Task OnConnectedAsync()
    {
        return base.OnConnectedAsync();
    }

    /// <summary>
    /// Receives execution results from the desktop agent and completes the pending asynchronous task in the coordinator.
    /// </summary>
    /// <param name="requestId">The unique request correlation identifier.</param>
    /// <param name="resultJson">The serialized JSON string returned by the tool execution.</param>
    /// <param name="isError">Flag indicating whether the tool execution encountered an error.</param>
    public Task SubmitToolResultAsync(string requestId, string resultJson, bool isError)
    {
        // Unblocks the waiting HTTP JSON-RPC handler by resolving the correlated TaskCompletionSource.
        coordinator.TryCompleteRequest(requestId, resultJson, isError);
        return Task.CompletedTask;
    }
}