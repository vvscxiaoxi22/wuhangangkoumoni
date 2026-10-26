using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using PortTraining.YardCraneSimulator.SharedNet;

// Real loopback socket end-to-end checks for the hand-rolled WebSocket transport and wire
// protocol - production source (YardCraneMiniWebSocket.cs, YardCraneS7LinkProtocol.cs),
// unmodified, on real TCP sockets. Deliberately does NOT instantiate YardCraneS7InputSource
// (see README: since it gained a YardCraneDevice field, constructing it requires loading
// Unity.InputSystem, which fails at CLR type-load time outside a real Unity process in a way a
// try/catch cannot contain - confirmed 2026-09-16). This harness verifies the transport and
// protocol only; YardCraneS7InputSource's own behavior (consume-once buttons, fail-safe
// emergency stop, staleness) must be verified in real Unity Play from here on.
internal static class S7LinkHarnessProgram
{
    private static int failures;

    private static int FindFreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static void Check(string name, bool condition, string detail)
    {
        if (condition)
        {
            Console.WriteLine("[PASS] " + name + ": " + detail);
        }
        else
        {
            failures++;
            Console.WriteLine("[FAIL] " + name + ": " + detail);
        }
    }

    private static bool SpinWaitUntil(Func<bool> condition, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(10);
        }

