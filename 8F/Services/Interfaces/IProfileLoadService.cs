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
        /// <summary>
        /// Applies the standard source channel selection rule:
        /// 1. Exactly one saved channel -> use it.
        /// 2. Several saved channels, one matches targetChannel Id -> use it.
        /// 3. No match -> use the lowest-numbered saved channel.
        /// </summary>
        ChannelData? SelectSourceChannel(IReadOnlyList<ChannelData> availableChannels, int targetChannel);

        /// <summary>
        /// Loads a configuration profile from the database into the target channel.
        /// </summary>
        Task<ProfileLoadResult> LoadFromDbAsync(
            int profileId,
            int targetChannel,
            ProfileLoadSource source,
            string? clientIp = null,
            bool isInteractive = false);

        /// <summary>
        /// Loads a configuration profile from a file into the target channel.
        /// </summary>
        Task<ProfileLoadResult> LoadFromFileAsync(
            string filePath,
            int targetChannel,
            ProfileLoadSource source,
            bool isInteractive = false);

        /// <summary>
        /// Loads already-parsed channel data and settings into the target channel.
        /// </summary>
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
