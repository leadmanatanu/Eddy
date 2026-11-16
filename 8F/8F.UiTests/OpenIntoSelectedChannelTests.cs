using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Xunit;
using _8F.Models;
using _8F.Repositories.Implementations;
using _8F.Services;
using _8F.Services.Implementations;
using _8F.Services.Interfaces;
using _8F.Views;

namespace _8F.UiTests
{
    [Collection("DatabaseCollection")]
    public class OpenIntoSelectedChannelTests
    {
        [Fact]
        public void OP_05_HelperRule1_SingleSavedChannel_ReturnsThatChannel()
        {
            var service = new ProfileLoadService(() => null);
            var channels = new List<ChannelData>
            {
                new() { Id = 1, graphDatas = new List<GraphData> { new() { Id = 1, freq = 1000 } } }
            };

            var chForTarget1 = service.SelectSourceChannel(channels, 1);
            var chForTarget2 = service.SelectSourceChannel(channels, 2);

            Assert.NotNull(chForTarget1);
            Assert.Equal(1, chForTarget1.Id);
            Assert.NotNull(chForTarget2);
            Assert.Equal(1, chForTarget2.Id);
        }

        [Fact]
        public void OP_06_HelperRule2_MatchingChannelId_ReturnsMatchingChannel()
        {
            var service = new ProfileLoadService(() => null);
            var channels = new List<ChannelData>
            {
                new() { Id = 1, graphDatas = new List<GraphData> { new() { Id = 1, freq = 1000 } } },
                new() { Id = 2, graphDatas = new List<GraphData> { new() { Id = 1, freq = 2000 } } }
            };

            var ch1 = service.SelectSourceChannel(channels, 1);
            var ch2 = service.SelectSourceChannel(channels, 2);

            Assert.NotNull(ch1);
            Assert.Equal(1, ch1.Id);
            Assert.Equal(1000, ch1.graphDatas[0].freq);

            Assert.NotNull(ch2);
            Assert.Equal(2, ch2.Id);
            Assert.Equal(2000, ch2.graphDatas[0].freq);
        }

        [Fact]
        public void OP_07_HelperRule3_NoMatch_ReturnsLowestNumberedChannel()
        {
            var service = new ProfileLoadService(() => null);
            var channels = new List<ChannelData>
            {
                new() { Id = 3, graphDatas = new List<GraphData> { new() { Id = 1, freq = 3000 } } },
                new() { Id = 2, graphDatas = new List<GraphData> { new() { Id = 1, freq = 2000 } } }
            };

            // Target channel 1 does not exist in [2, 3] -> should return lowest (2)
            var ch1 = service.SelectSourceChannel(channels, 1);

            Assert.NotNull(ch1);
            Assert.Equal(2, ch1.Id);
            Assert.Equal(2000, ch1.graphDatas[0].freq);
        }

        [Fact]
        public async Task OP_01_OpenDb_IntoChannel1_WhenChannel1Selected_LeavesChannel2Untouched()
        {
            await TestEnvironment.ResetTestDatabaseAsync();
            var (idA, idB) = await TestEnvironment.SeedKnownProfilesAsync();

            MainWindow? mainWin = await WpfTestHelper.InvokeAsync(() =>
            {
                var win = new MainWindow();
                win.InitialGraphData(false);
                win.chNo = 2;
                // Channel 1 selected
                win.SelectCh1();
                // Set initial Channel 2 freq to 9999
                DeviceCOM.channelDatas[1].graphDatas[0].freq = 9999;
                DeviceCOM.ChannelBalanceRequired[1] = false;
                DeviceCOM.ChannelBalanceRequired[2] = false;
                return win;
            });

            var service = new ProfileLoadService(
                () => mainWin,
                () => new InspectionLogRepository(TestEnvironment.ConnectionString));

            // Act: load Profile idA into target Channel 1
            var result = await service.LoadFromDbAsync(idA, 1, ProfileLoadSource.MenuOpenDb, isInteractive: false);

            Assert.True(result.Success, result.Message);
            Assert.Equal(1, result.TargetChannel);

            await WpfTestHelper.InvokeAsync(() =>
            {
                // Assert selection remains Channel 1
                Assert.Equal(1, mainWin!.GetSelectedChannelNumber());

                // Assert Channel 1 data was replaced
                Assert.Equal(1010, DeviceCOM.channelDatas[0].graphDatas[0].freq);

                // Assert Channel 2 data was UNTOUCHED
                Assert.Equal(9999, DeviceCOM.channelDatas[1].graphDatas[0].freq);

                // Assert balance flag marked only for target channel
                Assert.True(DeviceCOM.ChannelBalanceRequired[1]);
                Assert.False(DeviceCOM.ChannelBalanceRequired[2]);

                // Assert CurrentProfileState records profile for channel 1
                var ch1Profile = mainWin.CurrentProfile.GetForChannel(1);
                Assert.NotNull(ch1Profile);
                Assert.Equal(idA, ch1Profile.ProfileId);
            });
        }

