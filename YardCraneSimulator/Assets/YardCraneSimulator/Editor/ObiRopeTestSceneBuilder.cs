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
        public const string TestScenePath = "Assets/test.unity";
        public const string TestPlayerPath = "Builds/Tests/ObiRopePhysicsTest/ObiRopePhysicsTest.exe";

        private const string AutoConfigureSessionKey = "YardCraneSimulator.ObiRopeTest.AutoConfigured.v2";

        static ObiRopeTestSceneBuilder()
        {
            EditorApplication.delayCall += AutoConfigureOpenEmptyTestScene;
        }

        [MenuItem("Tools/Yard Crane/Obi Rope/Configure Test Scene")]
        public static void ConfigureTestScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play mode before configuring the Obi Rope test scene.");
            }

            var scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            // YC_RuntimeRoot is both the stable model-placement skeleton and the
            // runnable new eight-rope MAX greybox.
            YardCraneModelHierarchyBuilder.CreateOrRepairInActiveScene();

            if (!EditorSceneManager.SaveScene(scene, TestScenePath))
            {
                throw new InvalidOperationException($"Failed to save Obi Rope test scene: {TestScenePath}");
            }

            Selection.activeGameObject = GameObject.Find(YardCraneModelHierarchyBuilder.RootName);
            Debug.Log("YC_RuntimeRoot new eight-rope MAX greybox configured. Press Play to run the automated sequence.");
        }

        [MenuItem("Tools/Yard Crane/Obi Rope/Build Windows Smoke Test")]
        public static void BuildWindowsSmokeTest()
        {
            ConfigureTestScene();

            var outputDirectory = Path.GetDirectoryName(TestPlayerPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { TestScenePath },
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

        private static void AutoConfigureOpenEmptyTestScene()
        {
            if (SessionState.GetBool(AutoConfigureSessionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            var activeScene = SceneManager.GetActiveScene();
            if (!string.Equals(activeScene.path, TestScenePath, StringComparison.Ordinal) ||
                UnityEngine.Object.FindFirstObjectByType<YardCraneEightRopeMaxPrototypeRig>() != null)
            {
                return;
            }

            SessionState.SetBool(AutoConfigureSessionKey, true);
            ConfigureTestScene();
            ValidateImportedVersion();
        }
    }
}
