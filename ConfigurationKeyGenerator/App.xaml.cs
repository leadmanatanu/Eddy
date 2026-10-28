using System;
using System.Windows;
using Shared.MagkraftTools;

namespace ConfigurationKeyGenerator
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Step 1: Resolve connection string
            string connectionString;
            try
            {
                connectionString = DatabaseBootstrap.ResolveConnectionString();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Database Configuration Missing",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            // Step 2: Ensure database exists and apply migrations before showing any window
            try
            {
                await DatabaseBootstrap.EnsureDatabaseAsync(connectionString);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to initialize the database:\n\n{ex.Message}",
                    "Database Initialization Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(2);
                return;
            }

            // Step 3: Show Main Window after database is ready
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}
