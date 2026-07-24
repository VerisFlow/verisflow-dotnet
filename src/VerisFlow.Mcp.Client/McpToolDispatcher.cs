using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace VerisFlow.Mcp.Client;

/// <summary>
/// Default implementation of <see cref="IMcpToolDispatcher"/> that resolves tools via registry and captures execution exceptions.
/// </summary>
public class McpToolDispatcher : IMcpToolDispatcher
{
    private readonly IMcpToolRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpToolDispatcher"/> class.
    /// </summary>
    /// <param name="registry">The tool registry used to look up tool handlers.</param>
    public McpToolDispatcher(IMcpToolRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// Dispatches execution to the registered tool handler or returns a JSON error response if missing or upon exception.
    /// </summary>
    /// <param name="toolName">The name of the target tool to dispatch.</param>
    /// <param name="argumentsJson">The JSON arguments string to pass to the tool.</param>
    /// <returns>A tuple containing the response JSON payload and an error flag.</returns>
    public async Task<(string ResultJson, bool IsError)> DispatchAsync(string toolName, string argumentsJson)
    {
        try
        {
            var handler = _registry.GetTool(toolName);
            if (handler == null)
            {
                var notFoundResponse = new { error = "Tool not recognized by the local agent." };
                return (JsonSerializer.Serialize(notFoundResponse), true);
            }

            return await handler.ExecuteAsync(argumentsJson);
        }
        catch (Exception ex)
        {
            var exceptionResponse = new { error = ex.Message, stackTrace = ex.StackTrace };
            return (JsonSerializer.Serialize(exceptionResponse), true);
        }
    }
}