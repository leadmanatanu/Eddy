using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace _8F.ViewModels
{
    public class FreqViewModel : BaseViewModel
    {
        private bool _isTxStrengthEnabled;
        public bool IsTxStrengthEnabled
        {
            get => _isTxStrengthEnabled;
            set
            {
                if (SetProperty(ref _isTxStrengthEnabled, value))
                {
                    OnPropertyChanged(nameof(TxStrengthVisibility));
                }
            }
        }

        public Visibility TxStrengthVisibility => IsTxStrengthEnabled ? Visibility.Visible : Visibility.Collapsed;

        private int? _txStrengthValue = 100;
        public int? TxStrengthValue
        {
            get => _txStrengthValue;
            set => SetProperty(ref _txStrengthValue, value);
        }

        private List<GraphData> _graphDataList = new();
        public List<GraphData> GraphDataList
        {
            get => _graphDataList;
            set => SetProperty(ref _graphDataList, value);
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private bool _isSaved = false;
        public bool IsSaved
        {
            get => _isSaved;
            set => SetProperty(ref _isSaved, value);
        }

        public DeviceCOM? PortCOM { get; set; }
        public Window? OwnerWindow { get; set; }
        public Action? CloseAction { get; set; }

        private DispatcherTimer _clearLabelTimer = new();

        public ICommand SaveConfigCommand { get; }
        public ICommand CloseCommand { get; }

        public FreqViewModel()
        {
            IsTxStrengthEnabled = _8F.Services.ConfigKey.ProtectedSettings.IsTxStrengthEnable;

            SaveConfigCommand = new RelayCommand(ExecuteSaveConfig);
            CloseCommand = new RelayCommand(ExecuteClose);

            LoadFrequencyData();
        }

        public void LoadFrequencyData()
        {
            var selectedChannel = DeviceCOM.channelDatas?.FirstOrDefault(c => c.IsSeleted);
            if (selectedChannel != null && selectedChannel.graphDatas != null)
            {
                GraphDataList = selectedChannel.graphDatas
                    .Select(x => new GraphData
                    {
                        Id = x.Id,
                        Name = x.Name,
                        freq = x.freq,
                        gain = x.gain,
                        phase = x.phase,
                        isEnable = x.isEnable,
                    }).ToList();

                if (selectedChannel.graphDatas.Any())
                {
                    TxStrengthValue = selectedChannel.TxStrength;
                }
            }
        }

        private void ExecuteSaveConfig()
        {
            try
            {
                StatusMessage = string.Empty;

                var ch = DeviceCOM.channelDatas?.FirstOrDefault(c => c.IsSeleted == true);
                if (ch == null) return;

                byte txStrength = 100;

                if (IsTxStrengthEnabled)
                {
                    if (!TxStrengthValue.HasValue)
                    {
                        StatusMessage = "Please enter Tx Strength (1-100).";
                        return;
                    }

                    if (TxStrengthValue < 1 || TxStrengthValue > 100)
                    {
                        StatusMessage = "Tx Strength must be between 1 and 100.";
                        return;
                    }

                    txStrength = (byte)TxStrengthValue.Value;
                }

                var msg = Validate(GraphDataList);

                if (DeviceCOM.IsSystemBusy)
                {
                    msg.Add("System is busy so you can not perform this command, please wait...");
                    StatusMessage = msg.FirstOrDefault() ?? "System busy.";
                    return;
                }

                if (msg.Count == 0)
                {
                    FrequencyWrite frequencyWrite = new FrequencyWrite
                    {
                        FC = 4,
                        CN = ch.Id,
                        T = txStrength,
                        FD = new List<Frequency>()
                    };

                    foreach (var Gdata in GraphDataList)
                    {
                        Frequency frequency = new Frequency()
                        {
                            FN = Gdata.Id,
                            F = Gdata.freq,
                            G = Gdata.gain,
                            P = Gdata.phase,
                            E = Gdata.isEnable ? 1 : 0,
                        };

                        frequencyWrite.FD.Add(frequency);
                    }

                    var rat = false;
                    var isJson = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["IsJSON"]);

                    if (isJson)
                    {
                        rat = PortCOM != null && PortCOM.WriteData(JsonConvert.SerializeObject(frequencyWrite));
                    }
                    else
                    {
                        int length = (frequencyWrite.FD.Count * 10) + (IsTxStrengthEnabled ? 7 : 6);
                        byte[] data = new byte[length];
                        data[0] = Convert.ToByte(2);
                        data[1] = Convert.ToByte(4);
                        data[2] = Convert.ToByte((frequencyWrite.FD.Count * 10) + (IsTxStrengthEnabled ? 2 : 1));
                        data[3] = Convert.ToByte(ch.Id);
                        int startB = 4;

                        foreach (var kvp in frequencyWrite.FD)
                        {
                            data[startB] = Convert.ToByte(kvp.FN);

                            data[startB + 1] = (byte)(Convert.ToUInt32(kvp.F) & 0xFF);
                            data[startB + 2] = (byte)((Convert.ToUInt32(kvp.F) >> 8) & 0xFF);
                            data[startB + 3] = (byte)((Convert.ToUInt32(kvp.F) >> 16) & 0xFF);
                            data[startB + 4] = (byte)((Convert.ToUInt32(kvp.F) >> 24) & 0xFF);

                            data[startB + 5] = (byte)(Convert.ToUInt16(kvp.G * 10) & 0xFF);
                            data[startB + 6] = (byte)((Convert.ToUInt16(kvp.G * 10) >> 8) & 0xFF);

                            data[startB + 7] = (byte)(Convert.ToUInt16(kvp.P * 10) & 0xFF);
                            data[startB + 8] = (byte)((Convert.ToUInt16(kvp.P * 10) >> 8) & 0xFF);

                            data[startB + 9] = Convert.ToByte(kvp.E);
                            startB += 10;
                        }

                        if (IsTxStrengthEnabled)
                        {
                            data[startB] = txStrength;
                        }

                        rat = PortCOM != null && PortCOM.WriteDataInBytes(data);
                    }

                    if (rat)
                    {
                        StatusMessage = "Configuration Saved!!!";
                        IsSaved = true;

                        ch.TxStrength = txStrength;
                        foreach (var f in ch.graphDatas)
                        {
                            var tf = GraphDataList.FirstOrDefault(d => d.Id == f.Id);
                            if (tf != null)
                            {
                                f.freq = tf.freq;
                                f.gain = tf.gain;
                                f.phase = tf.phase;
                                f.isEnable = tf.isEnable;
                            }
                        }

                        if (OwnerWindow is MainWindow mw)
                        {
                            mw.ImplementChanges(1);
                        }
                    }
                    else
                    {
                        StatusMessage = "Configuration Saved but no response from the ECT Instrument, please reboot it and write the configuration again!!!";
                    }
                }
                else
                {
                    StatusMessage = string.Join("\n", msg);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Error while saving the Configuration!!! \n Message:- " + ex.Message;
            }

            _clearLabelTimer.Stop();
            _clearLabelTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
            _clearLabelTimer.Tick += (s, e) =>
            {
                StatusMessage = string.Empty;
                _clearLabelTimer.Stop();
            };
            _clearLabelTimer.Start();
        }

        private List<string> Validate(List<GraphData> list)
        {
            List<string> validationMsg = new List<string>();
            foreach (var item in list)
            {
                if (item.freq < 100 || item.freq > 1000000)
                {
                    validationMsg.Add($"Frequency for {item.Name} must be between 100 and 1,000,000 Hz.");
                }
                if (item.gain < 0 || item.gain > 90)
                {
                    validationMsg.Add($"Gain for {item.Name} must be between 0 and 90 dB.");
                }
                if (item.phase < 0 || item.phase > 359)
                {
                    validationMsg.Add($"Phase for {item.Name} must be between 0 and 359 degrees.");
                }
            }
            return validationMsg;
        }

        private void ExecuteClose()
        {
            CloseAction?.Invoke();
        }
    }
}
