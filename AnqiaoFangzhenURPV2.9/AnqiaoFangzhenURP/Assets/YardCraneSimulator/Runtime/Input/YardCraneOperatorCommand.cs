using System;
using System.Collections.Generic;

namespace PortTraining.YardCraneSimulator.Controls
{
    /// <summary>
    /// Every discrete (press-once) operator intent a device can report in one frame. Continuous
    /// values (the three motion axes, spreader rotation axis, horn-held) are not intents - they
    /// are read every frame regardless of "press" semantics, so they stay as their own fields on
    /// the command structs below rather than being folded into this set.
    /// </summary>
    public enum YardCraneOperatorIntent
    {
        ContainerLock = 0,
        ContainerUnlock = 1,
        Pause = 2,
        WheelClampToggle = 3,
        ControlPowerToggle = 4,
        EmergencyStopToggle = 5,
        FaultReset = 6,
        TrolleyAnchorToggle = 7,
        GantryAnchorToggle = 8,
        SpreaderPumpToggle = 9,
        SpreaderSizeFt20 = 10,
        SpreaderSizeFt40 = 11,
        SpreaderReturnToZero = 12
    }

    /// <summary>
    /// Device-independent operator intent. Motion execution must only consume this
    /// command and must never read keyboard, gamepad or PLC APIs directly.
    /// </summary>
    public readonly struct YardCraneOperatorCommand
    {
        private static readonly HashSet<YardCraneOperatorIntent> EmptyIntents = new();

        private readonly HashSet<YardCraneOperatorIntent> intents;

        public YardCraneOperatorCommand(
            string sourceId,
            long sequence,
            long capturedAtMs,
            float gantryAxis,
            float trolleyAxis,
            float hoistAxis,
            bool hornHeld,
            float spreaderRotationAxis = 0f,
            IEnumerable<YardCraneOperatorIntent> intents = null)
        {
            SourceId = sourceId ?? string.Empty;
            Sequence = sequence;
            CapturedAtMs = capturedAtMs;
            GantryAxis = ClampNormalized(gantryAxis);
            TrolleyAxis = ClampNormalized(trolleyAxis);
            HoistAxis = ClampNormalized(hoistAxis);
            SpreaderRotationAxis = ClampNormalized(spreaderRotationAxis);
            HornHeld = hornHeld;
            this.intents = intents == null ? EmptyIntents : new HashSet<YardCraneOperatorIntent>(intents);
        }

        public string SourceId { get; }
        public long Sequence { get; }
        public long CapturedAtMs { get; }
        public float GantryAxis { get; }
        public float TrolleyAxis { get; }
        public float HoistAxis { get; }
        public float SpreaderRotationAxis { get; }
        public bool HornHeld { get; }

        public bool Has(YardCraneOperatorIntent intent) => intents.Contains(intent);
        public int IntentCount => intents.Count;
        public IEnumerable<YardCraneOperatorIntent> ActiveIntents => intents;

        public bool HandlesCentered =>
            Math.Abs(GantryAxis) <= 0.0001f &&
            Math.Abs(TrolleyAxis) <= 0.0001f &&
            Math.Abs(HoistAxis) <= 0.0001f &&
            Math.Abs(SpreaderRotationAxis) <= 0.0001f;

        public bool HasIntent => !HandlesCentered || HornHeld || intents.Count > 0;

        public static YardCraneOperatorCommand Zero(long sequence, long capturedAtMs)
        {
            return new YardCraneOperatorCommand("NONE", sequence, capturedAtMs, 0f, 0f, 0f, false);
        }

        private static float ClampNormalized(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }

            return Math.Max(-1f, Math.Min(1f, value));
        }
    }

    public readonly struct YardCraneInputSnapshot
    {
        private static readonly HashSet<YardCraneOperatorIntent> EmptyIntents = new();

        private readonly HashSet<YardCraneOperatorIntent> intents;

        public YardCraneInputSnapshot(
            string sourceId,
            bool isConnected,
            float gantryAxis,
            float trolleyAxis,
            float hoistAxis,
            bool hornHeld,
            float spreaderRotationAxis = 0f,
            IEnumerable<YardCraneOperatorIntent> intents = null)
        {
            SourceId = sourceId ?? string.Empty;
            IsConnected = isConnected;
            GantryAxis = gantryAxis;
            TrolleyAxis = trolleyAxis;
            HoistAxis = hoistAxis;
            SpreaderRotationAxis = spreaderRotationAxis;
            HornHeld = hornHeld;
            this.intents = intents == null ? EmptyIntents : new HashSet<YardCraneOperatorIntent>(intents);
        }

        public string SourceId { get; }
        public bool IsConnected { get; }
        public float GantryAxis { get; }
        public float TrolleyAxis { get; }
        public float HoistAxis { get; }
        public float SpreaderRotationAxis { get; }
        public bool HornHeld { get; }

        public bool Has(YardCraneOperatorIntent intent) => intents.Contains(intent);
        public int IntentCount => intents.Count;
        public IEnumerable<YardCraneOperatorIntent> ActiveIntents => intents;

        public bool HandlesCentered =>
            Math.Abs(GantryAxis) <= 0.0001f &&
            Math.Abs(TrolleyAxis) <= 0.0001f &&
            Math.Abs(HoistAxis) <= 0.0001f &&
            Math.Abs(SpreaderRotationAxis) <= 0.0001f;

        public bool HasIntent => !HandlesCentered || HornHeld || intents.Count > 0;
    }

    public readonly struct YardCraneInputRouteResult
    {
        public YardCraneInputRouteResult(YardCraneOperatorCommand command, bool activeSourceLost)
        {
            Command = command;
            ActiveSourceLost = activeSourceLost;
        }

        public YardCraneOperatorCommand Command { get; }
        public bool ActiveSourceLost { get; }
    }
}
