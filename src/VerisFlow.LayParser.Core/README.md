# VerisFlow.VenusDeckParser.Core

VerisFlow.VenusDeckParser.Core is a .NET library designed to parse and process Hamilton Venus deck layout (`.lay`) files. It transforms raw layout configurations into structured, calculated labware data to facilitate automated liquid handling intelligence.

Target Frameworks: `.NET Standard 2.0` | `.NET 8.0` | `.NET 9.0`

---

## ✨ Key Features

**Deck Layout Parsing**
The `DeckLayoutParser` reads raw `.lay` files to extract precise labware instance information. It captures essential properties such as file paths, IDs, SiteIDs, Templates, and 3D TForm vectors (X, Y, Z) using robust regular expressions.

**Comprehensive Data Processing**
The `LabwareDataProcessor` converts the extracted raw data into final actionable coordinates. It isolates TForm vectors and incorporates ZTrans values to compute the absolute `FinalX`, `FinalY`, and `FinalZ` coordinates for every element on the deck.

**Intelligent Labware Resolution**
The library automatically determines the `LabwareType` (Carrier, RackCarrier, Rack, Container) by analyzing underlying file extensions like `.tml`, `.rck`, and `.ctr`. It scans these linked property files to extract physical dimensions (Dx, Dy), grid layouts (Rows, Columns, AlphaIndex), and identifies specific capabilities such as whether a piece of labware is a loadable carrier or functions as a Tip Rack.

---

## 🚀 Quick Start

### Installation

```bash
dotnet add package VerisFlow.VenusDeckParser.Core

```

### Parsing and Processing Example

The following self-contained code demonstrates how to read a layout file and process the data to calculate absolute positions and labware types. You can copy and run this directly in your environment.

```csharp
using System;
using System.Collections.Generic;
using VerisFlow.VenusDeckParser.Core;

public class Program
{
    public static void Main()
    {
        // 1. Define the path to your Hamilton Venus deck layout file
        string deckLayoutFilePath = @"C:\Hamilton\Methods\MyDeckLayout.lay";

        // 2. Parse the raw layout file to extract base properties and TForm vectors
        List<LabwareInfo> rawLabwareData = DeckLayoutParser.GetLabwareInfo(deckLayoutFilePath);

        if (rawLabwareData.Count == 0)
        {
            Console.WriteLine("No labware found or file could not be read.");
            return;
        }

        // 3. Process the raw data to calculate final coordinates and resolve labware properties
        List<ProcessedLabwareInfo> processedDeck = LabwareDataProcessor.Process(rawLabwareData);

        // 4. Output the processed layout information for downstream execution
        foreach (var labware in processedDeck)
        {
            Console.WriteLine($"Labware ID: {labware.Id} | Type: {labware.LabwareType}");
            Console.WriteLine($"Location: X={labware.FinalX:F3}, Y={labware.FinalY:F3}, Z={labware.FinalZ:F3}");
            Console.WriteLine($"Grid: {labware.Column} Columns x {labware.Row} Rows");
            Console.WriteLine($"Is Tip Rack: {labware.TipRack} | Loadable: {labware.Loadable}");
            Console.WriteLine(new string('-', 40));
        }
    }
}

```

---

## 🏗️ Architecture & Processing Pipeline

The parser operates through a sequential data pipeline to guarantee data completeness and structural integrity.

**1. Locate & Extract**
The `DeckLayoutParser.GetLabwareInfo` method streams the text of the `.lay` file to locate instance counts. It handles relative file paths automatically by appending them to the default Hamilton directory `C:\Program Files (x86)\HAMILTON\LabWare\` if a root path is missing.

**2. Calculate Coordinates**
Inside `LabwareDataProcessor.Process`, the system calculates the `FinalX` and `FinalY` positional values from the parsed `TForm3` properties, while `FinalZ` is mapped directly from the `ZTrans` property.

**3. Deep Property Inspection**
To retrieve dimensional metadata, the processor reads the target file linked to each labware instance using the `ReadLabwareProperties` method. It attempts to parse `Rows` and `Columns` natively; if this explicit grid data is missing from the file definition, the logic dynamically falls back to the `HoleCnt` property to assign the row count.

---

## 📂 Core Models & Enums

**`ProcessedLabwareInfo`**
The primary output model containing all calculated properties for a given component, including dimensional sizing (`Dx`, `Dy`), layout dimensions (`Row`, `Column`), and state flags (`AlphaIndex` and `TipRack`).

**`LabwareType`**
An enumeration classifying the target element strictly as `Carrier`, `RackCarrier`, `Rack`, `Container`, or `Unknown`.

**`TFormVector`**
Represents 3D space vectors (`X`, `Y`, `Z`) utilized for internal structural parsing and intermediate raw data representation.

---

## 🤝 Contributing Guidelines

Contributions are highly encouraged to expand the parsing capabilities and intelligence of the library.

1. Fork the repository.
2. Create a feature branch (`git checkout -b feature/AdvancedParsing`).
3. Commit your changes containing detailed English code comments (`git commit -m 'Add AdvancedParsing'`).
4. Push to the branch (`git push origin feature/AdvancedParsing`).
5. Open a Pull Request detailing the enhancements and underlying logic.

---

## 📄 License

This project is licensed under the **MIT License**.