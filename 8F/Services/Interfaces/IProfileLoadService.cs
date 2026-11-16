using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace _8F.Services.Interfaces
{
    public enum ProfileLoadSource
    {
        MenuOpenDb,
        MenuOpenFile,
        MenuImport,
        ModbusPanel,
        ModbusMaster
    }

    public record ProfileLoadResult(
        bool Success,
        string Message,
        int TargetChannel,
        ushort? ProfileId,
        string ProfileName,
        string? FilePath
    );

    public interface IProfileLoadService
    {
        ChannelData? SelectSourceChannel(IReadOnlyList<ChannelData> availableChannels, int targetChannel);
        Task<ProfileLoadResult> LoadFromDbAsync(
            int profileId,
            int targetChannel,
            ProfileLoadSource source,
            string? clientIp = null,
            bool isInteractive = false);

        Task<ProfileLoadResult> LoadFromFileAsync(
            string filePath,
            int targetChannel,
            ProfileLoadSource source,
            bool isInteractive = false);
        Task<ProfileLoadResult> LoadFromChannelsAsync(
            List<ChannelData> channels,
            SystemSettings? settings,
            string profileName,
            ushort? profileId,
            string? filePath,
            int targetChannel,
            ProfileLoadSource source,
            string? clientIp = null,
            bool isInteractive = false);
    }
}
