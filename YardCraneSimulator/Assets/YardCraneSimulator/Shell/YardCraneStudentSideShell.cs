using PortTraining.YardCraneSimulator.Training;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace PortTraining.YardCraneSimulator.Shell
{
    // The post-login student screen (mode selection + learning training), living in its
    // own StudentHome scene. Split out of YardCraneStudentShell (StudentEntry scene) on
    // 2026-09-08; originally as a second UIDocument coexisting in the same scene, then
    // the same day moved to a real separate scene at the user's request, since a genuine
    // scene switch is simpler than keeping two documents mutually exclusive via
    // display:none in one scene (Unity already tears down/rebuilds everything on a scene
    // load, so there is no coexistence problem to manage by hand). A student login in
    // StudentEntry hands the profile through the static YardCraneStudentHomeHandoff holder
    // and loads this scene. Direct scene entry creates a local student profile so the home
    // scene can also start training without first visiting StudentEntry. Logout() uses the
    // scene-configured login destination.
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class YardCraneStudentSideShell : MonoBehaviour
    {
        public const string Version = "2026-10-10-yc-student-home-v36-eleven-fault-categories";
        public const string SceneName = "StudentHome";
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

        private sealed class CraneFaultCatalog
        {
            public string craneType;
            public string sourceWorkbook;
            public string sourceSheet;
            public FaultCategory[] categories;
        }

        private sealed class FaultCategory
        {
            public string name;
            public FaultRecord[] faults;
        }

        private sealed class FaultRecord
        {
            public int number;
            public string lineTag;
            public string faultPoint;
            public string phenomenon;
        }

        private UIDocument document;
        // The UIDocument keeps using this Inspector-assigned folder asset directly.
        // The hidden reference also lets validation recover it after a domain reload.
        [SerializeField, HideInInspector] private PanelSettings panelTemplate;
        [InspectorName("登录场景")]
        [Tooltip("Scene loaded only when the student explicitly clicks logout. Leave empty to disable logout navigation.")]
        public string LoginSceneName = "StudentEntry";
        [InspectorName("场桥实训场景")]
        [Tooltip("Scene loaded when a Yard Crane student starts learning, free practice or assessment.")]
        public string YardCraneSceneName = "YardCrane";
        [InspectorName("岸桥实训场景")]
        [Tooltip("Scene loaded when a Quay Crane student starts learning, free practice or assessment.")]
        public string QuayCraneSceneName = "港口场景";
        [SerializeField]
        [Tooltip("当前学生端使用的任务目录；场桥场景绑定场桥任务信息.asset，岸桥场景绑定岸桥任务信息.asset。")]
        private ScriptableObject taskCatalogAsset;
        [SerializeField]
        [Tooltip("岸桥任务目录；登录入口选择岸桥后使用岸桥任务信息.asset。")]
        private ScriptableObject quayTaskCatalogAsset;
        [SerializeField]
        [Tooltip("进入实训场景前写入的任务状态资产。")]
        private ScriptableObject taskStateAsset;
        [SerializeField]
        [Tooltip("岸桥故障目录；登录入口配置为岸桥时读取岸桥故障信息.asset。")]
        private ScriptableObject quayFaultCatalogAsset;
        [SerializeField]
        [Tooltip("场桥故障目录；登录入口配置为场桥时读取场桥故障信息.asset。")]
        private ScriptableObject yardFaultCatalogAsset;
        private PanelSettings panelSettings;
        private Font runtimeFont;
        private VisualElement root;

        [SerializeField]
        [Tooltip("This scene's own Display2/lower companion screen's UIDocument. Must " +
                 "live on a GameObject that is NOT a child of this one - same nested-" +
                 "UIDocument constraint as YardCraneStudentShell's lowerDocument.")]
        private UIDocument lowerDocument;
        private UIDocument documentLower;
        [SerializeField, HideInInspector] private PanelSettings lowerPanelTemplate;
        private PanelSettings panelSettingsLower;
        private Font runtimeFontLower;
        private VisualElement rootLower;

        private VisualElement studentHomePage;
        private VisualElement studentAssessmentPage;
        private VisualElement studentLearningPage;
        private Label greeting;
        private Label identity;
        private Label learningIdentity;
        private Label launchStatus;
        private Button logoutButton;
        private Button launchButton;
        private Button quitButton;
        private Button learningModeButton;
        private Button assessmentModeButton;
        private Button assessmentBackButton;
        private Label assessmentPaperTitle;
        private Label assessmentPaperCount;
        private Label assessmentPaperStatus;
        private ScrollView assessmentQuestionList;
        private YardCraneAssessmentPaper currentAssessmentPaper;
        private Button learningBackButton;
        private Button learningBackCrumb;
        private Button learningCloseButton;
        private Button drillGantryStart;
        private Button drillTrolleyStart;
        private Button drillHoistStart;
        private Button drillTiltStart;
        private Button drillPitchStart;
        private Label learningLaunchStatus;
        private Button learningNavSingle;
        private Button learningNavLink;
        private Button learningNavWork;
        private Button learningNavSkill;
        private Button learningNavFault;
        private Button learningScopeLocal;
        private Button learningScopeRemote;

        // 2026-09-11: 联动操作 (manual §5.2.5-8) + 能力提升 first item (manual §5.2.9). These
        // rows sit alongside singleDrillRow inside student-learning-page and are toggled via
        // ShowSingleTraining/ShowLinkedTraining/ShowSkillTraining - see SetLearningCategory.
        private VisualElement singleDrillRow;
        private VisualElement linkedDrillRow;
        private VisualElement workDrillRow;
        private VisualElement skillDrillRow;
        private VisualElement faultDrillRow;
        private Label faultCategoryHeading;
        private VisualElement faultCategoryGrid;
        private VisualElement faultDetailView;
        private Button faultDetailBackButton;
        private Label faultDetailTitle;
        private Label faultDetailCount;
        private Label faultDetailNotice;
        private VisualElement faultDetailList;
        private Button faultSimulationButton;
        private CraneFaultCatalog faultCatalog;
        private YardCraneCraneKind? loadedFaultCatalogKind;
        private string selectedFaultCategoryName = string.Empty;
        private YardCraneTaskCatalog taskCatalog;
        private Button drillLinkedGantryTrolleyStart;
        private Button drillLinkedGantryHoistStart;
        private Button drillLinkedTrolleyHoistStart;
        private Button drillLinkedGantryTrolleyHoistStart;
        private Button drillSkillGantryStraightStart;
        private readonly System.Collections.Generic.List<Button> dynamicTaskStartButtons =
            new System.Collections.Generic.List<Button>();

        private sealed class CatalogTaskButtonBinding
        {
            public YardCraneTaskDefinition Task;
            public VisualElement Options;
        }
        private bool upperEventsBound;

        // Set from YardCraneStudentHomeHandoff.Consume() in Awake(), before BuildInterface()
        // ever runs - see Awake()'s comment for what happens when there is nothing to consume.
        private YardCraneLocalStudentProfile profile;
        private YardCraneLocalTrainingReturnPage requestedInitialPage =
            YardCraneLocalTrainingReturnPage.StudentHome;
        private string requestedNotice = string.Empty;
        private bool initialPageApplied;
        private bool showing;
        private bool built;
        private bool building;
        private bool editorSuspended;
        private double nextBuildAt;
        private string lastBuildProblem;
        private System.Guid returnedTrainingSessionId;

        public bool IsReady => built && isActiveAndEnabled && !editorSuspended &&
                               document != null && document.isActiveAndEnabled &&
                               document.rootVisualElement == root && root != null &&
                               document.panelSettings == panelSettings &&
                               root.Q<Button>("logout-button") == logoutButton &&
                               documentLower != null && documentLower.isActiveAndEnabled &&
                               documentLower.rootVisualElement == rootLower && rootLower != null &&
                               documentLower.panelSettings == panelSettingsLower && lowerDocument == documentLower;
        public bool IsShowing => showing;
        public YardCraneStudentSidePage Page { get; private set; } = YardCraneStudentSidePage.Home;
        public VisualElement VisualRoot => root;
        public UIDocument Document => document;
        public VisualElement VisualRootLower => rootLower;
        public UIDocument DocumentLower => documentLower;

        private void Awake()
        {
            if (!Application.isPlaying) return;
            profile = YardCraneStudentHomeHandoff.Consume(out requestedInitialPage, out requestedNotice);
            var returnedContext = YardCraneFallbackSession.CurrentContext;
            if (returnedContext != null)
            {
                returnedTrainingSessionId = returnedContext.SessionId;
                // An external operational scene may return with a plain LoadScene call and
                // therefore cannot populate StudentHomeHandoff. Preserve the student from the
                // still-live session before Start closes that completed/abandoned run.
                profile ??= YardCraneFallbackSession.Student;
            }
            if (profile == null)
            {
                // StudentHome may be the build's first scene. Give that direct-entry path a
                // valid local student identity so all three training entrances can reuse the
                // same task-state and session handoff used after a normal login.
                if (!YardCraneLocalStudentProfile.TryCreate(
                        "local-student",
                        "本地学员",
                        out profile,
                        out var profileError))
                {
                    Debug.LogError(
                        $"[YC_STUDENT_HOME_LOCAL_PROFILE_FAILED] {profileError}",
                        this);
                    return;
                }

                Debug.Log(
                    "[YC_STUDENT_HOME_LOCAL_PROFILE] Direct StudentHome entry is using " +
                    "the local student identity.",
                    this);
            }
        }

        private void OnEnable()
        {
            nextBuildAt = 0;
            BuildInterface();
        }

        // Mirrors YardCraneStudentShell.Start(): UIDocument.OnEnable order relative to this
        // ExecuteAlways component is not guaranteed, so retry once after scene components
        // have finished initialising.
        private void Start()
        {
            FinalizeReturnedTrainingSession();
            if (!isActiveAndEnabled || IsReady) return;
            BuildInterface();
        }

        private void FinalizeReturnedTrainingSession()
        {
            if (!Application.isPlaying || returnedTrainingSessionId == System.Guid.Empty) return;

            var sessionId = returnedTrainingSessionId;
            returnedTrainingSessionId = System.Guid.Empty;
            var navigationReleased = YardCraneLocalTrainingNavigationBridge.TryReleaseAtStudentHome(sessionId);
            var sessionCleared = YardCraneFallbackSession.TryClear(sessionId);
            var identityCleared = false;
            if (YardCraneTrainingSessionIdentity.SessionId == sessionId)
            {
                YardCraneTrainingSessionIdentity.Clear();
                identityCleared = true;
            }

            Debug.Log(
                $"[YC_STUDENT_HOME_RETURN_FINALIZED] sessionId={sessionId:D}, " +
                $"navigationReleased={navigationReleased}, sessionCleared={sessionCleared}, " +
                $"identityCleared={identityCleared}",
                this);
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
                if (!BuildUpperInterface()) return;
                if (!BuildLowerInterface()) return;

                // This scene's own entry point - shown by default like the login shell's
                // login page, not hidden-until-shown (that made sense when this coexisted
                // in StudentEntry; a dedicated scene has no "wrong screen showing" problem
                // to guard against).
                RefreshIdentity();
                SetStatus(launchStatus, string.Empty);
                SetStatus(learningLaunchStatus, string.Empty);
                var initialPage = initialPageApplied
                    ? (Page == YardCraneStudentSidePage.Learning
                        ? YardCraneStudentSidePage.Learning
                        : Page == YardCraneStudentSidePage.Assessment
                            ? YardCraneStudentSidePage.Assessment
                            : YardCraneStudentSidePage.Home)
                    : (requestedInitialPage == YardCraneLocalTrainingReturnPage.LearningSelection
                        ? YardCraneStudentSidePage.Learning
                        : YardCraneStudentSidePage.Home);
                Page = initialPage;
                SetPage(initialPage);
                if (initialPage == YardCraneStudentSidePage.Assessment)
                    RenderAssessmentQuestions(currentAssessmentPaper);
                if (!string.IsNullOrEmpty(requestedNotice))
                {
                    SetStatus(
                        initialPage == YardCraneStudentSidePage.Learning
                            ? learningLaunchStatus
                            : launchStatus,
                        requestedNotice,
                        true);
                    requestedNotice = string.Empty;
                }
                initialPageApplied = true;
                requestedInitialPage = YardCraneLocalTrainingReturnPage.StudentHome;
                showing = true;
                root.style.display = DisplayStyle.Flex;
                rootLower.style.display = DisplayStyle.Flex;
                built = true;
                lastBuildProblem = null;
                if (Application.isPlaying)
                    Debug.Log($"[YC_STUDENT_SIDE_SHELL_READY] version={Version}, studentNumber={(profile != null ? profile.AccountId : "(editor-preview)")}", this);
            }
            finally { building = false; }
        }

        private bool BuildProblem(string detail)
        {
            if (lastBuildProblem != detail)
            {
                lastBuildProblem = detail;
                Debug.LogError($"[YC_STUDENT_SIDE_SHELL_MISSING_ASSET] {detail}", this);
            }
            return false;
        }

        private bool BuildUpperInterface()
        {
            document = GetComponent<UIDocument>();
            var problem = YardCraneShellPanelUtility.PrepareDocument(
                document, ref panelTemplate, ref panelSettings, "Student Side (Display1)", out var docReady);
            if (problem != null) return BuildProblem(problem);
            if (!docReady) return false;
            if (upperEventsBound && root == document.rootVisualElement && logoutButton != null &&
                root.Q<Button>("logout-button") == logoutButton) return true;
            UnbindEvents();

            root = document.rootVisualElement;
            root.style.flexGrow = 1;
            YardCraneShellPanelUtility.ReleaseOwnedObject(runtimeFont);
            runtimeFont = YardCraneShellPanelUtility.CreateShellFont();
            if (runtimeFont != null) root.style.unityFontDefinition = FontDefinition.FromFont(runtimeFont);

            studentHomePage = root.Q("student-home-page");
            studentAssessmentPage = root.Q("student-assessment-page");
            studentLearningPage = root.Q("student-learning-page");
            greeting = root.Q<Label>("greeting");
            identity = root.Q<Label>("identity");
            learningIdentity = root.Q<Label>("learning-identity");
            launchStatus = root.Q<Label>("launch-status");
            logoutButton = root.Q<Button>("logout-button");
            launchButton = root.Q<Button>("launch-button");
            quitButton = root.Q<Button>("quit-button");
            learningModeButton = root.Q<Button>("learning-mode-button");
            assessmentModeButton = root.Q<Button>("assessment-mode-button");
            assessmentBackButton = root.Q<Button>("assessment-back-button");
            assessmentPaperTitle = root.Q<Label>("assessment-paper-title");
            assessmentPaperCount = root.Q<Label>("assessment-paper-count");
            assessmentPaperStatus = root.Q<Label>("assessment-paper-status");
            assessmentQuestionList = root.Q<ScrollView>("assessment-question-list");
            learningBackButton = root.Q<Button>("learning-back-button");
            learningBackCrumb = root.Q<Button>("learning-back-crumb");
            learningCloseButton = root.Q<Button>("learning-close-button");
            drillGantryStart = root.Q<Button>("drill-gantry-start");
            drillTrolleyStart = root.Q<Button>("drill-trolley-start");
            drillHoistStart = root.Q<Button>("drill-hoist-start");
            drillTiltStart = root.Q<Button>("drill-tilt-start");
            drillPitchStart = root.Q<Button>("drill-pitch-start");
            learningLaunchStatus = root.Q<Label>("learning-launch-status");
            learningNavSingle = root.Q<Button>("learning-nav-single");
            learningNavLink = root.Q<Button>("learning-nav-link");
            learningNavWork = root.Q<Button>("learning-nav-work");
            learningNavSkill = root.Q<Button>("learning-nav-skill");
            learningNavFault = root.Q<Button>("learning-nav-fault");
            learningScopeLocal = root.Q<Button>("learning-scope-local");
            learningScopeRemote = root.Q<Button>("learning-scope-remote");
            singleDrillRow = root.Q("single-drill-row");
            linkedDrillRow = root.Q("linked-drill-row");
            skillDrillRow = root.Q("skill-drill-row");
            faultDrillRow = root.Q("fault-drill-row");
            faultCategoryHeading = root.Q<Label>("fault-category-heading");
            faultCategoryGrid = root.Q("fault-category-grid");
            faultDetailView = root.Q("fault-detail-view");
            faultDetailBackButton = root.Q<Button>("fault-detail-back-button");
            faultDetailTitle = root.Q<Label>("fault-detail-title");
            faultDetailCount = root.Q<Label>("fault-detail-count");
            faultDetailNotice = root.Q<Label>("fault-detail-notice");
            faultDetailList = root.Q("fault-detail-list");
            faultSimulationButton = root.Q<Button>("fault-simulation-button");
            drillLinkedGantryTrolleyStart = root.Q<Button>("drill-linked-gantry-trolley-start");
            drillLinkedGantryHoistStart = root.Q<Button>("drill-linked-gantry-hoist-start");
            drillLinkedTrolleyHoistStart = root.Q<Button>("drill-linked-trolley-hoist-start");
            drillLinkedGantryTrolleyHoistStart = root.Q<Button>("drill-linked-gantry-trolley-hoist-start");
            drillSkillGantryStraightStart = root.Q<Button>("drill-skill-gantry-straight-start");
            if (studentHomePage == null || studentAssessmentPage == null ||
                studentLearningPage == null || identity == null ||
                learningIdentity == null || launchStatus == null || learningLaunchStatus == null ||
                logoutButton == null || launchButton == null || quitButton == null ||
                learningModeButton == null || assessmentModeButton == null || assessmentBackButton == null ||
                assessmentPaperTitle == null || assessmentPaperCount == null ||
                assessmentPaperStatus == null || assessmentQuestionList == null || learningBackButton == null ||
                learningBackCrumb == null || learningCloseButton == null || drillGantryStart == null ||
                drillTrolleyStart == null || drillHoistStart == null || drillTiltStart == null ||
                drillPitchStart == null || learningNavSingle == null || learningNavLink == null ||
                learningNavWork == null || learningNavSkill == null || learningNavFault == null ||
                learningScopeLocal == null || learningScopeRemote == null ||
                singleDrillRow == null || linkedDrillRow == null || skillDrillRow == null ||
                faultDrillRow == null ||
                faultCategoryHeading == null || faultCategoryGrid == null || faultDetailView == null ||
                faultDetailBackButton == null || faultDetailTitle == null || faultDetailCount == null ||
                faultDetailNotice == null || faultDetailList == null || faultSimulationButton == null ||
                drillLinkedGantryTrolleyStart == null || drillLinkedGantryHoistStart == null ||
                drillLinkedTrolleyHoistStart == null || drillLinkedGantryTrolleyHoistStart == null)
                return BuildProblem("Student Side: UI nodes do not match StudentSideShell.uxml; reload the matching student-side UI asset.");
            if (!TryLoadFaultCatalog(out var faultCatalogIssue))
                return BuildProblem($"Student Side: {faultCatalogIssue}");
            if (!TryLoadTaskCatalog(out var taskCatalogIssue))
                return BuildProblem($"Student Side: {taskCatalogIssue}");
            if (taskStateAsset == null)
                return BuildProblem("Student Side: assign Assets/Data/任务State.asset.");
            ApplyCranePresentation();
            if (!PopulateFaultCategoryUi(out var faultCategoryUiIssue))
                return BuildProblem($"Student Side: {faultCategoryUiIssue}");
            PopulateTaskCatalogUi();

            logoutButton.clicked += Logout;
            launchButton.clicked += LaunchTraining;
            quitButton.clicked += YardCraneShellPanelUtility.Quit;
            learningModeButton.clicked += OpenLearningTraining;
            assessmentModeButton.clicked += OpenAssessment;
            assessmentBackButton.clicked += CloseAssessment;
            learningBackButton.clicked += CloseLearningTraining;
            learningBackCrumb.clicked += CloseLearningTraining;
            learningCloseButton.clicked += YardCraneShellPanelUtility.Quit;
            learningNavSingle.clicked += ShowSingleTraining;
            learningNavLink.clicked += ShowLinkedTraining;
            learningNavWork.clicked += ShowWorkTraining;
            learningNavSkill.clicked += ShowSkillTraining;
            learningNavFault.clicked += ShowFaultTraining;
            foreach (var button in faultDrillRow.Query<Button>(className: "fault-card").ToList())
                button.RegisterCallback<ClickEvent>(OnFaultCardClicked);
            faultDetailBackButton.clicked += ShowFaultCategoryList;
            faultSimulationButton.clicked += StartFaultSimulation;
            learningScopeLocal.clicked += SelectLocalScope;
            learningScopeRemote.clicked += SelectRemoteScope;
            foreach (var button in root.Query<Button>().ToList()) button.focusable = false;
            upperEventsBound = true;
            return true;
        }

        private bool BuildLowerInterface()
        {
            if (documentLower != lowerDocument)
                YardCraneShellPanelUtility.ReleasePanel(documentLower, lowerPanelTemplate, ref panelSettingsLower);
            documentLower = lowerDocument;
            if (documentLower == null)
            {
                return BuildProblem("Lower (Display2): assign Lower Document to this scene's own lower screen UIDocument.");
            }
            if (documentLower == document || documentLower.transform.IsChildOf(document.transform) ||
                document.transform.IsChildOf(documentLower.transform))
            {
                return BuildProblem("Lower Document must reference an independent UIDocument; the screens cannot be the same object or nested.");
            }
            var problem = YardCraneShellPanelUtility.PrepareDocument(
                documentLower, ref lowerPanelTemplate, ref panelSettingsLower, "Student Side Lower (Display2)", out var docReady);
            if (problem != null) return BuildProblem(problem);
            if (!docReady) return false;
            if (rootLower == documentLower.rootVisualElement) return true;

            rootLower = documentLower.rootVisualElement;
            rootLower.style.flexGrow = 1;

            YardCraneShellPanelUtility.ReleaseOwnedObject(runtimeFontLower);
            runtimeFontLower = YardCraneShellPanelUtility.CreateShellFont();
            if (runtimeFontLower != null) rootLower.style.unityFontDefinition = FontDefinition.FromFont(runtimeFontLower);

            rootLower.pickingMode = PickingMode.Ignore;
            rootLower.Query<VisualElement>().ForEach(element => element.pickingMode = PickingMode.Ignore);
            return true;
        }

        private bool PopulateFaultCategoryUi(out string problem)
        {
            problem = null;
            var categories = faultCatalog?.categories;
            var buttons = faultCategoryGrid?.Query<Button>(className: "fault-card").ToList();
            if (categories == null || buttons == null || buttons.Count != categories.Length)
            {
                problem =
                    $"fault category UI contains {buttons?.Count ?? 0} cards, but the current catalog requires {categories?.Length ?? 0}.";
                return false;
            }

            for (var index = 0; index < categories.Length; index++)
            {
                var category = categories[index];
                var button = buttons[index];
                var eyebrow = button.Q<Label>(className: "fault-card-eyebrow");
                var title = button.Q<Label>(className: "fault-card-title");
                var note = button.Q<Label>(className: "fault-card-note");
                if (category == null || eyebrow == null || title == null || note == null)
                {
                    problem = $"fault category card {index + 1} does not match StudentSideShell.uxml.";
                    return false;
                }

                var faultCount = category.faults?.Length ?? 0;
                eyebrow.text = $"{index + 1:00}  /  {FaultCategoryCode(category.name)}";
                title.text = category.name;
                note.text = $"{FaultCategoryNote(category.name)} · {faultCount}项";
            }
            return true;
        }

        private static string FaultCategoryCode(string categoryName)
        {
            switch (categoryName)
            {
                case "电源故障": return "POWER";
                case "起升机构": return "HOIST";
                case "大车机构": return "GANTRY";
                case "小车机构": return "TROLLEY";
                case "导板机构": return "GUIDE BOARD";
                case "俯仰机构": return "PITCH";
                case "前后倾机构": return "FORE-AFT TILT";
                case "平面回转机构": return "ROTATION";
                case "左右倾机构": return "SIDE TILT";
                case "夹轮器": return "WHEEL CLAMP";
                case "吊具泵": return "SPREADER PUMP";
                case "元器件损坏": return "COMPONENT";
                default: return "FAULT";
            }
        }

        private static string FaultCategoryNote(string categoryName)
        {
            switch (categoryName)
            {
                case "电源故障": return "电源回路与供电异常";
                case "起升机构": return "起升动作与限位异常";
                case "大车机构": return "大车行走与限位异常";
                case "小车机构": return "小车行走与限位异常";
                case "导板机构": return "导板动作与限位异常";
                case "俯仰机构": return "俯仰动作与限位异常";
                case "前后倾机构": return "吊具前后倾动作异常";
                case "平面回转机构": return "吊具平面回转异常";
                case "左右倾机构": return "吊具左右倾动作异常";
                case "夹轮器": return "夹轮器动作异常";
                case "吊具泵": return "吊具泵及液压动作异常";
                case "元器件损坏": return "控制与限位元器件损坏";
                default: return "故障训练项目";
            }
        }

        private void PopulateTaskCatalogUi()
        {
            var singleTasks = YardCraneLegacyTaskAssetBridge.TasksForCategory(
                taskCatalog, YardCraneLegacyTaskAssetBridge.SingleCategory);
            ConfigureExistingTaskCard(drillGantryStart, "drill-gantry-options", TaskAt(singleTasks, 0));
            ConfigureExistingTaskCard(drillTrolleyStart, "drill-trolley-options", TaskAt(singleTasks, 1));
            ConfigureExistingTaskCard(drillHoistStart, "drill-hoist-options", TaskAt(singleTasks, 2));
            ConfigureExistingTaskCard(drillTiltStart, "drill-tilt-options", TaskAt(singleTasks, 3));
            ConfigureExistingTaskCard(drillPitchStart, "drill-pitch-options", TaskAt(singleTasks, 4));
            AppendOverflowTaskCards(singleDrillRow, singleTasks, 5, "SINGLE");

            var linkedTasks = YardCraneLegacyTaskAssetBridge.TasksForCategory(
                taskCatalog, YardCraneLegacyTaskAssetBridge.LinkedCategory);
            ConfigureExistingTaskCard(
                drillLinkedGantryTrolleyStart,
                "drill-linked-gantry-trolley-options",
                TaskAt(linkedTasks, 0));
            ConfigureExistingTaskCard(
                drillLinkedGantryHoistStart,
                "drill-linked-gantry-hoist-options",
                TaskAt(linkedTasks, 1));
            ConfigureExistingTaskCard(
                drillLinkedTrolleyHoistStart,
                "drill-linked-trolley-hoist-options",
                TaskAt(linkedTasks, 2));
            ConfigureExistingTaskCard(
                drillLinkedGantryTrolleyHoistStart,
                "drill-linked-gantry-trolley-hoist-options",
                TaskAt(linkedTasks, 3));
            AppendOverflowTaskCards(linkedDrillRow, linkedTasks, 4, "LINKED");

            var learningRows = skillDrillRow.parent;
            if (workDrillRow == null || workDrillRow.parent != learningRows)
            {
                workDrillRow = new VisualElement { name = "work-drill-row" };
                workDrillRow.AddToClassList("learning-drill-row");
                var skillIndex = learningRows.hierarchy.IndexOf(skillDrillRow);
                learningRows.hierarchy.Insert(Mathf.Max(0, skillIndex), workDrillRow);
            }
            workDrillRow.style.display = DisplayStyle.None;

            RebuildDynamicTaskRow(
                workDrillRow,
                YardCraneLegacyTaskAssetBridge.TasksForCategory(
                    taskCatalog, YardCraneLegacyTaskAssetBridge.WorkCategory),
                "WORKFLOW");
            RebuildDynamicTaskRow(
                skillDrillRow,
                YardCraneLegacyTaskAssetBridge.TasksForCategory(
                    taskCatalog, YardCraneLegacyTaskAssetBridge.SkillCategory),
                "SKILL");
            learningNavWork.RemoveFromClassList("disabled-module");
            learningNavWork.SetEnabled(workDrillRow.childCount > 0);
        }

        private static YardCraneTaskDefinition TaskAt(YardCraneTaskDefinition[] tasks, int index) =>
            tasks != null && index >= 0 && index < tasks.Length ? tasks[index] : null;

        private void ConfigureExistingTaskCard(
            Button startButton,
            string optionsName,
            YardCraneTaskDefinition task)
        {
            var card = startButton?.parent;
            if (card == null) return;
            card.style.display = task != null ? DisplayStyle.Flex : DisplayStyle.None;
            startButton.userData = null;
            if (task == null) return;

            var title = card.Q<Label>(className: "drill-card-title");
            var description = card.Q<Label>(className: "drill-card-field-label");
            var options = card.Q<VisualElement>(optionsName);
            if (title != null) title.text = task.Name;
            if (description != null) description.text = task.Description;
            PopulateTaskOptions(options, task);
            startButton.userData = new CatalogTaskButtonBinding { Task = task, Options = options };
            startButton.RegisterCallback<ClickEvent>(OnCatalogTaskClicked);
            dynamicTaskStartButtons.Add(startButton);
        }

        private void AppendOverflowTaskCards(
            VisualElement row,
            YardCraneTaskDefinition[] tasks,
            int firstOverflowIndex,
            string eyebrow)
        {
            for (var index = firstOverflowIndex; index < tasks.Length; index++)
                row.Add(CreateDynamicTaskCard(tasks[index], index, tasks.Length, eyebrow));
        }

        private void RebuildDynamicTaskRow(
            VisualElement row,
            YardCraneTaskDefinition[] tasks,
            string eyebrow)
        {
            row.Clear();
            for (var index = 0; index < tasks.Length; index++)
                row.Add(CreateDynamicTaskCard(tasks[index], index, tasks.Length, eyebrow));
        }

        private VisualElement CreateDynamicTaskCard(
            YardCraneTaskDefinition task,
            int index,
            int count,
            string eyebrow)
        {
            var card = new VisualElement();
            card.AddToClassList("drill-card");
            if (index == count - 1) card.AddToClassList("drill-card-last");

            var head = new VisualElement();
            head.AddToClassList("drill-card-head");
            var number = new Label((index + 1).ToString("00"));
            number.AddToClassList("drill-card-index");
            var eyebrowLabel = new Label(eyebrow);
            eyebrowLabel.AddToClassList("drill-card-eyebrow");
            head.Add(number);
            head.Add(eyebrowLabel);
            card.Add(head);

            var title = new Label(task.Name);
            title.AddToClassList("drill-card-title");
            card.Add(title);
            var description = new Label(task.Description);
            description.AddToClassList("drill-card-field-label");
            card.Add(description);

            var options = new VisualElement();
            options.AddToClassList("drill-options");
            PopulateTaskOptions(options, task);
            card.Add(options);

            var spacer = new VisualElement();
            spacer.AddToClassList("drill-card-spacer");
            card.Add(spacer);

            var startButton = new Button { text = "开始  →", focusable = false };
            startButton.AddToClassList("drill-start-button");
            startButton.userData = new CatalogTaskButtonBinding { Task = task, Options = options };
            startButton.RegisterCallback<ClickEvent>(OnCatalogTaskClicked);
            dynamicTaskStartButtons.Add(startButton);
            card.Add(startButton);
            return card;
        }

        private static void PopulateTaskOptions(VisualElement options, YardCraneTaskDefinition task)
        {
            if (options == null) return;
            options.Clear();
            for (var index = 0; index < task.OptionCount; index++)
            {
                var toggle = new Toggle
                {
                    text = YardCraneLegacyTaskAssetBridge.OptionLabel(task, index),
                    value = index == 0
                };
                toggle.AddToClassList("drill-option");
                options.Add(toggle);
            }
            BindExclusiveToggleGroup(options);
        }

        private void OnCatalogTaskClicked(ClickEvent evt)
        {
            if (!(evt.currentTarget is Button button) ||
                !(button.userData is CatalogTaskButtonBinding binding)) return;
            StartCatalogTask(binding.Task, binding.Options);
        }

        private void Update()
        {
            if (editorSuspended) return;
            if (!IsReady) BuildInterface();
            if (!IsReady) return;
        }

        private void Logout()
        {
            if (!Application.isPlaying) return;
            if (Page == YardCraneStudentSidePage.Launching) return;
            var loginSceneName = LoginSceneName?.Trim();
            if (string.IsNullOrEmpty(loginSceneName))
            {
                SetStatus(launchStatus, "未配置登录场景，已取消返回。", true);
                return;
            }
            if (!Application.CanStreamedLevelBeLoaded(loginSceneName))
            {
                SetStatus(launchStatus, $"登录场景未加入构建：{loginSceneName}。", true);
                return;
            }
            YardCraneFallbackSession.Clear();
            YardCraneTrainingSessionIdentity.Clear();
            SceneManager.LoadSceneAsync(loginSceneName, LoadSceneMode.Single);
        }

        private void LaunchTraining()
        {
            if (Page != YardCraneStudentSidePage.Home) return;
            if (!Application.isPlaying)
            {
                SetStatus(launchStatus, "预览模式。登录后可进入自由练习。");
                return;
            }
            if (profile == null) return;
            var freeTasks = YardCraneLegacyTaskAssetBridge.TasksForCategory(
                taskCatalog, YardCraneLegacyTaskAssetBridge.FreeCategory);
            var task = TaskAt(freeTasks, 0);
            if (task == null)
            {
                SetStatus(launchStatus, "任务目录中没有『自由练习』。", true);
                return;
            }
            if (!TryCreateCatalogTrainingContext(task, 0, out var context, out var problem))
            {
                SetStatus(launchStatus, problem, true);
                return;
            }
            var selection = new YardCraneTaskSelection(taskCatalog, task, 0, 0f, 1);
            BeginTraining(
                context,
                launchStatus,
                "正在进入自由练习…",
                taskSelection: selection);
        }

        private void OpenLearningTraining()
        {
            if (Page != YardCraneStudentSidePage.Home) return;
            SetStatus(learningLaunchStatus, string.Empty);
            SetPage(YardCraneStudentSidePage.Learning);
        }

        private void OpenAssessment()
        {
            if (Page != YardCraneStudentSidePage.Home) return;
            if (Application.isPlaying && profile == null) return;

            var craneType = CurrentAssessmentCraneType();
            currentAssessmentPaper = null;
            assessmentPaperTitle.text = $"{craneType} · 当前考核试卷";
            SetPage(YardCraneStudentSidePage.Assessment);

            if (!YardCraneAssessmentQuestionBankStore.TryLoadCurrentPaper(
                    craneType, out var paper, out var problem))
            {
                RenderAssessmentQuestions(null);
                SetStatus(
                    assessmentPaperStatus,
                    string.IsNullOrWhiteSpace(problem)
                        ? "暂无教师发布的考核题目。"
                        : $"考核试卷读取失败：{problem}",
                    true);
                return;
            }

            currentAssessmentPaper = paper;
            RenderAssessmentQuestions(paper);
            SetStatus(
                assessmentPaperStatus,
                Application.isPlaying
                    ? "请选择一道题进入实训场景作答。"
                    : "预览模式：已读取本机考核试卷，运行后可选择题目作答。");
        }

        private void CloseAssessment()
        {
            if (Page != YardCraneStudentSidePage.Assessment) return;
            currentAssessmentPaper = null;
            assessmentQuestionList?.Clear();
            SetStatus(launchStatus, string.Empty);
            SetPage(YardCraneStudentSidePage.Home);
        }

        private void RenderAssessmentQuestions(YardCraneAssessmentPaper paper)
        {
            if (assessmentQuestionList == null || assessmentPaperCount == null) return;
            assessmentQuestionList.Clear();
            var questions = paper?.items;
            var count = questions?.Count ?? 0;
            assessmentPaperCount.text = $"{count} 道题";
            if (questions == null) return;

            foreach (var question in questions)
            {
                if (question == null) continue;
                var row = new VisualElement();
                row.AddToClassList("assessment-question-row");

                var kind = new Label(
                    string.Equals(
                        question.kind,
                        YardCraneAssessmentQuestionBankStore.FaultKind,
                        System.StringComparison.Ordinal)
                        ? "故障题"
                        : "实训题") { pickingMode = PickingMode.Ignore };
                kind.AddToClassList("assessment-question-kind");

                var title = new Label(AssessmentQuestionTitle(question))
                {
                    pickingMode = PickingMode.Ignore
                };
                title.AddToClassList("assessment-question-title");

                var meta = new Label(AssessmentQuestionMeta(question))
                {
                    pickingMode = PickingMode.Ignore
                };
                meta.AddToClassList("assessment-question-meta");

                var start = new Button { text = "开始作答", focusable = false };
                start.AddToClassList("assessment-question-start");
                start.SetEnabled(Application.isPlaying && profile != null);
                var selectedQuestion = question;
                start.clicked += () => StartAssessmentQuestion(selectedQuestion);

                row.Add(kind);
                row.Add(title);
                row.Add(meta);
                row.Add(start);
                assessmentQuestionList.Add(row);
            }
        }

        private static string AssessmentQuestionTitle(YardCraneAssessmentQuestion question)
        {
            if (question?.training != null)
                return JoinAssessmentText(question.training.category, question.training.taskName);
            var faults = YardCraneAssessmentQuestionBankStore.FaultItems(question);
            if (faults.Count > 0)
                return "故障排除";
            return "未命名考核题";
        }

        private static string AssessmentQuestionMeta(YardCraneAssessmentQuestion question)
        {
            var faults = YardCraneAssessmentQuestionBankStore.FaultItems(question);
            if (faults.Count > 0)
                return $"包含 {faults.Count} 条故障";
            if (question?.training == null) return string.Empty;

            var training = question.training;
            var target = string.IsNullOrWhiteSpace(training.targetDescription)
                ? "无固定目标"
                : training.targetDescription.Trim();
            var durationSeconds = Mathf.RoundToInt(training.durationSeconds);
            var duration = durationSeconds > 0
                ? durationSeconds % 60 == 0
                    ? $"{durationSeconds / 60} min"
                    : $"{durationSeconds} 秒"
                : "不限时";
            var operation = training.trainingMode == 2 ? "远程操作" : "本地操作";
            return $"{target} · {duration} · {operation}";
        }

        private static string JoinAssessmentText(string first, string second)
        {
            first = (first ?? string.Empty).Trim();
            second = (second ?? string.Empty).Trim();
            if (first.Length == 0) return second.Length == 0 ? "未命名考核题" : second;
            return second.Length == 0 ? first : $"{first} · {second}";
        }

        private void StartAssessmentQuestion(YardCraneAssessmentQuestion question)
        {
            if (Page != YardCraneStudentSidePage.Assessment ||
                !Application.isPlaying || profile == null || question == null) return;

            var craneType = CurrentAssessmentCraneType();
            if (!string.Equals(question.craneType, craneType, System.StringComparison.Ordinal))
            {
                SetStatus(assessmentPaperStatus, "该题不属于当前设备类型，请让教师重新发布试卷。", true);
                return;
            }

            string title;
            string instruction;
            int durationSeconds;
            string problem;
            YardCraneAssessmentTrainingQuestionData assessmentTrainingQuestion = null;
            YardCranePublishedFaultAssignment faultAssignment = null;
            if (string.Equals(
                    question.kind,
                    YardCraneAssessmentQuestionBankStore.TrainingKind,
                    System.StringComparison.Ordinal))
            {
                if (!TryProjectTrainingQuestion(
                        question,
                        out title,
                        out instruction,
                        out durationSeconds,
                        out assessmentTrainingQuestion,
                        out problem))
                {
                    SetStatus(assessmentPaperStatus, $"实训题准备失败：{problem}", true);
                    return;
                }
            }
            else if (string.Equals(
                         question.kind,
                         YardCraneAssessmentQuestionBankStore.FaultKind,
                         System.StringComparison.Ordinal))
            {
                if (!TryProjectFaultQuestion(
                        question,
                        out title,
                        out instruction,
                        out durationSeconds,
                        out faultAssignment,
                        out problem))
                {
                    SetStatus(assessmentPaperStatus, $"故障题准备失败：{problem}", true);
                    return;
                }
            }
            else
            {
                SetStatus(assessmentPaperStatus, "该题使用了不支持的题目类型。", true);
                return;
            }

            var context = YardCraneLocalTrainingSessionContext.CreateAssessment(
                CreateParticipant(),
                YardCraneLocalTrainingExercise.PickupWorkflow,
                new YardCraneLocalTaskDisplayInfo(
                    title,
                    instruction,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty),
                durationSeconds);
            BeginTraining(
                context,
                assessmentPaperStatus,
                $"正在进入『{AssessmentQuestionTitle(question)}』…",
                question.questionId,
                assessmentTrainingQuestion: assessmentTrainingQuestion,
                faultAssignment: faultAssignment);
        }

        private bool TryProjectTrainingQuestion(
            YardCraneAssessmentQuestion question,
            out string title,
            out string instruction,
            out int durationSeconds,
            out YardCraneAssessmentTrainingQuestionData trainingSnapshot,
            out string problem)
        {
            title = string.Empty;
            instruction = string.Empty;
            durationSeconds = 600;
            trainingSnapshot = null;
            problem = null;
            var training = question?.training;
            if (training == null)
            {
                problem = "题目缺少实训内容。";
                return false;
            }
            if (training.targetId < 0 ||
                (training.trainingMode != 1 && training.trainingMode != 2) ||
                float.IsNaN(training.durationSeconds) ||
                float.IsInfinity(training.durationSeconds) ||
                training.durationSeconds < 0f)
            {
                problem = $"『{training.taskName}』的发布快照无效。";
                return false;
            }

            var requestedDuration = Mathf.RoundToInt(training.durationSeconds);
            durationSeconds = requestedDuration > 0 ? requestedDuration : 600;
            trainingSnapshot = training;
            title = $"任务考核 · {AssessmentQuestionTitle(question)}";
            var description = string.IsNullOrWhiteSpace(training.taskDescription)
                ? "请按题目要求完成实训操作。"
                : training.taskDescription.Trim();
            var shownTarget = string.IsNullOrWhiteSpace(training.targetDescription)
                ? "无固定目标"
                : training.targetDescription.Trim();
            instruction = string.IsNullOrWhiteSpace(shownTarget)
                ? description
                : $"{description} 当前目标：{shownTarget}。";
            return true;
        }

        private bool TryProjectFaultQuestion(
            YardCraneAssessmentQuestion question,
            out string title,
            out string instruction,
            out int durationSeconds,
            out YardCranePublishedFaultAssignment assignment,
            out string problem)
        {
            title = string.Empty;
            instruction = string.Empty;
            durationSeconds = 600;
            assignment = null;
            problem = null;
            var faults = YardCraneAssessmentQuestionBankStore.FaultItems(question);
            if (faults.Count == 0)
            {
                problem = "题目缺少有效故障内容。";
                return false;
            }

            var publishedFaults = new System.Collections.Generic.List<YardCranePublishedFaultItem>();
            var faultNumbers = new System.Collections.Generic.List<string>();
            foreach (var fault in faults)
            {
                if (fault == null || fault.faultId <= 0)
                {
                    problem = "题目包含无效故障编号。";
                    return false;
                }
                publishedFaults.Add(new YardCranePublishedFaultItem
                {
                    faultId = fault.faultId,
                    category = fault.category,
                    phenomenon = fault.phenomenon,
                    faultPoint = fault.faultPoint,
                    lineTag = fault.lineTag
                });
                faultNumbers.Add(fault.faultId.ToString("D3"));
            }

            assignment = new YardCranePublishedFaultAssignment
            {
                formatVersion = YardCranePublishedFaultAssignmentStore.FormatVersion,
                assignmentId = question.questionId,
                publishedAtUtc = currentAssessmentPaper?.publishedAtUtc,
                craneType = CurrentAssessmentCraneType(),
                isPublished = true,
                faults = publishedFaults
            };
            title = $"任务考核 · {AssessmentQuestionTitle(question)}";
            instruction =
                $"请根据故障现象完成 {faults.Count} 条故障的排除。故障编号：{string.Join("、", faultNumbers)}。";
            return true;
        }

        private static string CurrentAssessmentCraneType() =>
            YardCraneStudentShell.SelectedCraneKind == YardCraneCraneKind.QuayCrane
                ? "岸桥"
                : "场桥";

        private void CloseLearningTraining()
        {
            if (Page != YardCraneStudentSidePage.Learning) return;
            if (faultDetailView != null && faultDetailView.resolvedStyle.display != DisplayStyle.None)
            {
                ShowFaultCategoryList();
                return;
            }
            SetStatus(launchStatus, string.Empty);
            SetPage(YardCraneStudentSidePage.Home);
        }

        private void ShowSingleTraining() =>
            SetLearningCategory(learningNavSingle, singleDrillRow, "已选择单项练习，请选择训练项目与参数。");

        // 2026-09-11: 联动操作/能力提升 gained their own rows. 2026-09-19 adds the
        // fault-selection interface while keeping fault injection and assessment explicitly
        // out of scope until the corresponding runtime subsystem exists.
        private void ShowLinkedTraining() =>
            SetLearningCategory(learningNavLink, linkedDrillRow, "已选择联动操作，请选择预设组合。");
        private void ShowWorkTraining() =>
            SetLearningCategory(learningNavWork, workDrillRow, "已选择作业操作，请选择任务与目标。");
        private void ShowSkillTraining() =>
            SetLearningCategory(learningNavSkill, skillDrillRow, "已选择能力提升，请选择训练项目。");
        private void ShowFaultTraining()
        {
            SetLearningCategory(learningNavFault, faultDrillRow, "已进入故障排除，请选择需要查看的故障类型。");
            ShowFaultCategoryList();
        }

        private void SetLearningCategory(Button activeNav, VisualElement activeRow, string statusText)
        {
            if (Page != YardCraneStudentSidePage.Learning) return;
            learningNavSingle.EnableInClassList("active", activeNav == learningNavSingle);
            singleDrillRow.style.display = activeRow == singleDrillRow ? DisplayStyle.Flex : DisplayStyle.None;
            learningNavLink.EnableInClassList("active", activeNav == learningNavLink);
            linkedDrillRow.style.display = activeRow == linkedDrillRow ? DisplayStyle.Flex : DisplayStyle.None;
            learningNavWork.EnableInClassList("active", activeNav == learningNavWork);
            workDrillRow.style.display = activeRow == workDrillRow ? DisplayStyle.Flex : DisplayStyle.None;
            learningNavSkill.EnableInClassList("active", activeNav == learningNavSkill);
            skillDrillRow.style.display = activeRow == skillDrillRow ? DisplayStyle.Flex : DisplayStyle.None;
            learningNavFault.EnableInClassList("active", activeNav == learningNavFault);
            faultDrillRow.style.display = activeRow == faultDrillRow ? DisplayStyle.Flex : DisplayStyle.None;
            SetStatus(learningLaunchStatus, statusText);
        }

        private void OnFaultCardClicked(ClickEvent evt)
        {
            if (Page != YardCraneStudentSidePage.Learning) return;
            var selected = evt.currentTarget as Button;
            if (selected == null) return;

            foreach (var button in faultDrillRow.Query<Button>(className: "fault-card").ToList())
                button.EnableInClassList("selected", button == selected);

            var title = selected.Q<Label>(className: "fault-card-title")?.text ?? "故障类型";
            ShowFaultDetail(title);
        }

        private void ShowFaultDetail(string category)
        {
            var selectedCategory = FindFaultCategory(category);
            if (selectedCategory == null)
            {
                SetStatus(learningLaunchStatus, $"未在故障表中找到『{category}』，请检查数据资源。", true);
                return;
            }

            var faults = selectedCategory.faults ?? System.Array.Empty<FaultRecord>();
            faultCategoryHeading.style.display = DisplayStyle.None;
            faultCategoryGrid.style.display = DisplayStyle.None;
            faultDetailView.style.display = DisplayStyle.Flex;
            selectedFaultCategoryName = category;
            faultDetailTitle.text = category;
            faultDetailCount.text = $"{faults.Length} 项";
            faultDetailNotice.text = "按序号查看故障点、故障现象和分类，可上下滚动浏览。";

            faultDetailList.Clear();
            for (var index = 0; index < faults.Length; index++)
            {
                var fault = faults[index];
                var row = new VisualElement();
                row.AddToClassList("fault-detail-row");
                row.Add(CreateFaultTableCell(fault.number.ToString(), "fault-col-number"));
                row.Add(CreateFaultTableCell(fault.faultPoint, "fault-col-point"));
                row.Add(CreateFaultTableCell(fault.phenomenon, "fault-col-symptom"));
                row.Add(CreateFaultTableCell(selectedCategory.name, "fault-col-category"));
                faultDetailList.Add(row);
            }

            SetStatus(learningLaunchStatus, $"正在查看『{category}』完整故障列表，可上下滚动浏览。");
        }

        private static Label CreateFaultTableCell(string text, string columnClass)
        {
            var label = new Label(text);
            label.AddToClassList("fault-table-cell");
            label.AddToClassList(columnClass);
            return label;
        }

        private void ShowFaultCategoryList()
        {
            if (Page != YardCraneStudentSidePage.Learning) return;
            faultCategoryHeading.style.display = DisplayStyle.Flex;
            faultCategoryGrid.style.display = DisplayStyle.Flex;
            faultDetailView.style.display = DisplayStyle.None;
            selectedFaultCategoryName = string.Empty;
            SetStatus(learningLaunchStatus, "请选择故障类型，学习训练始终使用本机完整故障资料目录。");
        }

        private void StartFaultSimulation()
        {
            if (Page != YardCraneStudentSidePage.Learning) return;
            if (string.IsNullOrWhiteSpace(selectedFaultCategoryName))
            {
                SetStatus(learningLaunchStatus, "请先选择一个故障类型。", true);
                return;
            }
            if (!TryCreateFaultTrainingAssignment(
                    selectedFaultCategoryName,
                    out var assignment,
                    out var faultCount,
                    out var problem))
            {
                SetStatus(learningLaunchStatus, $"故障练习准备失败：{problem}", true);
                return;
            }
            if (!Application.isPlaying)
            {
                SetStatus(
                    learningLaunchStatus,
                    $"预览模式：{selectedFaultCategoryName} · {faultCount} 条故障。");
                return;
            }
            if (profile == null) return;

            var context = YardCraneLocalTrainingSessionContext.Create(
                CreateParticipant(),
                YardCraneLocalTrainingMode.GuidedLearning,
                YardCraneLocalTrainingExercise.None,
                YardCraneLocalTrainingTarget.None,
                YardCraneLocalTimingPolicy.CountUp(startCountdownSeconds: 5),
                YardCraneLocalAssistLevel.Guided,
                YardCraneLocalScoreTrustLevel.Unscored,
                YardCraneLocalTrainingReturnPage.LearningSelection,
                new YardCraneLocalTaskDisplayInfo(
                    $"学习训练 · {selectedFaultCategoryName}",
                    $"练习当前类别中的 {faultCount} 条故障，并按任务状态完成模拟排障。",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty));
            BeginTraining(
                context,
                learningLaunchStatus,
                $"正在进入『{selectedFaultCategoryName} · {faultCount} 条故障』…",
                faultAssignment: assignment,
                faultTraining: true);
        }

        private bool TryCreateFaultTrainingAssignment(
            string categoryName,
            out YardCranePublishedFaultAssignment assignment,
            out int faultCount,
            out string problem)
        {
            assignment = null;
            faultCount = 0;
            problem = null;
            var category = FindFaultCategory(categoryName);
            if (category == null)
            {
                problem = $"未在故障表中找到『{categoryName}』。";
                return false;
            }

            var trainingFaults = category.faults ?? System.Array.Empty<FaultRecord>();
            if (trainingFaults.Length == 0)
            {
                problem = $"『{categoryName}』当前没有可训练的故障。";
                return false;
            }

            var items = new System.Collections.Generic.List<YardCranePublishedFaultItem>(
                trainingFaults.Length);
            foreach (var fault in trainingFaults)
            {
                if (fault == null || fault.number <= 0)
                {
                    problem = $"『{categoryName}』包含无效故障ID。";
                    return false;
                }
                items.Add(new YardCranePublishedFaultItem
                {
                    faultId = fault.number,
                    category = categoryName,
                    phenomenon = fault.phenomenon,
                    faultPoint = fault.faultPoint,
                    lineTag = fault.lineTag
                });
            }

            faultCount = items.Count;
            assignment = new YardCranePublishedFaultAssignment
            {
                formatVersion = YardCranePublishedFaultAssignmentStore.FormatVersion,
                assignmentId = $"learning-{System.Guid.NewGuid():N}",
                publishedAtUtc = System.DateTime.UtcNow.ToString("O"),
                craneType = CurrentAssessmentCraneType(),
                faults = items,
                isPublished = false
            };
            return true;
        }

        private bool TryLoadFaultCatalog(out string problem)
        {
            problem = null;
            var selectedKind = YardCraneStudentShell.SelectedCraneKind;
            if (faultCatalog != null && loadedFaultCatalogKind == selectedKind) return true;

            var craneType = selectedKind == YardCraneCraneKind.QuayCrane ? "岸桥" : "场桥";
            var asset = selectedKind == YardCraneCraneKind.QuayCrane
                ? quayFaultCatalogAsset
                : yardFaultCatalogAsset;
            var categoryNames = selectedKind == YardCraneCraneKind.QuayCrane
                ? QuayFaultCategoryNames
                : YardFaultCategoryNames;
            if (!TryBuildFaultCatalog(asset, craneType, categoryNames, out var parsed, out problem))
            {
                faultCatalog = null;
                loadedFaultCatalogKind = null;
                return false;
            }

            faultCatalog = parsed;
            loadedFaultCatalogKind = selectedKind;
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
                !(faultDictionaryField.GetValue(asset) is System.Collections.IDictionary sourceCatalog))
            {
                problem = $"{asset.name}.asset does not expose a readable _故障 dictionary.";
                return false;
            }

            var grouped = new System.Collections.Generic.Dictionary<
                string, System.Collections.Generic.List<FaultRecord>>(System.StringComparer.Ordinal);
            foreach (var categoryName in categoryNames)
                grouped.Add(categoryName, new System.Collections.Generic.List<FaultRecord>());

            var usedIds = new System.Collections.Generic.HashSet<int>();
            foreach (System.Collections.DictionaryEntry source in sourceCatalog)
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
            }
            if (usedIds.Count == 0)
            {
                problem = $"{asset.name}.asset contains no fault records.";
                return false;
            }

            var categories = new FaultCategory[categoryNames.Length];
            for (var index = 0; index < categoryNames.Length; index++)
            {
                var categoryName = categoryNames[index];
                var faults = grouped[categoryName];
                if (faults.Count == 0)
                {
                    problem = $"{asset.name}.asset category '{categoryName}' contains no fault records.";
                    return false;
                }
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
                categories = categories
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
            var lineTagField = sourceType.GetField("标号");
            var faultPointField = sourceType.GetField("故障点");
            var phenomenonField = sourceType.GetField("故障现象");
            var categoryField = sourceType.GetField("类型");
            lineTag = lineTagField?.GetValue(source) as string;
            faultPoint = faultPointField?.GetValue(source) as string;
            phenomenon = phenomenonField?.GetValue(source) as string;
            category = categoryField?.GetValue(source) as string;

            return lineTagField?.FieldType == typeof(string) &&
                   faultPointField?.FieldType == typeof(string) &&
                   phenomenonField?.FieldType == typeof(string) &&
                   categoryField?.FieldType == typeof(string);
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
            var shortName = craneName == "岸桥" ? "QC" : "YC";

            var roleLabel = root.Q<Label>(className: "mode-crumb-role");
            var overline = root.Q<Label>(className: "mode-overline");
            var stationLabel = root.Q<Label>(className: "station-label");
            var machineChip = root.Q<Label>(className: "machine-chip");
            var profileAvatar = root.Q<Label>(className: "profile-avatar");
            var welcomeTitle = root.Q<Label>(className: "welcome-mark-title");
            var welcomeCaption = root.Q<Label>(className: "welcome-mark-caption");
            var learningDescription = learningModeButton?.Q<Label>(className: "mode-card-desc");
            if (roleLabel != null) roleLabel.text = $"{craneName}司机";
            foreach (var topLabel in root.Query<Label>(className: "mode-top-label").ToList())
                topLabel.text = $"{englishName}  /  TRAINING MODE";
            if (overline != null) overline.text = $"{englishName}  /  TRAINING MODE";
            if (stationLabel != null) stationLabel.text = $"●  {craneName} · 学生端";
            if (machineChip != null) machineChip.text = craneName;
            if (profileAvatar != null) profileAvatar.text = shortName;
            if (welcomeTitle != null) welcomeTitle.text = shortName;
            if (welcomeCaption != null) welcomeCaption.text = $"{craneName}司机训练";
            if (learningDescription != null)
                learningDescription.text = $"系统化学习作业流程\n掌握{craneName}操作技能";
        }

        private FaultCategory FindFaultCategory(string categoryName)
        {
            if (!TryLoadFaultCatalog(out _)) return null;
            foreach (var category in faultCatalog.categories)
            {
                if (category.name == categoryName) return category;
            }
            return null;
        }

        private void ShowLearningNotReady(string category)
        {
            if (Page != YardCraneStudentSidePage.Learning) return;
            SetStatus(learningLaunchStatus, $"{category}尚未接入，当前仍显示单项练习。");
        }

        private int SelectedLearningTrainingMode =>
            learningScopeRemote != null && learningScopeRemote.ClassListContains("learning-scope-chip-active") ? 2 : 1;

        private void SelectLocalScope() => SelectLearningScope(1);

        private void SelectRemoteScope() => SelectLearningScope(2);

        private void SelectLearningScope(int trainingMode)
        {
            if (Page != YardCraneStudentSidePage.Learning) return;
            if (Application.isPlaying &&
                !YardCraneLegacyTaskAssetBridge.TryWriteTrainingMode(taskStateAsset, trainingMode, out var problem))
            {
                SetStatus(learningLaunchStatus, $"模式切换失败：{problem}", true);
                return;
            }
            learningScopeLocal.EnableInClassList("learning-scope-chip-active", trainingMode == 1);
            learningScopeRemote.EnableInClassList("learning-scope-chip-active", trainingMode == 2);
        }

        // 2026-09-11 real-scale recalibration. Target values are cross-checked against
        // 《场桥集装箱起重机仿真系统（含远程）用户手册V1.0.docx》§5.2 (单项练习) where the
        // manual's own numbers fit the new reachable range; where they don't fit the current
        // coordinate convention (see below), a reasoned in-range substitute is used instead and
        // called out - this is not a blind copy of the manual.
        private void StartGantryDrill()
        {
            if (taskCatalog != null)
            {
                StartCatalogTask(taskCatalog.Find("大车移动"), root.Q("drill-gantry-options"));
                return;
            }
            // Manual §5.2.1: -50m/-30m/30m/50m, tolerance ±0.5m. These are signed offsets from
            // gantry home, same convention this code already uses, and now comfortably fit the
            // new ±212.5m reachable range (vs. ±8m before) - used verbatim.
            var values = new[] { -50d, -30d, 30d, 50d };
            StartGuidedDrill(
                "drill-gantry-options",
                values,
                YardCraneLocalTrainingExercise.GantryMovement,
                YardCraneLocalTrainingMetric.GantryPosition,
                YardCraneLocalTargetReferenceFrame.GantryHomeRelativeMeters,
                0.5d,
                "大车移动",
                value => $"{value:+0;-0;0}m",
                value => $"将大车移动到相对原点 {value:+0;-0;0}m，并稳定保持。");
        }

        private void StartTrolleyDrill()
        {
            if (taskCatalog != null)
            {
                StartCatalogTask(taskCatalog.Find("小车移动"), root.Q("drill-trolley-options"));
                return;
            }
            // Manual §5.2.2 gives 15m/35m/45m, but those read as absolute distances from one
            // rail, not signed offsets from a centered home - they don't fit this code's
            // TrolleyLocalXMeters convention (signed, home-relative) or the new ±14m reachable
            // range. Kept the existing signed-offset shape, scaled up in-range instead. Tolerance
            // ±0.5m matches the manual's stated precision for this drill.
            var values = new[] { -10d, 5d, 10d };
            StartGuidedDrill(
                "drill-trolley-options",
                values,
                YardCraneLocalTrainingExercise.TrolleyMovement,
                YardCraneLocalTrainingMetric.TrolleyPosition,
                YardCraneLocalTargetReferenceFrame.TrolleyLocalXMeters,
                0.5d,
                "小车移动",
                value => $"{value:+0.0;-0.0;0.0}m",
                value => $"将小车移动到本地坐标 {value:+0.0;-0.0;0.0}m，并稳定保持。");
        }

        private void StartHoistDrill()
        {
            if (taskCatalog != null)
            {
                StartCatalogTask(taskCatalog.Find("吊具起升"), root.Q("drill-hoist-options"));
                return;
            }
            // Manual §5.2.3 gives absolute target heights (5m/10m/20m from ground), but this
            // code's SpreaderStartRelativeMeters frame is a signed offset from wherever THIS
            // session's spreader happened to start (recomputed each run) - there is no fixed
            // mapping from the manual's absolute heights into that frame. Kept the existing
            // relative-offset shape, scaled to the new ~21m total travel range instead; real
            // reachability is still enforced per-session by YardCraneLocalTrainingTargetValidator.
            var values = new[] { 5d, -5d, -10d };
            StartGuidedDrill(
                "drill-hoist-options",
                values,
                YardCraneLocalTrainingExercise.SpreaderHoisting,
                YardCraneLocalTrainingMetric.SpreaderHeight,
                YardCraneLocalTargetReferenceFrame.SpreaderStartRelativeMeters,
                0.5d,
                "吊具起升",
                value => value > 0d ? $"上升 {value:0}m" : $"下降 {-value:0}m",
                value => value > 0d
                    ? $"从本次训练起始高度上升 {value:0}m，并稳定保持。"
                    : $"从本次训练起始高度下降 {-value:0}m，并稳定保持。");
        }
        // The final two callbacks retain their legacy field/button names so existing scene
        // serialization and UI queries keep working; their visible YC semantics are rotation
        // and return-to-zero (not QC-only spreader tilt / boom trim).
        private void StartTiltDrill()
        {
            if (taskCatalog != null)
            {
                StartCatalogTask(
                    taskCatalog.Find("吊具旋转") ?? taskCatalog.Find("吊具倾斜"),
                    root.Q("drill-tilt-options"));
                return;
            }
            // Manual §5.2.4 only lists two options (左旋3°/右旋3°) - the third "左旋1°" here was
            // not in the manual and is dropped. Tolerance raised from 0.15° to 0.5°: 0.15° was
            // copied from the linear drills' meter-scale tolerance without redoing it for
            // degrees, which is unreasonably tight for gamepad/keyboard precision - 0.5° is a
            // reasoned fix, not a manual-sourced number (the manual excerpt available here does
            // not state an explicit rotation tolerance).
            var values = new[] { -3d, 3d };
            StartGuidedDrill(
                "drill-tilt-options",
                values,
                YardCraneLocalTrainingExercise.SpreaderRotation,
                YardCraneLocalTrainingMetric.SpreaderRotation,
                YardCraneLocalTargetReferenceFrame.SpreaderPhysicalRotationDegrees,
                0.5d,
                "吊具回旋",
                value => value < 0d ? $"左旋 {-value:0}°" : $"右旋 {value:0}°",
                value => value < 0d
                    ? $"将吊具向左回旋至 {-value:0}°，并稳定保持。"
                    : $"将吊具向右回旋至 {value:0}°，并稳定保持。");
        }

        private void StartPitchDrill()
        {
            if (Page != YardCraneStudentSidePage.Learning) return;
            var holdAfterZero = SelectedToggleIndex("drill-pitch-options") == 1;
            var displayText = holdAfterZero ? "回零后稳定保持" : "完成自动回零";
            var instruction = holdAfterZero
                ? "先手动回旋离开零位，再触发自动回零并稳定保持。"
                : "先手动回旋离开零位，再触发一次真实的自动回零。";
            // Same 2026-09-11 tolerance fix as StartTiltDrill: the old 0.10/0.15 values were
            // meter-scale tolerances reused as degrees. Bumped in the same direction (still
            // tighter for the hold variant, since it demands staying there, not just arriving).
            var target = new YardCraneLocalTrainingTarget(
                YardCraneLocalTrainingMetric.SpreaderZeroing,
                YardCraneLocalTargetReferenceFrame.SpreaderPhysicalRotationDegrees,
                0d,
                holdAfterZero ? 0.30d : 0.5d,
                displayText,
                holdAfterZero
                    ? YardCraneLocalSpreaderZeroingPolicy.ReturnAndHold
                    : YardCraneLocalSpreaderZeroingPolicy.AutoReturn,
                holdAfterZero ? 1.5d : 0d);
            BeginGuidedTraining(YardCraneLocalTrainingExercise.SpreaderZeroing, "吊具回零", target, instruction);
        }

        // 2026-09-16: 《岸桥场桥项目技术要求2026.8.14》2.1.2 gives concrete combos for these
        // four drills (the manual excerpt used on 2026-09-11 did not). Gantry sub-values below
        // are that document's numbers verbatim (50m/-50m) - they fit this code's
        // GantryHomeRelativeMeters convention and the ±212.5m reachable range without
        // modification, same as the already-adopted 大车移动 single-item values.
        //
        // Trolley/hoist sub-values are NOT that document's numbers: it gives 小车15m/35m and
        // 起升10m/20m, which read as absolute/rail-referenced distances the same way the
        // manual's single-item 小车/吊具起升 numbers did (see 2026-09-11 comments on
        // StartTrolleyDrill/StartHoistDrill) - 15m/35m do not fit this code's ±14m
        // TrolleyLocalXMeters range at all (35m is unreachable). Per user decision
        // (2026-09-16), kept the existing mirrored-pair shape and scaled both trolley and hoist
        // to ±10m - a reasoned in-range equivalent bumped up from the prior ±5m to track the
        // tech requirement's larger numbers, not a unit conversion of 15/35/10/20 into this
        // frame. Disclosed here, not implied as the document's own values.
        private void StartLinkedGantryTrolleyDrill()
        {
            if (taskCatalog != null)
            {
                StartCatalogTask(
                    taskCatalog.Find("大车小车联动"),
                    root.Q("drill-linked-gantry-trolley-options"));
                return;
            }
            var gantryValues = new[] { 50d, -50d };
            var trolleyValues = new[] { 10d, -10d };
            var index = SelectedToggleIndex("drill-linked-gantry-trolley-options");
            if (index < 0 || index >= gantryValues.Length)
            {
                SetStatus(learningLaunchStatus, "请选择『大车小车联动』的预设组合。", true);
                return;
            }
            var gantryValue = gantryValues[index];
            var trolleyValue = trolleyValues[index];
            var gantryTarget = new YardCraneLocalTrainingTarget(
                YardCraneLocalTrainingMetric.GantryPosition,
                YardCraneLocalTargetReferenceFrame.GantryHomeRelativeMeters,
                gantryValue,
                0.5d,
                $"大车 {gantryValue:+0;-0;0}m");
            var trolleyTarget = new YardCraneLocalTrainingTarget(
                YardCraneLocalTrainingMetric.TrolleyPosition,
                YardCraneLocalTargetReferenceFrame.TrolleyLocalXMeters,
                trolleyValue,
                0.5d,
                $"小车 {trolleyValue:+0;-0;0}m");
            BeginGuidedTraining(
                YardCraneLocalTrainingExercise.LinkedGantryTrolley,
                "大车小车联动",
                gantryTarget,
                new[] { trolleyTarget },
                "同时将大车和小车移动到目标位置，并稳定保持。");
        }

        private void StartLinkedGantryHoistDrill()
        {
            if (taskCatalog != null)
            {
                StartCatalogTask(
                    taskCatalog.Find("大车起升联动"),
                    root.Q("drill-linked-gantry-hoist-options"));
                return;
            }
            // Gantry sub-values are the 2026.8.14 technical requirement's 50m/-50m; hoist
            // sub-values are a reasoned ±10m in-range equivalent, not that document's 10m/20m -
            // see the shared note on StartLinkedGantryTrolleyDrill above.
            var gantryValues = new[] { 50d, -50d };
            var hoistValues = new[] { 10d, -10d };
            var index = SelectedToggleIndex("drill-linked-gantry-hoist-options");
            if (index < 0 || index >= gantryValues.Length)
            {
                SetStatus(learningLaunchStatus, "请选择『大车起升』的预设组合。", true);
                return;
            }
            var gantryValue = gantryValues[index];
            var hoistValue = hoistValues[index];
            var gantryTarget = new YardCraneLocalTrainingTarget(
                YardCraneLocalTrainingMetric.GantryPosition,
                YardCraneLocalTargetReferenceFrame.GantryHomeRelativeMeters,
                gantryValue,
                0.5d,
                $"大车 {gantryValue:+0;-0;0}m");
            var hoistTarget = new YardCraneLocalTrainingTarget(
                YardCraneLocalTrainingMetric.SpreaderHeight,
                YardCraneLocalTargetReferenceFrame.SpreaderStartRelativeMeters,
                hoistValue,
                0.5d,
                hoistValue > 0d ? $"上升 {hoistValue:0}m" : $"下降 {-hoistValue:0}m");
            BeginGuidedTraining(
                YardCraneLocalTrainingExercise.LinkedGantryHoist,
                "大车起升",
                gantryTarget,
                new[] { hoistTarget },
                "同时将大车移动到目标位置、吊具升降到目标高度，并稳定保持。");
        }

        private void StartLinkedTrolleyHoistDrill()
        {
            if (taskCatalog != null)
            {
                StartCatalogTask(
                    taskCatalog.Find("小车起升联动"),
                    root.Q("drill-linked-trolley-hoist-options"));
                return;
            }
            // No gantry axis here to take the technical requirement's numbers verbatim. Both
            // sub-values are the same reasoned ±10m in-range equivalent used by the other three
            // linked drills - see the note on StartLinkedGantryTrolleyDrill above.
            var trolleyValues = new[] { 10d, -10d };
            var hoistValues = new[] { 10d, -10d };
            var index = SelectedToggleIndex("drill-linked-trolley-hoist-options");
            if (index < 0 || index >= trolleyValues.Length)
            {
                SetStatus(learningLaunchStatus, "请选择『小车起升』的预设组合。", true);
                return;
            }
            var trolleyValue = trolleyValues[index];
            var hoistValue = hoistValues[index];
            var trolleyTarget = new YardCraneLocalTrainingTarget(
                YardCraneLocalTrainingMetric.TrolleyPosition,
                YardCraneLocalTargetReferenceFrame.TrolleyLocalXMeters,
                trolleyValue,
                0.5d,
                $"小车 {trolleyValue:+0;-0;0}m");
            var hoistTarget = new YardCraneLocalTrainingTarget(
                YardCraneLocalTrainingMetric.SpreaderHeight,
                YardCraneLocalTargetReferenceFrame.SpreaderStartRelativeMeters,
                hoistValue,
                0.5d,
                hoistValue > 0d ? $"上升 {hoistValue:0}m" : $"下降 {-hoistValue:0}m");
            BeginGuidedTraining(
                YardCraneLocalTrainingExercise.LinkedTrolleyHoist,
                "小车起升",
                trolleyTarget,
                new[] { hoistTarget },
                "同时将小车移动到目标位置、吊具升降到目标高度，并稳定保持。");
        }

        private void StartLinkedGantryTrolleyHoistDrill()
        {
            if (taskCatalog != null)
            {
                StartCatalogTask(
                    taskCatalog.Find("大车小车起升联动"),
                    root.Q("drill-linked-gantry-trolley-hoist-options"));
                return;
            }
            // Gantry sub-values are the 2026.8.14 technical requirement's 50m/-50m; trolley/hoist
            // sub-values are the same reasoned ±10m in-range equivalent as the other three
            // linked drills, not that document's 15/35m and 10/20m - see the shared note on
            // StartLinkedGantryTrolleyDrill above.
            var gantryValues = new[] { 50d, -50d };
            var trolleyValues = new[] { 10d, -10d };
            var hoistValues = new[] { 10d, -10d };
            var index = SelectedToggleIndex("drill-linked-gantry-trolley-hoist-options");
            if (index < 0 || index >= gantryValues.Length)
            {
                SetStatus(learningLaunchStatus, "请选择『大车小车起升』的预设组合。", true);
                return;
            }
            var gantryValue = gantryValues[index];
            var trolleyValue = trolleyValues[index];
            var hoistValue = hoistValues[index];
            var gantryTarget = new YardCraneLocalTrainingTarget(
                YardCraneLocalTrainingMetric.GantryPosition,
                YardCraneLocalTargetReferenceFrame.GantryHomeRelativeMeters,
                gantryValue,
                0.5d,
                $"大车 {gantryValue:+0;-0;0}m");
            var trolleyTarget = new YardCraneLocalTrainingTarget(
                YardCraneLocalTrainingMetric.TrolleyPosition,
                YardCraneLocalTargetReferenceFrame.TrolleyLocalXMeters,
                trolleyValue,
                0.5d,
                $"小车 {trolleyValue:+0;-0;0}m");
            var hoistTarget = new YardCraneLocalTrainingTarget(
                YardCraneLocalTrainingMetric.SpreaderHeight,
                YardCraneLocalTargetReferenceFrame.SpreaderStartRelativeMeters,
                hoistValue,
                0.5d,
                hoistValue > 0d ? $"上升 {hoistValue:0}m" : $"下降 {-hoistValue:0}m");
            BeginGuidedTraining(
                YardCraneLocalTrainingExercise.LinkedGantryTrolleyHoist,
                "大车小车起升",
                gantryTarget,
                new[] { trolleyTarget, hoistTarget },
                "同时将大车、小车移动到目标位置、吊具升降到目标高度，并稳定保持。");
        }

        // Same GantryPosition metric as the 大车移动 single-item drill above; only one fixed
        // target rather than a choice of several - no options row on this card, see
        // skill-drill-row in StudentSideShell.uxml.
        //
        // 2026-09-16: manual V1.0 §5.2.9 gave -50m→0m (drive back to home); 《岸桥场桥项目技术
        // 要求2026.8.14》2.1.4 gives "初始位置到50m处目标位置A点" instead - the two source
        // documents disagree on which end is the target. Per user decision (2026-09-16), the
        // 2026.8.14 technical requirement takes precedence as the newer, more formal source;
        // target changed from the manual's 0m to 50m accordingly. 50m fits the ±212.5m reachable
        // gantry range without modification.
        private void StartGantryStraightLineDrill()
        {
            var target = new YardCraneLocalTrainingTarget(
                YardCraneLocalTrainingMetric.GantryPosition,
                YardCraneLocalTargetReferenceFrame.GantryHomeRelativeMeters,
                50d,
                0.5d,
                "大车 +50m");
            BeginGuidedTraining(
                YardCraneLocalTrainingExercise.SkillGantryStraightLine,
                "大车直线行走",
                target,
                "将大车沿直线开到相对原点 +50m，并稳定保持。");
        }

        private void StartCatalogTask(YardCraneTaskDefinition task, VisualElement options)
        {
            if (Page != YardCraneStudentSidePage.Learning) return;
            if (task == null)
            {
                SetStatus(learningLaunchStatus, "任务目录中没有找到该训练项目。", true);
                return;
            }

            var targetId = SelectedToggleIndex(options);
            if (targetId < 0 || targetId >= task.OptionCount)
            {
                SetStatus(learningLaunchStatus, $"请选择『{task.Name}』的训练目标。", true);
                return;
            }
            var optionText = YardCraneLegacyTaskAssetBridge.OptionLabel(task, targetId);
            if (!Application.isPlaying)
            {
                SetStatus(learningLaunchStatus, $"预览模式：{task.Name} · {optionText}");
                return;
            }
            if (profile == null) return;
            if (!TryCreateCatalogTrainingContext(task, targetId, out var context, out var problem))
            {
                SetStatus(learningLaunchStatus, problem, true);
                return;
            }

            var selection = new YardCraneTaskSelection(taskCatalog, task, targetId, 0f, SelectedLearningTrainingMode);
            BeginTraining(
                context,
                learningLaunchStatus,
                $"正在进入『{task.Name} · {optionText}』…",
                taskSelection: selection);
        }

        private bool TryCreateCatalogTrainingContext(
            YardCraneTaskDefinition task,
            int targetId,
            out YardCraneLocalTrainingSessionContext context,
            out string problem)
        {
            context = null;
            problem = null;
            var optionText = YardCraneLegacyTaskAssetBridge.OptionLabel(task, targetId);
            var instruction = string.IsNullOrWhiteSpace(task.Description)
                ? optionText
                : $"{task.Description}。当前目标：{optionText}。";
            var displayInfo = new YardCraneLocalTaskDisplayInfo(
                $"学习训练 · {task.Name}",
                instruction,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);

            if (task.TaskType == YardCraneLegacyTaskAssetBridge.FreeCategory)
            {
                context = YardCraneLocalTrainingSessionContext.CreateFreePractice(CreateParticipant());
                return true;
            }

            YardCraneLocalTrainingExercise exercise;
            YardCraneLocalTrainingTarget primary;
            YardCraneLocalTrainingTarget[] additional = System.Array.Empty<YardCraneLocalTrainingTarget>();
            double gantry;
            double trolley;
            double height;
            double angle;
            switch (task.Name)
            {
                case "大车移动":
                    if (!TryTargetNumber(task, targetId, "目标位置", out gantry, out problem)) return false;
                    exercise = YardCraneLocalTrainingExercise.GantryMovement;
                    primary = CreateCatalogTarget(
                        YardCraneLocalTrainingMetric.GantryPosition,
                        YardCraneLocalTargetReferenceFrame.GantryHomeRelativeMeters,
                        gantry,
                        optionText);
                    break;
                case "小车移动":
                    if (!TryTargetNumber(task, targetId, "目标位置", out trolley, out problem)) return false;
                    exercise = YardCraneLocalTrainingExercise.TrolleyMovement;
                    primary = CreateCatalogTarget(
                        YardCraneLocalTrainingMetric.TrolleyPosition,
                        YardCraneLocalTargetReferenceFrame.TrolleyLocalXMeters,
                        trolley,
                        optionText);
                    break;
                case "吊具起升":
                    if (!TryTargetNumber(task, targetId, "目标高度", out height, out problem)) return false;
                    exercise = YardCraneLocalTrainingExercise.SpreaderHoisting;
                    primary = CreateCatalogTarget(
                        YardCraneLocalTrainingMetric.SpreaderHeight,
                        YardCraneLocalTargetReferenceFrame.SpreaderSceneWorldYMeters,
                        height,
                        optionText);
                    break;
                case "吊具旋转":
                    if (!TryTargetNumber(task, targetId, "角度", out angle, out problem)) return false;
                    exercise = YardCraneLocalTrainingExercise.SpreaderRotation;
                    primary = CreateCatalogTarget(
                        YardCraneLocalTrainingMetric.SpreaderRotation,
                        YardCraneLocalTargetReferenceFrame.SpreaderPhysicalRotationDegrees,
                        angle,
                        optionText);
                    break;
                case "大车小车联动":
                    if (!TryTargetNumber(task, targetId, "大车目标", out gantry, out problem) ||
                        !TryTargetNumber(task, targetId, "小车目标", out trolley, out problem)) return false;
                    exercise = YardCraneLocalTrainingExercise.LinkedGantryTrolley;
                    primary = CreateCatalogTarget(
                        YardCraneLocalTrainingMetric.GantryPosition,
                        YardCraneLocalTargetReferenceFrame.GantryHomeRelativeMeters,
                        gantry,
                        $"大车 {gantry:0.##}m");
                    additional = new[]
                    {
                        CreateCatalogTarget(
                            YardCraneLocalTrainingMetric.TrolleyPosition,
                            YardCraneLocalTargetReferenceFrame.TrolleyLocalXMeters,
                            trolley,
                            $"小车 {trolley:0.##}m")
                    };
                    break;
                case "大车起升联动":
                    if (!TryTargetNumber(task, targetId, "大车目标", out gantry, out problem) ||
                        !TryTargetNumber(task, targetId, "吊具高度", out height, out problem)) return false;
                    exercise = YardCraneLocalTrainingExercise.LinkedGantryHoist;
                    primary = CreateCatalogTarget(
                        YardCraneLocalTrainingMetric.GantryPosition,
                        YardCraneLocalTargetReferenceFrame.GantryHomeRelativeMeters,
                        gantry,
                        $"大车 {gantry:0.##}m");
                    additional = new[]
                    {
                        CreateCatalogTarget(
                            YardCraneLocalTrainingMetric.SpreaderHeight,
                            YardCraneLocalTargetReferenceFrame.SpreaderSceneWorldYMeters,
                            height,
                            $"吊具 {height:0.##}m")
                    };
                    break;
                case "小车起升联动":
                    if (!TryTargetNumber(task, targetId, "小车目标", out trolley, out problem) ||
                        !TryTargetNumber(task, targetId, "吊具高度", out height, out problem)) return false;
                    exercise = YardCraneLocalTrainingExercise.LinkedTrolleyHoist;
                    primary = CreateCatalogTarget(
                        YardCraneLocalTrainingMetric.TrolleyPosition,
                        YardCraneLocalTargetReferenceFrame.TrolleyLocalXMeters,
                        trolley,
                        $"小车 {trolley:0.##}m");
                    additional = new[]
                    {
                        CreateCatalogTarget(
                            YardCraneLocalTrainingMetric.SpreaderHeight,
                            YardCraneLocalTargetReferenceFrame.SpreaderSceneWorldYMeters,
                            height,
                            $"吊具 {height:0.##}m")
                    };
                    break;
                case "大车小车起升联动":
                    if (!TryCompositeTravelTarget(task, targetId, out gantry, out trolley, out problem) ||
                        !TryTargetNumber(task, targetId, "吊具高度", out height, out problem)) return false;
                    exercise = YardCraneLocalTrainingExercise.LinkedGantryTrolleyHoist;
                    primary = CreateCatalogTarget(
                        YardCraneLocalTrainingMetric.GantryPosition,
                        YardCraneLocalTargetReferenceFrame.GantryHomeRelativeMeters,
                        gantry,
                        $"大车 {gantry:0.##}m");
                    additional = new[]
                    {
                        CreateCatalogTarget(
                            YardCraneLocalTrainingMetric.TrolleyPosition,
                            YardCraneLocalTargetReferenceFrame.TrolleyLocalXMeters,
                            trolley,
                            $"小车 {trolley:0.##}m"),
                        CreateCatalogTarget(
                            YardCraneLocalTrainingMetric.SpreaderHeight,
                            YardCraneLocalTargetReferenceFrame.SpreaderSceneWorldYMeters,
                            height,
                            $"吊具 {height:0.##}m")
                    };
                    break;
                default:
                    // The imported catalogue contains work and skill items whose dedicated
                    // runtime judging mechanics are not implemented yet. They still enter the
                    // real scene with the selected catalogue snapshot and an honest open-ended
                    // local session; no completion rule is fabricated here.
                    context = YardCraneLocalTrainingSessionContext.Create(
                        CreateParticipant(),
                        YardCraneLocalTrainingMode.FreePractice,
                        YardCraneLocalTrainingExercise.FreePractice,
                        YardCraneLocalTrainingTarget.None,
                        YardCraneLocalTimingPolicy.CountUp(),
                        YardCraneLocalAssistLevel.Standard,
                        YardCraneLocalScoreTrustLevel.LocalEstimated,
                        YardCraneLocalTrainingReturnPage.StudentHome,
                        displayInfo);
                    return true;
            }

            context = YardCraneLocalTrainingSessionContext.CreateGuidedLearning(
                CreateParticipant(), exercise, primary, additional, displayInfo);
            return true;
        }

        private static YardCraneLocalTrainingTarget CreateCatalogTarget(
            YardCraneLocalTrainingMetric metric,
            YardCraneLocalTargetReferenceFrame referenceFrame,
            double value,
            string displayText)
        {
            return new YardCraneLocalTrainingTarget(metric, referenceFrame, value, 0.5d, displayText);
        }

        private static bool TryTargetNumber(
            YardCraneTaskDefinition task,
            int targetId,
            string titleFragment,
            out double value,
            out string problem)
        {
            value = 0d;
            problem = null;
            foreach (var field in task.TargetFields)
            {
                if (!field.Title.Contains(titleFragment)) continue;
                if (targetId < field.Values.Length &&
                    YardCraneLegacyTaskAssetBridge.TryParseNumber(field.Values[targetId], out value))
                    return true;
                break;
            }
            problem = $"『{task.Name}』的目标字段『{titleFragment}』不是有效数字。";
            return false;
        }

        private static bool TryCompositeTravelTarget(
            YardCraneTaskDefinition task,
            int targetId,
            out double gantry,
            out double trolley,
            out string problem)
        {
            gantry = 0d;
            trolley = 0d;
            problem = null;
            foreach (var field in task.TargetFields)
            {
                if (!field.Title.Contains("行车目标") || targetId >= field.Values.Length) continue;
                var text = field.Values[targetId] ?? string.Empty;
                var gantryMatch = System.Text.RegularExpressions.Regex.Match(
                    text, @"大车\s*(-?\d+(?:\.\d+)?)");
                var trolleyMatch = System.Text.RegularExpressions.Regex.Match(
                    text, @"小车\s*(-?\d+(?:\.\d+)?)");
                if (gantryMatch.Success && trolleyMatch.Success &&
                    YardCraneLegacyTaskAssetBridge.TryParseNumber(gantryMatch.Groups[1].Value, out gantry) &&
                    YardCraneLegacyTaskAssetBridge.TryParseNumber(trolleyMatch.Groups[1].Value, out trolley))
                    return true;
                break;
            }
            problem = $"『{task.Name}』的行车目标无法解析为大车和小车位置。";
            return false;
        }

        private void StartGuidedDrill(
            string optionGroup,
            double[] values,
            YardCraneLocalTrainingExercise exercise,
            YardCraneLocalTrainingMetric metric,
            YardCraneLocalTargetReferenceFrame referenceFrame,
            double tolerance,
            string title,
            System.Func<double, string> displayText,
            System.Func<double, string> instruction)
        {
            if (Page != YardCraneStudentSidePage.Learning) return;
            var index = SelectedToggleIndex(optionGroup);
            if (index < 0 || index >= values.Length)
            {
                SetStatus(learningLaunchStatus, $"请选择『{title}』的训练目标。", true);
                return;
            }

            var value = values[index];
            var target = new YardCraneLocalTrainingTarget(
                metric,
                referenceFrame,
                value,
                tolerance,
                displayText(value));
            BeginGuidedTraining(exercise, title, target, instruction(value));
        }

        private void BeginGuidedTraining(
            YardCraneLocalTrainingExercise exercise,
            string title,
            YardCraneLocalTrainingTarget target,
            string instruction)
        {
            BeginGuidedTraining(exercise, title, target, System.Array.Empty<YardCraneLocalTrainingTarget>(), instruction);
        }

        // 2026-09-11: overload for a 联动 exercise - additionalTargets rides alongside target,
        // see YardCraneLocalTrainingSessionContext.AdditionalTargets for the validation
        // contract. Status/result text joins every axis' DisplayText so the student sees the
        // full combined target, not just the primary axis.
        private void BeginGuidedTraining(
            YardCraneLocalTrainingExercise exercise,
            string title,
            YardCraneLocalTrainingTarget target,
            YardCraneLocalTrainingTarget[] additionalTargets,
            string instruction)
        {
            var combinedDisplayText = target.DisplayText;
            foreach (var extra in additionalTargets) combinedDisplayText += "、" + extra.DisplayText;
            if (!Application.isPlaying)
            {
                SetStatus(learningLaunchStatus, $"预览模式：{title} · {combinedDisplayText}");
                return;
            }
            if (profile == null) return;
            var context = YardCraneLocalTrainingSessionContext.CreateGuidedLearning(
                CreateParticipant(),
                exercise,
                target,
                additionalTargets,
                new YardCraneLocalTaskDisplayInfo(
                    $"学习训练 · {title}",
                    instruction,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty));
            BeginTraining(context, learningLaunchStatus, $"正在进入『{title} · {combinedDisplayText}』…");
        }

        private void BeginTraining(
            YardCraneLocalTrainingSessionContext context,
            Label statusLabel,
            string launchingText,
            string assignedTaskId = null,
            YardCraneTaskSelection? taskSelection = null,
            YardCraneAssessmentTrainingQuestionData assessmentTrainingQuestion = null,
            YardCranePublishedFaultAssignment faultAssignment = null,
            bool faultTraining = false)
        {
            if (context == null || profile == null ||
                !YardCraneStudentShellPolicy.CanLaunchTraining(profile.Role)) return;
            var taskStatePayloadCount =
                (taskSelection.HasValue ? 1 : 0) +
                (assessmentTrainingQuestion != null ? 1 : 0) +
                (faultAssignment != null ? 1 : 0);
            if (taskStatePayloadCount != 1 || (faultTraining && faultAssignment == null))
            {
                SetStatus(statusLabel, "本次训练内容不完整，未写入任务状态，已取消进入实训场景。", true);
                Debug.LogError(
                    $"[YC_STUDENT_TASK_STATE_PAYLOAD_INVALID] payloadCount={taskStatePayloadCount}, " +
                    $"faultTraining={faultTraining}",
                    this);
                return;
            }
            var craneName = YardCraneStudentShell.SelectedCraneKind == YardCraneCraneKind.QuayCrane
                ? "岸桥"
                : "场桥";
            var operationalSceneName = YardCraneStudentShell.SelectedCraneKind == YardCraneCraneKind.QuayCrane
                ? QuayCraneSceneName
                : YardCraneSceneName;
            operationalSceneName = operationalSceneName?.Trim();
            if (string.IsNullOrEmpty(operationalSceneName))
            {
                SetStatus(statusLabel, $"{craneName}实训场景名未配置。", true);
                return;
            }
            if (!Application.CanStreamedLevelBeLoaded(operationalSceneName))
            {
                SetStatus(statusLabel, $"{craneName}实训场景未加入构建：{operationalSceneName}。", true);
                return;
            }
            if (YardCraneFallbackSession.IsActive)
            {
                SetStatus(statusLabel, "已有训练会话正在运行，请先返回或结束该会话。", true);
                return;
            }
            if (!YardCraneFallbackSession.TryBegin(profile, context, out var error))
            {
                SetStatus(statusLabel, $"训练会话创建失败：{error}", true);
                return;
            }

            try
            {
                if (taskSelection.HasValue &&
                    !YardCraneLegacyTaskAssetBridge.TryWriteTaskState(
                        taskStateAsset, taskSelection.Value, out var taskStateProblem))
                {
                    throw new System.InvalidOperationException(
                        $"Cannot write Assets/Data/任务State.asset: {taskStateProblem}");
                }
                if (assessmentTrainingQuestion != null &&
                    !YardCraneLegacyTaskAssetBridge.TryWriteAssessmentTrainingTaskState(
                        taskStateAsset,
                        assessmentTrainingQuestion,
                        out var assessmentStateProblem))
                {
                    throw new System.InvalidOperationException(
                        $"Cannot write training assessment to Assets/Data/任务State.asset: {assessmentStateProblem}");
                }
                if (faultAssignment != null &&
                    !(faultTraining
                        ? YardCraneLegacyTaskAssetBridge.TryWriteFaultTrainingTaskState(
                            taskStateAsset,
                            faultAssignment,
                            out var faultStateProblem)
                        : YardCraneLegacyTaskAssetBridge.TryWriteFaultTaskState(
                            taskStateAsset,
                            faultAssignment,
                            context.Timing.DurationSeconds ?? 0,
                            out faultStateProblem)))
                {
                    throw new System.InvalidOperationException(
                        $"Cannot write fault task to Assets/Data/任务State.asset: {faultStateProblem}");
                }
                if (faultTraining &&
                    !YardCraneLegacyTaskAssetBridge.TryWriteTrainingMode(
                        taskStateAsset, SelectedLearningTrainingMode, out var faultTrainingModeProblem))
                {
                    throw new System.InvalidOperationException(
                        $"Cannot write fault training mode to Assets/Data/任务State.asset: {faultTrainingModeProblem}");
                }
                YardCraneTrainingSessionIdentity.Begin(
                    context.Participant.StudentNumber,
                    context.Participant.DisplayName,
                    context.SessionId);
                // 2026-09-17: must run after Begin() above, which unconditionally resets
                // AssignedTaskId to empty - see that field's comment on YardCraneTrainingSessionIdentity.
                if (!string.IsNullOrEmpty(assignedTaskId))
                {
                    YardCraneTrainingSessionIdentity.RecordAssignedTaskId(assignedTaskId);
                }
                // StudentHome loads the operational scene directly. Keep one persistent
                // navigation owner alive across that Single-mode scene switch so the scene HUD
                // can return to the originating student page without routing through Bootstrap.
                YardCraneLocalTrainingNavigationBridge.Begin(profile, context.SessionId);
                var loadOperation = SceneManager.LoadSceneAsync(
                    operationalSceneName,
                    LoadSceneMode.Single);
                if (loadOperation == null)
                    throw new System.InvalidOperationException("Unity returned no training scene-load operation.");
            }
            catch (System.Exception exception)
            {
                // Launch is a transaction: a rejected direct scene request must not leave
                // identity/session state that blocks a retry from StudentHome.
                YardCraneLocalTrainingNavigationBridge.TryAbortLaunch(context.SessionId);
                YardCraneFallbackSession.TryClear(context.SessionId);
                if (YardCraneTrainingSessionIdentity.SessionId == context.SessionId)
                    YardCraneTrainingSessionIdentity.Clear();
                SetStatus(statusLabel, "无法进入训练场景，请检查构建配置后重试。", true);
                Debug.LogError(
                    $"[YC_STUDENT_TRAINING_LAUNCH_FAILED] sessionId={context.SessionId:D}, {exception}",
                    this);
                return;
            }

            Debug.Log(
                $"[YC_STUDENT_TRAINING_HANDOFF] crane={YardCraneStudentShell.SelectedCraneKind}, " +
                $"scene={operationalSceneName}, sessionId={context.SessionId:D}",
                this);
            SetPage(YardCraneStudentSidePage.Launching);
            SetStatus(statusLabel, launchingText);
        }

        private YardCraneLocalTrainingParticipant CreateParticipant()
        {
            return new YardCraneLocalTrainingParticipant(profile.StudentNumber, profile.DisplayName);
        }

        private int SelectedToggleIndex(string groupName)
        {
            return SelectedToggleIndex(root?.Q(groupName));
        }

        private static int SelectedToggleIndex(VisualElement container)
        {
            if (container == null) return -1;
            var toggles = container.Query<Toggle>().ToList();
            for (var index = 0; index < toggles.Count; index++)
            {
                if (toggles[index].value) return index;
            }
            return -1;
        }

        // UI Toolkit has no built-in radio-button group; Toggle only gives independent
        // checkboxes. This keeps exactly one option checked per drill card.
        private static void BindExclusiveToggleGroup(VisualElement container)
        {
            if (container == null) return;
            var toggles = container.Query<Toggle>().ToList();
            foreach (var toggle in toggles)
            {
                toggle.RegisterValueChangedCallback(changeEvent =>
                {
                    if (!changeEvent.newValue)
                    {
                        toggle.SetValueWithoutNotify(true);
                        return;
                    }
                    foreach (var sibling in toggles)
                    {
                        if (sibling != toggle) sibling.SetValueWithoutNotify(false);
                    }
                });
            }
        }

        private void SetPage(YardCraneStudentSidePage page)
        {
            var launchingFromLearning =
                page == YardCraneStudentSidePage.Launching &&
                Page == YardCraneStudentSidePage.Learning;
            var launchingFromAssessment =
                page == YardCraneStudentSidePage.Launching &&
                Page == YardCraneStudentSidePage.Assessment;
            Page = page;
            var home = page == YardCraneStudentSidePage.Home ||
                       (page == YardCraneStudentSidePage.Launching &&
                        !launchingFromLearning && !launchingFromAssessment);
            var learning = page == YardCraneStudentSidePage.Learning || launchingFromLearning;
            var assessment = page == YardCraneStudentSidePage.Assessment || launchingFromAssessment;
            studentHomePage.style.display = home ? DisplayStyle.Flex : DisplayStyle.None;
            studentLearningPage.style.display = learning ? DisplayStyle.Flex : DisplayStyle.None;
            studentAssessmentPage.style.display = assessment ? DisplayStyle.Flex : DisplayStyle.None;
            launchButton.SetEnabled(page == YardCraneStudentSidePage.Home &&
                                    (!Application.isPlaying || (profile != null &&
                                     YardCraneStudentShellPolicy.CanLaunchTraining(profile.Role))));
            logoutButton.SetEnabled(page == YardCraneStudentSidePage.Home);
            assessmentBackButton.SetEnabled(page == YardCraneStudentSidePage.Assessment);
        }

        private void RefreshIdentity()
        {
            var name = profile != null ? profile.DisplayName : "预览学员";
            var text = profile != null ? $"{name}  ·  {profile.AccountId}" : name;
            identity.text = text;
            learningIdentity.text = text;
            if (greeting != null) greeting.text = $"{name}，准备开始新一次作业？";
        }

        private static void SetStatus(Label label, string text, bool error = false)
        {
            label.text = text ?? string.Empty;
            label.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
            label.EnableInClassList("danger", error);
        }

        private void OnDisable()
        {
            ReleaseInterface();
        }

        private void UnbindEvents()
        {
            upperEventsBound = false;
            if (logoutButton != null) logoutButton.clicked -= Logout;
            if (launchButton != null) launchButton.clicked -= LaunchTraining;
            if (quitButton != null) quitButton.clicked -= YardCraneShellPanelUtility.Quit;
            if (learningModeButton != null) learningModeButton.clicked -= OpenLearningTraining;
            if (assessmentModeButton != null) assessmentModeButton.clicked -= OpenAssessment;
            if (assessmentBackButton != null) assessmentBackButton.clicked -= CloseAssessment;
            if (learningBackButton != null) learningBackButton.clicked -= CloseLearningTraining;
            if (learningBackCrumb != null) learningBackCrumb.clicked -= CloseLearningTraining;
            if (learningCloseButton != null) learningCloseButton.clicked -= YardCraneShellPanelUtility.Quit;
            if (drillGantryStart != null) drillGantryStart.clicked -= StartGantryDrill;
            if (drillTrolleyStart != null) drillTrolleyStart.clicked -= StartTrolleyDrill;
            if (drillHoistStart != null) drillHoistStart.clicked -= StartHoistDrill;
            if (drillTiltStart != null) drillTiltStart.clicked -= StartTiltDrill;
            if (drillPitchStart != null) drillPitchStart.clicked -= StartPitchDrill;
            if (drillLinkedGantryTrolleyStart != null)
                drillLinkedGantryTrolleyStart.clicked -= StartLinkedGantryTrolleyDrill;
            if (drillLinkedGantryHoistStart != null)
                drillLinkedGantryHoistStart.clicked -= StartLinkedGantryHoistDrill;
            if (drillLinkedTrolleyHoistStart != null)
                drillLinkedTrolleyHoistStart.clicked -= StartLinkedTrolleyHoistDrill;
            if (drillLinkedGantryTrolleyHoistStart != null)
                drillLinkedGantryTrolleyHoistStart.clicked -= StartLinkedGantryTrolleyHoistDrill;
            if (drillSkillGantryStraightStart != null)
                drillSkillGantryStraightStart.clicked -= StartGantryStraightLineDrill;
            if (learningNavSingle != null) learningNavSingle.clicked -= ShowSingleTraining;
            if (learningNavLink != null) learningNavLink.clicked -= ShowLinkedTraining;
            if (learningNavWork != null) learningNavWork.clicked -= ShowWorkTraining;
            if (learningNavSkill != null) learningNavSkill.clicked -= ShowSkillTraining;
            if (learningNavFault != null) learningNavFault.clicked -= ShowFaultTraining;
            if (faultDrillRow != null)
            {
                foreach (var button in faultDrillRow.Query<Button>(className: "fault-card").ToList())
                    button.UnregisterCallback<ClickEvent>(OnFaultCardClicked);
            }
            if (faultDetailBackButton != null) faultDetailBackButton.clicked -= ShowFaultCategoryList;
            if (faultSimulationButton != null) faultSimulationButton.clicked -= StartFaultSimulation;
            if (learningScopeLocal != null) learningScopeLocal.clicked -= SelectLocalScope;
            if (learningScopeRemote != null) learningScopeRemote.clicked -= SelectRemoteScope;
            foreach (var button in dynamicTaskStartButtons)
            {
                if (button != null) button.UnregisterCallback<ClickEvent>(OnCatalogTaskClicked);
            }
            dynamicTaskStartButtons.Clear();
        }

        private void ReleaseInterface()
        {
            built = false;
            UnbindEvents();
            assessmentQuestionList?.Clear();
            if (root != null) root.style.unityFontDefinition = StyleKeyword.Null;
            if (rootLower != null) rootLower.style.unityFontDefinition = StyleKeyword.Null;
            YardCraneShellPanelUtility.ReleasePanel(document, panelTemplate, ref panelSettings);
            YardCraneShellPanelUtility.ReleasePanel(documentLower, lowerPanelTemplate, ref panelSettingsLower);
            root = null;
            YardCraneShellPanelUtility.ReleaseOwnedObject(runtimeFont);
            runtimeFont = null;
            document = null;
            studentAssessmentPage = null;
            assessmentBackButton = null;
            assessmentPaperTitle = null;
            assessmentPaperCount = null;
            assessmentPaperStatus = null;
            assessmentQuestionList = null;
            currentAssessmentPaper = null;
            workDrillRow = null;
            taskCatalog = null;
            faultCatalog = null;
            loadedFaultCatalogKind = null;
            rootLower = null;
            YardCraneShellPanelUtility.ReleaseOwnedObject(runtimeFontLower);
            runtimeFontLower = null;
            documentLower = null;
        }

