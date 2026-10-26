using System;
using System.IO;
using HikvisionFourDisplay.Runtime.Decoding;
using UnityEngine;

namespace HikvisionFourDisplay.Runtime.Presentation
{
    /// <summary>
    /// Owns one DisplayNVideoSurface quad and its status label. Always assigns the
    /// decoded texture through an instance material (renderer.material), never
    /// renderer.sharedMaterial, so a fault on one feed cannot bleed its texture or
    /// state text onto another feed's screen.
    /// </summary>
    public sealed class FeedSurfacePresenter : MonoBehaviour
    {
        private const float FrameWidth = 16f;
        private const float FrameHeight = 9f;
        private const string FallbackImageFileName = "默认图片.png";

        private static Texture2D fallbackTexture;
        private static bool fallbackLoadAttempted;
        private static int fallbackTextureUsers;

        [SerializeField] private MeshRenderer videoSurfaceRenderer;
        [SerializeField] private TextMesh statusLabel;
        [SerializeField] private Camera ownerCamera;
        [SerializeField] private int displayNumber;
        [SerializeField] private Color idleBackgroundColor = Color.black;

        private Material instanceMaterial;
        private bool fallbackTextureAcquired;
        private int rotationDegrees;
        private bool showingVideo;
        private DecoderFrameInfo presentedFrameInfo;

        public int RotationDegrees => rotationDegrees;

        public void Configure(MeshRenderer renderer, TextMesh label, Camera camera, int number, Color placeholderColor)
        {
            videoSurfaceRenderer = renderer;
            statusLabel = label;
            ownerCamera = camera;
            displayNumber = number;
            idleBackgroundColor = placeholderColor;
        }

        private void Awake()
        {
            AcquireFallbackTexture();

            var shader = Shader.Find("Unlit/Texture");
            instanceMaterial = new Material(shader) { name = $"Display{displayNumber}FeedMaterial" };
            videoSurfaceRenderer.material = instanceMaterial;

            if (!ShowFallbackImage())
            {
                videoSurfaceRenderer.enabled = false;
                if (ownerCamera != null) ownerCamera.backgroundColor = idleBackgroundColor;
            }
        }

        public void ApplyState(FeedConnectionState state, Texture texture, DecoderFrameInfo frameInfo, string detail = null)
        {
            if (state == FeedConnectionState.Playing && texture != null)
            {
                showingVideo = true;
                presentedFrameInfo = frameInfo.IsValid
                    ? frameInfo
                    : new DecoderFrameInfo(texture.width, texture.height);
                instanceMaterial.mainTexture = texture;
                ApplyTextureOrientation(flipVertically: true);
                ApplyVideoLayout();
                videoSurfaceRenderer.enabled = true;
                if (ownerCamera != null) ownerCamera.backgroundColor = Color.black;
                if (statusLabel != null) statusLabel.text = string.Empty;
                return;
            }

            showingVideo = false;
            presentedFrameInfo = default;
            videoSurfaceRenderer.transform.localRotation = Quaternion.identity;
            if (ShowFallbackImage()) return;

            videoSurfaceRenderer.enabled = false;
            if (ownerCamera != null) ownerCamera.backgroundColor = idleBackgroundColor;
            if (statusLabel != null) statusLabel.text = DescribeState(state, detail);
        }

        /// <summary>Positive angles rotate the decoded video clockwise; fallback artwork stays upright.</summary>
        public void SetRotationDegrees(int degrees)
        {
            var normalized = (degrees % 360 + 360) % 360;
            rotationDegrees = ((normalized + 45) / 90 * 90) % 360;
            if (showingVideo)
                ApplyVideoLayout();
            else if (videoSurfaceRenderer != null)
                videoSurfaceRenderer.transform.localRotation = Quaternion.identity;
        }

        private void ApplyVideoLayout()
        {
            videoSurfaceRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, -rotationDegrees);
            ApplyAspectFit(presentedFrameInfo.Width, presentedFrameInfo.Height,
                swapAxes: rotationDegrees == 90 || rotationDegrees == 270);
        }

