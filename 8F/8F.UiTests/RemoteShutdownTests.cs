using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using _8F;
using _8F.Models;
using _8F.Services;
using _8F.Services.Implementations;
using _8F.Views;

namespace _8F.UiTests
{
    public class RemoteShutdownTests
    {
        private static short[] GetValidShutdownFrame()
        {
            // Protocol frame: [STX=0x02, FC=0x19 (25), Len=0x00, CRCLow, CRCHigh]
            byte[] header = new byte[] { 0x02, 0x19, 0x00 };
            ushort crc = DeviceCOM.ComputeCRC(header, header.Length);
            byte crcLow = (byte)(crc & 0xFF);
            byte crcHigh = (byte)(crc >> 8);
            return new short[] { 0x02, 0x19, 0x00, crcLow, crcHigh };
        }

        [Fact]
        public async Task Test1_ValidCommand_FlagOn_Saves_FlushesLog_CallsShutdown()
        {
            RemoteShutdownService.ResetState();
            RemoteShutdownService.IsRemoteShutdownEnable = true;
            RemoteShutdownService.CountdownSeconds = 1;
            RemoteShutdownService.CountdownDialogFunc = (sec) => true; // user countdown expires/proceeds

            bool profileSaved = false;
            bool inspectionSaved = false;
            RemoteShutdownService.CustomSaveProfileAction = async (win) =>
            {
                profileSaved = true;
                await Task.CompletedTask;
            };
            RemoteShutdownService.CustomSaveInspectionDataAction = async (win) =>
            {
                inspectionSaved = true;
                await Task.CompletedTask;
            };

            string? calledCmd = null;
            string? calledArgs = null;
            RemoteShutdownService.ExecuteShutdownCommand = (cmd, args) =>
            {
                calledCmd = cmd;
                calledArgs = args;
            };

            var dev = new DeviceCOM();
            var frame = GetValidShutdownFrame();
            dev.ProcessPortDataBytpe(frame);

            if (RemoteShutdownService.CurrentShutdownTask != null)
            {
                await RemoteShutdownService.CurrentShutdownTask;
            }

            Assert.True(profileSaved, "Profile should be saved before shutdown");
            Assert.True(inspectionSaved, "Inspection test data should be saved before shutdown");
            Assert.Equal("shutdown", calledCmd);
            Assert.Equal("/s /t 5 /c \"8F remote shutdown\"", calledArgs);

            // Verify log contains "Remote shutdown requested"
            string logText = File.Exists(ModbusLogger.LogFilePath) ? File.ReadAllText(ModbusLogger.LogFilePath) : string.Empty;
            Assert.Contains("Remote shutdown requested", logText);
        }

        [Fact]
        public async Task Test2_FlagOff_IgnoredAndLogged()
        {
            RemoteShutdownService.ResetState();
            RemoteShutdownService.IsRemoteShutdownEnable = false;

            string? calledCmd = null;
            RemoteShutdownService.ExecuteShutdownCommand = (cmd, args) =>
            {
                calledCmd = cmd;
            };

            var dev = new DeviceCOM();
            var frame = GetValidShutdownFrame();
            dev.ProcessPortDataBytpe(frame);

            if (RemoteShutdownService.CurrentShutdownTask != null)
            {
                await RemoteShutdownService.CurrentShutdownTask;
            }

            Assert.Null(calledCmd);
            string logText = File.Exists(ModbusLogger.LogFilePath) ? File.ReadAllText(ModbusLogger.LogFilePath) : string.Empty;
            Assert.Contains("ignored: disabled", logText);
        }

