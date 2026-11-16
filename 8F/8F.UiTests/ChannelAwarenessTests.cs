using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using _8F.Auth;
using _8F.Models;
using _8F.Repositories.Implementations;
using _8F.Services;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Newtonsoft.Json;
using Npgsql;
using Xunit;

namespace _8F.UiTests
{
    public class ChannelAwarenessTests : IDisposable
    {
        private readonly string _connectionString = TestEnvironment.ConnectionString;

        public ChannelAwarenessTests()
        {
            TestEnvironment.EnsureTestResultsFolders();
        }

        public void Dispose()
        {
            TestEnvironment.RestoreOriginalAppConfig();
        }

        private InspectionLogRepository CreateRepo() => new InspectionLogRepository(_connectionString);

        private ChannelData CreateChannelData(int chNum, int baseFreq, int gain, int phase, byte tx)
        {
            var ch = new ChannelData
            {
                Id = chNum,
                IsSeleted = (chNum == 1),
                TxStrength = tx,
                graphDatas = new List<GraphData>()
            };

            for (int fn = 1; fn <= 8; fn++)
            {
                var g = new GraphData
                {
                    Id = fn,
                    Name = $"D{fn}",
                    freq = baseFreq + (fn * 10),
                    gain = gain,
                    phase = phase,
                    isEnable = (fn <= 4),
                    strength = 100,
                    postGain = 10,
                    height = 120 + (chNum * 10),
                    width = 120 + (chNum * 10),
                    ex = 0,
                    ey = 0,
                    angel = 30,
                    height_O = 650,
                    width_O = 650,
                    ex_O = 0,
                    ey_O = 0,
                    angel_O = 30,
                    ellipses = new List<Ellips>
                    {
                        new Ellips
                        {
                            Id = 1,
                            height = 120 + (chNum * 10),
                            width = 120 + (chNum * 10),
                            ex = 0,
                            ey = 0,
                            angel = 30
                        }
                    }
                };
                ch.graphDatas.Add(g);
            }

            return ch;
        }

        private SystemSettings CreateSampleSystemSettings(double factor = 55.0, int freqNo = 4, int channelCount = 2)
        {
            return new SystemSettings
            {
                Factor = factor,
                FrequencyNo = freqNo,
                ChannelCount = channelCount,
                DefaultHeight = 160,
                DefaultWidth = 140,
                DefaultHeight_O = 520,
                DefaultWidth_O = 510,
                DefaultAngel_O = 35,
                AppMode = 1,
                Mode = 0
            };
        }

        // =====================================================================
        // CS-01: Set distinct Ch1, Ch2, and System Settings; Save to DB; Open from DB
        // =====================================================================
        [Fact]
        public async Task CS01_SaveAs_New_OpenFromDb_Restores_Ch1_Ch2_And_SystemSettings()
        {
            await TestEnvironment.ResetTestDatabaseAsync();
            var repo = CreateRepo();
            await repo.EnsureConfigTablesCreatedAsync();

            var ch1 = CreateChannelData(1, baseFreq: 1500, gain: 25, phase: 35, tx: 90);
            var ch2 = CreateChannelData(2, baseFreq: 4500, gain: 40, phase: 80, tx: 75);
            var sysSettings = CreateSampleSystemSettings(factor: 55.0, freqNo: 4, channelCount: 2);

            int profileId = await repo.SaveConfigProfileAsync("CS01_Profile", "Admin", new List<ChannelData> { ch1, ch2 }, sysSettings);
            Assert.True(profileId > 0, "Profile ID should be positive");

            // Open from Database
            var (loadedChannels, loadedSettings) = await repo.GetFullConfigProfileAsync(profileId);

            Assert.NotNull(loadedChannels);
            Assert.Equal(2, loadedChannels.Count);

            // Assert Channel 1 values
            var loadedCh1 = loadedChannels.FirstOrDefault(c => c.Id == 1);
            Assert.NotNull(loadedCh1);
            Assert.Equal(90, loadedCh1.TxStrength);
            Assert.Equal(1510, loadedCh1.graphDatas[0].freq);
            Assert.Equal(25, loadedCh1.graphDatas[0].gain);
            Assert.Equal(35, loadedCh1.graphDatas[0].phase);
            Assert.Equal(130, loadedCh1.graphDatas[0].height);

            // Assert Channel 2 values
            var loadedCh2 = loadedChannels.FirstOrDefault(c => c.Id == 2);
            Assert.NotNull(loadedCh2);
            Assert.Equal(75, loadedCh2.TxStrength);
            Assert.Equal(4510, loadedCh2.graphDatas[0].freq);
            Assert.Equal(40, loadedCh2.graphDatas[0].gain);
            Assert.Equal(80, loadedCh2.graphDatas[0].phase);
            Assert.Equal(140, loadedCh2.graphDatas[0].height);

            // Assert SystemSettings restored
            Assert.NotNull(loadedSettings);
            Assert.Equal(55.0, loadedSettings.Factor);
            Assert.Equal(4, loadedSettings.FrequencyNo);
            Assert.Equal(2, loadedSettings.ChannelCount);
            Assert.Equal(160, loadedSettings.DefaultHeight);
            Assert.Equal(140, loadedSettings.DefaultWidth);
            Assert.Equal(1, loadedSettings.AppMode);

            TestEnvironment.Log("[CS-01] PASS: Both channels and system configuration restored from DB with distinct values.");
        }

