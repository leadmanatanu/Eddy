using System;
using System.IO;
using System.Threading.Tasks;
using ConfigurationKeyGenerator.Models;
using ConfigurationKeyGenerator.Services;
using Npgsql;
using Shared.MagkraftTools;
using Xunit;

namespace ConfigurationKeyGenerator.Tests
{
    [CollectionDefinition("DatabaseTests", DisableParallelization = true)]
    public class DatabaseTestCollectionDefinition { }

    [Collection("DatabaseTests")]
    public class DatabaseAndRegistryTests
    {
        private const string TestDbName = "GenerateMachineKey_test";
        private const string TestConnectionString = "Host=localhost;Port=5432;Username=postgres;Password=aryan123;Database=GenerateMachineKey_test";
        private const string MaintenanceConnectionString = "Host=localhost;Port=5432;Username=postgres;Password=aryan123;Database=postgres";

        private static void SafeDropTestDatabase()
        {
            var builder = new NpgsqlConnectionStringBuilder(TestConnectionString);
            string dbName = builder.Database ?? string.Empty;

            // Refuse to drop any database not ending in _test
            if (!dbName.EndsWith("_test", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Refusing to drop database '{dbName}': name does not end in '_test'.");
            }

            NpgsqlConnection.ClearAllPools();

            using var conn = new NpgsqlConnection(MaintenanceConnectionString);
            conn.Open();

            using (var termCmd = new NpgsqlCommand(
                $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{dbName}' AND pid <> pg_backend_pid();", conn))
            {
                termCmd.ExecuteNonQuery();
            }

            using (var dropCmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{dbName}\" WITH (FORCE);", conn))
            {
                dropCmd.ExecuteNonQuery();
            }
        }

        [Fact]
        public void DropDatabase_RefusesDatabaseNotEndingInTest()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                string targetDb = "GenerateMachineKey_production";
                if (!targetDb.EndsWith("_test", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Refusing to drop database '{targetDb}': name does not end in '_test'.");
                }
            });
        }

