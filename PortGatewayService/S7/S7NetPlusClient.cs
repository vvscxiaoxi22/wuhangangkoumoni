using System;
using System.Threading;
using System.Threading.Tasks;
using S7.Net;

namespace PortTraining.PortGatewayService.S7
{
    // IS7Client adapter over the S7netplus library (NuGet "S7netplus" 0.20.0,
    // https://github.com/S7NetPlus/s7netplus), confirmed against the library's own
    // source before writing this (Plc constructor, CpuType.S71500, DataType.DataBlock,
    // OpenAsync/Close/IsConnected/ReadBytesAsync/WriteBytesAsync all verified, not
    // guessed from memory). Targets the S7-1500 family specifically per the CpuType
    // used below - see decision record 2.17 for why this library, and its own GitHub
    // issue tracker ("Support for Optimized block Access for S7-1500?") for why the
    // target PLC's DBs must have "Optimized block access" UNCHECKED in TIA Portal:
    // this library (like the classic Snap7 protocol it is a reimplementation of)
    // cannot read DBs that keep TIA's default optimized/symbolic layout.
    public sealed class S7NetPlusClient : IS7Client
    {
        private readonly Plc plc;

        public S7NetPlusClient(S7ConnectionOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            plc = new Plc(CpuType.S71500, options.IpAddress, (short)options.Rack, (short)options.Slot);
        }

        public bool IsConnected => plc.IsConnected;

        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            return plc.OpenAsync(cancellationToken);
        }

        public void Disconnect()
        {
            plc.Close();
        }

        public async Task<byte[]> ReadBytesAsync(int dbNumber, int startByte, int length, CancellationToken cancellationToken)
        {
            return await plc.ReadBytesAsync(DataType.DataBlock, dbNumber, startByte, length, cancellationToken)
                .ConfigureAwait(false);
        }

        public Task WriteBytesAsync(int dbNumber, int startByte, byte[] data, CancellationToken cancellationToken)
        {
            return plc.WriteBytesAsync(DataType.DataBlock, dbNumber, startByte, data, cancellationToken);
        }
    }
}
