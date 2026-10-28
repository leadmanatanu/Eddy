using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using _8F.Services.ConfigKey;
using Xunit;

namespace _8F.Tests
{
    public class ConfigKeyTests
    {
        private static readonly byte[] TestSecret = new byte[32];

        static ConfigKeyTests()
        {
            for (int i = 0; i < 32; i++) TestSecret[i] = (byte)i;
        }

        private static Dictionary<string, object> CreateDefaultTestSettings()
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { ConfigurationWhitelist.IsOpenWithDb, true },
                { ConfigurationWhitelist.IsOpenWithFile, false },
                { ConfigurationWhitelist.IsSaveWithDb, true },
                { ConfigurationWhitelist.IsSaveWithFile, false },
                { ConfigurationWhitelist.IsSaveAsWithDb, true },
                { ConfigurationWhitelist.IsSaveAsWithFile, false },
                { ConfigurationWhitelist.IsImportConfigurationWithDb, true },
                { ConfigurationWhitelist.IsImportConfigurationWithFile, false },
                { ConfigurationWhitelist.IsExportConfigurationWithDb, false },
                { ConfigurationWhitelist.IsExportConfigurationWithFile, false },
                { ConfigurationWhitelist.IsExitEnable, true },

                { ConfigurationWhitelist.IsModbusServerEnable, true },
                { ConfigurationWhitelist.ModbusServerPort, 5020 },
                { ConfigurationWhitelist.ModbusHeartbeatIntervalMs, 1000 },

                { ConfigurationWhitelist.IsPasswordEnable, false },
                { ConfigurationWhitelist.IsChangeConfigurationEnable, true },
                { ConfigurationWhitelist.IsThresholdSettingEnable, true },
                { ConfigurationWhitelist.IsAutoEllipseEnable, true },
                { ConfigurationWhitelist.IsReNewConfig, true },
                { ConfigurationWhitelist.IsWriteConfigurationEnable, true },
                { ConfigurationWhitelist.IsCopyChannel1ConfigurationEnable, true },
                { ConfigurationWhitelist.IsChangePasswordEnable, true },

                { ConfigurationWhitelist.IsBatchWiseLogEnable, true },
                { ConfigurationWhitelist.IsSerialNumberLogEnable, true },
                { ConfigurationWhitelist.IsPdfReportEnable, true },

                { ConfigurationWhitelist.IsTotalCountVisible, false },
                { ConfigurationWhitelist.IsOkCountVisible, true },
                { ConfigurationWhitelist.IsNotOkCountVisible, false },

                { ConfigurationWhitelist.IsLogRequiredOnBalance, true },
                { ConfigurationWhitelist.IsTxStrengthEnable, true },
                { ConfigurationWhitelist.IsTestLogOff, true },

