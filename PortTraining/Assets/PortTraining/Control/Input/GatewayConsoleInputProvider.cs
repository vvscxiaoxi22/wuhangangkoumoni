using System;
using PortTraining.Control.Gateway;

namespace PortTraining.Control.Input
{
    public sealed class GatewayConsoleInputProvider : IControlInputProvider
    {
        private readonly IEquipmentGatewayClient gateway;

        public GatewayConsoleInputProvider(IEquipmentGatewayClient gateway)
        {
            this.gateway = gateway;
        }

        public string SourceId => "PhysicalConsole";
        public bool IsAvailable => gateway?.Latest?.gatewayOnline == true && gateway.Latest.plcOnline;

        public OperatorInputSnapshot Read()
        {
            var snapshot = gateway?.Latest;
            if (snapshot == null)
            {
                return new OperatorInputSnapshot
                {
                    sourceId = SourceId,
                    available = false,
                    capturedAt = DateTime.UtcNow.ToString("O")
                };
            }

            return new OperatorInputSnapshot
            {
                sourceId = SourceId,
                available = IsAvailable,
                gantry = snapshot.gantryCommand,
                trolley = snapshot.trolleyCommand,
                hoist = snapshot.hoistCommand,
                horn = snapshot.hornPressed,
                spreaderLock = snapshot.spreaderLockPressed,
                spreaderUnlock = snapshot.spreaderUnlockPressed,
                controlPowerOn = snapshot.controlPowerOn,
                emergencyStopReleased = snapshot.emergencyStopReleased,
                handlesCentered = snapshot.handlesCentered,
                sequence = snapshot.sequence,
                capturedAt = snapshot.capturedAt
            };
        }
    }
}

