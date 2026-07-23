using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using VerisFlow.VenusAuto.Core.Contracts;
using VerisFlow.VenusAuto.Core.Internal;
using VerisFlow.VenusAuto.Core.Models;
using VerisFlow.VenusAuto.Core.Services;
using Xunit;

namespace VerisFlow.VenusAuto.Core.Tests;

/// <summary>
/// Unit test suite for validating automation workflows and window orchestration logic in <see cref="VenusRunControlService"/>.
/// </summary>
public class VenusRunControlServiceTests
{
    private readonly Mock<IWindowOrchestrator> _orchestratorMock;
    private readonly Mock<ISilentSimulator> _simulatorMock;
    private readonly Mock<IDialogGuard> _dialogGuardMock;
    private readonly IOptionsSnapshot<VenusAutoOptions> _options;

    /// <summary>
    /// Initializes test context, instantiating mock interfaces and configuring default options snapshot values.
    /// </summary>
    public VenusRunControlServiceTests()
    {
        _orchestratorMock = new Mock<IWindowOrchestrator>();
        _simulatorMock = new Mock<ISilentSimulator>();
        _dialogGuardMock = new Mock<IDialogGuard>();

        // Ensure Process.GetProcessesByName resolves a valid active process during test execution.
        var optionsConfig = new VenusAutoOptions
        {
            RunControlProcessName = System.Diagnostics.Process.GetCurrentProcess().ProcessName,
            RunControlUI = new AppCoordinates
            {
                StartButton = new RelativePoint { X = 100, Y = 200 },
                AbortButton = new RelativePoint { X = 300, Y = 400 }
            }
        };

        var optionsMock = new Mock<IOptionsSnapshot<VenusAutoOptions>>();
        optionsMock.Setup(m => m.Value).Returns(optionsConfig);
        _options = optionsMock.Object;
    }

    /// <summary>
    /// Helper factory method to construct an instance of <see cref="VenusRunControlService"/> with current mock instances and a null logger.
    /// </summary>
    /// <returns>A configured <see cref="VenusRunControlService"/> instance.</returns>
    private VenusRunControlService CreateService()
    {
        return new VenusRunControlService(
            _options,
            _orchestratorMock.Object,
            _simulatorMock.Object,
            _dialogGuardMock.Object,
            NullLogger<VenusRunControlService>.Instance);
    }

    /// <summary>
    /// Tests that <see cref="VenusRunControlService.StartRunAsync"/> throws an <see cref="InvalidOperationException"/>
    /// when the window orchestrator fails to locate a valid window handle (<see cref="IntPtr.Zero"/>).
    /// </summary>
    [Fact]
    public async Task StartRunAsync_ThrowsInvalidOperationException_WhenMainWindowNotFound()
    {
        // Arrange: Configure orchestrator mock to return zero handle, simulating a missing or non-interactive window
        _orchestratorMock.Setup(x => x.FindInteractiveWindowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IntPtr.Zero);

        var service = CreateService();

        // Act & Assert: Execute method and verify that an InvalidOperationException is thrown
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartRunAsync());

        // Assert: Verify that no user input or click simulation was executed against invalid window handles
        _simulatorMock.Verify(x => x.ClickRelativeAsync(It.IsAny<IntPtr>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    /// <summary>
    /// Tests that <see cref="VenusRunControlService.StartRunAsync"/> locates the target process window handle and triggers
    /// a relative UI click event using the coordinate configuration defined in options.
    /// </summary>
    [Fact]
    public async Task StartRunAsync_ExecutesClick_WithConfiguredCoordinates()
    {
        // Arrange: Mock orchestrator to return a dummy window handle for the configured target process name
        var dummyHwnd = (IntPtr)12345;
        _orchestratorMock.Setup(x => x.FindInteractiveWindowAsync(_options.Value.RunControlProcessName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dummyHwnd);

        var service = CreateService();

        // Act: Trigger run control process execution
        await service.StartRunAsync();

        // Assert: Validate click simulation execution with exact window handle and start button relative coordinates
        _simulatorMock.Verify(x => x.ClickRelativeAsync(
            dummyHwnd,
            _options.Value.RunControlUI.StartButton.X,
            _options.Value.RunControlUI.StartButton.Y),
            Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="VenusRunControlService.ResumeRunAsync"/> waits for the "Execution paused" dialog window
    /// and invokes a click action on the dialog's "Resume" button once detected.
    /// </summary>
    [Fact]
    public async Task ResumeRunAsync_ClicksResume_WhenDialogIsDetected()
    {
        // Arrange: Configure dialog guard mock to return a dummy handle upon finding the expected dialog title
        var dummyDialogHwnd = (IntPtr)54321;

        _dialogGuardMock.Setup(x => x.WaitForDialogAsync(It.IsAny<int>(), "Execution paused", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dummyDialogHwnd);

        var service = CreateService();

        // Act: Execute resume workflow
        await service.ResumeRunAsync();

        // Assert: Ensure button click simulation was dispatched specifically for the "Resume" text button on the target dialog
        _simulatorMock.Verify(x => x.ClickButtonByTextAsync(dummyDialogHwnd, "Resume"), Times.Once);
    }
}