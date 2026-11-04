using _8F.Repositories.Implementations;
using _8F.Repositories.Interfaces;

namespace _8F.Auth
{
    public static class AppServices
    {
        public static IAuthRepository AuthRepository { get; } = new AuthRepository();
        public static IConfigChangeRepository ConfigChangeRepository { get; } = new ConfigChangeRepository();
        public static IPasswordHasher PasswordHasher { get; } = new Pbkdf2PasswordHasher();
        public static ISessionContext SessionContext { get; } = new SessionContext();
        public static IAuthService AuthService { get; } = new AuthService(AuthRepository, PasswordHasher, SessionContext);
    }
}
