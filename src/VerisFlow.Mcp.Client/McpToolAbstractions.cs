using System.Threading.Tasks;

namespace VerisFlow.Mcp.Client;

/// <summary>
/// Represents an executable handler for a specific Model Context Protocol (MCP) tool.
/// </summary>
public interface IMcpToolHandler
{
    /// <summary>
    /// Gets the unique identifier or name of the tool.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes the underlying tool logic asynchronously using raw JSON arguments.
    /// </summary>
    /// <param name="argumentsJson">The raw JSON string containing invocation parameters.</param>
    /// <returns>A tuple containing the JSON result payload and a boolean flag indicating error status.</returns>
    Task<(string ResultJson, bool IsError)> ExecuteAsync(string argumentsJson);
}

/// <summary>
/// Provides resolution logic to discover and retrieve registered MCP tool handlers.
/// </summary>
public interface IMcpToolRegistry
{
    /// <summary>
    /// Resolves an <see cref="IMcpToolHandler"/> by its registered tool name.
    /// </summary>
    /// <param name="toolName">The unique name of the target tool.</param>
    /// <returns>The matching tool handler instance, or <c>null</c> if not found.</returns>
    IMcpToolHandler? GetTool(string toolName);
}

/// <summary>
/// Defines the dispatcher abstraction responsible for resolving and executing MCP tools.
/// </summary>
public interface IMcpToolDispatcher
{
    /// <summary>
    /// Dispatches a tool execution request by name, invoking the corresponding handler and handling error scenarios.
    /// </summary>
    /// <param name="toolName">The name of the tool to execute.</param>
    /// <param name="argumentsJson">The JSON input parameters for the tool.</param>
    /// <returns>A tuple containing the output JSON string and a boolean flag indicating if execution failed.</returns>
    Task<(string ResultJson, bool IsError)> DispatchAsync(string toolName, string argumentsJson);
}