                { ConfigurationWhitelist.FrequencyNo, 8 }
            };
        }

        private static string CreateConfigKeyJson(string serial, byte[] secret, Dictionary<string, object> settings)
        {
            string machineId = MachineIdCodec.Encode(serial, secret);
            byte[] salt = new byte[16];
            RandomNumberGenerator.Fill(salt);
            byte[] nonce = new byte[12];
            RandomNumberGenerator.Fill(nonce);

            string normalizedSerial = MachineIdCodec.Normalize(serial);
            byte[] settingsKey = HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                secret,
                32,
                salt,
                Encoding.UTF8.GetBytes("config|" + normalizedSerial));

            var payloadObj = new
            {
                product = "8F",
                customerName = "Test Client",
                issuedOn = DateTime.UtcNow.ToString("O"),
                settings = settings
            };
            string payloadJson = JsonSerializer.Serialize(payloadObj);
            byte[] plaintext = Encoding.UTF8.GetBytes(payloadJson);

            byte[] ciphertext = new byte[plaintext.Length];
            byte[] tag = new byte[16];
            using (var aes = new AesGcm(settingsKey, 16))
            {
                aes.Encrypt(nonce, plaintext, ciphertext, tag, Encoding.UTF8.GetBytes(machineId));
            }

            byte[] dataWithTag = new byte[ciphertext.Length + 16];
            Buffer.BlockCopy(ciphertext, 0, dataWithTag, 0, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, dataWithTag, ciphertext.Length, 16);

            var keyObj = new
            {
                machineId = machineId,
                salt = Convert.ToBase64String(salt),
                nonce = Convert.ToBase64String(nonce),
                data = Convert.ToBase64String(dataWithTag)
            };
            return JsonSerializer.Serialize(keyObj);
        }

        [Fact]
        public void VerifyProgramDataConfigKeyForUser()
        {
            string desktopConfigPath = @"c:\Users\atm00\OneDrive\Desktop\config.txt";
            string keyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Eddy", "8F", "config.key");

            if (File.Exists(desktopConfigPath))
            {
                // Parse desktop config.txt and generate fresh config.key for ABCD1234E
                var rawDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (string line in File.ReadAllLines(desktopConfigPath))
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("//"))
                        continue;
                    string[] parts = trimmed.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        rawDict[parts[0].Trim()] = parts[1].Trim();
                    }
                }

                bool valid = ConfigurationWhitelist.TryFilterAndValidate(rawDict, out var filtered, out string? err);
                Assert.True(valid, $"Validation error: {err}");

                byte[] secret = ConfigKeyService.GetEmbeddedSecret();
                string newKeyJson = CreateConfigKeyJson("ABCD1234E", secret, filtered);
                Directory.CreateDirectory(Path.GetDirectoryName(keyPath)!);
                File.WriteAllText(keyPath, newKeyJson);
            }

            Assert.True(File.Exists(keyPath));

            ProtectedSettings.Reset();
            var result = ConfigKeyService.Load(configPathOverride: keyPath, serialOverride: "ABCD1234E");

            Assert.True(result.Success, $"Verification failed with error: {result.ErrorCode} - {result.ErrorMessage}");
            Assert.True(ProtectedSettings.IsVerified);

            Assert.Equal(4, ProtectedSettings.FrequencyNo);
            Assert.True(ProtectedSettings.IsOpenWithFile);
            Assert.False(ProtectedSettings.IsOpenWithDb);
            Assert.True(ProtectedSettings.IsTotalCountVisible);
            Assert.True(ProtectedSettings.IsOkCountVisible);
            Assert.False(ProtectedSettings.IsNotOkCountVisible);
            Assert.True(ProtectedSettings.IsChangeConfigurationEnable);
            Assert.True(ProtectedSettings.IsThresholdSettingEnable);
            Assert.False(ProtectedSettings.IsAutoEllipseEnable);
            Assert.True(ProtectedSettings.IsBatchWiseLogEnable);
            Assert.True(ProtectedSettings.IsPdfReportEnable);
        }

        [Fact]
        public void TestVector_MatchesSpecification()
        {
            // Shared contract test vector:
            // Test secret: 00 01 … 1F
            // Serial: A1234567890B
            // Expected Machine ID: 06B79C1DE2974253F751657C2342B47A90D174BB60DDD9FC55462A379E108BE0DC17612CA464A523
            string serial = "A1234567890B";
            string expectedHex = "06B79C1DE2974253F751657C2342B47A90D174BB60DDD9FC55462A379E108BE0DC17612CA464A523";

            string encoded = MachineIdCodec.Encode(serial, TestSecret);
            Assert.Equal(expectedHex, encoded);

            string decoded = MachineIdCodec.Decode(expectedHex, TestSecret);
            Assert.Equal("A1234567890B", decoded);
        }

        [Fact]
        public void ValidKey_MatchingSerial_AppliesSettings()
        {
            string serial = "A1234567890B";
            var settings = CreateDefaultTestSettings();
            settings[ConfigurationWhitelist.FrequencyNo] = 4;
            settings[ConfigurationWhitelist.IsAutoEllipseEnable] = true;
            settings[ConfigurationWhitelist.ModbusServerPort] = 5025;

            string keyJson = CreateConfigKeyJson(serial, TestSecret, settings);

            ProtectedSettings.Reset();
            var result = ConfigKeyService.VerifyAndApply(serial, keyJson, TestSecret);

            Assert.True(result.Success);
            Assert.Null(result.ErrorCode);
            Assert.True(ProtectedSettings.IsVerified);
            Assert.Equal(4, ProtectedSettings.FrequencyNo);
            Assert.Equal(5025, ProtectedSettings.ModbusServerPort);
            Assert.True(ProtectedSettings.IsAutoEllipseEnable);
            Assert.True(ProtectedSettings.IsOpenWithDb);
        }

        [Fact]
        public void MismatchedSerial_FailsWithCK04_NothingEnabled()
        {
            string keySerial = "A1234567890B";
            string appConfigSerial = "DIFFERENT-SERIAL-999";

            string keyJson = CreateConfigKeyJson(keySerial, TestSecret, CreateDefaultTestSettings());

            ProtectedSettings.Reset();
            var result = ConfigKeyService.VerifyAndApply(appConfigSerial, keyJson, TestSecret);

            Assert.False(result.Success);
            Assert.Equal(ConfigKeyService.CodeSerialMismatch, result.ErrorCode);
            Assert.False(ProtectedSettings.IsVerified);
        }

        [Fact]
        public void TamperedMachineId_FailsWithCK03()
        {
            string serial = "A1234567890B";
            string keyJson = CreateConfigKeyJson(serial, TestSecret, CreateDefaultTestSettings());

            using var doc = JsonDocument.Parse(keyJson);
            var root = doc.RootElement;
            string machineId = root.GetProperty("machineId").GetString()!;

            // Flip one character in machineId
            char flippedChar = machineId[0] == '0' ? '1' : '0';
            string tamperedMachineId = flippedChar + machineId.Substring(1);

            var tamperedObj = new
            {
                machineId = tamperedMachineId,
                salt = root.GetProperty("salt").GetString(),
                nonce = root.GetProperty("nonce").GetString(),
                data = root.GetProperty("data").GetString()
            };
            string tamperedJson = JsonSerializer.Serialize(tamperedObj);

            ProtectedSettings.Reset();
            var result = ConfigKeyService.VerifyAndApply(serial, tamperedJson, TestSecret);

            Assert.False(result.Success);
            Assert.Equal(ConfigKeyService.CodeMachineIdDecodeFailed, result.ErrorCode);
        }

        [Fact]
        public void TamperedSalt_FailsWithCK05()
        {
            string serial = "A1234567890B";
            string keyJson = CreateConfigKeyJson(serial, TestSecret, CreateDefaultTestSettings());

            using var doc = JsonDocument.Parse(keyJson);
            var root = doc.RootElement;
            byte[] salt = Convert.FromBase64String(root.GetProperty("salt").GetString()!);
            salt[0] ^= 0xFF; // Tamper 1 byte

            var tamperedObj = new
            {
                machineId = root.GetProperty("machineId").GetString(),
                salt = Convert.ToBase64String(salt),
                nonce = root.GetProperty("nonce").GetString(),
                data = root.GetProperty("data").GetString()
            };
            string tamperedJson = JsonSerializer.Serialize(tamperedObj);

            ProtectedSettings.Reset();
            var result = ConfigKeyService.VerifyAndApply(serial, tamperedJson, TestSecret);

            Assert.False(result.Success);
            Assert.Equal(ConfigKeyService.CodePayloadDecryptionFailed, result.ErrorCode);
        }

        [Fact]
        public void TamperedNonce_FailsWithCK05()
        {
            string serial = "A1234567890B";
            string keyJson = CreateConfigKeyJson(serial, TestSecret, CreateDefaultTestSettings());

            using var doc = JsonDocument.Parse(keyJson);
            var root = doc.RootElement;
            byte[] nonce = Convert.FromBase64String(root.GetProperty("nonce").GetString()!);
            nonce[0] ^= 0xFF; // Tamper 1 byte

            var tamperedObj = new
            {
                machineId = root.GetProperty("machineId").GetString(),
                salt = root.GetProperty("salt").GetString(),
                nonce = Convert.ToBase64String(nonce),
                data = root.GetProperty("data").GetString()
            };
            string tamperedJson = JsonSerializer.Serialize(tamperedObj);

            ProtectedSettings.Reset();
            var result = ConfigKeyService.VerifyAndApply(serial, tamperedJson, TestSecret);

            Assert.False(result.Success);
            Assert.Equal(ConfigKeyService.CodePayloadDecryptionFailed, result.ErrorCode);
        }

        [Fact]
        public void TamperedData_FailsWithCK05()
        {
            string serial = "A1234567890B";
            string keyJson = CreateConfigKeyJson(serial, TestSecret, CreateDefaultTestSettings());

            using var doc = JsonDocument.Parse(keyJson);
            var root = doc.RootElement;
            byte[] data = Convert.FromBase64String(root.GetProperty("data").GetString()!);
            data[0] ^= 0xFF; // Tamper 1 byte in ciphertext or tag

            var tamperedObj = new
            {
                machineId = root.GetProperty("machineId").GetString(),
                salt = root.GetProperty("salt").GetString(),
                nonce = root.GetProperty("nonce").GetString(),
                data = Convert.ToBase64String(data)
            };
            string tamperedJson = JsonSerializer.Serialize(tamperedObj);

            ProtectedSettings.Reset();
            var result = ConfigKeyService.VerifyAndApply(serial, tamperedJson, TestSecret);

            Assert.False(result.Success);
            Assert.Equal(ConfigKeyService.CodePayloadDecryptionFailed, result.ErrorCode);
        }

        [Fact]
        public void MissingFile_FailsWithCK02()
        {
            string nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".key");

            ProtectedSettings.Reset();
            var result = ConfigKeyService.Load(configPathOverride: nonExistentPath, serialOverride: "A1234567890B", secretOverride: TestSecret);

            Assert.False(result.Success);
            Assert.Equal(ConfigKeyService.CodeFileInvalidOrMissing, result.ErrorCode);
        }

        [Fact]
        public void EmptySerial_FailsWithCK01()
        {
            ProtectedSettings.Reset();
            var result = ConfigKeyService.Load(serialOverride: "", secretOverride: TestSecret);

            Assert.False(result.Success);
            Assert.Equal(ConfigKeyService.CodeSerialNumberMissing, result.ErrorCode);
        }

        [Fact]
        public void InvalidFrequencyNo_FailsWithCK06()
        {
            string serial = "A1234567890B";
            var settings = CreateDefaultTestSettings();
            settings[ConfigurationWhitelist.FrequencyNo] = 16; // Only 1, 4, 8 valid

            string keyJson = CreateConfigKeyJson(serial, TestSecret, settings);

            ProtectedSettings.Reset();
            var result = ConfigKeyService.VerifyAndApply(serial, keyJson, TestSecret);

            Assert.False(result.Success);
            Assert.Equal(ConfigKeyService.CodePayloadValidationFailed, result.ErrorCode);
        }

        [Fact]
        public void ModbusEnabled_MissingPort_FailsWithCK06()
        {
            string serial = "A1234567890B";
            var settings = CreateDefaultTestSettings();
            settings[ConfigurationWhitelist.IsModbusServerEnable] = true;
            settings.Remove(ConfigurationWhitelist.ModbusServerPort); // Missing while Modbus=true

            string keyJson = CreateConfigKeyJson(serial, TestSecret, settings);

            ProtectedSettings.Reset();
            var result = ConfigKeyService.VerifyAndApply(serial, keyJson, TestSecret);

            Assert.False(result.Success);
            Assert.Equal(ConfigKeyService.CodePayloadValidationFailed, result.ErrorCode);
        }

        [Fact]
        public void ErrorMessage_ShowsOnlyCodeAndNoSensitiveIdentifiers()
        {
            string serial = "A1234567890B";
            var result = ConfigKeyResult.Fail(ConfigKeyService.CodeSerialMismatch, "Internal detail without serial");

            Assert.Equal("Configuration verification failed. Contact Magkraft support. Code CK-04", result.ErrorMessage);
            Assert.DoesNotContain(serial, result.ErrorMessage);
            Assert.DoesNotContain("machineid", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void NoDirectAppSettingsReads_ForWhitelistedKeys_In8FSource()
        {
            // Anti-regression test: verifies no whitelisted keys are read via ConfigurationManager.AppSettings
            string projectDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "8F"));
            Assert.True(Directory.Exists(projectDir), $"8F project directory not found at: {projectDir}");

            var csFiles = Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories);
            var violations = new List<string>();

            // Pattern for AppSettings["Key"] or AppSettings['Key']
            string pattern = @"AppSettings\[\s*[""'](" + string.Join("|", ConfigurationWhitelist.AllKeys) + @")[""']\s*\]";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);

            foreach (var file in csFiles)
            {
                if (file.Contains("\\obj\\") || file.Contains("\\bin\\") || file.Contains("/obj/") || file.Contains("/bin/"))
                {
                    continue;
                }

                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    // Skip comment lines
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("//") || trimmed.StartsWith("/*") || trimmed.StartsWith("*"))
                    {
                        continue;
                    }

                    var match = regex.Match(line);
                    if (match.Success)
                    {
                        violations.Add($"{Path.GetFileName(file)}:L{i + 1} reads '{match.Groups[1].Value}' directly from AppSettings: {trimmed}");
                    }
                }
            }

            Assert.Empty(violations);
        }
    }
}
