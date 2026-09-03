namespace PortTraining.Control.EmbeddedGateway
{
    public sealed class SimulatedPlcTransport : IPlcTransport
    {
        public bool IsConnected { get; private set; }

        public void Connect()
        {
            IsConnected = true;
        }

        public EquipmentGatewayFrame Poll()
        {
            return new EquipmentGatewayFrame
            {
                PlcOnline = IsConnected,
                EmergencyStopReleased = true,
                ControlPowerOn = true,
                HandlesCentered = true,
                ControlMode = "Local"
            };
        }

        public void Disconnect()
        {
            IsConnected = false;
        }
    }
}