        [Fact]
        public async Task OP_02_OpenDb_IntoChannel2_WhenChannel2Selected_LeavesChannel1Untouched()
        {
            await TestEnvironment.ResetTestDatabaseAsync();
            var (idA, idB) = await TestEnvironment.SeedKnownProfilesAsync();

            MainWindow? mainWin = await WpfTestHelper.InvokeAsync(() =>
            {
                var win = new MainWindow();
                win.InitialGraphData(false);
                win.chNo = 2;
                // Select Channel 2
                DeviceCOM.channelDatas[0].IsSeleted = false;
                DeviceCOM.channelDatas[1].IsSeleted = true;
                // Set initial Channel 1 freq to 8888
                DeviceCOM.channelDatas[0].graphDatas[0].freq = 8888;
                DeviceCOM.ChannelBalanceRequired[1] = false;
                DeviceCOM.ChannelBalanceRequired[2] = false;
                return win;
            });

            var service = new ProfileLoadService(
                () => mainWin,
                () => new InspectionLogRepository(TestEnvironment.ConnectionString));

            // Act: load Profile idB into target Channel 2
            var result = await service.LoadFromDbAsync(idB, 2, ProfileLoadSource.MenuOpenDb, isInteractive: false);

            Assert.True(result.Success, result.Message);
            Assert.Equal(2, result.TargetChannel);

            await WpfTestHelper.InvokeAsync(() =>
            {
                // Assert selection remains Channel 2
                Assert.Equal(2, mainWin!.GetSelectedChannelNumber());

                // Assert Channel 2 data was updated
                Assert.Equal(4010, DeviceCOM.channelDatas[1].graphDatas[0].freq);

                // Assert Channel 1 data was UNTOUCHED
                Assert.Equal(8888, DeviceCOM.channelDatas[0].graphDatas[0].freq);

                // Assert balance flag marked only for target channel
                Assert.False(DeviceCOM.ChannelBalanceRequired[1]);
                Assert.True(DeviceCOM.ChannelBalanceRequired[2]);

                // Assert CurrentProfileState records profile for channel 2
                var ch2Profile = mainWin.CurrentProfile.GetForChannel(2);
                Assert.NotNull(ch2Profile);
                Assert.Equal(idB, ch2Profile.ProfileId);
            });
        }

