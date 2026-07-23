# Changelog - VerisFlow.VenusAuto.Core

All notable changes to the `VerisFlow.VenusAuto.Core` package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-07-23

### Added

* Initial release of `VerisFlow.VenusAuto.Core`.
* Non-intrusive background automation engine for Hamilton Venus software using Win32 P/Invoke messaging.
* Core `IVenusRunControlService` interface and implementation for execution control (`Start`, `Pause`, `Resume`, `Abort`, `LoadMethod`).
* Window layout arrangement (`ArrangeWindowAsync`) and process startup verification (`EnsureProcessStartedAsync`).
* Automated modal dialog detection and interception (`IDialogGuard`) for auto-dismissing benign warnings and capturing critical error dialogs.