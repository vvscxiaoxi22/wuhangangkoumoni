using PortTraining.YardCraneSimulator.Shell;
using UnityEditor;
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
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("界面预览", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "无需播放即可预览界面，请在 Game 窗口查看。预览只切换页面，不执行登录或创建训练会话。",
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

                using (new EditorGUILayout.HorizontalScope())
                {
                    PreviewButton(shell, "学生首页", YardCraneShellRole.Student, YardCraneStudentShellPage.Home);
                    PreviewButton(shell, "教师首页", YardCraneShellRole.Teacher, YardCraneStudentShellPage.Home);
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
                   scene.name == YardCraneStudentShell.SceneName && shell.gameObject.scene == scene;
        }

        private static void RefreshPreview()
        {
            if (IsEditorBusy()) return;
            var now = EditorApplication.timeSinceStartup;
            if (now < nextRefreshAt) return;
            nextRefreshAt = now + RefreshIntervalSeconds;

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.name != YardCraneStudentShell.SceneName)
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
