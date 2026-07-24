# Changelog - VerisFlow.Mcp.Client

All notable changes to the `VerisFlow.Mcp.Client` package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-07-24

### Added
* Initial release of `VerisFlow.Mcp.Client`.
* SignalR hub connection lifecycle management via `McpClientService` with support for automatic reconnection and asynchronous access token authentication.
* Model Context Protocol (MCP) tool abstractions (`IMcpToolHandler`, `IMcpToolRegistry`, `IMcpToolDispatcher`) for extensible tool discovery and execution.
* Request dispatching logic implemented in `McpToolDispatcher`, featuring automatic exception catching and structured JSON error payload formatting.
* Communication protocol specifications (`McpProtocolMethods`) defining `ExecuteToolAsync` and `SubmitToolResultAsync` SignalR invocation contracts.