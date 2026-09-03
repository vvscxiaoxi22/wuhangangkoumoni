namespace PortTraining.Control.EmbeddedGateway
{
    public interface IPlcTransport
    {
        bool IsConnected { get; }
        void Connect();
        EquipmentGatewayFrame Poll();
        void Disconnect();
    }
}

