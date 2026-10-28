using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using _8F.Services.ConfigKey;

namespace ConfigurationKeyGenerator.Services
{
    /// <summary>
    /// Service to generate encrypted config.key files for Machine IDs using AES-256-GCM.
    /// Excludes decryption capability (generator only encrypts).
    /// </summary>
    public class ConfigurationFileService
    {
        private static byte[]? _cachedSecret;

        public static byte[] GetEmbeddedSecret()
        {
            if (_cachedSecret != null) return _cachedSecret;

            var assembly = typeof(ConfigurationFileService).Assembly;
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
        /// Generates the encrypted config.key UTF-8 JSON bytes.
        /// </summary>
        public byte[] GenerateEncryptedKeyFile(
            string product,
            string customerName,
            string machineId,
            string serial,
            IReadOnlyDictionary<string, object> settings,
            byte[]? secretOverride = null)
        {
            if (string.IsNullOrWhiteSpace(product))
                throw new ArgumentException("Product is required.", nameof(product));

            if (string.IsNullOrWhiteSpace(customerName))
                throw new ArgumentException("Customer name is required.", nameof(customerName));

            if (string.IsNullOrWhiteSpace(machineId))
                throw new ArgumentException("Machine ID is required.", nameof(machineId));

            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            byte[] secret = secretOverride ?? GetEmbeddedSecret();
            if (secret.Length != 32)
            {
                throw new ArgumentException($"App secret must be exactly 32 bytes, got {secret.Length}.", nameof(secret));
            }

            string normalizedSerial = MachineIdCodec.Normalize(serial);
            string trimmedMachineId = machineId.Trim();

            // 1. Generate 16-byte random salt and 12-byte random nonce
            byte[] salt = new byte[16];
            byte[] nonce = new byte[12];
            RandomNumberGenerator.Fill(salt);
            RandomNumberGenerator.Fill(nonce);

            // 2. Derive 32-byte key using HKDF-SHA256:
            // configKey = HKDF-SHA256(ikm = secret, salt = salt, info = UTF8("config|" + Normalize(serial)))
            byte[] configKey = HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                ikm: secret,
                outputLength: 32,
                salt: salt,
                info: Encoding.UTF8.GetBytes("config|" + normalizedSerial));

            // 3. Construct plaintext payload JSON:
            // { "product", "customerName", "issuedOn": UTC ISO-8601, "settings": { key: value } }
            var payloadObj = new Dictionary<string, object>
            {
                ["product"] = product.Trim(),
                ["customerName"] = customerName.Trim(),
                ["issuedOn"] = DateTime.UtcNow.ToString("o"),
                ["settings"] = settings
            };

            string payloadJson = JsonSerializer.Serialize(payloadObj);
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(payloadJson);

            // 4. Encrypt with AES-256-GCM, AAD = UTF8(machineId), tag appended
            byte[] aad = Encoding.UTF8.GetBytes(trimmedMachineId);
            byte[] ciphertext = new byte[plaintextBytes.Length];
            byte[] tag = new byte[16];

            using (var aesGcm = new AesGcm(configKey, 16))
            {
                aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag, aad);
            }

            byte[] dataWithTag = new byte[ciphertext.Length + 16];
            Buffer.BlockCopy(ciphertext, 0, dataWithTag, 0, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, dataWithTag, ciphertext.Length, 16);

            // 5. Build envelope JSON:
            // { "machineId": "<hex>", "salt": "<b64>", "nonce": "<b64>", "data": "<b64>" }
            var envelope = new Dictionary<string, string>
            {
                ["machineId"] = trimmedMachineId,
                ["salt"] = Convert.ToBase64String(salt),
                ["nonce"] = Convert.ToBase64String(nonce),
                ["data"] = Convert.ToBase64String(dataWithTag)
            };

            string envelopeJson = JsonSerializer.Serialize(envelope, new JsonSerializerOptions { WriteIndented = true });
            return Encoding.UTF8.GetBytes(envelopeJson);
        }
    }
}
