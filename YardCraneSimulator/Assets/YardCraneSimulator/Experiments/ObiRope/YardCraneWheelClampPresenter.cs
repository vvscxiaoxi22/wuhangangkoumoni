using PortTraining.YardCraneSimulator.Crane;
using PortTraining.YardCraneSimulator.Operations;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Experiments.RopePhysics
{
    /// <summary>
    /// Owns the wheel-clamp mechanism's actuator-interface forwarding and HUD presentation,
    /// extracted out of <see cref="YardCraneEightRopeMaxPrototypeRig"/> as the first step of
    /// splitting that file's many bundled mechanisms apart (wheel clamp chosen as the template:
    /// simplest logic, most Play-tested this session).
    ///
    /// Deliberately does NOT own:
    /// - The clamp-state-change detection/logging inside UpdateManualGantryMotion. That code
    ///   reads the same YardCraneGantryDriveStep the gantry physics step already produced each
    ///   FixedUpdate, so moving it here would mean either duplicating the step call or passing
    ///   the step result through an extra layer - not worth the risk for a first extraction pass.
    /// - The reset-stationary gate in TryResetPrototype, which ANDs wheel clamp together with
    ///   the anchors and spreader size, so it cannot cleanly belong to one mechanism's class.
    ///
    /// The wheel-clamp state machine itself already lived in the Unity-independent
    /// YardCraneGantryDriveModel before this extraction; this class only moves the Rig-side glue
    /// (actuator interface plumbing + HUD text) around it.
    /// </summary>
    internal sealed class YardCraneWheelClampPresenter
    {
        private readonly YardCraneGantryDriveModel gantryDrive;
        private readonly Object logContext;

        public YardCraneWheelClampPresenter(YardCraneGantryDriveModel gantryDrive, Object logContext)
        {
            this.gantryDrive = gantryDrive;
            this.logContext = logContext;
        }

        public WheelClampState State => gantryDrive?.WheelClampState ?? WheelClampState.Unknown;
        public bool IsClamped => gantryDrive != null && gantryDrive.WheelClampState == WheelClampState.Clamped;
        public bool IsTransitioning =>
            gantryDrive != null && gantryDrive.WheelClampState == WheelClampState.Transitioning;

        /// <param name="mechanismReady">
        /// Caller-computed <c>!setupFailed &amp;&amp; loopsReady</c> - kept as a parameter rather
        /// than fields on this class so the presenter does not need to know about the Rig's
        /// broader startup/fault state, only about the gantry drive it was given.
        /// </param>
        public bool TryQueue(bool clampApplied, bool mechanismReady, out string rejectionCode)
        {
            if (gantryDrive == null || !mechanismReady)
            {
                rejectionCode = YardCraneOperationErrorCodes.MechanismNotReady;
                return false;
            }

            if (!gantryDrive.TryRequestWheelClamp(clampApplied, out var driveError))
            {
                rejectionCode = YardCraneOperationErrorCodes.WheelClampInterlockRejected;
                Debug.LogWarning(
                    $"[YC_WHEEL_CLAMP_REJECTED] target={(clampApplied ? "Clamped" : "Released")}, " +
                    $"state={gantryDrive.WheelClampState}, speed={gantryDrive.VelocityMetersPerSecond:F3}m/s, " +
                    $"reason={driveError}",
                    logContext);
                return false;
            }

            rejectionCode = YardCraneOperationErrorCodes.None;
            Debug.Log(
                $"[YC_WHEEL_CLAMP_REQUESTED] target={(clampApplied ? "Clamped" : "Released")}, " +
                $"state={gantryDrive.WheelClampState}",
                logContext);
            return true;
        }

        public string BuildStatusLine()
        {
            var state = State;
            var color = StatusColor(state);
            return $"夹轮器：{YardCraneEightRopeMaxPrototypeRig.Colorize(TranslateState(state), color)}";
        }

        /// <summary>
        /// Single source of truth for the Chinese label of each <see cref="WheelClampState"/>,
        /// shared by the main debug panel and the key-status panel so no English enum value is
        /// ever shown to the user.
        /// </summary>
        public static string TranslateState(WheelClampState state)
        {
            switch (state)
            {
                case WheelClampState.Released:
                    return "已松开";
                case WheelClampState.Transitioning:
                    return "动作中";
                case WheelClampState.Clamped:
                    return "已夹紧";
                case WheelClampState.Fault:
                    return "故障";
                default:
                    return "状态未知";
            }
        }

        private static string StatusColor(WheelClampState state)
        {
            switch (state)
            {
                case WheelClampState.Released:
                    return YardCraneEightRopeMaxPrototypeRig.KeyStatusColorGreen;
                case WheelClampState.Transitioning:
                    return YardCraneEightRopeMaxPrototypeRig.KeyStatusColorAmber;
                case WheelClampState.Clamped:
                    return YardCraneEightRopeMaxPrototypeRig.KeyStatusColorNeutral;
                case WheelClampState.Fault:
                    return YardCraneEightRopeMaxPrototypeRig.KeyStatusColorRed;
                default:
                    return YardCraneEightRopeMaxPrototypeRig.KeyStatusColorRed;
            }
        }
    }
}
