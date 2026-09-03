using System;
using System.Collections.Generic;
using PortTraining.Control.EmbeddedGateway;
using PortTraining.Control.Gateway;
using PortTraining.Core.Assessment;
using PortTraining.Core.Bootstrap;
using PortTraining.Core.Identity;
using PortTraining.Core.Progress;
using PortTraining.Core.Records;
using PortTraining.Core.Station;
using PortTraining.Core.WorkOrders;
using UnityEngine;
using UnityEngine.UIElements;

namespace PortTraining.UI.Runtime
{
    [DefaultExecutionOrder(-800)]
    [RequireComponent(typeof(UIDocument))]
    public sealed class StudentShellController : MonoBehaviour
    {
        private const float DiagnosticsRefreshInterval = 0.25f;
        private static readonly bool AssessmentCenterEnabled = false;

        private enum TaskCenterFilter
        {
            All,
            Formal,
            Collaborative,
            FaultTraining,
            Practice,
            Assessment
        }

        private enum TrainingRecordsFilter
        {
            All,
            Formal,
            Collaborative,
            FaultTraining,
            Practice
        }

        private readonly LocalStudentSessionService sessionService = new LocalStudentSessionService();
        private readonly LocalStudentProgressService progressService = new LocalStudentProgressService();
        private readonly LocalAssessmentService assessmentService = new LocalAssessmentService();
        private readonly LocalTrainingRecordService trainingRecordService = new LocalTrainingRecordService();
        private readonly LocalWorkOrderRepository workOrderRepository = new LocalWorkOrderRepository();

        private VisualElement root;
        private VisualElement loginPage;
        private VisualElement workbenchPage;
        private TextField studentIdField;
        private TextField passwordField;
        private Label loginError;
        private Button loginButton;
        private Button enterOperationButton;
        private Label toast;
        private EmbeddedGatewayClient gateway;
        private StationProfile station;
        private StudentSession currentSession;
        private float nextDiagnosticsRefresh;
        private float hideToastAt;
        private WorkOrderSnapshot currentWorkOrder;
        private WorkOrderStateMachine workOrderState;
        private IReadOnlyList<WorkOrderSnapshot> cachedStationWorkOrders = Array.Empty<WorkOrderSnapshot>();
        private WorkOrderSnapshot selectedTaskCenterOrder;
        private TaskCenterFilter activeTaskCenterFilter;
        private VisualElement taskList;
        private Button taskSelectButton;
        private IReadOnlyList<WorkOrderSnapshot> cachedAssessmentOrders = Array.Empty<WorkOrderSnapshot>();
        private WorkOrderSnapshot selectedAssessmentOrder;
        private VisualElement assessmentList;
        private Button assessmentStartButton;
        private IReadOnlyList<TrainingRecordSnapshot> cachedTrainingRecords = Array.Empty<TrainingRecordSnapshot>();
        private TrainingRecordSnapshot selectedTrainingRecord;
        private TrainingRecordsFilter activeRecordsFilter;
        private VisualElement recordsList;

        public event Action PracticeOperationRequested;
        public event Action<WorkOrderSnapshot> WorkOrderOperationRequested;

        public void ReportCurrentAssessmentCompleted(
            int durationSeconds,
            float score,
            int evaluatedOperations,
            int safeOperations)
        {
            if (currentSession == null || currentWorkOrder?.TaskType != WorkOrderTaskType.Assessment)
            {
                Debug.LogWarning("[PortTraining] Assessment result ignored because no assessment is active.");
                return;
            }

            var completed = assessmentService.CompleteAttempt(
                currentSession.studentId,
                currentWorkOrder.workOrderId,
                durationSeconds,
                score,
                currentWorkOrder.passingScore);
            if (completed == null)
            {
                Debug.LogWarning("[PortTraining] Assessment result ignored because no running attempt was found.");
                return;
            }

            progressService.RecordTrainingResult(
                currentSession.studentId,
                true,
                durationSeconds,
                score,
                evaluatedOperations,
                safeOperations);
            AppendCurrentTrainingRecord(
                true,
                durationSeconds,
                true,
                score,
                evaluatedOperations,
                safeOperations,
                0f,
                0f,
                0f,
                0f,
                Array.Empty<TrainingIssueSnapshot>(),
                string.Empty);
            if (workOrderState?.State == WorkOrderState.Running)
            {
                workOrderState.TransitionTo(WorkOrderState.Completed);
                workOrderState.TransitionTo(WorkOrderState.Scored);
            }
            RefreshStudentPortrait();
            RefreshAssessmentCenterData();
        }

        public void ReportCurrentAssessmentTerminated(
            string reason,
            int durationSeconds,
            int evaluatedOperations,
            int safeOperations)
        {
            if (currentSession == null || currentWorkOrder?.TaskType != WorkOrderTaskType.Assessment)
            {
                return;
            }

            var terminated = assessmentService.TerminateAttempt(currentSession.studentId, currentWorkOrder.workOrderId, reason);
            if (terminated == null)
            {
                return;
            }

            progressService.RecordTrainingResult(
                currentSession.studentId,
                false,
                durationSeconds,
                null,
                evaluatedOperations,
                safeOperations);
            AppendCurrentTrainingRecord(
                false,
                durationSeconds,
                false,
                0f,
                evaluatedOperations,
                safeOperations,
                0f,
                0f,
                0f,
                0f,
                new[]
                {
                    new TrainingIssueSnapshot
                    {
                        category = "考核终止",
                        description = string.IsNullOrWhiteSpace(reason) ? "训练未完成" : reason,
                        severity = 2
                    }
                },
                string.Empty);
            if (workOrderState?.State == WorkOrderState.Running)
            {
                workOrderState.TransitionTo(WorkOrderState.Terminated);
            }
            RefreshStudentPortrait();
            RefreshAssessmentCenterData();
        }

        public void ReportCurrentTrainingCompleted(
            int durationSeconds,
            bool completed,
            bool hasScore,
            float score,
            int evaluatedOperations,
            int safeOperations,
            float positioningScore,
            float efficiencyScore,
            float processScore,
            float collaborationScore,
            TrainingIssueSnapshot[] issues,
            string teacherComment)
        {
            if (currentSession == null || currentWorkOrder == null || currentWorkOrder.TaskType == WorkOrderTaskType.Assessment)
            {
                Debug.LogWarning("[PortTraining] Training result ignored because no regular training task is active.");
                return;
            }

            progressService.RecordTrainingResult(
                currentSession.studentId,
                completed,
                durationSeconds,
                hasScore ? score : null,
                evaluatedOperations,
                safeOperations);
            AppendCurrentTrainingRecord(
                completed,
                durationSeconds,
                hasScore,
                score,
                evaluatedOperations,
                safeOperations,
                positioningScore,
                efficiencyScore,
                processScore,
                collaborationScore,
                issues,
                teacherComment);
            if (workOrderState?.State == WorkOrderState.Running)
            {
                workOrderState.TransitionTo(completed ? WorkOrderState.Completed : WorkOrderState.Terminated);
            }
            RefreshStudentPortrait();
            RefreshTrainingRecords();
        }

        private void Start()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            station = PortTrainingBootstrap.CurrentStation;
            gateway = FindAnyObjectByType<EmbeddedGatewayClient>();

            CacheElements();
            BindActions();
            PopulateStationIdentity();
            PrefillLastStudent();
            ShowLogin();
            RefreshDiagnostics();
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextDiagnosticsRefresh)
            {
                RefreshDiagnostics();
                nextDiagnosticsRefresh = Time.unscaledTime + DiagnosticsRefreshInterval;
            }

            if (hideToastAt > 0f && Time.unscaledTime >= hideToastAt)
            {
                toast.AddToClassList("toast--hidden");
                hideToastAt = 0f;
            }

