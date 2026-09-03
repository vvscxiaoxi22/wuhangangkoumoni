using System;
using System.Collections;
using PortTraining.YardCraneSimulator.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PortTraining.YardCraneSimulator.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class YardCraneBootstrap : MonoBehaviour
    {
        public const string MachineType = "YC";

        public SceneLaunchOptions LaunchOptions { get; private set; }
        public YardCraneSceneConfig SceneConfig { get; private set; }
        public YardCraneSceneStateMachine StateMachine { get; } = new YardCraneSceneStateMachine();

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

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

        private IEnumerator LoadStandaloneOperationalScene()
        {
            if (!StateMachine.TryTransition(YardCraneSceneState.Loading, out var transitionError))
            {
                FailStartup(transitionError);
                yield break;
            }

            var loadOperation = SceneManager.LoadSceneAsync(SceneConfig.operationalSceneName, LoadSceneMode.Single);
            if (loadOperation == null)
            {
                FailStartup($"Operational scene '{SceneConfig.operationalSceneName}' could not be loaded.");
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
                $"[YC_STANDALONE_SCENE_READY] scene={SceneConfig.operationalSceneName}, " +
                $"station={LaunchOptions.StationId}, state={StateMachine.Current}");
        }

        private void FailStartup(string error)
        {
            StateMachine.TryTransition(YardCraneSceneState.Failed, out _);
            Debug.LogError($"YardCraneSimulator startup rejected: {error}");

#if !UNITY_EDITOR
            Application.Quit(10);
#endif
        }
    }
}
