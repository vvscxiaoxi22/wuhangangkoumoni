using System;
using System.IO;
using PortTraining.YardCraneSimulator.Bootstrap;
using PortTraining.YardCraneSimulator.Experiments.RopePhysics;
using PortTraining.YardCraneSimulator.Presentation;
using PortTraining.YardCraneSimulator.Shell;
using PortTraining.YardCraneSimulator.Training;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace PortTraining.YardCraneSimulator.Editor
{
    /// <summary>Real Play-mode UI geometry/readback checks; does not submit machine commands.</summary>
    [InitializeOnLoad]
    public static class YardCraneHudVerification
    {
        private const string RunningKey = "YC_UI_VERIFY_RUNNING";
        private const string RestoreKey = "YC_UI_VERIFY_RESTORE";
        private const string OriginalSceneKey = "YC_UI_VERIFY_ORIGINAL_SCENE";
        private const string OutputDirectoryKey = "YC_UI_VERIFY_OUTPUT_DIRECTORY";
        private const string BootstrapScenePath = "Assets/YardCraneSimulator/Scenes/Bootstrap.unity";
        private static readonly string[] PromptIds =
        {
            "task-prompt", "countdown-prompt", "confirmation-prompt", "error-prompt",
            "completion-prompt", "assessment-prompt", "submission-prompt"
        };
        private static readonly string RequestPath = Path.GetFullPath("Temp/YC_UiVerification.request");
        private static readonly string ResultPath = Path.GetFullPath("Logs/YC_UiVerification.result.txt");
        private static int step;
        private static double startedAt, waitUntil;
        private static YardCraneOperatorHud hud;
        private static RenderTexture target;
        private static RenderTexture previousTarget;
        private static Camera[] sceneCameras;
        private static RenderTexture[] previousCameraTargets;
        private static YardCraneDisplayCameraLayout displayLayout;
        private static YardCraneSceneStateMachine lifecycle;
        private static string testOutputDirectory;
        private static string scoreIndexPath;
        private static int countdownCallbackCount;
        private static int confirmationCallbackCount;
        private static int confirmationCancelCallbackCount;
        private static int errorCallbackCount;
        private static int completionDetailsCallbackCount;
        private static int submissionCallbackCount;
        private static YardCraneHudScoreSubmission capturedSubmission;

        static YardCraneHudVerification()
        {
            RestoreOutputOverrideIfRunning();
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Tools/Yard Crane/Tests/Verify UI Toolkit HUD")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Start UI verification in Edit mode; it will own and stop its Play session.");
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save your scene before UI verification.");
            InvalidatePreviousResult();
            SessionState.SetBool(RunningKey, true);
            SessionState.SetBool(RestoreKey, false);
            try
            {
                YardCraneFallbackSession.Clear();
                YardCraneTrainingSessionIdentity.Clear();
                YardCraneStudentHomeHandoff.Consume();
                countdownCallbackCount = 0;
                confirmationCallbackCount = 0;
                confirmationCancelCallbackCount = 0;
                errorCallbackCount = 0;
                completionDetailsCallbackCount = 0;
                submissionCallbackCount = 0;
                capturedSubmission = default;
                testOutputDirectory = Path.Combine(
                    Path.GetFullPath("Temp/YardCraneVerificationScores"),
                    "Hud-" + Guid.NewGuid().ToString("N"));
                SessionState.SetString(OutputDirectoryKey, testOutputDirectory);
                YardCraneTrainingScoreRecorder.EditorOutputDirectoryOverride = testOutputDirectory;
                SessionState.SetString(OriginalSceneKey, EditorSceneManager.GetActiveScene().path);
                EditorSceneManager.OpenScene(BootstrapScenePath);
                RestoreOutputOverrideIfRunning();
                EditorApplication.isPlaying = true;
            }
            catch
            {
                SessionState.SetBool(RunningKey, false);
                SessionState.SetBool(RestoreKey, true);
                ReleaseVerificationStateAfterTeardown();
                throw;
            }
        }

        private static void RestoreOutputOverrideIfRunning()
        {
            if (!SessionState.GetBool(RunningKey, false)) return;
            testOutputDirectory = SessionState.GetString(OutputDirectoryKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(testOutputDirectory))
                YardCraneTrainingScoreRecorder.EditorOutputDirectoryOverride = testOutputDirectory;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            if (SessionState.GetBool(RunningKey, false))
            {
                SessionState.SetBool(RunningKey, false);
                SessionState.SetBool(RestoreKey, true);
            }
            ReleaseVerificationStateAfterTeardown();
        }

        private static void ReleaseVerificationStateAfterTeardown()
        {
            YardCraneTrainingScoreRecorder.EditorOutputDirectoryOverride = null;
            YardCraneFallbackSession.Clear();
            YardCraneTrainingSessionIdentity.Clear();
            YardCraneStudentHomeHandoff.Consume();
            SessionState.SetString(OutputDirectoryKey, string.Empty);
            testOutputDirectory = null;
            scoreIndexPath = null;
        }

        // Use an isolated workspace and omit -quit; Update exits after its result.
        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch only.");
            Run();
        }

        private static void Update()
        {
            if (SessionState.GetBool(RestoreKey, false) &&
                !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling)
            {
                SessionState.SetBool(RestoreKey, false);
                var original = SessionState.GetString(OriginalSceneKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(original) && File.Exists(original))
                    EditorSceneManager.OpenScene(original);
                ReleaseVerificationStateAfterTeardown();
            }

            // Recover a cancelled Enter Play Mode even when Run() was invoked from the menu
            // and no request file exists. Leaving the override active could redirect a real
            // student's next score into Temp.
            if (SessionState.GetBool(RunningKey, false) &&
                !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling)
            {
                SessionState.SetBool(RunningKey, false);
                SessionState.SetBool(RestoreKey, true);
                ReleaseVerificationStateAfterTeardown();
                step = 0;
                startedAt = 0;
                waitUntil = 0;
                return;
            }

            if (!SessionState.GetBool(RunningKey, false))
            {
                if (File.Exists(RequestPath) && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
                {
                    var request = File.ReadAllText(RequestPath).Trim();
                    if (request == "save-refresh")
                    {
                        var activeScene = EditorSceneManager.GetActiveScene();
                        if (activeScene.isDirty &&
                            (string.IsNullOrWhiteSpace(activeScene.path) || !EditorSceneManager.SaveScene(activeScene)))
                        {
                            File.Delete(RequestPath);
                            Finish(false, "Could not save the current scene before UI verification.");
                            return;
                        }
                        File.WriteAllText(RequestPath, "refresh");
                        Debug.Log($"[YC_UI_VERIFY_SCENE_SAVED] scene={activeScene.path}");
                        return;
                    }
                    if (request == "refresh")
                    {
                        File.WriteAllText(RequestPath, "run");
                        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                        return;
                    }
                    File.Delete(RequestPath);
                    try { Run(); }
                    catch (Exception error) { Finish(false, error.Message); }
                }
                return;
            }
            if (!EditorApplication.isPlaying) return;
            var now = EditorApplication.timeSinceStartup;
            if (startedAt == 0) startedAt = now;
            if (now - startedAt > 60) { Finish(false, "Timed out waiting for ready UI."); return; }
            if (now < waitUntil) return;
            try
            {
                if (step == 0)
                {
                    hud = UnityEngine.Object.FindFirstObjectByType<YardCraneOperatorHud>();
                    var rig = UnityEngine.Object.FindFirstObjectByType<YardCraneEightRopeMaxPrototypeRig>();
                    if (hud == null || !hud.IsReady || hud.Feed.IsStale || rig == null || !rig.LoopsReady) return;
                    Require(hud.Feed.Latest.Status.Has(YardCraneHudSignals.Locked) == rig.ContainerLocked, "lock-readback");
                    Require(hud.Feed.Latest.Status.Has(YardCraneHudSignals.SpreaderContact) == rig.SpreaderContactConfirmed,
                        "spreader-contact-readback");
                    Require(hud.VisualRoot.Q("lamp-touchdown-dot").ClassListContains("lamp-on") ==
                            hud.Feed.Latest.Status.Has(YardCraneHudSignals.SpreaderContact),
                        "touchdown-lamp-matches-spreader-contact");
                    Require(Mathf.Abs(hud.Feed.Latest.Telemetry.GantryPosition - rig.GantryPositionMeters) < 0.001f, "gantry-readback");
                    Require(hud.Feed.Latest.Status.Has(YardCraneHudSignals.BrakeKnown) == rig.GantryBrakeKnown &&
                            hud.Feed.Latest.Status.Has(YardCraneHudSignals.BrakeApplied) == rig.GantryBrakeApplied,
                        "gantry-brake-readback");
                    Require(hud.Feed.Latest.Status.Has(YardCraneHudSignals.TrolleyBrakeKnown) == rig.TrolleyBrakeKnown &&
                            hud.Feed.Latest.Status.Has(YardCraneHudSignals.TrolleyBrakeApplied) == rig.TrolleyBrakeApplied,
                        "trolley-brake-readback");
                    Require(hud.VisualRoot.Q("lock-pin-on-dot").ClassListContains("lamp-on") ==
                            (rig.GantryBrakeKnown && rig.GantryBrakeApplied),
                        "gantry-brake-lamp-matches-readback");
                    Require(hud.VisualRoot.Q("lock-pin-off-dot").ClassListContains("lamp-on") ==
                            (rig.TrolleyBrakeKnown && rig.TrolleyBrakeApplied),
                        "trolley-brake-lamp-matches-readback");
                    Require(hud.VisualRoot.Q("lamp-anchor-dot").ClassListContains("lamp-on") !=
                        hud.VisualRoot.Q("lamp-unlock-dot").ClassListContains("lamp-on"),
                        "lock-and-unlock-status-lamps-are-mutually-exclusive");
                    Require(!hud.VisualRoot.Q<Button>("guide-toggle").focusable, "guide-button-cannot-take-driving-focus");
                    Require(!hud.VisualRoot.Q<Button>("back-button").focusable, "back-button-cannot-take-driving-focus");
                    Require(!hud.VisualRoot.Q<Button>("exit-button").focusable, "exit-button-cannot-take-driving-focus");
                    Require(hud.NavigationCapabilities == YardCraneHudNavigationCapabilities.None &&
                            !hud.VisualRoot.Q<Button>("back-button").enabledSelf &&
                            !hud.VisualRoot.Q<Button>("exit-button").enabledSelf,
                        "direct-launch-navigation-is-visibly-disabled-without-an-executor");
                    Require(hud.VisualRoot.Q<Label>("navigation-note").resolvedStyle.display == DisplayStyle.Flex,
                        "direct-launch-shows-navigation-unavailable-reason");

                    var activeScene = SceneManager.GetActiveScene();
                    Require(activeScene.name == "YardCrane", "formal-operational-scene-is-active");
                    Require(hud.gameObject.scene == activeScene && hud.gameObject.name == "UIDocumentDisplay1",
                        "hud-is-owned-by-active-yard-crane-scene-as-uidocument-display1");

                    var runtimeDocuments = UnityEngine.Object.FindObjectsByType<UIDocument>(
                        FindObjectsInactive.Include, FindObjectsSortMode.None);
                    var runtimeHuds = UnityEngine.Object.FindObjectsByType<YardCraneOperatorHud>(
                        FindObjectsInactive.Include, FindObjectsSortMode.None);
                    var runtimeAdapters = UnityEngine.Object.FindObjectsByType<YardCraneHudSceneAdapter>(
                        FindObjectsInactive.Include, FindObjectsSortMode.None);
                    Require(runtimeDocuments.Length == 1 && runtimeHuds.Length == 1 && runtimeAdapters.Length == 1,
                        $"formal-runtime-has-one-ui-owner, documents={runtimeDocuments.Length}, " +
                        $"huds={runtimeHuds.Length}, adapters={runtimeAdapters.Length}");
                    Require(runtimeDocuments[0].gameObject == hud.gameObject &&
                            runtimeHuds[0].gameObject == hud.gameObject &&
                            runtimeAdapters[0].gameObject == hud.gameObject,
                        "uidocument-hud-and-adapter-share-scene-owned-display1-object");
                    Require(runtimeDocuments[0].panelSettings != null &&
                            runtimeDocuments[0].panelSettings.targetDisplay == 0,
                        "uidocument-display1-targets-unity-display-index-zero");

                    sceneCameras = FindSceneComponents<Camera>(activeScene);
                    var sceneListeners = FindSceneComponents<AudioListener>(activeScene);
                    var enabledSceneListeners = Array.FindAll(sceneListeners, item => item.isActiveAndEnabled);
                    var sceneLayouts = FindSceneComponents<YardCraneDisplayCameraLayout>(activeScene);
                    Require(sceneCameras.Length == 2, $"formal-scene-has-exactly-two-cameras:{sceneCameras.Length}");
                    Require(sceneLayouts.Length == 1, $"formal-scene-has-one-display-layout:{sceneLayouts.Length}");
                    Require(enabledSceneListeners.Length == 1,
                        $"formal-scene-has-one-enabled-audio-listener:{enabledSceneListeners.Length}");
                    Require(Array.FindAll(sceneCameras, item => item.CompareTag("MainCamera")).Length == 1,
                        "formal-scene-has-exactly-one-main-camera");
                    displayLayout = sceneLayouts[0];
                    Require(displayLayout.Display1Camera != null && displayLayout.Display2Camera != null &&
                            displayLayout.Display1Camera.gameObject.scene == activeScene &&
                            displayLayout.Display2Camera.gameObject.scene == activeScene &&
                            displayLayout.Display1Camera.targetDisplay == 0 &&
                            displayLayout.Display2Camera.targetDisplay == 1 &&
                            RectEquals(displayLayout.Display1Camera.rect, new Rect(0f, 0f, 1f, 1f)) &&
                            RectEquals(displayLayout.Display2Camera.rect, new Rect(0f, 0f, 1f, 1f)) &&
                            enabledSceneListeners[0].gameObject == displayLayout.Display1Camera.gameObject,
                        "display-layout-owns-two-scene-cameras-and-display1-owns-listener");

                    var bootstrap = UnityEngine.Object.FindFirstObjectByType<YardCraneBootstrap>();
                    if (bootstrap != null)
                    {
                        Require(!hud.transform.IsChildOf(bootstrap.transform),
                            "normal-launch-ui-remains-yard-crane-scene-owned");
                        lifecycle = bootstrap.StateMachine;
                        hud.BindSessionMode(YardCraneLaunchMode.Managed);
                        Require(hud.VisualRoot.Q<Label>("stage").text.StartsWith("任务训练 ·", StringComparison.Ordinal),
                            "managed-session-is-not-mislabeled-as-free-practice");
                        Require(hud.NavigationCapabilities == YardCraneHudNavigationCapabilities.None &&
                                !hud.VisualRoot.Q<Button>("back-button").enabledSelf &&
                                !hud.VisualRoot.Q<Button>("exit-button").enabledSelf &&
                                hud.VisualRoot.Q<Label>("navigation-note").text.Contains("平台"),
                            "managed-navigation-stays-disabled-until-a-coordinator-binds-actions");
                        hud.BindLocalFreePractice();
                        Require(hud.VisualRoot.Q<Label>("stage").text.StartsWith("自由练习 ·", StringComparison.Ordinal),
                            "local-free-practice-label-requires-explicit-local-context");
                        Require(hud.NavigationCapabilities == YardCraneHudNavigationCapabilities.None,
                            "changing-session-label-does-not-fabricate-navigation-capability");
                        hud.BindSessionMode(bootstrap.LaunchOptions.LaunchMode);
                    }
                    else
                    {
                        lifecycle = CreateRunningLifecycle();
                        hud.BindLifecycle(lifecycle);
                    }
                    previousTarget = hud.GetComponent<UIDocument>().panelSettings.targetTexture;
                    previousCameraTargets = Array.ConvertAll(sceneCameras, item => item.targetTexture);
                    SetTarget(1920, 1080);
                }
                else if (step == 1)
                {
                    Require(!hud.VisualRoot.Q("operator-hud").ClassListContains("dual-stacked"),
                        "native-display1-uses-independent-1920x1080-layout");
                    Require(displayLayout.AppliedLayout == YardCraneDisplayLayout.Landscape16By9 &&
                            displayLayout.Display1Camera.enabled && displayLayout.Display2Camera.enabled &&
                            displayLayout.Display1Camera.targetDisplay == 0 &&
                            displayLayout.Display2Camera.targetDisplay == 1 &&
                            RectEquals(displayLayout.Display1Camera.rect, new Rect(0f, 0f, 1f, 1f)) &&
                            RectEquals(displayLayout.Display2Camera.rect, new Rect(0f, 0f, 1f, 1f)),
                        "native-display-cameras-remain-full-screen-on-display1-and-display2");
                    Require(displayLayout.Display1Camera.targetTexture == target &&
                            displayLayout.Display2Camera.targetTexture == null,
                        "verification-target-is-confined-to-display1-camera");
                    Capture("display1-driving"); VerifyLayout("display1-driving", 1080f);
                    ClickGuide();
                }
                else if (step == 2)
                {
                    VerifyLayout("display1-guide", 1080f);
                    Require(hud.VisualRoot.Q("guide-card").resolvedStyle.display == DisplayStyle.Flex, "guide-open");
                    Require(hud.VisualRoot.Q("left-telemetry").resolvedStyle.display == DisplayStyle.None, "guide-replaces-not-overlaps-left-telemetry");
                    foreach (var id in new[] { "right-task-panel", "right-pose-panel", "right-lock-panel" })
                        Require(hud.VisualRoot.Q(id).resolvedStyle.display == DisplayStyle.Flex, "safety-always-visible:" + id);
                    Capture("display1-guide");
                    ClickGuide();
                }
                else if (step == 3)
                {
                    Require(!hud.VisualRoot.Q("operator-hud").ClassListContains("dual-stacked"),
                        "seven-prompt-sequence-stays-on-independent-display1");
                    VerifyLayout("display1-before-prompts", 1080f);
                }
                else if (step == 4)
                {
                    Require(hud.ActivePromptKind == YardCraneHudPromptKind.None &&
                            CountVisiblePrompts() == 0 && !hud.IsPromptVisible,
                        "all-seven-prompts-start-hidden");
                    hud.ShowTaskPrompt(new YardCraneHudTaskPrompt
                    {
                        Title = "任务提示 · 验证",
                        Instruction = "请将验证集装箱放置到指定位置",
                        SourceBay = "53",
                        DestinationBay = "55",
                        TargetContainer = "VERIFY-BOX",
                        DestinationSlot = "VERIFY-SLOT",
                        SourceCellIndex = 27,
                        DestinationCellIndex = 74
                    });
                }
                else if (step == 5)
                {
                    VerifySinglePrompt(YardCraneHudPromptKind.TaskBrief, "task-prompt", "prompt-task");
                    Require(hud.VisualRoot.Q<Label>("prompt-task-container").text == "VERIFY-BOX" &&
                            hud.VisualRoot.Q<Label>("prompt-task-slot").text == "VERIFY-SLOT",
                        "task-prompt-maps-caller-data");
                    Require(hud.VisualRoot.Q("task-source-grid").Query<VisualElement>(className: "target-cell").ToList().Count == 1 &&
                            hud.VisualRoot.Q("task-destination-grid").Query<VisualElement>(className: "target-cell").ToList().Count == 1,
                        "task-prompt-highlights-one-source-and-one-destination-cell");
                    Capture("prompt-task");
                    hud.ShowCountdownPrompt(
                        new YardCraneHudCountdownPrompt { Title = "倒计时 · 验证", TotalSeconds = 5 },
                        () => countdownCallbackCount++);
                }
                else if (step == 6)
                {
                    VerifySinglePrompt(YardCraneHudPromptKind.Countdown, "countdown-prompt", "prompt-countdown");
                    Require(int.TryParse(hud.VisualRoot.Q<Label>("countdown-value").text, out var remaining) &&
                            remaining > 0 && remaining <= 5,
                        "countdown-uses-live-monotonic-remaining-time");
                    Capture("prompt-countdown");
                    hud.ShowConfirmationPrompt(
                        new YardCraneHudConfirmationPrompt
                        {
                            Title = "提示信息 · 验证",
                            Message = "是否继续下一个验证任务？"
                        },
                        () => confirmationCallbackCount++,
                        () => confirmationCancelCallbackCount++);
                }
                else if (step == 7)
                {
                    VerifySinglePrompt(YardCraneHudPromptKind.Confirmation, "confirmation-prompt", "prompt-confirmation");
                    Require(countdownCallbackCount == 0, "replacing-countdown-does-not-fire-stale-callback");
                    Capture("prompt-confirmation");
                    ClickButton("confirmation-prompt-close", "confirmation-close-pointer-hit");
                    Require(confirmationCallbackCount == 0 &&
                            confirmationCancelCallbackCount == 1 &&
                            hud.ActivePromptKind == YardCraneHudPromptKind.None,
                        "confirmation-close-has-cancel-semantics");
                    hud.ShowConfirmationPrompt(
                        new YardCraneHudConfirmationPrompt
                        {
                            Title = "提示信息 · 确认验证",
                            Message = "确认按钮必须触发独立确认回调"
                        },
                        () => confirmationCallbackCount++,
                        () => confirmationCancelCallbackCount++);
                    ClickButton("confirmation-confirm", "confirmation-confirm-pointer-hit");
                    Require(confirmationCallbackCount == 1 &&
                            confirmationCancelCallbackCount == 1 &&
                            hud.ActivePromptKind == YardCraneHudPromptKind.None,
                        "confirmation-callback-fires-once-and-closes-own-prompt");
                    hud.ShowErrorPrompt(
                        new YardCraneHudErrorPrompt
                        {
                            Title = "错误提示 · 验证",
                            Message = "验证放箱位置不正确，请检查后重新操作。"
                        },
                        () => errorCallbackCount++);
                }
                else if (step == 8)
                {
                    VerifySinglePrompt(YardCraneHudPromptKind.Error, "error-prompt", "prompt-error");
                    Capture("prompt-error");
                    ClickButton("error-prompt-close", "error-close-pointer-hit");
                    Require(errorCallbackCount == 1 && hud.ActivePromptKind == YardCraneHudPromptKind.None,
                        "error-close-has-acknowledge-semantics");
                    hud.ShowErrorPrompt(
                        new YardCraneHudErrorPrompt
                        {
                            Title = "错误提示 · 按钮验证",
                            Message = "确认按钮必须触发独立已读回调。"
                        },
                        () => errorCallbackCount++);
                    ClickButton("error-acknowledge", "error-acknowledge-pointer-hit");
                    Require(errorCallbackCount == 2 && hud.ActivePromptKind == YardCraneHudPromptKind.None,
                        "error-acknowledge-fires-once-and-closes-own-prompt");
                    hud.ShowCompletionPrompt(
                        new YardCraneHudCompletionPrompt
                        {
                            Title = "任务完成 · 验证",
                            ElapsedSeconds = 1800,
                            Score = 96
                        },
                        () => completionDetailsCallbackCount++);
                }
                else if (step == 9)
                {
                    VerifySinglePrompt(YardCraneHudPromptKind.Completion, "completion-prompt", "prompt-completion");
                    Require(hud.VisualRoot.Q<Label>("completion-duration").text == "30 min" &&
                            hud.VisualRoot.Q<Label>("completion-score").text == "96",
                        "completion-prompt-maps-frozen-result-values");
                    Capture("prompt-completion");
                    ClickButton("completion-details", "completion-details-pointer-hit");
                    Require(completionDetailsCallbackCount == 1 && hud.ActivePromptKind == YardCraneHudPromptKind.None,
                        "completion-details-fires-once-and-closes-own-prompt");
                    hud.ShowAssessmentPrompt(new YardCraneHudAssessmentPrompt
                    {
                        Title = "考核详情 · 验证",
                        Project = "单项操作 · 大车移动（验证）",
                        ElapsedSeconds = 780,
                        Score = 92,
                        Lines = new[]
                        {
                            new YardCraneHudAssessmentLine("大车启动速度过快", 5, "前4秒超过限制"),
                            new YardCraneHudAssessmentLine("露出半个锁孔", 5, "对箱操作检测"),
                            new YardCraneHudAssessmentLine("超出停顿次数", 3, "验证说明"),
                            new YardCraneHudAssessmentLine("小车速度超限", 2, "滚动行验证一"),
                            new YardCraneHudAssessmentLine("吊具摆动超限", 4, "滚动行验证二"),
                            new YardCraneHudAssessmentLine("落箱偏差", 6, "滚动行验证三")
                        }
                    });
                }
                else if (step == 10)
                {
                    VerifySinglePrompt(YardCraneHudPromptKind.AssessmentDetails, "assessment-prompt", "prompt-assessment");
                    var table = hud.VisualRoot.Q<ScrollView>("assessment-table-body");
                    Require(table.contentContainer.childCount == 6,
                        "assessment-prompt-renders-each-deduction-row");
                    Require(table.pickingMode == PickingMode.Position &&
                            table.verticalScroller.pickingMode == PickingMode.Position &&
                            table.verticalScroller.highValue > table.verticalScroller.lowValue,
                        "assessment-overflow-is-scrollable-and-interactive");
                    table.scrollOffset = new Vector2(0f, table.verticalScroller.highValue);
                    Require(table.scrollOffset.y > 0f, "assessment-scroll-offset-can-reach-later-rows");
                    Capture("prompt-assessment");
                    hud.ShowScoreSubmissionPrompt(new YardCraneHudScoreSubmissionPrompt
                    {
                        Title = "成绩提交 · 无执行方验证"
                    });
                }
                else if (step == 11)
                {
                    VerifySinglePrompt(YardCraneHudPromptKind.ScoreSubmission, "submission-prompt", "prompt-submission-disabled");
                    Require(!hud.VisualRoot.Q<Button>("submission-submit").enabledSelf &&
                            hud.VisualRoot.Q<Label>("submission-note").text.Contains("尚未接入") &&
                            string.IsNullOrEmpty(hud.VisualRoot.Q<TextField>("submission-name").value) &&
                            string.IsNullOrEmpty(hud.VisualRoot.Q<TextField>("submission-student-number").value),
                        "submission-without-authority-is-disabled-and-does-not-invent-identity");
                    hud.ShowScoreSubmissionPrompt(
                        new YardCraneHudScoreSubmissionPrompt
                        {
                            Title = "成绩提交 · 验证",
                            DisplayName = "验收学员",
                            StudentNumber = "VERIFY-001",
                            Major = "验证专业",
                            ClassName = "验证班级"
                        },
                        value =>
                        {
                            submissionCallbackCount++;
                            capturedSubmission = value;
                        });
                }
                else if (step == 12)
                {
                    VerifySinglePrompt(YardCraneHudPromptKind.ScoreSubmission, "submission-prompt", "prompt-submission");
                    Require(hud.VisualRoot.Q<Button>("submission-submit").enabledSelf,
                        "submission-with-valid-identity-and-executor-is-enabled");
                    Capture("prompt-submission");
                    ClickButton("submission-submit", "submission-submit-pointer-hit");
                }
                else if (step == 13)
                {
                    Require(submissionCallbackCount == 1 &&
                            capturedSubmission.StudentNumber == "VERIFY-001" &&
                            capturedSubmission.DisplayName == "验收学员",
                        "submission-sends-caller-identity-once");
                    Require(hud.ActivePromptKind == YardCraneHudPromptKind.ScoreSubmission &&
                            !hud.VisualRoot.Q<Button>("submission-submit").enabledSelf &&
                            hud.VisualRoot.Q<Label>("submission-note").text.Contains("等待"),
                        "submission-waits-for-authoritative-receipt-instead-of-claiming-success");
                    Require(countdownCallbackCount == 0, "superseded-countdown-never-fires-later");
                    hud.ClosePrompt();
                    Require(CountVisiblePrompts() == 0 && !hud.IsPromptVisible,
                        "closing-prompt-clears-all-seven-prompt-states");
                }
                else if (step == 14)
                {
                    Require(!hud.VisualRoot.Q("operator-hud").ClassListContains("prompt-open") &&
                            hud.VisualRoot.Q("nav-buttons").resolvedStyle.display == DisplayStyle.Flex,
                        "closing-prompt-clears-modal-state-with-base-hud-still-visible");
                    SetTarget(1920, 1080);
                    hud.GetComponent<YardCraneHudSceneAdapter>().enabled = false;
                }
                else if (step == 15)
                {
                    Require(!hud.VisualRoot.Q("operator-hud").ClassListContains("dual-stacked"),
                        "1920x1080-display1-remains-independent-after-prompts");
                    Capture("display1-source-loss");
                    VerifyLayout("display1-source-loss", 1080f);
                    Require(hud.Feed.IsStale, "missing-source-is-stale");
                    Require(hud.VisualRoot.Q<Label>("gantry-position").text == "—", "missing-source-clears-number");
                    foreach (var id in new[]
                             {
                                 "lamp-touchdown-dot", "lamp-anchor-dot", "lamp-unlock-dot", "lamp-single-dot",
                                 "lamp-double-dot", "lock-pin-on-dot", "lock-pin-off-dot"
                             })
                        Require(!hud.VisualRoot.Q(id).ClassListContains("lamp-on"), "missing-source-clears-lamp:" + id);
                    Require(hud.VisualRoot.Q("alert-strip").resolvedStyle.display == DisplayStyle.Flex, "missing-source-shows-alert-strip");
                    Require(hud.VisualRoot.Q<Label>("stage").text != "—", "task-capsule-never-shows-a-bare-dash");
                    hud.GetComponent<YardCraneHudSceneAdapter>().enabled = true;
                }
                else if (step == 16)
                {
                    // Not asserting alert-strip is hidden here: recovery only guarantees staleness
                    // clears, not that every other Alert() precondition (Ready/emergency/etc.) in
                    // this particular demo/regression rig state happens to be calm at this instant.
                    Require(!hud.Feed.IsStale && hud.VisualRoot.Q<Label>("gantry-position").text != "—", "source-recovery");
                    SetTarget(1920, 1080);
                    Require(YardCraneLocalStudentProfile.TryCreate(
                            "HUD-VERIFY-01", "HUD验证员", out var profile, out var profileError),
                        "local-return-profile:" + profileError);
                    YardCraneFallbackSession.Begin(profile, YardCraneLocalTrainingKind.FreePractice);
                    YardCraneTrainingSessionIdentity.Begin(
                        profile.StudentNumber, profile.DisplayName, YardCraneFallbackSession.LocalSessionId);
                    scoreIndexPath = Path.Combine(testOutputDirectory, "students", "HUD-VERIFY-01.json");
                    var bridge = YardCraneLocalTrainingNavigationBridge.Begin(profile);
                    Require(bridge.IsBound &&
                            hud.NavigationCapabilities ==
                            (YardCraneHudNavigationCapabilities.Back | YardCraneHudNavigationCapabilities.Exit) &&
                            hud.VisualRoot.Q<Button>("back-button").enabledSelf &&
                            hud.VisualRoot.Q<Button>("exit-button").enabledSelf &&
                            hud.VisualRoot.Q<Label>("navigation-note").resolvedStyle.display == DisplayStyle.None,
                        "local-bridge-atomically-enables-two-real-navigation-actions");
                    hud.ShowConfirmationPrompt(
                        new YardCraneHudConfirmationPrompt
                        {
                            Title = "旧运行提示",
                            Message = "该提示必须在运行进入终态时自动失效"
                        },
                        () => confirmationCallbackCount++,
                        () => confirmationCallbackCount++);
                    Require(hud.ActivePromptKind == YardCraneHudPromptKind.Confirmation,
                        "pre-terminal-prompt-is-visible-before-transition");
                    Require(lifecycle.TryTransition(YardCraneSceneState.Finalizing, out var error),
                        "running-to-finalizing:" + error);
                    Require(hud.ActivePromptKind == YardCraneHudPromptKind.None &&
                            CountVisiblePrompts() == 0 &&
                            confirmationCallbackCount == 1,
                        "terminal-transition-invalidates-stale-prompt-without-running-action");
                }
                else if (step == 17)
                {
                    Require(hud.IsResultVisible, "finalizing-shows-result-status");
                    Require(lifecycle.TryTransition(YardCraneSceneState.ExitAllowed, out var error),
                        "finalizing-to-exit-allowed:" + error);
                    Require(YardCraneTrainingSessionIdentity.Outcome == YardCraneTrainingRunOutcome.Completed,
                        "exit-allowed-marks-run-completed-before-scene-teardown");
                }
                else if (step == 18)
                {
                    Require(hud.IsResultVisible, "exit-allowed-keeps-result-visible");
                    Require(hud.VisualRoot.Q<Label>("result-title").text.Contains("完成"),
                        "completion-result-copy");
                    Require(hud.VisualRoot.Q<Label>("result-trust").text.Contains("不显示虚假分数"),
                        "result-does-not-fabricate-score");
                    VerifyVisibleInUpperScreen("result-card", "display1-result", 1080f);
                    Capture("display1-result");
                    ClickButton("back-button", "back-button-pointer-hit");
                }
                else if (step == 19)
                {
                    if (SceneManager.GetActiveScene().name != YardCraneStudentSideShell.SceneName) return;
                    var studentHome = UnityEngine.Object.FindFirstObjectByType<YardCraneStudentSideShell>();
                    if (studentHome == null || !studentHome.IsReady) return;
                    Require(studentHome.VisualRoot.Q<Label>("identity").text.Contains("HUD验证员"),
                        "back-restores-local-student-home-identity");
                    Require(!YardCraneFallbackSession.IsActive, "back-closes-local-training-session");
                    Require(UnityEngine.Object.FindFirstObjectByType<YardCraneBootstrap>() == null,
                        "back-removes-persistent-bootstrap-owner");
                    Require(!YardCraneTrainingSessionIdentity.HasStudent &&
                            YardCraneTrainingSessionIdentity.Outcome == YardCraneTrainingRunOutcome.None,
                        "back-clears-completed-session-identity-after-rig-teardown");
                    var runFiles = Directory.Exists(testOutputDirectory)
                        ? Directory.GetFiles(testOutputDirectory, "run-*.json")
                        : Array.Empty<string>();
                    Require(runFiles.Length == 1, "completed-run-produces-one-isolated-audit-record");
                    var runRecord = File.ReadAllText(runFiles[0]);
                    Require(runRecord.Contains("\"studentNumber\": \"HUD-VERIFY-01\"") &&
                            runRecord.Contains("\"outcome\": \"completed\"") &&
                            runRecord.Contains("\"countsTowardResults\": true"),
                        "completed-audit-record-has-completed-result-semantics");
                    Require(File.Exists(scoreIndexPath), "completed-run-enters-isolated-student-result-index");
                    var scoreIndex = File.ReadAllText(scoreIndexPath);
                    Require(scoreIndex.Contains("\"outcome\": \"completed\"") &&
                            scoreIndex.Contains("\"countsTowardResults\": true") &&
                            scoreIndex.Contains(Path.GetFileName(runFiles[0])),
                        "isolated-student-index-references-completed-audit-record");
                    Finish(true, "YardCrane-scene-owned UIDocumentDisplay1; native Display1+Display2 cameras; " +
                        "seven independently triggered mutually-exclusive prompts; " +
                        "base driving HUD remains visible beneath every prompt; " +
                        "all prompts centered in and confined to the independent 1920x1080 Display1; " +
                        "stale callback rejection; honest unavailable submission; " +
                        "full-screen native dual-display routing; " +
                        "source loss/recovery; disabled unowned navigation; atomically bound local navigation; " +
                        "completed result isolated from formal scores; HUD back returns to StudentHome");
                    return;
                }
                step++;
                waitUntil = now + 1.0;
                EditorApplication.QueuePlayerLoopUpdate();
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }

        private static void SetTarget(int width, int height)
        {
            var old = target;
            target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            target.Create();
            hud.GetComponent<UIDocument>().panelSettings.targetTexture = target;
            if (displayLayout != null && displayLayout.Display1Camera != null)
                displayLayout.Display1Camera.targetTexture = target;
            if (old != null) UnityEngine.Object.Destroy(old);
        }

        private static T[] FindSceneComponents<T>(Scene scene) where T : Component
        {
            var components = new System.Collections.Generic.List<T>();
            foreach (var root in scene.GetRootGameObjects())
                components.AddRange(root.GetComponentsInChildren<T>(true));
            return components.ToArray();
        }

        private static bool RectEquals(Rect first, Rect second)
        {
            return Mathf.Abs(first.x - second.x) < 0.0001f &&
                   Mathf.Abs(first.y - second.y) < 0.0001f &&
                   Mathf.Abs(first.width - second.width) < 0.0001f &&
                   Mathf.Abs(first.height - second.height) < 0.0001f;
        }

        private static void ClickGuide()
        {
            ClickButton("guide-toggle", "guide-button-pointer-hit");
        }

        private static void ClickButton(string id, string hitAssertion)
        {
            var button = hud.VisualRoot.Q<Button>(id);
            var center = button.worldBound.center;
            var hit = hud.VisualRoot.panel.Pick(center);
            Require(hit == button || (hit != null && button.Contains(hit)), hitAssertion);
            using (var down = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = center }))
            { down.target = button; button.SendEvent(down); }
            using (var up = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = center }))
            { up.target = button; button.SendEvent(up); }
        }

        private static int CountVisiblePrompts()
        {
            var count = 0;
            foreach (var id in PromptIds)
                if (hud.VisualRoot.Q(id).resolvedStyle.display == DisplayStyle.Flex) count++;
            return count;
        }

        private static void VerifySinglePrompt(
            YardCraneHudPromptKind expectedKind,
            string elementName,
            string profile)
        {
            Require(hud.ActivePromptKind == expectedKind && hud.IsPromptVisible,
                profile + ":active-kind");
            Require(CountVisiblePrompts() == 1,
                profile + ":exactly-one-of-seven-is-visible");
            Require(hud.VisualRoot.Q("prompt-layer").resolvedStyle.display == DisplayStyle.Flex,
                profile + ":prompt-layer-visible");
            Require(hud.VisualRoot.Q("result-card").resolvedStyle.display == DisplayStyle.None,
                profile + ":legacy-result-card-does-not-stack");
            foreach (var id in new[]
                     {
                         "timer-box", "task-capsule", "lamp-strip", "left-column", "left-telemetry",
                         "right-task-panel", "right-pose-panel", "right-lock-panel", "nav-buttons"
                     })
            {
                Require(hud.VisualRoot.Q(id).resolvedStyle.display == DisplayStyle.Flex,
                    profile + ":base-hud-remains-visible:" + id);
            }
            VerifyVisibleInUpperScreen(elementName, profile, 1080f);
            var rootBounds = hud.VisualRoot.Q("operator-hud").worldBound;
            var promptBounds = hud.VisualRoot.Q(elementName).worldBound;
            var expectedCenter = new Vector2(rootBounds.center.x, rootBounds.yMin + 540f);
            Require(Mathf.Abs(promptBounds.center.x - expectedCenter.x) <= 2f &&
                    Mathf.Abs(promptBounds.center.y - expectedCenter.y) <= 2f,
                $"{profile}:centered-in-upper-screen, bounds={promptBounds}, expectedCenter={expectedCenter}");
        }

        private static void VerifyLayout(string profile, float upperHeight)
        {
            var root = hud.VisualRoot.Q("operator-hud").worldBound;
            Require(root.width > 100 && root.height > 100, profile + ":root-has-layout");
            foreach (var id in new[]
                     {
                         "timer-box", "task-capsule", "lamp-strip", "left-column",
                         "right-task-panel", "right-pose-panel", "right-lock-panel", "nav-buttons",
                         "navigation-note"
                     })
            {
                var bounds = hud.VisualRoot.Q(id).worldBound;
                Require(bounds.xMin >= root.xMin && bounds.xMax <= root.xMax + 1 &&
                    bounds.yMin >= root.yMin && bounds.yMax <= root.yMin + upperHeight,
                    $"{profile}:{id} upper-half/no-clipping, bounds={bounds}, root={root}");
            }
            var left = hud.VisualRoot.Q("left-column").worldBound;
            var right = hud.VisualRoot.Q("right-task-panel").worldBound;
            Require(right.xMin - left.xMax > root.width * 0.25f, profile + ":clear-central-view");
            foreach (var label in hud.VisualRoot.Query<Label>().ToList())
                if (label.resolvedStyle.display != DisplayStyle.None && label.worldBound.width > 0)
                    Require(label.worldBound.xMax <= root.xMax + 1, profile + ":label-not-off-screen:" + label.name);
        }

        private static void VerifyVisibleInUpperScreen(string elementName, string profile, float upperHeight)
        {
            var root = hud.VisualRoot.Q("operator-hud").worldBound;
            var bounds = hud.VisualRoot.Q(elementName).worldBound;
            Require(bounds.width > 10 && bounds.height > 10, profile + ":has-layout");
            Require(bounds.xMin >= root.xMin && bounds.xMax <= root.xMax + 1 &&
                    bounds.yMin >= root.yMin && bounds.yMax <= root.yMin + upperHeight,
                $"{profile}:upper-screen bounds={bounds}, root={root}");
        }

        private static YardCraneSceneStateMachine CreateRunningLifecycle()
        {
            var machine = new YardCraneSceneStateMachine();
            Require(machine.TryTransition(YardCraneSceneState.WaitingForCoordinator, out _), "test-lifecycle-waiting");
            Require(machine.TryTransition(YardCraneSceneState.Loading, out _), "test-lifecycle-loading");
            Require(machine.TryTransition(YardCraneSceneState.Ready, out _), "test-lifecycle-ready");
            Require(machine.TryTransition(YardCraneSceneState.Running, out _), "test-lifecycle-running");
            return machine;
        }

        private static void Capture(string name)
        {
            var previous = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Logs/UiVerification");
                File.WriteAllBytes($"Logs/UiVerification/{name}.png", image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.Destroy(image); }
        }

        private static void Require(bool value, string name)
        { if (!value) throw new InvalidOperationException(name); }

        private static void InvalidatePreviousResult()
        {
            var resultDirectory = Path.GetDirectoryName(ResultPath);
            Directory.CreateDirectory(resultDirectory);
            if (File.Exists(ResultPath)) File.Delete(ResultPath);
            File.WriteAllText(ResultPath,
                $"[YC_UI_PLAY_RUNNING] utc={DateTime.UtcNow:O}, awaiting current run result");
        }

        private static void Finish(bool passed, string detail)
        {
            var ownsPlaySession = SessionState.GetBool(RunningKey, false);
            SessionState.SetBool(RunningKey, false);
            try
            {
                try
                {
                    if (hud != null)
                    {
                        var document = hud.GetComponent<UIDocument>();
                        if (document != null && document.panelSettings != null)
                            document.panelSettings.targetTexture = previousTarget;
                        var adapter = hud.GetComponent<YardCraneHudSceneAdapter>();
                        if (adapter != null) adapter.enabled = true;
                    }
                    if (sceneCameras != null && previousCameraTargets != null)
                    {
                        var count = Mathf.Min(sceneCameras.Length, previousCameraTargets.Length);
                        for (var index = 0; index < count; index++)
                            if (sceneCameras[index] != null)
                                sceneCameras[index].targetTexture = previousCameraTargets[index];
                    }
                    if (target != null) UnityEngine.Object.Destroy(target);
                }
                catch (Exception cleanupError)
                {
                    passed = false;
                    detail += $" | visual cleanup failed safely: {cleanupError.Message}";
                }

                var message = $"[YC_UI_PLAY_{(passed ? "PASS" : "FAIL")}] " +
                              $"version={YardCraneOperatorHud.Version}, utc={DateTime.UtcNow:O}, {detail}";
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
                    File.WriteAllText(ResultPath, message);
                    if (passed) Debug.Log(message); else Debug.LogError(message);
                }
                catch (Exception resultError)
                {
                    passed = false;
                    Debug.LogError($"[YC_UI_VERIFY_RESULT_WRITE_FAILED] {resultError}");
                }
            }
            finally
            {
                hud = null;
                target = null;
                previousTarget = null;
                sceneCameras = null;
                previousCameraTargets = null;
                displayLayout = null;
                step = 0;
                startedAt = 0;
                waitUntil = 0;
                lifecycle = null;
                if (ownsPlaySession)
                {
                    if (!Application.isBatchMode) SessionState.SetBool(RestoreKey, true);
                    EditorApplication.isPlaying = false;
                }
                if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
            }
        }
    }
}
