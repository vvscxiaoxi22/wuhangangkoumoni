# 给 Claude 的学生首页修改指令

请修改 `StudentHome` 场景 Display1 的学生端首页。

先阅读：

- `YardCraneSimulator/Assets/YardCraneSimulator/Art/UI/StudentHome/DesignPackage_v1/README.md`
- `YardCraneSimulator/Assets/YardCraneSimulator/Art/UI/StudentHome/DesignPackage_v1/Docs/学生端首页复刻设计说明-v1.0.md`
- 视觉参考：`YardCraneSimulator/Assets/YardCraneSimulator/Art/UI/StudentHome/DesignPackage_v1/Reference/student-home-reference-v1.png`

严格按照参考图复刻，不自行改变布局。使用素材包中的背景、三枚透明模式图标、卡片和箭头视觉，修改现有：

- `StudentSideShell.uxml`
- `StudentSideShellStyles.uss`
- 必要时仅调整 `YardCraneStudentSideShell.cs` 的首页展示逻辑

保留现有按钮 ID 和事件：`learning-mode-button`、`launch-button`、`assessment-mode-button`、`logout-button`、`quit-button`。不要另建平行首页，不修改 Display2、登录、训练中心、Bootstrap、场桥物理、输入、PLC/S7 或 PortGatewayService。

首页必须包含顶部身份导航、模式选择标题、学习训练/自由练习/任务考核三张卡。三张卡初始视觉完全一致，不给自由练习设置默认选中或高亮；悬停反馈对三张卡统一保留。任务考核可点击；业务未接入时继续显示诚实提示，不伪造任务或成绩。移除视觉上的旧侧边栏、统计卡和右下常驻设备状态。

不要启动 Unity，不提交 Git。完成后列出修改文件、保留的事件闭环、静态检查结果和仍需主控在 Unity 中验证的内容。
