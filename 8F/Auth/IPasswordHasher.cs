namespace _8F.Auth
{
    public interface IPasswordHasher
    {
        (byte[] Hash, byte[] Salt, int Iterations) HashPassword(string password, int iterations = 210000);
        bool VerifyPassword(string password, byte[] storedHash, byte[] salt, int iterations);
    }
}
