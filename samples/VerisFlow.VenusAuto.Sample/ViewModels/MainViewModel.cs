using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using VerisFlow.VenusAuto.Core.Contracts;
using VerisFlow.VenusAuto.Core.Models;
using VerisFlow.VenusAuto.Sample.Models;
using VerisFlow.VenusAuto.Sample.Native;

namespace VerisFlow.VenusAuto.Sample.ViewModels
{
    /// <summary>
    /// ViewModel managing UI automation probing, Win32 action testing, and interaction with the Venus execution engine.
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        private CaptureSnapshot? _currentCapture;
        private string _testInputText = string.Empty;
        private readonly IVenusRunControlService _venusService;

        /// <summary>
        /// Gets or sets the snapshot of the currently captured UI element and screen state.
        /// </summary>
        public CaptureSnapshot? CurrentCapture
        {
            get => _currentCapture;
            set { _currentCapture = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets the text input used for key simulation testing.
        /// </summary>
        public string TestInputText
        {
            get => _testInputText;
            set { _testInputText = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets the command that triggers a simulated click on the captured UI target.
        /// </summary>
        public ICommand TestClickCommand { get; }

        /// <summary>
        /// Gets the command that sends keyboard input to the captured UI target.
        /// </summary>
        public ICommand TestTextCommand { get; }

        private string _integrationTestOutput = "Ready to test Venus Auto Engine...";

        /// <summary>
        /// Gets or sets the output log string displayed for integration test operations.
        /// </summary>
        public string IntegrationTestOutput
        {
            get => _integrationTestOutput;
            set { _integrationTestOutput = value; OnPropertyChanged(); }
        }

        private string _methodFilePath = string.Empty;

        /// <summary>
        /// Gets or sets the file path of the Venus method file to be loaded.
        /// </summary>
        public string MethodFilePath
        {
            get => _methodFilePath;
            set { _methodFilePath = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets the command that initiates method execution via the Venus service.
        /// </summary>
        public ICommand TestEngineStartCommand { get; }

        /// <summary>
        /// Gets the command that queries the current engine execution status.
        /// </summary>
        public ICommand TestEngineStatusCommand { get; }

        /// <summary>
        /// Gets the command that pauses the running method execution.
        /// </summary>
        public ICommand TestEnginePauseCommand { get; }

        /// <summary>
        /// Gets the command that resumes a paused method execution.
        /// </summary>
        public ICommand TestEngineResumeCommand { get; }

        /// <summary>
        /// Gets the command that aborts the currently running method execution.
        /// </summary>
        public ICommand TestEngineAbortCommand { get; }

        /// <summary>
        /// Gets the command that opens a file dialog to select a method file.
        /// </summary>
        public ICommand BrowseMethodCommand { get; }

        /// <summary>
        /// Gets the command that loads the selected method file into the Venus engine.
        /// </summary>
        public ICommand TestEngineLoadCommand { get; }

        /// <summary>
        /// Gets the command that verifies and ensures the Venus process is running.
        /// </summary>
        public ICommand TestEnsureStartedCommand { get; }

        /// <summary>
        /// Gets the command that arranges the target window using predefined layouts.
        /// </summary>
        public ICommand TestArrangeWindowCommand { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MainViewModel"/> class.
        /// </summary>
        /// <param name="venusService">The service interface for controlling Venus engine execution.</param>
        public MainViewModel(IVenusRunControlService venusService)
        {
            _venusService = venusService;
            TestClickCommand = new RelayCommand(ExecuteTestClick, CanExecuteTest);
            TestTextCommand = new RelayCommand(ExecuteTestText, CanExecuteTest);
            TestEngineStartCommand = new RelayCommand(async _ => await ExecuteEngineStartAsync());
            TestEngineStatusCommand = new RelayCommand(async _ => await ExecuteEngineStatusAsync());
            TestEnginePauseCommand = new RelayCommand(async _ => await ExecuteEnginePauseAsync());
            TestEngineResumeCommand = new RelayCommand(async _ => await ExecuteEngineResumeAsync());
            TestEngineAbortCommand = new RelayCommand(async _ => await ExecuteEngineAbortAsync());
            BrowseMethodCommand = new RelayCommand(_ => ExecuteBrowseMethod());
            TestEngineLoadCommand = new RelayCommand(async _ => await ExecuteEngineLoadAsync());
            TestEnsureStartedCommand = new RelayCommand(async _ => await ExecuteEnsureStartedAsync());
            TestArrangeWindowCommand = new RelayCommand(async _ => await ExecuteArrangeWindowAsync());
        }

        /// <summary>
        /// Captures UI element details, coordinates, and pixel color under the current mouse cursor position.
        /// </summary>
        // Executes the capture logic when the global hotkey is triggered.
        public void ExecuteCapture()
        {
            NativeMethods.GetCursorPos(out NativeMethods.POINT screenPoint);
            IntPtr targetHwnd = NativeMethods.WindowFromPoint(screenPoint);

            if (targetHwnd == IntPtr.Zero) return;

            char[] className = new char[256];
            int classLength = NativeMethods.GetClassName(targetHwnd, className, className.Length);

            char[] windowText = new char[1024];
            int textLength = NativeMethods.GetWindowText(targetHwnd, windowText, windowText.Length);

            IntPtr parentHwnd = NativeMethods.GetParent(targetHwnd);

            // Traverse up the window tree to find the top-level main window
            IntPtr rootWindowHwnd = NativeMethods.GetAncestor(targetHwnd, NativeMethods.GA_ROOT);
            if (rootWindowHwnd == IntPtr.Zero)
            {
                rootWindowHwnd = targetHwnd;
            }

            NativeMethods.POINT clientPoint = screenPoint;

            // Calculate coordinates relative to the top-level main window instead of the immediate target control
            NativeMethods.ScreenToClient(rootWindowHwnd, ref clientPoint);

            IntPtr hdc = NativeMethods.GetDC(IntPtr.Zero);
            uint pixelColor = NativeMethods.GetPixel(hdc, screenPoint.X, screenPoint.Y);
            _ = NativeMethods.ReleaseDC(IntPtr.Zero, hdc);

            Color color = Color.FromArgb(
                255,
                (byte)(pixelColor & 0x000000FF),
                (byte)((pixelColor & 0x0000FF00) >> 8),
                (byte)((pixelColor & 0x00FF0000) >> 16));

            // Update the bound property
            CurrentCapture = new CaptureSnapshot
            {
                Hwnd = targetHwnd,
                ParentHwnd = parentHwnd,
                ClassName = classLength > 0 ? new string(className, 0, classLength) : string.Empty,
                WindowText = textLength > 0 ? new string(windowText, 0, textLength) : string.Empty,
                AbsoluteX = screenPoint.X,
                AbsoluteY = screenPoint.Y,
                RelativeX = clientPoint.X,
                RelativeY = clientPoint.Y,
                PixelColor = color
            };
        }

        /// <summary>
        /// Validates whether action commands can be executed against a valid captured window handle.
        /// </summary>
        /// <param name="parameter">Optional command parameter.</param>
        /// <returns><c>true</c> if a valid window handle exists; otherwise, <c>false</c>.</returns>
        private bool CanExecuteTest(object? parameter) => CurrentCapture != null && CurrentCapture.Hwnd != IntPtr.Zero;

        /// <summary>
        /// Posts Win32 mouse down and mouse up messages to simulate a click at the relative coordinates of the captured target.
        /// </summary>
        /// <param name="parameter">Optional command parameter.</param>
        private void ExecuteTestClick(object? parameter)
        {
            if (CurrentCapture == null || CurrentCapture.Hwnd == IntPtr.Zero) return;

            IntPtr hwnd = CurrentCapture.Hwnd;
            int x = CurrentCapture.RelativeX;
            int y = CurrentCapture.RelativeY;

            // In Win32, coordinates for mouse messages are packed into the lParam parameter.
            // Low-order word specifies the x-coordinate, high-order word specifies the y-coordinate.
            IntPtr lParam = (IntPtr)((y << 16) | (x & 0xFFFF));

            // Send silent mouse down, then mouse up
            NativeMethods.PostMessage(hwnd, NativeMethods.WM_LBUTTONDOWN, (IntPtr)NativeMethods.MK_LBUTTON, lParam);
            NativeMethods.PostMessage(hwnd, NativeMethods.WM_LBUTTONUP, IntPtr.Zero, lParam);
        }

        /// <summary>
        /// Sends simulated key press messages to the target window based on the provided input text.
        /// </summary>
        /// <param name="parameter">Optional command parameter.</param>
        private void ExecuteTestText(object? parameter)
        {
            if (CurrentCapture == null || CurrentCapture.Hwnd == IntPtr.Zero) return;

            // Simple parser for {F5} text input
            if (TestInputText.Trim().Equals("{F5}", StringComparison.OrdinalIgnoreCase))
            {
                IntPtr hwnd = CurrentCapture.Hwnd;
                NativeMethods.PostMessage(hwnd, NativeMethods.WM_KEYDOWN, (IntPtr)NativeMethods.VK_F5, IntPtr.Zero);
                NativeMethods.PostMessage(hwnd, NativeMethods.WM_KEYUP, (IntPtr)NativeMethods.VK_F5, IntPtr.Zero);
            }
            else
            {
                MessageBox.Show("For this testing phase, please type '{F5}' exactly to test sending the F5 key.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Asynchronously starts method execution via the Venus service.
        /// </summary>
        private async Task ExecuteEngineStartAsync()
        {
            IntegrationTestOutput = "Executing StartRunAsync()...";
            try
            {
                await _venusService.StartRunAsync();
                IntegrationTestOutput = "StartRunAsync() completed successfully.";
            }
            catch (Exception ex)
            {
                IntegrationTestOutput = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Asynchronously queries and logs the execution status from the Venus service.
        /// </summary>
        private async Task ExecuteEngineStatusAsync()
        {
            IntegrationTestOutput = "Executing GetStatusAsync()...";
            try
            {
                var status = await _venusService.GetStatusAsync();
                IntegrationTestOutput = $"Status: {status.State}\nHasError: {status.HasErrorDialog}\nMsg: {status.RawStatusText}\nMethod: {status.LoadedMethodName ?? "None"}\nError Details: {status.ErrorMessage ?? "None"}";
            }
            catch (Exception ex)
            {
                IntegrationTestOutput = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Asynchronously pauses the currently running Venus execution.
        /// </summary>
        private async Task ExecuteEnginePauseAsync()
        {
            IntegrationTestOutput = "Executing PauseRunAsync()...";
            try
            {
                await _venusService.PauseRunAsync();
                IntegrationTestOutput = "PauseRunAsync() completed successfully.";
            }
            catch (Exception ex)
            {
                IntegrationTestOutput = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Asynchronously resumes a paused Venus execution.
        /// </summary>
        private async Task ExecuteEngineResumeAsync()
        {
            IntegrationTestOutput = "Executing ResumeRunAsync()...";
            try
            {
                await _venusService.ResumeRunAsync();
                IntegrationTestOutput = "ResumeRunAsync() completed successfully.";
            }
            catch (Exception ex)
            {
                IntegrationTestOutput = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Asynchronously aborts the active Venus execution.
        /// </summary>
        private async Task ExecuteEngineAbortAsync()
        {
            IntegrationTestOutput = "Executing AbortRunAsync()...";
            try
            {
                await _venusService.AbortRunAsync();
                IntegrationTestOutput = "AbortRunAsync() completed successfully.";
            }
            catch (Exception ex)
            {
                IntegrationTestOutput = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Displays an open file dialog allowing the user to select a Venus method file.
        /// </summary>
        private void ExecuteBrowseMethod()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Venus Methods (*.med;*.hsl)|*.med;*.hsl|All files (*.*)|*.*",
                Title = "Select Venus Method File"
            };

            if (dialog.ShowDialog() == true)
            {
                MethodFilePath = dialog.FileName;
            }
        }

        /// <summary>
        /// Asynchronously loads the selected method file into the Venus engine.
        /// </summary>
        private async Task ExecuteEngineLoadAsync()
        {
            if (string.IsNullOrWhiteSpace(MethodFilePath))
            {
                IntegrationTestOutput = "Error: Please select a method file first.";
                return;
            }

            IntegrationTestOutput = $"Executing LoadMethodAsync('{MethodFilePath}')...";
            try
            {
                await _venusService.LoadMethodAsync(MethodFilePath);
                IntegrationTestOutput = "LoadMethodAsync() sequence initiated.";
            }
            catch (Exception ex)
            {
                IntegrationTestOutput = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Asynchronously verifies that the target Venus process is running.
        /// </summary>
        private async Task ExecuteEnsureStartedAsync()
        {
            IntegrationTestOutput = "Executing EnsureProcessStartedAsync()...";
            try
            {
                await _venusService.EnsureProcessStartedAsync();
                IntegrationTestOutput = "EnsureProcessStartedAsync() completed. Process is running.";
            }
            catch (Exception ex)
            {
                IntegrationTestOutput = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Asynchronously aligns the target window layout on screen.
        /// </summary>
        private async Task ExecuteArrangeWindowAsync()
        {
            IntegrationTestOutput = "Executing ArrangeWindowAsync(RightHalf)...";
            try
            {
                // We test the RightHalf preset here. You can change this to Center or Maximize for other tests.
                await _venusService.ArrangeWindowAsync(WindowLayoutPreset.RightHalf);
                IntegrationTestOutput = "ArrangeWindowAsync() completed. Window moved to the right half of the screen.";
            }
            catch (Exception ex)
            {
                IntegrationTestOutput = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event and triggers command requery updates.
        /// </summary>
        /// <param name="name">The name of the property that changed.</param>
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            // Force re-evaluation of commands
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// A standard ICommand implementation for relaying actions to underlying viewmodel logic.
    /// </summary>
    // A standard ICommand implementation for relaying actions.
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        /// <summary>
        /// Initializes a new instance of the <see cref="RelayCommand"/> class.
        /// </summary>
        /// <param name="execute">The action to execute when the command is invoked.</param>
        /// <param name="canExecute">The status predicate determining whether the command can execute.</param>
        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        /// <summary>
        /// Occurs when changes occur that affect whether or not the command should execute.
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// Determines whether the command can execute in its current state.
        /// </summary>
        /// <param name="parameter">Data used by the command.</param>
        /// <returns><c>true</c> if this command can be executed; otherwise, <c>false</c>.</returns>
        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);

        /// <summary>
        /// Executes the command action.
        /// </summary>
        /// <param name="parameter">Data used by the command.</param>
        public void Execute(object? parameter) => _execute(parameter);
    }
}