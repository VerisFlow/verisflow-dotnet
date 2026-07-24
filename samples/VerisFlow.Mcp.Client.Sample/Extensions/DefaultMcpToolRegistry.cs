using System;
using System.Collections.Generic;
using System.Linq;
using VerisFlow.Mcp.Client;

namespace VerisFlow.Mcp.Client.Sample;

/// <summary>
/// Provides the default implementation of the <see cref="IMcpToolRegistry"/> interface for managing and retrieving tool handlers.
/// </summary>
public class DefaultMcpToolRegistry : IMcpToolRegistry
{
    /// <summary>
    /// Maps tool names to their corresponding handlers using case-insensitive key comparison.
    /// </summary>
    private readonly Dictionary<string, IMcpToolHandler> _handlers;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultMcpToolRegistry"/> class with the specified collection of tool handlers.
    /// </summary>
    /// <param name="handlers">An enumerable collection of tool handlers to register in the registry.</param>
    public DefaultMcpToolRegistry(IEnumerable<IMcpToolHandler> handlers)
    {
        _handlers = handlers.ToDictionary(h => h.Name, h => h, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Retrieves a registered tool handler matching the specified tool name.
    /// </summary>
    /// <param name="toolName">The case-insensitive name of the tool handler to retrieve.</param>
    /// <returns>The matching <see cref="IMcpToolHandler"/> instance if found; otherwise, <c>null</c>.</returns>
    public IMcpToolHandler? GetTool(string toolName)
    {
        _handlers.TryGetValue(toolName, out var handler);
        return handler;
    }
}