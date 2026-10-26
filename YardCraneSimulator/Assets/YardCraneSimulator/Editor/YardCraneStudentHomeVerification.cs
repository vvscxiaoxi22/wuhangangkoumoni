using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PortTraining.YardCraneSimulator.Bootstrap;
using PortTraining.YardCraneSimulator.Controls;
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
    /// <summary>
    /// Real UI Toolkit layout and interaction verification for the split StudentHome scene.
    /// Exercises all three StudentHome entry modes and their complete
    /// StudentHome -> Bootstrap -> YardCrane -> HUD back -> StudentHome round trips.
    /// </summary>
    [InitializeOnLoad]
    public static class YardCraneStudentHomeVerification
    {
        private const string RunningKey = "YC_STUDENT_HOME_VERIFY_RUNNING";
        private const string RestoreKey = "YC_STUDENT_HOME_VERIFY_RESTORE";
        private const string OriginalSceneKey = "YC_STUDENT_HOME_VERIFY_ORIGINAL_SCENE";
        private const string OutputDirectoryKey = "YC_STUDENT_HOME_VERIFY_OUTPUT_DIRECTORY";
        private const string StudentHomeScenePath = "Assets/YardCraneSimulator/Scenes/StudentHome.unity";
        private const string OperationalScenePath = "Assets/YardCraneSimulator/Scenes/YardCrane.unity";
        private const string OperationalSceneName = "YardCrane";
        private const string TaskStateAssetPath = "Assets/Data/任务State.asset";
        private const string PaperStateAssetPath = "Assets/Data/Resources/试卷类型.asset";
        private static readonly string RequestPath = Path.GetFullPath("Temp/YC_StudentHomeVerification.request");
        private static readonly string ResultPath = Path.GetFullPath("Logs/YC_StudentHomeVerification.result.txt");

        private static YardCraneStudentSideShell shell;
        private static RenderTexture target;
        private static RenderTexture previousTarget;
        private static int step;
        private static double startedAt;
        private static double waitUntil;
        private static Guid localSessionId;
        private static string testOutputDirectory;
        private static string assessmentOutputDirectory;
        private static string scoreIndexPath;

        static YardCraneStudentHomeVerification()
        {
            PrepareHandoffIfRunning();
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Tools/Yard Crane/Tests/Verify Student Home")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Start StudentHome verification in Edit mode.");
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the active scene before StudentHome verification.");
            if (!File.Exists(StudentHomeScenePath))
                throw new FileNotFoundException("StudentHome scene is missing.", StudentHomeScenePath);
            if (!File.Exists(OperationalScenePath))
                throw new FileNotFoundException("Formal YardCrane scene is missing.", OperationalScenePath);

            InvalidatePreviousResult();
            SessionState.SetBool(RunningKey, true);
            SessionState.SetBool(RestoreKey, false);
            try
            {
                YardCraneFallbackSession.Clear();
                YardCraneTrainingSessionIdentity.Clear();
                YardCraneStudentHomeHandoff.Consume();
                YardCraneBootstrap.EditorOperationalSceneOverrideForVerification = string.Empty;
                testOutputDirectory = Path.Combine(
                    Path.GetFullPath("Temp/YardCraneVerificationScores"),
                    "StudentHome-" + Guid.NewGuid().ToString("N"));
                SessionState.SetString(OutputDirectoryKey, testOutputDirectory);
                YardCraneTrainingScoreRecorder.EditorOutputDirectoryOverride = testOutputDirectory;
                ConfigureAssessmentStore(testOutputDirectory);
                BackupAssessmentAssets();
                SeedAssessmentPaper();
                SessionState.SetString(OriginalSceneKey, EditorSceneManager.GetActiveScene().path);
                EditorSceneManager.OpenScene(StudentHomeScenePath);
                PrepareHandoffIfRunning();
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

        private static void PrepareHandoffIfRunning()
        {
            if (!SessionState.GetBool(RunningKey, false)) return;
            testOutputDirectory = SessionState.GetString(OutputDirectoryKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(testOutputDirectory))
            {
                YardCraneTrainingScoreRecorder.EditorOutputDirectoryOverride = testOutputDirectory;
                ConfigureAssessmentStore(testOutputDirectory);
            }
            if (!YardCraneLocalStudentProfile.TryCreate("YC-VERIFY", "验收学员", out var profile, out var error))
                throw new InvalidOperationException("Could not create verification profile: " + error);
            YardCraneStudentHomeHandoff.Begin(profile);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;

            // EnteredEditMode happens only after scene teardown/OnDestroy has finished, so it
            // is the first safe point to release the score-directory override and identity.
            // This also repairs a cancelled/failed Enter Play Mode attempt even when no file
            // request exists to trigger the stale-request recovery path below.
            var interrupted = SessionState.GetBool(RunningKey, false);
            if (interrupted)
            {
                SessionState.SetBool(RunningKey, false);
                SessionState.SetBool(RestoreKey, true);
            }
            ReleaseVerificationStateAfterTeardown();
        }

        private static void ReleaseVerificationStateAfterTeardown()
        {
            RestoreAssessmentAssets();
            YardCraneAssessmentQuestionBankStore.EditorOutputDirectoryOverride = null;
            YardCraneTrainingScoreRecorder.EditorOutputDirectoryOverride = null;
            YardCraneBootstrap.EditorOperationalSceneOverrideForVerification = string.Empty;
            YardCraneFallbackSession.Clear();
            YardCraneTrainingSessionIdentity.Clear();
            YardCraneStudentHomeHandoff.Consume();
            SessionState.SetString(OutputDirectoryKey, string.Empty);
            testOutputDirectory = null;
            DeleteAssessmentOutputDirectory();
            assessmentOutputDirectory = null;
            scoreIndexPath = null;
            localSessionId = Guid.Empty;
        }

        private static void ConfigureAssessmentStore(string outputDirectory)
        {
            assessmentOutputDirectory = string.IsNullOrWhiteSpace(outputDirectory)
                ? null
                : Path.Combine(outputDirectory, "assessment-paper");
            YardCraneAssessmentQuestionBankStore.EditorOutputDirectoryOverride =
                assessmentOutputDirectory;
        }

        private static void BackupAssessmentAssets()
        {
            if (string.IsNullOrWhiteSpace(assessmentOutputDirectory))
                throw new InvalidOperationException("Assessment verification output directory is missing.");

            Directory.CreateDirectory(assessmentOutputDirectory);
            BackupAsset(TaskStateAssetPath, "task-state.asset.backup");
            BackupAsset(PaperStateAssetPath, "paper-state.asset.backup");
        }

        private static void BackupAsset(string assetPath, string backupName)
        {
            var sourcePath = Path.GetFullPath(assetPath);
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Assessment verification asset is missing.", sourcePath);
            File.Copy(
                sourcePath,
                Path.Combine(assessmentOutputDirectory, backupName),
                true);
        }

        private static void SeedAssessmentPaper()
        {
            var training = new YardCraneAssessmentTrainingQuestionData
            {
                sourceKey = "student-home-verification:yard:gantry:-30",
                trainingProject = "场桥集装箱起重机仿真系统",
                category = "单项操作",
                taskName = "大车移动",
                taskType = "单项操作",
                targetId = 0,
                targetDescription = "目标位置(m) -30",
                taskDescription = "操作场桥大车移动到指定位置",
                field1Title = "目标位置(m):",
                field1Content = "-30",
                durationSeconds = 600f,
                trainingMode = 1,
                operationMode = "本地操作"
            };
            if (!YardCraneAssessmentQuestionBankStore.TryAddTrainingQuestions(
                    "场桥", new[] { training }, out var added, out var addProblem) ||
                added == null || added.Length != 1)
            {
                throw new InvalidOperationException(
                    "Could not seed isolated assessment question: " +
                    (string.IsNullOrWhiteSpace(addProblem) ? "unexpected question count" : addProblem));
            }
            if (!YardCraneAssessmentQuestionBankStore.TrySetIncluded(
                    "场桥", added[0].questionId, true, out var paper, out var publishProblem) ||
                paper == null || !paper.isPublished || paper.items == null || paper.items.Count != 1)
            {
                throw new InvalidOperationException(
                    "Could not publish isolated assessment paper: " +
                    (string.IsNullOrWhiteSpace(publishProblem) ? "unexpected paper state" : publishProblem));
            }
        }

        private static void RestoreAssessmentAssets()
        {
            if (string.IsNullOrWhiteSpace(assessmentOutputDirectory) ||
                !Directory.Exists(assessmentOutputDirectory)) return;

            RestoreAsset(TaskStateAssetPath, "task-state.asset.backup");
            RestoreAsset(PaperStateAssetPath, "paper-state.asset.backup");
        }

        private static void RestoreAsset(string assetPath, string backupName)
        {
            var backupPath = Path.Combine(assessmentOutputDirectory, backupName);
            if (!File.Exists(backupPath)) return;
            File.Copy(backupPath, Path.GetFullPath(assetPath), true);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        private static void DeleteAssessmentOutputDirectory()
        {
            if (string.IsNullOrWhiteSpace(assessmentOutputDirectory) ||
                !Directory.Exists(assessmentOutputDirectory)) return;
            try
            {
                Directory.Delete(assessmentOutputDirectory, true);
            }
            catch (Exception error)
            {
                Debug.LogWarning(
                    $"[YC_STUDENT_HOME_VERIFY_ASSESSMENT_CLEANUP_FAILED] {error.Message}");
            }
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

            // A compile failure can cancel Enter Play Mode after Run() persisted the running
            // flag. Recover even for a manual menu run with no request file, otherwise the
            // Editor-only output override could silently redirect a later real run.
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
                if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                var request = File.ReadAllText(RequestPath).Trim();
                if (request == "refresh")
                {
                    File.WriteAllText(RequestPath, "run");
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    return;
                }

                if ((request == "run" || request == "run-after-stop") &&
                    EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    // File-triggered verification is also used while a designer has left the
                    // scene in Play Mode. Stop that preview first and wait for the complete
                    // EnteredEditMode teardown before opening/switching verification scenes.
                    // The request stays on disk, so no verification intent is lost across the
                    // play-mode domain reload.
                    File.WriteAllText(RequestPath, "run-after-stop");
                    EditorApplication.isPlaying = false;
                    return;
                }

                if (request != "run" && request != "run-after-stop") return;
                File.Delete(RequestPath);
                try { Run(); }
                catch (Exception error) { Finish(false, error.ToString()); }
                return;
            }

            if (!EditorApplication.isPlaying) return;
            var now = EditorApplication.timeSinceStartup;
            if (startedAt == 0) startedAt = now;
            if (now - startedAt > 240)
            {
                Finish(false, "Timed out waiting for StudentHome UI.");
                return;
            }
            if (now < waitUntil) return;

            try
            {
                if (step == 0)
                {
                    shell = UnityEngine.Object.FindFirstObjectByType<YardCraneStudentSideShell>();
                    if (shell == null || !shell.IsReady) return;
                    Require(SceneManager.GetActiveScene().name == YardCraneStudentSideShell.SceneName,
                        "dedicated-student-home-scene");
                    Require(shell.Page == YardCraneStudentSidePage.Home, "home-is-default-page");
                    Require(shell.Document != shell.DocumentLower, "display-documents-are-independent");
                    Require(shell.Document.panelSettings.targetDisplay == 0, "upper-targets-display1");
                    Require(shell.DocumentLower.panelSettings.targetDisplay == 1, "lower-targets-display2");
                    previousTarget = shell.Document.panelSettings.targetTexture;
                    target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                    target.Create();
                    shell.Document.panelSettings.targetTexture = target;
                }
                else if (step == 1)
                {
                    VerifyHomeLayout();
                    Capture("student-home-reference-fidelity");
                    Click(shell.VisualRoot, shell.VisualRoot.Q<Button>("learning-mode-button"));
                }
                else if (step == 2)
                {
                    Require(shell.Page == YardCraneStudentSidePage.Learning,
                        "learning-card-opens-learning-page");
                    Require(shell.VisualRoot.Q("student-learning-page").resolvedStyle.display == DisplayStyle.Flex,
                        "learning-page-is-visible");
                    VerifyLearningLayoutAndInteractions();
                }
                else if (step == 3)
                {
                    Require(shell.Page == YardCraneStudentSidePage.Home,
                        "learning-back-returns-home");
                    Click(shell.VisualRoot, shell.VisualRoot.Q<Button>("learning-mode-button"));
                    Require(shell.Page == YardCraneStudentSidePage.Learning,
                        "learning-page-reopens-for-real-launch");
                    var gantryOptions = shell.VisualRoot.Q("drill-gantry-options").Query<Toggle>().ToList();
                    gantryOptions[0].value = true;
                    Click(shell.VisualRoot, shell.VisualRoot.Q<Button>("drill-gantry-start"));
                    var context = YardCraneFallbackSession.CurrentContext;
                    Require(shell.Page == YardCraneStudentSidePage.Launching && context != null &&
                            context.Mode == YardCraneLocalTrainingMode.GuidedLearning &&
                            context.Exercise == YardCraneLocalTrainingExercise.GantryMovement &&
                            context.Target.Metric == YardCraneLocalTrainingMetric.GantryPosition &&
                            context.Target.Value == -30d &&
                            context.ReturnPage == YardCraneLocalTrainingReturnPage.LearningSelection,
                        "guided-learning-entry-freezes-selected-target-and-return-page");
                    localSessionId = context.SessionId;
                    Require(YardCraneTrainingSessionIdentity.SessionId == localSessionId &&
                            YardCraneTrainingSessionIdentity.Outcome == YardCraneTrainingRunOutcome.InProgress,
                        "guided-learning-binds-one-correlated-running-identity");
                }
                else if (step == 4)
                {
                    var bridge = YardCraneLocalTrainingNavigationBridge.Active;
                    var hud = UnityEngine.Object.FindFirstObjectByType<YardCraneOperatorHud>();
                    var bootstrap = UnityEngine.Object.FindFirstObjectByType<YardCraneBootstrap>();
                    var rig = UnityEngine.Object.FindFirstObjectByType<YardCraneEightRopeMaxPrototypeRig>();
                    var adapter = UnityEngine.Object.FindFirstObjectByType<YardCraneHudSceneAdapter>();
                    var coordinator = UnityEngine.Object.FindFirstObjectByType<YardCraneLocalTrainingModeCoordinator>();
                    var host = UnityEngine.Object.FindFirstObjectByType<YardCraneStandaloneOperationHost>();
                    if (bridge == null || !bridge.IsBound || hud == null || !hud.IsReady || hud.Feed.IsStale ||
                        bootstrap == null || bootstrap.SceneConfig == null || rig == null || !rig.LoopsReady ||
                        adapter == null || coordinator == null || host == null ||
                        bootstrap.StateMachine.Current != YardCraneSceneState.Running) return;

                    VerifyOperationalSceneComposition(bootstrap, hud, adapter);
                    Require(coordinator.Context.SessionId == localSessionId &&
                            coordinator.Context.Mode == YardCraneLocalTrainingMode.GuidedLearning &&
                            !coordinator.IsTracking && !host.IsTrainingInputEnabled &&
                            hud.ActivePromptKind == YardCraneHudPromptKind.TaskBrief,
                        "guided-learning-shows-task-brief-before-enabling-input");
                    var startButton = hud.VisualRoot.Q<Button>("task-prompt-close");
                    Require(startButton.text == "开始", "guided-task-brief-has-explicit-start-action");
                    Click(hud.VisualRoot, startButton);
                }
                else if (step == 5)
                {
                    var hud = UnityEngine.Object.FindFirstObjectByType<YardCraneOperatorHud>();
                    var coordinator = UnityEngine.Object.FindFirstObjectByType<YardCraneLocalTrainingModeCoordinator>();
                    var host = UnityEngine.Object.FindFirstObjectByType<YardCraneStandaloneOperationHost>();
                    if (hud == null || !hud.IsReady || coordinator == null || host == null ||
                        hud.ActivePromptKind != YardCraneHudPromptKind.Countdown) return;
                    Require(!coordinator.IsTracking && !host.IsTrainingInputEnabled,
                        "guided-five-second-countdown-keeps-business-input-gated");
                    Require(hud.VisualRoot.Q<Label>("countdown-prompt-title").text.Contains("学习训练") &&
                            int.TryParse(hud.VisualRoot.Q<Label>("countdown-value").text, out var remaining) &&
                            remaining >= 1 && remaining <= 5,
                        "guided-countdown-is-visible-and-bounded-to-five-seconds");
                }
                else if (step == 6)
                {
                    var bridge = YardCraneLocalTrainingNavigationBridge.Active;
                    var hud = UnityEngine.Object.FindFirstObjectByType<YardCraneOperatorHud>();
                    var bootstrap = UnityEngine.Object.FindFirstObjectByType<YardCraneBootstrap>();
                    var adapter = UnityEngine.Object.FindFirstObjectByType<YardCraneHudSceneAdapter>();
                    var coordinator = UnityEngine.Object.FindFirstObjectByType<YardCraneLocalTrainingModeCoordinator>();
                    var host = UnityEngine.Object.FindFirstObjectByType<YardCraneStandaloneOperationHost>();
                    if (bridge == null || !bridge.IsBound || hud == null || !hud.IsReady ||
                        bootstrap == null || adapter == null || coordinator == null || !coordinator.IsTracking ||
                        host == null || bootstrap.StateMachine.Current != YardCraneSceneState.Running) return;

                    Require(coordinator.Context.SessionId == localSessionId &&
                            coordinator.Context.Mode == YardCraneLocalTrainingMode.GuidedLearning &&
                            hud.SessionModeLabel == "引导学习" && host.IsTrainingInputEnabled &&
                            hud.ActivePromptKind == YardCraneHudPromptKind.None &&
                            coordinator.ElapsedSeconds >= 0d && coordinator.ElapsedSeconds < 1.5d,
                        "guided-learning-context-reaches-one-scene-coordinator-after-task-brief-and-real-countdown");
                    Require(new[] { "00:00", "00:01" }.Contains(
                                hud.VisualRoot.Q<Label>("elapsed").text),
                        "guided-visible-elapsed-clock-excludes-scene-startup-and-countdown");
                    Require(hud.TrainingTaskPresentation != null &&
                            hud.TrainingTaskPresentation.Title.Contains("大车移动") &&
                            hud.TrainingTaskPresentation.PrimaryValue.Contains("-30m"),
                        "guided-learning-target-is-visible-in-driving-hud");
                    Click(hud.VisualRoot, hud.VisualRoot.Q<Button>("back-button"));
                    Require(bootstrap.StateMachine.Current == YardCraneSceneState.Terminated &&
                            YardCraneStudentHomeHandoff.PendingReturnPage ==
                            YardCraneLocalTrainingReturnPage.LearningSelection,
                        "guided-learning-back-terminates-and-preserves-learning-return-destination");
                }
                else if (step == 7)
                {
                    if (SceneManager.GetActiveScene().name != YardCraneStudentSideShell.SceneName) return;
                    shell = UnityEngine.Object.FindFirstObjectByType<YardCraneStudentSideShell>();
                    if (shell == null || !shell.IsReady ||
                        YardCraneLocalTrainingNavigationBridge.Active != null) return;
                    Require(shell.Page == YardCraneStudentSidePage.Learning &&
                            !YardCraneFallbackSession.IsActive &&
                            !YardCraneTrainingSessionIdentity.HasStudent,
                        "guided-learning-back-restores-learning-selection-and-clears-owned-session");
                    var guidedFiles = Directory.GetFiles(testOutputDirectory, "run-*.json");
                    Require(guidedFiles.Length == 1, "guided-learning-writes-one-audit-record");
                    var guidedRecord = File.ReadAllText(guidedFiles[0]);
                    Require(guidedRecord.Contains("\"trainingMode\": \"GuidedLearning\"") &&
                            guidedRecord.Contains("\"exercise\": \"GantryMovement\"") &&
                            guidedRecord.Contains("\"targetMetricId\": \"gantry.position.relative.m\"") &&
                            guidedRecord.Contains($"\"formatVersion\": \"{YardCraneTrainingScoreRecorder.FormatVersion}\"") &&
                            guidedRecord.Contains("\"scoreTrustLevel\": \"Unscored\"") &&
                            guidedRecord.Contains("\"hasScore\": false") &&
                            guidedRecord.Contains("\"totalScore\": 0") &&
                            guidedRecord.Contains("\"countsTowardResults\": false") &&
                            JsonNumber(guidedRecord, "elapsedSeconds") > 0d,
                        "guided-learning-audit-is-targeted-unscored-and-not-a-result");
                    Click(shell.VisualRoot, shell.VisualRoot.Q<Button>("learning-back-button"));
                    Require(shell.VisualRoot.Q<Button>("launch-button").enabledSelf,
                        "free-practice-is-launchable");
                    scoreIndexPath = Path.Combine(
                        testOutputDirectory, "students", "YC-VERIFY.json");
                    Click(shell.VisualRoot, shell.VisualRoot.Q<Button>("launch-button"));
                    Require(shell.Page == YardCraneStudentSidePage.Launching,
                        "free-practice-click-enters-launching-state");
                    Require(YardCraneFallbackSession.IsActive &&
                            YardCraneFallbackSession.TrainingKind == YardCraneLocalTrainingKind.FreePractice &&
                            YardCraneFallbackSession.Student?.StudentNumber == "YC-VERIFY",
                        "launch-begins-local-fallback-session");
                    localSessionId = YardCraneFallbackSession.LocalSessionId;
                    Require(localSessionId != Guid.Empty &&
                            YardCraneTrainingSessionIdentity.HasStudent &&
                            YardCraneTrainingSessionIdentity.StudentNumber == "YC-VERIFY" &&
                            YardCraneTrainingSessionIdentity.DisplayName == "验收学员" &&
                            YardCraneTrainingSessionIdentity.SessionId == localSessionId &&
                            YardCraneTrainingSessionIdentity.Outcome == YardCraneTrainingRunOutcome.InProgress,
                        "launch-binds-in-progress-training-identity");
                    Require(YardCraneLocalTrainingNavigationBridge.Active != null,
                        "launch-creates-navigation-bridge-before-scene-switch");
                    Require(YardCraneStudentHomeHandoff.PendingProfile == null,
                        "launch-does-not-reuse-login-handoff");
                }
                else if (step == 8)
                {
                    var bridge = YardCraneLocalTrainingNavigationBridge.Active;
                    var hud = UnityEngine.Object.FindFirstObjectByType<YardCraneOperatorHud>();
                    var bootstrap = UnityEngine.Object.FindFirstObjectByType<YardCraneBootstrap>();
                    var rig = UnityEngine.Object.FindFirstObjectByType<YardCraneEightRopeMaxPrototypeRig>();
                    var adapter = UnityEngine.Object.FindFirstObjectByType<YardCraneHudSceneAdapter>();
                    var coordinator = UnityEngine.Object.FindFirstObjectByType<YardCraneLocalTrainingModeCoordinator>();
                    var host = UnityEngine.Object.FindFirstObjectByType<YardCraneStandaloneOperationHost>();
                    if (bridge == null || !bridge.IsBound || hud == null || !hud.IsReady ||
                        hud.Feed.IsStale || bootstrap == null || bootstrap.LaunchOptions == null ||
                        bootstrap.SceneConfig == null || rig == null || !rig.LoopsReady || adapter == null ||
                        coordinator == null || !coordinator.IsTracking || host == null ||
                        bootstrap.StateMachine.Current != YardCraneSceneState.Running) return;

                    Require(SceneManager.GetActiveScene().name == bootstrap.SceneConfig.operationalSceneName,
                        "free-practice-loads-configured-operational-scene");
                    VerifyOperationalSceneComposition(bootstrap, hud, adapter);
                    Require(YardCraneFallbackSession.IsActive &&
                            YardCraneFallbackSession.TrainingKind == YardCraneLocalTrainingKind.FreePractice &&
                            YardCraneFallbackSession.Student?.StudentNumber == "YC-VERIFY",
                        "free-practice-owns-local-fallback-session");
                    Require(YardCraneTrainingSessionIdentity.HasStudent &&
                            YardCraneTrainingSessionIdentity.StudentNumber == "YC-VERIFY" &&
                            YardCraneTrainingSessionIdentity.SessionId == YardCraneFallbackSession.LocalSessionId,
                        "training-identity-is-bound-to-local-session");
                    Require(CountAll<YardCraneBootstrap>() == 1,
                        "free-practice-has-one-bootstrap-owner");
                    Require(CountAll<YardCraneOperatorHud>() == 1,
                        "free-practice-has-one-hud");
                    Require(CountAll<YardCraneLocalTrainingNavigationBridge>() == 1 &&
                            CountAll<YardCraneHudSceneAdapter>() == 1,
                        "free-practice-has-one-navigation-bridge");
                    Require(hud.NavigationCapabilities ==
                            (YardCraneHudNavigationCapabilities.Back | YardCraneHudNavigationCapabilities.Exit),
                        "local-bridge-enables-both-real-navigation-actions");
                    Require(hud.VisualRoot.Q<Label>("stage").text.StartsWith(
                            "自由练习 ·", StringComparison.Ordinal),
                        "student-owned-run-is-labelled-free-practice");
                    Require(coordinator.Context.Mode == YardCraneLocalTrainingMode.FreePractice &&
                            host.IsTrainingInputEnabled && hud.TrainingTaskPresentation != null &&
                            hud.TrainingTaskPresentation.SecondaryValue == "不自动完成",
                        "free-practice-is-open-ended-with-business-input-enabled");
                    Click(hud.VisualRoot, hud.VisualRoot.Q<Button>("back-button"));
                    Require(bootstrap.StateMachine.Current == YardCraneSceneState.Terminated,
                        "hud-back-marks-running-session-terminated");
                    Require(YardCraneTrainingSessionIdentity.Outcome == YardCraneTrainingRunOutcome.Terminated,
                        "terminated-lifecycle-marks-score-record-non-completed");
                    Require(!bridge.IsBound,
                        "hud-back-unbinds-navigation-before-scene-switch");
                    Require(YardCraneStudentHomeHandoff.PendingProfile?.StudentNumber == "YC-VERIFY",
                        "hud-back-prepares-one-shot-return-profile");
                    Require(YardCraneFallbackSession.IsActive &&
                            YardCraneFallbackSession.LocalSessionId == localSessionId &&
                            YardCraneTrainingSessionIdentity.HasStudent &&
                            YardCraneTrainingSessionIdentity.SessionId == localSessionId,
                        "hud-back-retains-identity-until-operational-scene-unloads");
                }
                else if (step == 9)
                {
                    if (SceneManager.GetActiveScene().name != YardCraneStudentSideShell.SceneName) return;
                    shell = UnityEngine.Object.FindFirstObjectByType<YardCraneStudentSideShell>();
                    if (shell == null || !shell.IsReady ||
                        YardCraneLocalTrainingNavigationBridge.Active != null ||
                        CountAll<YardCraneBootstrap>() != 0 ||
                        CountAll<YardCraneOperatorHud>() != 0 ||
                        CountAll<YardCraneLocalTrainingNavigationBridge>() != 0 ||
                        CountAll<YardCraneHudSceneAdapter>() != 0) return;

                    Require(CountAll<YardCraneStudentSideShell>() == 1 &&
                            shell.Page == YardCraneStudentSidePage.Home &&
                            shell.VisualRoot.Q<Label>("identity").text.Contains("YC-VERIFY") &&
                            shell.VisualRoot.Q<Label>("identity").text.Contains("验收学员"),
                        "hud-back-restores-original-student-home-identity");
                    Require(shell.VisualRoot.Q<Label>("greeting").text.Contains("验收学员") &&
                            shell.VisualRoot.Q<Button>("launch-button").enabledSelf,
                        "returned-home-is-ready-for-another-run");
                    Require(!YardCraneFallbackSession.IsActive &&
                            YardCraneFallbackSession.Student == null &&
                            YardCraneFallbackSession.LocalSessionId == Guid.Empty,
                        "hud-back-clears-local-fallback-session");
                    Require(!YardCraneTrainingSessionIdentity.HasStudent &&
                            YardCraneTrainingSessionIdentity.StudentNumber == string.Empty &&
                            YardCraneTrainingSessionIdentity.DisplayName == string.Empty &&
                            YardCraneTrainingSessionIdentity.SessionId == Guid.Empty &&
                            YardCraneTrainingSessionIdentity.Outcome == YardCraneTrainingRunOutcome.None,
                        "hud-back-clears-training-identity-after-operational-scene-unload");
                    Require(YardCraneStudentHomeHandoff.PendingProfile == null,
                        "returned-home-consumes-one-shot-profile-handoff");
                    var runFiles = Directory.Exists(testOutputDirectory)
                        ? Directory.GetFiles(testOutputDirectory, "run-*.json")
                        : Array.Empty<string>();
                    Require(runFiles.Length == 2,
                        "guided-and-free-practice-each-produce-one-isolated-audit-record");
                    var freeRecord = runFiles.Select(File.ReadAllText)
                        .Single(text => text.Contains("\"trainingMode\": \"FreePractice\""));
                     Require(freeRecord.Contains("\"studentNumber\": \"YC-VERIFY\"") &&
                             freeRecord.Contains("\"displayName\": \"验收学员\"") &&
                             freeRecord.Contains("\"identityContextMatched\": true") &&
                             freeRecord.Contains("\"outcome\": \"terminated\"") &&
                             freeRecord.Contains("\"scoreTrustLevel\": \"LocalEstimated\"") &&
                             freeRecord.Contains("\"hasScore\": false") &&
                             freeRecord.Contains("\"totalScore\": 0") &&
                             freeRecord.Contains("\"countsTowardResults\": false"),
                        "terminated-free-practice-retains-policy-trust-without-a-score-or-result");
                    Require(!File.Exists(scoreIndexPath),
                        "terminated-runs-do-not-enter-student-result-index");
                    Click(shell.VisualRoot, shell.VisualRoot.Q<Button>("assessment-mode-button"));
                    var assessmentPage = shell.VisualRoot.Q("student-assessment-page");
                    var assessmentRows = shell.VisualRoot
                        .Query<VisualElement>(className: "assessment-question-row").ToList();
                    var assessmentStartButtons = shell.VisualRoot
                        .Query<Button>(className: "assessment-question-start").ToList();
                    Require(shell.Page == YardCraneStudentSidePage.Assessment &&
                            assessmentPage != null &&
                            assessmentPage.style.display.value == DisplayStyle.Flex,
                        "assessment-card-opens-question-list-before-launch");
                    Require(assessmentRows.Count == 1 && assessmentStartButtons.Count == 1 &&
                            assessmentStartButtons[0].enabledSelf &&
                            assessmentRows[0].Q<Label>(className: "assessment-question-title").text
                                .Contains("大车移动") &&
                            assessmentRows[0].Q<Label>(className: "assessment-question-meta").text
                                .Contains("10 min"),
                        "assessment-list-renders-one-published-isolated-question");
                    Require(YardCraneAssessmentQuestionBankStore.TryLoadCurrentPaper(
                                "场桥", out var assessmentPaper, out var paperProblem) &&
                            assessmentPaper.items.Count == 1,
                        "assessment-list-reads-current-paper:" + paperProblem);
                }
                else if (step == 10)
                {
                    var assessmentStartButtons = shell.VisualRoot
                        .Query<Button>(className: "assessment-question-start").ToList();
                    Require(shell.Page == YardCraneStudentSidePage.Assessment &&
                            assessmentStartButtons.Count == 1,
                        "assessment-question-remains-selectable-after-layout");
                    Require(YardCraneAssessmentQuestionBankStore.TryLoadCurrentPaper(
                                "场桥", out var assessmentPaper, out var paperProblem) &&
                            assessmentPaper.items.Count == 1,
                        "assessment-start-loads-the-same-current-paper:" + paperProblem);
                    var selectedQuestion = assessmentPaper.items[0];
                    var paperStateBeforeLaunch = File.ReadAllText(
                        Path.GetFullPath(PaperStateAssetPath));
                    Click(shell.VisualRoot, assessmentStartButtons[0]);
                    var context = YardCraneFallbackSession.CurrentContext;
                    Require(shell.Page == YardCraneStudentSidePage.Launching && context != null &&
                            context.Mode == YardCraneLocalTrainingMode.Assessment &&
                            context.Exercise == YardCraneLocalTrainingExercise.PickupWorkflow &&
                            context.ScoreTrustLevel == YardCraneLocalScoreTrustLevel.Unscored &&
                            context.Timing.Mode == YardCraneLocalTimingMode.CountDown &&
                            context.Timing.DurationSeconds == 600 &&
                            context.Timing.StartCountdownSeconds == 5,
                        "assessment-entry-freezes-local-unscored-workflow-and-time-limit");
                    Require(YardCraneTrainingSessionIdentity.AssignedTaskId ==
                            selectedQuestion.questionId,
                        "assessment-entry-correlates-running-session-to-selected-question");
                    var taskState = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                        TaskStateAssetPath);
                    Require(taskState != null &&
                            ReadAssetField<string>(taskState, "实训项目") ==
                                "场桥集装箱起重机仿真系统" &&
                            ReadAssetField<string>(taskState, "任务名称") == "大车移动" &&
                            ReadAssetField<string>(taskState, "任务类型") == "单项操作" &&
                            ReadAssetField<int>(taskState, "目标ID") == 0 &&
                            ReadAssetField<string>(taskState, "字段1内容") == "-30" &&
                            Math.Abs(ReadAssetField<float>(taskState, "实训设定时间") - 600f) < 0.01f &&
                            ReadAssetField<int>(taskState, "实训模式") == 1,
                        "assessment-selection-projects-one-training-question-into-task-state");
                    Require(File.ReadAllText(Path.GetFullPath(PaperStateAssetPath)) ==
                            paperStateBeforeLaunch,
                        "assessment-selection-does-not-mutate-legacy-paper-state-asset");
                    localSessionId = context.SessionId;
                }
                else if (step == 11)
                {
                    var bridge = YardCraneLocalTrainingNavigationBridge.Active;
                    var hud = UnityEngine.Object.FindFirstObjectByType<YardCraneOperatorHud>();
                    var bootstrap = UnityEngine.Object.FindFirstObjectByType<YardCraneBootstrap>();
                    var adapter = UnityEngine.Object.FindFirstObjectByType<YardCraneHudSceneAdapter>();
                    var coordinator = UnityEngine.Object.FindFirstObjectByType<YardCraneLocalTrainingModeCoordinator>();
                    var host = UnityEngine.Object.FindFirstObjectByType<YardCraneStandaloneOperationHost>();
                    if (bridge == null || !bridge.IsBound || hud == null || !hud.IsReady || hud.Feed.IsStale ||
                        bootstrap == null || adapter == null || coordinator == null ||
                        host == null || bootstrap.StateMachine.Current != YardCraneSceneState.Running) return;

                    VerifyOperationalSceneComposition(bootstrap, hud, adapter);
                    Require(coordinator.Context.SessionId == localSessionId &&
                            coordinator.Context.Mode == YardCraneLocalTrainingMode.Assessment &&
                            !coordinator.IsTracking && !host.IsTrainingInputEnabled &&
                            hud.ActivePromptKind == YardCraneHudPromptKind.TaskBrief,
                        "assessment-shows-task-brief-before-enabling-input");
                    var startButton = hud.VisualRoot.Q<Button>("task-prompt-close");
                    Require(startButton.text == "开始", "assessment-task-brief-has-explicit-start-action");
                    Click(hud.VisualRoot, startButton);
                }
                else if (step == 12)
                {
                    var hud = UnityEngine.Object.FindFirstObjectByType<YardCraneOperatorHud>();
                    var coordinator = UnityEngine.Object.FindFirstObjectByType<YardCraneLocalTrainingModeCoordinator>();
                    var host = UnityEngine.Object.FindFirstObjectByType<YardCraneStandaloneOperationHost>();
                    if (hud == null || !hud.IsReady || coordinator == null || host == null ||
                        hud.ActivePromptKind != YardCraneHudPromptKind.Countdown) return;
                    Require(!coordinator.IsTracking && !host.IsTrainingInputEnabled,
                        "assessment-five-second-countdown-keeps-business-input-gated");
                    Require(hud.VisualRoot.Q<Label>("countdown-prompt-title").text.Contains("本地考核") &&
                            int.TryParse(hud.VisualRoot.Q<Label>("countdown-value").text, out var remaining) &&
                            remaining >= 1 && remaining <= 5,
                        "assessment-countdown-is-visible-and-bounded-to-five-seconds");
                }
                else if (step == 13)
                {
                    var bridge = YardCraneLocalTrainingNavigationBridge.Active;
                    var hud = UnityEngine.Object.FindFirstObjectByType<YardCraneOperatorHud>();
                    var bootstrap = UnityEngine.Object.FindFirstObjectByType<YardCraneBootstrap>();
                    var adapter = UnityEngine.Object.FindFirstObjectByType<YardCraneHudSceneAdapter>();
                    var coordinator = UnityEngine.Object.FindFirstObjectByType<YardCraneLocalTrainingModeCoordinator>();
                    var host = UnityEngine.Object.FindFirstObjectByType<YardCraneStandaloneOperationHost>();
                    if (bridge == null || !bridge.IsBound || hud == null || !hud.IsReady ||
                        bootstrap == null || adapter == null || coordinator == null || !coordinator.IsTracking ||
                        host == null || bootstrap.StateMachine.Current != YardCraneSceneState.Running) return;

                    Require(coordinator.Context.SessionId == localSessionId &&
                            coordinator.Context.Mode == YardCraneLocalTrainingMode.Assessment &&
                            hud.SessionModeLabel == "本地考核" && host.IsTrainingInputEnabled &&
                            hud.ActivePromptKind == YardCraneHudPromptKind.None &&
                            coordinator.ElapsedSeconds >= 0d && coordinator.ElapsedSeconds < 1.5d,
                        "assessment-context-reaches-one-scene-coordinator-after-task-brief-and-real-countdown");
                    Require(new[] { "00:00", "00:01" }.Contains(
                                hud.VisualRoot.Q<Label>("elapsed").text),
                        "assessment-visible-elapsed-clock-matches-post-countdown-origin");
                    Require(hud.TrainingTaskPresentation != null &&
                            hud.TrainingTaskPresentation.Title.Contains("大车移动") &&
                            hud.TrainingTaskPresentation.SecondaryLabel == "剩余时间：",
                        "assessment-hud-shows-selected-published-question-and-time-limit");
                    Click(hud.VisualRoot, hud.VisualRoot.Q<Button>("back-button"));
                    Require(YardCraneStudentHomeHandoff.PendingReturnPage ==
                            YardCraneLocalTrainingReturnPage.StudentHome,
                        "assessment-back-targets-student-home");
                }
                else if (step == 14)
                {
                    if (SceneManager.GetActiveScene().name != YardCraneStudentSideShell.SceneName) return;
                    shell = UnityEngine.Object.FindFirstObjectByType<YardCraneStudentSideShell>();
                    if (shell == null || !shell.IsReady ||
                        YardCraneLocalTrainingNavigationBridge.Active != null) return;

                    Require(shell.Page == YardCraneStudentSidePage.Home &&
                            !YardCraneFallbackSession.IsActive &&
                            !YardCraneTrainingSessionIdentity.HasStudent,
                        "assessment-back-restores-home-and-clears-owned-session");
                    var runFiles = Directory.GetFiles(testOutputDirectory, "run-*.json");
                    Require(runFiles.Length == 3,
                        "all-three-entry-modes-produce-independent-audit-records");
                    var assessmentRecord = runFiles.Select(File.ReadAllText)
                        .Single(text => text.Contains("\"trainingMode\": \"Assessment\""));
                    Require(assessmentRecord.Contains("大车移动") &&
                            assessmentRecord.Contains("\"assignedTaskId\": \"training-") &&
                            assessmentRecord.Contains($"\"formatVersion\": \"{YardCraneTrainingScoreRecorder.FormatVersion}\"") &&
                            assessmentRecord.Contains("\"identityContextMatched\": true") &&
                            assessmentRecord.Contains("\"scoreTrustLevel\": \"Unscored\"") &&
                            assessmentRecord.Contains("\"isOfficial\": false") &&
                            assessmentRecord.Contains("\"hasScore\": false") &&
                            assessmentRecord.Contains("\"totalScore\": 0") &&
                            assessmentRecord.Contains("\"countsTowardResults\": false"),
                        "local-assessment-audit-cannot-masquerade-as-formal-score");
                    Require(!File.Exists(scoreIndexPath),
                        "unscored-local-assessment-does-not-enter-student-result-index");

                    // Prove the negative branch with a real scene transition. A missing
                    // operational scene must return to this drill picker with a one-shot
                    // error; it must not strand the user in Bootstrap or manufacture a run.
                    Click(shell.VisualRoot, shell.VisualRoot.Q<Button>("learning-mode-button"));
                    Require(shell.Page == YardCraneStudentSidePage.Learning,
                        "startup-failure-probe-enters-learning-selection");
                    YardCraneBootstrap.EditorOperationalSceneOverrideForVerification =
                        "__YC_VERIFICATION_MISSING_OPERATIONAL_SCENE__";
                    Click(shell.VisualRoot, shell.VisualRoot.Q<Button>("drill-gantry-start"));
                    var failedContext = YardCraneFallbackSession.CurrentContext;
                    Require(shell.Page == YardCraneStudentSidePage.Launching && failedContext != null &&
                            failedContext.Mode == YardCraneLocalTrainingMode.GuidedLearning &&
                            failedContext.ReturnPage == YardCraneLocalTrainingReturnPage.LearningSelection,
                        "startup-failure-probe-freezes-retry-destination-before-bootstrap");
                    localSessionId = failedContext.SessionId;
                }
                else
                {
                    if (SceneManager.GetActiveScene().name != YardCraneStudentSideShell.SceneName) return;
                    shell = UnityEngine.Object.FindFirstObjectByType<YardCraneStudentSideShell>();
                    if (shell == null || !shell.IsReady ||
                        YardCraneLocalTrainingNavigationBridge.Active != null ||
                        YardCraneBootstrap.Active != null) return;

                    YardCraneBootstrap.EditorOperationalSceneOverrideForVerification = string.Empty;
                    var failureStatus = shell.VisualRoot.Q<Label>("learning-launch-status");
                    Require(shell.Page == YardCraneStudentSidePage.Learning &&
                            failureStatus.resolvedStyle.display == DisplayStyle.Flex &&
                            failureStatus.ClassListContains("danger") &&
                            failureStatus.text.Contains("训练场景启动失败"),
                        "bootstrap-startup-failure-returns-to-origin-with-retryable-error");
                    Require(!YardCraneFallbackSession.IsActive &&
                            !YardCraneTrainingSessionIdentity.HasStudent &&
                            YardCraneStudentHomeHandoff.PendingProfile == null &&
                            YardCraneStudentHomeHandoff.PendingNotice == string.Empty,
                        "bootstrap-startup-failure-cleans-owned-session-and-one-shot-handoff");
                    Require(Directory.GetFiles(testOutputDirectory, "run-*.json").Length == 3,
                        "bootstrap-startup-failure-does-not-manufacture-an-operational-run-record");
                    Finish(true,
                        "1920x1080 three-card reference layout with equal initial card styling; " +
                        "independent Display2; image resources; " +
                        "learning reference layout; five single + four linked + one skill training entries + eight fault-selection entries; " +
                        "five distinct yard-crane thumbnails loaded; YC terminology; " +
                        "reachable exclusive target selection; both learning back routes; " +
                        "actual guided-learning, free-practice and local-assessment launches with HUD-back round trips; " +
                        "task briefs and five-second countdown input gates; frozen session context and return destinations; delayed identity cleanup; " +
                        "formal YardCrane scene-owned Display1 HUD, " +
                        "one UIDocument/adapter, two cameras, one MainCamera and one AudioListener; " +
                        "single Bootstrap/HUD/bridge/coordinator runtime ownership; " +
                        "mode/trust-aware non-official audit records; " +
                        "forced missing-operational-scene recovery to the originating picker");
                    return;
                }

                step++;
                waitUntil = now + 0.75;
                EditorApplication.QueuePlayerLoopUpdate();
            }
            catch (Exception error)
            {
                Finish(false, error.ToString());
            }
        }

        private static void VerifyHomeLayout()
        {
            var root = shell.VisualRoot.Q("student-side-shell").worldBound;
            var page = shell.VisualRoot.Q("student-home-page");
            Require(root.width > 1900 && root.height > 1070, "reference-canvas-resolves-at-1920x1080");
            Require(page.resolvedStyle.display == DisplayStyle.Flex, "home-page-visible");
            Require(shell.VisualRoot.Q<Label>("identity").text.Contains("YC-VERIFY"),
                "login-identity-survives-handoff");

            var cards = new[]
            {
                shell.VisualRoot.Q<Button>("learning-mode-button"),
                shell.VisualRoot.Q<Button>("launch-button"),
                shell.VisualRoot.Q<Button>("assessment-mode-button")
            };
            Require(cards.All(card => card != null && card.enabledSelf && !card.focusable),
                "all-three-mode-cards-enabled-and-non-focus-stealing");
            Require(cards.All(card => card.worldBound.width >= 342 && card.worldBound.width <= 346 &&
                                      card.worldBound.height >= 418 && card.worldBound.height <= 422),
                $"three-mode-card-size-matches-external-reference:actual=" +
                string.Join(",", cards.Select(card => $"{card.worldBound.width:0.0}x{card.worldBound.height:0.0}")));
            Require(cards[0].worldBound.xMax < cards[1].worldBound.xMin &&
                    cards[1].worldBound.xMax < cards[2].worldBound.xMin,
                "three-mode-cards-do-not-overlap");
            Require(cards.All(card => !card.ClassListContains("mode-card-selected")),
                "all-three-mode-cards-start-with-equal-visual-priority");

            var modeArt = shell.VisualRoot.Query<VisualElement>(className: "mode-card-art").ToList();
            Require(modeArt.Count == 3, "three-mode-art-elements");
            Require(modeArt.All(element => element.resolvedStyle.backgroundImage.texture != null),
                "three-mode-art-textures-loaded");
            Require(shell.VisualRoot.Q<VisualElement>(className: "mode-topbar") != null &&
                    shell.VisualRoot.Q<VisualElement>(className: "mode-grid") != null,
                "reference-topbar-and-card-grid-exist");
        }

        private static void VerifyLearningLayoutAndInteractions()
        {
            var root = shell.VisualRoot;
            var page = root.Q("student-learning-page");
            var home = root.Q("student-home-page");
            Require(page.resolvedStyle.display == DisplayStyle.Flex &&
                    home.resolvedStyle.display == DisplayStyle.None,
                "learning-page-is-exclusive-over-home");

            var singleRow = root.Q("single-drill-row");
            var linkedRow = root.Q("linked-drill-row");
            var skillRow = root.Q("skill-drill-row");
            var faultRow = root.Q("fault-drill-row");
            Require(singleRow.resolvedStyle.display == DisplayStyle.Flex &&
                    linkedRow.resolvedStyle.display == DisplayStyle.None &&
                    skillRow.resolvedStyle.display == DisplayStyle.None &&
                    faultRow.resolvedStyle.display == DisplayStyle.None,
                "single-item-training-is-the-default-visible-category");

            var singleCards = singleRow.Query<VisualElement>(className: "drill-card").ToList();
            var singleMedia = singleRow.Query<VisualElement>(className: "drill-card-media").ToList();
            var singlePhotos = singleRow.Query<VisualElement>(className: "drill-card-photo").ToList();
            var singleStarts = new[]
            {
                root.Q<Button>("drill-gantry-start"),
                root.Q<Button>("drill-trolley-start"),
                root.Q<Button>("drill-hoist-start"),
                root.Q<Button>("drill-tilt-start"),
                root.Q<Button>("drill-pitch-start")
            };
            Require(singleCards.Count == 5 && singleMedia.Count == 5 && singlePhotos.Count == 5 &&
                    singleStarts.All(button => button != null && button.enabledSelf),
                "learning-page-has-five-complete-single-item-cards");

            var singleTitles = singleRow.Query<Label>(className: "drill-card-title").ToList();
            var expectedSingleTitles = new[] { "大车移动", "小车移动", "吊具起升", "吊具回旋", "吊具回零" };
            Require(singleTitles.Select(label => label.text).SequenceEqual(expectedSingleTitles),
                "single-item-drills-use-yard-crane-terminology");
            var allLearningText = string.Join("|", page.Query<Label>().ToList().Select(label => label.text));
            Require(!allLearningText.Contains("岸桥") &&
                    !allLearningText.Contains("QUAY BRIDGE") &&
                    !allLearningText.Contains("HOOK TILTING"),
                "learning-page-rejects-quay-crane-only-terminology");

            var firstCard = singleCards[0].worldBound;
            Debug.Log($"[YC_STUDENT_HOME_REFERENCE_GEOMETRY] " +
                      $"card={firstCard.width:0.0}x{firstCard.height:0.0}, " +
                      $"cardY={firstCard.yMin:0.0}-{firstCard.yMax:0.0}");
            Require(singleCards.All(card => Math.Abs(card.worldBound.width - firstCard.width) <= 2f &&
                                            Math.Abs(card.worldBound.height - firstCard.height) <= 2f &&
                                            Math.Abs(card.worldBound.yMin - firstCard.yMin) <= 2f &&
                                            Math.Abs(card.worldBound.yMax - firstCard.yMax) <= 2f),
                "single-item-cards-are-equal-size-and-aligned");
            Require(firstCard.height >= 800f && firstCard.height <= 880f,
                $"learning-card-height-fills-external-reference-body:actual={firstCard.height:0.0}");
            for (var index = 1; index < singleCards.Count; index++)
            {
                var gap = singleCards[index].worldBound.xMin - singleCards[index - 1].worldBound.xMax;
                Require(gap >= 12f && gap <= 16f,
                    $"learning-card-gap-{index}:actual={gap:0.0}");
            }

            var sidebar = root.Q<VisualElement>(className: "learning-sidebar").worldBound;
            var heading = root.Q<Label>(className: "learning-heading").worldBound;
            Debug.Log($"[YC_STUDENT_HOME_REFERENCE_GEOMETRY] " +
                      $"sidebar={sidebar.width:0.0}x{sidebar.height:0.0}, " +
                      $"headingY={heading.yMin:0.0}-{heading.yMax:0.0}");
            Require(sidebar.xMax < firstCard.xMin &&
                    sidebar.width >= 214f && sidebar.width <= 218f &&
                    sidebar.height >= 956f && sidebar.height <= 960f,
                $"learning-sidebar-matches-external-reference:actual={sidebar.width:0.0}x{sidebar.height:0.0}");
            Require(heading.yMax < firstCard.yMin,
                "learning-heading-is-above-card-grid");

            var firstMedia = singleMedia[0].worldBound;
            Require(singleMedia.All(element => Math.Abs(element.worldBound.width - firstMedia.width) <= 2f &&
                                               Math.Abs(element.worldBound.height - firstMedia.height) <= 2f &&
                                               element.worldBound.xMin >= singleCards[singleMedia.IndexOf(element)].worldBound.xMin &&
                                               element.worldBound.xMax <= singleCards[singleMedia.IndexOf(element)].worldBound.xMax),
                "learning-media-are-equal-and-contained");
            Require(firstMedia.height >= 126f && firstMedia.height <= 130f,
                $"learning-media-height-matches-external-reference:actual={firstMedia.height:0.0}");

            var expectedTexturePaths = new[]
            {
                "Assets/YardCraneSimulator/Shell/Resources/YardCraneShell/StudentSide/Display1UI/LearningThumbnails/DrillGantry.png",
                "Assets/YardCraneSimulator/Shell/Resources/YardCraneShell/StudentSide/Display1UI/LearningThumbnails/DrillTrolley.png",
                "Assets/YardCraneSimulator/Shell/Resources/YardCraneShell/StudentSide/Display1UI/LearningThumbnails/DrillHoist.png",
                "Assets/YardCraneSimulator/Shell/Resources/YardCraneShell/StudentSide/Display1UI/LearningThumbnails/DrillSpreaderRotation.png",
                "Assets/YardCraneSimulator/Shell/Resources/YardCraneShell/StudentSide/Display1UI/LearningThumbnails/DrillSpreaderZeroing.png"
            };
            var actualTexturePaths = singlePhotos.Select(photo =>
            {
                var texture = photo.resolvedStyle.backgroundImage.texture;
                Require(texture != null, $"learning-thumbnail-loaded:{photo.name}");
                return AssetDatabase.GetAssetPath(texture);
            }).ToArray();
            Require(actualTexturePaths.SequenceEqual(expectedTexturePaths) &&
                    actualTexturePaths.Distinct().Count() == 5,
                "five-distinct-yard-crane-thumbnails-are-bound-in-order");
            Require(root.Q<VisualElement>(className: "learning-title-icon")
                        .resolvedStyle.backgroundImage.texture != null,
                "learning-title-icon-is-loaded");
            Capture("student-learning-yard-crane-reference");

            var firstButton = singleStarts[0].worldBound;
            Require(singleStarts.All(button => Math.Abs(button.worldBound.width - firstButton.width) <= 2f &&
                                               Math.Abs(button.worldBound.height - firstButton.height) <= 2f &&
                                               Math.Abs(button.worldBound.yMax - firstButton.yMax) <= 2f),
                "single-item-start-buttons-share-one-baseline");
            Require(firstButton.height >= 40f && firstButton.height <= 44f,
                $"learning-start-button-height-matches-external-reference:actual={firstButton.height:0.0}");

            var singleOptionGroups = new[]
            {
                root.Q("drill-gantry-options"), root.Q("drill-trolley-options"),
                root.Q("drill-hoist-options"), root.Q("drill-tilt-options"),
                root.Q("drill-pitch-options")
            };
            VerifyExclusiveOptionGroups(singleOptionGroups);

            var linkedStarts = new[]
            {
                root.Q<Button>("drill-linked-gantry-trolley-start"),
                root.Q<Button>("drill-linked-gantry-hoist-start"),
                root.Q<Button>("drill-linked-trolley-hoist-start"),
                root.Q<Button>("drill-linked-gantry-trolley-hoist-start")
            };
            var skillStart = root.Q<Button>("drill-skill-gantry-straight-start");
            var allStarts = singleStarts.Concat(linkedStarts).Append(skillStart).ToArray();
            Require(root.Query<VisualElement>(className: "drill-card").ToList().Count == 10 &&
                    allStarts.Length == 10 &&
                    allStarts.All(button => button != null && button.enabledSelf &&
                                               button.pickingMode == PickingMode.Position),
                "all-ten-training-cards-have-live-start-actions");

            var local = root.Q<Button>("learning-scope-local");
            var remote = root.Q<Button>("learning-scope-remote");
            Require(local.ClassListContains("learning-scope-chip-active") &&
                    !remote.ClassListContains("learning-scope-chip-active"),
                "learning-local-scope-is-default");
            Click(root, remote);
            Require(root.Q<Label>("learning-launch-status").text.Contains("远程训练尚未接入") &&
                    local.ClassListContains("learning-scope-chip-active") &&
                    !remote.ClassListContains("learning-scope-chip-active"),
                "remote-scope-is-an-honest-placeholder");
            Click(root, local);

            Click(root, root.Q<Button>("learning-nav-link"));
            Require(singleRow.resolvedStyle.display == DisplayStyle.None &&
                    linkedRow.resolvedStyle.display == DisplayStyle.Flex &&
                    skillRow.resolvedStyle.display == DisplayStyle.None &&
                    root.Q<Button>("learning-nav-link").ClassListContains("active"),
                "linked-category-switches-to-its-own-live-row");
            var linkedCards = linkedRow.Query<VisualElement>(className: "drill-card").ToList();
            var linkedTitles = linkedRow.Query<Label>(className: "drill-card-title").ToList();
            var expectedLinkedTitles = new[] { "大车小车联动", "大车起升", "小车起升", "大车小车起升" };
            Require(linkedCards.Count == 4 &&
                    linkedTitles.Select(label => label.text).SequenceEqual(expectedLinkedTitles) &&
                    linkedCards.All(card => card.worldBound.width > 200f && card.worldBound.height > 500f),
                "linked-category-exposes-four-visible-yard-crane-drills");
            VerifyExclusiveOptionGroups(new[]
            {
                root.Q("drill-linked-gantry-trolley-options"),
                root.Q("drill-linked-gantry-hoist-options"),
                root.Q("drill-linked-trolley-hoist-options"),
                root.Q("drill-linked-gantry-trolley-hoist-options")
            });

            Click(root, root.Q<Button>("learning-nav-skill"));
            Require(singleRow.resolvedStyle.display == DisplayStyle.None &&
                    linkedRow.resolvedStyle.display == DisplayStyle.None &&
                    skillRow.resolvedStyle.display == DisplayStyle.Flex &&
                    root.Q<Button>("learning-nav-skill").ClassListContains("active") &&
                    skillRow.Query<VisualElement>(className: "drill-card").ToList().Count == 1 &&
                    skillRow.Q<Label>(className: "drill-card-title").text == "大车直线行走",
                "skill-category-exposes-the-live-gantry-straight-line-drill");

            Click(root, root.Q<Button>("learning-nav-single"));
            var workPlaceholder = root.Q<Button>("learning-nav-work");
            Click(root, workPlaceholder);
            Require(shell.Page == YardCraneStudentSidePage.Learning &&
                    root.Q<Label>("learning-launch-status").text.Contains("作业采样尚未接入") &&
                    singleRow.resolvedStyle.display == DisplayStyle.Flex,
                "learning-placeholder-is-honest:learning-nav-work");

            Click(root, root.Q<Button>("learning-nav-fault"));
            var faultCards = faultRow.Query<Button>(className: "fault-card").ToList();
            var faultTitles = faultRow.Query<Label>(className: "fault-card-title").ToList();
            var expectedFaultTitles = new[]
            {
                "电源故障", "俯仰小车机构故障", "起升、大车机构故障", "夹轮器故障",
                "左右倾故障", "平面回转故障", "吊具泵故障", "前后倾故障"
            };
            var expectedFaultCounts = new[] { 12, 14, 17, 3, 6, 4, 6, 5 };
            Require(singleRow.resolvedStyle.display == DisplayStyle.None &&
                    linkedRow.resolvedStyle.display == DisplayStyle.None &&
                    skillRow.resolvedStyle.display == DisplayStyle.None &&
                    faultRow.resolvedStyle.display == DisplayStyle.Flex &&
                    root.Q<Button>("learning-nav-fault").ClassListContains("active") &&
                    faultCards.Count == 8 &&
                    faultCards.All(button => button.enabledSelf && button.pickingMode == PickingMode.Position) &&
                    faultTitles.Select(label => label.text).SequenceEqual(expectedFaultTitles),
                "fault-category-exposes-eight-reference-fault-types");
            var faultCategoryGrid = root.Q("fault-category-grid");
            var faultDetailView = root.Q("fault-detail-view");
            var faultDetailList = root.Q("fault-detail-list");
            var faultScroll = root.Q<ScrollView>("fault-detail-scroll");
            for (var index = 0; index < faultCards.Count; index++)
            {
                Click(root, faultCards[index]);
                var faultRows = faultDetailList.Query<VisualElement>(className: "fault-detail-row").ToList();
                var actionButtons = faultDetailList.Query<Button>(className: "fault-table-action").ToList();
                Require(faultCards[index].ClassListContains("selected") &&
                        faultCategoryGrid.resolvedStyle.display == DisplayStyle.None &&
                        faultDetailView.resolvedStyle.display == DisplayStyle.Flex &&
                        root.Q<Label>("fault-detail-title").text == expectedFaultTitles[index] &&
                        root.Q<Label>("fault-detail-count").text == $"{expectedFaultCounts[index]} 项" &&
                        faultRows.Count == expectedFaultCounts[index] &&
                        actionButtons.Count == expectedFaultCounts[index] * 3,
                    $"fault-card-opens-its-scrollable-detail-list:{index}");

                if (index == 0)
                {
                    var firstFaultCells = faultRows[0].Query<Label>(className: "fault-table-cell").ToList();
                    Require(root.Query<Label>(className: "fault-table-header-cell").ToList().Count == 6 &&
                            faultScroll != null &&
                            faultScroll.mode == ScrollViewMode.Vertical &&
                            faultScroll.verticalScrollerVisibility == ScrollerVisibility.AlwaysVisible &&
                            firstFaultCells.Count == 3 &&
                            firstFaultCells[0].text == "主电路无供电" &&
                            firstFaultCells[1].text == "010-1" &&
                            firstFaultCells[2].text == "KM04:2--KM01:21",
                        "fault-detail-has-six-columns-full-source-row-and-vertical-scroll");
                    Click(root, actionButtons[0]);
                    Require(root.Q<Label>("learning-launch-status").text.Contains("未包含在当前工作簿中"),
                        "fault-resource-button-does-not-fake-missing-drawings");
                }

                Click(root, root.Q<Button>("fault-detail-back-button"));
                Require(faultCategoryGrid.resolvedStyle.display == DisplayStyle.Flex &&
                        faultDetailView.resolvedStyle.display == DisplayStyle.None,
                    $"fault-detail-back-restores-category-grid:{index}");
            }

            Require(!string.Join("|", root.Query<Label>().ToList().Select(label => label.text))
                    .Contains("训练场景尚未接入"),
                "all-ten-live-training-starts-do-not-advertise-placeholder-scenes");

            Capture("student-learning-yard-crane-interactions");
            Click(root, root.Q<Button>("learning-nav-single"));
            Click(root, root.Q<Button>("learning-back-crumb"));
            Require(shell.Page == YardCraneStudentSidePage.Home,
                "learning-breadcrumb-returns-home");
            Click(root, root.Q<Button>("learning-mode-button"));
            Require(shell.Page == YardCraneStudentSidePage.Learning,
                "learning-page-can-reopen");
            Click(root, root.Q<Button>("learning-back-button"));
            Require(shell.Page == YardCraneStudentSidePage.Home,
                "learning-top-back-returns-home");
        }

        private static void VerifyExclusiveOptionGroups(VisualElement[] optionGroups)
        {
            foreach (var group in optionGroups)
            {
                Require(group != null, "option-group-exists");
                var toggles = group.Query<Toggle>().ToList();
                Require(toggles.Count >= 2 && toggles.Count(toggle => toggle.value) == 1,
                    $"exclusive-default:{group.name}");
                toggles[1].value = true;
                Require(toggles[1].value && toggles.Count(toggle => toggle.value) == 1,
                    $"exclusive-select:{group.name}");
                toggles[1].value = false;
                Require(toggles[1].value && toggles.Count(toggle => toggle.value) == 1,
                    $"exclusive-cannot-clear-last:{group.name}");
            }
        }

        private static void Click(VisualElement visualRoot, Button button)
        {
            Require(button != null, "button-exists");
            var center = button.worldBound.center;
            var hit = visualRoot.panel.Pick(center);
            Require(hit == button || (hit != null && button.Contains(hit)), "pointer-hits-" + button.name);
            using (var down = PointerDownEvent.GetPooled(new Event
                   { type = EventType.MouseDown, button = 0, mousePosition = center }))
            {
                down.target = button;
                button.SendEvent(down);
            }
            using (var up = PointerUpEvent.GetPooled(new Event
                   { type = EventType.MouseUp, button = 0, mousePosition = center }))
            {
                up.target = button;
                button.SendEvent(up);
            }
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
                Directory.CreateDirectory("Logs/StudentHomeVerification");
                File.WriteAllBytes($"Logs/StudentHomeVerification/{name}.png", image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.Destroy(image);
            }
        }

        private static T ReadAssetField<T>(ScriptableObject asset, string fieldName)
        {
            if (asset == null)
                throw new InvalidOperationException($"Cannot read '{fieldName}' from a null asset.");
            var field = asset.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (field == null || field.FieldType != typeof(T))
                throw new InvalidOperationException(
                    $"{asset.name}.asset does not expose {fieldName} as {typeof(T).Name}.");
            return (T)field.GetValue(asset);
        }

        private static void Require(bool value, string name)
        {
            if (!value) throw new InvalidOperationException(name);
        }

        private static double JsonNumber(string json, string propertyName)
        {
            var match = Regex.Match(
                json ?? string.Empty,
                $"\\\"{Regex.Escape(propertyName)}\\\"\\s*:\\s*(-?[0-9]+(?:\\.[0-9]+)?(?:[eE][+-]?[0-9]+)?)");
            return match.Success && double.TryParse(
                    match.Groups[1].Value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var value)
                ? value
                : double.NaN;
        }

        private static string SourceFingerprint()
        {
            var paths = new[]
            {
                "Assets/YardCraneSimulator/Shell/YardCraneStudentSideShell.cs",
                "Assets/YardCraneSimulator/Shell/Resources/YardCraneShell/StudentSide/Display1UI/StudentFaultCatalog.json",
                "Assets/YardCraneSimulator/Runtime/Training/YardCraneLocalTrainingSession.cs",
                "Assets/YardCraneSimulator/Runtime/Training/YardCraneLocalTrainingModeCoordinator.cs",
                "Assets/YardCraneSimulator/Runtime/Training/YardCraneTrainingScoreRecorder.cs",
                "Assets/YardCraneSimulator/Editor/YardCraneStudentHomeVerification.cs"
            };
            using (var stream = new MemoryStream())
            {
                foreach (var path in paths.OrderBy(value => value, StringComparer.Ordinal))
                {
                    var header = Encoding.UTF8.GetBytes(path + "\n");
                    stream.Write(header, 0, header.Length);
                    var bytes = File.ReadAllBytes(path);
                    stream.Write(bytes, 0, bytes.Length);
                }
                using (var sha = SHA256.Create())
                {
                    return BitConverter.ToString(sha.ComputeHash(stream.ToArray()))
                        .Replace("-", string.Empty)
                        .ToLowerInvariant();
                }
            }
        }

        private static void InvalidatePreviousResult()
        {
            var resultDirectory = Path.GetDirectoryName(ResultPath);
            Directory.CreateDirectory(resultDirectory);
            if (File.Exists(ResultPath)) File.Delete(ResultPath);
            File.WriteAllText(ResultPath,
                $"[YC_STUDENT_HOME_PLAY_RUNNING] utc={DateTime.UtcNow:O}, awaiting current run result");
        }

        private static int CountAll<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        }

        private static void VerifyOperationalSceneComposition(
            YardCraneBootstrap bootstrap,
            YardCraneOperatorHud hud,
            YardCraneHudSceneAdapter adapter)
        {
            var scene = SceneManager.GetActiveScene();
            Require(scene.name == OperationalSceneName && scene.path == OperationalScenePath,
                $"formal-operational-scene-is-active:name={scene.name},path={scene.path}");
            Require(bootstrap.SceneConfig != null &&
                    bootstrap.SceneConfig.operationalSceneName == OperationalSceneName,
                "bootstrap-config-targets-formal-yard-crane-scene");

            var documents = SceneComponents<UIDocument>(scene);
            Require(CountAll<YardCraneOperatorHud>() == 1 && CountAll<UIDocument>() == 1,
                "operational-runtime-has-one-hud-and-one-uidocument");
            Require(documents.Length == 1 &&
                    documents[0].gameObject.name == "UIDocumentDisplay1" &&
                    documents[0].gameObject == hud.gameObject &&
                    hud.gameObject.scene == scene,
                "display1-uidocument-and-hud-are-owned-by-operational-scene");
            Require(documents[0].panelSettings != null &&
                    documents[0].panelSettings.targetDisplay == 0,
                "operational-hud-targets-display1");
            Require(!hud.transform.IsChildOf(bootstrap.transform),
                "operational-hud-is-not-bootstrap-owned");

            var cameras = SceneComponents<Camera>(scene);
            var mainCameras = cameras.Where(camera => camera.CompareTag("MainCamera")).ToArray();
            var listeners = SceneComponents<AudioListener>(scene);
            var enabledListeners = listeners.Where(listener => listener.isActiveAndEnabled).ToArray();
            var display1Camera = cameras.SingleOrDefault(camera => camera.name == "Display1Camera");
            var display2Camera = cameras.SingleOrDefault(camera => camera.name == "Display2Camera");
            Require(cameras.Length == 2,
                $"operational-scene-has-exactly-two-cameras:actual={cameras.Length}");
            Require(mainCameras.Length == 1 && Camera.main == mainCameras[0],
                $"operational-scene-has-one-active-main-camera:actual={mainCameras.Length}");
            Require(display1Camera != null && display2Camera != null &&
                    display1Camera.targetDisplay == 0 && display2Camera.targetDisplay == 1,
                "operational-scene-routes-full-screen-cameras-to-native-display1-and-display2");
            Require(enabledListeners.Length == 1 &&
                    enabledListeners[0].gameObject == mainCameras[0].gameObject,
                $"operational-scene-has-one-enabled-main-camera-audio-listener:actual={enabledListeners.Length}");

            var adapters = SceneComponents<YardCraneHudSceneAdapter>(scene);
            Require(CountAll<YardCraneHudSceneAdapter>() == 1 &&
                    adapters.Length == 1 && adapters[0] == adapter &&
                    adapter.gameObject == hud.gameObject,
                "operational-scene-has-one-display1-hud-adapter");
        }

        private static T[] SceneComponents<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }

        private static void Finish(bool passed, string detail)
        {
            var ownsPlay = SessionState.GetBool(RunningKey, false);
            SessionState.SetBool(RunningKey, false);
            try
            {
                try
                {
                    var document = shell != null ? shell.Document : null;
                    if (document != null && document.panelSettings != null)
                        document.panelSettings.targetTexture = previousTarget;
                    if (target != null) UnityEngine.Object.Destroy(target);
                }
                catch (Exception cleanupError)
                {
                    passed = false;
                    detail += $" | visual cleanup failed safely: {cleanupError.Message}";
                }

                var message = $"[YC_STUDENT_HOME_PLAY_{(passed ? "PASS" : "FAIL")}] " +
                              $"version={YardCraneStudentSideShell.Version}, " +
                              $"coordinatorVersion={YardCraneLocalTrainingModeCoordinator.Version}, " +
                              $"auditFormat={YardCraneTrainingScoreRecorder.FormatVersion}, " +
                              $"sourceFingerprint=sha256:{SourceFingerprint()}, " +
                              $"utc={DateTime.UtcNow:O}, {detail}";
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
                    File.WriteAllText(ResultPath, message);
                    if (passed) Debug.Log(message); else Debug.LogError(message);
                }
                catch (Exception resultError)
                {
                    passed = false;
                    Debug.LogError($"[YC_STUDENT_HOME_VERIFY_RESULT_WRITE_FAILED] {resultError}");
                }
            }
            finally
            {
                shell = null;
                target = null;
                previousTarget = null;
                step = 0;
                startedAt = 0;
                waitUntil = 0;
                if (ownsPlay)
                {
                    SessionState.SetBool(RestoreKey, true);
                    EditorApplication.isPlaying = false;
                }
                if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
            }
        }
    }
}
