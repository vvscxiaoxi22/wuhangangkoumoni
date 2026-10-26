using System;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PortTraining.PortGatewayService.Protocol;

// Manual PT-IF-006 launch helper for real integration test days when no scene
// launcher exists yet: connects to an ALREADY RUNNING PortGatewayService process
// (unlike GatewaySmokeClient/GatewayDebugClientHarness, this does not start its own
// server) and runs a real gateway.session.prepare, printing the values a human needs
// to hand to YardCraneSimulator's "Tools/Yard Crane/Gateway/Realtime Connection
// Test..." Editor window: leaseId, sessionToken (raw), operationRunId, sceneInstanceId.
//
// Usage:
//   dotnet run --project Tools/ManualSessionLauncher/ManualSessionLauncher.csproj -- --station-id DEV-01 --control-mode Local
//   dotnet run --project Tools/ManualSessionLauncher/ManualSessionLauncher.csproj -- --station-id DEV-01 --release <operationRunId>
internal static class Program
{
    private static async Task Main(string[] args)
    {
        var stationId = GetArg(args, "--station-id") ?? "DEV-01";
        var releaseOperationRunId = GetArg(args, "--release");

        var controlPipeName = PipeNaming.BuildControlPipeName(stationId);

        if (releaseOperationRunId != null)
        {
            await ReleaseAsync(controlPipeName, stationId, releaseOperationRunId).ConfigureAwait(false);
            return;
        }

        var controlMode = GetArg(args, "--control-mode") ?? "Local";
        var leaseDurationMs = long.Parse(GetArg(args, "--lease-duration-ms") ?? "1800000");
        await PrepareAsync(controlPipeName, stationId, controlMode, leaseDurationMs).ConfigureAwait(false);
    }

