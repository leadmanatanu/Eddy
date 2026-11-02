using NModbus;
using NModbus.Data;
using NModbus.Device;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using _8F.Services.Interfaces;

namespace _8F.Services.Implementations
{
    /// <summary>
    /// Modbus TCP Slave Service.
    /// 
    /// Holding Register Map:
    /// - Register 0 (Address 0): Profile ID (R/W, 0 = unassigned / invalid)
    /// - Register 1 (Address 1): Heartbeat Counter (R/W, auto-incremented every HeartbeatIntervalMs)
    /// - Register 2 (Address 2): Profile Load Status (R, 0=Idle, 1=Loading, 2=Loaded OK, 3=Failed, 4=Rejected Busy, 5=Rejected Invalid ID)
    /// </summary>
    public class ModbusSlaveService : IModbusSlaveService
    {
        private TcpListener? _tcpListener;
        private IModbusSlaveNetwork? _slaveNetwork;
        private IModbusSlave? _slave;
        private Timer? _heartbeatTimer;
        private ushort _heartbeatValue = 0;
        private readonly object _lock = new();
        private volatile bool _suppressNotification = false;

        public bool IsRunning { get; private set; }
        public int Port { get; private set; } = 502;
        public int HeartbeatIntervalMs { get; private set; } = 1000;

        public event EventHandler<ushort>? RegisterValueChanged;
        public event EventHandler<ushort>? HeartbeatValueChanged;

        public ushort ReadRegister(ushort address = 0)
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
            WriteRegister(0, value);
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

        /// <summary>
        /// Writes to a holding register without raising application events (used for write-backs and status updates).
        /// </summary>
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
                _tcpListener = new TcpListener(IPAddress.Any, Port);
                _tcpListener.Start();

                var factory = new ModbusFactory();
                _slaveNetwork = factory.CreateSlaveNetwork(_tcpListener);

                byte unitId = 1;
                var dataStore = new SlaveDataStore();
                _slave = factory.CreateSlave(unitId, dataStore);

                // Initialize holding registers:
                // Address 0 = Profile ID (0)
                // Address 1 = Heartbeat (0)
                // Address 2 = Profile Load Status (0 - Idle)
                _slave.DataStore.HoldingRegisters.WritePoints(0, new ushort[] { 0, 0, 0 });

                if (_slave.DataStore.HoldingRegisters is PointSource<ushort> ps)
                {
                    ps.BeforeWrite += (s, e) =>
                    {
                        if (_suppressNotification) return;

                        ushort[] written = e.Points.ToArray();
                        ushort start = e.StartAddress;

                        // Check if Address 0 (Profile ID) was in the written range
                        if (start == 0 && written.Length > 0)
                        {
                            ushort val = written[0];
                            ModbusLogger.Info($"[Modbus TCP Slave] Register 0 (Profile ID) write received: {val} (StartAddress={start}, Count={written.Length})");
                            Task.Run(() =>
                            {
                                try
                                {
                                    RegisterValueChanged?.Invoke(this, val);
                                }
                                catch (Exception ex)
                                {
                                    ModbusLogger.Error("[Modbus TCP Slave] Error executing RegisterValueChanged handler", ex);
                                }
                            });
                        }

                        // Check if Address 1 (Heartbeat) was in the written range
                        if (start <= 1 && (start + written.Length) > 1)
                        {
                            ushort val = written[1 - start];
                            _heartbeatValue = val;
                            Task.Run(() =>
                            {
                                try
                                {
                                    HeartbeatValueChanged?.Invoke(this, val);
                                }
                                catch (Exception ex)
                                {
                                    ModbusLogger.Error("[Modbus TCP Slave] Error executing HeartbeatValueChanged handler", ex);
                                }
                            });
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
                ModbusLogger.Info($"[Modbus TCP Slave] Started listening on TCP Port {Port}, Heartbeat interval {HeartbeatIntervalMs}ms.");
            }
            catch (Exception ex)
            {
                IsRunning = false;
                Stop();
                ModbusLogger.Error($"[Modbus TCP Slave] Failed to start on port {port}: {ex.Message}", ex);
                throw new InvalidOperationException($"Failed to start Modbus TCP Slave on port {port}: {ex.Message}", ex);
            }
        }

        private void OnHeartbeatTick(object? state)
        {
            if (!IsRunning || _slave?.DataStore?.HoldingRegisters == null) return;

            lock (_lock)
            {
                _heartbeatValue = (ushort)((_heartbeatValue + 1) % 65536);
                _slave.DataStore.HoldingRegisters.WritePoints(1, new ushort[] { _heartbeatValue });
            }
        }

        public void Stop()
        {
            try
            {
                _heartbeatTimer?.Dispose();
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
                _slaveNetwork = null;
                _slave = null;
                _tcpListener = null;
                IsRunning = false;
            }
        }
    }
}
