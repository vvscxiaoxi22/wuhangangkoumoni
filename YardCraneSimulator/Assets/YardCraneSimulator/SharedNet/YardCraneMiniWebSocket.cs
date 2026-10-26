using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace PortTraining.YardCraneSimulator.SharedNet
{
    /// <summary>
    /// A deliberately minimal RFC 6455 WebSocket implementation for one controlled use case:
    /// a single local process (the YardCraneS7Integration player) pushing small text telemetry
    /// frames over loopback to a single local client (the main training app). It is NOT a
    /// general-purpose WebSocket library - no extensions, no fragmentation, no multi-client
    /// fan-out, no binary frames. This exists only because bringing in a third-party WebSocket
    /// package was explicitly ruled out (no network access to fetch/vet one, and no unreviewed
    /// binary should land in a PLC-adjacent codebase); everything here is plain BCL sockets so
    /// it can be read and unit-tested end to end.
    /// </summary>
    internal static class YardCraneMiniWebSocketFrame
    {
        private const byte FinAndText = 0x81; // FIN=1, RSV=0, opcode=0x1 (text)
        private const byte FinAndClose = 0x88; // FIN=1, opcode=0x8 (close)
        private const byte OpcodeMask = 0x0F;
        private const byte Text = 0x1;
        private const byte Close = 0x8;
        private const byte Ping = 0x9;
        private const byte Pong = 0xA;

        /// <summary>Server -> client text frames are sent unmasked, per RFC 6455 §5.1.</summary>
        public static byte[] EncodeTextUnmasked(string text)
        {
            var payload = Encoding.UTF8.GetBytes(text);
            return EncodeHeaderAndPayload(FinAndText, payload, mask: false);
        }

        public static byte[] EncodeCloseUnmasked()
        {
            return EncodeHeaderAndPayload(FinAndClose, Array.Empty<byte>(), mask: false);
        }

        /// <summary>Every frame a client sends must be masked per RFC 6455 §5.1, close frames
        /// included - this server never reads it back (see the "one-way telemetry" note on
        /// YardCraneMiniWebSocketServer), but the client's own bytes on the wire should still be
        /// spec-correct rather than relying on nobody ever checking.</summary>
        public static byte[] EncodeCloseMasked()
        {
            return EncodeHeaderAndPayload(FinAndClose, Array.Empty<byte>(), mask: true);
        }

        private static byte[] EncodeHeaderAndPayload(byte firstByte, byte[] payload, bool mask)
        {
            var maskKey = mask ? RandomMaskKey() : null;
            var header = new List<byte> { firstByte };
            var maskBit = (byte)(mask ? 0x80 : 0x00);

            if (payload.Length <= 125)
            {
                header.Add((byte)(maskBit | payload.Length));
            }
            else if (payload.Length <= 65535)
            {
                header.Add((byte)(maskBit | 126));
                header.Add((byte)(payload.Length >> 8));
                header.Add((byte)(payload.Length & 0xFF));
            }
            else
            {
                header.Add((byte)(maskBit | 127));
                for (var shift = 56; shift >= 0; shift -= 8)
                {
                    header.Add((byte)((long)payload.Length >> shift & 0xFF));
                }
            }

            if (mask)
            {
                header.AddRange(maskKey);
            }

            var body = new byte[payload.Length];
            for (var i = 0; i < payload.Length; i++)
            {
                body[i] = mask ? (byte)(payload[i] ^ maskKey[i % 4]) : payload[i];
            }

            var result = new byte[header.Count + body.Length];
            header.CopyTo(result);
            Buffer.BlockCopy(body, 0, result, header.Count, body.Length);
            return result;
        }

        private static byte[] RandomMaskKey()
        {
            var key = new byte[4];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(key);
            return key;
        }

        /// <summary>
        /// Blocks reading one data (text) frame off the stream, transparently consuming and
        /// discarding Ping frames (there is no reciprocal Pong sent back - loopback-only,
        /// short-lived link, not worth the extra state machine for this first pass). Returns
        /// null when the peer sent a Close frame or the stream ended.
        /// </summary>
        public static string ReadNextTextFrame(System.IO.Stream stream)
        {
            while (true)
            {
                var first = ReadByteOrThrow(stream);
                var second = ReadByteOrThrow(stream);
                var opcode = (byte)(first & OpcodeMask);
                var masked = (second & 0x80) != 0;
                long length = second & 0x7F;

                if (length == 126)
                {
                    var lengthBytes = ReadExactly(stream, 2);
                    length = (lengthBytes[0] << 8) | lengthBytes[1];
                }
                else if (length == 127)
                {
                    var lengthBytes = ReadExactly(stream, 8);
                    length = 0;
                    foreach (var b in lengthBytes)
                    {
                        length = (length << 8) | b;
                    }
                }

                byte[] maskKey = null;
                if (masked)
                {
                    maskKey = ReadExactly(stream, 4);
                }

                var payload = length > 0 ? ReadExactly(stream, (int)length) : Array.Empty<byte>();
                if (masked)
                {
                    for (var i = 0; i < payload.Length; i++)
                    {
                        payload[i] ^= maskKey[i % 4];
                    }
                }

                switch (opcode)
                {
                    case Close:
                        return null;
                    case Ping:
                    case Pong:
                        continue; // not a data frame - loop for the next one
                    case Text:
                        return Encoding.UTF8.GetString(payload);
                    default:
                        // Binary/continuation frames are out of scope for this link; skip them
                        // rather than misinterpreting their bytes as text.
                        continue;
                }
            }
        }

        private static byte ReadByteOrThrow(System.IO.Stream stream)
        {
            var value = stream.ReadByte();
            if (value < 0)
            {
                throw new System.IO.EndOfStreamException("WebSocket stream closed while reading a frame header.");
            }

            return (byte)value;
        }

        private static byte[] ReadExactly(System.IO.Stream stream, int count)
        {
            var buffer = new byte[count];
            var offset = 0;
            while (offset < count)
            {
                var read = stream.Read(buffer, offset, count - offset);
                if (read <= 0)
                {
                    throw new System.IO.EndOfStreamException("WebSocket stream closed mid-frame.");
                }

                offset += read;
            }

            return buffer;
        }
    }

    internal static class YardCraneMiniWebSocketHandshake
    {
        // The fixed GUID RFC 6455 §1.3 defines for computing Sec-WebSocket-Accept.
        private const string HandshakeGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        public static string ComputeAcceptKey(string clientKey)
        {
            using var sha1 = SHA1.Create();
            var hash = sha1.ComputeHash(Encoding.ASCII.GetBytes(clientKey + HandshakeGuid));
            return Convert.ToBase64String(hash);
        }

        public static string GenerateClientKey()
        {
            var bytes = new byte[16];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// Reads raw HTTP request/response headers (byte-by-byte, deliberately unbuffered) up to
        /// the terminating blank line. Must not use a buffering reader here: any bytes it reads
        /// ahead of the blank line would be WebSocket frame bytes belonging to the data stream
        /// that follows on the same socket, and they would be lost once we hand the raw
        /// NetworkStream off to the frame reader above.
        /// </summary>
        public static Dictionary<string, string> ReadHeaders(System.IO.Stream stream, out string requestOrStatusLine)
        {
            var lines = new List<string>();
            var current = new StringBuilder();
            var previousWasCr = false;
            while (true)
            {
                var value = stream.ReadByte();
                if (value < 0)
                {
                    throw new System.IO.EndOfStreamException("Connection closed during WebSocket handshake.");
                }

                var ch = (char)value;
                if (ch == '\n' && previousWasCr)
                {
                    var line = current.ToString();
                    if (line.Length == 0)
                    {
                        break;
                    }

                    lines.Add(line);
                    current.Clear();
                    previousWasCr = false;
                    continue;
                }

                if (ch == '\r')
                {
                    previousWasCr = true;
                    continue;
                }

                if (previousWasCr)
                {
                    current.Append('\r');
                    previousWasCr = false;
                }

                current.Append(ch);
            }

            requestOrStatusLine = lines.Count > 0 ? lines[0] : string.Empty;
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 1; i < lines.Count; i++)
            {
                var separator = lines[i].IndexOf(':');
                if (separator <= 0)
                {
                    continue;
                }

                var name = lines[i].Substring(0, separator).Trim();
                var val = lines[i].Substring(separator + 1).Trim();
                headers[name] = val;
            }

            return headers;
        }

        public static void WriteServerAcceptResponse(System.IO.Stream stream, string acceptKey)
        {
            var response = string.Format(
                CultureInfo.InvariantCulture,
                "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {0}\r\n\r\n",
                acceptKey);
            var bytes = Encoding.ASCII.GetBytes(response);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
        }

        public static void WriteClientUpgradeRequest(System.IO.Stream stream, string host, int port, string path, string clientKey)
        {
            var request = string.Format(
                CultureInfo.InvariantCulture,
                "GET {0} HTTP/1.1\r\nHost: {1}:{2}\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: {3}\r\nSec-WebSocket-Version: 13\r\n\r\n",
                path,
                host,
                port,
                clientKey);
            var bytes = Encoding.ASCII.GetBytes(request);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
        }
    }

    /// <summary>
    /// Loopback-only WebSocket server: accepts one client at a time (a new connection replaces
    /// whatever was there before - "last client wins", which is all this link ever needs since
    /// there is only ever one training-app process talking to it) and pushes text frames to it.
    /// Binds to 127.0.0.1 specifically, never 0.0.0.0 - this is local IPC between two processes
    /// on the same machine, not a service meant to be reachable from the network.
    /// </summary>
    public sealed class YardCraneMiniWebSocketServer : IDisposable
    {
        private readonly object clientLock = new();
        private TcpListener listener;
        private TcpClient currentClient;
        private NetworkStream currentStream;
        private Thread acceptThread;
        private volatile bool running;

        public bool HasClient
        {
            get
            {
                lock (clientLock)
                {
                    return currentStream != null;
                }
            }
        }

        public void Start(int port)
        {
            if (running)
            {
                throw new InvalidOperationException("Server already started.");
            }

            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            running = true;
            acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "YC-S7Link-Server" };
            acceptThread.Start();
        }

        private void AcceptLoop()
        {
            while (running)
            {
                TcpClient client;
                try
                {
                    client = listener.AcceptTcpClient();
                }
                catch (Exception)
                {
                    break; // listener stopped/disposed
                }

                AcceptClient(client);
            }
        }

        private void AcceptClient(TcpClient client)
        {
            TcpClient previousClient;
            lock (clientLock)
            {
                previousClient = currentClient;
                currentClient = null;
                currentStream = null;
            }

            SafeClose(previousClient);

            try
            {
                client.NoDelay = true;
                var stream = client.GetStream();
                var headers = YardCraneMiniWebSocketHandshake.ReadHeaders(stream, out _);
                if (!headers.TryGetValue("Sec-WebSocket-Key", out var clientKey) || string.IsNullOrEmpty(clientKey))
                {
                    client.Close();
                    return;
                }

                var acceptKey = YardCraneMiniWebSocketHandshake.ComputeAcceptKey(clientKey);
                YardCraneMiniWebSocketHandshake.WriteServerAcceptResponse(stream, acceptKey);

                lock (clientLock)
                {
                    currentClient = client;
                    currentStream = stream;
                }
            }
            catch (Exception)
            {
                client.Close();
            }
        }

        /// <summary>Best-effort push; returns false (and drops the stale connection) if nobody
        /// is connected or the write failed - callers must treat "not delivered" as routine,
        /// not exceptional, since a client can disconnect at any time.</summary>
        public bool TryBroadcastText(string text)
        {
            NetworkStream stream;
            lock (clientLock)
            {
                stream = currentStream;
            }

            if (stream == null)
            {
                return false;
            }

            try
            {
                var frame = YardCraneMiniWebSocketFrame.EncodeTextUnmasked(text);
                stream.Write(frame, 0, frame.Length);
                return true;
            }
            catch (Exception)
            {
                lock (clientLock)
                {
                    if (currentStream == stream)
                    {
                        currentClient = null;
                        currentStream = null;
                    }
                }

                return false;
            }
        }

        public void Dispose()
        {
            running = false;
            try
            {
                listener?.Stop();
            }
            catch (Exception)
            {
                // Best-effort teardown.
            }

            TcpClient client;
            lock (clientLock)
            {
                client = currentClient;
                currentClient = null;
                currentStream = null;
            }

            SafeClose(client);
            acceptThread = null;
        }

        private static void SafeClose(TcpClient client)
        {
            if (client == null)
            {
                return;
            }

            try
            {
                client.Close();
            }
            catch (Exception)
            {
                // Best-effort teardown.
            }
        }
    }

    /// <summary>
    /// Loopback-only WebSocket client. Connect is synchronous with a bounded timeout by design:
    /// the caller (YardCraneS7InputSource, wired into training-session setup) needs a definite
    /// yes/no before deciding whether to let a training run start at all.
    /// </summary>
    public sealed class YardCraneMiniWebSocketClient : IDisposable
    {
        private TcpClient client;
        private NetworkStream stream;
        private Thread receiveThread;
        private volatile bool connected;

        public event Action<string> TextReceived;
        public event Action Disconnected;

        public bool IsConnected => connected;

        public bool TryConnect(string host, int port, int timeoutMs, out string error)
        {
            try
            {
                client = new TcpClient();
                var connectTask = client.ConnectAsync(host, port);
                if (!connectTask.Wait(timeoutMs))
                {
                    error = $"Timed out connecting to {host}:{port} within {timeoutMs}ms.";
                    CleanupAfterFailure();
                    return false;
                }

                stream = client.GetStream();
                stream.ReadTimeout = Math.Max(timeoutMs, 1000);
                var clientKey = YardCraneMiniWebSocketHandshake.GenerateClientKey();
                YardCraneMiniWebSocketHandshake.WriteClientUpgradeRequest(
                    stream, host, port, YardCraneS7LinkProtocol.HandshakePath, clientKey);

                var headers = YardCraneMiniWebSocketHandshake.ReadHeaders(stream, out var statusLine);
                if (statusLine.IndexOf("101", StringComparison.Ordinal) < 0)
                {
                    error = $"Handshake rejected: {statusLine}";
                    CleanupAfterFailure();
                    return false;
                }

                if (!headers.TryGetValue("Sec-WebSocket-Accept", out var accept) ||
                    !string.Equals(accept, YardCraneMiniWebSocketHandshake.ComputeAcceptKey(clientKey), StringComparison.Ordinal))
                {
                    error = "Handshake accept key mismatch.";
                    CleanupAfterFailure();
                    return false;
                }

                stream.ReadTimeout = Timeout.Infinite;
                connected = true;
                receiveThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "YC-S7Link-Client" };
                receiveThread.Start();
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                CleanupAfterFailure();
                return false;
            }
        }

        private void ReceiveLoop()
        {
            try
            {
                while (connected)
                {
                    var text = YardCraneMiniWebSocketFrame.ReadNextTextFrame(stream);
                    if (text == null)
                    {
                        break;
                    }

                    // Invoked on this background receive thread - subscribers must not touch
                    // Unity API directly here and must guard shared state appropriately.
                    TextReceived?.Invoke(text);
                }
            }
            catch (Exception)
            {
                // Connection lost - fall through to the disconnect notification below.
            }
            finally
            {
                connected = false;
                Disconnected?.Invoke();
            }
        }

        private void CleanupAfterFailure()
        {
            try
            {
                stream?.Dispose();
            }
            catch (Exception)
            {
                // Best-effort teardown.
            }

            try
            {
                client?.Close();
            }
            catch (Exception)
            {
                // Best-effort teardown.
            }

            stream = null;
            client = null;
            connected = false;
        }

        public void Dispose()
        {
            connected = false;
            try
            {
                if (stream != null)
                {
                    var closeFrame = YardCraneMiniWebSocketFrame.EncodeCloseMasked();
                    stream.Write(closeFrame, 0, closeFrame.Length);
                }
            }
            catch (Exception)
            {
                // Best-effort teardown.
            }

            CleanupAfterFailure();
            receiveThread = null;
        }
    }
}
