namespace PortTraining.PortGatewayService.Devices
{
    // Internal readiness snapshot for the gateway.session.prepare check (V1.1 section
    // 11: "重新读取设备帧、验证版本/急停/电源/手柄/台位"). This is not a wire type -
    // EquipmentInputFrame (section 10.2) is the real cross-process shape once the
    // realtime pipe (PT-IF-007) exists.
    public sealed class DeviceFrameSnapshot
    {
        public bool PlcOnline { get; set; }
        public bool EmergencyStopReleased { get; set; }
        public bool ControlPowerOn { get; set; }
        public bool HandlesCentered { get; set; }
        public string ControlMode { get; set; }
        public bool IsSimulated { get; set; }

        // V1.1 section 10.2 EquipmentInputFrame's normalized -1.0..1.0 handle axes and horn.
        public double GantryCommand { get; set; }
        public double TrolleyCommand { get; set; }
        public double HoistCommand { get; set; }
        public bool HornPressed { get; set; }
    }
}
