using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace _8F.UiTests
{
    public static class TestEnvironment
    {
        private static readonly SemaphoreSlim _dbLock = new(1, 1);
        public const string TestDbName = "8F_test";
        public const string ConnectionString = "Host=localhost;Port=5432;Username=postgres;Password=aryan123;Database=8F_test;Pooling=true;";
        public const string AdminPassword = "AdminPassword123";
        public const string OperatorPassword = "OperatorPassword123";

        public static string SolutionDir => Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));
        public static string AppConfigPath => Path.Combine(SolutionDir, "App.config");
        public static string AppConfigBackupPath => Path.Combine(SolutionDir, "App.config.backup_original");
        public static string ExePath => Path.Combine(SolutionDir, @"bin\Debug\net8.0-windows\8F.exe");
        public static string TestResultsDir => Path.Combine(SolutionDir, "TestResults");
        public static string ScreenshotsDir => Path.Combine(TestResultsDir, "screenshots");
        public static string LogFilePath => Path.Combine(TestResultsDir, "log.txt");

        public static void EnsureTestResultsFolders()
        {
            Directory.CreateDirectory(TestResultsDir);
            Directory.CreateDirectory(ScreenshotsDir);
        }

        public static void Log(string message)
        {
            EnsureTestResultsFolders();
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            File.AppendAllLines(LogFilePath, new[] { line });
            Console.WriteLine(line);
        }

        public static void BackupOriginalAppConfig()
        {
            if (!File.Exists(AppConfigBackupPath) && File.Exists(AppConfigPath))
            {
                File.Copy(AppConfigPath, AppConfigBackupPath, true);
            }
        }

        public static void RestoreOriginalAppConfig()
        {
            if (File.Exists(AppConfigBackupPath))
            {
                File.Copy(AppConfigBackupPath, AppConfigPath, true);
            }
        }

        public static async Task ResetTestDatabaseAsync()
        {
            await _dbLock.WaitAsync();
            try
            {
                await using var conn = new NpgsqlConnection(ConnectionString);
                await conn.OpenAsync();

                const string truncateSql = @"
                    TRUNCATE TABLE public.""ConfigEllipses"" CASCADE;
                    TRUNCATE TABLE public.""ConfigFrequencies"" CASCADE;
                    TRUNCATE TABLE public.""ConfigChannels"" CASCADE;
                    TRUNCATE TABLE public.""ConfigProfiles"" CASCADE;
                    TRUNCATE TABLE public.""ConfigChangeLog"" CASCADE;
                    TRUNCATE TABLE public.""AuthAuditLog"" CASCADE;
                    TRUNCATE TABLE public.""AuthCredentials"" CASCADE;
                ";
                await using (var cmd = new NpgsqlCommand(truncateSql, conn))
                {
                    await cmd.ExecuteNonQueryAsync();
                }

                // Seed Admin and Operator passwords
                var (adminHash, adminSalt, adminIter) = HashPassword(AdminPassword);
                var (opHash, opSalt, opIter) = HashPassword(OperatorPassword);

                const string insertCredSql = @"
                    INSERT INTO public.""AuthCredentials"" (""Role"", ""PasswordHash"", ""Salt"", ""Iterations"", ""UpdatedAt"", ""UpdatedByRole"")
                    VALUES 
                        ('Admin', @AdminHash, @AdminSalt, @AdminIter, now(), 'Setup'),
                        ('Operator', @OpHash, @OpSalt, @OpIter, now(), 'Setup')
                    ON CONFLICT (""Role"") DO UPDATE 
                    SET ""PasswordHash"" = EXCLUDED.""PasswordHash"",
                        ""Salt"" = EXCLUDED.""Salt"",
                        ""Iterations"" = EXCLUDED.""Iterations"",
                        ""UpdatedAt"" = now();
                ";
                await using (var cmd = new NpgsqlCommand(insertCredSql, conn))
                {
                    cmd.Parameters.AddWithValue("@AdminHash", adminHash);
                    cmd.Parameters.AddWithValue("@AdminSalt", adminSalt);
                    cmd.Parameters.AddWithValue("@AdminIter", adminIter);
                    cmd.Parameters.AddWithValue("@OpHash", opHash);
                    cmd.Parameters.AddWithValue("@OpSalt", opSalt);
                    cmd.Parameters.AddWithValue("@OpIter", opIter);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            finally
            {
                _dbLock.Release();
            }
        }

        public static async Task<(int ProfileIdA, int ProfileIdB)> SeedKnownProfilesAsync()
        {
            int idA = await InsertSeedProfileAsync("TestA", "Operator", freq1: 1000, freq2: 2000);
            int idB = await InsertSeedProfileAsync("TestB", "Operator", freq1: 3000, freq2: 4000);
            return (idA, idB);
        }

        private static async Task<int> InsertSeedProfileAsync(string name, string operatorName, int freq1, int freq2)
        {
            await using var conn = new NpgsqlConnection(ConnectionString);
            await conn.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync();

            const string sqlProf = @"
                INSERT INTO public.""ConfigProfiles"" (""Name"", ""OperatorName"", ""CreatedAt"", ""UpdatedAt"")
                VALUES (@Name, @Op, now(), now())
                RETURNING ""Id"";";
            int profileId;
            await using (var cmd = new NpgsqlCommand(sqlProf, conn, tx))
            {
                cmd.Parameters.AddWithValue("@Name", name);
                cmd.Parameters.AddWithValue("@Op", operatorName);
                profileId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            // Channel 1 & Channel 2
            for (int chNum = 1; chNum <= 2; chNum++)
            {
                const string sqlCh = @"
                    INSERT INTO public.""ConfigChannels"" (""ConfigProfileId"", ""ChannelNumber"", ""IsSelected"", ""TxStrength"")
                    VALUES (@Pid, @ChNum, @IsSel, 100)
                    RETURNING ""Id"";";
                int chDbId;
                await using (var cmd = new NpgsqlCommand(sqlCh, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@Pid", profileId);
                    cmd.Parameters.AddWithValue("@ChNum", chNum);
                    cmd.Parameters.AddWithValue("@IsSel", chNum == 1);
                    chDbId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                int fVal = (chNum == 1) ? freq1 : freq2;
                for (int fn = 1; fn <= 8; fn++)
                {
                    const string sqlFreq = @"
                        INSERT INTO public.""ConfigFrequencies"" (
                            ""ConfigChannelId"", ""FrequencyNumber"", ""Name"", ""Freq"", ""Gain"", ""Phase"", ""IsEnable"",
                            ""Strength"", ""PostGain"", ""Height"", ""Width"", ""Ex"", ""Ey"", ""Angel"",
                            ""HeightO"", ""WidthO"", ""ExO"", ""EyO"", ""AngelO""
                        ) VALUES (
                            @Cid, @Fn, @Name, @Freq, 20, 45, @Enable,
                            100, 10, 120, 120, 0, 0, 30,
                            650, 650, 0, 0, 30
                        ) RETURNING ""Id"";";
                    int fDbId;
                    await using (var cmd = new NpgsqlCommand(sqlFreq, conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@Cid", chDbId);
                        cmd.Parameters.AddWithValue("@Fn", fn);
                        cmd.Parameters.AddWithValue("@Name", $"D{fn}");
                        cmd.Parameters.AddWithValue("@Freq", fVal + (fn * 10));
                        cmd.Parameters.AddWithValue("@Enable", (fn <= 4));
                        fDbId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    }

                    const string sqlEll = @"
                        INSERT INTO public.""ConfigEllipses"" (""ConfigFrequencyId"", ""EllipseIndex"", ""Height"", ""Width"", ""Ex"", ""Ey"", ""Angel"")
                        VALUES (@Fid, 1, 120, 120, 0, 0, 30);";
                    await using (var cmd = new NpgsqlCommand(sqlEll, conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@Fid", fDbId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }

            await tx.CommitAsync();
            return profileId;
        }

        public static async Task<Dictionary<string, int>> GetTableCountsAsync()
        {
            await using var conn = new NpgsqlConnection(ConnectionString);
            await conn.OpenAsync();

            var counts = new Dictionary<string, int>();
            string[] tables = { "ConfigProfiles", "ConfigChannels", "ConfigFrequencies", "ConfigEllipses", "ConfigChangeLog" };
            foreach (var tbl in tables)
            {
                await using var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM public.\"{tbl}\";", conn);
                counts[tbl] = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
            return counts;
        }

        public static async Task<(DateTime CreatedAt, DateTime UpdatedAt, string OperatorName, string Name)?> GetProfileMetaAsync(int profileId)
        {
            await using var conn = new NpgsqlConnection(ConnectionString);
            await conn.OpenAsync();

            const string sql = @"SELECT ""CreatedAt"", ""UpdatedAt"", ""OperatorName"", ""Name"" FROM public.""ConfigProfiles"" WHERE ""Id"" = @Id;";
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", profileId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (reader.GetDateTime(0), reader.GetDateTime(1), reader.GetString(2), reader.GetString(3));
            }
            return null;
        }

        private static (byte[] Hash, byte[] Salt, int Iterations) HashPassword(string password)
        {
            const int iterations = 210000;
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                32);
            return (hash, salt, iterations);
        }

        public static void UpdateAppConfig(
            bool isSaveWithDb, bool isSaveWithFile,
            bool isSaveAsWithDb, bool isSaveAsWithFile,
            bool isOpenWithDb, bool isOpenWithFile,
            bool isImportWithDb, bool isImportWithFile,
            int channelCount = 2)
        {
            BackupOriginalAppConfig();
            string content = File.ReadAllText(AppConfigBackupPath);

            content = SetConfigKey(content, "ConnectionString", ConnectionString);
            content = SetConfigKey(content, "IsSaveWithDb", isSaveWithDb.ToString().ToLower());
            content = SetConfigKey(content, "IsSaveWithFile", isSaveWithFile.ToString().ToLower());
            content = SetConfigKey(content, "IsSaveAsWithDb", isSaveAsWithDb.ToString().ToLower());
            content = SetConfigKey(content, "IsSaveAsWithFile", isSaveAsWithFile.ToString().ToLower());
            content = SetConfigKey(content, "IsOpenWithDb", isOpenWithDb.ToString().ToLower());
            content = SetConfigKey(content, "IsOpenWithFile", isOpenWithFile.ToString().ToLower());
            content = SetConfigKey(content, "IsImportConfigurationWithDb", isImportWithDb.ToString().ToLower());
            content = SetConfigKey(content, "IsImportConfigurationWithFile", isImportWithFile.ToString().ToLower());
            content = SetConfigKey(content, "Channel", channelCount.ToString());
            content = SetConfigKey(content, "IsModbusServerEnable", "false"); // keep port clean during tests

            File.WriteAllText(AppConfigPath, content);
            string targetConfig = Path.Combine(SolutionDir, @"bin\Debug\net8.0-windows\8F.dll.config");
            File.WriteAllText(targetConfig, content);
            string targetExeConfig = Path.Combine(SolutionDir, @"bin\Debug\net8.0-windows\8F.exe.config");
            File.WriteAllText(targetExeConfig, content);
        }

        private static string SetConfigKey(string content, string key, string value)
        {
            string pattern = $@"<add key=""{key}"" value=""[^""]*""\s*/>";
            string replacement = $@"<add key=""{key}"" value=""{value}"" />";
            if (System.Text.RegularExpressions.Regex.IsMatch(content, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                return System.Text.RegularExpressions.Regex.Replace(content, pattern, replacement, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            return content.Replace("</appSettings>", $"  <add key=\"{key}\" value=\"{value}\" />\n  </appSettings>");
        }
    }

    public static class WpfTestHelper
    {
        private static Thread? _uiThread;
        private static System.Windows.Threading.Dispatcher? _dispatcher;
        private static readonly object _lock = new();

        public static System.Windows.Threading.Dispatcher GetDispatcher()
        {
            lock (_lock)
            {
                if (_dispatcher == null)
                {
                    var readyEvent = new ManualResetEventSlim(false);
                    _uiThread = new Thread(() =>
                    {
                        if (System.Windows.Application.Current == null)
                        {
                            _ = new System.Windows.Application();
                        }
                        _dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                        readyEvent.Set();
                        System.Windows.Threading.Dispatcher.Run();
                    });
                    _uiThread.SetApartmentState(ApartmentState.STA);
                    _uiThread.IsBackground = true;
                    _uiThread.Start();
                    readyEvent.Wait();
                }
                return _dispatcher!;
            }
        }

        public static async Task<T> InvokeAsync<T>(Func<T> func)
        {
            return await GetDispatcher().InvokeAsync(func);
        }

        public static async Task InvokeAsync(Action action)
        {
            await GetDispatcher().InvokeAsync(action);
        }
    }
}
