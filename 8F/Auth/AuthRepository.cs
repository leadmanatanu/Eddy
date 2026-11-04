using _8F.Repositories;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;
using Npgsql;

namespace _8F.Auth
{
    public class AuthRepository : IAuthRepository
    {
        private readonly string _connectionString;

        public AuthRepository(string? connectionString = null)
        {
            _connectionString = connectionString ?? DbConnectionProvider.ConnectionString;
        }

        private async Task<NpgsqlConnection> GetOpenConnectionAsync()
        {
            var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            return conn;
        }

        public async Task EnsureAuthTablesCreatedAsync()
        {
            const string sql = @"
                CREATE TABLE IF NOT EXISTS public.""AuthCredentials"" (
                    ""Role""          VARCHAR(16)  PRIMARY KEY CHECK (""Role"" IN ('Admin','Operator')),
                    ""PasswordHash""  BYTEA        NOT NULL,
                    ""Salt""          BYTEA        NOT NULL,
                    ""Iterations""    INTEGER      NOT NULL,
                    ""UpdatedAt""     TIMESTAMPTZ  NOT NULL DEFAULT now(),
                    ""UpdatedByRole"" VARCHAR(16)  NULL
                );

                CREATE TABLE IF NOT EXISTS public.""AuthAuditLog"" (
                    ""Id""        BIGSERIAL PRIMARY KEY,
                    ""EventTime"" TIMESTAMPTZ NOT NULL DEFAULT now(),
                    ""Event""     VARCHAR(32) NOT NULL,
                    ""Role""      VARCHAR(16) NULL,
                    ""Detail""    TEXT NULL
                );";

            await using var conn = await GetOpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<int> GetCredentialCountAsync()
        {
            const string sql = @"SELECT COUNT(*) FROM public.""AuthCredentials"";";
            await using var conn = await GetOpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(sql, conn);
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task<List<StoredCredential>> GetAllCredentialsAsync()
        {
            const string sql = @"
                SELECT ""Role"", ""PasswordHash"", ""Salt"", ""Iterations"", ""UpdatedAt"", ""UpdatedByRole""
                FROM public.""AuthCredentials"";";

            var list = new List<StoredCredential>();
            await using var conn = await GetOpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                string roleStr = reader.GetString(0);
                if (Enum.TryParse<Role>(roleStr, true, out var role))
                {
                    list.Add(new StoredCredential
                    {
                        Role = role,
                        PasswordHash = (byte[])reader["PasswordHash"],
                        Salt = (byte[])reader["Salt"],
                        Iterations = reader.GetInt32(3),
                        UpdatedAt = reader.GetDateTime(4),
                        UpdatedByRole = reader.IsDBNull(5) ? null : reader.GetString(5)
                    });
                }
            }

            return list;
        }

        public async Task<StoredCredential?> GetCredentialByRoleAsync(Role role)
        {
            const string sql = @"
                SELECT ""Role"", ""PasswordHash"", ""Salt"", ""Iterations"", ""UpdatedAt"", ""UpdatedByRole""
                FROM public.""AuthCredentials""
                WHERE ""Role"" = @Role;";

            await using var conn = await GetOpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Role", role.ToString());

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new StoredCredential
                {
                    Role = role,
                    PasswordHash = (byte[])reader["PasswordHash"],
                    Salt = (byte[])reader["Salt"],
                    Iterations = reader.GetInt32(3),
                    UpdatedAt = reader.GetDateTime(4),
                    UpdatedByRole = reader.IsDBNull(5) ? null : reader.GetString(5)
                };
            }

            return null;
        }

        public async Task SaveInitialCredentialsAsync(StoredCredential adminCred, StoredCredential operatorCred)
        {
            const string sql = @"
                INSERT INTO public.""AuthCredentials"" (""Role"", ""PasswordHash"", ""Salt"", ""Iterations"", ""UpdatedAt"", ""UpdatedByRole"")
                VALUES (@Role, @PasswordHash, @Salt, @Iterations, now(), @UpdatedByRole);";

            await using var conn = await GetOpenConnectionAsync();
            await using var tx = await conn.BeginTransactionAsync();

            try
            {
                await using (var cmd = new NpgsqlCommand(sql, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@Role", Role.Admin.ToString());
                    cmd.Parameters.AddWithValue("@PasswordHash", adminCred.PasswordHash);
                    cmd.Parameters.AddWithValue("@Salt", adminCred.Salt);
                    cmd.Parameters.AddWithValue("@Iterations", adminCred.Iterations);
                    cmd.Parameters.AddWithValue("@UpdatedByRole", (object?)adminCred.UpdatedByRole ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }

                await using (var cmd = new NpgsqlCommand(sql, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@Role", Role.Operator.ToString());
                    cmd.Parameters.AddWithValue("@PasswordHash", operatorCred.PasswordHash);
                    cmd.Parameters.AddWithValue("@Salt", operatorCred.Salt);
                    cmd.Parameters.AddWithValue("@Iterations", operatorCred.Iterations);
                    cmd.Parameters.AddWithValue("@UpdatedByRole", (object?)operatorCred.UpdatedByRole ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }

                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task UpdateCredentialAsync(StoredCredential cred)
        {
            const string sql = @"
                UPDATE public.""AuthCredentials""
                SET ""PasswordHash"" = @PasswordHash,
                    ""Salt"" = @Salt,
                    ""Iterations"" = @Iterations,
                    ""UpdatedAt"" = now(),
                    ""UpdatedByRole"" = @UpdatedByRole
                WHERE ""Role"" = @Role;";

            await using var conn = await GetOpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Role", cred.Role.ToString());
            cmd.Parameters.AddWithValue("@PasswordHash", cred.PasswordHash);
            cmd.Parameters.AddWithValue("@Salt", cred.Salt);
            cmd.Parameters.AddWithValue("@Iterations", cred.Iterations);
            cmd.Parameters.AddWithValue("@UpdatedByRole", (object?)cred.UpdatedByRole ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task LogAuditAsync(string eventName, Role? role, string? detail)
        {
            try
            {
                const string sql = @"
                    INSERT INTO public.""AuthAuditLog"" (""EventTime"", ""Event"", ""Role"", ""Detail"")
                    VALUES (now(), @Event, @Role, @Detail);";

                await using var conn = await GetOpenConnectionAsync();
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Event", eventName);
                cmd.Parameters.AddWithValue("@Role", role.HasValue ? (object)role.Value.ToString() : DBNull.Value);
                cmd.Parameters.AddWithValue("@Detail", (object?)detail ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to write auth audit entry: {ex.Message}");
            }
        }
    }
}
