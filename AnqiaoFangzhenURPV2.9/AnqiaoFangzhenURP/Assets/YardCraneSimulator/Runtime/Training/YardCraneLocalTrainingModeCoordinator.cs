using System;
using PortTraining.YardCraneSimulator.Bootstrap;
using PortTraining.YardCraneSimulator.Configuration;
using PortTraining.YardCraneSimulator.Controls;
using PortTraining.YardCraneSimulator.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PortTraining.YardCraneSimulator.Training
{
    /// <summary>
    /// Coordinates the frozen local-training context with the scene-owned HUD and its
    /// read-only telemetry feed. It creates no UIDocument, owns no actuator and never talks
    /// to PLC/Gateway services. Guided learning produces goal achievement only; assessment
    /// produces an explicitly local, unscored process result.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(14500)]
    public sealed class YardCraneLocalTrainingModeCoordinator : MonoBehaviour
    {
        public const string Version = "2026-09-21-yc-local-training-mode-v3-open-guided-learning";
        public const double DefaultTargetStableSeconds = 0.35d;

        private const string TrolleyTransformName = "20_TrolleyMotion";
        private const string SpreaderBodyTransformName = "30_SpreaderPhysics";
        private const string SpreaderPivotTransformName = "35_Spreader";
        private const string LocalResultTrustText =
            "仅本机场景过程判定 · 不生成分数 · 未经 NodeService / 教师平台确认";

        private YardCraneLocalTrainingSessionContext context;
        private YardCraneOperatorHud hud;
        private YardCraneBootstrap bootstrap;
        private YardCraneStandaloneOperationHost inputHost;

        // 2026-09-11: a linked exercise needs 2-3 axes achieved at once; a single-axis exercise
        // still uses this same list with exactly one entry, so there is only one code path.
        private readonly System.Collections.Generic.List<YardCraneLearningGoalTracker> learningTrackers = new();
        private YardCraneLocalAssessmentTracker assessmentTracker;
        private YardCraneHudTrainingTaskPresentation currentPresentation;

        private bool presentationBound;
        private bool evaluationPrepared;
        private bool taskBriefRequested;
        private bool countdownRequested;
        private bool completionRequested;
        // 2026-09-11: real Play evidence (diagnostic ClosePrompt logging, see 8节) showed that
        // clicking "步骤明细" closes the Completion prompt (HandleCompletionDetails calls
        // ClosePrompt before opening AssessmentDetails), so the completionRequested poll saw
        // ActivePromptKind go back to None the moment the student closed the details view and
        // navigated home - even though they never actually closed the completion summary
        // itself. This flag distinguishes "closed the details drill-down" (re-show the
        // completion summary) from "closed the completion summary itself" (actually navigate
        // home) - see the completionRequested poll in Update().
        private bool completionDetailsShown;
        private int completionElapsedSecondsCache;
        private int completionScoreCache;
        private bool trackingStarted;
        private bool evaluationFinished;
        private bool rejected;
        private bool sessionMismatchReported;
        private bool initialInputGatePending;

        private bool hasTrolleyFrame;
        private bool hasSpreaderFrame;
        private double trolleyHomeLocalX;
        private double trolleyWorldY;
        private double spreaderPivotOffsetY;
        private double spreaderStartHeight = double.NaN;
        private double resolvedLearningTarget = double.NaN;
        private double trackingStartedAt = double.NaN;
        private double finishedElapsed = double.NaN;
        private double lastProcessedSampleTime = double.NegativeInfinity;
        private int lastRenderedProgress = int.MinValue;
        private int lastRenderedRemaining = int.MinValue;
        private YardCraneLocalAssessmentStage lastRenderedAssessmentStage =
            (YardCraneLocalAssessmentStage)(-1);
        private string validationError = string.Empty;

        public YardCraneLocalTrainingSessionContext Context => context;
        public bool IsTracking => trackingStarted && !evaluationFinished && !rejected;
        public bool IsRejected => rejected;
        public bool IsEvaluationFinished => evaluationFinished;
        public string ValidationError => validationError;
        public double SpreaderStartHeight => spreaderStartHeight;
        public double ResolvedLearningTarget => resolvedLearningTarget;
        public double ElapsedSeconds => GetElapsedSeconds(Time.realtimeSinceStartupAsDouble);
        public YardCraneLearningGoalProgress LearningProgress => CombineLearningProgress(learningTrackers);
        public YardCraneLocalAssessmentProgress AssessmentProgress => assessmentTracker != null
            ? assessmentTracker.Progress
            : default;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe()
        {
            SceneManager.sceneLoaded -= InstallInScene;
            SceneManager.sceneLoaded += InstallInScene;
        }

        private static void InstallInScene(Scene scene, LoadSceneMode mode)
        {
            var activeContext = YardCraneLocalTrainingSession.CurrentContext;
            if (activeContext == null) return;

            YardCraneOperatorHud sceneHud = null;
            var hudCount = 0;
            foreach (var sceneRoot in scene.GetRootGameObjects())
            foreach (var candidate in sceneRoot.GetComponentsInChildren<YardCraneOperatorHud>(true))
            {
                sceneHud = candidate;
                hudCount++;
            }

            // Bootstrap and StudentHome legitimately contain no driving HUD. Installation is
            // deferred until the operational scene that owns exactly one HUD is loaded.
            if (hudCount == 0) return;
            if (hudCount != 1)
            {
                Debug.LogError(
                    $"[YC_LOCAL_TRAINING_COORDINATOR_REJECTED] scene={scene.name}, " +
                    $"reason=expected-one-scene-hud, actual={hudCount}");
                return;
            }

            var coordinator = sceneHud.GetComponent<YardCraneLocalTrainingModeCoordinator>();
            if (coordinator == null)
                coordinator = sceneHud.gameObject.AddComponent<YardCraneLocalTrainingModeCoordinator>();
            coordinator.Initialize(activeContext);
        }

        public void Initialize(YardCraneLocalTrainingSessionContext sessionContext)
        {
            if (sessionContext == null) throw new ArgumentNullException(nameof(sessionContext));
            if (context != null && context.SessionId != sessionContext.SessionId)
            {
                throw new InvalidOperationException(
                    "A local training coordinator cannot be rebound to a different session.");
            }

            context = sessionContext;
            hud = GetComponent<YardCraneOperatorHud>();
            if (hud == null)
            {
                validationError = "协调器必须挂接到场景已有的 YardCraneOperatorHud。";
                rejected = true;
                enabled = false;
                Debug.LogError($"[YC_LOCAL_TRAINING_COORDINATOR_REJECTED] {validationError}", this);
                return;
            }

            CaptureSceneReferenceFrames();
            if (context.Mode != YardCraneLocalTrainingMode.FreePractice &&
                !TrySetTrainingInputEnabled(false, out validationError))
            {
                // A missing host means there is no standalone business-input path yet. Retry
                // once after all sceneLoaded subscribers finish, then reject rather than run an
                // unenforced visual-only countdown.
                initialInputGatePending = true;
            }
            enabled = true;
            Debug.Log(
                $"[YC_LOCAL_TRAINING_COORDINATOR_BOUND] version={Version}, " +
                $"sessionId={context.SessionId:D}, mode={context.Mode}, exercise={context.Exercise}, " +
                $"metric={context.Target.MetricId}, authoritativeScore=false",
                this);
        }

        private void Update()
        {
            // 2026-09-11: pushed unconditionally, before every early-return below, so the
            // static holder always has this frame's value by the time Unity might destroy
            // this component - see YardCraneTrainingSessionIdentity.ElapsedSeconds for why a
            // push here (rather than a pull through a live component reference) is required.
            YardCraneTrainingSessionIdentity.RecordElapsedSeconds(ElapsedSeconds);

            if (context == null || hud == null) return;
            if (!OwnsCurrentSession())
            {
                HoldTrainingInput();
                if (!sessionMismatchReported)
                {
                    sessionMismatchReported = true;
                    Debug.LogWarning(
                        $"[YC_LOCAL_TRAINING_CONTEXT_CHANGED] coordinatorSession={context.SessionId:D}; " +
                        "evaluation stopped without clearing the replacement session.",
                        this);
                }
                enabled = false;
                return;
            }

            if (!hud.IsReady) return;
            EnsurePresentationBound();
            ReassertModeBinding();

            if (initialInputGatePending)
            {
                if (TrySetTrainingInputEnabled(false, out var gateError))
                {
                    initialInputGatePending = false;
                    validationError = string.Empty;
                }
                else
                {
                    initialInputGatePending = false;
                    RejectEvaluation(gateError);
                    return;
                }
            }

            // 2026-09-11: must be checked before the evaluationFinished early-return just below
            // - this flag only ever becomes true once evaluationFinished is already true (set
            // together in UpdateGuidedLearning's achieved branch), so it would never be reached
            // if it lived after that guard like the countdown/task-brief polls above do.
            if (completionRequested)
            {
                if (hud.ActivePromptKind == YardCraneHudPromptKind.None)
                {
                    if (completionDetailsShown)
                    {
                        // The student closed the 步骤明细 drill-down, not the completion
                        // summary itself (HandleCompletionDetails closes the summary before
                        // opening that view - see comment on completionDetailsShown). Return
                        // to the summary instead of ending the flow.
                        completionDetailsShown = false;
                        ShowCompletionSummary();
                    }
                    else
                    {
                        completionRequested = false;
                        hud.RequestBackNavigation();
                    }
                }
                return;
            }

            if (bootstrap == null)
            {
                bootstrap = YardCraneBootstrap.Active;
            }
            if (bootstrap == null || rejected || evaluationFinished) return;

            var sceneState = bootstrap.StateMachine.Current;
            if (context.Mode == YardCraneLocalTrainingMode.FreePractice)
            {
                if (!trackingStarted && sceneState == YardCraneSceneState.Running)
                {
                    if (!TrySetTrainingInputEnabled(true, out var gateError))
                    {
                        RejectEvaluation(gateError);
                        return;
                    }
                    BeginTracking();
                }
                return; // Open-ended by policy: no target tracker and no automatic completion.
            }

            if (!trackingStarted)
            {
                // 2026-09-11: same polling idiom as the countdown check just below - a prompt
                // this coordinator owns must actually close before the flow continues, rather
                // than assuming a fixed number of frames or wiring a prompt-specific callback.
                if (taskBriefRequested)
                {
                    if (hud.ActivePromptKind == YardCraneHudPromptKind.None)
                    {
                        taskBriefRequested = false;
                        RequestCountdownOrBegin();
                    }
                    return;
                }

                if (countdownRequested)
                {
                    // Closing or losing the scene-owned countdown cannot silently bypass it.
                    if (hud.ActivePromptKind == YardCraneHudPromptKind.None)
                    {
                        countdownRequested = false;
                        RequestCountdownOrBegin();
                    }
                    return;
                }

                if (sceneState != YardCraneSceneState.Running || !HasFreshReadySample()) return;
                if (!evaluationPrepared && !TryPrepareEvaluation(hud.Feed.Latest, out var error))
                {
                    RejectEvaluation(error);
                    return;
                }

                RequestTaskBriefOrBegin();
                return;
            }

            if (sceneState != YardCraneSceneState.Running && sceneState != YardCraneSceneState.Paused)
                return;

            var now = Time.realtimeSinceStartupAsDouble;
            var elapsed = GetElapsedSeconds(now);
            var assessmentDeadline = context.Mode == YardCraneLocalTrainingMode.Assessment &&
                                     context.Timing.DurationSeconds.HasValue
                ? trackingStartedAt + context.Timing.DurationSeconds.Value
                : double.PositiveInfinity;

            if (HasFreshReadySample())
            {
                var sample = hud.Feed.Latest;
                if (sample.SampledAtSeconds > lastProcessedSampleTime &&
                    (context.Mode != YardCraneLocalTrainingMode.Assessment ||
                     sample.SampledAtSeconds <= assessmentDeadline))
                {
                    lastProcessedSampleTime = sample.SampledAtSeconds;
                    if (context.Mode == YardCraneLocalTrainingMode.GuidedLearning &&
                        context.Target.HasTarget)
                        UpdateGuidedLearning(sample, elapsed);
                    else if (context.Mode == YardCraneLocalTrainingMode.Assessment)
                        UpdateAssessment(sample, elapsed);
                }
            }

            if (context.Mode == YardCraneLocalTrainingMode.Assessment && !evaluationFinished)
            {
                RenderAssessmentTimer(elapsed);
                if (now >= assessmentDeadline)
                {
                    var timeout = assessmentTracker.Timeout();
                    CompleteAssessmentFailure(timeout.FailureReason, elapsed);
                }
            }
        }

        private bool TryPrepareEvaluation(YardCraneHudSnapshot firstHealthySample, out string error)
        {
            switch (context.Mode)
            {
                case YardCraneLocalTrainingMode.GuidedLearning:
                    if (!context.Target.HasTarget)
                    {
                        evaluationPrepared = true;
                        error = string.Empty;
                        return true;
                    }
                    return TryPrepareGuidedLearning(firstHealthySample, out error);
                case YardCraneLocalTrainingMode.Assessment:
                    assessmentTracker = new YardCraneLocalAssessmentTracker();
                    evaluationPrepared = true;
                    error = string.Empty;
                    return true;
                default:
                    error = "本地训练模式不支持目标评估。";
                    return false;
            }
        }

        // 2026-09-11: a linked exercise (context.AdditionalTargets non-empty) needs the exact
        // same reachability/tracker preparation as a single-axis one, just once per axis. The
        // per-axis work is factored into TryPrepareGuidedLearningAxis below; this method just
        // loads the profile once and loops primary + additional targets over it. Any axis
        // failing validation rejects the whole exercise - no partial-axis start.
        private bool TryPrepareGuidedLearning(YardCraneHudSnapshot firstHealthySample, out string error)
        {
            learningTrackers.Clear();

            var profile = Resources.Load<YardCraneEightRopeMaxProfile>(
                YardCraneEightRopeMaxProfile.DefaultResourcePath);
            if (profile == null)
            {
                error = "无法读取当前灰盒物理配置，不能证明学习目标可达。";
                return false;
            }
            if (!profile.TryValidate(out var profileError))
            {
                error = "当前灰盒物理配置无效：" + profileError;
                return false;
            }

            if (!TryPrepareGuidedLearningAxis(
                    firstHealthySample, profile, context.Target, isPrimary: true, out var primaryTracker, out error))
            {
                return false;
            }
            learningTrackers.Add(primaryTracker);

            foreach (var extra in context.AdditionalTargets)
            {
                if (!TryPrepareGuidedLearningAxis(
                        firstHealthySample, profile, extra, isPrimary: false, out var extraTracker, out error))
                {
                    learningTrackers.Clear();
                    return false;
                }
                learningTrackers.Add(extraTracker);
            }

            evaluationPrepared = true;
            error = string.Empty;
            return true;
        }

        private bool TryPrepareGuidedLearningAxis(
            YardCraneHudSnapshot firstHealthySample,
            YardCraneEightRopeMaxProfile profile,
            YardCraneLocalTrainingTarget target,
            bool isPrimary,
            out YardCraneLearningGoalTracker tracker,
            out string error)
        {
            tracker = null;
            if (!TryReadMetric(firstHealthySample, target.Metric, out var currentValue))
            {
                error = $"当前 HUD 未提供可用的 {target.MetricId} 反馈，学习目标不能开始。";
                return false;
            }

            if ((target.Metric == YardCraneLocalTrainingMetric.TrolleyPosition ||
                 target.Metric == YardCraneLocalTrainingMetric.SpreaderHeight) &&
                !hasTrolleyFrame)
            {
                error = "无法读取小车初始坐标，不能证明学习目标可达。";
                return false;
            }
            if (target.Metric == YardCraneLocalTrainingMetric.SpreaderHeight && !hasSpreaderFrame)
            {
                error = "无法读取吊具场景坐标基准，不能证明学习目标可达。";
                return false;
            }

            var gantryLimit = Math.Max(
                0d,
                profile.motion.gantryTravelFromHomeMeters -
                profile.motion.gantryLimitSafetyMarginMeters);
            var trolleyLimit = Math.Max(
                0d,
                profile.motion.trolleyTravelFromHomeMeters -
                profile.motion.trolleyLimitSafetyMarginMeters);
            var rotationLimit = Math.Abs(profile.motion.spreaderRotationMaxAngleDegrees);
            var spreaderMinimum = hasSpreaderFrame
                ? trolleyWorldY - profile.geometryAndSafety.maximumSuspendedDropMeters +
                  spreaderPivotOffsetY
                : 0d;
            var spreaderMaximum = hasSpreaderFrame
                ? trolleyWorldY - profile.geometryAndSafety.minimumSuspendedDropMeters +
                  spreaderPivotOffsetY
                : 0d;

            var axisStartHeight = target.Metric == YardCraneLocalTrainingMetric.SpreaderHeight
                ? currentValue
                : double.NaN;
            if (isPrimary) spreaderStartHeight = axisStartHeight;
            var relativeMinimum = hasSpreaderFrame && !double.IsNaN(axisStartHeight)
                ? spreaderMinimum - axisStartHeight
                : 0d;
            var relativeMaximum = hasSpreaderFrame && !double.IsNaN(axisStartHeight)
                ? spreaderMaximum - axisStartHeight
                : 0d;
            var reachability = new YardCraneLocalTrainingReachability(
                new YardCraneLocalReachableRange(-gantryLimit, gantryLimit),
                new YardCraneLocalReachableRange(
                    hasTrolleyFrame ? trolleyHomeLocalX - trolleyLimit : 0d,
                    hasTrolleyFrame ? trolleyHomeLocalX + trolleyLimit : 0d),
                new YardCraneLocalReachableRange(spreaderMinimum, spreaderMaximum),
                new YardCraneLocalReachableRange(relativeMinimum, relativeMaximum),
                new YardCraneLocalReachableRange(-rotationLimit, rotationLimit));
            if (!YardCraneLocalTrainingTargetValidator.TryValidate(target, reachability, out error))
            {
                return false;
            }

            var resolvedTarget = target.ReferenceFrame ==
                                 YardCraneLocalTargetReferenceFrame.SpreaderStartRelativeMeters
                ? axisStartHeight + target.Value.Value
                : target.Value.Value;
            if (isPrimary) resolvedLearningTarget = resolvedTarget;
            if (!IsFinite(resolvedTarget) ||
                resolvedTarget < float.MinValue || resolvedTarget > float.MaxValue)
            {
                error = "学习目标无法转换为本地场景判定值。";
                return false;
            }

            if (!TryMapLearningMetric(target.Metric, out var learningMetric))
            {
                error = $"不支持的本地学习指标：{target.MetricId}。";
                return false;
            }

            var stableSeconds = target.Metric != YardCraneLocalTrainingMetric.SpreaderZeroing
                ? DefaultTargetStableSeconds
                : target.ZeroingPolicy == YardCraneLocalSpreaderZeroingPolicy.ReturnAndHold
                    ? target.RequiredHoldSeconds
                    : 0d;
            var localTolerance = (float)target.Tolerance.Value;
            if (float.IsNaN(localTolerance) || float.IsInfinity(localTolerance) || localTolerance <= 0f)
            {
                error = "学习目标容差无法转换为本地场景判定值。";
                return false;
            }
            tracker = new YardCraneLearningGoalTracker(
                learningMetric,
                (float)resolvedTarget,
                localTolerance,
                stableSeconds);
            error = string.Empty;
            return true;
        }

        // 2026-09-11: shows the "任务提醒" brief before the countdown. ShowTaskPrompt already
        // exists and is fully wired in the HUD (YardCraneOperatorHud.Prompts.cs); the
        // coordinator just never called it before this round. Bay/container fields are left at
        // their "—"/-1 defaults for GuidedLearning's axis-movement drills, which have no bay or
        // cargo concept - that part of the prompt's layout renders empty for those exercises, a
        // disclosed cosmetic gap, not a bug. Shared by GuidedLearning and Assessment (originally
        // GuidedLearning-only; extended to Assessment in the same round Assessment's completion
        // flow was brought onto ShowCompletionPrompt - see CompleteAssessmentPass below).
        // FreePractice bypasses this whole method via its own early-return branch in Update().
        private void RequestTaskBriefOrBegin()
        {
            if (trackingStarted || rejected || evaluationFinished || !evaluationPrepared) return;

            taskBriefRequested = true;
            hud.ShowTaskPrompt(new YardCraneHudTaskPrompt
            {
                Title = context.DisplayInfo.Title,
                Instruction = context.DisplayInfo.Instruction,
                ConfirmText = "开始"
            });
        }

        private void RequestCountdownOrBegin()
        {
            if (trackingStarted || rejected || evaluationFinished || !evaluationPrepared) return;
            var seconds = context.Timing.StartCountdownSeconds;
            if (seconds <= 0)
            {
                if (!TrySetTrainingInputEnabled(true, out var gateError))
                {
                    RejectEvaluation(gateError);
                    return;
                }
                BeginTracking();
                return;
            }

            if (!TrySetTrainingInputEnabled(false, out var countdownGateError))
            {
                RejectEvaluation(countdownGateError);
                return;
            }
            countdownRequested = true;
            hud.ShowCountdownPrompt(
                new YardCraneHudCountdownPrompt
                {
                    Title = context.Mode == YardCraneLocalTrainingMode.Assessment
                        ? "本地考核即将开始"
                        : "学习训练即将开始",
                    TotalSeconds = seconds
                },
                HandleCountdownElapsed);
        }

        private void HandleCountdownElapsed()
        {
            countdownRequested = false;
            if (!OwnsCurrentSession() || rejected || evaluationFinished || bootstrap == null) return;
            var state = bootstrap.StateMachine.Current;
            if (state != YardCraneSceneState.Running && state != YardCraneSceneState.Paused) return;
            if (!TrySetTrainingInputEnabled(true, out var gateError))
            {
                RejectEvaluation(gateError);
                return;
            }
            BeginTracking();
        }

        private void BeginTracking()
        {
            if (trackingStarted || rejected || evaluationFinished) return;
            trackingStarted = true;
            // One monotonic origin owns both the HUD's visible "用时" and all mode
            // completion/deadline calculations. This excludes scene startup and the visual
            // ready countdown from both clocks instead of displaying contradictory values.
            trackingStartedAt = hud.ResetElapsedClockForTraining();
            // Samples captured during the presentation countdown cannot satisfy an action.
            lastProcessedSampleTime = trackingStartedAt;

            if (context.Mode == YardCraneLocalTrainingMode.GuidedLearning &&
                context.Target.HasTarget)
            {
                SetTaskPresentation(
                    context.DisplayInfo.Title,
                    "学习目标：",
                    context.Target.DisplayText,
                    "完成进度：",
                    "0% · 进行中");
            }
            else if (context.Mode == YardCraneLocalTrainingMode.GuidedLearning)
            {
                SetTaskPresentation(
                    context.DisplayInfo.Title,
                    "学习内容：",
                    context.DisplayInfo.Instruction,
                    "完成规则：",
                    "按任务要求完成故障练习");
            }
            else if (context.Mode == YardCraneLocalTrainingMode.Assessment)
            {
                var progress = assessmentTracker.Progress;
                SetTaskPresentation(
                    context.DisplayInfo.Title,
                    "当前步骤：",
                    progress.Instruction,
                    "剩余时间：",
                    FormatDuration(context.Timing.DurationSeconds.GetValueOrDefault()));
            }

            Debug.Log(
                $"[YC_LOCAL_TRAINING_STARTED] sessionId={context.SessionId:D}, " +
                $"mode={context.Mode}, countdownSeconds={context.Timing.StartCountdownSeconds}, " +
                "timer=unscaled-monotonic",
                this);
        }

        // 2026-09-11: a linked exercise's "achieved" is all-axes-achieved (see
        // CombineLearningProgress); a single-axis exercise still has exactly one tracker in
        // learningTrackers, so this is the same behavior as before, just routed through the
        // combine step.
        private void UpdateGuidedLearning(YardCraneHudSnapshot sample, double elapsed)
        {
            foreach (var tracker in learningTrackers) tracker.Update(sample);
            var progress = CombineLearningProgress(learningTrackers);
            var combinedDisplayText = CombinedTargetDisplayText();
            var percent = Math.Max(0, Math.Min(100, (int)Math.Round(progress.Progress01 * 100f)));
            if (percent != lastRenderedProgress)
            {
                lastRenderedProgress = percent;
                SetTaskPresentation(
                    context.DisplayInfo.Title,
                    "学习目标：",
                    combinedDisplayText,
                    "完成进度：",
                    $"{percent}% · {(progress.ObservedRequiredAction ? "动作已识别" : "等待有效操作")}");
            }

            if (progress.State != YardCraneLearningGoalState.Achieved) return;
            evaluationFinished = true;
            finishedElapsed = elapsed;
            HoldTrainingInput();
            SetTaskPresentation(
                context.DisplayInfo.Title,
                "学习目标：",
                combinedDisplayText,
                "完成进度：",
                "100% · 已达成");
            // 2026-09-11: TryFinishLifecycle MUST run before ShowCompletionPrompt, not after -
            // confirmed by reading YardCraneOperatorHud.OnLifecycleChanged: it treats any prompt
            // already open at the moment the scene state transitions to a terminal state as a
            // "stale task modal" and force-closes it (its own comment: "a coordinator may
            // deliberately show the new run's completion prompt AFTER this transition"). The
            // original ordering here (show-then-finish) is exactly the mistake that comment
            // warns about, and real Play evidence confirmed it: the popup was opened and
            // immediately closed by that stale-modal cleanup before the student ever saw it,
            // and the subsequent completionRequested poll (seeing ActivePromptKind already back
            // to None) fired the return-to-StudentHome navigation on the very next frame.
            if (!TryFinishLifecycle(YardCraneSceneState.ExitAllowed, out var lifecycleError))
            {
                Debug.LogError(
                    $"[YC_LOCAL_LEARNING_LIFECYCLE_REJECTED] sessionId={context.SessionId:D}, " +
                    lifecycleError,
                    this);
                return;
            }
            // ShowCompletionPrompt (时长+得分) replaces the older BindLocalResult text-only
            // banner. Score is read from YardCraneTrainingSessionIdentity.CurrentScore (pushed
            // every frame by the Rig - see that field's comment) rather than this coordinator
            // holding any reference to the Rig's own trainingScore tracker, which would invert
            // the established Experiments->Runtime dependency direction. Showing this number
            // does not change GuidedLearning's Unscored/hasScore=false persistence below in
            // YardCraneTrainingScoreRecorder - it is a display-only addition.
            completionElapsedSecondsCache = (int)Math.Round(elapsed);
            completionScoreCache = YardCraneTrainingSessionIdentity.CurrentScore;
            completionRequested = true;
            completionDetailsShown = false;
            ShowCompletionSummary();
            Debug.Log(
                $"[YC_LOCAL_LEARNING_GOAL_ACHIEVED] sessionId={context.SessionId:D}, " +
                $"metric={CombinedTargetMetricIds()}, target={resolvedLearningTarget:0.###}, " +
                $"elapsedSeconds={elapsed:0.###}, score={completionScoreCache}, scoreGenerated=false",
                this);
        }

        // 2026-09-11: factored out so the completionRequested poll in Update() can re-show the
        // same summary after the student closes the 步骤明细 drill-down (see
        // completionDetailsShown) without duplicating the YardCraneHudCompletionPrompt data.
        private void ShowCompletionSummary()
        {
            hud.ShowCompletionPrompt(
                new YardCraneHudCompletionPrompt
                {
                    Title = "任务完成",
                    ElapsedSeconds = completionElapsedSecondsCache,
                    Score = completionScoreCache,
                    DetailsText = "步骤明细",
                    RetryText = "重新操作"
                },
                onDetails: ShowCompletionStepDetails,
                onRetry: null);
        }

        // 2026-09-11: "任务步骤明细" for whichever mode just completed. Called only after a
        // pass/achievement, so every line is necessarily already done - there is no partial-
        // progress state to represent here (unlike the live per-frame RenderAssessmentTask
        // progress rendering used during an in-progress assessment run).
        private void ShowCompletionStepDetails()
        {
            completionDetailsShown = true;
            var lines = context.Mode == YardCraneLocalTrainingMode.GuidedLearning
                ? GuidedLearningStepLines()
                : AssessmentStepLines();
            hud.ShowAssessmentPrompt(new YardCraneHudAssessmentPrompt
            {
                Title = "步骤明细",
                Project = context.DisplayInfo.Title,
                ElapsedSeconds = completionElapsedSecondsCache,
                Score = completionScoreCache,
                Lines = lines
            });
        }

        // One line per axis (primary + AdditionalTargets), each showing that axis' own target
        // description - a single-item drill has one line, a linked drill has 2-3.
        private System.Collections.Generic.List<YardCraneHudAssessmentLine> GuidedLearningStepLines()
        {
            var lines = new System.Collections.Generic.List<YardCraneHudAssessmentLine>
            {
                new YardCraneHudAssessmentLine(context.Target.DisplayText, 0, "已完成")
            };
            foreach (var extra in context.AdditionalTargets)
                lines.Add(new YardCraneHudAssessmentLine(extra.DisplayText, 0, "已完成"));
            return lines;
        }

        // 2026-09-11: the same five stage descriptions YardCraneLocalAssessmentTracker.
        // CreateProgress already uses for the live "当前步骤" readout during the run (see
        // RenderAssessmentTask) - reused verbatim here rather than invented fresh, so the
        // step list matches what the student actually saw stage-by-stage while working. Those
        // strings are private to that tracker class (not exposed as a public list), so they are
        // duplicated here rather than pulling in a bigger refactor of an already-tested class
        // for this round.
        private static System.Collections.Generic.List<YardCraneHudAssessmentLine> AssessmentStepLines()
        {
            return new System.Collections.Generic.List<YardCraneHudAssessmentLine>
            {
                new YardCraneHudAssessmentLine("对位并锁定目标箱", 0, "已完成"),
                new YardCraneHudAssessmentLine("带箱起升至安全高度", 0, "已完成"),
                new YardCraneHudAssessmentLine("带箱完成大车或小车转运", 0, "已完成"),
                new YardCraneHudAssessmentLine("下降并确认目标箱落稳", 0, "已完成"),
                new YardCraneHudAssessmentLine("落稳后解锁完成交箱", 0, "已完成")
            };
        }

        private string CombinedTargetDisplayText()
        {
            if (context.AdditionalTargets.Length == 0) return context.Target.DisplayText;
            var text = new System.Text.StringBuilder(context.Target.DisplayText);
            foreach (var extra in context.AdditionalTargets) text.Append('、').Append(extra.DisplayText);
            return text.ToString();
        }

        private string CombinedTargetMetricIds()
        {
            if (context.AdditionalTargets.Length == 0) return context.Target.MetricId;
            var text = new System.Text.StringBuilder(context.Target.MetricId);
            foreach (var extra in context.AdditionalTargets) text.Append('+').Append(extra.MetricId);
            return text.ToString();
        }

        // Combined progress across every axis of a (possibly linked) guided-learning exercise.
        // Achieved only when every tracker is Achieved; visible progress is the least-advanced
        // axis (a bucket-brigade metaphor - the axis furthest from done gates the percentage).
        private static YardCraneLearningGoalProgress CombineLearningProgress(
            System.Collections.Generic.IReadOnlyList<YardCraneLearningGoalTracker> trackers)
        {
            if (trackers == null || trackers.Count == 0) return default;
            var minProgress = float.MaxValue;
            var minProgressValue = float.NaN;
            var allAchieved = true;
            var allActionObserved = true;
            var anyWaiting = false;
            foreach (var tracker in trackers)
            {
                var progress = tracker.Progress;
                if (progress.State == YardCraneLearningGoalState.WaitingForData) anyWaiting = true;
                if (progress.State != YardCraneLearningGoalState.Achieved) allAchieved = false;
                if (!progress.ObservedRequiredAction) allActionObserved = false;
                if (progress.Progress01 < minProgress)
                {
                    minProgress = progress.Progress01;
                    minProgressValue = progress.CurrentValue;
                }
            }
            var state = anyWaiting
                ? YardCraneLearningGoalState.WaitingForData
                : allAchieved
                    ? YardCraneLearningGoalState.Achieved
                    : YardCraneLearningGoalState.Active;
            return new YardCraneLearningGoalProgress(state, minProgress, minProgressValue, allActionObserved);
        }

        private void UpdateAssessment(YardCraneHudSnapshot sample, double elapsed)
        {
            var progress = assessmentTracker.Update(sample);
            if (progress.Stage != lastRenderedAssessmentStage)
            {
                lastRenderedAssessmentStage = progress.Stage;
                RenderAssessmentTask(progress, elapsed);
            }

            switch (progress.Stage)
            {
                case YardCraneLocalAssessmentStage.Passed:
                    CompleteAssessmentPass(elapsed);
                    break;
                case YardCraneLocalAssessmentStage.Failed:
                    CompleteAssessmentFailure(progress.FailureReason, elapsed);
                    break;
            }
        }

        private void RenderAssessmentTimer(double elapsed)
        {
            if (!context.Timing.DurationSeconds.HasValue || assessmentTracker == null) return;
            var remaining = Math.Max(
                0,
                (int)Math.Ceiling(context.Timing.DurationSeconds.Value - elapsed));
            if (remaining == lastRenderedRemaining) return;
            lastRenderedRemaining = remaining;
            RenderAssessmentTask(assessmentTracker.Progress, elapsed);
        }

        private void RenderAssessmentTask(YardCraneLocalAssessmentProgress progress, double elapsed)
        {
            var remaining = context.Timing.DurationSeconds.HasValue
                ? Math.Max(0, (int)Math.Ceiling(context.Timing.DurationSeconds.Value - elapsed))
                : 0;
            SetTaskPresentation(
                context.DisplayInfo.Title,
                "当前步骤：",
                $"{progress.Instruction} ({progress.CompletedSteps}/{progress.TotalSteps})",
                "剩余时间：",
                FormatDuration(remaining));
        }

        // 2026-09-11: brought onto the same task-brief -> countdown -> completion-summary flow
        // as GuidedLearning (see ShowCompletionSummary/ShowCompletionStepDetails above) instead
        // of the older BindLocalResult text banner - same TryFinishLifecycle-before-
        // ShowCompletionPrompt ordering constraint applies here too (see 7节 in the discussion
        // record for why). CompleteAssessmentFailure below is deliberately NOT changed: a
        // trophy-icon "任务完成" popup would misrepresent a failed run, and the user's original
        // request was specifically about the success flow.
        private void CompleteAssessmentPass(double elapsed)
        {
            if (evaluationFinished) return;
            evaluationFinished = true;
            finishedElapsed = elapsed;
            HoldTrainingInput();
            if (!TryFinishLifecycle(YardCraneSceneState.ExitAllowed, out var error))
            {
                Debug.LogError(
                    $"[YC_LOCAL_ASSESSMENT_LIFECYCLE_REJECTED] sessionId={context.SessionId:D}, {error}",
                    this);
                return;
            }
            completionElapsedSecondsCache = (int)Math.Round(elapsed);
            completionScoreCache = YardCraneTrainingSessionIdentity.CurrentScore;
            completionRequested = true;
            completionDetailsShown = false;
            ShowCompletionSummary();
            Debug.Log(
                $"[YC_LOCAL_ASSESSMENT_PASSED] sessionId={context.SessionId:D}, " +
                $"elapsedSeconds={elapsed:0.###}, score={completionScoreCache}, scoreGenerated=false, " +
                "authorityConfirmed=false",
                this);
        }

        private void CompleteAssessmentFailure(string reason, double elapsed)
        {
            if (evaluationFinished) return;
            evaluationFinished = true;
            finishedElapsed = elapsed;
            HoldTrainingInput();
            var detail = string.IsNullOrWhiteSpace(reason)
                ? "本地流程未满足完成条件。"
                : reason.Trim();
            hud.BindLocalResult(new YardCraneHudLocalResultPresentation(
                "本地考核未通过",
                $"{detail} 本地计时 {FormatDuration(elapsed)}。",
                LocalResultTrustText,
                HudTone.Danger));
            if (!bootstrap.StateMachine.TryTransition(YardCraneSceneState.Failed, out var error))
            {
                Debug.LogError(
                    $"[YC_LOCAL_ASSESSMENT_LIFECYCLE_REJECTED] sessionId={context.SessionId:D}, {error}",
                    this);
            }
            Debug.LogWarning(
                $"[YC_LOCAL_ASSESSMENT_FAILED] sessionId={context.SessionId:D}, " +
                $"elapsedSeconds={elapsed:0.###}, reason={detail}, scoreGenerated=false",
                this);
        }

        private bool TryFinishLifecycle(YardCraneSceneState expectedTerminal, out string error)
        {
            var state = bootstrap.StateMachine.Current;
            if (state != YardCraneSceneState.Running && state != YardCraneSceneState.Paused)
            {
                error = $"Local training cannot finalize from lifecycle state {state}.";
                return false;
            }
            if (!bootstrap.StateMachine.TryTransition(YardCraneSceneState.Finalizing, out error))
                return false;
            return bootstrap.StateMachine.TryTransition(expectedTerminal, out error);
        }

        private void RejectEvaluation(string error)
        {
            HoldTrainingInput();
            rejected = true;
            validationError = string.IsNullOrWhiteSpace(error)
                ? "本地训练目标校验未通过。"
                : error.Trim();
            SetTaskPresentation(
                context.DisplayInfo.Title,
                context.Mode == YardCraneLocalTrainingMode.GuidedLearning ? "学习目标：" : "考核任务：",
                context.Mode == YardCraneLocalTrainingMode.GuidedLearning
                    ? context.Target.DisplayText
                    : context.DisplayInfo.Instruction,
                "训练状态：",
                "不可开始 · 请返回重选");
            hud.ShowErrorPrompt(new YardCraneHudErrorPrompt
            {
                Title = context.Mode == YardCraneLocalTrainingMode.GuidedLearning
                    ? "学习目标不可用"
                    : "本地考核不可用",
                Message = validationError,
                // This acknowledgement only dismisses the modal; navigation remains the
                // scene-owned HUD back button. Do not label it like a navigation action.
                AcknowledgeText = "我知道了"
            });
            Debug.LogError(
                $"[YC_LOCAL_TRAINING_EVALUATION_REJECTED] sessionId={context.SessionId:D}, " +
                $"metric={context.Target.MetricId}, reason={validationError}",
                this);
        }

        private void EnsurePresentationBound()
        {
            if (presentationBound) return;
            switch (context.Mode)
            {
                case YardCraneLocalTrainingMode.FreePractice:
                    currentPresentation = new YardCraneHudTrainingTaskPresentation(
                        context.DisplayInfo.Title,
                        "训练方式：",
                        "自主操作",
                        "完成规则：",
                        "不自动完成");
                    break;
                case YardCraneLocalTrainingMode.GuidedLearning:
                    currentPresentation = context.Target.HasTarget
                        ? new YardCraneHudTrainingTaskPresentation(
                            context.DisplayInfo.Title,
                            "学习目标：",
                            context.Target.DisplayText,
                            "完成进度：",
                            "等待场景就绪")
                        : new YardCraneHudTrainingTaskPresentation(
                            context.DisplayInfo.Title,
                            "学习内容：",
                            context.DisplayInfo.Instruction,
                            "完成规则：",
                            "按任务要求完成故障练习");
                    break;
                case YardCraneLocalTrainingMode.Assessment:
                    currentPresentation = new YardCraneHudTrainingTaskPresentation(
                        context.DisplayInfo.Title,
                        "当前步骤：",
                        context.DisplayInfo.Instruction,
                        "剩余时间：",
                        FormatDuration(context.Timing.DurationSeconds.GetValueOrDefault()));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            hud.BindLocalTrainingPresentation(CurrentModeLabel(), currentPresentation);
            presentationBound = true;
        }

        private void ReassertModeBinding()
        {
            var expected = CurrentModeLabel();
            if (!string.Equals(hud.SessionModeLabel, expected, StringComparison.Ordinal))
                hud.BindLocalTrainingPresentation(expected, currentPresentation);
        }

        private void SetTaskPresentation(
            string title,
            string primaryLabel,
            string primaryValue,
            string secondaryLabel,
            string secondaryValue)
        {
            currentPresentation = new YardCraneHudTrainingTaskPresentation(
                title,
                primaryLabel,
                primaryValue,
                secondaryLabel,
                secondaryValue);
            if (presentationBound) hud.UpdateLocalTrainingTask(currentPresentation);
        }

        private void CaptureSceneReferenceFrames()
        {
            var scene = gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded) return;

            Transform trolley = null;
            Transform spreaderBody = null;
            Transform spreaderPivot = null;
            var trolleyCount = 0;
            var bodyCount = 0;
            var pivotCount = 0;
            foreach (var sceneRoot in scene.GetRootGameObjects())
            foreach (var transformInScene in sceneRoot.GetComponentsInChildren<Transform>(true))
            {
                switch (transformInScene.name)
                {
                    case TrolleyTransformName:
                        trolley = transformInScene;
                        trolleyCount++;
                        break;
                    case SpreaderBodyTransformName:
                        spreaderBody = transformInScene;
                        bodyCount++;
                        break;
                    case SpreaderPivotTransformName:
                        spreaderPivot = transformInScene;
                        pivotCount++;
                        break;
                }
            }

            hasTrolleyFrame = trolleyCount == 1;
            if (hasTrolleyFrame)
            {
                trolleyHomeLocalX = trolley.localPosition.x;
                trolleyWorldY = trolley.position.y;
            }
            hasSpreaderFrame = hasTrolleyFrame && bodyCount == 1 && pivotCount == 1;
            if (hasSpreaderFrame)
                spreaderPivotOffsetY = spreaderPivot.position.y - spreaderBody.position.y;
        }

        private bool HasFreshReadySample()
        {
            return hud.Feed.HasSample && !hud.Feed.IsStale &&
                   hud.Feed.Latest.Status.Has(YardCraneHudSignals.Ready);
        }

        private bool OwnsCurrentSession()
        {
            var active = YardCraneLocalTrainingSession.CurrentContext;
            return active != null && active.SessionId == context.SessionId;
        }

        private bool TrySetTrainingInputEnabled(bool enabledValue, out string error)
        {
            if (inputHost == null)
            {
                inputHost = UnityEngine.Object.FindFirstObjectByType<YardCraneStandaloneOperationHost>(
                    FindObjectsInactive.Include);
            }
            if (inputHost == null)
            {
                error = "本地业务输入门控未就绪，不能安全开始训练。";
                return false;
            }

            try
            {
                inputHost.SetTrainingInputEnabled(enabledValue);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = "本地业务输入门控操作失败：" + exception.Message;
                return false;
            }
        }

        private void HoldTrainingInput()
        {
            if (inputHost == null) return;
            try { inputHost.SetTrainingInputEnabled(false); }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private double GetElapsedSeconds(double now)
        {
            if (!double.IsNaN(finishedElapsed)) return finishedElapsed;
            if (!trackingStarted || double.IsNaN(trackingStartedAt) || !IsFinite(now)) return 0d;
            return Math.Max(0d, now - trackingStartedAt);
        }

        private static bool TryReadMetric(
            YardCraneHudSnapshot sample,
            YardCraneLocalTrainingMetric metric,
            out double value)
        {
            switch (metric)
            {
                case YardCraneLocalTrainingMetric.GantryPosition:
                    value = sample.Telemetry.GantryPosition;
                    break;
                case YardCraneLocalTrainingMetric.TrolleyPosition:
                    value = sample.Telemetry.TrolleyPosition;
                    break;
                case YardCraneLocalTrainingMetric.SpreaderHeight:
                    value = sample.Telemetry.SpreaderHeight;
                    break;
                case YardCraneLocalTrainingMetric.SpreaderRotation:
                case YardCraneLocalTrainingMetric.SpreaderZeroing:
                    value = sample.Telemetry.PhysicalRotation;
                    break;
                default:
                    value = double.NaN;
                    return false;
            }
            return IsFinite(value);
        }

        private static bool TryMapLearningMetric(
            YardCraneLocalTrainingMetric source,
            out YardCraneLearningMetric destination)
        {
            switch (source)
            {
                case YardCraneLocalTrainingMetric.GantryPosition:
                    destination = YardCraneLearningMetric.GantryPosition;
                    return true;
                case YardCraneLocalTrainingMetric.TrolleyPosition:
                    destination = YardCraneLearningMetric.TrolleyPosition;
                    return true;
                case YardCraneLocalTrainingMetric.SpreaderHeight:
                    destination = YardCraneLearningMetric.SpreaderHeight;
                    return true;
                case YardCraneLocalTrainingMetric.SpreaderRotation:
                    destination = YardCraneLearningMetric.SpreaderRotation;
                    return true;
                case YardCraneLocalTrainingMetric.SpreaderZeroing:
                    destination = YardCraneLearningMetric.SpreaderZeroing;
                    return true;
                default:
                    destination = default;
                    return false;
            }
        }

        private static string ModeLabel(YardCraneLocalTrainingMode mode)
        {
            return mode switch
            {
                YardCraneLocalTrainingMode.FreePractice => "自由练习",
                YardCraneLocalTrainingMode.GuidedLearning => "引导学习",
                YardCraneLocalTrainingMode.Assessment => "本地考核",
                _ => "本地训练"
            };
        }

        private string CurrentModeLabel()
        {
            return context.Mode == YardCraneLocalTrainingMode.GuidedLearning &&
                   !context.Target.HasTarget
                ? "故障训练"
                : ModeLabel(context.Mode);
        }

        private static string FormatDuration(double totalSeconds)
        {
            var safeSeconds = Math.Max(0, Math.Min(int.MaxValue, (int)Math.Ceiling(totalSeconds)));
            var duration = TimeSpan.FromSeconds(safeSeconds);
            return duration.TotalHours >= 1d
                ? duration.ToString(@"h\:mm\:ss")
                : duration.ToString(@"m\:ss");
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private void OnDisable()
        {
            HoldTrainingInput();
        }

        private void OnDestroy()
        {
            HoldTrainingInput();
        }
    }
}
