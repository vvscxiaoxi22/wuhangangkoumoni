using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace HikvisionFourDisplay.Runtime.Configuration
{
    [Serializable]
    public sealed class FeedConfigEntry
    {
        public string feedId = string.Empty;
        public int displayIndex = -1;
        public string host = string.Empty;
        public int rtspPort = 554;
        public string channel = "101";
        public string streamType = "Main";
        public string credentialKey = string.Empty;
        public bool enabled;
        public int rotationDegrees;
    }

    [Serializable]
    public sealed class FeedConfigurationDocument
    {
        public int configVersion = 1;
        public int canvasDisplayIndex = -1;
        public string activeSchemeId = string.Empty;
        public FeedConfigEntry[] feeds = Array.Empty<FeedConfigEntry>();
    }

    /// <summary>
    /// Loads the non-sensitive per-feed connection config. No username/password/token
    /// field exists in this schema by construction, so this file can never carry a secret.
    /// </summary>
    public static class FeedConfigurationLoader
    {
        public const string ConfigFileName = "feeds.json";
        private const string TemplateFolder = "HikvisionFourDisplay";

        public static string ExternalConfigDirectory =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Config"));

        public static string ExternalConfigPath =>
            Path.Combine(ExternalConfigDirectory, ConfigFileName);

        public static void Save(FeedConfigurationDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (document.feeds == null) throw new InvalidDataException("Feed configuration has no camera list.");
            var warnings = new List<string>();
            ValidateEntries(document, warnings);
            if (warnings.Count > 0)
                throw new InvalidDataException("Feed configuration contains invalid camera or display values.");
            foreach (var entry in document.feeds)
            {
                if (entry.rtspPort < 1 || entry.rtspPort > 65535)
                    throw new InvalidDataException("Feed configuration contains an invalid RTSP port.");
            }

            Directory.CreateDirectory(ExternalConfigDirectory);
            var temporaryPath = ExternalConfigPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(document, true), new UTF8Encoding(false));
                if (File.Exists(ExternalConfigPath)) File.Replace(temporaryPath, ExternalConfigPath, null);
                else File.Move(temporaryPath, ExternalConfigPath);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        public static FeedConfigurationDocument Load(out IReadOnlyList<string> warnings, out string sourcePath)
        {
            var warningList = new List<string>();
            string json;

            if (File.Exists(ExternalConfigPath))
            {
                json = File.ReadAllText(ExternalConfigPath);
                sourcePath = ExternalConfigPath;
            }
            else
            {
                var templatePath = Path.Combine(Application.streamingAssetsPath, TemplateFolder, ConfigFileName.Replace(".json", ".template.json"));
                if (!File.Exists(templatePath))
                {
                    warningList.Add("No external config and no bundled template found; using an empty configuration.");
                    warnings = warningList;
                    sourcePath = null;
                    return new FeedConfigurationDocument();
                }

                json = File.ReadAllText(templatePath);
                sourcePath = templatePath;
                warningList.Add($"External config not found at {ExternalConfigPath}; loaded the bundled default configuration.");
                TrySeedExternalConfig(templatePath, warningList);
            }

            var document = ParseDocument(json, sourcePath, warningList);
            ValidateEntries(document, warningList);

            warnings = warningList;
            return document;
        }

        private static void TrySeedExternalConfig(string templatePath, List<string> warnings)
        {
            try
            {
                Directory.CreateDirectory(ExternalConfigDirectory);
                if (!File.Exists(ExternalConfigPath))
                    File.Copy(templatePath, ExternalConfigPath);
                warnings.Add($"Seeded {ExternalConfigPath} from the template; edit it with real site values.");
            }
            catch (Exception ex)
            {
                warnings.Add($"Could not seed external config file: {ex.Message}");
            }
        }

        private static FeedConfigurationDocument ParseDocument(string json, string sourcePath, List<string> warnings)
        {
            try
            {
                var document = JsonUtility.FromJson<FeedConfigurationDocument>(json);
                if (document == null) throw new InvalidDataException("Parsed document was null.");
                document.feeds ??= Array.Empty<FeedConfigEntry>();
                return document;
            }
            catch (Exception ex)
            {
                warnings.Add($"Failed to parse {sourcePath}: {ex.Message}");
                return new FeedConfigurationDocument();
            }
        }

        private static void ValidateEntries(FeedConfigurationDocument document, List<string> warnings)
        {
            if (document.canvasDisplayIndex < -1 || document.canvasDisplayIndex > 3)
                warnings.Add($"Canvas displayIndex {document.canvasDisplayIndex} is out of range; expected -1 or 0..3.");

            var seenDisplayIndex = new HashSet<int>();
            foreach (var entry in document.feeds)
            {
                if (entry == null)
                {
                    warnings.Add("Feed configuration contains an empty entry.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.feedId))
                    warnings.Add("Feed entry is missing feedId.");

                if (entry.displayIndex < 0 || entry.displayIndex > 3)
                    warnings.Add($"Feed '{entry.feedId}' has out-of-range displayIndex {entry.displayIndex}.");
                else if (!seenDisplayIndex.Add(entry.displayIndex))
                    warnings.Add($"Duplicate displayIndex {entry.displayIndex} on feed '{entry.feedId}'.");

                if (entry.enabled && string.IsNullOrWhiteSpace(entry.host))
                    warnings.Add($"Feed '{entry.feedId}' is enabled but has no host.");

                if (entry.enabled && string.IsNullOrWhiteSpace(entry.credentialKey))
                    warnings.Add($"Feed '{entry.feedId}' is enabled but has no credentialKey.");

                if (entry.rotationDegrees != 0 && entry.rotationDegrees != 90 &&
                    entry.rotationDegrees != 180 && entry.rotationDegrees != 270)
                {
                    warnings.Add($"Feed '{entry.feedId}' has an invalid rotation; using 0 degrees.");
                    entry.rotationDegrees = 0;
                }
            }
        }
    }
}
