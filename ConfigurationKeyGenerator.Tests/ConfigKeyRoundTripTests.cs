using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using _8F.Services.ConfigKey;
using ConfigurationKeyGenerator.Services;
using Xunit;

namespace ConfigurationKeyGenerator.Tests
{
    public class ConfigKeyRoundTripTests
    {
        private static byte[] CreateTestSecret()
        {
            byte[] secret = new byte[32];
            for (int i = 0; i < 32; i++)
            {
                secret[i] = (byte)i;
            }
            return secret;
        }

        private static Dictionary<string, object> CreateValidSettings()
        {
            var settings = new Dictionary<string, object>();
            foreach (var b in ConfigurationWhitelist.BooleanKeys)
            {
                settings[b] = false;
            }
            settings[ConfigurationWhitelist.IsOpenWithDb] = true;
            settings[ConfigurationWhitelist.IsSaveWithDb] = true;
            settings[ConfigurationWhitelist.IsModbusServerEnable] = true;
            settings[ConfigurationWhitelist.ModbusServerPort] = 5020;
            settings[ConfigurationWhitelist.ModbusHeartbeatIntervalMs] = 1000;
            settings[ConfigurationWhitelist.FrequencyNo] = 4;
            return settings;
        }

        [Fact]
        public void RoundTrip_With8FDecryptService_SucceedsAndAppliesSettings()
        {
            byte[] secret = CreateTestSecret();
            const string serial = "EDDY-2026-TEST";
            string machineId = MachineIdCodec.Encode(serial, secret);
            var settings = CreateValidSettings();

            var service = new ConfigurationFileService();
            byte[] keyBytes = service.GenerateEncryptedKeyFile(
                product: "EddyShorter",
                customerName: "Acme Corp",
                machineId: machineId,
                serial: serial,
                settings: settings,
                secretOverride: secret);

            string tempFile = Path.Combine(Path.GetTempPath(), $"config_{Guid.NewGuid():N}.key");
            try
            {
                File.WriteAllBytes(tempFile, keyBytes);

                var result = ConfigKeyService.Load(
                    configPathOverride: tempFile,
                    serialOverride: serial,
                    secretOverride: secret);

                Assert.True(result.Success, $"Decryption failed with code: {result.ErrorCode}, message: {result.ErrorMessage}");
                Assert.NotNull(result.Settings);
                Assert.True((bool)result.Settings[ConfigurationWhitelist.IsOpenWithDb]);
                Assert.True((bool)result.Settings[ConfigurationWhitelist.IsSaveWithDb]);
                Assert.False((bool)result.Settings[ConfigurationWhitelist.IsOpenWithFile]);
                Assert.Equal(5020, Convert.ToInt32(result.Settings[ConfigurationWhitelist.ModbusServerPort]));
                Assert.Equal(1000, Convert.ToInt32(result.Settings[ConfigurationWhitelist.ModbusHeartbeatIntervalMs]));
                Assert.Equal(4, Convert.ToInt32(result.Settings[ConfigurationWhitelist.FrequencyNo]));
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public void Decrypt_WrongSerial_FailsWithSerialMismatch()
        {
            byte[] secret = CreateTestSecret();
            const string serial = "CORRECT-SERIAL-1";
            const string wrongSerial = "WRONG-SERIAL-2";
            string machineId = MachineIdCodec.Encode(serial, secret);
            var settings = CreateValidSettings();

            var service = new ConfigurationFileService();
            byte[] keyBytes = service.GenerateEncryptedKeyFile(
                product: "EddyShorter",
                customerName: "Acme Corp",
                machineId: machineId,
                serial: serial,
                settings: settings,
                secretOverride: secret);

            string tempFile = Path.Combine(Path.GetTempPath(), $"config_{Guid.NewGuid():N}.key");
            try
            {
                File.WriteAllBytes(tempFile, keyBytes);

                var result = ConfigKeyService.Load(
                    configPathOverride: tempFile,
                    serialOverride: wrongSerial,
                    secretOverride: secret);

                Assert.False(result.Success);
                Assert.Equal(ConfigKeyService.CodeSerialMismatch, result.ErrorCode);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public void Decrypt_TamperedMachineId_Fails()
        {
            byte[] secret = CreateTestSecret();
            const string serial = "SERIAL-ABC";
            string machineId = MachineIdCodec.Encode(serial, secret);
            var settings = CreateValidSettings();

            var service = new ConfigurationFileService();
            byte[] keyBytes = service.GenerateEncryptedKeyFile(
                product: "EddyShorter",
                customerName: "Acme Corp",
                machineId: machineId,
                serial: serial,
                settings: settings,
                secretOverride: secret);

            string json = Encoding.UTF8.GetString(keyBytes);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Flip one character in machineId
            string originalId = root.GetProperty("machineId").GetString()!;
            char last = originalId[^1];
            char replacement = last == '0' ? '1' : '0';
            string tamperedId = originalId[..^1] + replacement;

            var tamperedEnvelope = new Dictionary<string, string>
            {
                ["machineId"] = tamperedId,
                ["salt"] = root.GetProperty("salt").GetString()!,
                ["nonce"] = root.GetProperty("nonce").GetString()!,
                ["data"] = root.GetProperty("data").GetString()!
            };

            string tamperedJson = JsonSerializer.Serialize(tamperedEnvelope);
            string tempFile = Path.Combine(Path.GetTempPath(), $"config_{Guid.NewGuid():N}.key");
            try
            {
                File.WriteAllText(tempFile, tamperedJson);

                var result = ConfigKeyService.Load(
                    configPathOverride: tempFile,
                    serialOverride: serial,
                    secretOverride: secret);

                Assert.False(result.Success);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void Decrypt_TamperedSalt_Fails()
        {
            byte[] secret = CreateTestSecret();
            const string serial = "SERIAL-SALT";
            string machineId = MachineIdCodec.Encode(serial, secret);
            var settings = CreateValidSettings();

            var service = new ConfigurationFileService();
            byte[] keyBytes = service.GenerateEncryptedKeyFile(
                product: "EddyShorter",
                customerName: "Acme Corp",
                machineId: machineId,
                serial: serial,
                settings: settings,
                secretOverride: secret);

            string json = Encoding.UTF8.GetString(keyBytes);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            byte[] salt = Convert.FromBase64String(root.GetProperty("salt").GetString()!);
            salt[0] ^= 0xFF; // tamper first byte

            var tamperedEnvelope = new Dictionary<string, string>
            {
                ["machineId"] = root.GetProperty("machineId").GetString()!,
                ["salt"] = Convert.ToBase64String(salt),
                ["nonce"] = root.GetProperty("nonce").GetString()!,
                ["data"] = root.GetProperty("data").GetString()!
            };

            string tamperedJson = JsonSerializer.Serialize(tamperedEnvelope);
            string tempFile = Path.Combine(Path.GetTempPath(), $"config_{Guid.NewGuid():N}.key");
            try
            {
                File.WriteAllText(tempFile, tamperedJson);

                var result = ConfigKeyService.Load(
                    configPathOverride: tempFile,
                    serialOverride: serial,
                    secretOverride: secret);

                Assert.False(result.Success);
                Assert.Equal(ConfigKeyService.CodePayloadDecryptionFailed, result.ErrorCode);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void Decrypt_TamperedNonce_Fails()
        {
            byte[] secret = CreateTestSecret();
            const string serial = "SERIAL-NONCE";
            string machineId = MachineIdCodec.Encode(serial, secret);
            var settings = CreateValidSettings();

            var service = new ConfigurationFileService();
            byte[] keyBytes = service.GenerateEncryptedKeyFile(
                product: "EddyShorter",
                customerName: "Acme Corp",
                machineId: machineId,
                serial: serial,
                settings: settings,
                secretOverride: secret);

            string json = Encoding.UTF8.GetString(keyBytes);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            byte[] nonce = Convert.FromBase64String(root.GetProperty("nonce").GetString()!);
            nonce[0] ^= 0xFF; // tamper first byte

            var tamperedEnvelope = new Dictionary<string, string>
            {
                ["machineId"] = root.GetProperty("machineId").GetString()!,
                ["salt"] = root.GetProperty("salt").GetString()!,
                ["nonce"] = Convert.ToBase64String(nonce),
                ["data"] = root.GetProperty("data").GetString()!
            };

            string tamperedJson = JsonSerializer.Serialize(tamperedEnvelope);
            string tempFile = Path.Combine(Path.GetTempPath(), $"config_{Guid.NewGuid():N}.key");
            try
            {
                File.WriteAllText(tempFile, tamperedJson);

                var result = ConfigKeyService.Load(
                    configPathOverride: tempFile,
                    serialOverride: serial,
                    secretOverride: secret);

                Assert.False(result.Success);
                Assert.Equal(ConfigKeyService.CodePayloadDecryptionFailed, result.ErrorCode);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void Decrypt_TamperedData_Fails()
        {
            byte[] secret = CreateTestSecret();
            const string serial = "SERIAL-DATA";
            string machineId = MachineIdCodec.Encode(serial, secret);
            var settings = CreateValidSettings();

            var service = new ConfigurationFileService();
            byte[] keyBytes = service.GenerateEncryptedKeyFile(
                product: "EddyShorter",
                customerName: "Acme Corp",
                machineId: machineId,
                serial: serial,
                settings: settings,
                secretOverride: secret);

            string json = Encoding.UTF8.GetString(keyBytes);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            byte[] data = Convert.FromBase64String(root.GetProperty("data").GetString()!);
            data[^1] ^= 0xFF; // tamper tag

            var tamperedEnvelope = new Dictionary<string, string>
            {
                ["machineId"] = root.GetProperty("machineId").GetString()!,
                ["salt"] = root.GetProperty("salt").GetString()!,
                ["nonce"] = root.GetProperty("nonce").GetString()!,
                ["data"] = Convert.ToBase64String(data)
            };

            string tamperedJson = JsonSerializer.Serialize(tamperedEnvelope);
            string tempFile = Path.Combine(Path.GetTempPath(), $"config_{Guid.NewGuid():N}.key");
            try
            {
                File.WriteAllText(tempFile, tamperedJson);

                var result = ConfigKeyService.Load(
                    configPathOverride: tempFile,
                    serialOverride: serial,
                    secretOverride: secret);

                Assert.False(result.Success);
                Assert.Equal(ConfigKeyService.CodePayloadDecryptionFailed, result.ErrorCode);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }
    }
}
