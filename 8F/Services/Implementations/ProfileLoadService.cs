using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using _8F.Auth;
using _8F.Helpers;
using _8F.Models;
using _8F.Repositories.Implementations;
using _8F.Repositories.Interfaces;
using _8F.Services.Interfaces;
using _8F.Views;

namespace _8F.Services.Implementations
{
    public class ProfileLoadService : IProfileLoadService
    {
        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private readonly Func<MainWindow?> _mainWindowProvider;
        private readonly Func<IConfigProfileRepository> _repoFactory;
        private readonly Func<IModbusSlaveService?> _modbusServiceProvider;
        private readonly Dictionary<int, ushort> _lastGoodProfileIds = new();

        private static MainWindow? SafeGetMainWindow()
        {
            if (Application.Current == null) return null;
            if (Application.Current.Dispatcher.CheckAccess())
            {
                return Application.Current.MainWindow as MainWindow;
            }
            return Application.Current.Dispatcher.Invoke(() => Application.Current.MainWindow as MainWindow);
        }

        public ProfileLoadService(
            Func<MainWindow?>? mainWindowProvider = null,
            Func<IConfigProfileRepository>? repoFactory = null,
            Func<IModbusSlaveService?>? modbusServiceProvider = null)
        {
            _mainWindowProvider = mainWindowProvider ?? SafeGetMainWindow;
            _repoFactory = repoFactory ?? (() => new InspectionLogRepository());
            _modbusServiceProvider = modbusServiceProvider ?? (() => _mainWindowProvider()?.modbusSlaveService);
        }

        public ChannelData? SelectSourceChannel(IReadOnlyList<ChannelData> availableChannels, int targetChannel)
        {
            if (availableChannels == null || availableChannels.Count == 0) return null;

            // Rule 1: If the profile contains exactly one saved channel, use it.
            if (availableChannels.Count == 1)
            {
                return availableChannels[0];
            }

            // Rule 2: If it contains several, use the saved channel whose number equals the target channel's number.
            var match = availableChannels.FirstOrDefault(c => c.Id == targetChannel);
            if (match != null)
            {
                return match;
            }

            // Rule 3: If there is no such match, use the lowest-numbered saved channel.
            return availableChannels.OrderBy(c => c.Id).First();
        }

