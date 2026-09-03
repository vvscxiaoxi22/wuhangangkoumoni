using UnityEngine;
using UnityEngine.UIElements;

namespace PortTraining.UI.Runtime
{
    [UxmlElement]
    public partial class TechBackgroundElement : VisualElement
    {
        private float phase;

        public TechBackgroundElement()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += DrawBackground;
            schedule.Execute(() =>
            {
                phase = (phase + 0.012f) % 1f;
                MarkDirtyRepaint();
            }).Every(50);
        }

        private void DrawBackground(MeshGenerationContext context)
        {
            var bounds = contentRect;
            if (bounds.width <= 1f || bounds.height <= 1f)
            {
                return;
            }

            var painter = context.painter2D;
            DrawPerspectiveGrid(painter, bounds);
            DrawQuayCraneOutline(painter, bounds);
            DrawSignalPaths(painter, bounds);
        }

        private void DrawPerspectiveGrid(Painter2D painter, Rect bounds)
        {
            var horizon = bounds.height * 0.56f;
            var center = bounds.width * 0.5f;

            painter.lineWidth = 1f;
            painter.strokeColor = new Color(0.25f, 0.78f, 1f, 0.10f);
            for (var index = 0; index <= 18; index++)
            {
                var bottomX = bounds.width * index / 18f;
                var horizonX = center + (bottomX - center) * 0.18f;
                StrokeLine(painter, new Vector2(bottomX, bounds.height), new Vector2(horizonX, horizon));
            }

            for (var index = 0; index < 12; index++)
            {
                var progress = (index + phase) / 12f;
                var eased = progress * progress;
                var y = Mathf.Lerp(horizon, bounds.height, eased);
                var alpha = Mathf.Lerp(0.05f, 0.16f, progress);
                painter.strokeColor = new Color(0.25f, 0.78f, 1f, alpha);
                StrokeLine(painter, new Vector2(0f, y), new Vector2(bounds.width, y));
            }

            painter.strokeColor = new Color(0.28f, 0.72f, 0.94f, 0.055f);
            for (var y = 52f; y < horizon - 20f; y += 52f)
            {
                StrokeLine(painter, new Vector2(0f, y), new Vector2(bounds.width, y));
            }
        }

        private static void DrawQuayCraneOutline(Painter2D painter, Rect bounds)
        {
            var scale = Mathf.Min(bounds.width / 1920f, bounds.height / 1080f);
            var origin = new Vector2(bounds.width * 0.09f, bounds.height * 0.18f);
            var width = 540f * scale;
            var height = 410f * scale;

            painter.lineWidth = Mathf.Max(1f, 1.4f * scale);
            painter.strokeColor = new Color(0.32f, 0.83f, 1f, 0.14f);

            StrokeLine(painter, origin + new Vector2(0f, height), origin + new Vector2(width * 0.16f, 0f));
            StrokeLine(painter, origin + new Vector2(width * 0.58f, height), origin + new Vector2(width * 0.42f, 0f));
            StrokeLine(painter, origin + new Vector2(width * 0.16f, 0f), origin + new Vector2(width * 0.82f, height * 0.08f));
            StrokeLine(painter, origin + new Vector2(width * 0.42f, 0f), origin + new Vector2(width * 0.82f, height * 0.08f));
            StrokeLine(painter, origin + new Vector2(width * 0.16f, 0f), origin + new Vector2(width * 0.42f, height));
            StrokeLine(painter, origin + new Vector2(width * 0.42f, 0f), origin + new Vector2(0f, height));

            var trolleyY = origin.y + height * 0.12f;
            var trolleyX = origin.x + width * 0.48f;
            StrokeLine(painter, new Vector2(trolleyX, trolleyY), new Vector2(trolleyX, trolleyY + height * 0.46f));
            StrokeLine(painter, new Vector2(trolleyX + 42f * scale, trolleyY), new Vector2(trolleyX + 42f * scale, trolleyY + height * 0.46f));
            StrokeLine(painter, new Vector2(trolleyX - 22f * scale, trolleyY + height * 0.46f), new Vector2(trolleyX + 64f * scale, trolleyY + height * 0.46f));
        }

        private void DrawSignalPaths(Painter2D painter, Rect bounds)
        {
            var pulse = 0.11f + Mathf.Sin(phase * Mathf.PI * 2f) * 0.025f;
            painter.lineWidth = 1.5f;
            painter.strokeColor = new Color(0.34f, 0.86f, 1f, pulse);

            var right = bounds.width * 0.78f;
            var top = bounds.height * 0.14f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(right, top));
            painter.LineTo(new Vector2(right + bounds.width * 0.08f, top));
            painter.LineTo(new Vector2(right + bounds.width * 0.11f, top + bounds.height * 0.055f));
            painter.LineTo(new Vector2(bounds.width, top + bounds.height * 0.055f));
            painter.Stroke();

            painter.strokeColor = new Color(0.34f, 0.86f, 1f, 0.075f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(bounds.width, bounds.height * 0.36f));
            painter.LineTo(new Vector2(bounds.width * 0.88f, bounds.height * 0.36f));
            painter.LineTo(new Vector2(bounds.width * 0.84f, bounds.height * 0.42f));
            painter.LineTo(new Vector2(bounds.width * 0.73f, bounds.height * 0.42f));
            painter.Stroke();
        }

        private static void StrokeLine(Painter2D painter, Vector2 start, Vector2 end)
        {
            painter.BeginPath();
            painter.MoveTo(start);
            painter.LineTo(end);
            painter.Stroke();
        }
    }
}

