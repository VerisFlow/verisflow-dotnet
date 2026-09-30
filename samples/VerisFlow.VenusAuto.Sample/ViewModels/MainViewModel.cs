using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using VerisFlow.VenusAuto.Core.Contracts;
using VerisFlow.VenusAuto.Core.Models;
using VerisFlow.VenusAuto.Sample.Models;
using VerisFlow.VenusAuto.Sample.Native;

namespace VerisFlow.VenusAuto.Sample.ViewModels
{
    /// <summary>
    /// Test bench for VerisFlow.VenusAuto.Core: window capture, raw input tests, run commands, method loading,
    /// and dialog inspection/response.
    /// </summary>
    /// <remarks>
    /// Each operation resolves the service in a new scope, so edits to appsettings.json (e.g. setting a command ID to 0
    /// to test the coordinate fallback) apply to the next operation without restarting.
    /// </remarks>
    public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private const int MaxLogLines = 500;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly DispatcherTimer _dialogTimer;
        private readonly Queue<string> _logLines = new();

        private CaptureSnapshot? _currentCapture;
        private string _testInputText = "{F5}";
        private string _methodFilePath = string.Empty;
        private string _logText = string.Empty;
        private string _dialogSummary = "Not read yet. Use Read Dialogs or enable auto-refresh.";
        private string _dialogKey = string.Empty;
        private bool _isBusy;
        private bool _autoRefreshDialogs;
        private bool _refreshingDialogs;
        private int _lastDialogCount = -1;

        public MainViewModel(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;

            _dialogTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _dialogTimer.Tick += async (_, _) => await RefreshDialogsAsync(quiet: true);

            TestClickCommand = new RelayCommand(_ => ExecuteTestClick(), _ => HasCapture);
            TestTextCommand = new RelayCommand(_ => ExecuteTestText(), _ => HasCapture);

            StartCommand = new RelayCommand(async _ => await RunAsync("Start", service => service.StartRunAsync()), _ => !IsBusy);
            StatusCommand = new RelayCommand(async _ => await RunAsync("Status", ReadStatusAsync), _ => !IsBusy);
            PauseCommand = new RelayCommand(async _ => await RunAsync("Pause", service => service.PauseRunAsync()), _ => !IsBusy);
            ResumeCommand = new RelayCommand(async _ => await RunAsync("Resume", service => service.ResumeRunAsync()), _ => !IsBusy);
            EnsureStartedCommand = new RelayCommand(async _ => await RunAsync("Ensure started", service => service.EnsureProcessStartedAsync()), _ => !IsBusy);
            ArrangeWindowCommand = new RelayCommand(async _ => await RunAsync("Arrange window (right half)", service => service.ArrangeWindowAsync(WindowLayoutPreset.RightHalf)), _ => !IsBusy);
            RequestAbortCommand = new RelayCommand(async _ => await RunAsync("Request abort", service => AbortAsync(service, confirm: false)), _ => !IsBusy);
            ConfirmAbortCommand = new RelayCommand(async _ => await ConfirmAbortAsync(), _ => !IsBusy);

            BrowseMethodCommand = new RelayCommand(_ => ExecuteBrowseMethod());
            LoadMethodCommand = new RelayCommand(async _ => await LoadMethodAsync(), _ => !IsBusy && !string.IsNullOrWhiteSpace(MethodFilePath));

            ReadDialogsCommand = new RelayCommand(async _ => await RefreshDialogsAsync(quiet: false));
            ClearLogCommand = new RelayCommand(_ => ClearLog());

            Log("Ready. Commands use RunControlIds from appsettings.json next to the executable; set an ID to 0 to test the coordinate fallback.");
        }

        // ------------------------------------------------------------------ Bindable state

        public CaptureSnapshot? CurrentCapture
        {
            get => _currentCapture;
            private set { _currentCapture = value; OnPropertyChanged(); }
        }

        public string TestInputText
        {
            get => _testInputText;
            set { _testInputText = value; OnPropertyChanged(); }
        }

