using System;
using System.Collections.Generic;
using PortTraining.YardCraneSimulator.Bootstrap;
using UnityEngine;
using UnityEngine.UIElements;

namespace PortTraining.YardCraneSimulator.Presentation
{
    [Flags]
    public enum YardCraneHudNavigationCapabilities
    {
        None = 0,
        Back = 1,
        Exit = 2
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(12000)]
    public sealed partial class YardCraneOperatorHud : MonoBehaviour
    {
        public const string Version = "2026-09-14-yc-driving-hud-v17-axis-brake-status";
        private static readonly string[] LampIds =
        {
            "lamp-touchdown-dot", "lamp-anchor-dot", "lamp-unlock-dot", "lamp-single-dot", "lamp-double-dot",
            "lock-pin-on-dot", "lock-pin-off-dot"
        };

        private enum HudVectorIcon
        {
            Clock,
            Task,
            Target,
            Location,
            Lock,
            Info,
            Warning,
            Trophy,
            Submission,
            Crane
        }

        private readonly YardCraneHudFeed feed = new();
        private readonly Dictionary<string, Label> labels = new();
        private readonly Dictionary<string, VisualElement> lamps = new();
        private readonly Dictionary<VisualElement, Action<MeshGenerationContext>> vectorIconDrawers = new();
        private IYardCraneHudSource source;
        private UIDocument document;
        private PanelSettings settings;
        private Font font;
        private VisualElement root;
        private VisualElement resultCard;
        private Label alertStrip;
        private Label lockPinOnLabel;
        private Label lockPinOffLabel;
        private Button guideButton;
        private Button backButton;
        private Button exitButton;
        private Label navigationNote;
        private UnityEngine.Object navigationOwner;
        private Action navigateBack;
        private Action navigateExit;
        private string backUnavailableReason = "导航执行方尚未就绪";
        private string exitUnavailableReason = "导航执行方尚未就绪";
        private YardCraneSceneStateMachine lifecycle;
        private YardCraneSceneState lifecycleState = YardCraneSceneState.Booting;
        private bool guideVisible;
        private bool built;
        private bool sourceReadFailed;
        private string sessionModeLabel = "场桥训练";
        private int lastWidth, lastHeight;
        private double sessionStartRealtime = -1;
        private int lastElapsedWholeSeconds = int.MinValue;
        public bool IsReady => built && root != null && document != null && document.enabled && isActiveAndEnabled;
        public VisualElement VisualRoot => root;
        public YardCraneHudFeed Feed => feed;
        public YardCraneSceneState LifecycleState => lifecycleState;
        public bool IsResultVisible => resultCard != null &&
                                       resultCard.resolvedStyle.display == DisplayStyle.Flex;

        public YardCraneHudNavigationCapabilities NavigationCapabilities =>
            (navigateBack != null ? YardCraneHudNavigationCapabilities.Back : YardCraneHudNavigationCapabilities.None) |
            (navigateExit != null ? YardCraneHudNavigationCapabilities.Exit : YardCraneHudNavigationCapabilities.None);
        public bool BackNavigationAvailable => navigateBack != null;
        public bool ExitNavigationAvailable => navigateExit != null;

        /// <summary>
        /// Starts the visible HUD elapsed clock at the exact instant a training mode starts
        /// accepting business input. The returned monotonic timestamp lets the mode
        /// coordinator use the identical origin for its own elapsed/remaining calculations.
        /// </summary>
        public double ResetElapsedClockForTraining()
        {
            var now = Time.realtimeSinceStartupAsDouble;
            sessionStartRealtime = now;
            lastElapsedWholeSeconds = int.MinValue;
            if (IsReady) RenderElapsed(now);
            return now;
        }

        public void Bind(IYardCraneHudSource stateSource)
        {
            var nextSource = stateSource ?? throw new ArgumentNullException(nameof(stateSource));
            if (!ReferenceEquals(source, nextSource)) ClosePrompt();
            source = nextSource;
            feed.Invalidate();
            sessionStartRealtime = -1;
            lastElapsedWholeSeconds = int.MinValue;
        }

