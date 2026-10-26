# 学生端首页素材包 v1

本目录用于复刻学生端 `StudentHome` 场景 Display1 的“模式选择”首页。完整视觉目标见 `Reference/student-home-reference-v1.png`，实现契约见 `Docs/学生端首页复刻设计说明-v1.0.md`。

## 内容

- `Background/student-home-background-v1.png`：不含 UI 的港口背景图。
- `ModeIcons/`：学习训练、自由练习、任务考核三枚透明 PNG 主图标。
- `Layout/`：顶部导航、品牌胶囊、普通/选中模式卡、背景遮罩与装饰线 SVG。
- `Icons/`：普通/选中箭头按钮、返回与退出 SVG。
- `Reference/`：用户确认的完整视觉参考图，仅用于对照。
- `manifest.json`：素材清单和设计分辨率。

## 使用原则

1. 运行时文字必须由 UXML/USS 绘制，不能把参考图中文字裁切成图片。
2. 三张模式卡位置、尺寸和视觉状态严格按设计文档实现，不新增侧边栏、统计卡或信息面板。
3. 主图标使用透明 PNG；面板、描边、阴影优先使用 USS，SVG 作为尺寸和视觉基准。
4. `StudentHome` Display1 设计分辨率为 1920×1080；1920×2160 是上下两块显示的总物理环境，不能把本页拉伸为 1920×2160。

