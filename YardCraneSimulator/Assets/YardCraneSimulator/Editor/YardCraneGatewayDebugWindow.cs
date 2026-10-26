using System;
using System.Text;
using System.Threading;
using PortTraining.YardCraneSimulator.Gateway;
using UnityEditor;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Editor
{
    // Manual PT-IF-007 connectivity test tool. Values here must come from a
    // gateway.session.prepare call already completed on PortGatewayService's control
    // pipe (no scene launcher exists yet to automate that step - run a manual tool on
    // the PortGatewayService side first and copy its leaseId/sessionToken/
    // sceneInstanceId/operationRunId in here). Deliberately isolated from the real
    // driving/input pipeline: this window does not touch YardCraneOperationController,
    // YardCraneInputRouter, or any gameplay code. It only proves the wire connection
    // and message shapes work.
    public sealed class YardCraneGatewayDebugWindow : EditorWindow
    {
        private string stationId = "DEV-01";
        private string operationRunId = "";
        private string leaseId = "";
        private string sceneInstanceId = "";
        private string sessionToken = "";
        private int framesToCapture = 5;
        private bool isRunning;
        private string lastResult = "";

        [MenuItem("Tools/Yard Crane/Gateway/Realtime Connection Test...")]
        private static void Open()
        {
            var window = GetWindow<YardCraneGatewayDebugWindow>(true, "Gateway Realtime Connection Test");
            window.minSize = new Vector2(440, 380);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "手动PT-IF-007连通性测试：连接PortGatewayService的实时管道，走完 " +
                "protocol.hello 握手，打印收到的 equipment.inputFrame。以下字段必须来自 " +
                "control管道上一次已经成功的 gateway.session.prepare 调用（本窗口不会自己发起 " +
                "session.prepare）。不接入正式驾驶输入链路，仅用于连通性排查。",
                MessageType.Info);

            EditorGUILayout.Space();
            stationId = EditorGUILayout.TextField("Station Id", stationId);
            operationRunId = EditorGUILayout.TextField("Operation Run Id", operationRunId);
            leaseId = EditorGUILayout.TextField("Lease Id", leaseId);
            sceneInstanceId = EditorGUILayout.TextField("Scene Instance Id", sceneInstanceId);
            sessionToken = EditorGUILayout.TextField("Session Token (raw)", sessionToken);
            framesToCapture = EditorGUILayout.IntSlider("Frames To Capture", framesToCapture, 1, 20);

            EditorGUILayout.Space();
            EditorGUI.BeginDisabledGroup(isRunning);
            if (GUILayout.Button(isRunning ? "Testing..." : "Test Connection"))
            {
                RunTest();
            }

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("详细过程和每一帧内容见Console窗口（标签[YC_GATEWAY_DEBUG]）。", EditorStyles.wordWrappedLabel);
            if (!string.IsNullOrEmpty(lastResult))
            {
                EditorGUILayout.HelpBox(lastResult, MessageType.None);
            }
        }

        private async void RunTest()
        {
            if (string.IsNullOrWhiteSpace(stationId) || string.IsNullOrWhiteSpace(operationRunId) ||
                string.IsNullOrWhiteSpace(leaseId) || string.IsNullOrWhiteSpace(sceneInstanceId) ||
                string.IsNullOrWhiteSpace(sessionToken))
            {
                lastResult = "所有字段都必须填写。";
                return;
            }

            isRunning = true;
            lastResult = "测试进行中，请查看Console...";
            Repaint();

            var pipeName = "pt.gateway.realtime." + NormalizeStationId(stationId);
            var request = new GatewayRealtimeHelloRequest
            {
                StationId = stationId,
                OperationRunId = operationRunId,
                SessionToken = sessionToken,
                LeaseId = leaseId,
                SceneInstanceId = sceneInstanceId,
                StreamId = Guid.NewGuid().ToString()
            };

            try
            {
                var result = await GatewayRealtimeDebugClient.ConnectAndCaptureAsync(
                    pipeName, request, framesToCapture, 256 * 1024, 5000,
                    message => Debug.Log(message), CancellationToken.None);

                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    lastResult = "失败：" + result.ErrorMessage;
                }
                else if (!result.HandshakeAccepted)
                {
                    lastResult = $"握手被拒绝：errorCode={result.Reply?.ErrorCode}，message={result.Reply?.ErrorMessage}";
                }
                else
                {
                    lastResult = $"成功：握手通过，收到{result.CapturedFrames.Count}帧equipment.inputFrame，详情见Console。";
                }
            }
            catch (Exception ex)
            {
                lastResult = "异常：" + ex.Message;
                Debug.LogException(ex);
            }
            finally
            {
                isRunning = false;
                Repaint();
            }
        }

        // Mirrors PortGatewayService/Protocol/PipeNaming.cs's normalization rule (V1.1
        // section 3.2), duplicated here for the same reason as the rest of this debug
        // client: no shared PortTraining.Contracts package exists yet.
        private static string NormalizeStationId(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (var character in value.ToLowerInvariant())
            {
                if ((character >= 'a' && character <= 'z') || (character >= '0' && character <= '9') || character == '-')
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }
    }
}
