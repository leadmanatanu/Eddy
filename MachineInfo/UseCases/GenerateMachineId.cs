using System;
using System.Threading;
using System.Threading.Tasks;
using _8F.Services.ConfigKey;
using MachineInfo.Application.Interfaces;
using MachineInfo.Domain.Entities;

namespace MachineInfo.Application.UseCases
{
    public interface IGenerateMachineId
    {
        Task<string> ExecuteAsync(string rawSerialNumber, CancellationToken ct = default);
    }

    /// <summary>
    /// Use case for generating and registering a deterministic Machine ID from an instrument serial number.
    /// Flow:
    /// 1. Normalize(serial)
    /// 2. Encode(normalized, secret) -> computedMachineId
    /// 3. Lookup machine_registry by normalized serial:
    ///    - If exists: verify stored ID matches computed ID. If match, return stored ID. If mismatch, throw error.
    ///    - If new: insert record and return computed ID.
    /// </summary>
    public class GenerateMachineId : IGenerateMachineId
    {
        private readonly IMachineRegistry _registry;
        private readonly IAppSecretProvider _secretProvider;

        public GenerateMachineId(IMachineRegistry registry, IAppSecretProvider secretProvider)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _secretProvider = secretProvider ?? throw new ArgumentNullException(nameof(secretProvider));
        }

        public async Task<string> ExecuteAsync(string rawSerialNumber, CancellationToken ct = default)
        {
            // Step 1: Normalize
            string normalized = MachineIdCodec.Normalize(rawSerialNumber);

            // Step 2: Encode
            byte[] secret = _secretProvider.GetSecret();
            string computedMachineId = MachineIdCodec.Encode(normalized, secret);

            // Step 3: Lookup machine_registry
            var existing = await _registry.GetBySerialNumberAsync(normalized, ct).ConfigureAwait(false);
            if (existing != null)
            {
                if (string.Equals(existing.MachineId, computedMachineId, StringComparison.OrdinalIgnoreCase))
                {
                    return existing.MachineId;
                }

                throw new InvalidOperationException(
                    $"Serial number '{normalized}' is already registered with a different Machine ID: '{existing.MachineId}'. " +
                    $"Computed ID was '{computedMachineId}'. The existing record was not overwritten.");
            }

            // Step 4: Insert new record
            var newRecord = new MachineRegistryRecord(
                Id: 0,
                SerialNumber: normalized,
                MachineId: computedMachineId,
                CreatedAt: DateTimeOffset.UtcNow,
                CreatedBy: Environment.UserName,
                Notes: null
            );

            await _registry.InsertAsync(newRecord, ct).ConfigureAwait(false);

            return computedMachineId;
        }
    }
}
