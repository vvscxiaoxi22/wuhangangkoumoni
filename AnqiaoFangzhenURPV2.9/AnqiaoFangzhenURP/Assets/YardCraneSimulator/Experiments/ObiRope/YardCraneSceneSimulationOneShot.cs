#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PortTraining.YardCraneSimulator.Experiments.RopePhysics
{
    /// <summary>
    /// Temporary, versioned bridge used once to materialize the scene-authored simulation
    /// contract in the already-open YardCrane scene. This file is removed after the marker
    /// below is observed in Editor.log; it is not part of the shipped runtime or authoring flow.
    /// </summary>
    [InitializeOnLoad]
    internal static class YardCraneSceneSimulationOneShot
    {
        private const string SessionKey = "YC.SceneSimulation.OneShot.v33";
        private const string RequiredScenePath = "Assets/YardCraneSimulator/Scenes/YardCrane.unity";

        static YardCraneSceneSimulationOneShot()
        {
            if (SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            EditorApplication.delayCall += RunOnce;
        }

        private static void RunOnce()
        {
            if (SessionState.GetBool(SessionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded ||
                !string.Equals(scene.path, RequiredScenePath, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning(
                    $"[YC_SCENE_AUTHORING_ONE_SHOT_SKIPPED] activeScene={scene.path}, required={RequiredScenePath}");
                return;
            }

            SessionState.SetBool(SessionKey, true);
            try
            {
                YardCraneSceneSimulationAuthoring.BakeActiveScene();
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException($"Unity refused to save {RequiredScenePath}.");
                }

                Debug.Log(
                    "[YC_SCENE_AUTHORING_ONE_SHOT_SAVED] scene=YardCrane, ropes=8, " +
                    "headblockPhysics=scene, containerPhysics=scene");
            }
            catch (Exception error)
            {
                Debug.LogError($"[YC_SCENE_AUTHORING_ONE_SHOT_FAILED] {error}");
            }
        }
    }
}
#endif
