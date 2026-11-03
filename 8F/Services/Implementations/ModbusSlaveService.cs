using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using _8F.Services.Interfaces;

namespace _8F.Services.Implementations
{
    public class ModbusSlaveService : IModbusSlaveService
    {
        #region Constants & Defaults
        public const int MinChannelCount = 1;
        public const int MaxChannelCount = 4;
        public const int DefaultChannelCount = 1;

        public const ushort HeartbeatAddress = 0;
        public const ushort FirstChannelAddress = 1;

        public const int DefaultPort = 5020;
        public const int DefaultHeartbeatIntervalMs = 1000;
        public const string DefaultBindAddress = "0.0.0.0";
        public const string DefaultUnitIdMode = "Any"; // "Any" or "Fixed"
        public const byte DefaultFixedUnitId = 0;
        public const bool DefaultEnableRawFrameLogging = false;
        public const bool DefaultAutoAddFirewallRule = false;

        private const int MaxRawFramesPerSecond = 50;
        private static readonly HashSet<string> LoopbackHosts = new(StringComparer.OrdinalIgnoreCase)
        {
            "127.0.0.1", "localhost", "::1"
        };
        #endregion

        #region Public Properties
        public int ChannelCount { get; private set; } = DefaultChannelCount;
        public int TotalRegisterCount => ChannelCount + 1;
        public bool IsChannelCountFallback { get; private set; } = false;
        public string? ChannelCountConfigError { get; private set; } = null;

        public bool IsRunning { get; private set; }
        public int Port { get; private set; } = DefaultPort;
        public int HeartbeatIntervalMs { get; private set; } = DefaultHeartbeatIntervalMs;
        public string BindAddress { get; private set; } = DefaultBindAddress;
        public string UnitIdMode { get; private set; } = DefaultUnitIdMode;
        public byte FixedUnitId { get; private set; } = DefaultFixedUnitId;
        public bool EnableRawFrameLogging { get; private set; } = DefaultEnableRawFrameLogging;
        public bool AutoAddFirewallRule { get; private set; } = DefaultAutoAddFirewallRule;

        public string UnitIdModeDescription => string.Equals(UnitIdMode, "Fixed", StringComparison.OrdinalIgnoreCase)
            ? $"Fixed (ID: {FixedUnitId})"
            : "Any (0..255)";

        public int ActiveClientCount => _activeClients;
        public DateTime? LastRequestTime => _lastRequestTimeUtc.HasValue ? _lastRequestTimeUtc.Value.ToLocalTime() : null;
        public long TotalRequestsCount => Interlocked.Read(ref _totalRequestsCount);
        public long TotalExceptionsCount => Interlocked.Read(ref _totalExceptionsCount);
        public long UnknownUnitIdRequestsCount => Interlocked.Read(ref _unknownUnitIdRequestsCount);
        public string ListeningEndpointsDescription => _listeningEndpointsDescription;
        public string StatusDescription { get; private set; } = "Stopped";
        #endregion

        #region Channel Mapping Helpers
        public static ushort ChannelIndexToAddress(int channelIndex) => (ushort)(FirstChannelAddress + channelIndex);
        public static int AddressToChannelIndex(ushort address) => address - FirstChannelAddress;
        public bool IsChannelAddress(ushort address) => address >= FirstChannelAddress && address < FirstChannelAddress + ChannelCount;
        #endregion

        #region Private Fields
        private ushort[] _registers = Array.Empty<ushort>();
        private readonly object _lock = new();
        private volatile bool _suppressNotification = false;

        private TcpListener? _tcpListener;
        private CancellationTokenSource? _serverCts;
        private Task? _listenerTask;
        private Timer? _heartbeatTimer;
        private ushort _heartbeatValue = 0;

        private Channel<ChannelProfileWrite>? _profileChannel;
        private CancellationTokenSource? _profileCts;
        private Task? _consumerTask;

