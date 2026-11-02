using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using _8F.Models;
using _8F.Services;
using _8F.ViewModels;

namespace _8F.Views
{
    public partial class ChannelPanel : UserControl
    {
        public ChannelViewModel ViewModel { get; }

        private double _factor = 20.0;
        private int _channelId = 1;
        private int _frequencyNo = 8;
        private ChannelData? _channelData;
        private double _currentS = 250.0;

        private readonly SolidColorBrush _enableColor = new(Colors.White);
        private readonly SolidColorBrush _disableColor = new(Colors.LightGray);
        private readonly SolidColorBrush _blueBrush = new(Colors.Blue);
        private readonly SolidColorBrush _brownBrush = new(Colors.Brown);
        private readonly SolidColorBrush _greenBrush = new(Colors.Green);
        private readonly SolidColorBrush _redBrush = new(Colors.Red);
        private readonly SolidColorBrush _whiteBrush = new(Colors.White);
        private readonly SolidColorBrush _defaultOverallBrush = new(Color.FromRgb(48, 83, 113));
        private readonly SolidColorBrush _gridBrush = new(Color.FromRgb(215, 215, 215)); // Matching Image 2
        private readonly SolidColorBrush _axisBrush = new(Color.FromRgb(85, 85, 85));   // Dark grey #555555 matching Image 2

        private Border[] _brCells = Array.Empty<Border>();
        private Canvas[] _canvases = Array.Empty<Canvas>();
        private Rectangle[] _recBgs = Array.Empty<Rectangle>();
        private Canvas[] _cnGrids = Array.Empty<Canvas>();
        private Line[] _axisHs = Array.Empty<Line>();
        private Line[] _axisVs = Array.Empty<Line>();
        private Canvas[] _cnBrs = Array.Empty<Canvas>();
        private Canvas[] _cnPoints = Array.Empty<Canvas>();
        private TextBlock[] _lblFreqs = Array.Empty<TextBlock>();
        private Canvas[] _canXYs = Array.Empty<Canvas>();
        private TextBlock[] _lblGraphXYs = Array.Empty<TextBlock>();
        private Rectangle[] _rResults = Array.Empty<Rectangle>();
        private Border[] _btnSettings = Array.Empty<Border>();

        public event EventHandler? PanelActivated;
        public event EventHandler<bool>? MaximizeToggled;
        public event EventHandler<int>? SettingRequested;
        public event EventHandler? BalanceRequested;
        public event EventHandler? TestRequested;
        public event EventHandler? ClearRequested;
        public event EventHandler? LogRequested;
        public event EventHandler? ResetCountRequested;

        public ChannelPanel()
        {
            InitializeComponent();
            ViewModel = new ChannelViewModel();
            DataContext = ViewModel;

            _gridBrush.Freeze();
            _axisBrush.Freeze();

            Loaded += (s, e) =>
            {
                InitControlArrays();
                if (gridPlotsContainer.ActualWidth > 0 && gridPlotsContainer.ActualHeight > 0)
                {
                    UpdatePlotSizes(gridPlotsContainer.ActualWidth, gridPlotsContainer.ActualHeight);
                }
                else
                {
                    ApplySizeS(_currentS);
                }
            };
        }

        private void InitControlArrays()
        {
            if (_canvases.Length > 0) return;

            _brCells = new[] { brCell1, brCell2, brCell3, brCell4, brCell5, brCell6, brCell7, brCell8 };
            _canvases = new[] { Canvas1, Canvas2, Canvas3, Canvas4, Canvas5, Canvas6, Canvas7, Canvas8 };
            _recBgs = new[] { recBg1, recBg2, recBg3, recBg4, recBg5, recBg6, recBg7, recBg8 };
            _cnGrids = new[] { cnGrid1, cnGrid2, cnGrid3, cnGrid4, cnGrid5, cnGrid6, cnGrid7, cnGrid8 };
            _axisHs = new[] { axisH1, axisH2, axisH3, axisH4, axisH5, axisH6, axisH7, axisH8 };
            _axisVs = new[] { axisV1, axisV2, axisV3, axisV4, axisV5, axisV6, axisV7, axisV8 };
            _cnBrs = new[] { cnBr1, cnBr2, cnBr3, cnBr4, cnBr5, cnBr6, cnBr7, cnBr8 };
            _cnPoints = new[] { cnPoints1, cnPoints2, cnPoints3, cnPoints4, cnPoints5, cnPoints6, cnPoints7, cnPoints8 };
            _lblFreqs = new[] { lblFreq1, lblFreq2, lblFreq3, lblFreq4, lblFreq5, lblFreq6, lblFreq7, lblFreq8 };
            _canXYs = new[] { canXY1, canXY2, canXY3, canXY4, canXY5, canXY6, canXY7, canXY8 };
            _lblGraphXYs = new[] { lblGraphXY1, lblGraphXY2, lblGraphXY3, lblGraphXY4, lblGraphXY5, lblGraphXY6, lblGraphXY7, lblGraphXY8 };
            _rResults = new[] { rResult1, rResult2, rResult3, rResult4, rResult5, rResult6, rResult7, rResult8 };
            _btnSettings = new[] { btnSetting1, btnSetting2, btnSetting3, btnSetting4, btnSetting5, btnSetting6, btnSetting7, btnSetting8 };

            for (int i = 0; i < 8; i++)
            {
                _lblGraphXYs[i].Text = "";
                _lblGraphXYs[i].Background = Brushes.Transparent;
            }
        }

