using System;
using System.Threading;
using System.Threading.Tasks;
using MachineInfo.Application.Interfaces;
using MachineInfo.Domain.Entities;
using Npgsql;

namespace MachineInfo.Infrastructure.Persistence
{
    public class NpgsqlMachineRegistry : IMachineRegistry
    {
        private readonly string _connectionString;

        public NpgsqlMachineRegistry(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Database connection string cannot be null or empty.", nameof(connectionString));
            }
            _connectionString = connectionString;
        }

        public async Task<MachineRegistryRecord?> GetBySerialNumberAsync(string normalizedSerialNumber, CancellationToken ct = default)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct).ConfigureAwait(false);

            const string sql = @"
                SELECT id, serial_number, machine_id, created_at, created_by, notes
                FROM machine_registry
                WHERE serial_number = @serial
                LIMIT 1;";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@serial", normalizedSerialNumber);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                return new MachineRegistryRecord(
                    Id: reader.GetInt64(0),
                    SerialNumber: reader.GetString(1),
                    MachineId: reader.GetString(2),
                    CreatedAt: reader.GetFieldValue<DateTimeOffset>(3),
                    CreatedBy: reader.IsDBNull(4) ? null : reader.GetString(4),
                    Notes: reader.IsDBNull(5) ? null : reader.GetString(5)
                );
            }

            return null;
        }

        public async Task<MachineRegistryRecord> InsertAsync(MachineRegistryRecord record, CancellationToken ct = default)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct).ConfigureAwait(false);

            const string sql = @"
                INSERT INTO machine_registry (serial_number, machine_id, created_at, created_by, notes)
                VALUES (@serial, @machineId, @createdAt, @createdBy, @notes)
                RETURNING id;";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@serial", record.SerialNumber);
            cmd.Parameters.AddWithValue("@machineId", record.MachineId);
            cmd.Parameters.AddWithValue("@createdAt", record.CreatedAt);
            cmd.Parameters.AddWithValue("@createdBy", (object?)record.CreatedBy ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@notes", (object?)record.Notes ?? DBNull.Value);

            object? idObj = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            long id = Convert.ToInt64(idObj);

            return record with { Id = id };
        }
    }
}
