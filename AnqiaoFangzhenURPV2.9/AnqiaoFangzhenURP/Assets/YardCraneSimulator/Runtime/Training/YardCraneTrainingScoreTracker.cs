using System;
using System.Collections.Generic;

namespace PortTraining.YardCraneSimulator.Training
{
    /// <summary>
    /// Pure C# safety/skill deduction ledger for one training run. Unity-independent so it
    /// can be exercised from a Harness the same way YardCraneAnchorInterlock/
    /// YardCraneGantryDriveModel are. Continuous conditions (speed, sway) are recorded
    /// edge-triggered via the UpdateXxx methods - one violation per "went from under the
    /// limit to over it", not one per physics tick while still over it. Discrete actions
    /// (the unsafe-unlock case) go through RecordViolation directly.
    /// </summary>
    public sealed class YardCraneTrainingScoreTracker
    {
        public const int StartingScore = 100;

        private readonly List<YardCraneTrainingViolation> violations = new();
        private bool gantryOverspeedActive;
        private bool trolleyOverspeedActive;
        private bool hoistOverspeedActive;
        private bool swayExceededActive;

        public int TotalScore { get; private set; } = StartingScore;
        public IReadOnlyList<YardCraneTrainingViolation> Violations => violations;

        public void Reset()
        {
            violations.Clear();
            TotalScore = StartingScore;
            gantryOverspeedActive = false;
            trolleyOverspeedActive = false;
            hoistOverspeedActive = false;
            swayExceededActive = false;
        }

        public void RecordViolation(
            YardCraneViolationCategory category, YardCraneViolationType type, int pointsDeducted,
            float occurredAtSeconds, string detail)
        {
            violations.Add(new YardCraneTrainingViolation(category, type, pointsDeducted, occurredAtSeconds, detail));
            TotalScore = Math.Max(0, TotalScore - pointsDeducted);
        }

        public void UpdateGantrySpeed(bool overLimit, int pointsDeducted, float occurredAtSeconds, string detail)
        {
            if (overLimit && !gantryOverspeedActive)
                RecordViolation(YardCraneViolationCategory.Skill, YardCraneViolationType.GantryOverspeed,
                    pointsDeducted, occurredAtSeconds, detail);
            gantryOverspeedActive = overLimit;
        }

        public void UpdateTrolleySpeed(bool overLimit, int pointsDeducted, float occurredAtSeconds, string detail)
        {
            if (overLimit && !trolleyOverspeedActive)
                RecordViolation(YardCraneViolationCategory.Skill, YardCraneViolationType.TrolleyOverspeed,
                    pointsDeducted, occurredAtSeconds, detail);
            trolleyOverspeedActive = overLimit;
        }

        public void UpdateHoistSpeed(bool overLimit, int pointsDeducted, float occurredAtSeconds, string detail)
        {
            if (overLimit && !hoistOverspeedActive)
                RecordViolation(YardCraneViolationCategory.Skill, YardCraneViolationType.HoistOverspeed,
                    pointsDeducted, occurredAtSeconds, detail);
            hoistOverspeedActive = overLimit;
        }

        public void UpdateSway(bool overLimit, int pointsDeducted, float occurredAtSeconds, string detail)
        {
            if (overLimit && !swayExceededActive)
                RecordViolation(YardCraneViolationCategory.Skill, YardCraneViolationType.ExcessiveSway,
                    pointsDeducted, occurredAtSeconds, detail);
            swayExceededActive = overLimit;
        }
    }
}
