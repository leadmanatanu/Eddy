using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Npgsql;

namespace _8F.Repositories
{
    /// <summary>
    /// Centralized PostgreSQL connection provider for all repositories, auth, and database operations.
    /// Ensures that all parts of the application target the same database (8F) and schema (public).
    /// </summary>
    public static class DbConnectionProvider
    {
        private static string? _cachedConnectionString;
        private static readonly object _lock = new();

        public static string ConnectionString
        {
            get
            {
                if (_cachedConnectionString != null)
                    return _cachedConnectionString;

                lock (_lock)
                {
                    _cachedConnectionString ??= ResolveConnectionString();
                    return _cachedConnectionString;
                }
            }
        }

        public static void Reset()
        {
            lock (_lock)
            {
                _cachedConnectionString = null;
            }
        }

        public static async Task<NpgsqlConnection> GetOpenConnectionAsync()
        {
            var conn = new NpgsqlConnection(ConnectionString);
            await conn.OpenAsync();
            return conn;
        }

        public static NpgsqlConnection CreateConnection()
        {
            return new NpgsqlConnection(ConnectionString);
        }

        public static string ResolveConnectionString()
        {
            // Priority 1: Check encrypted config.key / Config.txt override
            try
            {
                string? overrideCs = TryReadEncryptedConfigOverride();
                if (!string.IsNullOrWhiteSpace(overrideCs))
                    return overrideCs;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DbConnectionProvider] Encrypted config check warning: {ex.Message}");
            }

            // Priority 2: Environment variable override
            string? env = Environment.GetEnvironmentVariable("EDDY_DB_CONNECTION_STRING");
            if (!string.IsNullOrWhiteSpace(env))
                return env;

            // Priority 3: App.config ConnectionString setting
            string? appConfig = ConfigurationManager.AppSettings["ConnectionString"];
            if (!string.IsNullOrWhiteSpace(appConfig))
                return appConfig;

            // Priority 4: Default fallback pointing to 8F database
            return "Host=localhost;Port=5432;Username=postgres;Password=aryan123;Database=8F;Pooling=true;Minimum Pool Size=2;Maximum Pool Size=20;";
        }

        private static string? TryReadEncryptedConfigOverride()
        {
            string[] possiblePaths = new[]
            {
                @"C:\ProgramData\Eddy\8F\config.key",
                @"C:\ProgramData\Eddy\Config.txt",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config.txt"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.key")
            };

            foreach (var path in possiblePaths)
            {
                if (!File.Exists(path))
                    continue;

                string raw = File.ReadAllText(path).Trim();
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                string? json = null;

                // Try decrypting with AES-256 CBC key/IV used by ConfigurationKeyGenerator
                try
                {
                    byte[] key = Encoding.UTF8.GetBytes("12345678901234567890123456789012");
                    byte[] iv = Encoding.UTF8.GetBytes("1234567890123456");
                    byte[] cipherBytes = Convert.FromBase64String(raw);

                    using Aes aes = Aes.Create();
                    aes.Key = key;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using MemoryStream ms = new MemoryStream(cipherBytes);
                    using CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
                    using StreamReader sr = new StreamReader(cs);
                    json = sr.ReadToEnd();
                }
                catch
                {
                    // If not base64 or AES-CBC, raw may already be plaintext JSON
                    if (raw.StartsWith("{") && raw.EndsWith("}"))
                    {
                        json = raw;
                    }
                }

                if (!string.IsNullOrWhiteSpace(json))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        // Check direct property: "ConnectionString"
                        if (root.TryGetProperty("ConnectionString", out var csElem) && !string.IsNullOrWhiteSpace(csElem.GetString()))
                        {
                            return csElem.GetString();
                        }

                        // Check dictionary: "ConfigurationKeys" -> "ConnectionString"
                        if (root.TryGetProperty("ConfigurationKeys", out var keysElem) && keysElem.ValueKind == JsonValueKind.Object)
                        {
                            if (keysElem.TryGetProperty("ConnectionString", out var dictCsElem) && !string.IsNullOrWhiteSpace(dictCsElem.GetString()))
                            {
                                return dictCsElem.GetString();
                            }
                        }
                    }
                    catch
                    {
                        // Invalid JSON, continue checking next path
                    }
                }
            }

            return null;
        }
    }
}