        public async Task<ProfileLoadResult> LoadFromDbAsync(
            int profileId,
            int targetChannel,
            ProfileLoadSource source,
            string? clientIp = null,
            bool isInteractive = false)
        {
            await _loadLock.WaitAsync();
            try
            {
                var mainWin = _mainWindowProvider();
                if (mainWin == null)
                {
                    ModbusLogger.Error($"[ProfileLoadService] Main window is not available when loading profile {profileId} into Channel {targetChannel}.");
                    return new ProfileLoadResult(false, "Main window is not available.", targetChannel, (ushort)profileId, "", null);
                }

                if (profileId <= 0)
                {
                    ModbusLogger.Info($"[ProfileLoadService] Profile ID {profileId} received for Channel {targetChannel}. Profile ID 0 loads nothing.");
                    return new ProfileLoadResult(false, $"Profile ID {profileId} is invalid (must be > 0).", targetChannel, (ushort)profileId, "", null);
                }

                int maxChannels = mainWin.chNo;
                if (targetChannel < 1 || targetChannel > maxChannels)
                {
                    ModbusLogger.Warn($"[ProfileLoadService] Target channel {targetChannel} is outside active range (1..{maxChannels}) for Profile #{profileId}.");
                    RestoreModbusRegister(targetChannel);
                    return new ProfileLoadResult(false, $"Target channel {targetChannel} is outside active range (1..{maxChannels}).", targetChannel, (ushort)profileId, "", null);
                }

                if (!mainWin.isOpenWithDb)
                {
                    ModbusLogger.Warn($"[ProfileLoadService] Database mode is disabled (IsOpenWithDb = false). Cannot load Profile #{profileId} into Channel {targetChannel}.");
                    RestoreModbusRegister(targetChannel);
                    return new ProfileLoadResult(false, "Database mode is disabled (IsOpenWithDb = false).", targetChannel, (ushort)profileId, "", null);
                }

                var repo = _repoFactory();
                var (dbChannels, settings) = await repo.GetFullConfigProfileAsync(profileId);
                string profileName = await repo.GetProfileNameAsync(profileId) ?? $"Profile #{profileId}";

                if (dbChannels == null || dbChannels.Count == 0)
                {
                    ModbusLogger.Warn($"[ProfileLoadService] Profile #{profileId} not found in database or has no channels. Channel {targetChannel} unchanged.");
                    RestoreModbusRegister(targetChannel);
                    return new ProfileLoadResult(false, $"Profile #{profileId} not found in database.", targetChannel, (ushort)profileId, profileName, null);
                }

                var sourceChannel = SelectSourceChannel(dbChannels, targetChannel);

                if (sourceChannel == null || sourceChannel.graphDatas == null || sourceChannel.graphDatas.Count == 0)
                {
                    ModbusLogger.Warn($"[ProfileLoadService] No valid frequency data found for target channel {targetChannel} in Profile #{profileId}.");
                    RestoreModbusRegister(targetChannel);
                    return new ProfileLoadResult(false, $"Profile #{profileId} has no frequency/graph data for Channel {targetChannel}.", targetChannel, (ushort)profileId, profileName, null);
                }

                return await ApplyChannelToTargetAsync(mainWin, sourceChannel, settings, profileName, (ushort)profileId, null, targetChannel, source, clientIp);
            }
            catch (Exception ex)
            {
                ModbusLogger.Error($"[ProfileLoadService] Error loading profile {profileId} into channel {targetChannel}: {ex.Message}", ex);
                RestoreModbusRegister(targetChannel);
                return new ProfileLoadResult(false, $"Error loading profile: {ex.Message}", targetChannel, (ushort)profileId, "", null);
            }
            finally
            {
                _loadLock.Release();
            }
        }

        public async Task<ProfileLoadResult> LoadFromFileAsync(
            string filePath,
            int targetChannel,
            ProfileLoadSource source,
            bool isInteractive = false)
        {
            await _loadLock.WaitAsync();
            try
            {
                var mainWin = _mainWindowProvider();
                if (mainWin == null)
                {
                    ModbusLogger.Error($"[ProfileLoadService] Main window is not available when loading file '{filePath}' into Channel {targetChannel}.");
                    return new ProfileLoadResult(false, "Main window is not available.", targetChannel, null, "", filePath);
                }

                if (!File.Exists(filePath))
                {
                    ModbusLogger.Warn($"[ProfileLoadService] File '{filePath}' does not exist for Channel {targetChannel}.");
                    return new ProfileLoadResult(false, $"File '{filePath}' does not exist.", targetChannel, null, "", filePath);
                }

                int maxChannels = mainWin.chNo;
                if (targetChannel < 1 || targetChannel > maxChannels)
                {
                    ModbusLogger.Warn($"[ProfileLoadService] Target channel {targetChannel} is outside active range (1..{maxChannels}) for file '{filePath}'.");
                    return new ProfileLoadResult(false, $"Target channel {targetChannel} is outside active range (1..{maxChannels}).", targetChannel, null, "", filePath);
                }

                string json = await File.ReadAllTextAsync(filePath);
                var (parsedChData, sysSettings, profName) = ConfigurationImporter.ImportProfileFromJson(json);

                if (parsedChData == null || parsedChData.Count == 0)
                {
                    ModbusLogger.Warn($"[ProfileLoadService] Failed to parse configuration data from file '{filePath}' for Channel {targetChannel}.");
                    return new ProfileLoadResult(false, "Failed to parse valid configuration data from file.", targetChannel, null, "", filePath);
                }

                string profileName = !string.IsNullOrWhiteSpace(profName) ? profName : Path.GetFileNameWithoutExtension(filePath);

                var sourceChannel = SelectSourceChannel(parsedChData, targetChannel);

                if (sourceChannel == null || sourceChannel.graphDatas == null || sourceChannel.graphDatas.Count == 0)
                {
                    ModbusLogger.Warn($"[ProfileLoadService] No valid channel data found for target channel {targetChannel} in file '{filePath}'.");
                    return new ProfileLoadResult(false, $"No valid channel data found for target channel {targetChannel}.", targetChannel, null, profileName, filePath);
                }

                return await ApplyChannelToTargetAsync(mainWin, sourceChannel, sysSettings, profileName, null, filePath, targetChannel, source, null);
            }
            catch (Exception ex)
            {
                ModbusLogger.Error($"[ProfileLoadService] Error loading file '{filePath}' into channel {targetChannel}: {ex.Message}", ex);
                return new ProfileLoadResult(false, $"Error loading file: {ex.Message}", targetChannel, null, "", filePath);
            }
            finally
            {
                _loadLock.Release();
            }
        }

