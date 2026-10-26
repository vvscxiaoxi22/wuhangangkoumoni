using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PortTraining.PortGatewayService;
using PortTraining.PortGatewayService.Configuration;
using PortTraining.PortGatewayService.Protocol;
using PortTraining.PortGatewayService.Security;

// Self-contained smoke test for GatewayControlPipeServer: runs three server instances
// in-process (no separate process/terminal needed) and drives real NamedPipeClientStream
// connections against them, the same way this repo's other Tools/*Harness projects work.
// Three stations because lease state is per-server-instance and the tests need to
// observe conflicts/releases without stepping on each other:
//   SIM-ON     - allowSimulationFallback=true, used for the basic handshake/status
//                tests plus the full prepare -> conflict -> release -> prepare-again
//                lease lifecycle.
//   SIM-OFF    - allowSimulationFallback=false, proves a well-formed prepare is still
//                honestly rejected with PLC_OFFLINE when simulation is not opted in.
//   MODE-CHECK - allowSimulationFallback=true, isolated station used only for the
//                controlMode-mismatch case so it does not interact with SIM-ON's lease.
//   EXPIRY     - allowSimulationFallback=true, isolated station used only for the
//                lease-expiry case, which needs a real (short) wait and must not run
//                concurrently with anything else touching the same lease slot.
//   REALTIME   - allowSimulationFallback=true, runs BOTH a control and a realtime
//                pipe server sharing one GatewaySessionState (mirroring how Program.cs
//                wires a real deployment), used for the PT-IF-007 handshake/streaming
//                tests: a lease issued via the control pipe must be exactly what the
//                realtime pipe's protocol.hello validates against.
internal static class Program
{
    private const string SimOnStationId = "SMOKE-SIM-ON";
    private const string SimOffStationId = "SMOKE-SIM-OFF";
    private const string ModeCheckStationId = "SMOKE-MODE-CHECK";
    private const string ExpiryStationId = "SMOKE-EXPIRY";
    private const string RealtimeStationId = "SMOKE-REALTIME";
    private static int assertions;

