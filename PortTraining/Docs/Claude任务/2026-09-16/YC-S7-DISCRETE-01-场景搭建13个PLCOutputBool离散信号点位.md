# YC-S7-DISCRETE-01 场景搭建13个PLCOutputBool离散信号点位

## 1. 任务身份

- 任务编号：YC-S7-DISCRETE-01
- 任务名称：在`YardCraneS7Integration.unity`场景内搭建12个瞬动按钮+1个维持型急停共13个`PLCOutputBool`占位点位，并接入`YardCraneS7IntegrationBridge`
- 执行模式：交互式场景搭建（需要打开Unity编辑器、操作Hierarchy/Inspector；不得通过`Invoke-ClaudeTask.ps1`受控限定开发模式执行）
- 当前版本：`YardCraneS7IntegrationBridge.ImplementationVersion = 2026-09-16-yc-s7-integration-v3-discrete-signals`
- 主控：Codex
- 执行代理：具备Unity编辑器操作能力的交互式Claude Code或主控Codex（本任务涉及场景文件编辑与Inspector操作，不在受控Claude调用的“限定开发”模式许可范围内——该模式明确禁用Unity操作和场景文件修改，见`PortTraining/Tools/ClaudeCollaboration/README.md`）

## 2. 背景与已知现象

- 2026-09-15确认场桥S7直连架构：`YardCraneS7Integration.unity`是工程内唯一允许持有S7连接的场景，独立于正式`YardCrane`场景运行（详见`PortTraining/LM-Wiki/02-知识层-编译层/场桥S7直连接入架构决策-2026-09-15.md`）。
- 2026-09-16确认架构改为"S7独立进程常驻 + 主程序WebSocket客户端"：新增`YardCraneSimulator.SharedNet`程序集（手写WebSocket）、`YardCraneS7CommandWebSocketServer`（S7Integration侧广播）、`YardCraneS7InputSource`（主程序侧`IYardCraneInputSource`实现）、`YardCraneStandaloneOperationHost.Configure(requireS7Link:...)`新增可选参数。全部代码已用真实回环Socket测试验证（`Tools/S7LinkHarness`）并在真实Unity Play中确认端到端联通（用户实测：两个独立Unity进程通过WebSocket互通，数值安全归零语义正确）。
- 同日确认13个离散指令（锁箱/解锁/暂停/夹轮器切换/控制电源/急停/故障复位/大小车锚定切换/吊具泵切换/吊具20尺/吊具40尺/吊具回零）全部对应真实操控台按钮，PLC地址待现场提供，现场已确认：**除急停外其余12个均为瞬动（momentary），急停为维持型（maintained level）**。据此在代码层面已完成：
  - `YardCraneS7LinkProtocol.cs`新增`YardCraneS7MomentarySignal`枚举（12个瞬动信号）+`YardCraneS7LinkFrame`新增`MomentaryPressedMask`（位掩码）和`EmergencyStopEngaged`（电平，非边沿）。
  - `YardCraneS7IntegrationBridge.cs`新增`momentarySignals`（`YardCraneS7MomentaryBinding[]`，12项`{Signal, PLCOutputBool}`绑定）和`emergencyStopSignal`（单个`PLCOutputBool`）两个Inspector字段，含边沿检测/累加、失效安全读取和`ValidateConfiguration()`完整校验。
  - `YardCraneS7InputSource.cs`/`YardCraneStandaloneOperationHost.cs`已完成解码、消费一次语义、急停失效安全直读分支（`s7Exclusive`模式下`emergencyStopReleased`由S7电平直接、持续驱动，不走toggle）。
- **已有真实Unity Play证据**（2026-09-16）：`YardCraneEightRopeMaxPrototypeRig`勾选`Require S7 Link`后进Play，`[YC_STANDALONE_EMERGENCY_STOP] released=False, source=s7Link`→吊机正确进入`SafeHold`——这是失效安全设计的**正确行为**：因为本任务描述的13个`PLCOutputBool`点位当时尚未搭建，`emergencyStopSignal`在Inspector为空引用，Bridge按设计判定"未配置=已触发"，整条链路正确阻止吊机动作。
- 当前状态：**上述代码路径全部就绪且已验证；唯一缺口是`YardCraneS7Integration.unity`场景里还没有这13个`PLCOutputBool`对象，`YardCraneS7IntegrationBridge`的`Momentary Signals`数组和`Emergency Stop Signal`字段在Inspector里仍为空**。本任务只补这一块场景搭建，不改任何C#代码。
- 现有大小车两个`PLCOutputInt`点位（`GantryCommandRaw_SET_PLC_ADDRESS`/`TrolleyCommandRaw_SET_PLC_ADDRESS`）已经用完全相同的模式搭在场景里，可以直接参照其Inspector配置方式（`Name`占位地址、`OriginDataType`、`Settings.Active`/`Override`）。

