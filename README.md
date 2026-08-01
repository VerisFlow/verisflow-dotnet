# VerisFlow .NET Framework & Solution

Welcome to the **verisflow-dotnet** repository. These tools are .NET tailored for Hamilton Venus liquid handling automation, physical deck layout analytics, robotic trace execution auditing, and remote AI tool orchestration via Model Context Protocol (MCP) over SignalR and SSE.

---

## 🏛️ System Architecture

The following diagram illustrates how VerisFlow connects Web AI platforms (such as Claude and ChatGPT) with local laboratory automation hardware and processing engines:

```text
[ Web AI Clients / Assistants ]
     (Claude, ChatGPT, LLMs)
                │
                │ OAuth2 / SSE (JSON-RPC)
                ▼
┌─────────────────────────────────┐
│   VerisFlow.Mcp.Server.Sample   │ (Cloud Relay Gateway Service)
│   (ASP.NET Core / Web API Hub)  │
└────────────────┬────────────────┘
                 │
                 │ SignalR WebSocket (Encrypted Stream)
                 ▼
┌─────────────────────────────────┐
│   VerisFlow.Mcp.Client.Sample   │ (Local Lab WPF Agent)
│    (WPF Desktop Agent App)      │
└────────────────┬────────────────┘
                 │
  ┌──────────────┼──────────────────────────────┐
  │              │                              │
  ▼              ▼                              ▼
┌───────────────────────────┐ ┌───────────────────────────┐ ┌───────────────────────────┐
│  VenusDeckParser.Core     │ │      VenusAuto.Core       │ │     TraceLogic.Core       │
│ (3D Spatial Geometry)     │ │ (Background Automation)   │ │ (Trace Log Reconstruction)│
└───────────────────────────┘ └───────────────────────────┘ └───────────────────────────┘

```

---

## 📂 Repository Structure & Project Inventory

This monorepo isolates production-ready NuGet packages, desktop GUI inspector applications, diagnostic testbeds, and cloud gateway microservices.

### 📦 Core NuGet Libraries (`src/`)

* **`VerisFlow.LayParser.Core`**: Engine for parsing Hamilton Venus deck layout (`.lay`) files and resolving linked labware templates (`.tml`, `.rck`, `.ctr`). Calculates absolute 3D spatial coordinates ($FinalX, FinalY, FinalZ$), grid layouts, and labware types. Target Frameworks: `.NET Standard 2.0` | `.NET 8.0` | `.NET 9.0`.
* **`VerisFlow.TraceLogic.Core`**: File locator, line parser, and pipetting event reconstructor for Hamilton Venus trace log (`.trc`) files. Grouping raw actions into `PipettingStep` and `LiquidTransfer` records with CSV export support. Target Frameworks: `.NET Standard 2.0` | `.NET 8.0` | `.NET 9.0`.
* **`VerisFlow.VenusAuto.Core`**: Non-intrusive background UI automation library for `HxRun.exe`. Post Win32 background messages without taking cursor control, intercepts modal blocking dialogs (`#32770`), and captures asynchronous runtime state snapshots. Target Frameworks: `.NET Standard 2.0` | `.NET 8.0` | `.NET 9.0`.
* **`VerisFlow.Mcp.Client`**: Core infrastructure establishing real-time SignalR WebSocket connections with Cloud Relay gateways. Features extensible tool handlers (`IMcpToolHandler`, `IMcpToolDispatcher`) and automatic exception handling. Target Frameworks: `.NET Standard 2.0` | `.NET 8.0` | `.NET 9.0`.
* **`VerisFlow.Mcp.Server`**: Server-side infrastructure for Server-Sent Events (SSE) session streaming (`McpSseConnectionManager`) and lock-free asynchronous request correlation (`McpCoordinator`). Target Frameworks: `.NET Standard 2.0` | `.NET 8.0` | `.NET 9.0`.

### 🚀 Applications & Services (`samples/` & `apps/`)

