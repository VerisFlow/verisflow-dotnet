using System.Threading.Tasks;

namespace VerisFlow.Mcp.Server.Sample;
/// <summary>
/// Defines the methods that the local WPF Agent must implement.
/// The Cloud Relay uses this interface to push commands down to the local machine 
/// bypassing local firewalls via the established SignalR connection.
/// </summary>
public interface IMcpAgentClient
{
    /// <summary>
    /// Instructs the local agent to execute a specific tool (e.g., parsing a trace file).
    /// </summary>
    /// <param name="requestId">A unique identifier for the HTTP request initiated by the Web AI.</param>
    /// <param name="toolName">The specific action the AI wants to perform (e.g., 'parse_trace_summary').</param>
    /// <param name="argumentsJson">The parameters provided by the AI, serialized as a JSON string.</param>
    Task ExecuteToolAsync(string requestId, string toolName, string argumentsJson);
}

/// <summary>
/// Defines the methods exposed by the Cloud Relay Hub.
/// The local WPF Agent uses this interface to send execution results back to the cloud.
/// </summary>
public interface IMcpRelayHub
{
    /// <summary>
    /// Submits the result of a local tool execution back to the Cloud Relay, 
    /// which will then forward it to the waiting Web AI (ChatGPT/Gemini).
    /// </summary>
    /// <param name="requestId">The unique identifier matching the original request.</param>
    /// <param name="resultJson">The execution result or summary data, serialized as JSON.</param>
    /// <param name="isError">Flag indicating whether the local execution failed.</param>
    Task SubmitToolResultAsync(string requestId, string resultJson, bool isError);
}