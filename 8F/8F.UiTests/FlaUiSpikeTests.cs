using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Xunit;

namespace _8F.UiTests
{
    public class FlaUiSpikeTests
    {
        [Fact]
        public async Task Test_LaunchApp_And_Login_As_Admin()
        {
            TestEnvironment.EnsureTestResultsFolders();
            await TestEnvironment.ResetTestDatabaseAsync();
            TestEnvironment.UpdateAppConfig(
                isSaveWithDb: true, isSaveWithFile: false,
                isSaveAsWithDb: true, isSaveAsWithFile: false,
                isOpenWithDb: true, isOpenWithFile: false,
                isImportWithDb: true, isImportWithFile: false,
                channelCount: 2);

            var psi = new ProcessStartInfo
            {
                FileName = TestEnvironment.ExePath,
                WorkingDirectory = Path.GetDirectoryName(TestEnvironment.ExePath)!
            };

            using var app = Application.Launch(psi);
            using var automation = new UIA3Automation();

            try
            {
                // Wait for Login window or Main window
                var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(10));
                Assert.NotNull(window);

                // Save screenshot of initial window
                var screenshot = window.Capture();
                screenshot.Save(Path.Combine(TestEnvironment.ScreenshotsDir, "Spike_InitialWindow.png"));

                // If login window is showing, find automation password box
                var passwordBox = window.FindFirstDescendant(cf => cf.ByAutomationId("txtAutomationPassword"));

                if (passwordBox != null)
                {
                    // Set password via ValuePattern (no SendInput needed)
                    passwordBox.Patterns.Value.Pattern.SetValue(TestEnvironment.AdminPassword);

                    var okButton = window.FindFirstDescendant(cf => cf.ByAutomationId("btnLoginOK"))
                                   ?? window.FindFirstDescendant(cf => cf.ByName("OK"));
                    Assert.NotNull(okButton);
                    okButton.Patterns.Invoke.Pattern.Invoke();

                    // Now wait for MainWindow
                    var mainWindow = Retry.WhileNull(() =>
                    {
                        var topWindows = app.GetAllTopLevelWindows(automation);
                        foreach (var w in topWindows)
                        {
                            if (w.Title != "Login" && w.IsAvailable) return w;
                        }
                        return null;
                    }, TimeSpan.FromSeconds(10)).Result;

                    Assert.NotNull(mainWindow);

                    var mainScreenshot = mainWindow.Capture();
                    mainScreenshot.Save(Path.Combine(TestEnvironment.ScreenshotsDir, "Spike_MainWindow.png"));
                }
            }
            finally
            {
                app.Close();
                if (!app.HasExited)
                {
                    app.Kill();
                }
                TestEnvironment.RestoreOriginalAppConfig();
            }
        }
    }
}
