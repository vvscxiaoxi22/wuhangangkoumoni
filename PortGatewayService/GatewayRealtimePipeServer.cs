using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PortTraining.PortGatewayService.Configuration;
using PortTraining.PortGatewayService.Devices;
using PortTraining.PortGatewayService.Protocol;
using PortTraining.PortGatewayService.Security;

namespace PortTraining.PortGatewayService
{
    // PT-IF-007 (QC/YC scene EXE -> PortGatewayService, Named Pipe
    // "pt.gateway.realtime.{stationId}"). This is a skeleton: the protocol.hello
    // handshake (section 10.1) validates against the SAME GatewaySessionState the
    // control pipe issues leases into (leaseId, sceneInstanceId, and a constant-time
    // session token check per section 10.1), then streams equipment.inputFrame (section
    // 10.2) from IDeviceFrameSource at the negotiated interval. equipment.action /
    // equipment.actionAck and SimulatorFeedbackFrame (scene -> Gateway) are NOT
    // implemented yet - see PortGatewayService/README.md.
    public sealed class GatewayRealtimePipeServer
    {
        public const string ImplementationVersion = "2026-09-09-port-gateway-realtime-pipe-v2";
        private const int MaxConcurrentConnections = 4;
        private const int DefaultFrameIntervalMs = 20;
        private const int MinFrameIntervalMs = 1;
        private const int MaxFrameIntervalMs = 1000;

        private readonly string stationId;
        private readonly string pipeName;
        private readonly string senderInstanceId = Guid.NewGuid().ToString();
        private readonly Action<string> log;
        private readonly IDeviceFrameSource deviceFrameSource;
        private readonly GatewaySessionState sessionState;

        public GatewayRealtimePipeServer(
            GatewayStationOptions options,
            GatewaySessionState sessionState,
            Action<string> log = null,
            IDeviceFrameSource deviceFrameSource = null)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(options.StationId))
                throw new ArgumentException("StationId is required", nameof(options));

