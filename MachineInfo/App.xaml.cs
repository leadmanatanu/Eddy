using System;
using System.Text;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MachineInfo.Application.Interfaces;
using MachineInfo.Application.UseCases;
using MachineInfo.Infrastructure.Persistence;
using MachineInfo.Infrastructure.Security;
using Shared.MagkraftTools;

namespace MachineInfo
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private IServiceProvider? _serviceProvider;

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
                var legacyRows = await DatabaseBootstrap.EnsureDatabaseAsync(connectionString);
                if (legacyRows.Count > 0)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"Warning: Found {legacyRows.Count} legacy record(s) with 64-character hash IDs in 'machine_registry'.");
                    sb.AppendLine("These IDs are invalid under the deterministic AES-256-GCM scheme and must be regenerated:");
                    foreach (var row in legacyRows)
                    {
                        sb.AppendLine($" - ID {row.Id}: Serial='{row.SerialNumber}', MachineId='{row.MachineId}'");
                    }

                    MessageBox.Show(
                        sb.ToString(),
                        "Legacy Machine IDs Detected",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
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

            // Step 3: Configure Dependency Injection
            var services = new ServiceCollection();
            ConfigureServices(services, connectionString);
            _serviceProvider = services.BuildServiceProvider();

            // Step 4: Show Main Window
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        private static void ConfigureServices(IServiceCollection services, string connectionString)
        {
            services.AddSingleton(connectionString);
            services.AddSingleton<IMachineRegistry>(sp => new NpgsqlMachineRegistry(connectionString));
            services.AddSingleton<IAppSecretProvider>(sp => new EmbeddedResourceSecretProvider(typeof(App).Assembly));
            services.AddTransient<IGenerateMachineId, GenerateMachineId>();
            services.AddTransient<MainWindow>();
        }
    }
}
