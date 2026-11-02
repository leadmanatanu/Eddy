using System;

namespace _8F.Services.Interfaces
{
    public interface IModbusSlaveService
    {
        bool IsRunning { get; }
        int Port { get; }
        int HeartbeatIntervalMs { get; }
        ushort ReadRegister(ushort address = 0);
        void WriteRegister(ushort address, ushort value);
        void WriteRegister(ushort value);
        void WriteRegisterSilent(ushort address, ushort value);
        void Start(int port = 502, int heartbeatIntervalMs = 1000);
        void Stop();
        event EventHandler<ushort>? RegisterValueChanged;
        event EventHandler<ushort>? HeartbeatValueChanged;
    }
}
