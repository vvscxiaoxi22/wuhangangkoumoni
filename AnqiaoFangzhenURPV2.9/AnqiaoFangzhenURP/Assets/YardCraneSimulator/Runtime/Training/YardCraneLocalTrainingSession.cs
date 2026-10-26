using System;

namespace PortTraining.YardCraneSimulator.Training
{
    /// <summary>
    /// Student-facing purpose of a local scene run. This is independent of process launch mode.
    /// The types in this file are local runtime snapshots, not PortTraining.Contracts messages.
    /// </summary>
    public enum YardCraneLocalTrainingMode
    {
        FreePractice = 0,
        GuidedLearning = 1,
        Assessment = 2
    }

    public enum YardCraneLocalTrainingExercise
    {
        None = 0,
        FreePractice = 1,
        GantryMovement = 2,
        TrolleyMovement = 3,
        SpreaderHoisting = 4,
        SpreaderRotation = 5,
        SpreaderZeroing = 6,
        PickupWorkflow = 7,
        DropoffWorkflow = 8,

        // 2026-09-11: manual §5.2.5-8 "联动练习" - two or three axes must reach their own
        // target simultaneously (a single cursor in gantry/trolley/hoist space). See
        // YardCraneLocalTrainingSessionContext.AdditionalTargets for how the extra axes ride
        // alongside the existing single-axis Target field.
        LinkedGantryTrolley = 9,
        LinkedGantryHoist = 10,
        LinkedTrolleyHoist = 11,
        LinkedGantryTrolleyHoist = 12,

        // 2026-09-11: manual §5.2.9 "项目练习-大车直线行走" - same GantryPosition metric as
        // GantryMovement, kept as its own exercise value so it gets its own title/instruction
        // and audit trail instead of being indistinguishable from the single-item drill.
        SkillGantryStraightLine = 13
    }

    public enum YardCraneLocalTrainingMetric
    {
        None = 0,
        GantryPosition = 1,
        TrolleyPosition = 2,
        SpreaderHeight = 3,
        SpreaderRotation = 4,
        SpreaderZeroing = 5
    }

    /// <summary>
    /// Identifies the coordinate frame of Target.Value. Values selected in StudentHome are
    /// preserved in these frames and must be rejected if the active rig cannot reach them;
    /// consumers must never silently scale or reinterpret them.
    /// </summary>
    public enum YardCraneLocalTargetReferenceFrame
    {
        NotApplicable = 0,
        GantryHomeRelativeMeters = 1,
        TrolleyLocalXMeters = 2,
        SpreaderSceneWorldYMeters = 3,
        SpreaderPhysicalRotationDegrees = 4,

        // Signed height delta from the first healthy sample of this session:
        // +1 m means rise by 1 m; -1 m means lower by 1 m.
        SpreaderStartRelativeMeters = 5
    }

    public enum YardCraneLocalSpreaderZeroingPolicy
    {
        NotApplicable = 0,
        AutoReturn = 1,
        ReturnAndHold = 2
    }

    public enum YardCraneLocalTimingMode
    {
        Untimed = 0,
        CountUp = 1,
        CountDown = 2
    }

    // Names follow the V1.1 contract vocabulary. The local prefix prevents these values from
    // being mistaken for shared cross-process contract types.
    public enum YardCraneLocalAssistLevel
    {
        None = 0,
        Standard = 1,
        Guided = 2
    }

    public enum YardCraneLocalScoreTrustLevel
    {
        Unscored = 0,
        LocalEstimated = 1,
        AuthorityCalculated = 2
    }

    public enum YardCraneLocalTrainingReturnPage
    {
        StudentHome = 0,
        LearningSelection = 1
    }

    public static class YardCraneLocalTrainingMetricIds
    {
        public const string GantryPosition = "gantry.position.relative.m";
        public const string TrolleyPosition = "trolley.position.local-x.m";
        public const string SpreaderHeight = "spreader.height.scene-y.m";
        public const string SpreaderHeightStartRelative = "spreader.height.start-relative.m";
        public const string SpreaderRotation = "spreader.rotation.physical.deg";
        public const string SpreaderZeroing = "spreader.zero.physical.deg";
    }

    /// <summary>
    /// Immutable participant identity copied from the login shell. It deliberately contains no
    /// password, role, mutable profile reference or platform authentication claim.
    /// </summary>
    public sealed class YardCraneLocalTrainingParticipant
    {
        public YardCraneLocalTrainingParticipant(string studentNumber, string displayName)
        {
            StudentNumber = NormalizeRequired(studentNumber, nameof(studentNumber), 64);
            DisplayName = NormalizeRequired(displayName, nameof(displayName), 64);
        }

