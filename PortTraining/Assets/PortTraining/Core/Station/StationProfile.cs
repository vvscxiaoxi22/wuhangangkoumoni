using System;
using System.Collections.Generic;

namespace PortTraining.Core.Station
{
    public enum MachineType
    {
        QC,
        YC
    }

    public enum ControlMode
    {
        Local,
        Remote
    }

    public enum ClientDeploymentMode
    {
        DesktopSimulation,
        SimulatorCabin
    }

    public enum GatewayTransportMode
    {
        Simulated,
        S7
    }

    [Serializable]
    public sealed class StationProfileData
    {
        public string stationId = string.Empty;
        public string displayName = string.Empty;
        public string machineType = string.Empty;
        public string consoleProfileId = string.Empty;
        public string gatewayEndpoint = string.Empty;
        public string trainingNodeEndpoint = string.Empty;
        public string plcProfileId = string.Empty;
        public string pointMapVersion = string.Empty;
        public string sceneProfileId = string.Empty;
        public string deploymentMode = "DesktopSimulation";
        public string gatewayTransport = "Simulated";
        public string inputProfileId = string.Empty;
        public string displayProfileId = string.Empty;
        public bool allowSimulationFallback = true;
        public string[] supportedControlModes = Array.Empty<string>();
        public string[] supportedTaskTypes = Array.Empty<string>();
    }

    public sealed class StationProfile
    {
        public string StationId { get; }
        public string DisplayName { get; }
        public MachineType MachineType { get; }
        public string ConsoleProfileId { get; }
        public string GatewayEndpoint { get; }
        public string TrainingNodeEndpoint { get; }
        public string PlcProfileId { get; }
        public string PointMapVersion { get; }
        public string SceneProfileId { get; }
        public ClientDeploymentMode DeploymentMode { get; }
        public GatewayTransportMode GatewayTransport { get; }
        public string InputProfileId { get; }
        public string DisplayProfileId { get; }
        public bool AllowSimulationFallback { get; }
        public IReadOnlyList<ControlMode> SupportedControlModes { get; }
        public IReadOnlyList<string> SupportedTaskTypes { get; }

        private StationProfile(
            StationProfileData data,
            MachineType machineType,
            ClientDeploymentMode deploymentMode,
            GatewayTransportMode gatewayTransport,
            IReadOnlyList<ControlMode> controlModes)
        {
            StationId = data.stationId.Trim();
            DisplayName = data.displayName.Trim();
            MachineType = machineType;
            ConsoleProfileId = data.consoleProfileId.Trim();
            GatewayEndpoint = data.gatewayEndpoint.Trim();
            TrainingNodeEndpoint = data.trainingNodeEndpoint.Trim();
            PlcProfileId = data.plcProfileId.Trim();
            PointMapVersion = data.pointMapVersion.Trim();
            SceneProfileId = data.sceneProfileId.Trim();
            DeploymentMode = deploymentMode;
            GatewayTransport = gatewayTransport;
            InputProfileId = data.inputProfileId.Trim();
            DisplayProfileId = data.displayProfileId.Trim();
            AllowSimulationFallback = data.allowSimulationFallback;
            SupportedControlModes = controlModes;
            SupportedTaskTypes = data.supportedTaskTypes ?? Array.Empty<string>();
        }

        public static StationProfile Create(StationProfileData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            Require(data.stationId, nameof(data.stationId));
            Require(data.displayName, nameof(data.displayName));
            Require(data.machineType, nameof(data.machineType));
            Require(data.consoleProfileId, nameof(data.consoleProfileId));
            Require(data.gatewayEndpoint, nameof(data.gatewayEndpoint));
            Require(data.trainingNodeEndpoint, nameof(data.trainingNodeEndpoint));
            Require(data.plcProfileId, nameof(data.plcProfileId));
            Require(data.pointMapVersion, nameof(data.pointMapVersion));
            Require(data.sceneProfileId, nameof(data.sceneProfileId));
            Require(data.deploymentMode, nameof(data.deploymentMode));
            Require(data.gatewayTransport, nameof(data.gatewayTransport));
            Require(data.inputProfileId, nameof(data.inputProfileId));
            Require(data.displayProfileId, nameof(data.displayProfileId));

            if (!Enum.TryParse(data.machineType, true, out MachineType machineType))
            {
                throw new InvalidOperationException($"Unsupported machineType '{data.machineType}'.");
            }

            if (!Enum.TryParse(data.deploymentMode, true, out ClientDeploymentMode deploymentMode))
            {
                throw new InvalidOperationException($"Unsupported deploymentMode '{data.deploymentMode}'.");
            }

            if (!Enum.TryParse(data.gatewayTransport, true, out GatewayTransportMode gatewayTransport))
            {
                throw new InvalidOperationException($"Unsupported gatewayTransport '{data.gatewayTransport}'.");
            }

            var modes = new List<ControlMode>();
            foreach (var value in data.supportedControlModes ?? Array.Empty<string>())
            {
                if (!Enum.TryParse(value, true, out ControlMode mode))
                {
                    throw new InvalidOperationException($"Unsupported control mode '{value}'.");
                }

                if (!modes.Contains(mode))
                {
                    modes.Add(mode);
                }
            }

            if (modes.Count == 0)
            {
                throw new InvalidOperationException("At least one control mode is required.");
            }

            return new StationProfile(data, machineType, deploymentMode, gatewayTransport, modes);
        }

        private static void Require(string value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Station profile field '{fieldName}' is required.");
            }
        }
    }
}
