using System;
using UnityEngine;

namespace HikvisionFourDisplay.Runtime.Decoding
{
    /// <summary>
    /// Simulated decoder used until a real RTSP backend is confirmed and wired in.
    /// Produces a per-feed test pattern so the config/credential/controller/presenter
    /// pipeline is exercisable end-to-end without real cameras.
    /// </summary>
    public sealed class MockPatternDecoder : IVideoStreamDecoder
    {
        private const int TextureWidth = 320;
        private const int TextureHeight = 180;
        private const float ConnectDelaySeconds = 1.2f;
        private const float TargetFrameIntervalSeconds = 1f / 15f;

        private readonly string feedId;
        private readonly Color32 baseColor;

        private Texture2D texture;
        private Color32[] pixelBuffer;
        private FeedConnectionState state = FeedConnectionState.NotConfigured;
        private float stateEnteredAt;
        private float lastFrameAt;
        private bool connectRequested;

        public MockPatternDecoder(string feedId)
        {
            this.feedId = feedId;
            baseColor = HashToColor(feedId);
        }

        public FeedConnectionState State => state;
        public Texture CurrentTexture => texture;
        public DecoderFrameInfo FrameInfo => new DecoderFrameInfo(TextureWidth, TextureHeight);

        public event Action<FeedConnectionState> StateChanged;
        public event Action<string> FaultReported;

        public void Connect(DecoderConnectRequest request)
        {
            connectRequested = true;
            EnsureTexture();
            SetState(FeedConnectionState.Connecting);
        }

        public void Disconnect()
        {
            connectRequested = false;
            SetState(FeedConnectionState.NotConfigured);
        }

        public void Tick()
        {
            if (!connectRequested) return;

            if (state == FeedConnectionState.Connecting &&
                Time.unscaledTime - stateEnteredAt >= ConnectDelaySeconds)
            {
                SetState(FeedConnectionState.Playing);
            }

            if (state == FeedConnectionState.Playing &&
                Time.unscaledTime - lastFrameAt >= TargetFrameIntervalSeconds)
            {
                RenderFrame();
                lastFrameAt = Time.unscaledTime;
            }
        }

        public void SimulateNetworkLoss()
        {
            if (connectRequested) SetState(FeedConnectionState.NetworkLost);
        }

        public void SimulateAuthFailure()
        {
            SetState(FeedConnectionState.AuthFailed);
        }

        public void SimulateRecovery()
        {
            if (connectRequested) SetState(FeedConnectionState.Connecting);
        }

        private void RenderFrame()
        {
            var t = Time.unscaledTime;
            var barX = Mathf.RoundToInt((Mathf.Sin(t * 0.6f) * 0.5f + 0.5f) * (TextureWidth - 8));

            for (var y = 0; y < TextureHeight; y++)
            {
                var rowOffset = y * TextureWidth;
                for (var x = 0; x < TextureWidth; x++)
                {
                    var onBar = x >= barX && x < barX + 8;
                    pixelBuffer[rowOffset + x] = onBar ? Color.white : baseColor;
                }
            }

            texture.SetPixels32(pixelBuffer);
            texture.Apply(false);
        }

        private void EnsureTexture()
        {
            if (texture != null) return;

            texture = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false)
            {
                name = $"{feedId}_MockTexture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            pixelBuffer = new Color32[TextureWidth * TextureHeight];
        }

        private void SetState(FeedConnectionState next)
        {
            if (state == next) return;
            state = next;
            stateEnteredAt = Time.unscaledTime;
            StateChanged?.Invoke(state);
        }

        private static Color32 HashToColor(string id)
        {
            unchecked
            {
                var hash = 23;
                foreach (var c in id) hash = hash * 31 + c;
                var r = (byte)(80 + (hash & 0x7F));
                var g = (byte)(80 + ((hash >> 7) & 0x7F));
                var b = (byte)(80 + ((hash >> 14) & 0x7F));
                return new Color32(r, g, b, 255);
            }
        }

        public void Dispose()
        {
            connectRequested = false;
            if (texture != null)
            {
                UnityEngine.Object.Destroy(texture);
                texture = null;
            }
            pixelBuffer = null;
            state = FeedConnectionState.Disposed;
        }
    }

    public sealed class MockPatternDecoderFactory : IVideoStreamDecoderFactory
    {
        public IVideoStreamDecoder Create(string feedId) => new MockPatternDecoder(feedId);
    }
}
