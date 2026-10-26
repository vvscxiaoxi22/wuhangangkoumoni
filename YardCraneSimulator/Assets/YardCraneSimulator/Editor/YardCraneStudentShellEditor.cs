using PortTraining.YardCraneSimulator.Shell;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace PortTraining.YardCraneSimulator.Editor
{
    [InitializeOnLoad]
    [CustomEditor(typeof(YardCraneStudentShell))]
    public sealed class YardCraneStudentShellEditor : UnityEditor.Editor
    {
        private const double RefreshIntervalSeconds = 0.25;
        private static double nextRefreshAt;
        private static YardCraneStudentShell lastShell;
        private static UIDocument lastDocument;
        private static Rect lastBounds;
        private static float lastScale;
        private static bool lastReady;
        private static bool lastDualStacked;
        private static bool lastPortrait;
        private static YardCraneShellRole lastRole;
        private static YardCraneStudentShellPage lastPage;

        static YardCraneStudentShellEditor()
        {
            EditorApplication.update += RefreshPreview;
            AssemblyReloadEvents.beforeAssemblyReload += SuspendAll;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            EditorApplication.quitting += SuspendAll;
        }

        private static YardCraneStudentShell[] LoadedShells() =>
            Object.FindObjectsByType<YardCraneStudentShell>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        // YardCraneStudentSideShell (split out 2026-09-08) owns its own runtime-cloned
        // PanelSettings exactly the same way this shell does, so it needs the same
        // suspend-before-reload/resume-after-save protection. Extended here rather than
        // giving it a second [InitializeOnLoad] static constructor, to avoid double-
        // registering the same EditorApplication/AssemblyReloadEvents/EditorSceneManager
        // hooks from two places.
        private static YardCraneStudentSideShell[] LoadedSideShells() =>
            Object.FindObjectsByType<YardCraneStudentSideShell>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        private static YardCraneTeacherSideShell[] LoadedTeacherSideShells() =>
            Object.FindObjectsByType<YardCraneTeacherSideShell>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        private static void SuspendAll()
        {
            foreach (var shell in LoadedShells()) shell.EditorSuspendInterface();
            foreach (var shell in LoadedSideShells()) shell.EditorSuspendInterface();
            foreach (var shell in LoadedTeacherSideShells()) shell.EditorSuspendInterface();
        }

        private static void ResumeAll()
        {
            foreach (var shell in LoadedShells()) shell.EditorResumeInterface();
            foreach (var shell in LoadedSideShells()) shell.EditorResumeInterface();
            foreach (var shell in LoadedTeacherSideShells()) shell.EditorResumeInterface();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
                SuspendAll();
            else if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode)
                ResumeAll();
        }

        private static void OnSceneSaving(Scene scene, string path)
        {
            foreach (var shell in LoadedShells())
                if (shell.gameObject.scene == scene) shell.EditorSuspendInterface();
            foreach (var shell in LoadedSideShells())
                if (shell.gameObject.scene == scene) shell.EditorSuspendInterface();
            foreach (var shell in LoadedTeacherSideShells())
                if (shell.gameObject.scene == scene) shell.EditorSuspendInterface();
        }

        private static void OnSceneSaved(Scene scene)
        {
            // Wait until serialization has finished before putting transient panels back.
            EditorApplication.delayCall += () =>
            {
                foreach (var shell in LoadedShells())
                    if (shell.gameObject.scene == scene) shell.EditorResumeInterface();
                foreach (var shell in LoadedSideShells())
                    if (shell.gameObject.scene == scene) shell.EditorResumeInterface();
                foreach (var shell in LoadedTeacherSideShells())
                    if (shell.gameObject.scene == scene) shell.EditorResumeInterface();
                EditorApplication.QueuePlayerLoopUpdate();
            };
        }

        [MenuItem("Tools/Yard Crane/Repair StudentEntry UI References")]
        public static void RepairSceneReferences()
        {
            if (IsEditorBusy())
            {
                Debug.LogWarning("Stop Play and wait for compilation before repairing StudentEntry references.");
                return;
            }
            const string resourcesPath = "Assets/YardCraneSimulator/Shell/Resources/YardCraneShell/";
            var upperTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(resourcesPath + "Display1UI/StudentShellUpper.uxml");
            var lowerTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(resourcesPath + "Display2UI/StudentShellLower.uxml");
            var upperPanel = AssetDatabase.LoadAssetAtPath<PanelSettings>(resourcesPath + "Display1UI/StudentShellUpperPanel.asset");
            var lowerPanel = AssetDatabase.LoadAssetAtPath<PanelSettings>(resourcesPath + "Display2UI/StudentShellLowerPanel.asset");
            if (upperTree == null || lowerTree == null || upperPanel == null || lowerPanel == null)
            {
                Debug.LogError("StudentEntry UI source assets are missing; repair aborted.");
                return;
            }
            var activeScene = SceneManager.GetActiveScene();
            foreach (var shell in LoadedShells())
            {
                if (shell.gameObject.scene != activeScene) continue;
                var upper = shell.GetComponent<UIDocument>();
                if (upper == null || upper.visualTreeAsset != upperTree) continue;
                UIDocument lower = null;
                var matches = 0;
                foreach (var candidate in Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (candidate.gameObject.scene != shell.gameObject.scene || candidate == upper ||
                        candidate.visualTreeAsset != lowerTree || candidate.transform.IsChildOf(upper.transform) ||
                        upper.transform.IsChildOf(candidate.transform)) continue;
                    lower = candidate;
                    matches++;
                }
                if (matches != 1)
                {
                    Debug.LogError("Expected exactly one independent lower UIDocument with StudentShellLower.uxml; repair aborted.", shell);
                    continue;
                }
                shell.EditorSuspendInterface();
                Undo.RecordObjects(new Object[] { shell, upper, lower }, "Repair StudentEntry UI references");
                upper.panelSettings = upperPanel;
                lower.panelSettings = lowerPanel;
                upper.enabled = lower.enabled = true;
                var serialized = new SerializedObject(shell);
                serialized.FindProperty("upperPanelTemplate").objectReferenceValue = upperPanel;
                serialized.FindProperty("lowerPanelTemplate").objectReferenceValue = lowerPanel;
                serialized.FindProperty("lowerDocument").objectReferenceValue = lower;
                serialized.ApplyModifiedProperties();
                shell.EditorResumeInterface();
                EditorSceneManager.MarkSceneDirty(shell.gameObject.scene);
                Debug.Log("[YC_STUDENT_SHELL_REFERENCES_REPAIRED] Upper/Lower asset references restored. Save the scene; enable the shell if it was disabled for diagnosis.", shell);
            }
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("界面预览", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "无需播放即可预览界面，请在 Game 窗口查看。预览只切换页面，不执行登录或创建训练会话。" +
                "学生和教师登录成功后分别切换到独立的 StudentHome、TeacherHome 场景。",
                MessageType.Info);

            var shell = (YardCraneStudentShell)target;
            var canPreview = !IsEditorBusy() && IsCurrentEntryShell(shell);
            using (new EditorGUI.DisabledScope(!canPreview))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    PreviewButton(shell, "学生登录", YardCraneShellRole.Student, YardCraneStudentShellPage.Login);
                    PreviewButton(shell, "教师登录", YardCraneShellRole.Teacher, YardCraneStudentShellPage.Login);
                }
            }

            if (!IsCurrentEntryShell(shell))
                EditorGUILayout.HelpBox("请在当前 StudentEntry 场景中启用入口组件后预览。", MessageType.None);
        }

        private static void PreviewButton(
            YardCraneStudentShell shell,
            string label,
            YardCraneShellRole role,
            YardCraneStudentShellPage page)
        {
            if (!GUILayout.Button(label, GUILayout.Height(28))) return;
            shell.EditorPreview(role, page);
            shell.EditorRefreshPreviewLayout();
            EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private static bool IsEditorBusy()
        {
            return EditorApplication.isPlayingOrWillChangePlaymode ||
                   EditorApplication.isCompiling || EditorApplication.isUpdating;
        }

        private static bool IsCurrentEntryShell(YardCraneStudentShell shell)
        {
            var scene = SceneManager.GetActiveScene();
            return shell != null && shell.isActiveAndEnabled && scene.IsValid() && scene.isLoaded &&
                   shell.gameObject.scene == scene;
        }

        private static void RefreshPreview()
        {
            if (IsEditorBusy()) return;
            var now = EditorApplication.timeSinceStartup;
            if (now < nextRefreshAt) return;
            nextRefreshAt = now + RefreshIntervalSeconds;

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                lastShell = null;
                lastDocument = null;
                return;
            }

            YardCraneStudentShell shell = null;
            foreach (var candidate in Object.FindObjectsByType<YardCraneStudentShell>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!IsCurrentEntryShell(candidate)) continue;
                shell = candidate;
                break;
            }

            if (shell == null)
            {
                lastShell = null;
                lastDocument = null;
                return;
            }

            shell.EditorRefreshPreviewLayout();
            var document = shell.Document;
            var shellRoot = shell.VisualRoot?.Q("student-shell");
            var bounds = shellRoot != null ? shellRoot.worldBound : Rect.zero;
            var scale = document != null && document.panelSettings != null ? document.panelSettings.scale : 0f;
            var dualStacked = shellRoot != null && shellRoot.ClassListContains("dual-stacked");
            var portrait = shellRoot != null && shellRoot.ClassListContains("portrait");
            var changed = shell != lastShell || document != lastDocument || bounds != lastBounds ||
                          !Mathf.Approximately(scale, lastScale) || shell.IsReady != lastReady ||
                          dualStacked != lastDualStacked || portrait != lastPortrait ||
                          shell.Role != lastRole || shell.Page != lastPage;

            lastShell = shell;
            lastDocument = document;
            lastBounds = bounds;
            lastScale = scale;
            lastReady = shell.IsReady;
            lastDualStacked = dualStacked;
            lastPortrait = portrait;
            lastRole = shell.Role;
            lastPage = shell.Page;

            if (!changed) return;
            EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
    }
}
