using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace _8F.Services.Interfaces
{
    /// <summary>
    /// Modbus TCP Slave Service interface.
    /// The register map depends dynamically on the App.config "Channel" setting (1..4):
    ///   Channel = 1 : Registers = 2 (Address 0: Heartbeat, Address 1: Channel 1 Profile ID)
    ///   Channel = 2 : Registers = 3 (Address 0: Heartbeat, Addresses 1..2: Channels 1..2 Profile IDs)
    ///   Channel = 3 : Registers = 4 (Address 0: Heartbeat, Addresses 1..3: Channels 1..3 Profile IDs)
    ///   Channel = 4 : Registers = 5 (Address 0: Heartbeat, Addresses 1..4: Channels 1..4 Profile IDs)
    /// Changing App.config requires an application restart (read once at startup).
    /// Supported function codes: 03 (Read), 06 (Write Single), 16 (Write Multiple).
    /// </summary>
    public interface IModbusSlaveService
    {
        bool IsRunning { get; }
        int Port { get; }
        int HeartbeatIntervalMs { get; }
        int ChannelCount { get; }
        int TotalRegisterCount { get; }
        bool IsChannelCountFallback { get; }
        string? ChannelCountConfigError { get; }

        ushort ReadRegister(ushort address = 0);
        void WriteRegister(ushort address, ushort value);
        void WriteRegister(ushort value);
        void WriteRegisterSilent(ushort address, ushort value);
        void Start(int port = 502, int heartbeatIntervalMs = 1000);
        void Stop();
        event EventHandler<ushort>? RegisterValueChanged;
        event EventHandler<ushort>? HeartbeatValueChanged;
        event Func<int, ushort, Task>? ChannelProfileIdReceivedAsync;
        event Func<IReadOnlyDictionary<int, ushort>, Task>? ChannelProfilesReceivedAsync;
    }
}
