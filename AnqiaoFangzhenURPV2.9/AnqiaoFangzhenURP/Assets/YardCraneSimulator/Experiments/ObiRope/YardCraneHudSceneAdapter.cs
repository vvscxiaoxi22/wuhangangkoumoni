using PortTraining.YardCraneSimulator.Bootstrap;
using PortTraining.YardCraneSimulator.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PortTraining.YardCraneSimulator.Experiments.RopePhysics
{
    /// <summary>
    /// Only this bridge knows the experimental Rig. The UI assembly has no Obi dependency.
    /// It reads feedback, never calls an actuator or changes physics/input authority.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(11000)]
    public sealed class YardCraneHudSceneAdapter : MonoBehaviour, IYardCraneHudSource
    {
        private YardCraneEightRopeMaxPrototypeRig rig;
        private Transform trolley, spreader;
        private YardCraneDebugHudPresenter legacy;
        private YardCraneOperatorHud hud;
        private bool legacyWasEnabled;
        private bool legacySuppressed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe()
        {
            SceneManager.sceneLoaded -= InstallInScene;
            SceneManager.sceneLoaded += InstallInScene;
        }

        private static void InstallInScene(Scene scene, LoadSceneMode mode)
        {
            // One traversal on scene load, not a per-frame Find. The operational scene owns
            // the only UIDocument/HUD; neither Bootstrap nor this adapter creates UI.
            foreach (var sceneRoot in scene.GetRootGameObjects())
            foreach (var owner in sceneRoot.GetComponentsInChildren<YardCraneEightRopeMaxPrototypeRig>(true))
            {
                YardCraneOperatorHud sceneHud = null;
                foreach (var candidateRoot in scene.GetRootGameObjects())
                {
                    sceneHud ??= candidateRoot.GetComponentInChildren<YardCraneOperatorHud>(true);
                }

                if (sceneHud == null)
                {
                    Debug.LogError(
                        $"[YC_OPERATIONAL_HUD_MISSING] scene={scene.name}. Expected one scene-owned " +
                        "UIDocumentDisplay1 with YardCraneOperatorHud; no runtime fallback was created.", owner);
                    return;
                }

                var sceneAdapter = sceneHud.GetComponent<YardCraneHudSceneAdapter>();
                if (sceneAdapter == null)
                {
                    Debug.LogError(
                        $"[YC_OPERATIONAL_HUD_ADAPTER_MISSING] scene={scene.name}. " +
                        "UIDocumentDisplay1 must also contain YardCraneHudSceneAdapter.", sceneHud);
                    return;
                }

                var bootstrap = YardCraneBootstrap.Active;
                if (bootstrap != null)
                {
                    bootstrap.BindOperationalHud(sceneHud);
                }

                sceneAdapter.Initialize(owner);

                return;
            }
        }

        public void Initialize(YardCraneEightRopeMaxPrototypeRig owner)
        {
            rig = owner;
            trolley = owner.transform.Find("20_TrolleyMotion");
            spreader = owner.transform.Find("30_SpreaderPhysics/35_Spreader");
            legacy = owner.GetComponent<YardCraneDebugHudPresenter>();
            hud = GetComponent<YardCraneOperatorHud>();
            if (hud == null)
            {
                Debug.LogError(
                    "[YC_OPERATIONAL_HUD_INVALID] YardCraneHudSceneAdapter must share the existing " +
                    "UIDocumentDisplay1 GameObject with YardCraneOperatorHud; no runtime HUD was created.", this);
                enabled = false;
                return;
            }
            hud.Bind(this);
        }

        private void LateUpdate()
        {
            if (hud == null || rig == null) { RestoreLegacy(); return; }
            if (hud.VisualRoot != null) hud.VisualRoot.style.display = rig.showRuntimePanel
                ? UnityEngine.UIElements.DisplayStyle.Flex : UnityEngine.UIElements.DisplayStyle.None;
            if (hud.IsReady && isActiveAndEnabled)
            {
                if (legacy != null && !legacySuppressed)
                {
                    legacyWasEnabled = legacy.enabled;
                    legacy.enabled = false;
                    legacySuppressed = true;
                }
            }
            else RestoreLegacy();
        }

        public bool TryRead(out YardCraneHudSnapshot snapshot)
        {
            snapshot = default;
            if (!isActiveAndEnabled || rig == null || !rig.isActiveAndEnabled) return false;
            var host = rig.standaloneOperationHost;
            var flags = YardCraneHudSignals.None;
            void Flag(bool value, YardCraneHudSignals signal) { if (value) flags |= signal; }
            Flag(rig.LoopsReady, YardCraneHudSignals.Ready);
            Flag(rig.ContainerLocked, YardCraneHudSignals.Locked);
            Flag(rig.containerLockRequested, YardCraneHudSignals.Locking);
            Flag(rig.containerUnlockRequested, YardCraneHudSignals.Unlocking);
            Flag(rig.ContainerLandingConfirmed, YardCraneHudSignals.Landed);
            Flag(rig.SpreaderContactConfirmed, YardCraneHudSignals.SpreaderContact);
            Flag(rig.GantryBrakeKnown, YardCraneHudSignals.BrakeKnown);
            Flag(rig.GantryBrakeApplied, YardCraneHudSignals.BrakeApplied);
            Flag(rig.TrolleyBrakeKnown, YardCraneHudSignals.TrolleyBrakeKnown);
            Flag(rig.TrolleyBrakeApplied, YardCraneHudSignals.TrolleyBrakeApplied);
            Flag(host != null && host.isActiveAndEnabled, YardCraneHudSignals.SafetyKnown);
            Flag(host != null, YardCraneHudSignals.Standalone);
            Flag(host != null && host.IsControlPowerOn, YardCraneHudSignals.PowerOn);
            Flag(host != null && host.IsEmergencyStopReleased, YardCraneHudSignals.EmergencyReleased);
            Flag(rig.OperationPhase == Operations.YardCraneOperationPhase.SafeHold, YardCraneHudSignals.AwaitingReset);
            Flag(host != null && host.IsPaused, YardCraneHudSignals.Paused);
            Flag(rig.spreaderPump != null, YardCraneHudSignals.PumpKnown);
            Flag(rig.SpreaderPumpOn, YardCraneHudSignals.PumpOn);
            Flag(rig.SpreaderRotationReturningToZero, YardCraneHudSignals.ReturningToZero);
            Flag(rig.trolleyProximityInhibited, YardCraneHudSignals.TrolleyRestricted);
            Flag(rig.gantryProximityInhibited, YardCraneHudSignals.GantryRestricted);
            Flag(rig.HornActive, YardCraneHudSignals.Horn);

            var status = new YardCraneHudStatus(rig.OperationPhase, flags, rig.CurrentWheelClampState,
                rig.CurrentTrolleyAnchorState, rig.CurrentGantryAnchorState, rig.CurrentSpreaderSize,
                rig.CurrentSpreaderSizeState, host != null ? host.ActiveSourceId : string.Empty,
                // Twin-lift ("双箱") is not modeled by this yard-crane rig; see YardCraneHudLoadMode.
                YardCraneHudLoadMode.Unknown);
            var telemetry = new YardCraneHudTelemetry(
                rig.gantryDrive != null ? rig.GantryPositionMeters : float.NaN,
                rig.gantryDrive != null ? rig.GantrySpeedMetersPerSecondActual : float.NaN,
                trolley != null ? trolley.localPosition.x : float.NaN,
                trolley != null ? rig.currentTrolleyVelocity : float.NaN,
                spreader != null ? spreader.position.y : float.NaN,
                rig.headblockBody != null ? rig.headblockBody.linearVelocity.y : float.NaN,
                rig.SpreaderPhysicalRotationBound ? rig.SpreaderPhysicalRotationAngleDegrees : float.NaN);
            snapshot = new YardCraneHudSnapshot(Time.realtimeSinceStartupAsDouble, status, telemetry);
            return true;
        }

        private void RestoreLegacy()
        {
            if (legacySuppressed && legacy != null) legacy.enabled = legacyWasEnabled;
            legacySuppressed = false;
        }

        private void OnDisable()
        {
            // A failed/disabled source leaves the operator HUD visible as Unknown. Only a
            // disabled HUD object should restore IMGUI; never stack both interfaces.
            if (hud == null || !hud.IsReady) RestoreLegacy();
        }
        private void OnDestroy() => RestoreLegacy();
    }
}
