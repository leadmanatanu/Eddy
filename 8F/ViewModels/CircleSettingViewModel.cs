using _8F.Auth;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace _8F.ViewModels
{
    public class CircleSettingViewModel : BaseViewModel
    {
        private string _selectChannel = string.Empty;
        public string SelectChannel
        {
            get => _selectChannel;
            set => SetProperty(ref _selectChannel, value);
        }

        private ObservableCollection<EllipsDTO> _ellipses = new();
        public ObservableCollection<EllipsDTO> Ellipses
        {
            get => _ellipses;
            set => SetProperty(ref _ellipses, value);
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
        public ICommand AddNewCommand { get; }
        public ICommand DeleteSelectedCommand { get; }
        public ICommand CloseCommand { get; }

        public CircleSettingViewModel(string selectChannel)
        {
            _selectChannel = selectChannel;

            SaveConfigCommand = new RelayCommand(ExecuteSaveConfig);
            AddNewCommand = new RelayCommand(ExecuteAddNew);
            DeleteSelectedCommand = new RelayCommand(ExecuteDeleteSelected);
            CloseCommand = new RelayCommand(ExecuteClose);

            LoadAllChannelsEllipses();
        }

        public void LoadAllChannelsEllipses()
        {
            Ellipses.Clear();

            var selectedDevice = DeviceCOM.channelDatas.FirstOrDefault(c => c.IsSeleted);
            if (selectedDevice == null) return;

            int colorIndex = 0;

            foreach (var channel in selectedDevice.graphDatas)
            {
                foreach (var ell in channel.ellipses)
                {
                    var dto = new EllipsDTO
                    {
                        Id = ell.Id,
                        ChannelName = channel.Name,
                        height = ell.height,
                        width = ell.width,
                        ex = ell.ex,
                        ey = ell.ey,
                        angel = ell.angel,
                        ColorName = MyColor.GetColorName(colorIndex++).ToString()
                    };
                    Ellipses.Add(dto);
                }
            }
        }

        private void ExecuteSaveConfig()
        {
            if (!AppServices.AuthService.Can(Permission.EditSettings))
            {
                StatusMessage = "Admin access required to save configuration.";
                return;
            }

            try
            {
                SaveData();
                IsSaved = true;

                if (OwnerWindow is MainWindow mw)
                {
                    mw.ImplementChanges(2);
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

        private void SaveData()
        {
            StatusMessage = string.Empty;
            var ch = DeviceCOM.channelDatas.FirstOrDefault(c => c.IsSeleted == true);
            if (ch == null) return;

            ElliplseWrite ellipseWrite = new ElliplseWrite();
            ellipseWrite.FC = 5;
            ellipseWrite.CN = ch.Id;
            ellipseWrite.FD = new List<Frequ>();
            var Gdata = ch.graphDatas.FirstOrDefault(d => d.Name == _selectChannel);
            if (Gdata == null) return;

            Frequ frequ = new Frequ();
            frequ.FN = Gdata.Id;
            frequ.ED = new List<Elliplse>();

            ellipseWrite.FD.Clear();

            foreach (var graph in ch.graphDatas)
            {
                graph.ellipses.Clear();

                var freq = new Frequ();
                freq.FN = graph.Id;
                freq.ED = new List<Elliplse>();

                var channelEllipses = Ellipses
                    .Where(e => e.ChannelName == graph.Name)
                    .ToList();

                int id = 1;

                foreach (var item in channelEllipses)
                {
                    Ellips el = new Ellips
                    {
                        Id = id++,
                        height = item.height,
                        width = item.width,
                        ex = item.ex,
                        ey = item.ey,
                        angel = item.angel
                    };

                    graph.ellipses.Add(el);

                    freq.ED.Add(new Elliplse()
                    {
                        FN = graph.Id,
                        EId = el.Id,
                        a = el.height,
                        b = el.width,
                        t = el.angel,
                        x = (int)Math.Round(el.ex, MidpointRounding.AwayFromZero),
                        y = (int)Math.Round(el.ey, MidpointRounding.AwayFromZero)
                    });
                }

                ellipseWrite.FD.Add(freq);
            }

            var rat = false;
            var isJson = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["IsJSON"]);

            if (isJson)
            {
                rat = PortCOM != null && PortCOM.WriteData(JsonConvert.SerializeObject(ellipseWrite));
            }
            else
            {
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

                    data1[start1B + 1] = (byte)(Convert.ToInt16(kvp.ED[0].a) & 0xFF);
                    data1[start1B + 2] = (byte)((Convert.ToInt16(kvp.ED[0].a) >> 8) & 0xFF);

                    data1[start1B + 3] = (byte)(Convert.ToInt16(kvp.ED[0].b) & 0xFF);
                    data1[start1B + 4] = (byte)((Convert.ToInt16(kvp.ED[0].b) >> 8) & 0xFF);

                    data1[start1B + 5] = (byte)(Convert.ToInt16(kvp.ED[0].t) & 0xFF);
                    data1[start1B + 6] = (byte)((Convert.ToInt16(kvp.ED[0].t) >> 8) & 0xFF);

                    data1[start1B + 7] = (byte)(Convert.ToInt16(kvp.ED[0].x) & 0xFF);
                    data1[start1B + 8] = (byte)((Convert.ToInt16(kvp.ED[0].x) >> 8) & 0xFF);

                    data1[start1B + 9] = (byte)(Convert.ToInt16(kvp.ED[0].y) & 0xFF);
                    data1[start1B + 10] = (byte)((Convert.ToInt16(kvp.ED[0].y) >> 8) & 0xFF);

                    start1B += 11;
                }

                rat = PortCOM != null && PortCOM.WriteDataInBytes(data1);
            }

            if (rat)
            {
                StatusMessage = "Configuration Saved!!!";
            }
            else
            {
                StatusMessage = "Configuration Saved but no response from the ECT Instrument, please reboot it and write the configuration again!!!";
            }
        }

        private void ExecuteAddNew()
        {
            EllipsDTO ellips = new EllipsDTO
            {
                Id = Ellipses.Count + 1,
                height = DeviceCOM.DefaultHeight,
                width = DeviceCOM.DefaultWidth
            };
            Ellipses.Add(ellips);
        }

        private void ExecuteDeleteSelected(object? parameter)
        {
            if (!AppServices.AuthService.Can(Permission.EditSettings)) return;
            if (parameter is EllipsDTO item)
            {
                Ellipses.Remove(item);
            }
        }

        private void ExecuteClose()
        {
            if (IsSaved)
            {
                ExecuteSaveConfig();
            }
            CloseAction?.Invoke();
        }
    }
}
