namespace PortTraining.PortGatewayService.Devices
{
    public interface IDeviceFrameSource
    {
        DeviceFrameSnapshot GetCurrentFrame();
    }
}
