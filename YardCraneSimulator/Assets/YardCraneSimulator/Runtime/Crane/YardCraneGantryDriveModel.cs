using System;

namespace PortTraining.YardCraneSimulator.Crane
{
    public enum WheelClampState
    {
        Unknown = 0,
        Released = 1,
        Transitioning = 2,
        Clamped = 3,
        Fault = 4
    }

    /// <summary>
    /// Unity-independent decision for one horizontal crane axis approaching a container.
    /// Surface gaps are negative while overlapping, zero while touching and positive while clear.
    /// </summary>
    public static class YardCraneContainerApproachInterlock
    {
        private const float InputDeadband = 0.0001f;
        private const float CenteredAxisToleranceMeters = 0.001f;

        public static bool ShouldInhibit(
            float requestedAxisInput,
            float containerCenterOffsetOnAxisMeters,
            float axisSurfaceGapMeters,
            float orthogonalSurfaceGapMeters,
            float verticalSurfaceGapMeters,
            float axisStoppingMarginMeters,
            float contactEnvelopeMarginMeters)
        {
            if (Math.Abs(requestedAxisInput) <= InputDeadband)
            {
                return false;
            }

            if (!IsFinite(requestedAxisInput) ||
                !IsFinite(containerCenterOffsetOnAxisMeters) ||
                !IsFinite(axisSurfaceGapMeters) ||
                !IsFinite(orthogonalSurfaceGapMeters) ||
                !IsFinite(verticalSurfaceGapMeters) ||
                !IsNonNegativeFinite(axisStoppingMarginMeters) ||
                !IsNonNegativeFinite(contactEnvelopeMarginMeters))
            {
                // Invalid geometry must fail safe while a motion command is active.
                return true;
            }

            var isInsideContactCorridor =
                axisSurfaceGapMeters <= axisStoppingMarginMeters &&
                orthogonalSurfaceGapMeters <= contactEnvelopeMarginMeters &&
                verticalSurfaceGapMeters <= contactEnvelopeMarginMeters;
            if (!isInsideContactCorridor)
            {
                return false;
            }

            if (Math.Abs(containerCenterOffsetOnAxisMeters) <= CenteredAxisToleranceMeters)
            {
                // The spreader is landed centrally: either direction would drag it across the box top.
                return true;
            }

            // Preserve the escape direction. Only a command that closes the axis gap is inhibited.
            return requestedAxisInput * containerCenterOffsetOnAxisMeters > 0f;
        }

        private static bool IsNonNegativeFinite(float value) => IsFinite(value) && value >= 0f;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public enum YardCraneAnchorState
    {
        Unknown = 0,
        Released = 1,
        Transitioning = 2,
        Anchored = 3,
        Fault = 4
    }

    /// <summary>
    /// Unity-independent storm-anchor pin state machine. The gantry rail anchor and the
    /// trolley anchor are physically independent from the wheel clamp and from each
    /// other (confirmed against the physical console), so this is a standalone class
    /// instantiated twice rather than folded into the wheel-clamp state machine above.
    /// Transition timing mirrors YardCraneGantryDriveModel's wheel-clamp handling.
    /// </summary>
    public sealed class YardCraneAnchorInterlock
    {
        private readonly float transitionSeconds;
        private float transitionRemainingSeconds;
        private bool transitionTargetAnchored;

        public YardCraneAnchorInterlock(float transitionSeconds)
        {
            if (!IsNonNegativeFinite(transitionSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(transitionSeconds));
            }

            this.transitionSeconds = transitionSeconds;
            Reset();
        }

        public YardCraneAnchorState State { get; private set; }

        public void Reset(bool anchored = false)
        {
            State = anchored ? YardCraneAnchorState.Anchored : YardCraneAnchorState.Released;
            transitionRemainingSeconds = 0f;
            transitionTargetAnchored = anchored;
        }

