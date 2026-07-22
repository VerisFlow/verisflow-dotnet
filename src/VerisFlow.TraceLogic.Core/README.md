# VerisFlow.TraceLogic.Core

**VerisFlow.TraceLogic.Core is a .NET library designed to locate, parse, and export Hamilton Venus trace log files (`.trc`), transforming raw robotic execution logs into structured liquid transfer events.**

Target Frameworks: `.NET Standard 2.0` | `.NET 8.0` | `.NET 9.0`

---

## ✨ Key Features

* **File Discovery**: A dedicated `TraceLocator` scans system directories for `.trc` files, applying precise time-based filters to locate target logs.
* **Pipetting Reconstruction**: Couples related operations—such as Tip Pick Up, Aspirate, Dispense, and Tip Eject—into logical `PipettingStep` and `LiquidTransfer` models.
* **CSV Data Exporting**: Provides an `ITraceDataExporter` service to write custom-selected columns to physical files or memory streams.

---

## 🚀 Quick Start

### Installation

```bash
dotnet add package VerisFlow.TraceLogic.Core

```

### Dependency Injection & Parsing Example

```csharp
using Microsoft.Extensions.DependencyInjection;
using TraceLogic.Core;
using TraceLogic.Core.Exporting;
using TraceLogic.Core.Interfaces;
using TraceLogic.Core.Models;

// 1. Configure services
var services = new ServiceCollection();
services.AddLogging();
services.AddTraceLogic();
var serviceProvider = services.BuildServiceProvider();

// 2. Resolve core services
var parser = serviceProvider.GetRequiredService<ITraceFileParser>();
var exporter = serviceProvider.GetRequiredService<ITraceDataExporter>();

// 3. Parse a .trc file
TraceAnalysisResult result = parser.Parse(@"C:\Hamilton\Log\RunTrace.trc");

if (result.Errors.Count > 0)
{
    Console.WriteLine($"Parsing failed: {string.Join(", ", result.Errors)}");
    return;
}

// 4. Iterate structured liquid transfer events
foreach (var transfer in result.LiquidTransfers)
{
    Console.WriteLine($"[CH{transfer.ChannelId}] {transfer.SourceLabware}:{transfer.SourcePositionId} -> {transfer.TargetLabware}:{transfer.TargetPositionId} | {transfer.Volume} µL");
}

```

### Exporting Selected Data to CSV

```csharp
// Define dynamic column mappings for export
var columnsToExport = new List<ExportColumnInfo>
{
    new ExportColumnInfo { Header = "Timestamp", PropertyName = nameof(LiquidTransfer.Timestamp) },
    new ExportColumnInfo { Header = "Source Labware", PropertyName = nameof(LiquidTransfer.SourceLabware) },
    new ExportColumnInfo { Header = "Target Labware", PropertyName = nameof(LiquidTransfer.TargetLabware) },
    new ExportColumnInfo { Header = "Volume (uL)", PropertyName = nameof(LiquidTransfer.Volume) },
    new ExportColumnInfo { Header = "Channel", PropertyName = nameof(LiquidTransfer.ChannelId) }
};

// Export to file
exporter.Export(result.LiquidTransfers, columnsToExport, @"C:\Export\LiquidTransfers.csv");

```

---

## 🏗️ Architecture & Processing Pipeline

TraceLogic processes Hamilton Venus log files through a structured data pipeline:

1. **Locate**: `TraceLocator` searches system or custom target directories for `.trc` log files matching time window conditions.
2. **Parse Lines**: `TraceFileParser` streams log entries line-by-line, parsing raw log lines into strongly typed `TraceEntry` objects.
3. **Aggregate Steps**: Identifies start and completion boundaries for channel operations, grouping them into `PipettingStep` objects containing duration and channel actions.
4. **Synthesize Transfers**: Tracks state across liquid handling actions to generate chronological `LiquidTransfer` records matching source aspirates to target dispenses.
5. **Export**: `CsvDataExporter` extracts configured properties from liquid transfer models and outputs formatted CSV data.

---

## 📂 Namespace Overview

* **`TraceLogic.Core`**: Contains extension methods like `AddTraceLogic()` for DI container registration.
* **`TraceLogic.Core.Interfaces`**: Defines primary abstraction contracts including `ITraceFileParser`, `ITraceDataExporter`, and `ITraceLocator`.
* **`TraceLogic.Core.Models`**: Houses result containers (`TraceAnalysisResult`), transfer entries (`LiquidTransfer`), and raw log representations (`TraceEntry`).
* **`TraceLogic.Core.Exporting`**: Infrastructure for data transformation and CSV file generation.

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