        public string MethodFilePath
        {
            get => _methodFilePath;
            set { _methodFilePath = value; OnPropertyChanged(); }
        }

        public string LogText
        {
            get => _logText;
            private set { _logText = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set { _isBusy = value; OnPropertyChanged(); }
        }

        public bool AutoRefreshDialogs
        {
            get => _autoRefreshDialogs;
            set
            {
                _autoRefreshDialogs = value;
                OnPropertyChanged();

                if (value)
                {
                    _dialogTimer.Start();
                    Log("Dialog auto-refresh on.");
                }
                else
                {
                    _dialogTimer.Stop();
                    Log("Dialog auto-refresh off.");
                }
            }
        }

        public ObservableCollection<DialogItemViewModel> Dialogs { get; } = new();

        public string DialogSummary
        {
            get => _dialogSummary;
            private set { _dialogSummary = value; OnPropertyChanged(); }
        }

        private bool HasCapture => CurrentCapture != null && CurrentCapture.Hwnd != IntPtr.Zero;

        // ------------------------------------------------------------------ Commands

        public ICommand TestClickCommand { get; }

        public ICommand TestTextCommand { get; }

        public ICommand StartCommand { get; }

        public ICommand StatusCommand { get; }

        public ICommand PauseCommand { get; }

        public ICommand ResumeCommand { get; }

        public ICommand EnsureStartedCommand { get; }

        public ICommand ArrangeWindowCommand { get; }

        public ICommand RequestAbortCommand { get; }

        public ICommand ConfirmAbortCommand { get; }

        public ICommand BrowseMethodCommand { get; }

        public ICommand LoadMethodCommand { get; }

        public ICommand ReadDialogsCommand { get; }

        public ICommand ClearLogCommand { get; }

        // ------------------------------------------------------------------ Capture (F2)

        /// <summary>
        /// Captures the deepest window under the cursor, including disabled controls, with its IDs and relations.
        /// </summary>
        public void ExecuteCapture()
        {
            NativeMethods.GetCursorPos(out NativeMethods.POINT screenPoint);

            IntPtr target = NativeMethods.WindowFromPoint(screenPoint);
            if (target == IntPtr.Zero) return;

            // WindowFromPoint skips disabled controls; walk down with ChildWindowFromPointEx to find them.
            for (var depth = 0; depth < 32; depth++)
            {
                var clientPoint = screenPoint;
                NativeMethods.ScreenToClient(target, ref clientPoint);

                var child = NativeMethods.ChildWindowFromPointEx(target, clientPoint, NativeMethods.CWP_SKIPINVISIBLE | NativeMethods.CWP_SKIPTRANSPARENT);
                if (child == IntPtr.Zero || child == target) break;
                target = child;
            }

            var className = new char[256];
            var classLength = NativeMethods.GetClassName(target, className, className.Length);

            var windowText = new char[1024];
            var textLength = NativeMethods.GetWindowText(target, windowText, windowText.Length);

            var root = NativeMethods.GetAncestor(target, NativeMethods.GA_ROOT);
            if (root == IntPtr.Zero) root = target;

            var relativePoint = screenPoint;
            NativeMethods.ScreenToClient(root, ref relativePoint);

            NativeMethods.GetWindowThreadProcessId(target, out var processId);

            IntPtr hdc = NativeMethods.GetDC(IntPtr.Zero);
            uint pixel = NativeMethods.GetPixel(hdc, screenPoint.X, screenPoint.Y);
            _ = NativeMethods.ReleaseDC(IntPtr.Zero, hdc);

            CurrentCapture = new CaptureSnapshot
            {
                Hwnd = target,
                ParentHwnd = NativeMethods.GetParent(target),
                RootHwnd = root,
                RootOwnerHwnd = NativeMethods.GetWindow(root, NativeMethods.GW_OWNER),
                ClassName = classLength > 0 ? new string(className, 0, classLength) : string.Empty,
                WindowText = textLength > 0 ? new string(windowText, 0, textLength) : string.Empty,
                ControlId = target == root ? 0 : NativeMethods.GetDlgCtrlID(target),
                Style = NativeMethods.GetWindowLong(target, NativeMethods.GWL_STYLE),
                ProcessId = processId,
                AbsoluteX = screenPoint.X,
                AbsoluteY = screenPoint.Y,
                RelativeX = relativePoint.X,
                RelativeY = relativePoint.Y,
                PixelColor = Color.FromArgb(
                    255,
                    (byte)(pixel & 0x000000FF),
                    (byte)((pixel & 0x0000FF00) >> 8),
                    (byte)((pixel & 0x00FF0000) >> 16))
            };

            Log($"Captured {CurrentCapture.ClassName} {CurrentCapture.HwndText}, control ID {CurrentCapture.ControlIdText}, text '{CurrentCapture.WindowText}'.");
        }

        // ------------------------------------------------------------------ Raw input tests

        private void ExecuteTestClick()
        {
            if (!HasCapture) return;

            var capture = CurrentCapture!;
            var lParam = (IntPtr)((capture.RelativeY << 16) | (capture.RelativeX & 0xFFFF));

            NativeMethods.PostMessage(capture.Hwnd, NativeMethods.WM_LBUTTONDOWN, (IntPtr)NativeMethods.MK_LBUTTON, lParam);
            NativeMethods.PostMessage(capture.Hwnd, NativeMethods.WM_LBUTTONUP, IntPtr.Zero, lParam);
            Log($"Posted a click to {capture.HwndText} at {capture.RelativeText}.");
        }

        private void ExecuteTestText()
        {
            if (!HasCapture) return;

            if (TestInputText.Trim().Equals("{F5}", StringComparison.OrdinalIgnoreCase))
            {
                var hwnd = CurrentCapture!.Hwnd;
                NativeMethods.PostMessage(hwnd, NativeMethods.WM_KEYDOWN, (IntPtr)NativeMethods.VK_F5, IntPtr.Zero);
                NativeMethods.PostMessage(hwnd, NativeMethods.WM_KEYUP, (IntPtr)NativeMethods.VK_F5, IntPtr.Zero);
                Log($"Posted F5 to {CurrentCapture.HwndText}.");
            }
            else
            {
                Log("Only {F5} is supported by the key test.");
            }
        }

        // ------------------------------------------------------------------ Library operations

        private async Task ReadStatusAsync(IVenusRunControlService service)
        {
            var status = await service.GetStatusAsync();

            Log($"State: {status.State} | status text: '{status.RawStatusText}' | method: {status.LoadedMethodName ?? "-"}");

            var commands = status.Commands;
            Log(commands is null
                ? "Commands: toolbar state not available."
                : $"Commands: Start={YesNo(commands.CanStart)} Pause={YesNo(commands.CanPause)} Step={YesNo(commands.CanSingleStep)} Abort={YesNo(commands.CanAbort)}");

            if (status.HasErrorDialog)
            {
                Log($"Waiting for the user: {status.ErrorMessage}");
            }

            ShowDialogs(status.Dialogs);
        }

        private async Task AbortAsync(IVenusRunControlService service, bool confirm)
        {
            var result = await service.AbortRunAsync(confirm);
            Log($"Aborted: {result.Aborted}. {result.Detail}");
            ShowDialogs(await service.GetDialogsAsync());
        }

        private async Task ConfirmAbortAsync()
        {
            var answer = MessageBox.Show(
                "Abort the run in Run Control and confirm the abort?",
                "Confirm Abort",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (answer == MessageBoxResult.Yes)
            {
                await RunAsync("Confirm abort", service => AbortAsync(service, confirm: true));
            }
        }

        private async Task LoadMethodAsync()
        {
            var path = MethodFilePath;
            await RunAsync("Load method", async service =>
            {
                var result = await service.LoadMethodAsync(path);
                Log($"Loaded: {result.Loaded} | method: {result.LoadedMethodName ?? "-"} | {result.Detail}");
                ShowDialogs(result.PendingDialogs.Count > 0 ? result.PendingDialogs : await service.GetDialogsAsync());
            });
        }

        /// <summary>
        /// Presses a dialog button through the library; the dialog must still match the fingerprint that was read.
        /// </summary>
        public Task RespondAsync(VenusDialogInfo dialog, VenusDialogButton button)
            => RunAsync($"Respond '{button.Text}' [{button.Id}] to '{dialog.Title}'", async service =>
            {
                var response = await service.RespondToDialogAsync(dialog.Handle, button.Id, dialog.Fingerprint);
                Log($"Dialog closed: {response.DialogClosed}. Dialogs open now: {response.OpenDialogs.Count}.");
                ShowDialogs(response.OpenDialogs);
            });

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
        /// Runs one library operation in its own scope (fresh options), one at a time, logging the outcome.
        /// </summary>
        private async Task RunAsync(string name, Func<IVenusRunControlService, Task> action)
        {
            if (IsBusy)
            {
                Log($"{name}: skipped, another operation is still running.");
                return;
            }

            IsBusy = true;
            Log($"{name}...");

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IVenusRunControlService>();
                await action(service);
                Log($"{name}: done.");
            }
            catch (VenusDialogPendingException ex)
            {
                Log($"{name}: {ex.Message}");
                ShowDialogs(ex.Dialogs);
            }
            catch (Exception ex)
            {
                Log($"{name} failed ({ex.GetType().Name}): {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ------------------------------------------------------------------ Dialogs

        private async Task RefreshDialogsAsync(bool quiet)
        {
            if (_refreshingDialogs) return;
            _refreshingDialogs = true;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IVenusRunControlService>();
                var dialogs = await service.GetDialogsAsync();

                if (!quiet || dialogs.Count != _lastDialogCount)
                {
                    Log($"{dialogs.Count} dialog(s) open.");
                }

                ShowDialogs(dialogs);
            }
            catch (Exception ex)
            {
                if (!quiet)
                {
                    Log($"Reading dialogs failed ({ex.GetType().Name}): {ex.Message}");
                }
            }
            finally
            {
                _refreshingDialogs = false;
            }
        }

        /// <summary>
        /// Shows the dialogs; the list is only rebuilt when something changed, so buttons do not flicker on auto-refresh.
        /// </summary>
        private void ShowDialogs(IReadOnlyList<VenusDialogInfo> dialogs)
        {
            _lastDialogCount = dialogs.Count;

            var key = string.Join(";", dialogs.Select(d =>
                $"{d.Fingerprint}|{d.Message}|{string.Join(",", d.Buttons.Select(b => b.Enabled ? "1" : "0"))}|{string.Join(",", d.Options.Select(o => o.Checked ? "1" : "0"))}"));

            if (key == _dialogKey) return;
            _dialogKey = key;

            Dialogs.Clear();
            foreach (var dialog in dialogs)
            {
                Dialogs.Add(new DialogItemViewModel(dialog, this));
            }

            DialogSummary = dialogs.Count == 0
                ? "No Run Control dialog is open."
                : $"{dialogs.Count} dialog(s) open, topmost first. Nothing is answered automatically; choose a button to respond.";
        }

        // ------------------------------------------------------------------ Log

        private void Log(string message)
        {
            _logLines.Enqueue($"{DateTime.Now:HH:mm:ss.fff}  {message}");
            while (_logLines.Count > MaxLogLines)
            {
                _logLines.Dequeue();
            }

            LogText = string.Join(Environment.NewLine, _logLines);
        }

        private void ClearLog()
        {
            _logLines.Clear();
            LogText = string.Empty;
        }

        private static string YesNo(bool? value) => value switch
        {
            true => "yes",
            false => "no",
            null => "?"
        };

        // ------------------------------------------------------------------ Infrastructure

        public void Dispose() => _dialogTimer.Stop();

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            CommandManager.InvalidateRequerySuggested();
        }
    }
}