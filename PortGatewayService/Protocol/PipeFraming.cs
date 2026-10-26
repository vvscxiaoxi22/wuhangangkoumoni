using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PortTraining.PortGatewayService.Protocol
{
    // Raised for any condition V1.1 section 3.2 requires to close the connection and
    // log a protocol error: zero-length frame, oversized frame, or a frame that ends
    // mid-payload.
    public sealed class PipeProtocolViolationException : Exception
    {
        public PipeProtocolViolationException(string message) : base(message)
        {
        }
    }

    // Frame codec from V1.1 section 3.2: "4字节无符号小端长度 N" + "N字节 UTF-8 JSON".
    public static class PipeFraming
    {
        public static async Task WriteFrameAsync(Stream stream, byte[] payload, CancellationToken cancellationToken)
        {
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
        // Any other short read is a mid-frame disconnect and is reported as a violation.
        public static async Task<byte[]> ReadFrameAsync(Stream stream, int maxFrameBytes, CancellationToken cancellationToken)
        {
            var header = new byte[4];
            var gotHeader = await ReadExactAsync(stream, header, 0, header.Length, cancellationToken).ConfigureAwait(false);
            if (!gotHeader)
            {
                return null;
            }

            uint length = (uint)header[0] | ((uint)header[1] << 8) | ((uint)header[2] << 16) | ((uint)header[3] << 24);
            if (length == 0 || length > (uint)maxFrameBytes)
            {
                throw new PipeProtocolViolationException($"invalid-frame-length:{length}");
            }

            var payload = new byte[length];
            var gotPayload = await ReadExactAsync(stream, payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
            if (!gotPayload)
            {
                throw new PipeProtocolViolationException("connection-closed-mid-frame");
            }

            return payload;
        }

        private static async Task<bool> ReadExactAsync(
            Stream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var totalRead = 0;
            while (totalRead < count)
            {
                var read = await stream.ReadAsync(buffer, offset + totalRead, count - totalRead, cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    if (totalRead == 0) return false;
                    throw new PipeProtocolViolationException("connection-closed-mid-frame");
                }

                totalRead += read;
            }

            return true;
        }
    }
}
