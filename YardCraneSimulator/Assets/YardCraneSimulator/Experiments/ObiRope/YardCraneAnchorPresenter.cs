using PortTraining.YardCraneSimulator.Crane;
using PortTraining.YardCraneSimulator.Operations;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Experiments.RopePhysics
{
    /// <summary>
    /// Owns one storm-anchor mechanism's actuator-interface forwarding and HUD presentation -
    /// instantiated twice (trolley, gantry) since the two anchors are physically independent of
    /// each other and of the wheel clamp (confirmed against the physical console, see the
    /// 2026-09-02 discussion record). Third mechanism extracted out of
    /// <see cref="YardCraneEightRopeMaxPrototypeRig"/>, following the same boundary as
    /// <see cref="YardCraneWheelClampPresenter"/>: does NOT own the FixedUpdate-embedded
    /// inhibit-detection/logging inside UpdateManualGantryMotion/UpdateManualTrolleyMotion
    /// (tightly coupled to each axis's own physics step each frame) or the multi-mechanism
    /// reset-stationary gate in TryResetPrototype.
    /// </summary>
    internal sealed class YardCraneAnchorPresenter
    {
        private readonly YardCraneAnchorInterlock interlock;
        private readonly string hudLabel;
        private readonly string logTag;
        private readonly Object logContext;

        public YardCraneAnchorPresenter(
            YardCraneAnchorInterlock interlock, string hudLabel, string logTag, Object logContext)
        {
            this.interlock = interlock;
            this.hudLabel = hudLabel;
            this.logTag = logTag;
            this.logContext = logContext;
        }

        public YardCraneAnchorState State => interlock?.State ?? YardCraneAnchorState.Unknown;
        public bool IsApplied => interlock != null && interlock.State == YardCraneAnchorState.Anchored;
        public bool IsTransitioning =>
            interlock != null && interlock.State == YardCraneAnchorState.Transitioning;

        public void Reset()
        {
            interlock?.Reset();
        }

        /// <param name="mechanismReady">
        /// Caller-computed readiness (own interlock and any axis drive it depends on are
        /// non-null, !setupFailed, loopsReady) - kept as a parameter so this class does not need
        /// to know about the Rig's broader startup/fault state or which axis it belongs to.
        /// </param>
        public bool TryQueue(
            bool anchorApplied,
            bool axisIsStationary,
            bool mechanismReady,
            float axisVelocityMetersPerSecond,
            out string rejectionCode)
        {
            if (interlock == null || !mechanismReady)
            {
                rejectionCode = YardCraneOperationErrorCodes.MechanismNotReady;
                return false;
            }

            if (!interlock.TryRequestAnchor(anchorApplied, axisIsStationary, out var driveError))
            {
                rejectionCode = YardCraneOperationErrorCodes.AnchorInterlockRejected;
                Debug.LogWarning(
                    $"[YC_{logTag}_ANCHOR_REJECTED] target={(anchorApplied ? "Anchored" : "Released")}, " +
                    $"state={interlock.State}, speed={axisVelocityMetersPerSecond:F3}m/s, reason={driveError}",
                    logContext);
                return false;
            }

            rejectionCode = YardCraneOperationErrorCodes.None;
            Debug.Log(
                $"[YC_{logTag}_ANCHOR_REQUESTED] target={(anchorApplied ? "Anchored" : "Released")}, " +
                $"state={interlock.State}",
                logContext);
            return true;
        }

        public string BuildStatusLine()
        {
            var state = State;
            var color = StatusColor(state);
            return $"{hudLabel}：{YardCraneEightRopeMaxPrototypeRig.Colorize(TranslateState(state), color)}";
        }

        /// <summary>
        /// Single source of truth for the Chinese label of each <see cref="YardCraneAnchorState"/>
        /// - no English enum value is ever shown to the user.
        /// </summary>
        public static string TranslateState(YardCraneAnchorState state)
        {
            switch (state)
            {
                case YardCraneAnchorState.Released:
                    return "已释放";
                case YardCraneAnchorState.Transitioning:
                    return "动作中";
                case YardCraneAnchorState.Anchored:
                    return "已锚定";
                case YardCraneAnchorState.Fault:
                    return "故障";
                default:
                    return "状态未知";
            }
        }

        private static string StatusColor(YardCraneAnchorState state)
        {
            switch (state)
            {
                case YardCraneAnchorState.Released:
                    return YardCraneEightRopeMaxPrototypeRig.KeyStatusColorGreen;
                case YardCraneAnchorState.Transitioning:
                    return YardCraneEightRopeMaxPrototypeRig.KeyStatusColorAmber;
                case YardCraneAnchorState.Anchored:
                    return YardCraneEightRopeMaxPrototypeRig.KeyStatusColorNeutral;
                case YardCraneAnchorState.Fault:
                    return YardCraneEightRopeMaxPrototypeRig.KeyStatusColorRed;
                default:
                    return YardCraneEightRopeMaxPrototypeRig.KeyStatusColorRed;
            }
        }
    }
}
