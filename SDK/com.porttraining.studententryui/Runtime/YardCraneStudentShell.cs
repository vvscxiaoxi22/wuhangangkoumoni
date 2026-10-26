using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace PortTraining.YardCraneSimulator.Shell
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class YardCraneStudentShell : MonoBehaviour
    {
        public const string Version = "2026-09-07-yc-entry-shell-v7-dual-display";
        public const string SceneName = "StudentEntry";
        public const string BootstrapSceneName = "Bootstrap";

        // Upper screen (Display 1) - the interactive login/home UI.
        private UIDocument document;
        private PanelSettings panelSettings;
        private Font runtimeFont;
        private Camera fallbackCamera;
        private GameObject editorPreviewRoot;
        private VisualElement root;

        // Lower screen (Display 2) - static decorative content, no interaction/state.
        private UIDocument documentLower;
        private PanelSettings panelSettingsLower;
        private Font runtimeFontLower;
        private Camera fallbackCameraLower;
        private GameObject lowerHost;
        private GameObject editorPreviewRootLower;
        private VisualElement rootLower;

        private bool builtForPlayMode;
        private bool displayActivated;
        private VisualElement loginPage;
        private VisualElement studentHomePage;
        private VisualElement teacherHomePage;
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
        private Label greeting;
        private Label identity;
        private Label teacherGreeting;
        private Label teacherIdentity;
        private Label keyboardStatus;
        private Label gamepadStatus;
        private Label launchStatus;
        private Button loginButton;
        private Button studentRoleButton;
        private Button teacherRoleButton;
        private Button logoutButton;
        private Button teacherLogoutButton;
        private Button launchButton;
        private Button quitButton;
        private Button teacherQuitButton;
        private YardCraneLocalStudentProfile profile;
        private float nextDeviceRefresh;
        private int lastWidth;
        private int lastHeight;
        private bool built;

        public bool IsReady => built && root != null && isActiveAndEnabled;
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
            if (gameObject.scene.name != SceneName) return;
            BuildInterface();
        }

        private void BuildInterface()
        {
            ReleaseInterface();
            builtForPlayMode = Application.isPlaying;
            if (Application.isPlaying) ActivateSecondDisplayIfNeeded();

            if (!BuildUpperInterface()) return;
            BuildLowerInterface();

            ApplyTrainingCatalog();
            if (Application.isPlaying) YardCraneFallbackSession.Clear();
            profile = null;
            Page = YardCraneStudentShellPage.Login;
            ApplyRole(YardCraneShellRole.Student);
            SetPage(YardCraneStudentShellPage.Login);
            lastWidth = -1;
            lastHeight = -1;
            UpdateScale();
            if (Application.isPlaying) RefreshDevices();
            built = true;
            if (Application.isPlaying)
                Debug.Log($"[YC_ENTRY_SHELL_READY] version={Version}, roles=student+teacher, mode=local-fallback, authentication=student-identity+local-admin, displays={Display.displays.Length}", this);
        }

        private void ActivateSecondDisplayIfNeeded()
        {
            if (displayActivated || Display.displays.Length <= 1) return;
            Display.displays[1].Activate();
            displayActivated = true;
        }

        private bool BuildUpperInterface()
        {
            var tree = Resources.Load<VisualTreeAsset>("YardCraneShell/StudentShellUpper");
            var stylesheet = Resources.Load<StyleSheet>("YardCraneShell/StudentShellUpperStyles");
            var panelTemplate = Resources.Load<PanelSettings>("YardCraneShell/StudentShellUpperPanel");
            var loginBackdropTexture = Resources.Load<Texture2D>(
                "YardCraneShell/Art/PortTrainingOceanBackdrop_v1");
            if (tree == null || stylesheet == null || panelTemplate == null || loginBackdropTexture == null)
            {
                Debug.LogError("[YC_STUDENT_SHELL_MISSING_ASSET] Upper (Display1) entry cannot be displayed.", this);
                return false;
            }

            panelSettings = Instantiate(panelTemplate);
            panelSettings.hideFlags = HideFlags.HideAndDontSave;
            panelSettings.name = "YC Student Shell Upper Runtime Panel";
            panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            panelSettings.sortingOrder = 5;
            panelSettings.targetDisplay = 0;
            if (Application.isPlaying)
            {
                document = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();
            }
            else
            {
                editorPreviewRoot = new GameObject("YC StudentEntry UI Preview (Upper)")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                SceneManager.MoveGameObjectToScene(editorPreviewRoot, gameObject.scene);
                document = editorPreviewRoot.AddComponent<UIDocument>();
            }
            EnsureBackgroundCamera(0, ref fallbackCamera, editorPreviewRoot, transform,
                new Color(3f / 255f, 43f / 255f, 96f / 255f, 1f), "00_StudentEntryBackgroundCamera_Upper");
            document.panelSettings = panelSettings;
            document.visualTreeAsset = tree;
            document.enabled = true;
            root = document.rootVisualElement;
            root.styleSheets.Add(stylesheet);
            root.style.flexGrow = 1;
            runtimeFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei", "Noto Sans CJK SC", "Arial Unicode MS" }, 24);
            if (runtimeFont != null)
            {
                runtimeFont.hideFlags = HideFlags.HideAndDontSave;
                root.style.unityFontDefinition = FontDefinition.FromFont(runtimeFont);
            }

            loginPage = root.Q("login-page");
            studentHomePage = root.Q("student-home-page");
            teacherHomePage = root.Q("teacher-home-page");
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
            greeting = root.Q<Label>("greeting");
            identity = root.Q<Label>("identity");
            teacherGreeting = root.Q<Label>("teacher-greeting");
            teacherIdentity = root.Q<Label>("teacher-identity");
            keyboardStatus = root.Q<Label>("keyboard-status");
            gamepadStatus = root.Q<Label>("gamepad-status");
            launchStatus = root.Q<Label>("launch-status");
            loginButton = root.Q<Button>("login-button");
            studentRoleButton = root.Q<Button>("role-student");
            teacherRoleButton = root.Q<Button>("role-teacher");
            logoutButton = root.Q<Button>("logout-button");
            teacherLogoutButton = root.Q<Button>("teacher-logout-button");
            launchButton = root.Q<Button>("launch-button");
            quitButton = root.Q<Button>("quit-button");
            teacherQuitButton = root.Q<Button>("teacher-quit-button");
            var loginBackdrop = root.Q<Image>("login-background");
            loginBackdrop.image = loginBackdropTexture;
            loginBackdrop.scaleMode = ScaleMode.ScaleAndCrop;

            RemoveDefaultControlChrome(studentNumberField);
            RemoveDefaultControlChrome(displayNameField);
            RemoveDefaultControlChrome(teacherPasswordField);
            foreach (var button in root.Query<Button>().ToList())
                button.style.backgroundImage = StyleKeyword.None;

            loginButton.clicked += Login;
            studentRoleButton.clicked += SelectStudentRole;
            teacherRoleButton.clicked += SelectTeacherRole;
            logoutButton.clicked += Logout;
            teacherLogoutButton.clicked += Logout;
            launchButton.clicked += LaunchTraining;
            quitButton.clicked += Quit;
            teacherQuitButton.clicked += Quit;
            studentNumberField.RegisterCallback<KeyDownEvent>(SubmitOnEnter);
            displayNameField.RegisterCallback<KeyDownEvent>(SubmitOnEnter);
            teacherPasswordField.RegisterCallback<KeyDownEvent>(SubmitOnEnter);
            foreach (var button in root.Query<Button>().ToList()) button.focusable = false;

            return true;
        }

        private void BuildLowerInterface()
        {
            var tree = Resources.Load<VisualTreeAsset>("YardCraneShell/StudentShellLower");
            var stylesheet = Resources.Load<StyleSheet>("YardCraneShell/StudentShellLowerStyles");
            var panelTemplate = Resources.Load<PanelSettings>("YardCraneShell/StudentShellLowerPanel");
            var lowerPosterTexture = Resources.Load<Texture2D>(
                "YardCraneShell/Art/QingdaoAutomatedTerminalPoster_v2");
            if (tree == null || stylesheet == null || panelTemplate == null || lowerPosterTexture == null)
            {
                Debug.LogError("[YC_STUDENT_SHELL_MISSING_ASSET] Lower (Display2) entry cannot be displayed.", this);
                return;
            }

            panelSettingsLower = Instantiate(panelTemplate);
            panelSettingsLower.hideFlags = HideFlags.HideAndDontSave;
            panelSettingsLower.name = "YC Student Shell Lower Runtime Panel";
            panelSettingsLower.scaleMode = PanelScaleMode.ConstantPixelSize;
            panelSettingsLower.sortingOrder = 5;
            panelSettingsLower.targetDisplay = 1;

            if (Application.isPlaying)
            {
                lowerHost = new GameObject("YC StudentEntry Lower Screen (Display2)");
                lowerHost.transform.SetParent(transform, false);
                documentLower = lowerHost.AddComponent<UIDocument>();
            }
            else
            {
                editorPreviewRootLower = new GameObject("YC StudentEntry UI Preview (Lower)")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                SceneManager.MoveGameObjectToScene(editorPreviewRootLower, gameObject.scene);
                documentLower = editorPreviewRootLower.AddComponent<UIDocument>();
            }
            EnsureBackgroundCamera(1, ref fallbackCameraLower, editorPreviewRootLower,
                lowerHost != null ? lowerHost.transform : transform,
                new Color(4f / 255f, 45f / 255f, 97f / 255f, 1f), "00_StudentEntryBackgroundCamera_Lower");
            documentLower.panelSettings = panelSettingsLower;
            documentLower.visualTreeAsset = tree;
            documentLower.enabled = true;
            rootLower = documentLower.rootVisualElement;
            rootLower.styleSheets.Add(stylesheet);
            rootLower.style.flexGrow = 1;

            runtimeFontLower = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei", "Noto Sans CJK SC", "Arial Unicode MS" }, 24);
            if (runtimeFontLower != null)
            {
                runtimeFontLower.hideFlags = HideFlags.HideAndDontSave;
                rootLower.style.unityFontDefinition = FontDefinition.FromFont(runtimeFontLower);
            }

            var lowerPoster = rootLower.Q<Image>("lower-poster");
            lowerPoster.image = lowerPosterTexture;
            lowerPoster.scaleMode = ScaleMode.ScaleToFit;
            rootLower.pickingMode = PickingMode.Ignore;
            rootLower.Query<VisualElement>().ForEach(element => element.pickingMode = PickingMode.Ignore);
        }

        private void EnsureBackgroundCamera(
            int targetDisplay, ref Camera fallback, GameObject previewRoot, Transform playModeParent,
            Color backgroundColor, string cameraName)
        {
            var activeCameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var candidate in activeCameras)
            {
                if (candidate.enabled && candidate.targetTexture == null && candidate.targetDisplay == targetDisplay)
                    return;
            }

            var cameraObject = new GameObject(cameraName);
            if (previewRoot != null) cameraObject.hideFlags = HideFlags.HideAndDontSave;
            cameraObject.transform.SetParent(previewRoot != null ? previewRoot.transform : playModeParent, false);
            fallback = cameraObject.AddComponent<Camera>();
            fallback.clearFlags = CameraClearFlags.SolidColor;
            fallback.backgroundColor = backgroundColor;
            fallback.cullingMask = 0;
            fallback.depth = -100f;
            fallback.targetDisplay = targetDisplay;
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
            if (built && builtForPlayMode != Application.isPlaying) BuildInterface();
            if (!IsReady) return;
            UpdateScale();
            if (!Application.isPlaying) return;
            if (Time.unscaledTime >= nextDeviceRefresh) RefreshDevices();
        }