        private void OnPlotsAreaSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdatePlotSizes(e.NewSize.Width, e.NewSize.Height);
        }

        public void UpdatePlotSizes(double availW, double availH)
        {
            if (availW <= 0 || availH <= 0) return;
            InitControlArrays();

            // 4 columns with 8 px gap (Margin="4" on each border => 4*2*4 = 32px total horizontal margin)
            double sw = (availW - 32.0) / 4.0;
            // 2 rows with 8 px gap (Margin="4" on each border => 2*2*4 = 16px total vertical margin)
            double sh = (availH - 16.0) / 2.0;

            double s = Math.Floor(Math.Min(sw, sh));
            if (s < 100) s = 100;

            if (Math.Abs(s - _currentS) < 0.5 && _cnGrids[0].Children.Count > 0) return;
            _currentS = s;

            ApplySizeS(s);
        }

        private void ApplySizeS(double s)
        {
            double mid = Math.Round(s / 2.0);
            double step = s / 25.0; // 25 divisions matching legacy 250 / 10

            for (int p = 0; p < 8; p++)
            {
                // Cell & Canvas size
                _brCells[p].Width = s;
                _brCells[p].Height = s;
                _canvases[p].Width = s;
                _canvases[p].Height = s;

                // Background rectangle
                _recBgs[p].Width = s;
                _recBgs[p].Height = s;

                // Fine Grid lines (1 px, crisp, matching Image 2)
                _cnGrids[p].Width = s;
                _cnGrids[p].Height = s;
                _cnGrids[p].Children.Clear();

                for (int i = 1; i <= 24; i++)
                {
                    double pos = Math.Round(i * step);
                    if (Math.Abs(pos - mid) < 1.0) continue; // Skip center axis

                    // Horizontal minor grid line
                    Line hLine = new Line
                    {
                        X1 = 0,
                        Y1 = pos,
                        X2 = s,
                        Y2 = pos,
                        Stroke = _gridBrush,
                        StrokeThickness = 1.0,
                        SnapsToDevicePixels = true
                    };
                    RenderOptions.SetEdgeMode(hLine, EdgeMode.Aliased);
                    _cnGrids[p].Children.Add(hLine);

                    // Vertical minor grid line
                    Line vLine = new Line
                    {
                        X1 = pos,
                        Y1 = 0,
                        X2 = pos,
                        Y2 = s,
                        Stroke = _gridBrush,
                        StrokeThickness = 1.0,
                        SnapsToDevicePixels = true
                    };
                    RenderOptions.SetEdgeMode(vLine, EdgeMode.Aliased);
                    _cnGrids[p].Children.Add(vLine);
                }

                // Major axes (Dark grey #555555, 1px, above grid)
                _axisHs[p].X1 = 0;
                _axisHs[p].Y1 = mid;
                _axisHs[p].X2 = s;
                _axisHs[p].Y2 = mid;
                _axisHs[p].Stroke = _axisBrush;
                _axisHs[p].StrokeThickness = 1.0;
                _axisHs[p].SnapsToDevicePixels = true;
                RenderOptions.SetEdgeMode(_axisHs[p], EdgeMode.Aliased);

                _axisVs[p].X1 = mid;
                _axisVs[p].Y1 = 0;
                _axisVs[p].X2 = mid;
                _axisVs[p].Y2 = s;
                _axisVs[p].Stroke = _axisBrush;
                _axisVs[p].StrokeThickness = 1.0;
                _axisVs[p].SnapsToDevicePixels = true;
                RenderOptions.SetEdgeMode(_axisVs[p], EdgeMode.Aliased);

                // Position cnBr (ellipses & points) at center
                Canvas.SetLeft(_cnBrs[p], mid);
                Canvas.SetTop(_cnBrs[p], mid);

                // Chrome: Setting button anchored to bottom-left (5 px margin)
                Canvas.SetLeft(_btnSettings[p], 5);
                Canvas.SetTop(_btnSettings[p], s - 29); // 24 height + 5 margin

                // Chrome: Checkbox / Result square anchored to bottom-right (5 px margin)
                Canvas.SetLeft(_rResults[p], s - 25); // 20 width + 5 margin
                Canvas.SetTop(_rResults[p], s - 25);

                // Chrome: Readout label at top-right
                Canvas.SetLeft(_canXYs[p], s);
                Canvas.SetTop(_canXYs[p], 0);
            }

            // Redraw thresholds with new scale factor
            if (_channelData != null)
            {
                UpdateThresholds(_channelData, _frequencyNo);
            }
            var responses = DeviceCOM.GetResponsesSnapshot(_channelId).Where(r => !r.IsAutoEllipseTest).ToList();
            RefreshPlotData(responses);
        }

