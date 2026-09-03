using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace PortTraining.Core.Progress
{
    public sealed class LocalStudentProgressService
    {
        public StudentProgressSnapshot Load(string studentId)
        {
            var normalizedStudentId = NormalizeStudentId(studentId);
            var path = GetProgressPath(normalizedStudentId);
            if (!File.Exists(path))
            {
                return new StudentProgressSnapshot { studentId = normalizedStudentId };
            }

            try
            {
                var progress = JsonUtility.FromJson<StudentProgressSnapshot>(File.ReadAllText(path));
                return progress ?? new StudentProgressSnapshot { studentId = normalizedStudentId };
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PortTraining] Student progress cache ignored: {exception.Message}");
                return new StudentProgressSnapshot { studentId = normalizedStudentId };
            }
        }

        public void Save(StudentProgressSnapshot progress)
        {
            if (progress == null)
            {
                throw new ArgumentNullException(nameof(progress));
            }

            progress.studentId = NormalizeStudentId(progress.studentId);
            progress.updatedAt = DateTime.UtcNow.ToString("O");
            var path = GetProgressPath(progress.studentId);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(progress, true));
        }

        public void RecordTrainingResult(
            string studentId,
            bool taskCompleted,
            int durationSeconds,
            float? score,
            int evaluatedOperations,
            int safeOperations)
        {
            var progress = Load(studentId);
            progress.trainingSessionCount++;
            progress.practiceDurationSeconds += Math.Max(0, durationSeconds);
            progress.assignedTaskCount++;
            if (taskCompleted)
            {
                progress.completedTaskCount++;
            }
            if (score.HasValue)
            {
                progress.scoredSessionCount++;
                progress.totalScore += Math.Clamp(score.Value, 0f, 100f);
            }
            progress.evaluatedOperationCount += Math.Max(0, evaluatedOperations);
            progress.safeOperationCount += Math.Clamp(safeOperations, 0, Math.Max(0, evaluatedOperations));
            Save(progress);
        }

        private static string GetProgressPath(string studentId)
        {
            return Path.Combine(Application.persistentDataPath, "Progress", $"student-{studentId}.json");
        }

        private static string NormalizeStudentId(string studentId)
        {
            var value = (studentId ?? string.Empty).Trim();
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
