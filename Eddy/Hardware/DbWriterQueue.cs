using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Npgsql;

namespace Eddy
{
    public class TubeSaveItem
    {
        public string SpoolId { get; set; } = Guid.NewGuid().ToString("N");
        public volatile bool IsInserted = false;
        public DateTime TimeStamp { get; set; }
        public Part? Part { get; set; }
        public Configuration? Configuration { get; set; }
        public GraphData GraphData { get; set; } = null!;
        public string BatchName { get; set; } = string.Empty;
        public bool Result { get; set; }
    }

    public static class DbWriterQueue
    {
        private static readonly BlockingCollection<TubeSaveItem> _spoolQueue = new();
        private static readonly BlockingCollection<TubeSaveItem> _queue = new();
        private static readonly Thread _spoolThread;
        private static readonly Thread _workerThread;
        private static volatile bool _isWriting = false;
        private static volatile bool _isSpooling = false;

        public static event Action<string>? OnStatusMessage;
        public static string? LastStatusMessage { get; private set; }

        public static int PendingCount => _queue.Count + (_isWriting ? 1 : 0);

        static DbWriterQueue()
        {
            _spoolThread = new Thread(ProcessSpoolQueue)
            {
                IsBackground = true,
                Name = "DbSpoolThread"
            };
            _spoolThread.Start();

            _workerThread = new Thread(ProcessQueue)
            {
                IsBackground = true,
                Name = "DbWriterQueueThread"
            };
            _workerThread.Start();

            // Run recovery once at startup in the background
            Task.Run(ReimportFailedLogs);
        }

        public static void Enqueue(TubeSaveItem item)
        {
            if (item == null) return;
            try
            {
                _spoolQueue.Add(item);
                _queue.Add(item);

                int pending = PendingCount;
                if (pending > 50)
                {
                    string msg = $"Warning: Database save queue has {pending} pending tubes. Writing in background...";
                    LastStatusMessage = msg;
                    try
                    {
                        OnStatusMessage?.Invoke(msg);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DbWriterQueue] Enqueue error: {ex.Message}");
                SaveFallback(item, ex);
            }
        }

        public static bool Flush(int timeoutMs = 5000)
        {
            var sw = Stopwatch.StartNew();
            while (_spoolQueue.Count > 0 || _queue.Count > 0 || _isWriting || _isSpooling)
            {
                if (sw.ElapsedMilliseconds > timeoutMs)
                {
                    return false;
                }
                Thread.Sleep(20);
            }
            return true;
        }

        private static string GetPendingDir()
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "FailedLogs", "pending");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void ProcessSpoolQueue()
        {
            foreach (var item in _spoolQueue.GetConsumingEnumerable())
            {
                _isSpooling = true;
                try
                {
                    WriteSpool(item);
                }
                finally
                {
                    _isSpooling = false;
                }
            }
        }

        private static void WriteSpool(TubeSaveItem item)
        {
            // If the row was already inserted before the spool thread starts writing, skip writing
            if (item.IsInserted)
            {
                DeleteSpoolFile(item.SpoolId);
                return;
            }

            string dir = GetPendingDir();
            string file = Path.Combine(dir, $"{item.SpoolId}.json");
            string tmp = Path.Combine(dir, $"{item.SpoolId}.json.tmp");

            try
            {
                string json = JsonConvert.SerializeObject(item);
                File.WriteAllText(tmp, json);

                // Check again: if inserted while we were serializing/writing to tmp, delete and skip rename
                if (item.IsInserted)
                {
                    try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                    DeleteSpoolFile(item.SpoolId);
                    return;
                }

                // Atomic rename to .json
                File.Move(tmp, file, overwrite: true);

                if (item.IsInserted)
                {
                    DeleteSpoolFile(item.SpoolId);
                }
            }
            catch (Exception ex)
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                if (item.IsInserted)
                {
                    DeleteSpoolFile(item.SpoolId);
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[DbWriterQueue] Spool write failed: {ex.Message}");
                string msg = $"Warning: Could not write spool file for file '{item.BatchName}'. Tube remains queued in memory.";
                LastStatusMessage = msg;
                try
                {
                    OnStatusMessage?.Invoke(msg);
                }
                catch { }
            }
        }

