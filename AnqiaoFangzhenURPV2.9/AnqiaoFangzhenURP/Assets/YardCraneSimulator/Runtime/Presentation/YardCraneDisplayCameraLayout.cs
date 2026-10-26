using UnityEngine;

namespace PortTraining.YardCraneSimulator.Presentation
{
    /// <summary>
    /// Owns the formal operational scene's two native Unity displays. The simulator uses
    /// two independent 1920x1080 Windows displays: Display1 renders the driving view and
    /// HUD, while Display2 renders its companion camera without a second UIDocument.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class YardCraneDisplayCameraLayout : MonoBehaviour
    {
        public const string Version = "2026-09-10-yc-operational-scene-v2-native-dual-display";

        [SerializeField] private Camera display1Camera;
        [SerializeField] private Camera display2Camera;

        private YardCraneDisplayLayout appliedLayout;
        private bool secondDisplayActivated;

        public Camera Display1Camera => display1Camera;
        public Camera Display2Camera => display2Camera;
        public YardCraneDisplayLayout AppliedLayout => appliedLayout;

        public void Configure(Camera upperDisplay1Camera, Camera lowerDisplay2Camera)
        {
            display1Camera = upperDisplay1Camera;
            display2Camera = lowerDisplay2Camera;
            ApplyNativeDisplayBaseline();
        }

        private void Awake()
        {
            ApplyNativeDisplayBaseline();
            ActivateSecondDisplayIfAvailable();
        }

        private void Update()
        {
            ActivateSecondDisplayIfAvailable();
        }

        private void ApplyNativeDisplayBaseline()
        {
            if (display1Camera == null || display2Camera == null)
            {
                Debug.LogError(
                    "[YC_OPERATIONAL_DISPLAY_INVALID] Two serialized cameras are required.", this);
                return;
            }

            ConfigureCamera(display1Camera, 0, true);
            ConfigureCamera(display2Camera, 1, true);
            appliedLayout = YardCraneDisplayLayout.Landscape16By9;

            Debug.Log(
                $"[YC_OPERATIONAL_DISPLAY_READY] version={Version}, layout={appliedLayout}, " +
                $"displayCount={Display.displays.Length}, cameras=2, cameraTargets=0+1, " +
                "display1Hud=1920x1080", this);
        }

        private void ActivateSecondDisplayIfAvailable()
        {
            if (!Application.isPlaying || secondDisplayActivated || Display.displays.Length < 2) return;
            Display.displays[1].Activate();
            secondDisplayActivated = true;
            Debug.Log("[YC_OPERATIONAL_DISPLAY2_ACTIVATED] unityDisplayIndex=1", this);
        }

        private static void ConfigureCamera(Camera camera, int targetDisplay, bool enabled)
        {
            if (camera == null) return;
            camera.targetDisplay = targetDisplay;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.enabled = enabled;
        }
    }
}
