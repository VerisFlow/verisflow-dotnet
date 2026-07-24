# VerisFlow.Mcp.Client

**VerisFlow.Mcp.Client is a .NET client library designed to establish a real-time SignalR connection with Cloud Relay, enabling remote AI models to execute Model Context Protocol (MCP) tools on local systems.**

Target Frameworks: `.NET Standard 2.0` | `.NET 8.0` | `.NET 9.0`

---

## ✨ Key Features

* **Real-time SignalR Relay Connection**: Connects to Cloud Relay hub endpoints using ASP.NET Core SignalR client with built-in automatic reconnects and token-based authentication options.
* **Decoupled Tool Handler Architecture**: Provides clean interfaces (`IMcpToolHandler`, `IMcpToolRegistry`, `IMcpToolDispatcher`) to register, resolve, and execute local MCP tools.
* **Safe Request Dispatching**: Intercepts incoming tool execution commands, handles missing tools, and captures unexpected runtime exceptions gracefully into structured JSON error payloads.
* **Standardized Protocol Methods**: Encapsulates Cloud Relay communication logic through defined method contracts (`ExecuteToolAsync` and `SubmitToolResultAsync`).

---

## 🚀 Quick Start

### Installation

```bash
dotnet add package VerisFlow.Mcp.Client

```

### 1. Implement a Custom Tool Handler

Create a tool handler implementing `IMcpToolHandler` to expose local functionality to Cloud Relay.

```csharp
using System.Threading.Tasks;
using VerisFlow.Mcp.Client;

public class SystemInfoToolHandler : IMcpToolHandler
{
    public string Name => "get_system_info";

    public Task<(string ResultJson, bool IsError)> ExecuteAsync(string argumentsJson)
    {
        // Construct tool response payload
        string result = "{\"status\":\"ok\",\"os\":\"Windows/Linux\",\"version\":\"1.0.0\"}";
        return Task.FromResult((result, false));
    }
}

```

### 2. Register Tools and Start the Service

Initialize the registry, dispatcher, and client service to start receiving remote execution requests.

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VerisFlow.Mcp.Client;

// Define simple registry implementation
public class McpToolRegistry : IMcpToolRegistry
{
    private readonly Dictionary<string, IMcpToolHandler> _tools = new();

    public void Register(IMcpToolHandler handler) => _tools[handler.Name] = handler;

    public IMcpToolHandler? GetTool(string toolName) 
        => _tools.TryGetValue(toolName, out var handler) ? handler : null;
}

public class Program
{
    public static async Task Main(string[] args)
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<McpClientService>();

        // 1. Setup registry and register handlers
        var registry = new McpToolRegistry();
        registry.Register(new SystemInfoToolHandler());

        // 2. Wrap registry in dispatcher
        var dispatcher = new McpToolDispatcher(registry);

        // 3. Configure client service connection
        await using var clientService = new McpClientService(
            relayUrl: "https://cloud-relay.verisflow.com/mcp",
            dispatcher: dispatcher,
            logger: logger,
            accessTokenProvider: () => Task.FromResult<string?>("your_auth_token_here")
        );

        // 4. Start connection to Cloud Relay
        await clientService.StartAsync();

        Console.WriteLine("VerisFlow MCP Client connected. Press Enter to exit...");
        Console.ReadLine();

        // 5. Stop connection
        await clientService.StopAsync();
    }
}

```

---

## 🏗️ Architecture & Processing Pipeline

`VerisFlow.Mcp.Client` bridges remote AI requests from Cloud Relay to local machine execution using the following workflow:

1. **Establish Connection**: `McpClientService` opens an active SignalR connection to the Cloud Relay endpoint with automatic reconnect logic enabled.
2. **Listen for Requests**: The service listens for server-side `ExecuteToolAsync` invocations containing `requestId`, `toolName`, and `argsJson`.
3. **Dispatch Tool Execution**: `McpToolDispatcher` queries `IMcpToolRegistry` for a matching `IMcpToolHandler`.
4. **Execute & Catch Exceptions**:
* If the tool exists, `ExecuteAsync` is called with raw JSON arguments.
* If the tool is missing or throws an exception, `McpToolDispatcher` intercepts the error and formats a JSON error payload with `isError = true`.


5. **Submit Results**: `McpClientService` invokes `SubmitToolResultAsync` on Cloud Relay, returning `requestId`, `resultJson`, and `isError` status.

---

## 📂 Namespace Overview

* **`VerisFlow.Mcp.Client`**: Core infrastructure containing `McpClientService` connection management, `IMcpToolHandler` interfaces, `IMcpToolRegistry` definitions, and `McpToolDispatcher` invocation logic.

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