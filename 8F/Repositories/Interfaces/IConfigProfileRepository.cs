using System.Collections.Generic;
using System.Threading.Tasks;
using _8F.Models;

namespace _8F.Services
{
    public interface IConfigProfileRepository
    {
        Task EnsureConfigTablesCreatedAsync();
        Task<int> SaveConfigProfileAsync(string name, string? operatorName, List<ChannelData> channelDatas, SystemSettings? systemSettings = null);
        Task<bool> UpdateConfigProfileAsync(int profileId, string name, List<ChannelData> channelDatas, SystemSettings? systemSettings = null);
        Task<bool> DeleteProfileAsync(int profileId);
        Task<List<ChannelData>> GetConfigProfileAsync(int profileId);
        Task<(List<ChannelData> Channels, SystemSettings? Settings)> GetFullConfigProfileAsync(int profileId);
        Task<string?> GetProfileNameAsync(int profileId);
        Task<ConfigProfileSummary?> GetConfigProfileByNameAsync(string name);
        Task<List<ConfigProfileSummary>> ListConfigProfilesAsync();
    }
}
