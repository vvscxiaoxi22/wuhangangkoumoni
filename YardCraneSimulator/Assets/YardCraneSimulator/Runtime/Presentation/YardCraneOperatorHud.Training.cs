using System;
using PortTraining.YardCraneSimulator.Bootstrap;

namespace PortTraining.YardCraneSimulator.Presentation
{
    public sealed partial class YardCraneOperatorHud
    {
        private const string DefaultLocalResultTrust =
            "本地训练过程判定 · 未经 NodeService / 教师平台确认 · 非正式成绩";

        private YardCraneHudTrainingTaskPresentation trainingTaskPresentation;
        private YardCraneHudLocalResultPresentation localResultPresentation;

        public string SessionModeLabel => sessionModeLabel;
        public YardCraneHudTrainingTaskPresentation TrainingTaskPresentation => trainingTaskPresentation;
        public YardCraneHudLocalResultPresentation LocalResultPresentation => localResultPresentation;

        /// <summary>
        /// Binds the frozen local mode and the persistent task card. It reuses this HUD's
        /// existing labels and never creates another UIDocument or visual tree.
        /// </summary>
        public void BindLocalTrainingPresentation(
            string modeLabel,
            YardCraneHudTrainingTaskPresentation presentation)
        {
            sessionModeLabel = DisplayText(modeLabel, 20, "本地训练");
            trainingTaskPresentation = presentation ??
                throw new ArgumentNullException(nameof(presentation));
            RenderTrainingTaskPresentation();
            RenderStatus();
        }

        public void UpdateLocalTrainingTask(YardCraneHudTrainingTaskPresentation presentation)
        {
            trainingTaskPresentation = presentation ??
                throw new ArgumentNullException(nameof(presentation));
            RenderTrainingTaskPresentation();
        }

        public void BindLocalResult(YardCraneHudLocalResultPresentation presentation)
        {
            localResultPresentation = presentation ??
                throw new ArgumentNullException(nameof(presentation));
            RenderLifecycle();
        }

        public void ClearLocalResult()
        {
            localResultPresentation = null;
            RenderLifecycle();
        }

        private void RenderTrainingTaskPresentation()
        {
            if (root == null || trainingTaskPresentation == null) return;
            Set("task-title", new HudReadout(DisplayText(trainingTaskPresentation.Title, 32, "本地训练")));
            Set("target-container-label", new HudReadout(
                DisplayText(trainingTaskPresentation.PrimaryLabel, 16, "当前目标：")));
            Set("task-container", new HudReadout(
                DisplayText(trainingTaskPresentation.PrimaryValue, 34, "—")));
            Set("target-slot-label", new HudReadout(
                DisplayText(trainingTaskPresentation.SecondaryLabel, 16, "训练状态：")));
            Set("task-slot", new HudReadout(
                DisplayText(trainingTaskPresentation.SecondaryValue, 34, "—")));
        }

        private bool TryRenderLocalResult(YardCraneSceneState state)
        {
            if (localResultPresentation == null ||
                (state != YardCraneSceneState.Finalizing &&
                 state != YardCraneSceneState.ExitAllowed &&
                 state != YardCraneSceneState.Terminated &&
                 state != YardCraneSceneState.Failed))
            {
                return false;
            }

            Set("result-title", new HudReadout(
                DisplayText(localResultPresentation.Title, 48, "本地训练结果"),
                localResultPresentation.Tone));
            Set("result-detail", new HudReadout(
                DisplayText(localResultPresentation.Detail, 160, "本次运行已结束。")));
            Set("result-trust", new HudReadout(
                DisplayText(localResultPresentation.TrustText, 120, DefaultLocalResultTrust)));
            return true;
        }

        private static string DisplayText(string value, int maxLength, string fallback)
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            if (normalized.Length <= maxLength) return normalized;
            return normalized.Substring(0, Math.Max(1, maxLength - 1)) + "…";
        }
    }
}
