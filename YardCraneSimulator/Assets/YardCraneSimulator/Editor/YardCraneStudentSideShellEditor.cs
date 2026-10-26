using PortTraining.YardCraneSimulator.Shell;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PortTraining.YardCraneSimulator.Editor
{
    // Inspector preview for the post-login student screen. The suspend/resume-around-
    // domain-reload protection this document's runtime-cloned PanelSettings needs is
    // already registered once by YardCraneStudentShellEditor's static hooks (extended to
    // cover this type too) - this class only adds the "学生首页" preview button, it does
    // not register its own EditorApplication/AssemblyReloadEvents hooks.
    [CustomEditor(typeof(YardCraneStudentSideShell))]
    public sealed class YardCraneStudentSideShellEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("界面预览", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "无需播放即可预览界面，请在 Game 窗口查看。预览以'预览学员'身份显示模式选择页，" +
                "可以切换学习训练页和查看操作提示，不创建训练会话。",
                MessageType.Info);

            var shell = (YardCraneStudentSideShell)target;
            var canPreview = !IsEditorBusy() && IsCurrentEntryShell(shell);
            using (new EditorGUI.DisabledScope(!canPreview))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("学生首页", GUILayout.Height(28))) Preview(shell);
                    if (GUILayout.Button("学习训练", GUILayout.Height(28))) PreviewLearning(shell);
                    if (GUILayout.Button("隐藏预览", GUILayout.Height(28))) HidePreview(shell);
                }
            }

            if (!IsCurrentEntryShell(shell))
                EditorGUILayout.HelpBox("请在当前 StudentHome 场景中启用该组件后预览。", MessageType.None);
        }

        private static void Preview(YardCraneStudentSideShell shell)
        {
            shell.EditorPreviewHome();
            shell.EditorRefreshPreviewLayout();
            EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private static void HidePreview(YardCraneStudentSideShell shell)
        {
            shell.EditorHidePreview();
            EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private static void PreviewLearning(YardCraneStudentSideShell shell)
        {
            shell.EditorPreviewLearning();
            shell.EditorRefreshPreviewLayout();
            EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private static bool IsEditorBusy()
        {
            return EditorApplication.isPlayingOrWillChangePlaymode ||
                   EditorApplication.isCompiling || EditorApplication.isUpdating;
        }

        private static bool IsCurrentEntryShell(YardCraneStudentSideShell shell)
        {
            var scene = SceneManager.GetActiveScene();
            return shell != null && shell.isActiveAndEnabled && scene.IsValid() && scene.isLoaded &&
                   scene.name == YardCraneStudentSideShell.SceneName && shell.gameObject.scene == scene;
        }
    }
}
