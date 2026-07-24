# VerisFlow.Mcp.Server

**VerisFlow.Mcp.Server is a .NET server-side core infrastructure library designed to manage active Server-Sent Events (SSE) streaming connections and coordinate asynchronous Model Context Protocol (MCP) tool execution requests between server orchestrators and client agents.**

Target Frameworks: `.NET Standard 2.0` | `.NET 8.0` | `.NET 9.0`

---

## ✨ Key Features

* **SSE Connection Streaming Management**: `McpSseConnectionManager` maintains high-throughput Server-Sent Events sessions using unbounded `System.Threading.Channels` for real-time outbound protocol event delivery.
* **Asynchronous Request Coordination**: `McpCoordinator` correlates unique request identifiers to `TaskCompletionSource<ToolResult>` instances, enabling thread-safe non-blocking waiting for client execution responses.
* **Thread-Safe & Lock-Free Design**: Leverages `ConcurrentDictionary` and channel primitives to handle high-concurrency client sessions and result callbacks without locking overhead.
* **Standardized Result Payload**: Captures tool execution output and status in a clean `ToolResult` abstraction.

---

## 🚀 Quick Start

### Installation

```bash
dotnet add package VerisFlow.Mcp.Server

```

### Usage Example

```csharp
using System;
using System.Threading.Tasks;
using VerisFlow.Mcp.Server;

public class McpServerWorkflow
{
    private readonly McpSseConnectionManager _connectionManager = new();
    private readonly McpCoordinator _coordinator = new();

    public async Task RunServerExampleAsync()
    {
        // 1. Establish an SSE streaming connection for a client session
        string sessionId = _connectionManager.CreateConnection(out var channelReader);

        // 2. Register an asynchronous pending request with a unique ID
        string requestId = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<ToolResult>();
        _coordinator.Register(requestId, tcs);

        // 3. Send tool invocation request message over SSE
        string payload = $"{{\"requestId\":\"{requestId}\",\"tool\":\"get_system_info\"}}";
        await _connectionManager.SendMessageAsync(sessionId, payload);

        // 4. Simulate receiving a client callback on a separate thread/endpoint
        _ = Task.Run(async () =>
        {
            await Task.Delay(100);
            string responseJson = "{\"status\":\"success\",\"cpuUsage\":\"12%\"}";
            _coordinator.TryCompleteRequest(requestId, responseJson, isError: false);
        });

        // 5. Await execution result synchronously or asynchronously
        ToolResult result = await tcs.Task;
        Console.WriteLine($"Result received: {result.Data} (IsError: {result.IsError})");

        // 6. Clean up SSE session upon disconnection
        _connectionManager.RemoveConnection(sessionId);
    }
}

```

---

## 🏗️ Architecture & Processing Pipeline

`VerisFlow.Mcp.Server` acts as the server-side hub bridging remote MCP clients and local orchestration workflows:

1. **Session Initialization**: `McpSseConnectionManager` generates a session ID and allocates an unbounded `Channel<string>` for outbound SSE streaming.
2. **Request Registration**: Before invoking a tool remotely, `McpCoordinator` maps the `requestId` to a `TaskCompletionSource<ToolResult>`.
3. **Outbound Streaming**: The server writes the JSON command into the client's SSE channel.
4. **Callback Matching**: When the client agent returns execution results via HTTP/SignalR, `McpCoordinator.TryCompleteRequest` atomically removes the pending request tracker and sets the `ToolResult`.
5. **Session Teardown**: Upon client disconnection, `McpSseConnectionManager` completes the underlying channel writer and releases resources.

---

## 📂 Namespace Overview

* **`VerisFlow.Mcp.Server`**: Core server namespace containing `McpCoordinator` for request tracking, `McpSseConnectionManager` for session channel management, and `ToolResult` for execution outcome modeling.

---

## 🤝 Contributing Guidelines

Contributions are welcome! To contribute:

1. Fork the repository.
2. Create a feature branch (`git checkout -b feature/AmazingFeature`).
3. Commit your changes (`git commit -m 'Add AmazingFeature'`).
4. Push to the branch (`git push origin feature/AmazingFeature`).
5. Open a Pull Request with context about your changes.

---

## 📄 License

This project is licensed under the **MIT License**.