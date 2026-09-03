# YC-P0-03-CABIN-01 场桥驾驶台安全控制链

## 1. 任务身份

- 任务编号：`YC-P0-03-CABIN-01`
- 任务名称：Standalone控制电源、急停、显式故障复位与HUD安全状态闭环
- 执行模式：限定开发
- 当前版本：`2026-09-02-eight-rope-max-v18-contact-interlock`
- 主控：Codex
- 执行代理：Claude Code

## 2. 背景与已知现象

- 三张实体场桥操控面板照片明确包含`控制合/控制关`、急停、`故障复位`和`紧停指示`。
- 当前`YardCraneOperationController`的实时帧已经包含`EmergencyStopReleased`与`ControlPowerOn`，异常时会清零输出并进入`SafeHold`；`TryClearSafeHold`也会校验电源、急停、手柄回中和机构静止。
- 当前Standalone主机却把PLC在线、急停释放和控制电源全部永久传为`true`；进入`SafeHold`后只要下一帧手柄回中便自动调用`TryClearSafeHold`，没有要求操作者执行故障复位。
- 当前HUD只显示大车制动、夹轮器、抓箱和防碰联锁，不显示控制电源、急停及“需复位”状态。
- 2026-09-01最新低空防碰修复属于用户已有改动，禁止回退：Harness关键字段为`containerContactInterlock=True`。

## 3. 本次目标

1. 在设备无关输入快照和命令中增加三个离散意图：`ControlPowerTogglePressed`、`EmergencyStopTogglePressed`、`FaultResetPressed`；保持既有构造调用源兼容，新增参数使用尾部默认值。
2. Standalone模拟面板默认保持`控制电源已合`、`急停已释放`，避免破坏既有单机启动；操作后使用持久状态进入实时安全输入，而不是每帧硬编码`true`。
3. 默认桌面映射：
   - 键盘`F8`：控制电源合/关切换。
   - 键盘`F9`：急停按下/释放切换。
   - 键盘`R`：故障复位。
   - Xbox `D-pad Up`：控制电源合/关切换。
   - Xbox `View/Back`：急停按下/释放切换。
   - Xbox `A`：故障复位。
4. 急停按下或控制电源断开时，当前帧必须把真实布尔状态传给`YardCraneRealtimeInput`，由现有控制器进入`SafeHold`并清零三轴及鸣笛；同时清除待执行的锁箱、解锁和夹轮器动作。
5. 禁止自动恢复：处于`SafeHold`时，即使电源正常、急停释放、机构静止且手柄回中，也必须等待一次新的`FaultResetPressed`才能调用`TryClearSafeHold`。
6. 若急停未释放、控制电源未合、手柄未回中或机构未静止，复位不得恢复运动；保留“需要复位”状态和明确日志，且不得每个FixedUpdate刷屏。
7. 非`SafeHold`状态下按复位，仍通过现有`YardCraneOperationActionType.Reset`进入现有物理复位互锁；不得直接调用或绕过Rig物理复位方法。
8. 输入源失联、窗口失焦和暂停导致的`SafeHold`同样要求操作者恢复条件后显式按复位；不能静默恢复旧运动值。
9. Standalone主机公开只读状态：控制电源、急停是否释放、是否等待复位；不得把这些模拟状态伪装成真实PLC状态。
10. HUD关键状态卡增加三行中文状态：`控制电源`、`急停`、`安全复位`。安全正常用绿色，断电/急停/等待复位使用醒目的琥珀或红色；同时把新增按键加入操作指引。
11. 保持现有大车、小车、起升、鸣笛、锁/解锁、夹轮器、低空防碰和两倍HUD缩放逻辑不变。

## 4. 明确不做

- 不实现真实PLC、S7、Gateway或点表；Unity禁止直接连接PLC。
- 不实现20/40尺、吊具泵、吊具旋转/回零、锚定、旁路、报警、灯光或除雾器。
- 不改场景、Prefab、`.meta`、Package、ProjectSettings或物理Profile。
- 不修改速度、加速度、制动距离、绳索、质量、碰撞器或防碰阈值。
- 不启动Unity、不执行Shell、不安装依赖、不提交Git。

## 5. 必读资料

