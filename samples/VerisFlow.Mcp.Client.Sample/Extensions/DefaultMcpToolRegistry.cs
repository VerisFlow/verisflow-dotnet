using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using VerisFlow.Mcp.Client;

namespace VerisFlow.Mcp.Client.Sample;

/// <summary>
/// Provides the default implementation of the <see cref="IMcpToolRegistry"/> interface for managing and retrieving tool handlers.
/// </summary>
public class DefaultMcpToolRegistry : IMcpToolRegistry
{
    /// <summary>
    /// Maps tool names to their corresponding handler types using case-insensitive comparison.
    /// </summary>
    private readonly Dictionary<string, Type> _toolTypeMap;

    /// <summary>
    /// Service provider used for on-demand resolution of tool handlers.
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Cache of instantiated tool handlers to avoid duplicate creation.
    /// </summary>
    private readonly ConcurrentDictionary<string, IMcpToolHandler> _handlers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultMcpToolRegistry"/> class with lazy resolution support.
    /// </summary>
    /// <param name="serviceProvider">The service provider instance used to resolve handler dependencies on demand.</param>
    public DefaultMcpToolRegistry(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        var handlerType = typeof(IMcpToolHandler);
        var implementations = typeof(McpToolHandlerBase).Assembly
            .GetTypes()
            .Where(t => handlerType.IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

        _toolTypeMap = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        foreach (var impl in implementations)
        {
            try
            {
                // Extract tool name without executing target constructor dependencies or initializing hardware services
                var uninitialized = (IMcpToolHandler)RuntimeHelpers.GetUninitializedObject(impl);
                var toolName = uninitialized.Name;

                if (!string.IsNullOrWhiteSpace(toolName))
                {
                    _toolTypeMap[toolName] = impl;
                }
            }
            catch
            {
                // Suppress reflection inspection errors for non-standard handlers
            }
        }
    }

    /// <summary>
    /// Retrieves a registered tool handler matching the specified tool name.
    /// </summary>
    /// <param name="toolName">The case-insensitive name of the tool handler to retrieve.</param>
    /// <returns>The matching <see cref="IMcpToolHandler"/> instance if found; otherwise, <c>null</c>.</returns>
    public IMcpToolHandler? GetTool(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return null;
        }

        if (!_toolTypeMap.TryGetValue(toolName, out var handlerType))
        {
            return null;
        }

        return _handlers.GetOrAdd(toolName, _ =>
            (IMcpToolHandler)ActivatorUtilities.GetServiceOrCreateInstance(_serviceProvider, handlerType));
    }
}