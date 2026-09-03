using System;
using System.Collections.Generic;

namespace PortTraining.YardCraneSimulator.Bootstrap
{
    public enum YardCraneLaunchMode
    {
        Managed = 0,
        Standalone = 1,
        EditorDevelopment = 2
    }

    public sealed class SceneLaunchOptions
    {
        public const string SupportedProtocolVersion = "1.1.0";
        public const string SessionTokenEnvironmentVariable = "PORTTRAINING_SESSION_TOKEN";

        public string StationId { get; private set; }
        public Guid OperationRunId { get; private set; }
        public Guid SceneInstanceId { get; private set; }
        public string CoordinatorPipe { get; private set; }
        public string GatewayPipe { get; private set; }
        public Guid GatewayLeaseId { get; private set; }
        public string DisplayProfile { get; private set; }
        public string SceneProfile { get; private set; }
        public string ProtocolVersion { get; private set; }
        public string SessionToken { get; private set; }
        public bool IsEditorDevelopmentSession { get; private set; }
        public YardCraneLaunchMode LaunchMode { get; private set; }
        public bool IsStandaloneSession => LaunchMode == YardCraneLaunchMode.Standalone;

        private SceneLaunchOptions()
        {
        }

        public static bool TryParse(
            string[] commandLineArgs,
            Func<string, string> environmentReader,
            out SceneLaunchOptions options,
            out string error)
        {
            options = null;
            error = string.Empty;

            if (commandLineArgs == null)
            {
                error = "Command-line arguments are unavailable.";
                return false;
            }

            var values = ParsePairs(commandLineArgs, out error);
            if (values == null)
            {
                return false;
            }

            if (!TryRequired(values, "--station-id", out var stationId, out error) ||
                !TryGuid(values, "--operation-run-id", out var operationRunId, out error) ||
                !TryGuid(values, "--scene-instance-id", out var sceneInstanceId, out error) ||
                !TryRequired(values, "--coordinator-pipe", out var coordinatorPipe, out error) ||
                !TryRequired(values, "--gateway-pipe", out var gatewayPipe, out error) ||
                !TryGuid(values, "--gateway-lease-id", out var gatewayLeaseId, out error) ||
                !TryRequired(values, "--display-profile", out var displayProfile, out error) ||
                !TryRequired(values, "--scene-profile", out var sceneProfile, out error) ||
                !TryRequired(values, "--protocol-version", out var protocolVersion, out error))
            {
                return false;
            }

            if (!string.Equals(protocolVersion, SupportedProtocolVersion, StringComparison.Ordinal))
            {
                error = $"Unsupported protocol version '{protocolVersion}'. Expected '{SupportedProtocolVersion}'.";
                return false;
            }

            var sessionToken = environmentReader?.Invoke(SessionTokenEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(sessionToken) || sessionToken.Length < 43)
            {
                error = $"Environment variable {SessionTokenEnvironmentVariable} must contain a 256-bit one-time token.";
                return false;
            }

            options = new SceneLaunchOptions
            {
                StationId = stationId,
                OperationRunId = operationRunId,
                SceneInstanceId = sceneInstanceId,
                CoordinatorPipe = coordinatorPipe,
                GatewayPipe = gatewayPipe,
                GatewayLeaseId = gatewayLeaseId,
                DisplayProfile = displayProfile,
                SceneProfile = sceneProfile,
                ProtocolVersion = protocolVersion,
                SessionToken = sessionToken,
                IsEditorDevelopmentSession = false,
                LaunchMode = YardCraneLaunchMode.Managed
            };

            return true;
        }

        public static bool TryParseRuntime(
            string[] commandLineArgs,
            Func<string, string> environmentReader,
            string defaultSceneProfile,
            bool editorDevelopment,
            out SceneLaunchOptions options,
            out string error)
        {
            options = null;
            if (commandLineArgs == null)
            {
                error = "Command-line arguments are unavailable.";
                return false;
            }

            var values = ParsePairs(commandLineArgs, out error);
            if (values == null)
            {
                return false;
            }

            if (values.TryGetValue("--run-mode", out var requestedMode))
            {
                if (string.Equals(requestedMode, "managed", StringComparison.OrdinalIgnoreCase))
                {
                    return TryParse(commandLineArgs, environmentReader, out options, out error);
                }

                if (!string.Equals(requestedMode, "standalone", StringComparison.OrdinalIgnoreCase))
                {
                    error = $"Unsupported run mode '{requestedMode}'. Expected 'standalone' or 'managed'.";
                    return false;
                }

                options = CreateStandaloneDefaults(defaultSceneProfile, values);
                error = string.Empty;
                return true;
            }

            if (ContainsManagedArguments(values))
            {
                return TryParse(commandLineArgs, environmentReader, out options, out error);
            }

            options = editorDevelopment
                ? CreateEditorDevelopmentDefaults(defaultSceneProfile)
                : CreateStandaloneDefaults(defaultSceneProfile, values);
            error = string.Empty;
            return true;
        }

