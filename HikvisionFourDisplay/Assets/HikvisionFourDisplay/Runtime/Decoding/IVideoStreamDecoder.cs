using System;
using UnityEngine;

namespace HikvisionFourDisplay.Runtime.Decoding
{
    public enum FeedConnectionState
    {
        NotConfigured,
        Connecting,
        Playing,
        AuthFailed,
        NetworkLost,
        UnsupportedFormat,
        Reconnecting,
        Faulted,
        Disposed
    }

    public readonly struct DecoderFrameInfo
    {
        public readonly int Width;
        public readonly int Height;

        public DecoderFrameInfo(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public bool IsValid => Width > 0 && Height > 0;
    }

    public readonly struct DecoderConnectRequest
    {
        public readonly string FeedId;
        public readonly string Host;
        public readonly int RtspPort;
        public readonly string Channel;
        public readonly string StreamType;
        public readonly string Username;
        public readonly string Password;

        public DecoderConnectRequest(
            string feedId,
            string host,
            int rtspPort,
            string channel,
            string streamType,
            string username,
            string password)
        {
            FeedId = feedId;
            Host = host;
            RtspPort = rtspPort;
            Channel = channel;
            StreamType = streamType;
            Username = username;
            Password = password;
        }
    }

    /// <summary>
    /// Decode backends (mock, LibVLC, FFmpeg, ...) implement this without the
    /// controller/presenter layers knowing which backend is active.
    /// </summary>
    public interface IVideoStreamDecoder : IDisposable
    {
        FeedConnectionState State { get; }
        Texture CurrentTexture { get; }
        DecoderFrameInfo FrameInfo { get; }

        event Action<FeedConnectionState> StateChanged;
        event Action<string> FaultReported;

        void Connect(DecoderConnectRequest request);
        void Disconnect();

        /// <summary>Called once per frame from the owning controller's Update.</summary>
        void Tick();
    }

    public interface IVideoStreamDecoderFactory
    {
        IVideoStreamDecoder Create(string feedId);
    }
}
