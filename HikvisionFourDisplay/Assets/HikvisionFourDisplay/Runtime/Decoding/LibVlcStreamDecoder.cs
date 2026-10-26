using System;
using System.Runtime.InteropServices;
using System.Text;
using LibVLCSharp.Shared;
using UnityEngine;

namespace HikvisionFourDisplay.Runtime.Decoding
{
    /// <summary>
    /// RTSP decoder backed by libvlc via LibVLCSharp (see Docs/解码方案PoC.md).
    ///
    /// libvlc's video format/lock/unlock/display callbacks fire on libvlc's own decode
    /// thread, never on Unity's main thread. Everything those callbacks touch (nativeBuffer,
    /// the pending* fields) is therefore either confined to that thread or moved to the main
    /// thread under syncRoot before it reaches a Unity API (Texture2D, the StateChanged/
    /// FaultReported events consumed by CameraFeedController/FeedSurfacePresenter). Tick()
    /// is the only place that crosses back onto the main thread.
    /// </summary>
    public sealed class LibVlcStreamDecoder : IVideoStreamDecoder
    {
        private const string Chroma = "RV32"; // 32bpp; paired with TextureFormat.BGRA32 below

        private readonly object syncRoot = new object();

        private LibVLC libVlc;
        private Media media;
        private MediaPlayer mediaPlayer;

        // Kept as fields so the GC cannot collect these while libvlc holds native function
        // pointers into them.
        private MediaPlayer.LibVLCVideoFormatCb formatCallback;
        private MediaPlayer.LibVLCVideoCleanupCb cleanupCallback;
        private MediaPlayer.LibVLCVideoLockCb lockCallback;
        private MediaPlayer.LibVLCVideoUnlockCb unlockCallback;
        private MediaPlayer.LibVLCVideoDisplayCb displayCallback;

        // Owned exclusively by libvlc's decode-thread callbacks; Tick() never touches this.
        private IntPtr nativeBuffer;

        // Cross-thread handoff to Tick(); guarded by syncRoot.
        private byte[] pendingFrame;
        private bool frameReady;
        private bool formatPending;
        private int pendingWidth;
        private int pendingHeight;
        private FeedConnectionState? pendingState;
        private string pendingFault;

        private Texture2D texture;
        private int frameWidth;
        private int frameHeight;
        private FeedConnectionState state = FeedConnectionState.NotConfigured;
        private bool disposed;

        public LibVlcStreamDecoder(string feedId)
        {
        }

        public FeedConnectionState State => state;
        public Texture CurrentTexture => texture;
        public DecoderFrameInfo FrameInfo => new DecoderFrameInfo(frameWidth, frameHeight);

        public event Action<FeedConnectionState> StateChanged;
        public event Action<string> FaultReported;

        public void Connect(DecoderConnectRequest request)
        {
            if (disposed) return;

            try
            {
                ReleaseSession();
                LibVlcRuntime.EnsureInitialized();

                // Hikvision's RTSP endpoint uses Digest authentication. With VLC 3.x the
                // rtsp-user/rtsp-pwd media options are accepted but are not forwarded to the
                // live555 RTSP access module. Supplying them as LibVLC instance arguments is
                // required for the authenticated DESCRIBE/SETUP exchange.
                var libVlcOptions = string.IsNullOrEmpty(request.Username)
                    ? new[]
                    {
                        "--rtsp-tcp",
                        "--network-caching=500",
                        "--rtsp-frame-buffer-size=2000000",
                        "--avcodec-hw=none",
                        "--no-audio"
                    }
                    : new[]
                    {
                        "--rtsp-tcp",
                        "--network-caching=500",
                        "--rtsp-frame-buffer-size=2000000",
                        "--avcodec-hw=none",
                        "--no-audio",
                        $"--rtsp-user={request.Username}",
                        $"--rtsp-pwd={request.Password}"
                    };

                libVlc = new LibVLC(false, libVlcOptions);
                mediaPlayer = new MediaPlayer(libVlc);

                formatCallback = OnVideoFormat;
                cleanupCallback = OnVideoCleanup;
                lockCallback = OnVideoLock;
                unlockCallback = OnVideoUnlock;
                displayCallback = OnVideoDisplay;

                mediaPlayer.SetVideoCallbacks(lockCallback, unlockCallback, displayCallback);
                mediaPlayer.SetVideoFormatCallbacks(formatCallback, cleanupCallback);

                mediaPlayer.Playing += OnPlaying;
                mediaPlayer.EncounteredError += OnError;
                mediaPlayer.EndReached += OnEndReached;

                media = new Media(libVlc, BuildUrl(request), FromType.FromLocation);
                mediaPlayer.Media = media;

                SetState(FeedConnectionState.Connecting);
                mediaPlayer.Play();
            }
            catch (Exception ex)
            {
                FaultReported?.Invoke(ex.GetType().Name);
                SetState(FeedConnectionState.Faulted);
                ReleaseSession();
            }
        }

        public void Disconnect()
        {
            ReleaseSession();
            if (!disposed) SetState(FeedConnectionState.NotConfigured);
        }

        public void Tick()
        {
            FeedConnectionState? stateToApply;
            string faultToReport;
            var sizeChanged = false;
            int uploadWidth = 0, uploadHeight = 0;

            lock (syncRoot)
            {
                stateToApply = pendingState;
                faultToReport = pendingFault;
                pendingState = null;
                pendingFault = null;

                if (formatPending)
                {
                    sizeChanged = true;
                    uploadWidth = pendingWidth;
                    uploadHeight = pendingHeight;
                    formatPending = false;
                }
            }

            if (faultToReport != null) FaultReported?.Invoke(faultToReport);
            if (stateToApply.HasValue) SetState(stateToApply.Value);
            if (sizeChanged) EnsureTexture(uploadWidth, uploadHeight);

            lock (syncRoot)
            {
                if (frameReady && texture != null && pendingFrame != null)
                {
                    texture.LoadRawTextureData(pendingFrame);
                    texture.Apply(false);
                    frameReady = false;
                }
            }
        }

