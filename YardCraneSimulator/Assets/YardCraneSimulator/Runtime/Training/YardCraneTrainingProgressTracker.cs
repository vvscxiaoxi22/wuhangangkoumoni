using System;
using PortTraining.YardCraneSimulator.Presentation;

namespace PortTraining.YardCraneSimulator.Training
{
    /// <summary>
    /// Local-only learning metrics evaluated from the existing read-only HUD snapshot.
    /// These values are presentation/training concepts, not Gateway or PLC contracts.
    /// </summary>
    public enum YardCraneLearningMetric
    {
        GantryPosition = 0,
        TrolleyPosition = 1,
        SpreaderHeight = 2,
        SpreaderRotation = 3,
        SpreaderZeroing = 4
    }

    public enum YardCraneLearningGoalState
    {
        WaitingForData = 0,
        Active = 1,
        Achieved = 2
    }

    public readonly struct YardCraneLearningGoalProgress
    {
        public YardCraneLearningGoalProgress(
            YardCraneLearningGoalState state,
            float progress01,
            float currentValue,
            bool observedRequiredAction)
        {
            State = state;
            Progress01 = Clamp01(progress01);
            CurrentValue = currentValue;
            ObservedRequiredAction = observedRequiredAction;
        }

        public YardCraneLearningGoalState State { get; }
        public float Progress01 { get; }
        public float CurrentValue { get; }
        public bool ObservedRequiredAction { get; }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            if (value < 0f) return 0f;
            return value > 1f ? 1f : value;
        }
    }

    /// <summary>
    /// Evaluates one guided-learning target from fresh HUD samples. A normal axis target
    /// must remain inside tolerance for a short dwell. Return-to-zero additionally requires
    /// an observed excursion and the rig's real ReturningToZero feedback, so an initially
    /// zero spreader can never be reported as a completed zeroing exercise.
    /// </summary>
    public sealed class YardCraneLearningGoalTracker
    {
        public const double DefaultStableSeconds = 0.35;
        public const double MaximumContinuousSampleGapSeconds =
            YardCraneHudFeed.StaleAfterSeconds;

        private readonly YardCraneLearningMetric metric;
        private readonly float target;
        private readonly float tolerance;
        private readonly double stableSeconds;
        private bool hasStart;
        private float startValue;
        private double lastSampleTime = double.NegativeInfinity;
        private double withinToleranceSince = double.NegativeInfinity;
        private bool observedAxisAction;
        private bool observedZeroingExcursion;
        private bool observedReturnToZero;

        public YardCraneLearningGoalTracker(
            YardCraneLearningMetric metric,
            float target,
            float tolerance,
            double stableSeconds = DefaultStableSeconds)
        {
            if (!IsFinite(target)) throw new ArgumentOutOfRangeException(nameof(target));
            if (!IsFinite(tolerance) || tolerance <= 0f)
                throw new ArgumentOutOfRangeException(nameof(tolerance));
            if (double.IsNaN(stableSeconds) || double.IsInfinity(stableSeconds) || stableSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(stableSeconds));

            this.metric = metric;
            this.target = target;
            this.tolerance = tolerance;
            this.stableSeconds = stableSeconds;
            Progress = new YardCraneLearningGoalProgress(
                YardCraneLearningGoalState.WaitingForData, 0f, float.NaN, false);
        }

        public YardCraneLearningGoalProgress Progress { get; private set; }

        public YardCraneLearningGoalProgress Update(YardCraneHudSnapshot sample)
        {
            if (Progress.State == YardCraneLearningGoalState.Achieved) return Progress;
            if (!IsFinite(sample.SampledAtSeconds) || sample.SampledAtSeconds <= lastSampleTime ||
                !sample.Status.Has(YardCraneHudSignals.Ready))
            {
                return Progress;
            }

            if (!TryReadMetric(sample, metric, out var value) || !IsFinite(value)) return Progress;
            if (!double.IsNegativeInfinity(lastSampleTime) &&
                sample.SampledAtSeconds - lastSampleTime > MaximumContinuousSampleGapSeconds)
            {
                // Missing samples cannot prove that the value stayed inside tolerance.
                // Preserve the observed action, but restart the continuous dwell window.
                withinToleranceSince = double.NegativeInfinity;
            }
            lastSampleTime = sample.SampledAtSeconds;

            if (!hasStart)
            {
                hasStart = true;
                startValue = value;
            }

            if (metric == YardCraneLearningMetric.SpreaderZeroing)
                return UpdateZeroing(sample, value);

            var distance = Math.Abs(value - target);
            var startDistance = Math.Abs(startValue - target);
            if (Math.Abs(value - startValue) >= Math.Max(0.005f, tolerance * 0.25f))
                observedAxisAction = true;
            var progress = startDistance <= tolerance
                ? (observedAxisAction && distance <= tolerance ? 1f : 0f)
                : 1f - (float)(distance / startDistance);
            return UpdateStableState(
                sample.SampledAtSeconds,
                value,
                progress,
                distance <= tolerance && observedAxisAction,
                observedAxisAction);
        }

        private YardCraneLearningGoalProgress UpdateZeroing(YardCraneHudSnapshot sample, float value)
        {
            var excursion = Math.Max(0.5f, tolerance * 3f);
            if (Math.Abs(value) >= excursion) observedZeroingExcursion = true;
            if (observedZeroingExcursion &&
                sample.Status.Has(YardCraneHudSignals.ReturningToZero))
            {
                observedReturnToZero = true;
            }

            var actionObserved = observedZeroingExcursion && observedReturnToZero;
            var progress = !observedZeroingExcursion
                ? 0f
                : !observedReturnToZero
                    ? 0.35f
                    : 0.35f + 0.65f * (1f - Math.Min(1f, Math.Abs(value) / excursion));
            var reached = actionObserved && Math.Abs(value - target) <= tolerance &&
                          !sample.Status.Has(YardCraneHudSignals.ReturningToZero);
            return UpdateStableState(sample.SampledAtSeconds, value, progress, reached, actionObserved);
        }

        private YardCraneLearningGoalProgress UpdateStableState(
            double sampleTime,
            float value,
            float progress,
            bool insideTolerance,
            bool actionObserved)
        {
            if (!insideTolerance)
            {
                withinToleranceSince = double.NegativeInfinity;
                Progress = new YardCraneLearningGoalProgress(
                    YardCraneLearningGoalState.Active, progress, value, actionObserved);
                return Progress;
            }

            if (double.IsNegativeInfinity(withinToleranceSince)) withinToleranceSince = sampleTime;
            var achieved = sampleTime - withinToleranceSince >= stableSeconds;
            Progress = new YardCraneLearningGoalProgress(
                achieved ? YardCraneLearningGoalState.Achieved : YardCraneLearningGoalState.Active,
                achieved ? 1f : progress,
                value,
                actionObserved);
            return Progress;
        }

        private static bool TryReadMetric(
            YardCraneHudSnapshot sample,
            YardCraneLearningMetric metric,
            out float value)
        {
            value = metric switch
            {
                YardCraneLearningMetric.GantryPosition => sample.Telemetry.GantryPosition,
                YardCraneLearningMetric.TrolleyPosition => sample.Telemetry.TrolleyPosition,
                YardCraneLearningMetric.SpreaderHeight => sample.Telemetry.SpreaderHeight,
                YardCraneLearningMetric.SpreaderRotation => sample.Telemetry.PhysicalRotation,
                YardCraneLearningMetric.SpreaderZeroing => sample.Telemetry.PhysicalRotation,
                _ => float.NaN
            };
            return IsFinite(value);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public enum YardCraneLocalAssessmentStage
    {
        WaitingForReady = 0,
        AwaitingLock = 1,
        AwaitingLoadedLift = 2,
        AwaitingLoadedTravel = 3,
        AwaitingLanding = 4,
        AwaitingRelease = 5,
        Passed = 6,
        Failed = 7
    }

    public readonly struct YardCraneLocalAssessmentProgress
    {
        public YardCraneLocalAssessmentProgress(
            YardCraneLocalAssessmentStage stage,
            int completedSteps,
            int totalSteps,
            string instruction,
            string failureReason)
        {
            Stage = stage;
            CompletedSteps = Math.Max(0, completedSteps);
            TotalSteps = Math.Max(1, totalSteps);
            Instruction = instruction ?? string.Empty;
            FailureReason = failureReason ?? string.Empty;
        }

        public YardCraneLocalAssessmentStage Stage { get; }
        public int CompletedSteps { get; }
        public int TotalSteps { get; }
        public string Instruction { get; }
        public string FailureReason { get; }
        public bool IsTerminal => Stage == YardCraneLocalAssessmentStage.Passed ||
                                  Stage == YardCraneLocalAssessmentStage.Failed;
        public float Progress01 => (float)CompletedSteps / TotalSteps;
    }

    /// <summary>
    /// Local process assessment based only on facts already exposed by the HUD adapter.
    /// It deliberately produces a local pass/fail result and no score. Platform/teacher
    /// authority, work-order validity and official scoring remain outside the Unity scene.
    /// </summary>
    public sealed class YardCraneLocalAssessmentTracker
    {
        public const int TotalSteps = 5;
        public const float DefaultMinimumLiftMeters = 0.25f;
        public const float DefaultMinimumTravelMeters = 0.50f;

        private readonly float minimumLiftMeters;
        private readonly float minimumTravelMeters;
        private double lastSampleTime = double.NegativeInfinity;
        private float lockHeight;
        private float travelStartGantry;
        private float travelStartTrolley;
        private bool observedLandedUnlockStart;

        public YardCraneLocalAssessmentTracker(
            float minimumLiftMeters = DefaultMinimumLiftMeters,
            float minimumTravelMeters = DefaultMinimumTravelMeters)
        {
            if (!IsFinite(minimumLiftMeters) || minimumLiftMeters <= 0f)
                throw new ArgumentOutOfRangeException(nameof(minimumLiftMeters));
            if (!IsFinite(minimumTravelMeters) || minimumTravelMeters <= 0f)
                throw new ArgumentOutOfRangeException(nameof(minimumTravelMeters));

            this.minimumLiftMeters = minimumLiftMeters;
            this.minimumTravelMeters = minimumTravelMeters;
            Progress = CreateProgress(YardCraneLocalAssessmentStage.WaitingForReady);
        }

        public YardCraneLocalAssessmentProgress Progress { get; private set; }

        public YardCraneLocalAssessmentProgress Update(YardCraneHudSnapshot sample)
        {
            if (Progress.IsTerminal || !IsFinite(sample.SampledAtSeconds) ||
                sample.SampledAtSeconds <= lastSampleTime ||
                !sample.Status.Has(YardCraneHudSignals.Ready))
            {
                return Progress;
            }

            lastSampleTime = sample.SampledAtSeconds;
            var locked = sample.Status.Has(YardCraneHudSignals.Locked);
            var transitioning = sample.Status.Has(YardCraneHudSignals.Locking) ||
                                sample.Status.Has(YardCraneHudSignals.Unlocking);

            switch (Progress.Stage)
            {
                case YardCraneLocalAssessmentStage.WaitingForReady:
                    if (!locked && !transitioning)
                        Progress = CreateProgress(YardCraneLocalAssessmentStage.AwaitingLock);
                    break;

                case YardCraneLocalAssessmentStage.AwaitingLock:
                    if (locked && !transitioning)
                    {
                        lockHeight = sample.Telemetry.SpreaderHeight;
                        travelStartGantry = sample.Telemetry.GantryPosition;
                        travelStartTrolley = sample.Telemetry.TrolleyPosition;
                        Progress = CreateProgress(YardCraneLocalAssessmentStage.AwaitingLoadedLift);
                    }
                    break;

                case YardCraneLocalAssessmentStage.AwaitingLoadedLift:
                    if (!locked)
                    {
                        Fail("箱体在确认起升前失去锁定，流程判定终止。");
                        break;
                    }
                    if (HasLoadedLifted(sample, lockHeight, minimumLiftMeters))
                    {
                        travelStartGantry = sample.Telemetry.GantryPosition;
                        travelStartTrolley = sample.Telemetry.TrolleyPosition;
                        Progress = CreateProgress(YardCraneLocalAssessmentStage.AwaitingLoadedTravel);
                    }
                    break;

                case YardCraneLocalAssessmentStage.AwaitingLoadedTravel:
                    if (!locked)
                    {
                        Fail("箱体在完成转运前失去锁定，流程判定终止。");
                        break;
                    }
                    if (HasTravelled(sample, travelStartGantry, travelStartTrolley, minimumTravelMeters))
                        Progress = CreateProgress(YardCraneLocalAssessmentStage.AwaitingLanding);
                    break;

                case YardCraneLocalAssessmentStage.AwaitingLanding:
                    if (!locked)
                    {
                        Fail("箱体未确认落稳即解锁，流程判定终止。");
                        break;
                    }
                    if (sample.Status.Has(YardCraneHudSignals.Landed))
                    {
                        observedLandedUnlockStart = false;
                        Progress = CreateProgress(YardCraneLocalAssessmentStage.AwaitingRelease);
                    }
                    break;

                case YardCraneLocalAssessmentStage.AwaitingRelease:
                    var unlocking = sample.Status.Has(YardCraneHudSignals.Unlocking);
                    var landed = sample.Status.Has(YardCraneHudSignals.Landed);
                    if (unlocking)
                    {
                        if (!landed)
                        {
                            Fail("箱体离开落稳状态后才开始解锁，流程判定终止。");
                            break;
                        }
                        observedLandedUnlockStart = true;
                        break;
                    }

                    if (locked)
                    {
                        if (!landed)
                        {
                            // Still locked: require a new landing confirmation before release.
                            observedLandedUnlockStart = false;
                            Progress = CreateProgress(YardCraneLocalAssessmentStage.AwaitingLanding);
                        }
                        else if (observedLandedUnlockStart)
                        {
                            // The sampled unlock attempt ended without releasing the load.
                            observedLandedUnlockStart = false;
                        }
                        break;
                    }

                    if (!transitioning)
                    {
                        if (observedLandedUnlockStart)
                            Progress = CreateProgress(YardCraneLocalAssessmentStage.Passed);
                        else
                            Fail("未观察到箱体落稳状态下的合法解锁过程。");
                    }
                    break;
            }

            return Progress;
        }

        public YardCraneLocalAssessmentProgress Timeout()
        {
            if (!Progress.IsTerminal) Fail("已达到本地考核时限。");
            return Progress;
        }

        private void Fail(string reason)
        {
            Progress = new YardCraneLocalAssessmentProgress(
                YardCraneLocalAssessmentStage.Failed,
                Progress.CompletedSteps,
                TotalSteps,
                "本地考核未通过",
                reason);
        }

        private static bool HasLoadedLifted(
            YardCraneHudSnapshot sample,
            float referenceHeight,
            float minimumLiftMeters)
        {
            if (IsFinite(referenceHeight) && IsFinite(sample.Telemetry.SpreaderHeight) &&
                sample.Telemetry.SpreaderHeight - referenceHeight >= minimumLiftMeters)
            {
                return true;
            }

            // ReadyLoaded is the normal stationary state immediately after locking; phase
            // alone cannot prove any lift. Requiring measured height change avoids advancing
            // the assessment on the frame after a completed lock.
            return false;
        }

        private static bool HasTravelled(
            YardCraneHudSnapshot sample,
            float startGantry,
            float startTrolley,
            float minimumTravelMeters)
        {
            var gantryTravel = IsFinite(startGantry) && IsFinite(sample.Telemetry.GantryPosition)
                ? Math.Abs(sample.Telemetry.GantryPosition - startGantry)
                : 0f;
            var trolleyTravel = IsFinite(startTrolley) && IsFinite(sample.Telemetry.TrolleyPosition)
                ? Math.Abs(sample.Telemetry.TrolleyPosition - startTrolley)
                : 0f;
            return Math.Max(gantryTravel, trolleyTravel) >= minimumTravelMeters;
        }

        private static YardCraneLocalAssessmentProgress CreateProgress(YardCraneLocalAssessmentStage stage)
        {
            return stage switch
            {
                YardCraneLocalAssessmentStage.WaitingForReady =>
                    new YardCraneLocalAssessmentProgress(stage, 0, TotalSteps, "等待场桥状态就绪", string.Empty),
                YardCraneLocalAssessmentStage.AwaitingLock =>
                    new YardCraneLocalAssessmentProgress(stage, 0, TotalSteps, "对位并锁定目标箱", string.Empty),
                YardCraneLocalAssessmentStage.AwaitingLoadedLift =>
                    new YardCraneLocalAssessmentProgress(stage, 1, TotalSteps, "带箱起升至安全高度", string.Empty),
                YardCraneLocalAssessmentStage.AwaitingLoadedTravel =>
                    new YardCraneLocalAssessmentProgress(stage, 2, TotalSteps, "带箱完成大车或小车转运", string.Empty),
                YardCraneLocalAssessmentStage.AwaitingLanding =>
                    new YardCraneLocalAssessmentProgress(stage, 3, TotalSteps, "下降并确认目标箱落稳", string.Empty),
                YardCraneLocalAssessmentStage.AwaitingRelease =>
                    new YardCraneLocalAssessmentProgress(stage, 4, TotalSteps, "落稳后解锁完成交箱", string.Empty),
                YardCraneLocalAssessmentStage.Passed =>
                    new YardCraneLocalAssessmentProgress(stage, TotalSteps, TotalSteps, "本地考核流程已达成", string.Empty),
                _ =>
                    new YardCraneLocalAssessmentProgress(stage, 0, TotalSteps, "本地考核未通过", string.Empty)
            };
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
