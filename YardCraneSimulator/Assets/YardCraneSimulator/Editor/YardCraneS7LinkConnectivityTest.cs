using System.Threading;
using PortTraining.YardCraneSimulator.SharedNet;
using UnityEditor;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Editor
{
    /// <summary>
    /// A manual, Edit-mode diagnostic for the S7 loopback link - deliberately independent of
    /// YardCraneS7InputSource/YardCraneStandaloneOperationHost (which are only ever exercised
    /// while a training run is Configure()'d). This exists to answer one question in isolation:
    /// "is the standalone YardCraneS7Integration player's WebSocket server actually reachable
    /// and broadcasting frames from this machine" - without needing to enter Play, without
    /// touching training/session state, and without deciding anything about requireS7Link.
    /// </summary>
    public static class YardCraneS7LinkConnectivityTest
    {
        private const string Host = "127.0.0.1";
        private const int ConnectTimeoutMs = 3000;
        private const int SampleWindowMs = 2000;

        [MenuItem("Tools/Yard Crane/S7/Test WebSocket Link Connectivity")]
        public static void TestConnectivity()
        {
            var port = YardCraneS7LinkProtocol.DefaultPort;
            Debug.Log($"[YC_S7_LINK_TEST] Connecting to {Host}:{port} (timeout={ConnectTimeoutMs}ms)...");

            var client = new YardCraneMiniWebSocketClient();
            var frameCount = 0;
            var haveFrame = false;
            var lastFrame = default(YardCraneS7LinkFrame);

            client.TextReceived += text =>
            {
                if (YardCraneS7LinkProtocol.TryDecode(text, out var frame))
                {
                    frameCount++;
                    lastFrame = frame;
                    haveFrame = true;
                }
                else
                {
                    Debug.LogWarning($"[YC_S7_LINK_TEST] Received a frame that failed to decode: '{text}'");
                }
            };
            client.Disconnected += () => Debug.LogWarning("[YC_S7_LINK_TEST] Link disconnected.");

            if (!client.TryConnect(Host, port, ConnectTimeoutMs, out var connectError))
            {
                Debug.LogError(
                    $"[YC_S7_LINK_TEST_FAILED] Could not reach {Host}:{port} - {connectError}. " +
                    "Check that the standalone YardCraneS7Integration player is running and that its " +
                    "YardCraneS7CommandWebSocketServer component started (look for " +
                    "[YC_S7_LINK_SERVER_READY] in that player's own log/Console).");
                client.Dispose();
                return;
            }

            Debug.Log($"[YC_S7_LINK_TEST] WebSocket handshake succeeded. Sampling for {SampleWindowMs}ms " +
                      "(this call blocks the Editor briefly by design - it is a one-shot manual check, not a live monitor).");
            Thread.Sleep(SampleWindowMs);

            if (haveFrame)
            {
                Debug.Log(
                    $"[YC_S7_LINK_TEST_PASS] framesReceived={frameCount}, latest: sequence={lastFrame.Sequence}, " +
                    $"gantry={lastFrame.GantryNormalized:F4}, trolley={lastFrame.TrolleyNormalized:F4}, " +
                    $"valid={lastFrame.Valid}. Two Unity processes on this machine are talking over the link.");
            }
            else
            {
                Debug.LogWarning(
                    "[YC_S7_LINK_TEST] Connected, but received no frames within the sample window. Check the " +
                    "server's broadcastIntervalSeconds and that its bridge field is assigned in the Inspector - " +
                    "a null bridge reference makes YardCraneS7CommandWebSocketServer disable itself on Awake.");
            }

            client.Dispose();
        }
    }
}