        public bool TryRequestAnchor(bool anchorApplied, bool axisIsStationary, out string rejectionCode)
        {
            if (State == YardCraneAnchorState.Fault)
            {
                rejectionCode = "ANCHOR_FAULT";
                return false;
            }

            if (State == YardCraneAnchorState.Transitioning)
            {
                rejectionCode = "ANCHOR_BUSY";
                return false;
            }

            if (anchorApplied && !axisIsStationary)
            {
                rejectionCode = "ANCHOR_MOVING_INTERLOCK";
                return false;
            }

            var requestedState = anchorApplied ? YardCraneAnchorState.Anchored : YardCraneAnchorState.Released;
            if (State == requestedState)
            {
                rejectionCode = string.Empty;
                return true;
            }

            transitionTargetAnchored = anchorApplied;
            transitionRemainingSeconds = transitionSeconds;
            State = transitionRemainingSeconds <= 0f ? requestedState : YardCraneAnchorState.Transitioning;
            rejectionCode = string.Empty;
            return true;
        }

        public void Advance(float deltaTimeSeconds)
        {
            if (State != YardCraneAnchorState.Transitioning)
            {
                return;
            }

            transitionRemainingSeconds -= deltaTimeSeconds;
            if (transitionRemainingSeconds <= 0f)
            {
                transitionRemainingSeconds = 0f;
                State = transitionTargetAnchored ? YardCraneAnchorState.Anchored : YardCraneAnchorState.Released;
            }
        }

        private static bool IsNonNegativeFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }

    public enum YardCraneSpreaderSize
    {
        Unknown = 0,
        Ft20 = 1,
        Ft40 = 2,
        Twin20 = 3
    }

    public enum YardCraneSpreaderSizeMechanismState
    {
        Idle = 0,
        Transitioning = 1,
        Fault = 2
    }

    /// <summary>
    /// Unity-independent spreader size (20ft/40ft/Twin20) state machine. Mirrors
    /// YardCraneAnchorInterlock's transition timing, but carries a three-way target
    /// size instead of a boolean, and gates the request on the spreader hydraulic
    /// pump being on and the crane being unloaded in addition to stationary - resizing
    /// while a container is locked or the pump is off is not physically possible.
    /// </summary>
    public sealed class YardCraneSpreaderSizeInterlock
    {
        private readonly float transitionSeconds;
        private float transitionRemainingSeconds;
        private YardCraneSpreaderSize transitionTargetSize;

        public YardCraneSpreaderSizeInterlock(float transitionSeconds, YardCraneSpreaderSize initialSize)
        {
            if (!IsNonNegativeFinite(transitionSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(transitionSeconds));
            }

            this.transitionSeconds = transitionSeconds;
            Reset(initialSize);
        }

        public YardCraneSpreaderSizeMechanismState State { get; private set; }
        public YardCraneSpreaderSize CurrentSize { get; private set; }

        public void Reset(YardCraneSpreaderSize size)
        {
            CurrentSize = size;
            State = YardCraneSpreaderSizeMechanismState.Idle;
            transitionRemainingSeconds = 0f;
            transitionTargetSize = size;
        }

        public bool TryRequestSize(
            YardCraneSpreaderSize targetSize,
            bool pumpOn,
            bool axisIsStationary,
            bool containerLocked,
            out string rejectionCode)
        {
            if (State == YardCraneSpreaderSizeMechanismState.Fault)
            {
                rejectionCode = "SPREADER_SIZE_FAULT";
                return false;
            }

            if (State == YardCraneSpreaderSizeMechanismState.Transitioning)
            {
                rejectionCode = "SPREADER_SIZE_BUSY";
                return false;
            }

            if (targetSize == YardCraneSpreaderSize.Unknown)
            {
                rejectionCode = "SPREADER_SIZE_INVALID_TARGET";
                return false;
            }

            if (containerLocked)
            {
                rejectionCode = "SPREADER_SIZE_LOADED_INTERLOCK";
                return false;
            }

            if (!axisIsStationary)
            {
                rejectionCode = "SPREADER_SIZE_MOVING_INTERLOCK";
                return false;
            }

            if (!pumpOn)
            {
                rejectionCode = "SPREADER_SIZE_PUMP_OFF";
                return false;
            }

            if (CurrentSize == targetSize)
            {
                rejectionCode = string.Empty;
                return true;
            }

            transitionTargetSize = targetSize;
            transitionRemainingSeconds = transitionSeconds;
            if (transitionRemainingSeconds <= 0f)
            {
                State = YardCraneSpreaderSizeMechanismState.Idle;
                CurrentSize = targetSize;
            }
            else
            {
                State = YardCraneSpreaderSizeMechanismState.Transitioning;
            }

            rejectionCode = string.Empty;
            return true;
        }

