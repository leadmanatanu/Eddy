using NModbus;
using NModbus.Data;
using NModbus.Device;
using NModbus.Device.MessageHandlers;
using NModbus.Logging;
using NModbus.Message;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using _8F.Services.Interfaces;

namespace _8F.Services.Implementations
{
    public class ModbusSlaveService : IModbusSlaveService
    {
        public const int MinChannelCount = 1;
        public const int MaxChannelCount = 4;
        public const int DefaultChannelCount = 1;

        public const ushort HeartbeatAddress = 0;
        public const ushort FirstChannelAddress = 1;

        public int ChannelCount { get; private set; } = DefaultChannelCount;
        public int TotalRegisterCount => ChannelCount + 1;
        public bool IsChannelCountFallback { get; private set; } = false;
        public string? ChannelCountConfigError { get; private set; } = null;

        public static ushort ChannelIndexToAddress(int channelIndex) => (ushort)(FirstChannelAddress + channelIndex);
        public static int AddressToChannelIndex(ushort address) => address - FirstChannelAddress;
        public bool IsChannelAddress(ushort address) => address >= FirstChannelAddress && address < FirstChannelAddress + ChannelCount;

        private TcpListener? _tcpListener;
        private IModbusSlaveNetwork? _slaveNetwork;
        private IModbusSlave? _slave;
        private Timer? _heartbeatTimer;
        private ushort _heartbeatValue = 0;
        private readonly object _lock = new();
        private volatile bool _suppressNotification = false;

        private Channel<ChannelProfileWrite>? _profileChannel;
        private CancellationTokenSource? _cts;
        private Task? _consumerTask;

        public bool IsRunning { get; private set; }
        public int Port { get; private set; } = 502;
        public int HeartbeatIntervalMs { get; private set; } = 1000;

        public event EventHandler<ushort>? RegisterValueChanged;
        public event EventHandler<ushort>? HeartbeatValueChanged;
        public event Func<int, ushort, Task>? ChannelProfileIdReceivedAsync;
        public event Func<IReadOnlyDictionary<int, ushort>, Task>? ChannelProfilesReceivedAsync;

        public readonly record struct ChannelProfileWrite(int ChannelIndex, ushort ProfileId);

        /// <summary>
        /// Reads and validates the "Channel" setting from App.config.
        /// Valid values: 1 to 4. Any missing, empty, non-numeric, or out-of-range value
        /// logs an ERROR and falls back to DefaultChannelCount (1).
        /// Changing App.config requires an application restart (read once at startup).
        /// </summary>
        public static (int ValidatedCount, string? ErrorMessage) ResolveChannelCount()
        {
            string? raw = System.Configuration.ConfigurationManager.AppSettings["Channel"];
            if (string.IsNullOrWhiteSpace(raw))
            {
                return (DefaultChannelCount, "App.config 'Channel' key is missing or empty. Safe default of 1 channel applied.");
            }

            if (!int.TryParse(raw.Trim(), out int parsed))
            {
                return (DefaultChannelCount, $"App.config 'Channel' value '{raw}' is not a valid integer. Safe default of 1 channel applied.");
            }

            if (parsed < MinChannelCount || parsed > MaxChannelCount)
            {
                return (DefaultChannelCount, $"App.config 'Channel' value '{raw}' is outside allowed range ({MinChannelCount}..{MaxChannelCount}). Safe default of 1 channel applied.");
            }

            return (parsed, null);
        }

        public ushort ReadRegister(ushort address = HeartbeatAddress)
        {
            lock (_lock)
            {
                if (_slave?.DataStore?.HoldingRegisters != null)
                {
                    var points = _slave.DataStore.HoldingRegisters.ReadPoints(address, 1);
                    if (points != null && points.Length > 0)
                    {
                        return points[0];
                    }
                }
                return 0;
            }
        }

        public void WriteRegister(ushort value)
        {
            WriteRegister(HeartbeatAddress, value);
        }

        public void WriteRegister(ushort address, ushort value)
        {
            lock (_lock)
            {
                if (_slave?.DataStore?.HoldingRegisters != null)
                {
                    _slave.DataStore.HoldingRegisters.WritePoints(address, new ushort[] { value });
                }
            }
        }

