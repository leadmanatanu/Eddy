using System;
using System.Windows;
using System.Windows.Threading;
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
                lblStatus.Text = $"Active (Listening on TCP Port {_modbusService.Port}, Heartbeat {_modbusService.HeartbeatIntervalMs}ms)";
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
                ushort val0 = _modbusService.ReadRegister(0);
                lblRegister0Value.Text = val0.ToString();

                ushort val1 = _modbusService.ReadRegister(1);
                lblHeartbeatValue.Text = val1.ToString();

                ushort val2 = _modbusService.ReadRegister(2);
                if (lblStatusRegisterValue != null)
                {
                    lblStatusRegisterValue.Text = val2 switch
                    {
                        0 => "0 - Idle",
                        1 => "1 - Loading",
                        2 => "2 - Loaded OK",
                        3 => "3 - Failed",
                        4 => "4 - Rejected (Busy)",
                        5 => "5 - Rejected (Invalid ID)",
                        _ => val2.ToString()
                    };

                    lblStatusRegisterValue.Foreground = val2 switch
                    {
                        1 => System.Windows.Media.Brushes.Orange,
                        2 => System.Windows.Media.Brushes.Green,
                        3 => System.Windows.Media.Brushes.Red,
                        4 => System.Windows.Media.Brushes.DarkGoldenrod,
                        5 => System.Windows.Media.Brushes.Crimson,
                        _ => System.Windows.Media.Brushes.Gray
                    };
                }
            }
        }

        private void btnRefresh0_Click(object sender, RoutedEventArgs e)
        {
            RefreshRegisterDisplays();
        }

        private void btnRefresh1_Click(object sender, RoutedEventArgs e)
        {
            RefreshRegisterDisplays();
        }

        private void btnRefresh2_Click(object sender, RoutedEventArgs e)
        {
            RefreshRegisterDisplays();
        }

        private async void btnWrite0_Click(object sender, RoutedEventArgs e)
        {
            if (_modbusService == null || !_modbusService.IsRunning)
            {
                MessageBox.Show("Modbus TCP Slave server is not running. Please enable 'IsModbusServerEnable' in App.config.", "Modbus Server Off", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ushort.TryParse(txtWriteValue0.Text.Trim(), out ushort val))
            {
                _modbusService.WriteRegisterSilent(0, val);
                RefreshRegisterDisplays();

                if (Owner is MainWindow mainWin)
                {
                    await mainWin.OpenProfileByIdAsync(val, isUnattended: false);
                    RefreshRegisterDisplays();
                }
                else
                {
                    MessageBox.Show($"Holding register 0 updated to {val}!", "Modbus Write", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                MessageBox.Show("Please enter a valid ushort integer value between 0 and 65535.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnWrite1_Click(object sender, RoutedEventArgs e)
        {
            if (_modbusService == null || !_modbusService.IsRunning)
            {
                MessageBox.Show("Modbus TCP Slave server is not running. Please enable 'IsModbusServerEnable' in App.config.", "Modbus Server Off", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ushort.TryParse(txtWriteValue1.Text.Trim(), out ushort val))
            {
                _modbusService.WriteRegister(1, val);
                RefreshRegisterDisplays();
                MessageBox.Show($"Holding register 1 (Heartbeat) manually updated to {val}!\n(Note: Will be overwritten by next automatic timer tick)", "Modbus Write", MessageBoxButton.OK, MessageBoxImage.Information);
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
            base.OnClosed(e);
            _timer.Stop();
            if (_modbusService != null)
            {
                _modbusService.RegisterValueChanged -= ModbusService_RegisterValueChanged;
                _modbusService.HeartbeatValueChanged -= ModbusService_HeartbeatValueChanged;
            }
        }
    }
}
