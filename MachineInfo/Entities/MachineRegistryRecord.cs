using System;

namespace MachineInfo.Domain.Entities
{
    public sealed record MachineRegistryRecord(
        long Id,
        string SerialNumber,
        string MachineId,
        DateTimeOffset CreatedAt,
        string? CreatedBy = null,
        string? Notes = null
    );
}
