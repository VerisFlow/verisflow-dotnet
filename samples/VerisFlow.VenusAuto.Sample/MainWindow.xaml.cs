using System;
using System.Windows;
using System.Windows.Interop;
using VerisFlow.VenusAuto.Sample.Native;
using VerisFlow.VenusAuto.Sample.ViewModels;

namespace VerisFlow.VenusAuto.Sample
{
    /// <summary>
    /// Represents the main application window responsible for UI initialization, DataContext binding, and global hotkey message processing.
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// The unique identifier assigned to the global F2 hotkey registration.
        /// </summary>
        private const int HOTKEY_ID = 9000;

        /// <summary>
        /// The primary ViewModel instance handling application logic and UI capture tasks.
        /// </summary>
        private readonly MainViewModel _viewModel;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class with the injected ViewModel.
        /// </summary>
        /// <param name="viewModel">The <see cref="MainViewModel"/> instance to bind to the window's DataContext.</param>
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            this.DataContext = _viewModel;
        }

        /// <summary>
        /// Overrides window initialization to attach a hook into the Win32 message loop and register the global F2 hotkey.
        /// </summary>
        /// <param name="e">An <see cref="EventArgs"/> containing the event data.</param>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Hook into the WPF window message loop
            var helper = new WindowInteropHelper(this);
            HwndSource source = HwndSource.FromHwnd(helper.Handle);
            source.AddHook(HwndHook);

            // Register global hotkey F2
            if (!NativeMethods.RegisterHotKey(helper.Handle, HOTKEY_ID, NativeMethods.MOD_NONE, NativeMethods.VK_F2))
            {
                MessageBox.Show("Failed to register F2 hotkey. It might be in use by another application.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Overrides window closure to unregister the global F2 hotkey and clean up native resources.
        /// </summary>
        /// <param name="e">An <see cref="EventArgs"/> containing the event data.</param>
        protected override void OnClosed(EventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            NativeMethods.UnregisterHotKey(helper.Handle, HOTKEY_ID);
            base.OnClosed(e);
        }

        /// <summary>
        /// Filters and processes low-level Windows messages before WPF dispatch, handling global hotkey triggers.
        /// </summary>
        /// <param name="hwnd">The window handle receiving the message.</param>
        /// <param name="msg">The Win32 message identifier.</param>
        /// <param name="wParam">Additional message-specific parameter, containing the hotkey ID.</param>
        /// <param name="lParam">Additional message-specific parameter, containing key modifiers and virtual key code.</param>
        /// <param name="handled">Indicates whether the message has been handled by this custom hook procedure.</param>
        /// <returns>A return value specific to the message processing; always returns <see cref="IntPtr.Zero"/> in this hook.</returns>
        // Intercepts Windows messages before WPF processes them.
        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                _viewModel.ExecuteCapture();
                handled = true;
            }
            return IntPtr.Zero;
        }
    }
}