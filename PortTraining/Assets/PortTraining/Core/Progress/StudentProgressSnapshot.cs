using System;

namespace PortTraining.Core.Progress
{
    [Serializable]
    public sealed class StudentProgressSnapshot
    {
        public string studentId = string.Empty;
        public int assignedTaskCount;
        public int completedTaskCount;
        public int trainingSessionCount;
        public int practiceDurationSeconds;
        public int scoredSessionCount;
        public float totalScore;
        public int evaluatedOperationCount;
        public int safeOperationCount;
        public string updatedAt = string.Empty;

        public float CompletionRate => assignedTaskCount <= 0
            ? 0f
            : Math.Clamp(completedTaskCount * 100f / assignedTaskCount, 0f, 100f);

        public float AverageScore => scoredSessionCount <= 0
            ? 0f
            : totalScore / scoredSessionCount;

        public float SafetyRate => evaluatedOperationCount <= 0
            ? 0f
            : Math.Clamp(safeOperationCount * 100f / evaluatedOperationCount, 0f, 100f);
    }
}
