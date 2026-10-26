using System;
using PortTraining.YardCraneSimulator.Training;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

#if Mode_Anqiao && Mode_Changqiao
#error Build Profile cannot define both Mode_Anqiao and Mode_Changqiao. Keep exactly one crane mode define.
#endif

namespace PortTraining.YardCraneSimulator.Shell
{
    // The role-neutral login shell, normally configured in StudentEntry. It owns login UI only: student
    // and teacher identities are handed to their own StudentHome/TeacherHome scenes, each
    // with its own shell and UIDocuments.
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class YardCraneStudentShell : MonoBehaviour
    {
        public const string Version = "2026-10-01-yc-entry-shell-v33-student-task-state-identity";
        // Shared return-route name only. The login shell no longer gates its lifecycle by scene name.
        public const string SceneName = "StudentEntry";
        public const string BootstrapSceneName = "Bootstrap";
        // The independent post-login student scene (YardCraneStudentSideShell lives there
        // on its own GameObject with its own Display1+Display2 UIDocuments). A successful
        // student login hands off to it via a real scene switch, not an in-scene object
        // reference. StudentEntry now keeps only the role-neutral login UI.
        // 2026-09-20: these two are now Inspector-configurable public fields (not const) so a
        // scene can point login handoff at different student/teacher scenes without a code
        // change - defaults match the previous hardcoded values.
        [Tooltip("Scene loaded after a successful student login.")]
        public string StudentHomeSceneName = "StudentHome";
        [Tooltip("Scene loaded after a successful local teacher/admin login.")]
        public string TeacherHomeSceneName = "TeacherHome";
        [SerializeField, InspectorName("任务状态")]
        [Tooltip("学生登录成功后写入姓名和学号；请绑定与 StudentHome 相同的任务State.asset。")]
        private ScriptableObject taskStateAsset;

        // 2026-09-20: static, not per-instance - a single-mode scene load destroys this
        // GameObject, so an instance field would not survive the handoff to StudentHome/
        // TeacherHome. Same reasoning as the YardCraneTeacherHomeHandoff/
        // YardCraneStudentHomeHandoff static carriers in YardCraneStudentShellModel.cs.
        // Build Profiles select the product variant through one scripting define. The no-define
        // branch preserves the former QuayCrane default for editor/dev imports and logs once in
        // Play mode so an incorrectly configured deliverable is visible instead of silent.
#if Mode_Anqiao
        public const YardCraneCraneKind ConfiguredCraneKind = YardCraneCraneKind.QuayCrane;
        public const string CraneKindConfigurationSource = "Mode_Anqiao";
#elif Mode_Changqiao
        public const YardCraneCraneKind ConfiguredCraneKind = YardCraneCraneKind.YardCrane;
        public const string CraneKindConfigurationSource = "Mode_Changqiao";
#else
        public const YardCraneCraneKind ConfiguredCraneKind = YardCraneCraneKind.QuayCrane;
        public const string CraneKindConfigurationSource = "fallback-no-build-define";
#endif
        public static YardCraneCraneKind SelectedCraneKind { get; private set; } = ConfiguredCraneKind;
#if !Mode_Anqiao && !Mode_Changqiao
        private static bool missingCraneModeDefineWarningLogged;
#endif

        // Upper screen (Display 1) - the UIDocument itself lives on this same
        // GameObject and is configured in the scene (Source Asset + Panel Settings
        // assigned in the Inspector), not created by this script.
        private UIDocument document;
        // The folder asset assigned in the Inspector remains the UIDocument's live
        // PanelSettings. Keeping the serialized reference lets validation recover it after
        // a domain reload without creating a runtime copy.
        [SerializeField, HideInInspector] private PanelSettings upperPanelTemplate;
        private PanelSettings panelSettings;
        private Font runtimeFont;
        private VisualElement root;

        // Lower screen (Display 2) - static decorative content, no interaction/state.
        // Its UIDocument lives on a separate scene GameObject (must not be a child of
        // this one - see the comment on lowerDocument) and is wired up here.
        [SerializeField]
        [Tooltip("The Lower/Display2 screen's UIDocument. Must live on a GameObject that " +
                 "is NOT a child of this one: Unity treats a UIDocument whose GameObject " +
                 "is parented under another UIDocument's GameObject as a nested child " +
                 "panel and forces it to share the parent's PanelSettings, which breaks " +
                 "targeting two different Displays.")]
        private UIDocument lowerDocument;
        private UIDocument documentLower;
        [SerializeField, HideInInspector] private PanelSettings lowerPanelTemplate;
        private PanelSettings panelSettingsLower;
        private Font runtimeFontLower;
        private VisualElement rootLower;

        private bool displayActivated;
        private VisualElement loginPage;
        private TextField studentNumberField;
        private TextField displayNameField;
        private TextField teacherPasswordField;
        private VisualElement studentNameGroup;
        private VisualElement teacherPasswordGroup;
        private Label loginModeNote;
        private Label loginError;
        private Label loginTitle;
        private Label loginContext;
        private Label loginNote;
        private Label accountLabel;
        private Label systemTitle;
        private Label systemCode;
        private Button loginButton;
        private Button studentRoleButton;
        private Button teacherRoleButton;
        private YardCraneLocalStudentProfile profile;
        private int lastWidth;
        private int lastHeight;
        private bool built;
        private bool building;
        private bool editorSuspended;
        private double nextBuildAt;
        private string lastBuildProblem;

        public bool IsReady => built && isActiveAndEnabled && !editorSuspended &&
                               document != null && document.isActiveAndEnabled &&
                               document.rootVisualElement == root && root != null &&
                               document.panelSettings == panelSettings &&
                               root.Q<Button>("login-button") == loginButton &&
                               documentLower != null && documentLower.isActiveAndEnabled &&
                               documentLower.rootVisualElement == rootLower && rootLower != null &&
                               documentLower.panelSettings == panelSettingsLower && lowerDocument == documentLower;
        public YardCraneStudentShellPage Page { get; private set; } = YardCraneStudentShellPage.Login;
        public YardCraneShellRole Role { get; private set; } = YardCraneShellRole.Student;
        public VisualElement VisualRoot => root;
        public UIDocument Document => document;
        public VisualElement VisualRootLower => rootLower;
        public UIDocument DocumentLower => documentLower;

        private void Awake()
        {
            if (!Application.isPlaying) return;
            if (YardCraneStudentShellPolicy.RequiresBootstrap(Environment.GetCommandLineArgs()))
            {
                enabled = false;
                SceneManager.LoadSceneAsync(BootstrapSceneName, LoadSceneMode.Single);
            }
        }

        private void OnEnable()
        {
            nextBuildAt = 0;
            BuildInterface();
        }

        private void OnValidate()
        {
            ApplyBuildProfileCraneKind();
        }

        // UIDocument.OnEnable order is not guaranteed relative to this ExecuteAlways
        // component.  When this component wakes first, rootVisualElement is still null;
        // retry once after all scene components have completed their initialisation.
        private void Start()
        {
            if (!isActiveAndEnabled || IsReady) return;
            BuildInterface();
        }

        private void BuildInterface()
        {
            if (editorSuspended || building || IsReady || !isActiveAndEnabled ||
                Time.realtimeSinceStartupAsDouble < nextBuildAt) return;
            nextBuildAt = Time.realtimeSinceStartupAsDouble + 0.25;
            building = true;
            try
            {
                // A deferred root keeps its owned panel; retrying must not destroy/recreate it.
                // A live UXML rebuild invalidates bindings and does require a clean rebind.
                if (built) ReleaseInterface();
                if (Application.isPlaying) ActivateSecondDisplayIfNeeded();

                if (!BuildUpperInterface()) return;
                if (!BuildLowerInterface()) return;

                if (Application.isPlaying)
                {
                    YardCraneTeacherHomeHandoff.Clear();
                    YardCraneFallbackSession.Clear();
                    YardCraneTrainingSessionIdentity.Clear();
                }
                ApplyBuildProfileCraneKind();
                profile = null;
                Page = YardCraneStudentShellPage.Login;
                ApplyRole(YardCraneShellRole.Student);
                SetPage(YardCraneStudentShellPage.Login);
                root.style.display = DisplayStyle.Flex;
                lastWidth = -1;
                lastHeight = -1;
                UpdateScale();
                built = true;
                lastBuildProblem = null;
                if (Application.isPlaying)
                    Debug.Log($"[YC_ENTRY_SHELL_READY] version={Version}, roles=student+teacher, crane={SelectedCraneKind}, craneSource={CraneKindConfigurationSource}, destinations={StudentHomeSceneName}+{TeacherHomeSceneName}, authentication=student-identity+local-admin, displays={Display.displays.Length}", this);
            }
            finally { building = false; }
        }

        private bool BuildProblem(string detail)
        {
            // Editor refresh and frame retries must not flood the Console for one fault.
            if (lastBuildProblem != detail)
            {
                lastBuildProblem = detail;
                Debug.LogError($"[YC_STUDENT_SHELL_MISSING_ASSET] {detail}", this);
            }
            return false;
        }

        private void ActivateSecondDisplayIfNeeded()
        {
            if (displayActivated || Display.displays.Length <= 1) return;
            Display.displays[1].Activate();
            displayActivated = true;
        }

        private bool BuildUpperInterface()
        {
            document = GetComponent<UIDocument>();
            var problem = YardCraneShellPanelUtility.PrepareDocument(
                document, ref upperPanelTemplate, ref panelSettings, "Upper (Display1)", out var docReady);
            if (problem != null) return BuildProblem(problem);
            if (!docReady) return false;
            if (root == document.rootVisualElement && loginButton != null &&
                root.Q<Button>("login-button") == loginButton) return true;
            UnbindUpperEvents();

            var loginBackdropTexture = Resources.Load<Texture2D>(
                "YardCraneShell/Display1UI/PortTrainingOceanBackdrop_v1");
            var brandLogoTexture = Resources.Load<Texture2D>("YardCraneShell/Display1UI/logo");
            if (loginBackdropTexture == null || brandLogoTexture == null)
            {
                return BuildProblem("Upper (Display1): background texture or logo is missing.");
            }

            root = document.rootVisualElement;
            root.style.flexGrow = 1;
            YardCraneShellPanelUtility.ReleaseOwnedObject(runtimeFont);
            runtimeFont = YardCraneShellPanelUtility.CreateShellFont();
            if (runtimeFont != null) root.style.unityFontDefinition = FontDefinition.FromFont(runtimeFont);

            loginPage = root.Q("login-page");
            studentNumberField = root.Q<TextField>("student-number");
            displayNameField = root.Q<TextField>("display-name");
            teacherPasswordField = root.Q<TextField>("teacher-password");
            teacherPasswordField.isPasswordField = true;
            studentNameGroup = root.Q("student-name-group");
            teacherPasswordGroup = root.Q("teacher-password-group");
            loginModeNote = root.Q<Label>("login-mode-note");
            loginError = root.Q<Label>("login-error");
            loginTitle = root.Q<Label>("login-title");
            loginContext = root.Q<Label>("login-context");
            loginNote = root.Q<Label>("login-note");
            accountLabel = root.Q<Label>("account-label");
            systemTitle = root.Q<Label>("system-title");
            systemCode = root.Q<Label>(className: "brand-code");
            loginButton = root.Q<Button>("login-button");
            studentRoleButton = root.Q<Button>("role-student");
            teacherRoleButton = root.Q<Button>("role-teacher");
            var loginBackdrop = root.Q<Image>("login-background");
            loginBackdrop.image = loginBackdropTexture;
            loginBackdrop.scaleMode = ScaleMode.ScaleAndCrop;
            var brandLogo = root.Q<Image>("login-brand-logo");
            brandLogo.image = brandLogoTexture;
            brandLogo.scaleMode = ScaleMode.ScaleToFit;

            RemoveDefaultControlChrome(studentNumberField);
            RemoveDefaultControlChrome(displayNameField);
            RemoveDefaultControlChrome(teacherPasswordField);
            foreach (var button in root.Query<Button>().ToList())
                button.style.backgroundImage = StyleKeyword.None;

            loginButton.clicked += Login;
            studentRoleButton.clicked += SelectStudentRole;
            teacherRoleButton.clicked += SelectTeacherRole;
            studentNumberField.RegisterCallback<KeyDownEvent>(SubmitOnEnter);
            displayNameField.RegisterCallback<KeyDownEvent>(SubmitOnEnter);
            teacherPasswordField.RegisterCallback<KeyDownEvent>(SubmitOnEnter);
            foreach (var button in root.Query<Button>().ToList()) button.focusable = false;

            return true;
        }

        private bool BuildLowerInterface()
        {
            if (documentLower != lowerDocument)
                YardCraneShellPanelUtility.ReleasePanel(documentLower, lowerPanelTemplate, ref panelSettingsLower);
            documentLower = lowerDocument;
            if (documentLower == null)
            {
                return BuildProblem("Lower (Display2): assign Lower Document to the separate lower screen UIDocument.");
            }
            if (documentLower == document || documentLower.transform.IsChildOf(document.transform) ||
                document.transform.IsChildOf(documentLower.transform))
            {
                return BuildProblem("Lower Document must reference an independent UIDocument; the screens cannot be the same object or nested.");
            }
            var problem = YardCraneShellPanelUtility.PrepareDocument(
                documentLower, ref lowerPanelTemplate, ref panelSettingsLower, "Lower (Display2)", out var docReady);
            if (problem != null) return BuildProblem(problem);
            if (!docReady) return false;
            if (rootLower == documentLower.rootVisualElement) return true;

            var lowerPosterTexture = Resources.Load<Texture2D>(
                "YardCraneShell/Display2UI/QingdaoAutomatedTerminalPoster_v2");
            if (lowerPosterTexture == null)
            {
                return BuildProblem("Lower (Display2): poster texture is missing.");
            }

            rootLower = documentLower.rootVisualElement;
            rootLower.style.flexGrow = 1;
            // Visible by default - this is the companion screen shown whenever the student
            // side (with its own Display2) is not currently handed off.
            rootLower.style.display = DisplayStyle.Flex;

            YardCraneShellPanelUtility.ReleaseOwnedObject(runtimeFontLower);
            runtimeFontLower = YardCraneShellPanelUtility.CreateShellFont();
            if (runtimeFontLower != null) rootLower.style.unityFontDefinition = FontDefinition.FromFont(runtimeFontLower);

            var lowerPoster = rootLower.Q<Image>("lower-poster");
            lowerPoster.image = lowerPosterTexture;
            lowerPoster.scaleMode = ScaleMode.ScaleToFit;
            rootLower.pickingMode = PickingMode.Ignore;
            rootLower.Query<VisualElement>().ForEach(element => element.pickingMode = PickingMode.Ignore);
            return true;
        }

        private static void RemoveDefaultControlChrome(TextField field)
        {
            var input = field.Q<VisualElement>(className: "unity-text-input") ??
                        field.Q<VisualElement>(className: "unity-base-text-field__input");
            if (input == null) return;
            input.style.backgroundImage = StyleKeyword.None;
            input.style.backgroundColor = new Color(4f / 255f, 16f / 255f, 25f / 255f, 0.68f);
        }

        private void Update()
        {
            if (editorSuspended) return;
            if (!IsReady) BuildInterface();
            if (!IsReady) return;
            UpdateScale();
        }

#if UNITY_EDITOR
        // Editor lifecycle hooks restore scene asset references BEFORE Unity serializes
        // scenes or reloads the domain. They never save the user's scene themselves.
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

        public void EditorPreview(YardCraneShellRole role, YardCraneStudentShellPage page)
        {
            if (Application.isPlaying || editorSuspended || !isActiveAndEnabled) return;
            if (!IsReady) BuildInterface();
            if (!IsReady) return;
            profile = null;
            SetPage(YardCraneStudentShellPage.Login);
            ClearLoginInputs();
            ApplyRole(role);
            root.MarkDirtyRepaint();
        }

        public void EditorRefreshPreviewLayout()
        {
            if (Application.isPlaying || editorSuspended || !isActiveAndEnabled) return;
            if (!IsReady)
            {
                var role = Role;
                BuildInterface();
                if (IsReady) EditorPreview(role, YardCraneStudentShellPage.Login);
            }
            if (!IsReady) return;
            UpdateScale();
            root.MarkDirtyRepaint();
        }
#endif

        private void SubmitOnEnter(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
            Login();
            evt.StopPropagation();
        }

        private void Login()
        {
            if (!Application.isPlaying || !IsReady || Page != YardCraneStudentShellPage.Login) return;
            ApplyBuildProfileCraneKind();
            var nameOrPassword = Role == YardCraneShellRole.Teacher
                ? teacherPasswordField.value
                : displayNameField.value;
            if (!YardCraneLocalStudentProfile.TryLogin(
                    Role, studentNumberField.value, nameOrPassword, out profile, out var error))
            {
                loginError.text = error;
                loginError.style.display = DisplayStyle.Flex;
                return;
            }

            if (Role == YardCraneShellRole.Student &&
                !YardCraneLegacyTaskAssetBridge.TryWriteStudentIdentity(
                    taskStateAsset,
                    profile.StudentNumber,
                    profile.DisplayName,
                    out var identityProblem))
            {
                profile = null;
                loginError.text = $"无法写入学生身份：{identityProblem}";
                loginError.style.display = DisplayStyle.Flex;
                Debug.LogError($"[YC_ENTRY_TASK_STATE_IDENTITY_FAILED] {identityProblem}", this);
                return;
            }

            loginError.text = string.Empty;
            loginError.style.display = DisplayStyle.None;
            var loggedInProfile = profile;
            profile = null;
            ClearLoginInputs();
            Debug.Log($"[YC_ENTRY_LOCAL_LOGIN] role={Role}, crane={SelectedCraneKind}, localIdentityAccepted=true, remoteAuthentication=false", this);

            if (Role == YardCraneShellRole.Student)
            {
                HandOffToStudentSide(loggedInProfile);
                return;
            }

            HandOffToTeacherSide(loggedInProfile);
        }

        // Hands off to the independent StudentHome scene via a real scene switch (not an
        // in-scene object reference - see the 2026-09-08 discussion record for why this
        // replaced the earlier same-scene-second-UIDocument approach). The profile cannot
        // travel as a normal object reference across LoadSceneMode.Single (this scene's
        // objects, including this one, are about to be destroyed), so it goes through the
        // static YardCraneStudentHomeHandoff holder instead. Returning to login is just
        // StudentHome loading this scene back - a fresh instance rebuilds at the login
        // page by its own normal startup path, no explicit "reset" method needed.
        private void HandOffToStudentSide(YardCraneLocalStudentProfile studentProfile)
        {
            if (!Application.CanStreamedLevelBeLoaded(StudentHomeSceneName))
            {
                loginError.text = "学生端首页场景未加入构建，请检查工程配置。";
                loginError.style.display = DisplayStyle.Flex;
                Debug.LogError($"[YC_STUDENT_SHELL_HANDOFF_SCENE_MISSING] '{StudentHomeSceneName}' is not in Build Settings.", this);
                return;
            }
            YardCraneTeacherHomeHandoff.Clear();
            YardCraneStudentHomeHandoff.Begin(studentProfile);
            Debug.Log($"[YC_STUDENT_SHELL_HANDOFF] scene={StudentHomeSceneName}, crane={SelectedCraneKind}, studentNumber={studentProfile.AccountId}", this);
            SceneManager.LoadSceneAsync(StudentHomeSceneName, LoadSceneMode.Single);
        }

        private void HandOffToTeacherSide(YardCraneLocalStudentProfile teacherProfile)
        {
            if (!Application.CanStreamedLevelBeLoaded(TeacherHomeSceneName))
            {
                loginError.text = "教师端首页场景未加入构建，请检查工程配置。";
                loginError.style.display = DisplayStyle.Flex;
                Debug.LogError(
                    $"[YC_TEACHER_SHELL_HANDOFF_SCENE_MISSING] '{TeacherHomeSceneName}' is not in Build Settings.",
                    this);
                return;
            }
            YardCraneStudentHomeHandoff.Clear();
            YardCraneFallbackSession.Clear();
            YardCraneTrainingSessionIdentity.Clear();
            YardCraneTeacherHomeHandoff.Begin(teacherProfile);
            Debug.Log(
                $"[YC_TEACHER_SHELL_HANDOFF] scene={TeacherHomeSceneName}, crane={SelectedCraneKind}, account={teacherProfile.AccountId}",
                this);
            SceneManager.LoadSceneAsync(TeacherHomeSceneName, LoadSceneMode.Single);
        }

        private void SelectStudentRole() => ApplyRole(YardCraneShellRole.Student);
        private void SelectTeacherRole() => ApplyRole(YardCraneShellRole.Teacher);

        private void ApplyRole(YardCraneShellRole role)
        {
            if (Page != YardCraneStudentShellPage.Login) return;
            if (Role != role) ClearLoginInputs();
            Role = role;
            var teacher = role == YardCraneShellRole.Teacher;
            studentRoleButton.EnableInClassList("selected", !teacher);
            teacherRoleButton.EnableInClassList("selected", teacher);
            loginContext.text = "WELCOME ABOARD";
            accountLabel.text = teacher ? "管理员账号" : "学号";
            studentNameGroup.style.display = teacher ? DisplayStyle.None : DisplayStyle.Flex;
            teacherPasswordGroup.style.display = teacher ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshLoginCopy();
            loginError.text = string.Empty;
            loginError.style.display = DisplayStyle.None;
        }

        private void ApplyBuildProfileCraneKind()
        {
            var changed = SelectedCraneKind != ConfiguredCraneKind;
            SelectedCraneKind = ConfiguredCraneKind;
#if !Mode_Anqiao && !Mode_Changqiao
            if (Application.isPlaying && !missingCraneModeDefineWarningLogged)
            {
                missingCraneModeDefineWarningLogged = true;
                Debug.LogWarning(
                    "[YC_CRANE_MODE_DEFINE_MISSING] Build Profile has neither Mode_Anqiao nor " +
                    "Mode_Changqiao; using the compatibility fallback QuayCrane.",
                    this);
            }
#endif
            if (changed && loginTitle != null) RefreshLoginCopy();
        }

        private void RefreshLoginCopy()
        {
            if (loginTitle == null || loginNote == null || loginModeNote == null || loginButton == null) return;
            var teacher = Role == YardCraneShellRole.Teacher;
            var craneName = SelectedCraneKind == YardCraneCraneKind.QuayCrane ? "岸桥" : "场桥";
            loginTitle.text = teacher ? $"{craneName}教师管理员登录" : $"{craneName}学生登录";
            loginNote.text = teacher
                ? $"使用管理员账号，进入{craneName}教学工作台。"
                : $"填写学号和姓名，开始{craneName}实训。";
            loginModeNote.text = $"{craneName} · 本机{(teacher ? "管理员" : "学生")}登录 · 无需网络";
            loginButton.text = teacher ? "进入教学工作台  →" : "进入训练工作台  →";
            if (systemCode != null)
                systemCode.text = SelectedCraneKind == YardCraneCraneKind.QuayCrane
                    ? "QUAY CRANE SIMULATOR"
                    : "YARD CRANE SIMULATOR";
        }

        private void ClearLoginInputs()
        {
            studentNumberField.SetValueWithoutNotify(string.Empty);
            displayNameField.SetValueWithoutNotify(string.Empty);
            teacherPasswordField.SetValueWithoutNotify(string.Empty);
        }

        private void SetPage(YardCraneStudentShellPage _)
        {
            Page = YardCraneStudentShellPage.Login;
            loginPage.style.display = DisplayStyle.Flex;
        }

        private void UpdateScale()
        {
            var target = panelSettings.targetTexture;
            var width = target != null ? target.width : Screen.width;
            var height = target != null ? target.height : Screen.height;
            if (width == lastWidth && height == lastHeight) return;
            lastWidth = width;
            lastHeight = height;
            YardCraneShellPanelUtility.DetectDisplayLayout(width, height, out var isPortrait, out _);
            var shellRoot = root.Q("student-shell");
            shellRoot.EnableInClassList("portrait", isPortrait);
        }

        private void OnDisable()
        {
            ReleaseInterface();
        }

        private void UnbindUpperEvents()
        {
            if (loginButton != null) loginButton.clicked -= Login;
            if (studentRoleButton != null) studentRoleButton.clicked -= SelectStudentRole;
            if (teacherRoleButton != null) teacherRoleButton.clicked -= SelectTeacherRole;
            if (studentNumberField != null) studentNumberField.UnregisterCallback<KeyDownEvent>(SubmitOnEnter);
            if (displayNameField != null) displayNameField.UnregisterCallback<KeyDownEvent>(SubmitOnEnter);
            if (teacherPasswordField != null)
            {
                teacherPasswordField.UnregisterCallback<KeyDownEvent>(SubmitOnEnter);
                teacherPasswordField.SetValueWithoutNotify(string.Empty);
            }
        }

        private void ReleaseInterface()
        {
            built = false;
            UnbindUpperEvents();
            if (root != null) root.style.unityFontDefinition = StyleKeyword.Null;
            if (rootLower != null) rootLower.style.unityFontDefinition = StyleKeyword.Null;
            // Release only the runtime references; each UIDocument keeps using its serialized
            // folder asset across disable and domain reload.
            YardCraneShellPanelUtility.ReleasePanel(document, upperPanelTemplate, ref panelSettings);
            YardCraneShellPanelUtility.ReleasePanel(documentLower, lowerPanelTemplate, ref panelSettingsLower);
            root = null;
            YardCraneShellPanelUtility.ReleaseOwnedObject(runtimeFont);
            runtimeFont = null;
            document = null;

            rootLower = null;
            YardCraneShellPanelUtility.ReleaseOwnedObject(runtimeFontLower);
            runtimeFontLower = null;
            documentLower = null;
        }
    }
}
