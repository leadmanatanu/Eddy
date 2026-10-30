using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Eddy
{
    public class VisualHost : FrameworkElement
    {
        private readonly VisualCollection _children;

        public VisualHost()
        {
            _children = new VisualCollection(this);
        }

        public VisualHost(Visual visual) : this()
        {
            _children.Add(visual);
        }

        public void AddVisual(Visual visual) => _children.Add(visual);

        public void Clear() => _children.Clear();

        public int VisualCount => _children.Count;

        protected override int VisualChildrenCount => _children.Count;

        protected override Visual GetVisualChild(int index)
        {
            if (index < 0 || index >= _children.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _children[index];
        }
    }
    

    public class UdpReceiver
    {
        public UdpClient _udpClient;
        private IPEndPoint _remoteIpEndPoint;
        public BlockingCollection<byte[]> PacketQueue { get; }

        // A structure to hold the state information for the asynchronous operation
        public struct UdpState
        {
            public UdpClient u;
            public IPEndPoint e;
        }

        public UdpReceiver(int port, BlockingCollection<byte[]>? queue = null)
        {
            _remoteIpEndPoint = new IPEndPoint(IPAddress.Any, port);
            _udpClient = new UdpClient(_remoteIpEndPoint);
            _udpClient.Client.ReceiveBufferSize = 4 * 1024 * 1024;
            PacketQueue = queue ?? new BlockingCollection<byte[]>();

            Console.WriteLine($"Listening for UDP messages on port {port} (ReceiveBufferSize=4MB)...");
        }

        public void StartReceiving()
        {
            UdpState s = new UdpState();
            s.e = _remoteIpEndPoint;
            s.u = _udpClient;
            // Begin the asynchronous receive operation
            _udpClient.BeginReceive(new AsyncCallback(ReceiveCallback), s);
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            if (ar.AsyncState is not UdpState state)
            {
                return;
            }

            UdpClient u = state.u;
            IPEndPoint? e = state.e;

            try
            {
                // Complete the asynchronous receive operation and get the data
                byte[] received = u.EndReceive(ar, ref e);
                byte[] packet = new byte[received.Length];
                Buffer.BlockCopy(received, 0, packet, 0, received.Length);

                DeviceCOM.LogPacket(packet, "SOCK");

                if (!PacketQueue.IsAddingCompleted)
                {
                    PacketQueue.Add(packet);
                }
                DeviceCOM.receiveBytes = packet;
            }
            catch (ObjectDisposedException)
            {
                // Handle cases where the UdpClient might have been closed
                Console.WriteLine("UdpClient was disposed.");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during receive: {ex.Message}");
            }
            finally
            {
                try
                {
                    // Restart listening for the next datagram
                    UdpState s = new UdpState();
                    s.e = e ?? state.e; // Use the updated IPEndPoint for the next receive
                    s.u = u;
                    u.BeginReceive(new AsyncCallback(ReceiveCallback), s);
                }
                catch (ObjectDisposedException)
                {
                    // UdpClient closed during shutdown
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error restarting receive: {ex.Message}");
                }
            }
        }

        public void StopReceiving()
        {
            try
            {
                _udpClient.Close();
                _udpClient.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping UDP receiver: {ex.Message}");
            }
            Console.WriteLine("UDP receiver stopped.");
        }
    }
    public class MenuItemViewModel
    {
        private readonly ICommand _command;

        public MenuItemViewModel()
        {
            _command = new CommandViewModel(Execute);
        }
        public string Header { get; set; } = string.Empty;
        string? filename { get; set; }
        public MainWindow? mainWindow { get; set; }
        public ObservableCollection<MenuItemViewModel> MenuItems { get; set; } = new ObservableCollection<MenuItemViewModel>();

        public FrequencySetting? freqPop { get; set; }
        public Attenuation? attenuationPop { get; set; }
        public MarkerSetting? markerPop { get; set; }


        public ICommand Command
        {
            get
            {
                return _command;
            }
        }

        private void Execute()
        {
            if (mainWindow == null)
            {
                return;
            }

            if (DeviceCOM.IsTubeSatart || DeviceCOM.IsCalibarationStart && (Header == "Open" || Header == "New" || Header == "Save As" || Header == "Save" || Header == "Write Configuration" || Header == "Marker Setting"))
            {
                MessageBox.Show("The tube/calibration is in progress, no changes are allowed!", "Information");
            }
            else
            {
                if (Header == "Save")
                {
                    try
                    {
                        if (String.IsNullOrEmpty(mainWindow.filename))
                        {
                            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                            dlg.FileName = "Document"; // Default file name
                            dlg.DefaultExt = ".text"; // Default file extension
                            dlg.Filter = "Text documents (.txt)|*.txt"; // Filter files by extension

                            // Show save file dialog box
                            Nullable<bool> result = dlg.ShowDialog();

                            // Process save file dialog box results
                            if (result == true)
                            {
                                // Save document
                                mainWindow.filename = dlg.FileName;

                                string conecnt = JsonConvert.SerializeObject(DeviceCOM.Configuration);
                                File.WriteAllText(mainWindow.filename, conecnt);
                                //MessageBox.Show("Configuation changes saved at '" + filename + "'!!!!");
                                this.mainWindow.lblConfigFileName.Content = mainWindow.filename;
                            }

                        }
                        else
                        {
                            string conecnt = JsonConvert.SerializeObject(DeviceCOM.Configuration);
                            File.WriteAllText(mainWindow.filename, conecnt);
                        }

                    }
                    catch (Exception)
                    {
                        MessageBox.Show("Error while saving the configation file!!!!", "Error Information");
                    }

                }
                else if (Header == "Save As")
                {
                    try
                    {
                        Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                        dlg.FileName = "Document"; // Default file name
                        dlg.DefaultExt = ".text"; // Default file extension
                        dlg.Filter = "Text documents (.txt)|*.txt"; // Filter files by extension

                        // Show save file dialog box
                        Nullable<bool> result = dlg.ShowDialog();

                        // Process save file dialog box results
                        if (result == true)
                        {
                            // Save document
                            mainWindow.filename = dlg.FileName;

                            string conecnt = JsonConvert.SerializeObject(DeviceCOM.Configuration);
                            File.WriteAllText(mainWindow.filename, conecnt);
                            this.mainWindow.lblConfigFileName.Content = mainWindow.filename;
                        }
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("Error while saving the configuration file!!!!", "Error Information");
                    }
                }
                else if (Header == "Open")
                {

                    try
                    {
                        var dialog = new Microsoft.Win32.OpenFileDialog();
                        dialog.FileName = "Document"; // Default file name
                        dialog.DefaultExt = ".txt"; // Default file extension
                        dialog.Filter = "Text documents (.txt)|*.txt"; // Filter files by extension

                        // Show open file dialog box
                        bool? result = dialog.ShowDialog();

                        // Process open file dialog box results
                        if (result == true)
                        {
                            string data = File.ReadAllText(dialog.FileName);
                            var cfg = JsonConvert.DeserializeObject<Configuration>(data);
                            if (cfg != null)
                            {
                                DeviceCOM.Configuration = cfg;
                            }
                            // Open document
                            mainWindow.filename = dialog.FileName;
                            this.mainWindow.lblConfigFileName.Content = mainWindow.filename;
                        }
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("Error while loading the configuration file!!!!", "Error Information");
                    }
                }
                else if (Header == "New")
                {
                    mainWindow.filename = null;

                }
                else if (Header == "Exit")
                {
                    //this.mainWindow.btnLog.Visibility = Visibility.Hidden;
                    mainWindow.Close();
                }
                else if (Header == "Frequency Setting")
                {
                    freqPop = new FrequencySetting();
                    freqPop.Closing += freqPop_Closing;
                    freqPop.deviceCOM = mainWindow.deviceCOM;
                    freqPop.Owner = mainWindow;
                    freqPop.ShowDialog();
                }
                else if (Header == "Marker Setting")
                {
                    markerPop = new MarkerSetting();
                    markerPop.Closing += markerPop_Closing;
                    markerPop.deviceCOM = mainWindow.deviceCOM;
                    markerPop.Owner = mainWindow;
                    markerPop.ShowDialog();
                }
                else if (Header == "Attenuation")
                {
                    attenuationPop = new Attenuation();
                    //attenuationPop.Closing += freqPop_Closing;
                    attenuationPop.deviceCOM = mainWindow.deviceCOM;
                    attenuationPop.Owner = mainWindow;
                    attenuationPop.ShowDialog();
                }
                else if (Header == "Write Configuration")
                {
                    bool rat1;
                    bool rat2;
                    var msg = "Configuation Write successfully!!";
                    var IsEddyAdvance = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["IsEddyAdvance"]);
                    if (IsEddyAdvance)
                    {
                        rat1 = true;                        

                        if (DeviceCOM.IsJSON)
                        {
                            ConfigurationToWrite configurationToWrite = new ConfigurationToWrite();
                            configurationToWrite.FQ = DeviceCOM.Configuration.Frequency.FD;
                            configurationToWrite.FT = DeviceCOM.Configuration.Filter.FD;
                            var data = JsonConvert.SerializeObject(configurationToWrite);
                            rat2 = mainWindow.deviceCOM.WriteData(data);
                        }
                        else 
                        { 
                            byte[] data1 = new byte[29];
                            data1[0] = Convert.ToByte(2);
                            data1[1] = Convert.ToByte(57);
                            data1[2] = Convert.ToByte(24);
                            data1[3] = Convert.ToByte(1);
                            data1[4] = Convert.ToByte(0);
                            data1[5] = Convert.ToByte(1);

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

                            rat2 = mainWindow.deviceCOM.WriteDataInByte(data1);
                        }
                        
                        
                    }
                    else
                    {

                        rat1 = mainWindow.deviceCOM.WriteData(JsonConvert.SerializeObject(DeviceCOM.Configuration.Frequency));
                        Filter1 filter1 = new Filter1();
                        filter1.FD = new List<FilterFD1>();

                        foreach (var item in DeviceCOM.Configuration.Filter.FD)
                        {
                            filter1.FD.Add(new FilterFD1 { FN = item.FN, H = item.H, L = item.L });
                        }

                        rat2 = mainWindow.deviceCOM.WriteData(JsonConvert.SerializeObject(filter1));
                    }

                    //ConfigurationToWrite configurationWrite = new ConfigurationToWrite();
                    //configurationWrite.Frequency = DeviceCOM.Configuration.Frequency;
                    //configurationWrite.Filter = DeviceCOM.Configuration.Filter;
                    //var rat = mainWindow.deviceCOM.WriteData(JsonConvert.SerializeObject(configurationWrite));

                    if (!rat1 || !rat2)
                    {
                        msg = "No response from the system, please reboot the ECT Instrument";
                    }

                    MessageBox.Show(msg, "Information");
                }
                else if (Header == "Batch Wise Log")
                {
                    Logs logs = new Logs();
                    logs.ShowDialog();
                }
            }
        }

        private void freqPop_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (freqPop?.IsSaved == true)
            {
                this.mainWindow?.InitialGraphSetting();
                this.mainWindow?.D1Seeting();
            }
        }
        private void attenProp_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            //if (attenuationPop.IsSaved)
            //{
            //    this.mainWindow.InitialGraphSetting();
            //    this.mainWindow.D1Seeting();
            //}
        }
        private void markerPop_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            //if (markerPop.IsSaved)
            //{
            //    mainWindow.deviceCOM.WriteData(JsonConvert.SerializeObject(DeviceCOM.Configuration.Marker));   
            //}
        }

    }
    public class CommandViewModel : ICommand
    {
        private readonly Action _action;

        public CommandViewModel(Action action)
        {
            _action = action;
        }

        public void Execute(object? o)
        {
            _action();
        }

        public bool CanExecute(object? o)
        {
            return true;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }
    }

    public class MenuItemViewModel_APS
    {
        private readonly ICommand _command;

        public MenuItemViewModel_APS()
        {
            _command = new CommandViewModel(Execute);
        }
        public string Header { get; set; } = string.Empty;
        string? filename { get; set; }
        public MainWindow_APS? mainWindow { get; set; }
        public ObservableCollection<MenuItemViewModel_APS> MenuItems { get; set; } = new ObservableCollection<MenuItemViewModel_APS>();
        public FrequencySetting_APS? freqPop { get; set; }
        public Attenuation? attenuationPop { get; set; }
        public MarkerSetting? markerPop { get; set; }


        public ICommand Command
        {
            get
            {
                return _command;
            }
        }

        private void Execute()
        {
            if (mainWindow == null)
            {
                return;
            }

            if (DeviceCOM.IsTubeSatart || DeviceCOM.IsCalibarationStart && (Header == "Open" || Header == "New" || Header == "Save As" || Header == "Save" || Header == "Write Configuration" || Header == "Marker Setting"))
            {
                MessageBox.Show("The tube/calibration is in progress, no changes are allowed!", "Information");
            }
            else
            {
                if (Header == "Save")
                {
                    try
                    {
                        if (String.IsNullOrEmpty(mainWindow.filename))
                        {
                            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                            dlg.FileName = "Document"; // Default file name
                            dlg.DefaultExt = ".text"; // Default file extension
                            dlg.Filter = "Text documents (.txt)|*.txt"; // Filter files by extension

                            // Show save file dialog box
                            Nullable<bool> result = dlg.ShowDialog();

                            // Process save file dialog box results
                            if (result == true)
                            {
                                // Save document
                                mainWindow.filename = dlg.FileName;

                                string conecnt = JsonConvert.SerializeObject(DeviceCOM.Configuration);
                                File.WriteAllText(mainWindow.filename, conecnt);
                                //MessageBox.Show("Configuation changes saved at '" + filename + "'!!!!");
                                this.mainWindow.lblConfigFileName.Content = mainWindow.filename;
                            }

                        }
                        else
                        {
                            string conecnt = JsonConvert.SerializeObject(DeviceCOM.Configuration);
                            File.WriteAllText(mainWindow.filename, conecnt);
                        }

                    }
                    catch (Exception)
                    {
                        MessageBox.Show("Error while saving the configation file!!!!", "Error Information");
                    }

                }
                else if (Header == "Save As")
                {
                    try
                    {
                        Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                        dlg.FileName = "Document"; // Default file name
                        dlg.DefaultExt = ".text"; // Default file extension
                        dlg.Filter = "Text documents (.txt)|*.txt"; // Filter files by extension

                        // Show save file dialog box
                        Nullable<bool> result = dlg.ShowDialog();

                        // Process save file dialog box results
                        if (result == true)
                        {
                            // Save document
                            mainWindow.filename = dlg.FileName;

                            string conecnt = JsonConvert.SerializeObject(DeviceCOM.Configuration);
                            File.WriteAllText(mainWindow.filename, conecnt);
                            this.mainWindow.lblConfigFileName.Content = mainWindow.filename;
                        }
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("Error while saving the configuration file!!!!", "Error Information");
                    }
                }
                else if (Header == "Open")
                {

                    try
                    {
                        var dialog = new Microsoft.Win32.OpenFileDialog();
                        dialog.FileName = "Document"; // Default file name
                        dialog.DefaultExt = ".txt"; // Default file extension
                        dialog.Filter = "Text documents (.txt)|*.txt"; // Filter files by extension

                        // Show open file dialog box
                        bool? result = dialog.ShowDialog();

                        // Process open file dialog box results
                        if (result == true)
                        {
                            string data = File.ReadAllText(dialog.FileName);
                            var cfg = JsonConvert.DeserializeObject<Configuration>(data);
                            if (cfg != null)
                            {
                                DeviceCOM.Configuration = cfg;
                            }
                            // Open document
                            mainWindow.filename = dialog.FileName;
                            this.mainWindow.lblConfigFileName.Content = mainWindow.filename;
                        }
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("Error while loading the configuration file!!!!", "Error Information");
                    }
                }
                else if (Header == "New")
                {
                    mainWindow.filename = null;

                }
                else if (Header == "Exit")
                {
                    //this.mainWindow.btnLog.Visibility = Visibility.Hidden;
                    mainWindow.Close();
                }
                else if (Header == "Frequency Setting")
                {
                    freqPop = new FrequencySetting_APS();
                    freqPop.Closing += freqPop_Closing;
                    freqPop.deviceCOM = mainWindow.deviceCOM;
                    freqPop.Owner = mainWindow;
                    freqPop.ShowDialog();
                }
                else if (Header == "Marker Setting")
                {
                    markerPop = new MarkerSetting();
                    markerPop.Closing += markerPop_Closing;
                    markerPop.deviceCOM = mainWindow.deviceCOM;
                    markerPop.Owner = mainWindow;
                    markerPop.ShowDialog();
                }
                else if (Header == "Attenuation")
                {
                    attenuationPop = new Attenuation();
                    //attenuationPop.Closing += freqPop_Closing;
                    attenuationPop.deviceCOM = mainWindow.deviceCOM;
                    attenuationPop.Owner = mainWindow;
                    attenuationPop.ShowDialog();
                }
                else if (Header == "Write Configuration")
                {
                    bool rat1;
                    bool rat2;
                    var msg = "Configuation Write successfully!!";
                    var IsEddyAdvance = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["IsEddyAdvance"]);
                    if (IsEddyAdvance)
                    {
                        rat1 = true;

                        var isAbsolute = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["isAbsolute"]);

                        if (isAbsolute)
                        {
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

                            rat2 = mainWindow.deviceCOM.WriteDataInByte(data1);
                        }
                        else
                        {
                            ConfigurationToWrite configurationToWrite = new ConfigurationToWrite();
                            configurationToWrite.FQ = DeviceCOM.Configuration.Frequency.FD;
                            configurationToWrite.FT = DeviceCOM.Configuration.Filter.FD;
                            var data = JsonConvert.SerializeObject(configurationToWrite);
                            rat2 = mainWindow.deviceCOM.WriteData(data);
                        }
                    }
                    else
                    {

                        rat1 = mainWindow.deviceCOM.WriteData(JsonConvert.SerializeObject(DeviceCOM.Configuration.Frequency));
                        Filter1 filter1 = new Filter1();
                        filter1.FD = new List<FilterFD1>();

                        foreach (var item in DeviceCOM.Configuration.Filter.FD)
                        {
                            filter1.FD.Add(new FilterFD1 { FN = item.FN, H = item.H, L = item.L });
                        }

                        rat2 = mainWindow.deviceCOM.WriteData(JsonConvert.SerializeObject(filter1));
                    }

                    //ConfigurationToWrite configurationWrite = new ConfigurationToWrite();
                    //configurationWrite.Frequency = DeviceCOM.Configuration.Frequency;
                    //configurationWrite.Filter = DeviceCOM.Configuration.Filter;
                    //var rat = mainWindow.deviceCOM.WriteData(JsonConvert.SerializeObject(configurationWrite));

                    if (!rat1 || !rat2)
                    {
                        msg = "No response from the system, please reboot the ECT Instrument";
                    }

                    MessageBox.Show(msg, "Information");
                }
                else if (Header == "Batch Wise Log")
                {
                    Logs logs = new Logs();
                    logs.ShowDialog();
                }
            }
        }

        private void freqPop_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (freqPop?.IsSaved == true)
            {
                this.mainWindow?.InitialGraphSetting();
                this.mainWindow?.D1Seeting();
            }
        }
        private void attenProp_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            //if (attenuationPop.IsSaved)
            //{
            //    this.mainWindow.InitialGraphSetting();
            //    this.mainWindow.D1Seeting();
            //}
        }
        private void markerPop_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            //if (markerPop.IsSaved)
            //{
            //    mainWindow.deviceCOM.WriteData(JsonConvert.SerializeObject(DeviceCOM.Configuration.Marker));   
            //}
        }

    }
}