        private int _activeClients = 0;
        private long _totalRequestsCount = 0;
        private long _totalExceptionsCount = 0;
        private long _unknownUnitIdRequestsCount = 0;
        private DateTime? _lastRequestTimeUtc = null;
        private string _listeningEndpointsDescription = "None";

        private long _rawFrameLastSec = 0;
        private int _rawFrameLogCount = 0;
        #endregion

        #region Events
        public event EventHandler<ushort>? RegisterValueChanged;
        public event EventHandler<ushort>? HeartbeatValueChanged;
        public event Func<int, ushort, Task>? ChannelProfileIdReceivedAsync;
        public event Func<IReadOnlyDictionary<int, ushort>, Task>? ChannelProfilesReceivedAsync;

        public readonly record struct ChannelProfileWrite(int ChannelIndex, ushort ProfileId);
        #endregion

        #region Configuration Resolution
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

        private static string ResolveSetting(string key, string defaultValue)
        {
            string? val = System.Configuration.ConfigurationManager.AppSettings[key];
            return !string.IsNullOrWhiteSpace(val) ? val.Trim() : defaultValue;
        }

        private static bool ResolveBoolSetting(string key, bool defaultValue)
        {
            string? val = System.Configuration.ConfigurationManager.AppSettings[key];
            return !string.IsNullOrWhiteSpace(val) && bool.TryParse(val.Trim(), out bool b) ? b : defaultValue;
        }

        private static int ResolveIntSetting(string key, int defaultValue)
        {
            string? val = System.Configuration.ConfigurationManager.AppSettings[key];
            return !string.IsNullOrWhiteSpace(val) && int.TryParse(val.Trim(), out int i) ? i : defaultValue;
        }
        #endregion

        #region Public Methods
        public ushort ReadRegister(ushort address = HeartbeatAddress)
        {
            lock (_lock)
            {
                return (address < _registers.Length) ? _registers[address] : (ushort)0;
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
                if (address < _registers.Length)
                {
                    _registers[address] = value;
                }
            }
        }

        public void WriteRegisterSilent(ushort address, ushort value)
        {
            lock (_lock)
            {
                _suppressNotification = true;
                try
                {
                    if (address < _registers.Length)
                    {
                        _registers[address] = value;
                    }
                }
                finally
                {
                    _suppressNotification = false;
                }
            }
        }

