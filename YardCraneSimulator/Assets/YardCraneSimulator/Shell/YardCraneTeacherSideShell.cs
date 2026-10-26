using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using OMG.UI.DatePickerUITK;
using PortTraining.YardCraneSimulator.Training;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace PortTraining.YardCraneSimulator.Shell
{
    // Owns the dedicated TeacherHome scene. StudentEntry can hand a local administrator
    // identity across the scene boundary; direct scene entry creates the same admin profile.
    // This shell then owns two scene-configured UIDocuments: TeacherSide Display1 and Display2.
    // It never reuses StudentSide UXML/PanelSettings and never creates UIDocuments at run time.
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class YardCraneTeacherSideShell : MonoBehaviour
    {
        public const string Version = "2026-10-10-yc-teacher-home-v28-eleven-fault-categories";
        public const string SceneName = "TeacherHome";

        [InspectorName("登录场景")]
        [Tooltip("Scene loaded when the teacher clicks the return-to-login button. The scene must be enabled in Build Settings.")]
        public string LoginSceneName = YardCraneStudentShell.SceneName;

        private static readonly string[] QuayFaultCategoryNames =
        {
            "电源故障",
            "起升机构",
            "大车机构",
            "小车机构",
            "导板机构",
            "前后倾机构",
            "平面回转机构",
            "左右倾机构",
            "夹轮器",
            "吊具泵",
            "元器件损坏"
        };

        private static readonly string[] YardFaultCategoryNames =
        {
            "电源故障",
            "起升机构",
            "大车机构",
            "小车机构",
            "导板机构",
            "前后倾机构",
            "平面回转机构",
            "左右倾机构",
            "夹轮器",
            "吊具泵",
            "元器件损坏"
        };

        [Serializable]
        private sealed class FaultCatalogDocument
        {
            public int schemaVersion;
            public CraneFaultCatalog quayCrane;
            public CraneFaultCatalog yardCrane;
        }

        [Serializable]
        private sealed class CraneFaultCatalog : IYardCraneFaultCatalogSource
        {
            public string craneType;
            public string sourceWorkbook;
            public string sourceSheet;
            public FaultCategory[] categories;
            public FaultRecord[] unclassifiedFaults;

            public IEnumerable<YardCraneFaultCatalogRecord> EnumerateFaults()
            {
                foreach (var category in categories ?? Array.Empty<FaultCategory>())
                {
                    if (category == null) continue;
                    foreach (var fault in category.faults ?? Array.Empty<FaultRecord>())
                    {
                        if (fault == null) continue;
                        yield return new YardCraneFaultCatalogRecord(
                            fault.number,
                            fault.phenomenon,
                            fault.faultPoint,
                            fault.lineTag,
                            category.name);
                    }
                }

                foreach (var fault in unclassifiedFaults ?? Array.Empty<FaultRecord>())
                {
                    if (fault == null) continue;
                    yield return new YardCraneFaultCatalogRecord(
                        fault.number,
                        fault.phenomenon,
                        fault.faultPoint,
                        fault.lineTag,
                        "待归类");
                }
            }
        }

        [Serializable]
        private sealed class FaultCategory
        {
            public string name;
            public FaultRecord[] faults;
        }

        [Serializable]
        private sealed class FaultRecord
        {
            public int number;
            public string lineTag;
            public string faultPoint;
            public string phenomenon;
        }

        private sealed class FaultCategoryView
        {
            public readonly string Name;
            public readonly FaultRecord[] Faults;

            public FaultCategoryView(string name, FaultRecord[] faults)
            {
                Name = name;
                Faults = faults ?? Array.Empty<FaultRecord>();
            }
        }

        private sealed class AssessmentPreviewDetail
        {
            public readonly string Date;
            public readonly string RecordId;
            public readonly string Name;
            public readonly string StudentId;
            public readonly string ClassName;
            public readonly string Major;
            public readonly string Project;
            public readonly string Duration;
            public readonly string Score;
            public readonly string Status;
            public readonly string Summary;
            public readonly string[] DeductionItems;
            public readonly string[] DeductionValues;
            public readonly string[] DeductionCounts;
            public readonly string[] DeductionNotes;

            public AssessmentPreviewDetail(
                string recordId, string name, string studentId, string className, string major,
                string project, string duration, string score, string status, string summary,
                string[] deductionItems, string[] deductionValues, string[] deductionCounts,
                string[] deductionNotes, string date)
            {
                Date = date;
                RecordId = recordId;
                Name = name;
                StudentId = studentId;
                ClassName = className;
                Major = major;
                Project = project;
                Duration = duration;
                Score = score;
                Status = status;
                Summary = summary;
                DeductionItems = deductionItems;
                DeductionValues = deductionValues;
                DeductionCounts = deductionCounts;
                DeductionNotes = deductionNotes;
            }
        }

        [Serializable]
        private sealed class AssessmentScoreFile
        {
            public AssessmentScoreRecord[] data;
        }

        [Serializable]
        private sealed class AssessmentScoreRecord
        {
            public string 姓名;
            public string 学号;
            public string 班级;
            public string 专业;
            public string 项目;
            public string 用时;
            public double 成绩;
            public AssessmentScoreDeduction[] 详情列表;
        }

        [Serializable]
        private sealed class AssessmentScoreDeduction
        {
            public string 扣分项;
            public double 分值;
        }

        // These thresholds classify the existing numeric score for display only; no score is written.
        private const double AssessmentExcellentScore = 90;
        private const double AssessmentPassingScore = 60;
        private readonly List<AssessmentPreviewDetail> assessmentPreviewDetails = new();
        private string assessmentSelectedDate = string.Empty;
        private string assessmentLoadNotice;

        private UIDocument document;
        [SerializeField, HideInInspector] private PanelSettings panelTemplate;
        [SerializeField]
        [Tooltip("岸桥故障信息资产；教师端选择岸桥时从此资产构建8类故障目录。")]
        private ScriptableObject quayFaultCatalogAsset;
        [SerializeField]
        [Tooltip("场桥故障信息资产；教师端选择场桥时从此资产构建8类故障目录。")]
        private ScriptableObject yardFaultCatalogAsset;
        [SerializeField]
        [Tooltip("当前教师工作台使用的任务目录；场桥场景绑定场桥任务信息.asset，岸桥场景绑定岸桥任务信息.asset。")]
        private ScriptableObject taskCatalogAsset;
        [SerializeField]
        [Tooltip("岸桥任务目录；登录入口选择岸桥后使用岸桥任务信息.asset。")]
        private ScriptableObject quayTaskCatalogAsset;
        private PanelSettings panelSettings;
        private Font runtimeFont;
        private VisualElement root;

        [SerializeField]
        [Tooltip("TeacherHome Display2 UIDocument. It must be a separate root object, not this Display1 document or one of its children.")]
        private UIDocument lowerDocument;
        private UIDocument documentLower;
        [SerializeField, HideInInspector] private PanelSettings lowerPanelTemplate;
        private PanelSettings panelSettingsLower;
        private Font runtimeFontLower;
        private VisualElement rootLower;

        private Label identity;
        private Label greeting;
        private Label moduleStatus;
        private Label currentSection;
        private Label consoleLabel;
        private Label taskPageStatus;
        private Label faultPublishPageStatus;
        private Label faultSourceStateLabel;
        private Label faultSummaryCategoryValue;
        private Label faultSummaryTotalValue;
        private Label faultSummaryUnclassifiedValue;
        private Label faultListTitle;
        private Label faultListOverline;
        private Label faultSelectedCount;
        private Label faultSelectedCategory;
        private Label faultSelectedTitle;
        private Label faultSelectedMeta;
        private Label taskEditorCategoryCaption;
        private Label taskEditorTitleLabel;
        private Label taskEditorSelectionCount;
        private Label taskEditorStatus;
        private Label faultTaskDetailTitle;
        private Label faultTaskDetailCount;
        private Label assessmentPageStatus;
        private Label assessmentDetailRecord;
        private Label assessmentDetailName;
        private Label assessmentDetailStudent;
        private Label assessmentDetailClass;
        private Label assessmentDetailMajor;
        private Label assessmentDetailProject;
        private Label assessmentDetailSummary;
        private Label assessmentDetailDuration;
        private Label assessmentDetailScore;
        private Label assessmentDetailStatus;
        private DropdownField taskEditorTarget;
        private TextField assessmentDateField;
        private TextField assessmentSearchField;
        private VisualElement homePage;
        private VisualElement taskPage;
        private VisualElement faultPublishPage;
        private VisualElement faultCategoryList;
        private ScrollView faultListScroll;
        private ScrollView taskListScroll;
        private ScrollView faultTaskDetailList;
        private VisualElement assessmentPage;
        private VisualElement assessmentDetailOverlay;
        private VisualElement faultTaskDetailOverlay;
        private VisualElement taskEditorDrawer;
        private Button taskManagementButton;
        private Button assessmentStatisticsButton;
        private Button faultPublishPageBackButton;
        private Button faultPublishButton;
        private Button taskPageBackButton;
        private Button taskCreateButton;
        private Button taskFilterAllButton;
        private Button taskFilterPracticeButton;
        private Button taskFilterAssessmentButton;
        private Button taskEditorCancelButton;
        private Button taskEditorSaveButton;
        private Button faultTaskDetailCloseButton;
        private Button assessmentPageBackButton;
        private Button assessmentDateButton;
        private Button assessmentFilterAllButton;
        private Button assessmentFilterExcellentButton;
        private Button assessmentFilterReviewButton;
        private Button assessmentSearchButton;
        private Button assessmentDetailCloseButton;
        private Button logoutButton;
        private Button quitButton;
        // 2026-09-17: the 3 "05 本机模板/03 考核任务/02 实训任务" summary tiles above the task
        // table, in UXML document order - see RefreshTaskRowsFromStore.
        private readonly List<Label> taskSummaryValueLabels = new List<Label>();
        // Number of assessment result rows populated from the local score store.
        private int populatedAssessmentRowCount;
        private ScrollView assessmentListScroll;
        private ScrollView assessmentDeductionScroll;
        private VisualElement assessmentCalendarOverlay;
        private DatePicker assessmentCalendarPicker;
        private Button assessmentCalendarScrim;
        private Button assessmentCalendarCloseButton;
        private Button assessmentCalendarAllButton;
        private Button assessmentCalendarTodayButton;
        private Button assessmentCalendarCancelButton;
        private readonly List<Button> taskCategoryButtons = new List<Button>();
        private readonly List<Button> taskProjectButtons = new List<Button>();
        private readonly List<Button> taskDurationButtons = new List<Button>();
        private readonly List<Button> taskOperationButtons = new List<Button>();
        private readonly Dictionary<string, YardCraneTaskDefinition> selectedTaskDefinitions =
            new Dictionary<string, YardCraneTaskDefinition>(StringComparer.Ordinal);
        private string editingQuestionId;
        private string editingSourceKey;
        private int editingTargetId;
        private string taskQuestionFilter;
        private readonly List<VisualElement> assessmentRows = new List<VisualElement>();
        // Record count, passing percentage and excellent count, in asset order.
        private readonly List<Label> assessmentSummaryValueLabels = new List<Label>();
        private readonly List<Button> assessmentDetailButtons = new List<Button>();
        private readonly List<Label> assessmentDeductionItemLabels = new List<Label>();
        private readonly List<Label> assessmentDeductionValueLabels = new List<Label>();
        private readonly List<Label> assessmentDeductionCountLabels = new List<Label>();
        private readonly List<Label> assessmentDeductionNoteLabels = new List<Label>();
        private FaultCatalogDocument faultCatalog;
        private YardCraneTaskCatalog taskCatalog;
        private CraneFaultCatalog selectedFaultCrane;
        private readonly HashSet<int> selectedFaultIds = new HashSet<int>();
        private string assessmentRequiredClass;
        private YardCraneLocalStudentProfile profile;
        private bool displayActivated;
        private bool upperEventsBound;
        private bool built;
        private bool building;
        private bool editorSuspended;
        private double nextBuildAt;
        private string lastBuildProblem;

        public bool IsReady => built && isActiveAndEnabled && !editorSuspended &&
                               document != null && document.isActiveAndEnabled &&
                               document.rootVisualElement == root && root != null &&
                               document.panelSettings == panelSettings &&
                               root.Q<Button>("teacher-task-management-button") == taskManagementButton &&
                               root.Q<VisualElement>("teacher-task-page") == taskPage &&
                               root.Q<Button>("teacher-task-back-button") == taskPageBackButton &&
                               root.Q<VisualElement>("teacher-fault-publish-page") == faultPublishPage &&
                               root.Q<Button>("teacher-fault-publish-back-button") == faultPublishPageBackButton &&
                               root.Q<VisualElement>("teacher-assessment-page") == assessmentPage &&
                               root.Q<Button>("teacher-assessment-back-button") == assessmentPageBackButton &&
                               root.Q<DatePicker>("teacher-assessment-calendar-picker") == assessmentCalendarPicker &&
                               root.Q<VisualElement>("teacher-assessment-detail-overlay") == assessmentDetailOverlay &&
                               root.Q<Button>("teacher-assessment-detail-close") == assessmentDetailCloseButton &&
                               documentLower != null && documentLower.isActiveAndEnabled &&
                               documentLower.rootVisualElement == rootLower && rootLower != null &&
                               documentLower.panelSettings == panelSettingsLower && lowerDocument == documentLower;
        public VisualElement VisualRoot => root;
        public VisualElement VisualRootLower => rootLower;
        public UIDocument Document => document;
        public UIDocument DocumentLower => documentLower;

        private void Awake()
        {
            if (!Application.isPlaying) return;
            profile = YardCraneTeacherHomeHandoff.Consume();
            if (profile != null && profile.Role == YardCraneShellRole.Teacher) return;

            YardCraneTeacherHomeHandoff.Clear();
            YardCraneFallbackSession.Clear();
            YardCraneTrainingSessionIdentity.Clear();
            if (!YardCraneLocalStudentProfile.TryLogin(
                    YardCraneShellRole.Teacher,
                    "admin",
                    "admin",
                    out profile,
                    out var error))
            {
                enabled = false;
                Debug.LogError($"[YC_TEACHER_HOME_DEFAULT_ADMIN_FAILED] {error}", this);
                return;
            }
            Debug.Log("[YC_TEACHER_HOME_DEFAULT_ADMIN] account=admin", this);
        }

        private void OnEnable()
        {
            if (Application.isPlaying) ActivateSecondDisplayIfNeeded();
            nextBuildAt = 0;
            BuildInterface();
        }

        private void Start()
        {
            if (!isActiveAndEnabled || IsReady) return;
            BuildInterface();
        }

        private void Update()
        {
            if (editorSuspended) return;
            if (!IsReady) BuildInterface();
        }

        private void ActivateSecondDisplayIfNeeded()
        {
            if (displayActivated || Display.displays.Length <= 1) return;
            Display.displays[1].Activate();
            displayActivated = true;
        }

        private void BuildInterface()
        {
            if (editorSuspended || building || IsReady || !isActiveAndEnabled ||
                Time.realtimeSinceStartupAsDouble < nextBuildAt) return;
            nextBuildAt = Time.realtimeSinceStartupAsDouble + 0.25;
            building = true;
            try
            {
                if (built) ReleaseInterface();
                if (!BuildUpperInterface() || !BuildLowerInterface()) return;

                RefreshIdentity();
                ResetModuleSelection();
                ApplyCranePresentation();
                root.style.display = DisplayStyle.Flex;
                rootLower.style.display = DisplayStyle.Flex;
                built = true;
                lastBuildProblem = null;
                if (Application.isPlaying)
                {
                    Debug.Log(
                        $"[YC_TEACHER_SIDE_SHELL_READY] version={Version}, account={(profile != null ? profile.AccountId : "(editor-preview)")}, displays={Display.displays.Length}",
                        this);
                }
            }
            finally
            {
                building = false;
            }
        }

        private bool BuildUpperInterface()
        {
            document = GetComponent<UIDocument>();
            var problem = YardCraneShellPanelUtility.PrepareDocument(
                document, ref panelTemplate, ref panelSettings, "Teacher Side (Display1)", out var documentReady);
            if (problem != null) return BuildProblem(problem);
            if (!documentReady) return false;
            if (upperEventsBound && root == document.rootVisualElement &&
                root.Q<Button>("teacher-task-management-button") == taskManagementButton) return true;

            UnbindEvents();
            root = document.rootVisualElement;
            root.style.flexGrow = 1;
            YardCraneShellPanelUtility.ReleaseOwnedObject(runtimeFont);
            runtimeFont = YardCraneShellPanelUtility.CreateShellFont();
            if (runtimeFont != null) root.style.unityFontDefinition = FontDefinition.FromFont(runtimeFont);

            identity = root.Q<Label>("teacher-identity");
            greeting = root.Q<Label>("teacher-greeting");
            moduleStatus = root.Q<Label>("teacher-module-status");
            currentSection = root.Q<Label>("teacher-current-section");
            consoleLabel = root.Q<Label>("teacher-console-label");
            taskPageStatus = root.Q<Label>("teacher-task-page-status");
            faultPublishPageStatus = root.Q<Label>("teacher-fault-publish-page-status");
            faultSourceStateLabel = root.Q<Label>("teacher-fault-source-state-label");
            faultSummaryCategoryValue = root.Q<Label>("teacher-fault-summary-category-value");
            faultSummaryTotalValue = root.Q<Label>("teacher-fault-summary-total-value");
            faultSummaryUnclassifiedValue = root.Q<Label>("teacher-fault-summary-unclassified-value");
            faultListTitle = root.Q<Label>("teacher-fault-list-title");
            faultListOverline = root.Q<Label>("teacher-fault-list-overline");
            faultSelectedCount = root.Q<Label>("teacher-fault-selected-count");
            faultSelectedCategory = root.Q<Label>("teacher-fault-selected-category");
            faultSelectedTitle = root.Q<Label>("teacher-fault-selected-title");
            faultSelectedMeta = root.Q<Label>("teacher-fault-selected-meta");
            taskEditorCategoryCaption = root.Q<Label>("teacher-task-editor-category-caption");
            taskEditorTitleLabel = root.Q<Label>("teacher-task-editor-title");
            faultTaskDetailTitle = root.Q<Label>("teacher-fault-task-detail-title");
            faultTaskDetailCount = root.Q<Label>("teacher-fault-task-detail-count");
            assessmentPageStatus = root.Q<Label>("teacher-assessment-page-status");
            assessmentDetailRecord = root.Q<Label>("teacher-assessment-detail-record");
            assessmentDetailName = root.Q<Label>("teacher-assessment-detail-name");
            assessmentDetailStudent = root.Q<Label>("teacher-assessment-detail-student");
            assessmentDetailClass = root.Q<Label>("teacher-assessment-detail-class");
            assessmentDetailMajor = root.Q<Label>("teacher-assessment-detail-major");
            assessmentDetailProject = root.Q<Label>("teacher-assessment-detail-project");
            assessmentDetailSummary = root.Q<Label>("teacher-assessment-detail-summary");
            assessmentDetailDuration = root.Q<Label>("teacher-assessment-detail-duration");
            assessmentDetailScore = root.Q<Label>("teacher-assessment-detail-score");
            assessmentDetailStatus = root.Q<Label>("teacher-assessment-detail-status");
            taskEditorTarget = root.Q<DropdownField>("teacher-task-editor-target");
            assessmentDateField = root.Q<TextField>("teacher-assessment-date-field");
            assessmentSearchField = root.Q<TextField>("teacher-assessment-search-field");
            assessmentListScroll = root.Q<ScrollView>("teacher-assessment-list-scroll");
            assessmentDeductionScroll = root.Q<ScrollView>("teacher-assessment-deduction-scroll");
            assessmentCalendarOverlay = root.Q<VisualElement>("teacher-assessment-calendar-overlay");
            assessmentCalendarPicker = root.Q<DatePicker>("teacher-assessment-calendar-picker");
            assessmentCalendarScrim = root.Q<Button>("teacher-assessment-calendar-scrim");
            assessmentCalendarCloseButton = root.Q<Button>("teacher-assessment-calendar-close");
            assessmentCalendarAllButton = root.Q<Button>("teacher-assessment-calendar-all");
            assessmentCalendarTodayButton = root.Q<Button>("teacher-assessment-calendar-today");
            assessmentCalendarCancelButton = root.Q<Button>("teacher-assessment-calendar-cancel");
            homePage = root.Q<VisualElement>("teacher-home-page");
            taskPage = root.Q<VisualElement>("teacher-task-page");
            faultPublishPage = root.Q<VisualElement>("teacher-fault-publish-page");
            faultCategoryList = root.Q<VisualElement>("teacher-fault-category-list");
            faultListScroll = root.Q<ScrollView>("teacher-fault-list-scroll");
            taskListScroll = root.Q<ScrollView>("teacher-task-list-scroll");
            faultTaskDetailList = root.Q<ScrollView>("teacher-fault-task-detail-list");
            assessmentPage = root.Q<VisualElement>("teacher-assessment-page");
            assessmentDetailOverlay = root.Q<VisualElement>("teacher-assessment-detail-overlay");
            faultTaskDetailOverlay = root.Q<VisualElement>("teacher-fault-task-detail-overlay");
            taskEditorDrawer = root.Q<VisualElement>("teacher-task-editor-drawer");
            taskManagementButton = root.Q<Button>("teacher-task-management-button");
            assessmentStatisticsButton = root.Q<Button>("teacher-assessment-statistics-button");
            faultPublishPageBackButton = root.Q<Button>("teacher-fault-publish-back-button");
            faultPublishButton = root.Q<Button>("teacher-fault-publish-button");
            taskPageBackButton = root.Q<Button>("teacher-task-back-button");
            taskCreateButton = root.Q<Button>("teacher-task-create-button");
            taskFilterAllButton = root.Q<Button>("teacher-task-filter-all");
            taskFilterPracticeButton = root.Q<Button>("teacher-task-filter-practice");
            taskFilterAssessmentButton = root.Q<Button>("teacher-task-filter-assessment");
            taskEditorCancelButton = root.Q<Button>("teacher-task-editor-cancel");
            taskEditorSaveButton = root.Q<Button>("teacher-task-editor-save");
            faultTaskDetailCloseButton = root.Q<Button>("teacher-fault-task-detail-close");
            taskEditorSelectionCount = root.Q<Label>("teacher-task-editor-selection-count");
            taskEditorStatus = root.Q<Label>("teacher-task-editor-status");
            assessmentPageBackButton = root.Q<Button>("teacher-assessment-back-button");
            assessmentDateButton = root.Q<Button>("teacher-assessment-date-button");
            assessmentFilterAllButton = root.Q<Button>("teacher-assessment-filter-all");
            assessmentFilterExcellentButton = root.Q<Button>("teacher-assessment-filter-excellent");
            assessmentFilterReviewButton = root.Q<Button>("teacher-assessment-filter-review");
            assessmentSearchButton = root.Q<Button>("teacher-assessment-search-button");
            assessmentDetailCloseButton = root.Q<Button>("teacher-assessment-detail-close");
            logoutButton = root.Q<Button>("teacher-logout-button");
            quitButton = root.Q<Button>("teacher-quit-button");
            if (logoutButton != null) logoutButton.style.display = DisplayStyle.Flex;
            taskCategoryButtons.Clear();
            taskCategoryButtons.AddRange(root.Query<Button>(className: "teacher-task-category-button").ToList());
            taskProjectButtons.Clear();
            taskProjectButtons.AddRange(root.Query<Button>(className: "teacher-task-project-button").ToList());
            taskDurationButtons.Clear();
            taskDurationButtons.AddRange(root.Query<Button>(className: "teacher-task-duration-choice").ToList());
            taskOperationButtons.Clear();
            taskOperationButtons.AddRange(root.Query<Button>(className: "teacher-task-operation-choice").ToList());
            taskSummaryValueLabels.Clear();
            taskSummaryValueLabels.AddRange(root.Query<Label>(className: "teacher-task-summary-value").ToList());
            assessmentRows.Clear();
            assessmentRows.AddRange(root.Query<VisualElement>(className: "teacher-assessment-list-row").ToList());
            assessmentSummaryValueLabels.Clear();
            assessmentSummaryValueLabels.AddRange(
                root.Query<Label>(className: "teacher-assessment-summary-value").ToList());
            assessmentDetailButtons.Clear();
            assessmentDetailButtons.AddRange(root.Query<Button>(className: "teacher-assessment-detail-button").ToList());
            assessmentDeductionItemLabels.Clear();
            assessmentDeductionItemLabels.AddRange(root.Query<Label>(className: "teacher-assessment-detail-rule-item").ToList());
            assessmentDeductionValueLabels.Clear();
            assessmentDeductionValueLabels.AddRange(root.Query<Label>(className: "teacher-assessment-detail-rule-value").ToList());
            assessmentDeductionCountLabels.Clear();
            assessmentDeductionCountLabels.AddRange(root.Query<Label>(className: "teacher-assessment-detail-rule-count").ToList());
            assessmentDeductionNoteLabels.Clear();
            assessmentDeductionNoteLabels.AddRange(root.Query<Label>(className: "teacher-assessment-detail-rule-note").ToList());
            if (identity == null || greeting == null || moduleStatus == null ||
                currentSection == null || consoleLabel == null || taskPageStatus == null ||
                faultPublishPageStatus == null || faultSourceStateLabel == null ||
                faultSummaryCategoryValue == null || faultSummaryTotalValue == null ||
                faultSummaryUnclassifiedValue == null || faultListTitle == null ||
                faultListOverline == null || faultSelectedCount == null ||
                faultSelectedCategory == null || faultSelectedTitle == null || faultSelectedMeta == null ||
                taskEditorCategoryCaption == null || taskEditorSelectionCount == null || taskEditorStatus == null ||
                faultTaskDetailTitle == null || faultTaskDetailCount == null ||
                assessmentPageStatus == null ||
                assessmentDetailRecord == null || assessmentDetailName == null ||
                assessmentDetailStudent == null || assessmentDetailClass == null ||
                assessmentDetailMajor == null || assessmentDetailProject == null ||
                assessmentDetailSummary == null || assessmentDetailDuration == null ||
                assessmentDetailScore == null || assessmentDetailStatus == null ||
                taskEditorTitleLabel == null ||
                taskEditorTarget == null || assessmentDateField == null || assessmentSearchField == null ||
                assessmentListScroll == null || assessmentDeductionScroll == null ||
                assessmentCalendarOverlay == null || assessmentCalendarPicker == null ||
                assessmentCalendarScrim == null || assessmentCalendarCloseButton == null ||
                assessmentCalendarAllButton == null || assessmentCalendarTodayButton == null ||
                assessmentCalendarCancelButton == null ||
                homePage == null || taskPage == null || faultPublishPage == null ||
                faultCategoryList == null || faultListScroll == null || taskListScroll == null ||
                faultTaskDetailList == null || assessmentPage == null ||
                assessmentDetailOverlay == null || faultTaskDetailOverlay == null || taskEditorDrawer == null ||
                taskManagementButton == null || assessmentStatisticsButton == null ||
                faultPublishPageBackButton == null ||
                faultPublishButton == null ||
                taskPageBackButton == null || taskCreateButton == null ||
                taskFilterAllButton == null || taskFilterPracticeButton == null ||
                taskFilterAssessmentButton == null || taskEditorCancelButton == null ||
                taskEditorSaveButton == null || faultTaskDetailCloseButton == null ||
                taskSummaryValueLabels.Count != 3 || assessmentSummaryValueLabels.Count != 3 ||
                taskCategoryButtons.Count != 6 || taskProjectButtons.Count < 1 ||
                taskDurationButtons.Count != 4 ||
                taskOperationButtons.Count != 2 || assessmentRows.Count < 1 ||
                assessmentDetailButtons.Count != assessmentRows.Count || assessmentPageBackButton == null ||
                assessmentDateButton == null || assessmentFilterAllButton == null ||
                assessmentFilterExcellentButton == null || assessmentFilterReviewButton == null ||
                assessmentSearchButton == null || assessmentDetailCloseButton == null ||
                assessmentDeductionItemLabels.Count < 1 ||
                assessmentDeductionValueLabels.Count != assessmentDeductionItemLabels.Count ||
                assessmentDeductionCountLabels.Count != assessmentDeductionItemLabels.Count ||
                assessmentDeductionNoteLabels.Count != assessmentDeductionItemLabels.Count ||
                logoutButton == null || quitButton == null)
            {
                return BuildProblem(
                    "Teacher Side: UI nodes do not match TeacherSideShell.uxml; assign the matching TeacherSide Display1 asset.");
            }
            if (!TryLoadFaultCatalog(out var faultCatalogIssue))
                return BuildProblem($"Teacher Side: {faultCatalogIssue}");
            if (!TryLoadTaskCatalog(out var taskCatalogIssue))
                return BuildProblem($"Teacher Side: {taskCatalogIssue}");
            if (!EnsureTaskProjectButtonCapacity(out var taskProjectIssue))
                return BuildProblem($"Teacher Side: {taskProjectIssue}");

            taskManagementButton.clicked += ShowTaskManagementPage;
            assessmentStatisticsButton.clicked += ShowAssessmentStatisticsPage;
            faultPublishPageBackButton.clicked += ReturnToPreviousPage;
            faultPublishButton.clicked += PublishSelectedFaults;
            taskPageBackButton.clicked += ReturnToTeacherHome;
            assessmentPageBackButton.clicked += ReturnToTeacherHome;
            taskCreateButton.clicked += OpenTaskEditor;
            taskFilterAllButton.clicked += ShowAllTasks;
            taskFilterPracticeButton.clicked += ShowDraftTasks;
            taskFilterAssessmentButton.clicked += ShowPublishedTasks;
            taskEditorCancelButton.clicked += CancelTaskEditor;
            taskEditorSaveButton.clicked += SaveTaskAsDraft;
            taskEditorTarget.formatListItemCallback = FormatTaskTargetChoice;
            taskEditorTarget.formatSelectedValueCallback = FormatTaskTargetChoice;
            taskEditorTarget.RegisterValueChangedCallback(OnTaskTargetChanged);
            faultTaskDetailCloseButton.clicked += CloseFaultTaskDetail;
            assessmentDateButton.clicked += ShowAssessmentDateNotice;
            assessmentDateButton.tooltip = "选择统计日期";
            assessmentDateField.tooltip = "点击右侧选择成绩日期，或输入日期后按回车查询";
            assessmentDateField.RegisterCallback<KeyDownEvent>(OnAssessmentDateKeyDown);
            assessmentCalendarScrim.clicked += CloseAssessmentCalendar;
            assessmentCalendarCloseButton.clicked += CloseAssessmentCalendar;
            assessmentCalendarCancelButton.clicked += CloseAssessmentCalendar;
            assessmentCalendarAllButton.clicked += ShowAllAssessmentDates;
            assessmentCalendarTodayButton.clicked += ShowTodayAssessmentDate;
            assessmentCalendarPicker.RegisterValueChangedCallback(OnAssessmentCalendarDateChanged);
            assessmentCalendarOverlay.RegisterCallback<KeyDownEvent>(OnAssessmentCalendarKeyDown);
            assessmentFilterAllButton.clicked += ShowAllAssessmentRecords;
            assessmentFilterExcellentButton.clicked += ShowExcellentAssessmentRecords;
            assessmentFilterReviewButton.clicked += ShowReviewAssessmentRecords;
            assessmentSearchButton.clicked += ApplyAssessmentSearch;
            assessmentDetailCloseButton.clicked += CloseAssessmentDetail;
            for (var index = 0; index < assessmentDetailButtons.Count; index++)
            {
                assessmentDetailButtons[index].userData = index;
                assessmentDetailButtons[index].RegisterCallback<ClickEvent>(OnAssessmentDetailClicked);
            }
            for (var index = 0; index < taskCategoryButtons.Count; index++)
            {
                taskCategoryButtons[index].userData = index;
                taskCategoryButtons[index].RegisterCallback<ClickEvent>(OnTaskCategoryClicked);
            }
            foreach (var button in taskProjectButtons)
                button.RegisterCallback<ClickEvent>(OnTaskProjectClicked);
            foreach (var button in taskDurationButtons)
                button.RegisterCallback<ClickEvent>(OnTaskChoiceClicked);
            foreach (var button in taskOperationButtons)
                button.RegisterCallback<ClickEvent>(OnTaskChoiceClicked);
            logoutButton.clicked += ReturnToPreviousPage;
            quitButton.clicked += YardCraneShellPanelUtility.Quit;
            foreach (var button in root.Query<Button>().ToList())
            {
                button.focusable = false;
                button.style.backgroundImage = StyleKeyword.None;
            }
            upperEventsBound = true;
            return true;
        }

        private bool BuildLowerInterface()
        {
            if (documentLower != lowerDocument)
                YardCraneShellPanelUtility.ReleasePanel(documentLower, lowerPanelTemplate, ref panelSettingsLower);
            documentLower = lowerDocument;
            if (documentLower == null)
                return BuildProblem("Teacher Side Lower (Display2): assign the scene's separate TeacherSide UIDocument.");
            if (documentLower == document || documentLower.transform.IsChildOf(document.transform) ||
                document.transform.IsChildOf(documentLower.transform))
            {
                return BuildProblem(
                    "Teacher Side Lower must reference an independent UIDocument; Display1 and Display2 cannot be the same object or nested.");
            }

            var problem = YardCraneShellPanelUtility.PrepareDocument(
                documentLower, ref lowerPanelTemplate, ref panelSettingsLower,
                "Teacher Side Lower (Display2)", out var documentReady);
            if (problem != null) return BuildProblem(problem);
            if (!documentReady) return false;
            if (rootLower == documentLower.rootVisualElement) return true;

            rootLower = documentLower.rootVisualElement;
            rootLower.style.flexGrow = 1;
            YardCraneShellPanelUtility.ReleaseOwnedObject(runtimeFontLower);
            runtimeFontLower = YardCraneShellPanelUtility.CreateShellFont();
            if (runtimeFontLower != null)
                rootLower.style.unityFontDefinition = FontDefinition.FromFont(runtimeFontLower);
            rootLower.pickingMode = PickingMode.Ignore;
            rootLower.Query<VisualElement>().ForEach(element => element.pickingMode = PickingMode.Ignore);
            return true;
        }

        private bool BuildProblem(string detail)
        {
            if (lastBuildProblem != detail)
            {
                lastBuildProblem = detail;
                Debug.LogError($"[YC_TEACHER_SIDE_SHELL_MISSING_ASSET] {detail}", this);
            }
            return false;
        }

        private void RefreshIdentity()
        {
            var displayName = profile != null ? profile.DisplayName : "管理员";
            var account = profile != null ? profile.AccountId : "admin";
            identity.text = $"{displayName} {account}  ·  本机管理";
            greeting.text = $"{displayName}，选择要进入的教学模块";
        }

        private void ShowTaskManagementPage()
        {
            taskManagementButton.EnableInClassList("selected", true);
            assessmentStatisticsButton.EnableInClassList("selected", false);
            SetPageVisible(homePage, false);
            SetPageVisible(taskPage, true);
            SetPageVisible(faultPublishPage, false);
            SetPageVisible(assessmentPage, false);
            assessmentDetailOverlay.EnableInClassList("teacher-page-hidden", true);
            faultTaskDetailOverlay.EnableInClassList("teacher-page-hidden", true);
            taskEditorDrawer.EnableInClassList("teacher-page-hidden", true);
            logoutButton.EnableInClassList("teacher-page-hidden", false);
            logoutButton.tooltip = "返回教师首页";
            currentSection.text = "任务管理";
            consoleLabel.text = "TASK MANAGEMENT / LOCAL PREVIEW";
            taskQuestionFilter = null;
            taskFilterAllButton.EnableInClassList("selected", true);
            taskFilterPracticeButton.EnableInClassList("selected", false);
            taskFilterAssessmentButton.EnableInClassList("selected", false);
            RefreshTaskRowsFromStore();
        }

        private void ShowFaultPublishPage()
        {
            taskManagementButton.EnableInClassList("selected", false);
            assessmentStatisticsButton.EnableInClassList("selected", false);
            SetPageVisible(homePage, false);
            SetPageVisible(taskPage, false);
            SetPageVisible(faultPublishPage, true);
            SetPageVisible(assessmentPage, false);
            assessmentDetailOverlay.EnableInClassList("teacher-page-hidden", true);
            faultTaskDetailOverlay.EnableInClassList("teacher-page-hidden", true);
            taskEditorDrawer.EnableInClassList("teacher-page-hidden", true);
            logoutButton.EnableInClassList("teacher-page-hidden", false);
            logoutButton.tooltip = "返回任务管理";
            currentSection.text = "故障发布";
            consoleLabel.text = "FAULT PUBLISHING / CATALOG";
            selectedFaultIds.Clear();
            var configuredCatalog =
                YardCraneStudentShell.SelectedCraneKind == YardCraneCraneKind.QuayCrane
                    ? faultCatalog.quayCrane
                    : faultCatalog.yardCrane;
            ShowFaultCatalog(configuredCatalog);
        }

        private void PublishSelectedFaults()
        {
            var selected = SelectedFaultRecordsInCatalogOrder();
            if (selected.Count == 0)
            {
                faultPublishPageStatus.text = "请至少选择一条故障题目。";
                faultPublishPageStatus.EnableInClassList("notice", true);
                return;
            }

            var questions = new List<YardCraneAssessmentFaultQuestionData>(selected.Count);
            foreach (var fault in selected)
            {
                questions.Add(new YardCraneAssessmentFaultQuestionData
                {
                    faultId = fault.Id,
                    category = fault.Category,
                    phenomenon = fault.Phenomenon,
                    faultPoint = fault.FaultPoint,
                    lineTag = fault.LineTag
                });
            }

            if (!YardCraneAssessmentQuestionBankStore.TryAddFaultTask(
                    CurrentFaultCraneType, questions, out var added, out var error))
            {
                faultPublishPageStatus.text = $"故障题目添加失败：{error}";
                faultPublishPageStatus.EnableInClassList("notice", true);
                return;
            }

            ClearFaultSelection();
            ShowTaskManagementPage();
            taskPageStatus.text =
                added != null
                    ? $"已添加 1 条故障排除任务，包含 {selected.Count} 条故障。" +
                      "新任务默认未加入本次考核，请勾选行首方框。"
                    : $"包含这 {selected.Count} 条故障的任务已在列表中，未重复添加。";
            taskPageStatus.EnableInClassList("notice", true);
            Debug.Log(
                $"[YC_TEACHER_FAULT_QUESTIONS_ADDED] crane={CurrentFaultCraneType}, " +
                $"selected={selected.Count}, added={(added != null ? 1 : 0)}",
                this);
        }

        private void ShowFaultCatalog(CraneFaultCatalog crane)
        {
            if (crane == null) return;
            selectedFaultCrane = crane;

            var categories = crane.categories ?? Array.Empty<FaultCategory>();
            var unclassified = crane.unclassifiedFaults ?? Array.Empty<FaultRecord>();
            var classifiedCount = 0;
            foreach (var category in categories)
                classifiedCount += category?.faults?.Length ?? 0;

            var totalCount = classifiedCount + unclassified.Length;
            faultSummaryCategoryValue.text = categories.Length.ToString("00");
            faultSummaryTotalValue.text = totalCount.ToString("00");
            faultSummaryUnclassifiedValue.text = unclassified.Length.ToString("00");
            faultSourceStateLabel.text =
                $"当前：{crane.craneType} · {crane.sourceSheet}/{crane.sourceWorkbook}";

            faultCategoryList.Clear();
            FaultCategoryView firstView = null;
            Button firstButton = null;
            foreach (var category in categories)
            {
                if (category == null || string.IsNullOrWhiteSpace(category.name) ||
                    category.faults == null) continue;
                var view = new FaultCategoryView(category.name, category.faults);
                var button = CreateFaultCategoryButton(view);
                faultCategoryList.Add(button);
                firstView ??= view;
                firstButton ??= button;
            }

            if (unclassified.Length > 0)
            {
                var unclassifiedName = categories.Length == 0
                    ? "全部故障（原表未分类）"
                    : "待归类";
                var view = new FaultCategoryView(unclassifiedName, unclassified);
                var button = CreateFaultCategoryButton(view);
                faultCategoryList.Add(button);
                firstView ??= view;
                firstButton ??= button;
            }

            if (firstView != null && firstButton != null)
            {
                SelectFaultCategory(firstView, firstButton);
            }
            else
            {
                faultListTitle.text = "暂无故障";
                faultListOverline.text = "FAULT LIST / 0 ITEMS";
                faultListScroll.Clear();
                ClearFaultSelection();
            }

            faultPublishPageStatus.text =
                $"已加载{crane.craneType}故障目录：共 {totalCount} 条，可按类别选择并上下滚动浏览。";
            faultPublishPageStatus.EnableInClassList("notice", false);
        }

        private Button CreateFaultCategoryButton(FaultCategoryView view)
        {
            Button button = null;
            button = new Button(() => SelectFaultCategory(view, button)) { focusable = false };
            button.AddToClassList("teacher-fault-category-button");

            var name = new Label(view.Name) { pickingMode = PickingMode.Ignore };
            var count = new Label(view.Faults.Length.ToString("00")) { pickingMode = PickingMode.Ignore };
            count.AddToClassList("teacher-fault-category-count");
            button.Add(name);
            button.Add(count);
            return button;
        }

        private void SelectFaultCategory(FaultCategoryView view, Button selectedButton)
        {
            foreach (var button in faultCategoryList.Query<Button>(className: "teacher-fault-category-button").ToList())
                button.EnableInClassList("selected", button == selectedButton);

            faultListTitle.text = view.Name;
            faultListOverline.text = $"FAULT LIST / {view.Faults.Length} ITEMS";
            faultListScroll.Clear();
            for (var index = 0; index < view.Faults.Length; index++)
            {
                var fault = view.Faults[index];
                if (fault == null) continue;
                var row = CreateFaultListRow(fault, index);
                faultListScroll.Add(row);
            }
            RefreshFaultSelectionSummary();
        }

        private Button CreateFaultListRow(FaultRecord fault, int index)
        {
            Button row = null;
            row = new Button(() => SelectFaultRecord(fault)) { focusable = false };
            row.AddToClassList("teacher-fault-list-row");
            row.userData = fault.number;
            row.EnableInClassList("selected", selectedFaultIds.Contains(fault.number));
            var displayNumber = fault.number > 0 ? fault.number : index + 1;
            row.Add(CreateFaultListLabel(displayNumber.ToString("D2"),
                "teacher-fault-col-index", "teacher-fault-row-index"));
            row.Add(CreateFaultListLabel(ValueOrDash(fault.phenomenon),
                "teacher-fault-col-phenomenon", "teacher-fault-row-primary"));
            row.Add(CreateFaultListLabel(ValueOrDash(fault.lineTag),
                "teacher-fault-col-line", "teacher-fault-row-code"));
            row.Add(CreateFaultListLabel(ValueOrDash(fault.faultPoint),
                "teacher-fault-col-point", "teacher-fault-row-code"));
            return row;
        }

        private static Label CreateFaultListLabel(string text, string columnClass, string valueClass)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(columnClass);
            label.AddToClassList(valueClass);
            return label;
        }

        private void SelectFaultRecord(FaultRecord fault)
        {
            if (!selectedFaultIds.Add(fault.number)) selectedFaultIds.Remove(fault.number);
            foreach (var row in faultListScroll.Query<Button>(className: "teacher-fault-list-row").ToList())
            {
                row.EnableInClassList(
                    "selected",
                    row.userData is int faultId && selectedFaultIds.Contains(faultId));
            }
            RefreshFaultSelectionSummary();
        }

        private void ClearFaultSelection()
        {
            selectedFaultIds.Clear();
            RefreshFaultSelectionSummary();
        }

        private List<YardCraneFaultCatalogRecord> SelectedFaultRecordsInCatalogOrder()
        {
            var selected = new List<YardCraneFaultCatalogRecord>();
            if (selectedFaultCrane == null || selectedFaultIds.Count == 0) return selected;
            foreach (var record in selectedFaultCrane.EnumerateFaults())
            {
                if (selectedFaultIds.Contains(record.Id)) selected.Add(record);
            }
            return selected;
        }

        private void RefreshFaultSelectionSummary()
        {
            var selected = SelectedFaultRecordsInCatalogOrder();
            faultSelectedCount.text = $"已选择 {selected.Count} 项";
            faultPublishButton.SetEnabled(selected.Count > 0);
            if (selected.Count == 0)
            {
                faultSelectedCategory.text = selectedFaultCrane?.craneType ?? "故障目录";
                faultSelectedTitle.text = "请选择故障题目";
                faultSelectedMeta.text = "可跨类别多选";
                return;
            }

            var first = selected[0];
            if (selected.Count > 1)
            {
                var categories = new HashSet<string>(StringComparer.Ordinal);
                foreach (var fault in selected) categories.Add(fault.Category ?? string.Empty);
                faultSelectedCategory.text = $"{selectedFaultCrane.craneType}故障排除";
                faultSelectedTitle.text = $"已选择 {selected.Count} 条故障";
                faultSelectedMeta.text = $"涉及 {categories.Count} 个类别 · 一次生成一条任务";
                return;
            }
            faultSelectedCategory.text = first.Category;
            faultSelectedTitle.text = ValueOrDash(first.Phenomenon);
            faultSelectedMeta.text =
                $"{ValueOrDash(first.LineTag)}  ·  {ValueOrDash(first.FaultPoint)}";
        }

        private bool TryLoadFaultCatalog(out string problem)
        {
            problem = null;
            if (faultCatalog != null) return true;

            var quayMode =
                YardCraneStudentShell.SelectedCraneKind == YardCraneCraneKind.QuayCrane;
            var selectedAsset = quayMode ? quayFaultCatalogAsset : yardFaultCatalogAsset;
            var craneType = quayMode ? "岸桥" : "场桥";
            var categoryNames = quayMode ? QuayFaultCategoryNames : YardFaultCategoryNames;
            if (!TryBuildFaultCatalog(
                    selectedAsset, craneType, categoryNames, out var selectedCatalog, out problem)) return false;

            faultCatalog = new FaultCatalogDocument
            {
                schemaVersion = 1,
                quayCrane = quayMode ? selectedCatalog : null,
                yardCrane = quayMode ? null : selectedCatalog
            };
            return true;
        }

        private bool TryLoadTaskCatalog(out string problem)
        {
            problem = null;
            if (taskCatalog != null) return true;

            var selectedAsset = YardCraneStudentShell.SelectedCraneKind == YardCraneCraneKind.QuayCrane
                ? quayTaskCatalogAsset
                : taskCatalogAsset;
            if (!YardCraneLegacyTaskAssetBridge.TryReadCatalog(
                    selectedAsset, out taskCatalog, out problem)) return false;

            var expectedCraneType = YardCraneStudentShell.SelectedCraneKind == YardCraneCraneKind.QuayCrane
                ? "岸桥"
                : "场桥";
            if (taskCatalog.CraneType == expectedCraneType) return true;

            problem = $"selected {expectedCraneType} mode but task asset '{selectedAsset.name}' contains {taskCatalog.CraneType} tasks.";
            taskCatalog = null;
            return false;
        }

        private void ApplyCranePresentation()
        {
            if (root == null || taskCatalog == null) return;
            var craneName = taskCatalog.CraneType;
            var englishName = craneName == "岸桥" ? "QUAY CRANE" : "YARD CRANE";
            var roleLabel = root.Q<Label>(className: "teacher-crumb-role");
            var overline = root.Q<Label>(className: "teacher-overline");
            var taskDescription = taskManagementButton?.Q<Label>(className: "teacher-card-description");
            var taskSubheading = root.Q<Label>(className: "teacher-task-subheading");
            var lowerCode = rootLower?.Q<Label>(className: "teacher-lower-code");
            var lowerSubtitle = rootLower?.Q<Label>(className: "teacher-lower-subtitle");
            if (roleLabel != null) roleLabel.text = $"{craneName}教学";
            if (consoleLabel != null) consoleLabel.text = $"{englishName} / TEACHER CONSOLE";
            if (overline != null) overline.text = $"{englishName} / TEACHING CONSOLE";
            if (taskDescription != null) taskDescription.text = $"创建、组织与发布{craneName}训练任务";
            if (taskSubheading != null)
                taskSubheading.text = $"组织{craneName}训练项目、考核目标与执行方式";
            if (lowerCode != null) lowerCode.text = $"{englishName} / TEACHING CONSOLE";
            if (lowerSubtitle != null)
                lowerSubtitle.text = $"{craneName}集装箱起重机仿真系统 · 教师端";
        }

        private bool EnsureTaskProjectButtonCapacity(out string problem)
        {
            problem = null;
            var required = 1;
            foreach (var category in new[]
                     {
                         YardCraneLegacyTaskAssetBridge.SingleCategory,
                         YardCraneLegacyTaskAssetBridge.LinkedCategory,
                         YardCraneLegacyTaskAssetBridge.WorkCategory,
                         YardCraneLegacyTaskAssetBridge.SkillCategory,
                         YardCraneLegacyTaskAssetBridge.FreeCategory
                     })
            {
                required = Math.Max(
                    required,
                    YardCraneLegacyTaskAssetBridge.TasksForCategory(taskCatalog, category).Length);
            }

            var projectPanel = taskProjectButtons[0].parent;
            if (projectPanel == null)
            {
                problem = "task project buttons have no containing panel.";
                return false;
            }

            var projectNote = projectPanel.Q<VisualElement>(className: "teacher-task-project-note");
            var insertIndex = projectNote != null
                ? projectPanel.hierarchy.IndexOf(projectNote)
                : projectPanel.childCount;
            while (taskProjectButtons.Count < required)
            {
                var button = new Button { focusable = false };
                button.AddToClassList("teacher-task-project-button");
                projectPanel.hierarchy.Insert(insertIndex++, button);
                taskProjectButtons.Add(button);
            }

            var noteLabel = projectNote?.Q<Label>();
            if (noteLabel != null)
                noteLabel.text = $"项目来自当前{taskCatalog.CraneType}训练任务信息资产";
            return true;
        }

        private static bool TryBuildFaultCatalog(
            ScriptableObject asset,
            string craneType,
            string[] categoryNames,
            out CraneFaultCatalog catalog,
            out string problem)
        {
            catalog = null;
            problem = null;
            if (asset == null)
            {
                problem = $"{craneType} fault catalog asset is not assigned.";
                return false;
            }
            var faultDictionaryField = asset.GetType().GetField("_故障");
            if (faultDictionaryField == null ||
                !(faultDictionaryField.GetValue(asset) is IDictionary sourceCatalog))
            {
                problem = $"{asset.name}.asset does not expose a readable _故障 dictionary.";
                return false;
            }

            var grouped = new Dictionary<string, List<FaultRecord>>(StringComparer.Ordinal);
            foreach (var categoryName in categoryNames)
                grouped.Add(categoryName, new List<FaultRecord>());

            var recordCount = 0;
            var usedIds = new HashSet<int>();
            foreach (DictionaryEntry source in sourceCatalog)
            {
                if (!(source.Key is int sourceId) || sourceId <= 0 ||
                    source.Value == null || !usedIds.Add(sourceId))
                {
                    problem = $"{asset.name}.asset contains an invalid fault ID or empty record.";
                    return false;
                }

                if (!TryReadFaultRecord(source.Value, out var lineTag, out var faultPoint,
                        out var phenomenon, out var category))
                {
                    problem = $"{asset.name}.asset fault {sourceId} does not match 故障信息结构.";
                    return false;
                }
                if (!grouped.TryGetValue(category ?? string.Empty, out var faults))
                {
                    problem = $"{asset.name}.asset fault {sourceId} uses unsupported category '{category}'.";
                    return false;
                }

                faults.Add(new FaultRecord
                {
                    number = sourceId,
                    lineTag = lineTag,
                    faultPoint = faultPoint,
                    phenomenon = phenomenon
                });
                recordCount++;
            }
            if (recordCount == 0)
            {
                problem = $"{asset.name}.asset contains no fault records.";
                return false;
            }

            var categories = new FaultCategory[categoryNames.Length];
            for (var index = 0; index < categoryNames.Length; index++)
            {
                var categoryName = categoryNames[index];
                var faults = grouped[categoryName];
                faults.Sort((left, right) => left.number.CompareTo(right.number));
                categories[index] = new FaultCategory
                {
                    name = categoryName,
                    faults = faults.ToArray()
                };
            }

            catalog = new CraneFaultCatalog
            {
                craneType = craneType,
                sourceWorkbook = $"{asset.name}.asset",
                sourceSheet = "Assets/Data",
                categories = categories,
                unclassifiedFaults = Array.Empty<FaultRecord>()
            };
            return true;
        }

        private static bool TryReadFaultRecord(
            object source,
            out string lineTag,
            out string faultPoint,
            out string phenomenon,
            out string category)
        {
            var sourceType = source.GetType();
            lineTag = sourceType.GetField("标号")?.GetValue(source) as string;
            faultPoint = sourceType.GetField("故障点")?.GetValue(source) as string;
            phenomenon = sourceType.GetField("故障现象")?.GetValue(source) as string;
            category = sourceType.GetField("类型")?.GetValue(source) as string;

            return sourceType.GetField("标号")?.FieldType == typeof(string) &&
                   sourceType.GetField("故障点")?.FieldType == typeof(string) &&
                   sourceType.GetField("故障现象")?.FieldType == typeof(string) &&
                   sourceType.GetField("类型")?.FieldType == typeof(string);
        }

        private static string ValueOrDash(string value) =>
            string.IsNullOrWhiteSpace(value) ? "—" : value;

        private static void SetPageVisible(VisualElement page, bool visible)
        {
            if (page == null) return;
            page.EnableInClassList("teacher-page-hidden", !visible);
            page.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static bool IsTeacherViewOpen(VisualElement view) =>
            view != null && !view.ClassListContains("teacher-page-hidden");

        private void ReturnToPreviousPage()
        {
            if (IsTeacherViewOpen(assessmentCalendarOverlay))
            {
                CloseAssessmentCalendar();
                return;
            }
            if (IsTeacherViewOpen(taskEditorDrawer))
            {
                CancelTaskEditor();
                return;
            }
            if (IsTeacherViewOpen(faultTaskDetailOverlay))
            {
                CloseFaultTaskDetail();
                return;
            }
            if (IsTeacherViewOpen(assessmentDetailOverlay))
            {
                CloseAssessmentDetail();
                return;
            }
            if (IsTeacherViewOpen(faultPublishPage))
            {
                ShowTaskManagementPage();
                return;
            }
            if (IsTeacherViewOpen(taskPage) || IsTeacherViewOpen(assessmentPage))
            {
                ReturnToTeacherHome();
                return;
            }
            if (IsTeacherViewOpen(homePage)) Logout();
        }

        private void ReturnToTeacherHome()
        {
            ResetModuleSelection();
        }

        private void ShowAllTasks()
        {
            SetTaskFilter(null, "显示全部任务；勾选行首方框即加入本次考核内容。");
        }

        private void ShowDraftTasks()
        {
            SetTaskFilter("draft", "显示尚未纳入当前试卷的题目。");
        }

        private void ShowPublishedTasks()
        {
            SetTaskFilter("included", "显示已经加入本次考核内容的任务。");
        }

        private static string CurrentFaultCraneType =>
            YardCraneStudentShell.SelectedCraneKind == YardCraneCraneKind.QuayCrane
                ? "岸桥"
                : "场桥";

        private void SetTaskFilter(string filter, string notice)
        {
            taskQuestionFilter = filter;
            RenderQuestionRows();
            taskFilterAllButton.EnableInClassList("selected", string.IsNullOrEmpty(filter));
            taskFilterPracticeButton.EnableInClassList("selected", filter == "draft");
            taskFilterAssessmentButton.EnableInClassList("selected", filter == "included");
            taskPageStatus.text = notice;
            taskPageStatus.EnableInClassList("notice", true);
        }

        private void RefreshHomeCards()
        {
            YardCraneAssessmentQuestionBankStore.TryLoadQuestionBank(
                CurrentFaultCraneType, out var bank, out _);
            var totalCount = bank?.questions?.Count ?? 0;
            var includedCount = 0;
            if (bank?.questions != null)
                foreach (var question in bank.questions) if (question.included) includedCount++;
            var taskAvailabilityLabel = taskManagementButton?.Q<Label>(className: "teacher-card-availability");
            if (taskAvailabilityLabel != null)
            {
                taskAvailabilityLabel.text = includedCount > 0
                    ? $"当前试卷 {includedCount} 题 · 本机 JSON"
                    : totalCount > 0
                        ? $"题库 {totalCount} 题 · 本次考核尚未选择"
                        : "暂无已创建试卷 · 本机文件存储";
            }

            var runCount = LoadAssessmentScoreFiles(out _).Count;
            RefreshAssessmentAvailability(runCount);
        }

        private void RefreshAssessmentAvailability(int runCount)
        {
            var assessmentAvailabilityLabel =
                assessmentStatisticsButton?.Q<Label>(className: "teacher-card-availability");
            if (assessmentAvailabilityLabel != null)
            {
                assessmentAvailabilityLabel.text = runCount > 0
                    ? $"已记录 {runCount} 条学生考核 · 成绩 JSON"
                    : "暂无学生考核记录 · 成绩 JSON";
            }
        }

        private void RefreshTaskRowsFromStore()
        {
            if (!YardCraneAssessmentQuestionBankStore.TryLoadQuestionBank(
                    CurrentFaultCraneType, out var bank, out var error))
            {
                taskListScroll.Clear();
                taskPageStatus.text = $"题库读取失败：{error}";
                taskPageStatus.EnableInClassList("notice", true);
                return;
            }

            var includedCount = 0;
            foreach (var question in bank.questions) if (question.included) includedCount++;
            var totalCount = bank.questions.Count;
            var draftCount = totalCount - includedCount;

            RenderQuestionRows(bank);
            taskSummaryValueLabels[0].text = totalCount.ToString("D2");
            taskSummaryValueLabels[1].text = includedCount.ToString("D2");
            taskSummaryValueLabels[2].text = draftCount.ToString("D2");

            taskPageStatus.text = totalCount == 0
                ? "题库为空，请点击“新建任务”添加考核题目。"
                : $"题库共 {totalCount} 题，当前试卷已勾选 {includedCount} 题；勾选变化会立即保存本机 JSON。";
            taskPageStatus.EnableInClassList("notice", true);

            RefreshHomeCards();
        }

        private void RenderQuestionRows(YardCraneAssessmentQuestionBank bank = null)
        {
            if (taskListScroll == null) return;
            taskListScroll.Clear();
            if (bank == null && !YardCraneAssessmentQuestionBankStore.TryLoadQuestionBank(
                    CurrentFaultCraneType, out bank, out var error))
            {
                var errorLabel = new Label($"题库读取失败：{error}");
                errorLabel.AddToClassList("teacher-task-list-empty");
                taskListScroll.Add(errorLabel);
                return;
            }

            var visibleCount = 0;
            foreach (var question in bank.questions)
            {
                if (taskQuestionFilter == "draft" && question.included) continue;
                if (taskQuestionFilter == "included" && !question.included) continue;
                taskListScroll.Add(CreateQuestionRow(question));
                visibleCount++;
            }

            if (visibleCount == 0)
            {
                var empty = new Label(bank.questions.Count == 0
                    ? "尚未添加考核题目。"
                    : "当前筛选条件下没有题目。");
                empty.AddToClassList("teacher-task-list-empty");
                taskListScroll.Add(empty);
            }
        }

        private VisualElement CreateQuestionRow(YardCraneAssessmentQuestion question)
        {
            var faultItems = YardCraneAssessmentQuestionBankStore.FaultItems(question);
            var isFaultTask = string.Equals(
                question.kind,
                YardCraneAssessmentQuestionBankStore.FaultKind,
                StringComparison.Ordinal);
            var row = new VisualElement();
            row.AddToClassList("teacher-task-list-row");
            row.EnableInClassList("draft", !question.included);
            row.EnableInClassList("published", question.included);

            var publishCell = new VisualElement();
            publishCell.AddToClassList("teacher-task-col-status");
            publishCell.AddToClassList("teacher-task-status-cell");
            var inclusionButton = new Button
            {
                text = question.included ? "✓" : string.Empty,
                tooltip = question.included ? "从本次考核中移除" : "加入本次考核",
                focusable = false
            };
            inclusionButton.AddToClassList("teacher-task-inclusion-checkbox");
            inclusionButton.EnableInClassList("selected", question.included);
            var publishLabel = new Label(question.included ? "已加入" : "未加入");
            publishLabel.AddToClassList("teacher-task-publish-label");
            inclusionButton.clicked += () =>
            {
                var include = !question.included;
                if (YardCraneAssessmentQuestionBankStore.TrySetIncluded(
                        CurrentFaultCraneType, question.questionId, include, out var paper, out var error))
                {
                    RefreshTaskRowsFromStore();
                    taskPageStatus.text = include
                        ? $"已加入当前试卷：{QuestionTitle(question)}；当前共 {paper.items.Count} 题。"
                        : $"已从当前试卷移除：{QuestionTitle(question)}；当前共 {paper.items.Count} 题。";
                    taskPageStatus.EnableInClassList("notice", true);
                    return;
                }

                taskPageStatus.text = $"试卷保存失败：{error}";
                taskPageStatus.EnableInClassList("notice", true);
            };
            publishCell.Add(inclusionButton);
            publishCell.Add(publishLabel);
            row.Add(publishCell);

            row.Add(CreateTaskCellLabel(
                isFaultTask ? "故障排除" : "实训",
                "teacher-task-col-type", "teacher-task-chip", "teacher-task-chip-assessment"));

            var remote = question.training?.trainingMode == 2;
            row.Add(CreateTaskCellLabel(
                remote ? "远程" : "本地",
                "teacher-task-col-mode", "teacher-task-chip",
                remote ? "teacher-task-chip-remote" : "teacher-task-chip-local"));

            row.Add(CreateTaskCellLabel(
                isFaultTask ? $"故障 {faultItems.Count} 条" : QuestionTitle(question),
                "teacher-task-col-project", "teacher-task-main-copy"));
            row.Add(CreateTaskCellLabel(
                QuestionDetail(question), "teacher-task-col-goal", "teacher-task-secondary-copy"));
            row.Add(CreateTaskCellLabel(
                question.training == null
                    ? "—"
                    : $"{Mathf.Max(1, Mathf.RoundToInt(question.training.durationSeconds / 60f))} min",
                "teacher-task-col-duration", "teacher-task-duration"));

            var actions = new VisualElement();
            actions.AddToClassList("teacher-task-col-actions");
            actions.AddToClassList("teacher-task-actions-cell");
            if (question.training != null)
            {
                var edit = new Button(() => OpenTaskEditorForEdit(question)) { text = "编辑", focusable = false };
                edit.AddToClassList("teacher-task-row-action");
                edit.AddToClassList("teacher-task-edit-action");
                actions.Add(edit);
            }
            if (isFaultTask)
            {
                var view = new Button(() => OpenFaultTaskDetail(question))
                {
                    text = "查看故障",
                    focusable = false
                };
                view.AddToClassList("teacher-task-row-action");
                view.AddToClassList("teacher-task-view-action");
                actions.Add(view);
            }
            var delete = new Button(() => DeleteQuestion(question)) { text = "删除", focusable = false };
            delete.AddToClassList("teacher-task-row-action");
            delete.AddToClassList("teacher-task-delete-action");
            actions.Add(delete);
            row.Add(actions);
            return row;
        }

        private static Label CreateTaskCellLabel(string text, params string[] classes)
        {
            var label = new Label(text ?? string.Empty) { pickingMode = PickingMode.Ignore };
            foreach (var className in classes) label.AddToClassList(className);
            return label;
        }

        private static string QuestionTitle(YardCraneAssessmentQuestion question) =>
            question.training != null
                ? $"{question.training.category} · {question.training.taskName}"
                : $"故障排除 · 故障 {YardCraneAssessmentQuestionBankStore.FaultItems(question).Count} 条";

        private static string QuestionDetail(YardCraneAssessmentQuestion question)
        {
            if (question.training != null)
                return string.IsNullOrWhiteSpace(question.training.targetDescription)
                    ? "无固定目标"
                    : question.training.targetDescription;
            var faults = YardCraneAssessmentQuestionBankStore.FaultItems(question);
            if (faults.Count == 0) return "—";
            var categories = new HashSet<string>(StringComparer.Ordinal);
            foreach (var fault in faults)
                if (fault != null) categories.Add(fault.category ?? string.Empty);
            return $"涉及 {categories.Count} 个类别 · 点击查看故障列表";
        }

        private void OpenFaultTaskDetail(YardCraneAssessmentQuestion question)
        {
            var faults = YardCraneAssessmentQuestionBankStore.FaultItems(question);
            faultTaskDetailTitle.text = "故障排除任务明细";
            faultTaskDetailCount.text = $"共 {faults.Count} 条故障";
            faultTaskDetailList.Clear();
            for (var index = 0; index < faults.Count; index++)
            {
                var fault = faults[index];
                if (fault == null) continue;
                var row = new VisualElement();
                row.AddToClassList("teacher-fault-task-detail-row");
                row.Add(CreateTaskCellLabel(
                    fault.faultId.ToString("D3"),
                    "teacher-fault-task-detail-col-index",
                    "teacher-fault-task-detail-code"));
                row.Add(CreateTaskCellLabel(
                    ValueOrDash(fault.category),
                    "teacher-fault-task-detail-col-category"));
                row.Add(CreateTaskCellLabel(
                    ValueOrDash(fault.phenomenon),
                    "teacher-fault-task-detail-col-phenomenon"));
                row.Add(CreateTaskCellLabel(
                    ValueOrDash(fault.lineTag),
                    "teacher-fault-task-detail-col-line",
                    "teacher-fault-task-detail-code"));
                row.Add(CreateTaskCellLabel(
                    ValueOrDash(fault.faultPoint),
                    "teacher-fault-task-detail-col-point",
                    "teacher-fault-task-detail-code"));
                faultTaskDetailList.Add(row);
            }
            taskEditorDrawer.EnableInClassList("teacher-page-hidden", true);
            faultTaskDetailOverlay.EnableInClassList("teacher-page-hidden", false);
        }

        private void CloseFaultTaskDetail()
        {
            faultTaskDetailOverlay.EnableInClassList("teacher-page-hidden", true);
        }

        private void DeleteQuestion(YardCraneAssessmentQuestion question)
        {
            if (question == null) return;
            if (!YardCraneAssessmentQuestionBankStore.TryDeleteQuestion(
                    CurrentFaultCraneType, question.questionId, out var error))
            {
                taskPageStatus.text = $"题目删除失败：{error}";
                taskPageStatus.EnableInClassList("notice", true);
                return;
            }
            RefreshTaskRowsFromStore();
            taskPageStatus.text = $"已删除：{QuestionTitle(question)}；当前试卷 JSON 已同步。";
            taskPageStatus.EnableInClassList("notice", true);
        }

        private void OpenTaskEditor()
        {
            editingQuestionId = null;
            editingSourceKey = null;
            editingTargetId = 0;
            ResetTaskEditorOptions();
            taskEditorTitleLabel.text = "创建训练任务";
            taskEditorSaveButton.text = "添加项目到任务列表";
            taskEditorDrawer.EnableInClassList("teacher-page-hidden", false);
            taskPageStatus.text =
                "每次选择一个训练项目并添加为一条任务；需要其他任务时可再次创建。";
            taskPageStatus.EnableInClassList("notice", true);
        }

        private void OpenTaskEditorForEdit(YardCraneAssessmentQuestion question)
        {
            if (question?.training == null) return;
            editingQuestionId = question.questionId;
            editingSourceKey = question.training.sourceKey;
            editingTargetId = Mathf.Max(0, question.training.targetId);
            selectedTaskDefinitions.Clear();
            if (!SelectTaskCategoryAndProjectByText(
                    question.training.category, question.training.taskName))
            {
                taskPageStatus.text =
                    $"无法编辑：当前任务目录中已不存在 {question.training.category} / {question.training.taskName}。";
                taskPageStatus.EnableInClassList("notice", true);
                editingQuestionId = null;
                return;
            }

            SelectDurationBySeconds(Mathf.RoundToInt(question.training.durationSeconds));
            SelectExclusive(
                taskOperationButtons,
                question.training.trainingMode == 2
                    ? taskOperationButtons[1]
                    : taskOperationButtons[0]);
            taskEditorTitleLabel.text = "编辑训练任务";
            taskEditorSaveButton.text = "保存修改";
            taskEditorDrawer.EnableInClassList("teacher-page-hidden", false);
            SetTaskEditorStatus("正在编辑当前训练题，保存后返回任务列表。");
            taskPageStatus.text =
                $"正在编辑：{question.training.category} / {question.training.taskName}；保存不会改变该题目的勾选状态。";
            taskPageStatus.EnableInClassList("notice", true);
        }

        private bool SelectTaskCategoryAndProjectByText(string category, string project)
        {
            for (var categoryIndex = 0; categoryIndex < taskCategoryButtons.Count; categoryIndex++)
            {
                var buttonCategory = taskCategoryButtons[categoryIndex].text;
                if (buttonCategory != category &&
                    !(buttonCategory == "自由训练" && category == YardCraneLegacyTaskAssetBridge.FreeCategory))
                    continue;

                SelectTaskCategory(categoryIndex);
                for (var projectIndex = 0; projectIndex < taskProjectButtons.Count; projectIndex++)
                {
                    var projectButton = taskProjectButtons[projectIndex];
                    if (!(projectButton.userData is YardCraneTaskDefinition definition) ||
                        definition.Name != project) continue;
                    selectedTaskDefinitions[TaskSelectionKey(definition)] = definition;
                    RefreshVisibleTaskProjectSelections();
                    UpdateTaskEditorSelectionSummary();
                    return true;
                }
                return false;
            }
            return false;
        }

        private void SelectDurationBySeconds(int durationSeconds)
        {
            foreach (var button in taskDurationButtons)
            {
                if (ParseDurationMinutesToSeconds(button.text) == durationSeconds)
                {
                    SelectExclusive(taskDurationButtons, button);
                    return;
                }
            }
            // No exact match (duration catalogue changed, or a non-standard saved value) - leave
            // whatever selection SelectTaskCategoryAndProjectByText's caller already applied.
        }

        private void ResetTaskEditorOptions()
        {
            selectedTaskDefinitions.Clear();
            editingTargetId = 0;
            SelectTaskCategory(0);
            SelectExclusive(taskDurationButtons, taskDurationButtons[2]);
            SelectExclusive(taskOperationButtons, taskOperationButtons[0]);
            UpdateTaskEditorSelectionSummary();
        }

        private void OnTaskCategoryClicked(ClickEvent evt)
        {
            if (!(evt.currentTarget is Button button) || !(button.userData is int categoryIndex))
                return;
            if (!string.IsNullOrEmpty(editingQuestionId) &&
                !button.ClassListContains("selected"))
            {
                taskPageStatus.text = "编辑现有题目时不能更换训练项目；可修改目标、时长和操作方式。";
                taskPageStatus.EnableInClassList("notice", true);
                return;
            }
            if (string.IsNullOrEmpty(editingQuestionId) &&
                !button.ClassListContains("selected"))
            {
                selectedTaskDefinitions.Clear();
                editingTargetId = 0;
            }
            SelectTaskCategory(categoryIndex);
        }

        private void SelectTaskCategory(int categoryIndex)
        {
            if (categoryIndex < 0 || categoryIndex >= taskCategoryButtons.Count) return;

            if (taskCategoryButtons[categoryIndex].text == "故障排除")
            {
                // 故障排除没有通用的训练项目列表可填——它对应"故障发布"页的真实故障目录，
                // 点击后直接跳转到该页（ShowFaultPublishPage内部会关闭本弹窗），
                // 不在创建任务弹窗里假填项目/目标字段。
                ShowFaultPublishPage();
                return;
            }

            SelectExclusive(taskCategoryButtons, taskCategoryButtons[categoryIndex]);
            taskEditorCategoryCaption.text =
                $"{taskCategoryButtons[categoryIndex].text} · 单选训练项目";

            var category = taskCategoryButtons[categoryIndex].text == "自由训练"
                ? YardCraneLegacyTaskAssetBridge.FreeCategory
                : taskCategoryButtons[categoryIndex].text;
            var projects = YardCraneLegacyTaskAssetBridge.TasksForCategory(taskCatalog, category);
            for (var index = 0; index < taskProjectButtons.Count; index++)
            {
                var button = taskProjectButtons[index];
                var available = index < projects.Length;
                button.style.display = available ? DisplayStyle.Flex : DisplayStyle.None;
                button.SetEnabled(available);
                button.userData = available ? projects[index] : null;
                button.text = available ? projects[index].Name : string.Empty;
                button.tooltip = available ? projects[index].Description : string.Empty;
            }

            if (projects.Length == 0)
            {
                RefreshTaskTargetOptions(null);
                taskEditorTarget.SetValueWithoutNotify("当前任务目录没有该类别。");
                return;
            }

            RefreshVisibleTaskProjectSelections();
            UpdateTaskEditorSelectionSummary();
        }

        private void OnTaskProjectClicked(ClickEvent evt)
        {
            if (!(evt.currentTarget is Button button) ||
                !(button.userData is YardCraneTaskDefinition task)) return;
            var key = TaskSelectionKey(task);
            if (string.IsNullOrEmpty(editingQuestionId) && !selectedTaskDefinitions.ContainsKey(key))
                editingTargetId = 0;
            if (!string.IsNullOrEmpty(editingQuestionId))
            {
                if (!selectedTaskDefinitions.ContainsKey(key))
                {
                    taskPageStatus.text = "编辑现有题目时不能更换训练项目；可修改目标、时长和操作方式。";
                    taskPageStatus.EnableInClassList("notice", true);
                    return;
                }
            }
            else
            {
                selectedTaskDefinitions.Clear();
                selectedTaskDefinitions[key] = task;
            }

            RefreshVisibleTaskProjectSelections();
            UpdateTaskEditorSelectionSummary();
        }

        private static string TaskSelectionKey(YardCraneTaskDefinition task) =>
            $"{YardCraneLegacyTaskAssetBridge.CategoryForTask(task)}\u001f{task?.Name}";

        private void RefreshVisibleTaskProjectSelections()
        {
            foreach (var button in taskProjectButtons)
            {
                var selected = button.userData is YardCraneTaskDefinition task &&
                               selectedTaskDefinitions.ContainsKey(TaskSelectionKey(task));
                button.EnableInClassList("selected", selected);
            }
        }

        private void UpdateTaskEditorSelectionSummary()
        {
            var count = selectedTaskDefinitions.Count;
            taskEditorSelectionCount.text = count == 0 ? "未选择" : "已选择 1 项";
            if (count == 0)
            {
                RefreshTaskTargetOptions(null);
                SetTaskEditorStatus("请选择一个项目。");
                return;
            }

            SetTaskEditorStatus("已选择 1 个项目，点击下方按钮添加到任务列表。");
            foreach (var task in selectedTaskDefinitions.Values)
            {
                RefreshTaskTargetOptions(task);
                return;
            }
        }

        private static List<string> BuildTaskTargetChoices(YardCraneTaskDefinition task)
        {
            var choices = new List<string>();
            if (task == null) return choices;
            for (var index = 0; index < task.OptionCount; index++)
                choices.Add($"{index + 1:00} · {YardCraneLegacyTaskAssetBridge.OptionLabel(task, index)}");
            return choices;
        }

        private static string FormatTaskTargetChoice(string choice)
        {
            if (string.IsNullOrEmpty(choice)) return string.Empty;
            // Keep internal choices distinct when two asset targets have the same description.
            var separator = choice.IndexOf(" · ", StringComparison.Ordinal);
            return separator > 0 && int.TryParse(choice.Substring(0, separator), out _)
                ? choice.Substring(separator + 3)
                : choice;
        }

        private void RefreshTaskTargetOptions(YardCraneTaskDefinition task)
        {
            var choices = BuildTaskTargetChoices(task);
            taskEditorTarget.choices = choices;
            taskEditorTarget.SetEnabled(choices.Count > 1);
            if (choices.Count == 0)
            {
                taskEditorTarget.SetValueWithoutNotify("请选择一个训练项目。");
                taskEditorTarget.tooltip = string.Empty;
                return;
            }
            editingTargetId = Mathf.Clamp(editingTargetId, 0, choices.Count - 1);
            taskEditorTarget.SetValueWithoutNotify(choices[editingTargetId]);
            taskEditorTarget.tooltip = FormatTaskTargetChoice(choices[editingTargetId]);
        }

        private void OnTaskTargetChanged(ChangeEvent<string> evt)
        {
            if (selectedTaskDefinitions.Count != 1 || taskEditorTarget.index < 0) return;
            editingTargetId = taskEditorTarget.index;
            taskEditorTarget.tooltip = FormatTaskTargetChoice(evt.newValue);
        }

        private void SetTaskEditorStatus(string message, bool isError = false)
        {
            if (taskEditorStatus == null) return;
            taskEditorStatus.text = message ?? string.Empty;
            taskEditorStatus.EnableInClassList("error", isError);
        }

        private void OnTaskChoiceClicked(ClickEvent evt)
        {
            if (!(evt.currentTarget is Button button)) return;
            if (taskDurationButtons.Contains(button)) SelectExclusive(taskDurationButtons, button);
            else if (taskOperationButtons.Contains(button)) SelectExclusive(taskOperationButtons, button);
        }

        private static void SelectExclusive(List<Button> buttons, Button selected)
        {
            foreach (var button in buttons)
                button.EnableInClassList("selected", button == selected);
        }

        private void CancelTaskEditor()
        {
            editingQuestionId = null;
            editingSourceKey = null;
            editingTargetId = 0;
            selectedTaskDefinitions.Clear();
            taskEditorDrawer.EnableInClassList("teacher-page-hidden", true);
            taskPageStatus.text = "已取消，未产生任何改动。";
            taskPageStatus.EnableInClassList("notice", true);
        }

        private void SaveTaskAsDraft()
        {
            var duration = SelectedText(taskDurationButtons);
            var operation = SelectedText(taskOperationButtons);
            var durationSeconds = ParseDurationMinutesToSeconds(duration);
            var trainingMode = operation == "远程操作" ? 2 : 1;
            if (selectedTaskDefinitions.Count != 1)
            {
                taskPageStatus.text = "请选择一个训练项目。";
                taskPageStatus.EnableInClassList("notice", true);
                SetTaskEditorStatus("请选择一个训练项目。", true);
                return;
            }

            if (!string.IsNullOrEmpty(editingQuestionId))
            {
                YardCraneTaskDefinition selectedTask = null;
                foreach (var task in selectedTaskDefinitions.Values)
                {
                    selectedTask = task;
                    break;
                }
                var data = CreateTrainingQuestionData(
                    selectedTask,
                    Mathf.Clamp(editingTargetId, 0, Mathf.Max(0, selectedTask.OptionCount - 1)),
                    durationSeconds,
                    trainingMode,
                    editingSourceKey);
                if (!YardCraneAssessmentQuestionBankStore.TryUpdateTrainingQuestion(
                        CurrentFaultCraneType, editingQuestionId, data, out _, out var updateError))
                {
                    taskPageStatus.text = $"训练题保存失败：{updateError}";
                    taskPageStatus.EnableInClassList("notice", true);
                    SetTaskEditorStatus($"保存失败：{updateError}", true);
                    return;
                }

                var title = $"{data.category} / {data.taskName}";
                editingQuestionId = null;
                editingSourceKey = null;
                editingTargetId = 0;
                selectedTaskDefinitions.Clear();
                taskEditorDrawer.EnableInClassList("teacher-page-hidden", true);
                RefreshTaskRowsFromStore();
                taskPageStatus.text = $"已保存修改：{title}；该题目的发布勾选状态保持不变。";
                taskPageStatus.EnableInClassList("notice", true);
                return;
            }

            YardCraneTaskDefinition taskToAdd = null;
            foreach (var task in selectedTaskDefinitions.Values)
            {
                taskToAdd = task;
                break;
            }
            var additions = new List<YardCraneAssessmentTrainingQuestionData>
            {
                CreateTrainingQuestionData(
                    taskToAdd,
                    Mathf.Clamp(editingTargetId, 0, Mathf.Max(0, taskToAdd.OptionCount - 1)),
                    durationSeconds,
                    trainingMode)
            };
            if (!YardCraneAssessmentQuestionBankStore.TryAddTrainingQuestions(
                    CurrentFaultCraneType, additions, out var added, out var addError))
            {
                taskPageStatus.text = $"训练题添加失败：{addError}";
                taskPageStatus.EnableInClassList("notice", true);
                SetTaskEditorStatus($"添加失败：{addError}", true);
                return;
            }

            selectedTaskDefinitions.Clear();
            editingTargetId = 0;
            taskEditorDrawer.EnableInClassList("teacher-page-hidden", true);
            RefreshTaskRowsFromStore();
            taskPageStatus.text = added.Length > 0
                ? "已添加 1 道训练任务。新任务默认未加入本次考核，请勾选行首方框。"
                : "该训练项目已在任务列表中，未重复添加。";
            taskPageStatus.EnableInClassList("notice", true);
        }

        private YardCraneAssessmentTrainingQuestionData CreateTrainingQuestionData(
            YardCraneTaskDefinition task,
            int targetId,
            float durationSeconds,
            int trainingMode,
            string sourceKey = null)
        {
            var fields = task?.TargetFields ?? Array.Empty<YardCraneTaskTargetField>();
            string FieldTitle(int index) => index < fields.Length ? fields[index].Title : string.Empty;
            string FieldValue(int index) => index < fields.Length &&
                                            targetId >= 0 && targetId < fields[index].Values.Length
                ? fields[index].Values[targetId]
                : string.Empty;
            var category = YardCraneLegacyTaskAssetBridge.CategoryForTask(task);
            return new YardCraneAssessmentTrainingQuestionData
            {
                sourceKey = string.IsNullOrWhiteSpace(sourceKey)
                    ? $"{taskCatalog.ProjectName}\u001f{category}\u001f{task.Name}\u001f{Guid.NewGuid():N}"
                    : sourceKey,
                trainingProject = taskCatalog.ProjectName,
                category = category,
                taskName = task.Name,
                taskType = task.TaskType,
                targetId = targetId,
                targetDescription = YardCraneLegacyTaskAssetBridge.OptionLabel(task, targetId),
                taskDescription = task.Description,
                field1Title = FieldTitle(0),
                field2Title = FieldTitle(1),
                field3Title = FieldTitle(2),
                field1Content = FieldValue(0),
                field2Content = FieldValue(1),
                field3Content = FieldValue(2),
                durationSeconds = durationSeconds,
                trainingMode = trainingMode,
                operationMode = trainingMode == 2 ? "远程操作" : "本地操作"
            };
        }

        // durationText is one of "5 min"/"10 min"/"15 min"/"20 min" (see
        // TeacherSideShell.uxml's teacher-task-duration-choice buttons). Falls back to 10
        // minutes if the leading number cannot be parsed, rather than throwing - a malformed
        // duration should not block saving the rest of the assignment.
        private static int ParseDurationMinutesToSeconds(string durationText)
        {
            var firstToken = durationText != null ? durationText.Split(' ')[0] : null;
            return int.TryParse(firstToken, out var minutes) && minutes > 0 ? minutes * 60 : 600;
        }

        private static string SelectedText(List<Button> buttons)
        {
            foreach (var button in buttons)
                if (button.ClassListContains("selected")) return button.text;
            return "未选择";
        }

        private void ShowAssessmentStatisticsPage()
        {
            CloseAssessmentCalendar();
            taskManagementButton.EnableInClassList("selected", false);
            assessmentStatisticsButton.EnableInClassList("selected", true);
            SetPageVisible(homePage, false);
            SetPageVisible(taskPage, false);
            SetPageVisible(faultPublishPage, false);
            SetPageVisible(assessmentPage, true);
            taskEditorDrawer.EnableInClassList("teacher-page-hidden", true);
            assessmentDetailOverlay.EnableInClassList("teacher-page-hidden", true);
            logoutButton.EnableInClassList("teacher-page-hidden", false);
            logoutButton.tooltip = "返回教师首页";
            currentSection.text = "考核统计";
            consoleLabel.text = "ASSESSMENT ANALYTICS / LOCAL SCORE FILES";
            assessmentSearchField.value = string.Empty;
            assessmentSelectedDate = string.Empty;
            assessmentDateField.SetValueWithoutNotify(string.Empty);
            RefreshAssessmentRowsFromStore();
        }

        private void RefreshAssessmentRowsFromStore()
        {
            var records = LoadAssessmentScoreFiles(out assessmentLoadNotice);
            assessmentPreviewDetails.Clear();
            foreach (var detail in records)
                if (string.IsNullOrEmpty(assessmentSelectedDate) || detail.Date == assessmentSelectedDate)
                    assessmentPreviewDetails.Add(detail);

            EnsureAssessmentRowCapacity(assessmentPreviewDetails.Count);
            populatedAssessmentRowCount = assessmentPreviewDetails.Count;
            for (var index = 0; index < populatedAssessmentRowCount; index++)
                PopulateAssessmentRow(assessmentRows[index], assessmentPreviewDetails[index]);
            assessmentListScroll.scrollOffset = Vector2.zero;

            var excellentCount = 0;
            var passCount = 0;
            var reviewCount = 0;
            foreach (var detail in assessmentPreviewDetails)
            {
                if (detail.Status == "优秀") excellentCount++;
                else if (detail.Status == "合格") passCount++;
                else if (detail.Status == "待复核") reviewCount++;
            }
            var count = populatedAssessmentRowCount;
            assessmentSummaryValueLabels[0].text = count.ToString("D2");
            assessmentSummaryValueLabels[1].text = count == 0
                ? "—"
                : $"{Mathf.RoundToInt((excellentCount + passCount) / (float)count * 100f)}%";
            assessmentSummaryValueLabels[2].text = excellentCount.ToString("D2");
            assessmentFilterAllButton.text = $"全部记录  {count:D2}";
            assessmentFilterExcellentButton.text = $"优秀  {excellentCount:D2}";
            assessmentFilterReviewButton.text = $"待复核  {reviewCount:D2}";
            SetAssessmentFilter(null, null);
            RefreshAssessmentAvailability(records.Count);
        }

        private static List<AssessmentPreviewDetail> LoadAssessmentScoreFiles(out string notice)
        {
            var records = new List<AssessmentPreviewDetail>();
            var nullRecords = 0;
            var problems = new List<string>();
            var folder = Path.Combine(Application.streamingAssetsPath, "成绩");
            if (!Directory.Exists(folder))
            {
                notice = "成绩目录不存在，暂无考核记录。";
                return records;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly);
                Array.Sort(files, (left, right) => string.CompareOrdinal(right, left));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                notice = "成绩目录读取失败：" + exception.Message;
                return records;
            }

            foreach (var file in files)
            {
                try
                {
                    var document = JsonUtility.FromJson<AssessmentScoreFile>(File.ReadAllText(file));
                    if (document?.data == null)
                        throw new FormatException("缺少 data 成绩数组");
                    var fileName = Path.GetFileName(file);
                    var date = DateTime.TryParseExact(Path.GetFileNameWithoutExtension(file), "yyyyMMdd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate)
                        ? parsedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                        : string.Empty;
                    // No per-record timestamp is stored; later appended records are shown first.
                    for (var index = document.data.Length - 1; index >= 0; index--)
                    {
                        var record = document.data[index];
                        if (record == null)
                        {
                            nullRecords++;
                            continue;
                        }
                        records.Add(BuildAssessmentDetail(record, fileName, index, date));
                    }
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                                                   exception is ArgumentException || exception is FormatException)
                {
                    problems.Add(Path.GetFileName(file));
                }
            }

            notice = $"已读取 {files.Length} 个成绩文件";
            if (nullRecords > 0) notice += $"，忽略 {nullRecords} 个 null 数据项";
            if (problems.Count > 0) notice += "；读取失败：" + string.Join("、", problems);
            return records;
        }

        private static AssessmentPreviewDetail BuildAssessmentDetail(
            AssessmentScoreRecord record, string fileName, int index, string date)
        {
            var status = string.IsNullOrWhiteSpace(record.项目) || string.IsNullOrWhiteSpace(record.用时) ||
                         !IsValidAssessmentScore(record.成绩)
                ? "待复核"
                : record.成绩 >= AssessmentExcellentScore ? "优秀"
                : record.成绩 >= AssessmentPassingScore ? "合格" : "不合格";
            var items = new List<string>();
            var values = new List<string>();
            var counts = new List<string>();
            var notes = new List<string>();
            if (record.详情列表 != null)
            {
                foreach (var deduction in record.详情列表)
                {
                    if (deduction == null || (string.IsNullOrWhiteSpace(deduction.扣分项) && deduction.分值 == 0)) continue;
                    items.Add(AssessmentText(deduction.扣分项));
                    values.Add(double.IsNaN(deduction.分值) || double.IsInfinity(deduction.分值)
                        ? "—" : (-Math.Abs(deduction.分值)).ToString("0.##", CultureInfo.InvariantCulture));
                    // The JSON contains neither occurrence counts nor explanatory notes.
                    counts.Add("—");
                    notes.Add("—");
                }
            }
            var deductionCount = items.Count;
            if (deductionCount == 0)
            {
                items.Add("无扣分明细");
                values.Add("—");
                counts.Add("—");
                notes.Add("—");
            }
            var dateText = string.IsNullOrEmpty(date) ? "日期未标明" : date;
            var summary = $"考核日期：{dateText} · 扣分明细 {deductionCount} 项";
            if (status == "待复核") summary += "；项目、用时或分数需核对";
            return new AssessmentPreviewDetail(
                $"{fileName} / {index + 1}", AssessmentText(record.姓名), AssessmentText(record.学号),
                AssessmentText(record.班级), AssessmentText(record.专业), AssessmentText(record.项目),
                AssessmentText(record.用时), IsValidAssessmentScore(record.成绩)
                    ? record.成绩.ToString("0.##", CultureInfo.InvariantCulture) : "—",
                status, summary, items.ToArray(), values.ToArray(), counts.ToArray(), notes.ToArray(), date);
        }

        private static bool IsValidAssessmentScore(double score) =>
            !double.IsNaN(score) && !double.IsInfinity(score) && score >= 0 && score <= 100;

        private static string AssessmentText(string value) => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

        // Repeat the asset's row layout, preserving its USS classes rather than inventing another style.
        private static VisualElement CopyAssessmentRow(VisualElement template)
        {
            VisualElement copy = template is Button button ? new Button { text = button.text }
                : template is Label label ? new Label(label.text) : new VisualElement();
            foreach (var className in template.GetClasses())
                if (className != "teacher-assessment-list-row-last" && className != "teacher-assessment-detail-rule-row-last")
                    copy.AddToClassList(className);
            copy.tooltip = template.tooltip;
            if (!(template is Label) && !(template is Button))
                foreach (var child in template.Children()) copy.Add(CopyAssessmentRow(child));
            return copy;
        }

        private void EnsureAssessmentRowCapacity(int count)
        {
            while (assessmentRows.Count < count)
            {
                var row = CopyAssessmentRow(assessmentRows[0]);
                var button = row.Q<Button>(className: "teacher-assessment-detail-button");
                button.userData = assessmentRows.Count;
                button.RegisterCallback<ClickEvent>(OnAssessmentDetailClicked);
                assessmentRows.Add(row);
                assessmentDetailButtons.Add(button);
                assessmentListScroll.Add(row);
            }
        }

        private static void PopulateAssessmentRow(VisualElement row, AssessmentPreviewDetail detail)
        {
            var excellent = detail.Status == "优秀";
            var passed = detail.Status == "合格";
            var failed = detail.Status == "不合格";
            var review = detail.Status == "待复核";
            row.EnableInClassList("teacher-assessment-grade-excellent", excellent);
            row.EnableInClassList("teacher-assessment-grade-passed", passed);
            row.EnableInClassList("teacher-assessment-grade-failed", failed);
            row.EnableInClassList("teacher-assessment-grade-review", review);

            var nameLabel = row.Q<Label>(className: "teacher-assessment-col-name");
            if (nameLabel != null) nameLabel.text = detail.Name;
            var majorLabel = row.Q<Label>(className: "teacher-assessment-col-major");
            if (majorLabel != null) majorLabel.text = detail.Major;
            var classLabel = row.Q<Label>(className: "teacher-assessment-col-class");
            if (classLabel != null) classLabel.text = detail.ClassName;
            var studentLabel = row.Q<Label>(className: "teacher-assessment-col-student");
            if (studentLabel != null) studentLabel.text = detail.StudentId;
            var projectLabel = row.Q<Label>(className: "teacher-assessment-col-project");
            if (projectLabel != null) projectLabel.text = detail.Project;
            var durationLabel = row.Q<Label>(className: "teacher-assessment-col-duration");
            if (durationLabel != null) durationLabel.text = detail.Duration;

            var scoreCell = row.Q<VisualElement>(className: "teacher-assessment-score-cell");
            var scoreValueLabel = scoreCell?.Q<Label>(className: "teacher-assessment-score-value");
            if (scoreValueLabel != null) scoreValueLabel.text = detail.Score;
            var scoreChipLabel = scoreCell?.Q<Label>(className: "teacher-assessment-score-chip");
            if (scoreChipLabel != null)
            {
                scoreChipLabel.text = detail.Status;
                scoreChipLabel.EnableInClassList("teacher-assessment-score-excellent", excellent);
                scoreChipLabel.EnableInClassList("teacher-assessment-score-passed", passed);
                scoreChipLabel.EnableInClassList("teacher-assessment-score-failed", failed);
                scoreChipLabel.EnableInClassList("teacher-assessment-score-review", review);
            }
        }

        private void ShowAllAssessmentRecords()
        {
            assessmentSearchField.value = string.Empty;
            SetAssessmentFilter(null, null);
        }

        private void ShowExcellentAssessmentRecords()
        {
            assessmentSearchField.value = string.Empty;
            SetAssessmentFilter("teacher-assessment-grade-excellent", null);
        }

        private void ShowReviewAssessmentRecords()
        {
            assessmentSearchField.value = string.Empty;
            SetAssessmentFilter("teacher-assessment-grade-review", null);
        }

        private void SetAssessmentFilter(string requiredClass, string notice)
        {
            assessmentRequiredClass = requiredClass;
            assessmentFilterAllButton.EnableInClassList("selected", string.IsNullOrEmpty(requiredClass));
            assessmentFilterExcellentButton.EnableInClassList(
                "selected", requiredClass == "teacher-assessment-grade-excellent");
            assessmentFilterReviewButton.EnableInClassList(
                "selected", requiredClass == "teacher-assessment-grade-review");
            RefreshAssessmentRows(notice);
        }

        private void ApplyAssessmentSearch()
        {
            RefreshAssessmentRows(null);
        }

        private void RefreshAssessmentRows(string notice)
        {
            var keyword = assessmentSearchField.value == null
                ? string.Empty
                : assessmentSearchField.value.Trim();
            var visibleCount = 0;
            for (var index = 0; index < assessmentRows.Count; index++)
            {
                var row = assessmentRows[index];
                // Hide unused asset rows after reload; they must never show stale records.
                if (index >= populatedAssessmentRowCount)
                {
                    row.style.display = DisplayStyle.None;
                    continue;
                }

                var matchesGrade = string.IsNullOrEmpty(assessmentRequiredClass) ||
                                   row.ClassListContains(assessmentRequiredClass);
                var matchesKeyword = string.IsNullOrEmpty(keyword) || AssessmentRowContains(row, keyword);
                var visible = matchesGrade && matchesKeyword;
                row.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (visible) visibleCount++;
            }

            var dateText = string.IsNullOrEmpty(assessmentSelectedDate) ? "全部日期" : assessmentSelectedDate;
            assessmentPageStatus.text = string.IsNullOrEmpty(notice)
                ? $"{dateText} · 显示 {visibleCount}/{populatedAssessmentRowCount} 条考核记录；{assessmentLoadNotice}"
                : notice;
            assessmentPageStatus.EnableInClassList("notice", true);
        }

        private static bool AssessmentRowContains(VisualElement row, string keyword)
        {
            foreach (var label in row.Query<Label>().ToList())
            {
                if (!string.IsNullOrEmpty(label.text) &&
                    label.text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private void ShowAssessmentDateNotice()
        {
            var dates = AvailableAssessmentDates(LoadAssessmentScoreFiles(out _));
            var markedDates = new List<DateTime>(dates.Count);
            foreach (var date in dates)
                if (DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var recordedDate))
                    markedDates.Add(recordedDate);
            assessmentCalendarPicker.SetMarkedDates(markedDates);
            DateTime? selectedDate = null;
            if (TryNormalizeAssessmentDate(assessmentDateField.value, out var input) &&
                !string.IsNullOrEmpty(input))
                selectedDate = DateTime.ParseExact(input, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var initialDate = selectedDate ?? DateTime.Today;
            if (!selectedDate.HasValue)
            {
                if (dates.Count > 0)
                    initialDate = DateTime.ParseExact(dates[0], "yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            assessmentCalendarPicker.CurrentDate = initialDate;
            assessmentCalendarPicker.SetValueWithoutNotify(selectedDate);
            assessmentCalendarOverlay.EnableInClassList("teacher-page-hidden", false);
            assessmentCalendarOverlay.Focus();
        }

        private void CloseAssessmentCalendar()
        {
            assessmentCalendarOverlay?.EnableInClassList("teacher-page-hidden", true);
        }

        private void OnAssessmentCalendarDateChanged(ChangeEvent<DateTime?> evt)
        {
            if (evt.newValue.HasValue)
                ApplyAssessmentDate(evt.newValue.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            else
                CloseAssessmentCalendar();
            evt.StopPropagation();
        }

        private void ShowAllAssessmentDates() => ApplyAssessmentDate(string.Empty);

        private void ShowTodayAssessmentDate() =>
            ApplyAssessmentDate(DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        private void OnAssessmentCalendarKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape) return;
            CloseAssessmentCalendar();
            evt.StopPropagation();
        }

        private static List<string> AvailableAssessmentDates(IEnumerable<AssessmentPreviewDetail> records)
        {
            var distinctDates = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var record in records)
                if (!string.IsNullOrEmpty(record.Date)) distinctDates.Add(record.Date);
            var dates = new List<string>(distinctDates);
            dates.Reverse();
            return dates;
        }

        private void OnAssessmentDateKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
            ApplyAssessmentDate(assessmentDateField.value);
            evt.StopPropagation();
        }

        private static bool TryNormalizeAssessmentDate(string input, out string date)
        {
            date = (input ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(date)) return true;
            if (!DateTime.TryParseExact(date, new[] { "yyyy-MM-dd", "yyyyMMdd" },
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate)) return false;
            date = parsedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return true;
        }

        private void ApplyAssessmentDate(string input)
        {
            if (!TryNormalizeAssessmentDate(input, out var date))
            {
                assessmentPageStatus.text = "日期格式应为 yyyy-MM-dd 或 yyyyMMdd；留空查询全部日期。";
                assessmentPageStatus.EnableInClassList("notice", true);
                return;
            }
            assessmentSelectedDate = date;
            CloseAssessmentCalendar();
            assessmentDateField.SetValueWithoutNotify(date);
            assessmentSearchField.value = string.Empty;
            assessmentDetailOverlay.EnableInClassList("teacher-page-hidden", true);
            RefreshAssessmentRowsFromStore();
        }

        private void OnAssessmentDetailClicked(ClickEvent evt)
        {
            if (evt.currentTarget is Button button && button.userData is int detailIndex)
                OpenAssessmentDetail(detailIndex);
        }

        private void EnsureAssessmentDeductionCapacity(int count)
        {
            while (assessmentDeductionItemLabels.Count < count)
            {
                var row = CopyAssessmentRow(assessmentDeductionItemLabels[0].parent);
                assessmentDeductionScroll.Add(row);
                assessmentDeductionItemLabels.Add(row.Q<Label>(className: "teacher-assessment-detail-rule-item"));
                assessmentDeductionValueLabels.Add(row.Q<Label>(className: "teacher-assessment-detail-rule-value"));
                assessmentDeductionCountLabels.Add(row.Q<Label>(className: "teacher-assessment-detail-rule-count"));
                assessmentDeductionNoteLabels.Add(row.Q<Label>(className: "teacher-assessment-detail-rule-note"));
            }
        }

        private void OpenAssessmentDetail(int detailIndex)
        {
            if (detailIndex < 0 || detailIndex >= assessmentPreviewDetails.Count) return;
            var detail = assessmentPreviewDetails[detailIndex];
            assessmentDetailRecord.text = $"RECORD / {detail.RecordId}";
            assessmentDetailName.text = detail.Name;
            assessmentDetailStudent.text = detail.StudentId;
            assessmentDetailClass.text = detail.ClassName;
            assessmentDetailMajor.text = detail.Major;
            assessmentDetailProject.text = detail.Project;
            assessmentDetailSummary.text = detail.Summary;
            assessmentDetailDuration.text = detail.Duration;
            assessmentDetailScore.text = detail.Score;
            assessmentDetailStatus.text = detail.Status;
            SetAssessmentDetailResultStyle(detail.Status);

            EnsureAssessmentDeductionCapacity(detail.DeductionItems.Length);
            for (var index = 0; index < assessmentDeductionItemLabels.Count; index++)
            {
                var visible = index < detail.DeductionItems.Length;
                assessmentDeductionItemLabels[index].parent.style.display =
                    visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (!visible) continue;
                assessmentDeductionItemLabels[index].text = detail.DeductionItems[index];
                assessmentDeductionValueLabels[index].text = detail.DeductionValues[index];
                assessmentDeductionCountLabels[index].text = detail.DeductionCounts[index];
                assessmentDeductionNoteLabels[index].text = detail.DeductionNotes[index];
            }
            assessmentDeductionScroll.scrollOffset = Vector2.zero;

            assessmentDetailOverlay.EnableInClassList("teacher-page-hidden", false);
            assessmentPageStatus.text =
                $"正在查看 {detail.Name} 的成绩详情 · {detail.RecordId}";
            assessmentPageStatus.EnableInClassList("notice", true);
        }

        private void SetAssessmentDetailResultStyle(string status)
        {
            var excellent = status == "优秀";
            var passed = status == "合格";
            var failed = status == "不合格";
            var review = status == "待复核";
            assessmentDetailScore.EnableInClassList("teacher-assessment-detail-score-excellent", excellent);
            assessmentDetailScore.EnableInClassList("teacher-assessment-detail-score-passed", passed);
            assessmentDetailScore.EnableInClassList("teacher-assessment-detail-score-failed", failed);
            assessmentDetailScore.EnableInClassList("teacher-assessment-detail-score-review", review);
            assessmentDetailStatus.EnableInClassList("teacher-assessment-detail-verdict-excellent", excellent);
            assessmentDetailStatus.EnableInClassList("teacher-assessment-detail-verdict-passed", passed);
            assessmentDetailStatus.EnableInClassList("teacher-assessment-detail-verdict-failed", failed);
            assessmentDetailStatus.EnableInClassList("teacher-assessment-detail-verdict-review", review);
        }

        private void CloseAssessmentDetail()
        {
            assessmentDetailOverlay.EnableInClassList("teacher-page-hidden", true);
            RefreshAssessmentRows(null);
        }

        private void ResetModuleSelection()
        {
            CloseAssessmentCalendar();
            taskManagementButton.EnableInClassList("selected", false);
            assessmentStatisticsButton.EnableInClassList("selected", false);
            SetPageVisible(homePage, true);
            SetPageVisible(taskPage, false);
            SetPageVisible(faultPublishPage, false);
            SetPageVisible(assessmentPage, false);
            assessmentDetailOverlay.EnableInClassList("teacher-page-hidden", true);
            faultTaskDetailOverlay.EnableInClassList("teacher-page-hidden", true);
            taskEditorDrawer.EnableInClassList("teacher-page-hidden", true);
            logoutButton.EnableInClassList("teacher-page-hidden", false);
            logoutButton.tooltip = "返回登录";
            currentSection.text = "教学管理";
            consoleLabel.text = taskCatalog?.CraneType == "岸桥"
                ? "QUAY CRANE / TEACHER CONSOLE"
                : "YARD CRANE / TEACHER CONSOLE";
            taskFilterAllButton.EnableInClassList("selected", true);
            taskFilterPracticeButton.EnableInClassList("selected", false);
            taskFilterAssessmentButton.EnableInClassList("selected", false);
            assessmentRequiredClass = null;
            assessmentSearchField.value = string.Empty;
            foreach (var row in assessmentRows) row.style.display = DisplayStyle.None;
            assessmentFilterAllButton.EnableInClassList("selected", true);
            assessmentFilterExcellentButton.EnableInClassList("selected", false);
            assessmentFilterReviewButton.EnableInClassList("selected", false);
            assessmentPageStatus.text =
                "打开考核统计后读取 StreamingAssets/成绩 中的 JSON。";
            assessmentPageStatus.EnableInClassList("notice", false);
            taskPageStatus.text = "任务题库与当前试卷保存在本机 JSON；勾选题目后学生端即可读取。";
            taskPageStatus.EnableInClassList("notice", false);
            faultPublishPageStatus.text = "可跨类别多选故障；一次添加生成一条故障排除任务";
            faultPublishPageStatus.EnableInClassList("notice", false);
            moduleStatus.text = "本机管理员模式 · 任务试卷使用本机 JSON";
            moduleStatus.EnableInClassList("notice", false);
            // 2026-09-17: runs every time the home page becomes visible (initial BuildInterface
            // and every "返回" back to it), not just after a publish - covers the case where the
            // teacher navigates home, then back into task management, without revisiting the
            // sub-pages in between.
            RefreshHomeCards();
        }

        private void Logout()
        {
            if (!Application.isPlaying) return;
            var loginSceneName = (LoginSceneName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(loginSceneName) || !Application.CanStreamedLevelBeLoaded(loginSceneName))
            {
                Debug.LogError(
                    $"[YC_TEACHER_HOME_LOGIN_SCENE_MISSING] '{loginSceneName}' is not in Build Settings.",
                    this);
                return;
            }
            profile = null;
            YardCraneTeacherHomeHandoff.Clear();
            YardCraneFallbackSession.Clear();
            YardCraneTrainingSessionIdentity.Clear();
            SceneManager.LoadSceneAsync(loginSceneName, LoadSceneMode.Single);
        }

        private void OnDisable()
        {
            ReleaseInterface();
        }

        private void UnbindEvents()
        {
            upperEventsBound = false;
            if (taskManagementButton != null) taskManagementButton.clicked -= ShowTaskManagementPage;
            if (assessmentStatisticsButton != null)
                assessmentStatisticsButton.clicked -= ShowAssessmentStatisticsPage;
            if (faultPublishPageBackButton != null)
                faultPublishPageBackButton.clicked -= ReturnToPreviousPage;
            if (faultPublishButton != null)
                faultPublishButton.clicked -= PublishSelectedFaults;
            if (taskPageBackButton != null) taskPageBackButton.clicked -= ReturnToTeacherHome;
            if (assessmentPageBackButton != null) assessmentPageBackButton.clicked -= ReturnToTeacherHome;
            if (taskCreateButton != null) taskCreateButton.clicked -= OpenTaskEditor;
            if (taskFilterAllButton != null) taskFilterAllButton.clicked -= ShowAllTasks;
            if (taskFilterPracticeButton != null) taskFilterPracticeButton.clicked -= ShowDraftTasks;
            if (taskFilterAssessmentButton != null) taskFilterAssessmentButton.clicked -= ShowPublishedTasks;
            if (taskEditorCancelButton != null) taskEditorCancelButton.clicked -= CancelTaskEditor;
            if (taskEditorSaveButton != null) taskEditorSaveButton.clicked -= SaveTaskAsDraft;
            if (taskEditorTarget != null) taskEditorTarget.UnregisterValueChangedCallback(OnTaskTargetChanged);
            if (faultTaskDetailCloseButton != null)
                faultTaskDetailCloseButton.clicked -= CloseFaultTaskDetail;
            if (assessmentDateButton != null) assessmentDateButton.clicked -= ShowAssessmentDateNotice;
            if (assessmentDateField != null)
                assessmentDateField.UnregisterCallback<KeyDownEvent>(OnAssessmentDateKeyDown);
            if (assessmentCalendarScrim != null) assessmentCalendarScrim.clicked -= CloseAssessmentCalendar;
            if (assessmentCalendarCloseButton != null) assessmentCalendarCloseButton.clicked -= CloseAssessmentCalendar;
            if (assessmentCalendarCancelButton != null) assessmentCalendarCancelButton.clicked -= CloseAssessmentCalendar;
            if (assessmentCalendarAllButton != null) assessmentCalendarAllButton.clicked -= ShowAllAssessmentDates;
            if (assessmentCalendarTodayButton != null) assessmentCalendarTodayButton.clicked -= ShowTodayAssessmentDate;
            if (assessmentCalendarPicker != null)
                assessmentCalendarPicker.UnregisterValueChangedCallback(OnAssessmentCalendarDateChanged);
            if (assessmentCalendarOverlay != null)
                assessmentCalendarOverlay.UnregisterCallback<KeyDownEvent>(OnAssessmentCalendarKeyDown);
            if (assessmentFilterAllButton != null)
                assessmentFilterAllButton.clicked -= ShowAllAssessmentRecords;
            if (assessmentFilterExcellentButton != null)
                assessmentFilterExcellentButton.clicked -= ShowExcellentAssessmentRecords;
            if (assessmentFilterReviewButton != null)
                assessmentFilterReviewButton.clicked -= ShowReviewAssessmentRecords;
            if (assessmentSearchButton != null) assessmentSearchButton.clicked -= ApplyAssessmentSearch;
            if (assessmentDetailCloseButton != null)
                assessmentDetailCloseButton.clicked -= CloseAssessmentDetail;
            foreach (var button in assessmentDetailButtons)
                button.UnregisterCallback<ClickEvent>(OnAssessmentDetailClicked);
            foreach (var button in taskCategoryButtons)
                button.UnregisterCallback<ClickEvent>(OnTaskCategoryClicked);
            foreach (var button in taskProjectButtons)
                button.UnregisterCallback<ClickEvent>(OnTaskProjectClicked);
            foreach (var button in taskDurationButtons)
                button.UnregisterCallback<ClickEvent>(OnTaskChoiceClicked);
            foreach (var button in taskOperationButtons)
                button.UnregisterCallback<ClickEvent>(OnTaskChoiceClicked);
            if (logoutButton != null) logoutButton.clicked -= ReturnToPreviousPage;
            if (quitButton != null) quitButton.clicked -= YardCraneShellPanelUtility.Quit;
        }

        private void ReleaseInterface()
        {
            built = false;
            UnbindEvents();
            if (root != null) root.style.unityFontDefinition = StyleKeyword.Null;
            if (rootLower != null) rootLower.style.unityFontDefinition = StyleKeyword.Null;
            YardCraneShellPanelUtility.ReleasePanel(document, panelTemplate, ref panelSettings);
            YardCraneShellPanelUtility.ReleasePanel(documentLower, lowerPanelTemplate, ref panelSettingsLower);
            root = null;
            rootLower = null;
            taskCategoryButtons.Clear();
            taskProjectButtons.Clear();
            taskDurationButtons.Clear();
            taskOperationButtons.Clear();
            selectedTaskDefinitions.Clear();
            editingQuestionId = null;
            editingSourceKey = null;
            editingTargetId = 0;
            taskQuestionFilter = null;
            taskCatalog = null;
            faultCatalog = null;
            selectedFaultCrane = null;
            selectedFaultIds.Clear();
            assessmentRows.Clear();
            assessmentDetailButtons.Clear();
            assessmentDeductionItemLabels.Clear();
            assessmentDeductionValueLabels.Clear();
            assessmentDeductionCountLabels.Clear();
            assessmentDeductionNoteLabels.Clear();
            assessmentRequiredClass = null;
            assessmentCalendarOverlay = null;
            assessmentCalendarPicker = null;
            YardCraneShellPanelUtility.ReleaseOwnedObject(runtimeFont);
            YardCraneShellPanelUtility.ReleaseOwnedObject(runtimeFontLower);
            runtimeFont = null;
            runtimeFontLower = null;
            document = null;
            documentLower = null;
        }

#if UNITY_EDITOR
        public void EditorSuspendInterface()
        {
            editorSuspended = true;
            ReleaseInterface();
        }

        public void EditorResumeInterface()
        {
            editorSuspended = false;
            nextBuildAt = 0;
        }

        public void EditorRefreshPreviewLayout()
        {
            if (Application.isPlaying || editorSuspended || !isActiveAndEnabled) return;
            if (!IsReady) BuildInterface();
            if (!IsReady) return;
            RefreshIdentity();
            root.style.display = DisplayStyle.Flex;
            rootLower.style.display = DisplayStyle.Flex;
            root.MarkDirtyRepaint();
        }
#endif
    }

}