    private static async Task PrepareAsync(string controlPipeName, string stationId, string controlMode, long leaseDurationMs)
    {
        var operationRunId = Guid.NewGuid().ToString();
        var sceneInstanceId = Guid.NewGuid().ToString();
        var rawToken = GenerateSessionToken();
        var tokenHash = ComputeSessionTokenHash(rawToken);

        Console.WriteLine($"[ManualSessionLauncher] connecting to control pipe '{controlPipeName}'...");
        Console.WriteLine("[ManualSessionLauncher] make sure PortGatewayService is already running " +
                           "(dotnet run --project PortGatewayService.csproj, or the built .exe).");

        try
        {
            using (var client = new NamedPipeClientStream(".", controlPipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                await client.ConnectAsync(5000).ConfigureAwait(false);

                var hello = new MessageEnvelope
                {
                    MessageType = "protocol.hello",
                    Sender = "ManualSessionLauncher",
                    SenderInstanceId = Guid.NewGuid().ToString(),
                    StationId = stationId,
                    Sequence = 1,
                    OccurredAtUtc = EnvelopeSerializer.FormatOccurredAtUtc(DateTimeOffset.UtcNow)
                };
                await PipeFraming.WriteFrameAsync(client, EnvelopeSerializer.Serialize(hello), CancellationToken.None)
                    .ConfigureAwait(false);
                var helloReplyFrame = await PipeFraming
                    .ReadFrameAsync(client, ProtocolConstants.MaxControlFrameBytes, CancellationToken.None)
                    .ConfigureAwait(false);
                var helloReply = EnvelopeSerializer.Deserialize(helloReplyFrame);
                if (helloReply == null || helloReply.MessageType != "protocol.accepted")
                {
                    Console.WriteLine($"[ManualSessionLauncher] FAILED: hello was not accepted (messageType={helloReply?.MessageType}).");
                    return;
                }

                var prepare = new MessageEnvelope
                {
                    MessageType = "gateway.session.prepare",
                    Sender = "ManualSessionLauncher",
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
                            WorkOrderSnapshotHash = "sha256:" + new string('0', 64),
                            ControlMode = controlMode,
                            SceneVersion = "YC-MANUAL-TEST",
                            PointMapVersion = "YC-IO-0.1.0-DRAFT",
                            RequestedLeaseDurationMs = leaseDurationMs
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
                    var error = response != null
                        ? JsonSerializer.Deserialize<GatewayErrorPayload>(response.Payload.GetRawText(), GatewayJsonOptions.Default)
                        : null;
                    Console.WriteLine(
                        $"[ManualSessionLauncher] FAILED: gateway.session.prepare rejected. " +
                        $"errorCode={error?.ErrorCode}, message={error?.Message}");
                    return;
                }

                var lease = JsonSerializer.Deserialize<GatewaySessionLease>(
                    response.Payload.GetRawText(), GatewayJsonOptions.Default);

                Console.WriteLine();
                Console.WriteLine("========================================================");
                Console.WriteLine(" gateway.session.prepare 成功 - 把下面这些值填进Unity的");
                Console.WriteLine(" Tools/Yard Crane/Gateway/Realtime Connection Test... 窗口");
                Console.WriteLine("========================================================");
                Console.WriteLine($" Station Id        : {stationId}");
                Console.WriteLine($" Operation Run Id  : {operationRunId}");
                Console.WriteLine($" Lease Id          : {lease.LeaseId}");
                Console.WriteLine($" Scene Instance Id : {sceneInstanceId}");
                Console.WriteLine($" Session Token(raw): {rawToken}");
                Console.WriteLine($" Lease Epoch       : {lease.LeaseEpoch}");
                Console.WriteLine($" Is Simulated      : {lease.IsSimulated}");
                Console.WriteLine($" Expires At (UTC)  : {lease.ExpiresAtUtc}");
                Console.WriteLine("========================================================");
                Console.WriteLine(" 用完后释放这个租约（否则要等到期才能再签发新的）：");
                Console.WriteLine(
                    $"   dotnet run --project Tools/ManualSessionLauncher/ManualSessionLauncher.csproj -- " +
                    $"--station-id {stationId} --release {operationRunId}");
                Console.WriteLine("========================================================");
                Console.WriteLine();
                Console.WriteLine(
                    "[ManualSessionLauncher] 会话令牌是敏感值（V1.1第17节：一次性会话令牌不得写入日志），" +
                    "仅用于本次手动测试，测试完成后不要保留在工单记录或持久化日志文本里。");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ManualSessionLauncher] FAILED: {ex.Message}");
            Console.WriteLine("[ManualSessionLauncher] 确认PortGatewayService进程已经在运行、stationId拼写一致。");
        }
    }

    private static async Task ReleaseAsync(string controlPipeName, string stationId, string operationRunId)
    {
        Console.WriteLine($"[ManualSessionLauncher] releasing lease for operationRunId={operationRunId} on station {stationId}...");

        try
        {
            using (var client = new NamedPipeClientStream(".", controlPipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                await client.ConnectAsync(5000).ConfigureAwait(false);

                var hello = new MessageEnvelope
                {
                    MessageType = "protocol.hello",
                    Sender = "ManualSessionLauncher",
                    SenderInstanceId = Guid.NewGuid().ToString(),
                    StationId = stationId,
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
                    Sender = "ManualSessionLauncher",
                    SenderInstanceId = Guid.NewGuid().ToString(),
                    StationId = stationId,
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

                if (response != null && response.MessageType == "gateway.lease.released")
                {
                    Console.WriteLine("[ManualSessionLauncher] released successfully.");
                }
                else
                {
                    Console.WriteLine($"[ManualSessionLauncher] release failed or lease did not match: messageType={response?.MessageType}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ManualSessionLauncher] FAILED: {ex.Message}");
        }
    }

    private static string GenerateSessionToken()
    {
        // V1.1 section 9.1: "令牌为至少256位随机值" (at least 256 bits of randomness).
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string ComputeSessionTokenHash(string rawToken)
    {
        using (var sha256 = SHA256.Create())
        {
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawToken));
            return "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }

    private static string GetArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        }

        return null;
    }
}