        public static SceneLaunchOptions CreateEditorDevelopmentDefaults(string sceneProfile = "YC-SCENE-DEV")
        {
            return new SceneLaunchOptions
            {
                StationId = "YC-EDITOR-01",
                OperationRunId = Guid.Empty,
                SceneInstanceId = Guid.Empty,
                CoordinatorPipe = "pt.operation.editor",
                GatewayPipe = "pt.gateway.editor",
                GatewayLeaseId = Guid.Empty,
                DisplayProfile = "DESKTOP-16X9-V1",
                SceneProfile = sceneProfile,
                ProtocolVersion = SupportedProtocolVersion,
                SessionToken = string.Empty,
                IsEditorDevelopmentSession = true,
                LaunchMode = YardCraneLaunchMode.EditorDevelopment
            };
        }

        private static SceneLaunchOptions CreateStandaloneDefaults(
            string defaultSceneProfile,
            IReadOnlyDictionary<string, string> values)
        {
            var stationId = values.TryGetValue("--station-id", out var station) && !string.IsNullOrWhiteSpace(station)
                ? station
                : "YC-STANDALONE-01";
            var displayProfile = values.TryGetValue("--display-profile", out var display) && !string.IsNullOrWhiteSpace(display)
                ? display
                : "DESKTOP-16X9-V1";
            var sceneProfile = values.TryGetValue("--scene-profile", out var scene) && !string.IsNullOrWhiteSpace(scene)
                ? scene
                : defaultSceneProfile;

            return new SceneLaunchOptions
            {
                StationId = stationId,
                OperationRunId = Guid.Empty,
                SceneInstanceId = Guid.Empty,
                CoordinatorPipe = string.Empty,
                GatewayPipe = string.Empty,
                GatewayLeaseId = Guid.Empty,
                DisplayProfile = displayProfile,
                SceneProfile = sceneProfile,
                ProtocolVersion = SupportedProtocolVersion,
                SessionToken = string.Empty,
                IsEditorDevelopmentSession = false,
                LaunchMode = YardCraneLaunchMode.Standalone
            };
        }

        private static bool ContainsManagedArguments(IReadOnlyDictionary<string, string> values)
        {
            return values.ContainsKey("--operation-run-id") ||
                   values.ContainsKey("--scene-instance-id") ||
                   values.ContainsKey("--coordinator-pipe") ||
                   values.ContainsKey("--gateway-pipe") ||
                   values.ContainsKey("--gateway-lease-id");
        }

        private static Dictionary<string, string> ParsePairs(string[] args, out string error)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            error = string.Empty;

            for (var index = 0; index < args.Length; index++)
            {
                var key = args[index];
                if (string.IsNullOrWhiteSpace(key) || !key.StartsWith("--", StringComparison.Ordinal))
                {
                    continue;
                }

                if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    error = $"Missing value for startup argument '{key}'.";
                    return null;
                }

                if (result.ContainsKey(key))
                {
                    error = $"Duplicate startup argument '{key}'.";
                    return null;
                }

                result.Add(key, args[++index]);
            }

            return result;
        }

        private static bool TryRequired(
            IReadOnlyDictionary<string, string> values,
            string key,
            out string value,
            out string error)
        {
            if (!values.TryGetValue(key, out value) || string.IsNullOrWhiteSpace(value))
            {
                error = $"Required startup argument '{key}' is missing.";
                value = string.Empty;
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryGuid(
            IReadOnlyDictionary<string, string> values,
            string key,
            out Guid value,
            out string error)
        {
            value = Guid.Empty;
            if (!TryRequired(values, key, out var raw, out error))
            {
                return false;
            }

            if (!Guid.TryParse(raw, out value) || value == Guid.Empty)
            {
                error = $"Startup argument '{key}' must be a non-empty UUID.";
                return false;
            }

            return true;
        }
    }
}