* **`VerisFlow.LayParser.Desktop`**: High-performance WPF desktop application for dragging/dropping `.lay` files, inspecting spatial deck hierarchies, filtering along $X/Y$ tracks, and generating automated standalone Markdown layout reports. Target Frameworks: `.NET 8.0` | `.NET 9.0` (WPF).
* **`VerisFlow.Mcp.Client.Sample`**: WPF lab desktop agent application connecting to Cloud Relay. Includes MSAL Azure AD authentication, Dev/Prod environment toggles, WPF UI logging, and pre-built MCP tool handlers for Venus control and trace log analytics. Target Frameworks: `.NET 8.0` | `.NET 9.0` (WPF).
* **`VerisFlow.Mcp.Server.Sample`**: ASP.NET Core Web API acting as a Cloud Relay Gateway. Exposes OAuth2 proxy endpoints, handles MCP version `2024-11-05` over SSE (`/mcp/sse`) and JSON-RPC (`/mcp/messages`), and relays messages to WPF agents over SignalR (`/mcphub`). Target Frameworks: `.NET 8.0` | `.NET 9.0` (ASP.NET Core).
* **`VerisFlow.VenusAuto.Sample`**: WPF diagnostic testbed and developer playground. Supports global **F2** hotkey inspection of UI elements, screen-to-client coordinate calculation, silent Win32 click simulation testing, and live Venus run control operations. Target Frameworks: `.NET 8.0` | `.NET 9.0` (WPF).

---

## ✨ Key Feature Pillars

### 1. Spatial Layout Geometry & 3D Coordinates

Parse `.lay` binary and text configurations into strongly typed objects. Transform raw `TForm3` vectors and `ZTrans` offsets into absolute $FinalX, FinalY, FinalZ$ spatial dimensions, resolving grid dimensions ($Rows, Columns$) and physical sizing ($D_x, D_y$).

### 2. Silent Win32 Automation & Dialog Guard

Drive Hamilton `HxRun.exe` operations in the background via low-level Win32 `PostMessage` API. Perform Start, Pause, Resume, Abort, and Method Load operations asynchronously while an internal `DialogGuard` monitors and clears blocking modal dialogs.

### 3. Trace Telemetry & Liquid Transfer Auditing

Locate trace files by date/time windows, parse raw execution records, and pair Tip Pick Up, Aspirate, Dispense, and Tip Eject actions into structured liquid handling event streams ready for CSV or database exporting.

### 4. Cloud AI Model Context Protocol (MCP) Bridge

Enable Web AI assistants (such as Claude and ChatGPT) to execute local lab tools securely. The solution uses SSE streaming and SignalR WebSocket tunneling to bypass local NAT and firewall constraints without exposing local ports.

---

## 🛠 Building & Local Packaging

### Prerequisites

* **.NET SDK**: Install .NET 8.0 SDK or .NET 9.0 SDK.

### Build the Solution

Restore dependencies and compile all core libraries, desktop tools, and web services using the `.slnx` solution structure:

```bash
dotnet restore
dotnet build VerisFlow.Libraries.slnx -c Release

```

### Pack Core NuGet Libraries

To generate local `.nupkg` and `.snupkg` package artifacts for all five core libraries:

```bash
dotnet pack src/VerisFlow.VenusDeckParser.Core/VerisFlow.VenusDeckParser.Core.csproj -c Release -o ./artifacts
dotnet pack src/VerisFlow.TraceLogic.Core/VerisFlow.TraceLogic.Core.csproj -c Release -o ./artifacts
dotnet pack src/VerisFlow.VenusAuto.Core/VerisFlow.VenusAuto.Core.csproj -c Release -o ./artifacts
dotnet pack src/VerisFlow.Mcp.Client/VerisFlow.Mcp.Client.csproj -c Release -o ./artifacts
dotnet pack src/VerisFlow.Mcp.Server/VerisFlow.Mcp.Server.csproj -c Release -o ./artifacts

```

---

## ⚙️ Engineering Standards

This repository enforces unified C# development policies across all projects:

1. **Central Package Management (CPM)**: Third-party dependencies and version definitions are declared centrally in `Directory.Packages.props`. Project files reference packages without explicit version tags.
2. **Global Build Properties**: Shared metadata, language versions, `Nullable` checks, and build options are inherited from `Directory.Build.props`.
3. **Source Link & Reproducible Builds**: Configured with `PublishRepositoryUrl` and `ContinuousIntegrationBuild` to support source-level debugging for NuGet package users.
4. **Deterministic Package Feeds**: `nuget.config` restricts package resolution sources to prevent dependency confusion vulnerabilities.

---

## 📄 License

This repository is licensed under the **MIT License**.
