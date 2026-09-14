using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using VerisFlow.LayParser.Core;

namespace VerisFlow.VenusDeckParser.Desktop
{
    /// <summary>
    /// Interaction logic for the main application window.
    /// Handles user interactions, file selection, and delegates layout processing.
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// A collection to hold the processed labware data for binding to the DataGrid.
        /// </summary>
        public ObservableCollection<ProcessedLabwareInfo> ProcessedLabwareData { get; set; }

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// Sets up the data context required for UI data binding.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            ProcessedLabwareData = new ObservableCollection<ProcessedLabwareInfo>();
            // Set the DataContext for data binding
            this.DataContext = this;
        }

        /// <summary>
        /// Handles the click event for the deck layout selection button.
        /// Opens a file dialog specifically filtered for .lay files.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The routed event arguments.</param>
        private void SelectDeckLayout_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Deck Layout File",
                Filter = "Deck Layout Files (*.lay)|*.lay|All files (*.*)|*.*",
                DefaultExt = ".lay"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                ProcessDeckLayoutFile(openFileDialog.FileName);
            }
        }

        /// <summary>
        /// Asynchronously processes the deck layout file to prevent UI freezing.
        /// </summary>
        /// <param name="deckLayoutFile">The full path to the .lay file.</param>
        private async void ProcessDeckLayoutFile(string deckLayoutFile)
        {
            StatusTextBlock.Text = $"Processing: {Path.GetFileName(deckLayoutFile)}...";
            SelectLayoutButton.IsEnabled = false; // Disable button during processing

            try
            {
                string markdownFilePath = Path.ChangeExtension(deckLayoutFile, ".md");

                // Run all file/CPU-intensive operations on a background thread.
                var processedData = await Task.Run(() =>
                {
                    var raw = DeckLayoutParser.GetLabwareInfo(deckLayoutFile);
                    var processed = LabwareDataProcessor.Process(raw);
                    var sequences = DeckSequenceParser.GetSequenceInfo(deckLayoutFile);
                    var markdown = GenerateMarkdown(deckLayoutFile, processed, sequences);

                    // Use the synchronous method INSIDE the background task.
                    File.WriteAllText(markdownFilePath, markdown, Encoding.UTF8);

                    return processed; // Only return the data needed by the UI thread.
                });

                // --- Update UI on the UI thread ---
                ProcessedLabwareData.Clear();
                foreach (var item in processedData)
                {
                    ProcessedLabwareData.Add(item);
                }

                StatusTextBlock.Text = $"Displayed {processedData.Count} items and saved report to {markdownFilePath}.";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"An error occurred: {ex.Message}";
                MessageBox.Show($"Error processing file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Re-enable the button once processing is complete.
                SelectLayoutButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// Opens the secondary window to display labware in a hierarchical view with X-axis filtering.
        /// </summary>
        private void OpenHierarchyButton_Click(object sender, RoutedEventArgs e)
        {
            var hierarchyWindow = new DeckHierarchyWindow(new List<ProcessedLabwareInfo>(ProcessedLabwareData))
            {
                Owner = this
            };
            hierarchyWindow.Show();
        }

        /// <summary>
        /// Generates the detailed Markdown report with the final calculated labware information.
        /// </summary>
        /// <param name="deckLayoutFile">The original file path used as a reference in the report.</param>
        /// <param name="processedData">The collection of processed labware information.</param>
        /// <returns>A string containing the formatted markdown report.</returns>
        private string GenerateMarkdown(string deckLayoutFile, List<ProcessedLabwareInfo> processedData)
        {
            return GenerateMarkdown(deckLayoutFile, processedData, new List<SequenceInfo>());
        }

        /// <summary>
        /// Generates the comprehensive Markdown report containing both processed labware and sequence metadata.
        /// Appends structured Sequence information below the Labware section for downstream machine readability.
        /// </summary>
        /// <param name="deckLayoutFile">The source layout file path.</param>
        /// <param name="processedData">The processed labware collection.</param>
        /// <param name="sequences">The extracted sequence collections with nested matrices.</param>
        /// <returns>A formatted markdown document string.</returns>
        private string GenerateMarkdown(string deckLayoutFile, List<ProcessedLabwareInfo> processedData, List<SequenceInfo> sequences)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"# Deck Layout Report for {deckLayoutFile}");
            sb.AppendLine();
            sb.AppendLine($"**Generated on:** {DateTime.Now}");
            sb.AppendLine();

            sb.AppendLine("## Processed Labware Information");
            sb.AppendLine();

            if (processedData != null && processedData.Count > 0)
            {
                sb.AppendLine(FormattableString.Invariant($"**Total Labware Instances:** {processedData.Count}"));
                sb.AppendLine();

                // Markdown Table Header
                sb.AppendLine("| # | ID | Type | Template | X | Y | Z | Dx | Dy | Column | Row | TipRack | AlphaIndex |");
                sb.AppendLine("|---|----|------|----------|---|---|---|----|----|--------|-----|---------|------------|");

                // Table Rows
                foreach (var labware in processedData)
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "| {0} | `{1}` | {2} | {3} | {4:F3} | {5:F3} | {6:F3} | {7:F3} | {8:F3} | {9} | {10} | {11} | {12} |",
                        labware.Index,
                        labware.Id,
                        labware.LabwareType,
                        labware.Template,
                        labware.FinalX,
                        labware.FinalY,
                        labware.FinalZ,
                        labware.Dx,
                        labware.Dy,
                        labware.Column,
                        labware.Row,
                        labware.TipRack,
                        labware.AlphaIndex));
                }
            }
            else
            {
                sb.AppendLine("No labware information could be processed from the file.");
            }

            sb.AppendLine();
            sb.AppendLine("## Sequence Information");
            sb.AppendLine();

            if (sequences != null && sequences.Count > 0)
            {
                sb.AppendLine(FormattableString.Invariant($"**Total Sequences Defined:** {sequences.Count}"));
                sb.AppendLine();

                sb.AppendLine("| # | Sequence Name | Total Count | ReadOnly | Rack Count | Target Labwares (ObjId Descending) |");
                sb.AppendLine("|---|---------------|-------------|----------|------------|-----------------------------------|");

                foreach (var seq in sequences)
                {
                    string targetRacksSummary = seq.Matrices.Count > 0
                        ? string.Join(", ", seq.Matrices.Select(m => FormattableString.Invariant($"`{m.ObjId}` ({m.TotalWells})")))
                        : "None";

                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "| {0} | `{1}` | {2} | {3} | {4} | {5} |",
                        seq.Index,
                        seq.Name,
                        seq.TotalCount,
                        seq.ReadOnly,
                        seq.Matrices.Count,
                        targetRacksSummary));
                }

                sb.AppendLine();
                sb.AppendLine("### Sequence Detail Matrices");
                sb.AppendLine();

                foreach (var seq in sequences)
                {
                    sb.AppendLine(FormattableString.Invariant($"#### Sequence #{seq.Index}: `{seq.Name}`"));
                    sb.AppendLine();
                    sb.AppendLine(FormattableString.Invariant($"- Total Points: {seq.TotalCount}"));
                    sb.AppendLine(FormattableString.Invariant($"- Read Only: {seq.ReadOnly}"));
                    sb.AppendLine(FormattableString.Invariant($"- Rack Matrices: {seq.Matrices.Count}"));
                    sb.AppendLine();

                    if (seq.Matrices.Count > 0)
                    {
                        sb.AppendLine("| Labware (ObjId) | Seq Index | PosId | Row | Column |");
                        sb.AppendLine("|-----------------|-----------|-------|-----|--------|");

                        foreach (var matrix in seq.Matrices)
                        {
                            foreach (var pos in matrix.Positions)
                            {
                                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                                    "| `{0}` | {1} | `{2}` | {3} | {4} |",
                                    matrix.ObjId,
                                    pos.SequenceIndex,
                                    pos.PosId,
                                    pos.RowIndex,
                                    pos.ColumnIndex));
                            }
                        }
                    }
                    else
                    {
                        sb.AppendLine("_No target positions defined for this sequence._");
                    }

                    sb.AppendLine();
                }
            }
            else
            {
                sb.AppendLine("No sequence information found in this layout file.");
            }

            return sb.ToString();
        }

        #region Drag and Drop Handlers

        /// <summary>
        /// Validates the dragged object when it enters the application window boundaries.
        /// Ensures that only a single file with a .lay extension is accepted.
        /// </summary>
        private void MainContent_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                // Ensure only a single .lay file is being dragged
                if (files.Length == 1 && Path.GetExtension(files[0]).Equals(".lay", StringComparison.OrdinalIgnoreCase))
                {
                    e.Effects = DragDropEffects.Copy;
                    DragDropOverlay.Visibility = Visibility.Visible;
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            e.Handled = true;
        }

        /// <summary>
        /// Hides the drag-and-drop visual overlay when the dragged object leaves the window.
        /// </summary>
        private void MainContent_DragLeave(object sender, DragEventArgs e)
        {
            DragDropOverlay.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }

        /// <summary>
        /// Triggers the file processing logic once a valid file is dropped into the application.
        /// </summary>
        private void MainContent_Drop(object sender, DragEventArgs e)
        {
            DragDropOverlay.Visibility = Visibility.Collapsed;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length == 1 && Path.GetExtension(files[0]).Equals(".lay", StringComparison.OrdinalIgnoreCase))
                {
                    ProcessDeckLayoutFile(files[0]);
                }
            }
            e.Handled = true;
        }

        #endregion

        #region Custom Title Bar Event Handlers

        /// <summary>
        /// Enables dragging the window when the left mouse button is pressed on the custom title bar.
        /// </summary>
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        /// <summary>
        /// Minimizes the application window to the taskbar.
        /// </summary>
        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// Toggles the application window state between maximized and normal.
        /// </summary>
        private void Maximize_Restore_Click(object sender, RoutedEventArgs e)
        {
            WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
        }

        /// <summary>
        /// Closes the application window and terminates the process.
        /// </summary>
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Instantiates and displays the custom about dialog window.
        /// </summary>
        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AboutBox aboutBox = new AboutBox
            {
                // Set the owner to center the dialog over the main window
                Owner = this
            };
            aboutBox.ShowDialog();
        }
        #endregion
    }
}