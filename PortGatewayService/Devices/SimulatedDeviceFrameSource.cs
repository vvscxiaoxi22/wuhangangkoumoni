namespace PortTraining.PortGatewayService.Devices
{
    // Makes V1.1 section 18.1's allowSimulationFallback concept concrete: reports a
    // fixed "safe and ready, fixed Local mode" frame so gateway.session.prepare has a
    // legitimate way to succeed before any real S7 driver exists. GatewayControlPipeServer
    // only consults this when GatewayStationOptions.AllowSimulationFallback is true -
    // section 18.1 requires that to stay false at any real assessment station.
    // PlcOnline stays false: this is not a real PLC connection, only a stand-in.
    public sealed class SimulatedDeviceFrameSource : IDeviceFrameSource
    {
        public DeviceFrameSnapshot GetCurrentFrame()
        {
            return new DeviceFrameSnapshot
            {
                PlcOnline = false,
                EmergencyStopReleased = true,
                ControlPowerOn = true,
                HandlesCentered = true,
                ControlMode = "Local",
                IsSimulated = true,
                GantryCommand = 0.0,
                TrolleyCommand = 0.0,
                HoistCommand = 0.0,
                HornPressed = false
            };
        }
    }
}