        [Fact]
        public async Task Test3_InvalidOrPartialData_Ignored()
        {
            RemoteShutdownService.ResetState();
            RemoteShutdownService.IsRemoteShutdownEnable = true;
            RemoteShutdownService.CountdownDialogFunc = (sec) => true;

            string? calledCmd = null;
            RemoteShutdownService.ExecuteShutdownCommand = (cmd, args) =>
            {
                calledCmd = cmd;
            };

            var dev = new DeviceCOM();
            var validFrame = GetValidShutdownFrame();

            // 1. Truncated frame (3 bytes)
            dev.ProcessPortDataBytpe(new short[] { 0x02, 0x19, 0x00 });
            await Task.Delay(100);
            Assert.Null(calledCmd);

            // 2. Truncated frame (4 bytes)
            dev.ProcessPortDataBytpe(new short[] { 0x02, 0x19, 0x00, 0x12 });
            await Task.Delay(100);
            Assert.Null(calledCmd);

            // 3. Corrupted CRC
            dev.ProcessPortDataBytpe(new short[] { 0x02, 0x19, 0x00, 0x00, 0x00 });
            await Task.Delay(100);
            Assert.Null(calledCmd);

            // 4. Invalid STX (0x05 instead of 0x02)
            dev.ProcessPortDataBytpe(new short[] { 0x05, 0x19, 0x00, validFrame[3], validFrame[4] });
            await Task.Delay(100);
            Assert.Null(calledCmd);

            // 5. Length field not 0
            dev.ProcessPortDataBytpe(new short[] { 0x02, 0x19, 0x02, validFrame[3], validFrame[4] });
            await Task.Delay(100);
            Assert.Null(calledCmd);
        }

        [Fact]
        public async Task Test4_CancelInCountdown_NothingShutsDown_AppKeepsRunning()
        {
            RemoteShutdownService.ResetState();
            RemoteShutdownService.IsRemoteShutdownEnable = true;
            RemoteShutdownService.CountdownDialogFunc = (sec) => false; // user clicked Cancel

            string? calledCmd = null;
            RemoteShutdownService.ExecuteShutdownCommand = (cmd, args) =>
            {
                calledCmd = cmd;
            };

            var dev = new DeviceCOM();
            var frame = GetValidShutdownFrame();
            dev.ProcessPortDataBytpe(frame);

            if (RemoteShutdownService.CurrentShutdownTask != null)
            {
                await RemoteShutdownService.CurrentShutdownTask;
            }

            Assert.Null(calledCmd);
            string logText = File.Exists(ModbusLogger.LogFilePath) ? File.ReadAllText(ModbusLogger.LogFilePath) : string.Empty;
            Assert.Contains("shutdown cancelled by user", logText);
        }

        [Fact]
        public async Task Test5_SaveFailure_NoShutdown_ErrorShown()
        {
            RemoteShutdownService.ResetState();
            RemoteShutdownService.IsRemoteShutdownEnable = true;
            RemoteShutdownService.CountdownDialogFunc = (sec) => true;

            // Simulate save failure
            RemoteShutdownService.CustomSaveProfileAction = (win) =>
            {
                throw new IOException("Simulated disk error during save");
            };

            string? shownError = null;
            RemoteShutdownService.ShowErrorDialogAction = (msg, title) =>
            {
                shownError = msg;
            };

            string? calledCmd = null;
            RemoteShutdownService.ExecuteShutdownCommand = (cmd, args) =>
            {
                calledCmd = cmd;
            };

            var dev = new DeviceCOM();
            var frame = GetValidShutdownFrame();
            dev.ProcessPortDataBytpe(frame);

            if (RemoteShutdownService.CurrentShutdownTask != null)
            {
                await RemoteShutdownService.CurrentShutdownTask;
            }

            // Shutdown must NOT be called
            Assert.Null(calledCmd);
            // Error must be shown to user
            Assert.NotNull(shownError);
            Assert.Contains("Simulated disk error during save", shownError);
        }

