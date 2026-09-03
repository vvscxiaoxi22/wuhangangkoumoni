# YC-P0-03-CABIN-01-R1 主控审查修正

## 1. 任务身份

- 任务编号：`YC-P0-03-CABIN-01-R1`
- 任务名称：修正安全离散键被另一输入源抢占及1920×1080两倍HUD回退
- 执行模式：限定开发 / 继续会话
- 主控：Codex
- 执行代理：Claude Code
- 前序会话：`40a94e4f-0911-4129-9a11-f067b49da262`

## 2. 主控实际验证

- Harness已实际运行并PASS，包含`gantryBrake=True containerContactInterlock=True cabinSafetyChain=True`。
- Unity `6000.0.42f1`同版本Bee/Roslyn编译`YardCraneSimulator.Runtime`和`YardCraneSimulator.ObiRopeTest`均退出码`0`。
- 以下两项来自主控静态审查，尚未修正：
  1. 当键盘已经是活动输入源、键盘手柄轴回中并按F8/F9/R之一，而Xbox摇杆同时有运动意图时，`YardCraneInputRouter`只检查`active.HandlesCentered`便允许切换到Xbox，导致活动键盘本帧的安全离散意图丢失。这是安全控制输入丢帧风险。
  2. 新面板高度`220 + 12 + 276`使1920×1080横屏无法满足现有精确2倍布局高度，`CalculateRuntimeUiScale`会回退到约1.56倍，违反此前已经确认的“横屏空间足够时整体2倍”视觉基线。现有11行操作指引和7行状态可使用更紧凑但不裁切的逻辑高度。

## 3. 本次目标

1. 修正输入源切换：只有当前活动源`HandlesCentered`且`HasIntent == false`时，其他输入源才能取得权限。当前活动源只要有任一运动、鸣笛、业务离散或安全离散意图，本帧都必须保持权限并原样输出。
2. Harness增加确定性回归：先让键盘成为活动源；随后同帧输入“键盘F9或等价急停离散意图 + Xbox非零运动轴”，断言输出仍为键盘且急停意图未丢失、Xbox运动未混入；下一帧键盘完全无意图且回中后才允许切到Xbox。
3. 在不缩小现有字体、不删除状态或指引、不改变两倍缩放算法的前提下，压缩两个面板的逻辑高度常量，使1920×1080横屏满足`DesiredRuntimeUiScale=2`的现有并排判断。目标总逻辑高度必须满足：`MainPanelY + KeyStatusPanelHeight + OperationPanelInnerGap + OperationGuidePanelHeight + KeyStatusPanelGap <= 540`。
4. 逻辑高度仍须为每个GUILayout标签保留合理空间：关键状态卡含标题+7行；操作指引卡含标题+11行。不得用会明显裁字的极端高度。

## 4. 文件边界

- 允许修改文件：
  - `YardCraneSimulator/Assets/YardCraneSimulator/Runtime/Input/YardCraneInputRouter.cs`
  - `YardCraneSimulator/Tools/OperationStateMachineHarness/Program.cs`
  - `YardCraneSimulator/Assets/YardCraneSimulator/Experiments/ObiRope/YardCraneEightRopeMaxPrototypeRig.cs`
- 只读参考文件：
  - `YardCraneSimulator/Assets/YardCraneSimulator/Runtime/Input/YardCraneOperatorCommand.cs`
  - `YardCraneSimulator/Assets/YardCraneSimulator/Runtime/Input/YardCraneStandaloneOperationHost.cs`
- 禁止修改未列入允许修改文件的任何文件。

## 5. 约束

- 不改安全状态机、键位、物理参数、碰撞、防碰算法、场景或ProjectSettings。
- 不启动Unity、不运行Shell、不安装依赖、不提交Git。
- 不把静态检查描述为运行验证。

## 6. 交付

1. 说明两个问题的修正方式和精确行号。
2. 说明新增Harness断言如何在修正前失败、修正后通过。
3. 给出新面板高度与1920×1080精确2倍的算式。
4. 列出未验证范围；Harness、Unity编译和Play仍由Codex执行。

