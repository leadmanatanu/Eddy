using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using _8F.Models;
using Npgsql;

namespace _8F.Services
{
    /// <summary>
    /// Thread-safe database repository for managing Eddy Current inspection logs in PostgreSQL.
    /// Includes connection pooling, parameterized queries, and an offline JSON fallback queue for resilience.
    /// </summary>
    public class InspectionLogRepository : IInspectionLogRepository, IAutoEllipseRepository, IConfigProfileRepository
    {
        private readonly string _connectionString;
        private readonly string _offlineQueuePath;
        private readonly string _autoEllipseOfflineQueuePath;

        public InspectionLogRepository(string? connectionString = null)
        {
            // 1. Connection string resolution priority:
            //    Explicit parameter > Environment Variable > App.config > Default fallback
            _connectionString = connectionString 
                ?? Environment.GetEnvironmentVariable("EDDY_DB_CONNECTION_STRING")
                ?? ConfigurationManager.AppSettings["ConnectionString"]
                ?? "Host=localhost;Port=5432;Username=postgres;Password=aryan123;Database=EddyShorter;Pooling=true;Minimum Pool Size=2;Maximum Pool Size=20;";

            var eddyDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Eddy");

            _offlineQueuePath = Path.Combine(eddyDataFolder, "offline_logs_queue.json");
            _autoEllipseOfflineQueuePath = Path.Combine(eddyDataFolder, "auto_ellipse_offline_queue.json");
        }

        #region 1. Connection Setup & Health Check

        /// <summary>
        /// Gets an open NpgsqlConnection wrapped in asynchronous handling.
        /// </summary>
        public async Task<NpgsqlConnection> GetOpenConnectionAsync()
        {
            var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            return conn;
        }

        /// <summary>
        /// Verifies PostgreSQL database connectivity.
        /// </summary>
        public async Task<(bool IsConnected, string Message)> TestConnectionAsync()
        {
            try
            {
                await using var conn = await GetOpenConnectionAsync();
                await using var cmd = new NpgsqlCommand("SELECT 1;", conn);
                var result = await cmd.ExecuteScalarAsync();

                return result != null && (int)result == 1
                    ? (true, "Database connection established successfully.")
                    : (false, "Unexpected response from database.");
            }
            catch (Exception ex)
            {
                return (false, $"Connection Failed: {ex.Message}");
            }
        }

        #endregion

        #region 2. Create / Insert Operations

        /// <summary>
        /// Inserts an inspection record into PostgreSQL. Automatically falls back to offline queue if DB is unreachable.
        /// </summary>
        public async Task<bool> InsertLogAsync(InspectionLog log)
        {
            const string sql = @"
                INSERT INTO public.""Logs"" (
                    ""SerialNumber"", ""BatchId"", ""OperatorName"", ""InspectionTimestamp"",
                    ""ChannelNumber"", ""FrequencyHz"", ""FrequencyData"",
                    ""ResultPass"", ""DefectType"", ""MachineId"", ""CreatedAt""
                ) VALUES (
                    @SerialNumber, @BatchId, @OperatorName, @InspectionTimestamp,
                    @ChannelNumber, @FrequencyHz, @FrequencyData::jsonb,
                    @ResultPass, @DefectType, @MachineId, @CreatedAt
                )
                RETURNING ""Id"";";

            try
            {
                await using var conn = await GetOpenConnectionAsync();
                await using var cmd = new NpgsqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@SerialNumber", log.SerialNumber ?? string.Empty);
                cmd.Parameters.AddWithValue("@BatchId", log.BatchId ?? string.Empty);
                cmd.Parameters.AddWithValue("@OperatorName", log.OperatorName ?? string.Empty);
                cmd.Parameters.AddWithValue("@InspectionTimestamp", log.InspectionTimestamp.ToUniversalTime());
                cmd.Parameters.AddWithValue("@ChannelNumber", log.ChannelNumber);
                cmd.Parameters.AddWithValue("@FrequencyHz", log.FrequencyHz);
                cmd.Parameters.AddWithValue("@FrequencyData", string.IsNullOrWhiteSpace(log.FrequencyData) ? "[]" : log.FrequencyData);
                cmd.Parameters.AddWithValue("@ResultPass", log.ResultPass);
                cmd.Parameters.AddWithValue("@DefectType", (object?)log.DefectType ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@MachineId", log.MachineId ?? string.Empty);
                cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

                var insertedId = await cmd.ExecuteScalarAsync();
                if (insertedId != null && insertedId != DBNull.Value)
                {
                    log.Id = Convert.ToInt64(insertedId);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                // Fallback: Queue record locally to prevent inspection data loss
                EnqueueOfflineLog(log, ex.Message);
                return false;
            }
        }

        #endregion

        #region 3. Read / Query Operations
        public async Task<List<InspectionLog>> GetLogsByBatchAsync(string batchId)
        {
            const string sql = @"
                SELECT ""Id"", ""SerialNumber"", ""BatchId"", ""OperatorName"", ""InspectionTimestamp"",
                       ""ChannelNumber"", ""FrequencyHz"", ""FrequencyData"", ""ResultPass"",
                       ""DefectType"", ""MachineId"", ""CreatedAt""
                FROM public.""Logs""
                WHERE ""BatchId"" = @BatchId
                ORDER BY ""InspectionTimestamp"" ASC;";

            var list = new List<InspectionLog>();

            try
            {
                await using var conn = await GetOpenConnectionAsync();
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@BatchId", batchId);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(MapLog(reader));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error querying by BatchId: {ex.Message}");
            }

            return list;
        }

        /// <summary>
        /// Queries inspection records by Component Serial Number.
        /// </summary>
        public async Task<List<InspectionLog>> GetLogsBySerialNumberAsync(string serialNumber)
        {
            const string sql = @"
                SELECT ""Id"", ""SerialNumber"", ""BatchId"", ""OperatorName"", ""InspectionTimestamp"",
                       ""ChannelNumber"", ""FrequencyHz"", ""FrequencyData"", ""ResultPass"",
                       ""DefectType"", ""MachineId"", ""CreatedAt""
                FROM public.""Logs""
                WHERE ""SerialNumber"" = @SerialNumber
                ORDER BY ""InspectionTimestamp"" DESC;";

            var list = new List<InspectionLog>();

            try
            {
                await using var conn = await GetOpenConnectionAsync();
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@SerialNumber", serialNumber);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(MapLog(reader));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error querying by SerialNumber: {ex.Message}");
            }

            return list;
        }

