# VerisFlow.VenusAuto.Core

**VerisFlow.VenusAuto.Core is a .NET library providing robust, non-intrusive background automation and monitoring for Hamilton Venus software applications, specifically targeting the Venus Run Control executable.**

Target Frameworks: `.NET Standard 2.0` | `.NET 8.0` | `.NET 9.0`

---

## Key Features

**Non-Intrusive Background Control**
The framework utilizes low-level Win32 message posting techniques to execute background UI interaction events without taking control of the physical mouse cursor. By calculating relative pixel coordinates within the client area, the service can safely trigger start, pause, and abort operations directly on the target application window.

**Smart Dialog Interception**
A dedicated dialog guard continuously scans the thread windows of the target process to identify blocking modal dialogs, specifically targeting the standard `#32770` dialog class. It automatically dismisses recoverable warnings by synthesizing silent ENTER keystrokes, while accurately flagging paused states or critical blocking errors based on the extracted dialog message text.

**Comprehensive System Status Snapshots**
Developers can asynchronously poll the engine to receive an immutable snapshot of the Venus system runtime status. The service evaluates the active application state, extracting raw status text from UI controls to determine if the system is Idle, Running, Paused, or in an Error state, while also retrieving the currently loaded method name directly from the window title.

---

## Quick Start

To begin using the library, install the package via the .NET CLI.

```bash
dotnet add package VerisFlow.VenusAuto.Core

```

Next, you must provide a configuration node in your `appsettings.json` file. This configuration defines the target executable path and the exact coordinate mapping for the interactive UI elements in the Run Control window.

```json
{
  "VenusAutomation": {
    "RunControlProcessName": "HxRun",
    "MethodEditorProcessName": "HxHSLMetEd",
    "RunControlExecutablePath": "C:\\Program Files (x86)\\HAMILTON\\Bin\\HxRun.exe",
    "RunControlUI": {
      "StartButton": { "X": 67, "Y": 22 },
      "PauseButton": { "X": 106, "Y": 20 },
      "AbortButton": { "X": 185, "Y": 20 },
      "StatusWindow": { "X": 268, "Y": 20 }
    }
  }
}

```

The following complete, independent console application demonstrates the standard initialization and execution flow. It configures the dependency injection container, resolves the Venus automation service, loads a designated method file, and enters a monitoring loop to output the real-time execution status.

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VerisFlow.VenusAuto.Core.Contracts;
using VerisFlow.VenusAuto.Core.Extensions;
using VerisFlow.VenusAuto.Core.Models;

namespace VerisFlow.VenusAuto.MinimalSample
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // Build the configuration pipeline to map the UI coordinate settings
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Set up the generic host and register the core Venus Automation dependencies
            using IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.AddVenusAutomation(configuration);
                })
                .Build();

            // Resolve the core orchestration service from the dependency injection container
            var venusService = host.Services.GetRequiredService<IVenusRunControlService>();
            using var cts = new CancellationTokenSource();
            
            Console.WriteLine("Starting Venus Automation Sequence...");

            try
            {
                // Ensure the target Venus Run Control process is actively running
                Console.WriteLine("Checking process state...");
                await venusService.EnsureProcessStartedAsync(cts.Token);

                // Arrange the primary application window to the right half of the active monitor
                Console.WriteLine("Arranging application window...");
                await venusService.ArrangeWindowAsync(WindowLayoutPreset.RightHalf, cancellationToken: cts.Token);

                // Automate loading a method file into Venus Run Control
                string targetMethodPath = @"C:\Methods\SampleMethod.hsl";
                Console.WriteLine($"Loading method from: {targetMethodPath}");
                await venusService.LoadMethodAsync(targetMethodPath, cts.Token);

                // Begin execution of the loaded method
                Console.WriteLine("Triggering start sequence...");
                await venusService.StartRunAsync(cts.Token);

                Console.WriteLine("Monitoring execution status. Press CTRL+C to exit.");
                
                Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    Console.WriteLine("\nCancellation requested by user.");
                    eventArgs.Cancel = true;
                    cts.Cancel();
                };

                // Enter a polling loop to evaluate the active application state
                while (!cts.Token.IsCancellationRequested)
                {
                    var status = await venusService.GetStatusAsync(cts.Token);

                    Console.WriteLine($"[State: {status.State}] " +
                                      $"Method: {status.LoadedMethodName ?? "None"} | " +
                                      $"Message: {status.RawStatusText}");

                    if (status.State == RunState.Error && status.HasErrorDialog)
                    {
                        Console.WriteLine($"CRITICAL ERROR DETECTED: {status.ErrorMessage}");
                        break;
                    }

                    await Task.Delay(2000, cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Automation sequence was cancelled.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("Attempting graceful shutdown...");
                await venusService.GracefulShutdownAsync(CancellationToken.None);
            }
        }
    }
}

```

---

## Architecture & Processing Pipeline

The Venus Auto framework processes instructions through a highly decoupled dependency injection architecture. It begins by reading the `VenusAutoOptions` from the configuration provider to secure the required process names and UI coordinates. When a command is issued, the `WindowOrchestrator` scans the desktop environment to locate the top-level interactive window owned by the target executable process. Once the window handle is secured, the `SilentSimulator` takes over, posting asynchronous Win32 messages to synthesize clicks or keyboard shortcuts directly into the application's message queue. Concurrently, the `DialogGuard` monitors the active process threads, extracting text from standard dialog controls to intercept blocking warnings and ensure uninterrupted execution flow.

---

## Namespace Overview

The `VerisFlow.VenusAuto.Core.Models` namespace contains the data structures required for operation, including the configuration options, pixel coordinate definitions, and the system status records.

The `VerisFlow.VenusAuto.Core.Contracts` namespace defines the primary service interfaces, exposing the core `IVenusRunControlService` that developers will interact with to drive the automation.

The `VerisFlow.VenusAuto.Core.Internal` namespace isolates the unmanaged Win32 P/Invoke signatures and the concrete implementations for window orchestration, silent simulation, and dialog interception.

The `VerisFlow.VenusAuto.Core.Extensions` namespace provides the necessary `IServiceCollection` extension methods to seamlessly register the framework components into standard modern .NET applications.

---

## Contributing Guidelines

Contributions to this project are highly encouraged. Please fork the repository and create a dedicated feature branch for your modifications. Commit your changes with descriptive messages and push the branch to your origin repository. Finally, open a Pull Request detailing the context and technical specifics of your proposed additions to facilitate a thorough review process.

---

## License

This project is licensed under the MIT License.