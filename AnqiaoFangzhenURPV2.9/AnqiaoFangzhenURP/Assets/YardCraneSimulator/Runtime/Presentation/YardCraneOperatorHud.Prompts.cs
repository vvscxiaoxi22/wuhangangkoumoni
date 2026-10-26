using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PortTraining.YardCraneSimulator.Presentation
{
    public sealed partial class YardCraneOperatorHud
    {
        private const int BayGridColumns = 12;
        private const int BayGridRows = 9;

        private readonly Dictionary<YardCraneHudPromptKind, VisualElement> promptViews = new();
        private readonly List<Button> promptCloseButtons = new();
        private readonly List<TextField> submissionFields = new();

        private VisualElement promptLayer;
        private Button taskPromptCloseButton;
        private VisualElement taskSourceGrid;
        private VisualElement taskDestinationGrid;
        private VisualElement countdownRing;
        private ScrollView assessmentTableBody;
        private Button confirmationConfirmButton;
        private Button confirmationCancelButton;
        private Button errorAcknowledgeButton;
        private Button completionDetailsButton;
        private Button completionRetryButton;
        private Button submissionSubmitButton;
        private TextField submissionName;
        private TextField submissionStudentNumber;
        private TextField submissionMajor;
        private TextField submissionClass;

        private Action pendingPrompt;
        private Action countdownElapsedAction;
        private Action confirmationAcceptedAction;
        private Action confirmationCancelledAction;
        private Action errorAcknowledgedAction;
        private Action completionDetailsAction;
        private Action completionRetryAction;
        private Action<YardCraneHudScoreSubmission> scoreSubmissionAction;
        private double countdownDeadline = -1;
        private int countdownTotalSeconds;
        private int lastCountdownRemaining = int.MinValue;
        private bool submissionRequestPending;

        public YardCraneHudPromptKind ActivePromptKind { get; private set; }
        public bool IsPromptVisible => ActivePromptKind != YardCraneHudPromptKind.None && promptLayer != null;
        private bool HasDeferredPrompt => pendingPrompt != null;
        public event Action<YardCraneHudPromptKind> PromptChanged;

        private void QueryPromptElements(ICollection<string> missingNodes)
        {
            promptViews.Clear();
            promptCloseButtons.Clear();
            submissionFields.Clear();

            promptLayer = PromptNode<VisualElement>("prompt-layer", missingNodes);
            AddPromptView(YardCraneHudPromptKind.TaskBrief, "task-prompt", missingNodes);
            AddPromptView(YardCraneHudPromptKind.Countdown, "countdown-prompt", missingNodes);
            AddPromptView(YardCraneHudPromptKind.Confirmation, "confirmation-prompt", missingNodes);
            AddPromptView(YardCraneHudPromptKind.Error, "error-prompt", missingNodes);
            AddPromptView(YardCraneHudPromptKind.Completion, "completion-prompt", missingNodes);
            AddPromptView(YardCraneHudPromptKind.AssessmentDetails, "assessment-prompt", missingNodes);
            AddPromptView(YardCraneHudPromptKind.ScoreSubmission, "submission-prompt", missingNodes);

            foreach (var id in new[]
                     {
                         "confirmation-prompt-close", "error-prompt-close",
                         "completion-prompt-close", "assessment-prompt-close", "submission-prompt-close"
                     })
            {
                var button = PromptNode<Button>(id, missingNodes);
                if (button != null) promptCloseButtons.Add(button);
            }
            // 2026-09-11: queried separately (not just via the generic loop above) so its text
            // can be set per-call from YardCraneHudTaskPrompt.ConfirmText, the same way
            // confirmationConfirmButton/errorAcknowledgeButton already work below - it is still
            // added to promptCloseButtons so BindPromptInteractions wires its click the same as
            // any other close-style button (ClosePrompt, since its name is neither of the two
            // special-cased ones there).
            taskPromptCloseButton = PromptNode<Button>("task-prompt-close", missingNodes);
            if (taskPromptCloseButton != null) promptCloseButtons.Add(taskPromptCloseButton);

            taskSourceGrid = PromptNode<VisualElement>("task-source-grid", missingNodes);
            taskDestinationGrid = PromptNode<VisualElement>("task-destination-grid", missingNodes);
            countdownRing = PromptNode<VisualElement>("countdown-ring", missingNodes);
            assessmentTableBody = PromptNode<ScrollView>("assessment-table-body", missingNodes);
            confirmationConfirmButton = PromptNode<Button>("confirmation-confirm", missingNodes);
            confirmationCancelButton = PromptNode<Button>("confirmation-cancel", missingNodes);
            errorAcknowledgeButton = PromptNode<Button>("error-acknowledge", missingNodes);
            completionDetailsButton = PromptNode<Button>("completion-details", missingNodes);
            completionRetryButton = PromptNode<Button>("completion-retry", missingNodes);
            submissionSubmitButton = PromptNode<Button>("submission-submit", missingNodes);
            submissionName = PromptNode<TextField>("submission-name", missingNodes);
            submissionStudentNumber = PromptNode<TextField>("submission-student-number", missingNodes);
            submissionMajor = PromptNode<TextField>("submission-major", missingNodes);
            submissionClass = PromptNode<TextField>("submission-class", missingNodes);
            foreach (var field in new[] { submissionName, submissionStudentNumber, submissionMajor, submissionClass })
                if (field != null) submissionFields.Add(field);

            ConfigureVectorIcon("task-prompt-icon", HudVectorIcon.Task, missingNodes);
            ConfigureVectorIcon("countdown-prompt-icon", HudVectorIcon.Clock, missingNodes);
            ConfigureVectorIcon("confirmation-prompt-icon", HudVectorIcon.Info, missingNodes);
            ConfigureVectorIcon("error-prompt-icon", HudVectorIcon.Warning, missingNodes);
            ConfigureVectorIcon("completion-prompt-icon", HudVectorIcon.Trophy, missingNodes);
            ConfigureVectorIcon("assessment-prompt-icon", HudVectorIcon.Task, missingNodes);
            ConfigureVectorIcon("submission-prompt-icon", HudVectorIcon.Submission, missingNodes);
        }

        private T PromptNode<T>(string id, ICollection<string> missingNodes) where T : VisualElement
        {
            var element = root.Q<T>(id);
            if (element == null) missingNodes.Add(id);
            return element;
        }

        private void AddPromptView(YardCraneHudPromptKind kind, string id, ICollection<string> missingNodes)
        {
            var view = PromptNode<VisualElement>(id, missingNodes);
            if (view != null) promptViews[kind] = view;
        }

        private void BindPromptInteractions()
        {
            if (promptLayer != null)
            {
                promptLayer.pickingMode = PickingMode.Position;
                promptLayer.focusable = false;
            }
            foreach (var view in promptViews.Values)
            {
                view.pickingMode = PickingMode.Position;
                view.focusable = false;
            }
            foreach (var button in promptCloseButtons)
            {
                MakePromptButtonInteractive(button);
                if (button.name == "confirmation-prompt-close")
                    button.clicked += HandleConfirmationCancelled;
                else if (button.name == "error-prompt-close")
                    button.clicked += HandleErrorAcknowledged;
                else
                    button.clicked += ClosePrompt;
            }

            if (assessmentTableBody != null)
            {
                assessmentTableBody.pickingMode = PickingMode.Position;
                if (assessmentTableBody.verticalScroller != null)
                {
                    assessmentTableBody.verticalScroller.pickingMode = PickingMode.Position;
                    if (assessmentTableBody.verticalScroller.slider != null)
                        assessmentTableBody.verticalScroller.slider.pickingMode = PickingMode.Position;
                }
            }

            MakePromptButtonInteractive(confirmationConfirmButton);
            MakePromptButtonInteractive(confirmationCancelButton);
            MakePromptButtonInteractive(errorAcknowledgeButton);
            MakePromptButtonInteractive(completionDetailsButton);
            MakePromptButtonInteractive(completionRetryButton);
            MakePromptButtonInteractive(submissionSubmitButton);
            confirmationConfirmButton.clicked += HandleConfirmationAccepted;
            confirmationCancelButton.clicked += HandleConfirmationCancelled;
            errorAcknowledgeButton.clicked += HandleErrorAcknowledged;
            completionDetailsButton.clicked += HandleCompletionDetails;
            completionRetryButton.clicked += HandleCompletionRetry;
            submissionSubmitButton.clicked += HandleScoreSubmission;

            foreach (var field in submissionFields)
            {
                field.pickingMode = PickingMode.Position;
                var input = field.Q(className: "unity-base-text-field__input");
                if (input != null) input.pickingMode = PickingMode.Position;
                field.RegisterValueChangedCallback(HandleSubmissionFieldChanged);
            }
        }

        private static void MakePromptButtonInteractive(Button button)
        {
            if (button == null) return;
            button.pickingMode = PickingMode.Position;
            button.focusable = false;
            button.tabIndex = -1;
        }

        private void ReleasePromptInteractions()
        {
            foreach (var button in promptCloseButtons)
            {
                if (button == null) continue;
                if (button.name == "confirmation-prompt-close")
                    button.clicked -= HandleConfirmationCancelled;
                else if (button.name == "error-prompt-close")
                    button.clicked -= HandleErrorAcknowledged;
                else
                    button.clicked -= ClosePrompt;
            }
            if (confirmationConfirmButton != null) confirmationConfirmButton.clicked -= HandleConfirmationAccepted;
            if (confirmationCancelButton != null) confirmationCancelButton.clicked -= HandleConfirmationCancelled;
            if (errorAcknowledgeButton != null) errorAcknowledgeButton.clicked -= HandleErrorAcknowledged;
            if (completionDetailsButton != null) completionDetailsButton.clicked -= HandleCompletionDetails;
            if (completionRetryButton != null) completionRetryButton.clicked -= HandleCompletionRetry;
            if (submissionSubmitButton != null) submissionSubmitButton.clicked -= HandleScoreSubmission;
            foreach (var field in submissionFields)
                if (field != null) field.UnregisterValueChangedCallback(HandleSubmissionFieldChanged);

            HideAllPromptViews();
            ClearPromptCallbacks();
            pendingPrompt = null;
            promptViews.Clear();
            promptCloseButtons.Clear();
            submissionFields.Clear();
            promptLayer = null;
            taskPromptCloseButton = null;
            taskSourceGrid = null;
            taskDestinationGrid = null;
            countdownRing = null;
            assessmentTableBody = null;
            confirmationConfirmButton = null;
            confirmationCancelButton = null;
            errorAcknowledgeButton = null;
            completionDetailsButton = null;
            completionRetryButton = null;
            submissionSubmitButton = null;
            submissionName = null;
            submissionStudentNumber = null;
            submissionMajor = null;
            submissionClass = null;
        }

        public void ShowTaskPrompt(YardCraneHudTaskPrompt data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (DeferPrompt(() => ShowTaskPrompt(data))) return;
            BeginPrompt(YardCraneHudPromptKind.TaskBrief);
            SetPromptText("task-prompt-title", data.Title);
            if (taskPromptCloseButton != null)
            {
                var actionText = PromptText(data.ConfirmText, 16);
                var isExplicitAction = !string.Equals(actionText, "×", StringComparison.Ordinal) &&
                                       !string.Equals(actionText, "✕", StringComparison.Ordinal);
                taskPromptCloseButton.text = actionText;
                taskPromptCloseButton.tooltip = isExplicitAction ? actionText : "关闭任务提示";
                taskPromptCloseButton.EnableInClassList("task-confirm-action", isExplicitAction);
            }
            SetPromptText("task-source-bay", FormatBay(data.SourceBay));
            SetPromptText("task-destination-bay", FormatBay(data.DestinationBay));
            SetPromptText("task-instruction", data.Instruction, 180);
            SetPromptText("prompt-task-container", data.TargetContainer);
            SetPromptText("prompt-task-slot", data.DestinationSlot);
            BuildBayGrid(taskSourceGrid, data.SourceCellIndex);
            BuildBayGrid(taskDestinationGrid, data.DestinationCellIndex);
            NotifyPromptChanged(YardCraneHudPromptKind.TaskBrief);
        }

        public void ShowCountdownPrompt(YardCraneHudCountdownPrompt data, Action onElapsed = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (DeferPrompt(() => ShowCountdownPrompt(data, onElapsed))) return;
            BeginPrompt(YardCraneHudPromptKind.Countdown);
            SetPromptText("countdown-prompt-title", data.Title);
            countdownTotalSeconds = Math.Max(0, data.TotalSeconds);
            countdownDeadline = Time.realtimeSinceStartupAsDouble + countdownTotalSeconds;
            countdownElapsedAction = onElapsed;
            lastCountdownRemaining = int.MinValue;
            UpdatePromptCountdown(Time.realtimeSinceStartupAsDouble);
            if (ActivePromptKind == YardCraneHudPromptKind.Countdown)
                NotifyPromptChanged(YardCraneHudPromptKind.Countdown);
        }

        public void ShowConfirmationPrompt(
            YardCraneHudConfirmationPrompt data,
            Action onConfirmed = null,
            Action onCancelled = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (DeferPrompt(() => ShowConfirmationPrompt(data, onConfirmed, onCancelled))) return;
            BeginPrompt(YardCraneHudPromptKind.Confirmation);
            confirmationAcceptedAction = onConfirmed;
            confirmationCancelledAction = onCancelled;
            SetPromptText("confirmation-prompt-title", data.Title);
            SetPromptText("confirmation-message", data.Message, 240);
            confirmationConfirmButton.text = PromptText(data.ConfirmText, 16);
            confirmationCancelButton.text = PromptText(data.CancelText, 16);
            confirmationConfirmButton.SetEnabled(onConfirmed != null);
            NotifyPromptChanged(YardCraneHudPromptKind.Confirmation);
        }

        public void ShowErrorPrompt(YardCraneHudErrorPrompt data, Action onAcknowledged = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (DeferPrompt(() => ShowErrorPrompt(data, onAcknowledged))) return;
            BeginPrompt(YardCraneHudPromptKind.Error);
            errorAcknowledgedAction = onAcknowledged;
            SetPromptText("error-prompt-title", data.Title);
            SetPromptText("error-message", data.Message, 240);
            errorAcknowledgeButton.text = PromptText(data.AcknowledgeText, 16);
            NotifyPromptChanged(YardCraneHudPromptKind.Error);
        }

        public void ShowCompletionPrompt(
            YardCraneHudCompletionPrompt data,
            Action onDetails = null,
            Action onRetry = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (DeferPrompt(() => ShowCompletionPrompt(data, onDetails, onRetry))) return;
            BeginPrompt(YardCraneHudPromptKind.Completion);
            completionDetailsAction = onDetails;
            completionRetryAction = onRetry;
            SetPromptText("completion-prompt-title", data.Title);
            SetPromptText("completion-duration", FormatDuration(data.ElapsedSeconds));
            SetPromptText("completion-score", FormatScore(data.Score));
            completionDetailsButton.text = PromptText(data.DetailsText, 18);
            completionRetryButton.text = PromptText(data.RetryText, 18);
            completionDetailsButton.SetEnabled(onDetails != null);
            completionRetryButton.SetEnabled(onRetry != null);
            NotifyPromptChanged(YardCraneHudPromptKind.Completion);
        }

        public void ShowAssessmentPrompt(YardCraneHudAssessmentPrompt data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (DeferPrompt(() => ShowAssessmentPrompt(data))) return;
            BeginPrompt(YardCraneHudPromptKind.AssessmentDetails);
            SetPromptText("assessment-prompt-title", data.Title);
            SetPromptText("assessment-project", data.Project, 80);
            SetPromptText("assessment-duration", FormatDuration(data.ElapsedSeconds));
            SetPromptText("assessment-score", FormatScore(data.Score));
            BuildAssessmentRows(data.Lines);
            NotifyPromptChanged(YardCraneHudPromptKind.AssessmentDetails);
        }

        public void ShowScoreSubmissionPrompt(
            YardCraneHudScoreSubmissionPrompt data,
            Action<YardCraneHudScoreSubmission> onSubmit = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (DeferPrompt(() => ShowScoreSubmissionPrompt(data, onSubmit))) return;
            BeginPrompt(YardCraneHudPromptKind.ScoreSubmission);
            scoreSubmissionAction = onSubmit;
            submissionRequestPending = false;
            SetPromptText("submission-prompt-title", data.Title);
            SetFieldValue(submissionName, data.DisplayName);
            SetFieldValue(submissionStudentNumber, data.StudentNumber);
            SetFieldValue(submissionMajor, data.Major);
            SetFieldValue(submissionClass, data.ClassName);
            foreach (var field in submissionFields)
            {
                field.isReadOnly = !data.AllowIdentityEditing;
                field.focusable = data.AllowIdentityEditing;
                field.tabIndex = data.AllowIdentityEditing ? 0 : -1;
            }
            submissionSubmitButton.text = PromptText(data.SubmitText, 18);
            UpdateSubmissionAvailability();
            NotifyPromptChanged(YardCraneHudPromptKind.ScoreSubmission);
        }

        public void ClosePrompt()
        {
            var wasVisible = ActivePromptKind != YardCraneHudPromptKind.None;
            pendingPrompt = null;
            HideAllPromptViews();
            ClearPromptCallbacks();
            if (root != null)
            {
                RenderStatus();
                RenderLifecycle();
            }
            if (wasVisible) NotifyPromptChanged(YardCraneHudPromptKind.None);
        }

        private bool DeferPrompt(Action action)
        {
            if (IsReady) return false;
            pendingPrompt = action;
            return true;
        }

        private void FlushPendingPrompt()
        {
            var action = pendingPrompt;
            pendingPrompt = null;
            action?.Invoke();
        }

        private void BeginPrompt(YardCraneHudPromptKind kind)
        {
            if (!promptViews.TryGetValue(kind, out var view))
                throw new InvalidOperationException($"HUD prompt view is unavailable: {kind}");

            ClearPromptCallbacks();
            HideAllPromptViews();
            ActivePromptKind = kind;
            promptLayer.style.display = DisplayStyle.Flex;
            view.style.display = DisplayStyle.Flex;
            root.Q("operator-hud").EnableInClassList("prompt-open", true);
            if (resultCard != null) resultCard.style.display = DisplayStyle.None;
            // Prompts overlay the live driving HUD. The base HUD and any current safety alert
            // remain visible underneath; only prompt views and the terminal result are exclusive.
            SetGuideVisible(false);
        }

        private void HideAllPromptViews()
        {
            foreach (var view in promptViews.Values)
                if (view != null) view.style.display = DisplayStyle.None;
            if (promptLayer != null) promptLayer.style.display = DisplayStyle.None;
            if (root != null)
            {
                var hudRoot = root.Q("operator-hud");
                hudRoot?.EnableInClassList("prompt-open", false);
            }
            ActivePromptKind = YardCraneHudPromptKind.None;
            countdownDeadline = -1;
            lastCountdownRemaining = int.MinValue;
            if (countdownRing != null) countdownRing.EnableInClassList("countdown-urgent", false);
        }

        private void ClearPromptCallbacks()
        {
            countdownElapsedAction = null;
            confirmationAcceptedAction = null;
            confirmationCancelledAction = null;
            errorAcknowledgedAction = null;
            completionDetailsAction = null;
            completionRetryAction = null;
            scoreSubmissionAction = null;
            submissionRequestPending = false;
        }

        private void NotifyPromptChanged(YardCraneHudPromptKind kind)
        {
            try { PromptChanged?.Invoke(kind); }
            catch (Exception error) { Debug.LogException(error, this); }
        }

        private void UpdatePromptCountdown(double now)
        {
            if (ActivePromptKind != YardCraneHudPromptKind.Countdown || countdownDeadline < 0) return;
            var remaining = Math.Max(0, (int)Math.Ceiling(countdownDeadline - now));
            if (remaining != lastCountdownRemaining)
            {
                lastCountdownRemaining = remaining;
                SetPromptText("countdown-value", remaining.ToString());
                countdownRing?.EnableInClassList("countdown-urgent", remaining <= 3);
            }
            if (now < countdownDeadline) return;

            var completed = countdownElapsedAction;
            ClosePrompt();
            InvokePromptAction("countdown elapsed", completed);
        }

        private void HandleConfirmationAccepted()
        {
            var action = confirmationAcceptedAction;
            ClosePrompt();
            InvokePromptAction("confirmation accepted", action);
        }

        private void HandleConfirmationCancelled()
        {
            var action = confirmationCancelledAction;
            ClosePrompt();
            InvokePromptAction("confirmation cancelled", action);
        }

        private void HandleErrorAcknowledged()
        {
            var action = errorAcknowledgedAction;
            ClosePrompt();
            InvokePromptAction("error acknowledged", action);
        }

        private void HandleCompletionDetails()
        {
            var action = completionDetailsAction;
            ClosePrompt();
            InvokePromptAction("completion details requested", action);
        }

        private void HandleCompletionRetry()
        {
            var action = completionRetryAction;
            ClosePrompt();
            InvokePromptAction("completion retry requested", action);
        }

        private void HandleScoreSubmission()
        {
            if (submissionRequestPending || scoreSubmissionAction == null) return;
            var submission = new YardCraneHudScoreSubmission(
                submissionName.value,
                submissionStudentNumber.value,
                submissionMajor.value,
                submissionClass.value);
            submissionRequestPending = true;
            submissionSubmitButton.SetEnabled(false);
            SetPromptText("submission-note", "提交请求已发出，等待教学平台回执");
            try { scoreSubmissionAction(submission); }
            catch (Exception error)
            {
                submissionRequestPending = false;
                SetPromptText("submission-note", "提交请求未能发出，请稍后重试");
                UpdateSubmissionAvailability();
                Debug.LogException(error, this);
            }
        }

        private void HandleSubmissionFieldChanged(ChangeEvent<string> _)
        {
            if (!submissionRequestPending) UpdateSubmissionAvailability();
        }

        private void UpdateSubmissionAvailability()
        {
            if (submissionSubmitButton == null) return;
            var hasIdentity = !string.IsNullOrWhiteSpace(submissionName?.value) &&
                              !string.IsNullOrWhiteSpace(submissionStudentNumber?.value);
            var hasExecutor = scoreSubmissionAction != null;
            submissionSubmitButton.SetEnabled(hasIdentity && hasExecutor && !submissionRequestPending);
            SetPromptText("submission-note",
                !hasExecutor
                    ? "成绩提交服务尚未接入"
                    : !hasIdentity
                        ? "姓名与学号缺失，暂不能提交"
                        : "本界面仅发出提交请求，正式成绩由教学平台确认");
        }

        private void BuildBayGrid(VisualElement grid, int highlightedCell)
        {
            grid.Clear();
            var cellCount = BayGridColumns * BayGridRows;
            for (var index = 0; index < cellCount; index++)
            {
                var cell = new VisualElement { pickingMode = PickingMode.Ignore };
                cell.AddToClassList("bay-cell");
                if (index == highlightedCell) cell.AddToClassList("target-cell");
                grid.Add(cell);
            }
        }

        private void BuildAssessmentRows(IReadOnlyList<YardCraneHudAssessmentLine> lines)
        {
            assessmentTableBody.Clear();
            if (lines == null || lines.Count == 0)
            {
                var empty = new Label("暂无扣分记录") { pickingMode = PickingMode.Ignore, enableRichText = false };
                empty.AddToClassList("assessment-empty");
                assessmentTableBody.Add(empty);
                return;
            }

            var displayed = Math.Min(lines.Count, 50);
            for (var index = 0; index < displayed; index++)
            {
                var line = lines[index];
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.AddToClassList("assessment-row");
                row.Add(AssessmentCell(PromptText(line.Item, 80), "assessment-item-cell"));
                row.Add(AssessmentCell(line.PointsDeducted.ToString(), "assessment-points-cell"));
                row.Add(AssessmentCell(PromptText(line.Note, 160), "assessment-note-cell"));
                assessmentTableBody.Add(row);
            }
        }

        private static Label AssessmentCell(string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore, enableRichText = false };
            label.AddToClassList("assessment-cell");
            label.AddToClassList(className);
            return label;
        }

        private void SetPromptText(string id, string value, int maxLength = 64)
        {
            if (labels.TryGetValue(id, out var label)) label.text = PromptText(value, maxLength);
        }

        private static string PromptText(string value, int maxLength = 64)
        {
            if (string.IsNullOrWhiteSpace(value)) return "—";
            var trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : trimmed.Substring(0, maxLength) + "…";
        }

        private static string FormatBay(string value)
        {
            var text = PromptText(value, 20);
            if (text == "—" || text.StartsWith("Bay", StringComparison.OrdinalIgnoreCase)) return text;
            return "Bay " + text;
        }

        private static string FormatDuration(int? seconds)
        {
            if (!seconds.HasValue || seconds.Value < 0) return "—";
            var duration = TimeSpan.FromSeconds(seconds.Value);
            if (duration.TotalHours >= 1) return duration.ToString(@"h\:mm\:ss");
            if (duration.Seconds == 0) return $"{(int)duration.TotalMinutes} min";
            return duration.ToString(@"m\:ss");
        }

        private static string FormatScore(int? score)
        {
            if (!score.HasValue) return "—";
            return Math.Max(0, Math.Min(100, score.Value)).ToString();
        }

        private static void SetFieldValue(TextField field, string value)
        {
            field.SetValueWithoutNotify(string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim());
        }

        private void InvokePromptAction(string description, Action action)
        {
            if (action == null) return;
            try { action(); }
            catch (Exception error)
            {
                Debug.LogError($"[YC_UI_PROMPT_ACTION_FAILED] action={description}", this);
                Debug.LogException(error, this);
            }
        }
    }
}
