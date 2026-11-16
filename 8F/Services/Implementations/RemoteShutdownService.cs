using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using _8F.Models;
using _8F.Repositories.Implementations;
using _8F.Repositories.Interfaces;
using _8F.ViewModels;
using _8F.Views;

namespace _8F.Services.Implementations
{
    public static class RemoteShutdownService
    {
        private static int _shutdownInProgress = 0;
        private static bool? _isRemoteShutdownEnableOverride;

        public static MainWindow? MainWindowInstance { get; set; }

        public static bool IsRemoteShutdownEnable
        {
            get
            {
                if (_isRemoteShutdownEnableOverride.HasValue)
                    return _isRemoteShutdownEnableOverride.Value;

                var val = System.Configuration.ConfigurationManager.AppSettings["IsRemoteShutdownEnable"];
                if (bool.TryParse(val, out bool enabled))
                    return enabled;
                return false;
            }
            set => _isRemoteShutdownEnableOverride = value;
        }

        public static int CountdownSeconds { get; set; } = 15;

        public static Action<string, string> ExecuteShutdownCommand { get; set; } = (cmd, args) =>
        {
            Process.Start(cmd, args);
        };

        public static Func<int, bool>? CountdownDialogFunc { get; set; }

        public static Action<string, string>? ShowErrorDialogAction { get; set; }

        public static Func<MainWindow?, Task>? CustomSaveProfileAction { get; set; }

        public static Func<MainWindow?, Task>? CustomSaveInspectionDataAction { get; set; }

        public static Func<string, List<ChannelData>, SystemSettings, Task>? DbSaveOperation { get; set; }

        public static Action<string, string> FileWriteOperation { get; set; } = (path, content) => File.WriteAllText(path, content);

        public static string? LastFallbackSavedFilePath { get; private set; }

        public static string? LastShutdownCommand { get; private set; }
        public static string? LastShutdownArgs { get; private set; }

        public static Task? CurrentShutdownTask { get; set; }

        public static void ResetState()
        {
            if (CurrentShutdownTask != null && !CurrentShutdownTask.IsCompleted)
            {
                try { CurrentShutdownTask.Wait(1000); } catch { }
            }
            CurrentShutdownTask = null;
            Interlocked.Exchange(ref _shutdownInProgress, 0);
            _isRemoteShutdownEnableOverride = null;
            MainWindowInstance = null;
            CountdownDialogFunc = null;
            ShowErrorDialogAction = null;
            CustomSaveProfileAction = null;
            CustomSaveInspectionDataAction = null;
            DbSaveOperation = null;
            FileWriteOperation = (path, content) => File.WriteAllText(path, content);
            LastFallbackSavedFilePath = null;
            LastShutdownCommand = null;
            LastShutdownArgs = null;
            CountdownSeconds = 15;
            ExecuteShutdownCommand = (cmd, args) =>
            {
                LastShutdownCommand = cmd;
                LastShutdownArgs = args;
            };
        }

        private static MainWindow? GetMainWindowSafely(MainWindow? mainWindow)
        {
            if (mainWindow != null) return mainWindow;
            if (MainWindowInstance != null) return MainWindowInstance;

            try
            {
                if (Application.Current != null)
                {
                    if (Application.Current.Dispatcher.CheckAccess())
                    {
                        return Application.Current.MainWindow as MainWindow;
                    }
                    return Application.Current.Dispatcher.Invoke(() => Application.Current.MainWindow as MainWindow);
                }
            }
            catch { }

            return null;
        }

