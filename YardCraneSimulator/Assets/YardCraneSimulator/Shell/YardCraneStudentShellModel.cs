using System;
using System.Collections.Generic;
using PortTraining.YardCraneSimulator.Training;

namespace PortTraining.YardCraneSimulator.Shell
{
    // Legacy serialized values retain Home for backward compatibility. The entry shell
    // now normalizes every preview to Login; student and teacher homes live in their own
    // StudentHome and TeacherHome scenes.
    public enum YardCraneStudentShellPage
    {
        Login = 0,
        Home = 1
    }

    // The post-login student screen's own page state (YardCraneStudentSideShell). Kept as
    // a separate enum rather than reusing YardCraneStudentShellPage because the two shells
    // now own genuinely disjoint page sets - conflating them would misleadingly suggest
    // the login shell still knows about Learning/Launching.
    public enum YardCraneStudentSidePage
    {
        Home = 0,
        Learning = 1,
        Launching = 2,
        Assessment = 3
    }

    public enum YardCraneShellRole
    {
        Student = 0,
        Teacher = 1
    }

    // Which physical crane type this session targets - the yard crane (场桥, container
    // stacking in the yard, this project's original scope) or the quay crane (岸桥,
    // ship-side loading, being integrated from the Anqiao reference project). Explicit
    // = 0/1 values so a stored selection stays stable if members are reordered later.
    public enum YardCraneCraneKind
    {
        YardCrane = 0,
        QuayCrane = 1
    }

    // A teacher login is not a training session. Keep this one-shot scene handoff
    // separate from YardCraneStudentHomeHandoff and YardCraneFallbackSession so no
    // teacher identity can be mistaken for an active student run.
    public static class YardCraneTeacherHomeHandoff
    {
        public static YardCraneLocalStudentProfile PendingProfile { get; private set; }

        public static void Begin(YardCraneLocalStudentProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (profile.Role != YardCraneShellRole.Teacher)
                throw new ArgumentException("TeacherHome accepts only a teacher profile.", nameof(profile));
            PendingProfile = profile;
        }

        public static YardCraneLocalStudentProfile Consume()
        {
            var value = PendingProfile;
            PendingProfile = null;
            return value;
        }

        public static void Clear()
        {
            PendingProfile = null;
        }
    }

    public enum YardCraneLocalTrainingKind
    {
        FreePractice = 0,
        PickupWorkflow = 1,
        DropoffWorkflow = 2
    }

    public readonly struct YardCraneLocalTrainingOption
    {
        public YardCraneLocalTrainingOption(
            YardCraneLocalTrainingKind kind,
            string title,
            string description,
            bool isAvailable,
            string availabilityText)
        {
            Kind = kind;
            Title = title;
            Description = description;
            IsAvailable = isAvailable;
            AvailabilityText = availabilityText;
        }

        public YardCraneLocalTrainingKind Kind { get; }
        public string Title { get; }
        public string Description { get; }
        public bool IsAvailable { get; }
        public string AvailabilityText { get; }
    }

    public sealed class YardCraneLocalStudentProfile
    {
        private YardCraneLocalStudentProfile(
            YardCraneShellRole role,
            string studentNumber,
            string displayName)
        {
            Role = role;
            StudentNumber = studentNumber;
            DisplayName = displayName;
        }

        public YardCraneShellRole Role { get; }
        public string StudentNumber { get; }
        public string AccountId => StudentNumber;
        public string DisplayName { get; }

        public static bool TryCreate(
            string studentNumber,
            string displayName,
            out YardCraneLocalStudentProfile profile,
            out string error)
        {
            return TryLogin(
                YardCraneShellRole.Student,
                studentNumber,
                displayName,
                out profile,
                out error);
        }

