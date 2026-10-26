using System;
using HikvisionFourDisplay.Runtime.Configuration;
using HikvisionFourDisplay.Runtime.Decoding;
using HikvisionFourDisplay.Runtime.Diagnostics;
using HikvisionFourDisplay.Runtime.Presentation;
using HikvisionFourDisplay.Runtime.Security;
using UnityEngine;

namespace HikvisionFourDisplay.Runtime.Feeds
{
    /// <summary>
    /// Owns exactly one feed's decoder lifecycle: connect, capped-backoff retry,
    /// and release. Every failure path is caught and turned into a state/log line
    /// instead of an exception, so one feed can never stall or crash the other three.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraFeedController : MonoBehaviour
    {
        private const float BaseBackoffSeconds = 2f;
        private const float MaxBackoffSeconds = 30f;
        private const int MaxTrackedAttempts = 6;

        [SerializeField] private string feedId = string.Empty;
        [SerializeField] private int displayIndex = -1;
        [SerializeField] private FeedSurfacePresenter presenter;

        private IVideoStreamDecoder decoder;
        private ICredentialProvider credentialProvider;
        private FeedConfigEntry config;
        private bool initialized;
        private bool retryScheduled;
        private float nextAttemptAt;
        private int retryAttempt;
        private Texture lastPresentedTexture;

        public string FeedId => feedId;
        public int DisplayIndex => displayIndex;

        public void Configure(string id, int display, FeedSurfacePresenter surfacePresenter)
        {
            feedId = id;
            displayIndex = display;
            presenter = surfacePresenter;
        }

        public void Initialize(FeedConfigEntry entry, ICredentialProvider credentials, IVideoStreamDecoderFactory decoderFactory)
        {
            ReleaseDecoder();
            config = entry;
            credentialProvider = credentials;
            presenter.SetRotationDegrees(config?.rotationDegrees ?? 0);

            decoder = decoderFactory.Create(feedId);
            decoder.StateChanged += OnDecoderStateChanged;
            decoder.FaultReported += OnDecoderFault;
            initialized = true;
            retryAttempt = 0;
            retryScheduled = false;
            lastPresentedTexture = null;

            if (config == null || !config.enabled)
            {
                presenter.ApplyState(FeedConnectionState.NotConfigured, null, default);
                return;
            }

            AttemptConnect();
        }

        /// <summary>Updates this feed's display orientation immediately without reconnecting its decoder.</summary>
        public void SetRotationDegrees(int degrees)
        {
            presenter.SetRotationDegrees(degrees);
            if (config != null) config.rotationDegrees = presenter.RotationDegrees;
        }

        private void Update()
        {
            if (!initialized) return;

            decoder.Tick();

            // libvlc can report Playing before its video-format callback has created the
            // Unity texture. Re-apply Playing once that texture actually exists so the
            // presenter cannot remain hidden after an early state notification.
            if (decoder.State == FeedConnectionState.Playing &&
                decoder.CurrentTexture != null &&
                decoder.CurrentTexture != lastPresentedTexture)
            {
                lastPresentedTexture = decoder.CurrentTexture;
                presenter.ApplyState(FeedConnectionState.Playing, decoder.CurrentTexture, decoder.FrameInfo);
                Debug.Log($"[HFD_FEED_TEXTURE_READY] feedId={feedId} displayIndex={displayIndex} " +
                          $"width={decoder.FrameInfo.Width} height={decoder.FrameInfo.Height}");
            }

            if (retryScheduled && Time.unscaledTime >= nextAttemptAt)
            {
                retryScheduled = false;
                AttemptConnect();
            }
        }

        private void AttemptConnect()
        {
            if (config == null || !config.enabled) return;

            if (!credentialProvider.TryGet(config.credentialKey, out var credential))
            {
                Debug.LogWarning($"[HFD_FEED_CREDENTIAL_MISSING] feedId={feedId} displayIndex={displayIndex} credentialKey={config.credentialKey}");
                presenter.ApplyState(FeedConnectionState.AuthFailed, null, default, "credential not found");
                ScheduleRetry();
                return;
            }

            try
            {
                var request = new DecoderConnectRequest(
                    feedId, config.host, config.rtspPort, config.channel, config.streamType,
                    credential.Username, credential.Password);

                Debug.Log($"[HFD_FEED_CONNECT] feedId={feedId} displayIndex={displayIndex} " +
                          $"endpoint={LogRedaction.MaskEndpoint(config.host, config.rtspPort, config.channel)} streamType={config.streamType}");

                decoder.Connect(request);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HFD_FEED_CONNECT_ERROR] feedId={feedId} displayIndex={displayIndex} exceptionType={ex.GetType().Name}");
                presenter.ApplyState(FeedConnectionState.Faulted, null, default, "connect exception");
                ScheduleRetry();
            }
        }

        private void OnDecoderStateChanged(FeedConnectionState state)
        {
            presenter.ApplyState(state, decoder.CurrentTexture, decoder.FrameInfo);
            if (state == FeedConnectionState.Playing && decoder.CurrentTexture != null)
                lastPresentedTexture = decoder.CurrentTexture;
            else if (state != FeedConnectionState.Playing)
                lastPresentedTexture = null;
            Debug.Log($"[HFD_FEED_STATE] feedId={feedId} displayIndex={displayIndex} state={state}");

            switch (state)
            {
                case FeedConnectionState.Playing:
                    retryAttempt = 0;
                    break;
                case FeedConnectionState.NetworkLost:
                case FeedConnectionState.Faulted:
                case FeedConnectionState.UnsupportedFormat:
                case FeedConnectionState.AuthFailed:
                    ScheduleRetry();
                    break;
            }
        }

        private void OnDecoderFault(string message)
        {
            Debug.LogWarning($"[HFD_FEED_FAULT] feedId={feedId} displayIndex={displayIndex} msg={message}");
        }

        private void ScheduleRetry()
        {
            retryAttempt = Mathf.Min(retryAttempt + 1, MaxTrackedAttempts);
            var backoff = Mathf.Min(BaseBackoffSeconds * Mathf.Pow(2, retryAttempt - 1), MaxBackoffSeconds);
            nextAttemptAt = Time.unscaledTime + backoff;
            retryScheduled = true;
            Debug.Log($"[HFD_FEED_RETRY_SCHEDULED] feedId={feedId} displayIndex={displayIndex} attempt={retryAttempt} backoffSeconds={backoff:0.0}");
        }

        public void Shutdown() => ReleaseDecoder();

        private void OnDestroy() => ReleaseDecoder();
        private void OnApplicationQuit() => ReleaseDecoder();

        private void ReleaseDecoder()
        {
            if (decoder == null) return;
            decoder.StateChanged -= OnDecoderStateChanged;
            decoder.FaultReported -= OnDecoderFault;
            decoder.Dispose();
            decoder = null;
            initialized = false;
            lastPresentedTexture = null;
        }
    }
}