        public async Task<ProfileLoadResult> LoadFromChannelsAsync(
            List<ChannelData> channels,
            SystemSettings? settings,
            string profileName,
            ushort? profileId,
            string? filePath,
            int targetChannel,
            ProfileLoadSource source,
            string? clientIp = null,
            bool isInteractive = false)
        {
            await _loadLock.WaitAsync();
            try
            {
                var mainWin = _mainWindowProvider();
                if (mainWin == null)
                {
                    return new ProfileLoadResult(false, "Main window is not available.", targetChannel, profileId, profileName, filePath);
                }

                if (channels == null || channels.Count == 0)
                {
                    return new ProfileLoadResult(false, "No channel data supplied.", targetChannel, profileId, profileName, filePath);
                }

                int maxChannels = mainWin.chNo;
                if (targetChannel < 1 || targetChannel > maxChannels)
                {
                    return new ProfileLoadResult(false, $"Target channel {targetChannel} is outside active range (1..{maxChannels}).", targetChannel, profileId, profileName, filePath);
                }

                ChannelData? sourceChannel = null;
                if (isInteractive && channels.Count > 1)
                {
                    bool confirmed = false;
                    await mainWin.Dispatcher.InvokeAsync(() =>
                    {
                        var dlg = new ChannelRemappingWindow(channels, profileName, targetChannel)
                        {
                            Owner = mainWin
                        };
                        if (dlg.ShowDialog() == true && dlg.IsConfirmed)
                        {
                            if (dlg.IsImportAsIs || dlg.TargetMappings.Values.Any(v => v.Count > 0))
                            {
                                confirmed = true;
                                if (dlg.IsImportAsIs)
                                {
                                    sourceChannel = SelectSourceChannel(channels, targetChannel);
                                }
                                else
                                {
                                    var mappedSrc = dlg.TargetMappings.FirstOrDefault(kvp => kvp.Value.Contains(targetChannel));
                                    if (mappedSrc.Value != null)
                                    {
                                        sourceChannel = channels.FirstOrDefault(c => c.Id == mappedSrc.Key);
                                    }
                                    sourceChannel ??= SelectSourceChannel(channels, targetChannel);
                                }
                            }
                        }
                    });

                    if (!confirmed)
                    {
                        return new ProfileLoadResult(false, "User cancelled channel remapping.", targetChannel, profileId, profileName, filePath);
                    }
                }
                else
                {
                    sourceChannel = SelectSourceChannel(channels, targetChannel);
                }

                if (sourceChannel == null || sourceChannel.graphDatas == null || sourceChannel.graphDatas.Count == 0)
                {
                    return new ProfileLoadResult(false, $"No valid channel data found for target channel {targetChannel}.", targetChannel, profileId, profileName, filePath);
                }

                return await ApplyChannelToTargetAsync(mainWin, sourceChannel, settings, profileName, profileId, filePath, targetChannel, source, clientIp);
            }
            catch (Exception ex)
            {
                ModbusLogger.Error($"[ProfileLoadService] Error loading channels into channel {targetChannel}", ex);
                return new ProfileLoadResult(false, $"Error loading channels: {ex.Message}", targetChannel, profileId, profileName, filePath);
            }
            finally
            {
                _loadLock.Release();
            }
        }

