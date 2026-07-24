using System;
using System.Text.Json;
using System.Threading.Tasks;
using VerisFlow.Mcp.Client;

namespace VerisFlow.Mcp.Client.Sample;

/// <summary>
/// Serves as the base class for Model Context Protocol (MCP) tool handlers, providing standard argument parsing and error handling.
/// </summary>
public abstract class McpToolHandlerBase : IMcpToolHandler
{
    /// <summary>
    /// Gets the unique identifier name for the MCP tool handler.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Executes the tool handler asynchronously using raw JSON arguments.
    /// </summary>
    /// <param name="argumentsJson">The raw JSON string representing the arguments passed to the tool.</param>
    /// <returns>A tuple containing the JSON serialized execution result and an error indicator flag.</returns>
    public async Task<(string ResultJson, bool IsError)> ExecuteAsync(string argumentsJson)
    {
        try
        {
            using var doc = string.IsNullOrWhiteSpace(argumentsJson) ? JsonDocument.Parse("{}") : JsonDocument.Parse(argumentsJson);
            return await ExecuteCoreAsync(doc.RootElement);
        }
        catch (Exception ex)
        {
            var exceptionResponse = new { error = ex.Message, stackTrace = ex.StackTrace };
            return (JsonSerializer.Serialize(exceptionResponse), true);
        }
    }

    /// <summary>
    /// Core execution logic implemented by derived handlers using parsed JSON elements.
    /// </summary>
    /// <param name="arguments">The parsed JSON root element containing input arguments.</param>
    /// <returns>A tuple containing the JSON serialized execution result and an error indicator flag.</returns>
    protected abstract Task<(string ResultJson, bool IsError)> ExecuteCoreAsync(JsonElement arguments);
}