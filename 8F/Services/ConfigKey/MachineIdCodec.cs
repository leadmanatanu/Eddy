using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace _8F.Services.ConfigKey
{
    /// <summary>
    /// Shared Machine ID codec: encodes an instrument serial number into a deterministic Machine ID,
    /// and decodes a Machine ID back to the normalized serial number.
    /// </summary>
    public static class MachineIdCodec
    {
        private const int NonceLength = 12;
        private const int TagLength = 16;
        private const int MaxSerialLength = 64;
        private const int MinPayloadBytes = NonceLength + 1 + TagLength; // 29 bytes = 58 hex chars

        private static readonly byte[] EncInfo = Encoding.UTF8.GetBytes("machineid-enc");
        private static readonly byte[] NonceInfo = Encoding.UTF8.GetBytes("machineid-nonce");

        /// <summary>
        /// Normalizes a serial number: Trim().ToUpperInvariant().
        /// Rejects null/empty/whitespace, control characters, and length > 64.
        /// </summary>
        public static string Normalize(string? serial)
        {
            if (serial == null)
            {
                throw new ArgumentException("Serial number cannot be null.", nameof(serial));
            }

            string trimmed = serial.Trim();

            if (trimmed.Length == 0)
            {
                throw new ArgumentException("Serial number cannot be empty or whitespace.", nameof(serial));
            }

            if (trimmed.Any(char.IsControl))
            {
                throw new ArgumentException("Serial number cannot contain control characters.", nameof(serial));
            }

            if (trimmed.Length > MaxSerialLength)
            {
                throw new ArgumentException($"Serial number length ({trimmed.Length}) exceeds the maximum allowed length of {MaxSerialLength}.", nameof(serial));
            }

            return trimmed.ToUpperInvariant();
        }

        /// <summary>
        /// Checks if a Machine ID string matches the expected hex format and length bounds.
        /// </summary>
        public static bool IsValidFormat(string? machineId)
        {
            if (string.IsNullOrWhiteSpace(machineId))
            {
                return false;
            }

            string trimmed = machineId.Trim();
            if (trimmed.Length % 2 != 0)
            {
                return false;
            }

            // Minimum 29 bytes (58 hex chars), maximum (12 + 64*4 + 16)*2 = 568 hex chars
            if (trimmed.Length < MinPayloadBytes * 2 || trimmed.Length > (NonceLength + MaxSerialLength * 4 + TagLength) * 2)
            {
                return false;
            }

            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                bool isHex = (c >= '0' && c <= '9') ||
                             (c >= 'A' && c <= 'F') ||
                             (c >= 'a' && c <= 'f');
                if (!isHex)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Encodes a serial number into a unique, deterministic Machine ID using AES-256-GCM.
        /// </summary>
        public static string Encode(string serial, byte[] secret)
        {
            ValidateSecret(secret);
            string normalized = Normalize(serial);
            byte[] s = Encoding.UTF8.GetBytes(normalized);

            byte[] encKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, secret, 32, Array.Empty<byte>(), EncInfo);
            byte[] nonceKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, secret, 32, Array.Empty<byte>(), NonceInfo);

            byte[] nonce = new byte[NonceLength];
            using (var hmac = new HMACSHA256(nonceKey))
            {
                byte[] hmacFull = hmac.ComputeHash(s);
                Array.Copy(hmacFull, nonce, NonceLength);
            }

            byte[] ct = new byte[s.Length];
            byte[] tag = new byte[TagLength];

            using (var aesGcm = new AesGcm(encKey, TagLength))
            {
                aesGcm.Encrypt(nonce, s, ct, tag);
            }

            byte[] full = new byte[NonceLength + ct.Length + TagLength];
            Buffer.BlockCopy(nonce, 0, full, 0, NonceLength);
            Buffer.BlockCopy(ct, 0, full, NonceLength, ct.Length);
            Buffer.BlockCopy(tag, 0, full, NonceLength + ct.Length, TagLength);

            return Convert.ToHexString(full);
        }

        /// <summary>
        /// Decodes a Machine ID back into the normalized serial number, verifying round-trip equivalence.
        /// </summary>
        public static string Decode(string machineId, byte[] secret)
        {
            ValidateSecret(secret);

            if (!IsValidFormat(machineId))
            {
                throw new CryptographicException("Machine ID format is invalid.");
            }

            byte[] rawBytes;
            try
            {
                rawBytes = Convert.FromHexString(machineId.Trim());
            }
            catch (Exception ex)
            {
                throw new CryptographicException("Failed to decode Machine ID hex string.", ex);
            }

            if (rawBytes.Length < MinPayloadBytes)
            {
                throw new CryptographicException("Machine ID payload is too short.");
            }

            int ctLength = rawBytes.Length - NonceLength - TagLength;
            byte[] nonce = new byte[NonceLength];
            byte[] ct = new byte[ctLength];
            byte[] tag = new byte[TagLength];

            Buffer.BlockCopy(rawBytes, 0, nonce, 0, NonceLength);
            Buffer.BlockCopy(rawBytes, NonceLength, ct, 0, ctLength);
            Buffer.BlockCopy(rawBytes, NonceLength + ctLength, tag, 0, TagLength);

            byte[] encKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, secret, 32, Array.Empty<byte>(), EncInfo);
            byte[] pt = new byte[ctLength];

            try
            {
                using var aesGcm = new AesGcm(encKey, TagLength);
                aesGcm.Decrypt(nonce, ct, tag, pt);
            }
            catch (Exception ex)
            {
                throw new CryptographicException("Failed to decrypt Machine ID: authentication tag mismatch or corrupted data.", ex);
            }

            string serial;
            try
            {
                serial = Encoding.UTF8.GetString(pt);
            }
            catch (Exception ex)
            {
                throw new CryptographicException("Failed to decode decrypted serial as UTF-8.", ex);
            }

            // Verify round-trip re-encoding matches original input
            string normalized;
            try
            {
                normalized = Normalize(serial);
            }
            catch (Exception ex)
            {
                throw new CryptographicException("Decrypted serial failed normalization check.", ex);
            }

            string reEncoded = Encode(normalized, secret);
            if (!string.Equals(reEncoded, machineId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new CryptographicException("Machine ID round-trip check failed.");
            }

            return normalized;
        }

        private static void ValidateSecret(byte[] secret)
        {
            if (secret == null)
            {
                throw new ArgumentNullException(nameof(secret));
            }
            if (secret.Length != 32)
            {
                throw new ArgumentException($"App secret must be exactly 32 bytes, got {secret.Length}.", nameof(secret));
            }
        }
    }
}
