using System;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Crane
{
    public enum SpreaderLength
    {
        Unknown = 0,
        Feet20 = 20,
        Feet40 = 40
    }

    public enum TwistlockState
    {
        Unknown = 0,
        Unlocked = 1,
        Locking = 2,
        Locked = 3,
        Unlocking = 4,
        Fault = 5
    }

    [Serializable]
    public sealed class YardCraneMechanismState
    {
        [Header("Gantry")]
        public double gantryPositionMeters;
        public float gantrySpeedMetersPerSecond;

        [Header("Trolley")]
        public double trolleyPositionMeters;
        public float trolleySpeedMetersPerSecond;

        [Header("Hoist and spreader")]
        public double spreaderHeightMeters;
        public float hoistSpeedMetersPerSecond;
        public float spreaderRotationDegrees;
        public SpreaderLength spreaderLength;
        public TwistlockState twistlockState;
        public bool containerAttached;
        public bool spreaderPumpRunning;

        [Header("Safety")]
        public WheelClampState wheelClampState;
        public bool emergencyStopActive;
        public bool controlLeaseActive;
        public long lastInputSequence;
        public long lastFeedbackSequence;
    }
}
