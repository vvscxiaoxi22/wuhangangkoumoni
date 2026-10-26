using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HikvisionFourDisplay.Runtime.Configuration
{
    [Serializable]
    public sealed class CameraCatalogEntry
    {
        public string id = string.Empty;
        public string name = string.Empty;
        public string host = string.Empty;
        public int rtspPort = 554;
        public string channel = "1";
        public string streamType = "Main";
        public string credentialKey = string.Empty;
    }

    [Serializable]
    public sealed class CameraCatalogDocument
    {
        public int configVersion = 1;
        public CameraCatalogEntry[] cameras = Array.Empty<CameraCatalogEntry>();
    }

    /// <summary>Loads named site cameras without storing login credentials.</summary>
    public static class CameraCatalogLoader
    {
        [Serializable]
        private sealed class FileContents
        {
            public int configVersion = 1;
            public CameraCatalogEntry[] cameras;
        }

        public const string ConfigFileName = "cameras.json";
        public static string ExternalConfigPath =>
            Path.Combine(FeedConfigurationLoader.ExternalConfigDirectory, ConfigFileName);

        public static CameraCatalogDocument Load(out IReadOnlyList<string> warnings, out string sourcePath)
        {
            var warningList = new List<string>();
            warnings = warningList;
            sourcePath = ExternalConfigPath;
            if (!File.Exists(sourcePath))
            {
                sourcePath = Path.Combine(Application.streamingAssetsPath, "HikvisionFourDisplay", ConfigFileName);
                if (!File.Exists(sourcePath))
                {
                    warningList.Add("Camera catalog was not found; manual IP entry is available.");
                    sourcePath = null;
                    return new CameraCatalogDocument();
                }

                warningList.Add("External camera catalog was not found; using the bundled camera list.");
            }

            FileContents contents;
            try
            {
                contents = JsonUtility.FromJson<FileContents>(File.ReadAllText(sourcePath));
            }
            catch (Exception ex) when (ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new InvalidDataException("Unable to read cameras.json. The existing camera catalog has been preserved.");
            }

            if (contents == null || contents.configVersion != 1 || contents.cameras == null)
                throw new InvalidDataException("cameras.json has an unsupported format. The existing camera catalog has been preserved.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in contents.cameras)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id) || string.IsNullOrWhiteSpace(entry.name) ||
                    string.IsNullOrWhiteSpace(entry.host) || string.IsNullOrWhiteSpace(entry.channel) ||
                    string.IsNullOrWhiteSpace(entry.credentialKey) || entry.rtspPort < 1 || entry.rtspPort > 65535 ||
                    !ids.Add(entry.id))
                    throw new InvalidDataException("cameras.json contains an invalid or duplicate camera. The existing catalog has been preserved.");
            }

            return new CameraCatalogDocument { configVersion = contents.configVersion, cameras = contents.cameras };
        }
    }
}
