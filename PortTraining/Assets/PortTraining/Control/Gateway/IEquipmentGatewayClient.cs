using System;

namespace PortTraining.Control.Gateway
{
    public interface IEquipmentGatewayClient
    {
        bool IsConnected { get; }
        EquipmentSnapshot Latest { get; }
        event Action<EquipmentSnapshot> SnapshotReceived;
        void Connect();
        void Disconnect();
    }
}

