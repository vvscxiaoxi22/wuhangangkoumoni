using System;
using System.Globalization;

namespace PortTraining.YardCraneSimulator.Training
{
    /// <summary>
    /// A scene-space interval that the currently selected greybox rig can reach. The values
    /// use the same coordinate frame as <see cref="YardCraneLocalTrainingTarget.Value"/>.
    /// </summary>
    public readonly struct YardCraneLocalReachableRange
    {
        public YardCraneLocalReachableRange(double minimum, double maximum)
        {
            if (!IsFinite(minimum) || !IsFinite(maximum) || maximum < minimum)
                throw new ArgumentOutOfRangeException(nameof(maximum));

            Minimum = minimum;
            Maximum = maximum;
        }

        public double Minimum { get; }
        public double Maximum { get; }

        public bool Contains(double value)
        {
            return IsFinite(value) && value >= Minimum && value <= Maximum;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    /// <summary>
    /// Reachability expressed in the frozen context's explicit coordinate frames. This type
    /// deliberately has no Unity dependency so target-policy tests can run outside Play Mode.
    /// </summary>
    public readonly struct YardCraneLocalTrainingReachability
    {
        public YardCraneLocalTrainingReachability(
            YardCraneLocalReachableRange gantryHomeRelativeMeters,
            YardCraneLocalReachableRange trolleyLocalXMeters,
            YardCraneLocalReachableRange spreaderSceneWorldYMeters,
            YardCraneLocalReachableRange spreaderStartRelativeMeters,
            YardCraneLocalReachableRange spreaderPhysicalRotationDegrees)
        {
            GantryHomeRelativeMeters = gantryHomeRelativeMeters;
            TrolleyLocalXMeters = trolleyLocalXMeters;
            SpreaderSceneWorldYMeters = spreaderSceneWorldYMeters;
            SpreaderStartRelativeMeters = spreaderStartRelativeMeters;
            SpreaderPhysicalRotationDegrees = spreaderPhysicalRotationDegrees;
        }

        public YardCraneLocalReachableRange GantryHomeRelativeMeters { get; }
        public YardCraneLocalReachableRange TrolleyLocalXMeters { get; }
        public YardCraneLocalReachableRange SpreaderSceneWorldYMeters { get; }
        public YardCraneLocalReachableRange SpreaderStartRelativeMeters { get; }
        public YardCraneLocalReachableRange SpreaderPhysicalRotationDegrees { get; }

        public bool TryGetRange(
            YardCraneLocalTrainingMetric metric,
            YardCraneLocalTargetReferenceFrame referenceFrame,
            out YardCraneLocalReachableRange range)
        {
            switch (metric)
            {
                case YardCraneLocalTrainingMetric.GantryPosition
                    when referenceFrame == YardCraneLocalTargetReferenceFrame.GantryHomeRelativeMeters:
                    range = GantryHomeRelativeMeters;
                    return true;
                case YardCraneLocalTrainingMetric.TrolleyPosition
                    when referenceFrame == YardCraneLocalTargetReferenceFrame.TrolleyLocalXMeters:
                    range = TrolleyLocalXMeters;
                    return true;
                case YardCraneLocalTrainingMetric.SpreaderHeight
                    when referenceFrame == YardCraneLocalTargetReferenceFrame.SpreaderSceneWorldYMeters:
                    range = SpreaderSceneWorldYMeters;
                    return true;
                case YardCraneLocalTrainingMetric.SpreaderHeight
                    when referenceFrame == YardCraneLocalTargetReferenceFrame.SpreaderStartRelativeMeters:
                    range = SpreaderStartRelativeMeters;
                    return true;
                case YardCraneLocalTrainingMetric.SpreaderRotation
                    when referenceFrame == YardCraneLocalTargetReferenceFrame.SpreaderPhysicalRotationDegrees:
                case YardCraneLocalTrainingMetric.SpreaderZeroing
                    when referenceFrame == YardCraneLocalTargetReferenceFrame.SpreaderPhysicalRotationDegrees:
                    range = SpreaderPhysicalRotationDegrees;
                    return true;
                default:
                    range = default;
                    return false;
            }
        }
    }

    public static class YardCraneLocalTrainingTargetValidator
    {
        /// <summary>
        /// Validates the numeric target itself, not merely its tolerance band. An unreachable
        /// page value must be corrected at its source; it is never scaled, clamped or reinterpreted.
        /// </summary>
        public static bool TryValidate(
            YardCraneLocalTrainingTarget target,
            YardCraneLocalTrainingReachability reachability,
            out string error)
        {
            if (target == null)
            {
                error = "训练目标缺失。";
                return false;
            }

            if (!target.HasTarget || !target.Value.HasValue)
            {
                error = "引导学习必须提供明确的数值目标。";
                return false;
            }

            if (!reachability.TryGetRange(target.Metric, target.ReferenceFrame, out var range))
            {
                error = $"训练目标坐标系与指标不匹配：{target.MetricId}。";
                return false;
            }

            var value = target.Value.Value;
            if (range.Contains(value))
            {
                error = string.Empty;
                return true;
            }

            var unit = string.IsNullOrWhiteSpace(target.Unit) ? string.Empty : " " + target.Unit;
            error =
                $"目标 {Format(value)}{unit} 超出当前灰盒可达范围 " +
                $"[{Format(range.Minimum)}, {Format(range.Maximum)}]{unit}；" +
                "未做比例换算，请返回选择可达的场景坐标值。";
            return false;
        }

        private static string Format(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
