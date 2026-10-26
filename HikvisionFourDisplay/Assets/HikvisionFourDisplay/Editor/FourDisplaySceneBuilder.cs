using System;
using System.IO;
using System.Linq;
using HikvisionFourDisplay.Runtime.Bootstrap;
using HikvisionFourDisplay.Runtime.Feeds;
using HikvisionFourDisplay.Runtime.Presentation;
using HikvisionFourDisplay.Runtime.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HikvisionFourDisplay.Editor
{
    public static class FourDisplaySceneBuilder
    {
        public const string ScenePath = "Assets/HikvisionFourDisplay/Scenes/Main.unity";

        private static readonly Color[] PlaceholderColors =
        {
            new Color(0.035f, 0.145f, 0.235f, 1f),
            new Color(0.055f, 0.205f, 0.185f, 1f),
            new Color(0.205f, 0.105f, 0.255f, 1f),
            new Color(0.285f, 0.145f, 0.055f, 1f)
        };

        [MenuItem("Tools/Hikvision Four Display/Create Scene Scaffold")]
        public static void CreateSceneScaffold()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before creating the scene scaffold.");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var application = new GameObject("Application");

            var displays = new GameObject("Displays");
            var controllers = new CameraFeedController[4];
            for (var displayIndex = 0; displayIndex < 4; displayIndex++)
                controllers[displayIndex] = CreateDisplay(displays.transform, displayIndex);

            var bootstrap = application.AddComponent<FourDisplayBootstrap>();
            bootstrap.Configure(controllers);

            var configPanelObject = new GameObject("ConfigPanel");
            configPanelObject.transform.SetParent(application.transform, false);
            var configPanel = configPanelObject.AddComponent<FeedConfigurationPanelUI>();
            configPanel.Configure(bootstrap);

            EnsureFeedShaderIsBuilt();

            var sceneDirectory = Path.GetDirectoryName(ScenePath);
            if (string.IsNullOrWhiteSpace(sceneDirectory))
                throw new InvalidOperationException($"Invalid scene path: {ScenePath}");

            Directory.CreateDirectory(sceneDirectory);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException($"Failed to save scene: {ScenePath}");

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorSettings.serializationMode = SerializationMode.ForceText;
            PlayerSettings.companyName = "WuhanProject";
            PlayerSettings.productName = "Hikvision Four Display";
            PlayerSettings.bundleVersion = "0.2.0-mock-feeds";
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.allowFullscreenSwitch = false;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateScene(scene);

            Debug.Log(
                "[HFD_SCENE_SCAFFOLD_CREATED] scene=Main cameras=4 cameraTargets=0+1+2+3 " +
                "videoSurfaces=4 placeholders=4 audioListeners=1 feedControllers=4 status=PASS");
        }

        [MenuItem("Tools/Hikvision Four Display/Validate Scene Scaffold")]
        public static void ValidateSceneScaffold()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before validating the scene scaffold.");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ValidateScene(scene);
            Debug.Log(
                "[HFD_SCENE_STATIC_PASS] scene=Main cameras=4 cameraTargets=0+1+2+3 " +
                "viewports=full videoSurfaces=4 placeholders=4 audioListeners=1 feedControllers=4 buildScenes=1");
        }

        private static CameraFeedController CreateDisplay(Transform parent, int displayIndex)
        {
            var displayNumber = displayIndex + 1;
            var displayRoot = new GameObject($"Display{displayNumber}");
            displayRoot.transform.SetParent(parent, false);
            displayRoot.transform.localPosition = new Vector3(displayIndex * 24f, 0f, 0f);

            var cameraObject = new GameObject($"Display{displayNumber}Camera");
            cameraObject.transform.SetParent(displayRoot.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);

            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = PlaceholderColors[displayIndex] * 0.5f;
            camera.orthographic = true;
            camera.orthographicSize = 4.5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.targetTexture = null;
            camera.targetDisplay = displayIndex;
            camera.depth = displayIndex;

            if (displayIndex == 0)
            {
                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<AudioListener>();
            }

            var surfaceObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            surfaceObject.name = $"Display{displayNumber}VideoSurface";
            surfaceObject.transform.SetParent(displayRoot.transform, false);
            surfaceObject.transform.localPosition = Vector3.zero;
            surfaceObject.transform.localScale = new Vector3(16f, 9f, 1f);
            var collider = surfaceObject.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            surfaceObject.GetComponent<MeshRenderer>().enabled = false;

            var labelObject = new GameObject($"Display{displayNumber}Placeholder");
            labelObject.transform.SetParent(displayRoot.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0f, -0.1f);

            var label = labelObject.AddComponent<TextMesh>();
            label.fontSize = 100;
            label.characterSize = 0.075f;
            label.fontStyle = FontStyle.Bold;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = Color.white;
            label.text = $"DISPLAY {displayNumber}\nCAMERA {displayNumber}\nSTREAM NOT CONFIGURED";
            labelObject.GetComponent<MeshRenderer>().sortingOrder = 10;

            var presenter = surfaceObject.AddComponent<FeedSurfacePresenter>();
            presenter.Configure(surfaceObject.GetComponent<MeshRenderer>(), label, camera, displayNumber, PlaceholderColors[displayIndex] * 0.5f);

            var controller = displayRoot.AddComponent<CameraFeedController>();
            controller.Configure($"feed{displayNumber}", displayIndex, presenter);

            return controller;
        }

        private static void ValidateScene(Scene scene)
        {
            Require(scene.IsValid() && scene.isLoaded, "Main scene must be valid and loaded.");

            var roots = scene.GetRootGameObjects();
            Require(roots.Count(item => item.name == "Application") == 1,
                "Scene must contain exactly one Application root.");
            Require(roots.Count(item => item.name == "Displays") == 1,
                "Scene must contain exactly one Displays root.");

            var application = roots.Single(item => item.name == "Application");
            var displays = roots.Single(item => item.name == "Displays");
            var cameras = roots.SelectMany(item => item.GetComponentsInChildren<Camera>(true)).ToArray();
            var labels = roots.SelectMany(item => item.GetComponentsInChildren<TextMesh>(true)).ToArray();
            var listeners = roots.SelectMany(item => item.GetComponentsInChildren<AudioListener>(true))
                .Where(item => item.enabled)
                .ToArray();

            Require(cameras.Length == 4, $"Scene must contain exactly four cameras; found {cameras.Length}.");
            Require(labels.Length == 4, $"Scene must contain exactly four placeholders; found {labels.Length}.");
            Require(listeners.Length == 1,
                $"Scene must contain exactly one enabled AudioListener; found {listeners.Length}.");

            var bootstrap = application.GetComponent<FourDisplayBootstrap>();
            Require(bootstrap != null, "Application must carry a FourDisplayBootstrap component.");

            var configPanel = application.transform.Find("ConfigPanel");
            Require(configPanel != null && configPanel.GetComponent<FeedConfigurationPanelUI>() != null,
                "Application must contain a ConfigPanel child with FeedConfigurationPanelUI.");

            for (var displayIndex = 0; displayIndex < 4; displayIndex++)
            {
                var displayNumber = displayIndex + 1;
                var group = displays.transform.Find($"Display{displayNumber}");
                Require(group != null, $"Display{displayNumber} group is missing.");

                var cameraTransform = group.Find($"Display{displayNumber}Camera");
                Require(cameraTransform != null, $"Display{displayNumber}Camera is missing.");
                var camera = cameraTransform.GetComponent<Camera>();
                Require(camera != null, $"Display{displayNumber}Camera has no Camera component.");
                Require(camera.targetDisplay == displayIndex,
                    $"Display{displayNumber}Camera must target Unity Display index {displayIndex}.");
                Require(camera.rect == new Rect(0f, 0f, 1f, 1f),
                    $"Display{displayNumber}Camera must use a full-screen viewport.");
                Require(camera.targetTexture == null,
                    $"Display{displayNumber}Camera must not retain a RenderTexture target.");

                var surface = group.Find($"Display{displayNumber}VideoSurface");
                Require(surface != null && surface.GetComponent<MeshFilter>() != null &&
                        surface.GetComponent<MeshRenderer>() != null,
                    $"Display{displayNumber}VideoSurface is missing or invalid.");
                Require(!surface.GetComponent<MeshRenderer>().enabled,
                    $"Display{displayNumber}VideoSurface must remain disabled in the scaffold.");
                Require(surface.GetComponent<FeedSurfacePresenter>() != null,
                    $"Display{displayNumber}VideoSurface must carry a FeedSurfacePresenter.");

                var placeholder = group.Find($"Display{displayNumber}Placeholder");
                Require(placeholder != null && placeholder.GetComponent<TextMesh>() != null,
                    $"Display{displayNumber}Placeholder is missing or invalid.");

                var controller = group.GetComponent<CameraFeedController>();
                Require(controller != null, $"Display{displayNumber} group must carry a CameraFeedController.");
                Require(controller.FeedId == $"feed{displayNumber}",
                    $"Display{displayNumber} CameraFeedController must have feedId 'feed{displayNumber}'; found '{controller?.FeedId}'.");
                Require(controller.DisplayIndex == displayIndex,
                    $"Display{displayNumber} CameraFeedController must target displayIndex {displayIndex}; found {controller?.DisplayIndex}.");
            }

            Require(cameras.Count(item => item.CompareTag("MainCamera")) == 1,
                "Exactly one camera must use the MainCamera tag.");
            Require(listeners[0].gameObject.name == "Display1Camera",
                "The only enabled AudioListener must belong to Display1Camera.");

            var enabledBuildScenes = EditorBuildSettings.scenes.Where(item => item.enabled).ToArray();
            Require(enabledBuildScenes.Length == 1 && enabledBuildScenes[0].path == ScenePath,
                "Build Settings must contain only Main.unity as the enabled scene.");
        }

        private static void EnsureFeedShaderIsBuilt()
        {
            // FeedSurfacePresenter creates its material from this shader at runtime via
            // Shader.Find; Unity strips built-in shaders with no static reference from a
            // Player build unless they are marked "always included" in GraphicsSettings.
            var shader = Shader.Find("Unlit/Texture");
            if (shader == null)
                throw new InvalidOperationException("Built-in shader 'Unlit/Texture' could not be found.");

            var graphicsSettingsAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/GraphicsSettings.asset");
            if (graphicsSettingsAsset == null)
                throw new InvalidOperationException("Could not load ProjectSettings/GraphicsSettings.asset.");

            var serializedObject = new SerializedObject(graphicsSettingsAsset);
            var alwaysIncluded = serializedObject.FindProperty("m_AlwaysIncludedShaders");
            if (alwaysIncluded == null)
                throw new InvalidOperationException("GraphicsSettings has no m_AlwaysIncludedShaders property.");

            for (var i = 0; i < alwaysIncluded.arraySize; i++)
            {
                if (alwaysIncluded.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    return;
            }

            alwaysIncluded.InsertArrayElementAtIndex(alwaysIncluded.arraySize);
            alwaysIncluded.GetArrayElementAtIndex(alwaysIncluded.arraySize - 1).objectReferenceValue = shader;
            serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