        return condition();
    }

    private static void HandshakeAndFrameDelivery()
    {
        var port = FindFreeLoopbackPort();
        using var server = new YardCraneMiniWebSocketServer();
        server.Start(port);

        using var client = new YardCraneMiniWebSocketClient();
        string received = null;
        client.TextReceived += text => received = text;

        var connected = client.TryConnect("127.0.0.1", port, 2000, out var error);
        Check("connect-succeeds-against-listening-server", connected, "error=" + (error ?? "(none)"));

        var accepted = SpinWaitUntil(() => server.HasClient, 2000);
        Check("server-registers-accepted-client", accepted, "hasClient=" + server.HasClient);

        var sentFrame = new YardCraneS7LinkFrame(
            42, 0.4231f, -0.1000f, true,
            (1 << (int)YardCraneS7MomentarySignal.ContainerLock) | (1 << (int)YardCraneS7MomentarySignal.FaultReset),
            false);
        var sent = server.TryBroadcastText(YardCraneS7LinkProtocol.Encode(sentFrame));
        Check("server-broadcast-reports-delivered", sent, "sent=" + sent);

        var gotFrame = SpinWaitUntil(() => received != null, 2000);
        Check("client-receives-frame-text", gotFrame, "received=" + (received ?? "(none)"));

        var frame = default(YardCraneS7LinkFrame);
        var decoded = gotFrame && YardCraneS7LinkProtocol.TryDecode(received, out frame);
        Check(
            "frame-round-trips-all-fields-correctly",
            decoded &&
            frame.Sequence == 42 &&
            Math.Abs(frame.GantryNormalized - 0.4231f) < 0.0005f &&
            Math.Abs(frame.TrolleyNormalized - (-0.1000f)) < 0.0005f &&
            frame.Valid &&
            frame.HasMomentarySignal(YardCraneS7MomentarySignal.ContainerLock) &&
            frame.HasMomentarySignal(YardCraneS7MomentarySignal.FaultReset) &&
            !frame.HasMomentarySignal(YardCraneS7MomentarySignal.Pause) &&
            !frame.EmergencyStopEngaged,
            decoded
                ? $"sequence={frame.Sequence}, gantry={frame.GantryNormalized}, trolley={frame.TrolleyNormalized}, " +
                  $"valid={frame.Valid}, eStop={frame.EmergencyStopEngaged}"
                : "decode failed");
    }

    private static void InvalidAndEmergencyStopFramesDecodeCorrectly()
    {
        var port = FindFreeLoopbackPort();
        using var server = new YardCraneMiniWebSocketServer();
        server.Start(port);

        using var client = new YardCraneMiniWebSocketClient();
        string received = null;
        client.TextReceived += text => received = text;
        client.TryConnect("127.0.0.1", port, 2000, out _);
        SpinWaitUntil(() => server.HasClient, 2000);

        // valid=false (PLC-side comm timeout, already applied by the Bridge) and
        // emergencyStopEngaged=true (maintained level, engaged) must both round-trip exactly -
        // these are the two fields the fail-safe design most depends on carrying correctly.
        var frame = new YardCraneS7LinkFrame(7, 0.9f, 0.9f, false, 0, true);
        server.TryBroadcastText(YardCraneS7LinkProtocol.Encode(frame));
        var gotFrame = SpinWaitUntil(() => received != null, 2000);
        var decodedFrame = default(YardCraneS7LinkFrame);
        var decoded = gotFrame && YardCraneS7LinkProtocol.TryDecode(received, out decodedFrame);

        Check(
            "invalid-and-engaged-estop-frame-decodes-correctly",
            decoded && !decodedFrame.Valid && decodedFrame.EmergencyStopEngaged,
            decoded ? $"valid={decodedFrame.Valid}, eStop={decodedFrame.EmergencyStopEngaged}" : "decode failed");
    }

    private static void SafeZeroFrameHasExpectedShape()
    {
        var frame = YardCraneS7LinkFrame.SafeZero(99);
        Check(
            "safe-zero-fails-toward-emergency-stop-engaged",
            frame.Sequence == 99 && frame.GantryNormalized == 0f && frame.TrolleyNormalized == 0f &&
            !frame.Valid && frame.MomentaryPressedMask == 0 && frame.EmergencyStopEngaged,
            $"valid={frame.Valid}, eStop={frame.EmergencyStopEngaged}, mask={frame.MomentaryPressedMask}");
    }

    private static void ServerGoneIsDetectedByClient()
    {
        var port = FindFreeLoopbackPort();
        var server = new YardCraneMiniWebSocketServer();
        server.Start(port);

        using var client = new YardCraneMiniWebSocketClient();
        var disconnectedEventFired = false;
        client.Disconnected += () => disconnectedEventFired = true;
        client.TryConnect("127.0.0.1", port, 2000, out _);
        SpinWaitUntil(() => server.HasClient, 2000);

        server.Dispose(); // simulates the independent S7 player process disappearing

        // Wait on the event flag itself, not a proxy condition: the receive thread sets
        // connected=false and then invokes Disconnected in that order (see
        // YardCraneMiniWebSocketClient.ReceiveLoop's finally block), so polling IsConnected
        // alone can observe "disconnected" a moment before the event handler has actually run -
        // a real race in this test, not in the production code it's checking.
        var eventFired = SpinWaitUntil(() => disconnectedEventFired, 2000);
        Check(
            "server-teardown-is-detected",
            eventFired && !client.IsConnected,
            $"disconnectedEventFired={disconnectedEventFired}, clientIsConnected={client.IsConnected}");
    }

    private static void ConnectRefusedWhenNobodyListening()
    {
        var port = FindFreeLoopbackPort(); // reserved-then-released, guaranteed nobody bound to it
        using var client = new YardCraneMiniWebSocketClient();
        var started = DateTime.UtcNow;
        var connected = client.TryConnect("127.0.0.1", port, 1500, out var error);
        var elapsed = DateTime.UtcNow - started;
        Check(
            "connect-refused-fails-cleanly-and-promptly",
            !connected && !string.IsNullOrEmpty(error) && elapsed.TotalMilliseconds < 3000,
            $"connected={connected}, error={error}, elapsedMs={elapsed.TotalMilliseconds:F0}");
    }

    private static void Main()
    {
        HandshakeAndFrameDelivery();
        InvalidAndEmergencyStopFramesDecodeCorrectly();
        SafeZeroFrameHasExpectedShape();
        ServerGoneIsDetectedByClient();
        ConnectRefusedWhenNobodyListening();

        Console.WriteLine(
            "[YC_S7_LINK_HARNESS] failures=" + failures + ", environment=dotnet-real-loopback-sockets, unityPlay=false, " +
            "scope=transport+protocol-only (YardCraneS7InputSource excluded, see README)");
        Environment.Exit(failures == 0 ? 0 : 1);
    }
}
