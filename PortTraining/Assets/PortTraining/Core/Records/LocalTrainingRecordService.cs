using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PortTraining.Core.Records
{
    public sealed class LocalTrainingRecordService
    {
        public IReadOnlyList<TrainingRecordSnapshot> Load(string studentId)
        {
            var path = GetPath(studentId);
            if (!File.Exists(path))
            {
                return Array.Empty<TrainingRecordSnapshot>();
            }

            try
            {
                var collection = JsonUtility.FromJson<TrainingRecordCollection>(File.ReadAllText(path));
                return (collection?.records ?? Array.Empty<TrainingRecordSnapshot>())
                    .OrderByDescending(record => record.completedAt)
                    .ToArray();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PortTraining] Training archive ignored: {exception.Message}");
                return Array.Empty<TrainingRecordSnapshot>();
            }
        }

        public void Append(string studentId, TrainingRecordSnapshot record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            var records = new List<TrainingRecordSnapshot>(Load(studentId));
            record.recordId = string.IsNullOrWhiteSpace(record.recordId) ? Guid.NewGuid().ToString("N") : record.recordId;
            record.studentId = Normalize(studentId);
            record.completedAt = string.IsNullOrWhiteSpace(record.completedAt) ? DateTime.UtcNow.ToString("O") : record.completedAt;
            records.RemoveAll(item => string.Equals(item.recordId, record.recordId, StringComparison.OrdinalIgnoreCase));
            records.Add(record);

            var path = GetPath(studentId);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(new TrainingRecordCollection { records = records.ToArray() }, true));
        }

        private static string GetPath(string studentId)
        {
            return Path.Combine(Application.persistentDataPath, "Records", $"student-{Normalize(studentId)}.json");
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
