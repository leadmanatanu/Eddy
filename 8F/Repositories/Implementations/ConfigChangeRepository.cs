using System;
using System.Data;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using _8F.Auth;
using _8F.Models;
using _8F.Repositories.Interfaces;

namespace _8F.Repositories.Implementations
{
    public class ConfigChangeRepository : IConfigChangeRepository
    {
        private readonly string _connectionString;

        public ConfigChangeRepository(string? connectionString = null)
        {
            _connectionString = connectionString ?? DbConnectionProvider.ConnectionString;
        }

        private async Task<NpgsqlConnection> GetOpenConnectionAsync()
        {
            var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            return conn;
        }

        public async Task EnsureTableCreatedAsync()
        {
            const string sql = @"
                CREATE TABLE IF NOT EXISTS public.""ConfigChangeLog"" (
                    ""Id""            BIGSERIAL    PRIMARY KEY,
                    ""ChangedAt""     TIMESTAMPTZ  NOT NULL DEFAULT now(),
                    ""ChangedByRole"" VARCHAR(16)  NOT NULL,
                    ""Area""          VARCHAR(64)  NOT NULL,
                    ""Channel""       VARCHAR(16)  NULL,
                    ""Summary""       TEXT         NOT NULL,
                    ""Details""       JSONB        NULL
                );

                CREATE INDEX IF NOT EXISTS ""IX_ConfigChangeLog_Id_Desc"" ON public.""ConfigChangeLog"" (""Id"" DESC);
            ";

            await using var conn = await GetOpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task RecordChangeAsync(string area, string? channel, string summary, string? detailsJson = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(summary))
                    return;

                string cappedSummary = summary.Length > 300 ? summary.Substring(0, 297) + "..." : summary;
                string role = AppServices.SessionContext.CurrentRole != Role.None 
                    ? AppServices.SessionContext.CurrentRole.ToString() 
                    : "Admin";

                const string sql = @"
                    INSERT INTO public.""ConfigChangeLog"" 
                        (""ChangedAt"", ""ChangedByRole"", ""Area"", ""Channel"", ""Summary"", ""Details"")
                    VALUES 
                        (now(), @role, @area, @channel, @summary, @details::jsonb);
                ";

                await using var conn = await GetOpenConnectionAsync();
                await using var cmd = new NpgsqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@role", role);
                cmd.Parameters.AddWithValue("@area", area ?? "General");
                cmd.Parameters.AddWithValue("@channel", (object?)channel ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@summary", cappedSummary);
                cmd.Parameters.AddWithValue("@details", (object?)detailsJson ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                // Fail-safe: a failure to write the change log must never block or undo user operations
                System.Diagnostics.Debug.WriteLine($"[ConfigChangeLog Error] Failed to record change for {area}: {ex.Message}");
            }
        }

        public async Task<ConfigChangeLogEntry?> GetLatestChangeAsync()
        {
            try
            {
                const string sql = @"
                    SELECT ""Id"", ""ChangedAt"", ""ChangedByRole"", ""Area"", ""Channel"", ""Summary"", ""Details""::text AS ""DetailsText""
                    FROM public.""ConfigChangeLog""
                    ORDER BY ""Id"" DESC
                    LIMIT 1;
                ";

                await using var conn = await GetOpenConnectionAsync();
                await using var cmd = new NpgsqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new ConfigChangeLogEntry
                    {
                        Id = reader.GetInt64(reader.GetOrdinal("Id")),
                        ChangedAt = reader.GetDateTime(reader.GetOrdinal("ChangedAt")),
                        ChangedByRole = reader.GetString(reader.GetOrdinal("ChangedByRole")),
                        Area = reader.GetString(reader.GetOrdinal("Area")),
                        Channel = reader.IsDBNull(reader.GetOrdinal("Channel")) ? null : reader.GetString(reader.GetOrdinal("Channel")),
                        Summary = reader.GetString(reader.GetOrdinal("Summary")),
                        Details = reader.IsDBNull(reader.GetOrdinal("DetailsText")) ? null : reader.GetString(reader.GetOrdinal("DetailsText"))
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigChangeLog Error] Failed to read latest change: {ex.Message}");
                return null;
            }
        }
    }
}
