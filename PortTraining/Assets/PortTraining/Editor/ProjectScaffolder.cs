using System.IO;
using PortTraining.Control.EmbeddedGateway;
using PortTraining.Control.Input;
using PortTraining.Core.Bootstrap;
using PortTraining.Core.Station;
using PortTraining.Core.WorkOrders;
using PortTraining.Equipment.QC;
using PortTraining.Equipment.YC;
using PortTraining.UI.Runtime;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace PortTraining.Editor
{
    public static class ProjectScaffolder
    {
        private const string SceneDirectory = "Assets/PortTraining/Scenes";
        private const string BootstrapScene = SceneDirectory + "/Bootstrap.unity";
        private const string UiDirectory = "Assets/PortTraining/UI/Runtime";
        private const string StudentShellUxml = UiDirectory + "/StudentShell.uxml";
        private const string PanelSettingsAsset = UiDirectory + "/PortTrainingPanelSettings.asset";

        public static void Configure()
        {
            Directory.CreateDirectory(SceneDirectory);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var runtime = new GameObject("PortTrainingRuntime");
            runtime.AddComponent<PortTrainingBootstrap>();
            runtime.AddComponent<EmbeddedGatewayClient>();
            runtime.AddComponent<ControlInputRouter>();

            var equipmentModules = new GameObject("EquipmentModules");
            var qcModule = new GameObject("QC");
            qcModule.transform.SetParent(equipmentModules.transform);
            qcModule.AddComponent<QcEquipmentModule>();
            var ycModule = new GameObject("YC");
            ycModule.transform.SetParent(equipmentModules.transform);
            ycModule.AddComponent<YcEquipmentModule>();

            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsAsset);
            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panelSettings.referenceResolution = new Vector2Int(1920, 1080);
                panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panelSettings.match = 0.5f;
                AssetDatabase.CreateAsset(panelSettings, PanelSettingsAsset);
            }

            var studentUi = new GameObject("StudentShell");
            var document = studentUi.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(StudentShellUxml);
            studentUi.AddComponent<StudentShellController>();

            EditorSceneManager.SaveScene(scene, BootstrapScene);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapScene, true)
            };

            PlayerSettings.companyName = "PortTraining";
            PlayerSettings.productName = "PortTraining";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.runInBackground = true;

            AssetDatabase.SaveAssets();
            Debug.Log("[PortTraining] P0 project scaffold configured.");
        }

        public static void Validate()
        {
            var profile = StationProfileLoader.Load();
            if (profile.MachineType != MachineType.QC)
            {
                throw new InvalidDataException("Development station.json must load the QC profile.");
            }

            EditorSceneManager.OpenScene(BootstrapScene, OpenSceneMode.Single);
            RequireComponent<PortTrainingBootstrap>();
            RequireComponent<EmbeddedGatewayClient>();
            RequireComponent<ControlInputRouter>();
            RequireComponent<QcEquipmentModule>();
            RequireComponent<YcEquipmentModule>();
            RequireComponent<StudentShellController>();

            var document = Object.FindAnyObjectByType<UIDocument>();
            if (document == null || document.panelSettings == null || document.visualTreeAsset == null)
            {
                throw new MissingReferenceException("Student shell UIDocument is not fully configured.");
            }

            if (EditorBuildSettings.scenes.Length != 1 || EditorBuildSettings.scenes[0].path != BootstrapScene)
            {
                throw new InvalidDataException("Bootstrap scene is not the only enabled build scene.");
            }

            var qcModule = Object.FindAnyObjectByType<QcEquipmentModule>();
            var ycModule = Object.FindAnyObjectByType<YcEquipmentModule>();
            if (qcModule == null || ycModule == null || qcModule.gameObject == ycModule.gameObject)
            {
                throw new InvalidDataException("QC and YC modules must be placed on separate GameObjects.");
            }

            var visualRoot = document.visualTreeAsset.CloneTree();
            RequireVisualElement<Button>(visualRoot, "login-button");
            RequireVisualElement<VisualElement>(visualRoot, "login-wallpaper");
            RequireVisualElement<Button>(visualRoot, "free-training-button");
            RequireVisualElement<Button>(visualRoot, "enter-operation-button");
            RequireVisualElement<Label>(visualRoot, "task-status");
            RequireVisualElement<Label>(visualRoot, "gateway-pill");
            RequireVisualElement<Label>(visualRoot, "plc-pill");
            RequireVisualElement<VisualElement>(visualRoot, "home-view");
            RequireVisualElement<Button>(visualRoot, "nav-tasks");
            RequireVisualElement<VisualElement>(visualRoot, "task-center-view");
            RequireVisualElement<VisualElement>(visualRoot, "task-list");
            RequireVisualElement<Button>(visualRoot, "task-select-button");
            RequireVisualElement<Button>(visualRoot, "nav-assessment");
            RequireVisualElement<VisualElement>(visualRoot, "assessment-center-view");
            RequireVisualElement<VisualElement>(visualRoot, "assessment-list");
            RequireVisualElement<Button>(visualRoot, "assessment-start-button");
            RequireVisualElement<Button>(visualRoot, "nav-records");
            RequireVisualElement<VisualElement>(visualRoot, "training-records-view");
            RequireVisualElement<VisualElement>(visualRoot, "records-list");
            RequireVisualElement<Button>(visualRoot, "records-refresh-button");
            RequireVisualElement<Label>(visualRoot, "portrait-completion");
            RequireVisualElement<Label>(visualRoot, "portrait-duration");
            RequireVisualElement<Label>(visualRoot, "portrait-score");
            RequireVisualElement<Label>(visualRoot, "portrait-safety");
            RequireVisualElement<VisualElement>(visualRoot, "login-page");
            RequireVisualElement<VisualElement>(visualRoot, "workbench-page");

            var workOrders = new LocalWorkOrderRepository().LoadForStation(profile);
            if (workOrders.Count == 0)
            {
                throw new InvalidDataException($"No development work order matches station {profile.StationId}.");
            }

            Debug.Log($"[PortTraining] P1 validation passed for {profile.StationId} ({profile.MachineType}).");
        }

        public static void BuildWindowsDevelopment()
        {
            Validate();

            var buildDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Build/Windows"));
            Directory.CreateDirectory(buildDirectory);
            var executablePath = Path.Combine(buildDirectory, "PortTraining.exe");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { BootstrapScene },
                locationPathName = executablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new System.InvalidOperationException($"PortTraining Windows build failed: {report.summary.result}");
            }

            Debug.Log($"[PortTraining] Development build ready: {executablePath}");
        }

        private static void RequireComponent<T>() where T : Component
        {
            if (Object.FindAnyObjectByType<T>() == null)
            {
                throw new MissingComponentException($"Bootstrap scene is missing {typeof(T).Name}.");
            }
        }

        private static void RequireVisualElement<T>(VisualElement root, string name) where T : VisualElement
        {
            if (root.Q<T>(name) == null)
            {
                throw new MissingReferenceException(
                    string.IsNullOrEmpty(name)
                        ? $"Student shell is missing visual element of type {typeof(T).Name}."
                        : $"Student shell is missing visual element '{name}'.");
            }
        }
    }
}
