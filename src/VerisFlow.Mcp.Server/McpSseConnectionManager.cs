using System;
using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace VerisFlow.Mcp.Server;

/// <summary>
/// Manages active Server-Sent Events (SSE) connection channels for streaming MCP protocol messages to client endpoints.
/// </summary>
public class McpSseConnectionManager
{
    private readonly ConcurrentDictionary<string, Channel<string>> _connections = new();

    /// <summary>
    /// Creates a new SSE session and provides a channel reader for streaming outbound messages.
    /// </summary>
    /// <param name="reader">When this method returns, contains the <see cref="ChannelReader{T}"/> for consuming streamed messages.</param>
    /// <returns>The generated unique session identifier for the connection.</returns>
    public string CreateConnection(out ChannelReader<string> reader)
    {
        var sessionId = Guid.NewGuid().ToString("N");

        // Unbounded channel ensures high throughput for event-stream messages
        var channel = Channel.CreateUnbounded<string>();
        _connections.TryAdd(sessionId, channel);

        reader = channel.Reader;
        return sessionId;
    }

    /// <summary>
    /// Removes an active SSE connection session and completes its underlying channel writer.
    /// </summary>
    /// <param name="sessionId">The unique session identifier of the connection to remove.</param>
    public void RemoveConnection(string sessionId)
    {
        if (_connections.TryRemove(sessionId, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }

    /// <summary>
    /// Asynchronously sends a message payload to a specific active SSE session.
    /// </summary>
    /// <param name="sessionId">The target session identifier.</param>
    /// <param name="message">The text message payload to write to the channel.</param>
    /// <returns>A task representing the asynchronous channel write operation.</returns>
    public async Task SendMessageAsync(string sessionId, string message)
    {
        if (_connections.TryGetValue(sessionId, out var channel))
        {
            await channel.Writer.WriteAsync(message);
        }
    }
}