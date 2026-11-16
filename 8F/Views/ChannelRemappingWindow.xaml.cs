using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace _8F
{
    /// <summary>
    /// Interaction logic for ChannelRemappingWindow.xaml
    /// </summary>
    public partial class ChannelRemappingWindow : Window
    {
        public bool IsConfirmed { get; private set; } = false;
        public bool IsImportAsIs { get; private set; } = false;

        // Key = Source Channel ID, Value = List of Target Channel IDs (1..4)
        public Dictionary<int, List<int>> TargetMappings { get; private set; } = new();

        private readonly List<ChannelData> _incomingChannels;
        private readonly int? _defaultTargetChannel;
        private readonly Dictionary<int, List<CheckBox>> _rowCheckBoxes = new();

        public ChannelRemappingWindow(
            List<ChannelData> incomingChannels,
            string sourceLabel,
            int? defaultTargetChannel = null)
        {
            InitializeComponent();
            _incomingChannels = incomingChannels ?? new List<ChannelData>();
            _defaultTargetChannel = defaultTargetChannel;

            Title = "Channel Remapping";
            if (lblHeaderTitle != null) lblHeaderTitle.Content = "Import Channel Remapping";
            if (btnImportAsIs != null) btnImportAsIs.Content = "Import As-Is (Exact Match)";

            lblSourceDescription.Text = $"Source Data: \"{sourceLabel}\". Configure target channel assignments below:";
            BuildMappingRows();
        }

        private void BuildMappingRows()
        {
            pnlMappingRows.Children.Clear();
            _rowCheckBoxes.Clear();

            if (_incomingChannels.Count == 0) return;

            var visibleChannels = _incomingChannels;

            int preferredSourceId = 1;
            if (_defaultTargetChannel.HasValue)
            {
                var match = visibleChannels.FirstOrDefault(c => c.Id == _defaultTargetChannel.Value);
                preferredSourceId = match != null ? match.Id : visibleChannels.OrderBy(c => c.Id).First().Id;
            }

            const int maxTargets = 4;

            foreach (var srcCh in visibleChannels)
            {
                Border rowBorder = new Border
                {
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 250, 252)),
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 0, 0, 8)
                };

                Grid rowGrid = new Grid();
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) }); // Source Label
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Target CheckBoxes

                Label lblSource = new Label
                {
                    Content = $"Source Channel {srcCh.Id}",
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(lblSource, 0);
                rowGrid.Children.Add(lblSource);

                StackPanel pnlTargets = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center
                };

                List<CheckBox> checkBoxes = new();
                for (int targetCh = 1; targetCh <= maxTargets; targetCh++)
                {
                    bool isDefaultChecked = _defaultTargetChannel.HasValue
                        ? (srcCh.Id == preferredSourceId && targetCh == _defaultTargetChannel.Value)
                        : (targetCh == srcCh.Id);

                    CheckBox chk = new CheckBox
                    {
                        Content = $"Target Ch {targetCh}",
                        FontSize = 13,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42)),
                        Margin = new Thickness(0, 0, 16, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Tag = targetCh,
                        IsChecked = isDefaultChecked
                    };

                    chk.Checked += Chk_CheckedChanged;
                    chk.Unchecked += Chk_CheckedChanged;
                    checkBoxes.Add(chk);
                    pnlTargets.Children.Add(chk);
                }

                _rowCheckBoxes[srcCh.Id] = checkBoxes;
                Grid.SetColumn(pnlTargets, 1);
                rowGrid.Children.Add(pnlTargets);

                rowBorder.Child = rowGrid;
                pnlMappingRows.Children.Add(rowBorder);
            }

            ValidateConflicts();
        }

        private void Chk_CheckedChanged(object sender, RoutedEventArgs e)
        {
            ValidateConflicts();
        }

        private void ValidateConflicts()
        {
            const int maxTargets = 4;

            Dictionary<int, List<int>> targetCounts = new();
            for (int t = 1; t <= maxTargets; t++) targetCounts[t] = new List<int>();

            foreach (var kvp in _rowCheckBoxes)
            {
                int srcId = kvp.Key;
                foreach (var chk in kvp.Value)
                {
                    if (chk.IsChecked == true && chk.Tag is int targetId && targetCounts.ContainsKey(targetId))
                    {
                        targetCounts[targetId].Add(srcId);
                    }
                }
            }

            List<int> conflictingTargets = targetCounts.Where(kvp => kvp.Value.Count > 1).Select(kvp => kvp.Key).ToList();

            if (conflictingTargets.Count > 0)
            {
                brdWarning.Visibility = Visibility.Visible;
                lblWarning.Text = $"⚠️ Conflict Warning: Target Channel(s) {string.Join(", ", conflictingTargets)} selected by multiple source channels. Settings will be applied in row order.";
            }
            else
            {
                brdWarning.Visibility = Visibility.Collapsed;
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try
                {
                    DragMove();
                }
                catch (InvalidOperationException)
                {
                    // Ignore DragMove exceptions if mouse state changes mid-click
                }
            }
        }

        private void btnImportAsIs_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = true;
            IsImportAsIs = true;
            DialogResult = true;
            Close();
        }

        private void btnApplyMapping_Click(object sender, RoutedEventArgs e)
        {
            TargetMappings.Clear();
            foreach (var kvp in _rowCheckBoxes)
            {
                int srcId = kvp.Key;
                List<int> targetList = new();
                foreach (var chk in kvp.Value)
                {
                    if (chk.IsChecked == true && chk.Tag is int targetId)
                    {
                        targetList.Add(targetId);
                    }
                }
                TargetMappings[srcId] = targetList;
            }

            if (!TargetMappings.Values.Any(list => list.Count > 0))
            {
                MessageBox.Show("Please select at least one target channel.", "Channel Remapping", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsConfirmed = true;
            IsImportAsIs = false;
            DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            DialogResult = false;
            Close();
        }
    }
}
