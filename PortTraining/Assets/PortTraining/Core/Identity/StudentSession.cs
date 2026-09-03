using System;

namespace PortTraining.Core.Identity
{
    public enum StudentRole
    {
        QC_DRIVER,
        YC_DRIVER
    }

    public enum StudentLoginMode
    {
        Online,
        OfflineCache
    }

    [Serializable]
    public sealed class StudentSession
    {
        public string sessionId = string.Empty;
        public string studentId = string.Empty;
        public string studentName = string.Empty;
        public StudentRole role;
        public string stationId = string.Empty;
        public StudentLoginMode loginMode;
        public string loginAt = string.Empty;
    }
}

