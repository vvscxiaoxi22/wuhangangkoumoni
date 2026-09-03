using System;
using PortTraining.Core.Station;

namespace PortTraining.Control.EmbeddedGateway
{
    public sealed class S7PlcTransport : IPlcTransport
    {
        private readonly StationProfile station;

        public S7PlcTransport(StationProfile station)
        {
            this.station = station;
        }

        public bool IsConnected { get; private set; }

        public void Connect()
        {
            IsConnected = false;
            throw new NotSupportedException(
                $"S7 transport adapter is not installed for {station?.StationId ?? "unknown station"}. " +
                "Install the approved S7 adapter inside Control/EmbeddedGateway before simulator deployment.");
        }

        public EquipmentGatewayFrame Poll()
        {
            return new EquipmentGatewayFrame { PlcOnline = false };
        }

        public void Disconnect()
        {
            IsConnected = false;
        }
    }
}