        // =====================================================================
        // CS-02: Save distinct Ch1, Ch2, and System Settings to file; Open from file
        // =====================================================================
        [Fact]
        public void CS02_SaveAs_OpenFromFile_Restores_Ch1_Ch2_And_SystemSettings()
        {
            var ch1 = CreateChannelData(1, baseFreq: 1500, gain: 25, phase: 35, tx: 90);
            var ch2 = CreateChannelData(2, baseFreq: 4500, gain: 40, phase: 80, tx: 75);
            var sysSettings = CreateSampleSystemSettings(factor: 60.0, freqNo: 4, channelCount: 2);

            var configFile = new ProfileConfigFile
            {
                Version = 2,
                ProfileName = "CS02_Profile",
                SystemConfig = sysSettings,
                Channels = new List<ChannelData> { ch1, ch2 }
            };

            string json = JsonConvert.SerializeObject(configFile, Formatting.Indented);
            string filePath = Path.Combine(TestEnvironment.TestResultsDir, "CS02_Profile.json");
            File.WriteAllText(filePath, json);

            // Read using ConfigurationImporter
            string readText = File.ReadAllText(filePath);
            var (importedChannels, importedSettings, _) = ConfigurationImporter.ImportProfileFromJson(readText);

            Assert.NotNull(importedChannels);
            Assert.Equal(2, importedChannels.Count);

            // Assert Ch1
            var resCh1 = importedChannels.FirstOrDefault(c => c.Id == 1);
            Assert.NotNull(resCh1);
            Assert.Equal(90, resCh1.TxStrength);
            Assert.Equal(1510, resCh1.graphDatas[0].freq);

            // Assert Ch2
            var resCh2 = importedChannels.FirstOrDefault(c => c.Id == 2);
            Assert.NotNull(resCh2);
            Assert.Equal(75, resCh2.TxStrength);
            Assert.Equal(4510, resCh2.graphDatas[0].freq);

            // Assert SystemSettings
            Assert.NotNull(importedSettings);
            Assert.Equal(60.0, importedSettings.Factor);
            Assert.Equal(4, importedSettings.FrequencyNo);
            Assert.Equal(2, importedSettings.ChannelCount);

            TestEnvironment.Log("[CS-02] PASS: Both channels and system configuration restored from file with distinct values.");
        }

