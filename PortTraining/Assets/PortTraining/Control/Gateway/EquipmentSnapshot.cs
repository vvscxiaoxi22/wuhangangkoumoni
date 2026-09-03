using System;

namespace PortTraining.Control.Gateway
{
    [Serializable]
    public sealed class EquipmentSnapshot
    {
        public bool gatewayOnline;
        public bool plcOnline;
        public bool emergencyStopReleased;
        public bool controlPowerOn;
        public bool handlesCentered;
        public string controlMode = "Local";
        public float gantryCommand;
        public float trolleyCommand;
        public float hoistCommand;
        public bool hornPressed;
        public bool spreaderLockPressed;
        public bool spreaderUnlockPressed;
        public long sequence;
        public string capturedAt = string.Empty;
    }
}
