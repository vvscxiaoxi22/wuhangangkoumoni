using System;
using HikvisionFourDisplay.Runtime.Configuration;
using HikvisionFourDisplay.Runtime.Decoding;
using HikvisionFourDisplay.Runtime.Feeds;
using HikvisionFourDisplay.Runtime.Security;
using UnityEngine;

namespace HikvisionFourDisplay.Runtime.Bootstrap
{
    /// <summary>
    /// Activates the extended-desktop displays and hands each pre-wired
    /// CameraFeedController its config entry. The feedControllers array is assigned
    /// once at scene-build time in Display1..4 order, so mapping never depends on
    /// FindObjectsOfType ordering.
    /// </summary>
    public sealed class FourDisplayBootstrap : MonoBehaviour
    {
        [SerializeField] private CameraFeedController[] feedControllers = Array.Empty<CameraFeedController>();
        [SerializeField] private Canvas routedCanvas;

        private static bool displaysActivatedThisProcess;

        private ICredentialProvider credentialProvider;
        private IVideoStreamDecoderFactory decoderFactory;
        private readonly FeedConfigEntry[] appliedFeeds = new FeedConfigEntry[4];

        public void Configure(CameraFeedController[] controllers)
        {
            feedControllers = controllers ?? Array.Empty<CameraFeedController>();
        }

