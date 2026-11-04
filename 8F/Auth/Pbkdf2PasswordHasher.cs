using System;
using System.Security.Cryptography;
using System.Text;

namespace _8F.Auth
{
    public class Pbkdf2PasswordHasher : IPasswordHasher
    {
        public const int DefaultIterations = 210000;
        public const int SaltSize = 16;
        public const int HashSize = 32;

        public (byte[] Hash, byte[] Salt, int Iterations) HashPassword(string password, int iterations = DefaultIterations)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));
            if (iterations < DefaultIterations) iterations = DefaultIterations;

            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                HashSize);

            return (hash, salt, iterations);
        }

        public bool VerifyPassword(string password, byte[] storedHash, byte[] salt, int iterations)
        {
            if (password == null || storedHash == null || salt == null || iterations <= 0)
            {
                return false;
            }

            byte[] computed = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                storedHash.Length);

            return CryptographicOperations.FixedTimeEquals(computed, storedHash);
        }
    }
}