        public string StudentNumber { get; }
        public string AccountId => StudentNumber;
        public string DisplayName { get; }

        private static string NormalizeRequired(string value, string parameterName, int maximumLength)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (normalized.Length == 0 || normalized.Length > maximumLength)
                throw new ArgumentException("A non-empty local participant value is required.", parameterName);

            foreach (var character in normalized)
            {
                if (char.IsControl(character))
                    throw new ArgumentException("Local participant values cannot contain control characters.", parameterName);
            }

            return normalized;
        }
    }

    /// <summary>
    /// Immutable numeric exercise target. MetricId and Unit are derived from Metric, while
    /// ReferenceFrame makes the value's coordinate meaning explicit.
    /// </summary>
    public sealed class YardCraneLocalTrainingTarget
    {
        public static YardCraneLocalTrainingTarget None { get; } =
            new YardCraneLocalTrainingTarget(
                YardCraneLocalTrainingMetric.None,
                YardCraneLocalTargetReferenceFrame.NotApplicable,
                null,
                null,
                string.Empty,
                YardCraneLocalSpreaderZeroingPolicy.NotApplicable,
                0d);

        public YardCraneLocalTrainingTarget(
            YardCraneLocalTrainingMetric metric,
            YardCraneLocalTargetReferenceFrame referenceFrame,
            double? value,
            double? tolerance,
            string displayText,
            YardCraneLocalSpreaderZeroingPolicy zeroingPolicy =
                YardCraneLocalSpreaderZeroingPolicy.NotApplicable,
            double requiredHoldSeconds = 0d)
        {
            if (!Enum.IsDefined(typeof(YardCraneLocalTrainingMetric), metric))
                throw new ArgumentOutOfRangeException(nameof(metric));
            if (!Enum.IsDefined(typeof(YardCraneLocalTargetReferenceFrame), referenceFrame))
                throw new ArgumentOutOfRangeException(nameof(referenceFrame));
            if (!Enum.IsDefined(typeof(YardCraneLocalSpreaderZeroingPolicy), zeroingPolicy))
                throw new ArgumentOutOfRangeException(nameof(zeroingPolicy));
            if (!IsFinite(requiredHoldSeconds) || requiredHoldSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(requiredHoldSeconds));

            if (metric == YardCraneLocalTrainingMetric.None)
            {
                if (referenceFrame != YardCraneLocalTargetReferenceFrame.NotApplicable ||
                    value.HasValue || tolerance.HasValue || !string.IsNullOrWhiteSpace(displayText) ||
                    zeroingPolicy != YardCraneLocalSpreaderZeroingPolicy.NotApplicable ||
                    requiredHoldSeconds != 0d)
                {
                    throw new ArgumentException("A target with Metric.None cannot contain target data.");
                }
            }
            else
            {
                if (!value.HasValue || !IsFinite(value.Value))
                    throw new ArgumentOutOfRangeException(nameof(value), "A finite numeric target is required.");
                if (!tolerance.HasValue || !IsFinite(tolerance.Value) || tolerance.Value <= 0d)
                    throw new ArgumentOutOfRangeException(nameof(tolerance), "A positive finite tolerance is required.");
                if (!ReferenceFrameMatches(metric, referenceFrame))
                    throw new ArgumentException("The target reference frame does not match its metric.", nameof(referenceFrame));
                if (metric == YardCraneLocalTrainingMetric.SpreaderZeroing && value.Value != 0d)
                    throw new ArgumentException("Spreader zeroing target must be exactly zero.", nameof(value));
                if (metric == YardCraneLocalTrainingMetric.SpreaderZeroing)
                {
                    if (zeroingPolicy == YardCraneLocalSpreaderZeroingPolicy.NotApplicable)
                        throw new ArgumentException("Spreader zeroing target requires an explicit zeroing policy.", nameof(zeroingPolicy));
                    if (zeroingPolicy == YardCraneLocalSpreaderZeroingPolicy.AutoReturn && requiredHoldSeconds != 0d)
                        throw new ArgumentException("AutoReturn cannot require a post-zero hold.", nameof(requiredHoldSeconds));
                    if (zeroingPolicy == YardCraneLocalSpreaderZeroingPolicy.ReturnAndHold && requiredHoldSeconds <= 0d)
                        throw new ArgumentException("ReturnAndHold requires a positive hold duration.", nameof(requiredHoldSeconds));
                }
                else if (zeroingPolicy != YardCraneLocalSpreaderZeroingPolicy.NotApplicable ||
                         requiredHoldSeconds != 0d)
                {
                    throw new ArgumentException("Zeroing policy is only valid for a spreader-zeroing target.", nameof(zeroingPolicy));
                }
            }

            Metric = metric;
            ReferenceFrame = referenceFrame;
            Value = value;
            Tolerance = tolerance;
            DisplayText = (displayText ?? string.Empty).Trim();
            ZeroingPolicy = zeroingPolicy;
            RequiredHoldSeconds = requiredHoldSeconds;
            MetricId = MetricIdFor(metric, referenceFrame);
            Unit = UnitFor(metric);
        }

        public YardCraneLocalTrainingMetric Metric { get; }
        public string MetricId { get; }
        public YardCraneLocalTargetReferenceFrame ReferenceFrame { get; }
        public double? Value { get; }
        public string Unit { get; }
        public double? Tolerance { get; }
        public string DisplayText { get; }
        public YardCraneLocalSpreaderZeroingPolicy ZeroingPolicy { get; }
        public double RequiredHoldSeconds { get; }
        public bool HasTarget => Metric != YardCraneLocalTrainingMetric.None;

        private static bool ReferenceFrameMatches(
            YardCraneLocalTrainingMetric metric,
            YardCraneLocalTargetReferenceFrame referenceFrame)
        {
            switch (metric)
            {
                case YardCraneLocalTrainingMetric.GantryPosition:
                    return referenceFrame == YardCraneLocalTargetReferenceFrame.GantryHomeRelativeMeters;
                case YardCraneLocalTrainingMetric.TrolleyPosition:
                    return referenceFrame == YardCraneLocalTargetReferenceFrame.TrolleyLocalXMeters;
                case YardCraneLocalTrainingMetric.SpreaderHeight:
                    return referenceFrame == YardCraneLocalTargetReferenceFrame.SpreaderSceneWorldYMeters ||
                           referenceFrame == YardCraneLocalTargetReferenceFrame.SpreaderStartRelativeMeters;
                case YardCraneLocalTrainingMetric.SpreaderRotation:
                case YardCraneLocalTrainingMetric.SpreaderZeroing:
                    return referenceFrame == YardCraneLocalTargetReferenceFrame.SpreaderPhysicalRotationDegrees;
                default:
                    return false;
            }
        }

        private static string MetricIdFor(
            YardCraneLocalTrainingMetric metric,
            YardCraneLocalTargetReferenceFrame referenceFrame)
        {
            switch (metric)
            {
                case YardCraneLocalTrainingMetric.GantryPosition:
                    return YardCraneLocalTrainingMetricIds.GantryPosition;
                case YardCraneLocalTrainingMetric.TrolleyPosition:
                    return YardCraneLocalTrainingMetricIds.TrolleyPosition;
                case YardCraneLocalTrainingMetric.SpreaderHeight:
                    return referenceFrame == YardCraneLocalTargetReferenceFrame.SpreaderStartRelativeMeters
                        ? YardCraneLocalTrainingMetricIds.SpreaderHeightStartRelative
                        : YardCraneLocalTrainingMetricIds.SpreaderHeight;
                case YardCraneLocalTrainingMetric.SpreaderRotation:
                    return YardCraneLocalTrainingMetricIds.SpreaderRotation;
                case YardCraneLocalTrainingMetric.SpreaderZeroing:
                    return YardCraneLocalTrainingMetricIds.SpreaderZeroing;
                default:
                    return string.Empty;
            }
        }

        private static string UnitFor(YardCraneLocalTrainingMetric metric)
        {
            switch (metric)
            {
                case YardCraneLocalTrainingMetric.GantryPosition:
                case YardCraneLocalTrainingMetric.TrolleyPosition:
                case YardCraneLocalTrainingMetric.SpreaderHeight:
                    return "m";
                case YardCraneLocalTrainingMetric.SpreaderRotation:
                case YardCraneLocalTrainingMetric.SpreaderZeroing:
                    return "deg";
                default:
                    return string.Empty;
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    /// <summary>
    /// Immutable timing policy. StartCountdownSeconds is the pre-run ready countdown;
    /// DurationSeconds is the optional run limit and is required for CountDown mode.
    /// Live elapsed/remaining state does not belong in this launch snapshot.
    /// </summary>
    public sealed class YardCraneLocalTimingPolicy
    {
        public static YardCraneLocalTimingPolicy Untimed { get; } =
            new YardCraneLocalTimingPolicy(YardCraneLocalTimingMode.Untimed, 0, null);

        public YardCraneLocalTimingPolicy(
            YardCraneLocalTimingMode mode,
            int startCountdownSeconds,
            int? durationSeconds)
        {
            if (!Enum.IsDefined(typeof(YardCraneLocalTimingMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (startCountdownSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(startCountdownSeconds));
            if (durationSeconds.HasValue && durationSeconds.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(durationSeconds));
            if (mode == YardCraneLocalTimingMode.Untimed &&
                (startCountdownSeconds != 0 || durationSeconds.HasValue))
            {
                throw new ArgumentException("Untimed policy cannot define a countdown or duration.");
            }
            if (mode == YardCraneLocalTimingMode.CountDown && !durationSeconds.HasValue)
                throw new ArgumentException("CountDown policy requires a duration.", nameof(durationSeconds));

            Mode = mode;
            StartCountdownSeconds = startCountdownSeconds;
            DurationSeconds = durationSeconds;
        }

        public YardCraneLocalTimingMode Mode { get; }
        public int StartCountdownSeconds { get; }
        public int? DurationSeconds { get; }

        public static YardCraneLocalTimingPolicy CountUp(
            int startCountdownSeconds = 0,
            int? durationSeconds = null)
        {
            return new YardCraneLocalTimingPolicy(
                YardCraneLocalTimingMode.CountUp,
                startCountdownSeconds,
                durationSeconds);
        }

        public static YardCraneLocalTimingPolicy CountDown(
            int durationSeconds,
            int startCountdownSeconds = 3)
        {
            return new YardCraneLocalTimingPolicy(
                YardCraneLocalTimingMode.CountDown,
                startCountdownSeconds,
                durationSeconds);
        }
    }

    /// <summary>
    /// Immutable local task presentation snapshot. Empty location/container values mean that
    /// the information is unavailable; consumers may render an unknown marker but may not
    /// invent task facts. Numeric exercise values must always come from Target, never by parsing
    /// or duplicating one of these presentation strings.
    /// </summary>
    public sealed class YardCraneLocalTaskDisplayInfo
    {
        public YardCraneLocalTaskDisplayInfo(
            string title,
            string instruction,
            string sourceBay,
            string destinationBay,
            string targetContainer,
            string destinationSlot)
        {
            Title = Normalize(title);
            Instruction = Normalize(instruction);
            SourceBay = Normalize(sourceBay);
            DestinationBay = Normalize(destinationBay);
            TargetContainer = Normalize(targetContainer);
            DestinationSlot = Normalize(destinationSlot);
        }

        public string Title { get; }
        public string Instruction { get; }
        public string SourceBay { get; }
        public string DestinationBay { get; }
        public string TargetContainer { get; }
        public string DestinationSlot { get; }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim();
        }
    }

    /// <summary>
    /// Complete immutable snapshot created before leaving StudentHome. Changing any selection
    /// requires a new context and therefore a new SessionId.
    /// </summary>
    public sealed class YardCraneLocalTrainingSessionContext
    {
        public YardCraneLocalTrainingSessionContext(
            Guid sessionId,
            YardCraneLocalTrainingParticipant participant,
            YardCraneLocalTrainingMode mode,
            YardCraneLocalTrainingExercise exercise,
            YardCraneLocalTrainingTarget target,
            YardCraneLocalTimingPolicy timing,
            YardCraneLocalAssistLevel assistLevel,
            YardCraneLocalScoreTrustLevel scoreTrustLevel,
            YardCraneLocalTrainingReturnPage returnPage,
            YardCraneLocalTaskDisplayInfo displayInfo,
            YardCraneLocalTrainingTarget[] additionalTargets = null)
        {
            if (sessionId == Guid.Empty) throw new ArgumentException("Session id is required.", nameof(sessionId));
            Participant = participant ?? throw new ArgumentNullException(nameof(participant));
            if (!Enum.IsDefined(typeof(YardCraneLocalTrainingMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (!Enum.IsDefined(typeof(YardCraneLocalTrainingExercise), exercise))
                throw new ArgumentOutOfRangeException(nameof(exercise));
            if (!Enum.IsDefined(typeof(YardCraneLocalAssistLevel), assistLevel))
                throw new ArgumentOutOfRangeException(nameof(assistLevel));
            if (!Enum.IsDefined(typeof(YardCraneLocalScoreTrustLevel), scoreTrustLevel))
                throw new ArgumentOutOfRangeException(nameof(scoreTrustLevel));
            if (!Enum.IsDefined(typeof(YardCraneLocalTrainingReturnPage), returnPage))
                throw new ArgumentOutOfRangeException(nameof(returnPage));
            if (scoreTrustLevel == YardCraneLocalScoreTrustLevel.AuthorityCalculated)
            {
                throw new ArgumentException(
                    "A local scene session cannot claim an authority-calculated score.",
                    nameof(scoreTrustLevel));
            }

            Target = target ?? throw new ArgumentNullException(nameof(target));
            Timing = timing ?? throw new ArgumentNullException(nameof(timing));
            DisplayInfo = displayInfo ?? throw new ArgumentNullException(nameof(displayInfo));
            AdditionalTargets = additionalTargets ?? Array.Empty<YardCraneLocalTrainingTarget>();
            ValidatePolicy(
                mode, exercise, target, AdditionalTargets, timing, assistLevel, scoreTrustLevel, returnPage,
                displayInfo);

            SessionId = sessionId;
            Mode = mode;
            Exercise = exercise;
            AssistLevel = assistLevel;
            ScoreTrustLevel = scoreTrustLevel;
            ReturnPage = returnPage;
        }

        public Guid SessionId { get; }
        public Guid LocalSessionId => SessionId;
        public YardCraneLocalTrainingParticipant Participant { get; }
        public YardCraneLocalTrainingParticipant Student => Participant;
        public YardCraneLocalTrainingMode Mode { get; }
        public YardCraneLocalTrainingExercise Exercise { get; }
        public YardCraneLocalTrainingTarget Target { get; }

        // 2026-09-11: extra axes for a "联动" (linked) guided-learning exercise that must all
        // reach their own target simultaneously alongside Target. Empty for every other mode/
        // exercise - existing single-axis call sites never set this and are unaffected. See
        // RequireGuidedLearningPolicy/ExpectedMetricSetFor for the axis-set contract.
        public YardCraneLocalTrainingTarget[] AdditionalTargets { get; }
        public YardCraneLocalTimingPolicy Timing { get; }
        public YardCraneLocalAssistLevel AssistLevel { get; }
        public YardCraneLocalScoreTrustLevel ScoreTrustLevel { get; }
        public YardCraneLocalTrainingReturnPage ReturnPage { get; }
        public YardCraneLocalTaskDisplayInfo DisplayInfo { get; }

        public static YardCraneLocalTrainingSessionContext Create(
            YardCraneLocalTrainingParticipant participant,
            YardCraneLocalTrainingMode mode,
            YardCraneLocalTrainingExercise exercise,
            YardCraneLocalTrainingTarget target,
            YardCraneLocalTimingPolicy timing,
            YardCraneLocalAssistLevel assistLevel,
            YardCraneLocalScoreTrustLevel scoreTrustLevel,
            YardCraneLocalTrainingReturnPage returnPage,
            YardCraneLocalTaskDisplayInfo displayInfo,
            YardCraneLocalTrainingTarget[] additionalTargets = null)
        {
            return new YardCraneLocalTrainingSessionContext(
                Guid.NewGuid(),
                participant,
                mode,
                exercise,
                target,
                timing,
                assistLevel,
                scoreTrustLevel,
                returnPage,
                displayInfo,
                additionalTargets);
        }

        public static YardCraneLocalTrainingSessionContext CreateFreePractice(
            YardCraneLocalTrainingParticipant participant)
        {
            return Create(
                participant,
                YardCraneLocalTrainingMode.FreePractice,
                YardCraneLocalTrainingExercise.FreePractice,
                YardCraneLocalTrainingTarget.None,
                YardCraneLocalTimingPolicy.CountUp(),
                YardCraneLocalAssistLevel.Standard,
                YardCraneLocalScoreTrustLevel.LocalEstimated,
                YardCraneLocalTrainingReturnPage.StudentHome,
                new YardCraneLocalTaskDisplayInfo(
                    "自由练习",
                    "无指定训练目标，可练习全部合法操作。",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty));
        }

        // 2026-09-11: 5s (not the general-purpose 3s default other factories use) so the new
        // task-brief-then-countdown flow (see YardCraneLocalTrainingModeCoordinator) gives the
        // student a full 5 seconds after the brief closes before business input engages.
        public static YardCraneLocalTrainingSessionContext CreateGuidedLearning(
            YardCraneLocalTrainingParticipant participant,
            YardCraneLocalTrainingExercise exercise,
            YardCraneLocalTrainingTarget target,
            YardCraneLocalTaskDisplayInfo displayInfo,
            int startCountdownSeconds = 5)
        {
            return CreateGuidedLearning(
                participant, exercise, target, Array.Empty<YardCraneLocalTrainingTarget>(), displayInfo,
                startCountdownSeconds);
        }

        // 2026-09-11: overload for a "联动" (linked) exercise that must satisfy 2-3 axes at
        // once. additionalTargets rides alongside the existing single-axis target parameter;
        // see AdditionalTargets/RequireGuidedLearningPolicy for the validation contract.
        public static YardCraneLocalTrainingSessionContext CreateGuidedLearning(
            YardCraneLocalTrainingParticipant participant,
            YardCraneLocalTrainingExercise exercise,
            YardCraneLocalTrainingTarget target,
            YardCraneLocalTrainingTarget[] additionalTargets,
            YardCraneLocalTaskDisplayInfo displayInfo,
            int startCountdownSeconds = 5)
        {
            return Create(
                participant,
                YardCraneLocalTrainingMode.GuidedLearning,
                exercise,
                target,
                YardCraneLocalTimingPolicy.CountUp(startCountdownSeconds),
                YardCraneLocalAssistLevel.Guided,
                YardCraneLocalScoreTrustLevel.Unscored,
                YardCraneLocalTrainingReturnPage.LearningSelection,
                displayInfo,
                additionalTargets);
        }

        // 2026-09-11: default raised from 3 to 5, matching CreateGuidedLearning's countdown -
        // now that Assessment also goes through the same task-brief-then-countdown flow (see
        // YardCraneLocalTrainingModeCoordinator.RequestTaskBriefOrBegin), a different countdown
        // length between modes would feel inconsistent to a student switching between them.
        public static YardCraneLocalTrainingSessionContext CreateAssessment(
            YardCraneLocalTrainingParticipant participant,
            YardCraneLocalTrainingExercise exercise,
            YardCraneLocalTaskDisplayInfo displayInfo,
            int durationSeconds,
            int startCountdownSeconds = 5)
        {
            return Create(
                participant,
                YardCraneLocalTrainingMode.Assessment,
                exercise,
                YardCraneLocalTrainingTarget.None,
                YardCraneLocalTimingPolicy.CountDown(durationSeconds, startCountdownSeconds),
                YardCraneLocalAssistLevel.None,
                YardCraneLocalScoreTrustLevel.Unscored,
                YardCraneLocalTrainingReturnPage.StudentHome,
                displayInfo);
        }

        private static void ValidatePolicy(
            YardCraneLocalTrainingMode mode,
            YardCraneLocalTrainingExercise exercise,
            YardCraneLocalTrainingTarget target,
            YardCraneLocalTrainingTarget[] additionalTargets,
            YardCraneLocalTimingPolicy timing,
            YardCraneLocalAssistLevel assistLevel,
            YardCraneLocalScoreTrustLevel scoreTrustLevel,
            YardCraneLocalTrainingReturnPage returnPage,
            YardCraneLocalTaskDisplayInfo displayInfo)
        {
            if (displayInfo.Title.Length == 0)
                throw new ArgumentException("Local task display title is required.", nameof(displayInfo));

            switch (mode)
            {
                case YardCraneLocalTrainingMode.FreePractice:
                    if (additionalTargets.Length != 0)
                        throw new ArgumentException("FreePractice cannot carry additional targets.", nameof(additionalTargets));
                    RequireFreePracticePolicy(exercise, target, timing, assistLevel, scoreTrustLevel, returnPage);
                    return;
                case YardCraneLocalTrainingMode.GuidedLearning:
                    RequireGuidedLearningPolicy(
                        exercise, target, additionalTargets, timing, assistLevel, scoreTrustLevel, returnPage,
                        displayInfo);
                    return;
                case YardCraneLocalTrainingMode.Assessment:
                    if (additionalTargets.Length != 0)
                        throw new ArgumentException("Assessment cannot carry additional targets.", nameof(additionalTargets));
                    RequireAssessmentPolicy(
                        exercise, target, timing, assistLevel, scoreTrustLevel, returnPage, displayInfo);
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }

        private static void RequireFreePracticePolicy(
            YardCraneLocalTrainingExercise exercise,
            YardCraneLocalTrainingTarget target,
            YardCraneLocalTimingPolicy timing,
            YardCraneLocalAssistLevel assistLevel,
            YardCraneLocalScoreTrustLevel scoreTrustLevel,
            YardCraneLocalTrainingReturnPage returnPage)
        {
            if (exercise != YardCraneLocalTrainingExercise.FreePractice || target.HasTarget ||
                timing.Mode != YardCraneLocalTimingMode.CountUp || timing.StartCountdownSeconds != 0 ||
                timing.DurationSeconds.HasValue || assistLevel != YardCraneLocalAssistLevel.Standard ||
                scoreTrustLevel != YardCraneLocalScoreTrustLevel.LocalEstimated ||
                returnPage != YardCraneLocalTrainingReturnPage.StudentHome)
            {
                throw new ArgumentException("FreePractice policy must be open-ended, standard-assisted and locally estimated.");
            }
        }

        private static void RequireGuidedLearningPolicy(
            YardCraneLocalTrainingExercise exercise,
            YardCraneLocalTrainingTarget target,
            YardCraneLocalTrainingTarget[] additionalTargets,
            YardCraneLocalTimingPolicy timing,
            YardCraneLocalAssistLevel assistLevel,
            YardCraneLocalScoreTrustLevel scoreTrustLevel,
            YardCraneLocalTrainingReturnPage returnPage,
            YardCraneLocalTaskDisplayInfo displayInfo)
        {
            var isOpenLearning = exercise == YardCraneLocalTrainingExercise.None &&
                                 !target.HasTarget && additionalTargets.Length == 0;
            if (isOpenLearning)
            {
                if (timing.Mode != YardCraneLocalTimingMode.CountUp ||
                    timing.DurationSeconds.HasValue ||
                    assistLevel != YardCraneLocalAssistLevel.Guided ||
                    scoreTrustLevel != YardCraneLocalScoreTrustLevel.Unscored ||
                    returnPage != YardCraneLocalTrainingReturnPage.LearningSelection ||
                    displayInfo.Instruction.Length == 0)
                {
                    throw new ArgumentException(
                        "Open guided learning requires instructions, guided assistance, count-up timing and LearningSelection return.");
                }
                return;
            }

            var expectedMetrics = ExpectedMetricSetFor(exercise);
            if (expectedMetrics.Count == 0 || !target.HasTarget ||
                !expectedMetrics.Contains(target.Metric) || timing.Mode != YardCraneLocalTimingMode.CountUp ||
                timing.DurationSeconds.HasValue || assistLevel != YardCraneLocalAssistLevel.Guided ||
                scoreTrustLevel != YardCraneLocalScoreTrustLevel.Unscored ||
                returnPage != YardCraneLocalTrainingReturnPage.LearningSelection ||
                displayInfo.Instruction.Length == 0 || target.DisplayText.Length == 0 ||
                (exercise == YardCraneLocalTrainingExercise.SpreaderZeroing &&
                 target.Value.GetValueOrDefault() != 0d) ||
                (exercise == YardCraneLocalTrainingExercise.SpreaderRotation &&
                 target.Value.GetValueOrDefault() == 0d))
            {
                throw new ArgumentException(
                    "GuidedLearning requires a matching numeric target, guided assistance, count-up timing and LearningSelection return.");
            }

            // Linked exercises (expectedMetrics.Count > 1) must have every extra axis carry a
            // real target, and the full set (primary + additional) must cover expectedMetrics
            // exactly - no missing axis, no duplicate axis, no stray axis outside the set.
            var coveredMetrics = new System.Collections.Generic.HashSet<YardCraneLocalTrainingMetric> { target.Metric };
            foreach (var extra in additionalTargets)
            {
                if (extra == null || !extra.HasTarget || extra.DisplayText.Length == 0)
                {
                    throw new ArgumentException(
                        "Every additional guided-learning target must carry a real numeric target.",
                        nameof(additionalTargets));
                }
                if (!coveredMetrics.Add(extra.Metric))
                {
                    throw new ArgumentException(
                        "Additional guided-learning targets must each cover a distinct metric.",
                        nameof(additionalTargets));
                }
            }
            if (!coveredMetrics.SetEquals(expectedMetrics))
            {
                throw new ArgumentException(
                    "Guided-learning targets do not cover exactly the exercise's expected axis set.",
                    nameof(additionalTargets));
            }
        }

        private static void RequireAssessmentPolicy(
            YardCraneLocalTrainingExercise exercise,
            YardCraneLocalTrainingTarget target,
            YardCraneLocalTimingPolicy timing,
            YardCraneLocalAssistLevel assistLevel,
            YardCraneLocalScoreTrustLevel scoreTrustLevel,
            YardCraneLocalTrainingReturnPage returnPage,
            YardCraneLocalTaskDisplayInfo displayInfo)
        {
            var isWorkflow = exercise == YardCraneLocalTrainingExercise.PickupWorkflow ||
                             exercise == YardCraneLocalTrainingExercise.DropoffWorkflow;
            if (!isWorkflow || target.HasTarget || timing.Mode != YardCraneLocalTimingMode.CountDown ||
                timing.StartCountdownSeconds <= 0 || !timing.DurationSeconds.HasValue ||
                assistLevel != YardCraneLocalAssistLevel.None ||
                scoreTrustLevel != YardCraneLocalScoreTrustLevel.Unscored ||
                returnPage != YardCraneLocalTrainingReturnPage.StudentHome ||
                displayInfo.Instruction.Length == 0)
            {
                throw new ArgumentException(
                    "Assessment requires a timed local workflow, no assistance, no official score and StudentHome return.");
            }
        }

        // 2026-09-11: replaces the old single-metric ExpectedMetricFor. Single-axis exercises
        // return a one-element set (identical behavior to before); the four Linked* exercises
        // return the 2-3 axes manual §5.2.5-8 describes as one combined cursor target.
        private static System.Collections.Generic.HashSet<YardCraneLocalTrainingMetric> ExpectedMetricSetFor(
            YardCraneLocalTrainingExercise exercise)
        {
            switch (exercise)
            {
                case YardCraneLocalTrainingExercise.GantryMovement:
                case YardCraneLocalTrainingExercise.SkillGantryStraightLine:
                    return new System.Collections.Generic.HashSet<YardCraneLocalTrainingMetric>
                        { YardCraneLocalTrainingMetric.GantryPosition };
                case YardCraneLocalTrainingExercise.TrolleyMovement:
                    return new System.Collections.Generic.HashSet<YardCraneLocalTrainingMetric>
                        { YardCraneLocalTrainingMetric.TrolleyPosition };
                case YardCraneLocalTrainingExercise.SpreaderHoisting:
                    return new System.Collections.Generic.HashSet<YardCraneLocalTrainingMetric>
                        { YardCraneLocalTrainingMetric.SpreaderHeight };
                case YardCraneLocalTrainingExercise.SpreaderRotation:
                    return new System.Collections.Generic.HashSet<YardCraneLocalTrainingMetric>
                        { YardCraneLocalTrainingMetric.SpreaderRotation };
                case YardCraneLocalTrainingExercise.SpreaderZeroing:
                    return new System.Collections.Generic.HashSet<YardCraneLocalTrainingMetric>
                        { YardCraneLocalTrainingMetric.SpreaderZeroing };
                case YardCraneLocalTrainingExercise.LinkedGantryTrolley:
                    return new System.Collections.Generic.HashSet<YardCraneLocalTrainingMetric>
                    {
                        YardCraneLocalTrainingMetric.GantryPosition,
                        YardCraneLocalTrainingMetric.TrolleyPosition
                    };
                case YardCraneLocalTrainingExercise.LinkedGantryHoist:
                    return new System.Collections.Generic.HashSet<YardCraneLocalTrainingMetric>
                    {
                        YardCraneLocalTrainingMetric.GantryPosition,
                        YardCraneLocalTrainingMetric.SpreaderHeight
                    };
                case YardCraneLocalTrainingExercise.LinkedTrolleyHoist:
                    return new System.Collections.Generic.HashSet<YardCraneLocalTrainingMetric>
                    {
                        YardCraneLocalTrainingMetric.TrolleyPosition,
                        YardCraneLocalTrainingMetric.SpreaderHeight
                    };
                case YardCraneLocalTrainingExercise.LinkedGantryTrolleyHoist:
                    return new System.Collections.Generic.HashSet<YardCraneLocalTrainingMetric>
                    {
                        YardCraneLocalTrainingMetric.GantryPosition,
                        YardCraneLocalTrainingMetric.TrolleyPosition,
                        YardCraneLocalTrainingMetric.SpreaderHeight
                    };
                default:
                    return new System.Collections.Generic.HashSet<YardCraneLocalTrainingMetric>();
            }
        }
    }

    /// <summary>
    /// Process-local registry for the frozen context. Begin never replaces an active run;
    /// TryClear(sessionId) prevents a stale callback from clearing a newer session.
    /// </summary>
    public static class YardCraneLocalTrainingSession
    {
        private static readonly object SyncRoot = new object();
        private static YardCraneLocalTrainingSessionContext currentContext;

        public static YardCraneLocalTrainingSessionContext CurrentContext
        {
            get
            {
                lock (SyncRoot) return currentContext;
            }
        }

        public static bool IsActive => CurrentContext != null;
        public static Guid SessionId => CurrentContext?.SessionId ?? Guid.Empty;

        public static void Begin(YardCraneLocalTrainingSessionContext context)
        {
            if (!TryBegin(context, out var error)) throw new InvalidOperationException(error);
        }

        public static bool TryBegin(YardCraneLocalTrainingSessionContext context, out string error)
        {
            if (context == null)
            {
                error = "Local training context is required.";
                return false;
            }

            lock (SyncRoot)
            {
                if (ReferenceEquals(currentContext, context))
                {
                    error = string.Empty;
                    return true;
                }

                if (currentContext != null)
                {
                    error = "A local training session is already active. Clear it before beginning another run.";
                    return false;
                }

                currentContext = context;
                error = string.Empty;
                return true;
            }
        }

        public static bool TryClear(Guid expectedSessionId)
        {
            if (expectedSessionId == Guid.Empty) return false;
            lock (SyncRoot)
            {
                if (currentContext == null || currentContext.SessionId != expectedSessionId) return false;
                currentContext = null;
                return true;
            }
        }

        public static void Clear()
        {
            lock (SyncRoot) currentContext = null;
        }
    }
}