#if UNITY_EDITOR
        // Mirrors YardCraneStudentShell's editor lifecycle hooks - see its identical
        // comment for why this exists (restore scene asset references before Unity
        // serializes scenes or reloads the domain; never saves the scene itself).
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

        public void EditorPreviewHome()
        {
            if (Application.isPlaying || editorSuspended || !isActiveAndEnabled) return;
            if (!IsReady) BuildInterface();
            if (!IsReady) return;
            profile = null;
            RefreshIdentity();
            showing = true;
            root.style.display = DisplayStyle.Flex;
            rootLower.style.display = DisplayStyle.Flex;
            SetPage(YardCraneStudentSidePage.Home);
            SetStatus(launchStatus, string.Empty);
            SetStatus(learningLaunchStatus, string.Empty);
            root.MarkDirtyRepaint();
        }

        public void EditorPreviewLearning()
        {
            if (Application.isPlaying || editorSuspended || !isActiveAndEnabled) return;
            EditorPreviewHome();
            if (!IsReady) return;
            OpenLearningTraining();
            root.MarkDirtyRepaint();
        }

        public void EditorHidePreview()
        {
            if (Application.isPlaying || editorSuspended || !isActiveAndEnabled) return;
            if (!IsReady) return;
            showing = false;
            root.style.display = DisplayStyle.None;
            rootLower.style.display = DisplayStyle.None;
            root.MarkDirtyRepaint();
        }

        public void EditorRefreshPreviewLayout()
        {
            if (Application.isPlaying || editorSuspended || !isActiveAndEnabled) return;
            if (!IsReady)
            {
                var wasShowing = showing;
                BuildInterface();
                if (IsReady && !wasShowing) EditorHidePreview();
            }
            if (!IsReady) return;
            root.MarkDirtyRepaint();
        }
#endif
    }
}
