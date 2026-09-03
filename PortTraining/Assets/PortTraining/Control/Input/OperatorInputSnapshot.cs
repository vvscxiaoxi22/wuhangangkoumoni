using System;

namespace PortTraining.Control.Input
{
    [Serializable]
    public sealed class OperatorInputSnapshot
    {
        public string sourceId = string.Empty;
        public bool available;
        public float gantry;
        public float trolley;
        public float hoist;
        public bool horn;
        public bool spreaderLock;
        public bool spreaderUnlock;
        public bool controlPowerOn;
        public bool emergencyStopReleased;
        public bool handlesCentered;
        public long sequence;
        public string capturedAt = string.Empty;
    }
}

