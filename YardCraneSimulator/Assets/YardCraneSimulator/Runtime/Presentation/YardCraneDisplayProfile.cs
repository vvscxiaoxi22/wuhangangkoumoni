using System;

namespace PortTraining.YardCraneSimulator.Presentation
{
    public enum YardCraneDisplayLayout
    {
        Landscape16By9 = 0,
        Portrait9By16 = 1,
        DualStacked1920By2160 = 2
    }

    /// <summary>
    /// Resolution policy used by the scene-owned operational driving/result UI.
    /// The current simulator uses two native 1920x1080 Unity displays. The legacy
    /// 1920x2160 layout remains detectable only for backward-compatible deployments.
    /// </summary>
    public readonly struct YardCraneDisplayProfile
    {
        public const string DualStackedProfileId = "SIMULATOR-DUAL-STACK-1920X2160-V1";
        public const string NativeDualDisplayProfileId = "SIMULATOR-DUAL-DISPLAY-1920X1080X2-V2";

        private YardCraneDisplayProfile(
            YardCraneDisplayLayout layout,
            int referenceWidth,
            int referenceHeight)
        {
            Layout = layout;
            ReferenceWidth = referenceWidth;
            ReferenceHeight = referenceHeight;
        }

        public YardCraneDisplayLayout Layout { get; }
        public int ReferenceWidth { get; }
        public int ReferenceHeight { get; }
        public bool IsDualStacked => Layout == YardCraneDisplayLayout.DualStacked1920By2160;
        public bool IsPortrait => Layout == YardCraneDisplayLayout.Portrait9By16;

        public float CalculateScale(int width, int height)
        {
            if (width <= 0 || height <= 0) return 1f;
            return Math.Max(0.1f, Math.Min(
                width / (float)ReferenceWidth,
                height / (float)ReferenceHeight));
        }

        public static YardCraneDisplayProfile Detect(int width, int height)
        {
            if (width <= 0 || height <= 0)
                return new YardCraneDisplayProfile(YardCraneDisplayLayout.Landscape16By9, 1920, 1080);

            if (height > width)
            {
                // 1920x2160 is only slightly taller than square (8:9). A true 9:16
                // portrait panel is much narrower, so the two layouts cannot alias.
                if (width / (float)height >= 0.8f)
                    return new YardCraneDisplayProfile(
                        YardCraneDisplayLayout.DualStacked1920By2160, 1920, 2160);

                return new YardCraneDisplayProfile(YardCraneDisplayLayout.Portrait9By16, 1080, 1920);
            }

            return new YardCraneDisplayProfile(YardCraneDisplayLayout.Landscape16By9, 1920, 1080);
        }
    }
}