            var clock = root?.Q<Label>("clock-label");
            if (clock != null)
            {
                clock.text = DateTime.Now.ToString("HH:mm");
            }
        }

        private void CacheElements()
        {
            loginPage = root.Q<VisualElement>("login-page");
            workbenchPage = root.Q<VisualElement>("workbench-page");
            studentIdField = root.Q<TextField>("student-id");
            passwordField = root.Q<TextField>("password");
            loginError = root.Q<Label>("login-error");
            loginButton = root.Q<Button>("login-button");
            enterOperationButton = root.Q<Button>("enter-operation-button");
            toast = root.Q<Label>("toast");
            taskList = root.Q<VisualElement>("task-list");
            taskSelectButton = root.Q<Button>("task-select-button");
            assessmentList = root.Q<VisualElement>("assessment-list");
            assessmentStartButton = root.Q<Button>("assessment-start-button");
            recordsList = root.Q<VisualElement>("records-list");
        }

        private void BindActions()
        {
            loginButton.clicked += Login;
            root.Q<Button>("logout-button").clicked += ShowLogin;
            root.Q<Button>("free-training-button").clicked += CreateFreeTraining;
            enterOperationButton.clicked += EnterOperation;
            passwordField.RegisterCallback<KeyDownEvent>(OnPasswordKeyDown);

            RegisterModuleNavigation("nav-workbench", "home-view");
            RegisterModuleNavigation("nav-tasks", "task-center-view");
            RegisterModuleNavigation("nav-records", "training-records-view");
            var assessmentNavigation = root.Q<Button>("nav-assessment");
            assessmentNavigation.SetEnabled(AssessmentCenterEnabled);
            assessmentNavigation.tooltip = "一期暂未开放";
            root.Q<Button>("task-filter-assessment").SetEnabled(AssessmentCenterEnabled);
            root.Q<Button>("task-refresh-button").clicked += RefreshTaskCenterData;
            root.Q<Button>("task-center-free-training").clicked += CreateFreeTraining;
            taskSelectButton.clicked += SetSelectedTaskAsCurrent;
            root.Q<Button>("task-filter-all").clicked += () => SetTaskCenterFilter(TaskCenterFilter.All, "task-filter-all");
            root.Q<Button>("task-filter-formal").clicked += () => SetTaskCenterFilter(TaskCenterFilter.Formal, "task-filter-formal");
            root.Q<Button>("task-filter-collaborative").clicked += () => SetTaskCenterFilter(TaskCenterFilter.Collaborative, "task-filter-collaborative");
            root.Q<Button>("task-filter-fault").clicked += () => SetTaskCenterFilter(TaskCenterFilter.FaultTraining, "task-filter-fault");
            root.Q<Button>("task-filter-practice").clicked += () => SetTaskCenterFilter(TaskCenterFilter.Practice, "task-filter-practice");
            root.Q<Button>("task-filter-assessment").clicked += () => SetTaskCenterFilter(TaskCenterFilter.Assessment, "task-filter-assessment");
            root.Q<Button>("assessment-refresh-button").clicked += RefreshAssessmentCenterData;
            assessmentStartButton.clicked += StartSelectedAssessment;
            root.Q<Button>("records-refresh-button").clicked += RefreshTrainingRecords;
            root.Q<Button>("records-filter-all").clicked += () => SetTrainingRecordsFilter(TrainingRecordsFilter.All, "records-filter-all");
            root.Q<Button>("records-filter-formal").clicked += () => SetTrainingRecordsFilter(TrainingRecordsFilter.Formal, "records-filter-formal");
            root.Q<Button>("records-filter-collaborative").clicked += () => SetTrainingRecordsFilter(TrainingRecordsFilter.Collaborative, "records-filter-collaborative");
            root.Q<Button>("records-filter-fault").clicked += () => SetTrainingRecordsFilter(TrainingRecordsFilter.FaultTraining, "records-filter-fault");
            root.Q<Button>("records-filter-practice").clicked += () => SetTrainingRecordsFilter(TrainingRecordsFilter.Practice, "records-filter-practice");
        }

        private void RegisterModuleNavigation(string buttonName, string viewName)
        {
            var button = root.Q<Button>(buttonName);
            var view = root.Q<VisualElement>(viewName);
            if (button == null || view == null)
            {
                Debug.LogWarning($"[PortTraining] Module navigation is incomplete: {buttonName} -> {viewName}.");
                return;
            }

            button.clicked += () => ShowModule(viewName, buttonName);
        }

        private void ShowModule(string viewName, string activeButtonName)
        {
            root.Query<VisualElement>(className: "module-view").ForEach(view => view.AddToClassList("page--hidden"));
            root.Query<Button>(className: "nav-button").ForEach(button => button.RemoveFromClassList("nav-button--active"));

            var target = root.Q<VisualElement>(viewName);
            var activeButton = root.Q<Button>(activeButtonName);
            target?.RemoveFromClassList("page--hidden");
            activeButton?.AddToClassList("nav-button--active");

            if (viewName == "task-center-view")
            {
                RefreshTaskCenterData();
            }
            else if (viewName == "assessment-center-view")
            {
                RefreshAssessmentCenterData();
            }
            else if (viewName == "training-records-view")
            {
                RefreshTrainingRecords();
            }
        }

        private void PopulateStationIdentity()
        {
            var workbenchStation = root.Q<Label>("workbench-station");
            if (station == null)
            {
                workbenchStation.text = "未识别工位";
                loginError.text = "工位配置异常，请联系管理员";
                loginButton.SetEnabled(false);
                return;
            }

            workbenchStation.text = $"{station.DisplayName} · {station.StationId}";
            root.Q<Label>("task-source").text = station.StationId;
            root.Q<Label>("health-pointmap").text = $"点表版本 · {station.PointMapVersion}";
        }

        private void PrefillLastStudent()
        {
            var cached = sessionService.LoadLastSession();
            if (cached != null)
            {
                studentIdField.value = cached.studentId;
            }
        }

        private void OnPasswordKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                Login();
            }
        }

        private void Login()
        {
            var result = sessionService.Login(studentIdField.value, passwordField.value, station);
            if (!result.Success)
            {
                loginError.text = result.Error;
                return;
            }

            currentSession = result.Session;
            loginError.text = string.Empty;
            passwordField.value = string.Empty;
            PopulateStudentSession();
            ShowWorkbench();
            ShowToast("已通过本机离线身份校验");
        }

        private void PopulateStudentSession()
        {
            root.Q<Label>("student-name").text = currentSession.studentName;
            root.Q<Label>("student-role").text = currentSession.role == StudentRole.QC_DRIVER ? "岸桥司机" : "场桥司机";
            root.Q<Label>("workspace-greeting").text = $"{currentSession.studentId}，本工位已自动绑定{(currentSession.role == StudentRole.QC_DRIVER ? "岸桥" : "场桥")}司机岗位";
            RefreshStudentPortrait();
        }

        private void ShowLogin()
        {
            currentSession = null;
            currentWorkOrder = null;
            workOrderState = null;
            loginPage.RemoveFromClassList("page--hidden");
            workbenchPage.AddToClassList("page--hidden");
        }

        private void ShowWorkbench()
        {
            loginPage.AddToClassList("page--hidden");
            workbenchPage.RemoveFromClassList("page--hidden");
            ShowModule("home-view", "nav-workbench");
            LoadCurrentWorkOrder();
        }

        private void LoadCurrentWorkOrder()
        {
            currentWorkOrder = null;
            workOrderState = null;

            cachedStationWorkOrders = workOrderRepository.LoadForStation(station);
            PopulateDashboardOverview(cachedStationWorkOrders);

            foreach (var candidate in cachedStationWorkOrders)
            {
                if (!AssessmentCenterEnabled && candidate.TaskType == WorkOrderTaskType.Assessment)
                {
                    continue;
                }
                currentWorkOrder = candidate;
                break;
            }

            selectedTaskCenterOrder = currentWorkOrder;
            RebuildTaskCenter();

            if (currentWorkOrder == null)
            {
                root.Q<Label>("task-status").text = "暂无工单";
                root.Q<Label>("task-title").text = "当前没有待执行工单";
                root.Q<Label>("task-detail").text = "可进入自由练习；联网后新工单会自动同步到任务中心。";
                root.Q<Label>("task-source").text = station?.StationId ?? "本机工位";
                root.Q<Label>("task-target").text = "等待调度";
                enterOperationButton.text = "暂无可执行任务";
                enterOperationButton.SetEnabled(false);
                RefreshCollaborationCard(null);
                return;
            }

            workOrderState = new WorkOrderStateMachine();
            PrepareWorkOrderForDirectEntry();
            RefreshWorkOrderCard();
        }

        private void RefreshTaskCenterData()
        {
            var selectedId = selectedTaskCenterOrder?.workOrderId;
            cachedStationWorkOrders = workOrderRepository.LoadForStation(station);
            PopulateDashboardOverview(cachedStationWorkOrders);

            selectedTaskCenterOrder = null;
            foreach (var workOrder in cachedStationWorkOrders)
            {
                if ((!string.IsNullOrWhiteSpace(selectedId) && workOrder.workOrderId == selectedId) ||
                    (selectedTaskCenterOrder == null && currentWorkOrder?.workOrderId == workOrder.workOrderId))
                {
                    selectedTaskCenterOrder = workOrder;
                    break;
                }
            }

            RebuildTaskCenter();
        }

        private void SetTaskCenterFilter(TaskCenterFilter filter, string activeButtonName)
        {
            activeTaskCenterFilter = filter;
            foreach (var name in new[] { "task-filter-all", "task-filter-formal", "task-filter-collaborative", "task-filter-fault", "task-filter-practice", "task-filter-assessment" })
            {
                root.Q<Button>(name)?.EnableInClassList("task-filter--active", name == activeButtonName);
            }
            RebuildTaskCenter();
        }

        private void RebuildTaskCenter()
        {
            if (taskList == null)
            {
                return;
            }

            taskList.Clear();
            var visibleCount = 0;
            WorkOrderSnapshot firstVisible = null;

            foreach (var workOrder in cachedStationWorkOrders)
            {
                if (!MatchesTaskCenterFilter(workOrder))
                {
                    continue;
                }

                firstVisible ??= workOrder;
                visibleCount++;
                taskList.Add(CreateTaskListItem(workOrder));
            }

            root.Q<Label>("task-center-count").text = $"{visibleCount} 个可执行工单";

            if (visibleCount == 0)
            {
                var empty = new Label("当前筛选条件下没有工单");
                empty.AddToClassList("task-list-empty");
                taskList.Add(empty);
                selectedTaskCenterOrder = null;
            }
            else if (selectedTaskCenterOrder == null || !MatchesTaskCenterFilter(selectedTaskCenterOrder))
            {
                selectedTaskCenterOrder = firstVisible;
            }

            UpdateTaskCenterDetail();
        }

        private VisualElement CreateTaskListItem(WorkOrderSnapshot workOrder)
        {
            var item = new VisualElement();
            item.AddToClassList("task-list-item");
            item.EnableInClassList("task-list-item--selected", selectedTaskCenterOrder?.workOrderId == workOrder.workOrderId);

            var main = new VisualElement();
            main.AddToClassList("task-list-item__main");
            var topline = new VisualElement();
            topline.AddToClassList("task-list-item__topline");
            var operation = new Label($"{OperationDisplayName(workOrder)} · {workOrder.workflowName}");
            operation.AddToClassList("task-list-item__operation");
            var number = new Label(workOrder.orderNumber);
            number.AddToClassList("task-list-item__number");
            topline.Add(operation);
            topline.Add(number);

            var route = new Label($"{workOrder.sourceLocation} → {workOrder.targetLocation}");
            route.AddToClassList("task-list-item__route");
            var assignment = workOrder.AssignmentType == WorkOrderAssignmentType.Collaborative ? "联合工单" : "单机任务";
            var meta = new Label($"{TaskTypeDisplayName(workOrder)} · {SourceDisplayName(workOrder)}下发 · {assignment} · 箱号 {Fallback(workOrder.containerId, "--")}");
            meta.AddToClassList("task-list-item__meta");
            main.Add(topline);
            main.Add(route);
            main.Add(meta);

            var captured = workOrder;
            var detailButton = new Button(() => SelectTaskCenterOrder(captured)) { text = "查看" };
            detailButton.AddToClassList("task-list-item__button");
            item.Add(main);
            item.Add(detailButton);
            return item;
        }

        private void SelectTaskCenterOrder(WorkOrderSnapshot workOrder)
        {
            selectedTaskCenterOrder = workOrder;
            RebuildTaskCenter();
        }

        private void UpdateTaskCenterDetail()
        {
            var selected = selectedTaskCenterOrder;
            taskSelectButton.SetEnabled(selected != null);

            if (selected == null)
            {
                root.Q<Label>("task-detail-status").text = "未选择";
                root.Q<Label>("task-detail-title").text = "当前没有可查看的工单";
                root.Q<Label>("task-detail-number").text = "--";
                foreach (var name in new[] { "task-detail-route", "task-detail-container", "task-detail-source", "task-detail-mode", "task-detail-vehicle", "task-detail-time" })
                {
                    root.Q<Label>(name).text = "--";
                }
                return;
            }

            var isCurrent = currentWorkOrder?.workOrderId == selected.workOrderId;
            root.Q<Label>("task-detail-status").text = isCurrent ? "当前任务" : "待执行";
            root.Q<Label>("task-detail-title").text = $"{OperationDisplayName(selected)} · {selected.workflowName}";
            root.Q<Label>("task-detail-number").text = selected.orderNumber;
            root.Q<Label>("task-detail-route").text = $"{selected.sourceLocation} → {selected.targetLocation}";
            root.Q<Label>("task-detail-container").text = $"{selected.containerId} · {selected.containerIsoCode} · {selected.containerWeightTonnes:0.0} t";
            root.Q<Label>("task-detail-source").text = SourceDisplayName(selected);
            var assignment = selected.AssignmentType == WorkOrderAssignmentType.Collaborative ? "联合工单" : "单机任务";
            root.Q<Label>("task-detail-mode").text = $"{TaskTypeDisplayName(selected)} · {assignment}";
            root.Q<Label>("task-detail-vehicle").text = $"{Fallback(selected.vehicleId, "未分配")} / {Fallback(selected.laneId, "未分配")}";
            root.Q<Label>("task-detail-time").text = Fallback(selected.dueAt, "未设置截止时间");
            taskSelectButton.text = "进入任务";
            taskSelectButton.SetEnabled(true);
        }

        private bool MatchesTaskCenterFilter(WorkOrderSnapshot workOrder)
        {
            if (!AssessmentCenterEnabled && workOrder.TaskType == WorkOrderTaskType.Assessment)
            {
                return false;
            }

            return activeTaskCenterFilter switch
            {
                TaskCenterFilter.Formal => workOrder.TaskType == WorkOrderTaskType.Formal,
                TaskCenterFilter.Collaborative => workOrder.AssignmentType == WorkOrderAssignmentType.Collaborative,
                TaskCenterFilter.FaultTraining => workOrder.TaskType == WorkOrderTaskType.FaultTraining,
                TaskCenterFilter.Practice => workOrder.TaskType == WorkOrderTaskType.FreePractice,
                TaskCenterFilter.Assessment => workOrder.TaskType == WorkOrderTaskType.Assessment,
                _ => true
            };
        }

        private void SetSelectedTaskAsCurrent()
        {
            if (selectedTaskCenterOrder == null)
            {
                ShowToast("请先选择一条工单");
                return;
            }

            if (selectedTaskCenterOrder.TaskType == WorkOrderTaskType.Assessment)
            {
                if (!AssessmentCenterEnabled)
                {
                    ShowToast("考核中心将在后续阶段开放");
                    return;
                }
                selectedAssessmentOrder = selectedTaskCenterOrder;
                if (CanOperate(gateway?.Latest))
                {
                    StartSelectedAssessment();
                }
                else
                {
                    ShowModule("assessment-center-view", "nav-assessment");
                    ShowToast("考核项目已选中，请先完成设备检查");
                }
                return;
            }

            currentWorkOrder = selectedTaskCenterOrder;
            workOrderState = new WorkOrderStateMachine();
            PrepareWorkOrderForDirectEntry();
            RefreshWorkOrderCard();

            if (workOrderState.State == WorkOrderState.Ready && CanOperate(gateway?.Latest))
            {
                EnterOperation();
                return;
            }

            ShowModule("home-view", "nav-workbench");
            ShowToast("任务已绑定，设备满足安全条件后即可直接进入驾驶室");
        }

        private void RefreshAssessmentCenterData()
        {
            var selectedId = selectedAssessmentOrder?.workOrderId;
            var available = workOrderRepository.LoadForStation(station);
            var assessments = new List<WorkOrderSnapshot>();
            foreach (var workOrder in available)
            {
                if (workOrder.TaskType == WorkOrderTaskType.Assessment)
                {
                    assessments.Add(workOrder);
                }
            }

            cachedAssessmentOrders = assessments;
            selectedAssessmentOrder = null;
            foreach (var assessment in cachedAssessmentOrders)
            {
                if (!string.IsNullOrWhiteSpace(selectedId) && assessment.workOrderId == selectedId)
                {
                    selectedAssessmentOrder = assessment;
                    break;
                }
            }

            if (selectedAssessmentOrder == null && cachedAssessmentOrders.Count > 0)
            {
                selectedAssessmentOrder = cachedAssessmentOrders[0];
            }

            RebuildAssessmentCenter();
        }

        private void RebuildAssessmentCenter()
        {
            if (assessmentList == null)
            {
                return;
            }

            assessmentList.Clear();
            var attempts = assessmentService.LoadAttempts(currentSession?.studentId);
            var pendingCount = 0;
            var runningCount = 0;
            var completedCount = 0;
            var passedCount = 0;
            AssessmentAttemptSnapshot latestCompleted = null;

            foreach (var assessment in cachedAssessmentOrders)
            {
                var latest = FindLatestAttempt(attempts, assessment.workOrderId);
                switch (latest?.State ?? AssessmentAttemptState.NotStarted)
                {
                    case AssessmentAttemptState.InProgress:
                        runningCount++;
                        break;
                    case AssessmentAttemptState.Completed:
                        completedCount++;
                        if (latest.passed)
                        {
                            passedCount++;
                        }
                        if (latestCompleted == null || string.CompareOrdinal(latest.completedAt, latestCompleted.completedAt) > 0)
                        {
                            latestCompleted = latest;
                        }
                        break;
                    default:
                        pendingCount++;
                        break;
                }

                assessmentList.Add(CreateAssessmentListItem(assessment, latest));
            }

            if (cachedAssessmentOrders.Count == 0)
            {
                var empty = new Label("当前岗位暂无待考项目");
                empty.AddToClassList("assessment-list-empty");
                assessmentList.Add(empty);
                selectedAssessmentOrder = null;
            }

            root.Q<Label>("assessment-center-count").text = $"{pendingCount} 项待考";
            root.Q<Label>("assessment-pending-count").text = pendingCount.ToString();
            root.Q<Label>("assessment-running-count").text = runningCount.ToString();
            root.Q<Label>("assessment-completed-count").text = completedCount.ToString();
            root.Q<Label>("assessment-pass-meta").text = completedCount > 0
                ? $"通过 {passedCount} / {completedCount} 项"
                : "暂无考核记录";
            root.Q<Label>("assessment-latest-score").text = latestCompleted != null ? $"{latestCompleted.score:0.0}" : "--";
            root.Q<Label>("assessment-latest-meta").text = latestCompleted != null
                ? (latestCompleted.passed ? "最近一次 · 已通过" : "最近一次 · 未通过")
                : "完成考核后生成";

            UpdateAssessmentDetail(attempts);
        }

        private VisualElement CreateAssessmentListItem(WorkOrderSnapshot assessment, AssessmentAttemptSnapshot attempt)
        {
            var item = new VisualElement();
            item.AddToClassList("assessment-list-item");
            item.EnableInClassList("assessment-list-item--selected", selectedAssessmentOrder?.workOrderId == assessment.workOrderId);

            var accent = new VisualElement();
            accent.AddToClassList("assessment-list-item__accent");
            item.Add(accent);

            var main = new VisualElement();
            main.AddToClassList("assessment-list-item__main");
            var topline = new VisualElement();
            topline.AddToClassList("assessment-list-item__topline");
            var title = new Label(assessment.workflowName);
            title.AddToClassList("assessment-list-item__title");
            var number = new Label(assessment.orderNumber);
            number.AddToClassList("assessment-list-item__number");
            topline.Add(title);
            topline.Add(number);
            main.Add(topline);

            var route = new Label($"{assessment.sourceLocation} → {assessment.targetLocation}");
            route.AddToClassList("assessment-list-item__route");
            main.Add(route);
            var meta = new Label($"{assessment.assessmentDurationMinutes} 分钟 · 合格线 {assessment.passingScore:0} 分 · {ControlModeDisplayName(assessment.controlMode)}");
            meta.AddToClassList("assessment-list-item__meta");
            main.Add(meta);
            item.Add(main);

            var status = new Label(AssessmentStatusDisplayName(attempt));
            status.AddToClassList("assessment-list-item__status");
            status.EnableInClassList("assessment-list-item__status--running", attempt?.State == AssessmentAttemptState.InProgress);
            status.EnableInClassList("assessment-list-item__status--completed", attempt?.State == AssessmentAttemptState.Completed);
            item.Add(status);

            var captured = assessment;
            item.RegisterCallback<ClickEvent>(_ => SelectAssessment(captured));
            return item;
        }

        private void SelectAssessment(WorkOrderSnapshot assessment)
        {
            selectedAssessmentOrder = assessment;
            RebuildAssessmentCenter();
        }

        private void UpdateAssessmentDetail(IReadOnlyList<AssessmentAttemptSnapshot> attempts)
        {
            var selected = selectedAssessmentOrder;
            var names = new[]
            {
                "assessment-detail-role", "assessment-detail-control", "assessment-detail-duration",
                "assessment-detail-pass", "assessment-detail-attempt", "assessment-detail-due",
                "assessment-detail-route", "assessment-detail-container"
            };

            if (selected == null)
            {
                root.Q<Label>("assessment-detail-status").text = "未选择";
                root.Q<Label>("assessment-detail-title").text = "当前没有可执行的考核";
                root.Q<Label>("assessment-detail-number").text = "--";
                foreach (var name in names)
                {
                    root.Q<Label>(name).text = "--";
                }
                UpdateAssessmentReadiness(attempts);
                return;
            }

            var latest = FindLatestAttempt(attempts, selected.workOrderId);
            var attemptCount = CountAttempts(attempts, selected.workOrderId);
            var status = root.Q<Label>("assessment-detail-status");
            status.text = AssessmentStatusDisplayName(latest);
            status.EnableInClassList("assessment-status--running", latest?.State == AssessmentAttemptState.InProgress);
            status.EnableInClassList("assessment-status--ready", latest?.State == AssessmentAttemptState.Completed && latest.passed);

            root.Q<Label>("assessment-detail-title").text = selected.workflowName;
            root.Q<Label>("assessment-detail-number").text = selected.orderNumber;
            root.Q<Label>("assessment-detail-role").text = station?.MachineType == MachineType.YC ? "场桥司机" : "岸桥司机";
            root.Q<Label>("assessment-detail-control").text = $"{ControlModeDisplayName(selected.controlMode)} · 已锁定";
            root.Q<Label>("assessment-detail-duration").text = $"{selected.assessmentDurationMinutes} 分钟";
            root.Q<Label>("assessment-detail-pass").text = $"{selected.passingScore:0} 分";
            root.Q<Label>("assessment-detail-attempt").text = $"{attemptCount} / {Math.Max(1, selected.maxAttempts)} 次";
            root.Q<Label>("assessment-detail-due").text = FormatAssessmentDueTime(selected.dueAt);
            root.Q<Label>("assessment-detail-route").text = $"{selected.sourceLocation} → {selected.targetLocation}";
            root.Q<Label>("assessment-detail-container").text = $"箱号 {Fallback(selected.containerId, "--")} · {Fallback(selected.containerIsoCode, "--")} · {selected.containerWeightTonnes:0.0} t";
            root.Q<Label>("assessment-safety-weight").text = $"{selected.safetyScoreWeight} 分";
            root.Q<Label>("assessment-quality-weight").text = $"{selected.qualityScoreWeight} 分";
            root.Q<Label>("assessment-efficiency-weight").text = $"{selected.efficiencyScoreWeight} 分";
            root.Q<VisualElement>("assessment-safety-bar").style.width = Length.Percent(Math.Clamp(selected.safetyScoreWeight / 40f * 100f, 0f, 100f));
            root.Q<VisualElement>("assessment-quality-bar").style.width = Length.Percent(Math.Clamp(selected.qualityScoreWeight / 40f * 100f, 0f, 100f));
            root.Q<VisualElement>("assessment-efficiency-bar").style.width = Length.Percent(Math.Clamp(selected.efficiencyScoreWeight / 40f * 100f, 0f, 100f));
            UpdateAssessmentReadiness(attempts);
        }

        private void UpdateAssessmentReadiness(IReadOnlyList<AssessmentAttemptSnapshot> attempts = null)
        {
            if (assessmentStartButton == null)
            {
                return;
            }

            var readiness = root.Q<Label>("assessment-readiness-state");
            var detail = root.Q<Label>("assessment-readiness-detail");
            if (selectedAssessmentOrder == null)
            {
                readiness.text = "等待";
                readiness.RemoveFromClassList("assessment-readiness__state--ready");
                detail.text = "等待选择考核项目";
                assessmentStartButton.text = "请选择考核项目";
                assessmentStartButton.SetEnabled(false);
                return;
            }

            attempts ??= assessmentService.LoadAttempts(currentSession?.studentId);
            var latest = FindLatestAttempt(attempts, selectedAssessmentOrder.workOrderId);
            var attemptCount = CountAttempts(attempts, selectedAssessmentOrder.workOrderId);
            if (latest?.State == AssessmentAttemptState.Completed)
            {
                readiness.text = latest.passed ? "已通过" : "已完成";
                readiness.AddToClassList("assessment-readiness__state--ready");
                detail.text = $"本次得分 {latest.score:0.0}，详细结果可在成绩档案中查看";
                assessmentStartButton.text = "考核已完成";
                assessmentStartButton.SetEnabled(false);
                return;
            }

            if (latest?.State == AssessmentAttemptState.Terminated && attemptCount >= Math.Max(1, selectedAssessmentOrder.maxAttempts))
            {
                readiness.text = "已终止";
                readiness.RemoveFromClassList("assessment-readiness__state--ready");
                detail.text = "可用考核次数已用完，请联系教师重新下发";
                assessmentStartButton.text = "不可再次进入";
                assessmentStartButton.SetEnabled(false);
                return;
            }

            var canOperate = CanOperate(gateway?.Latest);
            readiness.text = canOperate ? "已就绪" : "未就绪";
            readiness.EnableInClassList("assessment-readiness__state--ready", canOperate);
            detail.text = canOperate
                ? "PLC、急停、控制电源及操控台输入均满足考核条件"
                : "请检查 PLC 通信、急停复位、控制电源和手柄回中";
            assessmentStartButton.text = latest?.State == AssessmentAttemptState.InProgress ? "继续当前考核" : "开始考核";
            assessmentStartButton.SetEnabled(canOperate);
        }

        private void StartSelectedAssessment()
        {
            if (selectedAssessmentOrder == null)
            {
                ShowToast("请先选择一项考核");
                return;
            }

            if (!CanOperate(gateway?.Latest))
            {
                ShowToast("设备尚未满足考核准入条件");
                return;
            }

            var attempt = assessmentService.BeginAttempt(
                currentSession?.studentId,
                selectedAssessmentOrder.workOrderId,
                selectedAssessmentOrder.maxAttempts);
            if (attempt == null)
            {
                ShowToast("该考核没有剩余可用次数，请联系教师");
                RebuildAssessmentCenter();
                return;
            }

            currentWorkOrder = selectedAssessmentOrder;
            workOrderState = new WorkOrderStateMachine();
            PrepareWorkOrderForDirectEntry();
            RefreshWorkOrderCard();
            RebuildAssessmentCenter();
            EnterOperation();
        }

        private static AssessmentAttemptSnapshot FindLatestAttempt(
            IReadOnlyList<AssessmentAttemptSnapshot> attempts,
            string workOrderId)
        {
            AssessmentAttemptSnapshot latest = null;
            foreach (var attempt in attempts ?? Array.Empty<AssessmentAttemptSnapshot>())
            {
                if (!string.Equals(attempt.workOrderId, workOrderId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (latest == null || attempt.attemptNumber > latest.attemptNumber)
                {
                    latest = attempt;
                }
            }
            return latest;
        }

        private static int CountAttempts(IReadOnlyList<AssessmentAttemptSnapshot> attempts, string workOrderId)
        {
            var count = 0;
            foreach (var attempt in attempts ?? Array.Empty<AssessmentAttemptSnapshot>())
            {
                if (string.Equals(attempt.workOrderId, workOrderId, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }
            return count;
        }

        private static string AssessmentStatusDisplayName(AssessmentAttemptSnapshot attempt)
        {
            return attempt?.State switch
            {
                AssessmentAttemptState.InProgress => "进行中",
                AssessmentAttemptState.Completed => attempt.passed ? $"{attempt.score:0} · 通过" : $"{attempt.score:0} · 未通过",
                AssessmentAttemptState.Terminated => "已终止",
                _ => "待考"
            };
        }

        private static string ControlModeDisplayName(string controlMode)
        {
            return string.Equals(controlMode, "ROCS", StringComparison.OrdinalIgnoreCase)
                ? "ROCS 远程操控"
                : "本地操控";
        }

        private static string FormatAssessmentDueTime(string dueAt)
        {
            return DateTime.TryParse(dueAt, out var due)
                ? due.ToLocalTime().ToString("MM-dd HH:mm")
                : "未设置";
        }

        private void RefreshTrainingRecords()
        {
            var selectedId = selectedTrainingRecord?.recordId;
            cachedTrainingRecords = trainingRecordService.Load(currentSession?.studentId);
            selectedTrainingRecord = null;
            foreach (var record in cachedTrainingRecords)
            {
                if (!string.IsNullOrWhiteSpace(selectedId) && record.recordId == selectedId)
                {
                    selectedTrainingRecord = record;
                    break;
                }
            }
            RebuildTrainingRecords();
        }

        private void SetTrainingRecordsFilter(TrainingRecordsFilter filter, string activeButtonName)
        {
            activeRecordsFilter = filter;
            foreach (var name in new[]
                     {
                         "records-filter-all", "records-filter-formal", "records-filter-collaborative",
                         "records-filter-fault", "records-filter-practice"
                     })
            {
                root.Q<Button>(name)?.EnableInClassList("records-filter--active", name == activeButtonName);
            }
            RebuildTrainingRecords();
        }

        private void RebuildTrainingRecords()
        {
            if (recordsList == null)
            {
                return;
            }

            recordsList.Clear();
            PopulateTrainingRecordMetrics();
            var visibleCount = 0;
            TrainingRecordSnapshot firstVisible = null;
            foreach (var record in cachedTrainingRecords)
            {
                if (!MatchesTrainingRecordsFilter(record))
                {
                    continue;
                }

                firstVisible ??= record;
                visibleCount++;
                recordsList.Add(CreateTrainingRecordItem(record));
            }

            root.Q<Label>("records-count-badge").text = $"{cachedTrainingRecords.Count} 条训练记录";
            root.Q<Label>("records-visible-count").text = $"{visibleCount} 条";
            if (visibleCount == 0)
            {
                var emptyText = cachedTrainingRecords.Count == 0
                    ? "暂无训练记录\n完成正式作业、排故实训或自由练习后，记录将自动归档到这里。"
                    : "当前筛选条件下没有训练记录";
                var empty = new Label(emptyText);
                empty.AddToClassList("records-list-empty");
                recordsList.Add(empty);
                selectedTrainingRecord = null;
            }
            else if (selectedTrainingRecord == null || !MatchesTrainingRecordsFilter(selectedTrainingRecord))
            {
                selectedTrainingRecord = firstVisible;
            }

            UpdateTrainingRecordDetail();
        }

        private void PopulateTrainingRecordMetrics()
        {
            var durationSeconds = 0;
            var completedCount = 0;
            var scoredCount = 0;
            var totalScore = 0f;
            var evaluatedOperations = 0;
            var safeOperations = 0;
            foreach (var record in cachedTrainingRecords)
            {
                durationSeconds += Math.Max(0, record.durationSeconds);
                if (record.completed)
                {
                    completedCount++;
                }
                if (record.hasScore)
                {
                    scoredCount++;
                    totalScore += record.score;
                }
                evaluatedOperations += Math.Max(0, record.evaluatedOperations);
                safeOperations += Math.Clamp(record.safeOperations, 0, Math.Max(0, record.evaluatedOperations));
            }

            root.Q<Label>("records-duration").text = durationSeconds > 0 ? FormatDuration(durationSeconds) : "--";
            root.Q<Label>("records-duration-meta").text = cachedTrainingRecords.Count > 0
                ? $"累计 {cachedTrainingRecords.Count} 次训练"
                : "暂无训练记录";
            root.Q<Label>("records-completion").text = cachedTrainingRecords.Count > 0 ? completedCount.ToString() : "--";
            root.Q<Label>("records-completion-meta").text = cachedTrainingRecords.Count > 0
                ? $"完成率 {completedCount * 100f / cachedTrainingRecords.Count:0}%"
                : "完成任务后生成";
            root.Q<Label>("records-average").text = scoredCount > 0 ? $"{totalScore / scoredCount:0.0}" : "--";
            root.Q<Label>("records-average-meta").text = scoredCount > 0 ? $"基于 {scoredCount} 次有效评价" : "暂无有效评分";
            root.Q<Label>("records-safety").text = evaluatedOperations > 0
                ? $"{safeOperations * 100f / evaluatedOperations:0}%"
                : "--";
            root.Q<Label>("records-safety-meta").text = evaluatedOperations > 0
                ? $"安全操作 {safeOperations} / {evaluatedOperations} 项"
                : "暂无安全评估";
        }

        private VisualElement CreateTrainingRecordItem(TrainingRecordSnapshot record)
        {
            var item = new VisualElement();
            item.AddToClassList("records-list-item");
            item.EnableInClassList("records-list-item--selected", selectedTrainingRecord?.recordId == record.recordId);

            var main = new VisualElement();
            main.AddToClassList("records-list-item__main");
            var topline = new VisualElement();
            topline.AddToClassList("records-list-item__topline");
            var title = new Label(Fallback(record.taskName, "未命名训练"));
            title.AddToClassList("records-list-item__title");
            var number = new Label(Fallback(record.orderNumber, "本地训练"));
            number.AddToClassList("records-list-item__number");
            topline.Add(title);
            topline.Add(number);
            main.Add(topline);

            var route = new Label(Fallback(record.route, "未记录作业路线"));
            route.AddToClassList("records-list-item__route");
            main.Add(route);
            var meta = new Label($"{TrainingRecordTypeDisplayName(record)} · {TrainingRecordRoleDisplayName(record)} · {FormatDuration(record.durationSeconds)} · {FormatRecordTime(record.completedAt)}");
            meta.AddToClassList("records-list-item__meta");
            main.Add(meta);
            item.Add(main);

            var resultText = record.hasScore ? $"{record.score:0.0}" : record.completed ? "已完成" : "未完成";
            var result = new Label(resultText);
            result.AddToClassList("records-list-item__result");
            result.EnableInClassList("records-list-item__result--incomplete", !record.completed);
            item.Add(result);

            var captured = record;
            item.RegisterCallback<ClickEvent>(_ => SelectTrainingRecord(captured));
            return item;
        }

        private void SelectTrainingRecord(TrainingRecordSnapshot record)
        {
            selectedTrainingRecord = record;
            RebuildTrainingRecords();
        }

        private void UpdateTrainingRecordDetail()
        {
            var record = selectedTrainingRecord;
            var valueNames = new[]
            {
                "records-detail-role", "records-detail-duration", "records-detail-time",
                "records-detail-route", "records-detail-container"
            };
            if (record == null)
            {
                root.Q<Label>("records-detail-status").text = "未选择";
                root.Q<Label>("records-detail-title").text = cachedTrainingRecords.Count == 0 ? "完成训练后生成档案" : "请选择一条训练记录";
                root.Q<Label>("records-detail-number").text = "--";
                foreach (var name in valueNames)
                {
                    root.Q<Label>(name).text = "--";
                }
                root.Q<Label>("records-detail-score").text = "综合评价 --";
                SetRecordCapability("records-safety-bar", "records-safety-value", 0f, false, false);
                SetRecordCapability("records-positioning-bar", "records-positioning-value", 0f, false, false);
                SetRecordCapability("records-efficiency-bar", "records-efficiency-value", 0f, false, false);
                SetRecordCapability("records-process-bar", "records-process-value", 0f, false, false);
                SetRecordCapability("records-collaboration-bar", "records-collaboration-value", 0f, false, false);
                root.Q<Label>("records-issues").text = "完成训练后，系统将在这里汇总关键事件和主要问题。";
                root.Q<Label>("records-suggestion").text = "暂无改进建议";
                return;
            }

            var status = root.Q<Label>("records-detail-status");
            status.text = record.completed ? "已完成" : "未完成";
            status.EnableInClassList("records-detail-status--completed", record.completed);
            root.Q<Label>("records-detail-title").text = Fallback(record.taskName, "未命名训练");
            root.Q<Label>("records-detail-number").text = Fallback(record.orderNumber, "本地训练");
            root.Q<Label>("records-detail-role").text = $"{TrainingRecordRoleDisplayName(record)} / {ControlModeDisplayName(record.controlMode)}";
            root.Q<Label>("records-detail-duration").text = FormatDuration(record.durationSeconds);
            root.Q<Label>("records-detail-time").text = FormatRecordTime(record.completedAt);
            root.Q<Label>("records-detail-route").text = Fallback(record.route, "未记录作业路线");
            root.Q<Label>("records-detail-container").text = $"箱号 {Fallback(record.containerId, "--")} · {TrainingRecordTypeDisplayName(record)}";
            root.Q<Label>("records-detail-score").text = record.hasScore ? $"综合评价 {record.score:0.0}" : "综合评价 --";

            SetRecordCapability("records-safety-bar", "records-safety-value", record.SafetyRate, record.evaluatedOperations > 0, false);
            SetRecordCapability("records-positioning-bar", "records-positioning-value", record.positioningScore, record.positioningScore > 0f, false);
            SetRecordCapability("records-efficiency-bar", "records-efficiency-value", record.efficiencyScore, record.efficiencyScore > 0f, false);
            SetRecordCapability("records-process-bar", "records-process-value", record.processScore, record.processScore > 0f, false);
            var collaborationApplies = string.Equals(record.assignmentType, nameof(WorkOrderAssignmentType.Collaborative), StringComparison.OrdinalIgnoreCase);
            SetRecordCapability("records-collaboration-bar", "records-collaboration-value", record.collaborationScore, record.collaborationScore > 0f, !collaborationApplies);
            root.Q<Label>("records-issues").text = BuildIssueSummary(record);
            root.Q<Label>("records-suggestion").text = BuildTrainingSuggestion(record);
        }

        private void SetRecordCapability(string barName, string valueName, float value, bool hasValue, bool notApplicable)
        {
            var normalized = Math.Clamp(value, 0f, 100f);
            root.Q<VisualElement>(barName).style.width = Length.Percent(hasValue ? normalized : 0f);
            root.Q<Label>(valueName).text = notApplicable ? "不适用" : hasValue ? $"{normalized:0}" : "--";
        }

        private bool MatchesTrainingRecordsFilter(TrainingRecordSnapshot record)
        {
            return activeRecordsFilter switch
            {
                TrainingRecordsFilter.Formal => string.Equals(record.taskType, nameof(WorkOrderTaskType.Formal), StringComparison.OrdinalIgnoreCase),
                TrainingRecordsFilter.Collaborative => string.Equals(record.assignmentType, nameof(WorkOrderAssignmentType.Collaborative), StringComparison.OrdinalIgnoreCase),
                TrainingRecordsFilter.FaultTraining => string.Equals(record.taskType, nameof(WorkOrderTaskType.FaultTraining), StringComparison.OrdinalIgnoreCase),
                TrainingRecordsFilter.Practice => string.Equals(record.taskType, nameof(WorkOrderTaskType.FreePractice), StringComparison.OrdinalIgnoreCase),
                _ => true
            };
        }

        private void AppendCurrentTrainingRecord(
            bool completed,
            int durationSeconds,
            bool hasScore,
            float score,
            int evaluatedOperations,
            int safeOperations,
            float positioningScore,
            float efficiencyScore,
            float processScore,
            float collaborationScore,
            TrainingIssueSnapshot[] issues,
            string teacherComment)
        {
            if (currentSession == null || currentWorkOrder == null)
            {
                return;
            }

            trainingRecordService.Append(currentSession.studentId, new TrainingRecordSnapshot
            {
                studentId = currentSession.studentId,
                workOrderId = currentWorkOrder.workOrderId,
                orderNumber = currentWorkOrder.orderNumber,
                taskName = currentWorkOrder.workflowName,
                taskType = currentWorkOrder.TaskType.ToString(),
                operationType = currentWorkOrder.OperationType.ToString(),
                machineType = currentWorkOrder.machineType,
                controlMode = currentWorkOrder.controlMode,
                assignmentType = currentWorkOrder.AssignmentType.ToString(),
                route = $"{currentWorkOrder.sourceLocation} → {currentWorkOrder.targetLocation}",
                containerId = currentWorkOrder.containerId,
                durationSeconds = Math.Max(0, durationSeconds),
                completed = completed,
                hasScore = hasScore,
                score = Math.Clamp(score, 0f, 100f),
                evaluatedOperations = Math.Max(0, evaluatedOperations),
                safeOperations = Math.Clamp(safeOperations, 0, Math.Max(0, evaluatedOperations)),
                positioningScore = Math.Clamp(positioningScore, 0f, 100f),
                efficiencyScore = Math.Clamp(efficiencyScore, 0f, 100f),
                processScore = Math.Clamp(processScore, 0f, 100f),
                collaborationScore = Math.Clamp(collaborationScore, 0f, 100f),
                teacherComment = teacherComment ?? string.Empty,
                issues = issues ?? Array.Empty<TrainingIssueSnapshot>()
            });
        }

        private static string TrainingRecordTypeDisplayName(TrainingRecordSnapshot record)
        {
            if (string.Equals(record.taskType, nameof(WorkOrderTaskType.FaultTraining), StringComparison.OrdinalIgnoreCase))
            {
                return "排故实训";
            }
            if (string.Equals(record.taskType, nameof(WorkOrderTaskType.FreePractice), StringComparison.OrdinalIgnoreCase))
            {
                return "自由练习";
            }
            return string.Equals(record.assignmentType, nameof(WorkOrderAssignmentType.Collaborative), StringComparison.OrdinalIgnoreCase)
                ? "联合作业"
                : "正式作业";
        }

        private static string TrainingRecordRoleDisplayName(TrainingRecordSnapshot record)
        {
            return string.Equals(record.machineType, nameof(MachineType.YC), StringComparison.OrdinalIgnoreCase)
                ? "场桥司机"
                : "岸桥司机";
        }

        private static string FormatRecordTime(string completedAt)
        {
            return DateTime.TryParse(completedAt, out var time)
                ? time.ToLocalTime().ToString("MM-dd HH:mm")
                : "时间未记录";
        }

        private static string BuildIssueSummary(TrainingRecordSnapshot record)
        {
            if (record.issues == null || record.issues.Length == 0)
            {
                return "本次训练未记录明显违规或异常事件。";
            }

            var issues = new List<string>();
            for (var index = 0; index < record.issues.Length && index < 3; index++)
            {
                var issue = record.issues[index];
                issues.Add($"• {Fallback(issue.category, "操作问题")}：{Fallback(issue.description, "未记录说明")}");
            }
            return string.Join("\n", issues);
        }

        private static string BuildTrainingSuggestion(TrainingRecordSnapshot record)
        {
            if (!string.IsNullOrWhiteSpace(record.teacherComment))
            {
                return record.teacherComment;
            }
            if (!record.completed)
            {
                return "建议先复盘任务中断原因，再进行同类型基础流程练习。";
            }
            if (record.evaluatedOperations > 0 && record.SafetyRate < 90f)
            {
                return "优先加强安全联锁与操作顺序训练，避免在条件未满足时继续操作。";
            }
            if (record.positioningScore > 0f && record.positioningScore < 80f)
            {
                return "建议增加吊具对位专项练习，重点控制小车减速时机和落点偏差。";
            }
            if (record.efficiencyScore > 0f && record.efficiencyScore < 80f)
            {
                return "建议复盘无效等待和重复操作，提升起升、小车与落箱动作衔接。";
            }
            return "当前表现稳定，可继续挑战同岗位更复杂的正式或联合作业。";
        }

        private void CreateFreeTraining()
        {
            if (station == null)
            {
                ShowToast("工位配置未就绪，不能创建训练");
                return;
            }

            foreach (var attempt in assessmentService.LoadAttempts(currentSession?.studentId))
            {
                if (attempt.State == AssessmentAttemptState.InProgress)
                {
                    ShowToast("当前存在进行中的考核，不能切换到自由练习");
                    return;
                }
            }

            if (!CanOperate(gateway?.Latest))
            {
                ShowToast("设备尚未满足自由练习启动条件");
                return;
            }

            Debug.Log($"[PortTraining] Free practice requested by {currentSession?.studentId} at {station.StationId}.");
            PracticeOperationRequested?.Invoke();
            ShowToast("自由练习启动请求已提交，不占用正式工单");
        }

        private void EnterOperation()
        {
            if (currentWorkOrder == null || workOrderState == null)
            {
                ShowToast("当前没有可执行工单");
                return;
            }

            PrepareWorkOrderForDirectEntry();

            if (workOrderState.State != WorkOrderState.Ready || !CanOperate(gateway?.Latest))
            {
                ShowToast("设备尚未满足进入驾驶室的安全条件");
                return;
            }

            if (currentWorkOrder.TaskType == WorkOrderTaskType.Assessment)
            {
                var attempt = assessmentService.BeginAttempt(
                    currentSession?.studentId,
                    currentWorkOrder.workOrderId,
                    currentWorkOrder.maxAttempts);
                if (attempt == null)
                {
                    ShowToast("该考核没有剩余可用次数，请联系教师");
                    return;
                }
            }

            workOrderState.TransitionTo(WorkOrderState.Running);
            RefreshWorkOrderCard();
            Debug.Log($"[PortTraining] Work order {currentWorkOrder.orderNumber} requested by {currentSession?.studentId} at {station?.StationId}.");
            WorkOrderOperationRequested?.Invoke(currentWorkOrder);
            ShowToast(currentWorkOrder.TaskType == WorkOrderTaskType.Assessment
                ? "考核计时已开始，操控方式已锁定，正在进入虚拟驾驶室"
                : "工单已进入执行状态，正在切换虚拟驾驶室");
        }

        private void RefreshDiagnostics()
        {
            if (root == null)
            {
                return;
            }

            var snapshot = gateway?.Latest;
            SetPill("gateway-pill", snapshot?.gatewayOnline == true, snapshot?.gatewayOnline == true ? "Gateway 在线" : "Gateway 离线");
            SetPill("plc-pill", snapshot?.plcOnline == true, snapshot?.plcOnline == true ? "PLC 在线" : "PLC 离线");

            var canOperate = CanOperate(snapshot);
            var healthTitle = root.Q<Label>("health-title");
            var healthDetail = root.Q<Label>("health-detail");
            if (healthTitle != null)
            {
                healthTitle.text = canOperate ? "设备可以开始训练" : "设备尚未就绪";
            }
            if (healthDetail != null)
            {
                healthDetail.text = canOperate ? "安全条件、控制电源和操控台输入均满足要求。" : "可浏览平台内容，但暂不允许进入实体操控训练。";
            }
            SetHealth("health-plc", snapshot?.plcOnline == true, snapshot?.plcOnline == true ? "PLC 通信 · 正常" : "PLC 通信 · 未连接");
            SetHealth("health-estop", snapshot?.emergencyStopReleased == true, snapshot?.emergencyStopReleased == true ? "急停状态 · 已复位" : "急停状态 · 未复位");
            SetHealth("health-handle", snapshot?.handlesCentered == true, snapshot?.handlesCentered == true ? "手柄回中 · 正常" : "手柄回中 · 未满足");
            UpdateAssessmentReadiness();

            if (workOrderState?.State == WorkOrderState.Preflight && canOperate)
            {
                TryCompletePreflight();
                RefreshWorkOrderCard();
            }
        }

        private void TryCompletePreflight()
        {
            if (workOrderState?.State == WorkOrderState.Preflight && CanOperate(gateway?.Latest))
            {
                workOrderState.TransitionTo(WorkOrderState.Ready);
            }
        }

        private void PrepareWorkOrderForDirectEntry()
        {
            if (workOrderState?.State == WorkOrderState.PendingAcceptance)
            {
                workOrderState.TransitionTo(WorkOrderState.Accepted);
                workOrderState.TransitionTo(WorkOrderState.Preflight);
            }

            TryCompletePreflight();
        }

        private void RefreshWorkOrderCard()
        {
            if (currentWorkOrder == null || workOrderState == null)
            {
                return;
            }

            var operationName = OperationDisplayName(currentWorkOrder);
            var sourceName = SourceDisplayName(currentWorkOrder);
            var assignmentName = currentWorkOrder.AssignmentType == WorkOrderAssignmentType.Collaborative ? "联合工单" : "单机任务";

            root.Q<Label>("task-title").text = $"{operationName} · {currentWorkOrder.workflowName}";
            root.Q<Label>("task-detail").text =
                $"{currentWorkOrder.orderNumber} · {TaskTypeDisplayName(currentWorkOrder)} · {assignmentName} · {sourceName}下发 · 箱号 {Fallback(currentWorkOrder.containerId, "--")}";
            root.Q<Label>("task-source").text = currentWorkOrder.sourceLocation;
            root.Q<Label>("task-target").text = currentWorkOrder.targetLocation;
            RefreshCollaborationCard(currentWorkOrder);

            var status = root.Q<Label>("task-status");
            status.EnableInClassList("work-order-status--ready", workOrderState.State == WorkOrderState.Ready || workOrderState.State == WorkOrderState.Running);

            switch (workOrderState.State)
            {
                case WorkOrderState.PendingAcceptance:
                    status.text = "正在准备";
                    enterOperationButton.text = "检查设备状态";
                    enterOperationButton.SetEnabled(false);
                    break;
                case WorkOrderState.Preflight:
                    status.text = "等待设备";
                    enterOperationButton.text = "设备尚未就绪";
                    enterOperationButton.SetEnabled(false);
                    break;
                case WorkOrderState.Ready:
                    status.text = "可以执行";
                    enterOperationButton.text = $"进入{(station.MachineType == MachineType.QC ? "岸桥" : "场桥")}驾驶室";
                    enterOperationButton.SetEnabled(CanOperate(gateway?.Latest));
                    break;
                case WorkOrderState.Running:
                    status.text = "执行中";
                    enterOperationButton.text = "工单执行中";
                    enterOperationButton.SetEnabled(false);
                    break;
                default:
                    status.text = workOrderState.State.ToString();
                    enterOperationButton.SetEnabled(false);
                    break;
            }
        }

        private void PopulateDashboardOverview(IReadOnlyList<WorkOrderSnapshot> workOrders)
        {
            var count = workOrders?.Count ?? 0;
            RefreshStudentPortrait();

            if (count == 0)
            {
                root.Q<Label>("queue-title").text = "当前队列已清空";
                root.Q<Label>("queue-detail").text = "可进入自由练习，或等待教师端与 TOS 下发新工单。";
                return;
            }

            var active = workOrders[0];
            if (count > 1)
            {
                var next = workOrders[1];
                root.Q<Label>("queue-title").text = $"{OperationDisplayName(next)} · {next.orderNumber}";
                root.Q<Label>("queue-detail").text = $"{next.sourceLocation} → {next.targetLocation} · 箱号 {next.containerId}";
            }
            else
            {
                root.Q<Label>("queue-title").text = "当前无后续待办工单";
                root.Q<Label>("queue-detail").text = "完成当前工单后，平台会继续同步教师端或 TOS 任务。";
            }
        }

        private void RefreshStudentPortrait()
        {
            var progress = progressService.Load(currentSession?.studentId);
            if (progress == null)
            {
                return;
            }

            root.Q<Label>("portrait-completion").text = progress.assignedTaskCount > 0 ? $"{progress.CompletionRate:0}%" : "--";
            root.Q<Label>("portrait-completion-meta").text = progress.assignedTaskCount > 0
                ? $"已完成 {progress.completedTaskCount} / {progress.assignedTaskCount} 项"
                : "完成任务后生成";

            root.Q<Label>("portrait-duration").text = progress.practiceDurationSeconds > 0
                ? FormatDuration(progress.practiceDurationSeconds)
                : "--";
            root.Q<Label>("portrait-duration-meta").text = progress.practiceDurationSeconds > 0
                ? $"累计 {progress.trainingSessionCount} 次训练"
                : "暂无训练记录";

            root.Q<Label>("portrait-score").text = progress.scoredSessionCount > 0 ? $"{progress.AverageScore:0.0}" : "--";
            root.Q<Label>("portrait-score-meta").text = progress.scoredSessionCount > 0
                ? $"基于 {progress.scoredSessionCount} 次有效评分"
                : "完成评分后生成";

            root.Q<Label>("portrait-safety").text = progress.evaluatedOperationCount > 0 ? $"{progress.SafetyRate:0}%" : "--";
            root.Q<Label>("portrait-safety-meta").text = progress.evaluatedOperationCount > 0
                ? $"安全操作 {progress.safeOperationCount} / {progress.evaluatedOperationCount} 项"
                : "暂无安全评估数据";
        }

        private static string FormatDuration(int totalSeconds)
        {
            var duration = TimeSpan.FromSeconds(Math.Max(0, totalSeconds));
            if (duration.TotalHours >= 1d)
            {
                return $"{(int)duration.TotalHours}小时 {duration.Minutes}分";
            }
            return $"{Math.Max(1, duration.Minutes)}分钟";
        }

        private void RefreshCollaborationCard(WorkOrderSnapshot workOrder)
        {
            var title = root.Q<Label>("collaboration-title");
            var detail = root.Q<Label>("collaboration-detail");
            var qc = root.Q<Label>("collaboration-qc");
            var agv = root.Q<Label>("collaboration-agv");
            var yc = root.Q<Label>("collaboration-yc");

            if (workOrder == null)
            {
                title.text = "当前无协同任务";
                detail.text = "接收到联合工单后，将在这里显示岗位交接链路。";
                qc.text = "岸桥 · 待命";
                agv.text = "AGV · 待命";
                yc.text = "场桥 · 待命";
                return;
            }

            var isCollaborative = workOrder.AssignmentType == WorkOrderAssignmentType.Collaborative;
            title.text = isCollaborative ? "联合链路已建立" : "独立训练工单";
            detail.text = isCollaborative
                ? $"本工位将与 {Fallback(workOrder.partnerStationId, "协同工位")} 完成作业交接。"
                : "当前由本工位执行，其他业务环节由平台模拟。";

            var qcStation = station?.MachineType == MachineType.QC ? station.StationId : Fallback(workOrder.partnerStationId, "岸桥协同位");
            var ycStation = station?.MachineType == MachineType.YC ? station.StationId : Fallback(workOrder.partnerStationId, "场桥协同位");
            qc.text = $"岸桥 · {qcStation}";
            agv.text = $"AGV · {Fallback(workOrder.vehicleId, "等待调度")}";
            yc.text = $"场桥 · {ycStation}";
        }

        private static string OperationDisplayName(WorkOrderSnapshot workOrder)
        {
            if (workOrder.TaskType == WorkOrderTaskType.FaultTraining)
            {
                return "排故实训";
            }
            if (workOrder.TaskType == WorkOrderTaskType.FreePractice || workOrder.OperationType == WorkOrderOperationType.Practice)
            {
                return "自由练习";
            }
            if (workOrder.TaskType == WorkOrderTaskType.Assessment)
            {
                return "考核任务";
            }
            return workOrder.OperationType == WorkOrderOperationType.Load ? "装船" :
                workOrder.OperationType == WorkOrderOperationType.YardTransfer ? "移箱" : "卸船";
        }

        private static string TaskTypeDisplayName(WorkOrderSnapshot workOrder)
        {
            return workOrder.TaskType switch
            {
                WorkOrderTaskType.FaultTraining => "排故实训",
                WorkOrderTaskType.FreePractice => "自由训练",
                WorkOrderTaskType.Assessment => "考核任务",
                _ => "正式作业"
            };
        }

        private static string SourceDisplayName(WorkOrderSnapshot workOrder)
        {
            return workOrder.Source == WorkOrderSource.TOS ? "TOS" :
                workOrder.Source == WorkOrderSource.Teacher ? "教师端" : "本机";
        }

        private static string Fallback(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private void SetPill(string name, bool ok, string text)
        {
            var pill = root.Q<Label>(name);
            if (pill == null)
            {
                return;
            }
            pill.text = text;
            pill.EnableInClassList("status-pill--ok", ok);
        }

        private void SetHealth(string name, bool ok, string text)
        {
            var label = root.Q<Label>(name);
            if (label == null)
            {
                return;
            }
            label.text = text;
            label.EnableInClassList("health-item--ok", ok);
        }

        private static bool CanOperate(EquipmentSnapshot snapshot)
        {
            return snapshot != null &&
                   snapshot.gatewayOnline &&
                   snapshot.plcOnline &&
                   snapshot.emergencyStopReleased &&
                   snapshot.controlPowerOn &&
                   snapshot.handlesCentered;
        }

        private void ShowToast(string message)
        {
            toast.text = message;
            toast.RemoveFromClassList("toast--hidden");
            hideToastAt = Time.unscaledTime + 2.8f;
        }
    }
}
