using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace HikvisionFourDisplay.Runtime.Configuration
{
    [Serializable]
    public sealed class FeedSchemeEntry
    {
        public string id = string.Empty;
        public string name = string.Empty;
        public FeedConfigurationDocument configuration;
    }

    [Serializable]
    public sealed class FeedSchemeDocument
    {
        public int configVersion = 1;
        public FeedSchemeEntry[] schemes = Array.Empty<FeedSchemeEntry>();
    }

    /// <summary>Stores named four-display configurations, including credential references only.</summary>
    public static class FeedSchemeStore
    {
        [Serializable]
        private sealed class FileContents
        {
            public int configVersion = 1;
            public FeedSchemeEntry[] schemes;
        }

        public const string ConfigFileName = "schemes.json";
        public static string ExternalConfigPath =>
            Path.Combine(FeedConfigurationLoader.ExternalConfigDirectory, ConfigFileName);

        public static FeedSchemeDocument Load(out IReadOnlyList<string> warnings)
        {
            var warningList = new List<string>();
            warnings = warningList;
            if (!File.Exists(ExternalConfigPath)) return new FeedSchemeDocument();

            FileContents contents;
            try
            {
                contents = JsonUtility.FromJson<FileContents>(File.ReadAllText(ExternalConfigPath));
            }
            catch (Exception ex) when (ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException)
            {
                // Do not include parser errors or file contents in operator messages/logs.
                throw new InvalidDataException("Unable to read schemes.json. The existing scheme file has been preserved.");
            }

            if (contents == null || contents.schemes == null)
                throw new InvalidDataException("schemes.json does not contain a valid scheme list. The existing file has been preserved.");

            var document = new FeedSchemeDocument
            {
                configVersion = contents.configVersion,
                schemes = contents.schemes
            };
            ValidateDocument(document);
            return document;
        }

        public static FeedSchemeEntry SaveOrUpdate(string name, FeedConfigurationDocument configuration)
        {
            var trimmedName = (name ?? string.Empty).Trim();
            if (trimmedName.Length == 0) throw new ArgumentException("A scheme name is required.", nameof(name));
            var configurationCopy = CloneConfiguration(configuration);
            ValidateConfiguration(configurationCopy);
            var document = Load(out _);
            var entries = new List<FeedSchemeEntry>(document.schemes);
            FeedSchemeEntry savedEntry = null;
            for (var i = 0; i < entries.Count; i++)
            {
                if (!string.Equals(entries[i].name.Trim(), trimmedName, StringComparison.OrdinalIgnoreCase)) continue;
                savedEntry = new FeedSchemeEntry
                {
                    id = entries[i].id,
                    name = trimmedName,
                    configuration = configurationCopy
                };
                entries[i] = savedEntry;
                break;
            }

            if (savedEntry == null)
            {
                savedEntry = new FeedSchemeEntry
                {
                    id = Guid.NewGuid().ToString("N"),
                    name = trimmedName,
                    configuration = configurationCopy
                };
                entries.Add(savedEntry);
            }

            document.schemes = entries.ToArray();
            Save(document);
            return new FeedSchemeEntry
            {
                id = savedEntry.id,
                name = savedEntry.name,
                configuration = CloneConfiguration(savedEntry.configuration)
            };
        }

        public static bool Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            var document = Load(out _);
            var entries = new List<FeedSchemeEntry>(document.schemes);
            var index = entries.FindIndex(entry => string.Equals(entry.id, id, StringComparison.Ordinal));
            if (index < 0) return false;
            entries.RemoveAt(index);
            document.schemes = entries.ToArray();
            Save(document);
            return true;
        }

        public static bool TryGet(FeedSchemeDocument document, string id, out FeedConfigurationDocument configuration)
        {
            configuration = null;
            if (document?.schemes == null || string.IsNullOrWhiteSpace(id)) return false;
            foreach (var entry in document.schemes)
            {
                if (entry == null || !string.Equals(entry.id, id, StringComparison.Ordinal)) continue;
                configuration = CloneConfiguration(entry.configuration);
                return true;
            }

            return false;
        }

        public static FeedConfigurationDocument CloneConfiguration(FeedConfigurationDocument configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            var sourceEntries = configuration.feeds ?? Array.Empty<FeedConfigEntry>();
            var entries = new FeedConfigEntry[sourceEntries.Length];
            for (var i = 0; i < sourceEntries.Length; i++)
            {
                var entry = sourceEntries[i];
                if (entry == null) continue;
                entries[i] = new FeedConfigEntry
                {
                    feedId = entry.feedId,
                    displayIndex = entry.displayIndex,
                    host = entry.host,
                    rtspPort = entry.rtspPort,
                    channel = entry.channel,
                    streamType = entry.streamType,
                    credentialKey = entry.credentialKey,
                    enabled = entry.enabled,
                    rotationDegrees = entry.rotationDegrees
                };
            }

            return new FeedConfigurationDocument
            {
                configVersion = configuration.configVersion,
                canvasDisplayIndex = configuration.canvasDisplayIndex,
                activeSchemeId = configuration.activeSchemeId,
                feeds = entries
            };
        }

        public static void Save(FeedSchemeDocument document)
        {
            ValidateDocument(document);
            // Refuse to replace an unreadable/invalid existing file, even when the caller holds an old list.
            if (File.Exists(ExternalConfigPath)) Load(out _);
            Directory.CreateDirectory(FeedConfigurationLoader.ExternalConfigDirectory);
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

        private static void ValidateDocument(FeedSchemeDocument document)
        {
            if (document == null || document.configVersion != 1 || document.schemes == null)
                throw new InvalidDataException("The scheme file has an unsupported format. The existing file has been preserved.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in document.schemes)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id) || string.IsNullOrWhiteSpace(entry.name) ||
                    !ids.Add(entry.id) || !names.Add(entry.name.Trim()))
                    throw new InvalidDataException("The scheme list contains an invalid or duplicate entry. The existing file has been preserved.");
                ValidateConfiguration(entry.configuration);
            }
        }

        private static void ValidateConfiguration(FeedConfigurationDocument configuration)
        {
            if (configuration == null || configuration.configVersion != 1 || configuration.feeds == null ||
                configuration.feeds.Length != 4 ||
                configuration.canvasDisplayIndex < -1 || configuration.canvasDisplayIndex > 3)
                throw new InvalidDataException("A scheme contains an invalid display configuration.");
            var displayIndices = new HashSet<int>();
            foreach (var entry in configuration.feeds)
            {
                if (entry == null || entry.displayIndex < 0 || entry.displayIndex > 3 ||
                    !displayIndices.Add(entry.displayIndex) || string.IsNullOrWhiteSpace(entry.feedId) ||
                    entry.rtspPort < 1 || entry.rtspPort > 65535 ||
                    (entry.rotationDegrees != 0 && entry.rotationDegrees != 90 &&
                     entry.rotationDegrees != 180 && entry.rotationDegrees != 270) ||
                    (entry.enabled && (string.IsNullOrWhiteSpace(entry.host) || string.IsNullOrWhiteSpace(entry.credentialKey))))
                    throw new InvalidDataException("A scheme contains an invalid camera configuration.");
            }
        }
    }
}