        public void Initialize(int channelId, string channelName, double factor = 20)
        {
            InitializeChannel(channelId, channelName, factor);
        }

        public void InitializeChannel(int channelId, string channelName, double factor)
        {
            _channelId = channelId;
            _factor = factor <= 0 ? 20.0 : factor;
            ViewModel.ChannelId = channelId;
            ViewModel.ChannelName = channelName;
        }

        public void UpdateThresholds()
        {
            var chData = DeviceCOM.channelDatas?.FirstOrDefault(c => c.Id == _channelId);
            if (chData != null)
            {
                UpdateThresholds(chData, _frequencyNo > 0 ? _frequencyNo : 8);
            }
        }

        public void RefreshPlotData()
        {
            var responses = DeviceCOM.GetResponsesSnapshot(_channelId).Where(r => !r.IsAutoEllipseTest).ToList();
            RefreshPlotData(responses);
        }

        public void UpdateCounters()
        {
            var cnt = DeviceCOM.counter?.FirstOrDefault(c => c.Id == _channelId);
            UpdateCounters(cnt);
        }

        public void ClearPlotData()
        {
            ClearPlotData(true);
        }

        public void SetMaximized(bool isMax)
        {
            SetMaximized(isMax, null, null);
        }

        public void UpdateThresholds(ChannelData chData, int frequencyNo = 8)
        {
            _channelData = chData;
            _frequencyNo = frequencyNo;
            InitControlArrays();
            if (chData?.graphDatas == null) return;

            for (int i = 0; i < 8; i++)
            {
                int plotId = i + 1;
                var gData = chData.graphDatas.FirstOrDefault(g => g.Id == plotId);

                if (gData != null && gData.isEnable && plotId <= frequencyNo)
                {
                    _lblFreqs[i].Text = $"{gData.Name}-{gData.freq}Hz";
                    _recBgs[i].Fill = _enableColor;
                    _brCells[i].IsEnabled = true;
                    _brCells[i].Opacity = 1.0;

                    AddEllipsesToCanvas(_cnBrs[i], gData);
                }
                else
                {
                    _lblFreqs[i].Text = gData != null ? $"{gData.Name}" : $"D{plotId}";
                    _recBgs[i].Fill = _disableColor;
                    _brCells[i].IsEnabled = false;
                    _brCells[i].Opacity = 0.7;

                    // Clear any ellipses
                    for (int c = _cnBrs[i].Children.Count - 1; c >= 1; c--)
                    {
                        _cnBrs[i].Children.RemoveAt(c);
                    }
                }
            }
        }