        public static bool TryLogin(
            YardCraneShellRole role,
            string accountId,
            string nameOrPassword,
            out YardCraneLocalStudentProfile profile,
            out string error)
        {
            profile = null;
            var normalizedNumber = (accountId ?? string.Empty).Trim();

            if (role == YardCraneShellRole.Teacher)
            {
                if (!string.Equals(normalizedNumber, "admin", StringComparison.Ordinal) ||
                    !string.Equals(nameOrPassword, "admin", StringComparison.Ordinal))
                {
                    error = "管理员账号或密码错误，请重新输入。";
                    return false;
                }

                profile = new YardCraneLocalStudentProfile(YardCraneShellRole.Teacher, "admin", "管理员");
                error = string.Empty;
                return true;
            }

            if (role != YardCraneShellRole.Student)
            {
                error = "登录身份无效，请重新选择学生端或教师端。";
                return false;
            }

            var normalizedName = (nameOrPassword ?? string.Empty).Trim();

            if (normalizedNumber.Length < 2 || normalizedNumber.Length > 32 || !IsSafeStudentNumber(normalizedNumber))
            {
                error = "请输入 2–32 位学号，仅支持文字、字母、数字、短横线和下划线。";
                return false;
            }

            if (normalizedName.Length < 2 || normalizedName.Length > 24 || !IsSafeDisplayName(normalizedName))
            {
                error = "请输入 2–24 位姓名，不支持控制字符或特殊符号。";
                return false;
            }

            profile = new YardCraneLocalStudentProfile(role, normalizedNumber, normalizedName);
            error = string.Empty;
            return true;
        }

        private static bool IsSafeStudentNumber(string value)
        {
            foreach (var character in value)
            {
                if (!char.IsLetterOrDigit(character) && character != '-' && character != '_') return false;
            }

            return true;
        }

        private static bool IsSafeDisplayName(string value)
        {
            foreach (var character in value)
            {
                if (char.IsLetterOrDigit(character) || character == ' ' || character == '·') continue;
                return false;
            }

            return true;
        }
    }

    public static class YardCraneStudentShellPolicy
    {
        private static readonly YardCraneLocalTrainingOption[] TrainingOptions =
        {
            new YardCraneLocalTrainingOption(
                YardCraneLocalTrainingKind.FreePractice,
                "自由练习",
                "使用键盘或 Xbox 手柄进入场桥，完成大车、小车、起升和吊具基础操作。",
                true,
                "当前可用"),
            new YardCraneLocalTrainingOption(
                YardCraneLocalTrainingKind.PickupWorkflow,
                "标准提箱工单",
                "按箱位、车辆与目标位执行完整提箱流程。",
                false,
                "待工单规则接入"),
            new YardCraneLocalTrainingOption(
                YardCraneLocalTrainingKind.DropoffWorkflow,
                "标准收箱工单",
                "按来源车辆与目标箱位执行完整收箱流程。",
                false,
                "待工单规则接入")
        };

        private static readonly string[] ManagedArgumentNames =
        {
            "--operation-run-id",
            "--scene-instance-id",
            "--coordinator-pipe",
            "--gateway-pipe",
            "--gateway-lease-id",
            "--protocol-version"
        };

        public static IReadOnlyList<YardCraneLocalTrainingOption> Options => TrainingOptions;

        public static bool CanLaunchTraining(YardCraneShellRole role)
        {
            return role == YardCraneShellRole.Student;
        }

