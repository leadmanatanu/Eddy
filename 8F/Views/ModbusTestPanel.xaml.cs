using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using _8F.Auth;
using _8F.Services.Implementations;
using _8F.Services.Interfaces;

namespace _8F.Views
{
    public partial class ModbusTestPanel : Window
    {
        private readonly IModbusSlaveService? _modbusService;
        private readonly DispatcherTimer _timer;

        public ModbusTestPanel(IModbusSlaveService modbusService)
        {
            InitializeComponent();
            _modbusService = modbusService;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _timer.Tick += (s, e) => RefreshRegisterDisplays();
            _timer.Start();

            if (_modbusService != null)
            {
                _modbusService.RegisterValueChanged += ModbusService_RegisterValueChanged;
                _modbusService.HeartbeatValueChanged += ModbusService_HeartbeatValueChanged;
            }

            int chCount = _modbusService?.ChannelCount ?? 1;
            cardChannel1.Visibility = chCount >= 1 ? Visibility.Visible : Visibility.Collapsed;
            cardChannel2.Visibility = chCount >= 2 ? Visibility.Visible : Visibility.Collapsed;
            cardChannel3.Visibility = chCount >= 3 ? Visibility.Visible : Visibility.Collapsed;
            cardChannel4.Visibility = chCount >= 4 ? Visibility.Visible : Visibility.Collapsed;

            UpdateServerStatus();
            RefreshRegisterDisplays();
        }

        private void ModbusService_RegisterValueChanged(object? sender, ushort e)
        {
            Dispatcher.BeginInvoke(RefreshRegisterDisplays);
        }

        private void ModbusService_HeartbeatValueChanged(object? sender, ushort e)
        {
            Dispatcher.BeginInvoke(RefreshRegisterDisplays);
        }

