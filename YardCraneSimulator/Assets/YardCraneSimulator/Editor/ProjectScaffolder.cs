using System;
using System.IO;
using PortTraining.YardCraneSimulator.Bootstrap;
using PortTraining.YardCraneSimulator.Configuration;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Editor
{
    public static class ProjectScaffolder
    {
        private const string BootstrapScenePath = "Assets/YardCraneSimulator/Scenes/Bootstrap.unity";
        private const string StudentEntryScenePath = "Assets/YardCraneSimulator/Scenes/StudentEntry.unity";
        private const string OperationalScenePath = "Assets/YardCraneSimulator/Scenes/YardCrane.unity";
        private const string StudentHomeScenePath = "Assets/YardCraneSimulator/Scenes/StudentHome.unity";
        private const string TeacherHomeScenePath = "Assets/YardCraneSimulator/Scenes/TeacherHome.unity";

        public static void ConfigureProject()
        {
            ConfigurePlayerSettings();
            CreateStudentEntryScene();
            CreateBootstrapScene();
            ValidateProject();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("YardCraneSimulator project scaffold configured successfully.");
        }

        public static void ValidateProject()
        {
            if (!YardCraneSceneConfig.TryLoad(out var config, out var configError))
            {
                throw new InvalidOperationException(configError);
            }

            if (!string.Equals(config.machineType, YardCraneBootstrap.MachineType, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Bootstrap machine type and scene configuration do not match.");
            }

            var operationRunId = Guid.NewGuid();
            var sceneInstanceId = Guid.NewGuid();
            var leaseId = Guid.NewGuid();
            var args = new[]
            {
                "YardCraneSimulator.exe",
                "--station-id", "YC-LAB-01",
                "--operation-run-id", operationRunId.ToString("D"),
                "--scene-instance-id", sceneInstanceId.ToString("D"),
                "--coordinator-pipe", $"pt.operation.yc-lab-01.{operationRunId:D}",
                "--gateway-pipe", "pt.gateway.realtime.yc-lab-01",
                "--gateway-lease-id", leaseId.ToString("D"),
                "--display-profile", Presentation.YardCraneDisplayProfile.NativeDualDisplayProfileId,
                "--scene-profile", config.sceneProfileId,
                "--protocol-version", config.protocolVersion
            };

            if (!SceneLaunchOptions.TryParse(
                    args,
                    _ => "0123456789abcdefghijklmnopqrstuvwxyzABCDEFG",
                    out var options,
                    out var launchError))
            {
                throw new InvalidOperationException($"Startup contract validation failed: {launchError}");
            }

            if (options.OperationRunId != operationRunId || options.SceneInstanceId != sceneInstanceId)
            {
                throw new InvalidOperationException("Startup contract UUID values were not preserved.");
            }

            if (!File.Exists(BootstrapScenePath))
            {
                throw new FileNotFoundException("Bootstrap scene was not created.", BootstrapScenePath);
            }

            if (!File.Exists(StudentEntryScenePath))
            {
                throw new FileNotFoundException("Student entry scene was not created.", StudentEntryScenePath);
            }

            if (!File.Exists(OperationalScenePath))
            {
                throw new FileNotFoundException("The dedicated yard-crane operational scene is missing.", OperationalScenePath);
            }

            if (!File.Exists(StudentHomeScenePath))
            {
                throw new FileNotFoundException("Student home scene is missing.", StudentHomeScenePath);
            }

            if (!File.Exists(TeacherHomeScenePath))
            {
                throw new FileNotFoundException("Teacher home scene is missing.", TeacherHomeScenePath);
            }

            Debug.Log("YardCraneSimulator startup contract and local configuration validation passed.");
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "PortTraining";
            PlayerSettings.productName = "YardCraneSimulator";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.porttraining.yardcranesimulator");
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
            // Keep local development builds independent of the optional Windows C++/SDK toolchain.
            // A production IL2CPP profile will be introduced only after the release host is provisioned.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone, 1);

            EditorSettings.serializationMode = SerializationMode.ForceText;
        }

        private static void CreateBootstrapScene()
        {
            var sceneDirectory = Path.GetDirectoryName(BootstrapScenePath);
            if (!string.IsNullOrEmpty(sceneDirectory))
            {
                Directory.CreateDirectory(sceneDirectory);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var runtime = new GameObject("YardCraneRuntime");
            runtime.AddComponent<YardCraneBootstrap>();

            var sceneRoot = new GameObject("YardCraneSceneRoot");
            new GameObject("Environment").transform.SetParent(sceneRoot.transform);
            new GameObject("Equipment").transform.SetParent(sceneRoot.transform);
            new GameObject("VehiclesAndContainers").transform.SetParent(sceneRoot.transform);
            new GameObject("RuntimeUI").transform.SetParent(sceneRoot.transform);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 8f, -15f);
            cameraObject.transform.LookAt(new Vector3(0f, 2f, 0f));
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;

            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;

            if (!EditorSceneManager.SaveScene(scene, BootstrapScenePath))
            {
                throw new InvalidOperationException($"Failed to save bootstrap scene to {BootstrapScenePath}.");
            }

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(StudentEntryScenePath, true),
                new EditorBuildSettingsScene(BootstrapScenePath, true),
                new EditorBuildSettingsScene(OperationalScenePath, true),
                new EditorBuildSettingsScene(StudentHomeScenePath, true),
                new EditorBuildSettingsScene(TeacherHomeScenePath, true)
            };
        }

        private static void CreateStudentEntryScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("YardCraneStudentEntry").AddComponent<Shell.YardCraneStudentShell>();
            if (!EditorSceneManager.SaveScene(scene, StudentEntryScenePath))
            {
                throw new InvalidOperationException($"Failed to save student entry scene to {StudentEntryScenePath}.");
            }
        }
    }
}