        public void Advance(float deltaTimeSeconds)
        {
            if (State != YardCraneSpreaderSizeMechanismState.Transitioning)
            {
                return;
            }

            transitionRemainingSeconds -= deltaTimeSeconds;
            if (transitionRemainingSeconds <= 0f)
            {
                transitionRemainingSeconds = 0f;
                CurrentSize = transitionTargetSize;
                State = YardCraneSpreaderSizeMechanismState.Idle;
            }
        }

        private static bool IsNonNegativeFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }

    /// <summary>
    /// Unity-independent spreader rotation trim (left/right, +/- a few degrees). This is a
    /// held-direction input, not a one-shot action - the operator holds left/right and the
    /// angle ramps while held, mirroring the gantry/trolley/hoist axes rather than the
    /// discrete request/transition shape of the other interlocks in this file. Return-to-zero
    /// is a discrete request that starts an automatic ramp back to zero, cancelled the instant
    /// a held direction resumes. This class remains command-domain and Unity-independent. The
    /// scene adapter consumes <see cref="CurrentAngleDegrees"/> and drives only the spreader-side
    /// visual, collision, sensor and attachment nodes; the eight-rope solver continues to own the
    /// headblock rigidbody and rope sheaves.
    /// </summary>
    public sealed class YardCraneSpreaderRotationModel
    {
        private const float InputDeadband = 0.0001f;

        private readonly float maxAngleDegrees;
        private readonly float rotationRateDegreesPerSecond;
        private readonly float returnToZeroRateDegreesPerSecond;

        public YardCraneSpreaderRotationModel(
            float maxAngleDegrees,
            float rotationRateDegreesPerSecond,
            float returnToZeroRateDegreesPerSecond)
        {
            if (!IsPositiveFinite(maxAngleDegrees) ||
                !IsNonNegativeFinite(rotationRateDegreesPerSecond) ||
                !IsNonNegativeFinite(returnToZeroRateDegreesPerSecond))
            {
                throw new ArgumentOutOfRangeException(nameof(maxAngleDegrees));
            }

            this.maxAngleDegrees = maxAngleDegrees;
            this.rotationRateDegreesPerSecond = rotationRateDegreesPerSecond;
            this.returnToZeroRateDegreesPerSecond = returnToZeroRateDegreesPerSecond;
            Reset();
        }

        public float CurrentAngleDegrees { get; private set; }
        public bool IsReturningToZero { get; private set; }

        public void Reset()
        {
            CurrentAngleDegrees = 0f;
            IsReturningToZero = false;
        }

        public bool TryRequestReturnToZero(bool pumpOn, out string rejectionCode)
        {
            if (!pumpOn)
            {
                rejectionCode = "SPREADER_ROTATION_PUMP_OFF";
                return false;
            }

            IsReturningToZero = true;
            rejectionCode = string.Empty;
            return true;
        }

        public void Advance(float rotationInput, bool pumpOn, float deltaTimeSeconds)
        {
            if (!pumpOn)
            {
                // No hydraulic pressure: the trim holds its last commanded angle and any
                // in-progress auto-return is cancelled rather than silently resuming later.
                IsReturningToZero = false;
                return;
            }

            if (!IsFinite(rotationInput) || Math.Abs(rotationInput) > InputDeadband)
            {
                IsReturningToZero = false;
                if (IsFinite(rotationInput) && IsFinite(deltaTimeSeconds))
                {
                    var next = CurrentAngleDegrees + rotationInput * rotationRateDegreesPerSecond * deltaTimeSeconds;
                    CurrentAngleDegrees = Math.Max(-maxAngleDegrees, Math.Min(maxAngleDegrees, next));
                }

                return;
            }

            if (!IsReturningToZero)
            {
                return;
            }

            var step = returnToZeroRateDegreesPerSecond * deltaTimeSeconds;
            if (Math.Abs(CurrentAngleDegrees) <= step)
            {
                CurrentAngleDegrees = 0f;
                IsReturningToZero = false;
            }
            else
            {
                CurrentAngleDegrees -= Math.Sign(CurrentAngleDegrees) * step;
            }
        }

