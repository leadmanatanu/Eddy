using System;

namespace _8F.Auth
{
    public class StoredCredential
    {
        public Role Role { get; set; }
        public byte[] PasswordHash { get; set; } = Array.Empty<byte>();
        public byte[] Salt { get; set; } = Array.Empty<byte>();
        public int Iterations { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedByRole { get; set; }
    }
}
