using System;
using System.IO;
using PortTraining.YardCraneSimulator.Experiments.RopePhysics;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PortTraining.YardCraneSimulator.Editor
{
    [InitializeOnLoad]
    public static class ObiRopeTestSceneBuilder
    {
        public const string OperationalScenePath = "Assets/YardCraneSimulator/Scenes/YardCrane.unity";
        [Obsolete("Use OperationalScenePath instead.")]
        public const string TestScenePath = OperationalScenePath;
        public const string TestPlayerPath = "Builds/Tests/ObiRopePhysicsTest/ObiRopePhysicsTest.exe";

        private const string AutoConfigureSessionKey = "YardCraneSimulator.ObiRope.OperationalScene.AutoRepaired.v1";

        static ObiRopeTestSceneBuilder()
        {
            EditorApplication.delayCall += AutoRepairOpenOperationalScene;
        }

        [MenuItem("Tools/Yard Crane/Obi Rope/Repair Operational Scene")]
        public static void RepairOperationalScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play mode before repairing the yard-crane operational scene.");
            }

            var scene = EditorSceneManager.OpenScene(OperationalScenePath, OpenSceneMode.Single);

            // Repair only the stable runtime hierarchy. Never clear the operational
            // scene because it also owns cameras, UI and imported production models.
            YardCraneModelHierarchyBuilder.CreateOrRepairInActiveScene();
            YardCraneModelHierarchyBuilder.ValidateInActiveScene();

            if (!EditorSceneManager.SaveScene(scene, OperationalScenePath))
            {
                throw new InvalidOperationException(
                    $"Failed to save the yard-crane operational scene: {OperationalScenePath}");
            }

            Selection.activeGameObject = GameObject.Find(YardCraneModelHierarchyBuilder.RootName);
            Debug.Log(
                "[YC_OPERATIONAL_SCENE_REPAIRED] YC_RuntimeRoot was repaired and validated without clearing other scene content.");
        }

        // Retained for existing automation callers while the scene tooling migrates.
        [Obsolete("Use RepairOperationalScene instead.")]
        public static void ConfigureTestScene()
        {
            RepairOperationalScene();
        }

        [MenuItem("Tools/Yard Crane/Obi Rope/Build Windows Smoke Test")]
        public static void BuildWindowsSmokeTest()
        {
            RepairOperationalScene();

            var outputDirectory = Path.GetDirectoryName(TestPlayerPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { OperationalScenePath },
                locationPathName = TestPlayerPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"Obi Rope smoke-test build failed: {report.summary.result}");
            }

            Debug.Log($"Obi Rope Windows smoke-test player built: {TestPlayerPath}");
        }

        [MenuItem("Tools/Yard Crane/Obi Rope/Validate Imported Version")]
        public static void ValidateImportedVersion()
        {
            var changelogPath = Path.Combine(Application.dataPath, "Obi", "CHANGELOG_rope.txt");
            if (!File.Exists(changelogPath))
            {
                throw new FileNotFoundException("Obi Rope changelog was not found.", changelogPath);
            }

            var changelog = File.ReadAllText(changelogPath);
            if (!changelog.Contains("[7.1]", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The imported Obi Rope package is not version 7.1.");
            }

            Debug.Log("Obi Rope 7.1 import validation passed.");
        }

        private static void AutoRepairOpenOperationalScene()
        {
            if (SessionState.GetBool(AutoConfigureSessionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            var activeScene = SceneManager.GetActiveScene();
            if (!string.Equals(activeScene.path, OperationalScenePath, StringComparison.Ordinal) ||
                UnityEngine.Object.FindFirstObjectByType<YardCraneEightRopeMaxPrototypeRig>() != null)
            {
                return;
            }

            SessionState.SetBool(AutoConfigureSessionKey, true);
            RepairOperationalScene();
            ValidateImportedVersion();
        }
    }
}
