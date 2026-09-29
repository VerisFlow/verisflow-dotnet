using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using VerisFlow.LayParser.Core;

namespace VerisFlow.VenusDeckParser.Desktop
{
    public partial class DeckHierarchyWindow : Window
    {
        private readonly List<ProcessedLabwareInfo> _sourceData;
        private ObservableCollection<HierarchyNodeViewModel> _treeNodes;

        public DeckHierarchyWindow(List<ProcessedLabwareInfo> sourceData) : this(sourceData, string.Empty)
        {
        }

        public DeckHierarchyWindow(List<ProcessedLabwareInfo> sourceData, string instrumentName)
        {
            InitializeComponent();
            _sourceData = sourceData ?? new List<ProcessedLabwareInfo>();
            _treeNodes = new ObservableCollection<HierarchyNodeViewModel>();
            HierarchyTreeView.ItemsSource = _treeNodes;

            if (!string.IsNullOrWhiteSpace(instrumentName))
            {
                Title = $"Deck Layout Hierarchy - {instrumentName}";
                InstrumentHeaderTextBlock.Text = $"Instrument: {instrumentName}";
                InstrumentHeaderBadge.Visibility = Visibility.Visible;
            }

            BuildHierarchy(double.MinValue, double.MaxValue);
        }

        private void ApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            double minX = double.MinValue;
            double maxX = double.MaxValue;

            if (double.TryParse(MinXTextBox.Text, out double parsedMin))
            {
                minX = parsedMin;
            }

            if (double.TryParse(MaxXTextBox.Text, out double parsedMax))
            {
                maxX = parsedMax;
            }

            BuildHierarchy(minX, maxX);
        }

        /// <summary>
        /// Rebuilds the tree from the parent relation of the layout (Template/ParentId): carriers and rack carriers are
        /// roots, labware is placed under its parent carrier. The X filter applies to the roots; children follow their parent.
        /// Labware whose parent is not in the layout is shown as a root.
        /// </summary>
        private void BuildHierarchy(double minX, double maxX)
        {
            _treeNodes.Clear();

            var ids = new HashSet<string>(_sourceData.Select(l => l.Id), StringComparer.OrdinalIgnoreCase);

            var roots = _sourceData
                .Where(l => string.IsNullOrEmpty(l.ParentId) || !ids.Contains(l.ParentId))
                .Where(l => l.FinalX >= minX && l.FinalX <= maxX)
                .OrderBy(l => l.FinalX);

            foreach (var root in roots)
            {
                var rootNode = CreateNode(root);

                // Back to front; labware stacked on the same site from bottom to top.
                var children = _sourceData
                    .Where(l => string.Equals(l.ParentId, root.Id, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(l => l.FinalY)
                    .ThenBy(l => l.FinalZ);

                foreach (var child in children)
                {
                    rootNode.Children.Add(CreateNode(child));
                }

                _treeNodes.Add(rootNode);
            }
        }

        private static HierarchyNodeViewModel CreateNode(ProcessedLabwareInfo item)
        {
            bool isCarrier = item.LabwareType == LabwareType.Carrier || item.LabwareType == LabwareType.RackCarrier;

            string displayText = item.Id;
            if (!string.IsNullOrEmpty(item.StackId))
            {
                displayText += $"  [stack {item.StackId}]";
            }

            return new HierarchyNodeViewModel
            {
                IsCarrier = isCarrier,
                DisplayText = displayText,
                FinalX = item.FinalX,
                FinalY = item.FinalY,
                FinalZ = item.FinalZ,
                FontWeight = isCarrier ? FontWeights.Bold : FontWeights.Normal
            };
        }
    }

    /// <summary>
    /// A lightweight View-Model to support hierarchical binding in the TreeView.
    /// </summary>
    public class HierarchyNodeViewModel
    {
        public bool IsCarrier { get; set; }
        public string DisplayText { get; set; } = string.Empty;
        public double FinalX { get; set; }
        public double FinalY { get; set; }
        public double FinalZ { get; set; }
        public FontWeight FontWeight { get; set; }
        public ObservableCollection<HierarchyNodeViewModel> Children { get; set; } = new ObservableCollection<HierarchyNodeViewModel>();
    }
}