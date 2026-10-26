using System;
using System.Threading;
using System.Threading.Tasks;
using PortTraining.PortGatewayService.Devices;

namespace PortTraining.PortGatewayService.S7
{
    // Connection-lifecycle wrapper around IS7Client: retries with backoff while
    // disconnected, reports PlcOnline honestly, and never lets a connection failure
    // propagate out as an exception to callers of GetCurrentFrame() (which must stay
    // synchronous and non-blocking per IDeviceFrameSource - the real I/O runs in
    // RunAsync's background loop, which the caller must start separately).
    //
    // Point-table-driven register decoding (turning raw PLC bytes into the other
    // DeviceFrameSnapshot fields) is NOT implemented yet - GetCurrentFrame() reports
    // safe/unknown defaults for everything except PlcOnline until that exists, which
    // needs a real point table (see PortGatewayService点表配置格式草案-v0.1.md, still
    // all placeholder values pending real PLC confirmation).
    //
    // Not wired into GatewayControlPipeServer/GatewayRealtimePipeServer yet - those
    // still use SimulatedDeviceFrameSource. Swapping this in is a separate, deliberate
    // integration step once a real IS7Client implementation and point table exist.
    public sealed class RealS7DeviceFrameSource : IDeviceFrameSource
    {
        private readonly IS7Client client;
        private readonly S7ConnectionOptions options;
        private readonly Action<string> log;
        private readonly object stateLock = new object();
        private bool isConnected;

        public RealS7DeviceFrameSource(IS7Client client, S7ConnectionOptions options, Action<string> log = null)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.log = log ?? Console.WriteLine;
        }

        public DeviceFrameSnapshot GetCurrentFrame()
        {
            bool online;
            lock (stateLock)
            {
                online = isConnected;
            }

            // Honest defaults while there is no point-table decoding yet: when the PLC
            // is not confirmed online, do not claim any safety condition is satisfied.
            // controlMode has no real "unknown" value in the shared Local|Remote enum
            // yet, so it stays "Local" as the least-surprising placeholder - a known
            // limitation, not an actual reading.
            return new DeviceFrameSnapshot
            {
                PlcOnline = online,
                EmergencyStopReleased = false,
                ControlPowerOn = false,
                HandlesCentered = true,
                ControlMode = "Local",
                IsSimulated = false,
                GantryCommand = 0.0,
                TrolleyCommand = 0.0,
                HoistCommand = 0.0,
                HornPressed = false
            };
        }

        // Must be started explicitly by the caller (not part of IDeviceFrameSource) -
        // runs the connect/retry loop until cancellation. Never throws.
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            log($"[PGS_S7_CLIENT_STARTING] ipAddress={options.IpAddress}, rack={options.Rack}, slot={options.Slot}");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        connectCts.CancelAfter(options.ConnectTimeoutMs);
                        await client.ConnectAsync(connectCts.Token).ConfigureAwait(false);
                    }

                    SetConnected(true);
                    log("[PGS_S7_CLIENT_CONNECTED]");

                    // No point-table read loop yet (see class comment) - just watch the
                    // connection until it drops or we are asked to stop.
                    while (!cancellationToken.IsCancellationRequested && client.IsConnected)
                    {
                        await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                    }

                    SetConnected(false);
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        log("[PGS_S7_CLIENT_DISCONNECTED] reason=connection-lost");
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    SetConnected(false);
                    break;
                }
                catch (Exception ex)
                {
                    SetConnected(false);
                    log($"[PGS_S7_CLIENT_CONNECT_FAILED] detail={ex.Message}");
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    await Task.Delay(options.RetryBackoffMs, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            client.Disconnect();
            SetConnected(false);
            log("[PGS_S7_CLIENT_STOPPED]");
        }

        private void SetConnected(bool connected)
        {
            lock (stateLock)
            {
                isConnected = connected;
            }
        }
    }
}