        public void WriteRegisterSilent(ushort address, ushort value)
        {
            lock (_lock)
            {
                if (_slave?.DataStore?.HoldingRegisters != null)
                {
                    _suppressNotification = true;
                    try
                    {
                        _slave.DataStore.HoldingRegisters.WritePoints(address, new ushort[] { value });
                    }
                    finally
                    {
                        _suppressNotification = false;
                    }
                }
            }
        }

        public void Start(int port = 502, int heartbeatIntervalMs = 1000)
        {
            if (IsRunning) return;

            try
            {
                Port = port;
                HeartbeatIntervalMs = heartbeatIntervalMs > 0 ? heartbeatIntervalMs : 1000;

                string rawConfig = System.Configuration.ConfigurationManager.AppSettings["Channel"] ?? "(null)";
                var (resolvedCount, configError) = ResolveChannelCount();
                ChannelCount = resolvedCount;
                IsChannelCountFallback = !string.IsNullOrEmpty(configError);
                ChannelCountConfigError = configError;

                if (IsChannelCountFallback)
                {
                    ModbusLogger.Error($"[Modbus TCP Slave] Configuration ERROR for 'Channel' setting (raw='{rawConfig}'): {configError}");
                }
                else
                {
                    ModbusLogger.Info($"[Modbus TCP Slave] Configuration 'Channel' read successfully: raw='{rawConfig}', active channels={ChannelCount}.");
                }

                _tcpListener = new TcpListener(IPAddress.Any, Port);
                _tcpListener.Start();

                // Strictly configure only FC 03 (Read Holding), FC 06 (Write Single), and FC 16 (Write Multiple).
                // All other function codes will be rejected with Modbus Exception 01 (Illegal Function).
                var services = new IModbusFunctionService[]
                {
                    new ReadHoldingRegistersService(),
                    new WriteSingleRegisterService(),
                    new WriteMultipleRegistersService(),
                    new RejectingReadCoilsService(),
                    new RejectingReadInputsService(),
                    new RejectingReadInputRegistersService(),
                    new RejectingWriteSingleCoilService(),
                    new RejectingWriteMultipleCoilsService()
                };
                var factory = new ModbusFactory(services, includeBuiltIn: false, NullModbusLogger.Instance);
                _slaveNetwork = factory.CreateSlaveNetwork(_tcpListener);

                byte unitId = 1;
                var dataStore = new SlaveDataStore();
                _slave = factory.CreateSlave(unitId, dataStore);

                // Initialize exactly TotalRegisterCount holding registers:
                // Address 0 = Heartbeat (0)
                // Addresses 1..ChannelCount = Channels 1..ChannelCount Profile IDs (0)
                _slave.DataStore.HoldingRegisters.WritePoints(0, new ushort[TotalRegisterCount]);

                if (_slave.DataStore.HoldingRegisters is PointSource<ushort> ps)
                {
                    // Enforce address bounds: exactly TotalRegisterCount registers (0..ChannelCount).
                    // Any request outside 0..ChannelCount returns Modbus Exception 02 (Illegal Data Address).
                    ps.BeforeRead += (s, e) =>
                    {
                        if (e.StartAddress + e.NumberOfPoints > TotalRegisterCount)
                        {
                            ModbusLogger.Warn($"[Modbus TCP Slave] Rejected read out of bounds: StartAddress={e.StartAddress}, Count={e.NumberOfPoints}, TotalRegisters={TotalRegisterCount}.");
                            throw new InvalidModbusRequestException(2);
                        }
                    };

                    ps.BeforeWrite += (s, e) =>
                    {
                        if (e.StartAddress + e.Points.Length > TotalRegisterCount)
                        {
                            ModbusLogger.Warn($"[Modbus TCP Slave] Rejected write out of bounds: StartAddress={e.StartAddress}, Count={e.Points.Length}, TotalRegisters={TotalRegisterCount}.");
                            throw new InvalidModbusRequestException(2);
                        }
                    };

                    // Setup background channel consumer for profile loading
                    _profileChannel = Channel.CreateUnbounded<ChannelProfileWrite>(new UnboundedChannelOptions
                    {
                        SingleReader = true,
                        SingleWriter = false
                    });
                    _cts = new CancellationTokenSource();
                    _consumerTask = Task.Run(() => ProcessProfileWritesAsync(_cts.Token));

                    // Hook AfterWrite (values are already committed to store; non-blocking handoff)
                    ps.AfterWrite += (s, e) =>
                    {
                        if (_suppressNotification) return;

                        ushort start = e.StartAddress;
                        ushort count = e.NumberOfPoints;
                        ushort end = (ushort)(start + count - 1);

                        // Heartbeat write at address 0 (HeartbeatAddress):
                        // Accepted in store, overwritten by next tick, never triggers a profile load
                        if (start <= HeartbeatAddress && end >= HeartbeatAddress)
                        {
                            ushort hbVal = ReadRegister(HeartbeatAddress);
                            _heartbeatValue = hbVal;
                            try
                            {
                                HeartbeatValueChanged?.Invoke(this, hbVal);
                            }
                            catch (Exception ex)
                            {
                                ModbusLogger.Error("[Modbus TCP Slave] Error in HeartbeatValueChanged handler", ex);
                            }
                        }

                        // Channel Profile ID writes (addresses 1..ChannelCount: FirstChannelAddress .. FirstChannelAddress + ChannelCount - 1)
                        int minChAddr = Math.Max((int)start, (int)FirstChannelAddress);
                        int maxChAddr = Math.Min((int)end, (int)(FirstChannelAddress + ChannelCount - 1));

                        for (int addr = minChAddr; addr <= maxChAddr; addr++)
                        {
                            ushort val = ReadRegister((ushort)addr);
                            int channelIndex = AddressToChannelIndex((ushort)addr);
                            int channelNumber = channelIndex + 1;
                            ModbusLogger.Info($"[Modbus TCP Slave] Address {addr} (Channel {channelNumber} Profile ID) write received: {val}");

                            try
                            {
                                RegisterValueChanged?.Invoke(this, val);
                            }
                            catch { }

                            _profileChannel.Writer.TryWrite(new ChannelProfileWrite(channelIndex, val));
                        }
                    };
                }
                else
                {
                    string fatalError = "FATAL: Modbus data store HoldingRegisters is not PointSource<ushort>. Event hooks cannot attach!";
                    ModbusLogger.Error(fatalError);
                    throw new InvalidOperationException(fatalError);
                }

                _slaveNetwork.AddSlave(_slave);

                Task.Run(async () =>
                {
                    try
                    {
                        await _slaveNetwork.ListenAsync();
                    }
                    catch (Exception ex)
                    {
                        ModbusLogger.Error("[Modbus TCP Slave] Slave listener encountered an exception", ex);
                    }
                });

                // Start heartbeat timer
                _heartbeatTimer?.Dispose();
                _heartbeatTimer = new Timer(OnHeartbeatTick, null, HeartbeatIntervalMs, HeartbeatIntervalMs);

                IsRunning = true;

                var mapDesc = new StringBuilder();
                mapDesc.AppendLine($"[Modbus TCP Slave] Started listening on TCP Port {Port}, Heartbeat interval {HeartbeatIntervalMs}ms. Register map for {ChannelCount} active channel(s) ({TotalRegisterCount} registers):");
                mapDesc.AppendLine($"  Address 0: Heartbeat");
                for (int i = 0; i < ChannelCount; i++)
                {
                    mapDesc.AppendLine($"  Address {FirstChannelAddress + i}: Channel {i + 1} Profile ID");
                }
                ModbusLogger.Info(mapDesc.ToString().TrimEnd());
            }
            catch (Exception ex)
            {
                IsRunning = false;
                Stop();
                ModbusLogger.Error($"[Modbus TCP Slave] Failed to start on port {port}: {ex.Message}", ex);
                throw new InvalidOperationException($"Failed to start Modbus TCP Slave on port {port}: {ex.Message}", ex);
            }
        }

