using System;

namespace PortTraining.YardCraneSimulator.Presentation
{
    /// <summary>
    /// Persistent values for the existing right-hand task card. This is a view model only;
    /// callers remain responsible for task authority and completion decisions.
    /// </summary>
    public sealed class YardCraneHudTrainingTaskPresentation
    {
        public YardCraneHudTrainingTaskPresentation(
            string title,
            string primaryLabel,
            string primaryValue,
            string secondaryLabel,
            string secondaryValue)
        {
            Title = title ?? string.Empty;
            PrimaryLabel = primaryLabel ?? string.Empty;
            PrimaryValue = primaryValue ?? string.Empty;
            SecondaryLabel = secondaryLabel ?? string.Empty;
            SecondaryValue = secondaryValue ?? string.Empty;
        }

        public string Title { get; }
        public string PrimaryLabel { get; }
        public string PrimaryValue { get; }
        public string SecondaryLabel { get; }
        public string SecondaryValue { get; }
    }

    /// <summary>
    /// A caller-supplied local result summary for the existing lifecycle result card.
    /// No score field exists by design: authoritative scores come from NodeService/teacher
    /// services, and the scene's local coordinator must never imitate that authority.
    /// </summary>
    public sealed class YardCraneHudLocalResultPresentation
    {
        public YardCraneHudLocalResultPresentation(
            string title,
            string detail,
            string trustText,
            HudTone tone)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Detail = detail ?? string.Empty;
            TrustText = trustText ?? string.Empty;
            Tone = tone;
        }

        public string Title { get; }
        public string Detail { get; }
        public string TrustText { get; }
        public HudTone Tone { get; }
    }
}
