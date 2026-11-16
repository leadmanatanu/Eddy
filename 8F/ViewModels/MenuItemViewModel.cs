using _8F.Models;
using _8F.Services;
using _8F.Repositories;
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
using _8F.Auth;
using _8F.Services.Interfaces;
using _8F.Views;

namespace _8F.ViewModels
{
    public class MenuItemViewModel
    {
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
        private bool _isFileOpBusy;

        public MenuItemViewModel()
        {
            _command = new CommandViewModel(Execute);
        }

        public string Header { get; set; } = string.Empty;
        public Freq? freqPop { get; set; }
        string filename { get; set; } = string.Empty;
        public CircleSetting? ellipsesPop { get; set; }
        public MainWindow? mainWindow { get; set; }
        private ConfigSnapshot? _menuBeforeSnap;
        public ObservableCollection<MenuItemViewModel> MenuItems { get; set; } = new();
        public bool isRenewConfig = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["isrenewconfig"]);
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

            // 1. Session and Auth Action Handlers
            if (Header == "Logout")
            {
                mainWindow.PerformLogout();
                return;
            }

            if (Header == "Change Password")
            {
                mainWindow.ShowChangePasswordDialog();
                return;
            }

            // 2. Permission Verification (Execution Layer Enforcement)
            if (ConfigurationMenuHeaders.Contains(Header))
            {
                if (!AppServices.AuthService.Can(Permission.ConfigurationMenu))
                {
                    MessageBox.Show("Admin access required for configuration menu items.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else if (Header == "Write Configuration")
            {
                if (!AppServices.AuthService.Can(Permission.WriteConfiguration))
                {
                    MessageBox.Show("Admin access required to write configuration.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else if (Header == "New" || Header == "Open" || Header == "Save" || Header == "Save As" || Header == "Import Configuration" || Header == "Export Configuration")
            {
                if (!AppServices.AuthService.Can(Permission.FileOperations))
                {
                    MessageBox.Show("Admin access required for file configuration operations.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else if (Header == "Modbus Diagnostic Panel")
            {
                if (!AppServices.AuthService.Can(Permission.ModbusDiagnosticPanel))
                {
                    MessageBox.Show("Admin access required for Modbus diagnostic panel.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
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
                        _menuBeforeSnap = ConfigSnapshotService.TakeSnapshot();
                        freqPop = new Freq();
                        freqPop.Closing += freqPop_Closing;
                        freqPop.portCOM = mainWindow.portCOM;
                        freqPop.Owner = mainWindow;
                        freqPop.ShowDialog();
                    }
                    else if (Header == "Threshold Setting")
                    {
                        _menuBeforeSnap = ConfigSnapshotService.TakeSnapshot();
                        ellipsesPop = new CircleSetting("D1");
                        ellipsesPop.Closing += ellipsesPop_Closing;
                        ellipsesPop.portCOM = mainWindow.portCOM;
                        ellipsesPop.Owner = mainWindow;
                        ellipsesPop.ShowDialog();
                    }
                    else if (Header == "Auto Ellipse")
                    {
                        var snapBefore = ConfigSnapshotService.TakeSnapshot();
                        var autoEllipsePop = new AutoEllipse();
                        autoEllipsePop.portCOM = mainWindow.portCOM;
                        autoEllipsePop.Owner = mainWindow;
                        autoEllipsePop.Closing += (s, ev) =>
                        {
                            var snapAfter = ConfigSnapshotService.TakeSnapshot();
                            var diff = ConfigSnapshotService.Compare(snapBefore, snapAfter);
                            if (diff != null)
                            {
                                _ = AppServices.ConfigChangeRepository.RecordChangeAsync("Auto Ellipse", $"CH{mainWindow.chNo}", diff.Value.Summary, diff.Value.DetailsJson);
                            }
                        };
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
                            else
                            {
                                _ = AppServices.ConfigChangeRepository.RecordChangeAsync("Write Configuration", $"CH{mainWindow.chNo}", $"Configuration written to ECT instrument for Channel {mainWindow.chNo}");
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
                        var snapBefore = ConfigSnapshotService.TakeSnapshot();
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
                                        item.height_O = freq.height_O;
                                        item.width_O = freq.width_O;
                                        item.ex_O = freq.ex_O;
                                        item.ey_O = freq.ey_O;
                                        item.angel_O = freq.angel_O;
                                        item.isEnable = freq.isEnable;
                                        if (freq.ellipses != null)
                                        {
                                            item.ellipses = freq.ellipses.Select(e => new Ellips
                                            {
                                                Id = e.Id,
                                                height = e.height,
                                                width = e.width,
                                                ex = e.ex,
                                                ey = e.ey,
                                                angel = e.angel
                                            }).ToList();
                                        }
                                    }
                                }
                            }
                        }
                        var rat = mainWindow.ImplementChanges(0);
                        var msg = "Channel-1 Configuration copied to others successfully!!";
                        if (!rat && mainWindow.portCOM != null)
                        {
                            msg = "No response from the system, please reboot the ECT Instrument";
                        }
                        MessageBox.Show(msg, "Information");

                    }
                    else if (Header == "Data Log")
                    {
                        System.Diagnostics.Process.Start(new ProcessStartInfo
                        {
                            FileName = this.mainWindow.WebPage,
                            UseShellExecute = true
                        });

                    }
                    else if (Header == "Save")
                    {
                        _ = RunGuardedFileOpAsync(ExecuteSaveAsync, "Error while saving configuration");
                    }
                    else if (Header == "Save As")
                    {
                        _ = RunGuardedFileOpAsync(() => SaveNewProfileAsync(isSaveAsAction: true), "Error while saving configuration");
                    }
                    else if (Header == "Open")
                    {
                        _ = RunGuardedFileOpAsync(ExecuteOpenAsync, "Error accessing configuration profiles");
                    }
                    else if (Header == "Import Configuration")
                    {
                        _ = RunGuardedFileOpAsync(ExecuteImportAsync, "Error while importing configuration");
                    }
                    else if (Header == "Export Configuration")
                    {
                        ExecuteExport();
                    }
                    else if (Header == "New")
                    {
                        mainWindow.ClearOpenProfileState();
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
                    }
                    else if (Header == "Exit")
                    {
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

        private async Task RunGuardedFileOpAsync(Func<Task> action, string errorPrefix)
        {
            if (_isFileOpBusy || mainWindow == null) return;
            _isFileOpBusy = true;
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                MessageBox.Show(mainWindow, $"{errorPrefix}: {ex.Message}", "Error Information", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isFileOpBusy = false;
            }
        }

        private async Task ExecuteSaveAsync()
        {
            if (mainWindow == null) return;

            bool saveDb = mainWindow.isSaveWithDb;
            bool saveFile = mainWindow.isSaveWithFile;
            if (!saveDb && !saveFile) return;

            int selectedChannel = mainWindow.GetSelectedChannelNumber();
            int selectedIdx = mainWindow.GetSelectedChannelIndex();
            var chProfile = mainWindow.CurrentProfile.GetForChannel(selectedChannel);

            int? currentId = chProfile?.ProfileId ?? mainWindow.CurrentProfile.ProfileId;
            string? targetFilePath = chProfile?.FilePath ?? mainWindow.CurrentProfile.FilePath;
            string currentName = chProfile?.Name ?? mainWindow.CurrentProfile.Name
                ?? (!string.IsNullOrWhiteSpace(targetFilePath)
                    ? System.IO.Path.GetFileNameWithoutExtension(targetFilePath)
                    : "Profile");
            int? sourceChannelId = chProfile?.SourceChannelId ?? mainWindow.CurrentProfile.SourceChannelId;

            bool isDbOpen = currentId.HasValue && currentId.Value > 0;
            bool isFileOpen = !string.IsNullOrWhiteSpace(targetFilePath);

            if (!isDbOpen && !isFileOpen)
            {
                await SaveNewProfileAsync(isSaveAsAction: false);
                return;
            }

            var selectedCh = DeviceCOM.channelDatas != null && selectedIdx >= 0 && selectedIdx < DeviceCOM.channelDatas.Count
                ? DeviceCOM.channelDatas[selectedIdx]
                : null;
            if (selectedCh == null) return;

            var cloned = ConfigurationImporter.CloneChannel(selectedCh);
            cloned.Id = selectedChannel;
            var systemSettings = mainWindow.GetCurrentSystemSettings();

            IConfigProfileRepository repo = new InspectionLogRepository();

            if (isDbOpen && saveDb)
            {
                int openId = currentId!.Value;
                List<ChannelData> dbChannelsToSave;
                var (existingDbChannels, _) = await repo.GetFullConfigProfileAsync(openId);
                if (existingDbChannels != null && existingDbChannels.Count > 0)
                {
                    dbChannelsToSave = existingDbChannels;
                    int replaceIdx = -1;
                    if (sourceChannelId.HasValue)
                    {
                        replaceIdx = dbChannelsToSave.FindIndex(c => c.Id == sourceChannelId.Value);
                    }
                    if (replaceIdx < 0 && dbChannelsToSave.Count == 1)
                    {
                        replaceIdx = 0;
                    }
                    if (replaceIdx < 0)
                    {
                        replaceIdx = dbChannelsToSave.FindIndex(c => c.Id == selectedChannel);
                    }

                    if (replaceIdx >= 0)
                    {
                        cloned.Id = dbChannelsToSave[replaceIdx].Id;
                        dbChannelsToSave[replaceIdx] = cloned;
                    }
                    else
                    {
                        cloned.Id = selectedChannel;
                        dbChannelsToSave.Add(cloned);
                    }
                }
                else
                {
                    cloned.Id = sourceChannelId ?? selectedChannel;
                    dbChannelsToSave = new List<ChannelData> { cloned };
                }

                bool updated = await repo.UpdateConfigProfileAsync(openId, currentName, dbChannelsToSave, systemSettings);
                if (!updated)
                {
                    mainWindow.ClearOpenProfileState();
                    MessageBox.Show(
                        mainWindow,
                        $"Configuration profile '{currentName}' (ID: {openId}) no longer exists in the database.\nUse 'Save' or 'Save As' to save it as a new profile.",
                        "Profile Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                await AppServices.ConfigChangeRepository.RecordChangeAsync(
                    "Save profile",
                    $"Channel {selectedChannel}",
                    $"Saved profile '{currentName}' (ID: {openId})");
            }

            if (saveFile)
            {
                if (isFileOpen)
                {
                    List<ChannelData> fileChannels;
                    if (File.Exists(targetFilePath))
                    {
                        try
                        {
                            string existingJson = File.ReadAllText(targetFilePath!);
                            var (existingChannels, _, _) = ConfigurationImporter.ImportProfileFromJson(existingJson);
                            if (existingChannels != null && existingChannels.Count > 0)
                            {
                                fileChannels = existingChannels;
                                int replaceIdx = -1;
                                if (sourceChannelId.HasValue)
                                {
                                    replaceIdx = fileChannels.FindIndex(c => c.Id == sourceChannelId.Value);
                                }
                                if (replaceIdx < 0 && fileChannels.Count == 1)
                                {
                                    replaceIdx = 0;
                                }
                                if (replaceIdx < 0)
                                {
                                    replaceIdx = fileChannels.FindIndex(c => c.Id == selectedChannel);
                                }

                                var fileCloned = ConfigurationImporter.CloneChannel(selectedCh);
                                if (replaceIdx >= 0)
                                {
                                    fileCloned.Id = fileChannels[replaceIdx].Id;
                                    fileChannels[replaceIdx] = fileCloned;
                                }
                                else
                                {
                                    fileCloned.Id = selectedChannel;
                                    fileChannels.Add(fileCloned);
                                }
                            }
                            else
                            {
                                var fileCloned = ConfigurationImporter.CloneChannel(selectedCh);
                                fileCloned.Id = sourceChannelId ?? selectedChannel;
                                fileChannels = new List<ChannelData> { fileCloned };
                            }
                        }
                        catch
                        {
                            var fileCloned = ConfigurationImporter.CloneChannel(selectedCh);
                            fileCloned.Id = sourceChannelId ?? selectedChannel;
                            fileChannels = new List<ChannelData> { fileCloned };
                        }
                    }
                    else
                    {
                        var fileCloned = ConfigurationImporter.CloneChannel(selectedCh);
                        fileCloned.Id = sourceChannelId ?? selectedChannel;
                        fileChannels = new List<ChannelData> { fileCloned };
                    }

                    var container = new ProfileConfigFile
                    {
                        Version = 2,
                        ProfileName = currentName,
                        SystemConfig = systemSettings,
                        Channels = fileChannels
                    };
                    File.WriteAllText(targetFilePath!, JsonConvert.SerializeObject(container, Formatting.Indented));
                }
                else if (string.IsNullOrWhiteSpace(targetFilePath))
                {
                    targetFilePath = PromptSaveFilePath(currentName);
                    if (!string.IsNullOrWhiteSpace(targetFilePath))
                    {
                        var fileCloned = ConfigurationImporter.CloneChannel(selectedCh);
                        fileCloned.Id = sourceChannelId ?? selectedChannel;
                        var container = new ProfileConfigFile
                        {
                            Version = 2,
                            ProfileName = currentName,
                            SystemConfig = systemSettings,
                            Channels = new List<ChannelData> { fileCloned }
                        };
                        File.WriteAllText(targetFilePath, JsonConvert.SerializeObject(container, Formatting.Indented));
                    }
                }
            }

            mainWindow.CurrentProfile.SetForChannel(selectedChannel, currentId, currentName, targetFilePath, sourceChannelId);
            if (selectedIdx >= 0 && selectedIdx < mainWindow._loadedChannels.Length)
            {
                mainWindow._loadedChannels[selectedIdx].ProfileId = (ushort)(currentId ?? 0);
                mainWindow._loadedChannels[selectedIdx].ProfileName = currentName;
            }
            mainWindow.UpdateConfigProfileDisplay();

            _8F.Services.Implementations.ModbusLogger.Info($"[Config Save] Saved profile '{currentName}' (ID: {currentId}) for Channel {selectedChannel}.");

            MessageBox.Show(
                mainWindow,
                $"Configuration profile '{currentName}' saved successfully for Channel {selectedChannel}!",
                "Save Configuration",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private async Task SaveNewProfileAsync(bool isSaveAsAction)
        {
            if (mainWindow == null) return;

            bool saveDb = isSaveAsAction ? mainWindow.isSaveAsWithDb : mainWindow.isSaveWithDb;
            bool saveFile = isSaveAsAction ? mainWindow.isSaveAsWithFile : mainWindow.isSaveWithFile;
            if (!saveDb && !saveFile) return;

            int selectedChannel = mainWindow.GetSelectedChannelNumber();
            int selectedIdx = mainWindow.GetSelectedChannelIndex();

            var selectedCh = DeviceCOM.channelDatas != null && selectedIdx >= 0 && selectedIdx < DeviceCOM.channelDatas.Count
                ? DeviceCOM.channelDatas[selectedIdx]
                : null;
            if (selectedCh == null) return;

            var cloned = ConfigurationImporter.CloneChannel(selectedCh);
            cloned.Id = selectedChannel;
            var channelsToSave = new List<ChannelData> { cloned };
            var systemSettings = mainWindow.GetCurrentSystemSettings();

            IConfigProfileRepository repo = new InspectionLogRepository();

            var chProfile = mainWindow.CurrentProfile.GetForChannel(selectedChannel);
            string defaultName = !string.IsNullOrWhiteSpace(chProfile?.Name)
                ? chProfile.Name
                : (!string.IsNullOrWhiteSpace(chProfile?.FilePath)
                    ? System.IO.Path.GetFileNameWithoutExtension(chProfile.FilePath)
                    : (!string.IsNullOrWhiteSpace(mainWindow.CurrentProfile.Name)
                        ? mainWindow.CurrentProfile.Name
                        : string.Empty));
            string profileName = string.Empty;
            int? overwriteProfileId = null;

            string dlgTitle = isSaveAsAction ? "Save Configuration Profile As" : "Save Configuration Profile";
            while (true)
            {
                var profileDlg = new SaveProfileDialog(defaultName, dlgTitle)
                {
                    Owner = mainWindow
                };

                if (profileDlg.ShowDialog() != true)
                {
                    return;
                }

                profileName = profileDlg.ProfileName;

                if (saveDb)
                {
                    var existing = await repo.GetConfigProfileByNameAsync(profileName);
                    if (existing != null)
                    {
                        var choice = MessageBox.Show(
                            mainWindow,
                            $"A profile named '{existing.Name}' already exists.\n\nClick 'Yes' to overwrite it, 'No' to choose another name, or 'Cancel' to abort.",
                            "Profile Already Exists",
                            MessageBoxButton.YesNoCancel,
                            MessageBoxImage.Question,
                            MessageBoxResult.No);

                        if (choice == MessageBoxResult.Yes)
                        {
                            overwriteProfileId = existing.Id;
                            profileName = existing.Name;
                            break;
                        }

                        if (choice == MessageBoxResult.Cancel)
                        {
                            return;
                        }

                        defaultName = profileName;
                        continue;
                    }
                }

                overwriteProfileId = null;
                break;
            }

            string? targetFilePath = null;
            if (saveFile)
            {
                string suggestedFile = !string.IsNullOrWhiteSpace(profileName)
                    ? profileName
                    : (!string.IsNullOrWhiteSpace(defaultName) ? defaultName : "Document");
                targetFilePath = PromptSaveFilePath(suggestedFile);
                if (string.IsNullOrWhiteSpace(targetFilePath))
                {
                    return;
                }
            }

            int? savedId = null;

            if (saveDb)
            {
                await PersistFileAndDbAsync(targetFilePath, saveFile, profileName, systemSettings, channelsToSave, async () =>
                {
                    if (overwriteProfileId.HasValue)
                    {
                        bool updated = await repo.UpdateConfigProfileAsync(overwriteProfileId.Value, profileName, channelsToSave, systemSettings);
                        if (updated)
                        {
                            savedId = overwriteProfileId.Value;
                            return true;
                        }
                    }

                    savedId = await repo.SaveConfigProfileAsync(profileName, "Operator", channelsToSave, systemSettings);
                    return true;
                });
            }
            else if (saveFile && !string.IsNullOrWhiteSpace(targetFilePath))
            {
                var container = new ProfileConfigFile
                {
                    Version = 2,
                    ProfileName = profileName,
                    SystemConfig = systemSettings,
                    Channels = channelsToSave
                };
                File.WriteAllText(targetFilePath, JsonConvert.SerializeObject(container, Formatting.Indented));
            }

            mainWindow.CurrentProfile.SetForChannel(selectedChannel, savedId, profileName, targetFilePath);
            if (selectedIdx >= 0 && selectedIdx < mainWindow._loadedChannels.Length)
            {
                mainWindow._loadedChannels[selectedIdx].ProfileId = (ushort)(savedId ?? 0);
                mainWindow._loadedChannels[selectedIdx].ProfileName = profileName;
            }
            mainWindow.UpdateConfigProfileDisplay();

            if (saveDb)
            {
                string actionArea = isSaveAsAction ? "Save As profile" : "Save profile";
                string verb = overwriteProfileId.HasValue ? "Overwrote" : "Saved";
                await AppServices.ConfigChangeRepository.RecordChangeAsync(
                    actionArea,
                    $"Channel {selectedChannel}",
                    $"{verb} profile '{profileName}'" + (savedId > 0 ? $" (ID: {savedId})" : ""));
            }

            _8F.Services.Implementations.ModbusLogger.Info($"[Config Save] {(isSaveAsAction ? "Save As" : "Saved")} profile '{profileName}' (ID: {savedId}) for Channel {selectedChannel}.");

            MessageBox.Show(
                mainWindow,
                $"Configuration profile '{profileName}' saved successfully for Channel {selectedChannel}!",
                isSaveAsAction ? "Save As Configuration" : "Save Configuration",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private static async Task<bool> PersistFileAndDbAsync(
            string? targetFilePath,
            bool saveFile,
            string? profileName,
            SystemSettings systemSettings,
            List<ChannelData> channels,
            Func<Task<bool>> dbOperation)
        {
            if (!saveFile || string.IsNullOrWhiteSpace(targetFilePath))
            {
                return await dbOperation();
            }

            bool existed = File.Exists(targetFilePath);
            byte[]? backup = existed ? File.ReadAllBytes(targetFilePath) : null;
            var container = new ProfileConfigFile
            {
                Version = 2,
                ProfileName = profileName,
                SystemConfig = systemSettings,
                Channels = channels
            };
            File.WriteAllText(targetFilePath, JsonConvert.SerializeObject(container, Formatting.Indented));

            try
            {
                bool ok = await dbOperation();
                if (!ok)
                {
                    RestoreFile(targetFilePath, existed, backup);
                }
                return ok;
            }
            catch
            {
                RestoreFile(targetFilePath, existed, backup);
                throw;
            }
        }

        private static void RestoreFile(string path, bool existed, byte[]? backup)
        {
            try
            {
                if (existed && backup != null)
                {
                    File.WriteAllBytes(path, backup);
                }
                else if (!existed && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Best-effort file rollback
            }
        }

        private string? PromptSaveFilePath(string defaultFileName)
        {
            if (mainWindow == null) return null;

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = string.IsNullOrWhiteSpace(defaultFileName) ? "Document" : defaultFileName,
                DefaultExt = ".txt",
                Filter = "Text documents (.txt)|*.txt|JSON documents (*.json)|*.json|All Files (*.*)|*.*"
            };

            return dlg.ShowDialog(mainWindow) == true ? dlg.FileName : null;
        }

        private void UpdateOpenProfileUiState(int? profileId, string profileName, string? filePath, bool isDbBacked, int? sourceChannelId = null)
        {
            if (mainWindow == null) return;

            int targetChannel = mainWindow.GetSelectedChannelNumber();
            int? srcCh = sourceChannelId ?? mainWindow.CurrentProfile.GetForChannel(targetChannel)?.SourceChannelId;
            mainWindow.CurrentProfile.SetForChannel(targetChannel, profileId, profileName, filePath, srcCh);
            mainWindow.UpdateConfigProfileDisplay();
        }

        private async Task ExecuteOpenAsync()
        {
            if (mainWindow == null) return;

            if (mainWindow.isOpenWithDb && mainWindow.isOpenWithFile)
            {
                var sourceDlg = new OpenSourceSelectionDialog { Owner = mainWindow };
                if (sourceDlg.ShowDialog() != true) return;

                if (sourceDlg.SelectedSource == OpenSourceType.Database)
                {
                    await OpenProfileFromDbAsync();
                }
                else if (sourceDlg.SelectedSource == OpenSourceType.File)
                {
                    await OpenProfileFromFileAsync();
                }
            }
            else if (mainWindow.isOpenWithDb)
            {
                await OpenProfileFromDbAsync();
            }
            else if (mainWindow.isOpenWithFile)
            {
                await OpenProfileFromFileAsync();
            }
        }

        private async Task OpenProfileFromDbAsync()
        {
            if (mainWindow == null) return;

            ExportProfilePickerWindow profilePicker = new ExportProfilePickerWindow
            {
                Title = "Open Configuration Profile from Database",
                HeaderTitle = "Open Configuration Profile from Database",
                IsSelectionMode = true,
                AllowDelete = true,
                GetCurrentOpenProfileId = () => mainWindow.CurrentProfile.ProfileId,
                OnOpenProfileDeleted = () => mainWindow.ClearOpenProfileState(),
                Owner = mainWindow
            };
            profilePicker.ShowDialog();

            if (profilePicker.SelectedProfileId <= 0) return;

            int pId = profilePicker.SelectedProfileId;
            int targetChannel = mainWindow.GetSelectedChannelNumber();

            var result = await AppServices.ProfileLoadService.LoadFromDbAsync(
                pId,
                targetChannel,
                ProfileLoadSource.MenuOpenDb,
                isInteractive: false);

            if (result.Success)
            {
                UpdateOpenProfileUiState(pId, result.ProfileName, null, isDbBacked: true);
                mainWindow.modbusSlaveService?.WriteRegisterSilent(ModbusSlaveService.ChannelIndexToAddress(targetChannel - 1), (ushort)pId);
            }
            else
            {
                MessageBox.Show(mainWindow, result.Message, "Open Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task OpenProfileFromFileAsync()
        {
            if (mainWindow == null) return;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Open Configuration File",
                FileName = "Document",
                DefaultExt = ".txt",
                Filter = "JSON / Text documents (*.json;*.txt)|*.json;*.txt|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog(mainWindow) != true) return;

            string filePath = dialog.FileName;
            if (!File.Exists(filePath)) return;

            int targetChannel = mainWindow.GetSelectedChannelNumber();

            var result = await AppServices.ProfileLoadService.LoadFromFileAsync(
                filePath,
                targetChannel,
                ProfileLoadSource.MenuOpenFile,
                isInteractive: false);

            if (result.Success)
            {
                UpdateOpenProfileUiState(null, result.ProfileName, filePath, isDbBacked: false);
            }
            else
            {
                MessageBox.Show(mainWindow, result.Message, "Open Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task ExecuteImportAsync()
        {
            if (mainWindow == null) return;

            if (mainWindow.isImportConfigWithFile)
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Import Configuration File to Database",
                    FileName = "Document",
                    DefaultExt = ".txt",
                    Filter = "JSON / Text documents (*.json;*.txt)|*.json;*.txt|All Files (*.*)|*.*"
                };

                if (dialog.ShowDialog(mainWindow) == true)
                {
                    string data = File.ReadAllText(dialog.FileName);
                    var (parsedChData, sysSettings, profName) = ConfigurationImporter.ImportProfileFromJson(data);

                    if (parsedChData != null && parsedChData.Count > 0)
                    {
                        string profileName = !string.IsNullOrWhiteSpace(profName) ? profName : System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
                        int targetChannel = mainWindow.GetSelectedChannelNumber();

                        if (mainWindow.isImportConfigWithDb)
                        {
                            try
                            {
                                IConfigProfileRepository repo = new InspectionLogRepository();
                                int newId = await repo.SaveConfigProfileAsync(profileName, "Operator", parsedChData, sysSettings);

                                var result = await AppServices.ProfileLoadService.LoadFromChannelsAsync(
                                    parsedChData,
                                    sysSettings,
                                    profileName,
                                    (ushort)newId,
                                    dialog.FileName,
                                    targetChannel,
                                    _8F.Services.Interfaces.ProfileLoadSource.MenuImport,
                                    isInteractive: true);

                                if (result.Success)
                                {
                                    MessageBox.Show(mainWindow, $"Configuration imported from '{System.IO.Path.GetFileName(dialog.FileName)}' and saved to Database as '{profileName}'!", "Import to Database", MessageBoxButton.OK, MessageBoxImage.Information);
                                }
                                else
                                {
                                    MessageBox.Show(mainWindow, $"Configuration saved to Database as '{profileName}', but failed to load into Channel {targetChannel}: {result.Message}", "Import Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                                }
                            }
                            catch (Exception dbEx)
                            {
                                var result = await AppServices.ProfileLoadService.LoadFromChannelsAsync(
                                    parsedChData,
                                    sysSettings,
                                    profileName,
                                    null,
                                    dialog.FileName,
                                    targetChannel,
                                    _8F.Services.Interfaces.ProfileLoadSource.MenuImport,
                                    isInteractive: true);

                                MessageBox.Show(mainWindow, $"Configuration loaded into Channel {targetChannel}, but failed to save profile to database: {dbEx.Message}", "Database Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                        else
                        {
                            var result = await AppServices.ProfileLoadService.LoadFromChannelsAsync(
                                parsedChData,
                                sysSettings,
                                profileName,
                                null,
                                dialog.FileName,
                                targetChannel,
                                _8F.Services.Interfaces.ProfileLoadSource.MenuImport,
                                isInteractive: true);

                            if (result.Success)
                            {
                                MessageBox.Show(mainWindow, $"Configuration imported from '{System.IO.Path.GetFileName(dialog.FileName)}' successfully!", "Import Configuration", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else
                            {
                                MessageBox.Show(mainWindow, $"Failed to load imported configuration into Channel {targetChannel}: {result.Message}", "Import Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show(mainWindow, "Failed to parse valid configuration data from the selected file.", "Import Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            else if (mainWindow.isImportConfigWithDb)
            {
                await OpenOrImportProfileFromDbAsync("Import Configuration Profile from Database", "Import Error");
            }
        }

        private async Task OpenOrImportProfileFromDbAsync(string dialogTitle, string errorCaption)
        {
            if (mainWindow == null) return;

            ExportProfilePickerWindow profilePicker = new ExportProfilePickerWindow
            {
                Title = dialogTitle,
                HeaderTitle = dialogTitle,
                IsSelectionMode = true,
                AllowDelete = true,
                GetCurrentOpenProfileId = () => mainWindow.CurrentProfile.ProfileId,
                OnOpenProfileDeleted = () => mainWindow.ClearOpenProfileState(),
                Owner = mainWindow
            };
            profilePicker.ShowDialog();

            if (profilePicker.SelectedProfileId <= 0) return;

            int pId = profilePicker.SelectedProfileId;
            int targetChannel = mainWindow.GetSelectedChannelNumber();

            var result = await AppServices.ProfileLoadService.LoadFromDbAsync(
                pId,
                targetChannel,
                _8F.Services.Interfaces.ProfileLoadSource.MenuOpenDb,
                isInteractive: true);

            if (!result.Success)
            {
                MessageBox.Show(mainWindow, result.Message, errorCaption, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ExecuteExport()
        {
            if (mainWindow == null) return;

            try
            {
                if (mainWindow.isExportConfigWithDb)
                {
                    ExportProfilePickerWindow exportPicker = new ExportProfilePickerWindow
                    {
                        AllowDelete = false,
                        IsSelectionMode = false,
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
                        var container = new ProfileConfigFile
                        {
                            Version = 2,
                            ProfileName = mainWindow.CurrentProfile.Name,
                            SystemConfig = mainWindow.GetCurrentSystemSettings(),
                            Channels = DeviceCOM.channelDatas.Where(c => c.Id <= mainWindow.chNo).Select(c => ConfigurationImporter.CloneChannel(c)).ToList()
                        };
                        string json = JsonConvert.SerializeObject(container, Formatting.Indented);
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



        private void freqPop_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (freqPop != null && freqPop.IsSaved && mainWindow != null)
            {
                mainWindow.ImplementChanges(1);
                if (_menuBeforeSnap != null)
                {
                    var snapAfter = ConfigSnapshotService.TakeSnapshot();
                    var diff = ConfigSnapshotService.Compare(_menuBeforeSnap, snapAfter);
                    if (diff != null)
                    {
                        var activeCh = DeviceCOM.channelDatas.FirstOrDefault(c => c.IsSeleted);
                        int activeChNum = activeCh != null ? activeCh.Id : mainWindow.chNo;
                        if (mainWindow.isSaveWithDb || mainWindow.isOpenWithDb)
                        {
                            _ = AppServices.ConfigChangeRepository.RecordChangeAsync("Frequency", $"CH{activeChNum}", diff.Value.Summary, diff.Value.DetailsJson);
                        }
                    }
                    _menuBeforeSnap = null;
                }
            }
        }

        private void ellipsesPop_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (ellipsesPop != null && ellipsesPop.IsSaved && mainWindow != null)
            {
                mainWindow.ImplementChanges(2);
                if (_menuBeforeSnap != null)
                {
                    var snapAfter = ConfigSnapshotService.TakeSnapshot();
                    var diff = ConfigSnapshotService.Compare(_menuBeforeSnap, snapAfter);
                    if (diff != null)
                    {
                        var activeCh = DeviceCOM.channelDatas.FirstOrDefault(c => c.IsSeleted);
                        int activeChNum = activeCh != null ? activeCh.Id : 1;
                        if (mainWindow.isSaveWithDb || mainWindow.isOpenWithDb)
                        {
                            _ = AppServices.ConfigChangeRepository.RecordChangeAsync("Threshold", $"CH{activeChNum}", diff.Value.Summary, diff.Value.DetailsJson);
                        }
                    }
                    _menuBeforeSnap = null;
                }
            }
        }
    }
}
