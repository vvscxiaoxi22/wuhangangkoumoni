using System;
using PortTraining.YardCraneSimulator.Presentation;
using UnityEditor;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Editor
{
    [CustomEditor(typeof(YardCraneOperatorHud))]
    public sealed class YardCraneOperatorHudEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("独立提示框预览", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "以下按钮只在 Play 模式提供明确标记的预览数据，不会写入任务、成绩或学生档案。" +
                "正式流程请由任务/结果服务调用 HUD 的 Show…Prompt 接口。",
                MessageType.Info);

            var hud = (YardCraneOperatorHud)target;
            using (new EditorGUI.DisabledScope(!Application.isPlaying || !hud.IsReady))
            {
                Row(
                    "任务提示", () => hud.ShowTaskPrompt(new YardCraneHudTaskPrompt
                    {
                        Title = "任务提示 · 预览",
                        Instruction = "请将预览集装箱放置到指定位置",
                        SourceBay = "53",
                        DestinationBay = "55",
                        TargetContainer = "PREVIEW-BOX",
                        DestinationSlot = "PREVIEW-SLOT",
                        SourceCellIndex = 56,
                        DestinationCellIndex = 61
                    }),
                    "倒计时", () => hud.ShowCountdownPrompt(
                        new YardCraneHudCountdownPrompt { Title = "倒计时 · 预览", TotalSeconds = 5 },
                        () => PreviewAction(hud, "countdown-elapsed")));

                Row(
                    "提示信息", () => hud.ShowConfirmationPrompt(
                        new YardCraneHudConfirmationPrompt
                        {
                            Title = "提示信息 · 预览",
                            Message = "操作已完成，是否继续下一个预览任务？"
                        },
                        () => PreviewAction(hud, "confirmation-confirmed"),
                        () => PreviewAction(hud, "confirmation-cancelled")),
                    "错误提示", () => hud.ShowErrorPrompt(
                        new YardCraneHudErrorPrompt
                        {
                            Title = "错误提示 · 预览",
                            Message = "预览放箱位置不正确，请检查后重新操作。"
                        },
                        () => PreviewAction(hud, "error-acknowledged")));

                Row(
                    "任务完成", () => hud.ShowCompletionPrompt(
                        new YardCraneHudCompletionPrompt
                        {
                            Title = "任务完成 · 预览",
                            ElapsedSeconds = 1800,
                            Score = 100
                        },
                        () => PreviewAction(hud, "details-requested"),
                        () => PreviewAction(hud, "retry-requested")),
                    "考核详情", () => hud.ShowAssessmentPrompt(new YardCraneHudAssessmentPrompt
                    {
                        Title = "考核详情 · 预览",
                        Project = "单项操作 · 大车移动（预览）",
                        ElapsedSeconds = 780,
                        Score = 92,
                        Lines = new[]
                        {
                            new YardCraneHudAssessmentLine("大车启动速度过快", 5, "预览扣分说明"),
                            new YardCraneHudAssessmentLine("超出停顿次数", 3, "预览扣分说明")
                        }
                    }));

                Row(
                    "成绩提交", () => hud.ShowScoreSubmissionPrompt(
                        new YardCraneHudScoreSubmissionPrompt
                        {
                            Title = "成绩提交 · 预览",
                            DisplayName = "预览学员",
                            StudentNumber = "PREVIEW-001",
                            Major = "预览专业",
                            ClassName = "预览班级"
                        },
                        _ => PreviewAction(hud, "score-submit-requested")),
                    "关闭当前提示", hud.ClosePrompt);
            }

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("进入 Play 后可逐个触发七类提示框。", MessageType.None);
        }

        private static void Row(string leftLabel, Action left, string rightLabel, Action right)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(leftLabel, GUILayout.Height(28))) left();
            if (GUILayout.Button(rightLabel, GUILayout.Height(28))) right();
            EditorGUILayout.EndHorizontal();
        }

        private static void PreviewAction(YardCraneOperatorHud hud, string action)
        {
            Debug.Log($"[YC_UI_PROMPT_PREVIEW_ACTION] action={action}", hud);
        }
    }
}