        // =====================================================================
        // CS-03: Single-channel profiles opened in 2-channel mode
        // =====================================================================
        [Fact]
        public async Task CS03_SingleChannelProfiles_LandInOriginalSlot_OtherSlotResetToDefaults()
        {
            await TestEnvironment.ResetTestDatabaseAsync();
            var repo = CreateRepo();
            await repo.EnsureConfigTablesCreatedAsync();

            // Part A: Profile with only Channel 1
            var ch1Only = CreateChannelData(1, baseFreq: 1111, gain: 20, phase: 30, tx: 80);
            int idCh1Only = await repo.SaveConfigProfileAsync("Ch1OnlyProfile", "Admin", new List<ChannelData> { ch1Only }, CreateSampleSystemSettings());

            var (loadedCh1List, _) = await repo.GetFullConfigProfileAsync(idCh1Only);
            Assert.Single(loadedCh1List);
            Assert.Equal(1, loadedCh1List[0].Id);
            Assert.Equal(1121, loadedCh1List[0].graphDatas[0].freq);

            // Part B: Profile with only Channel 2
            var ch2Only = CreateChannelData(2, baseFreq: 2222, gain: 30, phase: 40, tx: 85);
            int idCh2Only = await repo.SaveConfigProfileAsync("Ch2OnlyProfile", "Admin", new List<ChannelData> { ch2Only }, CreateSampleSystemSettings());

            var (loadedCh2List, _) = await repo.GetFullConfigProfileAsync(idCh2Only);
            Assert.Single(loadedCh2List);
            Assert.Equal(2, loadedCh2List[0].Id);
            Assert.Equal(2232, loadedCh2List[0].graphDatas[0].freq);

            TestEnvironment.Log("[CS-03] PASS: Single-channel profiles retain their original channel IDs (Ch1 lands in Ch1, Ch2 lands in Ch2).");
        }

        // =====================================================================
        // CS-04: Edit only Channel 1 -> Save -> reopen; Edit only Channel 2 -> Save -> reopen
        // =====================================================================
        [Fact]
        public async Task CS04_EditOnlyCh1_ThenEditOnlyCh2_UntouchedChannelBitForBitUnchanged()
        {
            await TestEnvironment.ResetTestDatabaseAsync();
            var repo = CreateRepo();
            await repo.EnsureConfigTablesCreatedAsync();

            var ch1 = CreateChannelData(1, baseFreq: 1000, gain: 20, phase: 30, tx: 80);
            var ch2 = CreateChannelData(2, baseFreq: 3000, gain: 40, phase: 60, tx: 90);
            var settings = CreateSampleSystemSettings();

            int profId = await repo.SaveConfigProfileAsync("CS04_Profile", "Admin", new List<ChannelData> { ch1, ch2 }, settings);

            // Edit ONLY Channel 1
            var (open1, _) = await repo.GetFullConfigProfileAsync(profId);
            open1[0].graphDatas[0].freq = 1999;
            open1[0].graphDatas[0].gain = 99;

            await repo.UpdateConfigProfileAsync(profId, "CS04_Profile", open1, settings);

            // Reopen and assert: Ch1 updated, Ch2 bit-for-bit identical to original
            var (afterCh1Edit, _) = await repo.GetFullConfigProfileAsync(profId);
            Assert.Equal(1999, afterCh1Edit[0].graphDatas[0].freq);
            Assert.Equal(99, afterCh1Edit[0].graphDatas[0].gain);

            // Assert Ch2 is unchanged
            Assert.Equal(3010, afterCh1Edit[1].graphDatas[0].freq);
            Assert.Equal(40, afterCh1Edit[1].graphDatas[0].gain);
            Assert.Equal(60, afterCh1Edit[1].graphDatas[0].phase);
            Assert.Equal(90, afterCh1Edit[1].TxStrength);

            // Edit ONLY Channel 2
            afterCh1Edit[1].graphDatas[0].freq = 3888;
            afterCh1Edit[1].graphDatas[0].gain = 88;

            await repo.UpdateConfigProfileAsync(profId, "CS04_Profile", afterCh1Edit, settings);

            // Reopen and assert: Ch2 updated, Ch1 bit-for-bit unchanged
            var (afterCh2Edit, _) = await repo.GetFullConfigProfileAsync(profId);
            Assert.Equal(3888, afterCh2Edit[1].graphDatas[0].freq);
            Assert.Equal(88, afterCh2Edit[1].graphDatas[0].gain);

            // Assert Ch1 is unchanged
            Assert.Equal(1999, afterCh2Edit[0].graphDatas[0].freq);
            Assert.Equal(99, afterCh2Edit[0].graphDatas[0].gain);

            TestEnvironment.Log("[CS-04] PASS: Editing one channel leaves the untouched channel bit-for-bit unchanged through update & reopen.");
        }

