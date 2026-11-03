using System.Collections.Generic;
using System.Threading.Tasks;

namespace _8F.Services
{
    public interface IConfigProfileRepository
    {
        Task EnsureConfigTablesCreatedAsync();
        Task<int> SaveConfigProfileAsync(string name, string? operatorName, List<ChannelData> channelDatas);
        Task<List<ChannelData>> GetConfigProfileAsync(int profileId);
        Task<string?> GetProfileNameAsync(int profileId);
        Task<List<ConfigProfileSummary>> ListConfigProfilesAsync();
    }
}
