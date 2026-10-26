using System;
using System.Linq;
using System.Reflection;
using PortTraining.YardCraneSimulator.Presentation;
using PortTraining.YardCraneSimulator.Shell;
using PortTraining.YardCraneSimulator.Training;

internal static class Program
{
    private static int assertions;

    private static void Main()
    {
        Require(YardCraneLocalStudentProfile.TryCreate("  YC-001  ", "  张 三  ", out var profile, out _),
            "valid-local-profile");
        Require(profile.StudentNumber == "YC-001" && profile.DisplayName == "张 三", "profile-normalized");
        Require(profile.Role == YardCraneShellRole.Student, "legacy-profile-overload-is-student");
        Require(YardCraneLocalStudentProfile.TryLogin(
                YardCraneShellRole.Student, "  YC-002  ", "  李四  ", out var secondStudent, out var studentError) &&
                secondStudent.AccountId == "YC-002" && secondStudent.DisplayName == "李四" &&
                secondStudent.Role == YardCraneShellRole.Student && studentError == string.Empty,
            "student-login-requires-and-preserves-separate-identity-fields");
        RejectLogin(YardCraneShellRole.Student, null, "张三", "null-student-number-rejected");
        RejectLogin(YardCraneShellRole.Student, string.Empty, "张三", "empty-student-number-rejected");
        RejectLogin(YardCraneShellRole.Student, "   ", "张三", "blank-student-number-rejected");
        RejectLogin(YardCraneShellRole.Student, "A", "张三", "short-student-number-rejected");
        RejectLogin(YardCraneShellRole.Student, new string('A', 33), "张三", "long-student-number-rejected");
        RejectLogin(YardCraneShellRole.Student, "YC/001", "张三", "unsafe-student-number-rejected");
        RejectLogin(YardCraneShellRole.Student, "YC-001", null, "null-display-name-rejected");
        RejectLogin(YardCraneShellRole.Student, "YC-001", string.Empty, "empty-display-name-rejected");
        RejectLogin(YardCraneShellRole.Student, "YC-001", "   ", "blank-display-name-rejected");
        RejectLogin(YardCraneShellRole.Student, "YC-001", "张", "short-display-name-rejected");
        RejectLogin(YardCraneShellRole.Student, "YC-001", new string('张', 25), "long-display-name-rejected");
        RejectLogin(YardCraneShellRole.Student, "YC-001", "张@三", "unsafe-display-name-rejected");
        RejectLogin(YardCraneShellRole.Student, "YC-001", "张\n三", "control-character-display-name-rejected");

        Require(YardCraneLocalStudentProfile.TryLogin(
                YardCraneShellRole.Teacher, "admin", "admin", out var teacher, out var teacherError),
            "valid-local-administrator-login");
        Require(teacher.Role == YardCraneShellRole.Teacher && teacher.AccountId == "admin" &&
                teacher.DisplayName == "管理员" && teacherError == string.Empty,
            "teacher-has-fixed-administrator-identity");
        Require(YardCraneLocalStudentProfile.TryLogin(
                YardCraneShellRole.Teacher, "  admin  ", "admin", out var trimmedTeacher, out _) &&
                trimmedTeacher.AccountId == "admin",
            "teacher-account-allows-trimming");
        RejectLogin(YardCraneShellRole.Teacher, "T-001", "测试教师", "arbitrary-teacher-identity-rejected");
        RejectLogin(YardCraneShellRole.Teacher, "administrator", "admin", "wrong-teacher-account-rejected");
        RejectLogin(YardCraneShellRole.Teacher, "Admin", "admin", "teacher-account-case-is-significant");
        RejectLogin(YardCraneShellRole.Teacher, null, "admin", "null-teacher-account-rejected");
        RejectLogin(YardCraneShellRole.Teacher, string.Empty, "admin", "empty-teacher-account-rejected");
        RejectLogin(YardCraneShellRole.Teacher, "admin", "wrong", "wrong-teacher-password-rejected");
        RejectLogin(YardCraneShellRole.Teacher, "admin", "Admin", "teacher-password-case-is-significant");
        RejectLogin(YardCraneShellRole.Teacher, "admin", null, "null-teacher-password-rejected");
        RejectLogin(YardCraneShellRole.Teacher, "admin", string.Empty, "empty-teacher-password-rejected");
        RejectLogin(YardCraneShellRole.Teacher, "admin", "   ", "blank-teacher-password-rejected");
        RejectLogin(YardCraneShellRole.Teacher, "admin", " admin", "leading-password-space-rejected");
        RejectLogin(YardCraneShellRole.Teacher, "admin", "admin ", "trailing-password-space-rejected");
        RejectLogin((YardCraneShellRole)99, "admin", "admin", "unknown-role-rejected");

        var storedText = typeof(YardCraneLocalStudentProfile)
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(field => field.FieldType == typeof(string))
            .Select(field => (string)field.GetValue(teacher))
            .ToArray();
        Require(storedText.Length == 2 && storedText.Contains("admin") && storedText.Contains("管理员"),
            "teacher-profile-stores-only-account-and-display-name-no-password-copy");

        YardCraneTeacherHomeHandoff.Clear();
        YardCraneTeacherHomeHandoff.Begin(teacher);
        Require(ReferenceEquals(YardCraneTeacherHomeHandoff.PendingProfile, teacher),
            "teacher-home-handoff-preserves-authenticated-administrator");
        Require(ReferenceEquals(YardCraneTeacherHomeHandoff.Consume(), teacher) &&
                YardCraneTeacherHomeHandoff.PendingProfile == null &&
                YardCraneTeacherHomeHandoff.Consume() == null,
            "teacher-home-handoff-is-consumed-once");
        RequireThrows<ArgumentNullException>(
            () => YardCraneTeacherHomeHandoff.Begin(null),
            "teacher-home-handoff-rejects-null-profile");
        RequireThrows<ArgumentException>(
            () => YardCraneTeacherHomeHandoff.Begin(profile),
            "teacher-home-handoff-rejects-student-profile");
        YardCraneTeacherHomeHandoff.Clear();

        YardCraneStudentHomeHandoff.Begin(
            profile,
            YardCraneLocalTrainingReturnPage.LearningSelection,
            "训练场景启动失败，请检查场景配置后重试。");
        Require(ReferenceEquals(YardCraneStudentHomeHandoff.PendingProfile, profile) &&
                YardCraneStudentHomeHandoff.PendingReturnPage ==
                    YardCraneLocalTrainingReturnPage.LearningSelection &&
                YardCraneStudentHomeHandoff.PendingNotice ==
                    "训练场景启动失败，请检查场景配置后重试。",
            "failure-recovery-handoff-preserves-page-and-notice");
        var recoveredProfile = YardCraneStudentHomeHandoff.Consume(
            out var recoveredPage,
            out var recoveredNotice);
        Require(ReferenceEquals(recoveredProfile, profile) &&
                recoveredPage == YardCraneLocalTrainingReturnPage.LearningSelection &&
                recoveredNotice == "训练场景启动失败，请检查场景配置后重试。" &&
                YardCraneStudentHomeHandoff.PendingProfile == null &&
                YardCraneStudentHomeHandoff.PendingReturnPage ==
                    YardCraneLocalTrainingReturnPage.StudentHome &&
                YardCraneStudentHomeHandoff.PendingNotice == string.Empty,
            "failure-recovery-handoff-is-consumed-once");
        YardCraneStudentHomeHandoff.Begin(profile);
        YardCraneStudentHomeHandoff.Clear();
        Require(YardCraneStudentHomeHandoff.PendingProfile == null &&
                YardCraneStudentHomeHandoff.PendingReturnPage ==
                    YardCraneLocalTrainingReturnPage.StudentHome &&
                YardCraneStudentHomeHandoff.PendingNotice == string.Empty,
            "student-home-handoff-clear-removes-stale-cross-role-state");

        var ownedLifecycleSession = Guid.NewGuid();
        YardCraneTrainingSessionIdentity.Begin(
            profile.StudentNumber,
            profile.DisplayName,
            ownedLifecycleSession);
        YardCraneTrainingSessionIdentity.RecordElapsedSeconds(12.75d);
        YardCraneTrainingSessionIdentity.RecordElapsedSeconds(double.NaN);
        YardCraneTrainingSessionIdentity.RecordScore(87);
        Require(Math.Abs(YardCraneTrainingSessionIdentity.ElapsedSeconds - 12.75d) < 0.0001d &&
                YardCraneTrainingSessionIdentity.CurrentScore == 87,
            "training-identity-retains-last-valid-elapsed-time-and-score");
        Require(!YardCraneTrainingSessionIdentity.TryMarkFailed(Guid.NewGuid()) &&
                YardCraneTrainingSessionIdentity.Outcome == YardCraneTrainingRunOutcome.InProgress,
            "stale-bootstrap-cannot-fail-newer-training-identity");
        Require(YardCraneTrainingSessionIdentity.TryMarkFailed(ownedLifecycleSession) &&
                YardCraneTrainingSessionIdentity.Outcome == YardCraneTrainingRunOutcome.Failed,
            "owning-bootstrap-can-finish-matching-training-identity");
        YardCraneTrainingSessionIdentity.Clear();

        var options = YardCraneStudentShellPolicy.Options;
        Require(options.Count == 3, "three-visible-training-entries");
        Require(options.Count(option => option.IsAvailable) == 1, "only-one-truthfully-available-entry");
        Require(options.Single(option => option.IsAvailable).Kind == YardCraneLocalTrainingKind.FreePractice,
            "free-practice-is-the-only-live-entry");
        Require(YardCraneStudentShellPolicy.CanLaunchTraining(YardCraneShellRole.Student),
            "student-can-launch-training");
        Require(!YardCraneStudentShellPolicy.CanLaunchTraining(YardCraneShellRole.Teacher),
            "teacher-shell-cannot-bypass-task-service-and-launch-as-student");

        var dual = YardCraneDisplayProfile.Detect(1920, 2160);
        Require(dual.Layout == YardCraneDisplayLayout.DualStacked1920By2160 &&
                dual.ReferenceWidth == 1920 && dual.ReferenceHeight == 2160,
            "1920x2160-selects-dual-stacked-profile");
        Require(Math.Abs(dual.CalculateScale(1920, 2160) - 1f) < 0.0001f,
            "dual-stacked-native-scale-is-one");
        Require(YardCraneDisplayProfile.Detect(1080, 1920).IsPortrait,
            "1080x1920-remains-true-portrait");
        Require(!YardCraneDisplayProfile.Detect(1920, 1080).IsPortrait,
            "1920x1080-remains-landscape-fallback");

        Require(!YardCraneStudentShellPolicy.RequiresBootstrap(Array.Empty<string>()), "no-args-shows-local-shell");
        Require(!YardCraneStudentShellPolicy.RequiresBootstrap(new[] { "app.exe", "--run-mode", "standalone" }),
            "standalone-shows-local-shell");
        Require(YardCraneStudentShellPolicy.RequiresBootstrap(new[] { "app.exe", "--run-mode", "managed" }),
            "managed-bypasses-local-shell");
        Require(YardCraneStudentShellPolicy.RequiresBootstrap(new[] { "app.exe", "--operation-run-id", "invalid" }),
            "incomplete-managed-launch-fails-closed-through-bootstrap");

        YardCraneFallbackSession.Clear();
        Require(!YardCraneFallbackSession.IsActive, "session-starts-clear");
        YardCraneFallbackSession.Begin(profile, YardCraneLocalTrainingKind.FreePractice);
        Require(YardCraneFallbackSession.IsActive && YardCraneFallbackSession.LocalSessionId != Guid.Empty,
            "local-session-created");
        Require(YardCraneFallbackSession.Student == profile &&
                YardCraneFallbackSession.TrainingKind == YardCraneLocalTrainingKind.FreePractice,
            "local-session-preserves-selection");
        var freePractice = YardCraneFallbackSession.CurrentContext;
        Require(freePractice != null && freePractice.SessionId == YardCraneFallbackSession.LocalSessionId &&
                freePractice.Participant.StudentNumber == profile.StudentNumber &&
                freePractice.Participant.DisplayName == profile.DisplayName,
            "legacy-entry-creates-self-contained-context");
        Require(freePractice.Mode == YardCraneLocalTrainingMode.FreePractice &&
                freePractice.Exercise == YardCraneLocalTrainingExercise.FreePractice &&
                !freePractice.Target.HasTarget &&
                freePractice.Timing.Mode == YardCraneLocalTimingMode.CountUp &&
                freePractice.Timing.StartCountdownSeconds == 0 &&
                !freePractice.Timing.DurationSeconds.HasValue &&
                freePractice.AssistLevel == YardCraneLocalAssistLevel.Standard &&
                freePractice.ScoreTrustLevel == YardCraneLocalScoreTrustLevel.LocalEstimated &&
                freePractice.ReturnPage == YardCraneLocalTrainingReturnPage.StudentHome &&
                freePractice.DisplayInfo.Title == "自由练习",
            "free-practice-policy-is-frozen-and-explicit");
        Require(!YardCraneFallbackSession.TryClear(Guid.NewGuid()) &&
                ReferenceEquals(YardCraneFallbackSession.CurrentContext, freePractice),
            "stale-session-cannot-clear-active-context");
        YardCraneFallbackSession.Clear();
        Require(!YardCraneFallbackSession.IsActive && YardCraneFallbackSession.Student == null,
            "session-clears-without-persisting-identity");

        var participant = new YardCraneLocalTrainingParticipant(profile.StudentNumber, profile.DisplayName);
        var gantryTarget = new YardCraneLocalTrainingTarget(
            YardCraneLocalTrainingMetric.GantryPosition,
            YardCraneLocalTargetReferenceFrame.GantryHomeRelativeMeters,
            -30d,
            0.25d,
            "-30m");
        var guidedDisplay = new YardCraneLocalTaskDisplayInfo(
            "学习训练 · 大车移动",
            "将大车移动到目标位置并稳定保持。",
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty);
        var guided = YardCraneLocalTrainingSessionContext.CreateGuidedLearning(
            participant,
            YardCraneLocalTrainingExercise.GantryMovement,
            gantryTarget,
            guidedDisplay);
        Require(guided.Mode == YardCraneLocalTrainingMode.GuidedLearning &&
                guided.Exercise == YardCraneLocalTrainingExercise.GantryMovement &&
                guided.Target.MetricId == YardCraneLocalTrainingMetricIds.GantryPosition &&
                guided.Target.ReferenceFrame == YardCraneLocalTargetReferenceFrame.GantryHomeRelativeMeters &&
                guided.Target.Value == -30d && guided.Target.Unit == "m" &&
                guided.Target.Tolerance == 0.25d && guided.Target.DisplayText == "-30m" &&
                guided.Timing.StartCountdownSeconds == 5 &&
                guided.AssistLevel == YardCraneLocalAssistLevel.Guided &&
                guided.ScoreTrustLevel == YardCraneLocalScoreTrustLevel.Unscored &&
                guided.ReturnPage == YardCraneLocalTrainingReturnPage.LearningSelection &&
                ReferenceEquals(guided.DisplayInfo, guidedDisplay) &&
                guided.DisplayInfo.DestinationSlot == string.Empty,
            "guided-learning-context-preserves-target-tolerance-and-policy");
        var relativeHoistTarget = new YardCraneLocalTrainingTarget(
            YardCraneLocalTrainingMetric.SpreaderHeight,
            YardCraneLocalTargetReferenceFrame.SpreaderStartRelativeMeters,
            1d,
            0.10d,
            "+1m");
        var relativeHoist = YardCraneLocalTrainingSessionContext.CreateGuidedLearning(
            participant,
            YardCraneLocalTrainingExercise.SpreaderHoisting,
            relativeHoistTarget,
            new YardCraneLocalTaskDisplayInfo(
                "学习训练 · 吊具起升",
                "相对本次会话首个健康样本上升 1 米。",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty));
        Require(relativeHoist.Target.MetricId == YardCraneLocalTrainingMetricIds.SpreaderHeightStartRelative &&
                relativeHoist.Target.ReferenceFrame == YardCraneLocalTargetReferenceFrame.SpreaderStartRelativeMeters &&
                relativeHoist.Target.Value == 1d && relativeHoist.Target.Unit == "m",
            "spreader-height-supports-signed-session-start-relative-target");
        var zeroingDisplay = new YardCraneLocalTaskDisplayInfo(
            "学习训练 · 吊具回零",
            "执行回零并满足所选完成策略。",
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty);
        var autoZeroTarget = new YardCraneLocalTrainingTarget(
            YardCraneLocalTrainingMetric.SpreaderZeroing,
            YardCraneLocalTargetReferenceFrame.SpreaderPhysicalRotationDegrees,
            0d,
            0.25d,
            "自动回零",
            YardCraneLocalSpreaderZeroingPolicy.AutoReturn);
        var holdZeroTarget = new YardCraneLocalTrainingTarget(
            YardCraneLocalTrainingMetric.SpreaderZeroing,
            YardCraneLocalTargetReferenceFrame.SpreaderPhysicalRotationDegrees,
            0d,
            0.25d,
            "回零后保持",
            YardCraneLocalSpreaderZeroingPolicy.ReturnAndHold,
            2d);
        var autoZeroing = YardCraneLocalTrainingSessionContext.CreateGuidedLearning(
            participant,
            YardCraneLocalTrainingExercise.SpreaderZeroing,
            autoZeroTarget,
            zeroingDisplay);
        var holdZeroing = YardCraneLocalTrainingSessionContext.CreateGuidedLearning(
            participant,
            YardCraneLocalTrainingExercise.SpreaderZeroing,
            holdZeroTarget,
            zeroingDisplay);
        Require(autoZeroing.Target.ZeroingPolicy == YardCraneLocalSpreaderZeroingPolicy.AutoReturn &&
                autoZeroing.Target.RequiredHoldSeconds == 0d &&
                holdZeroing.Target.ZeroingPolicy == YardCraneLocalSpreaderZeroingPolicy.ReturnAndHold &&
                holdZeroing.Target.RequiredHoldSeconds == 2d,
            "zeroing-options-remain-distinct-without-parsing-display-text");

        var linkedTrolleyTarget = new YardCraneLocalTrainingTarget(
            YardCraneLocalTrainingMetric.TrolleyPosition,
            YardCraneLocalTargetReferenceFrame.TrolleyLocalXMeters,
            5d,
            0.5d,
            "小车 +5m");
        var linkedDisplay = new YardCraneLocalTaskDisplayInfo(
            "学习训练 · 大车小车联动",
            "同时将大车和小车移动到目标位置并稳定保持。",
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty);
        var linked = YardCraneLocalTrainingSessionContext.CreateGuidedLearning(
            participant,
            YardCraneLocalTrainingExercise.LinkedGantryTrolley,
            gantryTarget,
            new[] { linkedTrolleyTarget },
            linkedDisplay);
        Require(linked.Exercise == YardCraneLocalTrainingExercise.LinkedGantryTrolley &&
                linked.Target.Metric == YardCraneLocalTrainingMetric.GantryPosition &&
                linked.AdditionalTargets.Length == 1 &&
                linked.AdditionalTargets[0].Metric == YardCraneLocalTrainingMetric.TrolleyPosition &&
                linked.Timing.StartCountdownSeconds == 5,
            "linked-guided-context-freezes-every-axis-and-five-second-countdown");
        var skill = YardCraneLocalTrainingSessionContext.CreateGuidedLearning(
            participant,
            YardCraneLocalTrainingExercise.SkillGantryStraightLine,
            new YardCraneLocalTrainingTarget(
                YardCraneLocalTrainingMetric.GantryPosition,
                YardCraneLocalTargetReferenceFrame.GantryHomeRelativeMeters,
                0d,
                0.5d,
                "大车 0m"),
            new YardCraneLocalTaskDisplayInfo(
                "学习训练 · 大车直线行走",
                "将大车沿直线开回相对原点并稳定保持。",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty));
        Require(skill.Exercise == YardCraneLocalTrainingExercise.SkillGantryStraightLine &&
                skill.Target.Value == 0d && skill.AdditionalTargets.Length == 0 &&
                skill.Timing.StartCountdownSeconds == 5,
            "skill-guided-context-is-a-live-five-second-training-entry");
        RequireThrows<ArgumentException>(
            () => YardCraneLocalTrainingSessionContext.CreateGuidedLearning(
                participant,
                YardCraneLocalTrainingExercise.LinkedGantryTrolley,
                gantryTarget,
                linkedDisplay),
            "linked-guided-context-rejects-a-missing-axis");
        RequireThrows<ArgumentException>(
            () => YardCraneLocalTrainingSessionContext.CreateGuidedLearning(
                participant,
                YardCraneLocalTrainingExercise.LinkedGantryTrolley,
                gantryTarget,
                new[] { gantryTarget },
                linkedDisplay),
            "linked-guided-context-rejects-a-duplicate-axis");
        Require(YardCraneFallbackSession.TryBegin(profile, guided, out var guidedBeginError) &&
                guidedBeginError == string.Empty &&
                ReferenceEquals(YardCraneFallbackSession.CurrentContext, guided) &&
                YardCraneFallbackSession.LocalSessionId == guided.SessionId,
            "explicit-context-begins-through-compatible-shell-facade");
        var otherGuided = YardCraneLocalTrainingSessionContext.CreateGuidedLearning(
            participant,
            YardCraneLocalTrainingExercise.GantryMovement,
            gantryTarget,
            guidedDisplay);
        Require(!YardCraneFallbackSession.TryBegin(profile, otherGuided, out var overlappingError) &&
                !string.IsNullOrWhiteSpace(overlappingError) &&
                ReferenceEquals(YardCraneFallbackSession.CurrentContext, guided),
            "active-context-cannot-be-replaced");
        Require(YardCraneFallbackSession.TryClear(guided.SessionId) &&
                !YardCraneFallbackSession.IsActive && YardCraneFallbackSession.Student == null,
            "matching-session-clears-context-and-shell-profile");

        var assessmentDisplay = new YardCraneLocalTaskDisplayInfo(
            "本地模拟考核 · 标准提箱",
            "仅验证本地操作流程，不生成或提交正式成绩。",
            "堆场 A01",
            "车道 2",
            "TEST-LOCAL-01",
            "A01-02-03");
        var assessment = YardCraneLocalTrainingSessionContext.CreateAssessment(
            participant,
            YardCraneLocalTrainingExercise.PickupWorkflow,
            assessmentDisplay,
            300);
        Require(assessment.Mode == YardCraneLocalTrainingMode.Assessment &&
                assessment.Exercise == YardCraneLocalTrainingExercise.PickupWorkflow &&
                !assessment.Target.HasTarget &&
                assessment.Timing.Mode == YardCraneLocalTimingMode.CountDown &&
                assessment.Timing.DurationSeconds == 300 &&
                assessment.Timing.StartCountdownSeconds == 5 &&
                assessment.AssistLevel == YardCraneLocalAssistLevel.None &&
                assessment.ScoreTrustLevel == YardCraneLocalScoreTrustLevel.Unscored &&
                assessment.ReturnPage == YardCraneLocalTrainingReturnPage.StudentHome &&
                assessment.DisplayInfo.TargetContainer == "TEST-LOCAL-01" &&
                assessment.DisplayInfo.DestinationSlot == "A01-02-03",
            "assessment-context-is-timed-unassisted-unscored-local-workflow");

        YardCraneFallbackSession.Begin(profile, YardCraneLocalTrainingKind.PickupWorkflow);
        Require(YardCraneFallbackSession.TrainingKind == YardCraneLocalTrainingKind.PickupWorkflow &&
                YardCraneFallbackSession.CurrentContext.Mode == YardCraneLocalTrainingMode.Assessment &&
                YardCraneFallbackSession.CurrentContext.Exercise == YardCraneLocalTrainingExercise.PickupWorkflow &&
                YardCraneFallbackSession.CurrentContext.Timing.DurationSeconds == 600 &&
                YardCraneFallbackSession.CurrentContext.ScoreTrustLevel == YardCraneLocalScoreTrustLevel.Unscored,
            "legacy-pickup-entry-maps-to-explicit-local-assessment-policy");
        var legacyPickup = YardCraneFallbackSession.CurrentContext;
        Require(YardCraneFallbackSession.TryBegin(profile, legacyPickup, out var idempotentError) &&
                idempotentError == string.Empty && YardCraneFallbackSession.Student == profile,
            "same-profile-and-context-begin-is-idempotent");
        Require(YardCraneLocalStudentProfile.TryCreate(
                profile.StudentNumber,
                profile.DisplayName,
                out var equivalentProfile,
                out _) &&
                !YardCraneFallbackSession.TryBegin(equivalentProfile, legacyPickup, out var replacementError) &&
                !string.IsNullOrWhiteSpace(replacementError) && YardCraneFallbackSession.Student == profile,
            "equivalent-but-distinct-profile-cannot-rebind-active-context");
        YardCraneFallbackSession.Clear();
        YardCraneFallbackSession.Begin(profile, YardCraneLocalTrainingKind.DropoffWorkflow);
        Require(YardCraneFallbackSession.TrainingKind == YardCraneLocalTrainingKind.DropoffWorkflow &&
                YardCraneFallbackSession.CurrentContext.Mode == YardCraneLocalTrainingMode.Assessment &&
                YardCraneFallbackSession.CurrentContext.Exercise == YardCraneLocalTrainingExercise.DropoffWorkflow,
            "legacy-dropoff-entry-preserves-legacy-kind-in-explicit-context");
        YardCraneFallbackSession.Clear();

        YardCraneFallbackSession.Begin(profile, YardCraneLocalTrainingKind.FreePractice);
        YardCraneLocalTrainingSession.Clear();
        YardCraneLocalTrainingSession.Begin(otherGuided);
        Require(YardCraneFallbackSession.IsActive &&
                ReferenceEquals(YardCraneFallbackSession.CurrentContext, otherGuided) &&
                YardCraneFallbackSession.Student == null,
            "shell-profile-never-drifts-onto-context-started-outside-facade");
        YardCraneLocalTrainingSession.Clear();
        YardCraneFallbackSession.Clear();

        RequirePubliclyImmutable(
            "session-models-have-no-public-setters",
            typeof(YardCraneLocalTrainingParticipant),
            typeof(YardCraneLocalTrainingTarget),
            typeof(YardCraneLocalTimingPolicy),
            typeof(YardCraneLocalTaskDisplayInfo),
            typeof(YardCraneLocalTrainingSessionContext));
        RequireThrows<ArgumentException>(
            () => YardCraneLocalTrainingSessionContext.Create(
                participant,
                YardCraneLocalTrainingMode.Assessment,
                YardCraneLocalTrainingExercise.PickupWorkflow,
                YardCraneLocalTrainingTarget.None,
                YardCraneLocalTimingPolicy.CountDown(300),
                YardCraneLocalAssistLevel.None,
                YardCraneLocalScoreTrustLevel.AuthorityCalculated,
                YardCraneLocalTrainingReturnPage.StudentHome,
                assessmentDisplay),
            "local-session-cannot-claim-authority-score");
        RequireThrows<ArgumentException>(
            () => YardCraneLocalTrainingSessionContext.CreateGuidedLearning(
                participant,
                YardCraneLocalTrainingExercise.TrolleyMovement,
                gantryTarget,
                guidedDisplay),
            "guided-exercise-rejects-mismatched-target-metric");
        RequireThrows<ArgumentException>(
            () => new YardCraneLocalTrainingTarget(
                YardCraneLocalTrainingMetric.SpreaderZeroing,
                YardCraneLocalTargetReferenceFrame.SpreaderPhysicalRotationDegrees,
                1d,
                0.25d,
                "1°",
                YardCraneLocalSpreaderZeroingPolicy.AutoReturn),
            "zeroing-exercise-requires-zero-target");
        RequireThrows<ArgumentException>(
            () => new YardCraneLocalTrainingTarget(
                YardCraneLocalTrainingMetric.SpreaderZeroing,
                YardCraneLocalTargetReferenceFrame.SpreaderPhysicalRotationDegrees,
                0d,
                0.25d,
                "回零后保持",
                YardCraneLocalSpreaderZeroingPolicy.ReturnAndHold,
                0d),
            "return-and-hold-requires-positive-hold-duration");
        RequireThrows<ArgumentOutOfRangeException>(
            () => new YardCraneLocalTrainingTarget(
                YardCraneLocalTrainingMetric.GantryPosition,
                YardCraneLocalTargetReferenceFrame.GantryHomeRelativeMeters,
                double.NaN,
                0.25d,
                "invalid"),
            "target-rejects-nan-value");
        RequireThrows<ArgumentOutOfRangeException>(
            () => new YardCraneLocalTrainingTarget(
                YardCraneLocalTrainingMetric.GantryPosition,
                YardCraneLocalTargetReferenceFrame.GantryHomeRelativeMeters,
                double.PositiveInfinity,
                0.25d,
                "invalid"),
            "target-rejects-infinite-value");
        RequireThrows<ArgumentOutOfRangeException>(
            () => new YardCraneLocalTrainingTarget(
                YardCraneLocalTrainingMetric.GantryPosition,
                YardCraneLocalTargetReferenceFrame.GantryHomeRelativeMeters,
                1d,
                0d,
                "invalid"),
            "target-rejects-non-positive-tolerance");
        RequireThrows<ArgumentException>(
            () => new YardCraneLocalTrainingTarget(
                YardCraneLocalTrainingMetric.GantryPosition,
                YardCraneLocalTargetReferenceFrame.TrolleyLocalXMeters,
                1d,
                0.25d,
                "invalid"),
            "target-rejects-mismatched-reference-frame");
        var zeroRotationTarget = new YardCraneLocalTrainingTarget(
            YardCraneLocalTrainingMetric.SpreaderRotation,
            YardCraneLocalTargetReferenceFrame.SpreaderPhysicalRotationDegrees,
            0d,
            0.25d,
            "0°");
        RequireThrows<ArgumentException>(
            () => YardCraneLocalTrainingSessionContext.CreateGuidedLearning(
                participant,
                YardCraneLocalTrainingExercise.SpreaderRotation,
                zeroRotationTarget,
                guidedDisplay),
            "rotation-exercise-requires-explicit-non-zero-signed-target");
        RequireThrows<ArgumentException>(
            () => YardCraneLocalTrainingSessionContext.CreateGuidedLearning(
                participant,
                YardCraneLocalTrainingExercise.PickupWorkflow,
                YardCraneLocalTrainingTarget.None,
                guidedDisplay),
            "guided-learning-rejects-workflow-without-numeric-goal");
        var teacherContext = YardCraneLocalTrainingSessionContext.CreateFreePractice(
            new YardCraneLocalTrainingParticipant(teacher.StudentNumber, teacher.DisplayName));
        Require(!YardCraneFallbackSession.TryBegin(teacher, teacherContext, out var teacherBeginError) &&
                !string.IsNullOrWhiteSpace(teacherBeginError) &&
                !YardCraneFallbackSession.IsActive,
            "teacher-profile-cannot-begin-local-training-through-facade");

        Console.WriteLine($"[YC_STUDENT_SHELL_HARNESS_PASS] assertions={assertions}, unityPlay=false");
    }

    private static void RejectLogin(YardCraneShellRole role, string account, string nameOrPassword, string name)
    {
        Require(!YardCraneLocalStudentProfile.TryLogin(role, account, nameOrPassword, out var profile, out var error) &&
                profile == null && !string.IsNullOrWhiteSpace(error), name);
    }

    private static void Require(bool condition, string name)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(name);
        Console.WriteLine("PASS " + name);
    }

    private static void RequirePubliclyImmutable(string name, params Type[] types)
    {
        var immutable = types.All(type => type
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .All(property => property.SetMethod == null || !property.SetMethod.IsPublic));
        Require(immutable, name);
    }

    private static void RequireThrows<TException>(Action action, string name)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            Require(true, name);
            return;
        }

        Require(false, name);
    }
}