        private static bool IsPositiveFinite(float value) => IsFinite(value) && value > 0f;
        private static bool IsNonNegativeFinite(float value) => IsFinite(value) && value >= 0f;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public readonly struct YardCraneGantryDriveConfiguration
    {
        public YardCraneGantryDriveConfiguration(
            float maximumSpeedMetersPerSecond,
            float accelerationMetersPerSecondSquared,
            float brakingMetersPerSecondSquared,
            float travelFromHomeMeters,
            float limitSafetyMarginMeters,
            float wheelClampTransitionSeconds,
            float stationarySpeedThresholdMetersPerSecond)
        {
            if (!IsPositiveFinite(maximumSpeedMetersPerSecond) ||
                !IsPositiveFinite(accelerationMetersPerSecondSquared) ||
                !IsPositiveFinite(brakingMetersPerSecondSquared) ||
                !IsPositiveFinite(travelFromHomeMeters) ||
                !IsNonNegativeFinite(limitSafetyMarginMeters) ||
                !IsNonNegativeFinite(wheelClampTransitionSeconds) ||
                !IsNonNegativeFinite(stationarySpeedThresholdMetersPerSecond) ||
                travelFromHomeMeters <= limitSafetyMarginMeters)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumSpeedMetersPerSecond),
                    "Gantry drive configuration contains a non-finite or out-of-range value.");
            }

            MaximumSpeedMetersPerSecond = maximumSpeedMetersPerSecond;
            AccelerationMetersPerSecondSquared = accelerationMetersPerSecondSquared;
            BrakingMetersPerSecondSquared = brakingMetersPerSecondSquared;
            TravelFromHomeMeters = travelFromHomeMeters;
            LimitSafetyMarginMeters = limitSafetyMarginMeters;
            WheelClampTransitionSeconds = wheelClampTransitionSeconds;
            StationarySpeedThresholdMetersPerSecond = stationarySpeedThresholdMetersPerSecond;
        }

        public float MaximumSpeedMetersPerSecond { get; }
        public float AccelerationMetersPerSecondSquared { get; }
        public float BrakingMetersPerSecondSquared { get; }
        public float TravelFromHomeMeters { get; }
        public float LimitSafetyMarginMeters { get; }
        public float WheelClampTransitionSeconds { get; }
        public float StationarySpeedThresholdMetersPerSecond { get; }

        private static bool IsPositiveFinite(float value) => IsFinite(value) && value > 0f;
        private static bool IsNonNegativeFinite(float value) => IsFinite(value) && value >= 0f;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public readonly struct YardCraneGantryDriveStep
    {
        public YardCraneGantryDriveStep(
            float positionMeters,
            float velocityMetersPerSecond,
            WheelClampState wheelClampState,
            bool motionInhibitedByClamp,
            bool forwardLimitActive,
            bool backwardLimitActive,
            bool isBrakeApplied)
        {
            PositionMeters = positionMeters;
            VelocityMetersPerSecond = velocityMetersPerSecond;
            WheelClampState = wheelClampState;
            MotionInhibitedByClamp = motionInhibitedByClamp;
            ForwardLimitActive = forwardLimitActive;
            BackwardLimitActive = backwardLimitActive;
            IsBrakeApplied = isBrakeApplied;
        }

        public float PositionMeters { get; }
        public float VelocityMetersPerSecond { get; }
        public WheelClampState WheelClampState { get; }
        public bool MotionInhibitedByClamp { get; }
        public bool ForwardLimitActive { get; }
        public bool BackwardLimitActive { get; }

        /// <summary>
        /// True when the gantry travel drive brake is applied (holding) — no effective
        /// same-direction acceleration/cruise drive is present. False only while a valid,
        /// non-reversing command is actively accelerating or cruising the gantry, with a
        /// non-zero effective target velocity, and the wheel clamp released.
        /// </summary>
        public bool IsBrakeApplied { get; }
    }