    private static async Task Main()
    {
        VerifyPipeSecurityGrantsOnlyCurrentUserAndAdministrators();

        var simOnPipe = PipeNaming.BuildControlPipeName(SimOnStationId);
        var simOffPipe = PipeNaming.BuildControlPipeName(SimOffStationId);
        var modeCheckPipe = PipeNaming.BuildControlPipeName(ModeCheckStationId);
        var expiryPipe = PipeNaming.BuildControlPipeName(ExpiryStationId);
        var realtimeControlPipe = PipeNaming.BuildControlPipeName(RealtimeStationId);
        var realtimePipe = PipeNaming.BuildRealtimePipeName(RealtimeStationId);

        var simOnServer = new GatewayControlPipeServer(
            new GatewayStationOptions { StationId = SimOnStationId, AllowSimulationFallback = true },
            new GatewaySessionState(), message => { });
        var simOffServer = new GatewayControlPipeServer(
            new GatewayStationOptions { StationId = SimOffStationId, AllowSimulationFallback = false },
            new GatewaySessionState(), message => { });
        var modeCheckServer = new GatewayControlPipeServer(
            new GatewayStationOptions { StationId = ModeCheckStationId, AllowSimulationFallback = true },
            new GatewaySessionState(), message => { });
        var expiryServer = new GatewayControlPipeServer(
            new GatewayStationOptions { StationId = ExpiryStationId, AllowSimulationFallback = true },
            new GatewaySessionState(), message => { });

        var realtimeSessionState = new GatewaySessionState();
        var realtimeStationOptions = new GatewayStationOptions { StationId = RealtimeStationId, AllowSimulationFallback = true };
        var realtimeControlServer = new GatewayControlPipeServer(realtimeStationOptions, realtimeSessionState, message => { });
        var realtimeServer = new GatewayRealtimePipeServer(realtimeStationOptions, realtimeSessionState, message => { });

        using (var cts = new CancellationTokenSource())
        {
            var simOnTask = simOnServer.RunAsync(cts.Token);
            var simOffTask = simOffServer.RunAsync(cts.Token);
            var modeCheckTask = modeCheckServer.RunAsync(cts.Token);
            var expiryTask = expiryServer.RunAsync(cts.Token);
            var realtimeControlTask = realtimeControlServer.RunAsync(cts.Token);
            var realtimeTask = realtimeServer.RunAsync(cts.Token);

            await RunHappyPathAsync(simOnPipe, SimOnStationId).ConfigureAwait(false);
            await RunRejectsNonHelloFirstMessageAsync(simOnPipe, SimOnStationId).ConfigureAwait(false);
            await RunRejectsOversizedFrameHeaderAsync(simOnPipe).ConfigureAwait(false);
            await RunRejectsNonIncreasingSequenceAsync(simOnPipe, SimOnStationId).ConfigureAwait(false);
            await RunSessionPrepareRejectedForMissingFieldsAsync(simOnPipe, SimOnStationId).ConfigureAwait(false);
            await RunSessionPrepareAlwaysPlcOfflineWithoutSimulationAsync(simOffPipe, SimOffStationId).ConfigureAwait(false);
            await RunSessionPrepareRejectsControlModeMismatchAsync(modeCheckPipe, ModeCheckStationId).ConfigureAwait(false);
            await RunLeaseReleaseRejectsWhenNoLeaseExistsAsync(modeCheckPipe, ModeCheckStationId).ConfigureAwait(false);
            await RunSessionPrepareLeaseLifecycleAsync(simOnPipe, SimOnStationId).ConfigureAwait(false);
            await RunSessionPrepareLeaseExpiresAndFreesTheStationAsync(expiryPipe, ExpiryStationId).ConfigureAwait(false);
            await RunRealtimeHandshakeAndStreamingAsync(realtimeControlPipe, realtimePipe, RealtimeStationId).ConfigureAwait(false);

            cts.Cancel();
            try
            {
                await Task.WhenAll(
                    simOnTask, simOffTask, modeCheckTask, expiryTask, realtimeControlTask, realtimeTask).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        Console.WriteLine($"[PORT_GATEWAY_CONTROL_PIPE_SMOKE_PASS] assertions={assertions}");
    }

    // This checks the ACL-construction logic itself (the part GatewayPipeSecurity
    // actually writes), not OS-level enforcement against a different Windows account -
    // this sandbox only has the one interactive user, so "a different user is denied"
    // cannot be exercised here. The rest of this suite already proves the ACL does not
    // block the current user's own connections (every other test still connects fine).
    private static void VerifyPipeSecurityGrantsOnlyCurrentUserAndAdministrators()
    {
        var security = GatewayPipeSecurity.BuildDefault();
        var rules = security.GetAccessRules(true, false, typeof(SecurityIdentifier))
            .Cast<PipeAccessRule>()
            .ToList();

        var currentUser = WindowsIdentity.GetCurrent().User;
        var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);

        Require(
            rules.Any(r => r.IdentityReference == currentUser &&
                           r.AccessControlType == AccessControlType.Allow &&
                           (r.PipeAccessRights & PipeAccessRights.FullControl) == PipeAccessRights.FullControl),
            "pipe-acl-grants-current-user-full-control");
        Require(
            rules.Any(r => r.IdentityReference == administrators &&
                           r.AccessControlType == AccessControlType.Allow &&
                           (r.PipeAccessRights & PipeAccessRights.FullControl) == PipeAccessRights.FullControl),
            "pipe-acl-grants-administrators-full-control");
        Require(
            rules.All(r => r.IdentityReference != everyone),
            "pipe-acl-does-not-grant-everyone-access");
    }

    private static async Task<NamedPipeClientStream> ConnectAsync(string pipeName)
    {
        var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(2000).ConfigureAwait(false);
        return client;
    }

    private static async Task<MessageEnvelope> HandshakeAsync(NamedPipeClientStream client, string stationId, ulong helloSequence)
    {
        var hello = new MessageEnvelope
        {
            MessageType = "protocol.hello",
            Sender = "GatewaySmokeClient",
            SenderInstanceId = Guid.NewGuid().ToString(),
            StationId = stationId,
            Sequence = helloSequence,
            OccurredAtUtc = EnvelopeSerializer.FormatOccurredAtUtc(DateTimeOffset.UtcNow)
        };
        await PipeFraming.WriteFrameAsync(client, EnvelopeSerializer.Serialize(hello), CancellationToken.None)
            .ConfigureAwait(false);
        var acceptedFrame = await PipeFraming
            .ReadFrameAsync(client, ProtocolConstants.MaxControlFrameBytes, CancellationToken.None)
            .ConfigureAwait(false);
        return EnvelopeSerializer.Deserialize(acceptedFrame);
    }

    private static MessageEnvelope BuildSessionPrepare(
        string stationId, string operationRunId, ulong sequence, string controlMode, long requestedLeaseDurationMs)
    {
        return new MessageEnvelope
        {
            MessageType = "gateway.session.prepare",
            Sender = "GatewaySmokeClient",
            SenderInstanceId = Guid.NewGuid().ToString(),
            StationId = stationId,
            OperationRunId = operationRunId,
            Sequence = sequence,
            OccurredAtUtc = EnvelopeSerializer.FormatOccurredAtUtc(DateTimeOffset.UtcNow),
            Payload = JsonSerializer.SerializeToElement(
                new GatewaySessionPreparePayload
                {
                    OwnerInstanceId = Guid.NewGuid().ToString(),
                    SceneInstanceId = Guid.NewGuid().ToString(),
                    SessionTokenHash = "sha256:" + new string('a', 64),
                    WorkOrderSnapshotHash = "sha256:" + new string('b', 64),
                    ControlMode = controlMode,
                    SceneVersion = "YC-SCENE-V1",
                    PointMapVersion = "YC-IO-1.0.0",
                    RequestedLeaseDurationMs = requestedLeaseDurationMs
                },
                GatewayJsonOptions.Default)
        };
    }

    private static async Task RunHappyPathAsync(string pipeName, string stationId)
    {
        using (var client = await ConnectAsync(pipeName).ConfigureAwait(false))
        {
            var accepted = await HandshakeAsync(client, stationId, 1).ConfigureAwait(false);
            Require(accepted != null && accepted.MessageType == "protocol.accepted", "hello-is-accepted");

            var statusGet = new MessageEnvelope
            {
                MessageType = "gateway.status.get",
                Sender = "GatewaySmokeClient",
                SenderInstanceId = Guid.NewGuid().ToString(),
                StationId = stationId,
                Sequence = 2,
                OccurredAtUtc = EnvelopeSerializer.FormatOccurredAtUtc(DateTimeOffset.UtcNow)
            };
            await PipeFraming.WriteFrameAsync(client, EnvelopeSerializer.Serialize(statusGet), CancellationToken.None)
                .ConfigureAwait(false);
            var statusFrame = await PipeFraming
                .ReadFrameAsync(client, ProtocolConstants.MaxControlFrameBytes, CancellationToken.None)
                .ConfigureAwait(false);
            var statusEnvelope = EnvelopeSerializer.Deserialize(statusFrame);
            Require(statusEnvelope != null && statusEnvelope.MessageType == "gateway.status", "status-query-answered");

            var status = JsonSerializer.Deserialize<GatewayStatusPayload>(
                statusEnvelope.Payload.GetRawText(), GatewayJsonOptions.Default);
            Require(status.PlcOnline == false, "status-honestly-reports-no-real-plc-yet");
            Require(
                status.Faults != null && Array.IndexOf(status.Faults, "NO_PLC_CONFIGURED") >= 0,
                "status-flags-missing-plc-configuration");
        }
    }

    private static async Task RunRejectsNonHelloFirstMessageAsync(string pipeName, string stationId)
    {
        using (var client = await ConnectAsync(pipeName).ConfigureAwait(false))
        {
            var badFirst = new MessageEnvelope
            {
                MessageType = "gateway.status.get",
                Sender = "GatewaySmokeClient",
                SenderInstanceId = Guid.NewGuid().ToString(),
                StationId = stationId,
                Sequence = 1,
                OccurredAtUtc = EnvelopeSerializer.FormatOccurredAtUtc(DateTimeOffset.UtcNow)
            };
            await PipeFraming.WriteFrameAsync(client, EnvelopeSerializer.Serialize(badFirst), CancellationToken.None)
                .ConfigureAwait(false);
            Require(await IsClosedByServerAsync(client).ConfigureAwait(false), "non-hello-first-message-closes-connection");
        }
    }

    private static async Task RunRejectsOversizedFrameHeaderAsync(string pipeName)
    {
        using (var client = await ConnectAsync(pipeName).ConfigureAwait(false))
        {
            uint claimedLength = ProtocolConstants.MaxControlFrameBytes + 1;
            var oversizedHeader = new byte[4];
            oversizedHeader[0] = (byte)(claimedLength & 0xFF);
            oversizedHeader[1] = (byte)((claimedLength >> 8) & 0xFF);
            oversizedHeader[2] = (byte)((claimedLength >> 16) & 0xFF);
            oversizedHeader[3] = (byte)((claimedLength >> 24) & 0xFF);

            await client.WriteAsync(oversizedHeader, 0, oversizedHeader.Length).ConfigureAwait(false);
            await client.FlushAsync().ConfigureAwait(false);
            Require(await IsClosedByServerAsync(client).ConfigureAwait(false), "oversized-frame-header-closes-connection");
        }
    }

    private static async Task RunRejectsNonIncreasingSequenceAsync(string pipeName, string stationId)
    {
        using (var client = await ConnectAsync(pipeName).ConfigureAwait(false))
        {
            await HandshakeAsync(client, stationId, 5).ConfigureAwait(false);

            var replay = new MessageEnvelope
            {
                MessageType = "gateway.status.get",
                Sender = "GatewaySmokeClient",
                SenderInstanceId = Guid.NewGuid().ToString(),
                StationId = stationId,
                Sequence = 5,
                OccurredAtUtc = EnvelopeSerializer.FormatOccurredAtUtc(DateTimeOffset.UtcNow)
            };
            await PipeFraming.WriteFrameAsync(client, EnvelopeSerializer.Serialize(replay), CancellationToken.None)
                .ConfigureAwait(false);
            Require(await IsClosedByServerAsync(client).ConfigureAwait(false), "non-increasing-sequence-closes-connection");
        }
    }

    private static async Task RunSessionPrepareRejectedForMissingFieldsAsync(string pipeName, string stationId)
    {
        using (var client = await ConnectAsync(pipeName).ConfigureAwait(false))
        {
            await HandshakeAsync(client, stationId, 1).ConfigureAwait(false);

            var prepare = new MessageEnvelope
            {
                MessageType = "gateway.session.prepare",
                Sender = "GatewaySmokeClient",
                SenderInstanceId = Guid.NewGuid().ToString(),
                StationId = stationId,
                // operationRunId and every payload field deliberately omitted.
                Sequence = 2,
                OccurredAtUtc = EnvelopeSerializer.FormatOccurredAtUtc(DateTimeOffset.UtcNow)
            };
            await PipeFraming.WriteFrameAsync(client, EnvelopeSerializer.Serialize(prepare), CancellationToken.None)
                .ConfigureAwait(false);
            var responseFrame = await PipeFraming
                .ReadFrameAsync(client, ProtocolConstants.MaxControlFrameBytes, CancellationToken.None)
                .ConfigureAwait(false);
            var response = EnvelopeSerializer.Deserialize(responseFrame);
            Require(response != null && response.MessageType == "gateway.session.rejected", "incomplete-prepare-rejected");

            var error = JsonSerializer.Deserialize<GatewayErrorPayload>(
                response.Payload.GetRawText(), GatewayJsonOptions.Default);
            Require(error.ErrorCode == "CONTRACT_VALIDATION_FAILED", "incomplete-prepare-flagged-as-contract-violation");
            Require(!error.Retryable, "contract-violation-is-not-retryable");
        }
    }

    private static async Task RunSessionPrepareAlwaysPlcOfflineWithoutSimulationAsync(string pipeName, string stationId)
    {
        using (var client = await ConnectAsync(pipeName).ConfigureAwait(false))
        {
            await HandshakeAsync(client, stationId, 1).ConfigureAwait(false);

            var prepare = BuildSessionPrepare(stationId, Guid.NewGuid().ToString(), 2, "Local", 30000);
            await PipeFraming.WriteFrameAsync(client, EnvelopeSerializer.Serialize(prepare), CancellationToken.None)
                .ConfigureAwait(false);
            var responseFrame = await PipeFraming
                .ReadFrameAsync(client, ProtocolConstants.MaxControlFrameBytes, CancellationToken.None)
                .ConfigureAwait(false);
            var response = EnvelopeSerializer.Deserialize(responseFrame);
            Require(
                response != null && response.MessageType == "gateway.session.rejected",
                "well-formed-prepare-still-rejected-without-simulation-opt-in");

            var error = JsonSerializer.Deserialize<GatewayErrorPayload>(
                response.Payload.GetRawText(), GatewayJsonOptions.Default);
            Require(error.ErrorCode == "PLC_OFFLINE", "no-simulation-station-rejects-as-plc-offline-not-contract-error");
            Require(error.Retryable, "plc-offline-is-marked-retryable");
        }
    }

    private static async Task RunSessionPrepareRejectsControlModeMismatchAsync(string pipeName, string stationId)
    {
        using (var client = await ConnectAsync(pipeName).ConfigureAwait(false))
        {
            await HandshakeAsync(client, stationId, 1).ConfigureAwait(false);

            // SimulatedDeviceFrameSource always reports "Local"; asking for "Remote"
            // must be rejected as a mode mismatch rather than silently granted.
            var prepare = BuildSessionPrepare(stationId, Guid.NewGuid().ToString(), 2, "Remote", 30000);
            await PipeFraming.WriteFrameAsync(client, EnvelopeSerializer.Serialize(prepare), CancellationToken.None)
                .ConfigureAwait(false);
            var responseFrame = await PipeFraming
                .ReadFrameAsync(client, ProtocolConstants.MaxControlFrameBytes, CancellationToken.None)
                .ConfigureAwait(false);
            var response = EnvelopeSerializer.Deserialize(responseFrame);
            Require(response != null && response.MessageType == "gateway.session.rejected", "mode-mismatch-prepare-rejected");

            var error = JsonSerializer.Deserialize<GatewayErrorPayload>(
                response.Payload.GetRawText(), GatewayJsonOptions.Default);
            Require(error.ErrorCode == "CONTROL_MODE_MISMATCH", "mode-mismatch-flagged-with-correct-error-code");
            Require(error.Retryable, "mode-mismatch-is-marked-retryable");
        }
    }

    // Reuses ModeCheckStationId: the mode-mismatch prepare above never issues a lease
    // (it is rejected before that point), so this station is still guaranteed lease-free
    // here, making it a clean base case for "release when nothing was ever leased".
    private static async Task RunLeaseReleaseRejectsWhenNoLeaseExistsAsync(string pipeName, string stationId)
    {
        using (var client = await ConnectAsync(pipeName).ConfigureAwait(false))
        {
            await HandshakeAsync(client, stationId, 1).ConfigureAwait(false);

            var release = new MessageEnvelope
            {
                MessageType = "gateway.lease.release",
                Sender = "GatewaySmokeClient",
                SenderInstanceId = Guid.NewGuid().ToString(),
                StationId = stationId,
                OperationRunId = Guid.NewGuid().ToString(),
                Sequence = 2,
                OccurredAtUtc = EnvelopeSerializer.FormatOccurredAtUtc(DateTimeOffset.UtcNow)
            };
            await PipeFraming.WriteFrameAsync(client, EnvelopeSerializer.Serialize(release), CancellationToken.None)
                .ConfigureAwait(false);
            var responseFrame = await PipeFraming
                .ReadFrameAsync(client, ProtocolConstants.MaxControlFrameBytes, CancellationToken.None)
                .ConfigureAwait(false);
            var response = EnvelopeSerializer.Deserialize(responseFrame);
            Require(response != null && response.MessageType == "gateway.lease.rejected", "release-without-any-lease-is-rejected");

            var error = JsonSerializer.Deserialize<GatewayErrorPayload>(
                response.Payload.GetRawText(), GatewayJsonOptions.Default);
            Require(error.ErrorCode == "OPERATION_RUN_MISMATCH", "release-without-lease-flagged-as-operation-run-mismatch");
            Require(!error.Retryable, "operation-run-mismatch-is-not-retryable");
        }
    }

    private static async Task RunSessionPrepareLeaseLifecycleAsync(string pipeName, string stationId)
    {
        var firstRunId = Guid.NewGuid().ToString();

        // Connection 1: acquire the station's only lease.
        GatewaySessionLease firstLease;
        using (var client = await ConnectAsync(pipeName).ConfigureAwait(false))
        {
            await HandshakeAsync(client, stationId, 1).ConfigureAwait(false);
            var prepare = BuildSessionPrepare(stationId, firstRunId, 2, "Local", 60000);
            await PipeFraming.WriteFrameAsync(client, EnvelopeSerializer.Serialize(prepare), CancellationToken.None)
                .ConfigureAwait(false);
            var responseFrame = await PipeFraming
                .ReadFrameAsync(client, ProtocolConstants.MaxControlFrameBytes, CancellationToken.None)
                .ConfigureAwait(false);
            var response = EnvelopeSerializer.Deserialize(responseFrame);
            Require(response != null && response.MessageType == "gateway.session.prepared", "first-prepare-on-free-station-succeeds");

            firstLease = JsonSerializer.Deserialize<GatewaySessionLease>(
                response.Payload.GetRawText(), GatewayJsonOptions.Default);
            Require(!string.IsNullOrEmpty(firstLease.LeaseId), "prepared-lease-has-an-id");
            Require(firstLease.Status == "Reserved", "prepared-lease-status-is-reserved");
            Require(firstLease.IsSimulated, "prepared-lease-is-marked-simulated");
        }

        // Connection 2: a different run on the same station must conflict.
        using (var client = await ConnectAsync(pipeName).ConfigureAwait(false))
        {
            await HandshakeAsync(client, stationId, 1).ConfigureAwait(false);
            var prepare = BuildSessionPrepare(stationId, Guid.NewGuid().ToString(), 2, "Local", 30000);
            await PipeFraming.WriteFrameAsync(client, EnvelopeSerializer.Serialize(prepare), CancellationToken.None)
                .ConfigureAwait(false);
            var responseFrame = await PipeFraming
                .ReadFrameAsync(client, ProtocolConstants.MaxControlFrameBytes, CancellationToken.None)
                .ConfigureAwait(false);
            var response = EnvelopeSerializer.Deserialize(responseFrame);
            Require(response != null && response.MessageType == "gateway.session.rejected", "second-prepare-while-leased-is-rejected");

            var error = JsonSerializer.Deserialize<GatewayErrorPayload>(
                response.Payload.GetRawText(), GatewayJsonOptions.Default);
            Require(error.ErrorCode == "CONTROL_LEASE_CONFLICT", "conflicting-prepare-flagged-as-lease-conflict");
            Require(!error.Retryable, "lease-conflict-is-not-retryable");
        }

        // Connection 3: release the first run's lease.
        using (var client = await ConnectAsync(pipeName).ConfigureAwait(false))
        {
            await HandshakeAsync(client, stationId, 1).ConfigureAwait(false);
            var release = new MessageEnvelope
            {
                MessageType = "gateway.lease.release",
                Sender = "GatewaySmokeClient",
                SenderInstanceId = Guid.NewGuid().ToString(),
                StationId = stationId,
                OperationRunId = firstRunId,
                Sequence = 2,
                OccurredAtUtc = EnvelopeSerializer.FormatOccurredAtUtc(DateTimeOffset.UtcNow)
            };
            await PipeFraming.WriteFrameAsync(client, EnvelopeSerializer.Serialize(release), CancellationToken.None)
                .ConfigureAwait(false);
            var responseFrame = await PipeFraming
                .ReadFrameAsync(client, ProtocolConstants.MaxControlFrameBytes, CancellationToken.None)
                .ConfigureAwait(false);
            var response = EnvelopeSerializer.Deserialize(responseFrame);
            Require(response != null && response.MessageType == "gateway.lease.released", "matching-release-is-accepted");

            var releasedPayload = JsonSerializer.Deserialize<GatewayLeaseReleasedPayload>(
                response.Payload.GetRawText(), GatewayJsonOptions.Default);
            Require(releasedPayload.LeaseId == firstLease.LeaseId, "released-payload-echoes-the-correct-leaseId");
        }

        // Connection 4: the station is free again, and the new lease's epoch advanced.
        using (var client = await ConnectAsync(pipeName).ConfigureAwait(false))
        {
            await HandshakeAsync(client, stationId, 1).ConfigureAwait(false);
            var prepare = BuildSessionPrepare(stationId, Guid.NewGuid().ToString(), 2, "Local", 30000);
            await PipeFraming.WriteFrameAsync(client, EnvelopeSerializer.Serialize(prepare), CancellationToken.None)
                .ConfigureAwait(false);
            var responseFrame = await PipeFraming
                .ReadFrameAsync(client, ProtocolConstants.MaxControlFrameBytes, CancellationToken.None)
                .ConfigureAwait(false);
            var response = EnvelopeSerializer.Deserialize(responseFrame);
            Require(response != null && response.MessageType == "gateway.session.prepared", "prepare-after-release-succeeds-again");

            var secondLease = JsonSerializer.Deserialize<GatewaySessionLease>(
                response.Payload.GetRawText(), GatewayJsonOptions.Default);
            Require(secondLease.LeaseEpoch == firstLease.LeaseEpoch + 1, "lease-epoch-advances-across-station-lease-generations");
        }
    }

    // Exercises the Environment.TickCount64-based expiry path with a real (short) wait,
    // rather than only reviewing GatewayControlPipeServer.ExpireLeaseIfStaleNoLock by eye
    // (a known gap flagged when the lease lifecycle was first built).
    private static async Task RunSessionPrepareLeaseExpiresAndFreesTheStationAsync(string pipeName, string stationId)
    {
        const long shortLeaseDurationMs = 300;
        var firstRunId = Guid.NewGuid().ToString();

        // Connection 1: acquire a short-lived lease and let the connection close.
        GatewaySessionLease firstLease;
        using (var client = await ConnectAsync(pipeName).ConfigureAwait(false))
        {
            await HandshakeAsync(client, stationId, 1).ConfigureAwait(false);
            var prepare = BuildSessionPrepare(stationId, firstRunId, 2, "Local", shortLeaseDurationMs);
            await PipeFraming.WriteFrameAsync(client, EnvelopeSerializer.Serialize(prepare), CancellationToken.None)
                .ConfigureAwait(false);
            var responseFrame = await PipeFraming
                .ReadFrameAsync(client, ProtocolConstants.MaxControlFrameBytes, CancellationToken.None)
                .ConfigureAwait(false);
            var response = EnvelopeSerializer.Deserialize(responseFrame);
            Require(response != null && response.MessageType == "gateway.session.prepared", "short-lease-prepare-succeeds");

            firstLease = JsonSerializer.Deserialize<GatewaySessionLease>(
                response.Payload.GetRawText(), GatewayJsonOptions.Default);
        }

        // Connection 2: immediately retrying (before the lease's own duration has
        // elapsed) must still conflict - this is not just "any later prepare succeeds"
        // but specifically proves the station is genuinely still held right after issuance.
        using (var client = await ConnectAsync(pipeName).ConfigureAwait(false))
        {
            await HandshakeAsync(client, stationId, 1).ConfigureAwait(false);
            var prepare = BuildSessionPrepare(stationId, Guid.NewGuid().ToString(), 2, "Local", 30000);
            await PipeFraming.WriteFrameAsync(client, EnvelopeSerializer.Serialize(prepare), CancellationToken.None)
                .ConfigureAwait(false);
            var responseFrame = await PipeFraming
                .ReadFrameAsync(client, ProtocolConstants.MaxControlFrameBytes, CancellationToken.None)
                .ConfigureAwait(false);
            var response = EnvelopeSerializer.Deserialize(responseFrame);
            Require(
                response != null && response.MessageType == "gateway.session.rejected",
                "immediate-retry-before-expiry-still-conflicts");

            var error = JsonSerializer.Deserialize<GatewayErrorPayload>(
                response.Payload.GetRawText(), GatewayJsonOptions.Default);
            Require(error.ErrorCode == "CONTROL_LEASE_CONFLICT", "immediate-retry-conflict-has-correct-error-code");
        }

        // No gateway.owner.heartbeat exists yet to keep the lease alive, so waiting past
        // its own requested duration must be enough for the next prepare to reclaim it.
        await Task.Delay((int)shortLeaseDurationMs + 400).ConfigureAwait(false);

        // Connection 2.5: expiry is lazy (checked on the next request, not by a timer),
        // so a release that finally arrives for the now-expired lease must be rejected
        // rather than accepted or left to hang - this exact interaction was an explicitly
        // flagged, previously-untested gap.
        using (var client = await ConnectAsync(pipeName).ConfigureAwait(false))
        {
            await HandshakeAsync(client, stationId, 1).ConfigureAwait(false);
            var lateRelease = new MessageEnvelope
            {
                MessageType = "gateway.lease.release",
                Sender = "GatewaySmokeClient",
                SenderInstanceId = Guid.NewGuid().ToString(),
                StationId = stationId,
                OperationRunId = firstRunId,
                Sequence = 2,
                OccurredAtUtc = EnvelopeSerializer.FormatOccurredAtUtc(DateTimeOffset.UtcNow)
            };
            await PipeFraming.WriteFrameAsync(client, EnvelopeSerializer.Serialize(lateRelease), CancellationToken.None)
                .ConfigureAwait(false);
            var responseFrame = await PipeFraming
                .ReadFrameAsync(client, ProtocolConstants.MaxControlFrameBytes, CancellationToken.None)
                .ConfigureAwait(false);
            var response = EnvelopeSerializer.Deserialize(responseFrame);
            Require(
                response != null && response.MessageType == "gateway.lease.rejected",
                "late-release-of-already-expired-lease-is-rejected-not-accepted");

            var error = JsonSerializer.Deserialize<GatewayErrorPayload>(
                response.Payload.GetRawText(), GatewayJsonOptions.Default);
            Require(error.ErrorCode == "OPERATION_RUN_MISMATCH", "late-release-flagged-as-operation-run-mismatch");
        }

        // Connection 3: after the wait, a new prepare must succeed and the epoch must
        // advance - proving the stale lease was actually cleared, not merely ignored.
        using (var client = await ConnectAsync(pipeName).ConfigureAwait(false))
        {
            await HandshakeAsync(client, stationId, 1).ConfigureAwait(false);
            var prepare = BuildSessionPrepare(stationId, Guid.NewGuid().ToString(), 2, "Local", 30000);
            await PipeFraming.WriteFrameAsync(client, EnvelopeSerializer.Serialize(prepare), CancellationToken.None)
                .ConfigureAwait(false);
            var responseFrame = await PipeFraming
                .ReadFrameAsync(client, ProtocolConstants.MaxControlFrameBytes, CancellationToken.None)
                .ConfigureAwait(false);
            var response = EnvelopeSerializer.Deserialize(responseFrame);
            Require(
                response != null && response.MessageType == "gateway.session.prepared",
                "prepare-after-natural-expiry-succeeds");

            var secondLease = JsonSerializer.Deserialize<GatewaySessionLease>(
                response.Payload.GetRawText(), GatewayJsonOptions.Default);
            Require(secondLease.LeaseEpoch == firstLease.LeaseEpoch + 1, "expired-lease-epoch-still-advances-monotonically");
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

    private static MessageEnvelope BuildRealtimeHello(
        string stationId, string operationRunId, string sessionToken, string leaseId, string sceneInstanceId, string streamId)
    {
        return new MessageEnvelope
        {
            MessageType = "protocol.hello",
            Sender = "GatewaySmokeClient",
            SenderInstanceId = Guid.NewGuid().ToString(),
            StationId = stationId,
            OperationRunId = operationRunId,
            Sequence = 1,
            OccurredAtUtc = EnvelopeSerializer.FormatOccurredAtUtc(DateTimeOffset.UtcNow),
            Payload = JsonSerializer.SerializeToElement(
                new RealtimeHelloPayload
                {
                    SessionToken = sessionToken,
                    MachineType = "YC",
                    LeaseId = leaseId,
                    SceneInstanceId = sceneInstanceId,
                    StreamId = streamId,
                    SceneVersion = "YC-SCENE-V1",
                    ProtocolVersion = "1.1.0",
                    PointMapVersion = "YC-IO-1.0.0",
                    RequestedFrameIntervalMs = 20
                },
                GatewayJsonOptions.Default)
        };
    }

    // PT-IF-007 skeleton: proves the realtime pipe's protocol.hello genuinely validates
    // against the SAME lease the control pipe issues (not just "any string works"), and
    // that a real periodic equipment.inputFrame stream follows a successful handshake.
    private static async Task RunRealtimeHandshakeAndStreamingAsync(
        string controlPipeName, string realtimePipeName, string stationId)
    {
        // Step 0: malformed hello (every payload field missing) must be rejected on
        // contract grounds alone, independent of any lease state.
        using (var client = await ConnectAsync(realtimePipeName).ConfigureAwait(false))
        {
            var badHello = new MessageEnvelope
            {
                MessageType = "protocol.hello",
                Sender = "GatewaySmokeClient",
                SenderInstanceId = Guid.NewGuid().ToString(),
                StationId = stationId,
                OperationRunId = Guid.NewGuid().ToString(),
                Sequence = 1,
                OccurredAtUtc = EnvelopeSerializer.FormatOccurredAtUtc(DateTimeOffset.UtcNow)
            };
            await PipeFraming.WriteFrameAsync(client, EnvelopeSerializer.Serialize(badHello), CancellationToken.None)
                .ConfigureAwait(false);
            var responseFrame = await PipeFraming
                .ReadFrameAsync(client, ProtocolConstants.MaxRealtimeFrameBytes, CancellationToken.None)
                .ConfigureAwait(false);
            var response = EnvelopeSerializer.Deserialize(responseFrame);
            Require(response != null && response.MessageType == "protocol.rejected", "realtime-hello-missing-fields-is-rejected");

            var error = JsonSerializer.Deserialize<GatewayErrorPayload>(
                response.Payload.GetRawText(), GatewayJsonOptions.Default);
            Require(error.ErrorCode == "CONTRACT_VALIDATION_FAILED", "realtime-hello-missing-fields-flagged-as-contract-violation");
        }

        // Step 1: no lease exists yet on this station -> a structurally valid hello must
        // still be rejected, since there is nothing to authenticate it against.
        using (var client = await ConnectAsync(realtimePipeName).ConfigureAwait(false))
        {
            var hello = BuildRealtimeHello(
                stationId, Guid.NewGuid().ToString(), "irrelevant-token",
                Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
            await PipeFraming.WriteFrameAsync(client, EnvelopeSerializer.Serialize(hello), CancellationToken.None)
                .ConfigureAwait(false);
            var responseFrame = await PipeFraming
                .ReadFrameAsync(client, ProtocolConstants.MaxRealtimeFrameBytes, CancellationToken.None)
                .ConfigureAwait(false);
            var response = EnvelopeSerializer.Deserialize(responseFrame);
            Require(
                response != null && response.MessageType == "protocol.rejected",
                "realtime-hello-without-any-lease-is-rejected");

            var error = JsonSerializer.Deserialize<GatewayErrorPayload>(
                response.Payload.GetRawText(), GatewayJsonOptions.Default);
            Require(error.ErrorCode == "AUTHENTICATION_FAILED", "realtime-hello-without-lease-flagged-as-authentication-failed");
        }

        // Step 2: issue a real lease via the control pipe, with a genuine session token
        // hash (not the "sha256:aaaa...a" placeholder used elsewhere - this flow needs a
        // raw token that actually hashes to the registered value).
        var operationRunId = Guid.NewGuid().ToString();
        var sceneInstanceId = Guid.NewGuid().ToString();
        var rawToken = "smoke-test-session-token-" + Guid.NewGuid();
        var tokenHash = ComputeSessionTokenHash(rawToken);
        string leaseId;
        using (var client = await ConnectAsync(controlPipeName).ConfigureAwait(false))
        {
            await HandshakeAsync(client, stationId, 1).ConfigureAwait(false);
            var prepare = new MessageEnvelope
            {
                MessageType = "gateway.session.prepare",
                Sender = "GatewaySmokeClient",
                SenderInstanceId = Guid.NewGuid().ToString(),
                StationId = stationId,
                OperationRunId = operationRunId,
                Sequence = 2,
                OccurredAtUtc = EnvelopeSerializer.FormatOccurredAtUtc(DateTimeOffset.UtcNow),
                Payload = JsonSerializer.SerializeToElement(
                    new GatewaySessionPreparePayload
                    {
                        OwnerInstanceId = Guid.NewGuid().ToString(),
                        SceneInstanceId = sceneInstanceId,
                        SessionTokenHash = tokenHash,
                        WorkOrderSnapshotHash = "sha256:" + new string('b', 64),
                        ControlMode = "Local",
                        SceneVersion = "YC-SCENE-V1",
                        PointMapVersion = "YC-IO-1.0.0",
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
            Require(response != null && response.MessageType == "gateway.session.prepared", "realtime-test-lease-issued-successfully");

            var lease = JsonSerializer.Deserialize<GatewaySessionLease>(
                response.Payload.GetRawText(), GatewayJsonOptions.Default);
            leaseId = lease.LeaseId;
        }

        // Step 3: correct leaseId/sceneInstanceId but a wrong token must still be
        // rejected - proves the token check is real, not skipped once a lease exists.
        using (var client = await ConnectAsync(realtimePipeName).ConfigureAwait(false))
        {
            var hello = BuildRealtimeHello(
                stationId, operationRunId, "this-is-not-the-right-token", leaseId, sceneInstanceId, Guid.NewGuid().ToString());
            await PipeFraming.WriteFrameAsync(client, EnvelopeSerializer.Serialize(hello), CancellationToken.None)
                .ConfigureAwait(false);
            var responseFrame = await PipeFraming
                .ReadFrameAsync(client, ProtocolConstants.MaxRealtimeFrameBytes, CancellationToken.None)
                .ConfigureAwait(false);
            var response = EnvelopeSerializer.Deserialize(responseFrame);
            Require(
                response != null && response.MessageType == "protocol.rejected",
                "realtime-hello-with-wrong-token-is-rejected");

            var error = JsonSerializer.Deserialize<GatewayErrorPayload>(
                response.Payload.GetRawText(), GatewayJsonOptions.Default);
            Require(error.ErrorCode == "AUTHENTICATION_FAILED", "wrong-token-flagged-as-authentication-failed");
        }

        // Step 4: the correct token is accepted, and a real periodic equipment.inputFrame
        // stream follows - checked across two frames to prove it is a stream, not a
        // one-shot reply.
        var streamId = Guid.NewGuid().ToString();
        using (var client = await ConnectAsync(realtimePipeName).ConfigureAwait(false))
        {
            var hello = BuildRealtimeHello(stationId, operationRunId, rawToken, leaseId, sceneInstanceId, streamId);
            await PipeFraming.WriteFrameAsync(client, EnvelopeSerializer.Serialize(hello), CancellationToken.None)
                .ConfigureAwait(false);
            var responseFrame = await PipeFraming
                .ReadFrameAsync(client, ProtocolConstants.MaxRealtimeFrameBytes, CancellationToken.None)
                .ConfigureAwait(false);
            var response = EnvelopeSerializer.Deserialize(responseFrame);
            Require(
                response != null && response.MessageType == "protocol.accepted",
                "realtime-hello-with-correct-token-is-accepted");

            var firstFrameBytes = await PipeFraming
                .ReadFrameAsync(client, ProtocolConstants.MaxRealtimeFrameBytes, CancellationToken.None)
                .ConfigureAwait(false);
            var firstFrame = JsonSerializer.Deserialize<EquipmentInputFrame>(firstFrameBytes, GatewayJsonOptions.Default);
            Require(
                firstFrame != null && firstFrame.MessageType == "equipment.inputFrame",
                "received-a-real-equipment-input-frame");
            Require(firstFrame.OperationRunId == operationRunId, "input-frame-carries-correct-operationRunId");
            Require(firstFrame.StreamId == streamId, "input-frame-echoes-the-streamId-from-hello");
            Require(firstFrame.Sequence == 1, "first-input-frame-sequence-starts-at-one");
            Require(firstFrame.PlcOnline == false, "input-frame-honestly-reports-no-real-plc");
            Require(firstFrame.EmergencyStopReleased, "input-frame-reflects-simulated-safe-estop-state");

            var secondFrameBytes = await PipeFraming
                .ReadFrameAsync(client, ProtocolConstants.MaxRealtimeFrameBytes, CancellationToken.None)
                .ConfigureAwait(false);
            var secondFrame = JsonSerializer.Deserialize<EquipmentInputFrame>(secondFrameBytes, GatewayJsonOptions.Default);
            Require(secondFrame.Sequence == 2, "second-input-frame-sequence-advances");
        }

        // Step 5: reconnecting with the SAME streamId (everything else still valid -
        // correct token, correct leaseId/sceneInstanceId) must be rejected. V1.1 section
        // 3.2/10.1 requires a new streamId on every reconnect; this proves Gateway
        // actually enforces it rather than only documenting it.
        using (var client = await ConnectAsync(realtimePipeName).ConfigureAwait(false))
        {
            var hello = BuildRealtimeHello(stationId, operationRunId, rawToken, leaseId, sceneInstanceId, streamId);
            await PipeFraming.WriteFrameAsync(client, EnvelopeSerializer.Serialize(hello), CancellationToken.None)
                .ConfigureAwait(false);
            var responseFrame = await PipeFraming
                .ReadFrameAsync(client, ProtocolConstants.MaxRealtimeFrameBytes, CancellationToken.None)
                .ConfigureAwait(false);
            var response = EnvelopeSerializer.Deserialize(responseFrame);
            Require(
                response != null && response.MessageType == "protocol.rejected",
                "reused-streamId-on-reconnect-is-rejected");

            var error = JsonSerializer.Deserialize<GatewayErrorPayload>(
                response.Payload.GetRawText(), GatewayJsonOptions.Default);
            Require(error.ErrorCode == "AUTHENTICATION_FAILED", "reused-streamId-flagged-as-authentication-failed");
        }
    }

    private static async Task<bool> IsClosedByServerAsync(NamedPipeClientStream client)
    {
        try
        {
            var buffer = new byte[16];
            using (var cts = new CancellationTokenSource(2000))
            {
                var read = await client.ReadAsync(buffer, 0, buffer.Length, cts.Token).ConfigureAwait(false);
                return read == 0;
            }
        }
        catch (IOException)
        {
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static void Require(bool condition, string name)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(name);
        Console.WriteLine("PASS " + name);
    }
}
