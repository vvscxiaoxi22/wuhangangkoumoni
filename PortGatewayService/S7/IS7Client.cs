using System.Threading;
using System.Threading.Tasks;

namespace PortTraining.PortGatewayService.S7
{
    // Minimal seam between PortGatewayService's connection-lifecycle/retry logic and
    // whatever real S7 driver library gets chosen. V1.1 section 3.3: "真实S7库必须在
    // 目标PLC型号、固件和TIA配置上完成PoC后锁定" - that PoC has not happened yet (see
    // PortGatewayService/README.md), so this abstraction exists specifically to let the
    // connection lifecycle, retry/backoff and honest online/offline reporting be built
    // and tested today without committing to a specific library. Once a real PoC picks
    // one, only a thin adapter implementing this interface needs to be written -
    // everything in RealS7DeviceFrameSource stays the same.
    public interface IS7Client
    {
        bool IsConnected { get; }

        Task ConnectAsync(CancellationToken cancellationToken);

        void Disconnect();

        Task<byte[]> ReadBytesAsync(int dbNumber, int startByte, int length, CancellationToken cancellationToken);

        Task WriteBytesAsync(int dbNumber, int startByte, byte[] data, CancellationToken cancellationToken);
    }
}