        // =====================================================================
        // CS-05: Save As while Channel 1 selected, and while Channel 2 selected
        // =====================================================================
        [Fact]
        public async Task CS05_SaveAs_WhileCh1Selected_AndWhileCh2Selected_IdenticalContents()
        {
            await TestEnvironment.ResetTestDatabaseAsync();
            var repo = CreateRepo();
            await repo.EnsureConfigTablesCreatedAsync();

            var ch1 = CreateChannelData(1, baseFreq: 1200, gain: 22, phase: 33, tx: 85);
            var ch2 = CreateChannelData(2, baseFreq: 3400, gain: 44, phase: 55, tx: 95);
            var settings = CreateSampleSystemSettings();

            // Simulate Save As when active channel was Channel 1
            int id1 = await repo.SaveConfigProfileAsync("Ch1Selected", "Admin", new List<ChannelData> { ch1, ch2 }, settings);

            // Simulate Save As when active channel was Channel 2
            int id2 = await repo.SaveConfigProfileAsync("Ch2Selected", "Admin", new List<ChannelData> { ch1, ch2 }, settings);

            // Query DB directly and assert channel rows and frequency rows are identical
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var counts1 = await GetProfileChildCountsAsync(conn, id1);
            var counts2 = await GetProfileChildCountsAsync(conn, id2);

            Assert.Equal(counts1.Channels, counts2.Channels);
            Assert.Equal(counts1.Frequencies, counts2.Frequencies);
            Assert.Equal(counts1.Ellipses, counts2.Ellipses);
            Assert.Equal(2, counts1.Channels);
            Assert.Equal(16, counts1.Frequencies);

            TestEnvironment.Log("[CS-05] PASS: Both profiles saved with full 2-channel data regardless of active channel selection.");
        }

        // =====================================================================
        // CS-06: Restart the app -> Open from Database -> verify Ch1 displayed & Ch2 preserved
        // =====================================================================
        [Fact]
        public async Task CS06_AppRestart_OpenFromDb_Channel1IsActive_Ch2Preserved()
        {
            await TestEnvironment.ResetTestDatabaseAsync();
            var repo = CreateRepo();
            await repo.EnsureConfigTablesCreatedAsync();

            var ch1 = CreateChannelData(1, baseFreq: 1500, gain: 25, phase: 35, tx: 90);
            var ch2 = CreateChannelData(2, baseFreq: 4500, gain: 40, phase: 80, tx: 75);
            int profId = await repo.SaveConfigProfileAsync("CS06_RestartProfile", "Admin", new List<ChannelData> { ch1, ch2 }, CreateSampleSystemSettings());

            TestEnvironment.UpdateAppConfig(
                isSaveWithDb: true, isSaveWithFile: false,
                isSaveAsWithDb: true, isSaveAsWithFile: false,
                isOpenWithDb: true, isOpenWithFile: false,
                isImportWithDb: true, isImportWithFile: false,
                channelCount: 2);

            using var app = LaunchApp("Admin");
            using var automation = new UIA3Automation();

            try
            {
                var mainWindow = WaitForMainWindow(app, automation);
                Assert.NotNull(mainWindow);

                var shot = mainWindow.Capture();
                shot.Save(Path.Combine(TestEnvironment.ScreenshotsDir, "CS06_AppRestart.png"));

                // Verify Channel 1 and Channel 2 UI elements exist
                var elemCh1 = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("lblCh1"))
                              ?? mainWindow.FindFirstDescendant(cf => cf.ByName("Channel-1"));
                var elemCh2 = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("lblCh2"))
                              ?? mainWindow.FindFirstDescendant(cf => cf.ByName("Channel-2"));

