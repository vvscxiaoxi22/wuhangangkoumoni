using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PortTraining.PortGatewayService.S7;

// Verifies RealS7DeviceFrameSource's connect/retry/backoff/reconnect lifecycle against
// FakeS7Client - the parts that can genuinely be tested without a real PLC or S7
// library. Point-table register decoding is NOT implemented yet and is NOT tested here.
internal static class Program
{
    private static int assertions;

    private static async Task Main()
    {
        RunLoadFromAppSettingsParsesTheS7Block();
        RunLoadFromAppSettingsFallsBackToDefaultsWhenMissing();
        await RunAlwaysFailsRetriesRepeatedlyAsync().ConfigureAwait(false);
        await RunConnectsThenDropsAndReconnectsAsync().ConfigureAwait(false);
        await RunCancellationDuringLongBackoffStopsPromptlyAsync().ConfigureAwait(false);
        await RunRealS7NetPlusClientAgainstUnreachableTargetAsync().ConfigureAwait(false);

        Console.WriteLine($"[S7_DEVICE_FRAME_SOURCE_HARNESS_PASS] assertions={assertions}");
    }

    private static void RunLoadFromAppSettingsParsesTheS7Block()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "pgs-s7-options-test-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), """
                {
                  "stationId": "DEV-01",
                  "s7": {
                    "ipAddress": "192.168.0.50",
                    "rack": 2,
                    "slot": 3,
                    "connectTimeoutMs": 1234,
                    "retryBackoffMs": 5678
                  }
                }
                """);

            var options = S7ConnectionOptions.LoadFromAppSettings(tempDir);
            Require(options.IpAddress == "192.168.0.50", "loads-ipAddress-from-appsettings");
            Require(options.Rack == 2, "loads-rack-from-appsettings");
            Require(options.Slot == 3, "loads-slot-from-appsettings");
            Require(options.ConnectTimeoutMs == 1234, "loads-connectTimeoutMs-from-appsettings");
            Require(options.RetryBackoffMs == 5678, "loads-retryBackoffMs-from-appsettings");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static void RunLoadFromAppSettingsFallsBackToDefaultsWhenMissing()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "pgs-s7-options-test-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            // No appsettings.json at all in this directory.
            var options = S7ConnectionOptions.LoadFromAppSettings(tempDir);
            Require(options.IpAddress == "0.0.0.0", "missing-appsettings-falls-back-to-default-ipAddress");
            Require(options.Slot == 0, "missing-appsettings-falls-back-to-default-slot");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static async Task RunAlwaysFailsRetriesRepeatedlyAsync()
    {
        var fakeClient = new FakeS7Client { AlwaysFailConnect = true };
        var options = new S7ConnectionOptions { IpAddress = "192.0.2.1", ConnectTimeoutMs = 200, RetryBackoffMs = 100 };
        var source = new RealS7DeviceFrameSource(fakeClient, options, message => { });

        using (var cts = new CancellationTokenSource())
        {
            var runTask = source.RunAsync(cts.Token);

            Require(source.GetCurrentFrame().PlcOnline == false, "before-any-connect-attempt-plc-is-offline");

            // ~200ms timeout (unused here since the fake throws immediately) + 100ms
            // backoff per cycle; 900ms is comfortably several retry cycles.
            await Task.Delay(900).ConfigureAwait(false);

            Require(source.GetCurrentFrame().PlcOnline == false, "unreachable-plc-never-reports-online");
            Require(fakeClient.ConnectAttempts >= 2, $"unreachable-plc-retries-more-than-once (was {fakeClient.ConnectAttempts})");

            cts.Cancel();
            var stopped = await WaitForCompletionAsync(runTask, 3000).ConfigureAwait(false);
            Require(stopped, "always-fails-client-run-loop-stops-after-cancellation");
        }
    }

    private static async Task RunConnectsThenDropsAndReconnectsAsync()
    {
        var fakeClient = new FakeS7Client { ConnectSucceedsAfterMs = 0, StaysConnectedMs = 300 };
        var options = new S7ConnectionOptions { IpAddress = "192.0.2.2", ConnectTimeoutMs = 500, RetryBackoffMs = 100 };
        var source = new RealS7DeviceFrameSource(fakeClient, options, message => { });

        using (var cts = new CancellationTokenSource())
        {
            var runTask = source.RunAsync(cts.Token);

            await Task.Delay(150).ConfigureAwait(false);
            Require(source.GetCurrentFrame().PlcOnline, "successful-connect-reports-online");

            // The drop-detection loop polls every 1000ms, so the ~300ms drop is only
            // noticed on its next poll (~t=1000ms), then a ~100ms backoff before
            // reconnecting - 1600ms leaves generous margin for the reconnect to land.
            await Task.Delay(1600).ConfigureAwait(false);

            Require(fakeClient.ConnectAttempts >= 2, $"drop-triggers-a-reconnect-attempt (was {fakeClient.ConnectAttempts})");
            Require(source.GetCurrentFrame().PlcOnline, "reconnected-after-drop-reports-online-again");

            cts.Cancel();
            var stopped = await WaitForCompletionAsync(runTask, 3000).ConfigureAwait(false);
            Require(stopped, "connects-then-drops-client-run-loop-stops-after-cancellation");
            Require(source.GetCurrentFrame().PlcOnline == false, "after-stop-plc-reports-offline");
        }
    }

    private static async Task RunCancellationDuringLongBackoffStopsPromptlyAsync()
    {
        var fakeClient = new FakeS7Client { AlwaysFailConnect = true };
        // A deliberately long backoff (60s) - if cancellation only takes effect after
        // the backoff finishes naturally, this test will time out and fail, proving
        // the wait is genuinely cancellable rather than an uncancellable sleep.
        var options = new S7ConnectionOptions { IpAddress = "192.0.2.3", ConnectTimeoutMs = 200, RetryBackoffMs = 60000 };
        var source = new RealS7DeviceFrameSource(fakeClient, options, message => { });

        using (var cts = new CancellationTokenSource())
        {
            var runTask = source.RunAsync(cts.Token);
            await Task.Delay(300).ConfigureAwait(false); // let it enter the long retry-backoff wait
            cts.Cancel();

            var stoppedPromptly = await WaitForCompletionAsync(runTask, 5000).ConfigureAwait(false);
            Require(stoppedPromptly, "cancellation-during-a-60-second-backoff-stops-within-5-seconds-not-60");
        }
    }

    // The one test in this harness that exercises the REAL S7netplus library (not
    // FakeS7Client) - no real PLC exists to connect to, but pointing it at 127.0.0.1
    // on the default S7 port (102, which nothing in this environment listens on)
    // proves the real library's connection failure surfaces correctly through
    // RealS7DeviceFrameSource's retry loop instead of hanging or crashing the process.
    private static async Task RunRealS7NetPlusClientAgainstUnreachableTargetAsync()
    {
        var options = new S7ConnectionOptions
        {
            IpAddress = "127.0.0.1",
            Rack = 0,
            Slot = 0,
            ConnectTimeoutMs = 1000,
            RetryBackoffMs = 500
        };
        var realClient = new S7NetPlusClient(options);
        var source = new RealS7DeviceFrameSource(realClient, options, message => { });

        using (var cts = new CancellationTokenSource())
        {
            var runTask = source.RunAsync(cts.Token);

            Require(source.GetCurrentFrame().PlcOnline == false, "real-s7netplus-client-starts-offline");

            await Task.Delay(3000).ConfigureAwait(false);

            Require(
                source.GetCurrentFrame().PlcOnline == false,
                "real-s7netplus-client-against-unreachable-target-stays-offline-without-crashing");

            cts.Cancel();
            var stopped = await WaitForCompletionAsync(runTask, 5000).ConfigureAwait(false);
            Require(stopped, "real-s7netplus-client-run-loop-stops-after-cancellation");
        }
    }

    private static async Task<bool> WaitForCompletionAsync(Task task, int timeoutMs)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeoutMs)).ConfigureAwait(false);
        return completed == task;
    }

    private static void Require(bool condition, string name)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(name);
        Console.WriteLine("PASS " + name);
    }
}
