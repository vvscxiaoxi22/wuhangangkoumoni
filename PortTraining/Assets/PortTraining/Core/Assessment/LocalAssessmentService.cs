using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PortTraining.Core.Assessment
{
    public sealed class LocalAssessmentService
    {
        public IReadOnlyList<AssessmentAttemptSnapshot> LoadAttempts(string studentId)
        {
            var path = GetPath(studentId);
            if (!File.Exists(path))
            {
                return Array.Empty<AssessmentAttemptSnapshot>();
            }

            try
            {
                var collection = JsonUtility.FromJson<AssessmentAttemptCollection>(File.ReadAllText(path));
                return collection?.attempts ?? Array.Empty<AssessmentAttemptSnapshot>();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PortTraining] Assessment cache ignored: {exception.Message}");
                return Array.Empty<AssessmentAttemptSnapshot>();
            }
        }

        public AssessmentAttemptSnapshot FindLatest(string studentId, string workOrderId)
        {
            return LoadAttempts(studentId)
                .Where(attempt => string.Equals(attempt.workOrderId, workOrderId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(attempt => attempt.attemptNumber)
                .FirstOrDefault();
        }

        public AssessmentAttemptSnapshot BeginAttempt(string studentId, string workOrderId, int maxAttempts)
        {
            var attempts = new List<AssessmentAttemptSnapshot>(LoadAttempts(studentId));
            var previous = attempts
                .Where(attempt => string.Equals(attempt.workOrderId, workOrderId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(attempt => attempt.attemptNumber)
                .FirstOrDefault();

            if (previous?.State == AssessmentAttemptState.InProgress)
            {
                return previous;
            }

            var nextAttemptNumber = (previous?.attemptNumber ?? 0) + 1;
            if (nextAttemptNumber > Math.Max(1, maxAttempts))
            {
                return null;
            }

            var attempt = new AssessmentAttemptSnapshot
            {
                studentId = Normalize(studentId),
                workOrderId = workOrderId ?? string.Empty,
                attemptId = Guid.NewGuid().ToString("N"),
                state = nameof(AssessmentAttemptState.InProgress),
                attemptNumber = nextAttemptNumber,
                startedAt = DateTime.UtcNow.ToString("O")
            };
            attempts.Add(attempt);
            Save(studentId, attempts);
            return attempt;
        }

        public AssessmentAttemptSnapshot CompleteAttempt(
            string studentId,
            string workOrderId,
            int durationSeconds,
            float score,
            float passingScore)
        {
            var attempts = new List<AssessmentAttemptSnapshot>(LoadAttempts(studentId));
            var attempt = attempts
                .Where(item => string.Equals(item.workOrderId, workOrderId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.attemptNumber)
                .FirstOrDefault(item => item.State == AssessmentAttemptState.InProgress);
            if (attempt == null)
            {
                return null;
            }

            attempt.state = nameof(AssessmentAttemptState.Completed);
            attempt.completedAt = DateTime.UtcNow.ToString("O");
            attempt.durationSeconds = Math.Max(0, durationSeconds);
            attempt.score = Math.Clamp(score, 0f, 100f);
            attempt.passed = attempt.score >= passingScore;
            Save(studentId, attempts);
            return attempt;
        }

        public AssessmentAttemptSnapshot TerminateAttempt(string studentId, string workOrderId, string reason)
        {
            var attempts = new List<AssessmentAttemptSnapshot>(LoadAttempts(studentId));
            var attempt = attempts
                .Where(item => string.Equals(item.workOrderId, workOrderId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.attemptNumber)
                .FirstOrDefault(item => item.State == AssessmentAttemptState.InProgress);
            if (attempt == null)
            {
                return null;
            }

            attempt.state = nameof(AssessmentAttemptState.Terminated);
            attempt.completedAt = DateTime.UtcNow.ToString("O");
            attempt.terminationReason = reason ?? string.Empty;
            Save(studentId, attempts);
            return attempt;
        }

        private static void Save(string studentId, IReadOnlyList<AssessmentAttemptSnapshot> attempts)
        {
            var path = GetPath(studentId);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var collection = new AssessmentAttemptCollection { attempts = attempts.ToArray() };
            File.WriteAllText(path, JsonUtility.ToJson(collection, true));
        }

        private static string GetPath(string studentId)
        {
            return Path.Combine(Application.persistentDataPath, "Assessment", $"student-{Normalize(studentId)}.json");
        }

        private static string Normalize(string value)
        {
            value = (value ?? string.Empty).Trim();
            if (value.Length == 0)
            {
                return "anonymous";
            }

            var builder = new StringBuilder(value.Length);
            foreach (var character in value)
            {
                builder.Append(char.IsLetterOrDigit(character) || character == '-' || character == '_'
                    ? character
                    : '_');
            }
            return builder.ToString();
        }
    }
}
