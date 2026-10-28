using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace _8F.Services.ConfigKey
{
    public class ConfigKeyResult
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public IReadOnlyDictionary<string, object>? Settings { get; set; }

        public static ConfigKeyResult Fail(string code, string logDetails)
        {
            Trace.WriteLine($"[ConfigKey] Verification failure: Code={code}. Details: {logDetails}");
            Debug.WriteLine($"[ConfigKey] Verification failure: Code={code}. Details: {logDetails}");
            return new ConfigKeyResult
            {
                Success = false,
                ErrorCode = code,
                ErrorMessage = $"Configuration verification failed. Contact Magkraft support. Code {code}"
            };
        }

        public static ConfigKeyResult Ok(IReadOnlyDictionary<string, object> settings)
        {
            return new ConfigKeyResult
            {
                Success = true,
                Settings = settings
            };
        }
    }

    /// <summary>
    /// Configuration verification service:
    /// Decrypts config.key, validates Machine ID against App.config SerialNumber,
    /// verifies payload integrity via AES-256-GCM (AAD = machineId), and applies whitelisted settings.
    /// </summary>
    public static class ConfigKeyService
    {
        public const string CodeSerialNumberMissing = "CK-01";
        public const string CodeFileInvalidOrMissing = "CK-02";
        public const string CodeMachineIdDecodeFailed = "CK-03";
        public const string CodeSerialMismatch = "CK-04";
        public const string CodePayloadDecryptionFailed = "CK-05";
        public const string CodePayloadValidationFailed = "CK-06";

        private static byte[]? _cachedSecret;

        /// <summary>
        /// Reads the 32-byte app secret from the embedded resource 'Keys\config.secret'.
        /// </summary>
        public static byte[] GetEmbeddedSecret()
        {
            if (_cachedSecret != null) return _cachedSecret;

            var assembly = typeof(ConfigKeyService).Assembly;
            string? resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("Keys.config.secret", StringComparison.OrdinalIgnoreCase)
                                  || n.EndsWith("config.secret", StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
            {
                throw new InvalidOperationException("Embedded resource 'Keys/config.secret' was not found.");
            }

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException("Failed to open stream for 'Keys/config.secret'.");

            byte[] secret = new byte[32];
            int total = 0;
            while (total < 32)
            {
                int r = stream.Read(secret, total, 32 - total);
                if (r == 0) break;
                total += r;
            }

            if (total != 32)
            {
                throw new InvalidOperationException($"Embedded resource 'Keys/config.secret' must be exactly 32 bytes, read {total}.");
            }

            _cachedSecret = secret;
            return _cachedSecret;
        }

        /// <summary>
        /// Resolves the absolute path to config.key from App.config or defaults to CommonApplicationData.
        /// </summary>
        public static string ResolveConfigFilePath()
        {
            string? configuredPath = ConfigurationManager.AppSettings["ConfigFilePath"];
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                return Path.GetFullPath(configuredPath.Trim());
            }

            string commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            return Path.Combine(commonAppData, "Eddy", "8F", "config.key");
        }

        /// <summary>
        /// Runs the 7-step verification sequence at startup.
        /// </summary>
        public static ConfigKeyResult Load(string? configPathOverride = null, string? serialOverride = null, byte[]? secretOverride = null)
        {
            // 1. Read SerialNumber from App.config, Normalize it. Empty -> CK-01
            string? rawSerial = serialOverride ?? ConfigurationManager.AppSettings["SerialNumber"];
            if (string.IsNullOrWhiteSpace(rawSerial))
            {
                ProtectedSettings.Reset();
                return ConfigKeyResult.Fail(CodeSerialNumberMissing, "SerialNumber is missing or empty in configuration.");
            }

            string normalizedSerial;
            try
            {
                normalizedSerial = MachineIdCodec.Normalize(rawSerial);
            }
            catch (Exception ex)
            {
                ProtectedSettings.Reset();
                return ConfigKeyResult.Fail(CodeSerialNumberMissing, $"SerialNumber normalization error: {ex.Message}");
            }

            // 2. Resolve the config.key path; read and parse the JSON. Missing/unreadable/invalid -> CK-02
            string filePath = configPathOverride ?? ResolveConfigFilePath();
            if (!File.Exists(filePath))
            {
                if (File.Exists(filePath + ".txt"))
                {
                    filePath = filePath + ".txt";
                }
                else if (filePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) && File.Exists(filePath[..^4]))
                {
                    filePath = filePath[..^4];
                }
            }
            if (!File.Exists(filePath))
            {
                ProtectedSettings.Reset();
                return ConfigKeyResult.Fail(CodeFileInvalidOrMissing, "config.key file not found at resolved path.");
            }

            string jsonContent;
            try
            {
                jsonContent = File.ReadAllText(filePath, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                ProtectedSettings.Reset();
                return ConfigKeyResult.Fail(CodeFileInvalidOrMissing, $"Failed to read config.key file: {ex.Message}");
            }

            byte[] secret = secretOverride ?? GetEmbeddedSecret();
            return VerifyAndApply(normalizedSerial, jsonContent, secret);
        }

        /// <summary>
        /// Pure in-memory verification logic (steps 2-6) without file I/O dependency.
        /// </summary>
        public static ConfigKeyResult VerifyAndApply(string normalizedSerial, string configKeyJson, byte[] secret)
        {
            if (string.IsNullOrWhiteSpace(configKeyJson))
            {
                ProtectedSettings.Reset();
                return ConfigKeyResult.Fail(CodeFileInvalidOrMissing, "config.key content is empty.");
            }

            // Step 2 (parse JSON): { machineId, salt (b64), nonce (b64), data (b64) }
            string machineId;
            byte[] salt;
            byte[] nonce;
            byte[] dataWithTag;

            try
            {
                using var doc = JsonDocument.Parse(configKeyJson);
                var root = doc.RootElement;

                if (!root.TryGetProperty("machineId", out var pMachineId) &&
                    !root.TryGetProperty("MachineId", out pMachineId))
                {
                    ProtectedSettings.Reset();
                    return ConfigKeyResult.Fail(CodeFileInvalidOrMissing, "Missing 'machineId' property in JSON.");
                }
                machineId = pMachineId.GetString() ?? string.Empty;

                if (!root.TryGetProperty("salt", out var pSalt) &&
                    !root.TryGetProperty("Salt", out pSalt))
                {
                    ProtectedSettings.Reset();
                    return ConfigKeyResult.Fail(CodeFileInvalidOrMissing, "Missing 'salt' property in JSON.");
                }
                string saltB64 = pSalt.GetString() ?? string.Empty;
                salt = Convert.FromBase64String(saltB64);

                if (!root.TryGetProperty("nonce", out var pNonce) &&
                    !root.TryGetProperty("Nonce", out pNonce))
                {
                    ProtectedSettings.Reset();
                    return ConfigKeyResult.Fail(CodeFileInvalidOrMissing, "Missing 'nonce' property in JSON.");
                }
                string nonceB64 = pNonce.GetString() ?? string.Empty;
                nonce = Convert.FromBase64String(nonceB64);

                if (!root.TryGetProperty("data", out var pData) &&
                    !root.TryGetProperty("Data", out pData))
                {
                    ProtectedSettings.Reset();
                    return ConfigKeyResult.Fail(CodeFileInvalidOrMissing, "Missing 'data' property in JSON.");
                }
                string dataB64 = pData.GetString() ?? string.Empty;
                dataWithTag = Convert.FromBase64String(dataB64);

                if (string.IsNullOrWhiteSpace(machineId) || nonce.Length != 12 || dataWithTag.Length < 16)
                {
                    ProtectedSettings.Reset();
                    return ConfigKeyResult.Fail(CodeFileInvalidOrMissing, "Malformed field contents in config.key JSON.");
                }
            }
            catch (Exception ex)
            {
                ProtectedSettings.Reset();
                return ConfigKeyResult.Fail(CodeFileInvalidOrMissing, $"JSON parsing or Base64 decoding failed: {ex.Message}");
            }

            // Step 3: MachineIdCodec.Decode(machineId) -> serialFromKey. Failure -> CK-03
            string serialFromKey;
            try
            {
                serialFromKey = MachineIdCodec.Decode(machineId, secret);
            }
            catch (Exception ex)
            {
                ProtectedSettings.Reset();
                return ConfigKeyResult.Fail(CodeMachineIdDecodeFailed, $"MachineId decode failed: {ex.Message}");
            }

            // Step 4: Compare Normalize(serialFromKey) with normalizedSerial using constant-time comparison. Not equal -> CK-04
            string normalizedFromKey;
            try
            {
                normalizedFromKey = MachineIdCodec.Normalize(serialFromKey);
            }
            catch (Exception ex)
            {
                ProtectedSettings.Reset();
                return ConfigKeyResult.Fail(CodeMachineIdDecodeFailed, $"Serial from key normalization failed: {ex.Message}");
            }

            byte[] bKey = Encoding.UTF8.GetBytes(normalizedFromKey);
            byte[] bExpected = Encoding.UTF8.GetBytes(normalizedSerial);

            if (!CryptographicOperations.FixedTimeEquals(bKey, bExpected))
            {
                ProtectedSettings.Reset();
                return ConfigKeyResult.Fail(CodeSerialMismatch, "Serial number mismatch between key and configuration.");
            }

            // Step 5: Derive settings key, decrypt data with AAD = machineId. Failure -> CK-05
            // Settings key = HKDF-SHA256(ikm = secret, salt = salt, info = UTF8("config|" + Normalize(serial))) (32 bytes)
            byte[] settingsKey = HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                ikm: secret,
                outputLength: 32,
                salt: salt,
                info: Encoding.UTF8.GetBytes("config|" + normalizedSerial));

            byte[] aad = Encoding.UTF8.GetBytes(machineId);
            int ciphertextLength = dataWithTag.Length - 16;
            byte[] ciphertext = new byte[ciphertextLength];
            byte[] tag = new byte[16];
            Buffer.BlockCopy(dataWithTag, 0, ciphertext, 0, ciphertextLength);
            Buffer.BlockCopy(dataWithTag, ciphertextLength, tag, 0, 16);

            byte[] plaintextBytes = new byte[ciphertextLength];
            try
            {
                using var aesGcm = new AesGcm(settingsKey, 16);
                aesGcm.Decrypt(nonce, ciphertext, tag, plaintextBytes, aad);
            }
            catch (Exception ex)
            {
                ProtectedSettings.Reset();
                return ConfigKeyResult.Fail(CodePayloadDecryptionFailed, $"AES-GCM decryption failed: {ex.Message}");
            }

            // Step 6: Parse payload JSON { product, customerName, issuedOn, settings{} }, filter through whitelist. Failure -> CK-06
            Dictionary<string, object> verifiedSettings;
            try
            {
                string payloadJson = Encoding.UTF8.GetString(plaintextBytes);
                using var payloadDoc = JsonDocument.Parse(payloadJson);
                var root = payloadDoc.RootElement;

                if (!root.TryGetProperty("settings", out var pSettings) &&
                    !root.TryGetProperty("Settings", out pSettings) ||
                    pSettings.ValueKind != JsonValueKind.Object)
                {
                    ProtectedSettings.Reset();
                    return ConfigKeyResult.Fail(CodePayloadValidationFailed, "Missing or invalid 'settings' object in decrypted payload.");
                }

                var rawSettings = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in pSettings.EnumerateObject())
                {
                    rawSettings[prop.Name] = prop.Value.Clone();
                }

                if (!ConfigurationWhitelist.TryFilterAndValidate(rawSettings, out verifiedSettings, out string? valError))
                {
                    ProtectedSettings.Reset();
                    return ConfigKeyResult.Fail(CodePayloadValidationFailed, $"Payload settings validation failed: {valError}");
                }
            }
            catch (Exception ex)
            {
                ProtectedSettings.Reset();
                return ConfigKeyResult.Fail(CodePayloadValidationFailed, $"Payload JSON parsing error: {ex.Message}");
            }

            // Step 7: Apply to ProtectedSettings and return success
            ProtectedSettings.Apply(verifiedSettings);
            return ConfigKeyResult.Ok(verifiedSettings);
        }

        /// <summary>
        /// Displays the standard fail-closed verification error dialog.
        /// Never displays the serial, Machine ID, file content, or internal exceptions.
        /// </summary>
        public static void ShowVerificationError(string errorCode)
        {
            MessageBox.Show(
                $"Configuration verification failed. Contact Magkraft support. Code {errorCode}",
                "Configuration Verification Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
