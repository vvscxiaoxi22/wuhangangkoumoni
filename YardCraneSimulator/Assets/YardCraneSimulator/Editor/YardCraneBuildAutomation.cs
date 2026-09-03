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

        [MenuItem("Tools/PortTraining/Yard Crane/Build Windows Development")]
        public static void BuildWindowsDevelopment()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length < 2 || !scenes.Contains("Assets/YardCraneSimulator/Scenes/Bootstrap.unity") ||
                !scenes.Contains("Assets/test.unity"))
            {
                throw new InvalidOperationException(
                    "Development build must contain Bootstrap.unity followed by the operational test.unity scene.");
            }

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
}
