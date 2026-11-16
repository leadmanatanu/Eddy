using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading.Tasks;
using Xunit;
using _8F.Services.Implementations;

namespace _8F.UiTests
{
    public class ModbusSlaveTests
    {
        private const int TestPortBase = 15500;

        [Fact]
        public async Task MB_01_Start_IsNonBlockingAndStartsQuickly()
        {
            int testPort = TestPortBase + 1;
            var service = new ModbusSlaveService();

            var sw = Stopwatch.StartNew();
            await service.StartAsync(testPort, heartbeatIntervalMs: 500);
            sw.Stop();

            try
            {
                Assert.True(service.IsRunning);
                Assert.True(sw.ElapsedMilliseconds < 1000, $"StartAsync took {sw.ElapsedMilliseconds}ms, should be non-blocking (<1000ms)");
            }
            finally
            {
                await service.StopAsync();
            }
        }

        [Fact]
        public async Task MB_02_Stop_CleanlyStopsListenerAndDisconnectsActiveClients()
        {
            int testPort = TestPortBase + 2;
            var service = new ModbusSlaveService();
            await service.StartAsync(testPort, heartbeatIntervalMs: 500);

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", testPort);
                await Task.Delay(100);

                Assert.Equal(1, service.ActiveClientCount);

                // Stop server
                await service.StopAsync();
                Assert.False(service.IsRunning);

                // Attempt to read from client - should receive EOF (0 bytes) indicating disconnection
                var stream = client.GetStream();
                byte[] buffer = new byte[10];
                int read = 0;
                try
                {
                    read = await stream.ReadAsync(buffer, 0, buffer.Length);
                }
                catch { }

                Assert.Equal(0, read);
            }
            finally
            {
                await service.StopAsync();
            }
        }

        [Fact]
        public async Task MB_03_StartStopStart_SequenceSucceedsWithoutAddressAlreadyInUse()
        {
            int testPort = TestPortBase + 3;
            var service = new ModbusSlaveService();

            // First cycle
            await service.StartAsync(testPort, heartbeatIntervalMs: 500);
            Assert.True(service.IsRunning);
            await service.StopAsync();
            Assert.False(service.IsRunning);

            // Immediate restart on same port
            await service.StartAsync(testPort, heartbeatIntervalMs: 500);
            Assert.True(service.IsRunning);
            await service.StopAsync();
            Assert.False(service.IsRunning);
        }