        public static async Task<bool> HandleRemoteShutdownAsync(MainWindow? mainWindow = null)
        {
            var win = GetMainWindowSafely(mainWindow);

            if (Interlocked.CompareExchange(ref _shutdownInProgress, 1, 0) != 0)
            {
                ModbusLogger.Warn("[Remote Shutdown] Shutdown command received while shutdown is already in progress. Ignoring duplicate.");
                return false;
            }

            try
            {
                bool isEnabled = _isRemoteShutdownEnableOverride ?? (win != null ? win.isRemoteShutdownEnable : IsRemoteShutdownEnable);
                if (!isEnabled)
                {
                    ModbusLogger.Info("[Remote Shutdown] ignored: disabled");
                    return false;
                }

                ModbusLogger.Info($"Remote shutdown requested at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                var fileOpSw = Stopwatch.StartNew();
                while (MenuItemViewModel.IsFileOpInProgress && fileOpSw.ElapsedMilliseconds < 10000)
                {
                    await Task.Delay(100);
                }
                if (MenuItemViewModel.IsFileOpInProgress)
                {
                    ModbusLogger.Warn("[Remote Shutdown] Save/Import/Export operation in progress timed out after 10s. Aborting remote shutdown.");
                    return false;
                }

                if (DeviceCOM.IsSystemBusy || DeviceCOM.IsAutoEllipseActive)
                {
                    ModbusLogger.Info("[Remote Shutdown] Test/inspection is running. Waiting for completion...");
                    var sw = Stopwatch.StartNew();
                    while ((DeviceCOM.IsSystemBusy || DeviceCOM.IsAutoEllipseActive) && sw.ElapsedMilliseconds < 10000)
                    {
                        await Task.Delay(100);
                    }

                    if (DeviceCOM.IsSystemBusy || DeviceCOM.IsAutoEllipseActive)
                    {
                        ModbusLogger.Warn("[Remote Shutdown] Timed out waiting for test/inspection to complete after 10 seconds. Continuing shutdown sequence.");
                        DeviceCOM.IsSystemBusy = false;
                        DeviceCOM.IsAutoEllipseActive = false;
                    }
                    else
                    {
                        ModbusLogger.Info("[Remote Shutdown] Test/inspection completed cleanly.");
                    }
                }

                await Task.Delay(200);

                bool proceedWithShutdown = false;
                if (CountdownDialogFunc != null)
                {
                    proceedWithShutdown = CountdownDialogFunc(CountdownSeconds);
                }
                else if (Application.Current?.Dispatcher != null)
                {
                    proceedWithShutdown = await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var dlg = new RemoteShutdownCountdownWindow(CountdownSeconds)
                        {
                            Owner = win
                        };
                        bool? res = dlg.ShowDialog();
                        return res == true;
                    });
                }
                else
                {
                    proceedWithShutdown = true;
                }

                if (!proceedWithShutdown)
                {
                    ModbusLogger.Info("[Remote Shutdown] shutdown cancelled by user");
                    return false;
                }

                try
                {
                    if (CustomSaveProfileAction != null)
                    {
                        await CustomSaveProfileAction(win);
                    }
                    else
                    {
                        await SaveCurrentProfileAsync(win);
                    }

                    if (CustomSaveInspectionDataAction != null)
                    {
                        await CustomSaveInspectionDataAction(win);
                    }
                    else
                    {
                        await SaveInspectionTestDataAsync(win);
                    }

                    FlushLogs();
                }
                catch (Exception saveEx)
                {
                    ModbusLogger.Error($"[Remote Shutdown] Save step failed: {saveEx.Message}", saveEx);
                    ShowErrorToUser(win, $"Remote shutdown aborted due to save failure:\n\n{saveEx.Message}");
                    return false;
                }

                CloseHardware(win);

                try
                {
                    LastShutdownCommand = "shutdown";
                    LastShutdownArgs = "/s /t 5 /c \"8F remote shutdown\"";
                    ExecuteShutdownCommand("shutdown", "/s /t 5 /c \"8F remote shutdown\"");
                    return true;
                }
                catch (Exception procEx)
                {
                    ModbusLogger.Error($"[Remote Shutdown] Failed to execute shutdown command: {procEx.Message}", procEx);
                    ShowErrorToUser(win, $"PC shutdown command could not be executed:\n\n{procEx.Message}");
                    return false;
                }
            }
            finally
            {
                Interlocked.Exchange(ref _shutdownInProgress, 0);
            }
        }

        private static async Task SaveCurrentProfileAsync(MainWindow? win)
        {
            if (win == null) return;

            bool saveDb = win.isSaveWithDb;
            bool saveFile = win.isSaveWithFile;
            if (!saveDb && !saveFile) return;

            int selectedChannel = win.GetSelectedChannelNumber();
            int selectedIdx = win.GetSelectedChannelIndex();

            var selectedCh = DeviceCOM.channelDatas != null && selectedIdx >= 0 && selectedIdx < DeviceCOM.channelDatas.Count
                ? DeviceCOM.channelDatas[selectedIdx]
                : null;
            if (selectedCh == null) return;

            var cloned = ConfigurationImporter.CloneChannel(selectedCh);
            cloned.Id = selectedChannel;
            var channelsToSave = new List<ChannelData> { cloned };
            var systemSettings = win.GetCurrentSystemSettings();

            var chProfile = win.CurrentProfile.GetForChannel(selectedChannel);
            string profileName = !string.IsNullOrWhiteSpace(chProfile?.Name)
                ? chProfile.Name
                : (!string.IsNullOrWhiteSpace(win.CurrentProfile.Name)
                    ? win.CurrentProfile.Name
                    : $"AutoSave_{DateTime.Now:yyyyMMdd_HHmmss}");

            IConfigProfileRepository repo = new InspectionLogRepository();

            if (saveFile)
            {
                string targetPath = !string.IsNullOrWhiteSpace(chProfile?.FilePath)
                    ? chProfile.FilePath
                    : Path.Combine(GetSafeFallbackProfileDirectory(null), $"{profileName}.json");

                var container = new ProfileConfigFile
                {
                    Version = 2,
                    ProfileName = profileName,
                    SystemConfig = systemSettings,
                    Channels = channelsToSave
                };
                FileWriteOperation(targetPath, JsonConvert.SerializeObject(container, Formatting.Indented));
            }

            if (saveDb)
            {
                Task dbTask = DbSaveOperation != null
                    ? DbSaveOperation(profileName, channelsToSave, systemSettings)
                    : Task.Run(async () =>
                    {
                        if (chProfile?.ProfileId != null && chProfile.ProfileId.Value > 0)
                        {
                            await repo.UpdateConfigProfileAsync(chProfile.ProfileId.Value, profileName, channelsToSave, systemSettings);
                        }
                        else
                        {
                            var existing = await repo.GetConfigProfileByNameAsync(profileName);
                            if (existing != null)
                            {
                                await repo.UpdateConfigProfileAsync(existing.Id, profileName, channelsToSave, systemSettings);
                            }
                            else
                            {
                                await repo.SaveConfigProfileAsync(profileName, "Operator", channelsToSave, systemSettings);
                            }
                        }
                    });

                try
                {
                    await dbTask.WaitAsync(TimeSpan.FromSeconds(5));
                    ModbusLogger.Info($"[Remote Shutdown] Profile '{profileName}' saved to DB successfully.");
                }
                catch (Exception dbEx)
                {
                    ModbusLogger.Warn($"[Remote Shutdown] DB save failed ({dbEx.Message}). Falling back to file save.");

                    string fallbackFileName = $"AutoSave_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                    string fallbackDir = GetSafeFallbackProfileDirectory(chProfile?.FilePath);
                    string fallbackPath = Path.Combine(fallbackDir, fallbackFileName);

                    var container = new ProfileConfigFile
                    {
                        Version = 2,
                        ProfileName = profileName,
                        SystemConfig = systemSettings,
                        Channels = channelsToSave
                    };

                    try
                    {
                        FileWriteOperation(fallbackPath, JsonConvert.SerializeObject(container, Formatting.Indented));
                        LastFallbackSavedFilePath = fallbackPath;
                        ModbusLogger.Info($"[Remote Shutdown] Profile '{profileName}' saved to fallback file: {fallbackPath}");
                    }
                    catch (Exception fileEx)
                    {
                        ModbusLogger.Error($"[Remote Shutdown] Fallback file save failed: {fileEx.Message}", fileEx);
                        throw;
                    }
                }
            }

            ModbusLogger.Info($"[Remote Shutdown] Profile '{profileName}' saved successfully.");
        }