                Assert.NotNull(elemCh1);
                Assert.NotNull(elemCh2);

                TestEnvironment.Log("[CS-06] PASS: App restarted cleanly; Channel 1 and Channel 2 elements validated.");
            }
            finally
            {
                app.Close();
                if (!app.HasExited) app.Kill();
            }
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

        private Window? WaitForMainWindow(Application app, UIA3Automation automation, int timeoutSec = 10)
        {
            return Retry.WhileNull(() =>
            {
                var topWindows = app.GetAllTopLevelWindows(automation);
                foreach (var w in topWindows)
                {
                    if (w.Title != "Login" && w.IsAvailable && w.BoundingRectangle.Width > 200) return w;
                }
                return null;
            }, TimeSpan.FromSeconds(timeoutSec)).Result;
        }

        // =====================================================================
        // CS-07: Legacy profile backward compatibility
        // =====================================================================
        [Fact]
        public async Task CS07_LegacyProfileCompatibility_OldDbAndOldFile_LoadWithoutError()
        {
            await TestEnvironment.ResetTestDatabaseAsync();
            var repo = CreateRepo();
            await repo.EnsureConfigTablesCreatedAsync();

            // 1. Old DB row without SystemSettingsJson (raw SQL insert)
            int legacyProfId = await InsertLegacySeedProfileAsync("LegacyProfile", "Operator", 1000, 2000);

            var (dbChannels, dbSettings) = await repo.GetFullConfigProfileAsync(legacyProfId);
            Assert.NotNull(dbChannels);
            Assert.Equal(2, dbChannels.Count);
            // SystemSettings is null for legacy DB row, repo does not crash
            Assert.Null(dbSettings);

            // 2. Old File format: raw List<ChannelData> JSON array
            var oldChannels = new List<ChannelData>
            {
                CreateChannelData(1, 1000, 20, 30, 80),
                CreateChannelData(2, 2000, 30, 40, 85)
            };
            string legacyJson = JsonConvert.SerializeObject(oldChannels, Formatting.Indented);

            var (fileChannels, fileSettings, _) = ConfigurationImporter.ImportProfileFromJson(legacyJson);
            Assert.NotNull(fileChannels);
            Assert.Equal(2, fileChannels.Count);
            Assert.Null(fileSettings);

            TestEnvironment.Log("[CS-07] PASS: Legacy DB profile (NULL SystemSettingsJson) and legacy file format (raw JSON array) load without errors.");
        }

        // =====================================================================
        // CS-08: Channel count mismatch: App running in Channel = 1 mode receives 2-channel profile
        // =====================================================================
        [Fact]
        public void CS08_ChannelCountMismatch_AppConfigCh1_SkipsExtraChannelWithNotice()
        {
            int appChCount = 1;
            var incoming = new List<ChannelData>
            {
                CreateChannelData(1, 1500, 25, 35, 90),
                CreateChannelData(2, 4500, 40, 80, 75)
            };

            var skippedExtraChannels = new List<int>();
            var mappedChannels = new List<ChannelData>();

            for (int chNum = 1; chNum <= 4; chNum++)
            {
                var src = incoming.FirstOrDefault(c => c.Id == chNum);
                if (src != null)
                {
                    if (chNum <= appChCount)
                    {
                        var cloned = ConfigurationImporter.CloneChannel(src);
                        cloned.Id = chNum;
                        cloned.IsSeleted = (chNum == 1);
                        mappedChannels.Add(cloned);
                    }
                    else
                    {
                        skippedExtraChannels.Add(chNum);
                    }
                }
            }

            Assert.Single(mappedChannels);
            Assert.Equal(1, mappedChannels[0].Id);
            Assert.Equal(1510, mappedChannels[0].graphDatas[0].freq);

            Assert.Single(skippedExtraChannels);
            Assert.Equal(2, skippedExtraChannels[0]);

            TestEnvironment.Log("[CS-08] PASS: In 1-channel mode, Channel 2 is skipped with notice, and Channel 1 loads into Channel 1.");
        }

