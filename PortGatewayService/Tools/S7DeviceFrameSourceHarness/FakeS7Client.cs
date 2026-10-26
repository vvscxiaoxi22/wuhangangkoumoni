using System;
using System.Threading;
using System.Threading.Tasks;
using PortTraining.PortGatewayService.S7;

// Test double standing in for a real S7 driver - lets RealS7DeviceFrameSource's
// connect/retry/backoff/reconnect logic be verified for real without any actual PLC
// or S7 library, per the reasoning in IS7Client.cs and the decision record.
internal sealed class FakeS7Client : IS7Client
{
    public bool AlwaysFailConnect;
    public int ConnectSucceedsAfterMs;
    public int StaysConnectedMs = int.MaxValue;
    public int ConnectAttempts;

    private bool connected;
    private DateTime connectedAtUtc;

    public bool IsConnected
    {
        get
        {
            if (!connected) return false;
            if (StaysConnectedMs == int.MaxValue) return true;
            return (DateTime.UtcNow - connectedAtUtc).TotalMilliseconds < StaysConnectedMs;
        }
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref ConnectAttempts);

        if (AlwaysFailConnect)
        {
            throw new InvalidOperationException("simulated-connect-failure");
        }

        if (ConnectSucceedsAfterMs > 0)
        {
            await Task.Delay(ConnectSucceedsAfterMs, cancellationToken).ConfigureAwait(false);
        }

        connected = true;
        connectedAtUtc = DateTime.UtcNow;
    }

    public void Disconnect()
    {
        connected = false;
    }

    public Task<byte[]> ReadBytesAsync(int dbNumber, int startByte, int length, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("point-table register decoding is not implemented yet - not exercised here");
    }

    public Task WriteBytesAsync(int dbNumber, int startByte, byte[] data, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("point-table register decoding is not implemented yet - not exercised here");
    }
}
