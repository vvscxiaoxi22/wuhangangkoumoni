using System;
using System.Threading;
using System.Threading.Tasks;
using PortTraining.PortGatewayService.Configuration;

namespace PortTraining.PortGatewayService
{
    internal static class Program
    {
        private static async Task Main()
        {
            var options = GatewayStationOptions.LoadFromAppSettings(AppContext.BaseDirectory);
            var sessionState = new GatewaySessionState();
            var controlServer = new GatewayControlPipeServer(options, sessionState);
            var realtimeServer = new GatewayRealtimePipeServer(options, sessionState);

            using (var cts = new CancellationTokenSource())
            {
                Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    cts.Cancel();
                };

                await Task.WhenAll(
                    controlServer.RunAsync(cts.Token),
                    realtimeServer.RunAsync(cts.Token)).ConfigureAwait(false);
            }
        }
    }
}
