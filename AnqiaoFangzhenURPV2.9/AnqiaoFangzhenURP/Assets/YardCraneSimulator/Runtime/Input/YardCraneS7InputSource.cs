using System.Collections.Generic;
using PortTraining.YardCraneSimulator.SharedNet;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Controls
{
    /// <summary>
    /// Reads gantry/trolley commands, the 12 momentary discrete buttons and the maintained
    /// emergency-stop level relayed by the independent YardCraneS7Integration player over the
    /// loopback WebSocket link (see YardCraneS7CommandWebSocketServer on that side). This class
    /// holds no PLC/S7 connection itself and never will - it only ever consumes values the S7
    /// player has already limited, normalized and safety-zeroed (see
    /// 场桥S7直连接入架构决策-2026-09-15.md). Hoist, horn and spreader-rotation are not part of
    /// the current PLC point table, so this source reports them centered/false.
    ///
    /// Every received frame is also mirrored into a virtual YardCraneDevice
    /// (InputSystem.QueueStateEvent) purely for observability - the same pattern already used
    /// for the quay crane's real operator console (Assets/Input/AnqiaoDeviceState.cs) - so the
    /// current PLC-derived values are visible in Unity's Input Debugger. Deliberately NOT the
    /// source of truth this class reads back from, though: creating/reporting into a real
    /// InputSystem device requires Unity's own runtime and cannot execute outside it (confirmed
    /// 2026-09-16 - Tools/S7LinkHarness's real-socket tests threw
    /// FileNotFoundException(Unity.InputSystem) the moment device creation ran in a plain .NET
    /// process). Making Sample()/EmergencyStopEngaged depend on reading the device back would
    /// have made this class impossible to verify anywhere except live Unity Play, for a class
    /// whose correctness (fail-safe emergency stop, consume-once button semantics) is exactly
    /// what benefits most from being testable against real sockets outside Unity. So the
    /// lock-protected fields below remain the actual source of truth; ReportState is best-effort
    /// only and its failure (e.g. outside Unity) must never affect this class's own behavior.
    /// </summary>
    internal sealed class YardCraneS7InputSource : IYardCraneInputSource, System.IDisposable
    {
        public const string Id = "S7_PLC_LINK";

        // If no frame has arrived within this window (matching the philosophy of the S7
        // adapter's own 0.5s communication timeout on the other end of the link), treat the
        // link as stale rather than keep replaying the last value forever - a TCP connection
        // going quietly dead without the socket itself reporting an error is exactly the
        // failure mode this guards against.
        private const float StaleAfterSeconds = 0.5f;

        private readonly object stateLock = new();
        private YardCraneMiniWebSocketClient client;
        private YardCraneDevice device;
        private float lastFrameGantry;
        private float lastFrameTrolley;
        private bool lastFrameValid;
        private bool lastFrameEmergencyStopEngaged = true; // fail safe until a real frame arrives
        private int pendingMomentaryMask;
        private float lastFrameReceivedAtRealtime = -1f;
        private bool linkLost;

        public bool IsConnected => client != null && client.IsConnected;

        /// <summary>
        /// The maintained emergency-stop level, read directly rather than through
        /// Sample()/YardCraneInputSnapshot's intent list - see the design note on
        /// YardCraneS7LinkFrame.EmergencyStopEngaged for why a toggle/edge intent is unsafe for
        /// a maintained signal. Fails toward true (engaged) whenever the link itself is not
        /// currently trustworthy (no frame yet, stale, or disconnected) - never toward false.
        /// YardCraneStandaloneOperationHost reads this directly when s7Exclusive is active.
        /// </summary>
        public bool EmergencyStopEngaged
        {
            get
            {
                bool engaged;
                bool haveFrame;
                bool lost;
                float age;
                lock (stateLock)
                {
                    engaged = lastFrameEmergencyStopEngaged;
                    haveFrame = lastFrameReceivedAtRealtime >= 0f;
                    lost = linkLost;
                    age = haveFrame ? Time.realtimeSinceStartup - lastFrameReceivedAtRealtime : float.PositiveInfinity;
                }

                if (client == null || !client.IsConnected || lost || !haveFrame || age > StaleAfterSeconds)
                {
                    return true;
                }

                return engaged;
            }
        }

        /// <summary>
        /// Blocking connect with a bounded timeout - called once during training-session setup,
        /// where the caller needs a definite yes/no before deciding whether a run may start at
        /// all (see YardCraneStandaloneOperationHost.Configure). Not intended to be retried
        /// every frame.
        /// </summary>
        public bool TryConnect(string host, int port, int timeoutMs, out string error)
        {
            client = new YardCraneMiniWebSocketClient();
            client.TextReceived += HandleTextReceived;
            client.Disconnected += HandleDisconnected;

            if (client.TryConnect(host, port, timeoutMs, out error))
            {
                TryCreateMirrorDevice();
                return true;
            }

            client.TextReceived -= HandleTextReceived;
            client.Disconnected -= HandleDisconnected;
            client = null;
            return false;
        }

        public void Dispose()
        {
            if (client == null)
            {
                return;
            }

            client.TextReceived -= HandleTextReceived;
            client.Disconnected -= HandleDisconnected;
            client.Dispose();
            client = null;

            try
            {
                device?.DestroyDevice();
            }
            catch (System.Exception)
            {
                // Best-effort only - see TryCreateMirrorDevice.
            }

            device = null;
        }

        // Runs on the WebSocket client's background receive thread - the lock-protected fields
        // are this class's actual source of truth (see the class comment for why); the device
        // mirror below is best-effort observability only and must never be allowed to affect
        // this method's real work if it throws (e.g. outside a live Unity process).
        private void HandleTextReceived(string text)
        {
            if (!YardCraneS7LinkProtocol.TryDecode(text, out var frame))
            {
                return;
            }

            lock (stateLock)
            {
                lastFrameGantry = frame.GantryNormalized;
                lastFrameTrolley = frame.TrolleyNormalized;
                lastFrameValid = frame.Valid;
                lastFrameEmergencyStopEngaged = frame.EmergencyStopEngaged;
                // OR-accumulate, matching the Bridge's own accumulator: if two frames arrive
                // between two Sample() calls, neither one's presses may be dropped.
                pendingMomentaryMask |= frame.MomentaryPressedMask;
                lastFrameReceivedAtRealtime = Time.realtimeSinceStartup;
            }

            TryMirrorState(frame);
        }

        private void HandleDisconnected()
        {
            lock (stateLock)
            {
                linkLost = true;
                lastFrameEmergencyStopEngaged = true; // fail safe immediately, don't wait for the next poll
            }

            TryMirrorSafeState();
        }

        private void TryCreateMirrorDevice()
        {
            try
            {
                device = YardCraneDevice.Create();
                device.ReportState(YardCraneDeviceState.SafeState());
            }
            catch (System.Exception)
            {
                // Best-effort observability only - a real Unity process should never hit this,
                // but this class's actual behavior must not depend on it succeeding.
                device = null;
            }
        }

        private void TryMirrorState(in YardCraneS7LinkFrame frame)
        {
            if (device == null)
            {
                return;
            }

            try
            {
                device.ReportState(new YardCraneDeviceState
                {
                    Gantry = frame.GantryNormalized,
                    Trolley = frame.TrolleyNormalized,
                    ContainerLock = frame.HasMomentarySignal(YardCraneS7MomentarySignal.ContainerLock),
                    ContainerUnlock = frame.HasMomentarySignal(YardCraneS7MomentarySignal.ContainerUnlock),
                    Pause = frame.HasMomentarySignal(YardCraneS7MomentarySignal.Pause),
                    WheelClampToggle = frame.HasMomentarySignal(YardCraneS7MomentarySignal.WheelClampToggle),
                    ControlPowerToggle = frame.HasMomentarySignal(YardCraneS7MomentarySignal.ControlPowerToggle),
                    FaultReset = frame.HasMomentarySignal(YardCraneS7MomentarySignal.FaultReset),
                    GantryAnchorToggle = frame.HasMomentarySignal(YardCraneS7MomentarySignal.GantryAnchorToggle),
                    TrolleyAnchorToggle = frame.HasMomentarySignal(YardCraneS7MomentarySignal.TrolleyAnchorToggle),
                    SpreaderPumpToggle = frame.HasMomentarySignal(YardCraneS7MomentarySignal.SpreaderPumpToggle),
                    SpreaderSizeFt20 = frame.HasMomentarySignal(YardCraneS7MomentarySignal.SpreaderSizeFt20),
                    SpreaderSizeFt40 = frame.HasMomentarySignal(YardCraneS7MomentarySignal.SpreaderSizeFt40),
                    SpreaderReturnToZero = frame.HasMomentarySignal(YardCraneS7MomentarySignal.SpreaderReturnToZero),
                    EmergencyStopEngaged = frame.EmergencyStopEngaged
                });
            }
            catch (System.Exception)
            {
                // Best-effort only - see TryCreateMirrorDevice.
            }
        }

        private void TryMirrorSafeState()
        {
            try
            {
                device?.ReportState(YardCraneDeviceState.SafeState());
            }
            catch (System.Exception)
            {
                // Best-effort only - see TryCreateMirrorDevice.
            }
        }

        public YardCraneInputSnapshot Sample(YardCraneInputProfile profile)
        {
            if (client == null)
            {
                return new YardCraneInputSnapshot(Id, false, 0f, 0f, 0f, false);
            }

            float gantry;
            float trolley;
            bool valid;
            bool haveFrame;
            bool lost;
            float age;
            int momentaryMask;
            lock (stateLock)
            {
                gantry = lastFrameGantry;
                trolley = lastFrameTrolley;
                valid = lastFrameValid;
                haveFrame = lastFrameReceivedAtRealtime >= 0f;
                lost = linkLost;
                age = haveFrame ? Time.realtimeSinceStartup - lastFrameReceivedAtRealtime : float.PositiveInfinity;
                // Consume, not peek: each newly-pressed button is reported for exactly one
                // Sample() call, matching the keyboard/Xbox sources' wasPressedThisFrame
                // semantics, instead of being held "pressed" for every Update() until the next
                // broadcast arrives.
                momentaryMask = pendingMomentaryMask;
                pendingMomentaryMask = 0;
            }

            // A PLC-side comm timeout or a stale/missing frame is treated the same as the
            // WebSocket socket itself being down (IsConnected=false), not as "connected but
            // zero" - this is deliberately conservative: it escalates through the same
            // sourceLost/NotifyInputUnavailable path YardCraneStandaloneOperationHost already
            // uses for a genuinely disconnected keyboard/gamepad, rather than silently reporting
            // "everything's fine, commanding zero" when the real cause is untrustworthy data.
            var stale = !haveFrame || age > StaleAfterSeconds;
            var connected = client.IsConnected && !lost && !stale && valid;
            if (!connected)
            {
                return new YardCraneInputSnapshot(Id, false, 0f, 0f, 0f, false);
            }

            return new YardCraneInputSnapshot(Id, true, gantry, trolley, 0f, false, 0f, DecodeIntents(momentaryMask));
        }

        private static IEnumerable<YardCraneOperatorIntent> DecodeIntents(int momentaryMask)
        {
            if (momentaryMask == 0)
            {
                return null;
            }

            var intents = new List<YardCraneOperatorIntent>(12);
            foreach (YardCraneS7MomentarySignal signal in System.Enum.GetValues(typeof(YardCraneS7MomentarySignal)))
            {
                if ((momentaryMask & (1 << (int)signal)) != 0)
                {
                    intents.Add(MapMomentarySignal(signal));
                }
            }

            return intents;
        }

        // YardCraneS7MomentarySignal and YardCraneOperatorIntent are deliberately separate
        // enums with DIFFERENT numeric values (YardCraneOperatorIntent has EmergencyStopToggle
        // inserted in the middle, shifting everything after it) - this must stay an explicit
        // mapping, never a direct cast.
        private static YardCraneOperatorIntent MapMomentarySignal(YardCraneS7MomentarySignal signal)
        {
            switch (signal)
            {
                case YardCraneS7MomentarySignal.ContainerLock:
                    return YardCraneOperatorIntent.ContainerLock;
                case YardCraneS7MomentarySignal.ContainerUnlock:
                    return YardCraneOperatorIntent.ContainerUnlock;
                case YardCraneS7MomentarySignal.Pause:
                    return YardCraneOperatorIntent.Pause;
                case YardCraneS7MomentarySignal.WheelClampToggle:
                    return YardCraneOperatorIntent.WheelClampToggle;
                case YardCraneS7MomentarySignal.ControlPowerToggle:
                    return YardCraneOperatorIntent.ControlPowerToggle;
                case YardCraneS7MomentarySignal.FaultReset:
                    return YardCraneOperatorIntent.FaultReset;
                case YardCraneS7MomentarySignal.GantryAnchorToggle:
                    return YardCraneOperatorIntent.GantryAnchorToggle;
                case YardCraneS7MomentarySignal.TrolleyAnchorToggle:
                    return YardCraneOperatorIntent.TrolleyAnchorToggle;
                case YardCraneS7MomentarySignal.SpreaderPumpToggle:
                    return YardCraneOperatorIntent.SpreaderPumpToggle;
                case YardCraneS7MomentarySignal.SpreaderSizeFt20:
                    return YardCraneOperatorIntent.SpreaderSizeFt20;
                case YardCraneS7MomentarySignal.SpreaderSizeFt40:
                    return YardCraneOperatorIntent.SpreaderSizeFt40;
                case YardCraneS7MomentarySignal.SpreaderReturnToZero:
                    return YardCraneOperatorIntent.SpreaderReturnToZero;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(signal), signal, null);
            }
        }
    }
}
