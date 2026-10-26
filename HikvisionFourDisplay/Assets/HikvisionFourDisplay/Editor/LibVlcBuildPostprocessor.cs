using System;
using System.IO;
using HikvisionFourDisplay.Runtime.Configuration;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HikvisionFourDisplay.Editor
{
    /// <summary>
    /// Copies the libvlc module tree (HikvisionFourDisplay/VlcRuntime/plugins, kept outside
    /// Assets/ so Unity's native-plugin importer never touches it — see LibVlcRuntime.cs)
    /// into the built Player's Plugins/x86_64/plugins folder, next to libvlc.dll, which is
    /// where libvlc's module scan actually looks.
    /// </summary>
    public sealed class LibVlcBuildPostprocessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            var exePath = report.summary.outputPath;
            if (string.IsNullOrEmpty(exePath) || !exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                return; // Not a Windows build; libvlc plugins are only packaged for Windows.

            var buildDir = Path.GetDirectoryName(exePath);
            var productDataDir = Path.Combine(buildDir!, Path.GetFileNameWithoutExtension(exePath) + "_Data");
            var destination = Path.Combine(productDataDir, "Plugins", "x86_64", "plugins");

            CopyFeedConfiguration(buildDir!);
            CopyCameraChoices(buildDir!);

            var source = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "VlcRuntime", "plugins"));
            if (!Directory.Exists(source))
            {
                Debug.LogWarning($"[HFD_LIBVLC_POSTBUILD] source not found, skipping: {source}");
                return;
            }

            CopyDirectory(source, destination);
            Debug.Log($"[HFD_LIBVLC_POSTBUILD] copied {source} -> {destination}");
        }

        private static void CopyFeedConfiguration(string buildDir)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var externalConfig = Path.Combine(projectRoot, "Config", FeedConfigurationLoader.ConfigFileName);
            var templateConfig = Path.Combine(
                Application.streamingAssetsPath,
                "HikvisionFourDisplay",
                "feeds.template.json");
            var source = File.Exists(externalConfig) ? externalConfig : templateConfig;

            if (!File.Exists(source))
            {
                Debug.LogWarning($"[HFD_CONFIG_POSTBUILD] source not found, skipping: {source}");
                return;
            }

            var document = JsonUtility.FromJson<FeedConfigurationDocument>(File.ReadAllText(source));
            if (document == null)
            {
                Debug.LogWarning($"[HFD_CONFIG_POSTBUILD] invalid configuration, skipping: {source}");
                return;
            }

            var destinationDir = Path.Combine(buildDir, "Config");
            Directory.CreateDirectory(destinationDir);
            var destination = Path.Combine(destinationDir, FeedConfigurationLoader.ConfigFileName);
            File.WriteAllText(destination, JsonUtility.ToJson(document, true));
            Debug.Log($"[HFD_CONFIG_POSTBUILD] copied sanitized configuration {source} -> {destination}");
        }

        private static void CopyCameraChoices(string buildDir)
        {
            var destinationDir = Path.Combine(buildDir, "Config");
            Directory.CreateDirectory(destinationDir);
            var catalog = CameraCatalogLoader.Load(out _, out _);
            File.WriteAllText(Path.Combine(destinationDir, CameraCatalogLoader.ConfigFileName),
                JsonUtility.ToJson(catalog, true));
            if (File.Exists(FeedSchemeStore.ExternalConfigPath))
            {
                var schemes = FeedSchemeStore.Load(out _);
                File.WriteAllText(Path.Combine(destinationDir, FeedSchemeStore.ConfigFileName),
                    JsonUtility.ToJson(schemes, true));
            }
            Debug.Log($"[HFD_CHOICES_POSTBUILD] cameraCount={catalog.cameras.Length} credentialValuesIncluded=false");
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);

            foreach (var filePath in Directory.GetFiles(sourceDir))
            {
                if (filePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                var destFile = Path.Combine(destinationDir, Path.GetFileName(filePath));
                File.Copy(filePath, destFile, true);
            }

            foreach (var subDir in Directory.GetDirectories(sourceDir))
                CopyDirectory(subDir, Path.Combine(destinationDir, Path.GetFileName(subDir)));
        }
    }
}
