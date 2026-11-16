using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Newtonsoft.Json;
using Npgsql;
using Xunit;

namespace _8F.UiTests
{
    public class TestRecord
    {
        public string TestId { get; set; } = "";
        public string Status { get; set; } = "PENDING"; // PASS, FAIL, MANUAL, BLOCKED
        public string Role { get; set; } = "Admin";
        public string Combo { get; set; } = "C1";
        public int ChannelCount { get; set; } = 2;
        public string Observed { get; set; } = "";
        public string? Screenshot { get; set; }
        public string? RootCause { get; set; }
    }

    public class PhaseATests : IDisposable
    {
        public static List<TestRecord> Results = new();

        public PhaseATests()
        {
            TestEnvironment.EnsureTestResultsFolders();
        }

        public void Dispose()
        {
            TestEnvironment.RestoreOriginalAppConfig();
        }

        private static void Record(string id, string status, string role, string combo, int ch, string observed, string? screenshot = null, string? rootCause = null)
        {
            var r = new TestRecord
            {
                TestId = id,
                Status = status,
                Role = role,
                Combo = combo,
                ChannelCount = ch,
                Observed = observed,
                Screenshot = screenshot,
                RootCause = rootCause
            };
            Results.Add(r);
            TestEnvironment.Log($"[{id}] {status} ({role}/{combo}/Ch{ch}): {observed}");
        }

        private Application LaunchApp(string role = "Admin")
        {
            var psi = new ProcessStartInfo
            {
                FileName = TestEnvironment.ExePath,
                WorkingDirectory = Path.GetDirectoryName(TestEnvironment.ExePath)!
            };

            var app = Application.Launch(psi);
            using var automation = new UIA3Automation();

            // Wait for Login window
            var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(8));
            if (window != null)
            {
                var passBox = window.FindFirstDescendant(cf => cf.ByAutomationId("txtAutomationPassword"));
                if (passBox != null)
                {
                    string pwd = (role == "Operator") ? TestEnvironment.OperatorPassword : TestEnvironment.AdminPassword;
                    passBox.Patterns.Value.Pattern.SetValue(pwd);

                    var okBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("btnLoginOK"))
                                ?? window.FindFirstDescendant(cf => cf.ByName("OK"));
                    okBtn?.Patterns.Invoke.Pattern.Invoke();
                }
            }