        public void BindLifecycle(YardCraneSceneStateMachine stateMachine)
        {
            if (ReferenceEquals(lifecycle, stateMachine))
            {
                RenderLifecycle();
                return;
            }

            if (lifecycle != null) lifecycle.Changed -= OnLifecycleChanged;
            lifecycle = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
            lifecycleState = lifecycle.Current;
            if (isActiveAndEnabled) lifecycle.Changed += OnLifecycleChanged;
            // A newly-bound state machine belongs to a new run. Never retain an old run's
            // modal or deferred callback across that authority boundary.
            if (ActivePromptKind != YardCraneHudPromptKind.None || HasDeferredPrompt)
                ClosePrompt();
            else
                RenderLifecycle();
        }

        public void BindSessionMode(YardCraneLaunchMode launchMode)
        {
            sessionModeLabel = launchMode switch
            {
                YardCraneLaunchMode.Managed => "任务训练",
                YardCraneLaunchMode.Standalone => "单机训练",
                YardCraneLaunchMode.EditorDevelopment => "本机调试",
                _ => "场桥训练"
            };
            switch (launchMode)
            {
                case YardCraneLaunchMode.Managed:
                    SetNavigationFallbackReasons("平台返回尚未接入", "平台退出尚未接入");
                    break;
                case YardCraneLaunchMode.EditorDevelopment:
                    SetNavigationFallbackReasons("直接调试启动，无学生首页", "请在 Unity 中停止 Play");
                    break;
                default:
                    SetNavigationFallbackReasons("直接启动，无学生首页", "退出流程尚未接入");
                    break;
            }
            RenderStatus();
        }

        public void BindLocalFreePractice()
        {
            sessionModeLabel = "自由练习";
            RenderStatus();
        }

        private void OnEnable()
        {
            built = false;
            var tree = Resources.Load<VisualTreeAsset>("YardCraneUI/OperatorHud");
            var stylesheet = Resources.Load<StyleSheet>("YardCraneUI/OperatorHudStyles");
            var panelTemplate = Resources.Load<PanelSettings>("YardCraneUI/OperatorHudPanel");
            if (tree == null || stylesheet == null || panelTemplate == null)
            {
                Debug.LogError("[YC_UI_HUD_MISSING_ASSET] Legacy diagnostics remain available.", this);
                return;
            }

            settings = Instantiate(panelTemplate);
            settings.name = "YC Operator HUD Runtime Panel";
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.sortingOrder = 20;
            document = gameObject.GetComponent<UIDocument>();
            if (document == null)
            {
                Debug.LogError(
                    "[YC_UI_HUD_MISSING_UIDOCUMENT] The operational scene must own one UIDocumentDisplay1; " +
                    "runtime UI creation is disabled to prevent duplicate displays.", this);
                Destroy(settings);
                settings = null;
                return;
            }
            document.panelSettings = settings;
            document.visualTreeAsset = tree;
            document.enabled = true;
            root = document.rootVisualElement;
            root.styleSheets.Add(stylesheet);
            root.style.flexGrow = 1;
            root.Query<VisualElement>().ForEach(element =>
            {
                element.pickingMode = PickingMode.Ignore;
                element.focusable = false;
                if (element is Label label) { label.enableRichText = false; labels[label.name] = label; }
            });
            root.pickingMode = PickingMode.Ignore;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Noto Sans CJK SC", "Arial Unicode MS" }, 24);
            root.style.unityFontDefinition = FontDefinition.FromFont(font);

            lamps.Clear();
            var missingNodes = new List<string>();
            foreach (var id in LampIds)
            {
                var dot = root.Q(id);
                if (dot != null) lamps[id] = dot;
                else missingNodes.Add(id);
            }

            ConfigureVectorIcon("timer-icon", HudVectorIcon.Clock, missingNodes);
            ConfigureVectorIcon("task-crane-icon", HudVectorIcon.Crane, missingNodes);
            ConfigureVectorIcon("current-task-icon", HudVectorIcon.Task, missingNodes);
            ConfigureVectorIcon("target-container-icon", HudVectorIcon.Target, missingNodes);
            ConfigureVectorIcon("target-slot-icon", HudVectorIcon.Location, missingNodes);
            QueryPromptElements(missingNodes);

            guideButton = root.Q<Button>("guide-toggle");
            resultCard = root.Q("result-card");
            alertStrip = root.Q<Label>("alert-strip");
            lockPinOnLabel = root.Q<Label>("lock-pin-on-label");
            lockPinOffLabel = root.Q<Label>("lock-pin-off-label");
            backButton = root.Q<Button>("back-button");
            exitButton = root.Q<Button>("exit-button");
            navigationNote = root.Q<Label>("navigation-note");
            if (root.Q("operator-hud") == null || root.Q("left-telemetry") == null ||
                root.Q("guide-card") == null || root.Q("guide-lines") == null ||
                guideButton == null || resultCard == null || alertStrip == null ||
                lockPinOnLabel == null || lockPinOffLabel == null || backButton == null || exitButton == null ||
                navigationNote == null)
                missingNodes.Add("required-layout-node");
            if (missingNodes.Count > 0)
            {
                Debug.LogError($"[YC_UI_HUD_INVALID_TREE] Missing: {string.Join(", ", missingNodes)}. " +
                               "Load the matching OperatorHud.uxml and OperatorHudStyles.uss pair.", this);
                document.enabled = false;
                root = null;
                labels.Clear();
                lamps.Clear();
                ReleaseVectorIcons();
                if (settings != null) Destroy(settings);
                if (font != null) Destroy(font);
                settings = null;
                font = null;
                return;
            }
            guideButton.pickingMode = PickingMode.Position;
            guideButton.focusable = false; // Never consume a driving key or Xbox navigation focus.
            guideButton.tabIndex = -1;
            guideButton.clicked += ToggleGuide;
            foreach (var button in new[] { backButton, exitButton })
            {
                button.pickingMode = PickingMode.Position;
                button.focusable = false;
                button.tabIndex = -1;
            }
            backButton.clicked += HandleBackClicked;
            exitButton.clicked += HandleExitClicked;
            BindPromptInteractions();
            ApplyNavigationAvailability();
            PopulateGuide(false);
            SetGuideVisible(false);
            HideAllPromptViews();
            feed.StatusChanged += RenderStatus;
            feed.TelemetryChanged += RenderTelemetry;
            if (lifecycle != null)
            {
                lifecycle.Changed -= OnLifecycleChanged;
                lifecycle.Changed += OnLifecycleChanged;
                lifecycleState = lifecycle.Current;
            }
            RenderStatus();
            RenderTelemetry();
            RenderTrainingTaskPresentation();
            RenderLifecycle();
            UpdateScale();
            built = true;
            FlushPendingPrompt();
            Debug.Log($"[YC_UI_HUD_READY] version={Version}, readOnly=True, numericHz=10, source=scene-adapter", this);
        }