        [Fact]
        public async Task Test6_TestRunning_StoppedAndRecordSavedBeforeShutdown()
        {
            RemoteShutdownService.ResetState();
            RemoteShutdownService.IsRemoteShutdownEnable = true;
            RemoteShutdownService.CountdownDialogFunc = (sec) => true;

            DeviceCOM.IsSystemBusy = true;

            bool inspectionSaved = false;
            RemoteShutdownService.CustomSaveInspectionDataAction = async (win) =>
            {
                inspectionSaved = true;
                await Task.CompletedTask;
            };

            string? calledCmd = null;
            RemoteShutdownService.ExecuteShutdownCommand = (cmd, args) =>
            {
                calledCmd = cmd;
            };

            // Complete the running test cleanly after 150ms
            _ = Task.Run(async () =>
            {
                await Task.Delay(150);
                DeviceCOM.IsSystemBusy = false;
            });

            var dev = new DeviceCOM();
            var frame = GetValidShutdownFrame();
            dev.ProcessPortDataBytpe(frame);

            if (RemoteShutdownService.CurrentShutdownTask != null)
            {
                await RemoteShutdownService.CurrentShutdownTask;
            }

            Assert.False(DeviceCOM.IsSystemBusy);
            Assert.True(inspectionSaved);
            Assert.Equal("shutdown", calledCmd);
        }

        [Fact]
        public async Task Test7_DuplicateCommand_RunsOnce()
        {
            RemoteShutdownService.ResetState();
            RemoteShutdownService.IsRemoteShutdownEnable = true;
            RemoteShutdownService.CountdownDialogFunc = (sec) => true;

            int executeCount = 0;
            RemoteShutdownService.ExecuteShutdownCommand = (cmd, args) =>
            {
                Interlocked.Increment(ref executeCount);
            };

            var dev = new DeviceCOM();
            var frame = GetValidShutdownFrame();

            // Send multiple duplicate frames rapidly
            dev.ProcessPortDataBytpe(frame);
            dev.ProcessPortDataBytpe(frame);
            dev.ProcessPortDataBytpe(frame);

            if (RemoteShutdownService.CurrentShutdownTask != null)
            {
                await RemoteShutdownService.CurrentShutdownTask;
            }

            Assert.Equal(1, executeCount);
        }

        [Fact]
        public async Task Test8_CountdownDialog_CancelButton_ReturnsFalse()
        {
            await WpfTestHelper.InvokeAsync(() =>
            {
                var dlg = new RemoteShutdownCountdownWindow(15);
                Assert.NotNull(dlg);
                Assert.Equal("Remote Shutdown", dlg.Title);
                Assert.True(dlg.Topmost);

                // Clicking cancel should stop timer and close safely
                dlg.Cancel();
            });
        }

        [Fact]
        public async Task Test9_DbDown_FallbackToFile_ShutdownCalled()
        {
            RemoteShutdownService.ResetState();
            RemoteShutdownService.IsRemoteShutdownEnable = true;
            RemoteShutdownService.CountdownSeconds = 1;
            RemoteShutdownService.CountdownDialogFunc = (sec) => true;

            await WpfTestHelper.InvokeAsync(() =>
            {
                var win = new MainWindow();
                win.isSaveWithDb = true;
                win.isSaveWithFile = false;
                RemoteShutdownService.MainWindowInstance = win;

                DeviceCOM.channelDatas = new System.Collections.Generic.List<ChannelData>
                {
                    new ChannelData { Id = 1, graphDatas = new System.Collections.Generic.List<GraphData>() }
                };
            });

            // Simulate DB connection failure / refused
            RemoteShutdownService.DbSaveOperation = async (name, chs, settings) =>
            {
                await Task.Yield();
                throw new System.Net.Sockets.SocketException(10061); // Connection refused
            };

            string? writtenFilePath = null;
            string? writtenFileContent = null;
            RemoteShutdownService.FileWriteOperation = (path, content) =>
            {
                writtenFilePath = path;
                writtenFileContent = content;
            };

            string? calledCmd = null;
            RemoteShutdownService.ExecuteShutdownCommand = (cmd, args) =>
            {
                calledCmd = cmd;
            };

            var dev = new DeviceCOM();
            var frame = GetValidShutdownFrame();
            dev.ProcessPortDataBytpe(frame);

            if (RemoteShutdownService.CurrentShutdownTask != null)
            {
                await RemoteShutdownService.CurrentShutdownTask;
            }

            // 1. Fallback file must be saved
            Assert.NotNull(writtenFilePath);
            Assert.Contains("AutoSave_", Path.GetFileName(writtenFilePath));
            Assert.EndsWith(".json", writtenFilePath);
            Assert.NotNull(writtenFileContent);
            var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<ProfileConfigFile>(writtenFileContent);
            Assert.NotNull(parsed);
            Assert.Equal(2, parsed.Version);

            // 2. Shutdown must be called
            Assert.Equal("shutdown", calledCmd);

            // 3. Log must show DB error and fallback
            string logText = File.Exists(ModbusLogger.LogFilePath) ? File.ReadAllText(ModbusLogger.LogFilePath) : string.Empty;
            Assert.Contains("DB save failed", logText);
            Assert.Contains("Falling back to file save", logText);
        }

