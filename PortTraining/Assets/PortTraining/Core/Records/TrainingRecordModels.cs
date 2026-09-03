using System;

namespace PortTraining.Core.Records
{
    [Serializable]
    public sealed class TrainingIssueSnapshot
    {
        public string category = string.Empty;
        public string description = string.Empty;
        public string occurredAt = string.Empty;
        public int severity;
    }

    [Serializable]
    public sealed class TrainingRecordSnapshot
    {
        public string recordId = string.Empty;
        public string studentId = string.Empty;
        public string workOrderId = string.Empty;
        public string orderNumber = string.Empty;
        public string taskName = string.Empty;
        public string taskType = string.Empty;
        public string operationType = string.Empty;
        public string machineType = string.Empty;
        public string controlMode = string.Empty;
        public string assignmentType = string.Empty;
        public string route = string.Empty;
        public string containerId = string.Empty;
        public string completedAt = string.Empty;
        public int durationSeconds;
        public bool completed;
        public bool hasScore;
        public float score;
        public int evaluatedOperations;
        public int safeOperations;
        public float positioningScore;
        public float efficiencyScore;
        public float processScore;
        public float collaborationScore;
        public string teacherComment = string.Empty;
        public TrainingIssueSnapshot[] issues = Array.Empty<TrainingIssueSnapshot>();

        public float SafetyRate => evaluatedOperations <= 0
            ? 0f
            : Math.Clamp(safeOperations * 100f / evaluatedOperations, 0f, 100f);
    }

    [Serializable]
    public sealed class TrainingRecordCollection
    {
        public TrainingRecordSnapshot[] records = Array.Empty<TrainingRecordSnapshot>();
    }
}
