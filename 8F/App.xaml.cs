using System;
using System.Configuration;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using _8F.Auth;
using _8F.Views;

namespace _8F
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                try { File.WriteAllText(@"D:\New folder\Eddy\8F\crash.log", $"AppDomain Unhandled: {e.ExceptionObject}"); } catch { }
            };
            DispatcherUnhandledException += (s, e) => {
                try { File.WriteAllText(@"D:\New folder\Eddy\8F\crash.log", $"Dispatcher Unhandled: {e.Exception}"); } catch { }
            };
            TaskScheduler.UnobservedTaskException += (s, e) => {
                try { File.WriteAllText(@"D:\New folder\Eddy\8F\crash.log", $"TaskScheduler Unhandled: {e.Exception}"); } catch { }
            };
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // 1. Database Connection & Schema Migration
            try
            {
                await AppServices.AuthRepository.EnsureAuthTablesCreatedAsync();
                await AppServices.ConfigChangeRepository.EnsureTableCreatedAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Database Connection Error:\nFailed to connect to PostgreSQL or ensure authentication tables exist.\n\nDetail: {ex.Message}\n\nThe application will now exit.",
                    "Database Unreachable - Fail Closed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown();
                return;
            }

            // 2. Validate Credentials & First-Time Setup
            int credentialCount;
            try
            {
                credentialCount = await AppServices.AuthService.GetCredentialCountAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Authentication Error:\nFailed to query authentication credentials.\n\nDetail: {ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown();
                return;
            }

            if (credentialCount == 0)
            {
                // First-run setup required
                var setupDlg = new FirstRunSetupDialog(AppServices.AuthService);
                bool? setupResult = setupDlg.ShowDialog();

                if (setupResult != true)
                {
                    await AppServices.AuthService.LogAuditAsync("ExitNoAuth", null, "Application closed during first-time setup");
                    Shutdown();
                    return;
                }
            }
            else if (credentialCount != 2)
            {
                // Corrupt state (e.g. only 1 credential exists)
                MessageBox.Show(
                    $"Fatal Configuration Error:\nIncomplete credential state detected (found {credentialCount} role credentials, expected exactly 2: Admin and Operator).\n\nPlease contact a database administrator.",
                    "Corrupt Security State",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                await AppServices.AuthService.LogAuditAsync("ExitNoAuth", null, $"Fatal error: incomplete credential count ({credentialCount})");
                Shutdown();
                return;
            }

            // 3. Login Authentication
            var loginDlg = new LoginDialog(AppServices.AuthService);
            bool? loginResult = loginDlg.ShowDialog();

            if (loginResult != true || !AppServices.SessionContext.IsAuthenticated)
            {
                await AppServices.AuthService.LogAuditAsync("ExitNoAuth", null, "Application closed without successful authentication");
                Shutdown();
                return;
            }

            // 4. If Operator logged in, show latest configuration change notice
            if (AppServices.SessionContext.CurrentRole == Role.Operator)
            {
                await ShowOperatorLastChangeNoticeAsync();
            }

            // 5. Authenticated -> Launch MainWindow
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
        }

        public static async Task ShowOperatorLastChangeNoticeAsync(Window? owner = null)
        {
            try
            {
                string? showNotice = ConfigurationManager.AppSettings["ShowLastChangeOnOperatorLogin"];
                if (showNotice != null && showNotice.Equals("false", StringComparison.OrdinalIgnoreCase))
                    return;

                var latest = await AppServices.ConfigChangeRepository.GetLatestChangeAsync();
                var dlg = new LastChangeNoticeDialog(latest)
                {
                    Owner = owner,
                    WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen
                };
                dlg.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Notice Error] Failed to show last change notice: {ex.Message}");
            }
        }
    }
}