        /// <summary>
        /// Queries inspection records within a specified date range.
        /// </summary>
        public async Task<List<InspectionLog>> GetLogsByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            const string sql = @"
                SELECT ""Id"", ""SerialNumber"", ""BatchId"", ""OperatorName"", ""InspectionTimestamp"",
                       ""ChannelNumber"", ""FrequencyHz"", ""FrequencyData"", ""ResultPass"",
                       ""DefectType"", ""MachineId"", ""CreatedAt""
                FROM public.""Logs""
                WHERE ""InspectionTimestamp"" BETWEEN @StartDate AND @EndDate
                ORDER BY ""InspectionTimestamp"" DESC;";

            var list = new List<InspectionLog>();

            try
            {
                await using var conn = await GetOpenConnectionAsync();
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@StartDate", startDate.ToUniversalTime());
                cmd.Parameters.AddWithValue("@EndDate", endDate.ToUniversalTime());

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(MapLog(reader));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error querying by DateRange: {ex.Message}");
            }

            return list;
        }

        #endregion

        #region 4. Delete / Maintenance Operations

        /// <summary>
        /// Purges logs older than a specified number of days to maintain database size.
        /// </summary>
        public async Task<int> DeleteLogsOlderThanDaysAsync(int daysToKeep)
        {
            const string sql = @"
                DELETE FROM public.""Logs""
                WHERE ""InspectionTimestamp"" < @CutoffDate;";

            try
            {
                await using var conn = await GetOpenConnectionAsync();
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@CutoffDate", DateTime.UtcNow.AddDays(-daysToKeep));

                return await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error purging logs: {ex.Message}");
                return 0;
            }
        }

        #endregion

        #region 5. Offline Queue Fallback & Sync

