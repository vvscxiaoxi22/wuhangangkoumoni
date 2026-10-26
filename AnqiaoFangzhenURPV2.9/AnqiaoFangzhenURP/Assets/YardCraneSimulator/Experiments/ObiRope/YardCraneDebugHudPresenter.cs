using PortTraining.YardCraneSimulator.Crane;
using PortTraining.YardCraneSimulator.Operations;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Experiments.RopePhysics
{
    /// <summary>
    /// Temporary IMGUI (<c>OnGUI</c>) test display for <see cref="YardCraneEightRopeMaxPrototypeRig"/>
    /// - fifth piece extracted out of that file, and the last of the low-risk "pure
    /// presentation" pieces (it only reads state and draws text; it never writes back to the
    /// Rig or drives any physics). This is explicitly a stopgap: the real operator-facing UI
    /// will be built in UI Toolkit later and this whole class will be retired then, so it is
    /// deliberately not over-engineered - it reads a wide slice of the Rig's fields directly
    /// (promoted from private to internal on the Rig rather than wrapped in a bundled snapshot
    /// type) instead of building lasting presentation-layer architecture around throwaway code.
    ///
    /// Added to the Rig's GameObject at runtime by <see cref="YardCraneEightRopeMaxPrototypeRig"/>
    /// (mirroring how it already adds <c>YardCraneStandaloneOperationHost</c>), so no scene or
    /// prefab edits are required for this extraction.
    /// </summary>
    internal sealed class YardCraneDebugHudPresenter : MonoBehaviour
    {
        private const string OperationGuideColorCyan = "58C7FF";

        // Bounds of the main runtime panel (Rect(24,24,610,355)), needed to decide whether the
        // key-status panel can sit beside it or must stack below it on narrow screens.
        private const float MainPanelX = 24f;
        private const float MainPanelY = 24f;
        private const float MainPanelWidth = 610f;
        private const float MainPanelHeight = 355f;
        private const float KeyStatusPanelWidth = 250f;
        // Row heights are calibrated from the last Play-verified layout (136 for header + 4
        // status rows, 207 for header + 8 guide rows), which implies a shared box/header
        // overhead of ~48px plus ~22px per status row and ~20px per guide row. Sized for the
        // current header + 12 status rows / header + 18 guide rows. 24 + 312 + 12 + 408 + 24 =
        // 780 > 540, so this no longer fits the exact-2x beside layout at 1920x1080 - the
        // fallback branch in CalculateRuntimeUiScale computes a slightly reduced scale instead,
        // which is the intended degrade path, not a bug.
        private const float KeyStatusPanelHeight = 312f;
        private const float KeyStatusPanelGap = 24f;
        private const float OperationGuidePanelWidth = 250f;
        private const float OperationGuidePanelHeight = 408f;
        private const float OperationPanelInnerGap = 12f;
        private const float DesiredRuntimeUiScale = 2f;
        private const float MinimumRuntimeUiScale = 1f;

        private GUIStyle keyStatusHeaderStyle;
        private GUIStyle keyStatusValueStyle;
        private GUIStyle operationGuideHeaderStyle;
        private GUIStyle operationGuideValueStyle;

        private YardCraneEightRopeMaxPrototypeRig rig;

        public void Initialize(YardCraneEightRopeMaxPrototypeRig ownerRig)
        {
            rig = ownerRig;
        }

        private void OnGUI()
        {
            if (rig == null || !rig.showRuntimePanel)
            {
                return;
            }

            var runtimeUiScale = CalculateRuntimeUiScale();
            var previousGuiMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(runtimeUiScale, runtimeUiScale, 1f)) * previousGuiMatrix;

            try
            {
                GUILayout.BeginArea(new Rect(MainPanelX, MainPanelY, MainPanelWidth, MainPanelHeight), GUI.skin.box);
                GUILayout.Label("YC RUNTIME ROOT · 新八绳 MAX 灰盒");
                GUILayout.Space(6f);
                GUILayout.Label($"状态：{rig.runtimeStatus}");
                GUILayout.Label($"拓扑：双卷筒 · 8根独立直绳 · 8个Obi可视绳体");
                GUILayout.Label($"绳索：{(rig.loopsReady ? "8/8 Ready" : $"{rig.LoopCount}/8 Loading")} · Obi仅负责视觉");
                GUILayout.Label($"吊具位置：{FormatVector(rig.headblockBody != null ? rig.headblockBody.position : Vector3.zero)} · " +
                                $"小车速度：{rig.currentTrolleyVelocity:F2} m/s · 起升速度：{rig.currentHoistVelocity:F2} m/s");
                GUILayout.Label($"大车位置：{(rig.gantryDrive?.PositionMeters ?? 0f):F2} m · " +
                                $"大车速度：{(rig.gantryDrive?.VelocityMetersPerSecond ?? 0f):F2} m/s · " +
                                $"夹轮器：{YardCraneWheelClampPresenter.TranslateState(rig.wheelClampPresenter?.State ?? WheelClampState.Unknown)} · " +
                                $"鸣笛：{(rig.hornActive ? "ON" : "OFF")}");
                GUILayout.Label($"吊高：{rig.commandedSuspendedDrop:F2} m · 当前摆移：{rig.currentHorizontalSway:F3} m · " +
                                $"最大摆移：{rig.maximumObservedSway:F3} m");
                GUILayout.Label($"相对小车速度：{rig.currentRelativeHorizontalSpeed:F3} m/s · " +
                                $"扭转：{rig.maximumObservedTwistDegrees:F2}°");
                GUILayout.Label($"重载垂向挠度：{rig.loadedHoistDeflection * 1000f:F1} mm · " +
                                $"挠度速度：{rig.loadedHoistDeflectionVelocity:F3} m/s");
                GUILayout.Label($"单绳伸长：{rig.maximumObservedLoopExtension * 1000f:F1} mm · " +
                                $"求解前残差：{rig.maximumRawLoopDrift * 1000f:F1} mm");
                GUILayout.Space(4f);
                GUILayout.Label($"锁销：{rig.twistlockStatus} · 箱重：{rig.containerGrossMassKg / 1000f:F1} t · " +
                                $"悬挂总重：{rig.totalSuspendedMassKg / 1000f:F1} t");
                GUILayout.Label($"平均单绳张力：{rig.averageRopeTensionKn:F1} kN · 物理代理质量：" +
                                $"{(rig.headblockBody != null ? rig.headblockBody.mass : 0f):F2} kg");
                if (rig.containerLocked)
                {
                    GUILayout.Label($"落箱确认：{(rig.containerLandingConfirmed ? "已确认" : "未确认")} · " +
                                    $"水平 {rig.containerLandingHorizontalErrorMeters:F2} m · " +
                                    $"垂直 {rig.containerLandingVerticalErrorMeters:F2} m · " +
                                    $"偏航 {rig.containerLandingYawErrorDegrees:F1}° · " +
                                    $"速度 {rig.containerLandingSpeedMetersPerSecond:F2} m/s");
                }
                else
                {
                    GUILayout.Label($"对位误差：水平 {rig.containerHorizontalErrorMeters:F2} m · " +
                                    $"垂直 {rig.containerVerticalErrorMeters:F2} m · 偏航 {rig.containerYawErrorDegrees:F1}°");
                }
                GUILayout.EndArea();

                DrawKeyOperationStatusPanel(Screen.width / runtimeUiScale);
                DrawOperationGuidePanel(Screen.width / runtimeUiScale);
            }
            finally
            {
                GUI.matrix = previousGuiMatrix;
            }
        }

        private static float CalculateRuntimeUiScale()
        {
            // Prefer the requested exact 2x scale. Wide screens keep both panels side by side;
            // portrait/narrow screens stack them and reduce only when 2x would leave the canvas.
            var besideRequiredWidth = MainPanelX + MainPanelWidth + KeyStatusPanelGap +
                                      KeyStatusPanelWidth + KeyStatusPanelGap;
            var besideRequiredHeight = Mathf.Max(MainPanelY + MainPanelHeight,
                MainPanelY + KeyStatusPanelHeight + OperationPanelInnerGap + OperationGuidePanelHeight) +
                KeyStatusPanelGap;
            if (Screen.width >= besideRequiredWidth * DesiredRuntimeUiScale &&
                Screen.height >= besideRequiredHeight * DesiredRuntimeUiScale)
            {
                return DesiredRuntimeUiScale;
            }

            var stackedOperationPanelsWidth = KeyStatusPanelWidth + OperationPanelInnerGap +
                                              OperationGuidePanelWidth;
            var stackedRequiredWidth = MainPanelX + Mathf.Max(MainPanelWidth, stackedOperationPanelsWidth) +
                                       KeyStatusPanelGap;
            var stackedRequiredHeight = MainPanelY + MainPanelHeight + 12f +
                                        Mathf.Max(KeyStatusPanelHeight, OperationGuidePanelHeight) +
                                        KeyStatusPanelGap;
            return Mathf.Clamp(Mathf.Min(
                    DesiredRuntimeUiScale,
                    Screen.width / stackedRequiredWidth,
                    Screen.height / stackedRequiredHeight),
                MinimumRuntimeUiScale,
                DesiredRuntimeUiScale);
        }

        /// <summary>
        /// Compact, high-visibility status block for brake/clamp/grab state. Docks beside the
        /// main panel's upper-right when the screen is wide enough for both without overlap;
        /// otherwise stacks below the main panel, still within the upper half of the screen.
        /// </summary>
        private void DrawKeyOperationStatusPanel(float logicalScreenWidth)
        {
            EnsureKeyStatusStyles();

            var canFitBesideMainPanel = logicalScreenWidth >=
                MainPanelX + MainPanelWidth + KeyStatusPanelGap + KeyStatusPanelWidth + KeyStatusPanelGap;
            var rect = canFitBesideMainPanel
                ? new Rect(logicalScreenWidth - KeyStatusPanelWidth - KeyStatusPanelGap, MainPanelY,
                    KeyStatusPanelWidth, KeyStatusPanelHeight)
                : new Rect(MainPanelX, MainPanelY + MainPanelHeight + 12f,
                    KeyStatusPanelWidth, KeyStatusPanelHeight);

            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("关键操作状态", keyStatusHeaderStyle);
            GUILayout.Label(BuildGantryBrakeStatusLine(), keyStatusValueStyle);
            GUILayout.Label(BuildWheelClampStatusLine(), keyStatusValueStyle);
            GUILayout.Label(BuildTrolleyAnchorStatusLine(), keyStatusValueStyle);
            GUILayout.Label(BuildGantryAnchorStatusLine(), keyStatusValueStyle);
            GUILayout.Label(BuildSpreaderPumpStatusLine(), keyStatusValueStyle);
            GUILayout.Label(BuildSpreaderSizeStatusLine(), keyStatusValueStyle);
            GUILayout.Label(BuildSpreaderRotationStatusLine(), keyStatusValueStyle);
            GUILayout.Label(BuildContainerGrabStatusLine(), keyStatusValueStyle);
            GUILayout.Label(BuildSafetyInterlockStatusLine(), keyStatusValueStyle);
            GUILayout.Label(BuildControlPowerStatusLine(), keyStatusValueStyle);
            GUILayout.Label(BuildEmergencyStopStatusLine(), keyStatusValueStyle);
            GUILayout.Label(BuildFaultResetStatusLine(), keyStatusValueStyle);
            GUILayout.EndArea();
        }

        /// <summary>
        /// Persistent keyboard/Xbox mapping. It shares the right-hand HUD column on wide screens
        /// and sits beside the key-status card below the main panel on portrait/narrow screens.
        /// </summary>
        private void DrawOperationGuidePanel(float logicalScreenWidth)
        {
            EnsureOperationGuideStyles();

            var canFitBesideMainPanel = logicalScreenWidth >=
                MainPanelX + MainPanelWidth + KeyStatusPanelGap + OperationGuidePanelWidth +
                KeyStatusPanelGap;
            var rect = canFitBesideMainPanel
                ? new Rect(logicalScreenWidth - OperationGuidePanelWidth - KeyStatusPanelGap,
                    MainPanelY + KeyStatusPanelHeight + OperationPanelInnerGap,
                    OperationGuidePanelWidth, OperationGuidePanelHeight)
                : new Rect(MainPanelX + KeyStatusPanelWidth + OperationPanelInnerGap,
                    MainPanelY + MainPanelHeight + 12f,
                    OperationGuidePanelWidth, OperationGuidePanelHeight);

            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("操作指引", operationGuideHeaderStyle);
            GUILayout.Label(BuildOperationGuideLine("大车行走", "J / L", "左摇杆左右"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("小车行走", "A / D", "右摇杆左右"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("吊具升降", "W / S", "左摇杆上下"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("鸣笛", "Space", "LB"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("锁箱", "E", "X"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("解锁", "Q", "B"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("夹轮器", "C", "Y"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("小车锚定", "X", "D-Pad↓"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("大车锚定", "Z", "RB"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("吊具泵", "P", "LT"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("吊具20尺", "1", "D-Pad←"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("吊具40尺", "2", "D-Pad→"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("吊具旋转", "[ / ]", "右摇杆Y"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("吊具回零", "0", "R3"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("暂停", "Esc", "Menu"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("控制电源", "F8", "D-Pad↑"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("急停", "F9", "View"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("故障复位", "R", "A"), operationGuideValueStyle);
            GUILayout.EndArea();
        }

        private void EnsureKeyStatusStyles()
        {
            if (keyStatusHeaderStyle != null)
            {
                return;
            }

            keyStatusHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 14,
                richText = true
            };
            keyStatusValueStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 13,
                richText = true
            };
        }

        private void EnsureOperationGuideStyles()
        {
            if (operationGuideHeaderStyle != null)
            {
                return;
            }

            operationGuideHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 14,
                richText = true
            };
            operationGuideValueStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Normal,
                fontSize = 12,
                richText = true
            };
        }

        private static string BuildOperationGuideLine(string operation, string keyboard, string xbox)
        {
            return $"{operation}  {YardCraneEightRopeMaxPrototypeRig.Colorize($"[{keyboard}]", OperationGuideColorCyan)} · " +
                   $"{YardCraneEightRopeMaxPrototypeRig.Colorize($"[{xbox}]", OperationGuideColorCyan)}";
        }

        private string BuildGantryBrakeStatusLine()
        {
            var brakeReleased = rig.gantryDrive != null && !rig.gantryDrive.IsBrakeApplied;
            var label = brakeReleased ? "已松闸" : "已制动";
            var color = brakeReleased
                ? YardCraneEightRopeMaxPrototypeRig.KeyStatusColorGreen
                : YardCraneEightRopeMaxPrototypeRig.KeyStatusColorNeutral;
            return $"大车制动：{YardCraneEightRopeMaxPrototypeRig.Colorize(label, color)}";
        }

        private string BuildWheelClampStatusLine()
        {
            return rig.wheelClampPresenter?.BuildStatusLine() ??
                   $"夹轮器：{YardCraneEightRopeMaxPrototypeRig.Colorize("状态未知", YardCraneEightRopeMaxPrototypeRig.KeyStatusColorRed)}";
        }

        private string BuildTrolleyAnchorStatusLine()
        {
            return rig.trolleyAnchorPresenter?.BuildStatusLine() ??
                   $"小车锚定：{YardCraneEightRopeMaxPrototypeRig.Colorize("状态未知", YardCraneEightRopeMaxPrototypeRig.KeyStatusColorRed)}";
        }

        private string BuildGantryAnchorStatusLine()
        {
            return rig.gantryAnchorPresenter?.BuildStatusLine() ??
                   $"大车锚定：{YardCraneEightRopeMaxPrototypeRig.Colorize("状态未知", YardCraneEightRopeMaxPrototypeRig.KeyStatusColorRed)}";
        }

        private string BuildSpreaderPumpStatusLine()
        {
            return rig.spreaderPump?.BuildStatusLine() ??
                   $"吊具泵：{YardCraneEightRopeMaxPrototypeRig.Colorize("已停止", YardCraneEightRopeMaxPrototypeRig.KeyStatusColorNeutral)}";
        }

        private string BuildSpreaderSizeStatusLine()
        {
            return rig.spreaderSizePresenter?.BuildStatusLine() ??
                   $"吊具尺寸：{YardCraneEightRopeMaxPrototypeRig.Colorize("状态未知", YardCraneEightRopeMaxPrototypeRig.KeyStatusColorRed)}";
        }

        private string BuildSpreaderRotationStatusLine()
        {
            var angle = rig.spreaderRotation?.CurrentAngleDegrees ?? 0f;
            if (rig.spreaderRotation?.IsReturningToZero ?? false)
            {
                return $"吊具角度：{YardCraneEightRopeMaxPrototypeRig.Colorize($"回零中 {angle:+0.0;-0.0;0.0}°", YardCraneEightRopeMaxPrototypeRig.KeyStatusColorAmber)}";
            }

            var color = Mathf.Approximately(angle, 0f)
                ? YardCraneEightRopeMaxPrototypeRig.KeyStatusColorGreen
                : YardCraneEightRopeMaxPrototypeRig.KeyStatusColorNeutral;
            return $"吊具角度：{YardCraneEightRopeMaxPrototypeRig.Colorize($"{angle:+0.0;-0.0;0.0}°", color)}";
        }

        private string BuildContainerGrabStatusLine()
        {
            // Unlock-in-progress must win over the stable "locked" reading: containerLocked
            // stays true for the whole unlock request/execution window, so checking it first
            // would permanently hide the "解锁中" transition state.
            string label;
            string color;
            if (rig.containerUnlockRequested)
            {
                label = "解锁中";
                color = YardCraneEightRopeMaxPrototypeRig.KeyStatusColorAmber;
            }
            else if (rig.containerLocked)
            {
                label = "已抓箱";
                color = YardCraneEightRopeMaxPrototypeRig.KeyStatusColorGreen;
            }
            else if (rig.containerLockRequested)
            {
                label = "锁箱中";
                color = YardCraneEightRopeMaxPrototypeRig.KeyStatusColorAmber;
            }
            else
            {
                label = "未抓箱";
                color = YardCraneEightRopeMaxPrototypeRig.KeyStatusColorNeutral;
            }

            return $"抓箱：{YardCraneEightRopeMaxPrototypeRig.Colorize(label, color)}";
        }

        private string BuildSafetyInterlockStatusLine()
        {
            if (rig.trolleyProximityInhibited && rig.gantryProximityInhibited)
            {
                return $"安全联锁：{YardCraneEightRopeMaxPrototypeRig.Colorize("大车/小车已限制", YardCraneEightRopeMaxPrototypeRig.KeyStatusColorAmber)}";
            }

            if (rig.trolleyProximityInhibited)
            {
                var label = rig.containerLocked ? "小车低净空禁止" : "小车朝箱侧禁止";
                return $"安全联锁：{YardCraneEightRopeMaxPrototypeRig.Colorize(label, YardCraneEightRopeMaxPrototypeRig.KeyStatusColorAmber)}";
            }

            if (rig.gantryProximityInhibited)
            {
                var label = rig.containerLocked ? "大车低净空禁止" : "大车朝箱侧禁止";
                return $"安全联锁：{YardCraneEightRopeMaxPrototypeRig.Colorize(label, YardCraneEightRopeMaxPrototypeRig.KeyStatusColorAmber)}";
            }

            return $"安全联锁：{YardCraneEightRopeMaxPrototypeRig.Colorize("正常", YardCraneEightRopeMaxPrototypeRig.KeyStatusColorGreen)}";
        }

        private string BuildControlPowerStatusLine()
        {
            if (rig.standaloneOperationHost == null)
            {
                return $"控制电源：{YardCraneEightRopeMaxPrototypeRig.Colorize("未知", YardCraneEightRopeMaxPrototypeRig.KeyStatusColorNeutral)}";
            }

            var on = rig.standaloneOperationHost.IsControlPowerOn;
            var label = on ? "已合" : "已断";
            var color = on ? YardCraneEightRopeMaxPrototypeRig.KeyStatusColorGreen : YardCraneEightRopeMaxPrototypeRig.KeyStatusColorRed;
            return $"控制电源：{YardCraneEightRopeMaxPrototypeRig.Colorize(label, color)}";
        }

        private string BuildEmergencyStopStatusLine()
        {
            if (rig.standaloneOperationHost == null)
            {
                return $"急停：{YardCraneEightRopeMaxPrototypeRig.Colorize("未知", YardCraneEightRopeMaxPrototypeRig.KeyStatusColorNeutral)}";
            }

            var released = rig.standaloneOperationHost.IsEmergencyStopReleased;
            var label = released ? "已释放" : "已按下";
            var color = released ? YardCraneEightRopeMaxPrototypeRig.KeyStatusColorGreen : YardCraneEightRopeMaxPrototypeRig.KeyStatusColorRed;
            return $"急停：{YardCraneEightRopeMaxPrototypeRig.Colorize(label, color)}";
        }

        private string BuildFaultResetStatusLine()
        {
            var awaitingReset = rig.standaloneOperationHost != null
                ? rig.standaloneOperationHost.IsAwaitingFaultReset
                : rig.operationController != null && rig.operationController.Phase == YardCraneOperationPhase.SafeHold;
            var label = awaitingReset ? "等待复位" : "正常";
            var color = awaitingReset ? YardCraneEightRopeMaxPrototypeRig.KeyStatusColorRed : YardCraneEightRopeMaxPrototypeRig.KeyStatusColorGreen;
            return $"安全复位：{YardCraneEightRopeMaxPrototypeRig.Colorize(label, color)}";
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:F2}, {value.y:F2}, {value.z:F2})";
        }
    }
}