#if UNITY_EDITOR
        public void EditorPreview(YardCraneShellRole role, YardCraneStudentShellPage page)
        {
            if (Application.isPlaying || !isActiveAndEnabled || gameObject.scene.name != SceneName) return;
            if (!IsReady) BuildInterface();
            if (!IsReady) return;
            profile = null;
            SetPage(YardCraneStudentShellPage.Login);
            ClearLoginInputs();
            ApplyRole(role);
            greeting.text = "预览学员，准备开始新一次作业？";
            identity.text = "预览学员  ·  场桥司机";
            teacherGreeting.text = "管理员的教学工作台";
            teacherIdentity.text = "管理员  ·  本机管理";
            SetDeviceStatus(keyboardStatus, false, "预览");
            SetDeviceStatus(gamepadStatus, false, "预览");
            SetPage(page == YardCraneStudentShellPage.Home ? page : YardCraneStudentShellPage.Login);
            launchButton.SetEnabled(false);
            launchStatus.text = "点击 Unity Play 后可进入训练";
            root.MarkDirtyRepaint();
        }

        public void EditorRefreshPreviewLayout()
        {
            if (Application.isPlaying || !isActiveAndEnabled || gameObject.scene.name != SceneName) return;
            if (!IsReady || root.Q<Button>("login-button") != loginButton)
            {
                var role = Role;
                var page = Page;
                BuildInterface();
                EditorPreview(role, page);
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
            if (!Application.isPlaying || Page != YardCraneStudentShellPage.Login) return;
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

            loginError.text = string.Empty;
            loginError.style.display = DisplayStyle.None;
            if (Role == YardCraneShellRole.Student)
            {
                greeting.text = $"{profile.DisplayName}，准备开始新一次作业？";
                identity.text = $"学员 {profile.AccountId}  ·  场桥司机  ·  本地单机";
            }
            else
            {
                teacherGreeting.text = $"{profile.DisplayName}的教学工作台";
                teacherIdentity.text = $"{profile.DisplayName} {profile.AccountId}  ·  本机管理";
            }
            ClearLoginInputs();
            SetPage(YardCraneStudentShellPage.Home);
            Debug.Log($"[YC_ENTRY_LOCAL_LOGIN] role={Role}, localIdentityAccepted=true, remoteAuthentication=false", this);
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
            loginTitle.text = teacher ? "教师管理员登录" : "场桥学生登录";
            loginContext.text = "WELCOME ABOARD";
            accountLabel.text = teacher ? "管理员账号" : "学号";
            studentNameGroup.style.display = teacher ? DisplayStyle.None : DisplayStyle.Flex;
            teacherPasswordGroup.style.display = teacher ? DisplayStyle.Flex : DisplayStyle.None;
            loginNote.text = teacher ? "使用管理员账号，进入教学工作台。" : "填写学号和姓名，开始港口实训。";
            loginModeNote.text = teacher ? "本机管理员登录 · 无需网络" : "本机学生登录 · 无需网络";
            loginButton.text = teacher ? "进入教学工作台  →" : "进入训练工作台  →";
            loginError.text = string.Empty;
            loginError.style.display = DisplayStyle.None;
        }

        private void Logout()
        {
            if (!Application.isPlaying) return;
            if (Page == YardCraneStudentShellPage.Launching) return;
            profile = null;
            YardCraneFallbackSession.Clear();
            ClearLoginInputs();
            loginError.style.display = DisplayStyle.None;
            SetPage(YardCraneStudentShellPage.Login);
        }

        private void ClearLoginInputs()
        {
            studentNumberField.SetValueWithoutNotify(string.Empty);
            displayNameField.SetValueWithoutNotify(string.Empty);
            teacherPasswordField.SetValueWithoutNotify(string.Empty);
        }

        private void LaunchTraining()
        {
            if (!Application.isPlaying || Page != YardCraneStudentShellPage.Home || profile == null ||
                !YardCraneStudentShellPolicy.CanLaunchTraining(profile.Role)) return;
            if (!Application.CanStreamedLevelBeLoaded(BootstrapSceneName))
            {
                launchStatus.text = "启动场景未加入构建，请检查工程配置。";
                launchStatus.AddToClassList("danger");
                return;
            }

            YardCraneFallbackSession.Begin(profile, YardCraneLocalTrainingKind.FreePractice);
            SetPage(YardCraneStudentShellPage.Launching);
            launchStatus.text = "正在进入场桥驾驶场景…";
            launchStatus.RemoveFromClassList("danger");
            SceneManager.LoadSceneAsync(BootstrapSceneName, LoadSceneMode.Single);
        }

        private static void Quit()
        {
            if (!Application.isPlaying) return;
#if UNITY_EDITOR
            Debug.Log("[YC_STUDENT_SHELL_QUIT_IGNORED] Quit is disabled in the Unity Editor.");
#else
            Application.Quit(0);
#endif
        }

        private void SetPage(YardCraneStudentShellPage page)
        {
            Page = page;
            loginPage.style.display = page == YardCraneStudentShellPage.Login ? DisplayStyle.Flex : DisplayStyle.None;
            var home = page != YardCraneStudentShellPage.Login;
            studentHomePage.style.display = home && Role == YardCraneShellRole.Student ? DisplayStyle.Flex : DisplayStyle.None;
            teacherHomePage.style.display = home && Role == YardCraneShellRole.Teacher ? DisplayStyle.Flex : DisplayStyle.None;
            launchButton.SetEnabled(page == YardCraneStudentShellPage.Home &&
                                    YardCraneStudentShellPolicy.CanLaunchTraining(Role));
            logoutButton.SetEnabled(page == YardCraneStudentShellPage.Home);
            teacherLogoutButton.SetEnabled(page == YardCraneStudentShellPage.Home);
        }

        private void ApplyTrainingCatalog()
        {
            foreach (var option in YardCraneStudentShellPolicy.Options)
            {
                var id = option.Kind switch
                {
                    YardCraneLocalTrainingKind.FreePractice => "free",
                    YardCraneLocalTrainingKind.PickupWorkflow => "pickup",
                    _ => "dropoff"
                };
                root.Q<Label>($"task-{id}-title").text = option.Title;
                root.Q<Label>($"task-{id}-description").text = option.Description;
                root.Q<Label>($"task-{id}-availability").text = option.AvailabilityText;
                root.Q($"task-{id}").EnableInClassList("unavailable", !option.IsAvailable);
            }
        }

        private void RefreshDevices()
        {
            nextDeviceRefresh = Time.unscaledTime + 0.5f;
            SetDeviceStatus(keyboardStatus, Keyboard.current != null, Keyboard.current != null ? "已连接" : "等待输入");
            SetDeviceStatus(gamepadStatus, Gamepad.current != null, Gamepad.current != null ? "已连接" : "未连接 · 可选");
        }

        private static void SetDeviceStatus(Label label, bool ready, string text)
        {
            label.text = text;
            label.EnableInClassList("good", ready);
            label.EnableInClassList("muted", !ready);
        }

        private void UpdateScale()
        {
            var target = panelSettings.targetTexture;
            var width = target != null ? target.width : Screen.width;
            var height = target != null ? target.height : Screen.height;
            if (width == lastWidth && height == lastHeight) return;
            lastWidth = width;
            lastHeight = height;
            DetectDisplayLayout(width, height, out var isPortrait, out var scale);
            panelSettings.scale = scale;
            var shellRoot = root.Q("student-shell");
            shellRoot.EnableInClassList("portrait", isPortrait);
        }

        // Display1 (this screen) is now a real, independent 1920x1080 output - the old
        // "one 1920x2160 desktop split via CSS" simulation is gone. The only remaining
        // shape variation this shell needs to detect on its own (no dependency on the
        // shared Runtime/Presentation display-profile type used by the driving HUD, so
        // this package stays extractable) is a genuinely portrait single-screen deployment.
        private static void DetectDisplayLayout(int width, int height, out bool isPortrait, out float scale)
        {
            if (width <= 0 || height <= 0)
            {
                isPortrait = false;
                scale = 1f;
                return;
            }

            int referenceWidth;
            int referenceHeight;
            if (height > width)
            {
                isPortrait = true;
                referenceWidth = 1080;
                referenceHeight = 1920;
            }
            else
            {
                isPortrait = false;
                referenceWidth = 1920;
                referenceHeight = 1080;
            }

            scale = Math.Max(0.1f, Math.Min(width / (float)referenceWidth, height / (float)referenceHeight));
        }

        private void OnDisable()
        {
            ReleaseInterface();
        }

        private void ReleaseInterface()
        {
            built = false;
            if (loginButton != null) loginButton.clicked -= Login;
            if (studentRoleButton != null) studentRoleButton.clicked -= SelectStudentRole;
            if (teacherRoleButton != null) teacherRoleButton.clicked -= SelectTeacherRole;
            if (logoutButton != null) logoutButton.clicked -= Logout;
            if (teacherLogoutButton != null) teacherLogoutButton.clicked -= Logout;
            if (launchButton != null) launchButton.clicked -= LaunchTraining;
            if (quitButton != null) quitButton.clicked -= Quit;
            if (teacherQuitButton != null) teacherQuitButton.clicked -= Quit;
            if (studentNumberField != null) studentNumberField.UnregisterCallback<KeyDownEvent>(SubmitOnEnter);
            if (displayNameField != null) displayNameField.UnregisterCallback<KeyDownEvent>(SubmitOnEnter);
            if (teacherPasswordField != null)
            {
                teacherPasswordField.UnregisterCallback<KeyDownEvent>(SubmitOnEnter);
                teacherPasswordField.SetValueWithoutNotify(string.Empty);
            }
            if (document != null) document.enabled = false;
            root = null;
            ReleaseOwnedObject(fallbackCamera != null ? fallbackCamera.gameObject : null);
            ReleaseOwnedObject(editorPreviewRoot);
            ReleaseOwnedObject(panelSettings);
            ReleaseOwnedObject(runtimeFont);
            fallbackCamera = null;
            editorPreviewRoot = null;
            panelSettings = null;
            runtimeFont = null;
            document = null;

            if (documentLower != null) documentLower.enabled = false;
            rootLower = null;
            ReleaseOwnedObject(fallbackCameraLower != null ? fallbackCameraLower.gameObject : null);
            ReleaseOwnedObject(editorPreviewRootLower);
            ReleaseOwnedObject(lowerHost);
            ReleaseOwnedObject(panelSettingsLower);
            ReleaseOwnedObject(runtimeFontLower);
            fallbackCameraLower = null;
            editorPreviewRootLower = null;
            lowerHost = null;
            panelSettingsLower = null;
            runtimeFontLower = null;
            documentLower = null;
        }

        private static void ReleaseOwnedObject(UnityEngine.Object ownedObject)
        {
            if (ownedObject == null) return;
            if (Application.isPlaying) Destroy(ownedObject);
            else DestroyImmediate(ownedObject);
        }
    }
}
