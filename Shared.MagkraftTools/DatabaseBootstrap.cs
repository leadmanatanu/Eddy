#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Shared.MagkraftTools
{
    public record LegacyMachineRecord(long Id, string SerialNumber, string MachineId);

    /// <summary>
    /// Magkraft-only database bootstrap utility for GenerateMachineKey database.
    /// Excluded from client 8F build.
    /// </summary>
    public static class DatabaseBootstrap
    {
        public const string EnvVarName = "GENERATE_MACHINE_KEY_DB_CONN";
        public const string ConfigKeyName = "ConnectionStrings:GenerateMachineKey";

        /// <summary>
        /// Resolves the database connection string from environment variable or appsettings.json.
        /// Throws InvalidOperationException naming the missing setting if neither is found.
        /// </summary>
        public static string ResolveConnectionString(string? explicitConnectionString = null, string? appsettingsPath = null)
        {
            if (!string.IsNullOrWhiteSpace(explicitConnectionString))
            {
                return explicitConnectionString;
            }

            string? envVal = Environment.GetEnvironmentVariable(EnvVarName);
            if (!string.IsNullOrWhiteSpace(envVal))
            {
                return envVal;
            }

            string path = appsettingsPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("ConnectionStrings", out var csSection) &&
                        csSection.TryGetProperty("GenerateMachineKey", out var csProp))
                    {
                        string? cs = csProp.GetString();
                        if (!string.IsNullOrWhiteSpace(cs))
                        {
                            return cs;
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to parse configuration file '{path}': {ex.Message}", ex);
                }
            }

            throw new InvalidOperationException(
                $"Database connection string is missing. Please set environment variable '{EnvVarName}' or '{ConfigKeyName}' in appsettings.json.");
        }

        /// <summary>
        /// Ensures the database exists (using maintenance DB 'postgres') and runs migrations.
        /// </summary>
        public static async Task<List<LegacyMachineRecord>> EnsureDatabaseAsync(string connectionString, CancellationToken ct = default)
        {
            await EnsureDatabaseExistsAsync(connectionString, ct).ConfigureAwait(false);
            return await ApplyMigrationsAsync(connectionString, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Connects to maintenance DB 'postgres' and creates the target database if not present.
        /// Handles 42P04 (duplicate_database) as success and 42501 with a clear privilege error.
        /// </summary>
        public static async Task EnsureDatabaseExistsAsync(string connectionString, CancellationToken ct = default)
        {
            var targetBuilder = new NpgsqlConnectionStringBuilder(connectionString);
            string targetDbName = targetBuilder.Database ?? throw new ArgumentException("Database name must be specified in connection string.");

            var maintenanceBuilder = new NpgsqlConnectionStringBuilder(connectionString)
            {
                Database = "postgres"
            };

            await using var conn = new NpgsqlConnection(maintenanceBuilder.ConnectionString);
            await conn.OpenAsync(ct).ConfigureAwait(false);

            await using (var checkCmd = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name", conn))
            {
                checkCmd.Parameters.AddWithValue("@name", targetDbName);
                var exists = await checkCmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
                if (exists != null && exists != DBNull.Value)
                {
                    return; // Database already exists
                }
            }

            try
            {
                // Database names in PostgreSQL CREATE DATABASE cannot use parameterized queries
                string safeDbName = targetDbName.Replace("\"", "\"\"");
                await using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{safeDbName}\"", conn);
                await createCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            catch (PostgresException ex)
            {
                if (ex.SqlState == "42P04") // duplicate_database
                {
                    // Treat as success (race condition / concurrent creation)
                    return;
                }

                if (ex.SqlState == "42501") // insufficient_privilege
                {
                    throw new InvalidOperationException(
                        $"Insufficient PostgreSQL privileges to create database '{targetDbName}'. Error: {ex.MessageText}", ex);
                }

                throw;
            }
        }

        /// <summary>
        /// Creates table machine_registry and alters CHAR(64) to VARCHAR(256) idempotently.
        /// Returns any legacy 64-character records for reporting.
        /// </summary>
        public static async Task<List<LegacyMachineRecord>> ApplyMigrationsAsync(string connectionString, CancellationToken ct = default)
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(ct).ConfigureAwait(false);

            string createMachineRegistrySql = @"
                CREATE TABLE IF NOT EXISTS machine_registry (
                    id BIGSERIAL PRIMARY KEY,
                    serial_number VARCHAR(100) NOT NULL UNIQUE,
                    machine_id VARCHAR(256) NOT NULL UNIQUE,
                    created_at TIMESTAMPTZ DEFAULT NOW(),
                    created_by VARCHAR(100),
                    notes TEXT
                );";

            await using (var cmd = new NpgsqlCommand(createMachineRegistrySql, conn))
            {
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            string alterMachineRegistrySql = @"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE LOWER(table_name) = 'machine_registry'
                          AND LOWER(column_name) = 'machine_id'
                          AND (data_type = 'character' OR character_maximum_length < 256)
                    ) THEN
                        ALTER TABLE machine_registry ALTER COLUMN machine_id TYPE VARCHAR(256);
                    END IF;
                END $$;";

            await using (var cmd = new NpgsqlCommand(alterMachineRegistrySql, conn))
            {
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            string createConfigLogsSql = @"
                CREATE TABLE IF NOT EXISTS ConfigurationKeyLogs (
                    Id SERIAL PRIMARY KEY,
                    ProductName VARCHAR(100) NOT NULL,
                    CustomerName VARCHAR(100) NOT NULL,
                    MachineId VARCHAR(256) NOT NULL,
                    ConfigurationFileName VARCHAR(255),
                    GeneratedFileName VARCHAR(255),
                    GeneratedFile BYTEA NOT NULL,
                    GeneratedDate TIMESTAMPTZ DEFAULT NOW(),
                    UpdatedDate TIMESTAMPTZ,
                    SettingsJson TEXT
                );";

            await using (var cmd = new NpgsqlCommand(createConfigLogsSql, conn))
            {
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            string alterConfigLogsSql = @"
                ALTER TABLE ConfigurationKeyLogs ADD COLUMN IF NOT EXISTS SettingsJson TEXT;
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE LOWER(table_name) = 'configurationkeylogs'
                          AND LOWER(column_name) = 'machineid'
                          AND (data_type = 'character' OR character_maximum_length < 256)
                    ) THEN
                        ALTER TABLE ConfigurationKeyLogs ALTER COLUMN MachineId TYPE VARCHAR(256);
                    END IF;
                END $$;";

            await using (var cmd = new NpgsqlCommand(alterConfigLogsSql, conn))
            {
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            // Identify legacy rows holding old 64-character hash IDs
            var legacyRecords = new List<LegacyMachineRecord>();
            string queryLegacySql = @"
                SELECT id, serial_number, machine_id
                FROM machine_registry
                WHERE LENGTH(machine_id) = 64;";

            await using (var cmd = new NpgsqlCommand(queryLegacySql, conn))
            await using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    legacyRecords.Add(new LegacyMachineRecord(
                        reader.GetInt64(0),
                        reader.GetString(1),
                        reader.GetString(2)
                    ));
                }
            }

            return legacyRecords;
        }
    }
}