        public void Start(int port = DefaultPort, int heartbeatIntervalMs = DefaultHeartbeatIntervalMs)
        {
            if (IsRunning) return;

            try
            {
                Port = port > 0 ? port : ResolveIntSetting("ModbusServerPort", DefaultPort);
                HeartbeatIntervalMs = heartbeatIntervalMs > 0 ? heartbeatIntervalMs : ResolveIntSetting("ModbusHeartbeatIntervalMs", DefaultHeartbeatIntervalMs);

                BindAddress = ResolveSetting("ModbusBindAddress", DefaultBindAddress);
                UnitIdMode = ResolveSetting("ModbusUnitIdMode", DefaultUnitIdMode);
                FixedUnitId = (byte)ResolveIntSetting("ModbusFixedUnitId", DefaultFixedUnitId);
                EnableRawFrameLogging = ResolveBoolSetting("ModbusEnableRawFrameLogging", DefaultEnableRawFrameLogging);
                AutoAddFirewallRule = ResolveBoolSetting("ModbusAutoAddFirewallRule", DefaultAutoAddFirewallRule);

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

                lock (_lock)
                {
                    _registers = new ushort[TotalRegisterCount];
                }

                // Setup background channel consumer for profile loading
                _profileChannel = Channel.CreateUnbounded<ChannelProfileWrite>(new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false
                });
                _profileCts = new CancellationTokenSource();
                _consumerTask = Task.Run(() => ProcessProfileWritesAsync(_profileCts.Token));

                // Parse IPAddress for listener
                IPAddress bindIp;
                if (string.Equals(BindAddress, "0.0.0.0", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(BindAddress))
                {
                    bindIp = IPAddress.Any;
                }
                else if (string.Equals(BindAddress, "::", StringComparison.OrdinalIgnoreCase))
                {
                    bindIp = IPAddress.IPv6Any;
                }
                else if (!IPAddress.TryParse(BindAddress, out bindIp!))
                {
                    ModbusLogger.Warn($"[Modbus TCP Slave] Invalid bind address '{BindAddress}'. Falling back to 0.0.0.0 (all interfaces).");
                    bindIp = IPAddress.Any;
                    BindAddress = "0.0.0.0";
                }

                // Check and warn if bound to loopback
                if (LoopbackHosts.Contains(BindAddress))
                {
                    ModbusLogger.Warn($"[Modbus TCP Slave] WARNING: Bound to loopback ({BindAddress}) — only THIS machine can connect. Set 'ModbusBindAddress' to 0.0.0.0 (or this PC's LAN IP) so other systems can reach it.");
                }

                // Start TCP Listener
                _tcpListener = new TcpListener(bindIp, Port);
                _tcpListener.Start();

                // Determine and format effective endpoints
                var lanIps = GetLocalIPv4Addresses();
                if (bindIp.Equals(IPAddress.Any))
                {
                    _listeningEndpointsDescription = lanIps.Count > 0
                        ? $"0.0.0.0:{Port} (LAN: {string.Join(", ", lanIps.Select(ip => $"{ip}:{Port}"))})"
                        : $"0.0.0.0:{Port}";
                }
                else
                {
                    _listeningEndpointsDescription = $"{BindAddress}:{Port}";
                }

                _serverCts = new CancellationTokenSource();
                _listenerTask = Task.Run(() => AcceptClientsLoopAsync(_tcpListener, _serverCts.Token));

                // Start heartbeat timer
                _heartbeatTimer?.Dispose();
                _heartbeatTimer = new Timer(OnHeartbeatTick, null, HeartbeatIntervalMs, HeartbeatIntervalMs);

                IsRunning = true;
                StatusDescription = $"Running · {BindAddress}:{Port}";

                // Startup report
                var mapDesc = new StringBuilder();
                mapDesc.AppendLine($"[Modbus TCP Slave] Modbus TCP Slave Server Started Successfully.");
                mapDesc.AppendLine($"  Listening Endpoint(s): {_listeningEndpointsDescription}");
                mapDesc.AppendLine($"  Bind Address: {BindAddress} | Port: {Port}");
                mapDesc.AppendLine($"  Unit ID Mode: {UnitIdModeDescription}");
                mapDesc.AppendLine($"  Heartbeat Interval: {HeartbeatIntervalMs}ms");
                mapDesc.AppendLine($"  Raw Frame Logging: {(EnableRawFrameLogging ? "Enabled" : "Disabled")}");
                mapDesc.AppendLine($"  Register Map ({ChannelCount} active channel(s), {TotalRegisterCount} registers):");
                mapDesc.AppendLine($"    Address {HeartbeatAddress}: Heartbeat (auto-incrementing 16-bit)");
                for (int i = 0; i < ChannelCount; i++)
                {
                    mapDesc.AppendLine($"    Address {FirstChannelAddress + i}: Channel {i + 1} Profile ID");
                }
                ModbusLogger.Info(mapDesc.ToString().TrimEnd());

                // Post-start firewall check in background
                Task.Run(() => CheckAndHandleFirewall(Port, AutoAddFirewallRule));
            }
            catch (SocketException sex)
            {
                IsRunning = false;
                string reason = sex.SocketErrorCode switch
                {
                    SocketError.AddressAlreadyInUse => $"Port {Port} is already in use by another process or previous instance.",
                    SocketError.AccessDenied => $"Access denied binding to port {Port}. Ports below 1024 require Administrator privileges.",
                    SocketError.AddressNotAvailable => $"IP address '{BindAddress}' is not available on this machine.",
                    _ => $"Socket error ({sex.SocketErrorCode}): {sex.Message}"
                };
                StatusDescription = $"Failed: {reason}";
                ModbusLogger.Error($"[Modbus TCP Slave] Bind failed: {reason}", sex);
                Stop();
                throw new InvalidOperationException($"Failed to start Modbus TCP Slave on {BindAddress}:{Port}: {reason}", sex);
            }
            catch (Exception ex)
            {
                IsRunning = false;
                StatusDescription = $"Failed: {ex.Message}";
                ModbusLogger.Error($"[Modbus TCP Slave] Failed to start on {BindAddress}:{port}: {ex.Message}", ex);
                Stop();
                throw;
            }
        }

