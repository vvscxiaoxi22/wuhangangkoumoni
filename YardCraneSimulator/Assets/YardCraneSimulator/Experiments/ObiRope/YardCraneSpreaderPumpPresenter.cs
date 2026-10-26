using PortTraining.YardCraneSimulator.Operations;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Experiments.RopePhysics
{
    /// <summary>
    /// Owns the spreader pump's on/off state, actuator-interface forwarding and HUD
    /// presentation - the second mechanism extracted out of
    /// <see cref="YardCraneEightRopeMaxPrototypeRig"/>, following the same pattern as
    /// <see cref="YardCraneWheelClampPresenter"/>.
    ///
    /// Unlike wheel clamp, this state is also read as an interlock precondition by the
    /// spreader size and spreader rotation mechanisms (real hydraulic pressure gates both).
    /// Those call sites read <see cref="IsOn"/> directly; this class does not need to know
    /// about them.
    /// </summary>
    internal sealed class YardCraneSpreaderPumpPresenter
    {
        private readonly Object logContext;

        public YardCraneSpreaderPumpPresenter(Object logContext)
        {
            this.logContext = logContext;
        }

        public bool IsOn { get; private set; }

        public void Reset()
        {
            IsOn = false;
        }

        /// <param name="mechanismReady">Caller-computed <c>!setupFailed &amp;&amp; loopsReady</c>.</param>
        public bool TryQueue(bool requestedOn, bool mechanismReady, out string rejectionCode)
        {
            if (!mechanismReady)
            {
                rejectionCode = YardCraneOperationErrorCodes.MechanismNotReady;
                return false;
            }

            rejectionCode = YardCraneOperationErrorCodes.None;
            if (IsOn == requestedOn)
            {
                return true;
            }

            IsOn = requestedOn;
            Debug.Log($"[YC_SPREADER_PUMP_REQUESTED] target={(requestedOn ? "On" : "Off")}", logContext);
            return true;
        }

        public string BuildStatusLine()
        {
            var label = IsOn ? "已启动" : "已停止";
            var color = IsOn
                ? YardCraneEightRopeMaxPrototypeRig.KeyStatusColorGreen
                : YardCraneEightRopeMaxPrototypeRig.KeyStatusColorNeutral;
            return $"吊具泵：{YardCraneEightRopeMaxPrototypeRig.Colorize(label, color)}";
        }
    }
}
