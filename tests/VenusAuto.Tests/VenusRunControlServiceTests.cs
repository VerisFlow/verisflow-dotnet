using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using VerisFlow.VenusAuto.Core.Internal;
using VerisFlow.VenusAuto.Core.Models;
using VerisFlow.VenusAuto.Core.Services;
using Xunit;

namespace VerisFlow.VenusAuto.Core.Tests;

/// <summary>
/// Tests of <see cref="VenusRunControlService"/> with Win32 access mocked. The process name is the test process,
/// so Run Control is "running" for every test.
/// </summary>
public class VenusRunControlServiceTests
{
    private static readonly IntPtr MainWindow = (IntPtr)12345;

    private readonly Mock<IWindowOrchestrator> _orchestratorMock = new();
    private readonly Mock<ISilentSimulator> _simulatorMock = new();
    private readonly Mock<IWindowMessenger> _messengerMock = new();
    private readonly Mock<IDialogGuard> _dialogGuardMock = new();

    public VenusRunControlServiceTests()
    {
        _dialogGuardMock
            .Setup(x => x.GetDialogs(It.IsAny<int>(), It.IsAny<IntPtr>()))
            .Returns(Array.Empty<VenusDialogInfo>());

        _dialogGuardMock
            .Setup(x => x.WaitForCloseAsync(It.IsAny<IntPtr>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _messengerMock.Setup(x => x.PostCommand(It.IsAny<IntPtr>(), It.IsAny<int>())).Returns(true);
        _messengerMock.Setup(x => x.ClickButton(It.IsAny<IntPtr>(), It.IsAny<int>())).Returns(true);
        _messengerMock.Setup(x => x.GetText(It.IsAny<IntPtr>())).Returns(string.Empty);
    }

    private static VenusAutoOptions DefaultOptions() => new()
    {
        RunControlProcessName = System.Diagnostics.Process.GetCurrentProcess().ProcessName,
        RunControlUI = new AppCoordinates
        {
            StartButton = new RelativePoint { X = 100, Y = 200 },
            AbortButton = new RelativePoint { X = 300, Y = 400 },
            PauseButton = new RelativePoint { X = 500, Y = 600 }
        }
    };

    private VenusRunControlService CreateService(VenusAutoOptions? options = null)
    {
        var optionsMock = new Mock<IOptionsSnapshot<VenusAutoOptions>>();
        optionsMock.Setup(m => m.Value).Returns(options ?? DefaultOptions());

        return new VenusRunControlService(
            optionsMock.Object,
            _orchestratorMock.Object,
            _simulatorMock.Object,
            _messengerMock.Object,
            _dialogGuardMock.Object,
            NullLogger<VenusRunControlService>.Instance);
    }

    private void MainWindowIsOpen()
        => _orchestratorMock
            .Setup(x => x.FindInteractiveWindowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MainWindow);

    private static VenusDialogInfo Dialog(long handle, VenusDialogKind kind, string fingerprint, params int[] buttonIds)
        => new(
            handle,
            MainWindow.ToInt64(),
            kind,
            "Title",
            "Message",
            buttonIds.Select(id => new VenusDialogButton(id, $"Button {id}", true, false)).ToList(),
            Array.Empty<VenusDialogOption>(),
            true,
            fingerprint);

    [Fact]
    public async Task StartRunAsync_Throws_WhenMainWindowNotFound()
    {
        _orchestratorMock
            .Setup(x => x.FindInteractiveWindowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IntPtr.Zero);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().StartRunAsync(TestContext.Current.CancellationToken));

        _messengerMock.Verify(x => x.PostCommand(It.IsAny<IntPtr>(), It.IsAny<int>()), Times.Never);
        _simulatorMock.Verify(x => x.ClickRelativeAsync(It.IsAny<IntPtr>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task StartRunAsync_PostsStartCommand()
    {
        MainWindowIsOpen();
        _messengerMock.Setup(x => x.IsCommandEnabled(MainWindow, 32795)).Returns(true);

        await CreateService().StartRunAsync(TestContext.Current.CancellationToken);

        _messengerMock.Verify(x => x.PostCommand(MainWindow, 32795), Times.Once);
        _simulatorMock.Verify(x => x.ClickRelativeAsync(It.IsAny<IntPtr>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task StartRunAsync_Throws_WhenStartIsDisabled()
    {
        MainWindowIsOpen();
        _messengerMock.Setup(x => x.IsCommandEnabled(MainWindow, 32795)).Returns(false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().StartRunAsync(TestContext.Current.CancellationToken));

        _messengerMock.Verify(x => x.PostCommand(It.IsAny<IntPtr>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task StartRunAsync_Throws_WhenDialogBlocksRunControl()
    {
        MainWindowIsOpen();
        _dialogGuardMock
            .Setup(x => x.GetDialogs(It.IsAny<int>(), MainWindow))
            .Returns(new[] { Dialog(777, VenusDialogKind.Unknown, "fp", 1) });

        var exception = await Assert.ThrowsAsync<VenusDialogPendingException>(() => CreateService().StartRunAsync(TestContext.Current.CancellationToken));

        Assert.Single(exception.Dialogs);
        _messengerMock.Verify(x => x.PostCommand(It.IsAny<IntPtr>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task StartRunAsync_ClicksCoordinates_WhenNoCommandIdIsConfigured()
    {
        MainWindowIsOpen();
        var options = DefaultOptions();
        options.RunControlIds.StartCommand = 0;

        await CreateService(options).StartRunAsync(TestContext.Current.CancellationToken);

        _simulatorMock.Verify(x => x.ClickRelativeAsync(MainWindow, 100, 200), Times.Once);
        _messengerMock.Verify(x => x.PostCommand(It.IsAny<IntPtr>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task PauseRunAsync_PostsPauseCommand_WhenRunning()
    {
        MainWindowIsOpen();
        _messengerMock.Setup(x => x.IsCommandEnabled(MainWindow, 32796)).Returns(true);

        await CreateService().PauseRunAsync(TestContext.Current.CancellationToken);

        _messengerMock.Verify(x => x.PostCommand(MainWindow, 32796), Times.Once);
        _simulatorMock.Verify(x => x.ClickRelativeAsync(It.IsAny<IntPtr>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task PauseRunAsync_NoOps_WhenAlreadyPaused()
    {
        MainWindowIsOpen();
        var paused = Dialog(555, VenusDialogKind.Paused, "fp", 217, 211);
        _dialogGuardMock.Setup(x => x.GetDialogs(It.IsAny<int>(), MainWindow)).Returns(new[] { paused });

        await CreateService().PauseRunAsync(TestContext.Current.CancellationToken);

        _messengerMock.Verify(x => x.PostCommand(It.IsAny<IntPtr>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ResumeRunAsync_PressesResume_InPauseDialog()
    {
        MainWindowIsOpen();
        var paused = Dialog(555, VenusDialogKind.Paused, "fp", 217, 211);
        _dialogGuardMock.Setup(x => x.GetDialogs(It.IsAny<int>(), MainWindow)).Returns(new[] { paused });

        await CreateService().ResumeRunAsync(TestContext.Current.CancellationToken);

        _messengerMock.Verify(x => x.ClickButton((IntPtr)555, 217), Times.Once);
    }

    [Fact]
    public async Task ResumeRunAsync_Throws_WhenNotPaused()
    {
        MainWindowIsOpen();

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().ResumeRunAsync(TestContext.Current.CancellationToken));

        _messengerMock.Verify(x => x.ClickButton(It.IsAny<IntPtr>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task AbortRunAsync_WithoutConfirm_LeavesConfirmationOpen()
    {
        MainWindowIsOpen();
        var confirmation = Dialog(888, VenusDialogKind.AbortConfirmation, "fp", 220, 2);
        _messengerMock.Setup(x => x.IsCommandEnabled(MainWindow, 32798)).Returns(true);
        _dialogGuardMock
            .Setup(x => x.WaitForDialogAsync(It.IsAny<int>(), MainWindow, It.IsAny<Func<VenusDialogInfo, bool>>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(confirmation);

        var result = await CreateService().AbortRunAsync(confirm: false, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Aborted);
        Assert.Same(confirmation, result.PendingConfirmation);
        _messengerMock.Verify(x => x.PostCommand(MainWindow, 32798), Times.Once);
        _messengerMock.Verify(x => x.ClickButton(It.IsAny<IntPtr>(), 220), Times.Never);
    }

    [Fact]
    public async Task AbortRunAsync_WithConfirm_PressesConfirm()
    {
        MainWindowIsOpen();
        var confirmation = Dialog(888, VenusDialogKind.AbortConfirmation, "fp", 220, 2);
        _messengerMock.Setup(x => x.IsCommandEnabled(MainWindow, 32798)).Returns(true);
        _dialogGuardMock
            .Setup(x => x.WaitForDialogAsync(It.IsAny<int>(), MainWindow, It.IsAny<Func<VenusDialogInfo, bool>>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(confirmation);

        var result = await CreateService().AbortRunAsync(confirm: true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Aborted);
        _messengerMock.Verify(x => x.ClickButton((IntPtr)888, 220), Times.Once);
    }

    [Fact]
    public async Task AbortRunAsync_WhilePaused_UsesPauseDialog()
    {
        MainWindowIsOpen();
        var paused = Dialog(555, VenusDialogKind.Paused, "fp", 217, 211);

        // Sequence: Initial call detects the paused dialog; subsequent call within GetStatusAsync reflects the dialog closed upon abort.
        _dialogGuardMock
            .SetupSequence(x => x.GetDialogs(It.IsAny<int>(), MainWindow))
            .Returns(new[] { paused })
            .Returns(Array.Empty<VenusDialogInfo>());

        _dialogGuardMock
            .Setup(x => x.WaitForDialogAsync(It.IsAny<int>(), MainWindow, It.IsAny<Func<VenusDialogInfo, bool>>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VenusDialogInfo?)null);

        var result = await CreateService().AbortRunAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Aborted);
        _messengerMock.Verify(x => x.ClickButton((IntPtr)555, 211), Times.Once);
        _messengerMock.Verify(x => x.PostCommand(It.IsAny<IntPtr>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task AbortRunAsync_WhenBlockingDialogHasAbortButton_ClicksAbortButtonOnDialog()
    {
        MainWindowIsOpen();
        var errorWithAbort = new VenusDialogInfo(
            666,
            MainWindow.ToInt64(),
            VenusDialogKind.Unknown,
            "Error",
            "Hardware Error",
            new List<VenusDialogButton> { new(101, "Abort", true, false) },
            Array.Empty<VenusDialogOption>(),
            true,
            "fp");

        _dialogGuardMock
            .SetupSequence(x => x.GetDialogs(It.IsAny<int>(), MainWindow))
            .Returns(new[] { errorWithAbort })
            .Returns(Array.Empty<VenusDialogInfo>());

        _dialogGuardMock
            .Setup(x => x.WaitForDialogAsync(It.IsAny<int>(), MainWindow, It.IsAny<Func<VenusDialogInfo, bool>>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VenusDialogInfo?)null);

        var result = await CreateService().AbortRunAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Aborted);
        _messengerMock.Verify(x => x.ClickButton((IntPtr)666, 101), Times.Once);
        _messengerMock.Verify(x => x.PostCommand(It.IsAny<IntPtr>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsStatusAndLoadedMethod()
    {
        MainWindowIsOpen();
        _messengerMock.Setup(x => x.GetText(MainWindow)).Returns("Hamilton Run Control - TestMethod.med");
        _messengerMock.Setup(x => x.IsCommandEnabled(MainWindow, 32796)).Returns(true);

        var status = await CreateService().GetStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal("TestMethod.med", status.LoadedMethodName);
        Assert.Equal(RunState.Running, status.State);
    }

    [Fact]
    public async Task GetDialogsAsync_ReturnsDialogsFromGuard()
    {
        MainWindowIsOpen();
        var dialog = Dialog(111, VenusDialogKind.Unknown, "fp", 1);
        _dialogGuardMock.Setup(x => x.GetDialogs(It.IsAny<int>(), MainWindow)).Returns(new[] { dialog });

        var dialogs = await CreateService().GetDialogsAsync(TestContext.Current.CancellationToken);

        Assert.Single(dialogs);
        Assert.Equal(111, dialogs[0].Handle);
    }

    [Fact]
    public async Task RespondToDialogAsync_Throws_WhenDialogChanged()
    {
        MainWindowIsOpen();
        _dialogGuardMock
            .Setup(x => x.Inspect(It.IsAny<IntPtr>(), It.IsAny<int>(), It.IsAny<IntPtr>()))
            .Returns(Dialog(999, VenusDialogKind.Unknown, "current", 1, 2));

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().RespondToDialogAsync(999, 1, "stale", cancellationToken: TestContext.Current.CancellationToken));

        _messengerMock.Verify(x => x.ClickButton(It.IsAny<IntPtr>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task RespondToDialogAsync_PressesButton_WhenFingerprintMatches()
    {
        MainWindowIsOpen();
        _dialogGuardMock
            .Setup(x => x.Inspect(It.IsAny<IntPtr>(), It.IsAny<int>(), It.IsAny<IntPtr>()))
            .Returns(Dialog(999, VenusDialogKind.Unknown, "current", 1, 2));

        var response = await CreateService().RespondToDialogAsync(999, 2, "current", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(response.DialogClosed);
        _messengerMock.Verify(x => x.ClickButton((IntPtr)999, 2), Times.Once);
    }
}