        private void AddEllipsesToCanvas(Canvas cnBr, GraphData graphData)
        {
            // Child 0 is cnPoints, preserve it!
            for (int c = cnBr.Children.Count - 1; c >= 1; c--)
            {
                cnBr.Children.RemoveAt(c);
            }

            if (graphData.ellipses == null) return;

            // Scale factor relative to baseline seqLength = 250
            double effectiveFactor = _factor * (250.0 / _currentS);

            foreach (var item in graphData.ellipses)
            {
                int index = graphData.ellipses.IndexOf(item);
                Ellipse el = new Ellipse
                {
                    Fill = Brushes.Transparent,
                    Height = item.height / effectiveFactor,
                    Width = item.width / effectiveFactor,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Stroke = new SolidColorBrush(MyColor.GetColor(index)),
                    StrokeThickness = 1.0,
                    RenderTransformOrigin = new Point(0, 0),
                    SnapsToDevicePixels = true
                };
                RenderOptions.SetEdgeMode(el, EdgeMode.Aliased);

                Canvas.SetLeft(el, 0);
                Canvas.SetTop(el, 0);

                TranslateTransform tt = new TranslateTransform
                {
                    X = ((item.ex - (item.width / 2.0)) / effectiveFactor),
                    Y = (((item.ey * -1.0) - (item.height / 2.0)) / effectiveFactor)
                };

                RotateTransform rt = new RotateTransform
                {
                    CenterX = el.Width / 2.0,
                    CenterY = el.Height / 2.0,
                    Angle = item.angel * -1.0
                };

                TransformGroup tg = new TransformGroup();
                tg.Children.Add(rt);
                tg.Children.Add(tt);
                el.RenderTransform = tg;

                cnBr.Children.Add(el);
            }
        }

        public void RefreshPlotData(List<Response> channelResponses)
        {
            InitControlArrays();
            if (channelResponses == null || channelResponses.Count == 0) return;

            bool anyTest = false;
            int overallResult = 0;
            double effectiveFactor = _factor * (250.0 / _currentS);

            foreach (var res in channelResponses)
            {
                if (res.FC == 20) // Balance response
                {
                    for (int i = 0; i < 8; i++)
                    {
                        _cnPoints[i].Children.Clear();
                        _lblGraphXYs[i].Text = "";
                        _lblGraphXYs[i].Background = Brushes.Transparent;
                        _rResults[i].Fill = _whiteBrush;

                        Ellipse pt = new Ellipse
                        {
                            Height = 4,
                            Width = 4,
                            Fill = _blueBrush,
                            Stroke = _blueBrush,
                            RenderTransformOrigin = new Point(0, 0)
                        };
                        Canvas.SetLeft(pt, -2);
                        Canvas.SetTop(pt, -2);
                        _cnPoints[i].Children.Add(pt);
                    }
                }
                else if (res.FC == 22) // Test complete response
                {
                    anyTest = true;
                    overallResult = res.OR;

                    for (int i = 0; i < 8; i++)
                    {
                        int fn = i + 1;
                        var fd = res.FD?.FirstOrDefault(f => f.FN == fn);
                        if (fd != null)
                        {
                            _lblGraphXYs[i].Text = $"{fd.X},{fd.Y}";
                            _lblGraphXYs[i].Background = Brushes.LightBlue;

                            if (fd.R == 0)
                            {
                                _rResults[i].Fill = _greenBrush;
                            }
                            else
                            {
                                _rResults[i].Fill = _redBrush;
                            }

                            Ellipse pt = new Ellipse
                            {
                                Height = 4,
                                Width = 4,
                                Fill = _brownBrush,
                                Stroke = _brownBrush,
                                RenderTransformOrigin = new Point(0, 0)
                            };
                            double posX = (fd.X / effectiveFactor) - 2;
                            double posY = ((fd.Y * -1) / effectiveFactor) - 2;
                            Canvas.SetLeft(pt, posX);
                            Canvas.SetTop(pt, posY);
                            _cnPoints[i].Children.Add(pt);
                        }
                    }
                }
            }

            if (anyTest)
            {
                ViewModel.OverallResultBrush = (overallResult == 0) ? _greenBrush : _redBrush;
            }
            else
            {
                ViewModel.OverallResultBrush = _defaultOverallBrush;
            }
        }

        public void UpdateCounters(Counter? cnt)
        {
            if (cnt != null)
            {
                ViewModel.TotalCount = cnt.ResultCount;
                ViewModel.OkCount = cnt.ResultOkCount;
                ViewModel.NotOkCount = cnt.ResultOkNotCount;
            }
            else
            {
                ViewModel.TotalCount = 0;
                ViewModel.OkCount = 0;
                ViewModel.NotOkCount = 0;
            }
        }

