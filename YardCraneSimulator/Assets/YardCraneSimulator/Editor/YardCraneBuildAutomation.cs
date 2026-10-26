using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Editor
{
    public static class YardCraneBuildAutomation
    {
        private const string DevelopmentExecutable = "Builds/Development/YardCraneSimulator.exe";
        private const string PackageRoot = "Assets/YardCraneSimulator";
        private const string BuildProfilesRoot = "Assets/Settings/Build Profiles";
        private const string SessionRegistrationCheckKey =
            "PortTraining.YardCraneSimulator.RequiredSceneRegistrationChecked";
        private const string StudentEntryScene = "Assets/YardCraneSimulator/Scenes/StudentEntry.unity";
        private const string BootstrapScene = "Assets/YardCraneSimulator/Scenes/Bootstrap.unity";
        private const string OperationalScene = "Assets/YardCraneSimulator/Scenes/YardCrane.unity";
        private const string StudentHomeScene = "Assets/YardCraneSimulator/Scenes/StudentHome.unity";
        private const string TeacherHomeScene = "Assets/YardCraneSimulator/Scenes/TeacherHome.unity";
        private static bool sceneRegistrationQueued;

        private static readonly string[] RequiredSceneOrder =
        {
            StudentEntryScene,
            BootstrapScene,
            OperationalScene,
            StudentHomeScene,
            TeacherHomeScene
        };

        [MenuItem("Tools/PortTraining/Yard Crane/Register Required Scenes")]
        public static void RegisterRequiredScenes()
        {
            EnsureRequiredScenes(true);
        }

        [MenuItem("Tools/PortTraining/Yard Crane/Export UnityPackage Without Build Profiles")]
        public static void ExportUnityPackageWithoutBuildProfiles()
        {
            var projectDirectory = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
            var outputPath = EditorUtility.SaveFilePanel(
                "Export Yard Crane UnityPackage Without Build Profiles",
                projectDirectory,
                "YardCraneSimulator-NoBuildProfiles",
                "unitypackage");
            if (string.IsNullOrWhiteSpace(outputPath)) return;

            var packageAssets = AssetDatabase.FindAssets(string.Empty, new[] { PackageRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrWhiteSpace(path) && !AssetDatabase.IsValidFolder(path))
                .ToArray();
            var exportAssets = AssetDatabase.GetDependencies(packageAssets, true)
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal))
                .Where(path => !IsBuildProfilePath(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (exportAssets.Length == 0)
                throw new InvalidOperationException(
                    $"No exportable assets were found below {PackageRoot}.");

            AssetDatabase.ExportPackage(exportAssets, outputPath, ExportPackageOptions.Default);
            Debug.Log(
                $"[YC_UNITYPACKAGE_WITHOUT_BUILD_PROFILES_EXPORTED] path={outputPath}, " +
                $"assets={exportAssets.Length}, " +
                $"excluded={BuildProfilesRoot}/**");
        }

        internal static void QueueRequiredSceneRegistration()
        {
            if (sceneRegistrationQueued) return;
            sceneRegistrationQueued = true;
            EditorApplication.delayCall += RegisterRequiredScenesAfterReload;
        }

        internal static bool IsRequiredScenePath(string path)
        {
            return RequiredSceneOrder.Any(requiredPath =>
                string.Equals(requiredPath, path, StringComparison.OrdinalIgnoreCase));
        }

        internal static bool ShouldQueueRequiredSceneRegistration(
            string[] importedAssets,
            string[] movedAssets,
            bool didDomainReload)
        {
            if (importedAssets.Any(IsRequiredScenePath) || movedAssets.Any(IsRequiredScenePath))
            {
                return true;
            }

            // A new AssetPostprocessor is compiled after the package assets have been imported, so
            // the first callback that can see this type may contain only the domain-reload flag.
            // Check once per Editor session to cover that first import without writing settings on
            // every later script reload.
            if (!didDomainReload || SessionState.GetBool(SessionRegistrationCheckKey, false))
            {
                return false;
            }

            SessionState.SetBool(SessionRegistrationCheckKey, true);
            return true;
        }

        private static void RegisterRequiredScenesAfterReload()
        {
            sceneRegistrationQueued = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                QueueRequiredSceneRegistration();
                return;
            }
            EnsureRequiredScenes(false);
        }

        private static bool EnsureRequiredScenes(bool explicitRequest)
        {
            var missingAssets = RequiredSceneOrder
                .Where(path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                .ToArray();
            if (missingAssets.Length > 0)
            {
                if (explicitRequest)
                {
                    Debug.LogWarning(
                        "[YC_REQUIRED_SCENE_ASSETS_MISSING] Import the complete YardCraneSimulator package. " +
                        $"Missing scene assets: {string.Join(", ", missingAssets)}");
                }

                return false;
            }

            // Build profiles own their scene overrides. Package setup must update the receiving
            // project's global list without mutating whichever custom profile happens to be active.
            var scenes = EditorBuildSettings.globalScenes.ToList();
            var changed = false;
            foreach (var requiredPath in RequiredSceneOrder)
            {
                var index = scenes.FindIndex(scene =>
                    string.Equals(scene.path, requiredPath, StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                {
                    scenes.Add(new EditorBuildSettingsScene(requiredPath, true));
                    changed = true;
                    continue;
                }

                if (!scenes[index].enabled)
                {
                    scenes[index] = new EditorBuildSettingsScene(requiredPath, true);
                    changed = true;
                }
            }

            if (changed)
            {
                EditorBuildSettings.globalScenes = scenes.ToArray();
                Debug.Log(
                    "[YC_REQUIRED_SCENES_REGISTERED] Required scenes were appended/enabled in the " +
                    "global Scene List; existing project scenes and their order were preserved.");
            }
            else if (explicitRequest)
            {
                Debug.Log("[YC_REQUIRED_SCENES_READY] All required Yard Crane scenes are already enabled.");
            }

            return true;
        }

        private static bool IsBuildProfilePath(string path) =>
            string.Equals(path, BuildProfilesRoot, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(BuildProfilesRoot + "/", StringComparison.OrdinalIgnoreCase);

        [MenuItem("Tools/PortTraining/Yard Crane/Build Windows Development")]
        public static void BuildWindowsDevelopment()
        {
            if (!EnsureRequiredScenes(true))
            {
                throw new InvalidOperationException(
                    "Development build requires StudentEntry, Bootstrap, YardCrane, StudentHome and TeacherHome scene assets.");
            }

            // Build this application from its fixed scene set even when an importing project has
            // unrelated scenes. The registration step deliberately preserves those project scenes.
            var scenes = RequiredSceneOrder;

            var outputDirectory = Path.GetDirectoryName(DevelopmentExecutable);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = DevelopmentExecutable,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"YardCraneSimulator development build failed: result={report.summary.result}, " +
                    $"errors={report.summary.totalErrors}, warnings={report.summary.totalWarnings}");
            }

            Debug.Log(
                $"[YC_WINDOWS_DEVELOPMENT_BUILD_PASS] output={Path.GetFullPath(DevelopmentExecutable)}, " +
                $"sizeBytes={report.summary.totalSize}, duration={report.summary.totalTime}");
        }
    }

    internal sealed class YardCraneRequiredScenePostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths,
            bool didDomainReload)
        {
            if (!YardCraneBuildAutomation.ShouldQueueRequiredSceneRegistration(
                    importedAssets,
                    movedAssets,
                    didDomainReload)) return;
            YardCraneBuildAutomation.QueueRequiredSceneRegistration();
        }
    }
}
