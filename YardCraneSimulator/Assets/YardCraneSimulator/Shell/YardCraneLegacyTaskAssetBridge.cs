using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Training
{
    /// <summary>
    /// Read-only runtime view of the legacy Assets/Data task catalogue. The source data types
    /// live in Assembly-CSharp, while this project uses asmdefs, so callers receive plain DTOs
    /// and never need a compile-time dependency on 任务信息 or 任务信息结构.
    /// </summary>
    public sealed class YardCraneTaskCatalog
    {
        public YardCraneTaskCatalog(string craneType, string projectName, YardCraneTaskDefinition[] tasks)
        {
            CraneType = craneType;
            ProjectName = projectName;
            Tasks = tasks ?? Array.Empty<YardCraneTaskDefinition>();
        }

        public string CraneType { get; }
        public string ProjectName { get; }
        public YardCraneTaskDefinition[] Tasks { get; }

        public YardCraneTaskDefinition Find(string taskName)
        {
            foreach (var task in Tasks)
                if (string.Equals(task.Name, taskName, StringComparison.Ordinal)) return task;
            return null;
        }
    }

    public sealed class YardCraneTaskDefinition
    {
        public YardCraneTaskDefinition(
            string name,
            string taskType,
            string description,
            YardCraneTaskTargetField[] targetFields)
        {
            Name = name ?? string.Empty;
            TaskType = taskType ?? string.Empty;
            Description = description ?? string.Empty;
            TargetFields = targetFields ?? Array.Empty<YardCraneTaskTargetField>();
        }

        public string Name { get; }
        public string TaskType { get; }
        public string Description { get; }
        public YardCraneTaskTargetField[] TargetFields { get; }
        public int OptionCount => TargetFields.Length == 0 ? 1 : TargetFields[0].Values.Length;
    }

    public sealed class YardCraneTaskTargetField
    {
        public YardCraneTaskTargetField(string title, string[] values)
        {
            Title = title ?? string.Empty;
            Values = values ?? Array.Empty<string>();
        }

        public string Title { get; }
        public string[] Values { get; }
    }

    public readonly struct YardCraneTaskSelection
    {
        public YardCraneTaskSelection(
            YardCraneTaskCatalog catalog,
            YardCraneTaskDefinition task,
            int targetId,
            float durationSeconds,
            int trainingMode)
        {
            Catalog = catalog;
            Task = task;
            TargetId = targetId;
            DurationSeconds = durationSeconds;
            TrainingMode = trainingMode;
        }

        public YardCraneTaskCatalog Catalog { get; }
        public YardCraneTaskDefinition Task { get; }
        public int TargetId { get; }
        public float DurationSeconds { get; }
        public int TrainingMode { get; }
        public bool IsValid => Catalog != null && Task != null && TargetId >= 0 &&
                               TargetId < Task.OptionCount && (TrainingMode == 1 || TrainingMode == 2);
    }

    public static class YardCraneLegacyTaskAssetBridge
    {
        public const string SingleCategory = "单项操作";
        public const string LinkedCategory = "联动操作";
        public const string WorkCategory = "作业操作";
        public const string SkillCategory = "能力提升";
        public const string FreeCategory = "自由练习";
        public const int EmptyPaperType = 0;
        public const int TrainingAssessmentPaperType = 1;
        public const int FaultAssessmentPaperType = 2;

        private static readonly string[] AssignmentStringFieldNames =
        {
            "实训项目", "任务名称", "任务类型", "任务描述",
            "字段1标题", "字段2标题", "字段3标题",
            "字段1内容", "字段2内容", "字段3内容"
        };

        private static readonly HashSet<string> SupportedCategories = new HashSet<string>(StringComparer.Ordinal)
        {
            SingleCategory, LinkedCategory, WorkCategory, SkillCategory, FreeCategory
        };

        public static bool TryReadCatalog(
            ScriptableObject asset,
            out YardCraneTaskCatalog catalog,
            out string problem)
        {
            catalog = null;
            problem = null;
            if (asset == null)
            {
                problem = "task catalogue asset is not assigned.";
                return false;
            }

            var informationField = asset.GetType().GetField("_信息", BindingFlags.Instance | BindingFlags.Public);
            if (informationField == null || !(informationField.GetValue(asset) is IDictionary information))
            {
                problem = $"{asset.name}.asset does not expose a readable _信息 dictionary.";
                return false;
            }

            var tasks = new List<YardCraneTaskDefinition>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (DictionaryEntry pair in information)
            {
                if (!(pair.Key is string taskName) || string.IsNullOrWhiteSpace(taskName) ||
                    pair.Value == null || !names.Add(taskName))
                {
                    problem = $"{asset.name}.asset contains an invalid or duplicate task name.";
                    return false;
                }

                var valueType = pair.Value.GetType();
                var taskTypeField = valueType.GetField("任务类型", BindingFlags.Instance | BindingFlags.Public);
                var descriptionField = valueType.GetField("任务描述", BindingFlags.Instance | BindingFlags.Public);
                var targetsField = valueType.GetField("_目标", BindingFlags.Instance | BindingFlags.Public);
                var taskType = taskTypeField?.GetValue(pair.Value) as string;
                if (taskTypeField?.FieldType != typeof(string) || string.IsNullOrWhiteSpace(taskType))
                {
                    problem = $"{asset.name}.asset task '{taskName}' does not expose a valid 任务类型.";
                    return false;
                }

                taskType = taskType.Trim();
                if (!SupportedCategories.Contains(taskType)) continue;

                if (descriptionField?.FieldType != typeof(string) || targetsField == null ||
                    !(targetsField.GetValue(pair.Value) is IDictionary targets))
                {
                    problem = $"{asset.name}.asset task '{taskName}' does not match 任务信息结构.";
                    return false;
                }

                var targetFields = new List<YardCraneTaskTargetField>();
                var optionCount = -1;
                foreach (DictionaryEntry targetPair in targets)
                {
                    if (!(targetPair.Key is string title) || string.IsNullOrWhiteSpace(title) ||
                        targetPair.Value == null || targetPair.Value is string ||
                        !(targetPair.Value is IEnumerable valuesSource))
                    {
                        problem = $"{asset.name}.asset task '{taskName}' contains an invalid target field.";
                        return false;
                    }

                    var values = new List<string>();
                    foreach (var value in valuesSource)
                    {
                        values.Add(value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture));
                    }
                    if (values.Count == 0)
                    {
                        problem = $"{asset.name}.asset task '{taskName}' target '{title}' has no values.";
                        return false;
                    }
                    if (optionCount >= 0 && optionCount != values.Count)
                    {
                        problem = $"{asset.name}.asset task '{taskName}' target columns have different lengths.";
                        return false;
                    }

                    optionCount = values.Count;
                    targetFields.Add(new YardCraneTaskTargetField(title, values.ToArray()));
                }
                if (targetFields.Count > 3)
                {
                    problem = $"{asset.name}.asset task '{taskName}' has more than three target fields.";
                    return false;
                }

                tasks.Add(new YardCraneTaskDefinition(
                    taskName,
                    taskType,
                    descriptionField.GetValue(pair.Value) as string,
                    targetFields.ToArray()));
            }
            if (tasks.Count == 0)
            {
                problem = $"{asset.name}.asset contains no tasks.";
                return false;
            }

            string craneType;
            string projectName;
            if (asset.name.Contains("岸桥"))
            {
                craneType = "岸桥";
                projectName = "岸桥集装箱起重机仿真系统";
            }
            else if (asset.name.Contains("场桥"))
            {
                craneType = "场桥";
                projectName = "场桥集装箱起重机仿真系统";
            }
            else
            {
                problem = $"Cannot determine crane type from task catalogue '{asset.name}'.";
                return false;
            }

            catalog = new YardCraneTaskCatalog(craneType, projectName, tasks.ToArray());
            return true;
        }

        public static string CategoryForTask(YardCraneTaskDefinition task) =>
            task?.TaskType ?? string.Empty;

        public static YardCraneTaskDefinition[] TasksForCategory(
            YardCraneTaskCatalog catalog,
            string category)
        {
            if (catalog == null) return Array.Empty<YardCraneTaskDefinition>();
            var matches = new List<YardCraneTaskDefinition>();
            foreach (var task in catalog.Tasks)
                if (CategoryForTask(task) == category) matches.Add(task);
            return matches.ToArray();
        }

        public static string OptionLabel(YardCraneTaskDefinition task, int targetId)
        {
            if (task == null || targetId < 0 || targetId >= task.OptionCount) return string.Empty;
            if (task.TargetFields.Length == 0) return "无固定目标";

            var parts = new string[task.TargetFields.Length];
            for (var index = 0; index < task.TargetFields.Length; index++)
            {
                var field = task.TargetFields[index];
                parts[index] = $"{field.Title.TrimEnd(':', '：')} {field.Values[targetId]}";
            }
            return string.Join(" / ", parts);
        }

        public static bool TryCreateAssessmentSelection(
            YardCraneTaskCatalog catalog,
            YardCraneAssignedAssessmentTask assignedTask,
            out YardCraneTaskSelection selection,
            out string problem)
        {
            selection = default;
            problem = null;
            if (catalog == null || assignedTask == null)
            {
                problem = "Published training paper or task catalogue is missing.";
                return false;
            }

            var task = catalog.Find(assignedTask.project);
            if (task == null)
            {
                problem = $"Published task '{assignedTask.project}' is not in the current {catalog.CraneType} catalogue.";
                return false;
            }

            var targetId = 0;
            for (var index = 0; index < task.OptionCount; index++)
            {
                if (!string.Equals(
                        OptionLabel(task, index),
                        assignedTask.targetDescription,
                        StringComparison.Ordinal)) continue;
                targetId = index;
                break;
            }

            var trainingMode = string.Equals(
                assignedTask.operationMode, "远程操作", StringComparison.Ordinal) ? 2 : 1;
            selection = new YardCraneTaskSelection(
                catalog,
                task,
                targetId,
                Mathf.Max(0, assignedTask.durationSeconds),
                trainingMode);
            if (selection.IsValid) return true;
            problem = "Published training paper contains an invalid target or mode.";
            return false;
        }

        public static bool TryParseNumber(string text, out double value)
        {
            return double.TryParse(
                (text ?? string.Empty).Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }

        public static bool TryWriteStudentIdentity(
            ScriptableObject stateAsset,
            string studentNumber,
            string displayName,
            out string problem)
        {
            problem = null;
            if (stateAsset == null)
            {
                problem = "请在登录组件的『任务状态』中绑定任务State.asset。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(studentNumber) || string.IsNullOrWhiteSpace(displayName))
            {
                problem = "学生学号和姓名不能为空。";
                return false;
            }

            var type = stateAsset.GetType();
            var numberField = type.GetField("学号", BindingFlags.Instance | BindingFlags.Public);
            var nameField = type.GetField("姓名", BindingFlags.Instance | BindingFlags.Public);
            if (numberField?.FieldType != typeof(string) || numberField.IsInitOnly ||
                nameField?.FieldType != typeof(string) || nameField.IsInitOnly)
            {
                problem = $"{stateAsset.name}.asset 必须提供可写的 string 学号和姓名字段。";
                return false;
            }

            numberField.SetValue(stateAsset, studentNumber.Trim());
            nameField.SetValue(stateAsset, displayName.Trim());
            PersistAssetInEditor(stateAsset);
            return true;
        }

        public static bool TryWriteTrainingMode(
            ScriptableObject stateAsset,
            int trainingMode,
            out string problem)
        {
            problem = null;
            if (stateAsset == null)
            {
                problem = "请在学生首页组件中绑定任务State.asset。";
                return false;
            }
            if (trainingMode != 1 && trainingMode != 2)
            {
                problem = "实训模式只能是1（本地）或2（远程）。";
                return false;
            }
            var field = stateAsset.GetType().GetField("实训模式", BindingFlags.Instance | BindingFlags.Public);
            if (field?.FieldType != typeof(int) || field.IsInitOnly || field.IsLiteral)
            {
                problem = $"{stateAsset.name}.asset 必须提供可写的 int 实训模式字段。";
                return false;
            }
            field.SetValue(stateAsset, trainingMode);
            PersistAssetInEditor(stateAsset);
            return true;
        }

        public static bool TryWriteTaskState(
            ScriptableObject stateAsset,
            YardCraneTaskSelection selection,
            out string problem)
        {
            if (!selection.IsValid)
            {
                problem = "Selected task or target is invalid.";
                return false;
            }

            if (!TryGetAssignmentFields(
                    stateAsset,
                    requirePaperType: false,
                    requireResultFields: true,
                    out var fields,
                    out problem)) return false;

            WriteSelection(stateAsset, fields, selection);
            fields.Score.SetValue(stateAsset, 0);
            fields.Elapsed.SetValue(stateAsset, 0f);
            PersistAssetInEditor(stateAsset);
            return true;
        }

        public static bool TryWriteFaultTaskState(
            ScriptableObject stateAsset,
            YardCranePublishedFaultAssignment assignment,
            float durationSeconds,
            out string problem)
        {
            if (!TryCollectFaultIds(assignment, out var faultIds, out problem)) return false;
            if (!TryGetAssignmentFields(
                    stateAsset,
                    requirePaperType: false,
                    requireResultFields: true,
                    out var fields,
                    out problem)) return false;

            WriteFaultAssignment(stateAsset, fields, assignment, faultIds, durationSeconds);
            fields.Score.SetValue(stateAsset, 0);
            fields.Elapsed.SetValue(stateAsset, 0f);
            PersistAssetInEditor(stateAsset);
            return true;
        }

        public static bool TryWriteFaultTrainingTaskState(
            ScriptableObject stateAsset,
            YardCranePublishedFaultAssignment assignment,
            out string problem)
        {
            if (!TryCollectFaultIds(
                    assignment,
                    requirePublished: false,
                    out var faultIds,
                    out problem)) return false;
            if (!TryGetAssignmentFields(
                    stateAsset,
                    requirePaperType: false,
                    requireResultFields: true,
                    out var fields,
                    out problem)) return false;

            WriteFaultAssignment(stateAsset, fields, assignment, faultIds, 0f);
            var category = assignment.faults.Count > 0
                ? assignment.faults[0]?.category?.Trim()
                : string.Empty;
            fields.Strings["任务类型"].SetValue(stateAsset, "故障训练");
            fields.Strings["任务描述"].SetValue(
                stateAsset,
                $"学习训练选择的{assignment.craneType}{category}，共 {faultIds.Count} 条故障。");
            fields.Score.SetValue(stateAsset, 0);
            fields.Elapsed.SetValue(stateAsset, 0f);
            PersistAssetInEditor(stateAsset);
            return true;
        }

        public static bool TryWriteAssessmentTrainingTaskState(
            ScriptableObject stateAsset,
            YardCraneAssessmentTrainingQuestionData training,
            out string problem)
        {
            if (training == null || string.IsNullOrWhiteSpace(training.taskName) ||
                training.targetId < 0 ||
                (training.trainingMode != 1 && training.trainingMode != 2) ||
                float.IsNaN(training.durationSeconds) ||
                float.IsInfinity(training.durationSeconds) ||
                training.durationSeconds < 0f)
            {
                problem = "Published training question is invalid.";
                return false;
            }
            if (!TryGetAssignmentFields(
                    stateAsset,
                    requirePaperType: false,
                    requireResultFields: true,
                    out var fields,
                    out problem)) return false;

            fields.Strings["实训项目"].SetValue(stateAsset, training.trainingProject ?? string.Empty);
            fields.Strings["任务名称"].SetValue(stateAsset, training.taskName.Trim());
            fields.Strings["任务类型"].SetValue(
                stateAsset,
                string.IsNullOrWhiteSpace(training.taskType)
                    ? training.category ?? string.Empty
                    : training.taskType.Trim());
            fields.Strings["任务描述"].SetValue(stateAsset, training.taskDescription ?? string.Empty);
            fields.Strings["字段1标题"].SetValue(stateAsset, training.field1Title ?? string.Empty);
            fields.Strings["字段2标题"].SetValue(stateAsset, training.field2Title ?? string.Empty);
            fields.Strings["字段3标题"].SetValue(stateAsset, training.field3Title ?? string.Empty);
            fields.Strings["字段1内容"].SetValue(stateAsset, training.field1Content ?? string.Empty);
            fields.Strings["字段2内容"].SetValue(stateAsset, training.field2Content ?? string.Empty);
            fields.Strings["字段3内容"].SetValue(stateAsset, training.field3Content ?? string.Empty);
            fields.TargetId.SetValue(stateAsset, training.targetId);
            fields.Duration.SetValue(
                stateAsset,
                training.durationSeconds > 0f ? training.durationSeconds : 600f);
            fields.TrainingMode.SetValue(stateAsset, training.trainingMode);
            fields.FaultIds.SetValue(stateAsset, new List<int>());
            fields.Score.SetValue(stateAsset, 0);
            fields.Elapsed.SetValue(stateAsset, 0f);
            PersistAssetInEditor(stateAsset);
            return true;
        }

        public static bool TryValidatePaperAsset(ScriptableObject paperAsset, out string problem) =>
            TryGetAssignmentFields(
                paperAsset,
                requirePaperType: true,
                requireResultFields: false,
                out _,
                out problem);

        public static bool TryWriteTrainingPaper(
            ScriptableObject paperAsset,
            YardCraneTaskSelection selection,
            out string problem)
        {
            if (!selection.IsValid)
            {
                problem = "Published training paper selection is invalid.";
                return false;
            }
            if (!TryGetAssignmentFields(
                    paperAsset,
                    requirePaperType: true,
                    requireResultFields: false,
                    out var fields,
                    out problem)) return false;

            WriteSelection(paperAsset, fields, selection);
            fields.PaperType.SetValue(paperAsset, TrainingAssessmentPaperType);
            PersistAssetInEditor(paperAsset);
            return true;
        }

        public static bool TryWriteFaultPaper(
            ScriptableObject paperAsset,
            YardCranePublishedFaultAssignment assignment,
            out string problem)
        {
            if (!TryCollectFaultIds(assignment, out var faultIds, out problem)) return false;
            if (!TryGetAssignmentFields(
                    paperAsset,
                    requirePaperType: true,
                    requireResultFields: false,
                    out var fields,
                    out problem)) return false;

            fields.PaperType.SetValue(paperAsset, FaultAssessmentPaperType);
            WriteFaultAssignment(paperAsset, fields, assignment, faultIds, 0f);
            PersistAssetInEditor(paperAsset);
            return true;
        }

        private static bool TryCollectFaultIds(
            YardCranePublishedFaultAssignment assignment,
            out List<int> faultIds,
            out string problem) =>
            TryCollectFaultIds(
                assignment,
                requirePublished: true,
                out faultIds,
                out problem);

        private static bool TryCollectFaultIds(
            YardCranePublishedFaultAssignment assignment,
            bool requirePublished,
            out List<int> faultIds,
            out string problem)
        {
            faultIds = new List<int>();
            if (assignment == null || assignment.faults == null ||
                (requirePublished && !assignment.isPublished))
            {
                problem = requirePublished
                    ? "Published fault paper is missing or inactive."
                    : "Local fault training selection is missing.";
                return false;
            }

            var usedIds = new HashSet<int>();
            foreach (var fault in assignment.faults)
            {
                if (fault != null && fault.faultId > 0 && usedIds.Add(fault.faultId))
                    faultIds.Add(fault.faultId);
            }
            if (faultIds.Count > 0)
            {
                problem = null;
                return true;
            }

            problem = "Published fault paper contains no valid fault IDs.";
            return false;
        }

        private static void WriteFaultAssignment(
            ScriptableObject asset,
            AssignmentFields fields,
            YardCranePublishedFaultAssignment assignment,
            List<int> faultIds,
            float durationSeconds)
        {
            fields.Strings["实训项目"].SetValue(
                asset, $"{assignment.craneType}集装箱起重机仿真系统");
            fields.Strings["任务名称"].SetValue(asset, "故障排除");
            fields.Strings["任务类型"].SetValue(asset, "故障考核");
            fields.Strings["任务描述"].SetValue(
                asset, $"教师发布的{assignment.craneType}故障排除考核，共 {faultIds.Count} 题。");
            for (var index = 1; index <= 3; index++)
            {
                fields.Strings[$"字段{index}标题"].SetValue(asset, string.Empty);
                fields.Strings[$"字段{index}内容"].SetValue(asset, string.Empty);
            }
            fields.TargetId.SetValue(asset, 0);
            fields.Duration.SetValue(asset, Mathf.Max(0f, durationSeconds));
            fields.TrainingMode.SetValue(asset, 1);
            fields.FaultIds.SetValue(asset, new List<int>(faultIds));
        }

        public static bool TryClearPaper(ScriptableObject paperAsset, out string problem)
        {
            if (!TryGetAssignmentFields(
                    paperAsset,
                    requirePaperType: true,
                    requireResultFields: false,
                    out var fields,
                    out problem)) return false;

            fields.PaperType.SetValue(paperAsset, EmptyPaperType);
            foreach (var field in fields.Strings.Values) field.SetValue(paperAsset, string.Empty);
            fields.TargetId.SetValue(paperAsset, 0);
            fields.Duration.SetValue(paperAsset, 0f);
            fields.TrainingMode.SetValue(paperAsset, 0);
            fields.FaultIds.SetValue(paperAsset, new List<int>());
            PersistAssetInEditor(paperAsset);
            return true;
        }

        public static bool TryCopyPaperToTaskState(
            ScriptableObject paperAsset,
            ScriptableObject stateAsset,
            out int paperType,
            out string problem)
        {
            paperType = EmptyPaperType;
            if (!TryGetAssignmentFields(
                    paperAsset,
                    requirePaperType: true,
                    requireResultFields: false,
                    out var paperFields,
                    out problem) ||
                !TryGetAssignmentFields(
                    stateAsset,
                    requirePaperType: false,
                    requireResultFields: true,
                    out var stateFields,
                    out problem)) return false;

            paperType = (int)paperFields.PaperType.GetValue(paperAsset);
            if (paperType != TrainingAssessmentPaperType && paperType != FaultAssessmentPaperType)
            {
                problem = "No published assessment paper is available.";
                return false;
            }

            foreach (var name in AssignmentStringFieldNames)
                stateFields.Strings[name].SetValue(
                    stateAsset, paperFields.Strings[name].GetValue(paperAsset));
            stateFields.TargetId.SetValue(stateAsset, paperFields.TargetId.GetValue(paperAsset));
            stateFields.Duration.SetValue(stateAsset, paperFields.Duration.GetValue(paperAsset));
            stateFields.TrainingMode.SetValue(
                stateAsset, paperFields.TrainingMode.GetValue(paperAsset));
            var sourceFaultIds = paperFields.FaultIds.GetValue(paperAsset) as List<int>;
            stateFields.FaultIds.SetValue(
                stateAsset, sourceFaultIds == null ? new List<int>() : new List<int>(sourceFaultIds));
            stateFields.Score.SetValue(stateAsset, 0);
            stateFields.Elapsed.SetValue(stateAsset, 0f);
            PersistAssetInEditor(stateAsset);
            return true;
        }

        private sealed class AssignmentFields
        {
            public readonly Dictionary<string, FieldInfo> Strings =
                new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
            public FieldInfo PaperType;
            public FieldInfo TargetId;
            public FieldInfo Duration;
            public FieldInfo TrainingMode;
            public FieldInfo FaultIds;
            public FieldInfo Score;
            public FieldInfo Elapsed;
        }

        private static bool TryGetAssignmentFields(
            ScriptableObject asset,
            bool requirePaperType,
            bool requireResultFields,
            out AssignmentFields fields,
            out string problem)
        {
            fields = null;
            problem = null;
            if (asset == null)
            {
                problem = requirePaperType
                    ? "试卷类型 asset is not assigned."
                    : "任务State asset is not assigned.";
                return false;
            }

            var result = new AssignmentFields();
            var type = asset.GetType();
            foreach (var name in AssignmentStringFieldNames)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public);
                if (field?.FieldType != typeof(string))
                {
                    problem = $"{asset.name}.asset is missing string field '{name}'.";
                    return false;
                }
                result.Strings.Add(name, field);
            }

            result.TargetId = type.GetField("目标ID", BindingFlags.Instance | BindingFlags.Public);
            result.Duration = type.GetField("实训设定时间", BindingFlags.Instance | BindingFlags.Public);
            result.TrainingMode = type.GetField("实训模式", BindingFlags.Instance | BindingFlags.Public);
            result.FaultIds = type.GetField("故障内容", BindingFlags.Instance | BindingFlags.Public);
            if (result.TargetId?.FieldType != typeof(int) ||
                result.Duration?.FieldType != typeof(float) ||
                result.TrainingMode?.FieldType != typeof(int) ||
                result.FaultIds?.FieldType != typeof(List<int>))
            {
                problem = $"{asset.name}.asset does not match the assignment numeric/list fields.";
                return false;
            }

            if (requirePaperType)
            {
                result.PaperType = type.GetField("试卷类型", BindingFlags.Instance | BindingFlags.Public);
                if (result.PaperType?.FieldType != typeof(int))
                {
                    problem = $"{asset.name}.asset is missing int field '试卷类型'.";
                    return false;
                }
            }

            if (requireResultFields)
            {
                result.Score = type.GetField("得分", BindingFlags.Instance | BindingFlags.Public);
                result.Elapsed = type.GetField("用时", BindingFlags.Instance | BindingFlags.Public);
                if (result.Score?.FieldType != typeof(int) || result.Elapsed?.FieldType != typeof(float))
                {
                    problem = $"{asset.name}.asset does not match 任务StateCreat result fields.";
                    return false;
                }
            }

            fields = result;
            return true;
        }

        private static void WriteSelection(
            ScriptableObject asset,
            AssignmentFields fields,
            YardCraneTaskSelection selection)
        {
            fields.Strings["实训项目"].SetValue(asset, selection.Catalog.ProjectName);
            fields.Strings["任务名称"].SetValue(asset, selection.Task.Name);
            fields.Strings["任务类型"].SetValue(asset, selection.Task.TaskType);
            fields.Strings["任务描述"].SetValue(asset, selection.Task.Description);
            for (var index = 0; index < 3; index++)
            {
                var hasTarget = index < selection.Task.TargetFields.Length;
                fields.Strings[$"字段{index + 1}标题"].SetValue(
                    asset, hasTarget ? selection.Task.TargetFields[index].Title : string.Empty);
                fields.Strings[$"字段{index + 1}内容"].SetValue(
                    asset,
                    hasTarget
                        ? selection.Task.TargetFields[index].Values[selection.TargetId]
                        : string.Empty);
            }
            fields.TargetId.SetValue(asset, selection.TargetId);
            fields.Duration.SetValue(asset, Mathf.Max(0f, selection.DurationSeconds));
            fields.TrainingMode.SetValue(asset, selection.TrainingMode);
            fields.FaultIds.SetValue(asset, new List<int>());
        }

        private static void PersistAssetInEditor(ScriptableObject asset)
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(asset);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(asset);
#endif
        }
    }
}
