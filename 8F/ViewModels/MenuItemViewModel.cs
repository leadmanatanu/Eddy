using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;
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

namespace _8F.ViewModels
{
    public class MenuItemViewModel
    {
        public const string DefaultConfigPassword = "best@123";
        public const string CONFIG_MENU_PASSWORD = DefaultConfigPassword;

        private static readonly HashSet<string> ConfigurationMenuHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "Change Configuration",
            "Threshold Setting",
            "Auto Ellipse",
            "Operator Master",
            "Part Master",
            "Copy Channel-1 Configuration",
            "Batch Wise Log",
            "Serial Number Log"
        };

        private readonly ICommand _command;

        public MenuItemViewModel()
        {
            _command = new CommandViewModel(Execute);
        }

        public string Header { get; set; } = string.Empty;
        public Freq? freqPop { get; set; }
        string filename { get; set; } = string.Empty;
        public CircleSetting? ellipsesPop { get; set; }
        public MainWindow? mainWindow { get; set; }
        public ObservableCollection<MenuItemViewModel> MenuItems { get; set; } = new();
        public bool isRenewConfig => _8F.Services.ConfigKey.ProtectedSettings.IsReNewConfig;
        public ICommand Command
        {
            get
            {
                return _command;
            }
        }

        private void Execute()
        {
            if (mainWindow == null) return;

            if (ConfigurationMenuHeaders.Contains(Header))
            {
                if (mainWindow.isPasswordEnable)
                {
                    PasswordDialog passwordDlg = new PasswordDialog(CONFIG_MENU_PASSWORD)
                    {
                        Owner = mainWindow
                    };

                    bool? isAuth = passwordDlg.ShowDialog();
                    if (isAuth != true)
                    {
                        return;
                    }
                }
            }

            if (DeviceCOM.IsLogEnable)
            {
                MessageBox.Show("While logging you can not perform this command, please stop the log.", "Command Conflict");
            }
            else
            {
                if ((Header == "Open" || Header == "New" || Header == "Write Configuration") && DeviceCOM.IsSystemBusy)
                {
                    MessageBox.Show("System is busy so you can not perform this command, please wait...", "System Information");
                }
                else
                {
                    if (Header == "Change Configuration")
                    {
                        freqPop = new Freq();
                        freqPop.Closing += freqPop_Closing;
                        freqPop.portCOM = mainWindow.portCOM;
                        freqPop.Owner = mainWindow;
                        freqPop.ShowDialog();
                    }
                    else if (Header == "Threshold Setting")
                    {
                        ellipsesPop = new CircleSetting("D1");
                        ellipsesPop.Closing += ellipsesPop_Closing;
                        ellipsesPop.portCOM = mainWindow.portCOM;
                        ellipsesPop.Owner = mainWindow;
                        ellipsesPop.ShowDialog();
                    }
                    else if (Header == "Auto Ellipse")
                    {
                        var autoEllipsePop = new AutoEllipse();
                        autoEllipsePop.portCOM = mainWindow.portCOM;
                        autoEllipsePop.Owner = mainWindow;
                        autoEllipsePop.ShowDialog();
                    }
                    else if (Header == "Part Master")
                    {
                        PartFamilyMaster partMaster = new PartFamilyMaster();
                        partMaster.ShowDialog();
                    }
                    else if (Header == "Operator Master")
                    {
                        OperatorMaster operatorMaster = new OperatorMaster();
                        operatorMaster.ShowDialog();
                    }
                    else if (Header == "Write Configuration")
                    {
                        try
                        {
                            var msg = "Configuation Write successfully!!";
                            var rat = mainWindow.ImplementChanges(0);
                            if (!rat)
                            {
                                msg = "No response from the system, please reboot the ECT Instrument";
                            }

                            MessageBox.Show(msg, "Information");
                        }
                        catch (Exception)
                        {
                            MessageBox.Show("Error while writing the configuration!!!!", "Information");
                        }
                    }
                    else if (Header == "Copy Channel-1 Configuration")
                    {
                        var chNo1 = DeviceCOM.channelDatas.FirstOrDefault(c => c.Id == 1);
                        foreach (var ch in DeviceCOM.channelDatas)
                        {
                            if (ch.Id <= mainWindow.chNo && ch.Id != 1)
                            {
                                foreach (var item in ch.graphDatas)
                                {
                                    var freq = chNo1?.graphDatas.FirstOrDefault(g => g.Id == item.Id);
                                    if (freq != null)
                                    {
                                        item.freq = freq.freq;
                                        item.gain = freq.gain;
                                        item.phase = freq.phase;
                                        item.height = freq.height;
                                        item.width = freq.width;
                                        item.ex = freq.ex;
                                        item.ey = freq.ey;
                                        item.angel = freq.angel;
                                    }
                                }
                            }
                        }
                        var rat = mainWindow.ImplementChanges(0);
                        var msg = "Channel-1 Configuration copied to others successfully!!";
                        if (!rat)
                        {
                            msg = "No response from the system, please reboot the ECT Instrument";
                        }
                        MessageBox.Show(msg, "Information");

                    }
                    else if (Header == "Data Log")
                    {
                        //mainWindow.report = new Report();
                        //mainWindow.report.ShowDialog();

                        System.Diagnostics.Process.Start(new ProcessStartInfo
                        {
                            FileName = this.mainWindow.WebPage,
                            UseShellExecute = true
                        });

                    }
                    else if (Header == "Save")
                    {
                        try
                        {
                            if (mainWindow.isSaveWithDb)
                            {
                                SaveProfileDialog profileDlg = new SaveProfileDialog
                                {
                                    Owner = mainWindow
                                };

                                bool? result = profileDlg.ShowDialog();
                                if (result == true && !string.IsNullOrWhiteSpace(profileDlg.ProfileName))
                                {
                                    string profileName = profileDlg.ProfileName.Trim();
                                    var currentChannels = DeviceCOM.channelDatas;

                                    Task.Run(async () =>
                                    {
                                        try
                                        {
                                            _8F.Services.IConfigProfileRepository repo = new _8F.Services.InspectionLogRepository();
                                            await repo.SaveConfigProfileAsync(profileName, "Operator", currentChannels);

                                            mainWindow.Dispatcher.Invoke(() =>
                                            {
                                                mainWindow.lblConfigFileName.Content = profileName;
                                                MessageBox.Show($"Configuration Profile '{profileName}' saved to Database successfully!", "Database Save", MessageBoxButton.OK, MessageBoxImage.Information);
                                            });
                                        }
                                        catch (Exception dbEx)
                                        {
                                            mainWindow.Dispatcher.Invoke(() =>
                                            {
                                                MessageBox.Show($"Failed to save profile to database: {dbEx.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                            });
                                        }
                                    });
                                }
                            }
                            else if (mainWindow.isSaveWithFile)
                            {
                                if (String.IsNullOrEmpty(mainWindow.filename))
                                {
                                    Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                                    dlg.FileName = "Document";
                                    dlg.DefaultExt = ".txt";
                                    dlg.Filter = "Text documents (.txt)|*.txt";

                                    Nullable<bool> result = dlg.ShowDialog();
                                    if (result == true)
                                    {
                                        mainWindow.filename = dlg.FileName;
                                        string content = JsonConvert.SerializeObject(DeviceCOM.channelDatas);
                                        File.WriteAllText(mainWindow.filename, content);
                                        mainWindow.lblConfigFileName.Content = mainWindow.filename;
                                        MessageBox.Show("Configuration saved to file successfully!", "File Save", MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                }
                                else
                                {
                                    string content = JsonConvert.SerializeObject(DeviceCOM.channelDatas);
                                    File.WriteAllText(mainWindow.filename, content);
                                    mainWindow.lblConfigFileName.Content = mainWindow.filename;
                                    MessageBox.Show("Configuration saved to file successfully!", "File Save", MessageBoxButton.OK, MessageBoxImage.Information);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error while saving configuration: {ex.Message}", "Error Information", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else if (Header == "Save As")
                    {
                        try
                        {
                            if (mainWindow.isSaveAsWithDb)
                            {
                                SaveProfileDialog profileDlg = new SaveProfileDialog
                                {
                                    Owner = mainWindow
                                };

                                bool? result = profileDlg.ShowDialog();
                                if (result == true && !string.IsNullOrWhiteSpace(profileDlg.ProfileName))
                                {
                                    string profileName = profileDlg.ProfileName.Trim();
                                    var currentChannels = DeviceCOM.channelDatas;

                                    Task.Run(async () =>
                                    {
                                        try
                                        {
                                            _8F.Services.IConfigProfileRepository repo = new _8F.Services.InspectionLogRepository();
                                            await repo.SaveConfigProfileAsync(profileName, "Operator", currentChannels);

                                            mainWindow.Dispatcher.Invoke(() =>
                                            {
                                                mainWindow.lblConfigFileName.Content = profileName;
                                                MessageBox.Show($"Configuration Profile '{profileName}' saved to Database successfully!", "Database Save", MessageBoxButton.OK, MessageBoxImage.Information);
                                            });
                                        }
                                        catch (Exception dbEx)
                                        {
                                            mainWindow.Dispatcher.Invoke(() =>
                                            {
                                                MessageBox.Show($"Failed to save profile to database: {dbEx.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                            });
                                        }
                                    });
                                }
                            }
                            else if (mainWindow.isSaveAsWithFile)
                            {
                                Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                                dlg.FileName = "Document";
                                dlg.DefaultExt = ".txt";
                                dlg.Filter = "Text documents (.txt)|*.txt";

                                Nullable<bool> result = dlg.ShowDialog();
                                if (result == true)
                                {
                                    mainWindow.filename = dlg.FileName;
                                    string content = JsonConvert.SerializeObject(DeviceCOM.channelDatas);
                                    File.WriteAllText(mainWindow.filename, content);
                                    mainWindow.lblConfigFileName.Content = mainWindow.filename;
                                    MessageBox.Show("Configuration saved to file successfully!", "File Save", MessageBoxButton.OK, MessageBoxImage.Information);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error while saving configuration file: {ex.Message}", "Error Information", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else if (Header == "Open")
                    {
                        try
                        {
                            if (mainWindow.isOpenWithDb)
                            {
                                ExportProfilePickerWindow profilePicker = new ExportProfilePickerWindow
                                {
                                    Title = "Open Configuration Profile from Database",
                                    IsSelectionMode = true,
                                    Owner = mainWindow
                                };
                                profilePicker.ShowDialog();

                                if (profilePicker.SelectedProfileId > 0)
                                {
                                    int pId = profilePicker.SelectedProfileId;
                                    string pName = profilePicker.SelectedProfileName;
                                    Task.Run(async () =>
                                    {
                                        try
                                        {
                                            _8F.Services.IConfigProfileRepository repo = new _8F.Services.InspectionLogRepository();
                                            var dbChannels = await repo.GetConfigProfileAsync(pId);

                                            mainWindow.Dispatcher.Invoke(() =>
                                            {
                                                if (dbChannels != null && dbChannels.Count > 0)
                                                {
                                                    ApplyChannelDataWithMapping(dbChannels, $"DB: {pName}");
                                                }
                                                else
                                                {
                                                    MessageBox.Show("Selected database profile contains no channel data.", "Open Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                                                }
                                            });
                                        }
                                        catch (Exception dbEx)
                                        {
                                            mainWindow.Dispatcher.Invoke(() =>
                                            {
                                                MessageBox.Show($"Error loading profile from database: {dbEx.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                            });
                                        }
                                    });
                                }
                            }
                            else if (mainWindow.isOpenWithFile)
                            {
                                var dialog = new Microsoft.Win32.OpenFileDialog();
                                dialog.Title = "Open Configuration File";
                                dialog.FileName = "Document";
                                dialog.DefaultExt = ".txt";
                                dialog.Filter = "JSON / Text documents (*.json;*.txt)|*.json;*.txt|All Files (*.*)|*.*";

                                bool? result = dialog.ShowDialog();
                                if (result == true)
                                {
                                    string data = File.ReadAllText(dialog.FileName);
                                    List<ChannelData>? parsedChData = _8F.Services.ConfigurationImporter.ImportFromJson(data);

                                    if (parsedChData != null && parsedChData.Count > 0)
                                    {
                                        ApplyChannelDataWithMapping(parsedChData, dialog.FileName);
                                    }
                                    else
                                    {
                                        MessageBox.Show("Failed to parse valid configuration data from the selected file.", "Open Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error accessing configuration profiles: {ex.Message}", "Open Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else if (Header == "Import Configuration")
                    {
                        try
                        {
                            if (mainWindow.isImportConfigWithFile)
                            {
                                var dialog = new Microsoft.Win32.OpenFileDialog();
                                dialog.Title = "Import Configuration File to Database";
                                dialog.FileName = "Document";
                                dialog.DefaultExt = ".txt";
                                dialog.Filter = "JSON / Text documents (*.json;*.txt)|*.json;*.txt|All Files (*.*)|*.*";

                                bool? result = dialog.ShowDialog();
                                if (result == true)
                                {
                                    string data = File.ReadAllText(dialog.FileName);
                                    List<ChannelData>? parsedChData = _8F.Services.ConfigurationImporter.ImportFromJson(data);

                                    if (parsedChData != null && parsedChData.Count > 0)
                                    {
                                        string profileName = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);

                                        Task.Run(async () =>
                                        {
                                            try
                                            {
                                                _8F.Services.IConfigProfileRepository repo = new _8F.Services.InspectionLogRepository();
                                                await repo.SaveConfigProfileAsync(profileName, "Operator", parsedChData);

                                                mainWindow.Dispatcher.Invoke(() =>
                                                {
                                                    ApplyChannelDataWithMapping(parsedChData, dialog.FileName);
                                                    mainWindow.lblConfigFileName.Content = profileName;
                                                    MessageBox.Show($"Configuration imported from '{System.IO.Path.GetFileName(dialog.FileName)}' and saved to Database as '{profileName}'!", "Import to Database", MessageBoxButton.OK, MessageBoxImage.Information);
                                                });
                                            }
                                            catch (Exception dbEx)
                                            {
                                                mainWindow.Dispatcher.Invoke(() =>
                                                {
                                                    ApplyChannelDataWithMapping(parsedChData, dialog.FileName);
                                                    MessageBox.Show($"Configuration loaded from file, but failed to save profile to database: {dbEx.Message}", "Database Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                                                });
                                            }
                                        });
                                    }
                                    else
                                    {
                                        MessageBox.Show("Failed to parse valid configuration data from the selected file.", "Import Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                                    }
                                }
                            }
                            else if (mainWindow.isImportConfigWithDb)
                            {
                                ExportProfilePickerWindow profilePicker = new ExportProfilePickerWindow
                                {
                                    Title = "Import Configuration Profile from Database",
                                    IsSelectionMode = true,
                                    Owner = mainWindow
                                };
                                profilePicker.ShowDialog();

                                if (profilePicker.SelectedProfileId > 0)
                                {
                                    int pId = profilePicker.SelectedProfileId;
                                    string pName = profilePicker.SelectedProfileName;
                                    Task.Run(async () =>
                                    {
                                        try
                                        {
                                            _8F.Services.IConfigProfileRepository repo = new _8F.Services.InspectionLogRepository();
                                            var dbChannels = await repo.GetConfigProfileAsync(pId);

                                            mainWindow.Dispatcher.Invoke(() =>
                                            {
                                                if (dbChannels != null && dbChannels.Count > 0)
                                                {
                                                    ApplyChannelDataWithMapping(dbChannels, $"DB: {pName}");
                                                }
                                                else
                                                {
                                                    MessageBox.Show("Selected database profile contains no channel data.", "Import Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                                                }
                                            });
                                        }
                                        catch (Exception dbEx)
                                        {
                                            mainWindow.Dispatcher.Invoke(() =>
                                            {
                                                MessageBox.Show($"Error loading profile from database: {dbEx.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                            });
                                        }
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error while importing configuration: {ex.Message}", "Error Information", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else if (Header == "Export Configuration")
                    {
                        try
                        {
                            if (mainWindow.isExportConfigWithDb)
                            {
                                ExportProfilePickerWindow exportPicker = new ExportProfilePickerWindow
                                {
                                    Owner = mainWindow
                                };
                                exportPicker.ShowDialog();
                            }
                            else if (mainWindow.isExportConfigWithFile)
                            {
                                Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                                dlg.FileName = "ExportedConfiguration";
                                dlg.DefaultExt = ".json";
                                dlg.Filter = "JSON documents (*.json)|*.json|Text documents (*.txt)|*.txt";

                                bool? result = dlg.ShowDialog();
                                if (result == true)
                                {
                                    string json = JsonConvert.SerializeObject(DeviceCOM.channelDatas, Formatting.Indented);
                                    File.WriteAllText(dlg.FileName, json);
                                    MessageBox.Show($"Configuration exported to file '{dlg.FileName}' successfully!", "Export Configuration", MessageBoxButton.OK, MessageBoxImage.Information);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error while exporting configuration: {ex.Message}", "Error Information", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else if (Header == "New")
                    {
                        mainWindow.filename = string.Empty;
                        mainWindow.InitialGraphData(false);
                        mainWindow.ClearGraphData();
                        var rat = mainWindow.ImplementChanges(0);
                        if (!rat)
                        {
                            var msg = "No response from the system, please reboot the ECT Instrument";
                            MessageBox.Show(msg, "Information");
                        }
                        DeviceCOM.IsLogEnable = false;
                        this.mainWindow.lblLog.Content = "Start Log";
                        this.mainWindow.lblLog1.Content = "Start Log";
                        this.mainWindow.lblLog2.Content = "Start Log";
                        DeviceCOM.part = new Part();
                        this.mainWindow.lblPartLogs.Content = "";
                        this.mainWindow.lblConfigFileName.Content = "";
                        //this.mainWindow.btnLog.Visibility = Visibility.Hidden;
                    }
                    else if (Header == "Exit")
                    {
                        //this.mainWindow.btnLog.Visibility = Visibility.Hidden;
                        mainWindow.Close();
                    }
                    else if (Header == "Batch Wise Log")
                    {
                        if (isRenewConfig)
                        {
                            RenewBatchWiseLog renewLog = new RenewBatchWiseLog();
                            renewLog.ShowDialog();
                        }
                        else
                        {
                            Logs logs = new Logs();
                            logs.ShowDialog();
                        }
                    }
                    else if (Header == "Serial Number Log")
                    {
                        LogAll logs = new LogAll();
                        logs.ShowDialog();
                    }
                    else if (Header == "Modbus Diagnostic Panel")
                    {
                        if (mainWindow?.modbusSlaveService != null)
                        {
                            var panel = new _8F.Views.ModbusTestPanel(mainWindow.modbusSlaveService)
                            {
                                Owner = mainWindow
                            };
                            panel.ShowDialog();
                        }
                    }
                }
            }
        }

        private void ApplyChannelDataWithMapping(List<ChannelData> incoming, string sourceName)
        {
            if (mainWindow == null || incoming == null || incoming.Count == 0) return;

            // Commented out remapping popup window per user request - apply direct 1-to-1 mapping for all channels
            /*
            string displayName = System.IO.Path.GetFileName(sourceName);
            ChannelRemappingWindow remapWin = new ChannelRemappingWindow(incoming, displayName)
            {
                Owner = mainWindow
            };
            remapWin.ShowDialog();

            if (!remapWin.IsConfirmed)
            {
                return; // User cancelled mapping
            }

            var mappedChannels = _8F.Services.ConfigurationImporter.ApplyRemapping(incoming, remapWin.TargetMappings, remapWin.IsImportAsIs);
            */

            var mappedChannels = _8F.Services.ConfigurationImporter.ApplyRemapping(incoming, new Dictionary<int, List<int>>(), isImportAsIs: true);

            if (mappedChannels != null)
            {
                DeviceCOM.channelDatas = mappedChannels;
            }

            mainWindow.filename = sourceName;
            mainWindow.SelectCh1();
            mainWindow.ClearGraphData();

            var rat = mainWindow.ImplementChanges(0);
            if (!rat)
            {
                MessageBox.Show("Configuration loaded into application, but no response from ECT instrument. Please check connection.", "Instrument Notice", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            mainWindow.lblConfigFileName.Content = sourceName;
        }


        private void freqPop_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (freqPop != null && freqPop.IsSaved && mainWindow != null)
            {
                mainWindow.ImplementChanges(1);
            }
        }

        private void ellipsesPop_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (ellipsesPop != null && ellipsesPop.IsSaved && mainWindow != null)
            {
                mainWindow.ImplementChanges(2);
            }
        }
    }

}
