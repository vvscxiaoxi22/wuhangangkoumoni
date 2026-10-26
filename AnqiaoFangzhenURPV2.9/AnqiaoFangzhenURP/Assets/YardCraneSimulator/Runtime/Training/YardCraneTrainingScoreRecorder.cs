using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Training
{
    public readonly struct YardCraneTrainingSaveResult
    {
        public YardCraneTrainingSaveResult(
            string path,
            YardCraneTrainingRunOutcome outcome,
            YardCraneLocalScoreTrustLevel scoreTrustLevel,
            bool identityContextMatched,
            bool hasScore,
            bool countsTowardResults,
            int totalScore)
        {
            Path = path ?? string.Empty;
            Outcome = outcome;
            ScoreTrustLevel = scoreTrustLevel;
            IdentityContextMatched = identityContextMatched;
            HasScore = hasScore;
            CountsTowardResults = countsTowardResults;
            TotalScore = totalScore;
        }

        public string Path { get; }
        public YardCraneTrainingRunOutcome Outcome { get; }
        public YardCraneLocalScoreTrustLevel ScoreTrustLevel { get; }
        public bool IdentityContextMatched { get; }
        public bool HasScore { get; }
        public bool CountsTowardResults { get; }
        public int TotalScore { get; }
    }

    /// <summary>
    /// Public read-side projection of one assessment run, for the teacher-side statistics page
    /// (YardCraneTeacherSideShell). Deliberately a separate type from the private RunRecord
    /// storage format, so that format can keep changing (FormatVersion bumps) without the
    /// teacher UI's data contract moving with it.
    /// </summary>
    [Serializable]
    public sealed class YardCraneAssessmentRunSummary
    {
        public string sessionId;
        public string studentNumber;
        public string displayName;
        public string recordedAtUtc;
        public string outcome;
        // 2026-09-18: "excellent"/"pass"/"fail" - see YardCraneTrainingScoreRecorder.
        // AssessmentGradeName for the rule. Empty for any run that is not Assessment mode
        // (should not happen from ListAssessmentRuns, which already filters to Assessment, but
        // kept defensive rather than assumed).
        public string assessmentGrade;
        public double elapsedSeconds;
        public string assignedTaskId;
        public string taskTitle;
        public int violationCount;
        public string fileName;
        public YardCraneAssessmentRunViolationSummary[] violations;
    }

    [Serializable]
    public sealed class YardCraneAssessmentRunViolationSummary
    {
        // "Safety" or "Skill" (YardCraneViolationCategory.ToString()) - a Safety violation is
        // what fails an otherwise-completed run (see AssessmentGradeName).
        public string category;
        public string type;
        public int pointsDeducted;
        public string detail;
    }

    /// <summary>One entry from a student's local assessments/&lt;studentNumber&gt;.json index -
    /// see YardCraneTrainingScoreRecorder.ListAssessmentRunsForStudent. Lighter than
    /// YardCraneAssessmentRunSummary (no per-violation detail, no taskTitle) since the index is
    /// meant to answer "what did this student attempt and how did it go", not replace opening
    /// the underlying run-*.json when more detail is needed.</summary>
    [Serializable]
    public sealed class YardCraneAssessmentIndexEntrySummary
    {
        public string sessionId;
        public string recordedAtUtc;
        public string assessmentGrade;
        public string outcome;
        public double elapsedSeconds;
        public string assignedTaskId;
        public int violationCount;
        public string fileName;
    }

    /// <summary>
    /// Writes one YardCraneTrainingScoreTracker run to a local JSON file under
    /// YardCraneLocalDataPaths.RootDirectory, and - when the run is attached to a known student
    /// (YardCraneTrainingSessionIdentity.HasStudent) - appends a summary entry to that
    /// student's local index file, so their run history can be looked up without opening
    /// every run file individually. Neither is yet the NodeService wire/aggregation
    /// contract; field names are kept simple/self-describing.
    /// </summary>
    public static class YardCraneTrainingScoreRecorder
    {
        public const string FormatVersion = "2026-09-18-yc-training-score-v7-assessment-grade";
        private const string DirectoryName = "TrainingScores";
        private const string StudentIndexDirectoryName = "students";
        // 2026-09-18: assessment (PickupWorkflow) runs are deliberately Unscored (see
        // AppendToStudentIndex's comment) so they never enter the students/ index above - this
        // is a separate per-student index just for assessment attempts, keyed by the same
        // student number, so "official training score" history and "process-based assessment
        // result" history never conflate two different kinds of grading.
        private const string AssessmentIndexDirectoryName = "assessments";

#if UNITY_EDITOR
        // Play-mode verifiers must never write synthetic students into the same archive as
        // real local runs. This override does not exist in a Player build.
        public static string EditorOutputDirectoryOverride { get; set; }
#endif

        [Serializable]
        private struct ViolationRecord
        {
            public string category;
            public string type;
            public int pointsDeducted;
            public float occurredAtSeconds;
            public string detail;
        }

        [Serializable]
        private class RunRecord
        {
            public string formatVersion;
            public string recordedAtUtc;
            public string sessionId;
            public string studentNumber;
            public string displayName;
            public bool identityContextMatched;
            public string observedSessionId;
            public string observedStudentNumber;
            public string observedDisplayName;
            public string observedOutcome;
            public string trainingMode;
            public string exercise;
            public string taskTitle;
            // 2026-09-17: which YardCraneAssignedAssessmentTask this run was launched against,
            // if any - see YardCraneTrainingSessionIdentity.AssignedTaskId for why it is set
            // once at launch, not re-derived here. Empty for runs not launched from a
            // teacher-assigned task.
            public string assignedTaskId;
            public string targetMetricId;
            public bool hasTarget;
            public double targetValue;
            public string targetUnit;
            public double targetTolerance;
            // 2026-09-11: seconds from the same training-input-enabled origin the HUD's
            // visible "用时" uses, read from YardCraneTrainingSessionIdentity.ElapsedSeconds
            // (pushed every frame by the coordinator while tracking - see that field's comment
            // for why a push, not a live-component pull, is required here). 0 when no
            // coordinator ever ran for this session (e.g. a scene played outside StudentHome).
            public double elapsedSeconds;
            public string scoreTrustLevel;
            public bool isOfficial;
            public bool hasScore;
            public string outcome;
            public bool countsTowardResults;
            public int totalScore;
            // 2026-09-18: "excellent"/"pass"/"fail", computed only when trainingMode==Assessment
            // (empty string otherwise - see YardCraneTrainingScoreRecorder.AssessmentGradeName).
            // Assessment has no points-based score (see hasScore above, always false for it), so
            // this is a separate pass/fail-style verdict specific to the assessment mechanic.
            public string assessmentGrade;
            public ViolationRecord[] violations;
        }

        [Serializable]
        private struct StudentIndexEntry
        {
            public string sessionId;
            public string recordedAtUtc;
            public string outcome;
            public string trainingMode;
            public string exercise;
            public double elapsedSeconds;
            public string scoreTrustLevel;
            public bool isOfficial;
            public bool hasScore;
            public bool countsTowardResults;
            public int totalScore;
            public string fileName;
        }

        [Serializable]
        private class StudentIndex
        {
            public string studentNumber;
            public string displayName;
            public List<StudentIndexEntry> runs = new();
        }

        [Serializable]
        private struct AssessmentIndexEntry
        {
            public string sessionId;
            public string recordedAtUtc;
            public string assessmentGrade;
            public string outcome;
            public double elapsedSeconds;
            public string assignedTaskId;
            public int violationCount;
            public string fileName;
        }

        [Serializable]
        private class AssessmentIndex
        {
            public string studentNumber;
            public string displayName;
            public List<AssessmentIndexEntry> runs = new();
        }

        /// <summary>
        /// Serializes the tracker's current state and writes it to a new timestamped file.
        /// When a student identity is attached to the current session, also appends a summary
        /// to that student's local index under TrainingScores/students/. Returns the full path
        /// of the run file written, for logging.
        /// </summary>
        public static string Save(YardCraneTrainingScoreTracker tracker)
        {
            return SaveWithResult(tracker).Path;
        }

        /// <summary>
        /// Persists the run and returns the exact trust/result decision written to disk. This
        /// prevents callers from re-deriving a weaker countsTowardResults rule for logging.
        /// </summary>
        public static YardCraneTrainingSaveResult SaveWithResult(
            YardCraneTrainingScoreTracker tracker)
        {
            if (tracker == null) throw new ArgumentNullException(nameof(tracker));

            var recordedAtUtc = DateTime.UtcNow;
            var hasStudent = YardCraneTrainingSessionIdentity.HasStudent;
            var observedOutcome = YardCraneTrainingSessionIdentity.Outcome;
            var context = YardCraneLocalTrainingSession.CurrentContext;
            var contextMatchesIdentity = context != null &&
                                         context.SessionId == YardCraneTrainingSessionIdentity.SessionId;
            var outcome = contextMatchesIdentity
                ? observedOutcome
                : YardCraneTrainingRunOutcome.None;
            // Missing or mismatched context is not evidence of a locally scored run. Fail
            // closed so a stale identity cannot promote a context-less completion into the
            // student result index.
            var scoreTrust = contextMatchesIdentity
                ? context.ScoreTrustLevel
                : YardCraneLocalScoreTrustLevel.Unscored;
            // A score value is meaningful only after a completed locally-estimated run.
            // Terminated/interrupted/failed free practice must not retain the tracker's
            // default 100 as if it were a result merely because its policy permits local
            // estimation on a future successful completion path.
            var hasScore = scoreTrust == YardCraneLocalScoreTrustLevel.LocalEstimated &&
                           outcome == YardCraneTrainingRunOutcome.Completed;
            // Guided learning and local simulated assessment are deliberately Unscored.
            // They remain auditable run files but must not appear in the student's result
            // index as a completed/official score.
            var countsTowardResults = hasStudent && contextMatchesIdentity && hasScore;
            // 2026-09-18: only ever computed for a genuinely-attributed Assessment-mode run -
            // contextMatchesIdentity is already implied (an unmatched context cannot report
            // Mode==Assessment here; see trainingMode below, which uses the same guard).
            var isAssessmentMode = contextMatchesIdentity &&
                                    context.Mode == YardCraneLocalTrainingMode.Assessment;
            var assessmentGrade = isAssessmentMode ? AssessmentGradeName(outcome, tracker) : string.Empty;
            var target = contextMatchesIdentity ? context.Target : null;
            var record = new RunRecord
            {
                formatVersion = FormatVersion,
                recordedAtUtc = recordedAtUtc.ToString("O"),
                // Primary identity fields are attribution, not merely observation. A stale
                // global identity must never make an unrelated operational rig record look
                // as though it belongs to that student. Observed fields retain diagnostics.
                sessionId = contextMatchesIdentity ? context.SessionId.ToString() : string.Empty,
                studentNumber = hasStudent && contextMatchesIdentity
                    ? YardCraneTrainingSessionIdentity.StudentNumber : string.Empty,
                displayName = hasStudent && contextMatchesIdentity
                    ? YardCraneTrainingSessionIdentity.DisplayName : string.Empty,
                identityContextMatched = contextMatchesIdentity,
                observedSessionId = YardCraneTrainingSessionIdentity.SessionId == Guid.Empty
                    ? string.Empty : YardCraneTrainingSessionIdentity.SessionId.ToString(),
                observedStudentNumber = hasStudent
                    ? YardCraneTrainingSessionIdentity.StudentNumber : string.Empty,
                observedDisplayName = hasStudent
                    ? YardCraneTrainingSessionIdentity.DisplayName : string.Empty,
                observedOutcome = OutcomeName(observedOutcome),
                trainingMode = contextMatchesIdentity ? context.Mode.ToString() : "Unattributed",
                exercise = contextMatchesIdentity ? context.Exercise.ToString() : string.Empty,
                taskTitle = contextMatchesIdentity ? context.DisplayInfo.Title : string.Empty,
                assignedTaskId = YardCraneTrainingSessionIdentity.AssignedTaskId,
                targetMetricId = target?.MetricId ?? string.Empty,
                hasTarget = target?.HasTarget ?? false,
                targetValue = target?.Value ?? 0d,
                targetUnit = target?.Unit ?? string.Empty,
                targetTolerance = target?.Tolerance ?? 0d,
                elapsedSeconds = YardCraneTrainingSessionIdentity.ElapsedSeconds,
                scoreTrustLevel = scoreTrust.ToString(),
                // Unity never promotes a local record to platform authority. Official
                // scoring requires a NodeService/teacher-platform contract and receipt.
                isOfficial = false,
                hasScore = hasScore,
                outcome = OutcomeName(outcome),
                countsTowardResults = countsTowardResults,
                // JsonUtility cannot conditionally omit a field. Consumers must obey
                // hasScore; Unscored records write zero so a default 100 cannot masquerade
                // as a generated result. Violation rows remain local diagnostic evidence.
                totalScore = hasScore ? tracker.TotalScore : 0,
                assessmentGrade = assessmentGrade,
                violations = ToRecords(tracker.Violations)
            };

            var json = JsonUtility.ToJson(record, true);
            var directory = ResolveScoresDirectory();
            Directory.CreateDirectory(directory);
            var fileName = $"run-{recordedAtUtc:yyyyMMdd-HHmmss-fff}.json";
            var path = Path.Combine(directory, fileName);
            File.WriteAllText(path, json);

            // A stopped/failed/interrupted run remains as an audit record, but it must not
            // enter the student's result index as an apparent 100-point completed exercise.
            if (countsTowardResults)
            {
                AppendToStudentIndex(directory, record, fileName);
            }

            // Independent of countsTowardResults (always false for Assessment, which is
            // Unscored by design) - every attributed assessment attempt, completed or not,
            // belongs in the student's assessment history so a failed/aborted attempt is not
            // silently dropped from what the teacher can look back at.
            if (isAssessmentMode && hasStudent)
            {
                AppendToAssessmentIndex(directory, record, fileName);
            }

            return new YardCraneTrainingSaveResult(
                path,
                outcome,
                scoreTrust,
                contextMatchesIdentity,
                hasScore,
                countsTowardResults,
                record.totalScore);
        }

        // Read-modify-write of one student's local index. Not concurrency-safe against two
        // processes writing the same student's file at once - this project only ever runs one
        // Player per station, so that has never been a real scenario here (same single-writer
        // assumption YardCraneLocalDataPaths.RootDirectory's whole design already makes).
        private static void AppendToStudentIndex(string scoresDirectory, RunRecord record, string fileName)
        {
            var indexDirectory = Path.Combine(scoresDirectory, StudentIndexDirectoryName);
            Directory.CreateDirectory(indexDirectory);
            var safeFileNamePart = MakeSafeFileNamePart(record.studentNumber);
            var indexPath = Path.Combine(indexDirectory, $"{safeFileNamePart}.json");

            var index = File.Exists(indexPath)
                ? JsonUtility.FromJson<StudentIndex>(File.ReadAllText(indexPath))
                : null;
            index ??= new StudentIndex();
            index.runs ??= new List<StudentIndexEntry>();
            index.studentNumber = record.studentNumber;
            index.displayName = record.displayName;
            index.runs.Add(new StudentIndexEntry
            {
                sessionId = record.sessionId,
                recordedAtUtc = record.recordedAtUtc,
                outcome = record.outcome,
                trainingMode = record.trainingMode,
                exercise = record.exercise,
                elapsedSeconds = record.elapsedSeconds,
                scoreTrustLevel = record.scoreTrustLevel,
                isOfficial = record.isOfficial,
                hasScore = record.hasScore,
                countsTowardResults = record.countsTowardResults,
                totalScore = record.totalScore,
                fileName = fileName
            });

            File.WriteAllText(indexPath, JsonUtility.ToJson(index, true));
        }

        // 2026-09-18: parallel to AppendToStudentIndex above but for the separate assessments/
        // index (see AssessmentIndexDirectoryName's comment for why it is not the same file).
        private static void AppendToAssessmentIndex(string scoresDirectory, RunRecord record, string fileName)
        {
            var indexDirectory = Path.Combine(scoresDirectory, AssessmentIndexDirectoryName);
            Directory.CreateDirectory(indexDirectory);
            var safeFileNamePart = MakeSafeFileNamePart(record.studentNumber);
            var indexPath = Path.Combine(indexDirectory, $"{safeFileNamePart}.json");

            var index = File.Exists(indexPath)
                ? JsonUtility.FromJson<AssessmentIndex>(File.ReadAllText(indexPath))
                : null;
            index ??= new AssessmentIndex();
            index.runs ??= new List<AssessmentIndexEntry>();
            index.studentNumber = record.studentNumber;
            index.displayName = record.displayName;
            index.runs.Add(new AssessmentIndexEntry
            {
                sessionId = record.sessionId,
                recordedAtUtc = record.recordedAtUtc,
                assessmentGrade = record.assessmentGrade,
                outcome = record.outcome,
                elapsedSeconds = record.elapsedSeconds,
                assignedTaskId = record.assignedTaskId,
                violationCount = record.violations?.Length ?? 0,
                fileName = fileName
            });

            File.WriteAllText(indexPath, JsonUtility.ToJson(index, true));
        }

        /// <summary>One student's local assessment history, read from their assessments/ index
        /// file directly (not by rescanning every run-*.json like ListAssessmentRuns does).
        /// Empty when the student has no recorded assessment attempts.</summary>
        public static IReadOnlyList<YardCraneAssessmentIndexEntrySummary> ListAssessmentRunsForStudent(
            string studentNumber)
        {
            var indexPath = Path.Combine(
                ResolveScoresDirectory(), AssessmentIndexDirectoryName,
                $"{MakeSafeFileNamePart(studentNumber)}.json");
            if (!File.Exists(indexPath)) return Array.Empty<YardCraneAssessmentIndexEntrySummary>();

            try
            {
                var index = JsonUtility.FromJson<AssessmentIndex>(File.ReadAllText(indexPath));
                if (index?.runs == null) return Array.Empty<YardCraneAssessmentIndexEntrySummary>();

                var results = new List<YardCraneAssessmentIndexEntrySummary>(index.runs.Count);
                foreach (var entry in index.runs)
                {
                    results.Add(new YardCraneAssessmentIndexEntrySummary
                    {
                        sessionId = entry.sessionId,
                        recordedAtUtc = entry.recordedAtUtc,
                        assessmentGrade = entry.assessmentGrade,
                        outcome = entry.outcome,
                        elapsedSeconds = entry.elapsedSeconds,
                        assignedTaskId = entry.assignedTaskId,
                        violationCount = entry.violationCount,
                        fileName = entry.fileName
                    });
                }

                return results;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[YC_ASSESSMENT_STUDENT_INDEX_READ_FAILED] " +
                    $"studentNumber={studentNumber}, error={exception.Message}");
                return Array.Empty<YardCraneAssessmentIndexEntrySummary>();
            }
        }

        // 2026-09-17: assessment (PickupWorkflow) runs are deliberately Unscored, so they never
        // pass countsTowardResults and never reach AppendToStudentIndex above - the per-student
        // index only ever holds officially-scored runs. This scans the flat run-*.json files
        // directly instead, filtering to trainingMode==Assessment, since that is the only place
        // these runs exist on disk. Acceptable for a single training station's file count; not
        // an index, so this re-reads every file on every call.
        public static List<YardCraneAssessmentRunSummary> ListAssessmentRuns()
        {
            var results = new List<YardCraneAssessmentRunSummary>();
            var directory = ResolveScoresDirectory();
            if (!Directory.Exists(directory))
            {
                return results;
            }

            foreach (var path in Directory.GetFiles(directory, "run-*.json"))
            {
                RunRecord record;
                try
                {
                    record = JsonUtility.FromJson<RunRecord>(File.ReadAllText(path));
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[YC_ASSESSMENT_RUN_READ_FAILED] path={path}, error={exception.Message}");
                    continue;
                }

                if (record == null || record.trainingMode != nameof(YardCraneLocalTrainingMode.Assessment))
                {
                    continue;
                }

                var violationCount = record.violations != null ? record.violations.Length : 0;
                var violationSummaries = new YardCraneAssessmentRunViolationSummary[violationCount];
                for (var index = 0; index < violationCount; index++)
                {
                    var violation = record.violations[index];
                    violationSummaries[index] = new YardCraneAssessmentRunViolationSummary
                    {
                        category = violation.category,
                        type = violation.type,
                        pointsDeducted = violation.pointsDeducted,
                        detail = violation.detail
                    };
                }

                results.Add(new YardCraneAssessmentRunSummary
                {
                    sessionId = record.sessionId,
                    studentNumber = record.studentNumber,
                    displayName = record.displayName,
                    recordedAtUtc = record.recordedAtUtc,
                    outcome = record.outcome,
                    assessmentGrade = record.assessmentGrade,
                    elapsedSeconds = record.elapsedSeconds,
                    assignedTaskId = record.assignedTaskId,
                    taskTitle = record.taskTitle,
                    violationCount = violationCount,
                    fileName = Path.GetFileName(path),
                    violations = violationSummaries
                });
            }

            return results;
        }

        private static string ResolveScoresDirectory()
        {
#if UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace(EditorOutputDirectoryOverride))
                return Path.GetFullPath(EditorOutputDirectoryOverride);
#endif
            // 2026-09-17: was Application.persistentDataPath - see YardCraneLocalDataPaths for
            // why that resolved to the wrong folder in practice.
            return Path.Combine(YardCraneLocalDataPaths.RootDirectory, DirectoryName);
        }

        private static string OutcomeName(YardCraneTrainingRunOutcome outcome)
        {
            return outcome switch
            {
                YardCraneTrainingRunOutcome.Completed => "completed",
                YardCraneTrainingRunOutcome.Terminated => "terminated",
                YardCraneTrainingRunOutcome.Failed => "failed",
                YardCraneTrainingRunOutcome.InProgress => "interrupted",
                _ => "unattributed"
            };
        }

        // 2026-09-18: the assessment pass/fail rule. "fail" whenever the run did not finish the
        // full lock-hoist-transport-land-unlock sequence, or a Safety-category violation
        // occurred (currently only UnsafeUnlock) - checked by category, not by hardcoding
        // specific violation types, so any future Safety-tagged violation disqualifies a run
        // automatically without this rule needing to change. Completed with no violations at
        // all is "excellent"; completed with only Skill-category dings (currently HardImpact) is
        // "pass". This is a real, structural verdict (not a fabricated 0-100 score) - Assessment
        // stays Unscored (see hasScore in SaveWithResult) on purpose.
        private static string AssessmentGradeName(
            YardCraneTrainingRunOutcome outcome, YardCraneTrainingScoreTracker tracker)
        {
            var hasSafetyViolation = false;
            foreach (var violation in tracker.Violations)
            {
                if (violation.Category != YardCraneViolationCategory.Safety) continue;
                hasSafetyViolation = true;
                break;
            }

            if (outcome != YardCraneTrainingRunOutcome.Completed || hasSafetyViolation) return "fail";
            return tracker.Violations.Count > 0 ? "pass" : "excellent";
        }

        // Student numbers are already constrained by YardCraneLocalStudentProfile to
        // letters/digits/-/_ , but this file name is derived from persisted JSON content (a
        // student number read back from a prior run), not re-validated input - defend the
        // file system path explicitly rather than trusting that upstream constraint forever.
        private static string MakeSafeFileNamePart(string value)
        {
            if (string.IsNullOrEmpty(value)) return "unknown";
            var builder = new System.Text.StringBuilder(value.Length);
            foreach (var character in value)
            {
                builder.Append(char.IsLetterOrDigit(character) || character == '-' || character == '_'
                    ? character : '_');
            }
            return builder.Length > 0 ? builder.ToString() : "unknown";
        }

        private static ViolationRecord[] ToRecords(IReadOnlyList<YardCraneTrainingViolation> violations)
        {
            var records = new ViolationRecord[violations.Count];
            for (var i = 0; i < violations.Count; i++)
            {
                var violation = violations[i];
                records[i] = new ViolationRecord
                {
                    category = violation.Category.ToString(),
                    type = violation.Type.ToString(),
                    pointsDeducted = violation.PointsDeducted,
                    occurredAtSeconds = violation.OccurredAtSeconds,
                    detail = violation.Detail
                };
            }
            return records;
        }
    }
}
