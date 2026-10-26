using System;
using System.Threading;
using System.Threading.Tasks;
using PortTraining.YardCraneSimulator.Gateway;

// Command-line twin of "Tools/Yard Crane/Gateway/Realtime Connection Test..." (the
// Unity Editor window) - same underlying GatewayRealtimeDebugClient code, driven from
// the command line instead of an EditorWindow. Useful for a quick real-process check
// before touching Unity, or as a fallback if Unity is not ready for the real
// integration attempt. Connects to an ALREADY RUNNING PortGatewayService process; the
// leaseId/sessionToken/sceneInstanceId/operationRunId must come from a prior
// gateway.session.prepare (see PortGatewayService/Tools/ManualSessionLauncher).
//
// Usage:
//   dotnet run --project Tools/GatewayRealtimeManualClient/GatewayRealtimeManualClient.csproj -- ^
//     --station-id DEV-01 --operation-run-id <id> --lease-id <id> --scene-instance-id <id> ^
//     --session-token <raw> [--frames 5]
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var stationId = GetArg(args, "--station-id");
        var operationRunId = GetArg(args, "--operation-run-id");
        var leaseId = GetArg(args, "--lease-id");
        var sceneInstanceId = GetArg(args, "--scene-instance-id");
        var sessionToken = GetArg(args, "--session-token");
        var frames = int.Parse(GetArg(args, "--frames") ?? "5");

        if (string.IsNullOrWhiteSpace(stationId) || string.IsNullOrWhiteSpace(operationRunId) ||
            string.IsNullOrWhiteSpace(leaseId) || string.IsNullOrWhiteSpace(sceneInstanceId) ||
            string.IsNullOrWhiteSpace(sessionToken))
        {
            Console.WriteLine(
                "Usage: --station-id <id> --operation-run-id <id> --lease-id <id> " +
                "--scene-instance-id <id> --session-token <raw> [--frames N]");
            return 1;
        }

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

        var result = await GatewayRealtimeDebugClient.ConnectAndCaptureAsync(
            pipeName, request, frames, 256 * 1024, 5000, Console.WriteLine, CancellationToken.None).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            Console.WriteLine($"[GatewayRealtimeManualClient] FAILED: {result.ErrorMessage}");
            return 1;
        }

        if (!result.HandshakeAccepted)
        {
            Console.WriteLine(
                $"[GatewayRealtimeManualClient] REJECTED: errorCode={result.Reply?.ErrorCode}, " +
                $"message={result.Reply?.ErrorMessage}");
            return 1;
        }

        Console.WriteLine($"[GatewayRealtimeManualClient] SUCCESS: captured {result.CapturedFrames.Count} frame(s).");
        return 0;
    }

    // Mirrors PortGatewayService/Protocol/PipeNaming.cs's normalization rule.
    private static string NormalizeStationId(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var character in value.ToLowerInvariant())
        {
            if ((character >= 'a' && character <= 'z') || (character >= '0' && character <= '9') || character == '-')
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
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
