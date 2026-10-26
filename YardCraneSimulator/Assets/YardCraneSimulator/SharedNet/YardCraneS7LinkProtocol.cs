using System;
using System.Globalization;

namespace PortTraining.YardCraneSimulator.SharedNet
{
    /// <summary>
    /// The 12 momentary (edge-triggered, "pressed once") discrete PLC buttons this link
    /// carries - confirmed with the field team 2026-09-16 as momentary, unlike emergency stop
    /// (see YardCraneS7LinkFrame.EmergencyStopEngaged, which is a maintained level and
    /// deliberately NOT part of this set). Bit position = enum value; must stay stable once any
    /// real PLC point table references it, and must stay <= 31 entries to fit the wire mask.
    /// Lives here (not in Runtime's YardCraneOperatorIntent) so the standalone S7Integration
    /// player - which must not depend on the Runtime assembly - can still name these signals
    /// without pulling in the rest of the crane's input stack.
    /// </summary>
    public enum YardCraneS7MomentarySignal
    {
        ContainerLock = 0,
        ContainerUnlock = 1,
        Pause = 2,
        WheelClampToggle = 3,
        ControlPowerToggle = 4,
        FaultReset = 5,
        GantryAnchorToggle = 6,
        TrolleyAnchorToggle = 7,
        SpreaderPumpToggle = 8,
        SpreaderSizeFt20 = 9,
        SpreaderSizeFt40 = 10,
        SpreaderReturnToZero = 11
    }

    /// <summary>
    /// Wire format shared by the standalone YardCraneS7Integration player (server) and the
    /// main training app (client). Deliberately not JSON: this assembly has
    /// <c>noEngineReferences</c> set so it can be compiled and unit-tested outside Unity, and a
    /// hand-rolled delimited line format needs no dependency beyond the .NET base class library.
    /// </summary>
    public readonly struct YardCraneS7LinkFrame
    {
        public YardCraneS7LinkFrame(
            long sequence,
            float gantryNormalized,
            float trolleyNormalized,
            bool valid,
            int momentaryPressedMask,
            bool emergencyStopEngaged)
        {
            Sequence = sequence;
            GantryNormalized = gantryNormalized;
            TrolleyNormalized = trolleyNormalized;
            Valid = valid;
            MomentaryPressedMask = momentaryPressedMask;
            EmergencyStopEngaged = emergencyStopEngaged;
        }

        public long Sequence { get; }
        public float GantryNormalized { get; }
        public float TrolleyNormalized { get; }
        public bool Valid { get; }

        /// <summary>Bitmask over YardCraneS7MomentarySignal: which buttons were newly pressed
        /// since the previous broadcast (edge, already detected server-side) - accumulated, not
        /// sampled, so a press that happens between two broadcast ticks is never lost.</summary>
        public int MomentaryPressedMask { get; }

        /// <summary>Maintained level, not an edge/toggle: true for exactly as long as the real
        /// e-stop is physically engaged. Deliberately separate from MomentaryPressedMask - a
        /// dropped frame must never let the toggle-style edge bookkeeping drift out of sync with
        /// a safety circuit; every frame simply restates the current truth.</summary>
        public bool EmergencyStopEngaged { get; }

        public bool HasMomentarySignal(YardCraneS7MomentarySignal signal) =>
            (MomentaryPressedMask & (1 << (int)signal)) != 0;

        public static YardCraneS7LinkFrame SafeZero(long sequence)
        {
            // Safe-zero still reports EmergencyStopEngaged=true, not false: an unknown/invalid
            // link state must fail toward "treat e-stop as engaged", never toward "assume it is
            // released" - that asymmetry is the whole point of treating e-stop specially.
            return new YardCraneS7LinkFrame(sequence, 0f, 0f, false, 0, true);
        }
    }

    public static class YardCraneS7LinkProtocol
    {
        // The main app currently only ever runs one local training session, and the S7 player
        // is meant to be a single always-on process on the same machine - one fixed loopback
        // port is enough; nothing here claims to support multiple concurrent PLC links.
        public const int DefaultPort = 47812;
        public const string HandshakePath = "/yc-s7-link";
        private const string FramePrefix = "YCS7";
        // Bumped from 1 -> 2 when the momentary-signal mask and emergency-stop level were added;
        // TryDecode rejects any other version outright rather than guessing at a partial parse.
        private const int FrameVersion = 2;
        private const char Delimiter = '|';

        public static string Encode(in YardCraneS7LinkFrame frame)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}{8}{1}{8}{2}{8}{3:F4}{8}{4:F4}{8}{5}{8}{6}{8}{7}",
                FramePrefix,
                FrameVersion,
                frame.Sequence,
                frame.GantryNormalized,
                frame.TrolleyNormalized,
                frame.Valid ? 1 : 0,
                frame.MomentaryPressedMask,
                frame.EmergencyStopEngaged ? 1 : 0,
                Delimiter);
        }

        public static bool TryDecode(string line, out YardCraneS7LinkFrame frame)
        {
            frame = default;
            if (string.IsNullOrEmpty(line))
            {
                return false;
            }

            var parts = line.Split(Delimiter);
            if (parts.Length != 8 ||
                !string.Equals(parts[0], FramePrefix, StringComparison.Ordinal) ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var version) ||
                version != FrameVersion ||
                !long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sequence) ||
                !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var gantry) ||
                !float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var trolley) ||
                !int.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var validFlag) ||
                !int.TryParse(parts[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out var momentaryMask) ||
                !int.TryParse(parts[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out var emergencyStopFlag))
            {
                return false;
            }

            frame = new YardCraneS7LinkFrame(
                sequence, gantry, trolley, validFlag != 0, momentaryMask, emergencyStopFlag != 0);
            return true;
        }
    }
}
