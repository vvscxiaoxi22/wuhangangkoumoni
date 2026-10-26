using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Training
{
    [Serializable]
    public sealed class YardCraneAssessmentTrainingQuestionData
    {
        // Stable source identity from the task catalogue when one is available. The generated
        // questionId falls back to category + taskName when this is empty.
        public string sourceKey;
        public string trainingProject;
        public string category;
        public string taskName;
        public string taskType;
        public int targetId;
        public string targetDescription;
        public string taskDescription;
        public string field1Title;
        public string field2Title;
        public string field3Title;
        public string field1Content;
        public string field2Content;
        public string field3Content;
        public float durationSeconds;
        public int trainingMode;
        public string operationMode;
    }

    [Serializable]
    public sealed class YardCraneAssessmentFaultQuestionData
    {
        public int faultId;
        public string category;
        public string phenomenon;
        public string faultPoint;
        public string lineTag;
    }

    [Serializable]
    public sealed class YardCraneAssessmentQuestion
    {
        public string questionId;
        public string craneType;
        public string kind;
        public int bankOrder;
        public int paperOrder = -1;
        public bool included;
        public string createdAtUtc;
        public string updatedAtUtc;
        public YardCraneAssessmentTrainingQuestionData training;
        public YardCraneAssessmentFaultQuestionData fault;
        public List<YardCraneAssessmentFaultQuestionData> faults = new();
    }

    [Serializable]
    public sealed class YardCraneAssessmentQuestionBank
    {
        public string formatVersion;
        public string craneType;
        public string updatedAtUtc;
        public List<YardCraneAssessmentQuestion> questions = new();
    }

    [Serializable]
    public sealed class YardCraneAssessmentPaper
    {
        public string formatVersion;
        public string paperId;
        public int revision;
        public string craneType;
        public string publishedAtUtc;
        public bool isPublished;
        public List<YardCraneAssessmentQuestion> items = new();
    }

    /// <summary>
    /// Local, JSON-backed question bank and current-paper snapshot used by the teacher and
    /// student shells. It deliberately does not depend on the legacy Assets/Data scripts:
    /// ScriptableObject task state remains a single-question launch bridge, while the files
    /// written here are the durable source for a multi-question paper.
    ///
    /// Each crane type has its own bank and current-paper file. Questions are created excluded
    /// from the paper. SetIncluded is the publication operation: it updates the bank flag and
    /// rewrites a complete, ordered paper snapshot from every included question.
    /// </summary>
    public static class YardCraneAssessmentQuestionBankStore
    {
        public const string QuestionBankFormatVersion =
            "2026-09-20-yc-assessment-question-bank-v1";
        public const string PaperFormatVersion =
            "2026-09-20-yc-assessment-paper-v2-student-safe";
        private const string LegacyPaperFormatVersion =
            "2026-09-20-yc-assessment-paper-v1";
        public const string TrainingKind = "training";
        public const string FaultKind = "fault";

        private const string DirectoryName = "TeacherTasks";
        private const string YardCraneType = "场桥";
        private const string QuayCraneType = "岸桥";

#if UNITY_EDITOR
        // Verification code must never write into the same directory as a real local session.
        // This override is intentionally unavailable in Player builds.
        public static string EditorOutputDirectoryOverride { get; set; }
#endif

        public static bool TryLoadQuestionBank(
            string craneType,
            out YardCraneAssessmentQuestionBank bank,
            out string error)
        {
            bank = null;
            error = string.Empty;
            if (!TryNormalizeCraneType(craneType, out var normalizedCraneType, out error))
                return false;

            var path = ResolveBankPath(normalizedCraneType);
            if (!File.Exists(path))
            {
                bank = CreateEmptyBank(normalizedCraneType);
                return true;
            }

            try
            {
                var parsed = JsonUtility.FromJson<YardCraneAssessmentQuestionBank>(
                    File.ReadAllText(path));
                if (!TryValidateBank(parsed, normalizedCraneType, out error)) return false;
                bank = parsed;
                return true;
            }
            catch (Exception exception)
            {
                error = $"题库读取失败：{exception.Message}";
                Debug.LogWarning(
                    $"[YC_ASSESSMENT_QUESTION_BANK_READ_FAILED] path={path}, error={exception.Message}");
                return false;
            }
        }

        public static bool TryAddTrainingQuestions(
            string craneType,
            IEnumerable<YardCraneAssessmentTrainingQuestionData> trainingQuestions,
            out YardCraneAssessmentQuestion[] addedQuestions,
            out string error)
        {
            addedQuestions = Array.Empty<YardCraneAssessmentQuestion>();
            error = string.Empty;
            if (trainingQuestions == null)
            {
                error = "训练题集合不能为空。";
                return false;
            }
            if (!TryLoadQuestionBank(craneType, out var bank, out error)) return false;

            var existingIds = QuestionIds(bank.questions);
            var additions = new List<YardCraneAssessmentQuestion>();
            var nextOrder = NextBankOrder(bank.questions);
            var now = DateTime.UtcNow.ToString("O");
            foreach (var source in trainingQuestions)
            {
                if (!TryNormalizeTraining(source, out var normalized, out error)) return false;
                var questionId = StableTrainingQuestionId(bank.craneType, normalized);
                if (!existingIds.Add(questionId)) continue;

                var question = new YardCraneAssessmentQuestion
                {
                    questionId = questionId,
                    craneType = bank.craneType,
                    kind = TrainingKind,
                    bankOrder = nextOrder++,
                    paperOrder = -1,
                    included = false,
                    createdAtUtc = now,
                    updatedAtUtc = now,
                    training = normalized,
                    fault = null,
                    faults = null
                };
                bank.questions.Add(question);
                additions.Add(CloneQuestion(question));
            }

            if (additions.Count == 0) return true;
            bank.updatedAtUtc = now;
            if (!TryWriteBank(bank, out error)) return false;
            addedQuestions = additions.ToArray();
            return true;
        }

        public static bool TryAddFaultQuestions(
            string craneType,
            IEnumerable<YardCraneAssessmentFaultQuestionData> faultQuestions,
            out YardCraneAssessmentQuestion[] addedQuestions,
            out string error)
        {
            addedQuestions = Array.Empty<YardCraneAssessmentQuestion>();
            error = string.Empty;
            if (faultQuestions == null)
            {
                error = "故障题集合不能为空。";
                return false;
            }
            if (!TryLoadQuestionBank(craneType, out var bank, out error)) return false;

            var existingIds = QuestionIds(bank.questions);
            var additions = new List<YardCraneAssessmentQuestion>();
            var nextOrder = NextBankOrder(bank.questions);
            var now = DateTime.UtcNow.ToString("O");
            foreach (var source in faultQuestions)
            {
                if (!TryNormalizeFault(source, out var normalized, out error)) return false;
                var questionId = StableFaultQuestionId(bank.craneType, normalized.faultId);
                if (!existingIds.Add(questionId)) continue;

                var question = new YardCraneAssessmentQuestion
                {
                    questionId = questionId,
                    craneType = bank.craneType,
                    kind = FaultKind,
                    bankOrder = nextOrder++,
                    paperOrder = -1,
                    included = false,
                    createdAtUtc = now,
                    updatedAtUtc = now,
                    training = null,
                    fault = CloneFault(normalized),
                    faults = new List<YardCraneAssessmentFaultQuestionData>
                    {
                        normalized
                    }
                };
                bank.questions.Add(question);
                additions.Add(CloneQuestion(question));
            }

            if (additions.Count == 0) return true;
            bank.updatedAtUtc = now;
            if (!TryWriteBank(bank, out error)) return false;
            addedQuestions = additions.ToArray();
            return true;
        }

        public static bool TryAddFaultTask(
            string craneType,
            IEnumerable<YardCraneAssessmentFaultQuestionData> faultItems,
            out YardCraneAssessmentQuestion addedQuestion,
            out string error)
        {
            addedQuestion = null;
            error = string.Empty;
            if (faultItems == null)
            {
                error = "故障任务内容不能为空。";
                return false;
            }
            if (!TryLoadQuestionBank(craneType, out var bank, out error)) return false;

            var normalizedFaults = new List<YardCraneAssessmentFaultQuestionData>();
            var usedFaultIds = new HashSet<int>();
            foreach (var source in faultItems)
            {
                if (!TryNormalizeFault(source, out var normalized, out error)) return false;
                if (usedFaultIds.Add(normalized.faultId)) normalizedFaults.Add(normalized);
            }
            if (normalizedFaults.Count == 0)
            {
                error = "故障任务至少需要一条有效故障。";
                return false;
            }

            var questionId = StableFaultTaskQuestionId(
                bank.craneType,
                normalizedFaults.ConvertAll(item => item.faultId));
            if (QuestionIds(bank.questions).Contains(questionId)) return true;

            var now = DateTime.UtcNow.ToString("O");
            var question = new YardCraneAssessmentQuestion
            {
                questionId = questionId,
                craneType = bank.craneType,
                kind = FaultKind,
                bankOrder = NextBankOrder(bank.questions),
                paperOrder = -1,
                included = false,
                createdAtUtc = now,
                updatedAtUtc = now,
                training = null,
                fault = CloneFault(normalizedFaults[0]),
                faults = normalizedFaults
            };
            bank.questions.Add(question);
            bank.updatedAtUtc = now;
            if (!TryWriteBank(bank, out error)) return false;
            addedQuestion = CloneQuestion(question);
            return true;
        }

        public static bool TryUpdateTrainingQuestion(
            string craneType,
            string questionId,
            YardCraneAssessmentTrainingQuestionData trainingQuestion,
            out YardCraneAssessmentQuestion updatedQuestion,
            out string error)
        {
            updatedQuestion = null;
            error = string.Empty;
            if (!TryNormalizeQuestionId(questionId, out questionId, out error) ||
                !TryNormalizeTraining(trainingQuestion, out var normalized, out error) ||
                !TryLoadQuestionBank(craneType, out var bank, out error)) return false;

            var question = FindQuestion(bank.questions, questionId);
            if (question == null)
            {
                error = $"未找到训练题：{questionId}。";
                return false;
            }
            if (!string.Equals(question.kind, TrainingKind, StringComparison.Ordinal))
            {
                error = $"题目 {questionId} 不是训练题。";
                return false;
            }

            question.training = normalized;
            question.fault = null;
            question.faults = null;
            question.updatedAtUtc = DateTime.UtcNow.ToString("O");
            bank.updatedAtUtc = question.updatedAtUtc;

            if (question.included)
            {
                if (!TryBuildPaper(bank, out var paper, out error) ||
                    !TryWriteBankAndPaper(bank, paper, out error)) return false;
            }
            else if (!TryWriteBank(bank, out error))
            {
                return false;
            }

            updatedQuestion = CloneQuestion(question);
            return true;
        }

        public static bool TryDeleteQuestion(
            string craneType,
            string questionId,
            out string error)
        {
            error = string.Empty;
            if (!TryNormalizeQuestionId(questionId, out questionId, out error) ||
                !TryLoadQuestionBank(craneType, out var bank, out error)) return false;

            var removedIndex = bank.questions.FindIndex(
                candidate => string.Equals(candidate.questionId, questionId, StringComparison.Ordinal));
            if (removedIndex < 0)
            {
                error = $"未找到题目：{questionId}。";
                return false;
            }

            var rebuildPaper = bank.questions[removedIndex].included;
            bank.questions.RemoveAt(removedIndex);
            bank.updatedAtUtc = DateTime.UtcNow.ToString("O");
            if (!rebuildPaper) return TryWriteBank(bank, out error);

            return TryBuildPaper(bank, out var paper, out error) &&
                   TryWriteBankAndPaper(bank, paper, out error);
        }

        public static bool TrySetIncluded(
            string craneType,
            string questionId,
            bool included,
            out YardCraneAssessmentPaper paper,
            out string error)
        {
            paper = null;
            error = string.Empty;
            if (!TryNormalizeQuestionId(questionId, out questionId, out error) ||
                !TryLoadQuestionBank(craneType, out var bank, out error)) return false;

            var question = FindQuestion(bank.questions, questionId);
            if (question == null)
            {
                error = $"未找到题目：{questionId}。";
                return false;
            }

            question.included = included;
            question.updatedAtUtc = DateTime.UtcNow.ToString("O");
            bank.updatedAtUtc = question.updatedAtUtc;
            if (!TryBuildPaper(bank, out paper, out error)) return false;
            return TryWriteBankAndPaper(bank, paper, out error);
        }

        public static bool TryRebuildCurrentPaper(
            string craneType,
            out YardCraneAssessmentPaper paper,
            out string error)
        {
            paper = null;
            if (!TryLoadQuestionBank(craneType, out var bank, out error) ||
                !TryBuildPaper(bank, out paper, out error)) return false;
            return TryWritePaper(paper, out error);
        }

        public static bool TryLoadCurrentPaper(
            string craneType,
            out YardCraneAssessmentPaper paper,
            out string error)
        {
            paper = null;
            error = string.Empty;
            if (!TryNormalizeCraneType(craneType, out var normalizedCraneType, out error))
                return false;

            var path = ResolvePaperPath(normalizedCraneType);
            if (!File.Exists(path)) return false;
            try
            {
                var parsed = JsonUtility.FromJson<YardCraneAssessmentPaper>(File.ReadAllText(path));
                if (string.Equals(
                        parsed?.formatVersion,
                        LegacyPaperFormatVersion,
                        StringComparison.Ordinal))
                {
                    if (!TryUpgradeLegacyPaper(parsed, normalizedCraneType, out error) ||
                        !TryWritePaper(parsed, out error)) return false;
                }
                if (!TryValidatePaper(parsed, normalizedCraneType, out error)) return false;
                if (!parsed.isPublished || parsed.items.Count == 0) return false;
                paper = parsed;
                return true;
            }
            catch (Exception exception)
            {
                error = $"当前试卷读取失败：{exception.Message}";
                Debug.LogWarning(
                    $"[YC_ASSESSMENT_PAPER_READ_FAILED] path={path}, error={exception.Message}");
                return false;
            }
        }

        public static string StableTrainingQuestionId(
            string craneType,
            YardCraneAssessmentTrainingQuestionData trainingQuestion)
        {
            var sourceIdentity = string.IsNullOrWhiteSpace(trainingQuestion?.sourceKey)
                ? $"{trainingQuestion?.category}\u001e{trainingQuestion?.taskName}"
                : trainingQuestion.sourceKey;
            return StableId(TrainingKind, craneType, sourceIdentity);
        }

        public static string StableFaultQuestionId(string craneType, int faultId) =>
            StableId(FaultKind, craneType, faultId.ToString());

        public static string StableFaultTaskQuestionId(
            string craneType,
            IEnumerable<int> faultIds)
        {
            var ordered = new List<int>();
            if (faultIds != null)
            {
                var used = new HashSet<int>();
                foreach (var faultId in faultIds)
                    if (faultId > 0 && used.Add(faultId)) ordered.Add(faultId);
            }
            ordered.Sort();
            return StableId(FaultKind, craneType, string.Join(",", ordered));
        }

        public static IReadOnlyList<YardCraneAssessmentFaultQuestionData> FaultItems(
            YardCraneAssessmentQuestion question)
        {
            if (question?.faults != null && question.faults.Count > 0) return question.faults;
            if (question?.fault != null)
                return new[] { question.fault };
            return Array.Empty<YardCraneAssessmentFaultQuestionData>();
        }

        private static bool TryBuildPaper(
            YardCraneAssessmentQuestionBank bank,
            out YardCraneAssessmentPaper paper,
            out string error)
        {
            paper = null;
            error = string.Empty;
            if (!TryValidateBank(bank, bank?.craneType, out error)) return false;

            var existingPaperId = string.Empty;
            var existingRevision = 0;
            var paperPath = ResolvePaperPath(bank.craneType);
            if (File.Exists(paperPath))
            {
                try
                {
                    var existing = JsonUtility.FromJson<YardCraneAssessmentPaper>(
                        File.ReadAllText(paperPath));
                    if (string.Equals(
                            existing?.formatVersion,
                            LegacyPaperFormatVersion,
                            StringComparison.Ordinal) &&
                        !TryUpgradeLegacyPaper(existing, bank.craneType, out error)) return false;
                    if (!TryValidatePaper(existing, bank.craneType, out error)) return false;
                    existingPaperId = existing.paperId;
                    existingRevision = Math.Max(0, existing.revision);
                }
                catch (Exception exception)
                {
                    error = $"现有试卷读取失败，未覆盖原文件：{exception.Message}";
                    return false;
                }
            }

            var ordered = new List<YardCraneAssessmentQuestion>();
            foreach (var question in bank.questions)
                if (question.included) ordered.Add(question);
            ordered.Sort((left, right) =>
            {
                var orderComparison = left.bankOrder.CompareTo(right.bankOrder);
                return orderComparison != 0
                    ? orderComparison
                    : string.CompareOrdinal(left.questionId, right.questionId);
            });

            var items = new List<YardCraneAssessmentQuestion>(ordered.Count);
            for (var index = 0; index < ordered.Count; index++)
            {
                var snapshot = CloneQuestion(ordered[index]);
                snapshot.paperOrder = index;
                // The student-facing paper contains only the fault prompt. Fault point and
                // line/tag are teacher answer data and remain exclusively in the question bank.
                if (snapshot.fault != null)
                {
                    snapshot.fault.faultPoint = string.Empty;
                    snapshot.fault.lineTag = string.Empty;
                }
                if (snapshot.faults != null)
                {
                    foreach (var fault in snapshot.faults)
                    {
                        if (fault == null) continue;
                        fault.faultPoint = string.Empty;
                        fault.lineTag = string.Empty;
                    }
                }
                items.Add(snapshot);
            }

            paper = new YardCraneAssessmentPaper
            {
                formatVersion = PaperFormatVersion,
                paperId = string.IsNullOrEmpty(existingPaperId)
                    ? Guid.NewGuid().ToString("N")
                    : existingPaperId,
                revision = existingRevision + 1,
                craneType = bank.craneType,
                publishedAtUtc = DateTime.UtcNow.ToString("O"),
                isPublished = items.Count > 0,
                items = items
            };
            return true;
        }

        private static bool TryWriteBank(YardCraneAssessmentQuestionBank bank, out string error)
        {
            error = string.Empty;
            try
            {
                WriteTextAtomic(ResolveBankPath(bank.craneType), JsonUtility.ToJson(bank, true));
                return true;
            }
            catch (Exception exception)
            {
                error = $"题库写入失败：{exception.Message}";
                Debug.LogWarning($"[YC_ASSESSMENT_QUESTION_BANK_WRITE_FAILED] {exception}");
                return false;
            }
        }

        private static bool TryWritePaper(YardCraneAssessmentPaper paper, out string error)
        {
            error = string.Empty;
            try
            {
                WriteTextAtomic(ResolvePaperPath(paper.craneType), JsonUtility.ToJson(paper, true));
                return true;
            }
            catch (Exception exception)
            {
                error = $"当前试卷写入失败：{exception.Message}";
                Debug.LogWarning($"[YC_ASSESSMENT_PAPER_WRITE_FAILED] {exception}");
                return false;
            }
        }

        private static bool TryWriteBankAndPaper(
            YardCraneAssessmentQuestionBank bank,
            YardCraneAssessmentPaper paper,
            out string error)
        {
            var bankPath = ResolveBankPath(bank.craneType);
            var paperPath = ResolvePaperPath(bank.craneType);
            ExistingFile oldBank = null;
            ExistingFile oldPaper = null;
            try
            {
                oldBank = ReadExistingFile(bankPath);
                oldPaper = ReadExistingFile(paperPath);
                WriteTextAtomic(bankPath, JsonUtility.ToJson(bank, true));
                WriteTextAtomic(paperPath, JsonUtility.ToJson(paper, true));
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                var rollbackProblems = new List<string>();
                if (oldBank != null) TryRestoreFile(bankPath, oldBank, rollbackProblems);
                if (oldPaper != null) TryRestoreFile(paperPath, oldPaper, rollbackProblems);
                error = $"题库/试卷写入失败：{exception.Message}";
                if (rollbackProblems.Count > 0)
                    error += $"；回滚异常：{string.Join(" | ", rollbackProblems)}";
                Debug.LogWarning($"[YC_ASSESSMENT_BANK_PAPER_WRITE_FAILED] {error}");
                return false;
            }
        }

        private static bool TryValidateBank(
            YardCraneAssessmentQuestionBank bank,
            string expectedCraneType,
            out string error)
        {
            error = string.Empty;
            if (bank == null)
            {
                error = "题库文件为空。";
                return false;
            }
            if (!string.Equals(bank.formatVersion, QuestionBankFormatVersion, StringComparison.Ordinal))
            {
                error = $"不支持的题库版本：{bank.formatVersion ?? "(空)"}。";
                return false;
            }
            if (!string.Equals(bank.craneType, expectedCraneType, StringComparison.Ordinal))
            {
                error = $"题库设备类型不匹配：期望 {expectedCraneType}，实际 {bank.craneType ?? "(空)"}。";
                return false;
            }

            bank.questions ??= new List<YardCraneAssessmentQuestion>();
            var usedIds = new HashSet<string>(StringComparer.Ordinal);
            var usedOrders = new HashSet<int>();
            foreach (var question in bank.questions)
            {
                if (!TryValidateQuestion(question, bank.craneType, out error)) return false;
                if (!usedIds.Add(question.questionId))
                {
                    error = $"题库包含重复 questionId：{question.questionId}。";
                    return false;
                }
                if (question.bankOrder < 0 || !usedOrders.Add(question.bankOrder))
                {
                    error = $"题库题目 {question.questionId} 的 bankOrder 无效或重复。";
                    return false;
                }
            }
            return true;
        }

        private static bool TryValidatePaper(
            YardCraneAssessmentPaper paper,
            string expectedCraneType,
            out string error)
        {
            error = string.Empty;
            if (paper == null)
            {
                error = "当前试卷文件为空。";
                return false;
            }
            if (!string.Equals(paper.formatVersion, PaperFormatVersion, StringComparison.Ordinal))
            {
                error = $"不支持的试卷版本：{paper.formatVersion ?? "(空)"}。";
                return false;
            }
            if (!string.Equals(paper.craneType, expectedCraneType, StringComparison.Ordinal))
            {
                error = $"试卷设备类型不匹配：期望 {expectedCraneType}，实际 {paper.craneType ?? "(空)"}。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(paper.paperId) || paper.revision < 1)
            {
                error = "试卷缺少有效 paperId 或 revision。";
                return false;
            }

            paper.items ??= new List<YardCraneAssessmentQuestion>();
            if (paper.isPublished != (paper.items.Count > 0))
            {
                error = "试卷发布状态与题目数量不一致。";
                return false;
            }
            var usedIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < paper.items.Count; index++)
            {
                var item = paper.items[index];
                if (!TryValidateQuestion(item, paper.craneType, out error)) return false;
                if (!usedIds.Add(item.questionId))
                {
                    error = $"试卷包含重复 questionId：{item.questionId}。";
                    return false;
                }
                if (!item.included || item.paperOrder != index)
                {
                    error = $"试卷题目 {item.questionId} 的 included/paperOrder 无效。";
                    return false;
                }
                if (item.fault != null &&
                    (!string.IsNullOrEmpty(item.fault.faultPoint) ||
                     !string.IsNullOrEmpty(item.fault.lineTag)))
                {
                    error = $"试卷故障题 {item.questionId} 包含仅教师可见的答案字段。";
                    return false;
                }
                if (item.faults == null) continue;
                foreach (var fault in item.faults)
                {
                    if (fault == null ||
                        string.IsNullOrEmpty(fault.faultPoint) &&
                        string.IsNullOrEmpty(fault.lineTag)) continue;
                    error = $"试卷故障题 {item.questionId} 包含仅教师可见的答案字段。";
                    return false;
                }
            }
            return true;
        }

        private static bool TryUpgradeLegacyPaper(
            YardCraneAssessmentPaper paper,
            string expectedCraneType,
            out string error)
        {
            error = string.Empty;
            if (paper == null ||
                !string.Equals(
                    paper.formatVersion,
                    LegacyPaperFormatVersion,
                    StringComparison.Ordinal))
            {
                error = "旧版试卷内容无效，无法安全升级。";
                return false;
            }

            paper.items ??= new List<YardCraneAssessmentQuestion>();
            foreach (var item in paper.items)
            {
                if (item?.fault != null)
                {
                    item.fault.faultPoint = string.Empty;
                    item.fault.lineTag = string.Empty;
                }
                if (item?.faults == null) continue;
                foreach (var fault in item.faults)
                {
                    if (fault == null) continue;
                    fault.faultPoint = string.Empty;
                    fault.lineTag = string.Empty;
                }
            }
            paper.formatVersion = PaperFormatVersion;
            return TryValidatePaper(paper, expectedCraneType, out error);
        }

        private static bool TryValidateQuestion(
            YardCraneAssessmentQuestion question,
            string expectedCraneType,
            out string error)
        {
            error = string.Empty;
            if (question == null || string.IsNullOrWhiteSpace(question.questionId))
            {
                error = "题库包含空题目或空 questionId。";
                return false;
            }
            if (!string.Equals(question.craneType, expectedCraneType, StringComparison.Ordinal))
            {
                error = $"题目 {question.questionId} 的设备类型不匹配。";
                return false;
            }
            if (string.Equals(question.kind, TrainingKind, StringComparison.Ordinal))
            {
                if (!TryNormalizeTraining(question.training, out _, out error))
                {
                    error = $"训练题 {question.questionId} 无效：{error}";
                    return false;
                }
                if (question.fault != null)
                {
                    if (HasFaultPayload(question.fault))
                    {
                        error = $"训练题 {question.questionId} 不应包含故障字段。";
                        return false;
                    }

                    // JsonUtility may materialize a null nested serializable field as an
                    // empty default object after a JSON round trip. Treat only that exact
                    // empty shape as null; populated cross-kind data remains invalid.
                    question.fault = null;
                }
                if (question.faults != null && question.faults.Count > 0)
                {
                    error = $"训练题 {question.questionId} 不应包含故障列表。";
                    return false;
                }
                question.faults = null;
                return true;
            }
            if (string.Equals(question.kind, FaultKind, StringComparison.Ordinal))
            {
                var normalizedFaults = new List<YardCraneAssessmentFaultQuestionData>();
                var usedFaultIds = new HashSet<int>();
                if (question.faults != null)
                {
                    foreach (var source in question.faults)
                    {
                        if (!TryNormalizeFault(source, out var normalized, out error))
                        {
                            error = $"故障题 {question.questionId} 无效：{error}";
                            return false;
                        }
                        if (!usedFaultIds.Add(normalized.faultId))
                        {
                            error = $"故障题 {question.questionId} 包含重复故障编号 {normalized.faultId}。";
                            return false;
                        }
                        normalizedFaults.Add(normalized);
                    }
                }
                if (normalizedFaults.Count == 0)
                {
                    if (!TryNormalizeFault(question.fault, out var legacyFault, out error))
                    {
                        error = $"故障题 {question.questionId} 无效：{error}";
                        return false;
                    }
                    normalizedFaults.Add(legacyFault);
                }
                else if (question.fault != null && HasFaultPayload(question.fault))
                {
                    if (!TryNormalizeFault(question.fault, out var primaryFault, out error) ||
                        primaryFault.faultId != normalizedFaults[0].faultId)
                    {
                        error = $"故障题 {question.questionId} 的首条故障与故障列表不一致。";
                        return false;
                    }
                }
                if (question.training != null)
                {
                    if (HasTrainingPayload(question.training))
                    {
                        error = $"故障题 {question.questionId} 不应包含训练字段。";
                        return false;
                    }

                    question.training = null;
                }
                question.faults = normalizedFaults;
                question.fault = CloneFault(normalizedFaults[0]);
                return true;
            }

            error = $"题目 {question.questionId} 使用了不支持的 kind：{question.kind ?? "(空)"}。";
            return false;
        }

        private static bool HasTrainingPayload(YardCraneAssessmentTrainingQuestionData source)
        {
            if (source == null) return false;
            return !string.IsNullOrWhiteSpace(source.sourceKey) ||
                   !string.IsNullOrWhiteSpace(source.trainingProject) ||
                   !string.IsNullOrWhiteSpace(source.category) ||
                   !string.IsNullOrWhiteSpace(source.taskName) ||
                   !string.IsNullOrWhiteSpace(source.taskType) ||
                   source.targetId != 0 ||
                   !string.IsNullOrWhiteSpace(source.targetDescription) ||
                   !string.IsNullOrWhiteSpace(source.taskDescription) ||
                   !string.IsNullOrWhiteSpace(source.field1Title) ||
                   !string.IsNullOrWhiteSpace(source.field2Title) ||
                   !string.IsNullOrWhiteSpace(source.field3Title) ||
                   !string.IsNullOrWhiteSpace(source.field1Content) ||
                   !string.IsNullOrWhiteSpace(source.field2Content) ||
                   !string.IsNullOrWhiteSpace(source.field3Content) ||
                   source.durationSeconds != 0f ||
                   source.trainingMode != 0 ||
                   !string.IsNullOrWhiteSpace(source.operationMode);
        }

        private static bool HasFaultPayload(YardCraneAssessmentFaultQuestionData source)
        {
            if (source == null) return false;
            return source.faultId != 0 ||
                   !string.IsNullOrWhiteSpace(source.category) ||
                   !string.IsNullOrWhiteSpace(source.phenomenon) ||
                   !string.IsNullOrWhiteSpace(source.faultPoint) ||
                   !string.IsNullOrWhiteSpace(source.lineTag);
        }

        private static bool TryNormalizeTraining(
            YardCraneAssessmentTrainingQuestionData source,
            out YardCraneAssessmentTrainingQuestionData normalized,
            out string error)
        {
            normalized = null;
            error = string.Empty;
            if (source == null)
            {
                error = "训练题内容为空。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(source.taskName))
            {
                error = "训练题缺少 taskName。";
                return false;
            }
            if (source.targetId < 0)
            {
                error = "训练题 targetId 不能小于 0。";
                return false;
            }
            if (float.IsNaN(source.durationSeconds) || float.IsInfinity(source.durationSeconds) ||
                source.durationSeconds < 0f)
            {
                error = "训练题 durationSeconds 无效。";
                return false;
            }
            if (source.trainingMode != 1 && source.trainingMode != 2)
            {
                error = "训练题 trainingMode 必须为 1（本地）或 2（远程）。";
                return false;
            }

            normalized = new YardCraneAssessmentTrainingQuestionData
            {
                sourceKey = Clean(source.sourceKey),
                trainingProject = Clean(source.trainingProject),
                category = Clean(source.category),
                taskName = Clean(source.taskName),
                taskType = Clean(source.taskType),
                targetId = source.targetId,
                targetDescription = Clean(source.targetDescription),
                taskDescription = Clean(source.taskDescription),
                field1Title = Clean(source.field1Title),
                field2Title = Clean(source.field2Title),
                field3Title = Clean(source.field3Title),
                field1Content = Clean(source.field1Content),
                field2Content = Clean(source.field2Content),
                field3Content = Clean(source.field3Content),
                durationSeconds = source.durationSeconds,
                trainingMode = source.trainingMode,
                operationMode = string.IsNullOrWhiteSpace(source.operationMode)
                    ? source.trainingMode == 2 ? "远程操作" : "本地操作"
                    : source.operationMode.Trim()
            };
            return true;
        }

        private static bool TryNormalizeFault(
            YardCraneAssessmentFaultQuestionData source,
            out YardCraneAssessmentFaultQuestionData normalized,
            out string error)
        {
            normalized = null;
            error = string.Empty;
            if (source == null)
            {
                error = "故障题内容为空。";
                return false;
            }
            if (source.faultId <= 0)
            {
                error = "故障题 faultId 必须大于 0。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(source.phenomenon))
            {
                error = $"故障题 {source.faultId} 缺少故障现象。";
                return false;
            }

            normalized = new YardCraneAssessmentFaultQuestionData
            {
                faultId = source.faultId,
                category = Clean(source.category),
                phenomenon = Clean(source.phenomenon),
                faultPoint = Clean(source.faultPoint),
                lineTag = Clean(source.lineTag)
            };
            return true;
        }

        private static YardCraneAssessmentQuestionBank CreateEmptyBank(string craneType) =>
            new YardCraneAssessmentQuestionBank
            {
                formatVersion = QuestionBankFormatVersion,
                craneType = craneType,
                updatedAtUtc = DateTime.UtcNow.ToString("O"),
                questions = new List<YardCraneAssessmentQuestion>()
            };

        private static YardCraneAssessmentQuestion FindQuestion(
            List<YardCraneAssessmentQuestion> questions,
            string questionId)
        {
            foreach (var question in questions)
                if (string.Equals(question.questionId, questionId, StringComparison.Ordinal))
                    return question;
            return null;
        }

        private static HashSet<string> QuestionIds(IEnumerable<YardCraneAssessmentQuestion> questions)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var question in questions)
                if (question != null && !string.IsNullOrWhiteSpace(question.questionId))
                    ids.Add(question.questionId);
            return ids;
        }

        private static int NextBankOrder(IEnumerable<YardCraneAssessmentQuestion> questions)
        {
            var next = 0;
            foreach (var question in questions)
                if (question != null) next = Math.Max(next, question.bankOrder + 1);
            return next;
        }

        private static YardCraneAssessmentQuestion CloneQuestion(YardCraneAssessmentQuestion source)
        {
            return new YardCraneAssessmentQuestion
            {
                questionId = source.questionId,
                craneType = source.craneType,
                kind = source.kind,
                bankOrder = source.bankOrder,
                paperOrder = source.paperOrder,
                included = source.included,
                createdAtUtc = source.createdAtUtc,
                updatedAtUtc = source.updatedAtUtc,
                training = source.training == null
                    ? null
                    : new YardCraneAssessmentTrainingQuestionData
                    {
                        sourceKey = source.training.sourceKey,
                        trainingProject = source.training.trainingProject,
                        category = source.training.category,
                        taskName = source.training.taskName,
                        taskType = source.training.taskType,
                        targetId = source.training.targetId,
                        targetDescription = source.training.targetDescription,
                        taskDescription = source.training.taskDescription,
                        field1Title = source.training.field1Title,
                        field2Title = source.training.field2Title,
                        field3Title = source.training.field3Title,
                        field1Content = source.training.field1Content,
                        field2Content = source.training.field2Content,
                        field3Content = source.training.field3Content,
                        durationSeconds = source.training.durationSeconds,
                        trainingMode = source.training.trainingMode,
                        operationMode = source.training.operationMode
                    },
                fault = source.fault == null
                    ? null
                    : CloneFault(source.fault),
                faults = CloneFaults(source.faults)
            };
        }

        private static List<YardCraneAssessmentFaultQuestionData> CloneFaults(
            IEnumerable<YardCraneAssessmentFaultQuestionData> source)
        {
            if (source == null) return null;
            var result = new List<YardCraneAssessmentFaultQuestionData>();
            foreach (var item in source)
                if (item != null) result.Add(CloneFault(item));
            return result;
        }

        private static YardCraneAssessmentFaultQuestionData CloneFault(
            YardCraneAssessmentFaultQuestionData source) =>
            source == null
                ? null
                : new YardCraneAssessmentFaultQuestionData
                {
                    faultId = source.faultId,
                    category = source.category,
                    phenomenon = source.phenomenon,
                    faultPoint = source.faultPoint,
                    lineTag = source.lineTag
                };

        private static string StableId(string prefix, params string[] parts)
        {
            var canonical = new StringBuilder(prefix);
            foreach (var part in parts)
            {
                canonical.Append('\u001f');
                canonical.Append(Clean(part).Normalize(NormalizationForm.FormKC));
            }

            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
            var text = new StringBuilder(prefix.Length + 1 + hash.Length * 2);
            text.Append(prefix).Append('-');
            foreach (var value in hash) text.Append(value.ToString("x2"));
            return text.ToString();
        }

        private static bool TryNormalizeQuestionId(
            string source,
            out string normalized,
            out string error)
        {
            normalized = Clean(source);
            error = string.Empty;
            if (!string.IsNullOrEmpty(normalized)) return true;
            error = "questionId 不能为空。";
            return false;
        }

        private static bool TryNormalizeCraneType(
            string source,
            out string normalized,
            out string error)
        {
            normalized = Clean(source);
            error = string.Empty;
            if (normalized == YardCraneType || normalized == QuayCraneType) return true;
            error = $"不支持的设备类型：{normalized ?? "(空)"}。";
            return false;
        }

        private static string Clean(string value) => value?.Trim() ?? string.Empty;

        private static string ResolveBankPath(string craneType) =>
            Path.Combine(ResolveDirectory(), FilePrefix(craneType) + "-assessment-question-bank.json");

        private static string ResolvePaperPath(string craneType) =>
            Path.Combine(ResolveDirectory(), FilePrefix(craneType) + "-assessment-paper.json");

        private static string FilePrefix(string craneType) =>
            string.Equals(craneType, QuayCraneType, StringComparison.Ordinal) ? "quay" : "yard";

        private static string ResolveDirectory()
        {
#if UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace(EditorOutputDirectoryOverride))
                return Path.GetFullPath(EditorOutputDirectoryOverride);
#endif
            return Path.Combine(YardCraneLocalDataPaths.RootDirectory, DirectoryName);
        }

        private sealed class ExistingFile
        {
            public bool Exists;
            public string Text;
        }

        private static ExistingFile ReadExistingFile(string path) =>
            new ExistingFile
            {
                Exists = File.Exists(path),
                Text = File.Exists(path) ? File.ReadAllText(path) : null
            };

        private static void TryRestoreFile(
            string path,
            ExistingFile existing,
            ICollection<string> problems)
        {
            try
            {
                if (existing.Exists) WriteTextAtomic(path, existing.Text);
                else if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception exception)
            {
                problems.Add($"{Path.GetFileName(path)}: {exception.Message}");
            }
        }

        private static void WriteTextAtomic(string path, string text)
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException($"Cannot resolve directory for '{path}'.");
            Directory.CreateDirectory(directory);

            var temporaryPath = Path.Combine(
                directory,
                $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(temporaryPath, text ?? string.Empty, new UTF8Encoding(false));
                if (!File.Exists(path))
                {
                    File.Move(temporaryPath, path);
                    return;
                }

                try
                {
                    File.Replace(temporaryPath, path, null);
                    return;
                }
                catch (PlatformNotSupportedException)
                {
                    // Some Unity targets do not implement File.Replace. Fall through to a
                    // same-directory move with rollback rather than copying a partial file.
                }
                catch (IOException)
                {
                    // File.Replace can be unavailable on a particular filesystem. The fallback
                    // keeps the old file as a rollback source until the new move succeeds.
                }

                var backupPath = Path.Combine(
                    directory,
                    $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.bak");
                File.Move(path, backupPath);
                try
                {
                    File.Move(temporaryPath, path);
                    File.Delete(backupPath);
                }
                catch
                {
                    if (File.Exists(path)) File.Delete(path);
                    if (File.Exists(backupPath)) File.Move(backupPath, path);
                    throw;
                }
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }
    }
}
