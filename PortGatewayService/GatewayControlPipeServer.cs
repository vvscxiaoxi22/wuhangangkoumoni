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
    // PT-IF-006 (PortTraining -> PortGatewayService, Named Pipe "pt.gateway.control.{stationId}").
    // Implements: the mandatory protocol.hello handshake (section 3.2), the read-only
    // gateway.status.get/gateway.status exchange, and gateway.session.prepare /
    // gateway.lease.release (section 11). Without allowSimulationFallback, every
    // structurally valid session.prepare is honestly rejected with PLC_OFFLINE - there
    // is no real S7 driver. With it enabled (never true at a real assessment station -
    // section 18.1), a SimulatedDeviceFrameSource stands in so the lease lifecycle can
    // be exercised end to end. Lease state lives in the GatewaySessionState shared with
    // GatewayRealtimePipeServer, since a scene's protocol.hello on the realtime pipe
    // must validate against the same lease this class issues. gateway.session.activate,
    // owner.heartbeat and safeStateReceipt are not implemented yet - see
    // PortGatewayService/README.md.
    public sealed class GatewayControlPipeServer
    {
        public const string ImplementationVersion = "2026-09-07-port-gateway-control-pipe-v4";
        private const int MaxConcurrentConnections = 4;

        private readonly string stationId;
        private readonly bool allowSimulationFallback;
        private readonly string pipeName;
        private readonly string senderInstanceId = Guid.NewGuid().ToString();
        private readonly Action<string> log;
        private readonly IDeviceFrameSource deviceFrameSource;
        private readonly GatewaySessionState sessionState;

        public GatewayControlPipeServer(
            GatewayStationOptions options,
            GatewaySessionState sessionState,
            Action<string> log = null,
            IDeviceFrameSource deviceFrameSource = null)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(options.StationId))
                throw new ArgumentException("StationId is required", nameof(options));

            stationId = options.StationId;
            allowSimulationFallback = options.AllowSimulationFallback;
            pipeName = PipeNaming.BuildControlPipeName(stationId);
            this.sessionState = sessionState ?? throw new ArgumentNullException(nameof(sessionState));
            this.log = log ?? Console.WriteLine;
            this.deviceFrameSource = deviceFrameSource ?? new SimulatedDeviceFrameSource();
        }

        public string PipeName => pipeName;

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            log($"[PGS_CONTROL_PIPE_LISTENING] version={ImplementationVersion}, pipeName={pipeName}, " +
                $"stationId={stationId}, allowSimulationFallback={allowSimulationFallback}");

            while (!cancellationToken.IsCancellationRequested)
            {
                // V1.1 section 17: restrict the pipe's Windows ACL instead of relying on
                // whatever the OS default happens to grant.
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
                        .ReadFrameAsync(stream, ProtocolConstants.MaxControlFrameBytes, cancellationToken)
                        .ConfigureAwait(false);
                    if (helloFrame == null)
                    {
                        return;
                    }

                    var hello = EnvelopeSerializer.Deserialize(helloFrame);
                    if (hello == null || hello.MessageType != "protocol.hello")
                    {
                        log("[PGS_CONTROL_PIPE_PROTOCOL_ERROR] reason=first-message-not-hello, " +
                            $"messageType={hello?.MessageType}");
                        return;
                    }

                    var lastSequence = hello.Sequence;

                    var accepted = new MessageEnvelope
                    {
                        MessageType = "protocol.accepted",
                        CorrelationId = hello.MessageId,
                        StationId = stationId,
                        Sender = "PortGatewayService",
                        SenderInstanceId = senderInstanceId,
                        Sequence = 1,
                        OccurredAtUtc = EnvelopeSerializer.FormatOccurredAtUtc(DateTimeOffset.UtcNow)
                    };
                    await PipeFraming
                        .WriteFrameAsync(stream, EnvelopeSerializer.Serialize(accepted), cancellationToken)
                        .ConfigureAwait(false);

                    var replySequence = 1UL;
                    while (true)
                    {
                        var frame = await PipeFraming
                            .ReadFrameAsync(stream, ProtocolConstants.MaxControlFrameBytes, cancellationToken)
                            .ConfigureAwait(false);
                        if (frame == null)
                        {
                            return;
                        }

                        var request = EnvelopeSerializer.Deserialize(frame);
                        if (request == null)
                        {
                            log("[PGS_CONTROL_PIPE_PROTOCOL_ERROR] reason=null-envelope");
                            return;
                        }

                        if (request.Sequence <= lastSequence)
                        {
                            log("[PGS_CONTROL_PIPE_PROTOCOL_ERROR] reason=sequence-not-increasing, " +
                                $"previous={lastSequence}, received={request.Sequence}");
                            return;
                        }

                        lastSequence = request.Sequence;

                        if (request.MessageType == "gateway.status.get")
                        {
                            replySequence++;
                            var response = new MessageEnvelope
                            {
                                MessageType = "gateway.status",
                                CorrelationId = request.MessageId,
                                StationId = stationId,
                                Sender = "PortGatewayService",
                                SenderInstanceId = senderInstanceId,
                                Sequence = replySequence,
                                OccurredAtUtc = EnvelopeSerializer.FormatOccurredAtUtc(DateTimeOffset.UtcNow),
                                Payload = JsonSerializer.SerializeToElement(BuildStatusPayload(), GatewayJsonOptions.Default)
                            };
                            await PipeFraming
                                .WriteFrameAsync(stream, EnvelopeSerializer.Serialize(response), cancellationToken)
                                .ConfigureAwait(false);
                            continue;
                        }

                        if (request.MessageType == "gateway.session.prepare")
                        {
                            replySequence++;
                            var outcome = EvaluateSessionPrepare(request);
                            MessageEnvelope response;
                            if (outcome.Accepted)
                            {
                                response = new MessageEnvelope
                                {
                                    MessageType = "gateway.session.prepared",
                                    CorrelationId = request.MessageId,
                                    OperationId = request.OperationId,
                                    OperationRunId = request.OperationRunId,
                                    StationId = stationId,
                                    Sender = "PortGatewayService",
                                    SenderInstanceId = senderInstanceId,
                                    Sequence = replySequence,
                                    OccurredAtUtc = EnvelopeSerializer.FormatOccurredAtUtc(DateTimeOffset.UtcNow),
                                    Payload = JsonSerializer.SerializeToElement(outcome.Lease, GatewayJsonOptions.Default)
                                };
                                log("[PGS_SESSION_PREPARED] " +
                                    $"operationRunId={request.OperationRunId}, leaseId={outcome.Lease.LeaseId}, " +
                                    $"leaseEpoch={outcome.Lease.LeaseEpoch}, isSimulated={outcome.Lease.IsSimulated}");
                            }
                            else
                            {
                                // V1.1 section 11 names the success reply (gateway.session.prepared)
                                // but never names a failure reply. "gateway.session.rejected" follows
                                // the protocol.hello -> protocol.accepted/protocol.rejected naming
                                // precedent from section 10.1 - a reasonable extrapolation, not text
                                // lifted from the standard.
                                response = new MessageEnvelope
                                {
                                    MessageType = "gateway.session.rejected",
                                    CorrelationId = request.MessageId,
                                    OperationId = request.OperationId,
                                    OperationRunId = request.OperationRunId,
                                    StationId = stationId,
                                    Sender = "PortGatewayService",
                                    SenderInstanceId = senderInstanceId,
                                    Sequence = replySequence,
                                    OccurredAtUtc = EnvelopeSerializer.FormatOccurredAtUtc(DateTimeOffset.UtcNow),
                                    Payload = JsonSerializer.SerializeToElement(outcome.Rejection, GatewayJsonOptions.Default)
                                };
                                log("[PGS_SESSION_PREPARE_REJECTED] " +
                                    $"operationRunId={request.OperationRunId}, errorCode={outcome.Rejection.ErrorCode}");
                            }

                            await PipeFraming
                                .WriteFrameAsync(stream, EnvelopeSerializer.Serialize(response), cancellationToken)
                                .ConfigureAwait(false);
                            continue;
                        }

                        if (request.MessageType == "gateway.lease.release")
                        {
                            replySequence++;
                            string releasedLeaseId;
                            bool matched;
                            lock (sessionState.SyncRoot)
                            {
                                sessionState.ExpireIfStaleNoLock(log);
                                matched = sessionState.TryReleaseNoLock(request.OperationRunId, out releasedLeaseId);
                            }

                            MessageEnvelope response;
                            if (matched)
                            {
                                response = new MessageEnvelope
                                {
                                    MessageType = "gateway.lease.released",
                                    CorrelationId = request.MessageId,
                                    OperationRunId = request.OperationRunId,
                                    StationId = stationId,
                                    Sender = "PortGatewayService",
                                    SenderInstanceId = senderInstanceId,
                                    Sequence = replySequence,
                                    OccurredAtUtc = EnvelopeSerializer.FormatOccurredAtUtc(DateTimeOffset.UtcNow),
                                    Payload = JsonSerializer.SerializeToElement(
                                        new GatewayLeaseReleasedPayload { LeaseId = releasedLeaseId }, GatewayJsonOptions.Default)
                                };
                                log($"[PGS_LEASE_RELEASED] leaseId={releasedLeaseId}, operationRunId={request.OperationRunId}");
                            }
                            else
                            {
                                // V1.1 section 15 already defines OPERATION_RUN_MISMATCH as
                                // "消息不属于当前活动运行" (not retryable, discard and audit the
                                // source) - a stale/foreign operationRunId on a release request is
                                // exactly that case, so this reuses the existing code instead of
                                // inventing a new one. "gateway.lease.rejected" as the failure
                                // message name follows the same protocol.hello/session.prepare
                                // accept-reject naming precedent already used elsewhere.
                                response = new MessageEnvelope
                                {
                                    MessageType = "gateway.lease.rejected",
                                    CorrelationId = request.MessageId,
                                    OperationRunId = request.OperationRunId,
                                    StationId = stationId,
                                    Sender = "PortGatewayService",
                                    SenderInstanceId = senderInstanceId,
                                    Sequence = replySequence,
                                    OccurredAtUtc = EnvelopeSerializer.FormatOccurredAtUtc(DateTimeOffset.UtcNow),
                                    Payload = JsonSerializer.SerializeToElement(
                                        new GatewayErrorPayload
                                        {
                                            ErrorCode = "OPERATION_RUN_MISMATCH",
                                            Message = "no active lease matches this operationRunId " +
                                                      "(already released, expired, or never issued)",
                                            Retryable = false
                                        },
                                        GatewayJsonOptions.Default)
                                };
                                log("[PGS_LEASE_RELEASE_REJECTED] " +
                                    $"operationRunId={request.OperationRunId}, errorCode=OPERATION_RUN_MISMATCH");
                            }

                            await PipeFraming
                                .WriteFrameAsync(stream, EnvelopeSerializer.Serialize(response), cancellationToken)
                                .ConfigureAwait(false);
                            continue;
                        }

                        log("[PGS_CONTROL_PIPE_PROTOCOL_ERROR] reason=unknown-message-type, " +
                            $"messageType={request.MessageType}");
                        return;
                    }
                }
                catch (PipeProtocolViolationException ex)
                {
                    log($"[PGS_CONTROL_PIPE_PROTOCOL_ERROR] reason={ex.Message}");
                }
                catch (JsonException ex)
                {
                    log($"[PGS_CONTROL_PIPE_PROTOCOL_ERROR] reason=json-parse-failed, detail={ex.Message}");
                }
                catch (IOException ex)
                {
                    log($"[PGS_CONTROL_PIPE_DISCONNECTED] detail={ex.Message}");
                }
                catch (OperationCanceledException)
                {
                    // Shutting down; nothing to report.
                }
            }
        }

        private SessionPrepareOutcome EvaluateSessionPrepare(MessageEnvelope request)
        {
            var validator = new RequiredFieldValidator()
                .Require(request.OperationRunId, "operationRunId")
                .Require(request.StationId, "stationId");

            GatewaySessionPreparePayload payload;
            try
            {
                payload = JsonSerializer.Deserialize<GatewaySessionPreparePayload>(
                    request.Payload.GetRawText(), GatewayJsonOptions.Default) ?? new GatewaySessionPreparePayload();
            }
            catch (JsonException)
            {
                payload = new GatewaySessionPreparePayload();
            }

            validator
                .Require(payload.OwnerInstanceId, "payload.ownerInstanceId")
                .Require(payload.SceneInstanceId, "payload.sceneInstanceId")
                .Require(payload.SessionTokenHash, "payload.sessionTokenHash")
                .Require(payload.WorkOrderSnapshotHash, "payload.workOrderSnapshotHash")
                .RequireOneOf(payload.ControlMode, "payload.controlMode", "Local", "Remote")
                .Require(payload.SceneVersion, "payload.sceneVersion")
                .Require(payload.PointMapVersion, "payload.pointMapVersion")
                .RequirePositive(payload.RequestedLeaseDurationMs, "payload.requestedLeaseDurationMs");

            if (!validator.IsValid)
            {
                return SessionPrepareOutcome.Rejected(
                    "CONTRACT_VALIDATION_FAILED",
                    "missing or invalid fields: " + string.Join(", ", validator.MissingFields),
                    retryable: false);
            }

            lock (sessionState.SyncRoot)
            {
                sessionState.ExpireIfStaleNoLock(log);

                if (sessionState.CurrentLeaseNoLock != null)
                {
                    return SessionPrepareOutcome.Rejected(
                        "CONTROL_LEASE_CONFLICT",
                        $"station already holds an active lease (leaseId={sessionState.CurrentLeaseNoLock.Lease.LeaseId})",
                        retryable: false);
                }

                if (!allowSimulationFallback)
                {
                    // Honest default: no real S7 driver exists, so without an explicit
                    // simulation opt-in this can never succeed.
                    return SessionPrepareOutcome.Rejected(
                        "PLC_OFFLINE",
                        "no PLC connection exists yet and allowSimulationFallback is false for this station",
                        retryable: true);
                }

                var deviceFrame = deviceFrameSource.GetCurrentFrame();
                if (deviceFrame.ControlMode != payload.ControlMode)
                {
                    return SessionPrepareOutcome.Rejected(
                        "CONTROL_MODE_MISMATCH",
                        $"station is in {deviceFrame.ControlMode} but request asked for {payload.ControlMode}",
                        retryable: true);
                }

                var lease = sessionState.IssueNoLock(
                    request.OperationRunId,
                    stationId,
                    payload.SceneInstanceId,
                    payload.ControlMode,
                    payload.SessionTokenHash,
                    deviceFrame.IsSimulated,
                    payload.RequestedLeaseDurationMs.Value);

                return SessionPrepareOutcome.Prepared(lease);
            }
        }

        private GatewayStatusPayload BuildStatusPayload()
        {
            // Honest placeholder: no real S7 driver or point table exists yet (see
            // PortGatewayService/README.md), so plcOnline must stay false rather than
            // fabricate connectivity, regardless of allowSimulationFallback.
            return new GatewayStatusPayload
            {
                GatewayVersion = ImplementationVersion,
                PlcOnline = false,
                PointMapVersion = null,
                PollCycleMs = null,
                DataAgeMs = null,
                Faults = new[] { "NO_PLC_CONFIGURED" },
                Notes = allowSimulationFallback
                    ? "Scaffold build: no real S7 driver; allowSimulationFallback=true so " +
                      "gateway.session.prepare can succeed via a simulated device frame (dev/non-assessment only)."
                    : "Scaffold build: no real S7 driver or point table wired up yet; " +
                      "allowSimulationFallback=false so gateway.session.prepare always rejects with PLC_OFFLINE."
            };
        }

        private sealed class SessionPrepareOutcome
        {
            public bool Accepted;
            public GatewaySessionLease Lease;
            public GatewayErrorPayload Rejection;

            public static SessionPrepareOutcome Prepared(GatewaySessionLease lease)
            {
                return new SessionPrepareOutcome { Accepted = true, Lease = lease };
            }

            public static SessionPrepareOutcome Rejected(string errorCode, string message, bool retryable)
            {
                return new SessionPrepareOutcome
                {
                    Accepted = false,
                    Rejection = new GatewayErrorPayload { ErrorCode = errorCode, Message = message, Retryable = retryable }
                };
            }
        }
    }
}
