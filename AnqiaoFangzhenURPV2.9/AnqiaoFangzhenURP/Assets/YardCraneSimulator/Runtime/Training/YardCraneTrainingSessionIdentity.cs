using System;

namespace PortTraining.YardCraneSimulator.Training
{
    public enum YardCraneTrainingRunOutcome
    {
        None = 0,
        InProgress = 1,
        Completed = 2,
        Terminated = 3,
        Failed = 4
    }

    /// <summary>
    /// The student identity for whichever training run is currently in progress, if any.
    /// Populated by the front-end that actually knows who is training (today, the Shell's
    /// local student login) and read only when a score record is saved. Deliberately holds
    /// plain strings/Guid rather than referencing Shell's own profile type, so Training has
    /// zero dependency on any specific login UI or mechanism - only Shell depends on Training,
    /// never the other way around.
    /// </summary>
    public static class YardCraneTrainingSessionIdentity
    {
        public static bool HasStudent { get; private set; }
        public static string StudentNumber { get; private set; } = string.Empty;
        public static string DisplayName { get; private set; } = string.Empty;
        public static Guid SessionId { get; private set; } = Guid.Empty;
        public static YardCraneTrainingRunOutcome Outcome { get; private set; }

        // 2026-09-11: last-known elapsed seconds for whichever run is/was current, pushed
        // continuously (not read pull-style) by YardCraneLocalTrainingModeCoordinator.Update()
        // every frame while tracking. This deliberately avoids depending on that MonoBehaviour
        // still being alive at save time (the local JSON file is written from
        // YardCraneEightRopeMaxPrototypeRig.OnDestroy - Unity does not guarantee which of the
        // two scene-scoped components tears down first, and a static push here sidesteps that
        // ordering question entirely, unlike a pull through a live component reference would).
        public static double ElapsedSeconds { get; private set; }

        public static void RecordElapsedSeconds(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0d) return;
            ElapsedSeconds = seconds;
        }

        // 2026-09-11: same push pattern as ElapsedSeconds above, for the same destroy-order
        // reason - YardCraneEightRopeMaxPrototypeRig pushes its trainingScore.TotalScore here
        // every frame so the local-training completion popup can show a score without the
        // Training-layer coordinator ever referencing the Experiments-layer Rig type (Training
        // must not depend on Experiments; only the reverse is the established direction here).
        public static int CurrentScore { get; private set; } = YardCraneTrainingScoreTracker.StartingScore;

        public static void RecordScore(int score)
        {
            CurrentScore = Math.Max(0, score);
        }

        // 2026-09-17: which YardCraneAssignedAssessmentTask (if any) this run was launched
        // against - set once by YardCraneStudentSideShell.OpenAssessment when it reads the
        // currently-published task, not re-read at save time. This avoids mis-attributing a run
        // to a newer task the teacher publishes while the student is still mid-run (see the
        // discussion record for why "read at save time" was rejected). Empty for any run not
        // launched from a teacher-assigned task (e.g. the fallback default assessment, or any
        // non-assessment training mode).
        public static string AssignedTaskId { get; private set; } = string.Empty;

        public static void RecordAssignedTaskId(string taskId)
        {
            AssignedTaskId = taskId ?? string.Empty;
        }

        public static void Begin(string studentNumber, string displayName, Guid sessionId)
        {
            StudentNumber = studentNumber ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            SessionId = sessionId;
            HasStudent = !string.IsNullOrWhiteSpace(StudentNumber);
            Outcome = HasStudent
                ? YardCraneTrainingRunOutcome.InProgress
                : YardCraneTrainingRunOutcome.None;
            ElapsedSeconds = 0d;
            CurrentScore = YardCraneTrainingScoreTracker.StartingScore;
            AssignedTaskId = string.Empty;
        }

        public static bool MarkCompleted() =>
            TryFinish(YardCraneTrainingRunOutcome.Completed, SessionId);
        public static bool MarkTerminated() =>
            TryFinish(YardCraneTrainingRunOutcome.Terminated, SessionId);
        public static bool MarkFailed() =>
            TryFinish(YardCraneTrainingRunOutcome.Failed, SessionId);

        public static bool TryMarkCompleted(Guid expectedSessionId) =>
            TryFinish(YardCraneTrainingRunOutcome.Completed, expectedSessionId);
        public static bool TryMarkTerminated(Guid expectedSessionId) =>
            TryFinish(YardCraneTrainingRunOutcome.Terminated, expectedSessionId);
        public static bool TryMarkFailed(Guid expectedSessionId) =>
            TryFinish(YardCraneTrainingRunOutcome.Failed, expectedSessionId);

        private static bool TryFinish(
            YardCraneTrainingRunOutcome outcome,
            Guid expectedSessionId)
        {
            if (!HasStudent || expectedSessionId == Guid.Empty ||
                SessionId != expectedSessionId || Outcome == YardCraneTrainingRunOutcome.None)
                return false;
            if (Outcome == outcome) return true;
            if (Outcome != YardCraneTrainingRunOutcome.InProgress) return false;
            Outcome = outcome;
            return true;
        }

        public static void Clear()
        {
            HasStudent = false;
            StudentNumber = string.Empty;
            DisplayName = string.Empty;
            SessionId = Guid.Empty;
            Outcome = YardCraneTrainingRunOutcome.None;
            ElapsedSeconds = 0d;
            CurrentScore = YardCraneTrainingScoreTracker.StartingScore;
            AssignedTaskId = string.Empty;
        }
    }
}
