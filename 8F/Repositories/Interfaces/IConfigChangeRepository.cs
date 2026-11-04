using System.Threading.Tasks;
using _8F.Models;

namespace _8F.Repositories.Interfaces
{
    public interface IConfigChangeRepository
    {
        Task EnsureTableCreatedAsync();
        Task RecordChangeAsync(string area, string? channel, string summary, string? detailsJson = null);
        Task<ConfigChangeLogEntry?> GetLatestChangeAsync();
    }
}