        private void HandleBackClicked()
        {
            EnsureNavigationOwnerAlive();
            var action = navigateBack;
            if (action == null)
            {
                Debug.LogWarning("[YC_UI_HUD_NAV_BACK_UNAVAILABLE] No navigation owner is bound.", this);
                return;
            }
            Debug.Log("[YC_UI_HUD_NAV_BACK_REQUESTED] No scene/process action taken here.", this);
            action();
        }

        // 2026-09-11: identical behavior to the back button's own click handler above (owner
        // liveness check, the same log lines, the same navigateBack delegate) - just reachable
        // from a caller that is not a UXML button, e.g. the local-training coordinator invoking
        // it when the student closes the task-completion popup.
        public void RequestBackNavigation() => HandleBackClicked();

        private void HandleExitClicked()
        {
            EnsureNavigationOwnerAlive();
            var action = navigateExit;
            if (action == null)
            {
                Debug.LogWarning("[YC_UI_HUD_NAV_EXIT_UNAVAILABLE] No navigation owner is bound.", this);
                return;
            }
            Debug.Log("[YC_UI_HUD_NAV_EXIT_REQUESTED] No scene/process action taken here.", this);
            action();
        }

        public void BindNavigationExecutor(
            UnityEngine.Object owner,
            Action back,
            Action exit,
            string unavailableBack = null,
            string unavailableExit = null)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            navigationOwner = owner;
            navigateBack = back;
            navigateExit = exit;
            if (!string.IsNullOrWhiteSpace(unavailableBack)) backUnavailableReason = unavailableBack;
            if (!string.IsNullOrWhiteSpace(unavailableExit)) exitUnavailableReason = unavailableExit;
            ApplyNavigationAvailability();
        }

        public void UnbindNavigationExecutor(UnityEngine.Object owner)
        {
            if (!ReferenceEquals(navigationOwner, owner)) return;
            ClearNavigationExecutor();
            ApplyNavigationAvailability();
        }

