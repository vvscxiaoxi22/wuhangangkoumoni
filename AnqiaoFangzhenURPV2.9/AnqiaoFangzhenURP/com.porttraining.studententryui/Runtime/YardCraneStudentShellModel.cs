using System;
using System.Collections.Generic;

namespace PortTraining.YardCraneSimulator.Shell
{
    public enum YardCraneStudentShellPage
    {
        Login = 0,
        Home = 1,
        Launching = 2
    }

    public enum YardCraneShellRole
    {
        Student = 0,
        Teacher = 1
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
        public static bool IsActive { get; private set; }
        public static Guid LocalSessionId { get; private set; }
        public static YardCraneLocalStudentProfile Student { get; private set; }
        public static YardCraneLocalTrainingKind TrainingKind { get; private set; }

        public static void Begin(YardCraneLocalStudentProfile student, YardCraneLocalTrainingKind trainingKind)
        {
            Student = student ?? throw new ArgumentNullException(nameof(student));
            LocalSessionId = Guid.NewGuid();
            TrainingKind = trainingKind;
            IsActive = true;
        }

        public static void Clear()
        {
            IsActive = false;
            LocalSessionId = Guid.Empty;
            Student = null;
            TrainingKind = YardCraneLocalTrainingKind.FreePractice;
        }
    }
}
