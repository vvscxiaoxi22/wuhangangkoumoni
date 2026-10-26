using System;

namespace PortTraining.YardCraneSimulator.Operations
{
    /// <summary>
    /// One independently registered handler per discrete operator action
    /// (<see cref="YardCraneOperationActionType"/>). Before this, adding or changing a
    /// mechanism meant editing a shared switch inside
    /// <see cref="YardCraneOperationController.SubmitAction"/> alongside every other
    /// mechanism's case. Extracting each case into its own class means a new mechanism only
    /// needs a new <see cref="IYardCraneMechanism"/> implementation plus one registration
    /// line - the controller and the other mechanisms are untouched. This layer only reshapes
    /// action dispatch; it does not change <see cref="IYardCraneOperationActuator"/> or any
    /// actuator behavior, so it carries no Unity/Rig risk and no actuator interface changes.
    /// </summary>
    public interface IYardCraneMechanism
    {
        YardCraneOperationActionType ActionType { get; }

        /// <summary>
        /// Mirrors the pre-extraction switch case exactly: when
        /// <paramref name="motionCommandsCentered"/> is false the action is rejected with this
        /// mechanism's own interlock code without touching the actuator; otherwise the call is
        /// forwarded to the actuator and its own rejection code (if any) is returned unchanged.
        /// <paramref name="acceptedPhase"/> is only meaningful when this returns true - the
        /// caller falls back to the current phase on rejection, same as before extraction.
        /// </summary>
        bool TryHandle(
            string targetState,
            bool motionCommandsCentered,
            YardCraneOperationPhase currentPhase,
            out string rejectionCode,
            out YardCraneOperationPhase acceptedPhase);
    }

    public sealed class SpreaderLockMechanism : IYardCraneMechanism
    {
        private readonly IYardCraneOperationActuator actuator;

        public SpreaderLockMechanism(IYardCraneOperationActuator actuator)
        {
            this.actuator = actuator ?? throw new ArgumentNullException(nameof(actuator));
        }

        public YardCraneOperationActionType ActionType => YardCraneOperationActionType.SpreaderLock;

        public bool TryHandle(
            string targetState,
            bool motionCommandsCentered,
            YardCraneOperationPhase currentPhase,
            out string rejectionCode,
            out YardCraneOperationPhase acceptedPhase)
        {
            acceptedPhase = YardCraneOperationPhase.LockPending;
            if (!motionCommandsCentered)
            {
                rejectionCode = YardCraneOperationErrorCodes.LockInterlockRejected;
                return false;
            }

            return actuator.TryQueueContainerLock(out rejectionCode);
        }
    }

    public sealed class SpreaderUnlockMechanism : IYardCraneMechanism
    {
        private readonly IYardCraneOperationActuator actuator;

        public SpreaderUnlockMechanism(IYardCraneOperationActuator actuator)
        {
            this.actuator = actuator ?? throw new ArgumentNullException(nameof(actuator));
        }

        public YardCraneOperationActionType ActionType => YardCraneOperationActionType.SpreaderUnlock;

        public bool TryHandle(
            string targetState,
            bool motionCommandsCentered,
            YardCraneOperationPhase currentPhase,
            out string rejectionCode,
            out YardCraneOperationPhase acceptedPhase)
        {
            acceptedPhase = YardCraneOperationPhase.UnlockPending;
            if (!motionCommandsCentered)
            {
                rejectionCode = YardCraneOperationErrorCodes.UnlockInterlockRejected;
                return false;
            }

            return actuator.TryQueueContainerUnlock(out rejectionCode);
        }
    }

    public sealed class ResetMechanism : IYardCraneMechanism
    {
        private readonly IYardCraneOperationActuator actuator;

        public ResetMechanism(IYardCraneOperationActuator actuator)
        {
            this.actuator = actuator ?? throw new ArgumentNullException(nameof(actuator));
        }

        public YardCraneOperationActionType ActionType => YardCraneOperationActionType.Reset;

        public bool TryHandle(
            string targetState,
            bool motionCommandsCentered,
            YardCraneOperationPhase currentPhase,
            out string rejectionCode,
            out YardCraneOperationPhase acceptedPhase)
        {
            acceptedPhase = YardCraneOperationPhase.PreTensioning;
            if (!motionCommandsCentered)
            {
                rejectionCode = YardCraneOperationErrorCodes.ResetInterlockRejected;
                return false;
            }

            return actuator.TryReset(out rejectionCode);
        }
    }

    public sealed class WheelClampMechanism : IYardCraneMechanism
    {
        private readonly IYardCraneOperationActuator actuator;

        public WheelClampMechanism(IYardCraneOperationActuator actuator)
        {
            this.actuator = actuator ?? throw new ArgumentNullException(nameof(actuator));
        }

        public YardCraneOperationActionType ActionType => YardCraneOperationActionType.WheelClampChange;

        public bool TryHandle(
            string targetState,
            bool motionCommandsCentered,
            YardCraneOperationPhase currentPhase,
            out string rejectionCode,
            out YardCraneOperationPhase acceptedPhase)
        {
            acceptedPhase = YardCraneOperationPhase.WheelClampTransition;
            if (!motionCommandsCentered)
            {
                rejectionCode = YardCraneOperationErrorCodes.WheelClampInterlockRejected;
                return false;
            }

            return actuator.TryQueueWheelClamp(targetState == "Clamped", out rejectionCode);
        }
    }

