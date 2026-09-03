using System;
using System.IO;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Configuration
{
    [Serializable]
    public sealed class YardCraneSceneConfig
    {
        public string schemaVersion;
        public string machineType;
        public string sceneProfileId;
        public string pointMapVersion;
        public string protocolVersion;
        public string defaultDisplayProfileId;
        public string operationalSceneName;
        public string[] supportedDisplayProfileIds;
        public string[] supportedControlModes;
        public int realtimeFrameIntervalMs;

        public static bool TryLoad(out YardCraneSceneConfig config, out string error)
        {
            config = null;
            error = string.Empty;

            var path = Path.Combine(Application.streamingAssetsPath, "Config", "yard-crane-scene.json");
            if (!File.Exists(path))
            {
                error = $"Yard-crane scene configuration was not found: {path}";
                return false;
            }

            try
            {
                config = JsonUtility.FromJson<YardCraneSceneConfig>(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                error = $"Yard-crane scene configuration is invalid: {exception.Message}";
                return false;
            }

            if (config == null || !string.Equals(config.machineType, "YC", StringComparison.Ordinal))
            {
                error = "Yard-crane scene configuration must declare machineType 'YC'.";
                return false;
            }

            if (!string.Equals(config.protocolVersion, "1.1.0", StringComparison.Ordinal))
            {
                error = "Yard-crane scene configuration must declare protocolVersion '1.1.0'.";
                return false;
            }

            if (config.realtimeFrameIntervalMs < 10 || config.realtimeFrameIntervalMs > 100)
            {
                error = "realtimeFrameIntervalMs must be between 10 and 100 milliseconds.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(config.operationalSceneName))
            {
                error = "Yard-crane scene configuration must declare operationalSceneName.";
                return false;
            }

            return true;
        }
    }
}
