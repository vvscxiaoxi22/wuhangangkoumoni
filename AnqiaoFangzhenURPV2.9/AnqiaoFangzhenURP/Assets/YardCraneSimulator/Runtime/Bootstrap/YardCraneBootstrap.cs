using System;
using System.Collections;
using PortTraining.YardCraneSimulator.Configuration;
using PortTraining.YardCraneSimulator.Presentation;
using PortTraining.YardCraneSimulator.Training;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PortTraining.YardCraneSimulator.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class YardCraneBootstrap : MonoBehaviour
    {
        public const string MachineType = "YC";
        public static YardCraneBootstrap Active { get; private set; }
        public SceneLaunchOptions LaunchOptions { get; private set; }
        public YardCraneSceneConfig SceneConfig { get; private set; }
        public YardCraneSceneStateMachine StateMachine { get; } = new YardCraneSceneStateMachine();
        public YardCraneOperatorHud Hud => hud;
        public Guid TrainingSessionId => trainingSessionId;
        public bool HasStartupFailure => !string.IsNullOrEmpty(StartupFailureMessage);
        public string StartupFailureMessage { get; private set; } = string.Empty;
#if UNITY_EDITOR
        // Used only by the real Play-mode verifier to prove a broken operational-scene
        // configuration returns the student to a retryable page instead of stranding them
        // in Bootstrap. Player builds cannot override the configured scene name.
        public static string EditorOperationalSceneOverrideForVerification { get; set; } = string.Empty;
#endif

        private YardCraneOperatorHud hud;
        private Guid trainingSessionId;

        private void Awake()
        {
            if (Active != null && Active != this)
            {
                enabled = false;
                Debug.LogError(
                    $"[YC_DUPLICATE_BOOTSTRAP_REJECTED] active={Active.GetInstanceID()}, duplicate={GetInstanceID()}",
                    this);
                Destroy(gameObject);
                return;
            }

            Active = this;
            trainingSessionId = YardCraneTrainingSessionIdentity.SessionId;
            DontDestroyOnLoad(gameObject);
            StateMachine.Changed += HandleLifecycleChanged;

            if (!YardCraneSceneConfig.TryLoad(out var config, out var configError))
            {
                FailStartup(configError);
                return;
            }

            SceneConfig = config;

            if (!SceneLaunchOptions.TryParseRuntime(
                    Environment.GetCommandLineArgs(),
                    Environment.GetEnvironmentVariable,
                    config.sceneProfileId,
#if UNITY_EDITOR
                    true,
#else
                    false,
#endif
                    out var launchOptions,
                    out var launchError))
            {
                FailStartup(launchError);
                return;
            }

            if (!string.Equals(launchOptions.SceneProfile, config.sceneProfileId, StringComparison.Ordinal))
            {
                FailStartup($"Scene profile '{launchOptions.SceneProfile}' does not match configuration '{config.sceneProfileId}'.");
                return;
            }

            if (!config.SupportsDisplayProfile(launchOptions.DisplayProfile))
            {
                FailStartup($"Display profile '{launchOptions.DisplayProfile}' is not supported by this scene package.");
                return;
            }

            LaunchOptions = launchOptions;

            if (!StateMachine.TryTransition(YardCraneSceneState.WaitingForCoordinator, out var transitionError))
            {
                FailStartup(transitionError);
                return;
            }

            if (LaunchOptions.LaunchMode != YardCraneLaunchMode.Managed)
            {
                StartCoroutine(LoadStandaloneOperationalScene());
            }

            Debug.Log(
                $"YardCraneSimulator initialized. stationId={LaunchOptions.StationId}, " +
                $"sceneProfile={LaunchOptions.SceneProfile}, displayProfile={LaunchOptions.DisplayProfile}, " +
                $"protocolVersion={LaunchOptions.ProtocolVersion}, launchMode={LaunchOptions.LaunchMode}.");
        }

        /// <summary>
        /// The operational scene owns the single driving UIDocument. Bootstrap only supplies
        /// lifecycle/session data so loading Bootstrap can never create a duplicate screen.
        /// </summary>
        public void BindOperationalHud(YardCraneOperatorHud operationalHud)
        {
            hud = operationalHud != null
                ? operationalHud
                : throw new ArgumentNullException(nameof(operationalHud));
            hud.BindLifecycle(StateMachine);
            if (LaunchOptions != null)
            {
                hud.BindSessionMode(LaunchOptions.LaunchMode);
            }
        }

        private IEnumerator LoadStandaloneOperationalScene()
        {
            if (!StateMachine.TryTransition(YardCraneSceneState.Loading, out var transitionError))
            {
                FailStartup(transitionError);
                yield break;
            }

            var operationalSceneName = SceneConfig.operationalSceneName;
#if UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace(EditorOperationalSceneOverrideForVerification))
                operationalSceneName = EditorOperationalSceneOverrideForVerification.Trim();
#endif
            if (string.IsNullOrWhiteSpace(operationalSceneName) ||
                !Application.CanStreamedLevelBeLoaded(operationalSceneName))
            {
                FailStartup(
                    $"Operational scene '{operationalSceneName ?? "(null)"}' is not available in Build Settings.");
                yield break;
            }

            AsyncOperation loadOperation;
            try
            {
                loadOperation = SceneManager.LoadSceneAsync(operationalSceneName, LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                FailStartup(
                    $"Operational scene '{operationalSceneName}' could not be loaded: {exception.Message}");
                yield break;
            }
            if (loadOperation == null)
            {
                FailStartup($"Operational scene '{operationalSceneName}' could not be loaded.");
                yield break;
            }

            yield return loadOperation;

            if (!StateMachine.TryTransition(YardCraneSceneState.Ready, out transitionError) ||
                !StateMachine.TryTransition(YardCraneSceneState.Running, out transitionError))
            {
                FailStartup(transitionError);
                yield break;
            }

            Debug.Log(
                $"[YC_STANDALONE_SCENE_READY] scene={operationalSceneName}, " +
                $"station={LaunchOptions.StationId}, state={StateMachine.Current}");
        }

        private void FailStartup(string error)
        {
            StartupFailureMessage = string.IsNullOrWhiteSpace(error)
                ? "Unknown startup failure."
                : error.Trim();
            StateMachine.TryTransition(YardCraneSceneState.Failed, out _);
            Debug.LogError($"YardCraneSimulator startup rejected: {StartupFailureMessage}");

#if !UNITY_EDITOR
            // A local StudentHome launch owns a persistent navigation bridge which returns
            // the student to the exact selection page and presents a retryable error. Do not
            // terminate that player before the bridge can perform the recovery. Managed and
            // direct standalone launches retain the fail-fast process contract.
            var localContext = YardCraneLocalTrainingSession.CurrentContext;
            if (trainingSessionId == Guid.Empty ||
                YardCraneTrainingSessionIdentity.SessionId != trainingSessionId ||
                localContext == null || localContext.SessionId != trainingSessionId)
                Application.Quit(10);
#endif
        }

        private void HandleLifecycleChanged(YardCraneSceneState previous, YardCraneSceneState next)
        {
            switch (next)
            {
                case YardCraneSceneState.ExitAllowed:
                    YardCraneTrainingSessionIdentity.TryMarkCompleted(trainingSessionId);
                    break;
                case YardCraneSceneState.Terminated:
                    YardCraneTrainingSessionIdentity.TryMarkTerminated(trainingSessionId);
                    break;
                case YardCraneSceneState.Failed:
                    YardCraneTrainingSessionIdentity.TryMarkFailed(trainingSessionId);
                    break;
            }
        }

        private void OnDestroy()
        {
            StateMachine.Changed -= HandleLifecycleChanged;
            if (Active == this) Active = null;
        }

    }
}
