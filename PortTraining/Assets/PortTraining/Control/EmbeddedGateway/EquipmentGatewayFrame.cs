namespace PortTraining.Control.EmbeddedGateway
{
    public sealed class EquipmentGatewayFrame
    {
        public bool PlcOnline { get; set; }
        public bool EmergencyStopReleased { get; set; }
        public bool ControlPowerOn { get; set; }
        public bool HandlesCentered { get; set; }
        public string ControlMode { get; set; } = "Local";
        public float GantryCommand { get; set; }
        public float TrolleyCommand { get; set; }
        public float HoistCommand { get; set; }
        public bool HornPressed { get; set; }
        public bool SpreaderLockPressed { get; set; }
        public bool SpreaderUnlockPressed { get; set; }
    }
}
