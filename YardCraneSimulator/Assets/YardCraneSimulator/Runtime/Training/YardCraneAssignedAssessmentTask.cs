using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Training
{
    [Serializable]
    public sealed class YardCraneAssignedAssessmentTask
    {
        public string taskId;
        public string formatVersion;
        public string assignedAtUtc;
        public string category;
        public string project;
        public string targetDescription;
        public int durationSeconds;
        public string operationMode;
        // 2026-09-17 v3: whether this task is the one currently active for students (see
        // Publish/Unpublish below). Defaults to false, which also means any pre-v3 JSON file
        // loads every existing entry as unpublished - there is no "current" task until the
        // teacher explicitly (re)publishes one after upgrading.
        public bool isPublished;
    }

    [Serializable]
    internal sealed class YardCraneAssignedAssessmentTaskList
    {
        public List<YardCraneAssignedAssessmentTask> tasks = new();
    }

    /// <summary>
    /// Local file bridge between the teacher's task list and the student's "任务考核" entry
    /// point. 2026-09-17 v3: every task the teacher creates is a persisted list entry with its
    /// own lifecycle - created as a draft (isPublished=false), then Publish/Unpublish toggles
    /// whether it is the one students currently receive. At most one task is ever published at
    /// a time (device-wide, not per-student - see the discussion record for why): Publish
    /// unpublishes whatever else was published first, so there is never more than one "current"
    /// task. This is what backs the teacher-side task list (replacing its original 5 static
    /// template rows) and lets a completed run's YardCraneAssignedAssessmentTask.taskId be
    /// looked back up for the assessment statistics page. Same per-station (not networked),
    /// project-relative-directory + JsonUtility convention as YardCraneTrainingScoreRecorder.
    /// This is not the NodeService platform task contract.
    ///
    /// Category/project only ever reach the student as display text, never as something that
    /// selects a different graded mechanic: as of 2026-09-17, PickupWorkflow (lock-hoist-
    /// transport-land-unlock) is the only implemented local assessment exercise
    /// (YardCraneLocalTrainingSessionContext.RequireAssessmentPolicy only accepts
    /// PickupWorkflow/DropoffWorkflow, neither of which takes a numeric target) - this store
    /// does not claim category selection changes what the student is actually judged on.
    /// </summary>
    public static class YardCraneAssignedAssessmentTaskStore
    {
        public const string FormatVersion = "2026-09-17-yc-assigned-assessment-task-v3-publish-state";
        private const string DirectoryName = "TeacherTasks";
        private const string FileName = "assessment-tasks.json";

#if UNITY_EDITOR
        // Play-mode verifiers must never write into the same file a real local session reads
        // from. This override does not exist in a Player build.
        public static string EditorOutputDirectoryOverride { get; set; }
#endif

        /// <summary>Appends a new task as an unpublished draft and returns it (with its assigned
        /// taskId). Call Publish afterwards if it should immediately become the current task.</summary>
        public static YardCraneAssignedAssessmentTask Save(
            string category, string project, string targetDescription, int durationSeconds,
            string operationMode = "")
        {
            var record = new YardCraneAssignedAssessmentTask
            {
                taskId = Guid.NewGuid().ToString("N"),
                formatVersion = FormatVersion,
                assignedAtUtc = DateTime.UtcNow.ToString("O"),
                category = category ?? string.Empty,
                project = project ?? string.Empty,
                targetDescription = targetDescription ?? string.Empty,
                durationSeconds = Mathf.Max(1, durationSeconds),
                operationMode = operationMode ?? string.Empty,
                isPublished = false
            };

            var list = LoadList();
            list.tasks.Add(record);
            WriteList(list);
            return record;
        }

        /// <summary>The currently published task, if any - what a student reads on entry. At
        /// most one task is ever published (see Publish), but this scans defensively from the
        /// newest entry in case that invariant was ever violated by hand-edited data.</summary>
        public static bool TryLoadCurrent(out YardCraneAssignedAssessmentTask task)
        {
            var tasks = LoadList().tasks;
            for (var index = tasks.Count - 1; index >= 0; index--)
            {
                if (!tasks[index].isPublished) continue;
                task = tasks[index];
                return true;
            }

            task = null;
            return false;
        }

        /// <summary>Makes the given task the single currently-published one, unpublishing any
        /// other task that was previously published. Returns false if taskId is not found.</summary>
        public static bool Publish(string taskId)
        {
            var list = LoadList();
            var found = false;
            foreach (var candidate in list.tasks)
            {
                var isMatch = candidate.taskId == taskId;
                candidate.isPublished = isMatch;
                found |= isMatch;
            }

            if (!found) return false;
            WriteList(list);
            return true;
        }

        /// <summary>Revokes a task's published status; students stop reading it as the current
        /// task. Returns false if taskId is not found.</summary>
        public static bool Unpublish(string taskId)
        {
            var list = LoadList();
            foreach (var candidate in list.tasks)
            {
                if (candidate.taskId != taskId) continue;
                candidate.isPublished = false;
                WriteList(list);
                return true;
            }

            return false;
        }

        /// <summary>Updates an existing task's editable fields in place. taskId, formatVersion,
        /// assignedAtUtc and isPublished are left untouched - publish state only ever changes via
        /// Publish/Unpublish, never as a side effect of editing. Returns false if taskId is not
        /// found.</summary>
        public static bool Update(
            string taskId, string category, string project, string targetDescription,
            int durationSeconds, string operationMode)
        {
            var list = LoadList();
            foreach (var candidate in list.tasks)
            {
                if (candidate.taskId != taskId) continue;
                candidate.category = category ?? string.Empty;
                candidate.project = project ?? string.Empty;
                candidate.targetDescription = targetDescription ?? string.Empty;
                candidate.durationSeconds = Mathf.Max(1, durationSeconds);
                candidate.operationMode = operationMode ?? string.Empty;
                WriteList(list);
                return true;
            }

            return false;
        }

        /// <summary>Permanently removes a task from the list. Returns false if taskId is not
        /// found.</summary>
        public static bool Delete(string taskId)
        {
            var list = LoadList();
            var removed = list.tasks.RemoveAll(candidate => candidate.taskId == taskId) > 0;
            if (removed) WriteList(list);
            return removed;
        }

        /// <summary>Every task ever published on this station, oldest first. For the teacher's
        /// task list and for cross-referencing a completed run's assignedTaskId back to its
        /// category/project/target.</summary>
        public static IReadOnlyList<YardCraneAssignedAssessmentTask> LoadAll() => LoadList().tasks;

        public static bool TryFind(string taskId, out YardCraneAssignedAssessmentTask task)
        {
            if (!string.IsNullOrEmpty(taskId))
            {
                foreach (var candidate in LoadList().tasks)
                {
                    if (candidate.taskId == taskId)
                    {
                        task = candidate;
                        return true;
                    }
                }
            }

            task = null;
            return false;
        }

        private static YardCraneAssignedAssessmentTaskList LoadList()
        {
            var path = Path.Combine(ResolveDirectory(), FileName);
            if (!File.Exists(path))
            {
                return new YardCraneAssignedAssessmentTaskList();
            }

            try
            {
                var list = JsonUtility.FromJson<YardCraneAssignedAssessmentTaskList>(File.ReadAllText(path));
                list ??= new YardCraneAssignedAssessmentTaskList();
                list.tasks ??= new List<YardCraneAssignedAssessmentTask>();
                return list;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[YC_ASSIGNED_ASSESSMENT_TASK_READ_FAILED] path={path}, error={exception.Message}");
                return new YardCraneAssignedAssessmentTaskList();
            }
        }

        private static void WriteList(YardCraneAssignedAssessmentTaskList list)
        {
            var directory = ResolveDirectory();
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, FileName), JsonUtility.ToJson(list, true));
        }

        private static string ResolveDirectory()
        {
#if UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace(EditorOutputDirectoryOverride))
            {
                return Path.GetFullPath(EditorOutputDirectoryOverride);
            }
#endif
            // 2026-09-17: was Application.persistentDataPath - see YardCraneLocalDataPaths for
            // why that resolved to the wrong folder in practice.
            return Path.Combine(YardCraneLocalDataPaths.RootDirectory, DirectoryName);
        }
    }
}