        private void SetNavigationFallbackReasons(string backReason, string exitReason)
        {
            backUnavailableReason = backReason;
            exitUnavailableReason = exitReason;
            ApplyNavigationAvailability();
        }

        private void ApplyNavigationAvailability()
        {
            EnsureNavigationOwnerAlive(false);
            if (backButton == null || exitButton == null || navigationNote == null) return;

            var backAvailable = navigateBack != null;
            var exitAvailable = navigateExit != null;

            backButton.SetEnabled(backAvailable);
            exitButton.SetEnabled(exitAvailable);
            backButton.tooltip = backAvailable ? "返回学生首页" : backUnavailableReason;
            exitButton.tooltip = exitAvailable ? "退出训练" : exitUnavailableReason;
            var unavailable = !backAvailable || !exitAvailable;
            navigationNote.text = !backAvailable && !exitAvailable
                ? (backUnavailableReason == exitUnavailableReason
                    ? backUnavailableReason
                    : $"返回：{backUnavailableReason} · 退出：{exitUnavailableReason}")
                : !backAvailable
                    ? $"返回：{backUnavailableReason}"
                    : $"退出：{exitUnavailableReason}";
            navigationNote.style.display = unavailable ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void EnsureNavigationOwnerAlive(bool updateVisual = true)
        {
            if (ReferenceEquals(navigationOwner, null) || navigationOwner != null) return;
            ClearNavigationExecutor();
            if (updateVisual) ApplyNavigationAvailability();
        }

        private void ClearNavigationExecutor()
        {
            navigationOwner = null;
            navigateBack = null;
            navigateExit = null;
        }

        private void LateUpdate()
        {
            if (!IsReady) return;
            UpdateScale();
            try
            {
                if (source != null && source.TryRead(out var snapshot)) feed.Publish(snapshot, Time.realtimeSinceStartupAsDouble);
                else feed.Invalidate();
                sourceReadFailed = false;
            }
            catch (Exception)
            {
                feed.Invalidate();
                if (!sourceReadFailed) Debug.LogWarning("[YC_UI_SOURCE_UNAVAILABLE] State read failed; HUD cleared. Control authority is unchanged.", this);
                sourceReadFailed = true;
            }
            feed.Tick(Time.realtimeSinceStartupAsDouble);
            RenderElapsed(Time.realtimeSinceStartupAsDouble);
            UpdatePromptCountdown(Time.realtimeSinceStartupAsDouble);
        }

        private void UpdateScale()
        {
            var target = settings.targetTexture;
            var width = target != null ? target.width : Screen.width;
            var height = target != null ? target.height : Screen.height;
            if (lastWidth == width && lastHeight == height) return;
            lastWidth = width;
            lastHeight = height;
            var profile = YardCraneDisplayProfile.Detect(width, height);
            settings.scale = profile.CalculateScale(width, height);
            var hudRoot = root.Q("operator-hud");
            hudRoot.EnableInClassList("portrait", profile.IsPortrait);
            hudRoot.EnableInClassList("dual-stacked", profile.IsDualStacked);
        }

        // Wall-clock "用时" starts at the mode coordinator's real input-enabled instant. Direct
        // scene boots without a training coordinator retain the fallback behaviour of starting
        // on the first healthy sample. Frozen as "--:--" while stale so a disconnect never keeps
        // presenting a healthy timer.
        private void RenderElapsed(double now)
        {
            if (feed.IsStale)
            {
                if (lastElapsedWholeSeconds != -1) { Set("elapsed", new HudReadout("--:--")); lastElapsedWholeSeconds = -1; }
                return;
            }
            if (sessionStartRealtime < 0) sessionStartRealtime = now;
            var whole = (int)Math.Floor(now - sessionStartRealtime);
            if (whole == lastElapsedWholeSeconds) return;
            lastElapsedWholeSeconds = whole;
            Set("elapsed", new HudReadout(YardCraneHudText.Elapsed(whole)));
        }

        private void RenderStatus()
        {
            if (root == null) return;
            var s = feed.Latest.Status;
            var stale = feed.IsStale;
            var lifecycleOwnsBanner = lifecycle != null && lifecycleState != YardCraneSceneState.Running;

            // 任务栏始终显示"当前正在做什么"，绝不被安全告警顶掉；告警改到独立的窄提示条。
            if (lifecycleOwnsBanner) Set("stage", LifecycleBanner(lifecycleState));
            else if (stale) Set("stage", new HudReadout("等待数据", HudTone.Caution));
            else
            {
                var operation = YardCraneHudText.Operation(s);
                Set("stage", new HudReadout($"{sessionModeLabel} · {operation.Text}", operation.Tone));
            }

            var alert = lifecycleOwnsBanner
                ? null
                : YardCraneHudText.Alert(s, stale);
            if (alertStrip != null)
            {
                alertStrip.style.display = alert.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
                if (alert.HasValue) Set("alert-strip", alert.Value);
            }

            // Five status lamps. Unknown collapses to off, same as staleness - never show a fake "on".
            var ready = !stale && s.Has(YardCraneHudSignals.Ready);
            SetLamp("lamp-touchdown-dot", ready && s.Has(YardCraneHudSignals.SpreaderContact));
            var lockTransition = s.Has(YardCraneHudSignals.Locking) || s.Has(YardCraneHudSignals.Unlocking);
            var locked = ready && !lockTransition && s.Has(YardCraneHudSignals.Locked);
            var unlocked = ready && !lockTransition && !s.Has(YardCraneHudSignals.Locked);
            SetLamp("lamp-anchor-dot", locked);
            SetLamp("lamp-unlock-dot", unlocked);
            SetLamp("lamp-single-dot", ready && s.LoadMode == YardCraneHudLoadMode.Single);
            SetLamp("lamp-double-dot", ready && s.LoadMode == YardCraneHudLoadMode.Twin);

            // 制动状态：大车与小车分别读取各自驱动的制动状态，互不排斥。
            // 未就绪、未知或断流时对应指示灯灭，不保留上一帧状态。
            var gantryBrakeApplied = ready && s.Has(YardCraneHudSignals.BrakeKnown) &&
                                     s.Has(YardCraneHudSignals.BrakeApplied);
            var trolleyBrakeApplied = ready && s.Has(YardCraneHudSignals.TrolleyBrakeKnown) &&
                                      s.Has(YardCraneHudSignals.TrolleyBrakeApplied);
            SetLamp("lock-pin-on-dot", gantryBrakeApplied);
            SetLamp("lock-pin-off-dot", trolleyBrakeApplied);
            lockPinOnLabel?.EnableInClassList("active", gantryBrakeApplied);
            lockPinOffLabel?.EnableInClassList("active", trolleyBrakeApplied);

            root.EnableInClassList("data-stale", stale);
            PopulateGuide(s.InputSource == "XBOX_GAMEPAD");
        }

        private void OnLifecycleChanged(YardCraneSceneState previous, YardCraneSceneState next)
        {
            lifecycleState = next;
            if (LifecycleResultOwnsHud(next) &&
                (ActivePromptKind != YardCraneHudPromptKind.None || HasDeferredPrompt))
            {
                // Terminal/finalizing truth must not be hidden by a stale task modal. A
                // coordinator may deliberately show the new run's completion prompt after
                // this transition, but anything that predates the transition is invalid.
                ClosePrompt();
                return;
            }
            RenderLifecycle();
            RenderStatus();
        }

        private static bool LifecycleResultOwnsHud(YardCraneSceneState state)
        {
            return state == YardCraneSceneState.Finalizing ||
                   state == YardCraneSceneState.ExitAllowed ||
                   state == YardCraneSceneState.Terminated ||
                   state == YardCraneSceneState.Failed;
        }

        private void RenderLifecycle()
        {
            if (root == null || resultCard == null) return;
            var visible = ActivePromptKind == YardCraneHudPromptKind.None &&
                          LifecycleResultOwnsHud(lifecycleState);
            resultCard.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) return;

            if (TryRenderLocalResult(lifecycleState)) return;

            switch (lifecycleState)
            {
                case YardCraneSceneState.Finalizing:
                    Set("result-title", new HudReadout("正在生成训练结果", HudTone.Caution));
                    Set("result-detail", new HudReadout("正在收口运行状态，请保持程序开启。"));
                    break;
                case YardCraneSceneState.ExitAllowed:
                    Set("result-title", new HudReadout("本次训练已完成", HudTone.Good));
                    Set("result-detail", new HudReadout("运行流程已结束，现在可以安全退出。"));
                    break;
                case YardCraneSceneState.Terminated:
                    Set("result-title", new HudReadout("本次训练已终止", HudTone.Caution));
                    Set("result-detail", new HudReadout("运行已停止，未生成完成成绩。"));
                    break;
                default:
                    Set("result-title", new HudReadout("启动或运行失败", HudTone.Danger));
                    Set("result-detail", new HudReadout("请检查启动参数、场景配置与运行日志。"));
                    break;
            }

            Set("result-trust", new HudReadout("本地状态摘要 · 未接入 NodeService 评分 · 不显示虚假分数"));
        }

