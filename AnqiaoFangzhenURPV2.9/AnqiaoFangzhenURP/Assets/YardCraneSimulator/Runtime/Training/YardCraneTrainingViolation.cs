using System;

namespace PortTraining.YardCraneSimulator.Training
{
    public enum YardCraneViolationCategory
    {
        Safety = 0,
        Skill = 1
    }

    public enum YardCraneViolationType
    {
        UnsafeUnlock = 0,
        GantryOverspeed = 1,
        TrolleyOverspeed = 2,
        HoistOverspeed = 3,
        ExcessiveSway = 4,
        HardImpact = 5
    }

    public readonly struct YardCraneTrainingViolation
    {
        public YardCraneTrainingViolation(
            YardCraneViolationCategory category, YardCraneViolationType type, int pointsDeducted,
            float occurredAtSeconds, string detail)
        {
            Category = category;
            Type = type;
            PointsDeducted = pointsDeducted;
            OccurredAtSeconds = occurredAtSeconds;
            Detail = detail ?? string.Empty;
        }

        public YardCraneViolationCategory Category { get; }
        public YardCraneViolationType Type { get; }
        public int PointsDeducted { get; }
        public float OccurredAtSeconds { get; }
        public string Detail { get; }
    }
}