        private void EnqueueOfflineLog(InspectionLog log, string reason)
        {
            try
            {
                var dir = Path.GetDirectoryName(_offlineQueuePath);
                if (!Directory.Exists(dir) && dir != null)
                {
                    Directory.CreateDirectory(dir);
                }

                List<InspectionLog> queue = new();
                if (File.Exists(_offlineQueuePath))
                {
                    var existingJson = File.ReadAllText(_offlineQueuePath);
                    queue = JsonSerializer.Deserialize<List<InspectionLog>>(existingJson) ?? new();
                }

                queue.Add(log);
                File.WriteAllText(_offlineQueuePath, JsonSerializer.Serialize(queue, new JsonSerializerOptions { WriteIndented = true }));
                System.Diagnostics.Debug.WriteLine($"Log queued offline ({reason}). Current queue size: {queue.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Critical: Failed to queue offline log: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes and syncs locally queued logs once PostgreSQL connectivity is restored.
        /// </summary>
        public async Task<int> ProcessOfflineQueueAsync()
        {
            if (!File.Exists(_offlineQueuePath)) return 0;

            List<InspectionLog>? queue;
            try
            {
                var json = await File.ReadAllTextAsync(_offlineQueuePath);
                queue = JsonSerializer.Deserialize<List<InspectionLog>>(json);
                if (queue == null || queue.Count == 0) return 0;
            }
            catch
            {
                return 0;
            }

            int syncedCount = 0;
            var remainingQueue = new List<InspectionLog>();

            foreach (var log in queue)
            {
                bool success = await InsertLogAsync(log);
                if (success)
                {
                    syncedCount++;
                }
                else
                {
                    remainingQueue.Add(log);
                }
            }

            if (remainingQueue.Count > 0)
            {
                await File.WriteAllTextAsync(_offlineQueuePath, JsonSerializer.Serialize(remainingQueue));
            }
            else
            {
                File.Delete(_offlineQueuePath);
            }

            return syncedCount;
        }

        #endregion

        #region 6. Auto Ellipse Operations

        /// <summary>
        /// Inserts a raw multi-frequency Auto Ellipse test run into PostgreSQL.
        /// </summary>
        public async Task<bool> InsertAutoEllipseTestAsync(AutoEllipseTest test)
        {
            const string sql = @"
                INSERT INTO public.""AutoEllipseTests"" (
                    ""ChId"", ""TestNumber"", ""TimeStamp"", ""OperatorName"", ""FrequencyValues"", ""IsDeleted""
                ) VALUES (
                    @ChId, @TestNumber, @TimeStamp, @OperatorName, @FrequencyValues::json, @IsDeleted
                )
                RETURNING ""Id"";";

            try
            {
                await using var conn = await GetOpenConnectionAsync();
                await using var cmd = new NpgsqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@ChId", test.ChannelId);
                cmd.Parameters.AddWithValue("@TestNumber", test.TestNumber);
                cmd.Parameters.AddWithValue("@TimeStamp", test.TimeStamp.ToUniversalTime());
                cmd.Parameters.AddWithValue("@OperatorName", (object?)test.OperatorName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@FrequencyValues", string.IsNullOrWhiteSpace(test.FrequencyValuesJson) ? "{}" : test.FrequencyValuesJson);
                cmd.Parameters.AddWithValue("@IsDeleted", test.IsDeleted);

                var insertedId = await cmd.ExecuteScalarAsync();
                if (insertedId != null && insertedId != DBNull.Value)
                {
                    test.Id = Convert.ToInt64(insertedId);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                EnqueueOfflineAutoEllipseTest(test, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Hard-deletes a raw Auto Ellipse test run permanently from PostgreSQL.
        /// </summary>
        public async Task<bool> DeleteAutoEllipseTestAsync(long testId)
        {
            const string sql = @"
                DELETE FROM public.""AutoEllipseTests""
                WHERE ""Id"" = @Id;";

            try
            {
                await using var conn = await GetOpenConnectionAsync();
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Id", testId);

                int rows = await cmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error hard deleting AutoEllipse test record: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Fetches all active (non-deleted) raw Auto Ellipse test runs for a given channel from PostgreSQL.
        /// </summary>
        public async Task<List<AutoEllipseTest>> GetAutoEllipseTestsByChannelAsync(int channelId)
        {
            List<AutoEllipseTest> list = new();
            const string sql = @"
                SELECT ""Id"", ""ChId"", ""TestNumber"", ""TimeStamp"", ""OperatorName"", ""FrequencyValues""
                FROM public.""AutoEllipseTests""
                WHERE ""ChId"" = @ChId AND ""IsDeleted"" = FALSE
                ORDER BY ""TimeStamp"" ASC, ""Id"" ASC;";

            try
            {
                await using var conn = await GetOpenConnectionAsync();
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ChId", channelId);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new AutoEllipseTest
                    {
                        Id = reader.GetInt64(0),
                        ChannelId = reader.GetInt32(1),
                        TestNumber = reader.GetInt32(2),
                        TimeStamp = reader.GetDateTime(3),
                        OperatorName = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                        FrequencyValuesJson = reader.IsDBNull(5) ? "{}" : reader.GetString(5),
                        IsDeleted = false
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetAutoEllipseTestsByChannelAsync error: {ex.Message}");
            }
            return list;
        }

        /// <summary>
        /// Inserts a computed Auto Ellipse result record into PostgreSQL for audit logging.
        /// </summary>
        public async Task<bool> InsertAutoEllipseResultAsync(AutoEllipseResultRecord result)
        {
            const string sql = @"
                INSERT INTO public.""AutoEllipseResults"" (
                    ""ChId"", ""Frequency"", ""TimeStamp"", ""SelectedTestIds"",
                    ""ComputedCenterX"", ""ComputedCenterY"",
                    ""ComputedWidth"", ""ComputedHeight"", ""ComputedRotationAngle"",
                    ""SampleCount""
                ) VALUES (
                    @ChId, @Frequency, @TimeStamp, @SelectedTestIds::json,
                    @ComputedCenterX, @ComputedCenterY,
                    @ComputedWidth, @ComputedHeight, @ComputedRotationAngle,
                    @SampleCount
                )
                RETURNING ""Id"";";

            try
            {
                await using var conn = await GetOpenConnectionAsync();
                await using var cmd = new NpgsqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@ChId", result.ChannelId);
                cmd.Parameters.AddWithValue("@Frequency", result.Frequency ?? string.Empty);
                cmd.Parameters.AddWithValue("@TimeStamp", result.TimeStamp.ToUniversalTime());
                cmd.Parameters.AddWithValue("@SelectedTestIds", string.IsNullOrWhiteSpace(result.SelectedTestIdsJson) ? "[]" : result.SelectedTestIdsJson);
                cmd.Parameters.AddWithValue("@ComputedCenterX", result.ComputedCenterX);
                cmd.Parameters.AddWithValue("@ComputedCenterY", result.ComputedCenterY);
                cmd.Parameters.AddWithValue("@ComputedWidth", result.ComputedWidth);
                cmd.Parameters.AddWithValue("@ComputedHeight", result.ComputedHeight);
                cmd.Parameters.AddWithValue("@ComputedRotationAngle", result.ComputedRotationAngle);
                cmd.Parameters.AddWithValue("@SampleCount", result.SampleCount);

                var insertedId = await cmd.ExecuteScalarAsync();
                if (insertedId != null && insertedId != DBNull.Value)
                {
                    result.Id = Convert.ToInt64(insertedId);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error inserting AutoEllipse result audit: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Updates AutoEllipseTests rows for a channel setting Applied = true and AppliedTimeStamp = now.
        /// </summary>
        public async Task<bool> UpdateAutoEllipseTestsAppliedAsync(int channelId)
        {
            const string sql = @"
                UPDATE public.""AutoEllipseTests""
                SET ""Applied"" = TRUE, ""AppliedTimeStamp"" = @AppliedTimeStamp
                WHERE ""ChId"" = @ChId AND ""Applied"" = FALSE;";

            try
            {
                await using var conn = await GetOpenConnectionAsync();
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ChId", channelId);
                cmd.Parameters.AddWithValue("@AppliedTimeStamp", DateTime.UtcNow);

                int rows = await cmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating AutoEllipseTests applied state: {ex.Message}");
                return false;
            }
        }

        private void EnqueueOfflineAutoEllipseTest(AutoEllipseTest test, string reason)
        {
            try
            {
                var dir = Path.GetDirectoryName(_autoEllipseOfflineQueuePath);
                if (!Directory.Exists(dir) && dir != null)
                {
                    Directory.CreateDirectory(dir);
                }

                List<AutoEllipseTest> queue = new();
                if (File.Exists(_autoEllipseOfflineQueuePath))
                {
                    var json = File.ReadAllText(_autoEllipseOfflineQueuePath);
                    queue = JsonSerializer.Deserialize<List<AutoEllipseTest>>(json) ?? new();
                }

                queue.Add(test);
                File.WriteAllText(_autoEllipseOfflineQueuePath, JsonSerializer.Serialize(queue, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Critical: Failed to queue offline auto ellipse test: {ex.Message}");
            }
        }

        #endregion

        #region 6. Channel Configuration Persistence

        /// <summary>
        /// Ensures that the 4 channel configuration persistence tables exist in PostgreSQL.
        /// </summary>
        public async Task EnsureConfigTablesCreatedAsync()
        {
            const string sql = @"
                CREATE TABLE IF NOT EXISTS public.""ConfigProfiles"" (
                    ""Id"" integer NOT NULL GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    ""Name"" text NOT NULL,
                    ""OperatorName"" text,
                    ""CreatedAt"" timestamp without time zone NOT NULL DEFAULT now(),
                    ""UpdatedAt"" timestamp without time zone NOT NULL DEFAULT now()
                );

                CREATE TABLE IF NOT EXISTS public.""ConfigChannels"" (
                    ""Id"" integer NOT NULL GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    ""ConfigProfileId"" integer NOT NULL REFERENCES public.""ConfigProfiles""(""Id"") ON DELETE CASCADE,
                    ""ChannelNumber"" integer NOT NULL,
                    ""IsSelected"" boolean NOT NULL DEFAULT false,
                    ""TxStrength"" numeric
                );

                CREATE TABLE IF NOT EXISTS public.""ConfigFrequencies"" (
                    ""Id"" integer NOT NULL GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    ""ConfigChannelId"" integer NOT NULL REFERENCES public.""ConfigChannels""(""Id"") ON DELETE CASCADE,
                    ""FrequencyNumber"" integer NOT NULL,
                    ""Name"" text NOT NULL,
                    ""Freq"" numeric,
                    ""Gain"" numeric,
                    ""Phase"" numeric,
                    ""IsEnable"" boolean NOT NULL DEFAULT true,
                    ""Strength"" numeric,
                    ""PostGain"" numeric,
                    ""Height"" numeric,
                    ""Width"" numeric,
                    ""Ex"" numeric,
                    ""Ey"" numeric,
                    ""Angel"" numeric,
                    ""HeightO"" numeric,
                    ""WidthO"" numeric,
                    ""ExO"" numeric,
                    ""EyO"" numeric,
                    ""AngelO"" numeric
                );

                CREATE TABLE IF NOT EXISTS public.""ConfigEllipses"" (
                    ""Id"" integer NOT NULL GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    ""ConfigFrequencyId"" integer NOT NULL REFERENCES public.""ConfigFrequencies""(""Id"") ON DELETE CASCADE,
                    ""EllipseIndex"" integer NOT NULL,
                    ""Height"" numeric,
                    ""Width"" numeric,
                    ""Ex"" numeric,
                    ""Ey"" numeric,
                    ""Angel"" numeric
                );";

            try
            {
                await using var conn = await GetOpenConnectionAsync();
                await using var cmd = new NpgsqlCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error ensuring config tables created: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves a full ChannelData configuration profile transactionally across 4 tables.
        /// </summary>
        public async Task<int> SaveConfigProfileAsync(string name, string? operatorName, List<ChannelData> channelDatas)
        {
            await EnsureConfigTablesCreatedAsync();

            await using var conn = await GetOpenConnectionAsync();
            await using var tx = await conn.BeginTransactionAsync();

            try
            {
                // 1. Insert ConfigProfile
                const string sqlProfile = @"
                    INSERT INTO public.""ConfigProfiles"" (""Name"", ""OperatorName"", ""CreatedAt"", ""UpdatedAt"")
                    VALUES (@Name, @OperatorName, @CreatedAt, @UpdatedAt)
                    RETURNING ""Id"";";

                await using var cmdProfile = new NpgsqlCommand(sqlProfile, conn, tx);
                cmdProfile.Parameters.AddWithValue("@Name", string.IsNullOrWhiteSpace(name) ? "Untitled Config" : name);
                cmdProfile.Parameters.AddWithValue("@OperatorName", (object?)operatorName ?? DBNull.Value);
                cmdProfile.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);
                cmdProfile.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);

                int profileId = Convert.ToInt32(await cmdProfile.ExecuteScalarAsync());

                // 2. Insert ConfigChannels
                foreach (var ch in channelDatas)
                {
                    const string sqlChannel = @"
                        INSERT INTO public.""ConfigChannels"" (""ConfigProfileId"", ""ChannelNumber"", ""IsSelected"", ""TxStrength"")
                        VALUES (@ConfigProfileId, @ChannelNumber, @IsSelected, @TxStrength)
                        RETURNING ""Id"";";

                    await using var cmdChannel = new NpgsqlCommand(sqlChannel, conn, tx);
                    cmdChannel.Parameters.AddWithValue("@ConfigProfileId", profileId);
                    cmdChannel.Parameters.AddWithValue("@ChannelNumber", ch.Id);
                    cmdChannel.Parameters.AddWithValue("@IsSelected", ch.IsSeleted);
                    cmdChannel.Parameters.AddWithValue("@TxStrength", ch.TxStrength);

                    int channelDbId = Convert.ToInt32(await cmdChannel.ExecuteScalarAsync());

                    // 3. Insert ConfigFrequencies
                    if (ch.graphDatas != null)
                    {
                        foreach (var graph in ch.graphDatas)
                        {
                            const string sqlFreq = @"
                                INSERT INTO public.""ConfigFrequencies"" (
                                    ""ConfigChannelId"", ""FrequencyNumber"", ""Name"", ""Freq"", ""Gain"", ""Phase"", ""IsEnable"",
                                    ""Strength"", ""PostGain"", ""Height"", ""Width"", ""Ex"", ""Ey"", ""Angel"",
                                    ""HeightO"", ""WidthO"", ""ExO"", ""EyO"", ""AngelO""
                                ) VALUES (
                                    @ConfigChannelId, @FrequencyNumber, @Name, @Freq, @Gain, @Phase, @IsEnable,
                                    @Strength, @PostGain, @Height, @Width, @Ex, @Ey, @Angel,
                                    @HeightO, @WidthO, @ExO, @EyO, @AngelO
                                ) RETURNING ""Id"";";

                            await using var cmdFreq = new NpgsqlCommand(sqlFreq, conn, tx);
                            cmdFreq.Parameters.AddWithValue("@ConfigChannelId", channelDbId);
                            cmdFreq.Parameters.AddWithValue("@FrequencyNumber", graph.Id);
                            cmdFreq.Parameters.AddWithValue("@Name", graph.Name ?? $"D{graph.Id}");
                            cmdFreq.Parameters.AddWithValue("@Freq", graph.freq);
                            cmdFreq.Parameters.AddWithValue("@Gain", graph.gain);
                            cmdFreq.Parameters.AddWithValue("@Phase", graph.phase);
                            cmdFreq.Parameters.AddWithValue("@IsEnable", graph.isEnable);
                            cmdFreq.Parameters.AddWithValue("@Strength", graph.strength);
                            cmdFreq.Parameters.AddWithValue("@PostGain", graph.postGain);

                            cmdFreq.Parameters.AddWithValue("@Height", graph.height);
                            cmdFreq.Parameters.AddWithValue("@Width", graph.width);
                            cmdFreq.Parameters.AddWithValue("@Ex", graph.ex);
                            cmdFreq.Parameters.AddWithValue("@Ey", graph.ey);
                            cmdFreq.Parameters.AddWithValue("@Angel", graph.angel);

                            cmdFreq.Parameters.AddWithValue("@HeightO", graph.height_O);
                            cmdFreq.Parameters.AddWithValue("@WidthO", graph.width_O);
                            cmdFreq.Parameters.AddWithValue("@ExO", graph.ex_O);
                            cmdFreq.Parameters.AddWithValue("@EyO", graph.ey_O);
                            cmdFreq.Parameters.AddWithValue("@AngelO", graph.angel_O);

                            int freqDbId = Convert.ToInt32(await cmdFreq.ExecuteScalarAsync());

                            // 4. Insert ConfigEllipses
                            if (graph.ellipses != null)
                            {
                                foreach (var ell in graph.ellipses)
                                {
                                    const string sqlEll = @"
                                        INSERT INTO public.""ConfigEllipses"" (
                                            ""ConfigFrequencyId"", ""EllipseIndex"", ""Height"", ""Width"", ""Ex"", ""Ey"", ""Angel""
                                        ) VALUES (
                                            @ConfigFrequencyId, @EllipseIndex, @Height, @Width, @Ex, @Ey, @Angel
                                        );";

                                    await using var cmdEll = new NpgsqlCommand(sqlEll, conn, tx);
                                    cmdEll.Parameters.AddWithValue("@ConfigFrequencyId", freqDbId);
                                    cmdEll.Parameters.AddWithValue("@EllipseIndex", ell.Id);
                                    cmdEll.Parameters.AddWithValue("@Height", ell.height);
                                    cmdEll.Parameters.AddWithValue("@Width", ell.width);
                                    cmdEll.Parameters.AddWithValue("@Ex", ell.ex);
                                    cmdEll.Parameters.AddWithValue("@Ey", ell.ey);
                                    cmdEll.Parameters.AddWithValue("@Angel", ell.angel);

                                    await cmdEll.ExecuteNonQueryAsync();
                                }
                            }
                        }
                    }
                }

                await tx.CommitAsync();
                return profileId;
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                System.Diagnostics.Debug.WriteLine($"Error saving config profile: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Retrieves a saved ChannelData configuration profile from PostgreSQL by profile ID.
        /// </summary>
        public async Task<List<ChannelData>> GetConfigProfileAsync(int profileId)
        {
            List<ChannelData> channels = new();

            await using var conn = await GetOpenConnectionAsync();

            const string sqlChannels = @"
                SELECT ""Id"", ""ChannelNumber"", ""IsSelected"", ""TxStrength""
                FROM public.""ConfigChannels""
                WHERE ""ConfigProfileId"" = @ProfileId
                ORDER BY ""ChannelNumber"";";

            await using var cmdChannels = new NpgsqlCommand(sqlChannels, conn);
            cmdChannels.Parameters.AddWithValue("@ProfileId", profileId);

            await using var readerChannels = await cmdChannels.ExecuteReaderAsync();
            List<(int DbId, int ChannelNumber, bool IsSelected, double TxStrength)> channelRows = new();
            while (await readerChannels.ReadAsync())
            {
                int dbId = readerChannels.GetInt32(0);
                int chNum = readerChannels.GetInt32(1);
                bool isSel = readerChannels.GetBoolean(2);
                double txStr = readerChannels.IsDBNull(3) ? 0.0 : Convert.ToDouble(readerChannels.GetDecimal(3));
                channelRows.Add((dbId, chNum, isSel, txStr));
            }
            await readerChannels.CloseAsync();

            foreach (var chRow in channelRows)
            {
                ChannelData ch = new ChannelData
                {
                    Id = chRow.ChannelNumber,
                    IsSeleted = chRow.IsSelected,
                    TxStrength = Convert.ToInt32(chRow.TxStrength),
                    graphDatas = new List<GraphData>()
                };

                const string sqlFreqs = @"
                    SELECT ""Id"", ""FrequencyNumber"", ""Name"", ""Freq"", ""Gain"", ""Phase"", ""IsEnable"",
                           ""Strength"", ""PostGain"", ""Height"", ""Width"", ""Ex"", ""Ey"", ""Angel"",
                           ""HeightO"", ""WidthO"", ""ExO"", ""EyO"", ""AngelO""
                    FROM public.""ConfigFrequencies""
                    WHERE ""ConfigChannelId"" = @ConfigChannelId
                    ORDER BY ""FrequencyNumber"";";

                await using var cmdFreqs = new NpgsqlCommand(sqlFreqs, conn);
                cmdFreqs.Parameters.AddWithValue("@ConfigChannelId", chRow.DbId);

                await using var readerFreqs = await cmdFreqs.ExecuteReaderAsync();
                List<(int DbId, GraphData Graph)> freqRows = new();
                while (await readerFreqs.ReadAsync())
                {
                    int freqDbId = readerFreqs.GetInt32(0);
                    GraphData g = new GraphData
                    {
                        Id = readerFreqs.GetInt32(1),
                        Name = readerFreqs.GetString(2),
                        freq = readerFreqs.IsDBNull(3) ? 0 : Convert.ToInt32(readerFreqs.GetDecimal(3)),
                        gain = readerFreqs.IsDBNull(4) ? 0 : Convert.ToInt32(readerFreqs.GetDecimal(4)),
                        phase = readerFreqs.IsDBNull(5) ? 0 : Convert.ToInt32(readerFreqs.GetDecimal(5)),
                        isEnable = readerFreqs.GetBoolean(6),
                        strength = readerFreqs.IsDBNull(7) ? 0 : Convert.ToInt32(readerFreqs.GetDecimal(7)),
                        postGain = readerFreqs.IsDBNull(8) ? 0 : Convert.ToInt32(readerFreqs.GetDecimal(8)),
                        height = readerFreqs.IsDBNull(9) ? 0 : Convert.ToDouble(readerFreqs.GetDecimal(9)),
                        width = readerFreqs.IsDBNull(10) ? 0 : Convert.ToDouble(readerFreqs.GetDecimal(10)),
                        ex = readerFreqs.IsDBNull(11) ? 0 : Convert.ToDouble(readerFreqs.GetDecimal(11)),
                        ey = readerFreqs.IsDBNull(12) ? 0 : Convert.ToDouble(readerFreqs.GetDecimal(12)),
                        angel = readerFreqs.IsDBNull(13) ? 0 : Convert.ToDouble(readerFreqs.GetDecimal(13)),
                        height_O = readerFreqs.IsDBNull(14) ? 0 : Convert.ToDouble(readerFreqs.GetDecimal(14)),
                        width_O = readerFreqs.IsDBNull(15) ? 0 : Convert.ToDouble(readerFreqs.GetDecimal(15)),
                        ex_O = readerFreqs.IsDBNull(16) ? 0 : Convert.ToDouble(readerFreqs.GetDecimal(16)),
                        ey_O = readerFreqs.IsDBNull(17) ? 0 : Convert.ToDouble(readerFreqs.GetDecimal(17)),
                        angel_O = readerFreqs.IsDBNull(18) ? 0 : Convert.ToDouble(readerFreqs.GetDecimal(18)),
                        ellipses = new List<Ellips>()
                    };
                    freqRows.Add((freqDbId, g));
                }
                await readerFreqs.CloseAsync();

                foreach (var fRow in freqRows)
                {
                    const string sqlElls = @"
                        SELECT ""EllipseIndex"", ""Height"", ""Width"", ""Ex"", ""Ey"", ""Angel""
                        FROM public.""ConfigEllipses""
                        WHERE ""ConfigFrequencyId"" = @ConfigFrequencyId
                        ORDER BY ""EllipseIndex"";";

                    await using var cmdElls = new NpgsqlCommand(sqlElls, conn);
                    cmdElls.Parameters.AddWithValue("@ConfigFrequencyId", fRow.DbId);

                    await using var readerElls = await cmdElls.ExecuteReaderAsync();
                    while (await readerElls.ReadAsync())
                    {
                        Ellips ell = new Ellips
                        {
                            Id = readerElls.GetInt32(0),
                            height = readerElls.IsDBNull(1) ? 0 : Convert.ToDouble(readerElls.GetDecimal(1)),
                            width = readerElls.IsDBNull(2) ? 0 : Convert.ToDouble(readerElls.GetDecimal(2)),
                            ex = readerElls.IsDBNull(3) ? 0 : Convert.ToDouble(readerElls.GetDecimal(3)),
                            ey = readerElls.IsDBNull(4) ? 0 : Convert.ToDouble(readerElls.GetDecimal(4)),
                            angel = readerElls.IsDBNull(5) ? 0 : Convert.ToDouble(readerElls.GetDecimal(5))
                        };
                        fRow.Graph.ellipses.Add(ell);
                    }
                    await readerElls.CloseAsync();

                    ch.graphDatas.Add(fRow.Graph);
                }

                channels.Add(ch);
            }

            return channels;
        }

        /// <summary>
        /// Retrieves the name of a saved configuration profile by its ID.
        /// </summary>
        public async Task<string?> GetProfileNameAsync(int profileId)
        {
            await using var conn = await GetOpenConnectionAsync();
            const string sql = @"SELECT ""Name"" FROM public.""ConfigProfiles"" WHERE ""Id"" = @ProfileId LIMIT 1;";
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ProfileId", profileId);
            var result = await cmd.ExecuteScalarAsync();
            return result != null && result != DBNull.Value ? result.ToString() : null;
        }

        /// <summary>
        /// Lists available configuration profiles for selection UI or audit.
        /// </summary>
        public async Task<List<ConfigProfileSummary>> ListConfigProfilesAsync()
        {
            await EnsureConfigTablesCreatedAsync();
            List<ConfigProfileSummary> summaries = new();

            await using var conn = await GetOpenConnectionAsync();

            const string sql = @"
                SELECT ""Id"", ""Name"", ""OperatorName"", ""CreatedAt"", ""UpdatedAt""
                FROM public.""ConfigProfiles""
                ORDER BY ""CreatedAt"" DESC;";

            await using var cmd = new NpgsqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                summaries.Add(new ConfigProfileSummary
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    OperatorName = reader.IsDBNull(2) ? null : reader.GetString(2),
                    CreatedAt = reader.GetDateTime(3),
                    UpdatedAt = reader.GetDateTime(4)
                });
            }

            return summaries;
        }

        #endregion

        #region Helper Mapper

        private static InspectionLog MapLog(NpgsqlDataReader reader)
        {
            return new InspectionLog
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
                SerialNumber = reader.GetString(reader.GetOrdinal("SerialNumber")),
                BatchId = reader.GetString(reader.GetOrdinal("BatchId")),
                OperatorName = reader.GetString(reader.GetOrdinal("OperatorName")),
                InspectionTimestamp = reader.GetDateTime(reader.GetOrdinal("InspectionTimestamp")),
                ChannelNumber = reader.GetInt32(reader.GetOrdinal("ChannelNumber")),
                FrequencyHz = reader.GetDouble(reader.GetOrdinal("FrequencyHz")),
                FrequencyData = reader.IsDBNull(reader.GetOrdinal("FrequencyData")) ? "[]" : reader.GetString(reader.GetOrdinal("FrequencyData")),
                ResultPass = reader.GetBoolean(reader.GetOrdinal("ResultPass")),
                DefectType = reader.IsDBNull(reader.GetOrdinal("DefectType")) ? null : reader.GetString(reader.GetOrdinal("DefectType")),
                MachineId = reader.GetString(reader.GetOrdinal("MachineId")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
            };
        }

        #endregion
    }

    public class ConfigProfileSummary
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? OperatorName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