    /// <summary>
    /// Deterministic, Unity-independent gantry drive. It owns acceleration,
    /// braking, travel limits and the wheel-clamp interlock, but never moves a Transform.
    /// </summary>
    public sealed class YardCraneGantryDriveModel
    {
        private readonly YardCraneGantryDriveConfiguration configuration;
        private float clampTransitionRemainingSeconds;
        private bool clampTransitionTargetClamped;

        public YardCraneGantryDriveModel(YardCraneGantryDriveConfiguration configuration)
        {
            this.configuration = configuration;
            Reset();
        }

        public float PositionMeters { get; private set; }
        public float VelocityMetersPerSecond { get; private set; }
        public WheelClampState WheelClampState { get; private set; }
        public bool IsStationary => Math.Abs(VelocityMetersPerSecond) <=
                                    configuration.StationarySpeedThresholdMetersPerSecond;

        /// <summary>
        /// True when the gantry travel drive brake is applied (制动/保持). False only while a
        /// valid, non-reversing command is actively accelerating or cruising the gantry, with a
        /// non-zero effective target velocity, and the wheel clamp released. Reset always leaves
        /// the drive with the brake applied.
        /// </summary>
        public bool IsBrakeApplied { get; private set; }

        public void Reset(float positionMeters = 0f, bool wheelClampApplied = false)
        {
            PositionMeters = Clamp(positionMeters,
                -configuration.TravelFromHomeMeters,
                configuration.TravelFromHomeMeters);
            VelocityMetersPerSecond = 0f;
            WheelClampState = wheelClampApplied ? WheelClampState.Clamped : WheelClampState.Released;
            clampTransitionRemainingSeconds = 0f;
            clampTransitionTargetClamped = wheelClampApplied;
            IsBrakeApplied = true;
        }

        public bool TryRequestWheelClamp(bool clampApplied, out string rejectionCode)
        {
            if (WheelClampState == WheelClampState.Fault)
            {
                rejectionCode = "WHEEL_CLAMP_FAULT";
                return false;
            }

            if (WheelClampState == WheelClampState.Transitioning)
            {
                rejectionCode = "WHEEL_CLAMP_BUSY";
                return false;
            }

            if (clampApplied && !IsStationary)
            {
                rejectionCode = "WHEEL_CLAMP_MOVING_INTERLOCK";
                return false;
            }

            var requestedState = clampApplied ? WheelClampState.Clamped : WheelClampState.Released;
            if (WheelClampState == requestedState)
            {
                rejectionCode = string.Empty;
                return true;
            }

            clampTransitionTargetClamped = clampApplied;
            clampTransitionRemainingSeconds = configuration.WheelClampTransitionSeconds;
            if (clampTransitionRemainingSeconds <= 0f)
            {
                WheelClampState = requestedState;
            }
            else
            {
                WheelClampState = WheelClampState.Transitioning;
            }

            rejectionCode = string.Empty;
            return true;
        }

        public YardCraneGantryDriveStep Step(float normalizedCommand, float deltaTimeSeconds)
        {
            if (float.IsNaN(normalizedCommand) || float.IsInfinity(normalizedCommand) ||
                normalizedCommand < -1f || normalizedCommand > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(normalizedCommand));
            }