        private static HudReadout LifecycleBanner(YardCraneSceneState state)
        {
            return state switch
            {
                YardCraneSceneState.Booting => new HudReadout("系统启动中"),
                YardCraneSceneState.WaitingForCoordinator => new HudReadout("等待作业协调"),
                YardCraneSceneState.Loading => new HudReadout("正在加载驾驶场景"),
                YardCraneSceneState.Ready => new HudReadout("驾驶场景已就绪", HudTone.Good),
                YardCraneSceneState.Paused => new HudReadout("训练已暂停", HudTone.Caution),
                YardCraneSceneState.Finalizing => new HudReadout("正在生成训练结果", HudTone.Caution),
                YardCraneSceneState.ExitAllowed => new HudReadout("训练已完成", HudTone.Good),
                YardCraneSceneState.Terminated => new HudReadout("训练已终止", HudTone.Caution),
                YardCraneSceneState.Failed => new HudReadout("启动或运行失败", HudTone.Danger),
                _ => new HudReadout("等待设备数据")
            };
        }

        private void RenderTelemetry()
        {
            var t = feed.Latest.Telemetry;
            var valid = !feed.IsStale;
            Numeric("gantry-position", valid ? t.GantryPosition : float.NaN);
            Numeric("gantry-speed", valid ? t.GantrySpeed : float.NaN);
            Numeric("trolley-position", valid ? t.TrolleyPosition : float.NaN);
            Numeric("trolley-speed", valid ? t.TrolleySpeed : float.NaN);
            Numeric("spreader-height", valid ? t.SpreaderHeight : float.NaN);
            Numeric("spreader-speed", valid ? t.SpreaderSpeed : float.NaN);
            Numeric("rotation", valid ? t.PhysicalRotation : float.NaN, "+0.0;-0.0;0.0");
            // trim-left-right / trim-front-back (吊具左右/前后倾转) are intentionally left untouched
            // here: the physics model only solves a single horizontal rotation angle (bound to
            // "rotation" above), there is no independent two-axis trim data source. They keep the
            // UXML's static "—" forever rather than being wired to a value that would never change -
            // see the delivery report's field-gap list.
        }

