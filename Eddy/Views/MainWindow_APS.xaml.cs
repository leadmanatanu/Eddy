using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;
using OpenTK.Compute.OpenCL;
using OpenTK.Windowing.Common.Input;
using ScottPlot;
using ScottPlot.Colormaps;
using ScottPlot.Interactivity;
using ScottPlot.Plottables;
using ScottPlot.TickGenerators;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Security.Policy;
using System.Text;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using static SkiaSharp.HarfBuzz.SKShaper;
using Colors = System.Windows.Media.Colors;
using Ellipse = System.Windows.Shapes.Ellipse;

namespace Eddy
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow_APS : Window
    {
        public ObservableCollection<MenuItemViewModel_APS> MenuItems { get; set; } = new ObservableCollection<MenuItemViewModel_APS>();
        ScottPlot.Plot myPlot1 = null!;
        ScottPlot.Plot myPlot2 = null!;
        //ScottPlot.Plot myPlot3;
        ScottPlot.Plot myPlot4 = null!;
        ScottPlot.Plot myPlotA1Last = null!;
        // setup a logger that will grow as data is added
        DataStreamer logger1 = null!;
        DataStreamer logger2 = null!;
        //DataLogger logger3;
        DataLogger logger4 = null!;
        DataLogger loggerA1Last = null!;
        public DeviceCOM deviceCOM = null!;
        public string? filename { get; set; }

        DispatcherTimer dispatcherTimerui = null!;
        public PartConfig? partConfig { get; set; }

        UdpReceiver receiver = null!;
        string IpAddress = string.Empty;
        int Port;
        string resultStatus = String.Empty;



        public MainWindow_APS()
        {
            InitializeComponent();
            //resultStatus = "Invalid Result!!";
            //DeviceCOM.Ok = 100;
            //DeviceCOM.NoOk = 200;
            DeviceCOM.IsAttRequired = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["IsAttRequired"]);

            MenuItems = new ObservableCollection<MenuItemViewModel_APS>
            {
                new MenuItemViewModel_APS { Header = "File",
                    MenuItems = new ObservableCollection<MenuItemViewModel_APS>
                        {
                            new MenuItemViewModel_APS { Header = "New", mainWindow =this },
                            new MenuItemViewModel_APS { Header = "Open" ,mainWindow =this },
                            new MenuItemViewModel_APS { Header = "Save", mainWindow =this },
                            new MenuItemViewModel_APS { Header = "Save As", mainWindow =this },
                            new MenuItemViewModel_APS { Header = "Exit" ,mainWindow =this }
                        }
                },
                new MenuItemViewModel_APS { Header = "Configuration",
                    MenuItems =
                    (DeviceCOM.IsAttRequired==true ?new ObservableCollection<MenuItemViewModel_APS>
                        {
                            new MenuItemViewModel_APS { Header = "Marker Setting", mainWindow = this },
                            new MenuItemViewModel_APS { Header = "Frequency Setting", mainWindow = this },
                            new MenuItemViewModel_APS { Header = "Attenuation", mainWindow = this },
                            new MenuItemViewModel_APS { Header = "Write Configuration", mainWindow = this },
                        } : new ObservableCollection<MenuItemViewModel_APS>
                        {
                            new MenuItemViewModel_APS { Header = "Marker Setting", mainWindow = this },
                            new MenuItemViewModel_APS { Header = "Frequency Setting", mainWindow = this },
                            new MenuItemViewModel_APS { Header = "Write Configuration", mainWindow = this },
                        })

                },
                new MenuItemViewModel_APS { Header = "View Log",
                    MenuItems = new ObservableCollection<MenuItemViewModel_APS>
                    {
                        new MenuItemViewModel_APS { Header = "Batch Wise Log", mainWindow =this }
                    }
                },
            };
            //DeviceCOM.dataBuffer = new double[8000];
            this.DataContext = this;


            int tt = Convert.ToInt16(System.Configuration.ConfigurationManager.AppSettings["TestTime"]);
            int ss = Convert.ToInt16(System.Configuration.ConfigurationManager.AppSettings["SamplePerSecond"]);


            //logger1 = myPlot1.Add.DataLogger();            

            DeviceCOM.graphData = new GraphData();

            // Prepare Configurtion data
            DeviceCOM.Configuration = new Configuration() { TestTime = tt, SamplePerSecond = ss };
            DeviceCOM.Configuration.Marker = new Marker();
            DeviceCOM.Configuration.Frequency = new Frequency();
            DeviceCOM.Configuration.Frequency.FD = new List<FD>();
            DeviceCOM.Configuration.Frequency.FD.Add(new FD() { FN = 1 });
            DeviceCOM.Configuration.Frequency.FD.Add(new FD() { FN = 3 });
            //DeviceCOM.Configuration.Frequency.FD.Add(new FD() { FN = 2, E = 0 });
            DeviceCOM.Configuration.Filter = new Filter();
            DeviceCOM.Configuration.Filter.FD = new List<FilterFD>();
            DeviceCOM.Configuration.Filter.FD.Add(new FilterFD() { FN = 1 });
            DeviceCOM.Configuration.Filter.FD.Add(new FilterFD() { FN = 3 });

            DeviceCOM.BaudRate = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["BaudRate"]);
            DeviceCOM.PortName = Convert.ToString(System.Configuration.ConfigurationManager.AppSettings["PortName"]) ?? "";

            DeviceCOM.MaxValue = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["MaxValue"]);
            DeviceCOM.MaxValueABS = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["MaxValueABS"]);
            DeviceCOM.Factor = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["Factor"]);
            DeviceCOM.DBConnection = Convert.ToString(System.Configuration.ConfigurationManager.AppSettings["DBConnection"]) ?? "";

            List<int> statuses = new List<int>();
            statuses.Add(1);
            statuses.Add(2);
            statuses.Add(3);
            statuses.Add(4);
            statuses.Add(5);
            statuses.Add(6);
            statuses.Add(7);
            statuses.Add(8);
            statuses.Add(9);
            statuses.Add(10);
            statuses.Add(11);
            statuses.Add(12);
            statuses.Add(13);
            statuses.Add(14);
            statuses.Add(15);
            ddlTT.ItemsSource = statuses;


            InitialGraphSetting();
            logger1 = myPlot1.Add.DataStreamer((tt * ss));
            logger1.LineColor = ScottPlot.Colors.LightBlue; // Change line color here

            //logger1.ViewScrollRight();
            logger1.ViewScrollLeft();

            logger2 = myPlot2.Add.DataStreamer((tt * ss));
            logger2.LineColor = ScottPlot.Colors.LightBlue; // Change line color here

            //logger1.ViewScrollRight();
            logger2.ViewScrollLeft();

            deviceCOM = new DeviceCOM();
            deviceCOM.InitialPort();

            if (System.IO.File.Exists("Config.txt"))
            {
                var config = JsonConvert.DeserializeObject<Configuration>(System.IO.File.ReadAllText("Config.txt"));
                if (config != null && config.Frequency.FD != null && DeviceCOM.Configuration.Frequency.FD.Count == config.Frequency.FD.Count)
                {
                    DeviceCOM.Configuration = config;
                }
                ddlTT.SelectedIndex = DeviceCOM.Configuration.TestTime - 1;
            }
            else
            {
                ddlTT.SelectedIndex = tt - 1;
            }

            //deviceCOM.WriteData(JsonConvert.SerializeObject(DeviceCOM.Configuration.Marker));

            byte[] data = new byte[23];
            data[0] = Convert.ToByte(2);
            data[1] = Convert.ToByte(50);
            data[2] = Convert.ToByte(18);

            data[3] = (byte)(DeviceCOM.Configuration.Marker.FmS & 0xFF);
            data[4] = (byte)((DeviceCOM.Configuration.Marker.FmS >> 8) & 0xFF);

            data[5] = (byte)(DeviceCOM.Configuration.Marker.RmS & 0xFF);
            data[6] = (byte)((DeviceCOM.Configuration.Marker.RmS >> 8) & 0xFF);

            data[7] = (byte)(DeviceCOM.Configuration.Marker.M1 & 0xFF);
            data[8] = (byte)((DeviceCOM.Configuration.Marker.M1 >> 8) & 0xFF);

            data[9] = (byte)(DeviceCOM.Configuration.Marker.M2 & 0xFF);
            data[10] = (byte)((DeviceCOM.Configuration.Marker.M2 >> 8) & 0xFF);

            data[11] = (byte)(DeviceCOM.Configuration.Marker.P1mS & 0xFF);
            data[12] = (byte)((DeviceCOM.Configuration.Marker.P1mS >> 8) & 0xFF);

            data[13] = (byte)(DeviceCOM.Configuration.Marker.C1C2 & 0xFF);
            data[14] = (byte)((DeviceCOM.Configuration.Marker.C1C2 >> 8) & 0xFF);

            data[15] = (byte)(DeviceCOM.Configuration.Marker.CC2 & 0xFF);
            data[16] = (byte)((DeviceCOM.Configuration.Marker.CC2 >> 8) & 0xFF);

            data[17] = (byte)(DeviceCOM.Configuration.Marker.C2E & 0xFF);
            data[18] = (byte)((DeviceCOM.Configuration.Marker.C2E >> 8) & 0xFF);

            data[19] = (byte)(DeviceCOM.Configuration.Marker.MABC & 0xFF);
            data[20] = (byte)((DeviceCOM.Configuration.Marker.MABC >> 8) & 0xFF);

            deviceCOM.WriteDataInByte(data);

            byte[] data1 = new byte[49];
            data1[0] = Convert.ToByte(2);
            data1[1] = Convert.ToByte(57);
            data1[2] = Convert.ToByte(45);
            data1[3] = Convert.ToByte(1);
            data1[4] = Convert.ToByte(1);
            data1[5] = Convert.ToByte(2);

            int startBytes = 6;
            foreach (var fd in DeviceCOM.Configuration.Frequency.FD)
            {
                data1[startBytes] = Convert.ToByte(fd.FN);

                data1[startBytes + 1] = (byte)(fd.F & 0xFF);         // Lowest byte
                data1[startBytes + 2] = (byte)((fd.F >> 8) & 0xFF);  // Byte 2
                data1[startBytes + 3] = (byte)((fd.F >> 16) & 0xFF); // Byte 3
                data1[startBytes + 4] = (byte)((fd.F >> 24) & 0xFF); // Highest byte

                var gaint = Convert.ToInt16(fd.G * 10);
                data1[startBytes + 5] = (byte)(gaint & 0xFF);
                data1[startBytes + 6] = (byte)((gaint >> 8) & 0xFF);

                data1[startBytes + 7] = (byte)(fd.LTH & 0xFF);
                data1[startBytes + 8] = (byte)((fd.LTH >> 8) & 0xFF);

                data1[startBytes + 9] = (byte)(fd.UTH & 0xFF);
                data1[startBytes + 10] = (byte)((fd.UTH >> 8) & 0xFF);

                startBytes = startBytes + 11;
            }

            foreach (var fd in DeviceCOM.Configuration.Filter.FD)
            {
                data1[startBytes] = Convert.ToByte(fd.FN);

                var gaint = Convert.ToInt16(fd.G * 10);

                ushort h = (fd.FN == 1 ? Convert.ToUInt16(fd.H) : Convert.ToUInt16(gaint));
                ushort l = Convert.ToUInt16(fd.L);
                ushort x = Convert.ToUInt16(fd.X);
                ushort y = Convert.ToUInt16(fd.Y);

                // H
                data1[startBytes + 1] = (byte)(h & 0xFF);         // Low byte
                data1[startBytes + 2] = (byte)((h >> 8) & 0xFF);  // High byte

                // L
                data1[startBytes + 3] = (byte)(l & 0xFF);
                data1[startBytes + 4] = (byte)((l >> 8) & 0xFF);

                // X
                data1[startBytes + 5] = (byte)(x & 0xFF);
                data1[startBytes + 6] = (byte)((x >> 8) & 0xFF);

                // Y
                data1[startBytes + 7] = (byte)(y & 0xFF);
                data1[startBytes + 8] = (byte)((y >> 8) & 0xFF);

                startBytes = startBytes + 9;
            }
            data1[startBytes] = (byte)DeviceCOM.Configuration.Frequency.FD[0].AT;
            //data1[startBytes] = Convert.ToByte(DeviceCOM.Configuration.Frequency.FD[0].AT);

            deviceCOM.WriteDataInByte(data1);


            //ConfigurationToWrite configurationWrite = new ConfigurationToWrite();
            //configurationWrite.Frequency = DeviceCOM.Configuration.Frequency;
            //configurationWrite.Filter = DeviceCOM.Configuration.Filter;
            //deviceCOM.WriteData(JsonConvert.SerializeObject(configurationWrite));

            IpAddress = Convert.ToString(System.Configuration.ConfigurationManager.AppSettings["IP"]) ?? "";
            Port = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["Port"]);

            receiver = new UdpReceiver(Port);
            receiver.StartReceiving();

            //dispatcherTimer = new DispatcherTimer();
            //dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            //dispatcherTimer.Interval = new TimeSpan(0,0,0,0,10);
            //dispatcherTimer.Start();

            dispatcherTimerui = new DispatcherTimer();
            dispatcherTimerui.Tick += new EventHandler(dispatcherTimerui_Tick);
            dispatcherTimerui.Interval = new TimeSpan(0, 0, 0, 0, 10);
            dispatcherTimerui.Start();

            Task.Run(() => PollLoop());

            //Task.Run(() => UIUpdateLoop());

            //Status status = new Status() { FC = 23 };
            //var rat = deviceCOM.GetSystemStatus(JsonConvert.SerializeObject(status));
            // Marked Busy Flag 

            List<PieSlice> pieSlices = new List<PieSlice>();

            PieSlice pieSliceG = new PieSlice(0, ScottPlot.Colors.Green, "Ok");
            pieSlices.Add(pieSliceG);

            PieSlice pieSliceN = new PieSlice(0, ScottPlot.Colors.Red, "Not Ok");
            pieSlices.Add(pieSliceN);

            //PieSlice pieSliceT = new PieSlice(20, ScottPlot.Colors.Orange, "Total");
            //pieSlices.Add(pieSliceT);

            var pie = wpCounter.Plot.Add.Pie(pieSlices);

            wpCounter.Plot.Axes.Bottom.IsVisible = false;
            wpCounter.Plot.Axes.Left.IsVisible = false;

            wpCounter.Plot.Title("Counter Distribution");
            wpCounter.Refresh();

            //Get counter file and Reset wpCounter


        }

        HorizontalLine? thresholdLine4;
        HorizontalLine? thresholdLine5;
        HorizontalLine? thresholdLine6;

        HorizontalLine? thresholdLine7;
        HorizontalLine? thresholdLine8;
        HorizontalLine? thresholdLine9;
        public void InitialGraphSetting()
        {
            if (DeviceCOM.Configuration != null)
            {
                var limits = new ScottPlot.AxisLimits(0, (DeviceCOM.Configuration.TestTime * DeviceCOM.Configuration.SamplePerSecond), 0, DeviceCOM.Factor);
                var rule = new ScottPlot.AxisRules.MinimumBoundary(
                    xAxis: WpfPlot1.Plot.Axes.Bottom,
                    yAxis: WpfPlot1.Plot.Axes.Left,
                    limits: limits
                );

                WpfPlot1.Plot.Axes.Rules.Clear();
                WpfPlot1.Plot.Axes.Rules.Add(rule);
            }

            var d1 = DeviceCOM.Configuration?.Frequency?.FD?.FirstOrDefault(f => f.FN == 1);
            var a1 = DeviceCOM.Configuration?.Frequency?.FD?.FirstOrDefault(f => f.FN == 3);
            myPlot1 = WpfPlot1.Plot;

            if (d1 != null)
            {
                myPlot1.Title("D1 Response(" + d1.F.ToString() + "," + d1.G.ToString() + "," + d1.PP.ToString() + ")");
            }

            //myPlot1.Grid.XAxis.IsVisible = false;
            //myPlot1.Grid.XAxis.IsVisible = false;

            myPlot1.Axes.Bottom.IsVisible = false;
            //myPlot1.Axes.Left.IsVisible = false;

            WpfPlot1.Plot.FigureBackground.Color = ScottPlot.Colors.DarkGray;  // entire canvas background
            WpfPlot1.Plot.DataBackground.Color = ScottPlot.Colors.Black;

            // Set grid line colors
            WpfPlot1.Plot.Grid.LineColor = ScottPlot.Colors.Gray;
            //WpfPlot1.Plot.Grid.IsVisible = false;

            WpfPlot1.Plot.Axes.Bottom.TickGenerator = new NumericFixedInterval(2000000000); // 10 units
            WpfPlot1.Plot.Axes.Top.TickGenerator = new NumericFixedInterval(2000000000); // 10 units
            WpfPlot1.Plot.Axes.Left.TickGenerator = new NumericFixedInterval(20);   // 20 units
            WpfPlot1.Plot.Grid.LineWidth = 1;

            WpfPlot1.Refresh();

            var limits1 = new ScottPlot.AxisLimits(0, 20, 0, DeviceCOM.Factor);
            var rule1 = new ScottPlot.AxisRules.MinimumBoundary(
                xAxis: WpfPlot4.Plot.Axes.Bottom,
                yAxis: WpfPlot4.Plot.Axes.Left,
                limits: limits1
            );

            WpfPlot4.Plot.Axes.Rules.Clear();
            WpfPlot4.Plot.Axes.Rules.Add(rule1);

            myPlot4 = WpfPlot4.Plot;
            if (d1 != null)
            {
                myPlot4.Title("Last D1 Response (" + d1.F.ToString() + "," + d1.G.ToString() + "," + d1.PP.ToString() + ")");
            }

            //myPlot4.Grid.XAxis.IsVisible = false;
            //myPlot4.Grid.XAxis.IsVisible = false;

            logger4 = myPlot4.Add.DataLogger();
            logger4.LineColor = ScottPlot.Colors.Blue; // Change line color here


            myPlot4.Axes.Bottom.IsVisible = false;
            //myPlot4.Axes.Left.IsVisible = false;

            WpfPlot4.Plot.FigureBackground.Color = ScottPlot.Colors.DarkGray;  // entire canvas background
            WpfPlot4.Plot.DataBackground.Color = ScottPlot.Colors.Black;

            // Set grid line colors
            WpfPlot4.Plot.Grid.LineColor = ScottPlot.Colors.Gray;

            WpfPlot4.Plot.Axes.Bottom.TickGenerator = new NumericFixedInterval(2000000000); // 10 units
            WpfPlot4.Plot.Axes.Top.TickGenerator = new NumericFixedInterval(2000000000); // 10 units
            WpfPlot4.Plot.Axes.Left.TickGenerator = new NumericFixedInterval(20);   // 20 units
            //WpfPlot4.Plot.Axes.Bottom.

            if (d1 != null)
            {
                if (thresholdLine4 != null)
                {
                    WpfPlot4.Plot.Remove(thresholdLine4);
                }
                thresholdLine4 = WpfPlot4.Plot.Add.HorizontalLine(y: d1.LTH);
                thresholdLine4.LineWidth = 0.5f;
                thresholdLine4.Color = ScottPlot.Colors.Orange;

                if (thresholdLine5 != null)
                {
                    WpfPlot4.Plot.Remove(thresholdLine5);
                }
                thresholdLine5 = WpfPlot4.Plot.Add.HorizontalLine(y: d1.UTH);
                thresholdLine5.LineWidth = 0.5f;
                thresholdLine5.Color = ScottPlot.Colors.Red;

                if (thresholdLine6 != null)
                {
                    WpfPlot4.Plot.Remove(thresholdLine6);
                }
                thresholdLine6 = WpfPlot4.Plot.Add.HorizontalLine(y: d1.TH);
                thresholdLine6.LineWidth = 0.5f;
                thresholdLine6.Color = ScottPlot.Colors.White;
            }

            WpfPlot4.Plot.Grid.LineWidth = 1;
            WpfPlot4.Refresh();

            InitialGraphSettingAPS();
        }
        public void InitialGraphSettingAPS()
        {
            if (DeviceCOM.Configuration != null)
            {
                var limits = new ScottPlot.AxisLimits(0, (DeviceCOM.Configuration.TestTime * DeviceCOM.Configuration.SamplePerSecond), 0, DeviceCOM.Factor);
                var rule = new ScottPlot.AxisRules.MinimumBoundary(
                    xAxis: WpfPlotA1.Plot.Axes.Bottom,
                    yAxis: WpfPlotA1.Plot.Axes.Left,
                    limits: limits
                );

                WpfPlotA1.Plot.Axes.Rules.Clear();
                WpfPlotA1.Plot.Axes.Rules.Add(rule);
            }

            var d1 = DeviceCOM.Configuration?.Frequency?.FD?.FirstOrDefault(f => f.FN == 3);
            myPlot2 = WpfPlotA1.Plot;

            if (d1 != null)
            {
                myPlot2.Title("A1 Response(" + d1.G.ToString() + ")");
            }

            //myPlot1.Grid.XAxis.IsVisible = false;
            //myPlot1.Grid.XAxis.IsVisible = false;

            myPlot2.Axes.Bottom.IsVisible = false;
            //myPlot1.Axes.Left.IsVisible = false;

            WpfPlotA1.Plot.FigureBackground.Color = ScottPlot.Colors.DarkGray;  // entire canvas background
            WpfPlotA1.Plot.DataBackground.Color = ScottPlot.Colors.Black;

            // Set grid line colors
            WpfPlotA1.Plot.Grid.LineColor = ScottPlot.Colors.Gray;
            //WpfPlot1.Plot.Grid.IsVisible = false;

            WpfPlotA1.Plot.Axes.Bottom.TickGenerator = new NumericFixedInterval(2000000000); // 10 units
            WpfPlotA1.Plot.Axes.Top.TickGenerator = new NumericFixedInterval(2000000000); // 10 units
            WpfPlotA1.Plot.Axes.Left.TickGenerator = new NumericFixedInterval(20);   // 20 units
            WpfPlotA1.Plot.Grid.LineWidth = 1;

            WpfPlotA1.Refresh();


            var limits1 = new ScottPlot.AxisLimits(0, 20, 0, DeviceCOM.Factor);
            var rule1 = new ScottPlot.AxisRules.MinimumBoundary(
                xAxis: WpfPlotA1Last.Plot.Axes.Bottom,
                yAxis: WpfPlotA1Last.Plot.Axes.Left,
                limits: limits1
            );

            WpfPlotA1Last.Plot.Axes.Rules.Clear();
            WpfPlotA1Last.Plot.Axes.Rules.Add(rule1);

            myPlotA1Last = WpfPlotA1Last.Plot;
            if (d1 != null)
            {
                myPlotA1Last.Title("Last A1 Response (" +  d1.G.ToString()  + ")");
            }

            //myPlot4.Grid.XAxis.IsVisible = false;
            //myPlot4.Grid.XAxis.IsVisible = false;

            loggerA1Last = myPlotA1Last.Add.DataLogger();
            loggerA1Last.LineColor = ScottPlot.Colors.Blue; // Change line color here


            myPlotA1Last.Axes.Bottom.IsVisible = false;
            //myPlot4.Axes.Left.IsVisible = false;

            WpfPlotA1Last.Plot.FigureBackground.Color = ScottPlot.Colors.DarkGray;  // entire canvas background
            WpfPlotA1Last.Plot.DataBackground.Color = ScottPlot.Colors.Black;

            // Set grid line colors
            WpfPlotA1Last.Plot.Grid.LineColor = ScottPlot.Colors.Gray;

            WpfPlotA1Last.Plot.Axes.Bottom.TickGenerator = new NumericFixedInterval(2000000000); // 10 units
            WpfPlotA1Last.Plot.Axes.Top.TickGenerator = new NumericFixedInterval(2000000000); // 10 units
            WpfPlotA1Last.Plot.Axes.Left.TickGenerator = new NumericFixedInterval(20);   // 20 units
            //WpfPlotA1Last.Plot.Axes.Bottom.

            if (d1 != null)
            {
                if (thresholdLine7 != null)
                {
                    WpfPlotA1Last.Plot.Remove(thresholdLine7);
                }
                thresholdLine7 = WpfPlotA1Last.Plot.Add.HorizontalLine(y: d1.LTH);
                thresholdLine7.LineWidth = 0.5f;
                thresholdLine7.Color = ScottPlot.Colors.Orange;

                if (thresholdLine8 != null)
                {
                    WpfPlotA1Last.Plot.Remove(thresholdLine8);
                }
                thresholdLine8 = WpfPlotA1Last.Plot.Add.HorizontalLine(y: d1.UTH);
                thresholdLine8.LineWidth = 0.5f;
                thresholdLine8.Color = ScottPlot.Colors.Red;

                if (thresholdLine9 != null)
                {
                    WpfPlotA1Last.Plot.Remove(thresholdLine9);
                }
                thresholdLine9 = WpfPlotA1Last.Plot.Add.HorizontalLine(y: d1.TH);
                thresholdLine9.LineWidth = 0.5f;
                thresholdLine9.Color = ScottPlot.Colors.White;
            }

            WpfPlotA1Last.Plot.Grid.LineWidth = 1;
            WpfPlotA1Last.Refresh();

        }

        private void dispatcherTimerui_Tick(object? sender, EventArgs e)
        {
            UIUpdates();
        }

        private ConcurrentQueue<byte[]> processingQueue = new();

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            if (!receiver._udpClient.Client.Connected)
            {
                receiver.StartReceiving();
            }

            if (DeviceCOM.receiveBytes != null && DeviceCOM.receiveBytes.Length > 0)
            {
                var data = DeviceCOM.receiveBytes.ToArray();
                DeviceCOM.receiveBytes = null;
                processingQueue.Enqueue(data);
                TryStartProcessing();
                //Task.Run(() => ProcessPortDataTest(data));
                //Task.Run(() => ProcessPortData(data));
            }
        }


        private void PollLoop()
        {
            while (true)
            {
                if (DeviceCOM.receiveBytes != null && DeviceCOM.receiveBytes.Length > 0)
                {
                    var data = DeviceCOM.receiveBytes.ToArray();
                    DeviceCOM.receiveBytes = null;

                    processingQueue.Enqueue(data);
                    TryStartProcessing(); // same logic as before
                }

                //Thread.Sleep(1); // adjust this depending on how fast you want to pol
            }
        }

        private void UIUpdateLoop()
        {
            while (true)
            {
                UIUpdates();

            }
        }

        private bool isProcessing = false;

        private void TryStartProcessing()
        {
            if (isProcessing || processingQueue.IsEmpty)
                return;

            isProcessing = true;

            Task.Run(() =>
            {
                while (processingQueue.TryDequeue(out var data))
                {
                    ProcessPortData(data);
                }

                isProcessing = false;
            });
        }

        private void StopTude(bool result)
        {
            try
            {
                System.Threading.Thread.Sleep(10);
                myPlot4.Clear();
                logger4 = myPlot4.Add.DataLogger();
                logger4.Clear();

                myPlotA1Last.Clear();
                loggerA1Last = myPlotA1Last.Add.DataLogger();
                loggerA1Last.Clear();

                DeviceCOM.graphData.Result = result;
                var Ld = DeviceCOM.graphData.AmpD1.ToList();
                string ImageName = Guid.NewGuid().ToString() + ".jpeg";
                // Add Log
                if (DeviceCOM.IsLogEnable)
                {
                    // DeviceCOM.Configuration
                    //DeviceCOM.part
                    //DeviceCOM.graphData
                    /// result 
                    //DeviceCOM.part.Name
                    try
                    {
                        using (var con = new NpgsqlConnection(DeviceCOM.DBConnection))
                        {
                            con.Open();
                            if (DeviceCOM.part != null)
                            {
                                DeviceCOM.part.ImagePath = ImageName;
                            }

                            string partJson = JsonConvert.SerializeObject(DeviceCOM.part);
                            string configJson = JsonConvert.SerializeObject(DeviceCOM.Configuration);
                            string graphJson = JsonConvert.SerializeObject(DeviceCOM.graphData);

                            string sql = @"
                                INSERT INTO ""Logs"" 
                                (""TimeStamp"", ""PartJson"", ""ConfigurationJson"", ""GraphDataJson"", ""BatchName"", ""Result"")
                                VALUES 
                                (@time, @part, @config, @graph, @batch, @result)";

                            using (var cmd = new NpgsqlCommand(sql, con))
                            {
                                cmd.Parameters.AddWithValue("@time", DateTime.Now);
                                cmd.Parameters.AddWithValue("@part", NpgsqlTypes.NpgsqlDbType.Jsonb, partJson);
                                cmd.Parameters.AddWithValue("@config", NpgsqlTypes.NpgsqlDbType.Jsonb, configJson);
                                cmd.Parameters.AddWithValue("@graph", NpgsqlTypes.NpgsqlDbType.Jsonb, graphJson);
                                cmd.Parameters.AddWithValue("@batch", DeviceCOM.part?.Name ?? "");
                                cmd.Parameters.AddWithValue("@result", NpgsqlTypes.NpgsqlDbType.Boolean, result);

                                cmd.ExecuteNonQuery();
                            }
                        }
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("Something went wrong. Please try again");
                    }
                }

                LastGraph(Ld);

                //LastGraphA1(Ld);

                if (result)
                {
                    DeviceCOM.Ok = DeviceCOM.Ok + 1;
                }
                else
                {
                    DeviceCOM.NoOk = DeviceCOM.NoOk + 1;
                }

                // Write counter file

                wpCounter.Plot.Clear();

                List<PieSlice> pieSlices = new List<PieSlice>();

                PieSlice pieSliceG = new PieSlice(DeviceCOM.Ok, ScottPlot.Colors.Green, "Ok");
                pieSlices.Add(pieSliceG);

                PieSlice pieSliceN = new PieSlice(DeviceCOM.NoOk, ScottPlot.Colors.Red, "Not Ok");
                pieSlices.Add(pieSliceN);

                var pie = wpCounter.Plot.Add.Pie(pieSlices);

                wpCounter.Plot.Axes.Bottom.IsVisible = false;
                wpCounter.Plot.Axes.Left.IsVisible = false;

                //wpCounter.Plot.Title("Counter Distribution");
                wpCounter.Refresh();

                //lblOk.Content = "Ok Count-" + DeviceCOM.Ok.ToString();
                //lblNotOk.Content = "Not Ok Count-" + DeviceCOM.NoOk.ToString();
                //lblTotal.Content = "Total Count-" + (DeviceCOM.Ok + DeviceCOM.NoOk).ToString();

                DeviceCOM.graphData.AmpD1 = new List<Fdata>();

                string? imagePath = ConfigurationManager.AppSettings["ImagePath"];

                if (DeviceCOM.IsLogEnable && !string.IsNullOrWhiteSpace(imagePath))
                {
                    try
                    {
                        string fullPath = System.IO.Path.Combine(imagePath, ImageName);

                        // Ensure directory exists
                        Directory.CreateDirectory(imagePath);

                        WpfPlot4.Plot.SaveJpeg(fullPath, 600, 400);
                    }
                    catch (Exception)
                    {
                        // Log properly instead of silent failure
                        // Example: Logger.LogError(ex);
                    }
                }

            }
            catch (Exception)
            {

            }
        }

        private void LastGraph(List<Fdata> Ld)
        {
            var limits = new ScottPlot.AxisLimits(0, Ld.Count + 5, 0, DeviceCOM.Factor);
            var rule = new ScottPlot.AxisRules.MinimumBoundary(
                xAxis: WpfPlot4.Plot.Axes.Bottom,
                yAxis: WpfPlot4.Plot.Axes.Left,
                limits: limits
            );

            WpfPlot4.Plot.Axes.Rules.Clear();
            WpfPlot4.Plot.Axes.Rules.Add(rule);

            var limits1 = new ScottPlot.AxisLimits(0, Ld.Count + 5, 0, DeviceCOM.Factor);
            var rule1 = new ScottPlot.AxisRules.MinimumBoundary(
                xAxis: WpfPlotA1Last.Plot.Axes.Bottom,
                yAxis: WpfPlotA1Last.Plot.Axes.Left,
                limits: limits1
            );

            WpfPlotA1Last.Plot.Axes.Rules.Clear();
            WpfPlotA1Last.Plot.Axes.Rules.Add(rule1);

            var t = Ld.Count - 1;
            for (var i = 0; i <= t; i++)
            {
                var d = Ld[i];

                var AmpF = 0;
                if (d.Amp != 0)
                {
                    AmpF = (DeviceCOM.Factor * d.Amp) / DeviceCOM.MaxValue;
                }


                var d1 = DeviceCOM.Configuration?.Frequency?.FD?.FirstOrDefault(d => d.FN == 1);
                if (d1 != null)
                {
                    if (thresholdLine4 != null)
                    {
                        WpfPlot4.Plot.Remove(thresholdLine4);
                    }
                    thresholdLine4 = WpfPlot4.Plot.Add.HorizontalLine(y: d1.LTH);
                    thresholdLine4.LineWidth = 0.5f;
                    thresholdLine4.Color = ScottPlot.Colors.Orange;

                    if (thresholdLine5 != null)
                    {
                        WpfPlot4.Plot.Remove(thresholdLine5);
                    }
                    thresholdLine5 = WpfPlot4.Plot.Add.HorizontalLine(y: d1.UTH);
                    thresholdLine5.LineWidth = 0.5f;
                    thresholdLine5.Color = ScottPlot.Colors.Red;

                    if (thresholdLine6 != null)
                    {
                        WpfPlot4.Plot.Remove(thresholdLine6);
                    }
                    thresholdLine6 = WpfPlot4.Plot.Add.HorizontalLine(y: d1.TH);
                    thresholdLine6.LineWidth = 0.5f;
                    thresholdLine6.Color = ScottPlot.Colors.White;
                }

                logger4.Add(AmpF);


                var AmpFA1 = 0;
                if (d.Amp_ABS != 0)
                {
                    AmpFA1 = (DeviceCOM.Factor * d.Amp_ABS) / DeviceCOM.MaxValueABS;
                }


                var d5 = DeviceCOM.Configuration?.Frequency?.FD?.FirstOrDefault(d => d.FN == 3);
                if (d5 != null)
                {
                    if (thresholdLine7 != null)
                    {
                        WpfPlotA1Last.Plot.Remove(thresholdLine7);
                    }
                    thresholdLine7 = WpfPlotA1Last.Plot.Add.HorizontalLine(y: d5.LTH);
                    thresholdLine7.LineWidth = 0.5f;
                    thresholdLine7.Color = ScottPlot.Colors.Orange;

                    if (thresholdLine8 != null)
                    {
                        WpfPlotA1Last.Plot.Remove(thresholdLine8);
                    }
                    thresholdLine8 = WpfPlotA1Last.Plot.Add.HorizontalLine(y: d5.UTH);
                    thresholdLine8.LineWidth = 0.5f;
                    thresholdLine8.Color = ScottPlot.Colors.Red;

                    if (thresholdLine9 != null)
                    {
                        WpfPlotA1Last.Plot.Remove(thresholdLine9);
                    }
                    thresholdLine9 = WpfPlotA1Last.Plot.Add.HorizontalLine(y: d5.TH);
                    thresholdLine9.LineWidth = 0.5f;
                    thresholdLine9.Color = ScottPlot.Colors.White;
                }

                loggerA1Last.Add(AmpFA1);
            }


            var d2 = Ld.FirstOrDefault(d => d.IsMarked);
            if (d2 != null)
            {
                int index = Ld.IndexOf(d2);

                var thresholdLine10 = WpfPlot4.Plot.Add.VerticalLine(x: index);
                thresholdLine10.LineWidth = 1.5f;
                thresholdLine10.Color = ScottPlot.Colors.Red;
            }

            var d3 = Ld.LastOrDefault(d => d.IsMarked);
            if (d3 != null)
            {
                int index = Ld.IndexOf(d3);
                var thresholdLine10 = WpfPlot4.Plot.Add.VerticalLine(x: index);
                thresholdLine10.LineWidth = 1.5f;
                thresholdLine10.Color = ScottPlot.Colors.Red;
            }



            if (d2 != null)
            {
                int index = Ld.IndexOf(d2);

                var thresholdLine10 = WpfPlotA1Last.Plot.Add.VerticalLine(x: index);
                thresholdLine10.LineWidth = 1.5f;
                thresholdLine10.Color = ScottPlot.Colors.Red;
            }

            if (d3 != null)
            {
                int index = Ld.IndexOf(d3);
                var thresholdLine10 = WpfPlotA1Last.Plot.Add.VerticalLine(x: index);
                thresholdLine10.LineWidth = 1.5f;
                thresholdLine10.Color = ScottPlot.Colors.Red;
            }


            WpfPlot4.Refresh();
            WpfPlotA1Last.Refresh();


        }

        private void LastGraphA1(List<Fdata> Ld)
        {
            var limits = new ScottPlot.AxisLimits(0, Ld.Count + 5, 0, DeviceCOM.Factor);
            var rule = new ScottPlot.AxisRules.MinimumBoundary(
                xAxis: WpfPlotA1Last.Plot.Axes.Bottom,
                yAxis: WpfPlotA1Last.Plot.Axes.Left,
                limits: limits
            );

            WpfPlotA1Last.Plot.Axes.Rules.Clear();
            WpfPlotA1Last.Plot.Axes.Rules.Add(rule);

            var t = Ld.Count - 1;
            for (var i = 0; i <= t; i++)
            {
                var d = Ld[i];
                //}
                //foreach (var d in Ld)
                //{
                var AmpF = 0;
                if (d.Amp != 0)
                {
                    AmpF = (DeviceCOM.Factor * d.Amp_ABS) / DeviceCOM.MaxValueABS;
                }


                var d1 = DeviceCOM.Configuration?.Frequency?.FD?.FirstOrDefault(d => d.FN == 3);
                if (d1 != null)
                {
                    if (thresholdLine7 != null)
                    {
                        WpfPlotA1Last.Plot.Remove(thresholdLine7);
                    }
                    thresholdLine7 = WpfPlotA1Last.Plot.Add.HorizontalLine(y: d1.LTH);
                    thresholdLine7.LineWidth = 0.5f;
                    thresholdLine7.Color = ScottPlot.Colors.Orange;

                    if (thresholdLine8 != null)
                    {
                        WpfPlotA1Last.Plot.Remove(thresholdLine8);
                    }
                    thresholdLine8 = WpfPlotA1Last.Plot.Add.HorizontalLine(y: d1.UTH);
                    thresholdLine8.LineWidth = 0.5f;
                    thresholdLine8.Color = ScottPlot.Colors.Red;

                    if (thresholdLine9 != null)
                    {
                        WpfPlotA1Last.Plot.Remove(thresholdLine9);
                    }
                    thresholdLine9 = WpfPlotA1Last.Plot.Add.HorizontalLine(y: d1.TH);
                    thresholdLine9.LineWidth = 0.5f;
                    thresholdLine9.Color = ScottPlot.Colors.White;
                }

                loggerA1Last.Add(AmpF);
            }


            var d2 = Ld.FirstOrDefault(d => d.IsMarked);
            if (d2 != null)
            {
                int index = Ld.IndexOf(d2);

                var thresholdLine10 = WpfPlotA1Last.Plot.Add.VerticalLine(x: index);
                thresholdLine10.LineWidth = 1.5f;
                thresholdLine10.Color = ScottPlot.Colors.Red;
            }

            var d3 = Ld.LastOrDefault(d => d.IsMarked);
            if (d3 != null)
            {
                int index = Ld.IndexOf(d3);
                var thresholdLine10 = WpfPlotA1Last.Plot.Add.VerticalLine(x: index);
                thresholdLine10.LineWidth = 1.5f;
                thresholdLine10.Color = ScottPlot.Colors.Red;
            }

            WpfPlotA1Last.Refresh();
        }

        private void UIUpdates()
        {
            try
            {

                lblResultStatus.Content = resultStatus;
                Canvas2.Children.Clear();


                if (DeviceCOM.Configuration?.Frequency?.FD != null)
                {
                    var d1 = DeviceCOM.Configuration.Frequency.FD.FirstOrDefault(f => f.FN == 1);
                    if (d1 != null)
                    {
                        var elW = (470 * d1.LTH) / DeviceCOM.Factor;
                        Ellipse el1 = new Ellipse();
                        el1.Height = elW;
                        el1.Width = elW;
                        el1.Stroke = new SolidColorBrush(Colors.Orange);
                        el1.StrokeThickness = 1; // You can adjust this as needed
                        Canvas.SetLeft(el1, (-1 * (elW / 2)));
                        Canvas.SetTop(el1, (-1 * (elW / 2)));

                        Canvas2.Children.Add(el1);

                        var elW1 = (470 * d1.UTH) / DeviceCOM.Factor;
                        Ellipse el2 = new Ellipse();
                        el2.Height = elW1;
                        el2.Width = elW1;
                        el2.Stroke = new SolidColorBrush(Colors.Red);
                        el2.StrokeThickness = 1; // You can adjust this as needed
                        Canvas.SetLeft(el2, -1 * (elW1 / 2));
                        Canvas.SetTop(el2, -1 * (elW1 / 2));

                        Canvas2.Children.Add(el2);
                    }
                }

                var data = DeviceCOM.graphData.AmpD1.ToList();
                if (data.Count > 0)
                {
                    var dotsVisual = CreateDotsVisual(data);
                    var host = new VisualHost(dotsVisual);
                    Canvas2.Children.Add(host);
                }

                if (DeviceCOM.IsTubeSatart)
                {
                    btnTestStatus.Background = new SolidColorBrush(Colors.Green);
                    lblTest.Content = "Test On";
                }
                else
                {
                    btnTestStatus.Background = new SolidColorBrush(Colors.Gray);
                    lblTest.Content = "Test Off";
                }


                if (DeviceCOM.IsCalibarationStart)
                {
                    btnCali.Background = new SolidColorBrush(Colors.Orange);
                }
                else
                {
                    btnCali.Background = new SolidColorBrush(Colors.Gray);
                }

                var tContent = "Total Count-" + (DeviceCOM.Ok + DeviceCOM.NoOk).ToString();
                if (tContent != lblTotal.Content.ToString())
                {
                    lblOk.Content = "Ok Count-" + DeviceCOM.Ok.ToString();
                    lblNotOk.Content = "Not Ok Count-" + DeviceCOM.NoOk.ToString();
                    lblTotal.Content = "Total Count-" + (DeviceCOM.Ok + DeviceCOM.NoOk).ToString();
                }
            }
            catch (Exception)
            {

            }
            //InitialGraphSetting();
        }


        private DrawingVisual CreateDotsVisual(IEnumerable<Fdata> data)
        {
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                foreach (var d in data)
                {
                    double phaseRadians = d.phase * Math.PI / 180.0;
                    double AmpF1 = (235 * d.Amp) / DeviceCOM.MaxValue;
                    double x1 = AmpF1 * Math.Cos(phaseRadians);
                    double y1 = AmpF1 * Math.Sin(phaseRadians);

                    // Draw a small blue dot (1.5 radius circle)
                    dc.DrawEllipse(Brushes.Blue, null, new Point(x1, y1), 1.5, 1.5);
                }
            }
            return visual;
        }

        private void ProcessPortDataTest(byte[] indata)
        {
            try
            {
                List<string> strings = new List<string>();
                strings.Add(indata[3].ToString() + "-" + indata[5].ToString() + "-" + indata.Length);
                File.AppendAllLines("DataLog.txt", strings);
            }
            catch (Exception)
            { }
        }
        private void ProcessPortData(byte[] indata)
        {
            try
            {
                //List<string> strings = new List<string>();
                //strings.Add(indata[3].ToString() + "-" + indata[5].ToString() + "-" + indata.Length);
                //File.AppendAllLines("DataLog.txt", strings);

                //if (indata.Length == 2)
                //{
                if (indata[0] == 52 || indata[0] == 53 || indata[0] == 54 || indata[0] == 56 || indata[0] == 61)
                {
                    // 5 ==> Start Test // 56 Stop Test  ==> Busy/Fee
                    if (indata[0] == 61)
                    {
                        resultStatus = string.Empty;
                        if (indata[1] == 1)
                        {
                            DeviceCOM.IsCalibarationStart = true;
                            //btnCali.Background = new SolidColorBrush(Colors.Orange);                            
                        }
                        else if (indata[1] == 2)
                        {
                            DeviceCOM.IsCalibarationStart = false;
                            //btnCali.Background = new SolidColorBrush(Colors.Gray);
                        }
                    }

                    // 53 ==> Start Test // 56 Stop Test  ==> Busy/Fee
                    if (indata[0] == 53)
                    {
                        //D1Seeting();
                        DeviceCOM.IsTestOn = true;
                        resultStatus = string.Empty;
                        DeviceCOM.graphData.AmpD1 = new List<Fdata>();
                    }
                    else if (indata[0] == 56)
                    {
                        DeviceCOM.IsTestOn = false;
                        DeviceCOM.IsTubeSatart = false;
                        resultStatus = string.Empty;
                        DeviceCOM.graphData.AmpD1 = new List<Fdata>();
                    }

                    //if (indata[0] == 52 || indata[0] == 54)
                    //{
                    //    //myPlot1.Clear();
                    //    //logger1 = myPlot1.Add.DataLogger();
                    //    //logger1.Clear();

                    //    if (indata[0] == 54)
                    //    {
                    //        DeviceCOM.IsTubeSatart = true;
                    //        //btnTestStatus.Background = new SolidColorBrush(Colors.Orange);
                    //        DeviceCOM.graphData.AmpD1 = new List<Fdata>();
                    //        D1Seeting();
                    //    }

                    //    if (indata[0] == 52)
                    //    {
                    //        DeviceCOM.IsTubeSatart = false;
                    //        //btnTestStatus.Background = new SolidColorBrush(Colors.Gray);
                    //        StopTude(indata[1] == 0);
                    //        //dispatcherTimerui.Stop();

                    //    }
                    //}
                }
                //}
                else if (indata[0] == 55)
                {
                    int NoOfSamples = (indata[2] * 256) + indata[1];
                    int startIndex = indata[3];
                    int errCode = 0;//indata[6];
                    if (errCode == 0)
                    {
                        if (startIndex == 4)
                        {
                            DeviceCOM.IsTubeSatart = true;
                            //btnTestStatus.Background = new SolidColorBrush(Colors.Orange);
                            resultStatus = string.Empty;
                            DeviceCOM.graphData.AmpD1 = new List<Fdata>();
                        }


                        // C1 AMP Data
                        int Ch1NoIndex = 14;
                        int FN1 = indata[Ch1NoIndex];
                        int C1length = indata[15] + (indata[16] * 256);

                        int fStartIndex1 = 17;
                        int fEndIndex1 = fStartIndex1 + C1length - 1;

                        var C1ArrayCompress = new byte[C1length];

                        for (int i = 0; i < C1length; i++)
                        {
                            C1ArrayCompress[i] = indata[fStartIndex1 + i];
                        }

                        // C1 Phase data  
                        int C2length = indata[fEndIndex1 + 1] + (indata[fEndIndex1 + 2] * 256);
                        int fStartIndex2 = fEndIndex1 + 3;
                        int fEndIndex2 = fStartIndex2 + C2length - 1;

                        var C2ArrayCompress = new byte[C2length];

                        for (int i = 0; i < C2length; i++)
                        {
                            C2ArrayCompress[i] = indata[fStartIndex2 + i];
                        }

                        // A1 Phase data  
                        int C3length = indata[fEndIndex2 + 2] + (indata[fEndIndex2 + 3] * 256);
                        int fStartIndex3 = fEndIndex2 + 4;
                        int fEndIndex3 = fStartIndex3 + C3length - 1;

                        var C3ArrayCompress = new byte[C3length];

                        for (int i = 0; i < C3length; i++)
                        {
                            C3ArrayCompress[i] = indata[fStartIndex3 + i];
                        }

                        Int32 indexTudeState = ((indata[10] + (indata[11] << 8) + (indata[12] << 16) + (indata[13] << 24)) * 2);
                        Int32 indexPrePostState = ((indata[4] + (indata[5] << 8) + (indata[6] << 16) + (indata[7] << 24)) * 2);
                        var d1 = DeviceCOM.Configuration?.Frequency?.FD?.FirstOrDefault(f => f.FN == 1);
                        int pp = d1?.PP ?? 0;
                        for (int i = 0; i < C1ArrayCompress.Length; i = i + 2)
                        {
                            bool IsMark = false;
                            Int32 amp = C1ArrayCompress[i] + (C1ArrayCompress[i + 1] << 8);
                            Int32 phase = C2ArrayCompress[i] + (C2ArrayCompress[i + 1] << 8);
                            int rPhase = ((phase + pp) > 360 ? (phase + pp) - 360 : (phase + pp));
                            double phaseRadians = (rPhase) * Math.PI / 180.0;

                            Int32 amp_ABS = C3ArrayCompress[i] + (C3ArrayCompress[i + 1] << 8);

                            // Calculate Cartesian coordinates
                            double x = (amp * Math.Cos(phaseRadians));
                            double y = amp * Math.Sin(phaseRadians);

                            if (DeviceCOM.IsTubeSatart == true)
                            {

                                if (indata[9] == 1)
                                {
                                    if (i >= indexTudeState)
                                    {
                                        IsMark = true;
                                    }
                                }
                                else if (indata[9] == 3)
                                {
                                    if (i <= indexTudeState)
                                    {
                                        IsMark = true;
                                    }
                                }


                                if (startIndex == 4)
                                {
                                    if (i >= indexPrePostState)
                                    {
                                        DeviceCOM.graphData.AmpD1.Add(new Fdata() { Amp = amp, Amp_ABS = amp_ABS, phase = rPhase, x = x, y = y, IsMarked = IsMark });
                                    }
                                }
                                else if (startIndex == 6)
                                {
                                    if (i <= indexPrePostState)
                                    {
                                        DeviceCOM.graphData.AmpD1.Add(new Fdata() { Amp = amp, Amp_ABS = amp_ABS, phase = phase, x = x, y = y, IsMarked = IsMark });
                                    }
                                }
                                else if (startIndex == 2)
                                {
                                    DeviceCOM.graphData.AmpD1.Add(new Fdata() { Amp = amp, Amp_ABS = amp_ABS, phase = phase, x = x, y = y, IsMarked = IsMark });
                                }
                            }

                            var AmpF = 0;
                            if (amp != 0)
                            {
                                AmpF = ((DeviceCOM.Factor * amp) / DeviceCOM.MaxValue);
                            }

                            var AmpF_ABS = 0;
                            if (amp_ABS != 0)
                            {
                                AmpF_ABS = ((DeviceCOM.Factor * amp_ABS) / DeviceCOM.MaxValueABS);
                            }

                            logger1.Add(AmpF);

                            logger2.Add(AmpF_ABS);

                            WpfPlot1.Refresh();
                            WpfPlotA1.Refresh();
                        }


                        // C3 AMP Data
                        //int Ch3NoIndex = fEndIndex2 + 1;
                        //int FN3 = indata[Ch3NoIndex];
                        //int C3length = indata[Ch3NoIndex + 1] + (indata[Ch3NoIndex + 2] * 256);

                        //int fStartIndex3 = Ch3NoIndex + 3;
                        //int fEndIndex3 = fStartIndex3 + C3length - 1;

                        //var C3ArrayCompress = new byte[C3length];

                        //for (int i = 0; i < C3length; i++)
                        //{
                        //    C3ArrayCompress[i] = indata[fStartIndex3 + i];
                        //}

                        //for (int i = 0; i < C3ArrayCompress.Length; i = i + 2)
                        //{
                        //    Int32 amp = C3ArrayCompress[i] + (C3ArrayCompress[i + 1] << 8);                        
                        //}

                        if (startIndex == 6)
                        {
                            DeviceCOM.IsTubeSatart = false;
                            //btnTestStatus.Background = new SolidColorBrush(Colors.Gray);
                            if (indata[8] == 0 || indata[8] == 1 || indata[8] == 2)
                            {
                                resultStatus = string.Empty;
                                StopTude(indata[8] == 0);
                            }
                            else
                            {
                                resultStatus = "Invalid Test!!";
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show("Something went wrong, kindly restart the full system to resolve the issue!!", "Information");
                    }
                }
            }
            catch (Exception)
            {

            }
        }
        HorizontalLine? thresholdLine1;
        HorizontalLine? thresholdLine2;
        HorizontalLine? thresholdLine3;

        HorizontalLine? thresholdLine21;
        HorizontalLine? thresholdLine22;
        HorizontalLine? thresholdLine23;
        public void D1Seeting()
        {
            //WpfPlot1.Plot.Clear();
            if (DeviceCOM.Configuration != null)
            {
                var limits = new ScottPlot.AxisLimits(0, (DeviceCOM.Configuration.TestTime * DeviceCOM.Configuration.SamplePerSecond), 0, DeviceCOM.Factor);
                var rule = new ScottPlot.AxisRules.MinimumBoundary(
                    xAxis: WpfPlot1.Plot.Axes.Bottom,
                    yAxis: WpfPlot1.Plot.Axes.Left,
                    limits: limits
                );

                WpfPlot1.Plot.Axes.Rules.Clear();
                WpfPlot1.Plot.Axes.Rules.Add(rule);

                var d1 = DeviceCOM.Configuration.Frequency?.FD?.FirstOrDefault(d => d.FN == 1);

                if (d1 != null)
                {
                    if (thresholdLine1 != null)
                    {
                        WpfPlot1.Plot.Remove(thresholdLine1);
                    }
                    thresholdLine1 = WpfPlot1.Plot.Add.HorizontalLine(y: d1.LTH);
                    thresholdLine1.LineWidth = 0.5f;
                    thresholdLine1.Color = ScottPlot.Colors.Orange;

                    if (thresholdLine2 != null)
                    {
                        WpfPlot1.Plot.Remove(thresholdLine2);
                    }
                    thresholdLine2 = WpfPlot1.Plot.Add.HorizontalLine(y: d1.UTH);
                    thresholdLine2.LineWidth = 0.5f;
                    thresholdLine2.Color = ScottPlot.Colors.Red;

                    if (thresholdLine3 != null)
                    {
                        WpfPlot1.Plot.Remove(thresholdLine3);
                    }
                    thresholdLine3 = WpfPlot1.Plot.Add.HorizontalLine(y: d1.TH);
                    thresholdLine3.LineWidth = 0.5f;
                    thresholdLine3.Color = ScottPlot.Colors.White;
                }
            }

            D1SeetingAPS();
        }


        public void D1SeetingAPS()
        {
            //WpfPlot1.Plot.Clear();
            if (DeviceCOM.Configuration != null)
            {
                var limits = new ScottPlot.AxisLimits(0, (DeviceCOM.Configuration.TestTime * DeviceCOM.Configuration.SamplePerSecond), 0, DeviceCOM.Factor);
                var rule = new ScottPlot.AxisRules.MinimumBoundary(
                    xAxis: WpfPlotA1.Plot.Axes.Bottom,
                    yAxis: WpfPlotA1.Plot.Axes.Left,
                    limits: limits
                );

                WpfPlotA1.Plot.Axes.Rules.Clear();
                WpfPlotA1.Plot.Axes.Rules.Add(rule);

                var d1 = DeviceCOM.Configuration.Frequency?.FD?.FirstOrDefault(d => d.FN == 3);

                if (d1 != null)
                {
                    if (thresholdLine21 != null)
                    {
                        WpfPlotA1.Plot.Remove(thresholdLine21);
                    }
                    thresholdLine21 = WpfPlotA1.Plot.Add.HorizontalLine(y: d1.LTH);
                    thresholdLine21.LineWidth = 0.5f;
                    thresholdLine21.Color = ScottPlot.Colors.Orange;

                    if (thresholdLine22 != null)
                    {
                        WpfPlotA1.Plot.Remove(thresholdLine22);
                    }
                    thresholdLine22 = WpfPlotA1.Plot.Add.HorizontalLine(y: d1.UTH);
                    thresholdLine22.LineWidth = 0.5f;
                    thresholdLine22.Color = ScottPlot.Colors.Red;

                    if (thresholdLine23 != null)
                    {
                        WpfPlotA1.Plot.Remove(thresholdLine23);
                    }
                    thresholdLine23 = WpfPlotA1.Plot.Add.HorizontalLine(y: d1.TH);
                    thresholdLine23.LineWidth = 0.5f;
                    thresholdLine23.Color = ScottPlot.Colors.White;
                }
            }
        }

        private void ProcessPortData(string indata)
        {
            try
            {
                if (!string.IsNullOrEmpty(indata))
                {
                    //var res = JsonConvert.DeserializeObject<Response>(indata);

                    //if (res.FC == 21)
                    //{
                    //    //IsSystemBusy = true;
                    //    //busyStamp = System.DateTime.Now;
                    //}
                    //else if (res.FC == 22)
                    //{
                    //    //IsSystemBusy = false;                        
                    //}
                }
            }
            catch (Exception)
            {

            }
        }

        private void btnClear_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DeviceCOM.Ok = 0;
            DeviceCOM.NoOk = 0;

            lblOk.Content = "Ok Count-" + DeviceCOM.Ok.ToString();
            lblNotOk.Content = "Not Ok Count-" + DeviceCOM.NoOk.ToString();
            lblTotal.Content = "Total Count-" + (DeviceCOM.Ok + DeviceCOM.NoOk).ToString();

            wpCounter.Plot.Clear();

            wpCounter.Refresh();

            // Delete counter file 

        }

        private void btnLog_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DeviceCOM.IsLogEnable)
            {
                DeviceCOM.IsLogEnable = false;
                lblLog.Content = "Start Log";
            }
            else
            {
                partConfig = new PartConfig();
                partConfig.Closing += partConfig_Closing;
                partConfig.Owner = this;
                partConfig.ShowDialog();

            }
        }

        private void partConfig_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DeviceCOM.IsLogEnable)
            {
                lblLog.Content = "Stop Log";
                //lblPartLogs.Content = DeviceCOM.part.BatchName + " => " + DeviceCOM.part.Name;
            }
            else
            {
                //lblPartLogs.Content = "";
            }
        }

        private void ddlTT_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] != null)
            {
                var text = e.AddedItems[0]?.ToString() ?? "";
                if (DeviceCOM.Configuration != null)
                {
                    DeviceCOM.Configuration.TestTime = Convert.ToInt32(text);

                    D1Seeting();

                    System.IO.File.WriteAllText("Config.txt", JsonConvert.SerializeObject(DeviceCOM.Configuration));
                }
            }
        }

        private void btnCali_MouseDown(object sender, MouseButtonEventArgs e)
        {
            //if (DeviceCOM.IsTubeSatart || DeviceCOM.IsCalibarationStart)
            if (DeviceCOM.IsCalibarationStart)
            {
                MessageBox.Show("The tube/calibration is in progress, no calibration are allowed!", "Information");
            }
            else
            {
                byte[] data = new byte[6];
                data[0] = Convert.ToByte(2);
                data[1] = Convert.ToByte(61);
                data[2] = Convert.ToByte(1);
                data[3] = Convert.ToByte(1);

                if (!deviceCOM.WriteDataInByte(data))
                {
                    MessageBox.Show("Tno response from the ECT Instrument, please reboot it and try again!!!", "Information");
                }
            }
        }

        private void btnCalibration_MouseDown(object sender, MouseButtonEventArgs e)
        {
            //if (DeviceCOM.IsTubeSatart || DeviceCOM.IsCalibarationStart)
            if (DeviceCOM.IsCalibarationStart)

                {
                    MessageBox.Show("The tube/calibration is in progress, no calibration are allowed!", "Information");
            }
            else
            {
                byte[] data = new byte[6];
                data[0] = Convert.ToByte(2);
                data[1] = Convert.ToByte(58);
                data[2] = Convert.ToByte(1);
                data[3] = Convert.ToByte(1);

                if (!deviceCOM.WriteDataInByte(data))
                {
                    MessageBox.Show("Tno response from the ECT Instrument, please reboot it and try again!!!", "Information");
                }
            }
        }
    }


}