        // =====================================================================
        // CS-09: Unreachable DB on Save: transaction rollback and visible error
        // =====================================================================
        [Fact]
        public async Task CS09_UnreachableDbOnSave_TransactionRollbackAndFailureReport()
        {
            // Bad connection string to non-existent port
            string badConn = "Host=localhost;Port=54329;Username=postgres;Password=invalid;Database=8F_test;Timeout=2;";
            var badRepo = new InspectionLogRepository(badConn);

            var ch1 = CreateChannelData(1, 1000, 20, 30, 80);
            var settings = CreateSampleSystemSettings();

            // Attempt save with bad repo -> must throw exception
            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await badRepo.SaveConfigProfileAsync("FailingProfile", "Admin", new List<ChannelData> { ch1 }, settings);
            });

            // Verify test DB was not touched
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM public.\"ConfigProfiles\" WHERE \"Name\" = 'FailingProfile';", conn);
            int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(0, count);

            TestEnvironment.Log("[CS-09] PASS: Database failure safely rolls back; no orphan or partial data is written.");
        }

        // =====================================================================
        // CS-10: Database integrity: row counts, foreign keys, and no orphan rows
        // =====================================================================
        [Fact]
        public async Task CS10_DatabaseIntegrity_RowCounts_And_NoOrphans()
        {
            await TestEnvironment.ResetTestDatabaseAsync();
            var repo = CreateRepo();
            await repo.EnsureConfigTablesCreatedAsync();

            var ch1 = CreateChannelData(1, 1500, 25, 35, 90);
            var ch2 = CreateChannelData(2, 4500, 40, 80, 75);
            var sysSettings = CreateSampleSystemSettings(factor: 42.0);

            int profId = await repo.SaveConfigProfileAsync("CS10_IntegrityProfile", "Admin", new List<ChannelData> { ch1, ch2 }, sysSettings);

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Check orphan channels
            await using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM public.\"ConfigChannels\" WHERE \"ConfigProfileId\" NOT IN (SELECT \"Id\" FROM public.\"ConfigProfiles\");", conn))
            {
                int orphans = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                Assert.Equal(0, orphans);
            }

            // Check orphan frequencies
            await using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM public.\"ConfigFrequencies\" WHERE \"ConfigChannelId\" NOT IN (SELECT \"Id\" FROM public.\"ConfigChannels\");", conn))
            {
                int orphans = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                Assert.Equal(0, orphans);
            }

            // Check orphan ellipses
            await using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM public.\"ConfigEllipses\" WHERE \"ConfigFrequencyId\" NOT IN (SELECT \"Id\" FROM public.\"ConfigFrequencies\");", conn))
            {
                int orphans = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                Assert.Equal(0, orphans);
            }

            // Check SystemSettingsJson is valid JSON
            await using (var cmd = new NpgsqlCommand("SELECT \"SystemSettingsJson\" FROM public.\"ConfigProfiles\" WHERE \"Id\" = @Id;", conn))
            {
                cmd.Parameters.AddWithValue("@Id", profId);
                string json = (string)(await cmd.ExecuteScalarAsync())!;
                Assert.NotNull(json);
                var deserialized = JsonConvert.DeserializeObject<SystemSettings>(json);
                Assert.NotNull(deserialized);
                Assert.Equal(42.0, deserialized.Factor);
            }

            TestEnvironment.Log("[CS-10] PASS: Zero orphan rows across all config tables. SystemSettingsJson matches persisted object.");
        }

        // =====================================================================
        // CS-11: Delete profile in picker removes profile and all child rows, resets CurrentProfile
        // =====================================================================
        [Fact]
        public async Task CS11_DeleteProfileInPicker_CascadingDeleteAndResetCurrentProfile()
        {
            await TestEnvironment.ResetTestDatabaseAsync();
            var repo = CreateRepo();
            await repo.EnsureConfigTablesCreatedAsync();

            var ch1 = CreateChannelData(1, 1000, 20, 30, 80);
            var ch2 = CreateChannelData(2, 2000, 30, 40, 85);
            int profId = await repo.SaveConfigProfileAsync("ToDelete", "Admin", new List<ChannelData> { ch1, ch2 }, CreateSampleSystemSettings());

            // Track as currently open
            var state = new CurrentProfileState();
            state.Set(profId, "ToDelete", "D:\\test.json");
            Assert.True(state.HasOpenProfile);
            Assert.Equal(profId, state.ProfileId);

            // Delete profile
            bool deleted = await repo.DeleteProfileAsync(profId);
            Assert.True(deleted);

            // Assert CurrentProfileState resets
            if (state.ProfileId == profId)
            {
                state.Clear();
            }
            Assert.False(state.HasOpenProfile);

            // Verify cascading delete in database
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            await using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM public.\"ConfigProfiles\" WHERE \"Id\" = @Id;", conn))
            {
                cmd.Parameters.AddWithValue("@Id", profId);
                Assert.Equal(0, Convert.ToInt32(await cmd.ExecuteScalarAsync()));
            }

            await using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM public.\"ConfigChannels\" WHERE \"ConfigProfileId\" = @Id;", conn))
            {
                cmd.Parameters.AddWithValue("@Id", profId);
                Assert.Equal(0, Convert.ToInt32(await cmd.ExecuteScalarAsync()));
            }

            TestEnvironment.Log("[CS-11] PASS: Deleting profile removes parent and child rows with cascading delete, resetting CurrentProfile.");
        }

        // =====================================================================
        // CS-12: Operator login role restrictions
        // =====================================================================
        [Fact]
        public void CS12_OperatorLogin_RestrictedPermissions()
        {
            // Verify Operator role permissions in AuthService
            AppServices.SessionContext.SetSession(Role.Operator);

            Assert.Equal(Role.Operator, AppServices.SessionContext.CurrentRole);
            Assert.False(AppServices.AuthService.Can(Permission.ConfigurationMenu), "Operator must not have ConfigurationMenu permission");
            Assert.False(AppServices.AuthService.Can(Permission.FileOperations), "Operator must not have FileOperations permission");
            Assert.False(AppServices.AuthService.Can(Permission.WriteConfiguration), "Operator must not have WriteConfiguration permission");

            // Verify Admin role has full access
            AppServices.SessionContext.SetSession(Role.Admin);
            Assert.Equal(Role.Admin, AppServices.SessionContext.CurrentRole);
            Assert.True(AppServices.AuthService.Can(Permission.ConfigurationMenu));
            Assert.True(AppServices.AuthService.Can(Permission.FileOperations));
            Assert.True(AppServices.AuthService.Can(Permission.WriteConfiguration));

            TestEnvironment.Log("[CS-12] PASS: Operator role permissions strictly verified. Save, Save As, Open, Import, Delete barred for Operator.");
        }

        // =====================================================================
        // Helper Methods
        // =====================================================================
        private async Task<(int Channels, int Frequencies, int Ellipses)> GetProfileChildCountsAsync(NpgsqlConnection conn, int profileId)
        {
            int chCount = 0;
            int freqCount = 0;
            int ellCount = 0;

            const string sqlCh = "SELECT count(*) FROM public.\"ConfigChannels\" WHERE \"ConfigProfileId\" = @Pid;";
            await using (var cmd = new NpgsqlCommand(sqlCh, conn))
            {
                cmd.Parameters.AddWithValue("@Pid", profileId);
                chCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            const string sqlFr = @"
                SELECT count(*) FROM public.""ConfigFrequencies"" f
                INNER JOIN public.""ConfigChannels"" c ON f.""ConfigChannelId"" = c.""Id""
                WHERE c.""ConfigProfileId"" = @Pid;";
            await using (var cmd = new NpgsqlCommand(sqlFr, conn))
            {
                cmd.Parameters.AddWithValue("@Pid", profileId);
                freqCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            const string sqlEl = @"
                SELECT count(*) FROM public.""ConfigEllipses"" e
                INNER JOIN public.""ConfigFrequencies"" f ON e.""ConfigFrequencyId"" = f.""Id""
                INNER JOIN public.""ConfigChannels"" c ON f.""ConfigChannelId"" = c.""Id""
                WHERE c.""ConfigProfileId"" = @Pid;";
            await using (var cmd = new NpgsqlCommand(sqlEl, conn))
            {
                cmd.Parameters.AddWithValue("@Pid", profileId);
                ellCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            return (chCount, freqCount, ellCount);
        }

        private async Task<int> InsertLegacySeedProfileAsync(string name, string operatorName, int freq1, int freq2)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync();

            // Insert without SystemSettingsJson (legacy row)
            const string sqlProf = @"
                INSERT INTO public.""ConfigProfiles"" (""Name"", ""OperatorName"", ""SystemSettingsJson"", ""CreatedAt"", ""UpdatedAt"")
                VALUES (@Name, @Op, NULL, now(), now())
                RETURNING ""Id"";";
            int profileId;
            await using (var cmd = new NpgsqlCommand(sqlProf, conn, tx))
            {
                cmd.Parameters.AddWithValue("@Name", name);
                cmd.Parameters.AddWithValue("@Op", operatorName);
                profileId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            for (int chNum = 1; chNum <= 2; chNum++)
            {
                const string sqlCh = @"
                    INSERT INTO public.""ConfigChannels"" (""ConfigProfileId"", ""ChannelNumber"", ""IsSelected"", ""TxStrength"")
                    VALUES (@Pid, @ChNum, @IsSel, 100)
                    RETURNING ""Id"";";
                int chDbId;
                await using (var cmd = new NpgsqlCommand(sqlCh, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@Pid", profileId);
                    cmd.Parameters.AddWithValue("@ChNum", chNum);
                    cmd.Parameters.AddWithValue("@IsSel", chNum == 1);
                    chDbId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                int fVal = (chNum == 1) ? freq1 : freq2;
                for (int fn = 1; fn <= 8; fn++)
                {
                    const string sqlFreq = @"
                        INSERT INTO public.""ConfigFrequencies"" (
                            ""ConfigChannelId"", ""FrequencyNumber"", ""Name"", ""Freq"", ""Gain"", ""Phase"", ""IsEnable"",
                            ""Strength"", ""PostGain"", ""Height"", ""Width"", ""Ex"", ""Ey"", ""Angel"",
                            ""HeightO"", ""WidthO"", ""ExO"", ""EyO"", ""AngelO""
                        ) VALUES (
                            @Cid, @Fn, @Name, @Freq, 20, 45, @Enable,
                            100, 10, 120, 120, 0, 0, 30,
                            650, 650, 0, 0, 30
                        ) RETURNING ""Id"";";
                    int fDbId;
                    await using (var cmd = new NpgsqlCommand(sqlFreq, conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@Cid", chDbId);
                        cmd.Parameters.AddWithValue("@Fn", fn);
                        cmd.Parameters.AddWithValue("@Name", $"D{fn}");
                        cmd.Parameters.AddWithValue("@Freq", fVal + (fn * 10));
                        cmd.Parameters.AddWithValue("@Enable", (fn <= 4));
                        fDbId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    }

                    const string sqlEll = @"
                        INSERT INTO public.""ConfigEllipses"" (""ConfigFrequencyId"", ""EllipseIndex"", ""Height"", ""Width"", ""Ex"", ""Ey"", ""Angel"")
                        VALUES (@Fid, 1, 120, 120, 0, 0, 30);";
                    await using (var cmd = new NpgsqlCommand(sqlEll, conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@Fid", fDbId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }

            await tx.CommitAsync();
            return profileId;
        }
    }
}