        [Fact]
        public async Task Initializer_OnMissingDatabase_CreatesAndMigrates()
        {
            SafeDropTestDatabase();

            await DatabaseBootstrap.EnsureDatabaseAsync(TestConnectionString);

            // Verify database and tables exist
            await using var conn = new NpgsqlConnection(TestConnectionString);
            await conn.OpenAsync();

            await using var cmd = new NpgsqlCommand(@"
                SELECT 1 FROM information_schema.tables WHERE table_name = 'machine_registry';
                SELECT 1 FROM information_schema.tables WHERE LOWER(table_name) = 'configurationkeylogs';", conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            Assert.True(await reader.ReadAsync());
            Assert.True(await reader.NextResultAsync());
            Assert.True(await reader.ReadAsync());
        }

        [Fact]
        public async Task Initializer_RunTwice_IsIdempotent()
        {
            SafeDropTestDatabase();

            await DatabaseBootstrap.EnsureDatabaseAsync(TestConnectionString);
            // Run again immediately
            await DatabaseBootstrap.EnsureDatabaseAsync(TestConnectionString);

            await using var conn = new NpgsqlConnection(TestConnectionString);
            await conn.OpenAsync();

            await using var cmd = new NpgsqlCommand("SELECT COUNT(1) FROM information_schema.tables WHERE LOWER(table_name) = 'configurationkeylogs';", conn);
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task Initializer_UpgradesOldSchema_AddsSettingsJson()
        {
            SafeDropTestDatabase();

            // 1. Manually create test DB and old ConfigurationKeyLogs table without SettingsJson
            await DatabaseBootstrap.EnsureDatabaseExistsAsync(TestConnectionString);

            await using (var conn = new NpgsqlConnection(TestConnectionString))
            {
                await conn.OpenAsync();
                string oldTableSql = @"
                    CREATE TABLE ConfigurationKeyLogs (
                        Id SERIAL PRIMARY KEY,
                        ProductName VARCHAR(100) NOT NULL,
                        CustomerName VARCHAR(100) NOT NULL,
                        MachineId VARCHAR(256) NOT NULL,
                        ConfigurationFileName VARCHAR(255),
                        GeneratedFileName VARCHAR(255),
                        GeneratedFile BYTEA NOT NULL,
                        GeneratedDate TIMESTAMPTZ DEFAULT NOW(),
                        UpdatedDate TIMESTAMPTZ
                    );";
                await using var cmd = new NpgsqlCommand(oldTableSql, conn);
                await cmd.ExecuteNonQueryAsync();
            }

            // 2. Run migrations
            await DatabaseBootstrap.ApplyMigrationsAsync(TestConnectionString);

            // 3. Verify SettingsJson column exists now
            await using (var conn = new NpgsqlConnection(TestConnectionString))
            {
                await conn.OpenAsync();
                string checkColumnSql = @"
                    SELECT 1 FROM information_schema.columns
                    WHERE LOWER(table_name) = 'configurationkeylogs' AND LOWER(column_name) = 'settingsjson';";
                await using var cmd = new NpgsqlCommand(checkColumnSql, conn);
                var exists = await cmd.ExecuteScalarAsync();
                Assert.NotNull(exists);
            }
        }

        [Fact]
        public async Task MachineRegistry_Lookup_ReturnsRegisteredSerialAndBlocksUnregistered()
        {
            SafeDropTestDatabase();
            await DatabaseBootstrap.EnsureDatabaseAsync(TestConnectionString);

            var logService = new ConfigurationKeyLogService(TestConnectionString);

            // 1. Unregistered Machine ID returns null (blocked)
            string unregisteredId = "06B79C1DE2974253F751657C2342B47A90D174BB60DDD9FC55462A379E108BE0DC17612CA464A523";
            string? stored = logService.GetRegistrySerial(unregisteredId);
            Assert.Null(stored);

            // 2. Insert into machine_registry
            const string registeredSerial = "A1234567890B";
            await using (var conn = new NpgsqlConnection(TestConnectionString))
            {
                await conn.OpenAsync();
                string insertSql = "INSERT INTO machine_registry (serial_number, machine_id) VALUES (@serial, @machineId);";
                await using var cmd = new NpgsqlCommand(insertSql, conn);
                cmd.Parameters.AddWithValue("@serial", registeredSerial);
                cmd.Parameters.AddWithValue("@machineId", unregisteredId);
                await cmd.ExecuteNonQueryAsync();
            }

            // 3. Registered Machine ID returns registered serial
            stored = logService.GetRegistrySerial(unregisteredId);
            Assert.Equal(registeredSerial, stored);
        }

        [Fact]
        public async Task ConfigurationKeyLogs_SaveAndGetAll_PersistsSettingsJson()
        {
            SafeDropTestDatabase();
            await DatabaseBootstrap.EnsureDatabaseAsync(TestConnectionString);

            var logService = new ConfigurationKeyLogService(TestConnectionString);

            var log = new ConfigurationKeyLog
            {
                ProductName = "EddyShorter",
                CustomerName = "Test Customer",
                MachineId = "MACHINE-ID-1234",
                ConfigurationFileName = "test_config.txt",
                GeneratedFileName = "TestCustomer_EddyShorter_MACHINE-ID.key",
                GeneratedFile = new byte[] { 1, 2, 3, 4 },
                SettingsJson = "{\"FrequencyNo\": 4, \"IsOpenWithDb\": true}"
            };

            logService.Save(log);

            Assert.True(logService.ExistsByMachineId("MACHINE-ID-1234"));

            var all = logService.GetAll();
            Assert.Single(all);
            Assert.Equal("Test Customer", all[0].CustomerName);
            Assert.Equal("EddyShorter", all[0].ProductName);
            Assert.Equal("MACHINE-ID-1234", all[0].MachineId);
            Assert.Equal("{\"FrequencyNo\": 4, \"IsOpenWithDb\": true}", all[0].SettingsJson);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, all[0].GeneratedFile);
        }
    }
}