- `CLAUDE.md`
- `AGENTS.md`
- `YardCraneSimulator/AGENTS.md`
- `PortTraining/LM-Wiki/03-规则层/任务验证与文档交付规范.md`
- `PortTraining/Docs/讨论记录/2026-09-02.md`
- `PortTraining/Docs/讨论记录/2026-09-01.md`第12至15节
- `PortTraining/LM-Wiki/02-知识层-编译层/跨软件接口与参数标准-v1.1.md`第2.3、8、9节

## 6. 文件边界

- 允许修改文件：
  - `YardCraneSimulator/Assets/YardCraneSimulator/Runtime/Input/YardCraneOperatorCommand.cs`
  - `YardCraneSimulator/Assets/YardCraneSimulator/Runtime/Input/YardCraneInputSources.cs`
  - `YardCraneSimulator/Assets/YardCraneSimulator/Runtime/Input/YardCraneInputRouter.cs`
  - `YardCraneSimulator/Assets/YardCraneSimulator/Runtime/Input/YardCraneStandaloneOperationHost.cs`
  - `YardCraneSimulator/Assets/YardCraneSimulator/Experiments/ObiRope/YardCraneEightRopeMaxPrototypeRig.cs`
  - `YardCraneSimulator/Tools/OperationStateMachineHarness/Program.cs`
- 只读参考文件：
  - `YardCraneSimulator/Assets/YardCraneSimulator/Runtime/Operations/YardCraneOperationController.cs`
  - `YardCraneSimulator/Assets/YardCraneSimulator/Runtime/Crane/YardCraneGantryDriveModel.cs`
  - `YardCraneSimulator/Tools/OperationStateMachineHarness/OperationStateMachineHarness.csproj`
- 禁止修改：未列入“允许修改文件”的全部文件。

## 7. 约束与安全规则

- 不把键盘模拟急停表述为真实硬件急停；这只是Standalone输入适配器，未来Gateway提供实际保持量。
- 安全量使用当前持续状态，按钮只改变Standalone模拟状态；不得只发送一帧脉冲后自动恢复。
- 离散按键必须经过现有活动输入源路由，不允许键盘和Xbox同时写控制状态。
- 当一次采样存在相互冲突的离散动作时采用失效安全策略：安全跳闸优先，业务动作不得执行。
- 不允许用复位绕过急停、控制断、手柄未回中、机构运动或现有物理复位互锁。
- 不把编译成功或静态阅读描述为Unity运行验证。
- 发现范围外问题时只报告，不顺手修改。

## 8. 验收要求

- 静态验收：所有新增构造参数保持向后兼容；`HasIntent`包含新增操作；路由后的三类离散意图不丢失；`Zero`不得携带任何离散意图。
- Harness：保持所有既有断言通过，并补充控制器级安全测试，至少覆盖：控制电源断开进入SafeHold并清零、急停按下进入SafeHold、条件未恢复时拒绝清除、条件恢复且显式复位路径成功、恢复后旧运动不重放。Harness无法直接实例化Unity输入源时不得伪造已测试。
- 自动化验收：由主控执行`dotnet run --project YardCraneSimulator/Tools/OperationStateMachineHarness/OperationStateMachineHarness.csproj --configuration Release --no-restore`。
- Unity编译：由主控用Unity `6000.0.42f1`或同版本Bee/Roslyn验证Runtime和ObiRopeTest程序集。
- Unity运行验收：由主控/用户在Play中验证F8、F9、R及Xbox映射；急停/控制断时三轴和鸣笛立即归零；恢复条件后不按R不能运动；按R后重新初始化且旧指令不重放；HUD状态和操作指引正确。
- 相邻回归：Harness仍包含`gantryBrake=True`与`containerContactInterlock=True`；2026-09-01低空防碰逻辑、锁箱/解锁、夹轮器和HUD布局不得退化。

## 9. Claude交付格式

1. 实现结论。
2. 修改文件和精确行号。
3. 输入状态、SafeHold和显式复位的状态转换说明。
4. 实际修改内容。
5. 实际执行的静态检查；不得声称执行了Shell、Harness或Unity。
6. 未验证范围和风险。
7. 建议主控执行的验证步骤。

