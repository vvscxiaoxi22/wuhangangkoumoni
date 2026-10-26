# 场桥 HUD 状态层回归

在场桥工程根目录运行：

```powershell
dotnet run --project Tools/HudPresentationHarness/HudPresentationHarness.csproj -c Release
```

该工具直接编译生产 `YardCraneHudState.cs` 和 `YardCraneHudText.cs`，复用原状态机工程的领域类型，不使用 Unity 替身测试渲染。

2026-09-04 实际结果：`[YC_UI_HARNESS_PASS] assertions=22, unityPlay=false`。

范围：首次未知、状态变化立即通知、数值 10Hz、旧样本/未来样本拒绝、断流/禁用/恢复、无效数值、解锁优先级、急停优先级、锚定转换和方向性限制。

不能替代 Unity 编译、中文字体、鼠标命中、真实控制输入和横竖屏验证。后者使用 Unity 菜单 `Tools/Yard Crane/Tests/Verify UI Toolkit HUD`；输出位于 `Logs/YC_UiVerification.result.txt` 与 `Logs/UiVerification/`。
