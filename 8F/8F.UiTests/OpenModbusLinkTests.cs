using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
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
    public class OpenModbusLinkTests
    {
        private const int TestPortBase = 16000;

        [Fact]
        public async Task LK_01_MenuOpenDb_UpdatesTargetChannelModbusRegisterSilently()
        {
            await TestEnvironment.ResetTestDatabaseAsync();
            var (idA, idB) = await TestEnvironment.SeedKnownProfilesAsync();

            int testPort = TestPortBase + 1;
            var modbus = new ModbusSlaveService(channelCount: 2);
            await modbus.StartAsync(testPort);

            MainWindow? mainWin = await WpfTestHelper.InvokeAsync(() =>
            {
                var win = new MainWindow();
                win.InitialGraphData(false);
                win.chNo = 2;
                win.modbusSlaveService = modbus;
                return win;
            });

            try
            {
                var service = new ProfileLoadService(
                    () => mainWin,
                    () => new InspectionLogRepository(TestEnvironment.ConnectionString),
                    () => modbus);

                // Act: Menu Open DB Profile idA into Channel 1
                var result = await service.LoadFromDbAsync(idA, 1, ProfileLoadSource.MenuOpenDb, isInteractive: false);
                Assert.True(result.Success);

                // Assert Modbus holding register 1 was silently updated to idA
                ushort reg1 = modbus.ReadRegister(ModbusSlaveService.ChannelIndexToAddress(0));
                Assert.Equal(idA, reg1);

                // Assert holding register 2 is still 0
                ushort reg2 = modbus.ReadRegister(ModbusSlaveService.ChannelIndexToAddress(1));
                Assert.Equal(0, reg2);
            }
            finally
            {
                await modbus.StopAsync();
            }
        }

        [Fact]
        public async Task LK_02_MenuImport_UpdatesTargetChannelModbusRegisterSilently()
        {
            int testPort = TestPortBase + 2;
            var modbus = new ModbusSlaveService(channelCount: 2);
            await modbus.StartAsync(testPort);

            MainWindow? mainWin = await WpfTestHelper.InvokeAsync(() =>
            {
                var win = new MainWindow();
                win.InitialGraphData(false);
                win.chNo = 2;
                win.modbusSlaveService = modbus;
                return win;
            });

            try
            {
                var service = new ProfileLoadService(
                    () => mainWin,
                    () => new InspectionLogRepository(TestEnvironment.ConnectionString),
                    () => modbus);

                var channels = new List<ChannelData>
                {
                    new()
                    {
                        Id = 2,
                        TxStrength = 100,
                        graphDatas = new List<GraphData> { new() { Id = 1, freq = 7777, isEnable = true } }
                    }
                };

                // Act: Import Profile #55 into Channel 2
                var result = await service.LoadFromChannelsAsync(
                    channels,
                    null,
                    "ImportProfile",
                    profileId: 55,
                    filePath: "dummy.json",
                    targetChannel: 2,
                    source: ProfileLoadSource.MenuImport,
                    isInteractive: false);

                Assert.True(result.Success);

                // Assert Modbus holding register 2 was silently updated to 55
                ushort reg2 = modbus.ReadRegister(ModbusSlaveService.ChannelIndexToAddress(1));
                Assert.Equal(55, reg2);
            }
            finally
            {
                await modbus.StopAsync();
            }
        }

        [Fact]
        public async Task LK_03_MasterWrite_FC06_Address1_LoadsChannel1_WithoutChangingUiSelection()
        {
            await TestEnvironment.ResetTestDatabaseAsync();
            var (idA, idB) = await TestEnvironment.SeedKnownProfilesAsync();

            int testPort = TestPortBase + 3;
            var modbus = new ModbusSlaveService(channelCount: 2);
            await modbus.StartAsync(testPort);

            MainWindow? mainWin = await WpfTestHelper.InvokeAsync(() =>
            {
                var win = new MainWindow();
                win.InitialGraphData(false);
                win.chNo = 2;
                win.modbusSlaveService = modbus;

                // Select Channel 2 in UI!
                DeviceCOM.channelDatas[0].IsSeleted = false;
                DeviceCOM.channelDatas[1].IsSeleted = true;
                DeviceCOM.channelDatas[0].graphDatas[0].freq = 1111;
                return win;
            });

            _8F.Auth.AppServices.ProfileLoadService = new ProfileLoadService(
                () => mainWin,
                () => new InspectionLogRepository(TestEnvironment.ConnectionString),
                () => modbus);

            try
            {
                // Modbus Master connects and writes Profile idA to Register 1 (Channel 1)
                using (var client = new TcpClient())
                {
                    await client.ConnectAsync("127.0.0.1", testPort);
                    var stream = client.GetStream();

                    byte[] req = new byte[]
                    {
                        0x00, 0x01, 0x00, 0x00, 0x00, 0x06, 0x01, 0x06,
                        0x00, 0x01, // Address 1 = Channel 1
                        (byte)(idA >> 8), (byte)(idA & 0xFF)
                    };
                    await stream.WriteAsync(req, 0, req.Length);

                    // Master receives response immediately
                    byte[] resp = new byte[12];
                    int bytesRead = await stream.ReadAsync(resp, 0, 12);
                    Assert.Equal(12, bytesRead);
                    Assert.Equal(0x06, resp[7]); // Echoed FC 06
                }

                // Wait for background async load to complete
                await Task.Delay(500);

                await WpfTestHelper.InvokeAsync(() =>
                {
                    // Assert selection remains Channel 2 (UI was NOT switched!)
                    Assert.Equal(2, mainWin!.GetSelectedChannelNumber());

                    // Assert Channel 1 data was updated from DB profile idA
                    Assert.Equal(1010, DeviceCOM.channelDatas[0].graphDatas[0].freq);

                    // Assert Channel 1 profile state updated
                    var ch1Prof = mainWin.CurrentProfile.GetForChannel(1);
                    Assert.NotNull(ch1Prof);
                    Assert.Equal(idA, ch1Prof.ProfileId);
                });
            }
            finally
            {
                await modbus.StopAsync();
            }
        }

        [Fact]
        public async Task LK_04_MasterWrite_FC06_Address2_LoadsChannel2_WithoutChangingUiSelection()
        {
            await TestEnvironment.ResetTestDatabaseAsync();
            var (idA, idB) = await TestEnvironment.SeedKnownProfilesAsync();

            int testPort = TestPortBase + 4;
            var modbus = new ModbusSlaveService(channelCount: 2);
            await modbus.StartAsync(testPort);

            MainWindow? mainWin = await WpfTestHelper.InvokeAsync(() =>
            {
                var win = new MainWindow();
                win.InitialGraphData(false);
                win.chNo = 2;
                win.modbusSlaveService = modbus;

                // Select Channel 1 in UI
                win.SelectCh1();
                DeviceCOM.channelDatas[1].graphDatas[0].freq = 2222;
                return win;
            });

            _8F.Auth.AppServices.ProfileLoadService = new ProfileLoadService(
                () => mainWin,
                () => new InspectionLogRepository(TestEnvironment.ConnectionString),
                () => modbus);

            try
            {
                // Modbus Master writes Profile idB to Register 2 (Channel 2)
                using (var client = new TcpClient())
                {
                    await client.ConnectAsync("127.0.0.1", testPort);
                    var stream = client.GetStream();

                    byte[] req = new byte[]
                    {
                        0x00, 0x01, 0x00, 0x00, 0x00, 0x06, 0x01, 0x06,
                        0x00, 0x02, // Address 2 = Channel 2
                        (byte)(idB >> 8), (byte)(idB & 0xFF)
                    };
                    await stream.WriteAsync(req, 0, req.Length);

                    byte[] resp = new byte[12];
                    await stream.ReadAsync(resp, 0, 12);
                    Assert.Equal(0x06, resp[7]);
                }

                await Task.Delay(500);

                await WpfTestHelper.InvokeAsync(() =>
                {
                    // Selection remains Channel 1
                    Assert.Equal(1, mainWin!.GetSelectedChannelNumber());

                    // Channel 2 data updated to idB
                    Assert.Equal(4010, DeviceCOM.channelDatas[1].graphDatas[0].freq);

                    // Channel 2 profile state updated
                    var ch2Prof = mainWin.CurrentProfile.GetForChannel(2);
                    Assert.NotNull(ch2Prof);
                    Assert.Equal(idB, ch2Prof.ProfileId);
                });
            }
            finally
            {
                await modbus.StopAsync();
            }
        }

        [Fact]
        public async Task LK_05_MasterWrite_ProfileId0_TriggersNoLoad()
        {
            int testPort = TestPortBase + 5;
            var modbus = new ModbusSlaveService(channelCount: 2);
            await modbus.StartAsync(testPort);

            MainWindow? mainWin = await WpfTestHelper.InvokeAsync(() =>
            {
                var win = new MainWindow();
                win.InitialGraphData(false);
                win.chNo = 2;
                DeviceCOM.channelDatas[0].graphDatas[0].freq = 7777;
                return win;
            });

            _8F.Auth.AppServices.ProfileLoadService = new ProfileLoadService(
                () => mainWin,
                () => new InspectionLogRepository(TestEnvironment.ConnectionString),
                () => modbus);

            try
            {
                using (var client = new TcpClient())
                {
                    await client.ConnectAsync("127.0.0.1", testPort);
                    var stream = client.GetStream();

                    byte[] req = new byte[]
                    {
                        0x00, 0x01, 0x00, 0x00, 0x00, 0x06, 0x01, 0x06,
                        0x00, 0x01, // Channel 1
                        0x00, 0x00  // Profile ID 0
                    };
                    await stream.WriteAsync(req, 0, req.Length);

                    byte[] resp = new byte[12];
                    await stream.ReadAsync(resp, 0, 12);
                    Assert.Equal(0x06, resp[7]);
                }

                await Task.Delay(300);

                await WpfTestHelper.InvokeAsync(() =>
                {
                    // Channel 1 is unchanged
                    Assert.Equal(7777, DeviceCOM.channelDatas[0].graphDatas[0].freq);
                });
            }
            finally
            {
                await modbus.StopAsync();
            }
        }

        [Fact]
        public async Task LK_06_MasterWrite_UnknownProfileId_RestoresRegisterToLastGoodId()
        {
            await TestEnvironment.ResetTestDatabaseAsync();
            var (idA, idB) = await TestEnvironment.SeedKnownProfilesAsync();

            int testPort = TestPortBase + 6;
            var modbus = new ModbusSlaveService(channelCount: 2);
            await modbus.StartAsync(testPort);

            MainWindow? mainWin = await WpfTestHelper.InvokeAsync(() =>
            {
                var win = new MainWindow();
                win.InitialGraphData(false);
                win.chNo = 2;
                win.modbusSlaveService = modbus;
                return win;
            });

            var service = new ProfileLoadService(
                () => mainWin,
                () => new InspectionLogRepository(TestEnvironment.ConnectionString),
                () => modbus);
            _8F.Auth.AppServices.ProfileLoadService = service;

            try
            {
                // First load known Profile idA into Channel 1
                await service.LoadFromDbAsync(idA, 1, ProfileLoadSource.MenuOpenDb, isInteractive: false);
                Assert.Equal(idA, modbus.ReadRegister(1));

                // Now Modbus Master writes unknown Profile ID 9999 to Register 1
                using (var client = new TcpClient())
                {
                    await client.ConnectAsync("127.0.0.1", testPort);
                    var stream = client.GetStream();

                    byte[] req = new byte[]
                    {
                        0x00, 0x01, 0x00, 0x00, 0x00, 0x06, 0x01, 0x06,
                        0x00, 0x01,
                        (byte)(9999 >> 8), (byte)(9999 & 0xFF)
                    };
                    await stream.WriteAsync(req, 0, req.Length);

                    byte[] resp = new byte[12];
                    await stream.ReadAsync(resp, 0, 12);
                    Assert.Equal(0x06, resp[7]);
                }

                // Wait for background load failure & register rollback
                await Task.Delay(500);

                // Assert register was RESTORED back to last good ID (idA)
                ushort rolledBack = modbus.ReadRegister(1);
                Assert.Equal(idA, rolledBack);
            }
            finally
            {
                await modbus.StopAsync();
            }
        }

        [Fact]
        public async Task LK_07_ModbusTestPanel_WriteReg_LoadsCleanlyWithoutDeadlock()
        {
            await TestEnvironment.ResetTestDatabaseAsync();
            var (idA, idB) = await TestEnvironment.SeedKnownProfilesAsync();

            int testPort = TestPortBase + 7;
            var modbus = new ModbusSlaveService(channelCount: 2);
            await modbus.StartAsync(testPort);

            MainWindow? mainWin = await WpfTestHelper.InvokeAsync(() =>
            {
                var win = new MainWindow();
                win.InitialGraphData(false);
                win.chNo = 2;
                win.modbusSlaveService = modbus;
                return win;
            });

            _8F.Auth.AppServices.ProfileLoadService = new ProfileLoadService(
                () => mainWin,
                () => new InspectionLogRepository(TestEnvironment.ConnectionString),
                () => modbus);

            try
            {
                // Simulate ModbusTestPanel Write Ch 1
                var result = await _8F.Auth.AppServices.ProfileLoadService.LoadFromDbAsync(
                    idA,
                    1,
                    ProfileLoadSource.ModbusPanel,
                    isInteractive: false);

                Assert.True(result.Success);
                Assert.Equal(idA, modbus.ReadRegister(1));
            }
            finally
            {
                await modbus.StopAsync();
            }
        }

        [Fact]
        public async Task LK_08_NewProfile_ResetsHoldingRegistersToZero()
        {
            int testPort = TestPortBase + 8;
            var modbus = new ModbusSlaveService(channelCount: 2);
            await modbus.StartAsync(testPort);

            MainWindow? mainWin = await WpfTestHelper.InvokeAsync(() =>
            {
                var win = new MainWindow();
                win.InitialGraphData(false);
                win.chNo = 2;
                win.modbusSlaveService = modbus;

                // Simulate loaded profile
                win._loadedChannels[0].ProfileId = 12;
                win._loadedChannels[0].ProfileName = "Prof12";
                modbus.WriteRegister(1, 12);
                win._loadedChannels[1].ProfileId = 34;
                win._loadedChannels[1].ProfileName = "Prof34";
                modbus.WriteRegister(2, 34);

                // Act: ClearOpenProfileState (triggered on File > New or Profile Delete)
                win.ClearOpenProfileState();
                return win;
            });

            try
            {
                // Assert holding registers are reset to 0
                Assert.Equal(0, modbus.ReadRegister(1));
                Assert.Equal(0, modbus.ReadRegister(2));
            }
            finally
            {
                await modbus.StopAsync();
            }
        }
    }
}