    public sealed class TrolleyAnchorMechanism : IYardCraneMechanism
    {
        private readonly IYardCraneOperationActuator actuator;

        public TrolleyAnchorMechanism(IYardCraneOperationActuator actuator)
        {
            this.actuator = actuator ?? throw new ArgumentNullException(nameof(actuator));
        }

        public YardCraneOperationActionType ActionType => YardCraneOperationActionType.TrolleyAnchorChange;

        public bool TryHandle(
            string targetState,
            bool motionCommandsCentered,
            YardCraneOperationPhase currentPhase,
            out string rejectionCode,
            out YardCraneOperationPhase acceptedPhase)
        {
            acceptedPhase = YardCraneOperationPhase.AnchorTransition;
            if (!motionCommandsCentered)
            {
                rejectionCode = YardCraneOperationErrorCodes.AnchorInterlockRejected;
                return false;
            }

            return actuator.TryQueueTrolleyAnchor(targetState == "Anchored", out rejectionCode);
        }
    }

    public sealed class GantryAnchorMechanism : IYardCraneMechanism
    {
        private readonly IYardCraneOperationActuator actuator;

        public GantryAnchorMechanism(IYardCraneOperationActuator actuator)
        {
            this.actuator = actuator ?? throw new ArgumentNullException(nameof(actuator));
        }

        public YardCraneOperationActionType ActionType => YardCraneOperationActionType.GantryAnchorChange;

        public bool TryHandle(
            string targetState,
            bool motionCommandsCentered,
            YardCraneOperationPhase currentPhase,
            out string rejectionCode,
            out YardCraneOperationPhase acceptedPhase)
        {
            acceptedPhase = YardCraneOperationPhase.AnchorTransition;
            if (!motionCommandsCentered)
            {
                rejectionCode = YardCraneOperationErrorCodes.AnchorInterlockRejected;
                return false;
            }

            return actuator.TryQueueGantryAnchor(targetState == "Anchored", out rejectionCode);
        }
    }

    public sealed class SpreaderPumpMechanism : IYardCraneMechanism
    {
        private readonly IYardCraneOperationActuator actuator;

        public SpreaderPumpMechanism(IYardCraneOperationActuator actuator)
        {
            this.actuator = actuator ?? throw new ArgumentNullException(nameof(actuator));
        }

        public YardCraneOperationActionType ActionType => YardCraneOperationActionType.SpreaderPumpChange;

        public bool TryHandle(
            string targetState,
            bool motionCommandsCentered,
            YardCraneOperationPhase currentPhase,
            out string rejectionCode,
            out YardCraneOperationPhase acceptedPhase)
        {
            // Pump toggling completes instantly - it never owns a dedicated transition phase.
            acceptedPhase = currentPhase;
            if (!motionCommandsCentered)
            {
                rejectionCode = YardCraneOperationErrorCodes.SpreaderPumpInterlockRejected;
                return false;
            }

            return actuator.TryQueueSpreaderPump(targetState == "On", out rejectionCode);
        }
    }

    public sealed class SpreaderSizeMechanism : IYardCraneMechanism
    {
        private readonly IYardCraneOperationActuator actuator;

        public SpreaderSizeMechanism(IYardCraneOperationActuator actuator)
        {
            this.actuator = actuator ?? throw new ArgumentNullException(nameof(actuator));
        }

        public YardCraneOperationActionType ActionType => YardCraneOperationActionType.SpreaderSizeChange;

        public bool TryHandle(
            string targetState,
            bool motionCommandsCentered,
            YardCraneOperationPhase currentPhase,
            out string rejectionCode,
            out YardCraneOperationPhase acceptedPhase)
        {
            acceptedPhase = YardCraneOperationPhase.SpreaderSizeTransition;
            if (!motionCommandsCentered)
            {
                rejectionCode = YardCraneOperationErrorCodes.SpreaderSizeInterlockRejected;
                return false;
            }

            return actuator.TryQueueSpreaderSize(targetState, out rejectionCode);
        }
    }

    public sealed class SpreaderRotationReturnToZeroMechanism : IYardCraneMechanism
    {
        private readonly IYardCraneOperationActuator actuator;

        public SpreaderRotationReturnToZeroMechanism(IYardCraneOperationActuator actuator)
        {
            this.actuator = actuator ?? throw new ArgumentNullException(nameof(actuator));
        }

        public YardCraneOperationActionType ActionType => YardCraneOperationActionType.SpreaderRotationReturnToZero;

        public bool TryHandle(
            string targetState,
            bool motionCommandsCentered,
            YardCraneOperationPhase currentPhase,
            out string rejectionCode,
            out YardCraneOperationPhase acceptedPhase)
        {
            // Return-to-zero completes in the background (see YardCraneSpreaderRotationModel);
            // it never owns a dedicated transition phase either.
            acceptedPhase = currentPhase;
            if (!motionCommandsCentered)
            {
                rejectionCode = YardCraneOperationErrorCodes.SpreaderRotationInterlockRejected;
                return false;
            }

            return actuator.TryQueueSpreaderRotationReturnToZero(out rejectionCode);
        }
    }
}
