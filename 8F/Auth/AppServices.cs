using System.Windows;
using _8F.Repositories.Implementations;
using _8F.Repositories.Interfaces;
using _8F.Services.Implementations;
using _8F.Services.Interfaces;
using _8F.Views;

namespace _8F.Auth
{
    public static class AppServices
    {
        public static IAuthRepository AuthRepository { get; } = new AuthRepository();
        public static IConfigChangeRepository ConfigChangeRepository { get; } = new ConfigChangeRepository();
        public static IPasswordHasher PasswordHasher { get; } = new Pbkdf2PasswordHasher();
        public static ISessionContext SessionContext { get; } = new SessionContext();
        public static IAuthService AuthService { get; } = new AuthService(AuthRepository, PasswordHasher, SessionContext);

        private static IProfileLoadService? _profileLoadService;
        public static IProfileLoadService ProfileLoadService
        {
            get => _profileLoadService ??= new ProfileLoadService(GetMainWindow);
            set => _profileLoadService = value;
        }

        private static MainWindow? GetMainWindow()
        {
            if (Application.Current == null) return null;
            if (Application.Current.Dispatcher.CheckAccess())
            {
                return Application.Current.MainWindow as MainWindow;
            }
            return Application.Current.Dispatcher.Invoke(() => Application.Current.MainWindow as MainWindow);
        }
    }
}
