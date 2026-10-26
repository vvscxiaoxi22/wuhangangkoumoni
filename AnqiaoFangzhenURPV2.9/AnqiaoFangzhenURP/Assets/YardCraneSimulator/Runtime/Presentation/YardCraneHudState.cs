using System;
using PortTraining.YardCraneSimulator.Crane;
using PortTraining.YardCraneSimulator.Operations;

namespace PortTraining.YardCraneSimulator.Presentation
{
    // Presentation-only values. Never serialize these as a Gateway/PLC wire contract.
    [Flags]
    public enum YardCraneHudSignals
    {
        None = 0, Ready = 1 << 0, Locked = 1 << 1, Locking = 1 << 2,
        Unlocking = 1 << 3, Landed = 1 << 4, BrakeKnown = 1 << 5,
        BrakeApplied = 1 << 6, SafetyKnown = 1 << 7, PowerOn = 1 << 8,
        EmergencyReleased = 1 << 9, AwaitingReset = 1 << 10,
        PumpKnown = 1 << 11, PumpOn = 1 << 12, ReturningToZero = 1 << 13,
        TrolleyRestricted = 1 << 14, GantryRestricted = 1 << 15,
        Paused = 1 << 16, Horn = 1 << 17, Standalone = 1 << 18,
        SpreaderContact = 1 << 19, TrolleyBrakeKnown = 1 << 20,
        TrolleyBrakeApplied = 1 << 21
    }

    // Container load mode ("单箱/双箱" status lamp). This yard-crane rig does not model twin-lift:
    // per YardCraneGantryDriveModel.cs:187, twin-20ft handling is a quay-crane-only capability in
    // this project's baseline, and 2026-09-04's discussion record explicitly scoped Twin out of the
    // yard-crane simulator. The adapter always reports Unknown; the field exists so the driving HUD
    // has a real, data-driven slot ready for whenever/if that capability is modeled here.
    public enum YardCraneHudLoadMode { Unknown, Single, Twin }

    public readonly struct YardCraneHudStatus : IEquatable<YardCraneHudStatus>
    {
        public YardCraneHudStatus(YardCraneOperationPhase phase, YardCraneHudSignals signals,
            WheelClampState clamp, YardCraneAnchorState trolleyAnchor, YardCraneAnchorState gantryAnchor,
            YardCraneSpreaderSize size, YardCraneSpreaderSizeMechanismState sizeState, string inputSource,
            YardCraneHudLoadMode loadMode = YardCraneHudLoadMode.Unknown)
        {
            Phase = phase;
            Signals = signals;
            Clamp = clamp;
            TrolleyAnchor = trolleyAnchor;
            GantryAnchor = gantryAnchor;
            Size = size;
            SizeState = sizeState;
            InputSource = inputSource ?? string.Empty;
            LoadMode = loadMode;
        }

        public YardCraneOperationPhase Phase { get; }
        public YardCraneHudSignals Signals { get; }
        public WheelClampState Clamp { get; }
        public YardCraneAnchorState TrolleyAnchor { get; }
        public YardCraneAnchorState GantryAnchor { get; }
        public YardCraneSpreaderSize Size { get; }
        public YardCraneSpreaderSizeMechanismState SizeState { get; }
        public string InputSource { get; }
        public YardCraneHudLoadMode LoadMode { get; }
        public bool Has(YardCraneHudSignals signal) => (Signals & signal) == signal;
        public bool Equals(YardCraneHudStatus other) => Phase == other.Phase && Signals == other.Signals &&
            Clamp == other.Clamp && TrolleyAnchor == other.TrolleyAnchor && GantryAnchor == other.GantryAnchor &&
            Size == other.Size && SizeState == other.SizeState && InputSource == other.InputSource &&
            LoadMode == other.LoadMode;
        public override bool Equals(object other) => other is YardCraneHudStatus value && Equals(value);
        public override int GetHashCode() => (int)Signals ^ (int)Phase ^ (InputSource?.GetHashCode() ?? 0) ^ (int)LoadMode;
    }

    public readonly struct YardCraneHudTelemetry
    {
        public YardCraneHudTelemetry(float gantryPosition, float gantrySpeed, float trolleyPosition,
            float trolleySpeed, float spreaderHeight, float spreaderSpeed, float physicalRotation)
        {
            GantryPosition = gantryPosition; GantrySpeed = gantrySpeed;
            TrolleyPosition = trolleyPosition; TrolleySpeed = trolleySpeed;
            SpreaderHeight = spreaderHeight; SpreaderSpeed = spreaderSpeed;
            PhysicalRotation = physicalRotation;
        }
        public float GantryPosition { get; }
        public float GantrySpeed { get; }
        public float TrolleyPosition { get; }
        public float TrolleySpeed { get; }
        public float SpreaderHeight { get; }
        public float SpreaderSpeed { get; }
        public float PhysicalRotation { get; }
    }

    public readonly struct YardCraneHudSnapshot
    {
        public YardCraneHudSnapshot(double sampledAtSeconds, YardCraneHudStatus status, YardCraneHudTelemetry telemetry)
        { SampledAtSeconds = sampledAtSeconds; Status = status; Telemetry = telemetry; }
        public double SampledAtSeconds { get; }
        public YardCraneHudStatus Status { get; }
        public YardCraneHudTelemetry Telemetry { get; }
    }

    public interface IYardCraneHudSource
    {
        // Main thread only. A failed read must not invent a healthy/default sample.
        bool TryRead(out YardCraneHudSnapshot snapshot);
    }

    /// <summary>
    /// One-way main-thread feed. Status changes publish immediately; numeric refresh is capped
    /// at 10 Hz. Staleness is a UI warning, NOT the controller's independent safety watchdog.
    /// </summary>
    public sealed class YardCraneHudFeed
    {
        public const double NumericIntervalSeconds = 0.1;
        public const double StaleAfterSeconds = 0.5;
        private double nextNumericUpdate;
        public event Action StatusChanged;
        public event Action TelemetryChanged;
        public YardCraneHudSnapshot Latest { get; private set; }
        public bool HasSample { get; private set; }
        public bool IsStale { get; private set; } = true;

        public void Publish(YardCraneHudSnapshot sample, double now)
        {
            if (double.IsNaN(sample.SampledAtSeconds) || double.IsInfinity(sample.SampledAtSeconds) ||
                sample.SampledAtSeconds > now || now - sample.SampledAtSeconds > StaleAfterSeconds ||
                (HasSample && sample.SampledAtSeconds <= Latest.SampledAtSeconds))
            { Tick(now); return; }

            var recovered = IsStale || !HasSample;
            var changed = recovered || !sample.Status.Equals(Latest.Status);
            Latest = sample;
            HasSample = true;
            IsStale = false;
            if (changed) StatusChanged?.Invoke();
            if (recovered || now >= nextNumericUpdate)
            {
                nextNumericUpdate = now + NumericIntervalSeconds;
                TelemetryChanged?.Invoke();
            }
        }

        public void Tick(double now)
        {
            if (HasSample && now - Latest.SampledAtSeconds > StaleAfterSeconds) Invalidate();
        }

        public void Invalidate()
        {
            if (IsStale) return;
            IsStale = true;
            StatusChanged?.Invoke();
            TelemetryChanged?.Invoke();
        }
    }
}