        private void ApplyAspectFit(int width, int height, bool swapAxes = false)
        {
            if (width <= 0 || height <= 0) return;

            var videoAspect = swapAxes ? (float)height / width : (float)width / height;
            var frameAspect = FrameWidth / FrameHeight;

            float scaleX, scaleY;
            if (videoAspect > frameAspect)
            {
                scaleX = FrameWidth;
                scaleY = FrameWidth / videoAspect;
            }
            else
            {
                scaleY = FrameHeight;
                scaleX = FrameHeight * videoAspect;
            }

            // Fit the rotated bounding rectangle, then restore the Quad's unrotated local axes.
            videoSurfaceRenderer.transform.localScale = swapAxes
                ? new Vector3(scaleY, scaleX, 1f)
                : new Vector3(scaleX, scaleY, 1f);
        }

        private bool ShowFallbackImage()
        {
            if (fallbackTexture == null || instanceMaterial == null) return false;

            showingVideo = false;
            presentedFrameInfo = default;
            instanceMaterial.mainTexture = fallbackTexture;
            ApplyTextureOrientation(flipVertically: false);
            videoSurfaceRenderer.transform.localRotation = Quaternion.identity;
            ApplyAspectFit(fallbackTexture.width, fallbackTexture.height);
            videoSurfaceRenderer.enabled = true;
            if (ownerCamera != null) ownerCamera.backgroundColor = Color.black;
            if (statusLabel != null) statusLabel.text = string.Empty;
            return true;
        }

        private void ApplyTextureOrientation(bool flipVertically)
        {
            instanceMaterial.mainTextureScale = flipVertically
                ? new Vector2(1f, -1f)
                : Vector2.one;
            instanceMaterial.mainTextureOffset = flipVertically
                ? new Vector2(0f, 1f)
                : Vector2.zero;
        }

        private void AcquireFallbackTexture()
        {
            if (!fallbackLoadAttempted)
            {
                fallbackLoadAttempted = true;
                var imagePath = Path.Combine(Application.streamingAssetsPath, FallbackImageFileName);

                try
                {
                    if (!File.Exists(imagePath))
                    {
                        Debug.LogError($"[HFD_FALLBACK_IMAGE_MISSING] path={imagePath}");
                    }
                    else
                    {
                        var imageBytes = File.ReadAllBytes(imagePath);
                        var loadedTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
                        {
                            name = "HFD_DefaultFallbackImage",
                            wrapMode = TextureWrapMode.Clamp,
                            filterMode = FilterMode.Bilinear
                        };

                        if (loadedTexture.LoadImage(imageBytes, markNonReadable: true))
                        {
                            fallbackTexture = loadedTexture;
                            Debug.Log($"[HFD_FALLBACK_IMAGE_LOADED] path={imagePath} " +
                                      $"width={fallbackTexture.width} height={fallbackTexture.height}");
                        }
                        else
                        {
                            Destroy(loadedTexture);
                            Debug.LogError($"[HFD_FALLBACK_IMAGE_INVALID] path={imagePath}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[HFD_FALLBACK_IMAGE_ERROR] path={imagePath} exceptionType={ex.GetType().Name}");
                }
            }

            fallbackTextureUsers++;
            fallbackTextureAcquired = true;
        }

        private string DescribeState(FeedConnectionState state, string detail)
        {
            var headline = state switch
            {
                FeedConnectionState.NotConfigured => "NOT CONFIGURED",
                FeedConnectionState.Connecting => "CONNECTING...",
                FeedConnectionState.AuthFailed => "AUTH FAILED",
                FeedConnectionState.NetworkLost => "NETWORK LOST",
                FeedConnectionState.UnsupportedFormat => "FORMAT UNSUPPORTED",
                FeedConnectionState.Reconnecting => "RECONNECTING...",
                FeedConnectionState.Faulted => "FEED FAULT",
                FeedConnectionState.Disposed => "STOPPED",
                _ => "STREAM NOT CONFIGURED"
            };

            var text = $"DISPLAY {displayNumber}\nCAMERA {displayNumber}\n{headline}";
            return string.IsNullOrEmpty(detail) ? text : text + "\n" + detail;
        }

        private void OnDestroy()
        {
            if (instanceMaterial != null) Destroy(instanceMaterial);

            if (!fallbackTextureAcquired) return;
            fallbackTextureAcquired = false;
            fallbackTextureUsers = Mathf.Max(0, fallbackTextureUsers - 1);
            if (fallbackTextureUsers != 0) return;

            if (fallbackTexture != null) Destroy(fallbackTexture);
            fallbackTexture = null;
            fallbackLoadAttempted = false;
        }
    }
}
