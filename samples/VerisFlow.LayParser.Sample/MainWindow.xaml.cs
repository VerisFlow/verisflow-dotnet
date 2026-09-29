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

        private string _instrument = string.Empty;

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
                var result = await Task.Run(() =>
                {
                    var deckData = DeckLayoutParser.GetDeckData(deckLayoutFile);
                    var processed = LabwareDataProcessor.Process(deckData.Labware);
                    var sequences = DeckSequenceParser.GetSequenceInfo(deckLayoutFile);
                    var markdown = GenerateMarkdown(deckLayoutFile, deckData.Instrument, processed, sequences);

                    File.WriteAllText(markdownFilePath, markdown, Encoding.UTF8);

                    return (deckData.Instrument, ProcessedData: processed);
                });

                _instrument = result.Instrument ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(_instrument))
                {
                    InstrumentTextBlock.Text = $"Instrument: {_instrument}";
                    InstrumentBadge.Visibility = Visibility.Visible;
                }
                else
                {
                    InstrumentBadge.Visibility = Visibility.Collapsed;
                }

                // --- Update UI on the UI thread ---
                ProcessedLabwareData.Clear();
                foreach (var item in result.ProcessedData)
                {
                    ProcessedLabwareData.Add(item);
                }

                string instrumentPrefix = !string.IsNullOrWhiteSpace(_instrument)
                    ? $"[{_instrument}] "
                    : string.Empty;

                int noteCount = result.ProcessedData.Count(l => l.GeometryNotes.Count > 0 || !string.IsNullOrEmpty(l.ValidationWarning));
                string noteSuffix = noteCount > 0 ? $" {noteCount} labware have notes or warnings (see report)." : string.Empty;

                StatusTextBlock.Text = $"{instrumentPrefix}Displayed {result.ProcessedData.Count} items and saved report to {markdownFilePath}.{noteSuffix}";
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
        /// Opens the secondary window to display labware grouped by carrier, with X-axis filtering.
        /// </summary>
        private void OpenHierarchyButton_Click(object sender, RoutedEventArgs e)
        {
            var hierarchyWindow = new DeckHierarchyWindow(new List<ProcessedLabwareInfo>(ProcessedLabwareData), _instrument)
            {
                Owner = this
            };
            hierarchyWindow.Show();
        }

        #region Markdown Report

        /// <summary>
        /// Generates the Markdown report: labware, Z calculation, rack geometry, carrier sites, notes, and sequences.
        /// </summary>
        /// <param name="deckLayoutFile">The source layout file path.</param>
        /// <param name="instrument">The instrument model name extracted from layout.</param>
        /// <param name="processedData">The processed labware collection.</param>
        /// <param name="sequences">The extracted sequence collections with nested matrices.</param>
        /// <returns>A formatted markdown document string.</returns>
        private static string GenerateMarkdown(string deckLayoutFile, string instrument, List<ProcessedLabwareInfo> processedData, List<SequenceInfo> sequences)
        {
            var sb = new StringBuilder();
            var labware = processedData ?? new List<ProcessedLabwareInfo>();
            var libraryVersion = typeof(DeckLayoutParser).Assembly.GetName().Version;

            sb.AppendLine($"# Deck Layout Report for {deckLayoutFile}");
            sb.AppendLine();
            sb.AppendLine(FormattableString.Invariant($"**Generated on:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC  "));
            if (!string.IsNullOrWhiteSpace(instrument))
            {
                sb.AppendLine($"**Instrument:** `{instrument}`  ");
            }
            sb.AppendLine($"**Parser:** VerisFlow.LayParser.Core {(libraryVersion != null ? libraryVersion.ToString(3) : "unknown")}  ");
            sb.AppendLine();
            sb.AppendLine("> Coordinates are in mm. X/Y is the layout reference point: the front-left corner for carriers, " +
                          "the center of position 1 (A1) for racks. Origin X/Y is the front-left corner of the outline (Dx × Dy).");
            sb.AppendLine();

            AppendLabwareTable(sb, labware);
            AppendZCalculation(sb, labware);
            AppendRackGeometry(sb, labware);
            AppendCarrierSites(sb, labware);
            AppendNotes(sb, labware);
            AppendSequences(sb, sequences);

            return sb.ToString();
        }

        private static void AppendLabwareTable(StringBuilder sb, List<ProcessedLabwareInfo> labware)
        {
            sb.AppendLine("## Processed Labware Information");
            sb.AppendLine();

            if (labware.Count == 0)
            {
                sb.AppendLine("No labware information could be processed from the file.");
                sb.AppendLine();
                return;
            }

            sb.AppendLine(FormattableString.Invariant($"**Total Labware Instances:** {labware.Count}"));
            sb.AppendLine();
            sb.AppendLine("| # | ID | Type | Parent | Site | Track | Stack | X | Y | Z | Origin X | Origin Y | Dx | Dy | Dz | Column | Row | TipRack | AlphaIndex |");
            sb.AppendLine("|---|----|------|--------|------|-------|-------|---|---|---|----------|----------|----|----|----|--------|-----|---------|------------|");

            foreach (var item in labware)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "| {0} | `{1}` | {2} | {3} | {4} | {5} | {6} | {7} | {8} | {9} | {10} | {11} | {12} | {13} | {14} | {15} | {16} | {17} | {18} |",
                    item.Index,
                    item.Id,
                    item.LabwareType,
                    Code(item.ParentId),
                    Text(item.SiteId),
                    item.Track.HasValue ? FormattableString.Invariant($"{item.Track} ({item.TrackWidth}T)") : string.Empty,
                    Code(item.StackId),
                    Mm(item.FinalX),
                    Mm(item.FinalY),
                    Mm(item.FinalZ),
                    Mm(item.OriginX),
                    Mm(item.OriginY),
                    Mm(item.Dx),
                    Mm(item.Dy),
                    Mm(item.Dz),
                    item.Column,
                    item.Row,
                    item.TipRack,
                    item.AlphaIndex));
            }

            sb.AppendLine();
        }

        private static void AppendZCalculation(StringBuilder sb, List<ProcessedLabwareInfo> labware)
        {
            if (labware.Count == 0)
            {
                return;
            }

            sb.AppendLine("## Z Calculation");
            sb.AppendLine();
            sb.AppendLine("ZTransValue 0: Final Z = ZTrans + BaseMM. ZTransValue 1: Final Z = ZTrans + Cntr.1.base + BaseMM. ZTransValue 2 (carriers): Final Z = ZTrans.");
            sb.AppendLine();
            sb.AppendLine("| # | ID | ZTrans | ZTransValue | Cntr.1.base | BaseMM | Final Z | Status |");
            sb.AppendLine("|---|----|--------|-------------|-------------|--------|---------|--------|");

            foreach (var item in labware)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "| {0} | `{1}` | {2} | {3} | {4} | {5} | {6} | {7} |",
                    item.Index,
                    item.Id,
                    Mm(item.ZTrans),
                    item.ZTransValue.HasValue ? item.ZTransValue.Value.ToString(CultureInfo.InvariantCulture) : "—",
                    Mm(item.ContainerBaseOffset),
                    item.ContainerBaseMM.HasValue ? Mm(item.ContainerBaseMM.Value) : "—",
                    Mm(item.FinalZ),
                    item.IsZCalculationIncomplete ? "**Incomplete**" : "OK"));
            }

            sb.AppendLine();
        }

        private static void AppendRackGeometry(StringBuilder sb, List<ProcessedLabwareInfo> labware)
        {
            var racks = labware
                .Where(l => l.LabwareType == LabwareType.Rack || l.LabwareType == LabwareType.RackCarrier)
                .ToList();

            if (racks.Count == 0)
            {
                return;
            }

            sb.AppendLine("## Rack Geometry");
            sb.AppendLine();
            sb.AppendLine("Grid positions are numbered down each column from A1 (1 = A1, 2 = B1, ...). Explicit positions keep the spacing defined in the rack file.");
            sb.AppendLine();
            sb.AppendLine("| # | ID | Layout | Positions | Pitch X | Pitch Y | BndryX | BndryY | UseBndry | Hole Dx | Hole Dy | First Position | Last Position |");
            sb.AppendLine("|---|----|--------|-----------|---------|---------|--------|--------|----------|---------|---------|----------------|---------------|");

            foreach (var rack in racks)
            {
                var first = rack.Positions.Count > 0 ? rack.Positions[0] : null;
                var last = rack.Positions.Count > 0 ? rack.Positions[rack.Positions.Count - 1] : null;

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "| {0} | `{1}` | {2} | {3} | {4} | {5} | {6} | {7} | {8} | {9} | {10} | {11} | {12} |",
                    rack.Index,
                    rack.Id,
                    rack.HasExplicitPositions ? "Explicit" : "Grid",
                    rack.Positions.Count,
                    Mm(rack.PitchX),
                    Mm(rack.PitchY),
                    Mm(rack.BoundaryX),
                    Mm(rack.BoundaryY),
                    rack.UseBoundary ? "1" : "0",
                    Mm(rack.HoleDx),
                    Mm(rack.HoleDy),
                    Position(first),
                    Position(last)));
            }

            sb.AppendLine();
        }

        private static void AppendCarrierSites(StringBuilder sb, List<ProcessedLabwareInfo> labware)
        {
            var carriers = labware.Where(l => l.Sites.Count > 0).ToList();

            if (carriers.Count == 0)
            {
                return;
            }

            sb.AppendLine("## Carrier Sites");
            sb.AppendLine();
            sb.AppendLine("Site coordinates are the front-left corner of each site on the deck; Z is the height of the site surface.");
            sb.AppendLine();

            foreach (var carrier in carriers)
            {
                sb.AppendLine($"### `{carrier.Id}`");
                sb.AppendLine();
                sb.AppendLine("| Site | X | Y | Z | Dx | Dy | Stack Size | Labware |");
                sb.AppendLine("|------|---|---|---|----|----|------------|---------|");

                // Back to front, as the sites appear on the deck.
                foreach (var site in carrier.Sites.OrderByDescending(s => s.AbsoluteY))
                {
                    var placed = labware
                        .Where(l => string.Equals(l.ParentId, carrier.Id, StringComparison.OrdinalIgnoreCase)
                                 && string.Equals(l.SiteId, site.Id, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(l => l.FinalZ)
                        .Select(l => $"`{l.Id}`");

                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} |",
                        Text(site.Id),
                        Mm(site.AbsoluteX),
                        Mm(site.AbsoluteY),
                        Mm(site.AbsoluteZ),
                        Mm(site.Dx),
                        Mm(site.Dy),
                        site.StackSize,
                        string.Join(", ", placed)));
                }

                sb.AppendLine();
            }
        }

        private static void AppendNotes(StringBuilder sb, List<ProcessedLabwareInfo> labware)
        {
            sb.AppendLine("## Geometry Notes and Warnings");
            sb.AppendLine();

            var flagged = labware
                .Where(l => l.GeometryNotes.Count > 0 || !string.IsNullOrEmpty(l.ValidationWarning))
                .ToList();

            if (flagged.Count == 0)
            {
                sb.AppendLine("No geometry notes or warnings.");
                sb.AppendLine();
                return;
            }

            foreach (var item in flagged)
            {
                if (!string.IsNullOrEmpty(item.ValidationWarning))
                {
                    sb.AppendLine($"- `{item.Id}` **Warning:** {item.ValidationWarning}");
                }

                foreach (var note in item.GeometryNotes)
                {
                    sb.AppendLine($"- `{item.Id}`: {note}");
                }
            }

            sb.AppendLine();
        }

        private static void AppendSequences(StringBuilder sb, List<SequenceInfo> sequences)
        {
            sb.AppendLine("## Sequence Information");
            sb.AppendLine();

            if (sequences == null || sequences.Count == 0)
            {
                sb.AppendLine("No sequence information found in this layout file.");
                return;
            }

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

        private static string Mm(double value)
        {
            return value.ToString("F3", CultureInfo.InvariantCulture);
        }

        private static string Code(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : $"`{value}`";
        }

        private static string Text(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("|", "\\|");
        }

        private static string Position(LabwarePosition position)
        {
            return position == null
                ? "—"
                : FormattableString.Invariant($"{position.Name} ({position.X:F3}, {position.Y:F3})");
        }

        #endregion

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