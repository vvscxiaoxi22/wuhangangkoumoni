using System;
using System.Collections.Generic;

namespace PortTraining.YardCraneSimulator.Controls
{
    /// <summary>
    /// Selects one input authority at a time. A source change is permitted only
    /// once the current source has no pending intent at all (centered axes and no
    /// discrete press, safety or otherwise).
    /// </summary>
    public sealed class YardCraneInputRouter
    {
        private string activeSourceId = string.Empty;
        private long sequence;

        public string ActiveSourceId => activeSourceId;

        public YardCraneInputRouteResult Route(IReadOnlyList<YardCraneInputSnapshot> snapshots, long capturedAtMs)
        {
            if (snapshots == null)
            {
                throw new ArgumentNullException(nameof(snapshots));
            }

            YardCraneInputSnapshot active = default;
            var activeFound = false;
            for (var index = 0; index < snapshots.Count; index++)
            {
                if (string.Equals(snapshots[index].SourceId, activeSourceId, StringComparison.Ordinal))
                {
                    active = snapshots[index];
                    activeFound = true;
                    break;
                }
            }

            if (activeSourceId.Length > 0 && (!activeFound || !active.IsConnected))
            {
                activeSourceId = string.Empty;
                return new YardCraneInputRouteResult(YardCraneOperatorCommand.Zero(++sequence, capturedAtMs), true);
            }

            if (activeSourceId.Length == 0)
            {
                if (!TryFindIntent(snapshots, string.Empty, out active))
                {
                    return new YardCraneInputRouteResult(YardCraneOperatorCommand.Zero(++sequence, capturedAtMs), false);
                }

                activeSourceId = active.SourceId;
            }
            // A source change is only permitted once the current source is fully idle: centered
            // axes are not enough, because a discrete safety intent (fault reset, e-stop toggle,
            // control-power toggle) or any other business intent can still be pending on an
            // already-centered command and must not be dropped by handing authority away.
            else if (!active.HasIntent && TryFindIntent(snapshots, activeSourceId, out var requested))
            {
                active = requested;
                activeSourceId = requested.SourceId;
            }

            return new YardCraneInputRouteResult(
                new YardCraneOperatorCommand(
                    active.SourceId,
                    ++sequence,
                    capturedAtMs,
                    active.GantryAxis,
                    active.TrolleyAxis,
                    active.HoistAxis,
                    active.HornHeld,
                    active.LockPressed,
                    active.UnlockPressed,
                    active.PausePressed,
                    active.WheelClampTogglePressed,
                    active.ControlPowerTogglePressed,
                    active.EmergencyStopTogglePressed,
                    active.FaultResetPressed,
                    active.TrolleyAnchorTogglePressed,
                    active.GantryAnchorTogglePressed,
                    active.SpreaderPumpTogglePressed,
                    active.SpreaderSizeFt20Pressed,
                    active.SpreaderSizeFt40Pressed,
                    active.SpreaderRotationAxis,
                    active.SpreaderReturnToZeroPressed),
                false);
        }

        public void Reset()
        {
            activeSourceId = string.Empty;
        }

        private static bool TryFindIntent(
            IReadOnlyList<YardCraneInputSnapshot> snapshots,
            string excludedSourceId,
            out YardCraneInputSnapshot snapshot)
        {
            for (var index = 0; index < snapshots.Count; index++)
            {
                var candidate = snapshots[index];
                if (candidate.IsConnected && candidate.HasIntent &&
                    !string.Equals(candidate.SourceId, excludedSourceId, StringComparison.Ordinal))
                {
                    snapshot = candidate;
                    return true;
                }
            }

            snapshot = default;
            return false;
        }
    }
}