        public void Stop()
        {
            try
            {
                _heartbeatTimer?.Dispose();
                _profileCts?.Cancel();
                _profileChannel?.Writer.TryComplete();
                _serverCts?.Cancel();
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
                _profileCts = null;
                _profileChannel = null;
                _consumerTask = null;
                _serverCts = null;
                _tcpListener = null;
                _listenerTask = null;
                IsRunning = false;
                StatusDescription = "Stopped";
                _listeningEndpointsDescription = "None";
            }
        }
        #endregion

        #region TCP Accept & Client Handling Loop
        private async Task AcceptClientsLoopAsync(TcpListener listener, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await listener.AcceptTcpClientAsync(ct);
                    _ = Task.Run(() => HandleClientAsync(client, ct), ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    ModbusLogger.Error("[Modbus TCP Slave] Error accepting client in listener loop", ex);
                    try
                    {
                        await Task.Delay(500, ct);
                    }
                    catch { break; }
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken serverCt)
        {
            string clientEp = "Unknown";
            try
            {
                if (client.Client.RemoteEndPoint is IPEndPoint ep)
                {
                    clientEp = $"{ep.Address}:{ep.Port}";
                }

                // Enable socket keep-alive to clean up dead peers
                try
                {
                    client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                }
                catch { }

                int clientCount = Interlocked.Increment(ref _activeClients);
                ModbusLogger.Info($"[Modbus TCP Slave] Client connected: {clientEp}. Active clients: {clientCount}");

                using var stream = client.GetStream();
                byte[] mbapHeader = new byte[6];

                while (!serverCt.IsCancellationRequested && client.Connected)
                {
                    // 1. Read MBAP 6-byte header: TxId (2), ProtoId (2), Length (2)
                    bool ok = await ReadExactAsync(stream, mbapHeader, 0, 6, serverCt);
                    if (!ok) break; // Client disconnected gracefully

                    ushort txId = (ushort)((mbapHeader[0] << 8) | mbapHeader[1]);
                    ushort protoId = (ushort)((mbapHeader[2] << 8) | mbapHeader[3]);
                    ushort length = (ushort)((mbapHeader[4] << 8) | mbapHeader[5]);

                    if (protoId != 0 || length < 2 || length > 260)
                    {
                        ModbusLogger.Warn($"[Modbus TCP Slave] Malformed MBAP from {clientEp}: ProtoId={protoId}, Length={length}. Closing connection.");
                        break;
                    }

                    // 2. Read remaining (length) bytes: UnitId (0), FunctionCode (1), Data (length - 2)
                    byte[] payload = new byte[length];
                    ok = await ReadExactAsync(stream, payload, 0, length, serverCt);
                    if (!ok) break;

                    byte unitId = payload[0];
                    byte fc = payload[1];

                    // Full raw frame for logging
                    byte[] fullRxFrame = new byte[6 + length];
                    Buffer.BlockCopy(mbapHeader, 0, fullRxFrame, 0, 6);
                    Buffer.BlockCopy(payload, 0, fullRxFrame, 6, length);

                    if (EnableRawFrameLogging)
                    {
                        LogRawFrame("RX", clientEp, fullRxFrame, 0, fullRxFrame.Length);
                    }

                    _lastRequestTimeUtc = DateTime.UtcNow;

                    // 3. Unit ID Check
                    bool isFixedMode = string.Equals(UnitIdMode, "Fixed", StringComparison.OrdinalIgnoreCase);
                    if (isFixedMode && unitId != FixedUnitId)
                    {
                        Interlocked.Increment(ref _unknownUnitIdRequestsCount);
                        ModbusLogger.Info($"[Modbus TCP Slave] Request from {clientEp} | Unit: {unitId} | FC: {fc:D2} | Outcome: Ignored (Fixed mode, configured Unit ID is {FixedUnitId})");
                        // Do not respond in Fixed mode when Unit ID does not match
                        continue;
                    }

                    Interlocked.Increment(ref _totalRequestsCount);

                    // 4. Process Request
                    byte[] responsePdu = ProcessModbusRequest(clientEp, unitId, fc, payload, 2, length - 2);

                    // 5. Send Response
                    ushort respLength = (ushort)(1 + responsePdu.Length); // 1 byte UnitId + PDU length
                    byte[] fullTxFrame = new byte[6 + 1 + responsePdu.Length];
                    fullTxFrame[0] = (byte)(txId >> 8);
                    fullTxFrame[1] = (byte)(txId & 0xFF);
                    fullTxFrame[2] = 0; // Modbus protocol ID
                    fullTxFrame[3] = 0;
                    fullTxFrame[4] = (byte)(respLength >> 8);
                    fullTxFrame[5] = (byte)(respLength & 0xFF);
                    fullTxFrame[6] = unitId; // Echo back the requested unit ID
                    Buffer.BlockCopy(responsePdu, 0, fullTxFrame, 7, responsePdu.Length);

                    if (EnableRawFrameLogging)
                    {
                        LogRawFrame("TX", clientEp, fullTxFrame, 0, fullTxFrame.Length);
                    }

                    await stream.WriteAsync(fullTxFrame, 0, fullTxFrame.Length, serverCt);
                    await stream.FlushAsync(serverCt);
                }
            }
            catch (IOException ioEx) when (ioEx.InnerException is SocketException sex &&
                                         (sex.SocketErrorCode == SocketError.ConnectionReset ||
                                          sex.SocketErrorCode == SocketError.ConnectionAborted))
            {
                ModbusLogger.Info($"[Modbus TCP Slave] Client {clientEp} reset/aborted connection.");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ModbusLogger.Error($"[Modbus TCP Slave] Unexpected error handling client {clientEp}", ex);
            }
            finally
            {
                int remaining = Interlocked.Decrement(ref _activeClients);
                ModbusLogger.Info($"[Modbus TCP Slave] Client disconnected: {clientEp}. Active clients: {remaining}");
                try { client.Dispose(); } catch { }
            }
        }

        private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, int offset, int count, CancellationToken ct)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = await stream.ReadAsync(buffer, offset + totalRead, count - totalRead, ct);
                if (read == 0) return false;
                totalRead += read;
            }
            return true;
        }

        private byte[] ProcessModbusRequest(string clientEp, byte unitId, byte fc, byte[] payload, int pduOffset, int pduLength)
        {
            try
            {
                switch (fc)
                {
                    case 3: // Read Holding Registers
                        return HandleReadHoldingRegisters(clientEp, unitId, payload, pduOffset, pduLength);

                    case 6: // Write Single Register
                        return HandleWriteSingleRegister(clientEp, unitId, payload, pduOffset, pduLength);

                    case 16: // 0x10, Write Multiple Registers
                        return HandleWriteMultipleRegisters(clientEp, unitId, payload, pduOffset, pduLength);

                    default: // Unsupported Function Code -> Modbus Exception 01
                        Interlocked.Increment(ref _totalExceptionsCount);
                        ModbusLogger.Warn($"[Modbus TCP Slave] Request from {clientEp} | Unit: {unitId} | FC: {fc:D2} | Outcome: Exception 01 (Illegal Function Code {fc:D2})");
                        return new byte[] { (byte)(fc | 0x80), 0x01 };
                }
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _totalExceptionsCount);
                ModbusLogger.Error($"[Modbus TCP Slave] Exception processing FC {fc:D2} from {clientEp}", ex);
                return new byte[] { (byte)(fc | 0x80), 0x04 }; // Slave Device Failure
            }
        }
        #endregion

        #region Function Code Handlers
        private byte[] HandleReadHoldingRegisters(string clientEp, byte unitId, byte[] payload, int offset, int length)
        {
            if (length < 4)
            {
                Interlocked.Increment(ref _totalExceptionsCount);
                ModbusLogger.Warn($"[Modbus TCP Slave] Request from {clientEp} | Unit: {unitId} | FC: 03 | Outcome: Exception 03 (Payload too short: {length} bytes)");
                return new byte[] { 0x83, 0x03 };
            }

            ushort startAddress = (ushort)((payload[offset] << 8) | payload[offset + 1]);
            ushort count = (ushort)((payload[offset + 2] << 8) | payload[offset + 3]);

            if (count < 1 || count > 125)
            {
                Interlocked.Increment(ref _totalExceptionsCount);
                ModbusLogger.Warn($"[Modbus TCP Slave] Request from {clientEp} | Unit: {unitId} | FC: 03 | Addr: {startAddress} | Qty: {count} | Outcome: Exception 03 (Illegal Count {count})");
                return new byte[] { 0x83, 0x03 };
            }

            if (startAddress + count > TotalRegisterCount)
            {
                Interlocked.Increment(ref _totalExceptionsCount);
                ModbusLogger.Warn($"[Modbus TCP Slave] Request from {clientEp} | Unit: {unitId} | FC: 03 | Addr: {startAddress} | Qty: {count} | Outcome: Exception 02 (Illegal Address: start={startAddress}, count={count}, total={TotalRegisterCount})");
                return new byte[] { 0x83, 0x02 };
            }

            ushort[] values = new ushort[count];
            lock (_lock)
            {
                for (int i = 0; i < count; i++)
                {
                    values[i] = _registers[startAddress + i];
                }
            }

            byte byteCount = (byte)(count * 2);
            byte[] response = new byte[2 + byteCount];
            response[0] = 0x03;
            response[1] = byteCount;

            for (int i = 0; i < count; i++)
            {
                response[2 + i * 2] = (byte)(values[i] >> 8);
                response[3 + i * 2] = (byte)(values[i] & 0xFF);
            }

            ModbusLogger.Info($"[Modbus TCP Slave] Request from {clientEp} | Unit: {unitId} | FC: 03 (ReadHoldingRegisters) | Addr: {startAddress} | Qty: {count} | Outcome: OK (returned [{string.Join(", ", values)}])");
            return response;
        }

        private byte[] HandleWriteSingleRegister(string clientEp, byte unitId, byte[] payload, int offset, int length)
        {
            if (length < 4)
            {
                Interlocked.Increment(ref _totalExceptionsCount);
                ModbusLogger.Warn($"[Modbus TCP Slave] Request from {clientEp} | Unit: {unitId} | FC: 06 | Outcome: Exception 03 (Payload too short: {length} bytes)");
                return new byte[] { 0x86, 0x03 };
            }

            ushort address = (ushort)((payload[offset] << 8) | payload[offset + 1]);
            ushort value = (ushort)((payload[offset + 2] << 8) | payload[offset + 3]);

            if (address >= TotalRegisterCount)
            {
                Interlocked.Increment(ref _totalExceptionsCount);
                ModbusLogger.Warn($"[Modbus TCP Slave] Request from {clientEp} | Unit: {unitId} | FC: 06 | Addr: {address} | Value: {value} | Outcome: Exception 02 (Illegal Address: addr={address}, total={TotalRegisterCount})");
                return new byte[] { 0x86, 0x02 };
            }

            lock (_lock)
            {
                _registers[address] = value;
            }

            ModbusLogger.Info($"[Modbus TCP Slave] Request from {clientEp} | Unit: {unitId} | FC: 06 (WriteSingleRegister) | Addr: {address} | Value: {value} | Outcome: OK");

            // Handle Heartbeat or Channel Profile Write notification
            HandleRegisterWriteEvents(address, 1);

            // Echo back FC 06 request as response
            byte[] response = new byte[5];
            response[0] = 0x06;
            response[1] = payload[offset];
            response[2] = payload[offset + 1];
            response[3] = payload[offset + 2];
            response[4] = payload[offset + 3];
            return response;
        }

        private byte[] HandleWriteMultipleRegisters(string clientEp, byte unitId, byte[] payload, int offset, int length)
        {
            if (length < 5)
            {
                Interlocked.Increment(ref _totalExceptionsCount);
                ModbusLogger.Warn($"[Modbus TCP Slave] Request from {clientEp} | Unit: {unitId} | FC: 16 | Outcome: Exception 03 (Payload too short: {length} bytes)");
                return new byte[] { 0x90, 0x03 };
            }

            ushort startAddress = (ushort)((payload[offset] << 8) | payload[offset + 1]);
            ushort count = (ushort)((payload[offset + 2] << 8) | payload[offset + 3]);
            byte byteCount = payload[offset + 4];

            if (count < 1 || count > 123 || byteCount != count * 2 || length < 5 + byteCount)
            {
                Interlocked.Increment(ref _totalExceptionsCount);
                ModbusLogger.Warn($"[Modbus TCP Slave] Request from {clientEp} | Unit: {unitId} | FC: 16 | Addr: {startAddress} | Qty: {count} | ByteCount: {byteCount} | Outcome: Exception 03 (Illegal Count or ByteCount)");
                return new byte[] { 0x90, 0x03 };
            }

            if (startAddress + count > TotalRegisterCount)
            {
                Interlocked.Increment(ref _totalExceptionsCount);
                ModbusLogger.Warn($"[Modbus TCP Slave] Request from {clientEp} | Unit: {unitId} | FC: 16 | Addr: {startAddress} | Qty: {count} | Outcome: Exception 02 (Illegal Address: start={startAddress}, count={count}, total={TotalRegisterCount})");
                return new byte[] { 0x90, 0x02 };
            }

            ushort[] writtenValues = new ushort[count];
            lock (_lock)
            {
                for (int i = 0; i < count; i++)
                {
                    int valOffset = offset + 5 + i * 2;
                    ushort val = (ushort)((payload[valOffset] << 8) | payload[valOffset + 1]);
                    _registers[startAddress + i] = val;
                    writtenValues[i] = val;
                }
            }

            ModbusLogger.Info($"[Modbus TCP Slave] Request from {clientEp} | Unit: {unitId} | FC: 16 (WriteMultipleRegisters) | Addr: {startAddress} | Qty: {count} | Values: [{string.Join(", ", writtenValues)}] | Outcome: OK");

            // Handle Heartbeat or Channel Profile Write notifications
            HandleRegisterWriteEvents(startAddress, count);

            // Response for FC 16: FC (1 byte), StartAddress (2 bytes), Quantity (2 bytes)
            byte[] response = new byte[5];
            response[0] = 0x10;
            response[1] = payload[offset];
            response[2] = payload[offset + 1];
            response[3] = payload[offset + 2];
            response[4] = payload[offset + 3];
            return response;
        }

        private void HandleRegisterWriteEvents(ushort start, ushort count)
        {
            if (_suppressNotification) return;

            ushort end = (ushort)(start + count - 1);

            // Heartbeat write at address 0 (HeartbeatAddress)
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

            // Channel Profile ID writes (addresses 1..ChannelCount)
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

                _profileChannel?.Writer.TryWrite(new ChannelProfileWrite(channelIndex, val));
            }
        }
        #endregion

        #region Background Profile Consumer Pipeline
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
            if (!IsRunning) return;

            lock (_lock)
            {
                _heartbeatValue = (ushort)((_heartbeatValue + 1) % 65536);
                _suppressNotification = true;
                try
                {
                    if (HeartbeatAddress < _registers.Length)
                    {
                        _registers[HeartbeatAddress] = _heartbeatValue;
                    }
                }
                finally
                {
                    _suppressNotification = false;
                }
            }
        }
        #endregion

        #region Network & Firewall Helpers
        private void LogRawFrame(string direction, string clientEp, byte[] buffer, int offset, int length)
        {
            long currentSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (Interlocked.Read(ref _rawFrameLastSec) != currentSec)
            {
                Interlocked.Exchange(ref _rawFrameLastSec, currentSec);
                Interlocked.Exchange(ref _rawFrameLogCount, 0);
            }

            int count = Interlocked.Increment(ref _rawFrameLogCount);
            if (count <= MaxRawFramesPerSecond)
            {
                string hex = BitConverter.ToString(buffer, offset, length).Replace("-", " ");
                ModbusLogger.Info($"[Modbus TCP Slave] [RAW {direction}] {clientEp} -> {hex}");
            }
            else if (count == MaxRawFramesPerSecond + 1)
            {
                ModbusLogger.Warn("[Modbus TCP Slave] [RAW] Frame rate exceeded 50/sec. Temporarily suppressing raw frames for this second.");
            }
        }

        public static List<string> GetLocalIPv4Addresses()
        {
            var ips = new HashSet<string>();
            try
            {
                foreach (var netInterface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (netInterface.OperationalStatus != OperationalStatus.Up ||
                        netInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;

                    var ipProps = netInterface.GetIPProperties();
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                            !IPAddress.IsLoopback(addr.Address))
                        {
                            ips.Add(addr.Address.ToString());
                        }
                    }
                }
            }
            catch { }

            if (ips.Count == 0)
            {
                try
                {
                    using var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                    s.Connect("8.8.8.8", 80);
                    if (s.LocalEndPoint is IPEndPoint ep && !IPAddress.IsLoopback(ep.Address))
                    {
                        ips.Add(ep.Address.ToString());
                    }
                }
                catch { }
            }

            return ips.OrderBy(x => x).ToList();
        }

        private static void CheckAndHandleFirewall(int port, bool autoAddIfElevated)
        {
            string ruleName = $"8F_Modbus_{port}";
            bool exists = CheckFirewallRuleExists(ruleName);
            if (exists)
            {
                ModbusLogger.Info($"[Modbus TCP Slave] Firewall: Inbound allow rule '{ruleName}' exists for port {port}.");
                return;
            }

            string manualCommand = $"netsh advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow protocol=TCP localport={port}";

            if (autoAddIfElevated)
            {
                if (IsAdministrator())
                {
                    var (ok, detail) = TryAddFirewallRule(ruleName, port);
                    if (ok)
                    {
                        ModbusLogger.Info($"[Modbus TCP Slave] Firewall: Automatically opened inbound TCP port {port} (rule '{ruleName}').");
                        return;
                    }
                    ModbusLogger.Warn($"[Modbus TCP Slave] Firewall: Could not auto-add rule '{ruleName}': {detail}. Run manually: {manualCommand}");
                    return;
                }
                ModbusLogger.Warn($"[Modbus TCP Slave] Firewall: Auto-add rule is enabled, but application is not running with Administrator privileges.");
            }

            ModbusLogger.Warn($"[Modbus TCP Slave] Firewall: Inbound rule for port {port} is not found. Remote masters may be blocked.");
            ModbusLogger.Warn($"  To open the port, run this command in an Administrator Command Prompt:");
            ModbusLogger.Warn($"  {manualCommand}");
        }

        private static bool CheckFirewallRuleExists(string ruleName)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"advfirewall firewall show rule name=\"{ruleName}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc == null) return false;
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(3000);
                return proc.ExitCode == 0 && !output.Contains("No rules match");
            }
            catch
            {
                return false;
            }
        }

        private static (bool Success, string Message) TryAddFirewallRule(string ruleName, int port)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow protocol=TCP localport={port}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc == null) return (false, "Could not launch netsh");
                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit(4000);
                if (proc.ExitCode == 0) return (true, "OK");
                return (false, string.IsNullOrWhiteSpace(stderr) ? stdout.Trim() : stderr.Trim());
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private static bool IsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
        #endregion
    }
}