        private void Awake()
        {
            ActivateDisplays();

            // Hide the scene-authored Canvas before the first rendered frame. Start()
            // applies the persisted single-display selection immediately afterwards.
            var canvas = ResolveRoutedCanvas();
            if (canvas != null)
            {
                canvas.enabled = false;
                canvas.gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            credentialProvider = new WindowsCredentialManagerProvider();
            // The site cameras are now available for the pending real-stream validation.
            // Keep the decoder behind IVideoStreamDecoderFactory so the mock can still be
            // selected explicitly in a future diagnostic build without changing controllers.
            decoderFactory = new LibVlcDecoderFactory();

            LoadAndApplyConfiguration();
        }

        /// <summary>
        /// Re-reads Config/feeds.json and re-initializes every feed controller with it.
        /// Retained for callers that intentionally need a full four-feed refresh.
        /// </summary>
        public void ReloadConfiguration()
        {
            if (credentialProvider == null || decoderFactory == null) return; // Start() has not run yet.
            LoadAndApplyConfiguration();
        }

        /// <summary>
        /// Re-reads Config/feeds.json and re-initializes only the controller assigned to
        /// the requested display. The other displays keep their current decoder and texture.
        /// </summary>
        public bool ReloadFeed(int displayIndex)
        {
            if (credentialProvider == null || decoderFactory == null) return false; // Start() has not run yet.

            CameraFeedController targetController = null;
            foreach (var controller in feedControllers)
            {
                if (controller != null && controller.DisplayIndex == displayIndex)
                {
                    targetController = controller;
                    break;
                }
            }

            if (targetController == null)
            {
                Debug.LogWarning($"[HFD_CONFIG_FEED_RELOAD] displayIndex={displayIndex} status=controller_missing");
                return false;
            }

            var document = LoadConfiguration();
            ApplyCanvasDisplay(document.canvasDisplayIndex);
            targetController.Initialize(FindEntry(document, displayIndex), credentialProvider, decoderFactory);
            RememberFeed(displayIndex, FindEntry(document, displayIndex));
            Debug.Log($"[HFD_CONFIG_FEED_RELOAD] displayIndex={displayIndex} status=invoked");
            return true;
        }

        private void LoadAndApplyConfiguration()
        {
            var document = LoadConfiguration();
            ApplyConfiguration(document);
        }

        public bool ApplyConfiguration(FeedConfigurationDocument document, bool reconnectChangedOnly = false)
        {
            if (document == null || credentialProvider == null || decoderFactory == null) return false;
            ApplyCanvasDisplay(document.canvasDisplayIndex);

            foreach (var controller in feedControllers)
            {
                if (controller == null) continue;
                var entry = FindEntry(document, controller.DisplayIndex);
                var index = controller.DisplayIndex;
                if (reconnectChangedOnly && index >= 0 && index < appliedFeeds.Length &&
                    SameConnection(appliedFeeds[index], entry))
                    controller.SetRotationDegrees(entry?.rotationDegrees ?? 0);
                else
                    controller.Initialize(entry, credentialProvider, decoderFactory);
                RememberFeed(index, entry);
            }
            return true;
        }

        public bool SetFeedRotation(int displayIndex, int rotationDegrees)
        {
            foreach (var controller in feedControllers)
            {
                if (controller == null || controller.DisplayIndex != displayIndex) continue;
                controller.SetRotationDegrees(rotationDegrees);
                if (displayIndex >= 0 && displayIndex < appliedFeeds.Length && appliedFeeds[displayIndex] != null)
                    appliedFeeds[displayIndex].rotationDegrees = rotationDegrees;
                return true;
            }
            return false;
        }

        private void RememberFeed(int index, FeedConfigEntry entry)
        {
            if (index < 0 || index >= appliedFeeds.Length) return;
            var document = new FeedConfigurationDocument { feeds = entry == null
                ? Array.Empty<FeedConfigEntry>() : new[] { entry } };
            var copy = FeedSchemeStore.CloneConfiguration(document);
            appliedFeeds[index] = copy.feeds.Length == 0 ? null : copy.feeds[0];
        }

        private static bool SameConnection(FeedConfigEntry left, FeedConfigEntry right)
        {
            if (left == null || right == null) return left == right;
            return left.enabled == right.enabled && left.rtspPort == right.rtspPort &&
                   string.Equals(left.host, right.host, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(left.channel, right.channel, StringComparison.Ordinal) &&
                   string.Equals(left.streamType, right.streamType, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(left.credentialKey, right.credentialKey, StringComparison.Ordinal);
        }

        /// <summary>
        /// Routes the single scene Canvas to one Unity display. A negative or invalid
        /// index disables it, so the Canvas can never be duplicated across displays.
        /// </summary>
        public bool ApplyCanvasDisplay(int displayIndex)
        {
            var canvas = ResolveRoutedCanvas();
            if (canvas == null)
            {
                Debug.LogWarning("[HFD_CANVAS_ROUTE] status=canvas_missing");
                return false;
            }

            if (displayIndex < 0 || displayIndex > 3)
            {
                canvas.enabled = false;
                canvas.gameObject.SetActive(false);
                Debug.Log($"[HFD_CANVAS_ROUTE] status=disabled requestedDisplayIndex={displayIndex}");
                return true;
            }

            canvas.targetDisplay = displayIndex;
            if (!canvas.gameObject.activeSelf) canvas.gameObject.SetActive(true);
            canvas.enabled = true;

            if (displayIndex >= Display.displays.Length)
            {
                Debug.LogWarning($"[HFD_CANVAS_ROUTE] status=target_display_missing " +
                                 $"displayIndex={displayIndex} detectedDisplays={Display.displays.Length}");
            }
            else
            {
                Debug.Log($"[HFD_CANVAS_ROUTE] status=enabled displayIndex={displayIndex} canvas={canvas.name}");
            }
            return true;
        }

        private Canvas ResolveRoutedCanvas()
        {
            if (routedCanvas != null) return routedCanvas;

            Canvas fallback = null;
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var candidate in canvases)
            {
                if (candidate == null || candidate.gameObject.scene != gameObject.scene) continue;
                fallback ??= candidate;
                if (!string.Equals(candidate.name, "Canvas", StringComparison.Ordinal)) continue;
                routedCanvas = candidate;
                break;
            }

            routedCanvas ??= fallback;
            return routedCanvas;
        }

        private static FeedConfigurationDocument LoadConfiguration()
        {
            var document = FeedConfigurationLoader.Load(out var warnings, out var sourcePath);
            foreach (var warning in warnings)
                Debug.LogWarning($"[HFD_CONFIG_WARNING] {warning}");
            Debug.Log($"[HFD_CONFIG_LOADED] source={sourcePath ?? "none"} feedCount={document.feeds.Length}");
            return document;
        }

        private static FeedConfigEntry FindEntry(FeedConfigurationDocument document, int displayIndex)
        {
            foreach (var entry in document.feeds)
            {
                if (entry.displayIndex == displayIndex) return entry;
            }
            return null;
        }

        private void ActivateDisplays()
        {
            if (displaysActivatedThisProcess)
            {
                Debug.Log("[HFD_DISPLAY_ACTIVATE_SKIPPED] reason=already_activated_this_process");
                return;
            }
            displaysActivatedThisProcess = true;

            var displays = Display.displays;
            Debug.Log($"[HFD_DISPLAY_DETECT] count={displays.Length}");

            for (var i = 1; i < 4; i++)
            {
                if (i < displays.Length)
                {
                    displays[i].Activate();
                    Debug.Log($"[HFD_DISPLAY_ACTIVATED] index={i} width={displays[i].systemWidth} height={displays[i].systemHeight}");
                }
                else
                {
                    Debug.LogWarning($"[HFD_DISPLAY_MISSING] index={i} status=not_connected");
                }
            }
        }

        private void OnApplicationQuit()
        {
            foreach (var controller in feedControllers)
            {
                if (controller != null) controller.Shutdown();
            }
        }
    }
}
