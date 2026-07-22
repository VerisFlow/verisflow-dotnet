using System;
using System.Reflection;
using System.Windows;

namespace TraceLogic
{
    /// <summary>
    /// Interaction logic for the AboutBox dialog window, displaying application metadata and copyright details.
    /// </summary>
    public partial class AboutBox : Window
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AboutBox"/> class, inheriting the main window's icon
        /// and populating application assembly version information.
        /// </summary>
        public AboutBox()
        {
            InitializeComponent();

            // This ensures the AboutBox uses the same icon as the MainWindow
            // Safely inherit icon assets from active application context without triggering circular reference
            if (Application.Current.MainWindow != null && Application.Current.MainWindow != this)
            {
                this.Icon = Application.Current.MainWindow.Icon;
            }

            // Get the assembly version and display it
            // Dynamically resolve executing assembly identity to extract semantic build numbers
            Version? version = Assembly.GetExecutingAssembly().GetName().Version;

            if (version != null)
            {
                VersionTextBlock.Text = $"Version {version.Major}.{version.Minor}.{version.Build}";
            }
        }
    }
}