        public void ClearPlotData(bool keepBalanced)
        {
            InitControlArrays();

            for (int i = 0; i < 8; i++)
            {
                _cnPoints[i].Children.Clear();
                _lblGraphXYs[i].Text = "";
                _lblGraphXYs[i].Background = Brushes.Transparent;
                _rResults[i].Fill = _whiteBrush;
            }
            ViewModel.OverallResultBrush = _defaultOverallBrush;

            if (keepBalanced)
            {
                var balData = DeviceCOM.GetResponsesSnapshot(_channelId).Where(r => r.IsBalacenced).ToList();
                if (balData.Count > 0)
                {
                    RefreshPlotData(balData);
                }
            }
        }

        public void FullRedraw()
        {
            InitControlArrays();
            if (gridPlotsContainer.ActualWidth > 0 && gridPlotsContainer.ActualHeight > 0)
            {
                UpdatePlotSizes(gridPlotsContainer.ActualWidth, gridPlotsContainer.ActualHeight);
            }
            else
            {
                ApplySizeS(_currentS);
            }
        }

        public void SetMaximized(bool isMax, string? hiddenStatusText, Brush? hiddenStatusBrush)
        {
            ViewModel.IsMaximized = isMax;
            if (isMax && !string.IsNullOrEmpty(hiddenStatusText))
            {
                chipHiddenStatus.Visibility = Visibility.Visible;
                ViewModel.HiddenChannelText = hiddenStatusText;
                ViewModel.HiddenChannelBrush = hiddenStatusBrush ?? Brushes.Gray;
            }
            else
            {
                chipHiddenStatus.Visibility = Visibility.Collapsed;
            }
        }

        public void GetStatusSummary(out string statusText, out Brush statusBrush)
        {
            var cnt = DeviceCOM.counter.FirstOrDefault(c => c.Id == _channelId);
            int ok = cnt?.ResultOkCount ?? 0;
            int notOk = cnt?.ResultOkNotCount ?? 0;
            string chName = $"CH-{_channelId}";

            if (DeviceCOM.IsCommandInFlight && DeviceCOM.PendingCommandChannelId == _channelId)
            {
                statusText = $"{chName}: BUSY";
                statusBrush = new SolidColorBrush(Color.FromRgb(255, 152, 0)); // Orange
            }
            else if (notOk > 0)
            {
                statusText = $"{chName}: NG ({notOk})";
                statusBrush = _redBrush;
            }
            else if (ok > 0)
            {
                statusText = $"{chName}: OK ({ok})";
                statusBrush = _greenBrush;
            }
            else if (DeviceCOM.IsChannelBalanced(_channelId))
            {
                statusText = $"{chName}: BALANCED";
                statusBrush = _greenBrush;
            }
            else
            {
                statusText = $"{chName}: IDLE";
                statusBrush = Brushes.Gray;
            }
        }

        private void OnPanelMouseDown(object sender, MouseButtonEventArgs e)
        {
            PanelActivated?.Invoke(this, EventArgs.Empty);
        }

        private void OnHeaderMouseDown(object sender, MouseButtonEventArgs e)
        {
            PanelActivated?.Invoke(this, EventArgs.Empty);
        }

        private void OnMaximizeClick(object sender, RoutedEventArgs e)
        {
            PanelActivated?.Invoke(this, EventArgs.Empty);
            bool willMaximize = !ViewModel.IsMaximized;
            MaximizeToggled?.Invoke(this, willMaximize);
        }

        private void OnSettingClick(object sender, MouseButtonEventArgs e)
        {
            PanelActivated?.Invoke(this, EventArgs.Empty);
            if (sender is FrameworkElement elem && int.TryParse(elem.Tag?.ToString(), out int plotIdx))
            {
                SettingRequested?.Invoke(this, plotIdx);
            }
        }

        private void OnBalanceClick(object sender, MouseButtonEventArgs e)
        {
            PanelActivated?.Invoke(this, EventArgs.Empty);
            BalanceRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnTestClick(object sender, MouseButtonEventArgs e)
        {
            PanelActivated?.Invoke(this, EventArgs.Empty);
            TestRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnClearClick(object sender, MouseButtonEventArgs e)
        {
            PanelActivated?.Invoke(this, EventArgs.Empty);
            ClearRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnLogClick(object sender, MouseButtonEventArgs e)
        {
            PanelActivated?.Invoke(this, EventArgs.Empty);
            LogRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnResetCountClick(object sender, MouseButtonEventArgs e)
        {
            PanelActivated?.Invoke(this, EventArgs.Empty);
            ResetCountRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