        private static string BuildUrl(DecoderConnectRequest request)
        {
            return $"rtsp://{request.Host}:{request.RtspPort}/Streaming/Channels/{ResolveChannel(request.Channel, request.StreamType)}";
        }

        private static string ResolveChannel(string configuredChannel, string streamType)
        {
            var channel = string.IsNullOrWhiteSpace(configuredChannel) ? "1" : configuredChannel.Trim();
            var streamSuffix = string.Equals(streamType, "Sub", StringComparison.OrdinalIgnoreCase) ? "02" : "01";

            // Accept either a logical channel number ("1") or a complete Hikvision path
            // channel ("101"/"102"). The separate streamType field remains authoritative.
            if (!uint.TryParse(channel, out _)) return channel;
            if (channel.Length <= 2) return channel + streamSuffix;
            if (channel.EndsWith("01", StringComparison.Ordinal) ||
                channel.EndsWith("02", StringComparison.Ordinal))
                return channel.Substring(0, channel.Length - 2) + streamSuffix;

            return channel;
        }

        private void EnsureTexture(int width, int height)
        {
            if (width <= 0 || height <= 0) return;
            if (texture != null && texture.width == width && texture.height == height) return;

            if (texture != null) UnityEngine.Object.Destroy(texture);
            texture = new Texture2D(width, height, TextureFormat.BGRA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            frameWidth = width;
            frameHeight = height;
        }

        private void OnPlaying(object sender, EventArgs e) => RequestStateChange(FeedConnectionState.Playing, null);
        private void OnError(object sender, EventArgs e) => RequestStateChange(FeedConnectionState.Faulted, "player_error");
        private void OnEndReached(object sender, EventArgs e) => RequestStateChange(FeedConnectionState.NetworkLost, "end_reached");

        private void RequestStateChange(FeedConnectionState next, string faultReason)
        {
            lock (syncRoot)
            {
                pendingState = next;
                pendingFault = faultReason;
            }
        }

        // --- libvlc decode-thread callbacks below: no Unity API calls allowed here. ---

        private uint OnVideoFormat(ref IntPtr opaque, IntPtr chroma, ref uint width, ref uint height, ref uint pitches, ref uint lines)
        {
            var chromaBytes = Encoding.ASCII.GetBytes(Chroma);
            Marshal.Copy(chromaBytes, 0, chroma, chromaBytes.Length);

            pitches = width * 4;
            lines = height;
            var bufferSize = checked((int)(pitches * lines));

            nativeBuffer = Marshal.AllocHGlobal(bufferSize);

            lock (syncRoot)
            {
                pendingFrame = new byte[bufferSize];
                pendingWidth = (int)width;
                pendingHeight = (int)height;
                formatPending = true;
            }

            return 1;
        }

        private void OnVideoCleanup(ref IntPtr opaque)
        {
            if (nativeBuffer == IntPtr.Zero) return;
            Marshal.FreeHGlobal(nativeBuffer);
            nativeBuffer = IntPtr.Zero;
        }

        private IntPtr OnVideoLock(IntPtr opaque, IntPtr planes)
        {
            if (nativeBuffer != IntPtr.Zero) Marshal.WriteIntPtr(planes, 0, nativeBuffer);
            return IntPtr.Zero;
        }

        private void OnVideoUnlock(IntPtr opaque, IntPtr picture, IntPtr planes)
        {
        }

        private void OnVideoDisplay(IntPtr opaque, IntPtr picture)
        {
            if (nativeBuffer == IntPtr.Zero) return;

            lock (syncRoot)
            {
                if (pendingFrame == null) return;
                Marshal.Copy(nativeBuffer, pendingFrame, 0, pendingFrame.Length);
                frameReady = true;
            }
        }

        // --- end decode-thread callbacks ---

        private void SetState(FeedConnectionState next)
        {
            if (state == next) return;
            state = next;
            StateChanged?.Invoke(state);
        }

        private void ReleaseSession()
        {
            if (mediaPlayer != null)
            {
                mediaPlayer.Playing -= OnPlaying;
                mediaPlayer.EncounteredError -= OnError;
                mediaPlayer.EndReached -= OnEndReached;
                try { mediaPlayer.Stop(); }
                catch (Exception ex) { Debug.LogWarning($"[HFD_LIBVLC_STOP_WARNING] {ex.GetType().Name}"); }
                mediaPlayer.Dispose();
                mediaPlayer = null;
            }

            media?.Dispose();
            media = null;
            libVlc?.Dispose();
            libVlc = null;

            // nativeBuffer is freed by OnVideoCleanup, which libvlc guarantees to call while
            // tearing down the session above; freeing it again here would be a double-free.

            lock (syncRoot)
            {
                pendingFrame = null;
                frameReady = false;
                formatPending = false;
                pendingState = null;
                pendingFault = null;
            }

            if (texture != null)
            {
                UnityEngine.Object.Destroy(texture);
                texture = null;
            }
            frameWidth = 0;
            frameHeight = 0;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ReleaseSession();
            state = FeedConnectionState.Disposed;
        }
    }

    public sealed class LibVlcDecoderFactory : IVideoStreamDecoderFactory
    {
        public IVideoStreamDecoder Create(string feedId) => new LibVlcStreamDecoder(feedId);
    }
}