        [Fact]
        public async Task MB_04_FunctionCodes_FC03_FC06_FC16_Succeed_UnsupportedReturnsException01()
        {
            int testPort = TestPortBase + 4;
            var service = new ModbusSlaveService();
            await service.StartAsync(testPort, heartbeatIntervalMs: 500);

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", testPort);
                var stream = client.GetStream();

                // 1. FC 06: Write Single Register (Address 0, Value 1234)
                byte[] fc06Req = new byte[]
                {
                    0x00, 0x01, // Transaction ID
                    0x00, 0x00, // Protocol ID (0 = Modbus TCP)
                    0x00, 0x06, // Length (6 bytes follow)
                    0x01,       // Unit ID
                    0x06,       // FC 06 (Write Single Register)
                    0x00, 0x00, // Address 0
                    0x04, 0xD2  // Value = 1234
                };
                await stream.WriteAsync(fc06Req, 0, fc06Req.Length);
                byte[] fc06Resp = new byte[12];
                await stream.ReadAsync(fc06Resp, 0, 12);
                Assert.Equal(0x06, fc06Resp[7]); // Echoed FC 06
                Assert.Equal(1234, service.ReadRegister(0));

                // 2. FC 03: Read Holding Register (Address 0, Quantity 1)
                byte[] fc03Req = new byte[]
                {
                    0x00, 0x02,
                    0x00, 0x00,
                    0x00, 0x06,
                    0x01,
                    0x03,       // FC 03 (Read Holding Registers)
                    0x00, 0x00, // Start address 0
                    0x00, 0x01  // Count = 1
                };
                await stream.WriteAsync(fc03Req, 0, fc03Req.Length);
                byte[] fc03Resp = new byte[11];
                await stream.ReadAsync(fc03Resp, 0, 11);
                Assert.Equal(0x03, fc03Resp[7]); // Echoed FC 03
                Assert.Equal(2, fc03Resp[8]);    // Byte count = 2
                ushort readVal = (ushort)((fc03Resp[9] << 8) | fc03Resp[10]);
                Assert.Equal(1234, readVal);

                // 3. FC 05: Unsupported function code (Force Single Coil)
                byte[] fc05Req = new byte[]
                {
                    0x00, 0x03,
                    0x00, 0x00,
                    0x00, 0x06,
                    0x01,
                    0x05,       // FC 05 (Unsupported)
                    0x00, 0x01,
                    0xFF, 0x00
                };
                await stream.WriteAsync(fc05Req, 0, fc05Req.Length);
                byte[] fc05Resp = new byte[9];
                await stream.ReadAsync(fc05Resp, 0, 9);
                Assert.Equal(0x85, fc05Resp[7]); // Error FC (0x05 | 0x80)
                Assert.Equal(0x01, fc05Resp[8]); // Exception Code 01 (Illegal Function)
            }
            finally
            {
                await service.StopAsync();
            }
        }

        [Fact]
        public async Task MB_05_MalformedPackets_HandledWithoutCrashingServer()
        {
            int testPort = TestPortBase + 5;
            var service = new ModbusSlaveService();
            await service.StartAsync(testPort, heartbeatIntervalMs: 500);

            try
            {
                // Send garbage bytes
                using (var client = new TcpClient())
                {
                    await client.ConnectAsync("127.0.0.1", testPort);
                    var stream = client.GetStream();
                    byte[] garbage = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02 };
                    await stream.WriteAsync(garbage, 0, garbage.Length);
                    await Task.Delay(100);
                }

                // Verify server is still alive and accepting normal connections
                using (var client2 = new TcpClient())
                {
                    await client2.ConnectAsync("127.0.0.1", testPort);
                    Assert.True(client2.Connected);
                }
            }
            finally
            {
                await service.StopAsync();
            }
        }

        [Fact]
        public async Task MB_06_InactiveClient_DisconnectsAfterReadTimeout()
        {
            int testPort = TestPortBase + 6;
            var service = new ModbusSlaveService();
            await service.StartAsync(testPort, heartbeatIntervalMs: 500);

            try
            {
                var client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", testPort);

                // Allow server accept loop to register client
                for (int i = 0; i < 20 && service.ActiveClientCount == 0; i++)
                {
                    await Task.Delay(50);
                }
                Assert.Equal(1, service.ActiveClientCount);

                // Send partial header (3 bytes instead of 7) then go silent
                var stream = client.GetStream();
                await stream.WriteAsync(new byte[] { 0x00, 0x01, 0x00 }, 0, 3);

                // Wait for read timeout disconnect (timeout is 5 seconds)
                for (int i = 0; i < 70 && service.ActiveClientCount > 0; i++)
                {
                    await Task.Delay(100);
                }

                // Assert client was disconnected and ActiveClientCount is 0
                Assert.Equal(0, service.ActiveClientCount);
            }
            finally
            {
                await service.StopAsync();
            }
        }

        [Fact]
        public async Task MB_07_ConcurrentClients_HandledThreadSafely()
        {
            int testPort = TestPortBase + 7;
            var service = new ModbusSlaveService();
            await service.StartAsync(testPort, heartbeatIntervalMs: 500);

            try
            {
                var tasks = new List<Task>();
                for (int i = 0; i < 5; i++)
                {
                    ushort clientVal = (ushort)(500 + i);
                    tasks.Add(Task.Run(async () =>
                    {
                        using var client = new TcpClient();
                        await client.ConnectAsync("127.0.0.1", testPort);
                        var stream = client.GetStream();

                        byte[] req = new byte[]
                        {
                            0x00, 0x01, 0x00, 0x00, 0x00, 0x06, 0x01, 0x06, 0x00, 0x00,
                            (byte)(clientVal >> 8), (byte)(clientVal & 0xFF)
                        };
                        await stream.WriteAsync(req, 0, req.Length);
                        byte[] resp = new byte[12];
                        await stream.ReadAsync(resp, 0, 12);
                        Assert.Equal(0x06, resp[7]);
                    }));
                }

                await Task.WhenAll(tasks);
                Assert.True(service.TotalRequestsCount >= 5);
            }
            finally
            {
                await service.StopAsync();
            }
        }
    }
}
