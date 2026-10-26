# YC-UI-01 场桥驾驶 HUD 复刻实现

## 1. 任务身份

- 任务编号：YC-UI-01
- 任务名称：基于已确认素材包复刻场桥驾驶 HUD
- 执行模式：限定开发
- 当前版本：`YardCraneOperatorHud.Version = 2026-09-07-yc-bootstrap-ui-v2`
- 主控：Codex
- 执行代理：Claude Code

## 2. 背景与已知现象

- 用户可观察现象：现有 `OperatorHud` 仍是旧版左右大卡片布局，与 2026-09-09 确认的驾驶 HUD 设计图不一致。
- 已有运行证据：旧版 HUD 已存在 1920×1080、1920×2160、数据断开/恢复和结果页验证；本任务不能破坏这些已有闭环。
- 当前代码状态：`YardCraneOperatorHud` 通过 `IYardCraneHudSource` 读取只读快照；UI 不直接控制物理与输入；`YardCraneHudSceneAdapter` 提供场景数据。
- 已准备素材：`YardCraneSimulator/Assets/YardCraneSimulator/Art/UI/CraneHud/DesignPackage_v1/`，其中包含参考图、面板 SVG、状态灯亮/灭状态、图标、清单和完整设计说明。
- 工程没有显式安装 `com.unity.vectorgraphics`；不得为本任务安装新包。如果 SVG 不能被现有 UI Toolkit 直接加载，应按照素材几何在 USS 中复刻面板和状态灯，不得修改 `Packages/manifest.json`。

## 3. 本次目标

- 必须实现：
  1. 按 `Reference/crane-hud-ui-reference-v1.png` 和《场桥驾驶HUD复刻设计说明-v1.0.md》重构 `OperatorHud.uxml/.uss`。
  2. 形成左上计时、顶部窄任务栏、五状态灯、左侧六项遥测、右侧任务/姿态/锁销、右上返回/退出按钮的布局。
  3. 保持中间操作视野为空，不加入准星、对位框、X/Y偏差、风速、载荷、摆幅卡片。
  4. 五状态灯具有 on/off 视觉状态；单箱/双箱互斥；未知/离线时全部为 off。
  5. 锁销状态独立显示，离线时不得保留上一帧“已锁”。
  6. 保留现有只读快照、10 Hz 数值刷新、0.5 s stale 清空、生命周期结果页以及操作指引功能；操作指引可收纳，但不得覆盖安全状态。
  7. 返回/退出按钮只发出上层导航请求；若当前工程没有对应导航接口，保留无副作用的事件/委托入口，不得直接终止进程或加载场景。
  8. 更新现有 HUD 验证脚本，使其检查新增元素、中央视野和离线状态灯清空逻辑。
- 明确不做：
  - 不修改场桥物理、绳索、输入、PLC/S7、PortGatewayService、场景文件或 Build Settings。
  - 不伪造任务号、箱号、地址、姿态数值。现有快照没有字段时显示 `—`/“等待任务”，并在交付中列出缺口。
  - 不安装包，不启动 Unity，不提交 Git。
- 完成判据：
  - UXML 所有运行时查询 ID 与 C# 一致，无明显空引用路径。
  - 所有状态灯和锁销均有明确 on/off/unknown 映射。
  - 旧版 feed/lifecycle/stale 行为没有被删除。
  - 变更仅落在允许修改文件内。

## 4. 必读资料

- `CLAUDE.md`
- `AGENTS.md`
- `YardCraneSimulator/AGENTS.md`
- `PortTraining/LM-Wiki/03-规则层/任务验证与文档交付规范.md`
- `PortTraining/Docs/讨论记录/2026-09-09.md`
- `PortTraining/LM-Wiki/02-知识层-编译层/场桥UI-Toolkit数据接入与界面基线-v1.0.md`
- `YardCraneSimulator/Assets/YardCraneSimulator/Art/UI/CraneHud/DesignPackage_v1/README.md`
- `YardCraneSimulator/Assets/YardCraneSimulator/Art/UI/CraneHud/DesignPackage_v1/manifest.json`
- `YardCraneSimulator/Assets/YardCraneSimulator/Art/UI/CraneHud/DesignPackage_v1/Docs/场桥驾驶HUD复刻设计说明-v1.0.md`

## 5. 文件边界

- 允许修改文件：
  - `YardCraneSimulator/Assets/YardCraneSimulator/Resources/YardCraneUI/OperatorHud.uxml`
  - `YardCraneSimulator/Assets/YardCraneSimulator/Resources/YardCraneUI/OperatorHudStyles.uss`
  - `YardCraneSimulator/Assets/YardCraneSimulator/Runtime/Presentation/YardCraneOperatorHud.cs`
  - `YardCraneSimulator/Assets/YardCraneSimulator/Runtime/Presentation/YardCraneHudState.cs`
  - `YardCraneSimulator/Assets/YardCraneSimulator/Runtime/Presentation/YardCraneHudText.cs`
  - `YardCraneSimulator/Assets/YardCraneSimulator/Experiments/ObiRope/YardCraneHudSceneAdapter.cs`
  - `YardCraneSimulator/Assets/YardCraneSimulator/Editor/YardCraneHudVerification.cs`
- 只读参考文件：
  - `YardCraneSimulator/Assets/YardCraneSimulator/Art/UI/CraneHud/DesignPackage_v1/**`
  - `YardCraneSimulator/Assets/YardCraneSimulator/Resources/YardCraneUI/OperatorHudTheme.tss`
  - `YardCraneSimulator/Assets/YardCraneSimulator/Scenes/Bootstrap.unity`
- 禁止修改：未在“允许修改文件”中列出的所有文件。

## 6. 约束与安全规则

- 不启动 Unity，不操作 GUI，不安装依赖，不提交 Git。
- 不修改 PLC/S7、网关、输入和机构控制接口。
- UI 只能读取 `IYardCraneHudSource`，不得反向发送机构命令。
- 不将静态推断描述成运行验证。
- 不使用 UI 文案掩盖数据缺失；无来源字段必须明确显示未知态。
- 发现范围外问题时只报告，不顺手修改。

## 7. 验收要求

- 静态验收：核对 UXML 查询 ID、状态映射、空引用保护、stale 清空、生命周期和操作指引保留情况。
- 自动化验收：由主控运行 Unity 编辑器验证脚本。
- Unity运行验收：由主控在 Unity 6000.0.42f1 下验证 1920×1080 与 1920×2160、正常数据、数据断开/恢复、状态灯切换和结果页。
- 相邻回归：驾驶输入焦点不得被 HUD 按钮抢占；中央视野不得被遮挡；旧版 IMGUI 与 UI Toolkit 不得叠加。

## 8. Claude交付格式

1. 结论。
2. 实现说明。
3. 文件和精确行号。
4. 实际修改内容。
5. 实际执行的验证。
6. 未验证范围和风险。
7. 建议主控执行的下一步。