        [Fact]
        public async Task OP_03_OpenFile_IntoChannel1_WhenChannel1Selected_StaysOnChannel1()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"8F_TestFile_Ch1_{Guid.NewGuid():N}.json");
            try
            {
                var fileContent = new ProfileConfigFile
                {
                    Version = 2,
                    ProfileName = "FileTestCh1",
                    SystemConfig = new SystemSettings { ChannelCount = 2, Factor = 25 },
                    Channels = new List<ChannelData>
                    {
                        new()
                        {
                            Id = 1,
                            TxStrength = 95,
                            graphDatas = new List<GraphData>
                            {
                                new() { Id = 1, freq = 5555, isEnable = true, ellipses = new List<Ellips>() }
                            }
                        }
                    }
                };
                await File.WriteAllTextAsync(tempFile, JsonConvert.SerializeObject(fileContent, Formatting.Indented));

                MainWindow? mainWin = await WpfTestHelper.InvokeAsync(() =>
                {
                    var win = new MainWindow();
                    win.InitialGraphData(false);
                    win.chNo = 2;
                    win.SelectCh1();
                    DeviceCOM.channelDatas[1].graphDatas[0].freq = 7777;
                    return win;
                });

                var service = new ProfileLoadService(() => mainWin);
                var result = await service.LoadFromFileAsync(tempFile, 1, ProfileLoadSource.MenuOpenFile, isInteractive: false);

                Assert.True(result.Success, result.Message);

                await WpfTestHelper.InvokeAsync(() =>
                {
                    Assert.Equal(1, mainWin!.GetSelectedChannelNumber());
                    Assert.Equal(5555, DeviceCOM.channelDatas[0].graphDatas[0].freq);
                    Assert.Equal(7777, DeviceCOM.channelDatas[1].graphDatas[0].freq);
                    Assert.Equal(tempFile, mainWin.CurrentProfile.FilePath);
                });
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task OP_04_OpenFile_IntoChannel2_WhenChannel2Selected_StaysOnChannel2()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"8F_TestFile_Ch2_{Guid.NewGuid():N}.json");
            try
            {
                var fileContent = new ProfileConfigFile
                {
                    Version = 2,
                    ProfileName = "FileTestCh2",
                    SystemConfig = new SystemSettings { ChannelCount = 2, Factor = 30 },
                    Channels = new List<ChannelData>
                    {
                        new()
                        {
                            Id = 2,
                            TxStrength = 80,
                            graphDatas = new List<GraphData>
                            {
                                new() { Id = 1, freq = 6666, isEnable = true, ellipses = new List<Ellips>() }
                            }
                        }
                    }
                };
                await File.WriteAllTextAsync(tempFile, JsonConvert.SerializeObject(fileContent, Formatting.Indented));

                MainWindow? mainWin = await WpfTestHelper.InvokeAsync(() =>
                {
                    var win = new MainWindow();
                    win.InitialGraphData(false);
                    win.chNo = 2;
                    DeviceCOM.channelDatas[0].IsSeleted = false;
                    DeviceCOM.channelDatas[1].IsSeleted = true;
                    DeviceCOM.channelDatas[0].graphDatas[0].freq = 3333;
                    return win;
                });

                var service = new ProfileLoadService(() => mainWin);
                var result = await service.LoadFromFileAsync(tempFile, 2, ProfileLoadSource.MenuOpenFile, isInteractive: false);

                Assert.True(result.Success, result.Message);

                await WpfTestHelper.InvokeAsync(() =>
                {
                    Assert.Equal(2, mainWin!.GetSelectedChannelNumber());
                    Assert.Equal(6666, DeviceCOM.channelDatas[1].graphDatas[0].freq);
                    Assert.Equal(3333, DeviceCOM.channelDatas[0].graphDatas[0].freq);
                });
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task OP_08_SystemSettings_PreservesRunningChannelCountAndAppMode()
        {
            MainWindow? mainWin = await WpfTestHelper.InvokeAsync(() =>
            {
                var win = new MainWindow();
                win.InitialGraphData(false);
                win.chNo = 2;
                win.modeApp = 0;
                return win;
            });

            var conflictSettings = new SystemSettings
            {
                ChannelCount = 4, // Conflicts with running chNo = 2
                AppMode = 1,      // Conflicts with running modeApp = 0
                Factor = 42
            };

            var channels = new List<ChannelData>
            {
                new()
                {
                    Id = 1,
                    graphDatas = new List<GraphData> { new() { Id = 1, freq = 1234, isEnable = true } }
                }
            };

            var service = new ProfileLoadService(() => mainWin);
            var result = await service.LoadFromChannelsAsync(
                channels,
                conflictSettings,
                "ConflictTest",
                profileId: 100,
                filePath: null,
                targetChannel: 1,
                source: ProfileLoadSource.MenuOpenDb,
                isInteractive: false);

            Assert.True(result.Success);

            await WpfTestHelper.InvokeAsync(() =>
            {
                // Assert running ChannelCount and AppMode were PRESERVED
                Assert.Equal(2, mainWin!.chNo);
                Assert.Equal(0, mainWin.modeApp);
                // Assert safe setting (Factor) was applied
                Assert.Equal(42, mainWin.GetCurrentSystemSettings().Factor);
            });
        }
    }
}
