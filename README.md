# VerisFlow .NET Libraries

Welcome to the **verisflow-dotnet** repository. This monorepo houses a suite of .NET libraries and developer tooling tailored for laboratory automation, robotic liquid handler log analytics, and execution event data pipelines.

---

## 🏛 Repository Structure

The solution isolates core library modules from desktop sample applications, maintaining shared engineering standards across all projects.

* **`src/`**: Production-ready core libraries published to NuGet.
  * **`VerisFlow.TraceLogic.Core`**: Parser, locator, and exporter for Hamilton Venus trace log (`.trc`) files. Supports asynchronous streaming and zero-allocation regex algorithms. (See [Package README](src/VerisFlow.TraceLogic.Core/README.md) | [Changelog](src/VerisFlow.TraceLogic.Core/CHANGELOG.md))
* **`samples/`**: Sample applications and UI testbeds.
  * **`TraceLogic.Sample`**: WPF desktop application demonstrating real-world integration, UI data binding, and CSV export functionality using `VerisFlow.TraceLogic.Core`.

---

## 🛠 Building & Testing

### Prerequisites

* **.NET SDK**: .NET 8.0 SDK or .NET 9.0 SDK installed on your system.

### Build the Solution

Restore dependencies and compile all projects using the `.slnx` solution format:

```bash
dotnet restore
dotnet build VerisFlow.Libraries.slnx -c Release

```

### Local Packaging

To build and generate local `.nupkg` and `.snupkg` package artifacts:

```bash
dotnet pack src/VerisFlow.TraceLogic.Core/VerisFlow.TraceLogic.Core.csproj -c Release -o ./artifacts

```

---

## ⚙️ Engineering Standards

This repository enforces consistent C# coding standards and deterministic build configurations:

1. **Central Package Management (CPM)**: Package versions are defined exclusively in `Directory.Packages.props`. Individual `.csproj` files only reference package names without version numbers.
2. **Global Build Properties**: Shared properties such as `Nullable`, `ImplicitUsings`, `LangVersion`, `IncludeSymbols`, and `PackageLicenseExpression` are inherited from `Directory.Build.props`.
3. **Source Link & Reproducible Builds**: Configured with `PublishRepositoryUrl`, `EmbedUntrackedSources`, and `ContinuousIntegrationBuild` to ensure a seamless debugging experience for package consumers.
4. **Deterministic Feeds**: `nuget.config` explicitly maps package patterns to trusted sources to prevent dependency confusion attacks.

---

## 📄 License

This repository is licensed under the MIT License.