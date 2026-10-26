# 场桥驾驶 HUD 浅色毛玻璃素材包 v2

本目录对应用户在 2026-09-09 确认的新驾驶 HUD。完整效果见 `Reference/crane-hud-light-reference-v2.png`。

## 目录

- `Layout/`：计时、任务标题、五灯状态条、左遥测、右任务、右姿态、锁销条、锁销选中胶囊和分隔线。
- `Icons/`：时钟、返回、退出、任务、目标、位置、锁销和信息圆点。
- `StatusLamps/`：五种状态灯的亮/灭两态。
- `Reference/`：新 HUD 完整效果图和无 UI 驾驶背景，仅用于视觉对照。
- `Docs/`：像素级布局、状态绑定和验收要求。

素材均为透明 SVG；运行时文字由 UI Toolkit 绘制。工程没有显式安装 Vector Graphics 包，因此实际界面优先用 USS 复刻面板和圆灯，SVG 作为精确视觉基准，不允许为本界面安装新包。