        private async Task ProcessProfileWritesAsync(CancellationToken ct)
        {
            if (_profileChannel == null) return;

            try
            {
                while (await _profileChannel.Reader.WaitToReadAsync(ct))
                {
                    // Drain and coalesce multiple rapid writes per channel:
                    // Key is channelIndex (0..ChannelCount - 1), value is the latest profileId
                    var batch = new Dictionary<int, ushort>();
                    while (_profileChannel.Reader.TryRead(out var item))
                    {
                        if (item.ChannelIndex >= 0 && item.ChannelIndex < ChannelCount)
                        {
                            batch[item.ChannelIndex] = item.ProfileId;
                        }
                    }

                    if (batch.Count == 0 || ct.IsCancellationRequested) continue;

                    // Batch notification to MainWindow allows multi-channel atomic apply & single ImplementChanges call
                    if (ChannelProfilesReceivedAsync != null)
                    {
                        try
                        {
                            await ChannelProfilesReceivedAsync.Invoke(batch);
                        }
                        catch (Exception ex)
                        {
                            ModbusLogger.Error("[Modbus TCP Slave] Error processing profile writes batch", ex);
                        }
                    }
                    else if (ChannelProfileIdReceivedAsync != null)
                    {
                        foreach (var (channelIndex, profileId) in batch)
                        {
                            if (ct.IsCancellationRequested) break;
                            try
                            {
                                await ChannelProfileIdReceivedAsync.Invoke(channelIndex, profileId);
                            }
                            catch (Exception ex)
                            {
                                ModbusLogger.Error($"[Modbus TCP Slave] Error processing profile write for Channel {channelIndex + 1}, Profile {profileId}", ex);
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ModbusLogger.Error("[Modbus TCP Slave] Consumer loop error", ex);
            }
        }

        private void OnHeartbeatTick(object? state)
        {
            if (!IsRunning || _slave?.DataStore?.HoldingRegisters == null) return;

            lock (_lock)
            {
                _heartbeatValue = (ushort)((_heartbeatValue + 1) % 65536);
                _suppressNotification = true;
                try
                {
                    _slave.DataStore.HoldingRegisters.WritePoints(HeartbeatAddress, new ushort[] { _heartbeatValue });
                }
                finally
                {
                    _suppressNotification = false;
                }
            }
        }

        public void Stop()
        {
            try
            {
                _heartbeatTimer?.Dispose();
                _cts?.Cancel();
                _profileChannel?.Writer.TryComplete();
                _slaveNetwork?.Dispose();
                _tcpListener?.Stop();
                ModbusLogger.Info("[Modbus TCP Slave] Stopped.");
            }
            catch (Exception ex)
            {
                ModbusLogger.Error("[Modbus TCP Slave] Error while stopping", ex);
            }
            finally
            {
                _heartbeatTimer = null;
                _cts = null;
                _profileChannel = null;
                _consumerTask = null;
                _slaveNetwork = null;
                _slave = null;
                _tcpListener = null;
                IsRunning = false;
            }
        }

        #region Unsupported Modbus Function Code Rejection (Exc 01)
        private sealed class RejectingReadCoilsService : ReadCoilsService
        {
            protected override IModbusMessage Handle(ReadCoilsInputsRequest request, ISlaveDataStore dataStore)
                => throw new InvalidModbusRequestException(1);
        }

        private sealed class RejectingReadInputsService : ReadInputsService
        {
            protected override IModbusMessage Handle(ReadCoilsInputsRequest request, ISlaveDataStore dataStore)
                => throw new InvalidModbusRequestException(1);
        }

        private sealed class RejectingReadInputRegistersService : ReadInputRegistersService
        {
            protected override IModbusMessage Handle(ReadHoldingInputRegistersRequest request, ISlaveDataStore dataStore)
                => throw new InvalidModbusRequestException(1);
        }

        private sealed class RejectingWriteSingleCoilService : WriteSingleCoilService
        {
            protected override IModbusMessage Handle(WriteSingleCoilRequestResponse request, ISlaveDataStore dataStore)
                => throw new InvalidModbusRequestException(1);
        }

        private sealed class RejectingWriteMultipleCoilsService : WriteMultipleCoilsService
        {
            protected override IModbusMessage Handle(WriteMultipleCoilsRequest request, ISlaveDataStore dataStore)
                => throw new InvalidModbusRequestException(1);
        }
        #endregion
    }
}
