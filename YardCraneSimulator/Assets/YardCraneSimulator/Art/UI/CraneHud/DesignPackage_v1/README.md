# 场桥驾驶 HUD 素材包 v1

本目录是基于 2026-09-09 驾驶界面设计稿拆分出的 UI 素材与复刻说明，供 Claude 在 Unity UI Toolkit 中实现 `Bootstrap`/驾驶显示使用。

## 目录

- `Layout/`：透明玻璃面板、顶部任务栏、计时框、分隔线。
- `StatusLamps/`：制动、锚定、开锁、单箱、双箱五组状态灯的亮/灭 SVG。
- `Icons/`：返回、退出、锁销已锁/未锁、信息圆点。
- `Reference/`：完整 UI 设计稿和驾驶视角参考图，仅用于 Claude 对照，不作为运行时 HUD 背景。
- `Docs/`：页面设计与实现契约。

## 设计基准

- 画布：1920 × 1080（16:9），所有尺寸以该画布为基准。
- 适用显示：驾驶 HUD/Bootstrap Display2；不是 StudentHome、StudentEntry 或教师端页面。
- 缩放：UI Toolkit 根节点按 `min(width / 1920, height / 1080)` 等比缩放，面板锚点保持屏幕边缘安全区 24 px。
- 素材格式：SVG，透明背景、无文字、无业务状态；文字和数字由运行时代码使用 FontAsset 绘制。
- 禁止：中心准星、吊具对位框、X/Y 偏差框、风速/载荷/摆幅卡片、覆盖中间吊具视野的大型卡片。

## 使用顺序

1. 先阅读 `Docs/场桥驾驶HUD复刻设计说明-v1.0.md`。
2. 将 SVG 按文件名作为 UI Toolkit `VectorImage` 或导出为 PNG 后作为 `BackgroundImage`。
3. 业务数据只进入文档中定义的 `YardCraneUiStateSnapshot` 字段，素材本身不存储状态。
4. 灯和锁销必须通过状态字段切换亮/灭素材，不要修改 SVG 颜色。
