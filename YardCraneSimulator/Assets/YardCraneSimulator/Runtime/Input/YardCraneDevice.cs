using System.Runtime.InteropServices;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace PortTraining.YardCraneSimulator.Controls
{
    /// <summary>
    /// A virtual Input System device that YardCraneS7InputSource reports S7-derived state into
    /// from its background WebSocket receive thread - mirroring the existing AnqiaoDevice
    /// pattern already used for the quay crane's real operator console
    /// (Assets/Input/AnqiaoDeviceState.cs). InputSystem.QueueStateEvent is Unity's own
    /// documented thread-safe mechanism for exactly this "background thread reports, main
    /// thread reads" shape.
    ///
    /// Only Gantry/Trolley/EmergencyStopEngaged are actually read back through this device by
    /// YardCraneS7InputSource - the 12 momentary buttons are reported here too (so the device's
    /// state is a complete, honest mirror of what the S7 link last carried, visible in Unity's
    /// Input Debugger), but their press-once/consume-once semantics stay on
    /// YardCraneS7InputSource's own already-tested accumulator rather than this device's raw
    /// per-broadcast button levels, to avoid re-deriving edge-detection timing this device's
    /// event-queue behavior has not been Play-tested against.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct YardCraneDeviceState : IInputStateTypeInfo
    {
        public FourCC format => new FourCC('Y', 'C', 'D', 'S');

        [FieldOffset(0)]
        [InputControl(name = "Gantry", layout = "Axis")]
        public float Gantry;

        [FieldOffset(4)]
        [InputControl(name = "Trolley", layout = "Axis")]
        public float Trolley;

        [FieldOffset(8)]
        [InputControl(name = "ContainerLock", layout = "Button")]
        public bool ContainerLock;

        [FieldOffset(9)]
        [InputControl(name = "ContainerUnlock", layout = "Button")]
        public bool ContainerUnlock;

        [FieldOffset(10)]
        [InputControl(name = "Pause", layout = "Button")]
        public bool Pause;

        [FieldOffset(11)]
        [InputControl(name = "WheelClampToggle", layout = "Button")]
        public bool WheelClampToggle;

        [FieldOffset(12)]
        [InputControl(name = "ControlPowerToggle", layout = "Button")]
        public bool ControlPowerToggle;

        [FieldOffset(13)]
        [InputControl(name = "FaultReset", layout = "Button")]
        public bool FaultReset;

        [FieldOffset(14)]
        [InputControl(name = "GantryAnchorToggle", layout = "Button")]
        public bool GantryAnchorToggle;

        [FieldOffset(15)]
        [InputControl(name = "TrolleyAnchorToggle", layout = "Button")]
        public bool TrolleyAnchorToggle;

        [FieldOffset(16)]
        [InputControl(name = "SpreaderPumpToggle", layout = "Button")]
        public bool SpreaderPumpToggle;

        [FieldOffset(17)]
        [InputControl(name = "SpreaderSizeFt20", layout = "Button")]
        public bool SpreaderSizeFt20;

        [FieldOffset(18)]
        [InputControl(name = "SpreaderSizeFt40", layout = "Button")]
        public bool SpreaderSizeFt40;

        [FieldOffset(19)]
        [InputControl(name = "SpreaderReturnToZero", layout = "Button")]
        public bool SpreaderReturnToZero;

        // Reported as the raw level received from the link (True = engaged) - the fail-safe
        // "unknown link => treat as engaged" behavior lives in YardCraneS7InputSource, not here;
        // this struct only ever carries what was actually received (or a deliberate SafeState()
        // below for when the link is known to be down).
        [FieldOffset(20)]
        [InputControl(name = "EmergencyStopEngaged", layout = "Button")]
        public bool EmergencyStopEngaged;

        /// <summary>All axes centered, all buttons false, emergency stop reported engaged - used
        /// when the link is lost so anything inspecting this device directly (e.g. Unity's Input
        /// Debugger) never shows stale "still connected" values.</summary>
        public static YardCraneDeviceState SafeState()
        {
            return new YardCraneDeviceState { EmergencyStopEngaged = true };
        }
    }

    [InputControlLayout(displayName = "Yard Crane S7 Link", stateType = typeof(YardCraneDeviceState))]
    public class YardCraneDevice : InputDevice
    {
        public AxisControl Gantry { get; private set; }
        public AxisControl Trolley { get; private set; }
        public ButtonControl ContainerLock { get; private set; }
        public ButtonControl ContainerUnlock { get; private set; }
        public ButtonControl Pause { get; private set; }
        public ButtonControl WheelClampToggle { get; private set; }
        public ButtonControl ControlPowerToggle { get; private set; }
        public ButtonControl FaultReset { get; private set; }
        public ButtonControl GantryAnchorToggle { get; private set; }
        public ButtonControl TrolleyAnchorToggle { get; private set; }
        public ButtonControl SpreaderPumpToggle { get; private set; }
        public ButtonControl SpreaderSizeFt20 { get; private set; }
        public ButtonControl SpreaderSizeFt40 { get; private set; }
        public ButtonControl SpreaderReturnToZero { get; private set; }
        public ButtonControl EmergencyStopEngaged { get; private set; }

        protected override void FinishSetup()
        {
            base.FinishSetup();
            Gantry = GetChildControl<AxisControl>("Gantry");
            Trolley = GetChildControl<AxisControl>("Trolley");
            ContainerLock = GetChildControl<ButtonControl>("ContainerLock");
            ContainerUnlock = GetChildControl<ButtonControl>("ContainerUnlock");
            Pause = GetChildControl<ButtonControl>("Pause");
            WheelClampToggle = GetChildControl<ButtonControl>("WheelClampToggle");
            ControlPowerToggle = GetChildControl<ButtonControl>("ControlPowerToggle");
            FaultReset = GetChildControl<ButtonControl>("FaultReset");
            GantryAnchorToggle = GetChildControl<ButtonControl>("GantryAnchorToggle");
            TrolleyAnchorToggle = GetChildControl<ButtonControl>("TrolleyAnchorToggle");
            SpreaderPumpToggle = GetChildControl<ButtonControl>("SpreaderPumpToggle");
            SpreaderSizeFt20 = GetChildControl<ButtonControl>("SpreaderSizeFt20");
            SpreaderSizeFt40 = GetChildControl<ButtonControl>("SpreaderSizeFt40");
            SpreaderReturnToZero = GetChildControl<ButtonControl>("SpreaderReturnToZero");
            EmergencyStopEngaged = GetChildControl<ButtonControl>("EmergencyStopEngaged");
        }

        /// <summary>Thread-safe: InputSystem.QueueStateEvent is Unity's documented mechanism for
        /// reporting device state from a background thread (see YardCraneS7InputSource, which
        /// calls this from its WebSocket client's receive thread).</summary>
        public void ReportState(YardCraneDeviceState state)
        {
            InputSystem.QueueStateEvent(this, state);
        }

        public static YardCraneDevice Create()
        {
            return InputSystem.AddDevice<YardCraneDevice>();
        }

        public void DestroyDevice()
        {
            InputSystem.RemoveDevice(this);
        }
    }
}
