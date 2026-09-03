using System;

namespace PortTraining.Core.Assessment
{
    public enum AssessmentAttemptState
    {
        NotStarted,
        InProgress,
        Completed,
        Terminated
    }

    [Serializable]
    public sealed class AssessmentAttemptSnapshot
    {
        public string studentId = string.Empty;
        public string workOrderId = string.Empty;
        public string attemptId = string.Empty;
        public string state = nameof(AssessmentAttemptState.NotStarted);
        public int attemptNumber;
        public string startedAt = string.Empty;
        public string completedAt = string.Empty;
        public int durationSeconds;
        public float score;
        public bool passed;
        public string terminationReason = string.Empty;

        public AssessmentAttemptState State => Enum.TryParse(state, true, out AssessmentAttemptState parsed)
            ? parsed
            : AssessmentAttemptState.NotStarted;
    }

    [Serializable]
    public sealed class AssessmentAttemptCollection
    {
        public AssessmentAttemptSnapshot[] attempts = Array.Empty<AssessmentAttemptSnapshot>();
    }
}
