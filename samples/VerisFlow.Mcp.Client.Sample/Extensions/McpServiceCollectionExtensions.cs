using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using VerisFlow.Mcp.Client;

namespace VerisFlow.Mcp.Client.Sample;

/// <summary>
/// Provides extension methods for registering MCP tool handlers in the dependency injection container.
/// </summary>
public static class McpServiceCollectionExtensions
{
    /// <summary>
    /// Automatically scans the assembly and registers all concrete implementations of <see cref="IMcpToolHandler"/>.
    /// </summary>
    /// <param name="services">The service collection to register handlers into.</param>
    /// <returns>The updated service collection instance.</returns>
    public static IServiceCollection AddMcpToolHandlers(this IServiceCollection services)
    {
        var handlerType = typeof(IMcpToolHandler);
        var handlerImplementations = typeof(McpToolHandlerBase).Assembly
            .GetTypes()
            .Where(t => handlerType.IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

        foreach (var implementation in handlerImplementations)
        {
            services.AddTransient(implementation);
        }

        return services;
    }
}