        private static void DeleteSpoolFile(string spoolId)
        {
            if (string.IsNullOrEmpty(spoolId)) return;
            try
            {
                string dir = GetPendingDir();
                string file = Path.Combine(dir, $"{spoolId}.json");
                string tmp = Path.Combine(dir, $"{spoolId}.json.tmp");
                if (File.Exists(file)) File.Delete(file);
                if (File.Exists(tmp)) File.Delete(tmp);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DbWriterQueue] Delete spool file error: {ex.Message}");
            }
        }

        private static void QuarantineCorruptSpoolFile(string filePath)
        {
            try
            {
                string corruptDir = Path.Combine(AppContext.BaseDirectory, "FailedLogs", "corrupt");
                Directory.CreateDirectory(corruptDir);
                string target = Path.Combine(corruptDir, Path.GetFileName(filePath));
                if (File.Exists(target)) File.Delete(target);
                File.Move(filePath, target);

                string msg = $"Warning: Corrupt spool file '{Path.GetFileName(filePath)}' quarantined to FailedLogs/corrupt/.";
                LastStatusMessage = msg;
                try { OnStatusMessage?.Invoke(msg); } catch { }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DbWriterQueue] Quarantine failed: {ex.Message}");
            }
        }

        private static void ProcessQueue()
        {
            foreach (var item in _queue.GetConsumingEnumerable())
            {
                _isWriting = true;
                try
                {
                    SaveWithRetry(item);
                }
                catch (Exception ex)
                {
                    SaveFallback(item, ex);
                }
                finally
                {
                    _isWriting = false;
                    if (_queue.Count == 0 && LastStatusMessage != null && LastStatusMessage.StartsWith("Warning: Database save queue"))
                    {
                        LastStatusMessage = null;
                        try { OnStatusMessage?.Invoke("All pending database saves completed."); } catch { }
                    }
                }
            }
        }

        private static void SaveWithRetry(TubeSaveItem item)
        {
            const int maxRetries = 3;
            int attempt = 0;

            while (true)
            {
                attempt++;
                try
                {
                    SaveToDatabase(item);
                    return;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DbWriterQueue] Save attempt {attempt}/{maxRetries} failed: {ex.Message}");
                    if (attempt >= maxRetries)
                    {
                        SaveFallback(item, ex);
                        LastStatusMessage = $"Warning: Database save failed for file '{item.BatchName}' after {maxRetries} attempts. Saved to local backup log.";
                        try
                        {
                            OnStatusMessage?.Invoke(LastStatusMessage);
                        }
                        catch { }
                        return;
                    }

                    Thread.Sleep(500 * attempt);
                }
            }
        }

        private static string GetDbConnectionString()
        {
            string connStr = DeviceCOM.DBConnection;
            if (string.IsNullOrEmpty(connStr))
            {
                connStr = Convert.ToString(System.Configuration.ConfigurationManager.AppSettings["DBConnection"]) ?? "";
            }

            if (!connStr.Contains("Command Timeout", StringComparison.OrdinalIgnoreCase))
            {
                connStr = connStr.TrimEnd(';') + ";Command Timeout=120";
            }
            return connStr;
        }

        private static void SaveToDatabase(TubeSaveItem item)
        {
            string connStr = GetDbConnectionString();

            string partJson = JsonConvert.SerializeObject(item.Part);
            string configJson = JsonConvert.SerializeObject(item.Configuration);
            string graphJson = JsonConvert.SerializeObject(item.GraphData);

            const string sql = @"
                INSERT INTO ""Logs"" 
                (""TimeStamp"", ""PartJson"", ""ConfigurationJson"", ""GraphDataJson"", ""BatchName"", ""Result"")
                VALUES 
                (@time, @part, @config, @graph, @batch, @result)";

            using var con = new NpgsqlConnection(connStr);
            con.Open();
            using var cmd = new NpgsqlCommand(sql, con);
            cmd.CommandTimeout = 120;
            cmd.Parameters.AddWithValue("@time", item.TimeStamp);
            cmd.Parameters.AddWithValue("@part", NpgsqlTypes.NpgsqlDbType.Jsonb, partJson);
            cmd.Parameters.AddWithValue("@config", NpgsqlTypes.NpgsqlDbType.Jsonb, configJson);
            cmd.Parameters.AddWithValue("@graph", NpgsqlTypes.NpgsqlDbType.Jsonb, graphJson);
            cmd.Parameters.AddWithValue("@batch", item.BatchName);
            cmd.Parameters.AddWithValue("@result", NpgsqlTypes.NpgsqlDbType.Boolean, item.Result);

            cmd.ExecuteNonQuery();

            // Mark inserted and clean up spool file
            item.IsInserted = true;
            DeleteSpoolFile(item.SpoolId);
        }

        private static void SaveFallback(TubeSaveItem item, Exception ex)
        {
            try
            {
                string dir = Path.Combine(AppContext.BaseDirectory, "FailedLogs");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, $"{DateTime.Now:yyyyMMdd}_failed_tubes.jsonl");

                var payload = new
                {
                    Error = ex.ToString(),
                    item.TimeStamp,
                    item.BatchName,
                    item.Result,
                    item.Part,
                    item.Configuration,
                    item.GraphData
                };

                string line = JsonConvert.SerializeObject(payload) + Environment.NewLine;
                File.AppendAllText(file, line);
            }
            catch (Exception fallbackEx)
            {
                System.Diagnostics.Debug.WriteLine($"[DbWriterQueue] SaveFallback fatal error: {fallbackEx.Message}");
            }
            finally
            {
                item.IsInserted = true;
                DeleteSpoolFile(item.SpoolId);
            }
        }

        public static void ReimportFailedLogs()
        {
            try
            {
                string baseDir = Path.Combine(AppContext.BaseDirectory, "FailedLogs");
                string pendingDir = Path.Combine(baseDir, "pending");

                var validSpools = new System.Collections.Generic.List<(string spoolFile, TubeSaveItem item)>();

                // 1. Process pending spool directory (clean tmp files and quarantine corrupt ones without requiring DB)
                if (Directory.Exists(pendingDir))
                {
                    // Clean up and ignore any leftover *.tmp files from an interrupted write
                    var tmpFiles = Directory.GetFiles(pendingDir, "*.tmp");
                    foreach (var tmp in tmpFiles)
                    {
                        try { File.Delete(tmp); } catch { }
                    }

                    var spoolFiles = Directory.GetFiles(pendingDir, "*.json");
                    foreach (var spoolFile in spoolFiles)
                    {
                        TubeSaveItem? item = null;
                        try
                        {
                            string text = File.ReadAllText(spoolFile);
                            item = JsonConvert.DeserializeObject<TubeSaveItem>(text);
                            if (item == null || item.GraphData == null)
                            {
                                throw new InvalidDataException("Deserialized spool item or GraphData is null");
                            }
                            if (string.IsNullOrEmpty(item.SpoolId))
                            {
                                item.SpoolId = Path.GetFileNameWithoutExtension(spoolFile);
                            }
                            validSpools.Add((spoolFile, item));
                        }
                        catch (Exception parseEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"[DbWriterQueue] Corrupt spool file {spoolFile}: {parseEx.Message}");
                            QuarantineCorruptSpoolFile(spoolFile);
                        }
                    }
                }

                bool hasLegacy = Directory.Exists(baseDir) && Directory.GetFiles(baseDir, "*.jsonl").Length > 0;
                if (validSpools.Count == 0 && !hasLegacy)
                {
                    return;
                }

                int totalFailed = 0;
                int recovered = 0;

                string connStr = GetDbConnectionString();

                try
                {
                    using var con = new NpgsqlConnection(connStr);
                    con.Open();

                    // Re-import valid spool files
                    foreach (var (spoolFile, item) in validSpools)
                    {
                        totalFailed++;
                        try
                        {
                            using var cmd = new NpgsqlCommand(
                                @"SELECT 1 FROM ""Logs"" WHERE ""BatchName"" = @b AND ""TimeStamp"" = @t LIMIT 1;", con);
                            cmd.Parameters.AddWithValue("@b", item.BatchName);
                            cmd.Parameters.AddWithValue("@t", item.TimeStamp);
                            var exists = cmd.ExecuteScalar();

                            if (exists == null)
                            {
                                Enqueue(item);
                                recovered++;
                            }
                            else
                            {
                                // Row is already present in DB: clean up the spool file
                                File.Delete(spoolFile);
                            }
                        }
                        catch (Exception spoolEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"[DbWriterQueue] Reimport spool error: {spoolEx.Message}");
                        }
                    }

                    // 2. Process legacy failed logs in FailedLogs/*.jsonl
                    var files = Directory.GetFiles(baseDir, "*.jsonl");
                    foreach (var file in files)
                    {
                        if (file.EndsWith(".recovered", StringComparison.OrdinalIgnoreCase)) continue;

                        var lines = File.ReadAllLines(file);
                        foreach (var line in lines)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;
                            totalFailed++;
                            try
                            {
                                var rec = JsonConvert.DeserializeObject<FallbackLogRecord>(line);
                                if (rec == null || rec.GraphData == null) continue;

                                using var cmd = new NpgsqlCommand(
                                    @"SELECT 1 FROM ""Logs"" WHERE ""BatchName"" = @b AND ""TimeStamp"" = @t LIMIT 1;", con);
                                cmd.Parameters.AddWithValue("@b", rec.BatchName);
                                cmd.Parameters.AddWithValue("@t", rec.TimeStamp);
                                var exists = cmd.ExecuteScalar();

                                if (exists == null)
                                {
                                    Enqueue(new TubeSaveItem
                                    {
                                        TimeStamp = rec.TimeStamp,
                                        Part = rec.Part,
                                        Configuration = rec.Configuration,
                                        GraphData = rec.GraphData,
                                        BatchName = rec.BatchName,
                                        Result = rec.Result
                                    });
                                    recovered++;
                                }
                            }
                            catch (Exception lineEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"[DbWriterQueue] Reimport line error: {lineEx.Message}");
                            }
                        }

                        try
                        {
                            if (recovered > 0)
                            {
                                Flush(30000);
                            }
                            string recoveredPath = file + ".recovered";
                            if (File.Exists(recoveredPath)) File.Delete(recoveredPath);
                            File.Move(file, recoveredPath);
                        }
                        catch (Exception moveEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"[DbWriterQueue] Rename file error: {moveEx.Message}");
                        }
                    }
                }
                catch (Exception dbEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[DbWriterQueue] Database connection failed during recovery: {dbEx.Message}");
                }

                if (totalFailed > 0)
                {
                    string msg = $"Recovery: {recovered} of {totalFailed} failed tubes recovered from backup logs.";
                    LastStatusMessage = msg;
                    try
                    {
                        OnStatusMessage?.Invoke(msg);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DbWriterQueue] ReimportFailedLogs error: {ex.Message}");
            }
        }

        private class FallbackLogRecord
        {
            public string? Error { get; set; }
            public DateTime TimeStamp { get; set; }
            public string BatchName { get; set; } = string.Empty;
            public bool Result { get; set; }
            public Part? Part { get; set; }
            public Configuration? Configuration { get; set; }
            public GraphData? GraphData { get; set; }
        }
    }
}
