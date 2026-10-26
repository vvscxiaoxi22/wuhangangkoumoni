using System;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PortTraining.PortGatewayService;
using PortTraining.PortGatewayService.Configuration;
using PortTraining.PortGatewayService.Protocol;
using PortTraining.YardCraneSimulator.Gateway;

// Proves the EXACT Unity-side debug client code (linked from Assets/YardCraneSimulator/
// Runtime/Gateway/*.cs) can complete a real PT-IF-007 handshake and read real
// equipment.inputFrame frames from a real PortGatewayService instance - not just "looks
// right by eye". Runs a real GatewayControlPipeServer + GatewayRealtimePipeServer
// in-process (referenced directly from ../../../PortGatewayService), drives
// gateway.session.prepare on the control pipe using PortGatewayService's own protocol
// types, then hands the resulting lease/token to GatewayRealtimeDebugClient exactly as
// a real Unity scene would.
internal static class Program
{
    private const string StationId = "YC-HARNESS-01";
    private static int assertions;

    private static async Task Main()
    {
        var controlPipeName = PipeNaming.BuildControlPipeName(StationId);
        var realtimePipeName = PipeNaming.BuildRealtimePipeName(StationId);

        var sessionState = new GatewaySessionState();
        var options = new GatewayStationOptions { StationId = StationId, AllowSimulationFallback = true };
        var controlServer = new GatewayControlPipeServer(options, sessionState, message => { });
        var realtimeServer = new GatewayRealtimePipeServer(options, sessionState, message => { });

        using (var cts = new CancellationTokenSource())
        {
            var controlTask = controlServer.RunAsync(cts.Token);
            var realtimeTask = realtimeServer.RunAsync(cts.Token);

            await RunHappyPathAsync(controlPipeName, realtimePipeName).ConfigureAwait(false);
            await RunWrongTokenIsRejectedAsync(controlPipeName, realtimePipeName).ConfigureAwait(false);

            cts.Cancel();
            try
            {
                await Task.WhenAll(controlTask, realtimeTask).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        Console.WriteLine($"[YC_GATEWAY_DEBUG_CLIENT_HARNESS_PASS] assertions={assertions}");
    }

    private static async Task RunHappyPathAsync(string controlPipeName, string realtimePipeName)
    {
        var operationRunId = Guid.NewGuid().ToString();
        var sceneInstanceId = Guid.NewGuid().ToString();
        var rawToken = "harness-session-token-" + Guid.NewGuid();
        var tokenHash = ComputeSessionTokenHash(rawToken);

        var leaseId = await PrepareSessionAsync(controlPipeName, operationRunId, sceneInstanceId, tokenHash)
            .ConfigureAwait(false);
        Require(!string.IsNullOrEmpty(leaseId), "control-pipe-session-prepare-issued-a-lease");

        var request = new GatewayRealtimeHelloRequest
        {
            StationId = StationId,
            OperationRunId = operationRunId,
            SessionToken = rawToken,
            LeaseId = leaseId,
            SceneInstanceId = sceneInstanceId,
            StreamId = Guid.NewGuid().ToString()
        };

        var result = await GatewayRealtimeDebugClient
            .ConnectAndCaptureAsync(realtimePipeName, request, 2, ProtocolConstants.MaxRealtimeFrameBytes, 5000, null, CancellationToken.None)
            .ConfigureAwait(false);

        Require(string.IsNullOrEmpty(result.ErrorMessage), $"unity-side-client-hit-no-error (was: {result.ErrorMessage})");
        Require(result.HandshakeAccepted, "unity-side-client-handshake-accepted-by-real-gateway");
        Require(result.CapturedFrames.Count == 2, "unity-side-client-captured-two-real-frames");
        Require(result.CapturedFrames[0].Sequence == 1 && result.CapturedFrames[1].Sequence == 2,
            "captured-frame-sequence-increments");
        Require(result.CapturedFrames[0].MessageType == "equipment.inputFrame", "captured-frame-has-correct-messageType");
        Require(result.CapturedFrames[0].PlcOnline == false, "captured-frame-honestly-reports-no-real-plc");
        Require(result.CapturedFrames[0].EmergencyStopReleased, "captured-frame-reflects-simulated-safe-estop-state");
        Require(result.CapturedFrames[0].ControlMode == "Local", "captured-frame-reports-simulated-local-mode");

        // This station's lease slot is shared across every test in this harness -
        // release it, or the next gateway.session.prepare fails with
        // CONTROL_LEASE_CONFLICT for an unrelated reason before it ever reaches
        // whatever that next test actually means to check.
        await ReleaseSessionAsync(controlPipeName, operationRunId).ConfigureAwait(false);
    }

    private static async Task ReleaseSessionAsync(string controlPipeName, string operationRunId)
    {
        using (var client = new NamedPipeClientStream(".", controlPipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
        {
            await client.ConnectAsync(5000).ConfigureAwait(false);

            var hello = new MessageEnvelope
            {
                MessageType = "protocol.hello",
                Sender = "GatewayDebugClientHarness",
                SenderInstanceId = Guid.NewGuid().ToString(),
                StationId = StationId,
                Sequence = 1,
                OccurredAtUtc = EnvelopeSerializer.FormatOccurredAtUtc(DateTimeOffset.UtcNow)
            };
            await PipeFraming.WriteFrameAsync(client, EnvelopeSerializer.Serialize(hello), CancellationToken.None)
                .ConfigureAwait(false);
            await PipeFraming.ReadFrameAsync(client, ProtocolConstants.MaxControlFrameBytes, CancellationToken.None)
                .ConfigureAwait(false);

            var release = new MessageEnvelope
            {
                MessageType = "gateway.lease.release",
                Sender = "GatewayDebugClientHarness",
                SenderInstanceId = Guid.NewGuid().ToString(),
                StationId = StationId,
                OperationRunId = operationRunId,
                Sequence = 2,
                OccurredAtUtc = EnvelopeSerializer.FormatOccurredAtUtc(DateTimeOffset.UtcNow)
            };
            await PipeFraming.WriteFrameAsync(client, EnvelopeSerializer.Serialize(release), CancellationToken.None)
                .ConfigureAwait(false);
            var responseFrame = await PipeFraming
                .ReadFrameAsync(client, ProtocolConstants.MaxControlFrameBytes, CancellationToken.None)
                .ConfigureAwait(false);
            var response = EnvelopeSerializer.Deserialize(responseFrame);
            if (response == null || response.MessageType != "gateway.lease.released")
            {
                throw new InvalidOperationException($"gateway.lease.release did not succeed: messageType={response?.MessageType}");
            }
        }
    }

    private static async Task RunWrongTokenIsRejectedAsync(string controlPipeName, string realtimePipeName)
    {
        var operationRunId = Guid.NewGuid().ToString();
        var sceneInstanceId = Guid.NewGuid().ToString();
        var rawToken = "harness-session-token-" + Guid.NewGuid();
        var tokenHash = ComputeSessionTokenHash(rawToken);

        var leaseId = await PrepareSessionAsync(controlPipeName, operationRunId, sceneInstanceId, tokenHash)
            .ConfigureAwait(false);

        var request = new GatewayRealtimeHelloRequest
        {
            StationId = StationId,
            OperationRunId = operationRunId,
            SessionToken = "this-is-the-wrong-token",
            LeaseId = leaseId,
            SceneInstanceId = sceneInstanceId,
            StreamId = Guid.NewGuid().ToString()
        };

        var result = await GatewayRealtimeDebugClient
            .ConnectAndCaptureAsync(realtimePipeName, request, 1, ProtocolConstants.MaxRealtimeFrameBytes, 5000, null, CancellationToken.None)
            .ConfigureAwait(false);

        Require(!result.HandshakeAccepted, "unity-side-client-observes-rejection-for-wrong-token");
        Require(result.Reply != null && result.Reply.MessageType == "protocol.rejected", "rejection-has-correct-messageType");
        Require(result.Reply?.ErrorCode == "AUTHENTICATION_FAILED", "rejection-has-correct-errorCode");

        // The lease from this test is left dangling (never released or connected to
        // successfully) - harmless since the harness process exits right after, but if
        // this method grows more cases later they must not assume a clean station.
    }

    private static async Task<string> PrepareSessionAsync(
        string controlPipeName, string operationRunId, string sceneInstanceId, string sessionTokenHash)
    {
        using (var client = new NamedPipeClientStream(".", controlPipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
        {
            await client.ConnectAsync(5000).ConfigureAwait(false);

            var hello = new MessageEnvelope
            {
                MessageType = "protocol.hello",
                Sender = "GatewayDebugClientHarness",
                SenderInstanceId = Guid.NewGuid().ToString(),
                StationId = StationId,
                Sequence = 1,
                OccurredAtUtc = EnvelopeSerializer.FormatOccurredAtUtc(DateTimeOffset.UtcNow)
            };
            await PipeFraming.WriteFrameAsync(client, EnvelopeSerializer.Serialize(hello), CancellationToken.None)
                .ConfigureAwait(false);
            await PipeFraming.ReadFrameAsync(client, ProtocolConstants.MaxControlFrameBytes, CancellationToken.None)
                .ConfigureAwait(false);

            var prepare = new MessageEnvelope
            {
                MessageType = "gateway.session.prepare",
                Sender = "GatewayDebugClientHarness",
                SenderInstanceId = Guid.NewGuid().ToString(),
                StationId = StationId,
                OperationRunId = operationRunId,
                Sequence = 2,
                OccurredAtUtc = EnvelopeSerializer.FormatOccurredAtUtc(DateTimeOffset.UtcNow),
                Payload = JsonSerializer.SerializeToElement(
                    new GatewaySessionPreparePayload
                    {
                        OwnerInstanceId = Guid.NewGuid().ToString(),
                        SceneInstanceId = sceneInstanceId,
                        SessionTokenHash = sessionTokenHash,
                        WorkOrderSnapshotHash = "sha256:" + new string('b', 64),
                        ControlMode = "Local",
                        SceneVersion = "YC-SCENE-V1",
                        PointMapVersion = "YC-IO-0.1.0-DRAFT",
                        RequestedLeaseDurationMs = 60000
                    },
                    GatewayJsonOptions.Default)
            };
            await PipeFraming.WriteFrameAsync(client, EnvelopeSerializer.Serialize(prepare), CancellationToken.None)
                .ConfigureAwait(false);
            var responseFrame = await PipeFraming
                .ReadFrameAsync(client, ProtocolConstants.MaxControlFrameBytes, CancellationToken.None)
                .ConfigureAwait(false);
            var response = EnvelopeSerializer.Deserialize(responseFrame);
            if (response == null || response.MessageType != "gateway.session.prepared")
            {
                throw new InvalidOperationException(
                    $"gateway.session.prepare did not succeed: messageType={response?.MessageType}");
            }

            var lease = JsonSerializer.Deserialize<GatewaySessionLease>(
                response.Payload.GetRawText(), GatewayJsonOptions.Default);
            return lease.LeaseId;
        }
    }

    private static string ComputeSessionTokenHash(string rawToken)
    {
        using (var sha256 = SHA256.Create())
        {
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawToken));
            return "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }

    private static void Require(bool condition, string name)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(name);
        Console.WriteLine("PASS " + name);
    }
}