## 3. 本次目标

- 必须实现：
  1. 在`YardCraneS7Integration.unity`场景里，`20_PLC_To_Unity`节点下（与现有`GantryCommandRaw_SET_PLC_ADDRESS`/`TrolleyCommandRaw_SET_PLC_ADDRESS`同级），新建13个子GameObject，每个挂`PLCOutputBool`组件（`game4automation`命名空间），按下表配置：

     | GameObject名字 | `Name`字段（占位地址） | `OriginDataType` |
     |---|---|---|
     | ContainerLock_SET_PLC_BOOL_ADDRESS | SET_CONTAINERLOCK_BOOL_ADDRESS | BOOL |
     | ContainerUnlock_SET_PLC_BOOL_ADDRESS | SET_CONTAINERUNLOCK_BOOL_ADDRESS | BOOL |
     | Pause_SET_PLC_BOOL_ADDRESS | SET_PAUSE_BOOL_ADDRESS | BOOL |
     | WheelClampToggle_SET_PLC_BOOL_ADDRESS | SET_WHEELCLAMPTOGGLE_BOOL_ADDRESS | BOOL |
     | ControlPowerToggle_SET_PLC_BOOL_ADDRESS | SET_CONTROLPOWERTOGGLE_BOOL_ADDRESS | BOOL |
     | FaultReset_SET_PLC_BOOL_ADDRESS | SET_FAULTRESET_BOOL_ADDRESS | BOOL |
     | GantryAnchorToggle_SET_PLC_BOOL_ADDRESS | SET_GANTRYANCHORTOGGLE_BOOL_ADDRESS | BOOL |
     | TrolleyAnchorToggle_SET_PLC_BOOL_ADDRESS | SET_TROLLEYANCHORTOGGLE_BOOL_ADDRESS | BOOL |
     | SpreaderPumpToggle_SET_PLC_BOOL_ADDRESS | SET_SPREADERPUMPTOGGLE_BOOL_ADDRESS | BOOL |
     | SpreaderSizeFt20_SET_PLC_BOOL_ADDRESS | SET_SPREADERSIZEFT20_BOOL_ADDRESS | BOOL |
     | SpreaderSizeFt40_SET_PLC_BOOL_ADDRESS | SET_SPREADERSIZEFT40_BOOL_ADDRESS | BOOL |
     | SpreaderReturnToZero_SET_PLC_BOOL_ADDRESS | SET_SPREADERRETURNTOZERO_BOOL_ADDRESS | BOOL |
     | EmergencyStop_SET_PLC_BOOL_ADDRESS | SET_EMERGENCYSTOP_BOOL_ADDRESS | BOOL |

     每个对象的`Settings.Active`必须勾选、`Settings.Override`必须不勾选（校验会拒绝Override，安全信号禁止软件强制值）。
  2. 在`YardCraneS7IntegrationBridge`组件的Inspector上：
     - `Momentary Signals`数组（前12个，急停不进此数组）：Size设为12，每一项`Signal`下拉框分别选择`ContainerLock`/`ContainerUnlock`/`Pause`/`WheelClampToggle`/`ControlPowerToggle`/`FaultReset`/`GantryAnchorToggle`/`TrolleyAnchorToggle`/`SpreaderPumpToggle`/`SpreaderSizeFt20`/`SpreaderSizeFt40`/`SpreaderReturnToZero`（`PortTraining.YardCraneSimulator.SharedNet.YardCraneS7MomentarySignal`枚举，12个值必须不重不漏各出现一次），`Point`拖对应步骤1里新建的对象。
     - `Emergency Stop Signal`字段（单独字段，不在数组里）：拖`EmergencyStop_SET_PLC_BOOL_ADDRESS`对象。
  3. 保存场景。
- 明确不做：
  - 不修改任何`.cs`文件——全部相关代码（`YardCraneS7IntegrationBridge.cs`/`YardCraneS7LinkProtocol.cs`/`YardCraneS7InputSource.cs`/`YardCraneStandaloneOperationHost.cs`/`YardCraneS7CommandWebSocketServer.cs`）已完成并验证，本任务纯粹是场景资产搭建。
  - 不修改大小车两个现有`PLCOutputInt`点位。
  - 不填真实PLC地址——`Name`字段一律用上表占位值，真实地址等现场点表。
  - 不启用`connectOnPlay`，不修改`S7Interface`的IP/Rack/Slot等连接参数。
  - 不改`YardCraneS7Integration.unity`场景之外的任何场景或文件。