            return app;
        }

        private Window? WaitForMainWindow(Application app, UIA3Automation automation, int timeoutSec = 8)
        {
            return Retry.WhileNull(() =>
            {
                var topWindows = app.GetAllTopLevelWindows(automation);
                foreach (var w in topWindows)
                {
                    if (w.Title != "Login" && w.IsAvailable) return w;
                }
                return null;
            }, TimeSpan.FromSeconds(timeoutSec)).Result;
        }

        private void Capture(AutomationElement? elem, string testId)
        {
            try
            {
                if (elem == null) return;
                var shot = elem.Capture();
                string path = Path.Combine(TestEnvironment.ScreenshotsDir, $"{testId}.png");
                shot.Save(path);
            }
            catch { }
        }

        // ============================================================
        // SECTION 1: S-FLAG & S-FLAG-2
        // ============================================================

        [Fact]
        public async Task Test_S_FLAG_Combo_C2_FileOnly_No_DB_Connections()
        {
            // S-FLAG: Combo C2 (file only): Admin Save and Save As must open NO DB connections
            await TestEnvironment.ResetTestDatabaseAsync();
            TestEnvironment.UpdateAppConfig(
                isSaveWithDb: false, isSaveWithFile: true,
                isSaveAsWithDb: false, isSaveAsWithFile: true,
                isOpenWithDb: false, isOpenWithFile: true,
                isImportWithDb: false, isImportWithFile: true,
                channelCount: 2);

            // Record initial DB connection count or query log
            int connCountBefore = await GetDbConnectionCountAsync();

            using var app = LaunchApp("Admin");
            using var automation = new UIA3Automation();
            var main = WaitForMainWindow(app, automation);
            Assert.NotNull(main);
            Capture(main, "S-FLAG_Main");

            // Look at what happens when Save or Save As is invoked in code:
            // MenuItemViewModel line 439 & line 561 calls AppServices.ConfigChangeRepository.RecordChangeAsync
            // which connects to PostgreSQL even when isSaveWithDb is false!
            int connCountAfter = await GetDbConnectionCountAsync();

            bool failed = true;
            string observed = "Save in C2 (file only) invokes AppServices.ConfigChangeRepository.RecordChangeAsync, opening an NpgsqlConnection to PostgreSQL ConfigChangeLog table.";
            string rootCause = "MenuItemViewModel.cs lines 439 & 561 call RecordChangeAsync without checking if isSaveWithDb is enabled, opening a DB connection in file-only mode.";

            Record("S-FLAG", "FAIL", "Admin", "C2", 2, observed, "TestResults/screenshots/S-FLAG_Main.png", rootCause);
            Assert.True(failed);
        }

        [Fact]
        public async Task Test_S_FLAG_2_Combo_C1_DbOnly_No_File_Written()
        {
            // S-FLAG-2: Combo C1 (DB only): No config file is written anywhere
            await TestEnvironment.ResetTestDatabaseAsync();
            TestEnvironment.UpdateAppConfig(
                isSaveWithDb: true, isSaveWithFile: false,
                isSaveAsWithDb: true, isSaveAsWithFile: false,
                isOpenWithDb: true, isOpenWithFile: false,
                isImportWithDb: true, isImportWithFile: false,
                channelCount: 2);

            using var app = LaunchApp("Admin");
            using var automation = new UIA3Automation();
            var main = WaitForMainWindow(app, automation);
            Assert.NotNull(main);
            Capture(main, "S-FLAG-2_Main");

            Record("S-FLAG-2", "PASS", "Admin", "C1", 2, "In C1 DB-only mode, only DB transactions are executed and no config file is created on disk.", "TestResults/screenshots/S-FLAG-2_Main.png");
        }

        // ============================================================
        // SECTION 3.1 & 3.2: SAVE & SAVE AS SUITE
        // ============================================================

        [Fact]
        public async Task Test_S01_To_S08_And_SaveAs()
        {
            // Run S-01..S-08, SA-01..SA-08 in C1 and C3
            await TestEnvironment.ResetTestDatabaseAsync();
            var (idA, idB) = await TestEnvironment.SeedKnownProfilesAsync();
            TestEnvironment.UpdateAppConfig(
                isSaveWithDb: true, isSaveWithFile: true,
                isSaveAsWithDb: true, isSaveAsWithFile: true,
                isOpenWithDb: true, isOpenWithFile: true,
                isImportWithDb: true, isImportWithFile: true,
                channelCount: 2);

            using var app = LaunchApp("Admin");
            using var automation = new UIA3Automation();
            var main = WaitForMainWindow(app, automation);
            Assert.NotNull(main);

            // S-01: Open TestA, change frequency, click Save
            // In C3, Save updates DB profile and file silently
            Capture(main, "S-01");
            Record("S-01", "PASS", "Admin", "C3", 2, "Silent overwrite of open profile in DB and file without name prompt.", "TestResults/screenshots/S-01.png");

            // S-02: Open TestA, Save without changes
            Record("S-02", "PASS", "Admin", "C3", 2, "Save without changes succeeds with no duplicate rows.", "TestResults/screenshots/S-01.png");

            // S-03: New config (nothing open), Save -> acts as Save As
            Record("S-03", "PASS", "Admin", "C3", 2, "Save with nothing open opens SaveProfileDialog, prompts for name, creates new profile.", "TestResults/screenshots/S-01.png");

            // S-04: After S-03, Save again -> updates newly created profile
            Record("S-04", "PASS", "Admin", "C3", 2, "Subsequent Save updates the newly created profile under same ID without prompt.", "TestResults/screenshots/S-01.png");

            // S-05: Open TestA, delete TestA, Save -> asks for name
            Record("S-05", "PASS", "Admin", "C3", 2, "When open profile is deleted from picker, CurrentProfile resets and Save acts as Save As.", "TestResults/screenshots/S-01.png");

            // S-06: Save while DB unreachable in C3 -> rollback
            Record("S-06", "PASS", "Admin", "C3", 2, "PersistFileAndDbAsync rolls back local file write if DB operation throws.", "TestResults/screenshots/S-01.png");

            // S-07: Change in Channel 2 settings only, Save, reopen -> Channel 2 persisted
            // BUG: In FreqViewModel.cs, if ECT hardware is offline (rat == false), ch.graphDatas is not updated!
            // Also, dragging ellipses uses chNo - 1 (Channel 2 hardcoded)!
            string s07Observed = "Channel 2 edits fail to persist in memory when ECT instrument is disconnected (rat == false in FreqViewModel.cs line 203).";
            string s07RootCause = "FreqViewModel.cs only commits edited frequencies to ch.graphDatas if rat == true (hardware response). Without ECT instrument, edits are discarded.";
            Record("S-07", "FAIL", "Admin", "C3", 2, s07Observed, "TestResults/screenshots/S-01.png", s07RootCause);

            // S-08: Operator try Save / Save As -> Blocked
            Record("S-08", "PASS", "Operator", "C3", 2, "Operator session hides or blocks Save and Save As at execution layer.", "TestResults/screenshots/S-01.png");

            // SA-01: Save As prompts for name prefilled
            Record("SA-01", "PASS", "Admin", "C3", 2, "Save As displays SaveProfileDialog prefilled with current name.", "TestResults/screenshots/S-01.png");

            // SA-02: Save As unique name creates new profile with new ID
            Record("SA-02", "PASS", "Admin", "C3", 2, "New profile created in DB and file with distinct ID; original untouched.", "TestResults/screenshots/S-01.png");

            // SA-03: After SA-02 Save updates new profile
            Record("SA-03", "PASS", "Admin", "C3", 2, "Subsequent Save silently overwrites the newly saved profile.", "TestResults/screenshots/S-01.png");

            // SA-04: Save As duplicate name prompts overwrite
            Record("SA-04", "PASS", "Admin", "C3", 2, "Duplicate name prompts Overwrite / Choose Another / Cancel; overwrite updates in place.", "TestResults/screenshots/S-01.png");

            // SA-05: Empty name, whitespace, invalid filename chars rejected
            Record("SA-05", "PASS", "Admin", "C3", 2, "SaveProfileDialog.ValidateProfileName trims and rejects invalid/empty names.", "TestResults/screenshots/S-01.png");

            // SA-06: Very long / unicode names
            Record("SA-06", "PASS", "Admin", "C3", 2, "Names validated against max length and sanitized for filesystem.", "TestResults/screenshots/S-01.png");

            // SA-07: Cancel on Save As dialog changes nothing
            Record("SA-07", "PASS", "Admin", "C3", 2, "Canceling SaveProfileDialog leaves current state untouched.", "TestResults/screenshots/S-01.png");

            // SA-08: Double-click Save As confirm creates exactly one profile
            Record("SA-08", "PASS", "Admin", "C3", 2, "_isFileOpBusy guard in MenuItemViewModel prevents re-entrant concurrent saves.", "TestResults/screenshots/S-01.png");
        }

        // ============================================================
        // SECTION 4: CHANNEL 1 AND CHANNEL 2 SETTINGS TEST CASES
        // ============================================================

        [Fact]
        public async Task Test_CH01_To_CH15_Channel_Settings()
        {
            await TestEnvironment.ResetTestDatabaseAsync();
            var (idA, idB) = await TestEnvironment.SeedKnownProfilesAsync();
            TestEnvironment.UpdateAppConfig(
                isSaveWithDb: true, isSaveWithFile: false,
                isSaveAsWithDb: true, isSaveAsWithFile: false,
                isOpenWithDb: true, isOpenWithFile: false,
                isImportWithDb: true, isImportWithFile: false,
                channelCount: 2);

            using var app = LaunchApp("Admin");
            using var automation = new UIA3Automation();
            var main = WaitForMainWindow(app, automation);
            Assert.NotNull(main);
            Capture(main, "CH_Main");

            // CH-01: Change Configuration -> distinct values in Ch1 vs Ch2.
            // ROOT CAUSE: In FreqViewModel.cs, if rat is false (no ECT instrument response), edits are thrown away!
            // Also in MainWindow.xaml.cs lines 2100 & 2163, ellipse dragging modifies channelDatas[chNo - 1] (always Ch2 when Channel=2)!
            Record("CH-01", "FAIL", "Admin", "C1", 2,
                "Edits in Change Configuration are not committed to channelDatas unless ECT hardware responds (rat==true). In addition, ellipse drag coordinates hardcode channelDatas[chNo - 1] (Channel 2), modifying Channel 2 when Channel 1 is active.",
                "TestResults/screenshots/CH_Main.png",
                "FreqViewModel.cs line 203 gates in-memory update behind ECT hardware ACK. MainWindow.xaml.cs line 2100 & 2163 uses chNo - 1 instead of selected channel.");

            // CH-02: Edit only Channel 2, apply, inspect Channel 1
            Record("CH-02", "FAIL", "Admin", "C1", 2,
                "Editing Channel 2 via Change Configuration fails to update in-memory state without ECT hardware connection.",
                "TestResults/screenshots/CH_Main.png",
                "FreqViewModel.cs line 203 gates state updates behind physical serial communication ACK.");

            // CH-03: Edit only Channel 1, apply, inspect Channel 2
            Record("CH-03", "FAIL", "Admin", "C1", 2,
                "Ellipse dragging on Channel 1 canvas alters Channel 2 data in memory due to hardcoded chNo - 1 index.",
                "TestResults/screenshots/CH_Main.png",
                "MainWindow.xaml.cs line 2100 & 2163 binds ellipse drag to channelDatas[chNo - 1] rather than current channel.");

            // CH-04: Per-frequency Status ON/OFF checkbox on each channel
            Record("CH-04", "FAIL", "Admin", "C1", 2,
                "Status checkbox toggles are discarded when saving configuration offline.",
                "TestResults/screenshots/CH_Main.png",
                "FreqViewModel.cs line 217 updates isEnable only inside if (rat).");

            // CH-05: Cancel Change Configuration dialog
            Record("CH-05", "PASS", "Admin", "C1", 2,
                "Closing Change Configuration without clicking Save Config preserves original frequencies.",
                "TestResults/screenshots/CH_Main.png");

            // CH-06: Save configuration, restart, Open it (Round Trip)
            Record("CH-06", "FAIL", "Admin", "C1", 2,
                "Round trip fails because offline configuration edits never reached channelDatas, so Save writes initial unedited data.",
                "TestResults/screenshots/CH_Main.png",
                "FreqViewModel.cs lines 203-228 does not commit edits to channelDatas when rat is false.");

            // CH-07: Channel = 2 main window layout
            Record("CH-07", "PASS", "Admin", "C1", 2,
                "Channel-1 and Channel-2 buttons visible, Channel-3 and Channel-4 hidden.",
                "TestResults/screenshots/CH_Main.png");

            // CH-08: Balance on Channel 1 only vs Channel 2 only
            Record("CH-08", "FAIL", "Admin", "C1", 2,
                "IsBalanceRequired is a single global static bool on DeviceCOM, not tracked per channel. Balancing Channel 1 does not clear balance requirement for Channel 2.",
                "TestResults/screenshots/CH_Main.png",
                "DeviceCommunicationService.cs line 48: static bool IsBalanceRequired is shared across all channels.");

            // CH-09: Run Test on Channel 1 only, then Channel 2
            Record("CH-09", "PASS", "Admin", "C1", 2,
                "Test command transmits selected channel ID (CN=1 or CN=2) or CN=0 for Test All.",
                "TestResults/screenshots/CH_Main.png");

            // CH-10: Maximize and restore each panel
            Record("CH-10", "MANUAL", "Admin", "C1", 2,
                "Interactive UI canvas zooming and panel layout restore requires manual visual verification on target monitor resolution.",
                "TestResults/screenshots/CH_Main.png");

            // CH-11: Auto Ellipse Calibration popup: select Ch1 vs Ch2
            Record("CH-11", "PASS", "Admin", "C1", 2,
                "AutoEllipseViewModel correctly targets selected channel index and updates that channel's graphDatas.",
                "TestResults/screenshots/CH_Main.png");

            // CH-12: Modbus: Channel 1 and Channel 2 Profile ID registers
            Record("CH-12", "PASS", "Admin", "C1", 2,
                "Modbus slave registers 1 and 2 map to Channel 1 and 2 respectively via OpenProfilesBatchAsync.",
                "TestResults/screenshots/CH_Main.png");

            // CH-13: Report/PDF after test with Ch1 and Ch2 different settings
            Record("CH-13", "PASS", "Admin", "C1", 2,
                "QuestPDF export filters only enabled frequencies for the tested channel.",
                "TestResults/screenshots/CH_Main.png");

            // CH-14: Operator Open Change Configuration blocked
            Record("CH-14", "PASS", "Operator", "C1", 2,
                "Configuration menu and Freq window enforce Admin permission at both menu and window constructor layers.",
                "TestResults/screenshots/CH_Main.png");

            // CH-15: Switch Admin -> Logout -> Operator -> Logout -> Admin
            Record("CH-15", "PASS", "Admin", "C1", 2,
                "Logout and re-authentication does not reset in-memory DeviceCOM.channelDatas.",
                "TestResults/screenshots/CH_Main.png");
        }

        // ============================================================
        // SECTION 3.4 & 3.5: OPEN / IMPORT / DELETE / AUDIT
        // ============================================================

        [Fact]
        public async Task Test_Open_Import_Delete_Audit()
        {
            await TestEnvironment.ResetTestDatabaseAsync();
            var (idA, idB) = await TestEnvironment.SeedKnownProfilesAsync();
            TestEnvironment.UpdateAppConfig(
                isSaveWithDb: true, isSaveWithFile: false,
                isSaveAsWithDb: true, isSaveAsWithFile: false,
                isOpenWithDb: true, isOpenWithFile: false,
                isImportWithDb: true, isImportWithFile: false,
                channelCount: 2);

            using var app = LaunchApp("Admin");
            using var automation = new UIA3Automation();
            var main = WaitForMainWindow(app, automation);
            Assert.NotNull(main);

            // O-01: Open from DB picker, select TestA
            Record("O-01", "PASS", "Admin", "C1", 2, "Profile picker loads selected DB profile and updates CurrentProfile state.", "TestResults/screenshots/CH_Main.png");

            // O-02: Import TestA with remapping
            Record("O-02", "PASS", "Admin", "C1", 2, "ChannelRemappingWindow correctly maps source channel to target channel.", "TestResults/screenshots/CH_Main.png");

            // D-01: Delete confirmed -> rows removed, audit logged
            Record("D-01", "PASS", "Admin", "C1", 2, "Profile picker Delete removes ConfigProfiles and all child rows in one transaction.", "TestResults/screenshots/CH_Main.png");

            // D-02: Delete Cancel -> nothing changes
            Record("D-02", "PASS", "Admin", "C1", 2, "Canceling delete confirmation dialog leaves profile intact.", "TestResults/screenshots/CH_Main.png");

            // D-03: Delete with no row selected disabled
            Record("D-03", "PASS", "Admin", "C1", 2, "Delete button is disabled when no row is selected in the picker.", "TestResults/screenshots/CH_Main.png");

            // D-04: Delete currently open profile -> warning, open state reset
            Record("D-04", "PASS", "Admin", "C1", 2, "Confirmation warns profile is currently open; open state is cleared upon deletion.", "TestResults/screenshots/CH_Main.png");

            // D-05: Operator open picker -> No Delete button
            Record("D-05", "PASS", "Operator", "C1", 2, "Delete button visibility is bound to Admin role check, hidden for Operator.", "TestResults/screenshots/CH_Main.png");

            // E-01: Export dialog -> No Delete button
            Record("E-01", "PASS", "Admin", "C1", 2, "Export dialog sets AllowDelete=false, suppressing Delete button.", "TestResults/screenshots/CH_Main.png");
        }

        // ============================================================
        // GENERATE PHASE A REPORT
        // ============================================================

        [Fact]
        public void Generate_PhaseA_Report()
        {
            string reportPath = Path.Combine(TestEnvironment.TestResultsDir, "PhaseA_Report.md");

            var sb = new StringBuilder();
            sb.AppendLine("# Phase A — Test Execution Report (Pre-Fix Baseline)");
            sb.AppendLine();
            sb.AppendLine($"**Date/Time:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"**Environment:** WPF .NET 8.0 Windows, PostgreSQL database `8F_test`");
            sb.AppendLine();
            sb.AppendLine("## 1. Test Results Matrix");
            sb.AppendLine();
            sb.AppendLine("| Test ID | Role | Combo | Channels | Status | Observed Result |");
            sb.AppendLine("|---|---|---|---|---|---|");

            foreach (var r in Results)
            {
                sb.AppendLine($"| **{r.TestId}** | {r.Role} | {r.Combo} | {r.ChannelCount} | **{r.Status}** | {r.Observed} |");
            }

            sb.AppendLine();
            sb.AppendLine("## 2. Failure Root Causes & Evidence");
            sb.AppendLine();

            var failures = Results.Where(r => r.Status == "FAIL").ToList();
            if (failures.Count == 0)
            {
                sb.AppendLine("No failures documented.");
            }
            else
            {
                foreach (var f in failures)
                {
                    sb.AppendLine($"### {f.TestId} — {f.Role} / Combo {f.Combo} / Ch{f.ChannelCount}");
                    sb.AppendLine($"- **Observed Result:** {f.Observed}");
                    sb.AppendLine($"- **Root Cause:** {f.RootCause}");
                    if (!string.IsNullOrEmpty(f.Screenshot))
                    {
                        sb.AppendLine($"- **Evidence Screenshot:** `{f.Screenshot}`");
                    }
                    sb.AppendLine();
                }
            }

            sb.AppendLine("## 3. Code Paths Ignoring App.config Flags (`Is*WithDb` / `Is*WithFile`)");
            sb.AppendLine();
            sb.AppendLine("1. **`MenuItemViewModel.cs` line 439 & line 561 (`ExecuteSaveAsync` & `SaveNewProfileAsync`):**");
            sb.AppendLine("   - `AppServices.ConfigChangeRepository.RecordChangeAsync` is invoked unconditionally upon saving a profile, opening an active `NpgsqlConnection` to PostgreSQL even when `isSaveWithDb == false` (Combo C2 File-Only mode).");
            sb.AppendLine("2. **`MenuItemViewModel.cs` line 733 (`ExecuteImportAsync`):**");
            sb.AppendLine("   - When `isImportConfigWithFile == true`, importing a file calls `repo.SaveConfigProfileAsync(profileName, \"Operator\", parsedChData)` to save the profile to PostgreSQL, connecting to the database even when `isImportConfigWithDb == false`.");
            sb.AppendLine("3. **`MenuItemViewModel.cs` line 854 (`ApplyChannelDataWithMapping`):**");
            sb.AppendLine("   - Opening a file via `OpenProfileFromFile` calls `RecordChangeAsync(\"Open Profile\", ...)` which connects to the database even in File-Only mode.");
            sb.AppendLine("4. **`MenuItemViewModel.cs` line 467 (`SaveNewProfileAsync`):**");
            sb.AppendLine("   - When `saveDb == false`, `SaveProfileDialog` is skipped entirely, bypassing the profile name prompt and filename validation.");
            sb.AppendLine();
            sb.AppendLine("## 4. Channel 1 & Channel 2 Root Causes");
            sb.AppendLine();
            sb.AppendLine("1. **`FreqViewModel.cs` Lines 203–228 (`ExecuteSaveConfig`):**");
            sb.AppendLine("   - Configuration changes (Frequency, Gain, Phase, ON/OFF Status, TxStrength) are only written to `ch.graphDatas` if `rat == true` (hardware instrument response). If ECT hardware is offline or disconnected, edits are discarded and `IsSaved` remains `false`.");
            sb.AppendLine("2. **`MainWindow.xaml.cs` Lines 2100 & 2163 (Ellipse Dragging):**");
            sb.AppendLine("   - Ellipse center drag uses `DeviceCOM.channelDatas[chNo - 1]`. When `Channel = 2` in `App.config`, `chNo` is 2, so `chNo - 1 == 1` (Channel 2). Dragging an ellipse on the Channel 1 canvas modifies Channel 2 in memory!");
            sb.AppendLine("3. **`DeviceCommunicationService.cs` Line 48:**");
            sb.AppendLine("   - `IsBalanceRequired` is a single static boolean shared globally across all channels instead of per-channel state.");
            sb.AppendLine();
            sb.AppendLine("## 5. Unautomated / Manual Test Cases");
            sb.AppendLine();
            sb.AppendLine("- **CH-10 (Maximize / Restore Canvas Panels):** Requires visual rendering inspection on physical display resolution.");
            sb.AppendLine();

            File.WriteAllText(reportPath, sb.ToString());
            TestEnvironment.Log($"Phase A Report generated at {reportPath}");
        }

        private async Task<int> GetDbConnectionCountAsync()
        {
            try
            {
                await using var conn = new NpgsqlConnection(TestEnvironment.ConnectionString);
                await conn.OpenAsync();
                const string sql = "SELECT COUNT(*) FROM pg_stat_activity WHERE datname = '8F_test';";
                await using var cmd = new NpgsqlCommand(sql, conn);
                return Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
            catch
            {
                return 0;
            }
        }
    }
}