            stationId = options.StationId;
            pipeName = PipeNaming.BuildRealtimePipeName(stationId);
            this.sessionState = sessionState ?? throw new ArgumentNullException(nameof(sessionState));
            this.log = log ?? Console.WriteLine;
            this.deviceFrameSource = deviceFrameSource ?? new SimulatedDeviceFrameSource();
        }

        public string PipeName => pipeName;

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            log($"[PGS_REALTIME_PIPE_LISTENING] version={ImplementationVersion}, pipeName={pipeName}, stationId={stationId}");

            while (!cancellationToken.IsCancellationRequested)
            {
                var serverStream = NamedPipeServerStreamAcl.Create(
                    pipeName,
                    PipeDirection.InOut,
                    MaxConcurrentConnections,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    inBufferSize: 0,
                    outBufferSize: 0,
                    pipeSecurity: GatewayPipeSecurity.BuildDefault());

                try
                {
                    await serverStream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    serverStream.Dispose();
                    break;
                }

                _ = HandleConnectionAsync(serverStream, cancellationToken);
            }
        }

        private async Task HandleConnectionAsync(NamedPipeServerStream stream, CancellationToken cancellationToken)
        {
            using (stream)
            {
                try
                {
                    var helloFrame = await PipeFraming
                        .ReadFrameAsync(stream, ProtocolConstants.MaxRealtimeFrameBytes, cancellationToken)
                        .ConfigureAwait(false);
                    if (helloFrame == null)
                    {
                        return;
                    }

                    var hello = EnvelopeSerializer.Deserialize(helloFrame);
                    if (hello == null || hello.MessageType != "protocol.hello")
                    {
                        log("[PGS_REALTIME_PIPE_PROTOCOL_ERROR] reason=first-message-not-hello, " +
                            $"messageType={hello?.MessageType}");
                        return;
                    }

                    var outcome = EvaluateHello(hello);
                    if (!outcome.Accepted)
                    {
                        var rejected = new MessageEnvelope
                        {
                            MessageType = "protocol.rejected",
                            CorrelationId = hello.MessageId,
                            OperationRunId = hello.OperationRunId,
                            StationId = stationId,
                            Sender = "PortGatewayService",
                            SenderInstanceId = senderInstanceId,
                            Sequence = 1,
                            OccurredAtUtc = EnvelopeSerializer.FormatOccurredAtUtc(DateTimeOffset.UtcNow),
                            Payload = JsonSerializer.SerializeToElement(outcome.Rejection, GatewayJsonOptions.Default)
                        };
                        await PipeFraming
                            .WriteFrameAsync(stream, EnvelopeSerializer.Serialize(rejected), cancellationToken)
                            .ConfigureAwait(false);
                        log("[PGS_REALTIME_HELLO_REJECTED] " +
                            $"operationRunId={hello.OperationRunId}, errorCode={outcome.Rejection.ErrorCode}");
                        return;
                    }

                    var accepted = new MessageEnvelope
                    {
                        MessageType = "protocol.accepted",
                        CorrelationId = hello.MessageId,
                        OperationRunId = hello.OperationRunId,
                        StationId = stationId,
                        Sender = "PortGatewayService",
                        SenderInstanceId = senderInstanceId,
                        Sequence = 1,
                        OccurredAtUtc = EnvelopeSerializer.FormatOccurredAtUtc(DateTimeOffset.UtcNow)
                    };
                    await PipeFraming
                        .WriteFrameAsync(stream, EnvelopeSerializer.Serialize(accepted), cancellationToken)
                        .ConfigureAwait(false);
                    log($"[PGS_REALTIME_HELLO_ACCEPTED] operationRunId={hello.OperationRunId}, streamId={outcome.StreamId}");

                    await StreamInputFramesAsync(stream, hello.OperationRunId, outcome.StreamId, outcome.FrameIntervalMs, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (PipeProtocolViolationException ex)
                {
                    log($"[PGS_REALTIME_PIPE_PROTOCOL_ERROR] reason={ex.Message}");
                }
                catch (JsonException ex)
                {
                    log($"[PGS_REALTIME_PIPE_PROTOCOL_ERROR] reason=json-parse-failed, detail={ex.Message}");
                }
                catch (IOException ex)
                {
                    log($"[PGS_REALTIME_PIPE_DISCONNECTED] detail={ex.Message}");
                }
                catch (OperationCanceledException)
                {
                    // Shutting down; nothing to report.
                }
            }
        }

        // Only sends equipment.inputFrame - equipment.action and SimulatorFeedbackFrame
        // consumption are not implemented, so nothing is read back from the scene here.
        private async Task StreamInputFramesAsync(
            NamedPipeServerStream stream, string operationRunId, string streamId, int intervalMs, CancellationToken cancellationToken)
        {
            var sequence = 0UL;
            while (!cancellationToken.IsCancellationRequested)
            {
                sequence++;
                var deviceFrame = deviceFrameSource.GetCurrentFrame();
                var inputFrame = new EquipmentInputFrame
                {
                    OperationRunId = operationRunId,
                    StreamId = streamId,
                    Sequence = sequence,
                    CapturedAtUtc = EnvelopeSerializer.FormatOccurredAtUtc(DateTimeOffset.UtcNow),
                    DataAgeMs = 0,
                    Quality = "Good",
                    ControlMode = deviceFrame.ControlMode,
                    PlcOnline = deviceFrame.PlcOnline,
                    EmergencyStopReleased = deviceFrame.EmergencyStopReleased,
                    ControlPowerOn = deviceFrame.ControlPowerOn,
                    HandlesCentered = deviceFrame.HandlesCentered,
                    GantryCommand = deviceFrame.GantryCommand,
                    TrolleyCommand = deviceFrame.TrolleyCommand,
                    HoistCommand = deviceFrame.HoistCommand,
                    HornPressed = deviceFrame.HornPressed
                };

                var bytes = JsonSerializer.SerializeToUtf8Bytes(inputFrame, GatewayJsonOptions.Default);
                await PipeFraming.WriteFrameAsync(stream, bytes, cancellationToken).ConfigureAwait(false);

                try
                {
                    await Task.Delay(intervalMs, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private HelloOutcome EvaluateHello(MessageEnvelope hello)
        {
            var validator = new RequiredFieldValidator()
                .Require(hello.OperationRunId, "operationRunId")
                .Require(hello.StationId, "stationId");

            RealtimeHelloPayload payload;
            try
            {
                payload = JsonSerializer.Deserialize<RealtimeHelloPayload>(
                    hello.Payload.GetRawText(), GatewayJsonOptions.Default) ?? new RealtimeHelloPayload();
            }
            catch (JsonException)
            {
                payload = new RealtimeHelloPayload();
            }

            validator
                .Require(payload.SessionToken, "payload.sessionToken")
                .RequireOneOf(payload.MachineType, "payload.machineType", "QC", "YC")
                .Require(payload.LeaseId, "payload.leaseId")
                .Require(payload.SceneInstanceId, "payload.sceneInstanceId")
                .Require(payload.StreamId, "payload.streamId")
                .Require(payload.SceneVersion, "payload.sceneVersion")
                .Require(payload.ProtocolVersion, "payload.protocolVersion")
                .Require(payload.PointMapVersion, "payload.pointMapVersion");

            if (!validator.IsValid)
            {
                return HelloOutcome.Rejected(
                    "CONTRACT_VALIDATION_FAILED",
                    "missing or invalid fields: " + string.Join(", ", validator.MissingFields),
                    retryable: false);
            }

            GatewaySessionState.ActiveLeaseRecord lease;
            lock (sessionState.SyncRoot)
            {
                sessionState.ExpireIfStaleNoLock(log);
                lease = sessionState.CurrentLeaseNoLock;
            }

            // These all collapse to one code deliberately: whichever sub-reason applies,
            // the correct client response is the same (re-run gateway.session.prepare and
            // retry the realtime handshake from scratch), and finer-grained codes were not
            // worth guessing here without a documented shape to follow.
            if (lease == null ||
                !string.Equals(lease.Lease.OperationRunId, hello.OperationRunId, StringComparison.Ordinal) ||
                !string.Equals(lease.Lease.LeaseId, payload.LeaseId, StringComparison.Ordinal) ||
                !string.Equals(lease.Lease.SceneInstanceId, payload.SceneInstanceId, StringComparison.Ordinal))
            {
                return HelloOutcome.Rejected(
                    "AUTHENTICATION_FAILED",
                    "no active lease on this station matches the given operationRunId/leaseId/sceneInstanceId",
                    retryable: false);
            }

            if (!SessionTokenMatcher.Matches(payload.SessionToken, lease.SessionTokenHash))
            {
                return HelloOutcome.Rejected(
                    "AUTHENTICATION_FAILED",
                    "sessionToken does not match the hash registered at gateway.session.prepare",
                    retryable: false);
            }

            lock (sessionState.SyncRoot)
            {
                // Identity/token checks above ran outside the lock; re-confirm the same
                // lease is still current before committing streamId state under it.
                var stillCurrent = sessionState.CurrentLeaseNoLock != null &&
                    string.Equals(sessionState.CurrentLeaseNoLock.Lease.LeaseId, lease.Lease.LeaseId, StringComparison.Ordinal);
                if (!stillCurrent)
                {
                    return HelloOutcome.Rejected(
                        "AUTHENTICATION_FAILED",
                        "lease was released or replaced while this hello was being validated",
                        retryable: false);
                }

                // V1.1 section 3.2/10.1: "新生成的streamId；每次Named Pipe重连都必须改变" -
                // collapsed into the same AUTHENTICATION_FAILED code as the other hello
                // failures above, consistent with decision record 2.10's reasoning (the
                // correct client response is the same: generate a genuinely new streamId).
                if (lease.LastAcceptedStreamId != null &&
                    string.Equals(lease.LastAcceptedStreamId, payload.StreamId, StringComparison.Ordinal))
                {
                    return HelloOutcome.Rejected(
                        "AUTHENTICATION_FAILED",
                        "streamId must be new on every reconnect, not reused from a previous handshake",
                        retryable: false);
                }

                lease.LastAcceptedStreamId = payload.StreamId;
            }

            var intervalMs = payload.RequestedFrameIntervalMs ?? DefaultFrameIntervalMs;
            if (intervalMs < MinFrameIntervalMs || intervalMs > MaxFrameIntervalMs)
            {
                intervalMs = DefaultFrameIntervalMs;
            }

            return HelloOutcome.Prepared(payload.StreamId, intervalMs);
        }

        private sealed class HelloOutcome
        {
            public bool Accepted;
            public string StreamId;
            public int FrameIntervalMs;
            public GatewayErrorPayload Rejection;

            public static HelloOutcome Prepared(string streamId, int frameIntervalMs)
            {
                return new HelloOutcome { Accepted = true, StreamId = streamId, FrameIntervalMs = frameIntervalMs };
            }

            public static HelloOutcome Rejected(string errorCode, string message, bool retryable)
            {
                return new HelloOutcome
                {
                    Accepted = false,
                    Rejection = new GatewayErrorPayload { ErrorCode = errorCode, Message = message, Retryable = retryable }
                };
            }
        }
    }
}