        private void UpdateServerStatus()
        {
            if (_modbusService != null && _modbusService.IsRunning)
            {
                string fallbackSuffix = _modbusService.IsChannelCountFallback
                    ? $" (Fallback: {_modbusService.ChannelCountConfigError})"
                    : "";
                lblStatus.Text = $"Running · Port {_modbusService.Port}, Ch: {_modbusService.ChannelCount}{fallbackSuffix}";
                lblStatus.Foreground = System.Windows.Media.Brushes.Green;
            }
            else
            {
                lblStatus.Text = _modbusService?.StatusDescription ?? "Disabled / Stopped";
                lblStatus.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private void RefreshRegisterDisplays()
        {
            if (_modbusService != null)
            {
                UpdateServerStatus();
                lblEndpoints.Text = _modbusService.ListeningEndpointsDescription;
                lblUnitIdMode.Text = _modbusService.UnitIdModeDescription;
                lblClientCount.Text = _modbusService.ActiveClientCount.ToString();
                lblLastRequestTime.Text = _modbusService.LastRequestTime.HasValue
                    ? _modbusService.LastRequestTime.Value.ToString("HH:mm:ss.fff")
                    : "None";
                lblCounters.Text = $"Requests: {_modbusService.TotalRequestsCount} | Exceptions: {_modbusService.TotalExceptionsCount} | Unknown IDs: {_modbusService.UnknownUnitIdRequestsCount}";

                // Address 0: Heartbeat
                ushort valHb = _modbusService.ReadRegister(ModbusSlaveService.HeartbeatAddress);
                lblHeartbeatValue.Text = valHb.ToString();
                if (!txtWriteValueHeartbeat.IsKeyboardFocused && !txtWriteValueHeartbeat.IsFocused)
                {
                    txtWriteValueHeartbeat.Text = valHb.ToString();
                }

                int chCount = _modbusService.ChannelCount;

                // Address 1: Channel 1 Profile ID
                if (chCount >= 1)
                {
                    ushort val1 = _modbusService.ReadRegister(ModbusSlaveService.ChannelIndexToAddress(0));
                    lblRegister1Value.Text = val1.ToString();
                    if (!txtWriteValue1.IsKeyboardFocused && !txtWriteValue1.IsFocused)
                    {
                        txtWriteValue1.Text = val1.ToString();
                    }
                }

                // Address 2: Channel 2 Profile ID
                if (chCount >= 2)
                {
                    ushort val2 = _modbusService.ReadRegister(ModbusSlaveService.ChannelIndexToAddress(1));
                    lblRegister2Value.Text = val2.ToString();
                    if (!txtWriteValue2.IsKeyboardFocused && !txtWriteValue2.IsFocused)
                    {
                        txtWriteValue2.Text = val2.ToString();
                    }
                }

                // Address 3: Channel 3 Profile ID
                if (chCount >= 3)
                {
                    ushort val3 = _modbusService.ReadRegister(ModbusSlaveService.ChannelIndexToAddress(2));
                    lblRegister3Value.Text = val3.ToString();
                    if (!txtWriteValue3.IsKeyboardFocused && !txtWriteValue3.IsFocused)
                    {
                        txtWriteValue3.Text = val3.ToString();
                    }
                }

                // Address 4: Channel 4 Profile ID
                if (chCount >= 4)
                {
                    ushort val4 = _modbusService.ReadRegister(ModbusSlaveService.ChannelIndexToAddress(3));
                    lblRegister4Value.Text = val4.ToString();
                    if (!txtWriteValue4.IsKeyboardFocused && !txtWriteValue4.IsFocused)
                    {
                        txtWriteValue4.Text = val4.ToString();
                    }
                }
            }
        }

        private async void btnStartServer_Click(object sender, RoutedEventArgs e)
        {
            if (_modbusService == null) return;
            if (_modbusService.IsRunning) return;
            try
            {
                lblStatus.Text = "Starting...";
                lblStatus.Foreground = System.Windows.Media.Brushes.Orange;
                await _modbusService.StartAsync();
                UpdateServerStatus();
                RefreshRegisterDisplays();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start Modbus Server: {ex.Message}", "Start Server Error", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateServerStatus();
            }
        }

        private async void btnStopServer_Click(object sender, RoutedEventArgs e)
        {
            if (_modbusService == null) return;
            if (!_modbusService.IsRunning) return;
            try
            {
                lblStatus.Text = "Stopping...";
                lblStatus.Foreground = System.Windows.Media.Brushes.Orange;
                await _modbusService.StopAsync();
                UpdateServerStatus();
                RefreshRegisterDisplays();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error stopping Modbus Server: {ex.Message}", "Stop Server Error", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateServerStatus();
            }
        }

        private void btnRefreshHeartbeat_Click(object sender, RoutedEventArgs e) => RefreshRegisterDisplays();
        private void btnRefresh1_Click(object sender, RoutedEventArgs e) => RefreshRegisterDisplays();
        private void btnRefresh2_Click(object sender, RoutedEventArgs e) => RefreshRegisterDisplays();
        private void btnRefresh3_Click(object sender, RoutedEventArgs e) => RefreshRegisterDisplays();
        private void btnRefresh4_Click(object sender, RoutedEventArgs e) => RefreshRegisterDisplays();

        private async void btnWrite1_Click(object sender, RoutedEventArgs e) => await WriteChannelRegister(0, txtWriteValue1.Text);
        private async void btnWrite2_Click(object sender, RoutedEventArgs e) => await WriteChannelRegister(1, txtWriteValue2.Text);
        private async void btnWrite3_Click(object sender, RoutedEventArgs e) => await WriteChannelRegister(2, txtWriteValue3.Text);
        private async void btnWrite4_Click(object sender, RoutedEventArgs e) => await WriteChannelRegister(3, txtWriteValue4.Text);

        private async Task WriteChannelRegister(int channelIndex, string input)
        {
            if (_modbusService == null || !_modbusService.IsRunning)
            {
                lblPanelMessage.Foreground = new SolidColorBrush(Colors.Red);
                lblPanelMessage.Text = "Modbus TCP Slave server is not running.";
                return;
            }

            if (channelIndex >= _modbusService.ChannelCount)
            {
                lblPanelMessage.Foreground = new SolidColorBrush(Colors.Red);
                lblPanelMessage.Text = $"Channel {channelIndex + 1} is not active in current configuration.";
                return;
            }

            if (!ushort.TryParse(input.Trim(), out ushort val))
            {
                lblPanelMessage.Foreground = new SolidColorBrush(Colors.Red);
                lblPanelMessage.Text = "Please enter a valid integer between 0 and 65535.";
                return;
            }

            int targetChannel = channelIndex + 1;

            if (val == 0)
            {
                lblPanelMessage.Foreground = new SolidColorBrush(Colors.Gray);
                lblPanelMessage.Text = $"Channel {targetChannel} Profile ID 0 received: loads nothing.";
                return;
            }

            var result = await AppServices.ProfileLoadService.LoadFromDbAsync(
                val,
                targetChannel,
                ProfileLoadSource.ModbusPanel,
                isInteractive: false);

            RefreshRegisterDisplays();

            if (result.Success)
            {
                lblPanelMessage.Foreground = new SolidColorBrush(Colors.Green);
                lblPanelMessage.Text = $"Profile #{val} ('{result.ProfileName}') loaded successfully into Channel {targetChannel}.";
            }
            else
            {
                lblPanelMessage.Foreground = new SolidColorBrush(Colors.Red);
                lblPanelMessage.Text = $"Failed to load Profile #{val} into Channel {targetChannel}: {result.Message}";
            }
        }

        private void btnWriteHeartbeat_Click(object sender, RoutedEventArgs e)
        {
            if (_modbusService == null || !_modbusService.IsRunning)
            {
                MessageBox.Show("Modbus TCP Slave server is not running. Please click 'Start Server' or enable 'IsModbusServerEnable' in App.config.", "Modbus Server Off", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ushort.TryParse(txtWriteValueHeartbeat.Text.Trim(), out ushort val))
            {
                ushort address = ModbusSlaveService.HeartbeatAddress;
                _modbusService.WriteRegister(address, val);
                RefreshRegisterDisplays();
                MessageBox.Show($"Holding register {address} (Heartbeat) manually updated to {val}!\n(Note: Will be overwritten by next automatic timer tick)", "Modbus Write", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Please enter a valid ushort integer value between 0 and 65535.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer.Stop();
            if (_modbusService != null)
            {
                _modbusService.RegisterValueChanged -= ModbusService_RegisterValueChanged;
                _modbusService.HeartbeatValueChanged -= ModbusService_HeartbeatValueChanged;
            }
            base.OnClosed(e);
        }
    }
}
