using PortTraining.YardCraneSimulator.SharedNet;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.S7Integration
{
    /// <summary>
    /// Relays the already-safety-filtered output of <see cref="YardCraneS7IntegrationBridge"/>
    /// (limited, normalized, forced-to-zero-when-stale/disconnected) to whatever local process
    /// connects over the loopback WebSocket link. This component makes no PLC or safety
    /// decisions of its own - it only serializes values the Bridge has already computed, at a
    /// fixed cadence, so the main training app can read them without holding any S7 connection
    /// itself (see 场桥S7直连接入架构决策-2026-09-15.md: this scene is the only place allowed to
    /// hold the S7 connection; everything downstream only ever consumes derived values).
    /// </summary>
    [DefaultExecutionOrder(-9000)]
    [DisallowMultipleComponent]
    public sealed class YardCraneS7CommandWebSocketServer : MonoBehaviour
    {
        public const string ImplementationVersion = "2026-09-16-yc-s7-link-server-v2-discrete-signals";

        [Header("Source")]
        [SerializeField] private YardCraneS7IntegrationBridge bridge;

        [Header("Link")]
        [SerializeField, Min(1)] private int port = YardCraneS7LinkProtocol.DefaultPort;
        [SerializeField, Min(0.01f)] private float broadcastIntervalSeconds = 0.05f;

        [Header("Runtime diagnostics (read only)")]
        [SerializeField] private bool clientConnected;
        [SerializeField] private long framesSent;

        private YardCraneMiniWebSocketServer server;
        private long sequence;
        private float secondsSinceLastBroadcast;

        public bool HasClient => clientConnected;
        public long FramesSent => framesSent;

        private void Awake()
        {
            if (bridge == null)
            {
                Debug.LogError(
                    "[YC_S7_LINK_SERVER_MISSING_BRIDGE] Assign the YardCraneS7IntegrationBridge reference in the Inspector.",
                    this);
                enabled = false;
                return;
            }

            server = new YardCraneMiniWebSocketServer();
            try
            {
                server.Start(port);
                Debug.Log(
                    "[YC_S7_LINK_SERVER_READY] version=" + ImplementationVersion + ", port=" + port +
                    ", broadcastIntervalSeconds=" + broadcastIntervalSeconds,
                    this);
            }
            catch (System.Exception exception)
            {
                Debug.LogError(
                    "[YC_S7_LINK_SERVER_START_FAILED] port=" + port + ", " + exception.GetType().Name + ": " + exception.Message,
                    this);
                server = null;
                enabled = false;
            }
        }

        private void Update()
        {
            if (server == null)
            {
                return;
            }

            clientConnected = server.HasClient;

            secondsSinceLastBroadcast += Time.unscaledDeltaTime;
            if (secondsSinceLastBroadcast < broadcastIntervalSeconds)
            {
                return;
            }

            secondsSinceLastBroadcast = 0f;
            // ConsumeMomentaryPressedMask() drains the Bridge's accumulator - this component is
            // the ONLY caller, by design (see that method's own doc comment); calling it from
            // anywhere else would silently steal button presses from this broadcast loop.
            var frame = new YardCraneS7LinkFrame(
                ++sequence,
                bridge.GantryCommandNormalized,
                bridge.TrolleyCommandNormalized,
                bridge.CommandFrameValid,
                bridge.ConsumeMomentaryPressedMask(),
                bridge.EmergencyStopEngaged);
            if (server.TryBroadcastText(YardCraneS7LinkProtocol.Encode(frame)))
            {
                framesSent++;
            }
        }

        private void OnDestroy()
        {
            server?.Dispose();
            server = null;
        }

        private void OnApplicationQuit()
        {
            server?.Dispose();
            server = null;
        }
    }
}