        private void Numeric(string id, float value, string format = "F2") => Set(id, new HudReadout(YardCraneHudText.Number(value, format)));

        private void SetLamp(string id, bool on)
        {
            if (!lamps.TryGetValue(id, out var dot)) return;
            dot.EnableInClassList("lamp-on", on);
        }

        private void ConfigureVectorIcon(
            string id,
            HudVectorIcon icon,
            ICollection<string> missingNodes)
        {
            var element = root.Q(id);
            if (element == null)
            {
                missingNodes.Add(id);
                return;
            }

            Action<MeshGenerationContext> drawer = context => DrawVectorIcon(context, icon);
            element.generateVisualContent += drawer;
            vectorIconDrawers[element] = drawer;
            element.MarkDirtyRepaint();
        }

        private static void DrawVectorIcon(MeshGenerationContext context, HudVectorIcon icon)
        {
            var rect = context.visualElement.contentRect;
            var size = Mathf.Min(rect.width, rect.height);
            if (size < 2) return;

            var scale = size / 30f;
            var origin = rect.center - Vector2.one * (size * 0.5f);
            Vector2 Point(float x, float y) => origin + new Vector2(x * scale, y * scale);

            var painter = context.painter2D;
            painter.strokeColor = context.visualElement.ClassListContains("prompt-header-icon-danger")
                ? new Color32(255, 91, 116, 255)
                : context.visualElement.ClassListContains("prompt-header-icon")
                    ? new Color32(111, 226, 255, 255)
                    : new Color32(104, 229, 255, 255);
            painter.lineWidth = Mathf.Max(1.5f, 2.1f * scale);
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;

            void StrokeCircle(Vector2 center, float radius)
            {
                painter.BeginPath();
                painter.Arc(center, radius * scale, Angle.Degrees(0), Angle.Degrees(360), ArcDirection.Clockwise);
                painter.Stroke();
            }

            void StrokePolyline(params Vector2[] points)
            {
                painter.BeginPath();
                painter.MoveTo(points[0]);
                for (var index = 1; index < points.Length; index++) painter.LineTo(points[index]);
                painter.Stroke();
            }

            switch (icon)
            {
                case HudVectorIcon.Clock:
                    StrokeCircle(Point(15, 15), 11);
                    StrokePolyline(Point(15, 8), Point(15, 15), Point(20, 18));
                    break;

                case HudVectorIcon.Task:
                    StrokePolyline(Point(7, 4), Point(23, 4), Point(23, 26), Point(7, 26), Point(7, 4));
                    StrokePolyline(Point(11, 10), Point(19, 10));
                    StrokePolyline(Point(11, 15), Point(19, 15));
                    StrokePolyline(Point(11, 20), Point(17, 20));
                    break;

                case HudVectorIcon.Target:
                    StrokeCircle(Point(15, 15), 8);
                    StrokeCircle(Point(15, 15), 2.5f);
                    StrokePolyline(Point(15, 3), Point(15, 8));
                    StrokePolyline(Point(15, 22), Point(15, 27));
                    StrokePolyline(Point(3, 15), Point(8, 15));
                    StrokePolyline(Point(22, 15), Point(27, 15));
                    break;

                case HudVectorIcon.Location:
                    painter.BeginPath();
                    painter.MoveTo(Point(15, 27));
                    painter.BezierCurveTo(Point(12, 23), Point(7, 18), Point(7, 12));
                    painter.BezierCurveTo(Point(7, 7.5f), Point(10.5f, 4), Point(15, 4));
                    painter.BezierCurveTo(Point(19.5f, 4), Point(23, 7.5f), Point(23, 12));
                    painter.BezierCurveTo(Point(23, 18), Point(18, 23), Point(15, 27));
                    painter.Stroke();
                    StrokeCircle(Point(15, 12), 2.8f);
                    break;

                case HudVectorIcon.Lock:
                    painter.BeginPath();
                    painter.MoveTo(Point(9, 13));
                    painter.LineTo(Point(9, 10));
                    painter.BezierCurveTo(Point(9, 5.8f), Point(11.5f, 4), Point(15, 4));
                    painter.BezierCurveTo(Point(18.5f, 4), Point(21, 5.8f), Point(21, 10));
                    painter.LineTo(Point(21, 13));
                    painter.Stroke();
                    StrokePolyline(Point(6, 13), Point(24, 13), Point(24, 26), Point(6, 26), Point(6, 13));
                    StrokePolyline(Point(15, 18), Point(15, 22));
                    break;

                case HudVectorIcon.Crane:
                    StrokePolyline(Point(4, 8), Point(26, 8));
                    StrokePolyline(Point(8, 8), Point(8, 25), Point(5, 25), Point(11, 25));
                    StrokePolyline(Point(22, 8), Point(22, 25), Point(19, 25), Point(25, 25));
                    StrokePolyline(Point(8, 8), Point(15, 3), Point(22, 8));
                    StrokePolyline(Point(11, 8), Point(11, 22));
                    StrokePolyline(Point(19, 8), Point(19, 22));
                    StrokePolyline(Point(15, 8), Point(15, 18), Point(13, 21), Point(17, 21));
                    break;

                case HudVectorIcon.Info:
                    StrokeCircle(Point(15, 15), 11);
                    StrokePolyline(Point(15, 13), Point(15, 22));
                    StrokeCircle(Point(15, 8.5f), 0.9f);
                    break;

                case HudVectorIcon.Warning:
                    StrokePolyline(Point(15, 3), Point(27, 25), Point(3, 25), Point(15, 3));
                    StrokePolyline(Point(15, 10), Point(15, 17));
                    StrokeCircle(Point(15, 21), 0.9f);
                    break;

                case HudVectorIcon.Trophy:
                    StrokePolyline(Point(9, 5), Point(21, 5), Point(21, 12));
                    painter.BeginPath();
                    painter.MoveTo(Point(9, 5));
                    painter.LineTo(Point(9, 12));
                    painter.BezierCurveTo(Point(9, 18), Point(21, 18), Point(21, 12));
                    painter.Stroke();
                    StrokePolyline(Point(15, 18), Point(15, 23), Point(9, 26), Point(21, 26));
                    StrokePolyline(Point(9, 8), Point(4, 8), Point(4, 12), Point(9, 14));
                    StrokePolyline(Point(21, 8), Point(26, 8), Point(26, 12), Point(21, 14));
                    break;

                case HudVectorIcon.Submission:
                    StrokePolyline(Point(3, 11), Point(15, 5), Point(27, 11), Point(15, 17), Point(3, 11));
                    StrokePolyline(Point(8, 14), Point(8, 21), Point(15, 25), Point(22, 21), Point(22, 14));
                    StrokePolyline(Point(27, 11), Point(27, 21));
                    break;
            }
        }

