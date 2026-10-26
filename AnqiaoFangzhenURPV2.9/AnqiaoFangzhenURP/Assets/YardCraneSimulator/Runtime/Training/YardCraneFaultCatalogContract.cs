using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Training
{
    public readonly struct YardCraneFaultCatalogRecord
    {
        public readonly int Id;
        public readonly string Phenomenon;
        public readonly string FaultPoint;
        public readonly string LineTag;
        public readonly string Category;

        public YardCraneFaultCatalogRecord(
            int id,
            string phenomenon,
            string faultPoint,
            string lineTag,
            string category)
        {
            Id = id;
            Phenomenon = phenomenon;
            FaultPoint = faultPoint;
            LineTag = lineTag;
            Category = category;
        }
    }

    public interface IYardCraneFaultCatalogSource
    {
        IEnumerable<YardCraneFaultCatalogRecord> EnumerateFaults();
    }

    [Serializable]
    public sealed class YardCranePublishedFaultItem
    {
        public int faultId;
        public string category;
        public string phenomenon;
        public string faultPoint;
        public string lineTag;
    }

    [Serializable]
    public sealed class YardCranePublishedFaultAssignment
    {
        public string formatVersion;
        public string assignmentId;
        public string publishedAtUtc;
        public string craneType;
        public List<YardCranePublishedFaultItem> faults = new();
        public bool isPublished;
    }

    /// <summary>
    /// Local teacher-to-student bridge for the fault questions selected on TeacherHome.
    /// Each crane kind owns an independent current assignment, so the shore and yard Build
    /// Profiles cannot overwrite or consume one another's fault selection.
    /// </summary>
    public static class YardCranePublishedFaultAssignmentStore
    {
        public const string FormatVersion = "2026-09-20-yc-published-fault-assignment-v2-publish-state";
        private const string LegacyPublishedFormatVersion =
            "2026-09-20-yc-published-fault-assignment-v1";
        private const string DirectoryName = "TeacherTasks";

#if UNITY_EDITOR
        public static string EditorOutputDirectoryOverride { get; set; }
#endif

        public static bool TryPublish(
            string craneType,
            IEnumerable<YardCraneFaultCatalogRecord> records,
            out YardCranePublishedFaultAssignment assignment,
            out string error)
        {
            assignment = null;
            error = string.Empty;
            if (!IsSupportedCraneType(craneType))
            {
                error = $"不支持的设备类型：{craneType ?? "(空)"}。";
                return false;
            }
            if (records == null)
            {
                error = "未选择故障题目。";
                return false;
            }

            var faults = new List<YardCranePublishedFaultItem>();
            var usedIds = new HashSet<int>();
            foreach (var record in records)
            {
                if (record.Id <= 0 || !usedIds.Add(record.Id)) continue;
                faults.Add(new YardCranePublishedFaultItem
                {
                    faultId = record.Id,
                    category = record.Category ?? string.Empty,
                    phenomenon = record.Phenomenon ?? string.Empty,
                    faultPoint = record.FaultPoint ?? string.Empty,
                    lineTag = record.LineTag ?? string.Empty
                });
            }
            if (faults.Count == 0)
            {
                error = "未选择故障题目。";
                return false;
            }

            assignment = new YardCranePublishedFaultAssignment
            {
                formatVersion = FormatVersion,
                assignmentId = Guid.NewGuid().ToString("N"),
                publishedAtUtc = DateTime.UtcNow.ToString("O"),
                craneType = craneType,
                faults = faults,
                isPublished = true
            };

            try
            {
                var directory = ResolveDirectory();
                Directory.CreateDirectory(directory);
                File.WriteAllText(ResolvePath(craneType), JsonUtility.ToJson(assignment, true));
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                assignment = null;
                Debug.LogWarning($"[YC_PUBLISHED_FAULT_WRITE_FAILED] {exception}");
                return false;
            }
        }

        public static bool TryLoadCurrent(
            string craneType,
            out YardCranePublishedFaultAssignment assignment)
        {
            if (!TryLoadLatest(craneType, out assignment) || !assignment.isPublished)
            {
                assignment = null;
                return false;
            }
            return true;
        }

        public static bool TryLoadLatest(
            string craneType,
            out YardCranePublishedFaultAssignment assignment)
        {
            assignment = null;
            if (!IsSupportedCraneType(craneType)) return false;
            var path = ResolvePath(craneType);
            if (!File.Exists(path)) return false;

            try
            {
                var parsed = JsonUtility.FromJson<YardCranePublishedFaultAssignment>(
                    File.ReadAllText(path));
                if (parsed == null ||
                    !string.Equals(parsed.craneType, craneType, StringComparison.Ordinal) ||
                    parsed.faults == null || parsed.faults.Count == 0)
                    return false;
                if (string.Equals(
                        parsed.formatVersion,
                        LegacyPublishedFormatVersion,
                        StringComparison.Ordinal))
                    parsed.isPublished = true;
                assignment = parsed;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[YC_PUBLISHED_FAULT_READ_FAILED] path={path}, error={exception.Message}");
                return false;
            }
        }

        public static bool TryUnpublish(string craneType, out string error)
        {
            error = string.Empty;
            if (!IsSupportedCraneType(craneType))
            {
                error = $"不支持的设备类型：{craneType ?? "(空)"}。";
                return false;
            }

            try
            {
                if (!TryLoadLatest(craneType, out var assignment)) return true;
                assignment.formatVersion = FormatVersion;
                assignment.isPublished = false;
                WriteAssignment(assignment);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                Debug.LogWarning($"[YC_PUBLISHED_FAULT_UNPUBLISH_FAILED] {exception}");
                return false;
            }
        }

        public static bool TryRepublish(string craneType, out string error)
        {
            error = string.Empty;
            if (!IsSupportedCraneType(craneType))
            {
                error = $"不支持的设备类型：{craneType ?? "(空)"}。";
                return false;
            }

            try
            {
                if (!TryLoadLatest(craneType, out var assignment))
                {
                    error = "没有可重新发布的故障试卷。";
                    return false;
                }
                assignment.formatVersion = FormatVersion;
                assignment.publishedAtUtc = DateTime.UtcNow.ToString("O");
                assignment.isPublished = true;
                WriteAssignment(assignment);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                Debug.LogWarning($"[YC_PUBLISHED_FAULT_REPUBLISH_FAILED] {exception}");
                return false;
            }
        }

        public static bool TryDelete(string craneType, out string error)
        {
            error = string.Empty;
            if (!IsSupportedCraneType(craneType))
            {
                error = $"不支持的设备类型：{craneType ?? "(空)"}。";
                return false;
            }

            try
            {
                var path = ResolvePath(craneType);
                if (File.Exists(path)) File.Delete(path);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                Debug.LogWarning($"[YC_PUBLISHED_FAULT_DELETE_FAILED] {exception}");
                return false;
            }
        }

        private static bool IsSupportedCraneType(string craneType) =>
            string.Equals(craneType, "岸桥", StringComparison.Ordinal) ||
            string.Equals(craneType, "场桥", StringComparison.Ordinal);

        private static string ResolvePath(string craneType)
        {
            var fileName = string.Equals(craneType, "岸桥", StringComparison.Ordinal)
                ? "quay-fault-assignment.json"
                : "yard-fault-assignment.json";
            return Path.Combine(ResolveDirectory(), fileName);
        }

        private static void WriteAssignment(YardCranePublishedFaultAssignment assignment)
        {
            var directory = ResolveDirectory();
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                ResolvePath(assignment.craneType),
                JsonUtility.ToJson(assignment, true));
        }

        private static string ResolveDirectory()
        {
#if UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace(EditorOutputDirectoryOverride))
                return Path.GetFullPath(EditorOutputDirectoryOverride);
#endif
            return Path.Combine(YardCraneLocalDataPaths.RootDirectory, DirectoryName);
        }
    }
}
