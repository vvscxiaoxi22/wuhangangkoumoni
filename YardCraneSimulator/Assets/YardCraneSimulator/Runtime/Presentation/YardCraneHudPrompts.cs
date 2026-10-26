using System;
using System.Collections.Generic;

namespace PortTraining.YardCraneSimulator.Presentation
{
    /// <summary>
    /// Presentation-only HUD prompts. These values are not Gateway, PLC, TOS or score contracts.
    /// A task/assessment service may map its authoritative data into these view models.
    /// </summary>
    public enum YardCraneHudPromptKind
    {
        None = 0,
        TaskBrief = 1,
        Countdown = 2,
        Confirmation = 3,
        Error = 4,
        Completion = 5,
        AssessmentDetails = 6,
        ScoreSubmission = 7
    }

    public sealed class YardCraneHudTaskPrompt
    {
        public string Title { get; set; } = "任务提示";
        public string Instruction { get; set; } = "—";
        public string SourceBay { get; set; } = "—";
        public string DestinationBay { get; set; } = "—";
        public string TargetContainer { get; set; } = "—";
        public string DestinationSlot { get; set; } = "—";
        public int SourceCellIndex { get; set; } = -1;
        public int DestinationCellIndex { get; set; } = -1;

        // 2026-09-11: text for the prompt's single close/acknowledge action. Defaults to the
        // generic "×" this prompt always showed before this field existed; a caller starting a
        // guided drill passes an explicit action label (e.g. "开始") instead.
        public string ConfirmText { get; set; } = "×";
    }

    public sealed class YardCraneHudCountdownPrompt
    {
        public string Title { get; set; } = "倒计时";
        public int TotalSeconds { get; set; }
    }

    public sealed class YardCraneHudConfirmationPrompt
    {
        public string Title { get; set; } = "提示信息";
        public string Message { get; set; } = "—";
        public string ConfirmText { get; set; } = "确定";
        public string CancelText { get; set; } = "取消";
    }

    public sealed class YardCraneHudErrorPrompt
    {
        public string Title { get; set; } = "错误提示";
        public string Message { get; set; } = "—";
        public string AcknowledgeText { get; set; } = "我知道了";
    }

    public sealed class YardCraneHudCompletionPrompt
    {
        public string Title { get; set; } = "任务完成";
        public int? ElapsedSeconds { get; set; }
        public int? Score { get; set; }
        public string DetailsText { get; set; } = "查看错误详情";
        public string RetryText { get; set; } = "重新操作";
    }

    public readonly struct YardCraneHudAssessmentLine
    {
        public YardCraneHudAssessmentLine(string item, int pointsDeducted, string note)
        {
            Item = item ?? string.Empty;
            PointsDeducted = Math.Max(0, pointsDeducted);
            Note = note ?? string.Empty;
        }

        public string Item { get; }
        public int PointsDeducted { get; }
        public string Note { get; }
    }

    public sealed class YardCraneHudAssessmentPrompt
    {
        public string Title { get; set; } = "考核详情";
        public string Project { get; set; } = "—";
        public int? ElapsedSeconds { get; set; }
        public int? Score { get; set; }
        public IReadOnlyList<YardCraneHudAssessmentLine> Lines { get; set; } =
            Array.Empty<YardCraneHudAssessmentLine>();
    }

    public readonly struct YardCraneHudScoreSubmission
    {
        public YardCraneHudScoreSubmission(string displayName, string studentNumber, string major, string className)
        {
            DisplayName = displayName ?? string.Empty;
            StudentNumber = studentNumber ?? string.Empty;
            Major = major ?? string.Empty;
            ClassName = className ?? string.Empty;
        }

        public string DisplayName { get; }
        public string StudentNumber { get; }
        public string Major { get; }
        public string ClassName { get; }
    }

    public sealed class YardCraneHudScoreSubmissionPrompt
    {
        public string Title { get; set; } = "成绩提交";
        public string DisplayName { get; set; } = string.Empty;
        public string StudentNumber { get; set; } = string.Empty;
        public string Major { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string SubmitText { get; set; } = "确认提交";

        // Local authenticated identity should normally stay read-only. A caller must explicitly
        // opt in before the four fields can be edited; this UI never writes an official score itself.
        public bool AllowIdentityEditing { get; set; }
    }
}