        private async Task<ProfileLoadResult> ApplyChannelToTargetAsync(
            MainWindow mainWin,
            ChannelData sourceChannel,
            SystemSettings? settings,
            string profileName,
            ushort? profileId,
            string? filePath,
            int targetChannel,
            ProfileLoadSource source,
            string? clientIp)
        {
            bool isTargetSelected = false;

            await mainWin.Dispatcher.InvokeAsync(() =>
            {
                int currentSelectedCh = mainWin.GetSelectedChannelNumber();
                isTargetSelected = (targetChannel == currentSelectedCh);

                // Ensure DeviceCOM.channelDatas has 4 channels initialized
                if (DeviceCOM.channelDatas == null || DeviceCOM.channelDatas.Count < 4)
                {
                    mainWin.InitialGraphData(false);
                }

                int targetIdx = targetChannel - 1;

                // Deep clone source channel and assign target Channel ID
                var cloned = ConfigurationImporter.CloneChannel(sourceChannel);
                cloned.Id = targetChannel;
                cloned.IsSeleted = isTargetSelected;

                // Replace ONLY target channel in DeviceCOM.channelDatas
                if (DeviceCOM.channelDatas != null && targetIdx < DeviceCOM.channelDatas.Count)
                {
                    DeviceCOM.channelDatas[targetIdx] = cloned;
                }

                // Ensure Channel 1's data object is not the same object as Channel 2's (no shared references)
                if (DeviceCOM.channelDatas != null && DeviceCOM.channelDatas.Count > 1)
                {
                    for (int i = 0; i < DeviceCOM.channelDatas.Count; i++)
                    {
                        if (i != targetIdx && DeviceCOM.channelDatas[i] != null)
                        {
                            if (object.ReferenceEquals(DeviceCOM.channelDatas[i], cloned) ||
                                (DeviceCOM.channelDatas[i].graphDatas != null && cloned.graphDatas != null &&
                                 object.ReferenceEquals(DeviceCOM.channelDatas[i].graphDatas, cloned.graphDatas)))
                            {
                                DeviceCOM.channelDatas[i] = ConfigurationImporter.CloneChannel(DeviceCOM.channelDatas[i]);
                                DeviceCOM.channelDatas[i].Id = i + 1;
                            }
                        }
                    }
                }

                // Mark balance required for target channel ONLY
                DeviceCOM.ChannelBalanceRequired[targetChannel] = true;

                // Safe system settings: preserve running ChannelCount and AppMode
                if (settings != null)
                {
                    if (settings.ChannelCount > 0 && settings.ChannelCount != mainWin.chNo)
                    {
                        ModbusLogger.Warn($"[ProfileLoadService] Profile specifies {settings.ChannelCount} channels, but application is in {mainWin.chNo}-channel mode. Preserving running ChannelCount = {mainWin.chNo}.");
                    }
                    if (settings.AppMode != mainWin.modeApp)
                    {
                        ModbusLogger.Warn($"[ProfileLoadService] Profile specifies AppMode '{settings.AppMode}', but application is in AppMode '{mainWin.modeApp}'. Preserving running AppMode.");
                    }
                    mainWin.ApplySafeSystemSettings(settings);
                }

                // Update CurrentProfileState with target channel
                if (source == ProfileLoadSource.MenuImport)
                {
                    mainWin.CurrentProfile.SetForChannel(targetChannel, null, null, null, sourceChannel.Id);
                    if (targetIdx >= 0 && targetIdx < mainWin._loadedChannels.Length)
                    {
                        mainWin._loadedChannels[targetIdx].ProfileId = 0;
                        mainWin._loadedChannels[targetIdx].ProfileName = string.Empty;
                    }
                }
                else
                {
                    int? pIdInt = profileId.HasValue && profileId.Value > 0 ? (int)profileId.Value : null;
                    mainWin.CurrentProfile.SetForChannel(targetChannel, pIdInt, profileName, filePath, sourceChannel.Id);

                    if (targetIdx >= 0 && targetIdx < mainWin._loadedChannels.Length)
                    {
                        mainWin._loadedChannels[targetIdx].ProfileId = profileId ?? 0;
                        mainWin._loadedChannels[targetIdx].ProfileName = profileName;
                    }
                }

                // Update display / header
                mainWin.UpdateConfigProfileDisplay();

                // Refresh UI only if target is currently selected
                if (isTargetSelected)
                {
                    mainWin.ClearGraphDataByChId(targetChannel);
                }
            });

            // Push settings to ECT instrument for target channel only
            bool rat = false;
            try
            {
                rat = await mainWin.Dispatcher.InvokeAsync(() => mainWin.ImplementChanges(0, targetChannel));
            }
            catch (Exception ex)
            {
                ModbusLogger.Error($"[ProfileLoadService] Error transmitting changes to ECT instrument for Channel {targetChannel}", ex);
            }

            // Ensure target channel balance requirement remains set (preventing unintended reset by serial port write)
            DeviceCOM.ChannelBalanceRequired[targetChannel] = true;

            if (!rat && mainWin.portCOM == null)
            {
                ModbusLogger.Info($"[ProfileLoadService] Profile '{profileName}' loaded into Channel {targetChannel} in offline mode (ECT instrument not connected).");
            }
            else if (!rat)
            {
                ModbusLogger.Warn($"[ProfileLoadService] Profile '{profileName}' loaded into Channel {targetChannel}, but no response from ECT instrument.");
            }

            // Silent Modbus holding register update
            var modbus = _modbusServiceProvider();
            if (modbus != null && profileId.HasValue && profileId.Value > 0)
            {
                ushort regAddr = ModbusSlaveService.ChannelIndexToAddress(targetChannel - 1);
                modbus.WriteRegisterSilent(regAddr, profileId.Value);
                _lastGoodProfileIds[targetChannel] = profileId.Value;
            }

            // Audit log (when database is enabled)
            if (mainWin.isOpenWithDb || mainWin.isSaveWithDb)
            {
                try
                {
                    string actor = source switch
                    {
                        ProfileLoadSource.ModbusMaster => $"PLC ({clientIp ?? "Unknown IP"})",
                        ProfileLoadSource.ModbusPanel => "Modbus Diagnostic Panel",
                        _ => AppServices.SessionContext.CurrentRole.ToString()
                    };
                    await AppServices.ConfigChangeRepository.RecordChangeAsync(
                        area: "Profile Load",
                        channel: $"Channel {targetChannel}",
                        summary: $"Loaded Profile '{profileName}' (ID: {profileId}) into Channel {targetChannel} via {source}. Actor: {actor}",
                        detailsJson: null);
                }
                catch { }
            }

            ModbusLogger.Info($"[ProfileLoadService] Successfully loaded Profile '{profileName}' (ID: {profileId}) into Channel {targetChannel} via {source}.");
            return new ProfileLoadResult(true, $"Profile '{profileName}' loaded into Channel {targetChannel} successfully.", targetChannel, profileId, profileName, filePath);
        }

        private void RestoreModbusRegister(int channelNumber)
        {
            var modbus = _modbusServiceProvider();
            if (modbus == null) return;

            ushort regAddr = ModbusSlaveService.ChannelIndexToAddress(channelNumber - 1);
            ushort rollbackId = _lastGoodProfileIds.TryGetValue(channelNumber, out var goodId) ? goodId : (ushort)0;
            modbus.WriteRegisterSilent(regAddr, rollbackId);
            ModbusLogger.Warn($"[ProfileLoadService] Restored Modbus Register {regAddr} (Channel {channelNumber}) back to {rollbackId}.");
        }
    }
}