        private static async Task SaveInspectionTestDataAsync(MainWindow? win)
        {
            if (DeviceCOM.responses != null && DeviceCOM.responses.Count > 0)
            {
                var latest = DeviceCOM.responses.LastOrDefault();
                if (latest != null && latest.FD != null && latest.FD.Count > 0)
                {
                    if (win?.portCOM != null)
                    {
                        if (!string.IsNullOrEmpty(DeviceCOM.Code))
                        {
                            DeviceCOM.WriteLogCSVDirect(Convert.ToBoolean(latest.OR), DateTime.Now, latest);
                        }
                        if (!DeviceCOM.IsLogDisable && DeviceCOM.IsLogEnable)
                        {
                            win.portCOM.WriteLog(latest.CN, Convert.ToBoolean(latest.OR), DateTime.Now, latest);
                        }
                    }
                }
            }
            await Task.CompletedTask;
            ModbusLogger.Info("[Remote Shutdown] Inspection test data saved successfully.");
        }

        private static void FlushLogs()
        {
            ModbusLogger.Flush();
            ModbusLogger.Info("[Remote Shutdown] Logs flushed successfully.");
        }

        private static void CloseHardware(MainWindow? win)
        {
            if (win != null)
            {
                win.CloseHardwareConnections();
            }
        }

        private static void ShowErrorToUser(MainWindow? win, string message)
        {
            if (ShowErrorDialogAction != null)
            {
                ShowErrorDialogAction(message, "Remote Shutdown Error");
                return;
            }

            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(win, message, "Remote Shutdown Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        public static string GetSafeFallbackProfileDirectory(string? existingFilePath = null)
        {
            if (!string.IsNullOrWhiteSpace(existingFilePath))
            {
                try
                {
                    string? dir = Path.GetDirectoryName(existingFilePath);
                    if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir) && !IsInstallDirectory(dir) && IsDirectoryWritable(dir))
                    {
                        return dir;
                    }
                }
                catch { }
            }

            try
            {
                string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string fallbackDir = Path.Combine(programData, "8F", "Profiles");
                if (!Directory.Exists(fallbackDir))
                {
                    Directory.CreateDirectory(fallbackDir);
                }
                if (IsDirectoryWritable(fallbackDir))
                {
                    return fallbackDir;
                }
            }
            catch { }

            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string localDir = Path.Combine(localAppData, "8F", "Profiles");
                if (!Directory.Exists(localDir))
                {
                    Directory.CreateDirectory(localDir);
                }
                return localDir;
            }
            catch { }

            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static bool IsInstallDirectory(string dir)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).TrimEnd('\\', '/');
                string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86).TrimEnd('\\', '/');

                string full = Path.GetFullPath(dir).TrimEnd('\\', '/');
                return full.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase) ||
                       full.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase) ||
                       full.StartsWith(programFilesX86, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsDirectoryWritable(string dirPath)
        {
            try
            {
                string testFile = Path.Combine(dirPath, Path.GetRandomFileName() + ".tmp");
                using (var fs = File.Create(testFile, 1, FileOptions.DeleteOnClose))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
