using System;

namespace PortTraining.YardCraneSimulator.Controls
{
    /// <summary>
    /// Device-independent operator intent. Motion execution must only consume this
    /// command and must never read keyboard, gamepad or PLC APIs directly.
    /// </summary>
    public readonly struct YardCraneOperatorCommand
    {
        public YardCraneOperatorCommand(
            string sourceId,
            long sequence,
            long capturedAtMs,
            float gantryAxis,
            float trolleyAxis,
            float hoistAxis,
            bool hornHeld,
            bool lockPressed,
            bool unlockPressed,
            bool pausePressed,
            bool wheelClampTogglePressed = false,
            bool controlPowerTogglePressed = false,
            bool emergencyStopTogglePressed = false,
            bool faultResetPressed = false,
            bool trolleyAnchorTogglePressed = false,
            bool gantryAnchorTogglePressed = false,
            bool spreaderPumpTogglePressed = false,
            bool spreaderSizeFt20Pressed = false,
            bool spreaderSizeFt40Pressed = false,
            float spreaderRotationAxis = 0f,
            bool spreaderReturnToZeroPressed = false)
        {
            SourceId = sourceId ?? string.Empty;
            Sequence = sequence;
            CapturedAtMs = capturedAtMs;
            GantryAxis = ClampNormalized(gantryAxis);
            TrolleyAxis = ClampNormalized(trolleyAxis);
            HoistAxis = ClampNormalized(hoistAxis);
            HornHeld = hornHeld;
            LockPressed = lockPressed;
            UnlockPressed = unlockPressed;
            PausePressed = pausePressed;
            WheelClampTogglePressed = wheelClampTogglePressed;
            ControlPowerTogglePressed = controlPowerTogglePressed;
            EmergencyStopTogglePressed = emergencyStopTogglePressed;
            FaultResetPressed = faultResetPressed;
            TrolleyAnchorTogglePressed = trolleyAnchorTogglePressed;
            GantryAnchorTogglePressed = gantryAnchorTogglePressed;
            SpreaderPumpTogglePressed = spreaderPumpTogglePressed;
            SpreaderSizeFt20Pressed = spreaderSizeFt20Pressed;
            SpreaderSizeFt40Pressed = spreaderSizeFt40Pressed;
            SpreaderRotationAxis = ClampNormalized(spreaderRotationAxis);
            SpreaderReturnToZeroPressed = spreaderReturnToZeroPressed;
        }

        public string SourceId { get; }
        public long Sequence { get; }
        public long CapturedAtMs { get; }
        public float GantryAxis { get; }
        public float TrolleyAxis { get; }
        public float HoistAxis { get; }
        public bool HornHeld { get; }
        public bool LockPressed { get; }
        public bool UnlockPressed { get; }
        public bool PausePressed { get; }
        public bool WheelClampTogglePressed { get; }
        public bool ControlPowerTogglePressed { get; }
        public bool EmergencyStopTogglePressed { get; }
        public bool FaultResetPressed { get; }
        public bool TrolleyAnchorTogglePressed { get; }
        public bool GantryAnchorTogglePressed { get; }
        public bool SpreaderPumpTogglePressed { get; }
        public bool SpreaderSizeFt20Pressed { get; }
        public bool SpreaderSizeFt40Pressed { get; }
        public float SpreaderRotationAxis { get; }
        public bool SpreaderReturnToZeroPressed { get; }

        public bool HandlesCentered =>
            Math.Abs(GantryAxis) <= 0.0001f &&
            Math.Abs(TrolleyAxis) <= 0.0001f &&
            Math.Abs(HoistAxis) <= 0.0001f &&
            Math.Abs(SpreaderRotationAxis) <= 0.0001f;

        public bool HasIntent => !HandlesCentered || HornHeld || LockPressed || UnlockPressed ||
                                 PausePressed || WheelClampTogglePressed || ControlPowerTogglePressed ||
                                 EmergencyStopTogglePressed || FaultResetPressed ||
                                 TrolleyAnchorTogglePressed || GantryAnchorTogglePressed ||
                                 SpreaderPumpTogglePressed || SpreaderSizeFt20Pressed ||
                                 SpreaderSizeFt40Pressed || SpreaderReturnToZeroPressed;

        public static YardCraneOperatorCommand Zero(long sequence, long capturedAtMs)
        {
            return new YardCraneOperatorCommand("NONE", sequence, capturedAtMs, 0f, 0f, 0f, false, false, false, false);
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
        public YardCraneInputSnapshot(
            string sourceId,
            bool isConnected,
            float gantryAxis,
            float trolleyAxis,
            float hoistAxis,
            bool hornHeld,
            bool lockPressed,
            bool unlockPressed,
            bool pausePressed,
            bool wheelClampTogglePressed = false,
            bool controlPowerTogglePressed = false,
            bool emergencyStopTogglePressed = false,
            bool faultResetPressed = false,
            bool trolleyAnchorTogglePressed = false,
            bool gantryAnchorTogglePressed = false,
            bool spreaderPumpTogglePressed = false,
            bool spreaderSizeFt20Pressed = false,
            bool spreaderSizeFt40Pressed = false,
            float spreaderRotationAxis = 0f,
            bool spreaderReturnToZeroPressed = false)
        {
            SourceId = sourceId ?? string.Empty;
            IsConnected = isConnected;
            GantryAxis = gantryAxis;
            TrolleyAxis = trolleyAxis;
            HoistAxis = hoistAxis;
            HornHeld = hornHeld;
            LockPressed = lockPressed;
            UnlockPressed = unlockPressed;
            PausePressed = pausePressed;
            WheelClampTogglePressed = wheelClampTogglePressed;
            ControlPowerTogglePressed = controlPowerTogglePressed;
            EmergencyStopTogglePressed = emergencyStopTogglePressed;
            FaultResetPressed = faultResetPressed;
            TrolleyAnchorTogglePressed = trolleyAnchorTogglePressed;
            GantryAnchorTogglePressed = gantryAnchorTogglePressed;
            SpreaderPumpTogglePressed = spreaderPumpTogglePressed;
            SpreaderSizeFt20Pressed = spreaderSizeFt20Pressed;
            SpreaderSizeFt40Pressed = spreaderSizeFt40Pressed;
            SpreaderRotationAxis = spreaderRotationAxis;
            SpreaderReturnToZeroPressed = spreaderReturnToZeroPressed;
        }

        public string SourceId { get; }
        public bool IsConnected { get; }
        public float GantryAxis { get; }
        public float TrolleyAxis { get; }
        public float HoistAxis { get; }
        public bool HornHeld { get; }
        public bool LockPressed { get; }
        public bool UnlockPressed { get; }
        public bool PausePressed { get; }
        public bool WheelClampTogglePressed { get; }
        public bool ControlPowerTogglePressed { get; }
        public bool EmergencyStopTogglePressed { get; }
        public bool FaultResetPressed { get; }
        public bool TrolleyAnchorTogglePressed { get; }
        public bool GantryAnchorTogglePressed { get; }
        public bool SpreaderPumpTogglePressed { get; }
        public bool SpreaderSizeFt20Pressed { get; }
        public bool SpreaderSizeFt40Pressed { get; }
        public float SpreaderRotationAxis { get; }
        public bool SpreaderReturnToZeroPressed { get; }

        public bool HandlesCentered =>
            Math.Abs(GantryAxis) <= 0.0001f &&
            Math.Abs(TrolleyAxis) <= 0.0001f &&
            Math.Abs(HoistAxis) <= 0.0001f &&
            Math.Abs(SpreaderRotationAxis) <= 0.0001f;

        public bool HasIntent => !HandlesCentered || HornHeld || LockPressed || UnlockPressed ||
                                 PausePressed || WheelClampTogglePressed || ControlPowerTogglePressed ||
                                 EmergencyStopTogglePressed || FaultResetPressed ||
                                 TrolleyAnchorTogglePressed || GantryAnchorTogglePressed ||
                                 SpreaderPumpTogglePressed || SpreaderSizeFt20Pressed ||
                                 SpreaderSizeFt40Pressed || SpreaderReturnToZeroPressed;
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
