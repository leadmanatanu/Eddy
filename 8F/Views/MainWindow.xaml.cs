using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.IO.Ports;
using System.Management;
using System.Net.Sockets;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using File = System.IO.File;

namespace _8F
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<MenuItemViewModel> MenuItems { get; set; } = new();
        public CircleSetting? ellipsesPop { get; set; }
        public AutoEllipse? autoEllipsePop { get; set; }
        public PartConfig? partConfig { get; set; }
        public PartConfigReNew? partConfigReNew { get; set; }
        public DeviceCOM? portCOM;
        public Report? report;
        DispatcherTimer? dispatcherTimer;
        public int chNo;
        public string filename { get; set; } = string.Empty;
        double factor = 20;

        int ScreenId = 1;
        int BoxSize1 = 430;
        int BoxSize2 = 0;
        int BoxSize3 = 0;
        int BoxSize4 = 0;
        int seqLength = 0;
        int CommunicationType = 0;
        int FrequencyNo = 8;
        public string WebPage = string.Empty;

        int modeApp = 0;
        int mode = 0;
        //bool IsBalanceAll = false;
        public SolidColorBrush disableColor = new SolidColorBrush(Colors.DarkGray);
        public SolidColorBrush enableColor = new SolidColorBrush(Colors.White);
        bool IsSerialmatch = true;
        private SerialPort? _serialPort;

        DateTime CodeReadTime = DateTime.Now;
        int CodeReadGapInMS = 100;

        private static bool GetConfigBool(string key, bool defaultValue = true)
        {
            return _8F.Services.ConfigKey.ProtectedSettings.GetBool(key, defaultValue);


        }

        private static int GetConfigInt(string key, int defaultValue = 5020) => _8F.Services.ConfigKey.ProtectedSettings.GetInt(key, defaultValue); /*
        {
            return _8F.Services.ConfigKey.ProtectedSettings.GetBool(key, defaultValue);


        }

        */ bool isRenewConfig => _8F.Services.ConfigKey.ProtectedSettings.IsReNewConfig;
        bool isTestLogOff => _8F.Services.ConfigKey.ProtectedSettings.IsTestLogOff;
        bool isTxStrengthEnabled => _8F.Services.ConfigKey.ProtectedSettings.IsTxStrengthEnable;

        // Config-driven feature toggle fields (WithDb / WithFile)
        public bool isOpenWithDb => _8F.Services.ConfigKey.ProtectedSettings.IsOpenWithDb;
        public bool isOpenWithFile => _8F.Services.ConfigKey.ProtectedSettings.IsOpenWithFile;

        public bool isSaveWithDb => _8F.Services.ConfigKey.ProtectedSettings.IsSaveWithDb;
        public bool isSaveWithFile => _8F.Services.ConfigKey.ProtectedSettings.IsSaveWithFile;

        public bool isSaveAsWithDb => _8F.Services.ConfigKey.ProtectedSettings.IsSaveAsWithDb;
        public bool isSaveAsWithFile => _8F.Services.ConfigKey.ProtectedSettings.IsSaveAsWithFile;

        public bool isImportConfigWithDb => _8F.Services.ConfigKey.ProtectedSettings.IsImportConfigurationWithDb;
        public bool isImportConfigWithFile => _8F.Services.ConfigKey.ProtectedSettings.IsImportConfigurationWithFile;

        public bool isExportConfigWithDb => _8F.Services.ConfigKey.ProtectedSettings.IsExportConfigurationWithDb;
        public bool isExportConfigWithFile => _8F.Services.ConfigKey.ProtectedSettings.IsExportConfigurationWithFile;

        bool isExitEnable => _8F.Services.ConfigKey.ProtectedSettings.IsExitEnable;

        public bool isModbusServerEnable => _8F.Services.ConfigKey.ProtectedSettings.IsModbusServerEnable;
        public int modbusServerPort => _8F.Services.ConfigKey.ProtectedSettings.ModbusServerPort;
        public int modbusHeartbeatIntervalMs => _8F.Services.ConfigKey.ProtectedSettings.ModbusHeartbeatIntervalMs;
        public IModbusSlaveService modbusSlaveService = new ModbusSlaveService();

        public bool isPasswordEnable => _8F.Services.ConfigKey.ProtectedSettings.IsPasswordEnable;
        bool isChangeConfigEnable => _8F.Services.ConfigKey.ProtectedSettings.IsChangeConfigurationEnable;
        bool isThresholdSettingEnable => _8F.Services.ConfigKey.ProtectedSettings.IsThresholdSettingEnable;
        bool isAutoEllipseEnabled => _8F.Services.ConfigKey.ProtectedSettings.IsAutoEllipseEnable;
        bool isWriteConfigEnable => _8F.Services.ConfigKey.ProtectedSettings.IsWriteConfigurationEnable;
        bool isCopyChannel1ConfigEnable => _8F.Services.ConfigKey.ProtectedSettings.IsCopyChannel1ConfigurationEnable;

        bool isBatchWiseLogEnable => _8F.Services.ConfigKey.ProtectedSettings.IsBatchWiseLogEnable;
        bool isSerialNoLogEnable => _8F.Services.ConfigKey.ProtectedSettings.IsSerialNumberLogEnable;
        bool isPdfReportEnable => _8F.Services.ConfigKey.ProtectedSettings.IsPdfReportEnable;

        bool isTotalCountVisible => _8F.Services.ConfigKey.ProtectedSettings.IsTotalCountVisible;
        bool isOkCountVisible => _8F.Services.ConfigKey.ProtectedSettings.IsOkCountVisible;
        bool isNotOkCountVisible => _8F.Services.ConfigKey.ProtectedSettings.IsNotOkCountVisible;

        public MainWindow()
        {
            var configResult = _8F.Services.ConfigKey.ConfigKeyService.Load();
            if (!configResult.Success)
            {
                _8F.Services.ConfigKey.ConfigKeyService.ShowVerificationError(configResult.ErrorCode ?? "CK-02");
                Close();
                return;
            }
            /* if (false)
            {
                Close();
                return;
            }

            */ InitializeComponent();

            // UI Hides for Total Count, OK Count, and Not OK Count
            if (!isTotalCountVisible)
            {
                lblTCount.Visibility = Visibility.Collapsed;
                lblTCount1.Visibility = Visibility.Collapsed;
                lblTCount2.Visibility = Visibility.Collapsed;

                if (lblTCount.Parent is FrameworkElement p0) p0.Visibility = Visibility.Collapsed;
                if (lblTCount1.Parent is FrameworkElement p1) p1.Visibility = Visibility.Collapsed;
                if (lblTCount2.Parent is FrameworkElement p2) p2.Visibility = Visibility.Collapsed;
            }

            if (!isOkCountVisible)
            {
                lblOkCount.Visibility = Visibility.Collapsed;
                lblOkCount1.Visibility = Visibility.Collapsed;
                lblOkCount2.Visibility = Visibility.Collapsed;

                if (lblOkCount.Parent is FrameworkElement p0) p0.Visibility = Visibility.Collapsed;
                if (lblOkCount1.Parent is FrameworkElement p1) p1.Visibility = Visibility.Collapsed;
                if (lblOkCount2.Parent is FrameworkElement p2) p2.Visibility = Visibility.Collapsed;
            }

            if (!isNotOkCountVisible)
            {
                lblNotOkCount.Visibility = Visibility.Collapsed;
                lblNotOkCount1.Visibility = Visibility.Collapsed;
                lblNotOkCount2.Visibility = Visibility.Collapsed;

                if (lblNotOkCount.Parent is FrameworkElement p0) p0.Visibility = Visibility.Collapsed;
                if (lblNotOkCount1.Parent is FrameworkElement p1) p1.Visibility = Visibility.Collapsed;
                if (lblNotOkCount2.Parent is FrameworkElement p2) p2.Visibility = Visibility.Collapsed;
            }

            //DeviceCOM.Test();
            if (imgLogo.Visibility == Visibility.Visible)
            {
                string? LogoPath = System.Configuration.ConfigurationManager.AppSettings["LogoPath"];
                if (!string.IsNullOrEmpty(LogoPath))
                {
                    imgLogo.Source = new BitmapImage(new Uri(LogoPath));
                }
            }

            //List<string> lines = new List<string>
            //{
            //    "Application Started at " + DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss")
            //};
            //string FilePath = System.Configuration.ConfigurationManager.AppSettings["CSVPath"].ToString() +  "asd.csv";

            //File.AppendAllLines(FilePath, lines);
            //var FileName = System.DateTime.Now.ToString();

            WebPage = System.Configuration.ConfigurationManager.AppSettings["WebPage"] ?? string.Empty;
            DeviceCOM.IsJSON = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["IsJSON"]);
            DeviceCOM.IsLogRequiredOnBalance = _8F.Services.ConfigKey.ProtectedSettings.IsLogRequiredOnBalance;
            ScreenId = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["ScreenId"]);
            BoxSize1 = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["BoxSize1"]);
            BoxSize2 = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["BoxSize2"]);
            BoxSize3 = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["BoxSize3"]);
            BoxSize4 = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["BoxSize4"]);
            FrequencyNo = _8F.Services.ConfigKey.ProtectedSettings.FrequencyNo;
            var LogEnabled = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["LogEnable"]);
            modeApp = Convert.ToInt16(System.Configuration.ConfigurationManager.AppSettings["AppMode"]);
            CodeReadGapInMS = Convert.ToInt16(System.Configuration.ConfigurationManager.AppSettings["CodeReadGapInMS"]);
            if (modeApp == 1)
            {
                mode = Convert.ToInt16(System.Configuration.ConfigurationManager.AppSettings["Mode"]);
            }
            if (!LogEnabled)
            {
                btnLog.Visibility = Visibility.Hidden;
                btnLog1.Visibility = Visibility.Hidden;
                btnLog2.Visibility = Visibility.Hidden;
                LogWidth.Width = new GridLength(0.0, GridUnitType.Star);
                LogHeight.Height = new GridLength(0.0, GridUnitType.Star);
            }

            if (ScreenId == 1)
            {
                seqLength = BoxSize1;
                menuHeight.Height = new GridLength(0.5, GridUnitType.Star);
                chennelHeight.Height = new GridLength(0.7, GridUnitType.Star);
                buttonBarHeight.Height = new GridLength(0.0, GridUnitType.Star);
                buttonBarWidth.Width = new GridLength(.38, GridUnitType.Star);
                LogoWidth.Width = new GridLength(0.1, GridUnitType.Star);
            }
            else if (ScreenId == 2)
            {
                seqLength = BoxSize2;
                menuHeight.Height = new GridLength(0.8, GridUnitType.Star);
                chennelHeight.Height = new GridLength(0.6, GridUnitType.Star);
                buttonBarHeight.Height = new GridLength(2, GridUnitType.Star);
                buttonBarWidth.Width = new GridLength(0.0, GridUnitType.Star);
                LogoWidth.Width = new GridLength(0.1, GridUnitType.Star);

            }
            else if (ScreenId == 3)
            {
                seqLength = BoxSize3;
                menuHeight.Height = new GridLength(0.5, GridUnitType.Star);
                chennelHeight.Height = new GridLength(0.7, GridUnitType.Star);
                buttonBarHeight.Height = new GridLength(0.0, GridUnitType.Star);
                buttonBarWidth.Width = new GridLength(.38, GridUnitType.Star);
                LogoWidth.Width = new GridLength(0.1, GridUnitType.Star);
            }

            else if (ScreenId == 4)
            {
                seqLength = BoxSize4;
                menuHeight.Height = new GridLength(0.8, GridUnitType.Star);
                chennelHeight.Height = new GridLength(0.6, GridUnitType.Star);
                buttonBarHeight.Height = new GridLength(2, GridUnitType.Star);
                buttonBarWidth.Width = new GridLength(0.0, GridUnitType.Star);
                LogoWidth.Width = new GridLength(0.1, GridUnitType.Star);

                SetFrequencey();
            }

            portCOM = new DeviceCOM();

            if (isModbusServerEnable)
            {
                try
                {
                    modbusSlaveService.RegisterValueChanged += OnModbusRegisterIdReceived;
                    modbusSlaveService.Start(modbusServerPort, modbusHeartbeatIntervalMs);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Modbus TCP Slave start notice: {ex.Message}");
                }
            }

            factor = Convert.ToDouble(System.Configuration.ConfigurationManager.AppSettings["Factor"]);
            DeviceCOM.DefaultWidth = Convert.ToInt16(System.Configuration.ConfigurationManager.AppSettings["Width"]);
            DeviceCOM.DefaultHeight = Convert.ToInt16(System.Configuration.ConfigurationManager.AppSettings["Height"]);
            DeviceCOM.DefaultWidth_O = Convert.ToInt16(System.Configuration.ConfigurationManager.AppSettings["Width_O"]);
            DeviceCOM.DefaultHeight_O = Convert.ToInt16(System.Configuration.ConfigurationManager.AppSettings["Height_O"]);
            DeviceCOM.DefaultAngel_O = Convert.ToInt16(System.Configuration.ConfigurationManager.AppSettings["Angel_O"]);

            if (modeApp == 1)
            {
                el11.Visibility = Visibility.Visible;
            }
            else
            {
                el11.Visibility = Visibility.Hidden;
            }
            CommunicationType = Convert.ToInt16(System.Configuration.ConfigurationManager.AppSettings["CommunicationType"]);
            int baudRate = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["BaudRate"]);
            string portName = System.Configuration.ConfigurationManager.AppSettings["PortName"] ?? string.Empty;

            string IpAddress = System.Configuration.ConfigurationManager.AppSettings["IP"] ?? string.Empty;
            int Port = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["Port"]);

            DeviceCOM.ConnectionString = System.Configuration.ConfigurationManager.AppSettings["ConnectionString"] ?? string.Empty;

            portCOM.InitialPort(CommunicationType, portName, baudRate, IpAddress, Port);

            DeviceCOM.responses = new List<Response>();
            DeviceCOM.counter = new List<Counter>();
            for (int i = 0; i <= 8; i++)
            {
                DeviceCOM.counter.Add(new Counter { Id = i });
            }
            chNo = Convert.ToInt16(System.Configuration.ConfigurationManager.AppSettings["Channel"]);
            DeviceCOM.ChannelNo = chNo;
            if (chNo == 1)
            {
                btnCh1.Visibility = Visibility.Hidden;
                btnCh2.Visibility = Visibility.Hidden;
                btnCh3.Visibility = Visibility.Hidden;
                btnCh4.Visibility = Visibility.Hidden;
            }
            else if (chNo == 2)
            {
                btnCh1.Visibility = Visibility.Visible;
                btnCh2.Visibility = Visibility.Visible;
                btnCh3.Visibility = Visibility.Hidden;
                btnCh4.Visibility = Visibility.Hidden;
            }
            else if (chNo == 3)
            {
                btnCh1.Visibility = Visibility.Visible;
                btnCh2.Visibility = Visibility.Visible;
                btnCh3.Visibility = Visibility.Visible;
                btnCh4.Visibility = Visibility.Hidden;
            }
            else if (chNo == 4)
            {
                btnCh1.Visibility = Visibility.Visible;
                btnCh2.Visibility = Visibility.Visible;
                btnCh3.Visibility = Visibility.Visible;
                btnCh4.Visibility = Visibility.Visible;
            }

            MenuItems = new ObservableCollection<MenuItemViewModel>(new List<MenuItemViewModel?>
            {
                new MenuItemViewModel { Header = "File",
                    MenuItems = new ObservableCollection<MenuItemViewModel>(new List<MenuItemViewModel?>
                    {
                        new MenuItemViewModel { Header = "New", mainWindow = this },
                        (isOpenWithDb || isOpenWithFile) ? new MenuItemViewModel { Header = "Open", mainWindow = this } : null,
                        (isSaveWithDb || isSaveWithFile) ? new MenuItemViewModel { Header = "Save", mainWindow = this } : null,
                        (isSaveAsWithDb || isSaveAsWithFile) ? new MenuItemViewModel { Header = "Save As", mainWindow = this } : null,
                        (isImportConfigWithDb || isImportConfigWithFile) ? new MenuItemViewModel { Header = "Import Configuration", mainWindow = this } : null,
                        (isExportConfigWithDb || isExportConfigWithFile) ? new MenuItemViewModel { Header = "Export Configuration", mainWindow = this } : null,
                        isExitEnable ? new MenuItemViewModel { Header = "Exit", mainWindow = this } : null
                    }.OfType<MenuItemViewModel>())
                },
                new MenuItemViewModel { Header = "Configuration",
                    MenuItems = LogEnabled ? new ObservableCollection<MenuItemViewModel>(new List<MenuItemViewModel?>
                    {
                        isChangeConfigEnable ? new MenuItemViewModel { Header = "Change Configuration", mainWindow = this } : null,
                        isThresholdSettingEnable ? new MenuItemViewModel { Header = "Threshold Setting", mainWindow = this } : null,
                        isAutoEllipseEnabled ? new MenuItemViewModel { Header = "Auto Ellipse", mainWindow = this } : null,
                        isRenewConfig ? new MenuItemViewModel { Header = "Operator Master", mainWindow = this } : null,
                        isRenewConfig ? new MenuItemViewModel { Header = "Part Master", mainWindow = this } : null,
                        isWriteConfigEnable ? new MenuItemViewModel { Header = "Write Configuration", mainWindow = this } : null,
                        isCopyChannel1ConfigEnable ? new MenuItemViewModel { Header = "Copy Channel-1 Configuration", mainWindow = this } : null,
                    }.OfType<MenuItemViewModel>()
                    ) :
                    new ObservableCollection<MenuItemViewModel>(new List<MenuItemViewModel?>
                    {
                        isChangeConfigEnable ? new MenuItemViewModel { Header = "Change Configuration", mainWindow = this } : null,
                        isThresholdSettingEnable ? new MenuItemViewModel { Header = "Threshold Setting", mainWindow = this } : null,
                        isAutoEllipseEnabled ? new MenuItemViewModel { Header = "Auto Ellipse", mainWindow = this } : null,
                        isWriteConfigEnable ? new MenuItemViewModel { Header = "Write Configuration", mainWindow = this } : null,
                        isCopyChannel1ConfigEnable ? new MenuItemViewModel { Header = "Copy Channel-1 Configuration", mainWindow = this } : null
                    }.OfType<MenuItemViewModel>()
                    )
                },
                new MenuItemViewModel
                {
                    Header = "View Log",
                    MenuItems = new ObservableCollection<MenuItemViewModel>(new List<MenuItemViewModel?>
                    {
                        isBatchWiseLogEnable ? new MenuItemViewModel { Header = "Batch Wise Log", mainWindow = this } : null,
                        (!isRenewConfig && isSerialNoLogEnable) ? new MenuItemViewModel { Header = "Serial Number Log", mainWindow = this } : null
                    }.OfType<MenuItemViewModel>())
                },
                isModbusServerEnable ? new MenuItemViewModel
                {
                    Header = "Modbus",
                    MenuItems = new ObservableCollection<MenuItemViewModel>(new List<MenuItemViewModel?>
                    {
                        new MenuItemViewModel { Header = "Modbus Diagnostic Panel", mainWindow = this }
                    }.OfType<MenuItemViewModel>())
                } : null
            }.OfType<MenuItemViewModel>());
            DataContext = this;

            InitialGraphData(true);

            var CodePortName = Convert.ToString(System.Configuration.ConfigurationManager.AppSettings["CodePortName"]);
            if (!string.IsNullOrEmpty(CodePortName) && !CodePortName.Equals("NONE", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    _serialPort = new SerialPort(CodePortName, 115200);
                    _serialPort.DataBits = 8;
                    _serialPort.Parity = Parity.Even;
                    _serialPort.StopBits = StopBits.One;
                    _serialPort.Handshake = Handshake.None;
                    _serialPort.DataReceived += OnDataReceived;

                    if (!_serialPort.IsOpen)
                    {
                        _serialPort.Open();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CodePort {CodePortName} error: {ex.Message}");
                }
            }


            dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = TimeSpan.FromMilliseconds(100);
            dispatcherTimer.Start();

            Status status = new Status() { FC = 23 };

            bool rat = false;
            var IsJSON = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["IsJSON"]);
            if (IsJSON)
            {
                rat = portCOM.GetSystemStatus(JsonConvert.SerializeObject(status));
            }
            else
            {
                byte[] data = new byte[5];
                data[0] = Convert.ToByte(2);
                data[1] = Convert.ToByte(23);
                data[2] = Convert.ToByte(0);

                rat = portCOM.GetSystemStatusInBytes(data);
            }

            if (DeviceCOM.IsSystemBusy || !rat)
            {
                ImplementChanges(1);
            }
            else
            {
                var ratval = ImplementChanges(0);
            }
        }

        /* private bool ValidateMachine()
        {
            try
            {
                bool isValidationRequired = false;
                string? isValStr = System.Configuration.ConfigurationManager.AppSettings["IsMachineValidationRequired"];
                if (!string.IsNullOrEmpty(isValStr) && bool.TryParse(isValStr, out bool req))
                {
                    isValidationRequired = req;
                }

                if (!isValidationRequired)
                {
                    return true;
                }

                string filePath = @"C:\ProgramData\Eddy\Config.txt";

                // 1. Configuration file does not exist
                if (!File.Exists(filePath))
                {
                    string localConfig = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config.txt");
                    if (File.Exists(localConfig))
                    {
                        filePath = localConfig;
                    }
                    else
                    {
                        MessageBox.Show(
                            "The application configuration file could not be found.\n\n" +
                            "Please contact your administrator to obtain a valid configuration file.",
                            "Configuration Required",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        return false;
                    }
                }

                // 2. Read encrypted configuration
                string encryptedText = File.ReadAllText(filePath).Trim();

                if (string.IsNullOrWhiteSpace(encryptedText))
                {
                    MessageBox.Show(
                        "The application configuration file is empty.\n\n" +
                        "Please contact your administrator for a valid configuration file.",
                        "Invalid Configuration",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return false;
                }

                // 3. AES Key
                byte[] key = Encoding.UTF8.GetBytes("12345678901234567890123456789012");

                // 4. AES IV
                byte[] iv = Encoding.UTF8.GetBytes("1234567890123456");

                // 5. Convert Base64
                byte[] encryptedBytes;

                try
                {
                    encryptedBytes = Convert.FromBase64String(encryptedText);
                }
                catch
                {
                    MessageBox.Show(
                        "The application configuration file is invalid or corrupted.\n\n" +
                        "Please contact your administrator for a valid configuration file.",
                        "Invalid Configuration",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return false;
                }

                string json;

                // 6. Decrypt configuration
                using (Aes aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using MemoryStream ms =
                        new MemoryStream(encryptedBytes);

                    using CryptoStream cs =
                        new CryptoStream(
                            ms,
                            aes.CreateDecryptor(),
                            CryptoStreamMode.Read);

                    using StreamReader reader =
                        new StreamReader(cs);

                    json = reader.ReadToEnd();
                }

                // 7. Parse JSON
                using JsonDocument document =JsonDocument.Parse(json);

                JsonElement root = document.RootElement;

                // 8. Get configured Machine ID
                if (!root.TryGetProperty(
                        "MachineId",
                        out JsonElement machineIdElement))
                {
                    MessageBox.Show(
                        "The configuration file is missing required machine information.\n\n" +
                        "Please contact your administrator for a valid configuration file.",
                        "Invalid Configuration",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return false;
                }

                string configuredMachineId =
                    machineIdElement.GetString()?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(configuredMachineId))
                {
                    MessageBox.Show(
                        "The configuration file does not contain valid machine information.\n\n" +
                        "Please contact your administrator for assistance.",
                        "Invalid Configuration",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return false;
                }

                // 9. Generate current machine ID
                string currentMachineId = GetMachineId();

                if (string.IsNullOrWhiteSpace(currentMachineId))
                {
                    MessageBox.Show(
                        "Unable to verify this computer.\n\n" +
                        "Please contact your administrator for assistance.",
                        "Machine Verification Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return false;
                }

                // 10. Compare Machine IDs
                if (!string.Equals(
                        configuredMachineId,
                        currentMachineId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(
                        "This configuration is not authorized for this computer.\n\n" +
                        "Please contact your administrator to obtain a configuration " +
                        "file assigned to this computer.",
                        "Machine Verification Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return false;
                }

                // Machine verified successfully
                return true;
            }
            catch (CryptographicException)
            {
                MessageBox.Show(
                    "The application configuration could not be verified.\n\n" +
                    "Please contact your administrator for a valid configuration file.",
                    "Configuration Verification Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return false;
            }
            //catch (JsonException)
            //{
            //    MessageBox.Show(
            //        "The application configuration is invalid or corrupted.\n\n" +
            //        "Please contact your administrator for a valid configuration file.",
            //        "Invalid Configuration",
            //        MessageBoxButton.OK,
            //        MessageBoxImage.Error);

            //    return false;
            //}
            catch
            {
                MessageBox.Show(
                    "The application could not verify its configuration.\n\n" +
                    "Please contact your administrator for assistance.",
                    "Configuration Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return false;
            }
        }

        private string GetMachineId()
        {
            try
            {
                string cpuId =
                    GetWmiValue("Win32_Processor", "ProcessorId");

                string biosSerial =
                    GetWmiValue("Win32_BIOS", "SerialNumber");

                string boardSerial =
                    GetWmiValue("Win32_BaseBoard", "SerialNumber");

                string systemUuid =
                    GetWmiValue("Win32_ComputerSystemProduct", "UUID");

                string diskSerial =
                    GetWmiValue("Win32_DiskDrive", "SerialNumber");

                // Normalize values
                cpuId = Normalize(cpuId);
                biosSerial = Normalize(biosSerial);
                boardSerial = Normalize(boardSerial);
                systemUuid = Normalize(systemUuid);
                diskSerial = Normalize(diskSerial);

                // Check if all hardware information is unavailable
                if (string.IsNullOrWhiteSpace(cpuId) &&
                    string.IsNullOrWhiteSpace(biosSerial) &&
                    string.IsNullOrWhiteSpace(boardSerial) &&
                    string.IsNullOrWhiteSpace(systemUuid) &&
                    string.IsNullOrWhiteSpace(diskSerial))
                {
                    return string.Empty;
                }

                // IMPORTANT:
                // This MUST be exactly the same in MachineInfo.
                string combined =
                    $"CPU:{cpuId}|" +
                    $"BIOS:{biosSerial}|" +
                    $"BOARD:{boardSerial}|" +
                    $"UUID:{systemUuid}|" +
                    $"DISK:{diskSerial}";

                using (SHA256 sha = SHA256.Create())
                {
                    byte[] hash =
                        sha.ComputeHash(
                            Encoding.UTF8.GetBytes(combined));

                    StringBuilder sb = new StringBuilder();

                    foreach (byte b in hash)
                    {
                        sb.Append(b.ToString("X2"));
                    }

                    return sb.ToString();
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        private string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Trim().ToUpperInvariant();
        }

        private string GetWmiValue(string className,string propertyName)
        {
            try
            {
                using (ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher(
                        $"SELECT {propertyName} FROM {className}"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        return obj[propertyName]?.ToString() ?? "";
                    }
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        */ private void Client_DataReceived(object sender, string data)
        {
            ProcessCode(data);
        }

        private void Client_Disconnected(object sender, EventArgs e)
        {

        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            System.Threading.Thread.Sleep(20);
            if (_serialPort == null) return;
            string data = _serialPort.ReadExisting();
            ProcessCode(data);
        }

        private void ProcessCode(string data)
        {
            try
            {
                if ((DateTime.Now - CodeReadTime).TotalMilliseconds > CodeReadGapInMS)
                {
                    // Read all available data                    
                    Dispatcher.Invoke(() =>
                    {
                        lblCode.Content = data;
                    });
                    if (data != null && !data.ToLower().Contains("error"))
                    {
                        CodeReadTime = DateTime.Now;
                        if (DeviceCOM.IsSystemBusy)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                lblCode.Content = "System is busy so you can not perform this command, please wait...";
                            });
                        }
                        else
                        {

                            if (DeviceCOM.IsBalanceRequired)
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    lblCode.Content = "Unable to test because of balance command is required!";
                                });
                            }
                            else
                            {
                                if (DeviceCOM.IsLogEnable)
                                {
                                    DeviceCOM.IsLogDisable = false;
                                    DeviceCOM.IsAutoEllipseActive = false;
                                    DeviceCOM.Code = data;
                                    BalanceTest balanceTest = new BalanceTest() { FC = 17, CN = 0 };

                                    bool rat = false;
                                    var IsJSON = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["IsJSON"]);

                                    if (portCOM != null)
                                    {
                                        if (IsJSON)
                                        {
                                            rat = portCOM.WriteData(JsonConvert.SerializeObject(balanceTest));
                                        }
                                        else
                                        {
                                            byte[] data1 = new byte[6];
                                            data1[0] = Convert.ToByte(2);
                                            data1[1] = Convert.ToByte(17);
                                            data1[2] = Convert.ToByte(1);
                                            data1[3] = Convert.ToByte(0);

                                            rat = portCOM.WriteDataInBytes(data1);
                                        }
                                    }

                                    if (!rat)
                                    {
                                        Dispatcher.Invoke(() =>
                                        {
                                            lblCode.Content = "Unable to start test due to the error in the communication!";
                                        });
                                    }
                                    else
                                    {
                                        if (DeviceCOM.IsBalanceRequired)
                                        {
                                            Dispatcher.Invoke(() =>
                                            {
                                                lblCode.Content = "Unable to start test because of balance command is required!";
                                            });
                                            DeviceCOM.IsBalanceRequired = false;
                                        }
                                        if (DeviceCOM.IsBinRequired)
                                        {
                                            Dispatcher.Invoke(() =>
                                            {
                                                lblCode.Content = "Please put the previous component to NG bin before starting the test!";
                                            });
                                            DeviceCOM.IsBinRequired = false;
                                        }
                                    }
                                }
                                else
                                {
                                    Dispatcher.Invoke(() =>
                                    {
                                        lblCode.Content = "Please start log before scan the QR code!";
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                //Console.WriteLine("Error reading data: " + ex.Message);
            }
        }

        void SetFrequencey()
        {

            if (FrequencyNo == 4 || FrequencyNo == 1 || modeApp == 1)
            {
                if (modeApp == 1)
                {
                    BoxSize4 = 670;
                    seqLength = BoxSize4;
                    FrequencyNo = 1;
                }

                Grid.SetRow(br3, 1);
                Grid.SetColumn(br3, 1);
                Grid.SetRow(br4, 1);
                Grid.SetColumn(br4, 2);

                buttonbar2.Visibility = Visibility.Visible;
                buttonbar1.Visibility = Visibility.Hidden;
                counterbar2.Visibility = Visibility.Visible;
                counterbar1.Visibility = Visibility.Hidden;

                menuHeight.Height = new GridLength(0.35, GridUnitType.Star);
                chennelHeight.Height = new GridLength(0.5, GridUnitType.Star);
                buttonBarWidth.Width = new GridLength(1, GridUnitType.Star);
                FrequencySpaceCol3.Width = new GridLength(0, GridUnitType.Star);
                FrequencySpaceCol4.Width = new GridLength(0, GridUnitType.Star);
                buttonBarHeight.Height = new GridLength(0, GridUnitType.Star);

                br5.Visibility = Visibility.Hidden;
                br6.Visibility = Visibility.Hidden;
                br7.Visibility = Visibility.Hidden;
                br8.Visibility = Visibility.Hidden;

                if (FrequencyNo == 1 || modeApp == 1)
                {
                    br2.Visibility = Visibility.Hidden;
                    br3.Visibility = Visibility.Hidden;
                    br4.Visibility = Visibility.Hidden;
                }

            }
        }

        public string Reverse(string Input)
        {

            // Converting string to character array 
            char[] charArray = Input.ToCharArray();

            // Declaring an empty string
            string reversedString = String.Empty;

            int length, index;
            length = charArray.Length - 1;
            index = length;

            // Iterating the each character from right to left  
            while (index > -1)
            {

                // Appending character to the reversedstring.
                reversedString = reversedString + charArray[index];
                index--;
            }

            // Return the reversed string.
            return reversedString;
        }

        private readonly _8F.Services.ILicensingService _licensingService = new _8F.Services.LicensingService();

        private void CheckSerailNumber()
        {
            var serial = portCOM?.GetSeialNumber();
            if (serial == null) return;
            string configSerial = System.Configuration.ConfigurationManager.AppSettings["SerialNumber"] ?? string.Empty;

            IsSerialmatch = _licensingService.ValidateSerialNumber(serial.S1, serial.S2, serial.S, configSerial);

            if (!IsSerialmatch)
            {
                MessageBox.Show("Serial number is mistmatch!", "System Information");
                this.Close();
            }
        }

        private void dispatcherTimer_Tick(object? sender, EventArgs e)
        {
            //if (!IsSerialmatch)
            //{
            //    CheckSerailNumber();
            //}

            if (DeviceCOM.IsSystemBusy)
            {
                brStatus.Background = new SolidColorBrush(Colors.Red);
                if (mode == 0)
                {
                    if (DeviceCOM.busyStamp.AddSeconds(30) < System.DateTime.Now)
                    {
                        DeviceCOM.IsSystemBusy = false;
                        lblCode.Content = "";
                    }
                }
            }
            else
            {
                brStatus.Background = new SolidColorBrush(Colors.Green);
            }

            if (DeviceCOM.IsResponseRefreshRequired)
            {
                RefreshResponse();

                var SChId = DeviceCOM.channelDatas.FirstOrDefault(c => c.IsSeleted)?.Id;

                var cnt = DeviceCOM.counter.FirstOrDefault(c => c.Id == SChId) ?? DeviceCOM.counter.FirstOrDefault(c => c.Id == 0);
                if (cnt != null)
                {
                    lblTCount.Content = "Total Count - " + cnt.ResultCount.ToString();
                    lblOkCount.Content = "OK Count - " + cnt.ResultOkCount.ToString();
                    lblNotOkCount.Content = "Not Ok Count - " + cnt.ResultOkNotCount.ToString();

                    lblTCount1.Content = "Total Count - " + cnt.ResultCount.ToString();
                    lblOkCount1.Content = "OK Count - " + cnt.ResultOkCount.ToString();
                    lblNotOkCount1.Content = "Not Ok Count - " + cnt.ResultOkNotCount.ToString();

                    lblTCount2.Content = "Total Count - " + cnt.ResultCount.ToString();
                    lblOkCount2.Content = "OK Count - " + cnt.ResultOkCount.ToString();
                    lblNotOkCount2.Content = "Not Ok Count - " + cnt.ResultOkNotCount.ToString();
                }

                DeviceCOM.IsResponseRefreshRequired = false;

                lblCode.Content = "";
            }

            if (DeviceCOM.IsResponseClearRequired)
            {
                //ClearGraphData();

                //foreach (var ch in DeviceCOM.channelDatas)
                //{
                //    var rData = "{\"FC\":20,\"CN\":1,\"OR\":0,\"FD\":[{\"FN\":1,\"R\":0,\"X\":0,\"Y\":0},{\"FN\":2,\"R\":0,\"X\":0,\"Y\":0},{\"FN\":3,\"R\":0,\"X\":0,\"Y\":0},{\"FN\":4,\"R\":0,\"X\":0,\"Y\":0},{\"FN\":5,\"R\":0,\"X\":0,\"Y\":0},{\"FN\":6,\"R\":0,\"X\":0,\"Y\":0},{\"FN\":7,\"R\":0,\"X\":0,\"Y\":0},{\"FN\":8,\"R\":0,\"X\":0,\"Y\":0}]}";
                //    var res = JsonConvert.DeserializeObject<Response>(rData);
                //    res.CN = ch.Id;
                //    res.IsBalacenced = true;
                //    DeviceCOM.responses.Add(res);
                //}

                var SChId = DeviceCOM.channelDatas.FirstOrDefault(c => c.IsSeleted)?.Id;
                if (DeviceCOM.IsBalanceAll)
                {
                    ClearGraphData();
                }
                else
                {
                    ClearGraphDataByChId(Convert.ToInt32(SChId));
                }

                foreach (var ch in DeviceCOM.channelDatas)
                {
                    if (DeviceCOM.IsBalanceAll || ch.IsSeleted)
                    {
                        var rData = "{\"FC\":20,\"CN\":1,\"OR\":0,\"FD\":[{\"FN\":1,\"R\":0,\"X\":0,\"Y\":0},{\"FN\":2,\"R\":0,\"X\":0,\"Y\":0},{\"FN\":3,\"R\":0,\"X\":0,\"Y\":0},{\"FN\":4,\"R\":0,\"X\":0,\"Y\":0},{\"FN\":5,\"R\":0,\"X\":0,\"Y\":0},{\"FN\":6,\"R\":0,\"X\":0,\"Y\":0},{\"FN\":7,\"R\":0,\"X\":0,\"Y\":0},{\"FN\":8,\"R\":0,\"X\":0,\"Y\":0}]}";
                        var res = JsonConvert.DeserializeObject<Response>(rData);
                        if (res != null)
                        {
                            res.CN = ch.Id;
                            res.IsBalacenced = true;
                            DeviceCOM.responses.Add(res);
                        }
                    }
                }

                DeviceCOM.IsResponseRefreshRequired = true;
                DeviceCOM.IsResponseClearRequired = false;

            }

            if (DeviceCOM.ERRCode == 16)
            {
                DeviceCOM.ERRCode = 0;
                MessageBox.Show("Balance Operation failed, please reboot the ECT Instrument.", "Error Information");
            }
            else if (DeviceCOM.ERRCode == 17)
            {
                DeviceCOM.ERRCode = 0;
                MessageBox.Show("Test failed, please reconfigure and rebalance the ECT Instrument.", "Error Information");
            }
            else if (DeviceCOM.ERRCode == 19)
            {
                DeviceCOM.ERRCode = 0;
                MessageBox.Show("Test failed, please reconfigure and rebalance the ECT Instrument.", "Error Information");
            }

            try
            {
                if (_serialPort != null && !_serialPort.IsOpen)
                {
                    _serialPort.Open();
                }
            }
            catch
            {

            }

        }

        public void InitialGraphData(bool IsPayLaod)
        {
            if (IsPayLaod)
            {
                ClearGraphData();

                br1_rec1.Height = seqLength;
                br1_rec1.Width = seqLength;
                Canvas.SetLeft(br1_can1, seqLength);
                br1_rec2.Width = seqLength;
                Canvas.SetTop(br1_rec2, seqLength / 2);
                br1_rec3.Height = seqLength;
                Canvas.SetLeft(br1_rec3, seqLength / 2);
                Canvas.SetLeft(rResult1, seqLength - 25);
                Canvas.SetTop(rResult1, seqLength - 25);
                Canvas.SetLeft(cnBr1, seqLength / 2);
                Canvas.SetTop(cnBr1, seqLength / 2);
                Canvas.SetTop(D1, seqLength - 25);

                br2_rec1.Height = seqLength;
                br2_rec1.Width = seqLength;
                Canvas.SetLeft(br2_can1, seqLength);
                br2_rec2.Width = seqLength;
                Canvas.SetTop(br2_rec2, seqLength / 2);
                br2_rec3.Height = seqLength;
                Canvas.SetLeft(br2_rec3, seqLength / 2);
                Canvas.SetLeft(rResult2, seqLength - 25);
                Canvas.SetTop(rResult2, seqLength - 25);
                Canvas.SetLeft(cnBr2, seqLength / 2);
                Canvas.SetTop(cnBr2, seqLength / 2);
                Canvas.SetTop(D2, seqLength - 25);

                br3_rec1.Height = seqLength;
                br3_rec1.Width = seqLength;
                Canvas.SetLeft(br3_can1, seqLength);
                br3_rec2.Width = seqLength;
                Canvas.SetTop(br3_rec2, seqLength / 2);
                br3_rec3.Height = seqLength;
                Canvas.SetLeft(br3_rec3, seqLength / 2);
                Canvas.SetLeft(rResult3, seqLength - 25);
                Canvas.SetTop(rResult3, seqLength - 25);
                Canvas.SetLeft(cnBr3, seqLength / 2);
                Canvas.SetTop(cnBr3, seqLength / 2);
                Canvas.SetTop(D3, seqLength - 25);

                br4_rec1.Height = seqLength;
                br4_rec1.Width = seqLength;
                Canvas.SetLeft(br4_can1, seqLength);
                br4_rec2.Width = seqLength;
                Canvas.SetTop(br4_rec2, seqLength / 2);
                br4_rec3.Height = seqLength;
                Canvas.SetLeft(br4_rec3, seqLength / 2);
                Canvas.SetLeft(rResult4, seqLength - 25);
                Canvas.SetTop(rResult4, seqLength - 25);
                Canvas.SetLeft(cnBr4, seqLength / 2);
                Canvas.SetTop(cnBr4, seqLength / 2);
                Canvas.SetTop(D4, seqLength - 25);

                br5_rec1.Height = seqLength;
                br5_rec1.Width = seqLength;
                Canvas.SetLeft(br5_can1, seqLength);
                br5_rec2.Width = seqLength;
                Canvas.SetTop(br5_rec2, seqLength / 2);
                br5_rec3.Height = seqLength;
                Canvas.SetLeft(br5_rec3, seqLength / 2);
                Canvas.SetLeft(rResult5, seqLength - 25);
                Canvas.SetTop(rResult5, seqLength - 25);
                Canvas.SetLeft(cnBr5, seqLength / 2);
                Canvas.SetTop(cnBr5, seqLength / 2);
                Canvas.SetTop(D5, seqLength - 25);

                br6_rec1.Height = seqLength;
                br6_rec1.Width = seqLength;
                Canvas.SetLeft(br6_can1, seqLength);
                br6_rec2.Width = seqLength;
                Canvas.SetTop(br6_rec2, seqLength / 2);
                br6_rec3.Height = seqLength;
                Canvas.SetLeft(br6_rec3, seqLength / 2);
                Canvas.SetLeft(rResult6, seqLength - 25);
                Canvas.SetTop(rResult6, seqLength - 25);
                Canvas.SetLeft(cnBr6, seqLength / 2);
                Canvas.SetTop(cnBr6, seqLength / 2);
                Canvas.SetTop(D6, seqLength - 25);

                br7_rec1.Height = seqLength;
                br7_rec1.Width = seqLength;
                Canvas.SetLeft(br7_can1, seqLength);
                br7_rec2.Width = seqLength;
                Canvas.SetTop(br7_rec2, seqLength / 2);
                br7_rec3.Height = seqLength;
                Canvas.SetLeft(br7_rec3, seqLength / 2);
                Canvas.SetLeft(rResult7, seqLength - 25);
                Canvas.SetTop(rResult7, seqLength - 25);
                Canvas.SetLeft(cnBr7, seqLength / 2);
                Canvas.SetTop(cnBr7, seqLength / 2);
                Canvas.SetTop(D7, seqLength - 25);

                br8_rec1.Height = seqLength;
                br8_rec1.Width = seqLength;
                Canvas.SetLeft(br8_can1, seqLength);
                br8_rec2.Width = seqLength;
                Canvas.SetTop(br8_rec2, seqLength / 2);
                br8_rec3.Height = seqLength;
                Canvas.SetLeft(br8_rec3, seqLength / 2);
                Canvas.SetLeft(rResult8, seqLength - 25);
                Canvas.SetTop(rResult8, seqLength - 25);
                Canvas.SetLeft(cnBr8, seqLength / 2);
                Canvas.SetTop(cnBr8, seqLength / 2);
                Canvas.SetTop(D8, seqLength - 25);

                for (int i = 10; i < seqLength; i = i + 10)
                {
                    Rectangle r1 = new Rectangle();
                    r1.Height = .2;
                    r1.Width = seqLength;
                    Canvas.SetLeft(r1, 0);
                    Canvas.SetTop(r1, i);
                    r1.Stroke = new SolidColorBrush(Colors.Black);
                    r1.Fill = new SolidColorBrush(Colors.LightGray);
                    Canvas1.Children.Add(r1);

                    Rectangle r2 = new Rectangle();
                    r2.Height = .2;
                    r2.Width = seqLength;
                    Canvas.SetLeft(r2, 0);
                    Canvas.SetTop(r2, i);
                    r2.Stroke = new SolidColorBrush(Colors.Black);
                    r2.Fill = new SolidColorBrush(Colors.LightGray);
                    Canvas2.Children.Add(r2);

                    Rectangle r3 = new Rectangle();
                    r3.Height = .2;
                    r3.Width = seqLength;
                    Canvas.SetLeft(r3, 0);
                    Canvas.SetTop(r3, i);
                    r3.Stroke = new SolidColorBrush(Colors.Black);
                    r3.Fill = new SolidColorBrush(Colors.LightGray);
                    Canvas3.Children.Add(r3);

                    Rectangle r4 = new Rectangle();
                    r4.Height = .2;
                    r4.Width = seqLength;
                    Canvas.SetLeft(r4, 0);
                    Canvas.SetTop(r4, i);
                    r4.Stroke = new SolidColorBrush(Colors.Black);
                    r4.Fill = new SolidColorBrush(Colors.LightGray);
                    Canvas4.Children.Add(r4);

                    Rectangle r5 = new Rectangle();
                    r5.Height = .2;
                    r5.Width = seqLength;
                    Canvas.SetLeft(r5, 0);
                    Canvas.SetTop(r5, i);
                    r5.Stroke = new SolidColorBrush(Colors.Black);
                    r5.Fill = new SolidColorBrush(Colors.LightGray);
                    Canvas5.Children.Add(r5);

                    Rectangle r6 = new Rectangle();
                    r6.Height = .2;
                    r6.Width = seqLength;
                    Canvas.SetLeft(r6, 0);
                    Canvas.SetTop(r6, i);
                    r6.Stroke = new SolidColorBrush(Colors.Black);
                    r6.Fill = new SolidColorBrush(Colors.LightGray);
                    Canvas6.Children.Add(r6);

                    Rectangle r7 = new Rectangle();
                    r7.Height = .2;
                    r7.Width = seqLength;
                    Canvas.SetLeft(r7, 0);
                    Canvas.SetTop(r7, i);
                    r7.Stroke = new SolidColorBrush(Colors.Black);
                    r7.Fill = new SolidColorBrush(Colors.LightGray);
                    Canvas7.Children.Add(r7);

                    Rectangle r8 = new Rectangle();
                    r8.Height = .2;
                    r8.Width = seqLength;
                    Canvas.SetLeft(r8, 0);
                    Canvas.SetTop(r8, i);
                    r8.Stroke = new SolidColorBrush(Colors.Black);
                    r8.Fill = new SolidColorBrush(Colors.LightGray);
                    Canvas8.Children.Add(r8);

                    Rectangle rectangle1 = new Rectangle();
                    rectangle1.Height = seqLength;
                    rectangle1.Width = .1;
                    Canvas.SetLeft(rectangle1, i);
                    Canvas.SetTop(rectangle1, 0);
                    rectangle1.Stroke = new SolidColorBrush(Colors.Black);
                    rectangle1.Fill = new SolidColorBrush(Colors.LightGray);


                    Rectangle rr1 = new Rectangle();
                    rr1.Height = seqLength;
                    rr1.Width = .2;
                    Canvas.SetLeft(rr1, i);
                    Canvas.SetTop(rr1, 0);
                    rr1.Stroke = new SolidColorBrush(Colors.Black);
                    rr1.Fill = new SolidColorBrush(Colors.LightGray);
                    Canvas1.Children.Add(rr1);

                    Rectangle rr2 = new Rectangle();
                    rr2.Height = seqLength;
                    rr2.Width = .2;
                    Canvas.SetLeft(rr2, i);
                    Canvas.SetTop(rr2, 0);
                    rr2.Stroke = new SolidColorBrush(Colors.Black);
                    rr2.Fill = new SolidColorBrush(Colors.LightGray);
                    Canvas2.Children.Add(rr2);

                    Rectangle rr3 = new Rectangle();
                    rr3.Height = seqLength;
                    rr3.Width = .2;
                    Canvas.SetLeft(rr3, i);
                    Canvas.SetTop(rr3, 0);
                    rr3.Stroke = new SolidColorBrush(Colors.Black);
                    rr3.Fill = new SolidColorBrush(Colors.LightGray);
                    Canvas3.Children.Add(rr3);

                    Rectangle rr4 = new Rectangle();
                    rr4.Height = seqLength;
                    rr4.Width = .2;
                    Canvas.SetLeft(rr4, i);
                    Canvas.SetTop(rr4, 0);
                    rr4.Stroke = new SolidColorBrush(Colors.Black);
                    rr4.Fill = new SolidColorBrush(Colors.LightGray);
                    Canvas4.Children.Add(rr4);

                    Rectangle rr5 = new Rectangle();
                    rr5.Height = seqLength;
                    rr5.Width = .2;
                    Canvas.SetLeft(rr5, i);
                    Canvas.SetTop(rr5, 0);
                    rr5.Stroke = new SolidColorBrush(Colors.Black);
                    rr5.Fill = new SolidColorBrush(Colors.LightGray);
                    Canvas5.Children.Add(rr5);

                    Rectangle rr6 = new Rectangle();
                    rr6.Height = seqLength;
                    rr6.Width = .2;
                    Canvas.SetLeft(rr6, i);
                    Canvas.SetTop(rr6, 0);
                    rr6.Stroke = new SolidColorBrush(Colors.Black);
                    rr6.Fill = new SolidColorBrush(Colors.LightGray);
                    Canvas6.Children.Add(rr6);

                    Rectangle rr7 = new Rectangle();
                    rr7.Height = seqLength;
                    rr7.Width = .2;
                    Canvas.SetLeft(rr7, i);
                    Canvas.SetTop(rr7, 0);
                    rr7.Stroke = new SolidColorBrush(Colors.Black);
                    rr7.Fill = new SolidColorBrush(Colors.LightGray);
                    Canvas7.Children.Add(rr7);

                    Rectangle rr8 = new Rectangle();
                    rr8.Height = seqLength;
                    rr8.Width = .2;
                    Canvas.SetLeft(rr8, i);
                    Canvas.SetTop(rr8, 0);
                    rr8.Stroke = new SolidColorBrush(Colors.Black);
                    rr8.Fill = new SolidColorBrush(Colors.LightGray);
                    Canvas8.Children.Add(rr8);
                }
            }
            DeviceCOM.channelDatas = new List<ChannelData>();

            ChannelData channelData = new ChannelData();
            channelData.Id = 1;
            channelData.IsSeleted = true;
            channelData.graphDatas = IniGdata();
            DeviceCOM.channelDatas.Add(channelData);

            ChannelData channelData1 = new ChannelData();
            channelData1.Id = 2;
            channelData1.graphDatas = IniGdata();
            DeviceCOM.channelDatas.Add(channelData1);

            ChannelData channelData2 = new ChannelData();
            channelData2.Id = 3;
            channelData2.graphDatas = IniGdata();
            DeviceCOM.channelDatas.Add(channelData2);

            ChannelData channelData3 = new ChannelData();
            channelData3.Id = 4;
            channelData3.graphDatas = IniGdata();
            DeviceCOM.channelDatas.Add(channelData3);

            btnCh1.Background = new SolidColorBrush(Colors.DarkGray);
            btnCh2.Background = new SolidColorBrush(Colors.DarkGray);
            btnCh3.Background = new SolidColorBrush(Colors.DarkGray);
            btnCh4.Background = new SolidColorBrush(Colors.DarkGray);

            btnCh1.Background = new SolidColorBrush(Colors.Green);
        }

        public List<GraphData> IniGdata()
        {
            List<GraphData> graphDatas = new List<GraphData>();

            GraphData graphD1 = new GraphData();
            graphD1.Id = 1;
            graphD1.Name = "D1";
            Ellips elliplse1 = new Ellips();
            elliplse1.Id = 1;
            graphD1.ellipses.Add(elliplse1);
            graphDatas.Add(graphD1);

            GraphData graphD2 = new GraphData();
            graphD2.Id = 2;
            graphD2.Name = "D2";
            Ellips elliplse2 = new Ellips();
            elliplse2.Id = 1;
            graphD2.ellipses.Add(elliplse2);
            graphDatas.Add(graphD2);

            GraphData graphD3 = new GraphData();
            graphD3.Id = 3;
            graphD3.Name = "D3";
            Ellips elliplse3 = new Ellips();
            elliplse3.Id = 1;
            graphD3.ellipses.Add(elliplse3);
            graphDatas.Add(graphD3);

            GraphData graphD4 = new GraphData();
            graphD4.Id = 4;
            graphD4.Name = "D4";
            Ellips elliplse4 = new Ellips();
            elliplse4.Id = 1;
            graphD4.ellipses.Add(elliplse4);
            graphDatas.Add(graphD4);

            GraphData graphD5 = new GraphData();
            graphD5.Id = 5;
            graphD5.Name = "D5";
            Ellips elliplse5 = new Ellips();
            elliplse5.Id = 1;
            graphD5.ellipses.Add(elliplse5);
            graphDatas.Add(graphD5);

            GraphData graphD6 = new GraphData();
            graphD6.Id = 6;
            graphD6.Name = "D6";
            Ellips elliplse6 = new Ellips();
            elliplse6.Id = 1;
            graphD6.ellipses.Add(elliplse6);
            graphDatas.Add(graphD6);

            GraphData graphD7 = new GraphData();
            graphD7.Id = 7;
            graphD7.Name = "D7";
            Ellips elliplse7 = new Ellips();
            elliplse7.Id = 1;
            graphD7.ellipses.Add(elliplse7);
            graphDatas.Add(graphD7);

            GraphData graphD8 = new GraphData();
            graphD8.Id = 8;
            graphD8.Name = "D8";
            Ellips elliplse8 = new Ellips();
            elliplse8.Id = 1;
            graphD8.ellipses.Add(elliplse8);
            graphDatas.Add(graphD8);

            return graphDatas;
        }

        public bool ImplementChanges(int ChangeType)
        {
            if (portCOM == null) return false;
            var rat = false;
            if (ChangeType == 0)
            {
                FrequencyCount frequencyCount = new FrequencyCount() { FC = 1, C = FrequencyNo, NC = chNo };
                Mode _mode = new Mode() { FC = 2, M = 0 };

                var IsJSON = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["IsJSON"]);
                if (IsJSON)
                {
                    portCOM.WriteData(JsonConvert.SerializeObject(frequencyCount));
                    portCOM.WriteData(JsonConvert.SerializeObject(_mode));
                }
                else
                {
                    byte[] data = new byte[7];
                    data[0] = Convert.ToByte(2);
                    data[1] = Convert.ToByte(1);
                    data[2] = Convert.ToByte(2);
                    data[3] = Convert.ToByte(chNo);
                    data[4] = Convert.ToByte(FrequencyNo);

                    portCOM.WriteDataInBytes(data);

                    byte[] data1 = new byte[6];
                    data1[0] = Convert.ToByte(2);
                    data1[1] = Convert.ToByte(2);
                    data1[2] = Convert.ToByte(1);
                    data1[3] = Convert.ToByte(0);

                    portCOM.WriteDataInBytes(data1);
                }

            }

            foreach (var ch in DeviceCOM.channelDatas)
            {
                if (ch.Id <= chNo)
                {
                    FrequencyWrite frequencyWrite = new FrequencyWrite();
                    frequencyWrite.FC = 4;
                    frequencyWrite.CN = ch.Id;
                    frequencyWrite.FD = new List<Frequency>();

                    ElliplseWrite ellipseWrite = new ElliplseWrite();
                    ellipseWrite.FC = 5;
                    ellipseWrite.CN = ch.Id;
                    ellipseWrite.FD = new List<Frequ>();

                    foreach (GraphData graphData in ch.graphDatas)
                    {
                        if (ch.IsSeleted == true)
                        {
                            // Gdata.isEnable enable/disable the frequency graph 

                            if (graphData.Id == 1)
                            {
                                if (graphData.isEnable)
                                {
                                    lblFreq1.Text = graphData.Name + "-" + graphData.freq + "Hz";

                                    AddEllipses(cnBr1, graphData);

                                    br1_rec1.Fill = enableColor;
                                    D1.IsEnabled = true;
                                    br1.IsEnabled = true;
                                }
                                else
                                {
                                    br1_rec1.Fill = disableColor;
                                    D1.IsEnabled = false;
                                    br1.IsEnabled = false;
                                }

                            }
                            else if (graphData.Id == 2 && graphData.Id <= FrequencyNo)
                            {
                                if (graphData.isEnable)
                                {
                                    lblFreq2.Text = graphData.Name + "-" + graphData.freq + "Hz";

                                    AddEllipses(cnBr2, graphData);

                                    br2_rec1.Fill = enableColor;
                                    D2.IsEnabled = true;
                                    br2.IsEnabled = true;
                                }
                                else
                                {
                                    br2_rec1.Fill = disableColor;
                                    D2.IsEnabled = false;
                                    br2.IsEnabled = false;
                                }
                            }
                            else if (graphData.Id == 3 && graphData.Id <= FrequencyNo)
                            {
                                if (graphData.isEnable)
                                {
                                    lblFreq3.Text = graphData.Name + "-" + graphData.freq + "Hz";

                                    AddEllipses(cnBr3, graphData);

                                    br3_rec1.Fill = enableColor;
                                    D3.IsEnabled = true;
                                    br3.IsEnabled = true;
                                }
                                else
                                {
                                    br3_rec1.Fill = disableColor;
                                    D3.IsEnabled = false;
                                    br3.IsEnabled = false;
                                }
                            }
                            else if (graphData.Id == 4 && graphData.Id <= FrequencyNo)
                            {
                                if (graphData.isEnable)
                                {
                                    lblFreq4.Text = graphData.Name + "-" + graphData.freq + "Hz";

                                    AddEllipses(cnBr4, graphData);

                                    br4_rec1.Fill = enableColor;
                                    D4.IsEnabled = true;
                                    br4.IsEnabled = true;
                                }
                                else
                                {
                                    br4_rec1.Fill = disableColor;
                                    D4.IsEnabled = false;
                                    br4.IsEnabled = false;
                                }
                            }
                            else if (graphData.Id == 5 && graphData.Id <= FrequencyNo)
                            {
                                if (graphData.isEnable)
                                {

                                    lblFreq5.Text = graphData.Name + "-" + graphData.freq + "Hz";

                                    AddEllipses(cnBr5, graphData);

                                    br5_rec1.Fill = enableColor;
                                    D5.IsEnabled = true;
                                    br5.IsEnabled = true;
                                }
                                else
                                {
                                    br5_rec1.Fill = disableColor;
                                    D5.IsEnabled = false;
                                    br5.IsEnabled = false;
                                }
                            }
                            else if (graphData.Id == 6 && graphData.Id <= FrequencyNo)
                            {
                                if (graphData.isEnable)
                                {
                                    lblFreq6.Text = graphData.Name + "-" + graphData.freq + "Hz";

                                    AddEllipses(cnBr6, graphData);

                                    br6_rec1.Fill = enableColor;
                                    D6.IsEnabled = true;
                                    br6.IsEnabled = true;
                                }
                                else
                                {
                                    br6_rec1.Fill = disableColor;
                                    D6.IsEnabled = false;
                                    br6.IsEnabled = false;
                                }
                            }
                            else if (graphData.Id == 7 && graphData.Id <= FrequencyNo)
                            {
                                if (graphData.isEnable)
                                {
                                    lblFreq7.Text = graphData.Name + "-" + graphData.freq + "Hz";

                                    AddEllipses(cnBr7, graphData);

                                    br7_rec1.Fill = enableColor;
                                    D7.IsEnabled = true;
                                    br7.IsEnabled = true;
                                }
                                else
                                {
                                    br7_rec1.Fill = disableColor;
                                    D7.IsEnabled = false;
                                    br7.IsEnabled = false;
                                }
                            }
                            else if (graphData.Id == 8 && graphData.Id <= FrequencyNo)
                            {
                                if (graphData.isEnable)
                                {
                                    lblFreq8.Text = graphData.Name + "-" + graphData.freq + "Hz";

                                    AddEllipses(cnBr8, graphData);

                                    br8_rec1.Fill = enableColor;
                                    D8.IsEnabled = true;
                                    br8.IsEnabled = true;
                                }
                                else
                                {
                                    br8_rec1.Fill = disableColor;
                                    D8.IsEnabled = false;
                                    br8.IsEnabled = false;
                                }
                            }
                        }

                        if (ChangeType == 0 && graphData.Id <= FrequencyNo)
                        {
                            // write data to port for freq and setting
                            Frequency frequency = new Frequency() { FN = graphData.Id, F = graphData.freq, G = graphData.gain, P = graphData.phase, E = graphData.isEnable ? 1 : 0 };
                            Frequ frequ = new Frequ() { FN = graphData.Id, ED = new List<Elliplse>() };
                            foreach (var el in graphData.ellipses)
                            {
                                Elliplse elliplse = new Elliplse() { FN = graphData.Id, EId = el.Id, a = el.height, b = el.width, t = el.angel, x = (int)Math.Round(el.ex, MidpointRounding.AwayFromZero), y = (int)Math.Round(el.ey, MidpointRounding.AwayFromZero) };
                                frequ.ED.Add(elliplse);
                            }
                            frequencyWrite.FD.Add(frequency);

                            ellipseWrite.FD.Add(frequ);
                        }
                    }

                    if (ChangeType == 0)
                    {

                        bool rat1 = false;
                        bool rat2 = false;

                        var IsJSON = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["IsJSON"]);
                        if (IsJSON)
                        {
                            rat1 = portCOM.WriteData(JsonConvert.SerializeObject(frequencyWrite));
                            System.Threading.Thread.Sleep(500);
                            rat2 = portCOM.WriteData(JsonConvert.SerializeObject(ellipseWrite));
                        }
                        else
                        {
                            //int length = (frequencyWrite.FD.Count * 10) + 6;
                            int length = (frequencyWrite.FD.Count * 10) + (isTxStrengthEnabled ? 7 : 6);
                            byte[] data = new byte[length];
                            data[0] = Convert.ToByte(2);
                            data[1] = Convert.ToByte(4);
                            //data[2] = Convert.ToByte((frequencyWrite.FD.Count * 10) + 1);
                            data[2] = Convert.ToByte((frequencyWrite.FD.Count * 10) + (isTxStrengthEnabled ? 2 : 1));
                            data[3] = Convert.ToByte(ch.Id);
                            int startB = 4;
                            foreach (var kvp in frequencyWrite.FD)
                            {
                                data[startB] = Convert.ToByte(kvp.FN);

                                data[startB + 1] = (byte)(kvp.F & 0xFF);         // Lowest byte
                                data[startB + 2] = (byte)((kvp.F >> 8) & 0xFF);  // Byte 2
                                data[startB + 3] = (byte)((kvp.F >> 16) & 0xFF); // Byte 3
                                data[startB + 4] = (byte)((kvp.F >> 24) & 0xFF); // Highest byte

                                data[startB + 5] = (byte)(kvp.G & 0xFF);         // Lowest byte
                                data[startB + 6] = (byte)((kvp.G >> 8) & 0xFF);  // Byte 2

                                data[startB + 7] = (byte)(kvp.P & 0xFF);         // Lowest byte
                                data[startB + 8] = (byte)((kvp.P >> 8) & 0xFF);  // Byte 2

                                data[startB + 9] = (byte)(kvp.E);


                                startB = startB + 10;
                            }

                            if (isTxStrengthEnabled)
                            {
                                data[startB] = (byte)frequencyWrite.T;
                            }

                            rat1 = portCOM.WriteDataInBytes(data);
                            System.Threading.Thread.Sleep(500);


                            int length1 = (ellipseWrite.FD.Count * 11) + 6;
                            byte[] data1 = new byte[length1];
                            data1[0] = Convert.ToByte(2);
                            data1[1] = Convert.ToByte(5);
                            data1[2] = Convert.ToByte((ellipseWrite.FD.Count * 11) + 1);
                            data1[3] = Convert.ToByte(ch.Id);
                            int start1B = 4;

                            foreach (var kvp in ellipseWrite.FD)
                            {
                                data1[start1B] = Convert.ToByte(kvp.FN);

                                data1[start1B + 1] = (byte)(Convert.ToInt16(kvp.ED[0].a) & 0xFF);         // Lowest byte
                                data1[start1B + 2] = (byte)((Convert.ToInt16(kvp.ED[0].a) >> 8) & 0xFF);  // Byte 2

                                data1[start1B + 3] = (byte)(Convert.ToInt16(kvp.ED[0].b) & 0xFF);         // Lowest byte
                                data1[start1B + 4] = (byte)((Convert.ToInt16(kvp.ED[0].b) >> 8) & 0xFF);  // Byte 2


                                data1[start1B + 5] = (byte)(Convert.ToInt16(kvp.ED[0].t) & 0xFF);         // Lowest byte
                                data1[start1B + 6] = (byte)((Convert.ToInt16(kvp.ED[0].t) >> 8) & 0xFF);  // Byte 2

                                data1[start1B + 7] = (byte)(Convert.ToInt16(kvp.ED[0].x) & 0xFF);         // Lowest byte
                                data1[start1B + 8] = (byte)((Convert.ToInt16(kvp.ED[0].x) >> 8) & 0xFF);  // Byte 2

                                data1[start1B + 9] = (byte)(Convert.ToInt16(kvp.ED[0].y) & 0xFF);         // Lowest byte
                                data1[start1B + 10] = (byte)((Convert.ToInt16(kvp.ED[0].y) >> 8) & 0xFF);  // Byte 2

                                start1B = start1B + 11;
                            }

                            rat2 = portCOM.WriteDataInBytes(data1);
                        }

                        rat = rat1 && rat2;
                    }
                }
            }

            return rat;
        }

        public void AddEllipses(Canvas cnBr1, GraphData graphData)
        {
            // cnBr1

            //cnBr1.Children.Clear();

            for (var i = 1; i < cnBr1.Children.Count;)
            {
                cnBr1.Children.RemoveAt(1);
            }


            if (modeApp == 1)
            {
                el11.Visibility = Visibility.Visible;
                el11.Height = graphData.height_O / factor;
                el11.Width = graphData.width_O / factor;
                tt11.X = ((graphData.ex_O - (graphData.width_O / 2)) / factor);
                tt11.Y = (((graphData.ey_O * -1) - (graphData.height_O / 2)) / factor);
                el11.Stroke = new SolidColorBrush(Colors.DarkOrange);
                rtAngel11.CenterX = (el11.Width / 2);
                rtAngel11.CenterY = (el11.Height / 2);
                rtAngel11.Angle = graphData.angel_O * -1;

                Ellipse el1_1 = new Ellipse();
                el1_1.Height = graphData.height_O / factor;
                el1_1.Width = graphData.width_O / factor;
                el1_1.HorizontalAlignment = HorizontalAlignment.Center;
                el1_1.Stroke = new SolidColorBrush(Colors.DarkOrange);
                el1_1.VerticalAlignment = VerticalAlignment.Center;
                Canvas.SetLeft(el1_1, 0);
                Canvas.SetTop(el1_1, 0);
                el1_1.RenderTransformOrigin = new Point(0, 0);

                TranslateTransform tt1_1 = new TranslateTransform();
                tt1_1.X = ((graphData.ex_O - (graphData.width_O / 2)) / factor);
                tt1_1.Y = (((graphData.ey_O * -1) - (graphData.height_O / 2)) / factor);

                RotateTransform rtAngel1_1 = new RotateTransform();
                rtAngel1_1.CenterX = (el1_1.Width / 2);
                rtAngel1_1.CenterY = (el1_1.Height / 2);
                rtAngel1_1.Angle = graphData.angel_O * -1;

                TransformGroup transformGroup_1 = new TransformGroup();
                transformGroup_1.Children.Add(rtAngel1_1);
                transformGroup_1.Children.Add(tt1_1);

                el1_1.RenderTransform = transformGroup_1;
                cnBr1.Children.Add(el1_1);
            }



            foreach (var item in graphData.ellipses)
            {
                var index = graphData.ellipses.IndexOf(item);
                Ellipse el1 = new Ellipse() { Fill = Brushes.Transparent };
                el1.Height = item.height / factor;
                el1.Width = item.width / factor;
                el1.HorizontalAlignment = HorizontalAlignment.Center;
                el1.Stroke = new SolidColorBrush(MyColor.GetColor(index));
                el1.VerticalAlignment = VerticalAlignment.Center;
                Canvas.SetLeft(el1, 0);
                Canvas.SetTop(el1, 0);
                el1.RenderTransformOrigin = new Point(0, 0);

                TranslateTransform tt1 = new TranslateTransform();
                tt1.X = ((item.ex - (item.width / 2)) / factor);
                tt1.Y = (((item.ey * -1) - (item.height / 2)) / factor);

                RotateTransform rtAngel1 = new RotateTransform();
                rtAngel1.CenterX = (el1.Width / 2);
                rtAngel1.CenterY = (el1.Height / 2);
                rtAngel1.Angle = item.angel * -1;

                TransformGroup transformGroup = new TransformGroup();
                transformGroup.Children.Add(rtAngel1);
                transformGroup.Children.Add(tt1);

                if (graphData.ellipses.Count == 1)
                {
                    el1.MouseLeftButtonDown += Ellipse_MouseLeftButtonDown;
                    el1.MouseLeftButtonUp += Ellipse_MouseLeftButtonUp;
                    el1.MouseMove += Ellipse_MouseMove;
                    el1.DataContext = graphData.Id;
                }

                el1.RenderTransform = transformGroup;
                cnBr1.Children.Add(el1);
            }

        }

        private bool isDragging = false;
        private Point mouseStart;
        private TranslateTransform? dragTransform = null;



        private void Ellipse_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var ellipse = sender as Ellipse;
            if (ellipse == null) return;

            isDragging = true;
            mouseStart = e.GetPosition(cnBr1);
            ellipse.CaptureMouse();

            // find the TranslateTransform inside RenderTransform
            TransformGroup? tg = ellipse.RenderTransform as TransformGroup;
            dragTransform = tg?.Children.OfType<TranslateTransform>().FirstOrDefault();


        }

        private void Ellipse_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            (sender as UIElement)?.ReleaseMouseCapture();
            dragTransform = null;


        }


        private void Ellipse_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isDragging || dragTransform == null)
                return;

            Point currentPos = e.GetPosition(cnBr1);

            double dx = currentPos.X - mouseStart.X;
            double dy = currentPos.Y - mouseStart.Y;

            dragTransform.X += dx;
            dragTransform.Y += dy;

            mouseStart = currentPos;

            int FreqId = Convert.ToInt32(((Ellipse)sender).DataContext);

            DeviceCOM.channelDatas[chNo - 1].graphDatas[FreqId - 1].ellipses[0].ex = (dragTransform.X * factor) + DeviceCOM.channelDatas[chNo - 1].graphDatas[FreqId - 1].ellipses[0].width / 2;

            DeviceCOM.channelDatas[chNo - 1].graphDatas[FreqId - 1].ellipses[0].ey = (-1) * ((dragTransform.Y * factor) + DeviceCOM.channelDatas[chNo - 1].graphDatas[FreqId - 1].ellipses[0].height / 2);


        }


        private void D_Click(object sender, RoutedEventArgs e)
        {
            ellipsesPop = new CircleSetting(((Border)sender).Name);
            ellipsesPop.Closing += ellipsesPop_Closing;
            ellipsesPop.portCOM = portCOM;
            ellipsesPop.Owner = this;
            ellipsesPop.ShowDialog();
        }

        private void ellipsesPop_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (ellipsesPop != null && ellipsesPop.IsSaved)
            {
                ImplementChanges(2);
            }
        }

        public void SelectCh1()
        {


            var currentChannel = DeviceCOM.channelDatas.FirstOrDefault(c => c.IsSeleted == true);
            if (currentChannel?.Id != 1)
            {
                if (currentChannel != null) currentChannel.IsSeleted = false;
                var nextCh = DeviceCOM.channelDatas.FirstOrDefault(c => c.Id == 1);
                if (nextCh != null) nextCh.IsSeleted = true;
                btnCh1.Background = new SolidColorBrush(Colors.DarkGray);
                btnCh2.Background = new SolidColorBrush(Colors.DarkGray);
                btnCh3.Background = new SolidColorBrush(Colors.DarkGray);
                btnCh4.Background = new SolidColorBrush(Colors.DarkGray);

                btnCh1.Background = new SolidColorBrush(Colors.Green);

            }
        }

        private void btnCh_Click(object sender, RoutedEventArgs e)
        {
            var chId = Convert.ToUInt32(((Border)sender).Tag);
            var currentChannel = DeviceCOM.channelDatas.FirstOrDefault(c => c.IsSeleted == true);
            if (currentChannel?.Id != chId)
            {
                if (currentChannel != null) currentChannel.IsSeleted = false;
                var nextCh = DeviceCOM.channelDatas.FirstOrDefault(c => c.Id == chId);
                if (nextCh != null) nextCh.IsSeleted = true;
                btnCh1.Background = new SolidColorBrush(Colors.DarkGray);
                btnCh2.Background = new SolidColorBrush(Colors.DarkGray);
                btnCh3.Background = new SolidColorBrush(Colors.DarkGray);
                btnCh4.Background = new SolidColorBrush(Colors.DarkGray);
                ((Border)sender).Background = new SolidColorBrush(Colors.Green);
                ImplementChanges(1);
                DeviceCOM.IsResponseRefreshRequired = true;
            }
        }

        private void btnBalance_Click(object sender, RoutedEventArgs e)
        {
            if (portCOM == null) return;
            if (DeviceCOM.IsSystemBusy)
            {
                MessageBox.Show("System is busy so you can not perform this command, please wait...", "System Information");
            }
            else
            {

                var IsBalaneAll = (((Border)sender).Name == "btnBalance1All") || (((Border)sender).Name == "btnBalanceAll") || (((Border)sender).Name == "btnBalance2All");
                var SChId = DeviceCOM.channelDatas.FirstOrDefault(c => c.IsSeleted)?.Id;
                int ChId = IsBalaneAll ? 0 : Convert.ToInt32(SChId);
                BalanceTest balanceTest = new BalanceTest() { FC = 16, CN = ChId };

                bool rat = false;
                var IsJSON = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["IsJSON"]);
                if (IsJSON)
                {
                    rat = portCOM.WriteData(JsonConvert.SerializeObject(balanceTest));
                }
                else
                {
                    byte[] data = new byte[7];
                    data[0] = Convert.ToByte(2);
                    data[1] = Convert.ToByte(16);
                    data[2] = Convert.ToByte(2);
                    data[3] = Convert.ToByte(ChId);
                    data[4] = DeviceCOM.IsLogRequiredOnBalance ? (DeviceCOM.IsLogEnable ? Convert.ToByte(1) : Convert.ToByte(2)) : Convert.ToByte(0);

                    rat = portCOM.WriteDataInBytes(data);
                }

                if (rat)
                {
                    DeviceCOM.IsBalanceAll = IsBalaneAll;
                    DeviceCOM.IsBalanceBusyEnable = true;
                }
                else
                {
                    MessageBox.Show("Unable to balance due to the error in the communication!", "Error Information");
                }
            }

            lblCode.Content = "";

        }

        private void btnTest_Click(object sender, RoutedEventArgs e)
        {
            if (portCOM == null) return;
            if (DeviceCOM.IsSystemBusy)
            {
                MessageBox.Show("System is busy so you can not perform this command, please wait...", "System Information");

            }
            else
            {
                var IsTestAll = (((Border)sender).Name == "btnTest1All") || (((Border)sender).Name == "btnTestAll") || (((Border)sender).Name == "btnTest2All");
                var SChId = DeviceCOM.channelDatas.FirstOrDefault(c => c.IsSeleted)?.Id;
                int ChId = IsTestAll ? 0 : Convert.ToInt32(SChId);
                DeviceCOM.IsAutoEllipseActive = false;

                BalanceTest balanceTest = new BalanceTest() { FC = 17, CN = ChId };

                bool rat = false;
                var IsJSON = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["IsJSON"]);

                if (IsJSON)
                {
                    rat = portCOM.WriteData(JsonConvert.SerializeObject(balanceTest));
                }
                else
                {
                    byte[] data = new byte[6];
                    data[0] = Convert.ToByte(2);
                    data[1] = Convert.ToByte(17);
                    data[2] = Convert.ToByte(1);
                    data[3] = Convert.ToByte(ChId);

                    rat = portCOM.WriteDataInBytes(data);
                }

                if (!rat)
                {
                    MessageBox.Show("Unable to start test due to the error in the communication!", "Error Information");
                }
                else
                {
                    if (DeviceCOM.IsBalanceRequired)
                    {
                        MessageBox.Show("Unable to test because of balance command is required!", "Error Information");
                        DeviceCOM.IsBalanceRequired = false;
                    }
                    if (DeviceCOM.IsBinRequired)
                    {
                        MessageBox.Show("Please put the previous component to NG bin before starting the test!", "Error Information");
                        DeviceCOM.IsBinRequired = false;
                    }

                    if (!DeviceCOM.IsBalanceRequired && !DeviceCOM.IsBinRequired && isTestLogOff)
                    {
                        DeviceCOM.IsLogDisable = true;
                    }
                }
            }

            lblCode.Content = "";
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            var IsClearAll = (((Border)sender).Name == "btnClear1All") || (((Border)sender).Name == "btnClearAll") || (((Border)sender).Name == "btnClear2All");
            ClearGraphDataWithoutBalance(IsClearAll);
            lblCode.Content = "";
        }

        private void btnResetCounter_Click(object sender, MouseButtonEventArgs e)
        {
            foreach (var cnt in DeviceCOM.counter)
            {
                cnt.ResultCount = 0;
                cnt.ResultOkCount = 0;
                cnt.ResultOkNotCount = 0;
            }

            lblTCount.Content = "Total Count - 0";
            lblOkCount.Content = " OK Count - 0";
            lblNotOkCount.Content = "Not Ok Count - 0";

            lblTCount1.Content = "Total Count - 0";
            lblOkCount1.Content = "OK Count - 0";
            lblNotOkCount1.Content = "Not Ok Count - 0";

            lblTCount2.Content = "Total Count - 0";
            lblOkCount2.Content = "OK Count - 0";
            lblNotOkCount2.Content = "Not Ok Count - 0";

            lblCode.Content = "";
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (isModbusServerEnable && modbusSlaveService != null)
            {
                modbusSlaveService.Stop();
            }

            if (portCOM == null) return;
            if (CommunicationType == 0)
            {
                Status exitData = new Status() { FC = 24 };

                bool rat = false;
                var IsJSON = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["IsJSON"]);

                if (IsJSON)
                {
                    rat = portCOM.WriteData(JsonConvert.SerializeObject(exitData));
                }
                else
                {
                    byte[] data = new byte[5];
                    data[0] = Convert.ToByte(2);
                    data[1] = Convert.ToByte(24);
                    data[2] = Convert.ToByte(0);

                    rat = portCOM.WriteDataInBytes(data);
                }

                if (portCOM.port.IsOpen)
                    portCOM.port.Close();
            }
        }
        public void ClearGraphDataWithoutBalance(bool IsClearAll)
        {
            if (IsClearAll)
            {
                var balaceData = DeviceCOM.responses.Where(r => r.IsBalacenced).ToList();
                ClearGraphData();
                if (balaceData.Count > 0)
                {
                    DeviceCOM.responses.AddRange(balaceData);
                }
            }
            else
            {
                var SChId = DeviceCOM.channelDatas.FirstOrDefault(c => c.IsSeleted)?.Id;
                var balaceData = DeviceCOM.responses.Where(r => r.IsBalacenced && r.CN == SChId).ToList();
                ClearGraphDataByChId(Convert.ToInt32(SChId));
                if (balaceData.Count > 0)
                {
                    DeviceCOM.responses.AddRange(balaceData);
                }
            }
            DeviceCOM.IsResponseRefreshRequired = true;
        }
        public void ClearGraphData(bool IsDataClear = true)
        {
            if (IsDataClear)
            {
                DeviceCOM.responses = new List<Response>();
            }
            cn1.Children.Clear();
            rResult1.Fill = new SolidColorBrush(Colors.White);

            cn2.Children.Clear();
            rResult2.Fill = new SolidColorBrush(Colors.White);

            cn3.Children.Clear();
            rResult3.Fill = new SolidColorBrush(Colors.White);

            cn4.Children.Clear();
            rResult4.Fill = new SolidColorBrush(Colors.White);

            cn5.Children.Clear();
            rResult5.Fill = new SolidColorBrush(Colors.White);

            cn6.Children.Clear();
            rResult6.Fill = new SolidColorBrush(Colors.White);

            cn7.Children.Clear();
            rResult7.Fill = new SolidColorBrush(Colors.White);

            cn8.Children.Clear();
            rResult8.Fill = new SolidColorBrush(Colors.White);

            btnOverallResult.Background = new SolidColorBrush(Colors.LightBlue);
            btnOverallResult1.Background = new SolidColorBrush(Colors.LightBlue);

            lblGraphXY1.Text = "";
            lblGraphXY2.Text = "";
            lblGraphXY3.Text = "";
            lblGraphXY4.Text = "";
            lblGraphXY5.Text = "";
            lblGraphXY6.Text = "";
            lblGraphXY7.Text = "";
            lblGraphXY8.Text = "";
        }
        public void ClearGraphDataByChId(int chId)
        {
            DeviceCOM.responses.RemoveAll(r => r.CN == chId);

            if (chId == 1)
            {
                cn1.Children.Clear();
                rResult1.Fill = new SolidColorBrush(Colors.White);
                lblGraphXY1.Text = "";
            }
            else if (chId == 2)
            {
                cn2.Children.Clear();
                rResult2.Fill = new SolidColorBrush(Colors.White);
                lblGraphXY2.Text = "";
            }
            else if (chId == 3)
            {
                cn3.Children.Clear();
                rResult3.Fill = new SolidColorBrush(Colors.White);
                lblGraphXY3.Text = "";
            }
            else if (chId == 4)
            {
                cn4.Children.Clear();
                rResult4.Fill = new SolidColorBrush(Colors.White);
                lblGraphXY4.Text = "";
            }
            else if (chId == 5)
            {
                cn5.Children.Clear();
                rResult5.Fill = new SolidColorBrush(Colors.White);
                lblGraphXY5.Text = "";
            }
            else if (chId == 6)
            {
                cn6.Children.Clear();
                rResult6.Fill = new SolidColorBrush(Colors.White);
                lblGraphXY6.Text = "";
            }
            else if (chId == 7)
            {
                cn7.Children.Clear();
                rResult7.Fill = new SolidColorBrush(Colors.White);
                lblGraphXY7.Text = "";
            }
            else if (chId == 8)
            {
                cn8.Children.Clear();
                rResult8.Fill = new SolidColorBrush(Colors.White);
                lblGraphXY8.Text = "";
            }

            btnOverallResult.Background = new SolidColorBrush(Colors.LightBlue);
            btnOverallResult1.Background = new SolidColorBrush(Colors.LightBlue);
        }
        public void RefreshResponse()
        {
            ClearGraphData(false);
            var selectedChannel = DeviceCOM.channelDatas.FirstOrDefault(c => c.IsSeleted);
            if (selectedChannel == null) return;
            var selectedChannelData = DeviceCOM.responses.Where(r => r.CN == selectedChannel.Id && !r.IsAutoEllipseTest).ToList();

            foreach (var item in selectedChannelData)
            {
                foreach (var fd in item.FD)
                {
                    Ellipse el1 = new Ellipse();
                    el1.Height = 4;
                    el1.Width = 4;
                    var left = fd.X / factor;
                    var top = (fd.Y * -1) / factor;
                    if (left > (seqLength / 2))
                    {
                        left = (seqLength / 2);
                    }
                    if (top > (seqLength / 2))
                    {
                        top = (seqLength / 2);
                    }

                    if (left < ((seqLength / 2) * -1))
                    {
                        left = ((seqLength / 2) * -1);
                    }
                    if (top < ((seqLength / 2) * -1))
                    {
                        top = ((seqLength / 2) * -1);
                    }
                    Canvas.SetLeft(el1, left - 2);
                    Canvas.SetTop(el1, top - 2);
                    //r1.Stroke = new SolidColorBrush(Colors.Black);
                    if (selectedChannelData.IndexOf(item) == selectedChannelData.Count - 1)
                    {
                        if (item.IsBalacenced)
                        {
                            el1.Fill = new SolidColorBrush(Colors.Brown);
                        }
                        else
                        {
                            el1.Fill = new SolidColorBrush(Colors.Blue);
                            if (item.OR == 1)
                            {
                                btnOverallResult.Background = new SolidColorBrush(Colors.Green);
                                btnOverallResult1.Background = new SolidColorBrush(Colors.Green);
                            }
                            else
                            {
                                btnOverallResult.Background = new SolidColorBrush(Colors.Red);
                                btnOverallResult1.Background = new SolidColorBrush(Colors.Red);
                            }
                        }
                    }
                    else
                    {
                        if (item.IsBalacenced)
                        {
                            el1.Fill = new SolidColorBrush(Colors.Brown);
                        }
                        else
                        {
                            if (fd.R == 1)
                            {
                                el1.Fill = new SolidColorBrush(Colors.Green);
                            }
                            else
                            {
                                el1.Fill = new SolidColorBrush(Colors.Red);
                            }
                        }
                    }

                    // Gdata.isEnable 
                    if (fd.FN == 1)
                    {
                        cn1.Children.Add(el1);

                        if (!item.IsBalacenced)
                        {
                            lblGraphXY1.Text = fd.X.ToString() + "," + fd.Y.ToString();
                            if (fd.R == 1)
                            {
                                rResult1.Fill = new SolidColorBrush(Colors.Green);
                            }
                            else
                            {
                                rResult1.Fill = new SolidColorBrush(Colors.Red);
                            }
                        }
                    }
                    else if (fd.FN == 2)
                    {
                        cn2.Children.Add(el1);

                        if (!item.IsBalacenced)
                        {
                            lblGraphXY2.Text = fd.X.ToString() + "," + fd.Y.ToString();
                            if (fd.R == 1)
                            {
                                rResult2.Fill = new SolidColorBrush(Colors.Green);
                            }
                            else
                            {
                                rResult2.Fill = new SolidColorBrush(Colors.Red);
                            }
                        }
                    }
                    else if (fd.FN == 3)
                    {
                        cn3.Children.Add(el1);

                        if (!item.IsBalacenced)
                        {
                            lblGraphXY3.Text = fd.X.ToString() + "," + fd.Y.ToString();
                            if (fd.R == 1)
                            {
                                rResult3.Fill = new SolidColorBrush(Colors.Green);
                            }
                            else
                            {
                                rResult3.Fill = new SolidColorBrush(Colors.Red);
                            }
                        }
                    }
                    else if (fd.FN == 4)
                    {
                        cn4.Children.Add(el1);

                        if (!item.IsBalacenced)
                        {
                            lblGraphXY4.Text = fd.X.ToString() + "," + fd.Y.ToString();
                            if (fd.R == 1)
                            {
                                rResult4.Fill = new SolidColorBrush(Colors.Green);
                            }
                            else
                            {
                                rResult4.Fill = new SolidColorBrush(Colors.Red);
                            }
                        }
                    }
                    else if (fd.FN == 5)
                    {
                        cn5.Children.Add(el1);

                        if (!item.IsBalacenced)
                        {
                            lblGraphXY5.Text = fd.X.ToString() + "," + fd.Y.ToString();
                            if (fd.R == 1)
                            {
                                rResult5.Fill = new SolidColorBrush(Colors.Green);
                            }
                            else
                            {
                                rResult5.Fill = new SolidColorBrush(Colors.Red);
                            }
                        }
                    }
                    else if (fd.FN == 6)
                    {
                        cn6.Children.Add(el1);

                        if (!item.IsBalacenced)
                        {
                            lblGraphXY6.Text = fd.X.ToString() + "," + fd.Y.ToString();
                            if (fd.R == 1)
                            {
                                rResult6.Fill = new SolidColorBrush(Colors.Green);
                            }
                            else
                            {
                                rResult6.Fill = new SolidColorBrush(Colors.Red);
                            }
                        }
                    }
                    else if (fd.FN == 7)
                    {
                        cn7.Children.Add(el1);

                        if (!item.IsBalacenced)
                        {
                            lblGraphXY7.Text = fd.X.ToString() + "," + fd.Y.ToString();
                            if (fd.R == 1)
                            {
                                rResult7.Fill = new SolidColorBrush(Colors.Green);
                            }
                            else
                            {
                                rResult7.Fill = new SolidColorBrush(Colors.Red);
                            }
                        }
                    }
                    else if (fd.FN == 8)
                    {
                        cn8.Children.Add(el1);

                        if (!item.IsBalacenced)
                        {
                            lblGraphXY8.Text = fd.X.ToString() + "," + fd.Y.ToString();
                            if (fd.R == 1)
                            {
                                rResult8.Fill = new SolidColorBrush(Colors.Green);
                            }
                            else
                            {
                                rResult8.Fill = new SolidColorBrush(Colors.Red);
                            }
                        }
                    }
                }
            }

        }

        private void btnResetCounter_Click(object sender, RoutedEventArgs e)
        {
            var SChId = DeviceCOM.channelDatas.FirstOrDefault(c => c.IsSeleted)?.Id;
            var cnt = DeviceCOM.counter.FirstOrDefault(c => c.Id == SChId);
            if (cnt == null) return;

            cnt.ResultCount = 0;
            cnt.ResultOkCount = 0;
            cnt.ResultOkNotCount = 0;

            lblTCount.Content = "Total Count - " + cnt.ResultCount.ToString();
            lblOkCount.Content = "OK Count - " + cnt.ResultOkCount.ToString();
            lblNotOkCount.Content = "Not Ok Count - " + cnt.ResultOkNotCount.ToString();

            lblTCount1.Content = "Total Count - " + cnt.ResultCount.ToString();
            lblOkCount1.Content = "OK Count - " + cnt.ResultOkCount.ToString();
            lblNotOkCount1.Content = "Not Ok Count - " + cnt.ResultOkNotCount.ToString();

            lblTCount2.Content = "Total Count - " + cnt.ResultCount.ToString();
            lblOkCount2.Content = "OK Count - " + cnt.ResultOkCount.ToString();
            lblNotOkCount2.Content = "Not Ok Count - " + cnt.ResultOkNotCount.ToString();

            lblCode.Content = "";
        }

        private void btnLog_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (isPasswordEnable)
            {
                PasswordDialog passwordDlg = new PasswordDialog(MenuItemViewModel.CONFIG_MENU_PASSWORD)
                {
                    Owner = this
                };

                bool? isAuth = passwordDlg.ShowDialog();
                if (isAuth != true)
                {
                    return;
                }
            }

            lblCode.Content = "";
            if (DeviceCOM.IsLogEnable)
            {
                DeviceCOM.IsLogEnable = false;
                lblLog.Content = "Start Log";
                lblLog1.Content = "Start Log";
                lblLog2.Content = "Start Log";
                lblPartLogs.Content = "";
                if (DeviceCOM.IsLogRequiredOnBalance)
                {
                    if (DeviceCOM.IsSystemBusy)
                    {
                        MessageBox.Show("System is busy so you can not perform this command, please wait...", "System Information");
                    }
                    else
                    {

                        byte[] data = new byte[6];
                        data[0] = Convert.ToByte(2);
                        data[1] = Convert.ToByte(19);
                        data[2] = Convert.ToByte(1);
                        data[3] = DeviceCOM.IsLogEnable ? Convert.ToByte(1) : Convert.ToByte(2);

                        var rat = portCOM != null && portCOM.WriteDataInBytes(data);

                        if (!rat)
                        {
                            MessageBox.Show("Log stopped but no response from the ECT Instrument, please reboot it!!!", "System Information");
                        }
                    }
                }
            }
            else
            {
                var IsReNewConfig = _8F.Services.ConfigKey.ProtectedSettings.IsReNewConfig;
                if (IsReNewConfig)
                {
                    partConfigReNew = new PartConfigReNew();
                    partConfigReNew.Closing += partConfig_Closing;
                    partConfigReNew.portCOM = portCOM;
                    partConfigReNew.Owner = this;
                    partConfigReNew.ShowDialog();
                }
                else
                {
                    partConfig = new PartConfig();
                    partConfig.Closing += partConfig_Closing;
                    partConfig.Owner = this;
                    partConfig.ShowDialog();
                }

            }


        }
        private void partConfig_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DeviceCOM.IsLogEnable)
            {
                lblLog.Content = "Stop Log";
                lblLog1.Content = "Stop Log";
                lblLog2.Content = "Stop Log";
                lblPartLogs.Content = DeviceCOM.part.BatchName + " => " + DeviceCOM.part.Name;
            }
            else
            {
                lblPartLogs.Content = "";
            }
        }

        private void btnStop_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (portCOM == null) return;
            Status status = new Status() { FC = 18 };

            bool rat = false;
            var IsJSON = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["IsJSON"]);

            if (IsJSON)
            {
                rat = portCOM.WriteData(JsonConvert.SerializeObject(status));
            }
            else
            {
                byte[] data = new byte[6];
                data[0] = Convert.ToByte(2);
                data[1] = Convert.ToByte(18);
                data[2] = Convert.ToByte(1);
                data[3] = Convert.ToByte(chNo);
                rat = portCOM.WriteDataInBytes(data);
            }


        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (portCOM == null) return;
            if (e.Key == Key.B || e.Key == Key.Space || e.Key == Key.R)
            {
                if (e.Key == Key.B)
                {
                    if (DeviceCOM.IsSystemBusy)
                    {
                        MessageBox.Show("System is busy so you can not perform this command, please wait...", "System Information");
                    }
                    else
                    {
                        BalanceTest balanceTest = new BalanceTest() { FC = 16, CN = 0 };

                        bool rat = false;
                        var IsJSON = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["IsJSON"]);
                        if (IsJSON)
                        {
                            rat = portCOM.WriteData(JsonConvert.SerializeObject(balanceTest));
                        }
                        else
                        {
                            byte[] data = new byte[7];
                            data[0] = Convert.ToByte(2);
                            data[1] = Convert.ToByte(16);
                            data[2] = Convert.ToByte(2);
                            data[3] = Convert.ToByte(0);
                            data[4] = DeviceCOM.IsLogRequiredOnBalance ? (DeviceCOM.IsLogEnable ? Convert.ToByte(1) : Convert.ToByte(2)) : Convert.ToByte(0);

                            rat = portCOM.WriteDataInBytes(data);
                        }


                        if (rat)
                        {
                            DeviceCOM.IsBalanceAll = true;
                            DeviceCOM.IsBalanceBusyEnable = true;
                        }
                        else
                        {
                            MessageBox.Show("Unable to balance due to the error in the communication!", "Error Information");
                        }
                    }

                    lblCode.Content = "";
                }
                else if (e.Key == Key.R)
                {
                    foreach (var cnt in DeviceCOM.counter)
                    {
                        cnt.ResultCount = 0;
                        cnt.ResultOkCount = 0;
                        cnt.ResultOkNotCount = 0;
                    }

                    lblTCount.Content = "Total Count - 0";
                    lblOkCount.Content = "OK Count - 0";
                    lblNotOkCount.Content = "Not Ok Count - 0";

                    lblTCount1.Content = "Total Count - 0";
                    lblOkCount1.Content = "OK Count - 0";
                    lblNotOkCount1.Content = "Not Ok Count - 0";

                    lblTCount2.Content = "Total Count - 0";
                    lblOkCount2.Content = "OK Count - 0";
                    lblNotOkCount2.Content = "Not Ok Count - 0";

                    lblCode.Content = "";
                }
                else if (e.Key == Key.Space)
                {
                    if (DeviceCOM.IsSystemBusy)
                    {
                        MessageBox.Show("System is busy so you can not perform this command, please wait...", "System Information");

                    }
                    else
                    {
                        DeviceCOM.IsAutoEllipseActive = false;
                        BalanceTest balanceTest = new BalanceTest() { FC = 17, CN = 0 };

                        bool rat = false;
                        var IsJSON = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["IsJSON"]);

                        if (IsJSON)
                        {
                            rat = portCOM.WriteData(JsonConvert.SerializeObject(balanceTest));
                        }
                        else
                        {
                            byte[] data = new byte[6];
                            data[0] = Convert.ToByte(2);
                            data[1] = Convert.ToByte(17);
                            data[2] = Convert.ToByte(1);
                            data[3] = Convert.ToByte(0);

                            rat = portCOM.WriteDataInBytes(data);
                        }


                        if (!rat)
                        {
                            MessageBox.Show("Unable to start test due to the error in the communication!", "Error Information");
                        }
                        else
                        {
                            if (DeviceCOM.IsBalanceRequired)
                            {
                                MessageBox.Show("Unable to test because of balance command is required!", "Error Information");
                                DeviceCOM.IsBalanceRequired = false;
                            }
                            if (DeviceCOM.IsBinRequired)
                            {
                                MessageBox.Show("Please put the previous component to NG bin before starting the test!", "Error Information");
                                DeviceCOM.IsBinRequired = false;
                            }

                            if (!DeviceCOM.IsBalanceRequired && !DeviceCOM.IsBinRequired)
                            {
                                DeviceCOM.IsLogDisable = true;
                            }
                        }
                    }

                    lblCode.Content = "";
                }
            }
        }

        private void OnModbusRegisterIdReceived(object? sender, ushort profileId)
        {
            if (profileId == 0) return;

            Dispatcher.Invoke(async () =>
            {
                try
                {
                    IConfigProfileRepository repo = new InspectionLogRepository();
                    var dbChannels = await repo.GetConfigProfileAsync(profileId);
                    if (dbChannels != null && dbChannels.Count > 0)
                    {
                        DeviceCOM.channelDatas = dbChannels;
                        SelectCh1();
                        ClearGraphData();
                        var rat = ImplementChanges(0);
                        if (!rat)
                        {
                            System.Diagnostics.Debug.WriteLine($"Modbus Auto-Load Profile #{profileId}: Configuration loaded, but no response from ECT instrument.");
                        }
                        lblConfigFileName.Content = $"DB: Profile #{profileId}";
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Modbus Auto-Load Profile #{profileId}: Profile #{profileId} not found in database.");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error auto-loading Modbus DB Profile #{profileId}: {ex.Message}");
                }
            });
        }
    }

    public class BarcodeScanner
    {
        private readonly StringBuilder _buffer = new();
        private readonly DispatcherTimer _timer;

        public event EventHandler<string>? BarcodeScanned;

        public BarcodeScanner()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _timer.Tick += (s, e) =>
            {
                if (_buffer.Length > 0)
                {
                    string code = _buffer.ToString();
                    _buffer.Clear();
                    BarcodeScanned?.Invoke(this, code);
                }
                _timer.Stop();
            };
        }

        public void HandleKey(KeyEventArgs e)
        {
            char c = GetCharFromKey(e.Key);
            if (c == '\0')
                return;

            if (e.Key == Key.Enter)
            {
                string code = _buffer.ToString();
                _buffer.Clear();
                _timer.Stop();
                BarcodeScanned?.Invoke(this, code);
            }
            else
            {
                _buffer.Append(c);
                _timer.Stop();
                _timer.Start();
            }
        }

        private static char GetCharFromKey(Key key)
        {
            if (key >= Key.A && key <= Key.Z)
                return (char)('A' + (key - Key.A));
            if (key >= Key.D0 && key <= Key.D9)
                return (char)('0' + (key - Key.D0));
            if (key == Key.OemMinus)
                return '-';
            if (key == Key.Space)
                return ' ';
            if (key == Key.Enter)
                return '\r';

            return '\0';
        }
    }
}
