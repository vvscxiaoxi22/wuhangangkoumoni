using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PortTraining.YardCraneSimulator.Gateway
{
    // Mirrors PortGatewayService/Protocol/PipeFraming.cs's wire format (V1.1 section
    // 3.2: 4-byte little-endian length + UTF-8 JSON). Implemented independently here
    // because this is a different project/runtime (Unity) with no shared contracts
    // package yet (PortTraining.Contracts is still 未实现).
    public static class GatewayPipeFraming
    {
        public static async Task WriteFrameAsync(Stream stream, string json, CancellationToken cancellationToken)
        {
            var payload = Encoding.UTF8.GetBytes(json);
            var header = new byte[4];
            header[0] = (byte)(payload.Length & 0xFF);
            header[1] = (byte)((payload.Length >> 8) & 0xFF);
            header[2] = (byte)((payload.Length >> 16) & 0xFF);
            header[3] = (byte)((payload.Length >> 24) & 0xFF);
            await stream.WriteAsync(header, 0, header.Length, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        // Returns null only for a clean disconnect before any byte of a new frame arrived.
        public static async Task<string> ReadFrameAsync(Stream stream, int maxFrameBytes, CancellationToken cancellationToken)
        {
            var header = new byte[4];
            var gotHeader = await ReadExactAsync(stream, header, cancellationToken).ConfigureAwait(false);
            if (!gotHeader) return null;

            uint length = (uint)header[0] | ((uint)header[1] << 8) | ((uint)header[2] << 16) | ((uint)header[3] << 24);
            if (length == 0 || length > (uint)maxFrameBytes)
            {
                throw new InvalidOperationException($"invalid-frame-length:{length}");
            }

            var payload = new byte[length];
            var gotPayload = await ReadExactAsync(stream, payload, cancellationToken).ConfigureAwait(false);
            if (!gotPayload) throw new InvalidOperationException("connection-closed-mid-frame");

            return Encoding.UTF8.GetString(payload);
        }

        private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
        {
            var totalRead = 0;
            while (totalRead < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer, totalRead, buffer.Length - totalRead, cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    if (totalRead == 0) return false;
                    throw new InvalidOperationException("connection-closed-mid-frame");
                }

                totalRead += read;
            }

            return true;
        }
    }
}
