using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PortTraining.PortGatewayService.S7;

// Standalone, ad-hoc connectivity probe for on-site PLC integration testing.
// This is NOT part of the automated test suite (no Require/assertions) - it is a
// diagnostic CLI meant to be run by hand while a real (or simulated) PLC is
// reachable, to answer one question fast: "does S7netplus's ConnectAsync succeed
// against this IP/rack/slot, yes or no, and if a DB/start/length is given, can it
// also read bytes from that DB." It deliberately does not touch RealS7DeviceFrameSource
// or any Gateway pipe code - production wiring is a separate, deferred task
// (see PortGatewayService协议扩展决策记录-v1.0.md 2.15/2.17).
//
// Usage:
//   dotnet run --project Tools/S7ConnectivityProbe/S7ConnectivityProbe.csproj -- --ip 192.168.1.50
//   dotnet run --project Tools/S7ConnectivityProbe/S7ConnectivityProbe.csproj -- --ip 192.168.1.50 --rack 0 --slot 1
//   dotnet run --project Tools/S7ConnectivityProbe/S7ConnectivityProbe.csproj -- --ip 192.168.1.50 --db 1 --start 0 --length 4
//
// If --slot is omitted, both 0 and 1 are tried in turn, because the correct value
// for S7-1200/1500 is genuinely disputed between S7netplus's own docs (0) and the
// wider Snap7-family community convention (1) - see today's discussion record.
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch (IOException)
        {
            // Output is redirected to something that doesn't support changing encoding
            // (e.g. piped to a file) - fall back to whatever the default is.
        }

        var options = ParseArgs(args);
        if (options == null)
        {
            PrintUsage();
            return 1;
        }

        var slotsToTry = options.SlotExplicitlySet
            ? new List<int> { options.Slot }
            : new List<int> { 0, 1 };

        Console.WriteLine($"[S7_PROBE_START] ip={options.IpAddress} rack={options.Rack} slots-to-try=[{string.Join(",", slotsToTry)}] timeoutMs={options.ConnectTimeoutMs}");

        var anySucceeded = false;

        foreach (var slot in slotsToTry)
        {
            anySucceeded |= await TryOneAsync(options, slot).ConfigureAwait(false);
        }

        if (!anySucceeded)
        {
            Console.WriteLine();
            Console.WriteLine("[S7_PROBE_ALL_FAILED] 所有尝试都失败了。按顺序排查：");
            Console.WriteLine("  1. 先在这台机器上确认能ping通目标IP（网络层面能不能到）。");
            Console.WriteLine("  2. PowerShell里跑 Test-NetConnection -ComputerName <IP> -Port 102 确认102端口是否可达（防火墙/PLCSIM是否真的对外暴露）。");
            Console.WriteLine("  3. 确认TIA Portal里CPU属性→防护与安全→连接机制→\"允许来自远程对象的PUT/GET通信访问\"已勾选（S7-1200/1500默认关闭，不勾选会直接拒绝连接）。");
            Console.WriteLine("  4. 如果2也失败：标准S7-PLCSIM(非Advanced)通常只在运行TIA Portal的那台机器本地测试用，不一定能被局域网内其它电脑连接到——考虑把这个探测工具拿到运行TIA Portal的那台机器上直接跑（IP填127.0.0.1或PLCSIM虚拟网卡地址）。");
        }

        return anySucceeded ? 0 : 1;
    }

    private static async Task<bool> TryOneAsync(ProbeOptions options, int slot)
    {
        var connectionOptions = new S7ConnectionOptions
        {
            IpAddress = options.IpAddress,
            Rack = options.Rack,
            Slot = slot,
            ConnectTimeoutMs = options.ConnectTimeoutMs
        };

        Console.WriteLine();
        Console.WriteLine($"[S7_PROBE_ATTEMPT] rack={options.Rack} slot={slot}");

        var client = new S7NetPlusClient(connectionOptions);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        using (var cts = new CancellationTokenSource(options.ConnectTimeoutMs))
        {
            try
            {
                await client.ConnectAsync(cts.Token).ConfigureAwait(false);
                stopwatch.Stop();
                Console.WriteLine($"[S7_PROBE_CONNECT_OK] rack={options.Rack} slot={slot} elapsedMs={stopwatch.ElapsedMilliseconds} isConnected={client.IsConnected}");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Console.WriteLine($"[S7_PROBE_CONNECT_FAILED] rack={options.Rack} slot={slot} elapsedMs={stopwatch.ElapsedMilliseconds} error={ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        var readOk = true;
        if (options.TestDbNumber.HasValue)
        {
            using (var readCts = new CancellationTokenSource(options.ConnectTimeoutMs))
            {
                try
                {
                    var bytes = await client.ReadBytesAsync(options.TestDbNumber.Value, options.TestStartByte, options.TestLength, readCts.Token).ConfigureAwait(false);
                    Console.WriteLine($"[S7_PROBE_READ_OK] db={options.TestDbNumber} start={options.TestStartByte} length={options.TestLength} bytes=[{BitConverter.ToString(bytes)}]");
                }
                catch (Exception ex)
                {
                    readOk = false;
                    Console.WriteLine($"[S7_PROBE_READ_FAILED] db={options.TestDbNumber} start={options.TestStartByte} length={options.TestLength} error={ex.GetType().Name}: {ex.Message}");
                    Console.WriteLine("  连接成功但读取失败，最常见原因：目标DB在TIA Portal里勾选了\"优化的块访问\"，需要现场取消勾选后重新编译下载（见操作手册3.6节）。");
                }
            }
        }

        try
        {
            client.Disconnect();
        }
        catch (Exception)
        {
            // Best-effort cleanup only; connect/read results above already printed.
        }

        return readOk;
    }

    private static ProbeOptions ParseArgs(string[] args)
    {
        var options = new ProbeOptions();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--ip":
                    options.IpAddress = args[++i];
                    break;
                case "--rack":
                    options.Rack = int.Parse(args[++i]);
                    break;
                case "--slot":
                    options.Slot = int.Parse(args[++i]);
                    options.SlotExplicitlySet = true;
                    break;
                case "--timeout-ms":
                    options.ConnectTimeoutMs = int.Parse(args[++i]);
                    break;
                case "--db":
                    options.TestDbNumber = int.Parse(args[++i]);
                    break;
                case "--start":
                    options.TestStartByte = int.Parse(args[++i]);
                    break;
                case "--length":
                    options.TestLength = int.Parse(args[++i]);
                    break;
                default:
                    Console.WriteLine($"未知参数: {args[i]}");
                    return null;
            }
        }

        if (string.IsNullOrWhiteSpace(options.IpAddress))
        {
            Console.WriteLine("必须提供 --ip");
            return null;
        }

        return options;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("用法: dotnet run --project Tools/S7ConnectivityProbe/S7ConnectivityProbe.csproj -- --ip <PLC或PLCSIM的IP> [--rack 0] [--slot 0或1，省略则两个都试] [--timeout-ms 3000] [--db <DB号> --start <起始字节> --length <字节数>]");
    }

    private sealed class ProbeOptions
    {
        public string IpAddress { get; set; }
        public int Rack { get; set; }
        public int Slot { get; set; }
        public bool SlotExplicitlySet { get; set; }
        public int ConnectTimeoutMs { get; set; } = 3000;
        public int? TestDbNumber { get; set; }
        public int TestStartByte { get; set; }
        public int TestLength { get; set; } = 1;
    }
}
