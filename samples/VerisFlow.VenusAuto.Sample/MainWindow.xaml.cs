using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using VerisFlow.VenusAuto.Sample.Native;
using VerisFlow.VenusAuto.Sample.ViewModels;

namespace VerisFlow.VenusAuto.Sample
{
    /// <summary>
    /// Main window: binds the view model and handles the global F2 capture hotkey.
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int HOTKEY_ID = 9000;

        /// <summary>Space kept free around the window so its title bar and borders stay on screen.</summary>
        private const double ScreenMargin = 24;

        private readonly MainViewModel _viewModel;

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;

            FitToWorkArea();
        }

        /// <summary>
        /// Shrinks the window (and its minimum size) to the work area of the primary screen, so the whole window,
        /// including the title bar, is visible on small or scaled displays.
        /// </summary>
        private void FitToWorkArea()
        {
            var area = SystemParameters.WorkArea;
            var maxWidth = Math.Max(400, area.Width - ScreenMargin);
            var maxHeight = Math.Max(300, area.Height - ScreenMargin);

            Width = Math.Min(Width, maxWidth);
            Height = Math.Min(Height, maxHeight);
            MinWidth = Math.Min(MinWidth, Width);
            MinHeight = Math.Min(MinHeight, Height);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var helper = new WindowInteropHelper(this);
            HwndSource source = HwndSource.FromHwnd(helper.Handle);
            source.AddHook(HwndHook);

            if (!NativeMethods.RegisterHotKey(helper.Handle, HOTKEY_ID, NativeMethods.MOD_NONE, NativeMethods.VK_F2))
            {
                MessageBox.Show("Failed to register F2 hotkey. It might be in use by another application.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            NativeMethods.UnregisterHotKey(helper.Handle, HOTKEY_ID);
            _viewModel.Dispose();
            base.OnClosed(e);
        }

        private void LogBox_TextChanged(object sender, TextChangedEventArgs e)
            => ((TextBox)sender).ScrollToEnd();

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