using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PortTraining.YardCraneSimulator.Shell
{
    // Shared, stateless plumbing for shell scripts that each own one scene UIDocument.
    // UIDocuments keep using their Inspector-assigned PanelSettings assets directly so the
    // asset's Scale With Screen Size configuration remains the layout source of truth.
    // Runtime PanelSettings clones are forbidden because they hide later asset tuning from
    // the live document and make scaling diverge from the folder asset.
    public static class YardCraneShellPanelUtility
    {
        // Returns a problem description if the document/panel could not be prepared, or
        // null if preparation succeeded - the caller decides how to report it (each shell
        // script keeps its own BuildProblem(string) dedup-and-log method, exactly as
        // before this was extracted, so this stays a pure "what's wrong" check with no
        // logging side effect of its own). readyToBuildContent is only meaningful when the
        // return value is null: the document/panel are valid but the UIDocument's own root
        // may not exist yet (its OnEnable order relative to the caller is not guaranteed) -
        // that is not a problem, just a "retry next tick" signal.
        public static string PrepareDocument(
            UIDocument candidate,
            ref PanelSettings template,
            ref PanelSettings activePanel,
            string label,
            out bool readyToBuildContent)
        {
            readyToBuildContent = false;
            if (candidate == null) return $"{label}: UIDocument component is missing.";
            if (candidate.visualTreeAsset == null) return $"{label}: Source Asset is missing on {candidate.name}.";
            var configured = candidate.panelSettings;
            if (configured != null && (configured.hideFlags & HideFlags.DontSave) == 0)
            {
                template = configured;
            }
            if (template == null) return $"{label}: Panel Settings asset is missing on {candidate.name}.";
            if ((template.hideFlags & HideFlags.DontSave) != 0)
                return $"{label}: Panel Settings is transient. Assign the folder's PanelSettings asset.";
            if (candidate.panelSettings != template) candidate.panelSettings = template;
            activePanel = template;
            readyToBuildContent = candidate.rootVisualElement != null && candidate.isActiveAndEnabled;
            return null;
        }

        public static void ReleasePanel(UIDocument owner, PanelSettings template, ref PanelSettings activePanel)
        {
            // Keep the scene reference on the real asset across disable/domain reload. This
            // method no longer owns or destroys a runtime PanelSettings object.
            if (owner != null && template != null && owner.panelSettings == null)
                owner.panelSettings = template;
            activePanel = null;
        }

        public static void ReleaseOwnedObject(UnityEngine.Object ownedObject)
        {
            if (ownedObject == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(ownedObject);
            else UnityEngine.Object.DestroyImmediate(ownedObject);
        }

        public static void Quit()
        {
            if (!Application.isPlaying) return;
#if UNITY_EDITOR
            Debug.Log("[YC_SHELL_QUIT_IGNORED] Quit is disabled in the Unity Editor.");
#else
            Application.Quit(0);
#endif
        }

        public static Font CreateShellFont()
        {
            var font = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei", "Noto Sans CJK SC", "Arial Unicode MS" }, 24);
            if (font != null) font.hideFlags = HideFlags.HideAndDontSave;
            return font;
        }

        // Display1 (this screen) is a real, independent 1920x1080 output. The only shape
        // variation these shells need to detect on their own (kept separate from the
        // shared Runtime/Presentation display-profile type used by the driving HUD) is a
        // genuinely portrait single-screen deployment.
        public static void DetectDisplayLayout(int width, int height, out bool isPortrait, out float scale)
        {
            if (width <= 0 || height <= 0)
            {
                isPortrait = false;
                scale = 1f;
                return;
            }

            int referenceWidth;
            int referenceHeight;
            if (height > width)
            {
                isPortrait = true;
                referenceWidth = 1080;
                referenceHeight = 1920;
            }
            else
            {
                isPortrait = false;
                referenceWidth = 1920;
                referenceHeight = 1080;
            }

            scale = Math.Max(0.1f, Math.Min(width / (float)referenceWidth, height / (float)referenceHeight));
        }
    }
}