            if (float.IsNaN(deltaTimeSeconds) || float.IsInfinity(deltaTimeSeconds) || deltaTimeSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTimeSeconds));
            }

            var velocityBeforeStep = VelocityMetersPerSecond;
            AdvanceWheelClamp(deltaTimeSeconds);
            var inhibitedByClamp = Math.Abs(normalizedCommand) > 0.0001f &&
                                   WheelClampState != WheelClampState.Released;
            var targetVelocity = inhibitedByClamp
                ? 0f
                : normalizedCommand * configuration.MaximumSpeedMetersPerSecond;

            if (Math.Abs(targetVelocity) > 0.0001f)
            {
                var remainingTravel = targetVelocity > 0f
                    ? configuration.TravelFromHomeMeters - PositionMeters
                    : PositionMeters + configuration.TravelFromHomeMeters;
                var safeTravel = Math.Max(0f, remainingTravel - configuration.LimitSafetyMarginMeters);
                var limitSafeSpeed = (float)Math.Sqrt(2f * configuration.BrakingMetersPerSecondSquared * safeTravel);
                targetVelocity = Math.Sign(targetVelocity) * Math.Min(Math.Abs(targetVelocity), limitSafeSpeed);
            }

            var braking = Math.Abs(targetVelocity) <= 0.0001f ||
                          Math.Sign(targetVelocity) != Math.Sign(VelocityMetersPerSecond) ||
                          Math.Abs(targetVelocity) < Math.Abs(VelocityMetersPerSecond);
            VelocityMetersPerSecond = MoveTowards(
                VelocityMetersPerSecond,
                targetVelocity,
                (braking
                    ? configuration.BrakingMetersPerSecondSquared
                    : configuration.AccelerationMetersPerSecondSquared) * deltaTimeSeconds);

            var previousPosition = PositionMeters;
            PositionMeters = Clamp(
                PositionMeters + VelocityMetersPerSecond * deltaTimeSeconds,
                -configuration.TravelFromHomeMeters,
                configuration.TravelFromHomeMeters);
            if (Math.Abs(PositionMeters - previousPosition - VelocityMetersPerSecond * deltaTimeSeconds) > 0.000001f)
            {
                VelocityMetersPerSecond = (PositionMeters - previousPosition) / deltaTimeSeconds;
            }

            var forwardLimit = PositionMeters >=
                               configuration.TravelFromHomeMeters - configuration.LimitSafetyMarginMeters;
            var backwardLimit = PositionMeters <=
                                -configuration.TravelFromHomeMeters + configuration.LimitSafetyMarginMeters;
            if ((forwardLimit && VelocityMetersPerSecond > 0f) ||
                (backwardLimit && VelocityMetersPerSecond < 0f))
            {
                VelocityMetersPerSecond = 0f;
            }

            // targetVelocity already reflects the clamp interlock and the limit-approach speed
            // cap: it is driven to exactly zero once no further safe travel remains in the
            // commanded direction (see the limitSafeSpeed computation above), so holding a
            // command into a reached limit correctly yields no effective target velocity here.
            var hasEffectiveTargetVelocity = Math.Abs(targetVelocity) > 0.0001f;
            var isReversing = Math.Abs(normalizedCommand) > 0.0001f &&
                              Math.Abs(velocityBeforeStep) > configuration.StationarySpeedThresholdMetersPerSecond &&
                              Math.Sign(normalizedCommand) != Math.Sign(velocityBeforeStep);
            var isSpeedNonDecreasing =
                Math.Abs(VelocityMetersPerSecond) >= Math.Abs(velocityBeforeStep) - 0.0001f;
            IsBrakeApplied = !(WheelClampState == WheelClampState.Released &&
                              hasEffectiveTargetVelocity &&
                              !isReversing &&
                              isSpeedNonDecreasing);

            return new YardCraneGantryDriveStep(
                PositionMeters,
                VelocityMetersPerSecond,
                WheelClampState,
                inhibitedByClamp,
                forwardLimit,
                backwardLimit,
                IsBrakeApplied);
        }

        private void AdvanceWheelClamp(float deltaTimeSeconds)
        {
            if (WheelClampState != WheelClampState.Transitioning)
            {
                return;
            }

            clampTransitionRemainingSeconds -= deltaTimeSeconds;
            if (clampTransitionRemainingSeconds <= 0f)
            {
                clampTransitionRemainingSeconds = 0f;
                WheelClampState = clampTransitionTargetClamped
                    ? WheelClampState.Clamped
                    : WheelClampState.Released;
            }
        }

        private static float MoveTowards(float current, float target, float maximumDelta)
        {
            if (Math.Abs(target - current) <= maximumDelta)
            {
                return target;
            }

            return current + Math.Sign(target - current) * maximumDelta;
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
