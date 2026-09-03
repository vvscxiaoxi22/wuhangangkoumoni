using System;

namespace PortTraining.Core.WorkOrders
{
    public enum WorkOrderSource
    {
        Teacher,
        TOS,
        LocalPractice
    }

    public enum WorkOrderOperationType
    {
        Discharge,
        Load,
        YardTransfer,
        Practice
    }

    public enum WorkOrderTaskType
    {
        Formal,
        FaultTraining,
        FreePractice,
        Assessment
    }

    public enum WorkOrderAssignmentType
    {
        Standalone,
        Collaborative
    }

    public enum WorkOrderPriority
    {
        Normal,
        Urgent
    }

    public enum WorkOrderState
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
    public sealed class WorkOrderSnapshot
    {
        public string workOrderId = string.Empty;
        public string operationId = string.Empty;
        public string orderNumber = string.Empty;
        public string source = string.Empty;
        public string taskType = "Formal";
        public string operationType = string.Empty;
        public string assignmentType = string.Empty;
        public string priority = string.Empty;
        public string machineType = string.Empty;
        public string stationId = string.Empty;
        public string controlMode = string.Empty;
        public string workflowName = string.Empty;
        public string containerId = string.Empty;
        public string containerIsoCode = string.Empty;
        public float containerWeightTonnes;
        public string sourceLocation = string.Empty;
        public string targetLocation = string.Empty;
        public string vehicleId = string.Empty;
        public string laneId = string.Empty;
        public string partnerStationId = string.Empty;
        public string publishedAt = string.Empty;
        public string dueAt = string.Empty;
        public int assessmentDurationMinutes = 20;
        public float passingScore = 80f;
        public int maxAttempts = 1;
        public int safetyScoreWeight = 40;
        public int qualityScoreWeight = 35;
        public int efficiencyScoreWeight = 25;

        public WorkOrderSource Source => Parse<WorkOrderSource>(source, WorkOrderSource.Teacher);
        public WorkOrderTaskType TaskType => Parse<WorkOrderTaskType>(taskType, WorkOrderTaskType.Formal);
        public WorkOrderOperationType OperationType => Parse<WorkOrderOperationType>(operationType, WorkOrderOperationType.Discharge);
        public WorkOrderAssignmentType AssignmentType => Parse<WorkOrderAssignmentType>(assignmentType, WorkOrderAssignmentType.Standalone);
        public WorkOrderPriority Priority => Parse<WorkOrderPriority>(priority, WorkOrderPriority.Normal);

        private static T Parse<T>(string value, T fallback) where T : struct
        {
            return Enum.TryParse(value, true, out T parsed) ? parsed : fallback;
        }
    }

    [Serializable]
    public sealed class WorkOrderCollection
    {
        public WorkOrderSnapshot[] workOrders = Array.Empty<WorkOrderSnapshot>();
    }
}
