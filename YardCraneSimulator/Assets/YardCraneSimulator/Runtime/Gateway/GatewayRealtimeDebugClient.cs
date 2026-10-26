using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace PortTraining.YardCraneSimulator.Gateway
{
    public sealed class GatewayRealtimeDebugResult
    {
        public bool HandshakeAccepted;
        public GatewayProtocolReply Reply;
        public List<GatewayEquipmentInputFrame> CapturedFrames = new List<GatewayEquipmentInputFrame>();
        public string ErrorMessage;
    }

    // PT-IF-007 debug/test client: connects to PortGatewayService's realtime pipe,
    // completes the protocol.hello handshake (V1.1 section 10.1), and captures a
    // handful of equipment.inputFrame frames (section 10.2). This does not read
    // sessionToken/leaseId/etc. from anywhere on its own - those must come from a
    // gateway.session.prepare call made beforehand on the control pipe (there is no
    // scene launcher yet to automate that step).
    //
    // Deliberately does NOT implement: equipment.action/equipment.actionAck,
    // SimulatorFeedbackFrame (this project -> Gateway direction), or wiring any of
    // this into the real driving input pipeline. This exists only to prove the wire
    // connection and message shapes work before a real integration attempt.
    public static class GatewayRealtimeDebugClient
    {
        public static async Task<GatewayRealtimeDebugResult> ConnectAndCaptureAsync(
            string pipeName,
            GatewayRealtimeHelloRequest request,
            int framesToCapture,
            int maxFrameBytes,
            int connectTimeoutMs,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var result = new GatewayRealtimeDebugResult();
            log?.Invoke($"[YC_GATEWAY_DEBUG] connecting to pipe '{pipeName}'...");

            using (var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                try
                {
                    await client.ConnectAsync(connectTimeoutMs, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    result.ErrorMessage = $"connect-failed: {ex.Message}";
                    log?.Invoke($"[YC_GATEWAY_DEBUG] {result.ErrorMessage}");
                    return result;
                }

                log?.Invoke("[YC_GATEWAY_DEBUG] connected, sending protocol.hello...");
                var helloJson = request.ToJson(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), FormatUtcNow());

                try
                {
                    await GatewayPipeFraming.WriteFrameAsync(client, helloJson, cancellationToken).ConfigureAwait(false);

                    var replyJson = await GatewayPipeFraming
                        .ReadFrameAsync(client, maxFrameBytes, cancellationToken).ConfigureAwait(false);
                    if (replyJson == null)
                    {
                        result.ErrorMessage = "connection-closed-before-reply";
                        log?.Invoke($"[YC_GATEWAY_DEBUG] {result.ErrorMessage}");
                        return result;
                    }

                    var reply = GatewayProtocolReply.Parse(replyJson);
                    result.Reply = reply;

                    if (reply.MessageType != "protocol.accepted")
                    {
                        result.HandshakeAccepted = false;
                        log?.Invoke(
                            $"[YC_GATEWAY_DEBUG] handshake rejected: messageType={reply.MessageType}, " +
                            $"errorCode={reply.ErrorCode}, message={reply.ErrorMessage}");
                        return result;
                    }

                    result.HandshakeAccepted = true;
                    log?.Invoke("[YC_GATEWAY_DEBUG] handshake accepted, capturing frames...");

                    for (var i = 0; i < framesToCapture; i++)
                    {
                        var frameJson = await GatewayPipeFraming
                            .ReadFrameAsync(client, maxFrameBytes, cancellationToken).ConfigureAwait(false);
                        if (frameJson == null)
                        {
                            log?.Invoke("[YC_GATEWAY_DEBUG] connection closed while capturing frames");
                            break;
                        }

                        var frame = GatewayEquipmentInputFrame.Parse(frameJson);
                        result.CapturedFrames.Add(frame);
                        log?.Invoke(
                            $"[YC_GATEWAY_DEBUG] frame#{frame.Sequence}: plcOnline={frame.PlcOnline}, " +
                            $"controlMode={frame.ControlMode}, emergencyStopReleased={frame.EmergencyStopReleased}, " +
                            $"gantry={frame.GantryCommand:F2}, trolley={frame.TrolleyCommand:F2}, hoist={frame.HoistCommand:F2}");
                    }
                }
                catch (Exception ex)
                {
                    result.ErrorMessage = $"handshake-or-stream-failed: {ex.Message}";
                    log?.Invoke($"[YC_GATEWAY_DEBUG] {result.ErrorMessage}");
                }
            }

            return result;
        }

        private static string FormatUtcNow()
        {
            return DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        }
    }
}
