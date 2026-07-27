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

        public DeckHierarchyWindow(List<ProcessedLabwareInfo> sourceData)
        {
            InitializeComponent();
            _sourceData = sourceData;
            _treeNodes = new ObservableCollection<HierarchyNodeViewModel>();
            HierarchyTreeView.ItemsSource = _treeNodes;

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
        /// Rebuilds the tree view hierarchy based on the provided X-axis bounds.
        /// Labware instances implicitly belong to the Carrier that immediately precedes them.
        /// </summary>
        private void BuildHierarchy(double minX, double maxX)
        {
            _treeNodes.Clear();
            HierarchyNodeViewModel currentCarrierNode = null;

            foreach (var item in _sourceData)
            {
                if (item.FinalX < minX || item.FinalX > maxX)
                {
                    continue;
                }

                bool isCarrier = item.LabwareType == LabwareType.Carrier || item.LabwareType == LabwareType.RackCarrier;

                var node = new HierarchyNodeViewModel
                {
                    IsCarrier = isCarrier,
                    DisplayText = item.Id,
                    FinalX = item.FinalX,
                    FinalY = item.FinalY,
                    FinalZ = item.FinalZ,
                    FontWeight = isCarrier ? FontWeights.Bold : FontWeights.Normal
                };

                if (isCarrier)
                {
                    currentCarrierNode = node;
                    _treeNodes.Add(currentCarrierNode);
                }
                else
                {
                    if (currentCarrierNode != null)
                    {
                        currentCarrierNode.Children.Add(node);
                    }
                    else
                    {
                        // Handles edge cases where a labware appears before any carrier
                        _treeNodes.Add(node);
                    }
                }
            }

            foreach (var rootNode in _treeNodes)
            {
                if (rootNode.Children.Count > 1)
                {
                    var sortedChildren = rootNode.Children.OrderByDescending(c => c.FinalY).ToList();
                    rootNode.Children.Clear();
                    foreach (var child in sortedChildren)
                    {
                        rootNode.Children.Add(child);
                    }
                }
            }

            if (_treeNodes.Count > 1)
            {
                var sortedRoots = _treeNodes.OrderBy(n => n.FinalX).ToList();
                _treeNodes.Clear();
                foreach (var root in sortedRoots)
                {
                    _treeNodes.Add(root);
                }
            }
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