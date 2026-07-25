# Changelog - VerisFlow.LayParser.Core

All notable changes to the `VerisFlow.LayParser.Core` package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-07-25

### Added

* Initial release of the `VerisFlow.LayParser.Core` package at version 0.1.0.

* Introduced the `DeckLayoutParser` class to systematically parse Hamilton Venus deck layout (`.lay`) files.

* Added logic to automatically extract raw instance properties including IDs, relative and absolute file paths, and TForm space vectors.

* Implemented the `LabwareDataProcessor` to transform raw parsed elements into calculated, finalized deck coordinates (FinalX, FinalY, FinalZ).

* Enabled automatic assignment of `LabwareType` flags—such as Carrier, Rack, or Container—by inspecting definition file extensions like `.tml`, `.rck`, and `.ctr`.

* Integrated a file reading mechanism inside the processor to extract deep physical parameters like physical dimensions (Dx, Dy) and grid topologies from linked labware files.

* Built a fallback intelligence feature that assigns the `HoleCnt` variable to the `Rows` property if explicit row and column tags are missing from the configuration file.

* Designed comprehensive data models, including `LabwareInfo` and `ProcessedLabwareInfo`, to securely transport intermediate and finalized automation data structures.