        private void ReleaseVectorIcons()
        {
            foreach (var pair in vectorIconDrawers)
                pair.Key.generateVisualContent -= pair.Value;
            vectorIconDrawers.Clear();
        }

        private void Set(string id, HudReadout value)
        {
            if (!labels.TryGetValue(id, out var label)) return;
            if (label.text != value.Text) label.text = value.Text;
            label.EnableInClassList("good", value.Tone == HudTone.Good);
            label.EnableInClassList("caution", value.Tone == HudTone.Caution);
            label.EnableInClassList("danger", value.Tone == HudTone.Danger);
        }

        public void ToggleGuide() => SetGuideVisible(!guideVisible);

        public void SetGuideVisible(bool visible)
        {
            guideVisible = visible;
            if (root == null) return;
            root.Q("left-telemetry").style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
            root.Q("guide-card").style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            // Small icon button tucked into the panel corner - full-text labels ("操作指引") don't
            // fit a 28px badge, so the icon itself swaps to indicate the toggle affordance.
            guideButton.text = visible ? "✕" : "i";
        }

        private void PopulateGuide(bool xbox)
        {
            // Mirrors the current fixed mappings in YardCraneInputSources. Remapping is not
            // implemented yet; when it is, this catalog must come from the input profile.
            var card = root.Q("guide-lines");
            var sourceName = xbox ? "xbox" : "keyboard";
            if (card.userData as string == sourceName) return;
            card.userData = sourceName;
            card.Clear();
            string[] operations = { "大车行走", "小车行走", "吊具升降", "锁箱 / 解锁", "吊具旋转", "吊具回零", "吊具泵", "20 / 40 尺", "夹轮器", "小车锚定", "大车锚定", "鸣笛", "控制电源", "急停", "故障复位", "暂停 / 继续" };
            string[] keys = xbox
                ? new[] { "左杆 X", "右杆 X", "左杆 Y", "X / B", "右杆 Y", "R3", "LT", "十字键 ← / →", "Y", "十字键 ↓", "RB", "LB", "十字键 ↑", "View", "A", "Menu" }
                : new[] { "J / L", "A / D", "W / S", "E / Q", "[ / ]", "0", "P", "1 / 2", "C", "X", "Z", "Space", "F8", "F9", "R", "Esc" };
            for (var i = 0; i < operations.Length; i++)
            {
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.AddToClassList("guide-row");
                row.Add(new Label(operations[i]) { pickingMode = PickingMode.Ignore });
                var key = new Label(keys[i]) { pickingMode = PickingMode.Ignore };
                key.AddToClassList("keycap");
                row.Add(key);
                card.Add(row);
            }
        }

        private void OnDisable()
        {
            built = false;
            feed.StatusChanged -= RenderStatus;
            feed.TelemetryChanged -= RenderTelemetry;
            if (lifecycle != null) lifecycle.Changed -= OnLifecycleChanged;
            feed.Invalidate();
            if (guideButton != null) guideButton.clicked -= ToggleGuide;
            if (backButton != null) backButton.clicked -= HandleBackClicked;
            if (exitButton != null) exitButton.clicked -= HandleExitClicked;
            ReleasePromptInteractions();
            if (document != null) document.enabled = false;
            root = null;
            alertStrip = null;
            lockPinOnLabel = null;
            lockPinOffLabel = null;
            navigationNote = null;
            ReleaseVectorIcons();
            labels.Clear();
            lamps.Clear();
            lastWidth = lastHeight = 0;
            sessionStartRealtime = -1;
            lastElapsedWholeSeconds = int.MinValue;
            if (settings != null) Destroy(settings);
            if (font != null) Destroy(font);
        }
    }
}
