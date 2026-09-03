using System;

namespace PortTraining.Core.Tasks
{
    public enum AssignmentType
    {
        Standalone,
        Collaborative
    }

    public enum TrainingTaskMode
    {
        Practice,
        Training,
        Exam,
        Fault
    }

    public enum TaskExecutionState
    {
        PendingAcceptance,
        Accepted,
        Preflight,
        Ready,
        Running,
        Paused,
        Completed,
        Scored,
        Archived,
        Terminated
    }

    [Serializable]
    public sealed class TaskSnapshot
    {
        public string taskId = string.Empty;
        public string taskVersion = string.Empty;
        public string taskSource = string.Empty;
        public TrainingTaskMode taskMode;
        public AssignmentType assignmentType;
        public string machineType = string.Empty;
        public string stationId = string.Empty;
        public string controlMode = string.Empty;
        public string containerId = string.Empty;
        public string sourceLocation = string.Empty;
        public string targetLocation = string.Empty;
        public string vehicleAndLane = string.Empty;
        public string scoreRuleVersion = string.Empty;
        public string sceneVersion = string.Empty;
        public string publishedAt = string.Empty;
    }
}

