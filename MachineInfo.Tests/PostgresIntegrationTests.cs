using System;
using System.IO;
using System.Threading.Tasks;
using MachineInfo.Application.Interfaces;
using MachineInfo.Application.UseCases;
using MachineInfo.Infrastructure.Persistence;
using MachineInfo.Infrastructure.Security;
using Npgsql;
using Shared.MagkraftTools;
using Xunit;

namespace MachineInfo.Tests
{
    public class PostgresIntegrationTests
    {
        private const string TestDatabaseName = "GenerateMachineKey_test";

        private static string? GetTestConnectionString()
        {
            try
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                string appsettingsPath = Path.Combine(basePath, "appsettings.json");
                if (!File.Exists(appsettingsPath))
                {
                    // Look up in repository if running from bin
                    string repoRoot = Path.GetFullPath(Path.Combine(basePath, @"..\..\..\..\MachineInfo\appsettings.json"));
                    if (File.Exists(repoRoot))
                    {
                        appsettingsPath = repoRoot;
                    }
                }

                string baseConn = DatabaseBootstrap.ResolveConnectionString(appsettingsPath: appsettingsPath);
                var builder = new NpgsqlConnectionStringBuilder(baseConn)
                {
                    Database = TestDatabaseName
                };
                return builder.ConnectionString;
            }
            catch
            {
                return null;
            }
        }

        [Fact]
        public void SafetyCheck_RefusesToDropDatabase_WhenNameDoesNotEndInTest()
        {
            const string productionConn = "Host=localhost;Database=GenerateMachineKey;Username=postgres;Password=test";
            
            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                DropDatabaseSafely(productionConn);
            });

            Assert.Contains("Refusing to drop database", ex.Message);
            Assert.Contains("does not end with '_test'", ex.Message);
        }

        [Fact]
        public async Task EndToEnd_BootstrapAndGenerateTwice_ProducesOneRowAndSameId()
        {
            string? connStr = GetTestConnectionString();
            if (string.IsNullOrWhiteSpace(connStr))
            {
                // Postgres configuration not available
                return;
            }

            try
            {
                // Verify DB can connect
                var maintenance = new NpgsqlConnectionStringBuilder(connStr) { Database = "postgres" };
                await using (var testConn = new NpgsqlConnection(maintenance.ConnectionString))
                {
                    await testConn.OpenAsync();
                }
            }
            catch
            {
                // Skip if local Postgres instance is not reachable
                return;
            }

            // Clean previous test DB safely
            DropDatabaseSafely(connStr);

            // Step 1: Ensure database & migrations
            await DatabaseBootstrap.EnsureDatabaseAsync(connStr);

            // Step 2: Use case with Npgsql registry
            var registry = new NpgsqlMachineRegistry(connStr);
            var secretProvider = new TestSecretProvider();
            var useCase = new GenerateMachineId(registry, secretProvider);

            const string serial = "TEST-INTEGRATION-99";

            // First run: inserts row
            string id1 = await useCase.ExecuteAsync(serial);
            Assert.NotNull(id1);

            // Verify in database: 1 row
            var record1 = await registry.GetBySerialNumberAsync("TEST-INTEGRATION-99");
            Assert.NotNull(record1);
            Assert.Equal("TEST-INTEGRATION-99", record1.SerialNumber);
            Assert.Equal(id1, record1.MachineId);

            // Second run: looks up existing row and returns same ID without inserting duplicate
            string id2 = await useCase.ExecuteAsync($"  {serial.ToLowerInvariant()}  ");
            Assert.Equal(id1, id2);

            // Verify count remains 1
            await using (var conn = new NpgsqlConnection(connStr))
            {
                await conn.OpenAsync();
                await using var countCmd = new NpgsqlCommand("SELECT COUNT(*) FROM machine_registry WHERE serial_number = 'TEST-INTEGRATION-99';", conn);
                long count = Convert.ToInt64(await countCmd.ExecuteScalarAsync());
                Assert.Equal(1L, count);
            }
        }

        private static void DropDatabaseSafely(string connectionString)
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            string dbName = builder.Database ?? string.Empty;

            if (!dbName.EndsWith("_test", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Refusing to drop database '{dbName}' because its name does not end with '_test'.");
            }

            var maintenanceBuilder = new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres" };
            using var conn = new NpgsqlConnection(maintenanceBuilder.ConnectionString);
            try
            {
                conn.Open();

                // Terminate active connections
                using var termCmd = new NpgsqlCommand(
                    $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{dbName}' AND pid <> pg_backend_pid();", conn);
                termCmd.ExecuteNonQuery();

                using var dropCmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{dbName}\";", conn);
                dropCmd.ExecuteNonQuery();
            }
            catch (PostgresException)
            {
                // If maintenance DB cannot connect or drop fails, propagate or ignore
            }
        }

        private class TestSecretProvider : IAppSecretProvider
        {
            public byte[] GetSecret()
            {
                byte[] secret = new byte[32];
                for (int i = 0; i < 32; i++) secret[i] = (byte)i;
                return secret;
            }
        }
    }
}