        // Any sign of a managed launch must go through Bootstrap validation. Invalid or
        // incomplete managed arguments must never fall open into the local login shell.
        public static bool RequiresBootstrap(string[] commandLineArguments)
        {
            if (commandLineArguments == null) return false;
            for (var index = 0; index < commandLineArguments.Length; index++)
            {
                var argument = commandLineArguments[index];
                if (string.Equals(argument, "--run-mode", StringComparison.OrdinalIgnoreCase) &&
                    index + 1 < commandLineArguments.Length &&
                    string.Equals(commandLineArguments[index + 1], "managed", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                foreach (var managedName in ManagedArgumentNames)
                {
                    if (string.Equals(argument, managedName, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }

            return false;
        }
    }

    public static class YardCraneFallbackSession
    {
        private const int LegacyWorkflowDurationSeconds = 600;
        private static YardCraneLocalStudentProfile currentStudent;
        private static Guid currentStudentSessionId;
        private static YardCraneLocalTrainingSessionContext currentStudentContext;

        public static YardCraneLocalTrainingSessionContext CurrentContext =>
            YardCraneLocalTrainingSession.CurrentContext;
        public static bool IsActive => YardCraneLocalTrainingSession.IsActive;
        public static Guid LocalSessionId => YardCraneLocalTrainingSession.SessionId;
        public static YardCraneLocalStudentProfile Student
        {
            get
            {
                var context = CurrentContext;
                return ReferenceEquals(context, currentStudentContext) &&
                       context != null && context.SessionId == currentStudentSessionId
                    ? currentStudent
                    : null;
            }
        }

        // Compatibility view for existing callers. New code must use CurrentContext.Mode and
        // CurrentContext.Exercise so a guided exercise is never mistaken for free practice.
        public static YardCraneLocalTrainingKind TrainingKind => LegacyKindFor(CurrentContext);

        public static void Begin(YardCraneLocalStudentProfile student, YardCraneLocalTrainingKind trainingKind)
        {
            if (student == null) throw new ArgumentNullException(nameof(student));

            switch (trainingKind)
            {
                case YardCraneLocalTrainingKind.FreePractice:
                    Begin(
                        student,
                        YardCraneLocalTrainingSessionContext.CreateFreePractice(CreateParticipant(student)));
                    return;
                case YardCraneLocalTrainingKind.PickupWorkflow:
                    Begin(student, CreateLegacyWorkflowContext(
                        student,
                        YardCraneLocalTrainingExercise.PickupWorkflow,
                        "标准提箱工单"));
                    return;
                case YardCraneLocalTrainingKind.DropoffWorkflow:
                    Begin(student, CreateLegacyWorkflowContext(
                        student,
                        YardCraneLocalTrainingExercise.DropoffWorkflow,
                        "标准收箱工单"));
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(trainingKind));
            }
        }

        public static void Begin(
            YardCraneLocalStudentProfile student,
            YardCraneLocalTrainingSessionContext context)
        {
            if (!TryBegin(student, context, out var error)) throw new InvalidOperationException(error);
        }

        public static bool TryBegin(
            YardCraneLocalStudentProfile student,
            YardCraneLocalTrainingSessionContext context,
            out string error)
        {
            if (student == null)
            {
                error = "Local student profile is required.";
                return false;
            }
            if (student.Role != YardCraneShellRole.Student)
            {
                error = "Only a student profile can begin local training.";
                return false;
            }
            if (context == null)
            {
                error = "Local training context is required.";
                return false;
            }
            if (!string.Equals(
                    student.StudentNumber,
                    context.Participant.StudentNumber,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    student.DisplayName,
                    context.Participant.DisplayName,
                    StringComparison.Ordinal))
            {
                error = "Local training context belongs to a different student profile.";
                return false;
            }

            if (CurrentContext != null)
            {
                if (ReferenceEquals(CurrentContext, context) && ReferenceEquals(currentStudent, student))
                {
                    error = string.Empty;
                    return true;
                }

                error = "A local training session is already active. Clear it before beginning another run.";
                return false;
            }

            if (!YardCraneLocalTrainingSession.TryBegin(context, out error)) return false;
            currentStudent = student;
            currentStudentSessionId = context.SessionId;
            currentStudentContext = context;
            return true;
        }

        public static void Clear()
        {
            YardCraneLocalTrainingSession.Clear();
            currentStudent = null;
            currentStudentSessionId = Guid.Empty;
            currentStudentContext = null;
        }

        public static bool TryClear(Guid expectedSessionId)
        {
            if (!YardCraneLocalTrainingSession.TryClear(expectedSessionId)) return false;
            currentStudent = null;
            currentStudentSessionId = Guid.Empty;
            currentStudentContext = null;
            return true;
        }

        private static YardCraneLocalTrainingSessionContext CreateLegacyWorkflowContext(
            YardCraneLocalStudentProfile student,
            YardCraneLocalTrainingExercise exercise,
            string title)
        {
            var participant = CreateParticipant(student);
            return YardCraneLocalTrainingSessionContext.CreateAssessment(
                participant,
                exercise,
                new YardCraneLocalTaskDisplayInfo(
                    "本地模拟考核 · " + title,
                    "仅验证本地操作流程，不生成或提交正式成绩。",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty),
                LegacyWorkflowDurationSeconds);
        }

        private static YardCraneLocalTrainingParticipant CreateParticipant(
            YardCraneLocalStudentProfile student)
        {
            return new YardCraneLocalTrainingParticipant(student.StudentNumber, student.DisplayName);
        }

        private static YardCraneLocalTrainingKind LegacyKindFor(
            YardCraneLocalTrainingSessionContext context)
        {
            if (context == null) return YardCraneLocalTrainingKind.FreePractice;
            switch (context.Exercise)
            {
                case YardCraneLocalTrainingExercise.PickupWorkflow:
                    return YardCraneLocalTrainingKind.PickupWorkflow;
                case YardCraneLocalTrainingExercise.DropoffWorkflow:
                    return YardCraneLocalTrainingKind.DropoffWorkflow;
                default:
                    return YardCraneLocalTrainingKind.FreePractice;
            }
        }
    }

    // Carries the logged-in student's profile from YardCraneStudentShell (StudentEntry
    // scene) across the SceneManager.LoadSceneAsync boundary into YardCraneStudentSideShell
    // (StudentHome scene) - a plain scene reference cannot survive a Single-mode scene
    // load, since the previous scene's GameObjects are destroyed. Deliberately a separate
    // static holder from YardCraneFallbackSession rather than reusing it: that class means
    // "a local-fallback training run is active" (checked elsewhere to trust local data over
    // a real platform session), which is a different fact from "a student just logged in and
    // is about to view their home page, not training yet" - conflating the two would make
    // IsActive lie to any code that checks it before a drill actually starts.
    public static class YardCraneStudentHomeHandoff
    {
        public static YardCraneLocalStudentProfile PendingProfile { get; private set; }
        public static YardCraneLocalTrainingReturnPage PendingReturnPage { get; private set; } =
            YardCraneLocalTrainingReturnPage.StudentHome;
        public static string PendingNotice { get; private set; } = string.Empty;

        public static void Begin(YardCraneLocalStudentProfile profile)
        {
            Begin(profile, YardCraneLocalTrainingReturnPage.StudentHome);
        }

        public static void Begin(
            YardCraneLocalStudentProfile profile,
            YardCraneLocalTrainingReturnPage returnPage)
        {
            Begin(profile, returnPage, string.Empty);
        }

        public static void Begin(
            YardCraneLocalStudentProfile profile,
            YardCraneLocalTrainingReturnPage returnPage,
            string notice)
        {
            if (!Enum.IsDefined(typeof(YardCraneLocalTrainingReturnPage), returnPage))
                throw new ArgumentOutOfRangeException(nameof(returnPage));
            PendingProfile = profile ?? throw new ArgumentNullException(nameof(profile));
            PendingReturnPage = returnPage;
            PendingNotice = notice?.Trim() ?? string.Empty;
        }

        // Consumed once by the StudentHome scene on load, then cleared - so reopening or
        // reloading that scene without a fresh login does not silently "log someone back
        // in" with stale data.
        public static YardCraneLocalStudentProfile Consume()
        {
            return Consume(out _);
        }

        public static YardCraneLocalStudentProfile Consume(
            out YardCraneLocalTrainingReturnPage returnPage)
        {
            return Consume(out returnPage, out _);
        }

        public static YardCraneLocalStudentProfile Consume(
            out YardCraneLocalTrainingReturnPage returnPage,
            out string notice)
        {
            var profile = PendingProfile;
            returnPage = PendingReturnPage;
            notice = PendingNotice;
            PendingProfile = null;
            PendingReturnPage = YardCraneLocalTrainingReturnPage.StudentHome;
            PendingNotice = string.Empty;
            return profile;
        }

        public static void Clear()
        {
            PendingProfile = null;
            PendingReturnPage = YardCraneLocalTrainingReturnPage.StudentHome;
            PendingNotice = string.Empty;
        }
    }
}
