using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TraceLogic.Core;
using TraceLogic.Core.Exporting;
using TraceLogic.Core.Interfaces;
using TraceLogic.Core.Models;
using TraceLogic.Core.Parsing;

namespace TraceLogic
{
    /// <summary>
    /// Interaction logic for MainWindow, serving as the main view for file handling,
    /// visualization of trace log analysis, and data export functionalities.
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly ITraceFileParser _parser;
        private readonly ITraceDataExporter _exporter;

        private TraceAnalysisResult? _analysisResult;

        /// <summary>
        /// Gets or sets the parsed trace file analysis result bound to the UI components.
        /// </summary>
        public TraceAnalysisResult? AnalysisResult
        {
            get => _analysisResult;
            set
            {
                _analysisResult = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class and configures the core service dependencies.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // Initialize the dependency injection container to utilize the TraceLogic core services
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddTraceLogic();
            var serviceProvider = services.BuildServiceProvider();

            _parser = serviceProvider.GetRequiredService<ITraceFileParser>();
            _exporter = serviceProvider.GetRequiredService<ITraceDataExporter>();
        }

        #region Custom Title Bar Logic
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { this.DragMove(); }
        private void Close_Click(object sender, RoutedEventArgs e) { Application.Current.Shutdown(); }
        private void Minimize_Click(object sender, RoutedEventArgs e) { this.WindowState = WindowState.Minimized; }
        private void Maximize_Restore_Click(object sender, RoutedEventArgs e)
        {
            // Toggle between maximized and normal restored window state
            this.WindowState = this.WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
        }
        #endregion

        #region File Processing Logic
        private void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = "Trace files (*.trc)|*.trc|All files (*.*)|*.*", Title = "Select a Hamilton Venus Trace File" };
            if (openFileDialog.ShowDialog() == true) { ProcessFile(openFileDialog.FileName); }
        }
        private void MainContent_Drop(object sender, DragEventArgs e)
        {
            DragDropOverlay.Visibility = Visibility.Collapsed;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                // Extract the first file matching the expected .trc extension
                var trcFile = files.FirstOrDefault(f => Path.GetExtension(f).Equals(".trc", StringComparison.OrdinalIgnoreCase));
                if (trcFile != null) { ProcessFile(trcFile); }
                else { MessageBox.Show("Please drop a valid .trc file.", "Invalid File Type", MessageBoxButton.OK, MessageBoxImage.Warning); }
            }
        }
        private void MainContent_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effects = DragDropEffects.Copy; DragDropOverlay.Visibility = Visibility.Visible; }
            else { e.Effects = DragDropEffects.None; }
        }
        private void MainContent_DragLeave(object sender, DragEventArgs e) { DragDropOverlay.Visibility = Visibility.Collapsed; }
        private void ProcessFile(string filePath)
        {
            // Reset UI state and active results prior to initiating file parsing
            this.AnalysisResult = null;
            WelcomeMessage.Visibility = Visibility.Visible;
            DataTabs.Visibility = Visibility.Collapsed;

            // The parser is now injected and managed by the service provider
            var analysisResult = _parser.Parse(filePath);

            if (analysisResult.Errors.Count > 0)
            {
                StatusTextBlock.Text = "Error processing file.";
                MessageBox.Show(string.Join("\n", analysisResult.Errors), "Parsing Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            this.AnalysisResult = analysisResult;

            StatusTextBlock.Text = $"Successfully parsed {AnalysisResult.LiquidTransfers.Count} liquid transfer events from {AnalysisResult.FileName}.";
            WelcomeMessage.Visibility = Visibility.Collapsed;
            DataTabs.Visibility = Visibility.Visible;
            DataTabs.SelectedIndex = 0; // Focus on the new tab
        }
        #endregion

        #region Export and UI Logic

        /// <summary>
        /// Handles the click event for the new About button.
        /// </summary>
        private void About_Click(object sender, RoutedEventArgs e)
        {
            AboutBox aboutBox = new AboutBox
            {
                // This ensures the About Box opens centered over the MainWindow
                Owner = this
            };
            aboutBox.ShowDialog();
        }

        /// <summary>
        /// Handles the click event for the Export button.
        /// Logic is simplified to only export to CSV.
        /// </summary>
        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (AnalysisResult?.LiquidTransfers == null || AnalysisResult.LiquidTransfers.Count == 0)
            {
                MessageBox.Show("There is no data to export.", "Export Data", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                // Filter is now hardcoded for CSV only
                Filter = "CSV File (*.csv)|*.csv",
                Title = "Export Liquid Transfer Data",
                FileName = $"{Path.GetFileNameWithoutExtension(AnalysisResult.FileName)}_Export"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Get the currently visible and ordered columns from the DataGrid
                    // Dynamically resolve binding paths and headers for visible grid columns
                    var columnsToExport = LiquidTransferGrid.Columns
                        .Where(c => c.Visibility == Visibility.Visible)
                        .OrderBy(c => c.DisplayIndex)
                        .Select(c => new ExportColumnInfo
                        {
                            Header = c.Header?.ToString() ?? string.Empty,
                            PropertyName = (c.ClipboardContentBinding as System.Windows.Data.Binding)?.Path.Path ?? string.Empty
                        })
                        .ToList();

                    // Directly call the export method via the injected exporter service
                    _exporter.Export(AnalysisResult.LiquidTransfers, columnsToExport, saveFileDialog.FileName);

                    MessageBox.Show($"Data successfully exported to:\n{saveFileDialog.FileName}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred during export:\n{ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Handles the Checked/Unchecked events for the column visibility checkboxes.
        /// </summary>
        private void ColumnCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            // Map the Tag value from the CheckBox control to the corresponding DataGrid column index
            if (sender is CheckBox checkBox && checkBox.Tag != null
                && int.TryParse(checkBox.Tag.ToString(), out int columnIndex))
            {
                if (columnIndex >= 0 && columnIndex < LiquidTransferGrid.Columns.Count)
                {
                    LiquidTransferGrid.Columns[columnIndex].Visibility = (checkBox.IsChecked == true)
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
            }
        }

        #endregion

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event to notify subscribers of property changes.
        /// </summary>
        /// <param name="propertyName">Name of the property that changed.</param>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}