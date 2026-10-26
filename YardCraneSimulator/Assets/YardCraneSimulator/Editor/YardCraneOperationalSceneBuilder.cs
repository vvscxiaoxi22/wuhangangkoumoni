using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PortTraining.YardCraneSimulator.Experiments.RopePhysics;
using PortTraining.YardCraneSimulator.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace PortTraining.YardCraneSimulator.Editor
{
    /// <summary>
    /// Binds and validates the cameras and UIDocument already authored in the formal
    /// YardCrane operational scene. It never creates/replaces a camera or UIDocument.
    /// </summary>
    [InitializeOnLoad]
    public static class YardCraneOperationalSceneBuilder
    {
        public const string ScenePath = "Assets/YardCraneSimulator/Scenes/YardCrane.unity";
        public const string Display1DocumentName = "UIDocumentDisplay1";
        public const string Display1CameraName = "Display1Camera";
        public const string Display2CameraName = "Display2Camera";

        private const string StudentEntryScenePath = "Assets/YardCraneSimulator/Scenes/StudentEntry.unity";
        private const string BootstrapScenePath = "Assets/YardCraneSimulator/Scenes/Bootstrap.unity";
        private const string StudentHomeScenePath = "Assets/YardCraneSimulator/Scenes/StudentHome.unity";
        private const string TeacherHomeScenePath = "Assets/YardCraneSimulator/Scenes/TeacherHome.unity";
        private static readonly string RequestPath = Path.GetFullPath("Temp/YC_OperationalSceneMigration.request");
        private static readonly string ResultPath = Path.GetFullPath("Logs/YC_OperationalSceneMigration.result.txt");

        static YardCraneOperationalSceneBuilder()
        {
            EditorApplication.update -= ProcessRequest;
            EditorApplication.update += ProcessRequest;
        }

        [MenuItem("Tools/Yard Crane/Scene/Bind Existing YardCrane Scene Presentation")]
        public static void CreateOrRepair()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before repairing the YardCrane scene.");

            if (!File.Exists(ScenePath))
                throw new FileNotFoundException("The formal YardCrane scene does not exist.", ScenePath);

            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.isDirty && !string.Equals(activeScene.path, ScenePath, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Save the currently open non-YardCrane scene before binding the YardCrane presentation.");

            var scene = string.Equals(activeScene.path, ScenePath, StringComparison.Ordinal)
                ? activeScene
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ConfigurePresentation(scene);
            ConfigureBuildSettings();
            Validate(scene);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException($"Failed to save the formal operational scene: {ScenePath}");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[YC_OPERATIONAL_SCENE_READY] version={YardCraneDisplayCameraLayout.Version}, " +
                $"scene=YardCrane, cameras=2, uidocuments=1, ui=Display1, legacyTestExcludedFromBuild=true");
        }

        [MenuItem("Tools/Yard Crane/Scene/Validate Formal YardCrane Scene")]
        public static void ValidateFormalScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before validating the YardCrane scene asset.");
            if (SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the active scene before validating the YardCrane scene.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Validate(scene);
            Debug.Log(
                "[YC_OPERATIONAL_SCENE_STATIC_PASS] scene=YardCrane, cameras=2, uidocuments=1, " +
                "cameraTargets=Display1+Display2, viewports=full, hud=Display1");
        }

        private static void ProcessRequest()
        {
            if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            File.Delete(RequestPath);
            try
            {
                CreateOrRepair();
                WriteResult("PASS", "Existing YardCrane cameras/UIDocument were bound and statically validated.");
            }
            catch (Exception error)
            {
                WriteResult("FAIL", error.ToString());
                Debug.LogError($"[YC_OPERATIONAL_SCENE_BIND_FAIL] {error}");
            }
        }

        private static void ConfigurePresentation(Scene scene)
        {
            var runtimeRoot = FindSceneRoot(scene, YardCraneModelHierarchyBuilder.RootName) ??
                              throw new InvalidOperationException("YC_RuntimeRoot is missing after hierarchy repair.");
            var cameraRoot = runtimeRoot.transform.Find("70_Cameras") ??
                             throw new InvalidOperationException("YC_RuntimeRoot/70_Cameras is missing.");

            var cameras = GetSceneComponents<Camera>(scene).ToArray();
            var documents = GetSceneComponents<UIDocument>(scene).ToArray();
            Require(cameras.Length == 2,
                $"YardCrane must already contain exactly two cameras; found {cameras.Length}. No camera was created.");
            Require(documents.Length == 1,
                $"YardCrane must already contain exactly one UIDocument; found {documents.Length}. No UIDocument was created.");

            var display1Matches = cameras.Where(item => item.name == Display1CameraName).ToArray();
            var display2Matches = cameras.Where(item => item.name == Display2CameraName).ToArray();
            Require(display1Matches.Length == 1,
                $"YardCrane must contain exactly one camera named {Display1CameraName}; found {display1Matches.Length}.");
            Require(display2Matches.Length == 1,
                $"YardCrane must contain exactly one camera named {Display2CameraName}; found {display2Matches.Length}.");
            var display1Camera = display1Matches[0];
            var display2Camera = display2Matches[0];
            display1Camera.gameObject.tag = "MainCamera";
            if (display2Camera.CompareTag("MainCamera")) display2Camera.gameObject.tag = "Untagged";
            ConfigureCamera(display1Camera, 0f, 0);
            ConfigureCamera(display2Camera, 0f, 1);

            var display1Listener = display1Camera.GetComponent<AudioListener>() ??
                                   display1Camera.gameObject.AddComponent<AudioListener>();
            display1Listener.enabled = true;
            foreach (var listener in GetSceneComponents<AudioListener>(scene).ToArray())
            {
                if (listener != display1Listener) listener.enabled = false;
            }

            var layouts = GetSceneComponents<YardCraneDisplayCameraLayout>(scene).ToArray();
            Require(layouts.Length <= 1,
                $"YardCrane contains {layouts.Length} display layout coordinators; keep only one.");
            var layout = layouts.FirstOrDefault() ?? cameraRoot.gameObject.AddComponent<YardCraneDisplayCameraLayout>();
            layout.Configure(display1Camera, display2Camera);

            var hudDocument = documents[0];
            hudDocument.gameObject.name = Display1DocumentName;
            hudDocument.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/YardCraneSimulator/Resources/YardCraneUI/OperatorHud.uxml");
            hudDocument.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(
                "Assets/YardCraneSimulator/Resources/YardCraneUI/OperatorHudPanel.asset");
            var existingHuds = GetSceneComponents<YardCraneOperatorHud>(scene).ToArray();
            var existingAdapters = GetSceneComponents<YardCraneHudSceneAdapter>(scene).ToArray();
            Require(existingHuds.Length == 0 ||
                    (existingHuds.Length == 1 && existingHuds[0].gameObject == hudDocument.gameObject),
                "The one YardCraneOperatorHud must be attached to UIDocumentDisplay1.");
            Require(existingAdapters.Length == 0 ||
                    (existingAdapters.Length == 1 && existingAdapters[0].gameObject == hudDocument.gameObject),
                "The one YardCraneHudSceneAdapter must be attached to UIDocumentDisplay1.");
            if (existingHuds.Length == 0) hudDocument.gameObject.AddComponent<YardCraneOperatorHud>();
            if (existingAdapters.Length == 0) hudDocument.gameObject.AddComponent<YardCraneHudSceneAdapter>();
        }

        private static void ConfigureCamera(Camera camera, float depth, int targetDisplay)
        {
            camera.depth = depth;
            camera.targetDisplay = targetDisplay;
            camera.targetTexture = null;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.enabled = true;
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(StudentEntryScenePath, true),
                new EditorBuildSettingsScene(BootstrapScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(StudentHomeScenePath, true),
                new EditorBuildSettingsScene(TeacherHomeScenePath, true)
            };
        }

        private static void Validate(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || !string.Equals(scene.path, ScenePath, StringComparison.Ordinal))
                throw new InvalidOperationException("The formal YardCrane scene is not the active loaded scene.");

            var cameras = GetSceneComponents<Camera>(scene).ToArray();
            var documents = GetSceneComponents<UIDocument>(scene).ToArray();
            var huds = GetSceneComponents<YardCraneOperatorHud>(scene).ToArray();
            var adapters = GetSceneComponents<YardCraneHudSceneAdapter>(scene).ToArray();
            var layouts = GetSceneComponents<YardCraneDisplayCameraLayout>(scene).ToArray();
            var listeners = GetSceneComponents<AudioListener>(scene).ToArray();
            var enabledListeners = listeners.Where(item => item.isActiveAndEnabled).ToArray();

            Require(cameras.Length == 2, $"Expected two cameras, found {cameras.Length}.");
            Require(documents.Length == 1, $"Expected one UIDocument, found {documents.Length}.");
            Require(huds.Length == 1, $"Expected one YardCraneOperatorHud, found {huds.Length}.");
            Require(adapters.Length == 1, $"Expected one YardCraneHudSceneAdapter, found {adapters.Length}.");
            Require(layouts.Length == 1, $"Expected one display layout coordinator, found {layouts.Length}.");
            Require(enabledListeners.Length == 1,
                $"Expected one enabled AudioListener, found {enabledListeners.Length}.");
            Require(cameras.All(camera => camera.isActiveAndEnabled),
                "Both YardCrane cameras must be active and enabled.");
            Require(documents[0].isActiveAndEnabled && huds[0].isActiveAndEnabled &&
                    adapters[0].isActiveAndEnabled && layouts[0].isActiveAndEnabled,
                "UIDocumentDisplay1, HUD, adapter and display layout must all be active and enabled.");
            Require(documents[0].gameObject.name == Display1DocumentName,
                $"The UIDocument object must be named {Display1DocumentName}.");
            Require(documents[0].visualTreeAsset != null && documents[0].panelSettings != null,
                "UIDocumentDisplay1 must reference its UXML and PanelSettings assets.");
            Require(documents[0].panelSettings.targetDisplay == 0,
                "UIDocumentDisplay1 must target Unity Display 1 (index 0).");
            Require(layouts[0].Display1Camera != null && layouts[0].Display2Camera != null &&
                    cameras.Contains(layouts[0].Display1Camera) && cameras.Contains(layouts[0].Display2Camera) &&
                    layouts[0].Display1Camera != layouts[0].Display2Camera,
                "The display layout must reference the two different cameras already authored in YardCrane.");
            Require(layouts[0].Display1Camera.targetDisplay == 0 &&
                    layouts[0].Display2Camera.targetDisplay == 1 &&
                    cameras.All(camera => camera.targetTexture == null),
                "Display1Camera must target Unity Display 1/index 0 and Display2Camera must target Display 2/index 1.");
            Require(cameras.Count(camera => camera.CompareTag("MainCamera")) == 1,
                "Exactly one camera must carry the MainCamera tag.");
            Require(RectEquals(layouts[0].Display1Camera.rect, new Rect(0f, 0f, 1f, 1f)) &&
                    RectEquals(layouts[0].Display2Camera.rect, new Rect(0f, 0f, 1f, 1f)),
                "Both native-display cameras must use a full-screen viewport.");
            Require(layouts[0].Display1Camera.name == Display1CameraName &&
                    layouts[0].Display2Camera.name == Display2CameraName,
                "The display layout camera names do not match the authored Display1/Display2 contract.");
            Require(layouts[0].Display1Camera.CompareTag("MainCamera") &&
                    enabledListeners[0].gameObject == layouts[0].Display1Camera.gameObject,
                "Display1_MainCamera must own the only AudioListener.");

            var enabledScenes = EditorBuildSettings.scenes.Where(item => item.enabled).Select(item => item.path).ToArray();
            Require(enabledScenes.SequenceEqual(new[]
                {
                    StudentEntryScenePath, BootstrapScenePath, ScenePath, StudentHomeScenePath,
                    TeacherHomeScenePath
                }), "Build Settings scene order does not match the five-scene application flow.");
        }

        private static GameObject FindSceneRoot(Scene scene, string name)
        {
            return scene.GetRootGameObjects().FirstOrDefault(item => item.name == name);
        }

        private static IEnumerable<T> GetSceneComponents<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<T>(true))
                yield return component;
        }

        private static bool RectEquals(Rect first, Rect second)
        {
            return Mathf.Abs(first.x - second.x) < 0.0001f &&
                   Mathf.Abs(first.y - second.y) < 0.0001f &&
                   Mathf.Abs(first.width - second.width) < 0.0001f &&
                   Mathf.Abs(first.height - second.height) < 0.0001f;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void WriteResult(string status, string detail)
        {
            var directory = Path.GetDirectoryName(ResultPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(
                ResultPath,
                $"status={status}{Environment.NewLine}timestampUtc={DateTime.UtcNow:O}{Environment.NewLine}{detail}");
        }
    }
}
