using System.Threading;
using System.Threading.Tasks;
using MachineInfo.Domain.Entities;

namespace MachineInfo.Application.Interfaces
{
    public interface IMachineRegistry
    {
        Task<MachineRegistryRecord?> GetBySerialNumberAsync(string normalizedSerialNumber, CancellationToken ct = default);
        Task<MachineRegistryRecord> InsertAsync(MachineRegistryRecord record, CancellationToken ct = default);
    }
}
