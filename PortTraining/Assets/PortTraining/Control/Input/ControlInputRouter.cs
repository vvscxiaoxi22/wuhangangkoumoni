using System;
using PortTraining.Control.EmbeddedGateway;
using PortTraining.Core.Bootstrap;
using PortTraining.Core.Station;
using UnityEngine;

namespace PortTraining.Control.Input
{
    [DefaultExecutionOrder(-700)]
    public sealed class ControlInputRouter : MonoBehaviour
    {
        private IControlInputProvider desktopProvider;
        private IControlInputProvider consoleProvider;
        private IControlInputProvider activeProvider;

        public string ActiveSourceId => activeProvider?.SourceId ?? "Unavailable";
        public OperatorInputSnapshot Latest { get; private set; }
        public event Action<OperatorInputSnapshot> InputUpdated;

        private void Start()
        {
            desktopProvider = new DesktopSimulationInputProvider();
            consoleProvider = new GatewayConsoleInputProvider(FindAnyObjectByType<EmbeddedGatewayClient>());
            SelectProvider();
        }

        private void Update()
        {
            SelectProvider();
            Latest = activeProvider?.Read() ?? new OperatorInputSnapshot
            {
                sourceId = "Unavailable",
                available = false,
                capturedAt = DateTime.UtcNow.ToString("O")
            };
            InputUpdated?.Invoke(Latest);
        }

        private void SelectProvider()
        {
            var station = PortTrainingBootstrap.CurrentStation;
            if (station == null)
            {
                activeProvider = null;
                return;
            }

            if (station.DeploymentMode == ClientDeploymentMode.DesktopSimulation)
            {
                activeProvider = desktopProvider;
                return;
            }

            if (consoleProvider?.IsAvailable == true)
            {
                activeProvider = consoleProvider;
                return;
            }

            activeProvider = station.AllowSimulationFallback ? desktopProvider : null;
        }
    }
}