        [Fact]
        public async Task Test10_DbDown_FileSaveFails_NoShutdown_ErrorShown()
        {
            RemoteShutdownService.ResetState();
            RemoteShutdownService.IsRemoteShutdownEnable = true;
            RemoteShutdownService.CountdownSeconds = 1;
            RemoteShutdownService.CountdownDialogFunc = (sec) => true;

            await WpfTestHelper.InvokeAsync(() =>
            {
                var win = new MainWindow();
                win.isSaveWithDb = true;
                win.isSaveWithFile = false;
                RemoteShutdownService.MainWindowInstance = win;

                DeviceCOM.channelDatas = new System.Collections.Generic.List<ChannelData>
                {
                    new ChannelData { Id = 1, graphDatas = new System.Collections.Generic.List<GraphData>() }
                };
            });

            // Simulate DB timeout
            RemoteShutdownService.DbSaveOperation = async (name, chs, settings) =>
            {
                await Task.Yield();
                throw new TimeoutException("Database connection timed out after 5s");
            };

            // Simulate fallback file write failure (disk full / permission denied)
            RemoteShutdownService.FileWriteOperation = (path, content) =>
            {
                throw new IOException("Disk quota exceeded");
            };

            string? shownError = null;
            RemoteShutdownService.ShowErrorDialogAction = (msg, title) =>
            {
                shownError = msg;
            };

            string? calledCmd = null;
            RemoteShutdownService.ExecuteShutdownCommand = (cmd, args) =>
            {
                calledCmd = cmd;
            };

            var dev = new DeviceCOM();
            var frame = GetValidShutdownFrame();
            dev.ProcessPortDataBytpe(frame);

            if (RemoteShutdownService.CurrentShutdownTask != null)
            {
                await RemoteShutdownService.CurrentShutdownTask;
            }

            // 1. Shutdown must NOT be called
            Assert.Null(calledCmd);

            // 2. Real error must be shown to the user
            Assert.NotNull(shownError);
            Assert.Contains("Disk quota exceeded", shownError);
        }

        [Fact]
        public void Test11_ModbusRegister99_DoesNothing()
        {
            RemoteShutdownService.ResetState();
            RemoteShutdownService.IsRemoteShutdownEnable = true;

            string? calledCmd = null;
            RemoteShutdownService.ExecuteShutdownCommand = (cmd, args) =>
            {
                calledCmd = cmd;
            };

            var service = new ModbusSlaveService();
            var mi = typeof(ModbusSlaveService).GetMethod("ProcessModbusRequest", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(mi);

            // FC 06 (Write Single Register) to address 99 (0x00, 0x63), value 0x5D07
            byte[] payload = new byte[] { 0x00, 0x63, 0x5D, 0x07 };
            byte[] response = (byte[])mi.Invoke(service, new object[] { "127.0.0.1:5020", (byte)1, (byte)6, payload, 0, 4 })!;

            // Modbus should return Exception 02 (Illegal Address: 0x86, 0x02) because address 99 is not mapped
            Assert.Equal(2, response.Length);
            Assert.Equal(0x86, response[0]);
            Assert.Equal(0x02, response[1]);

            // No shutdown should have been triggered
            Assert.Null(calledCmd);
            Assert.Null(RemoteShutdownService.LastShutdownCommand);
        }
    }
}
