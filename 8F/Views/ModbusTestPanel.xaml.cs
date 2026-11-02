using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
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
                lblStatus.Text = $"Active (Port {_modbusService.Port}, Heartbeat {_modbusService.HeartbeatIntervalMs}ms, Channels: {_modbusService.ChannelCount}{fallbackSuffix})";
                lblStatus.Foreground = System.Windows.Media.Brushes.Green;
            }
            else
            {
                lblStatus.Text = "Disabled / Stopped";
                lblStatus.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private void RefreshRegisterDisplays()
        {
            if (_modbusService != null)
            {
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
                MessageBox.Show("Modbus TCP Slave server is not running. Please enable 'IsModbusServerEnable' in App.config.", "Modbus Server Off", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (channelIndex >= _modbusService.ChannelCount)
            {
                MessageBox.Show($"Channel {channelIndex + 1} is not active in current configuration.", "Channel Inactive", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ushort.TryParse(input.Trim(), out ushort val))
            {
                ushort address = ModbusSlaveService.ChannelIndexToAddress(channelIndex);
                _modbusService.WriteRegister(address, val);
                RefreshRegisterDisplays();

                if (Owner is MainWindow mainWin)
                {
                    await mainWin.OpenProfileForChannelAsync(channelIndex, val, isUnattended: false);
                    RefreshRegisterDisplays();
                }
                else
                {
                    MessageBox.Show($"Holding register {address} (Channel {channelIndex + 1}) updated to {val}!", "Modbus Write", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                MessageBox.Show("Please enter a valid ushort integer value between 0 and 65535.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnWriteHeartbeat_Click(object sender, RoutedEventArgs e)
        {
            if (_modbusService == null || !_modbusService.IsRunning)
            {
                MessageBox.Show("Modbus TCP Slave server is not running. Please enable 'IsModbusServerEnable' in App.config.", "Modbus Server Off", MessageBoxButton.OK, MessageBoxImage.Warning);
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
