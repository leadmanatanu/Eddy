using System;
using System.IO;
using Shared.MagkraftTools;
using Xunit;

namespace MachineInfo.Tests
{
    public class DatabaseBootstrapTests
    {
        [Fact]
        public void ResolveConnectionString_WhenMissing_ThrowsNamingSettings()
        {
            // Set dummy non-existent path and ensure env var is null
            string tempMissingFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            string? prev = Environment.GetEnvironmentVariable(DatabaseBootstrap.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(DatabaseBootstrap.EnvVarName, null);

                var ex = Assert.Throws<InvalidOperationException>(() =>
                {
                    DatabaseBootstrap.ResolveConnectionString(appsettingsPath: tempMissingFile);
                });

                Assert.Contains("Database connection string is missing", ex.Message);
                Assert.Contains(DatabaseBootstrap.EnvVarName, ex.Message);
                Assert.Contains(DatabaseBootstrap.ConfigKeyName, ex.Message);
            }
            finally
            {
                Environment.SetEnvironmentVariable(DatabaseBootstrap.EnvVarName, prev);
            }
        }

        [Fact]
        public void ResolveConnectionString_WhenEnvVarSet_ReturnsEnvVar()
        {
            string? prev = Environment.GetEnvironmentVariable(DatabaseBootstrap.EnvVarName);
            const string expectedConn = "Host=my-env-host;Database=GenerateMachineKey;Username=postgres;Password=pwd";
            try
            {
                Environment.SetEnvironmentVariable(DatabaseBootstrap.EnvVarName, expectedConn);
                string actual = DatabaseBootstrap.ResolveConnectionString();
                Assert.Equal(expectedConn, actual);
            }
            finally
            {
                Environment.SetEnvironmentVariable(DatabaseBootstrap.EnvVarName, prev);
            }
        }
    }
}
