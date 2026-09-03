using System;
using UnityEngine;

namespace PortTraining.Control.Input
{
    public sealed class DesktopSimulationInputProvider : IControlInputProvider
    {
        private long sequence;

        public string SourceId => "DesktopSimulation";
        public bool IsAvailable => true;

        public OperatorInputSnapshot Read()
        {
            var gantry = GetAxis(KeyCode.A, KeyCode.D);
            var trolley = GetAxis(KeyCode.S, KeyCode.W);
            var hoist = GetAxis(KeyCode.Q, KeyCode.E);

            return new OperatorInputSnapshot
            {
                sourceId = SourceId,
                available = true,
                gantry = gantry,
                trolley = trolley,
                hoist = hoist,
                horn = UnityEngine.Input.GetKey(KeyCode.Space),
                spreaderLock = UnityEngine.Input.GetKeyDown(KeyCode.L),
                spreaderUnlock = UnityEngine.Input.GetKeyDown(KeyCode.U),
                controlPowerOn = true,
                emergencyStopReleased = true,
                handlesCentered = Mathf.Approximately(gantry, 0f) && Mathf.Approximately(trolley, 0f) && Mathf.Approximately(hoist, 0f),
                sequence = ++sequence,
                capturedAt = DateTime.UtcNow.ToString("O")
            };
        }

        private static float GetAxis(KeyCode negative, KeyCode positive)
        {
            var value = 0f;
            if (UnityEngine.Input.GetKey(negative))
            {
                value -= 1f;
            }

            if (UnityEngine.Input.GetKey(positive))
            {
                value += 1f;
            }

            return value;
        }
    }
}