- 完成判据：
  - Hierarchy里`20_PLC_To_Unity`下恰好新增13个对象，均挂`PLCOutputBool`，配置符合上表。
  - `YardCraneS7IntegrationBridge`的`Momentary Signals`数组恰好12项，12个`YardCraneS7MomentarySignal`枚举值各出现一次不重复；`Emergency Stop Signal`字段非空。
  - 保持`S7Interface.Adress = 0.0.0.0`并触发`YardCraneS7IntegrationBridge.ValidateConfiguration()`时，首个失败原因必须是“PLC IP仍是占位值0.0.0.0”，用于确认连接仍被安全门禁阻止。**该结果不能作为13个离散点位配置正确的证据**：当前实现先检查PLC IP，尚未执行到离散点位校验；本任务又明确要求BOOL地址保持非法占位值，因此在真实点表到位前，完整配置校验本来就不可能通过。
  - 13个点位本身以Inspector静态逐项核对和场景序列化差异为本任务验收依据：对象数量、组件类型、`OriginDataType=BOOL`、`Settings.Active=true`、`Settings.Override=false`、12项枚举不重不漏、12个`Point`引用以及独立急停引用必须全部符合上表。不得把“只看到IP占位错误”误报成离散点位已经由运行时代码验证通过。

## 4. 必读资料

- `CLAUDE.md`
- `AGENTS.md`
- `PortTraining/LM-Wiki/02-知识层-编译层/场桥S7直连接入架构决策-2026-09-15.md`
- `PortTraining/Docs/讨论记录/2026-09-15.md`
- `PortTraining/Docs/讨论记录/2026-09-16.md`（"离散信号通道（12瞬动按钮 + 1维持型急停）"及之后各节，含今天完整的实现说明和真实Play验证记录）
- `YardCraneSimulator/Assets/YardCraneSimulator/S7Integration/Runtime/YardCraneS7IntegrationBridge.cs`（`ValidateMomentarySignals`/`ValidateEmergencyStopSignal`/`IsBoolOutputAddress`三个方法是本任务的实际校验依据）
- `YardCraneSimulator/Assets/YardCraneSimulator/SharedNet/YardCraneS7LinkProtocol.cs`（`YardCraneS7MomentarySignal`枚举定义）

## 5. 文件边界

- 允许修改文件：
  - `YardCraneSimulator/Assets/YardCraneSimulator/Scenes/YardCraneS7Integration.unity`
- 只读参考文件：
  - `YardCraneSimulator/Assets/YardCraneSimulator/S7Integration/Runtime/YardCraneS7IntegrationBridge.cs`
  - `YardCraneSimulator/Assets/YardCraneSimulator/SharedNet/YardCraneS7LinkProtocol.cs`
  - `YardCraneSimulator/Assets/YardCraneSimulator/S7Integration/Runtime/YardCraneS7CommandWebSocketServer.cs`
  - `PortTraining/Docs/讨论记录/2026-09-16.md`
- 禁止修改：未在"允许修改文件"中列出的所有文件，尤其是任何`.cs`文件。

## 6. 约束与安全规则

- 全程不修改`S7Interface`的连接参数（IP/Rack/Slot/MinCommCycleMs等）、不勾选`connectOnPlay`、不尝试连接真实PLC。
- 不给任何点位启用`Settings.Override`（尤其急停——这条校验专门防止用软件伪造安全信号，验收时应确认它确实生效）。
- 不编造真实PLC地址；占位值格式必须保持"不是合法BOOL地址"的形态（如上表所示），让`ValidateConfiguration()`继续正确拒绝，不能为了让校验"通过"而凑一个假地址。
- 不顺手修改其他未列出的场景内容（大小车点位、`YardCraneS7CommandWebSocketServer`等）。

## 7. 验收要求

- 场景内静态检查：Hierarchy层级、对象数量、组件配置逐项核对第3节的完成判据。
- Unity安全门禁检查：进入Play或用右键菜单触发`ValidateConfiguration()`，Console应首先报告“PLC IP仍是占位值0.0.0.0”，且不得发起真实PLC连接。此检查只验证连接门禁，不能替代13个点位的Inspector静态验收。
- 验证边界：由于PLC IP和13个BOOL地址均保持占位值，本任务不宣称`ValidateMomentarySignals`、`ValidateEmergencyStopSignal`或真实PLC读值已经运行验证；待现场点表到位并填写合法绝对地址后，再执行完整配置校验和真实联调。
- 相邻回归：确认大小车两个现有`PLCOutputInt`点位未被误改；确认场景其他部分（`YardCraneS7CommandWebSocketServer`、`Game4AutomationController`等）未受影响。

## 8. Claude交付格式

1. 结论。
2. 实际搭建内容（13个对象+Bridge两个字段的最终配置状态）。
3. 实际执行的验证（`ValidateConfiguration()`触发方式和Console输出）。
4. 未验证范围和风险（真实PLC地址/急停电平极性仍待现场确认，这些不是本任务范围）。
5. 建议下一步（拿到现场点表后填写13个合法BOOL绝对地址，并使用真实PLC或隔离的PLC仿真环境验证瞬动边沿和急停维持电平；禁止通过`Settings.Override`伪造急停安全信号）。
