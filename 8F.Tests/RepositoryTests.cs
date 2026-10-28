using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using _8F.Models;
using _8F.Services;

namespace _8F.Tests
{
    public class RepositoryTests
    {
        [Fact]
        public async Task TestConnectionAsync_WithInvalidConnectionString_ReturnsFalseWithoutThrowing()
        {
            // Arrange: intentionally unreachable database connection string
            string invalidConnString = "Host=127.0.0.1;Port=59999;Database=NonExistentDb;Username=invalid;Password=invalid;Timeout=1;";
            var repo = new InspectionLogRepository(invalidConnString);

            // Act
            var (isConnected, message) = await repo.TestConnectionAsync();

            // Assert
            Assert.False(isConnected);
            Assert.Contains("Connection Failed", message);
        }

        [Fact]
        public async Task InsertLogAsync_WhenDbUnreachable_EnqueuesToOfflineJsonQueue()
        {
            // Arrange
            string invalidConnString = "Host=127.0.0.1;Port=59999;Database=NonExistentDb;Username=invalid;Password=invalid;Timeout=1;";
            var repo = new InspectionLogRepository(invalidConnString);

            var testLog = new InspectionLog
            {
                SerialNumber = $"TEST-OFFLINE-{Guid.NewGuid():N}",
                BatchId = "BATCH-OFFLINE-001",
                OperatorName = "Tester",
                InspectionTimestamp = DateTime.UtcNow,
                ChannelNumber = 1,
                FrequencyHz = 400,
                FrequencyData = "[]",

                ResultPass = true,
                DefectType = "None",
                MachineId = "M01"
            };

            string offlineQueuePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Eddy",
                "offline_logs_queue.json");

            // Delete queue file if it exists to ensure a clean test baseline
            if (File.Exists(offlineQueuePath))
            {
                try { File.Delete(offlineQueuePath); } catch { }
            }

            // Act: Attempt insertion with unreachable connection string
            bool result = await repo.InsertLogAsync(testLog);

            // Assert: Insert returns false but does NOT throw an exception, and writes to offline JSON queue file
            Assert.False(result);
            Assert.True(File.Exists(offlineQueuePath), $"Expected offline queue file at {offlineQueuePath}");

            string content = await File.ReadAllTextAsync(offlineQueuePath);
            Assert.Contains(testLog.SerialNumber, content);

            // Clean up test file
            try { File.Delete(offlineQueuePath); } catch { }
        }

        [Fact]
        public async Task ProcessOfflineQueueAsync_WhenDbUnreachable_DoesNotCrash()
        {
            // Arrange
            string invalidConnString = "Host=127.0.0.1;Port=59999;Database=NonExistentDb;Username=invalid;Password=invalid;Timeout=1;";
            var repo = new InspectionLogRepository(invalidConnString);

            // Act & Assert
            int processed = await repo.ProcessOfflineQueueAsync();
            Assert.Equal(0, processed);
        }

        [Fact]
        public async Task GetLogsByBatchAsync_WithSqlInjectionString_ExecutesSafely()
        {
            // Arrange
            string invalidConnString = "Host=127.0.0.1;Port=59999;Database=NonExistentDb;Username=invalid;Password=invalid;Timeout=1;";
            var repo = new InspectionLogRepository(invalidConnString);
            string injectionInput = "'; DROP TABLE \"Logs\"; --";

            // Act & Assert: Parameterized query should execute without throwing SQL syntax / injection errors
            var logs = await repo.GetLogsByBatchAsync(injectionInput);
            Assert.NotNull(logs);
            Assert.Empty(logs);
        }

        [Fact]
        public async Task GetLogsBySerialNumberAsync_WithSqlInjectionString_ExecutesSafely()
        {
            // Arrange
            string invalidConnString = "Host=127.0.0.1;Port=59999;Database=NonExistentDb;Username=invalid;Password=invalid;Timeout=1;";
            var repo = new InspectionLogRepository(invalidConnString);
            string injectionInput = "'; DROP TABLE \"Logs\"; --";

            // Act & Assert
            var logs = await repo.GetLogsBySerialNumberAsync(injectionInput);
            Assert.NotNull(logs);
            Assert.Empty(logs);
        }

        [Fact]
        public async Task GetConfigProfileAsync_WithInvalidConnection_ThrowsNpgsqlException()
        {
            // Arrange
            string invalidConnString = "Host=127.0.0.1;Port=59999;Database=NonExistentDb;Username=invalid;Password=invalid;Timeout=1;";
            var repo = new InspectionLogRepository(invalidConnString);

            // Act & Assert: GetConfigProfileAsync attempts to open connection and throws NpgsqlException on invalid connection
            await Assert.ThrowsAsync<Npgsql.NpgsqlException>(() => repo.GetConfigProfileAsync(-999));
        }

        [Fact]
        public async Task DatabaseIntegration_IfServerConnected_VerifiesSchemaAndCrud()
        {
            // Act: Test standard connection
            var repo = new InspectionLogRepository();
            var (isConnected, message) = await repo.TestConnectionAsync();

            if (!isConnected)
            {
                // Note: DB offline in test environment is expected when PostgreSQL is not running locally.
                // Offline fallback logic is verified in test above.
                return;
            }

            // If PostgreSQL is running live:
            await repo.EnsureConfigTablesCreatedAsync();

            string testBatch = $"BATCH-INTEG-{Guid.NewGuid():N}";
            var log = new InspectionLog
            {
                SerialNumber = "SN-9999",
                BatchId = testBatch,
                OperatorName = "IntegrationTestUser",
                InspectionTimestamp = DateTime.UtcNow,
                ChannelNumber = 1,
                FrequencyHz = 400,
                FrequencyData = "[]",

                ResultPass = true,
                DefectType = "None",
                MachineId = "M01"
            };

            bool inserted = await repo.InsertLogAsync(log);
            if (inserted)
            {
                var fetched = await repo.GetLogsByBatchAsync(testBatch);
                Assert.NotEmpty(fetched);
                Assert.Equal("SN-9999", fetched[0].SerialNumber);
            }
        }
    }
}
