using PortTraining.YardCraneSimulator.Crane;
using PortTraining.YardCraneSimulator.Operations;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Experiments.RopePhysics
{
    /// <summary>
    /// Owns the spreader 20/40ft size mechanism's actuator-interface forwarding and HUD
    /// presentation - same extraction boundary as <see cref="YardCraneWheelClampPresenter"/>,
    /// <see cref="YardCraneSpreaderPumpPresenter"/> and <see cref="YardCraneAnchorPresenter"/>.
    /// The interlock itself requires the pump-on state as a precondition (real hydraulic
    /// pressure gates the size-change actuators); that value is passed in by the caller rather
    /// than read directly, so this class does not need to know about
    /// <see cref="YardCraneSpreaderPumpPresenter"/>.
    /// </summary>
    internal sealed class YardCraneSpreaderSizePresenter
    {
        private readonly YardCraneSpreaderSizeInterlock interlock;
        private readonly Object logContext;

        public YardCraneSpreaderSizePresenter(YardCraneSpreaderSizeInterlock interlock, Object logContext)
        {
            this.interlock = interlock;
            this.logContext = logContext;
        }

        public YardCraneSpreaderSizeMechanismState State =>
            interlock?.State ?? YardCraneSpreaderSizeMechanismState.Idle;
        public YardCraneSpreaderSize CurrentSize => interlock?.CurrentSize ?? YardCraneSpreaderSize.Unknown;
        public bool IsTransitioning =>
            interlock != null && interlock.State == YardCraneSpreaderSizeMechanismState.Transitioning;

        public void Reset(YardCraneSpreaderSize size)
        {
            interlock?.Reset(size);
        }

        /// <param name="mechanismReady">Caller-computed <c>!setupFailed &amp;&amp; loopsReady</c>.</param>
        public bool TryQueue(
            string targetSize,
            bool pumpOn,
            bool axisIsStationary,
            bool containerLocked,
            bool mechanismReady,
            out string rejectionCode)
        {
            if (interlock == null || !mechanismReady)
            {
                rejectionCode = YardCraneOperationErrorCodes.MechanismNotReady;
                return false;
            }

            if (!TryParseSize(targetSize, out var parsedSize))
            {
                rejectionCode = YardCraneOperationErrorCodes.ContractValidationFailed;
                return false;
            }

            if (!interlock.TryRequestSize(parsedSize, pumpOn, axisIsStationary, containerLocked, out var driveError))
            {
                rejectionCode = YardCraneOperationErrorCodes.SpreaderSizeInterlockRejected;
                Debug.LogWarning(
                    $"[YC_SPREADER_SIZE_REJECTED] target={targetSize}, state={interlock.State}, " +
                    $"currentSize={interlock.CurrentSize}, pumpOn={pumpOn}, " +
                    $"containerLocked={containerLocked}, reason={driveError}",
                    logContext);
                return false;
            }

            rejectionCode = YardCraneOperationErrorCodes.None;
            Debug.Log($"[YC_SPREADER_SIZE_REQUESTED] target={targetSize}, state={interlock.State}", logContext);
            return true;
        }

        public string BuildStatusLine()
        {
            var mechanismState = State;
            if (mechanismState == YardCraneSpreaderSizeMechanismState.Transitioning)
            {
                return $"吊具尺寸：{YardCraneEightRopeMaxPrototypeRig.Colorize("动作中", YardCraneEightRopeMaxPrototypeRig.KeyStatusColorAmber)}";
            }

            var color = mechanismState == YardCraneSpreaderSizeMechanismState.Fault
                ? YardCraneEightRopeMaxPrototypeRig.KeyStatusColorRed
                : YardCraneEightRopeMaxPrototypeRig.KeyStatusColorNeutral;
            return $"吊具尺寸：{YardCraneEightRopeMaxPrototypeRig.Colorize(TranslateSize(CurrentSize), color)}";
        }

        /// <summary>
        /// Single source of truth for the Chinese label of each <see cref="YardCraneSpreaderSize"/>
        /// - no English enum value is ever shown to the user.
        /// </summary>
        public static string TranslateSize(YardCraneSpreaderSize size)
        {
            switch (size)
            {
                case YardCraneSpreaderSize.Ft20:
                    return "20尺";
                case YardCraneSpreaderSize.Ft40:
                    return "40尺";
                default:
                    return "状态未知";
            }
        }

        private static bool TryParseSize(string targetSize, out YardCraneSpreaderSize parsedSize)
        {
            switch (targetSize)
            {
                case "FT20":
                    parsedSize = YardCraneSpreaderSize.Ft20;
                    return true;
                case "FT40":
                    parsedSize = YardCraneSpreaderSize.Ft40;
                    return true;
                default:
                    parsedSize = YardCraneSpreaderSize.Unknown;
                    return false;
            }
        }
    }
}
