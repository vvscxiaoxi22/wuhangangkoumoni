# YardCraneSimulator

港口实训平台的场桥独立 Unity 场景程序。

## 当前状态

- Unity：`6000.0.42f1`
- 工程阶段：八绳MAX灰盒物理、30吨载荷完整动作回归、v15配置解耦、v16场景内部作业状态机、v17单机输入链、v18大车物理驱动/夹轮器互锁/鸣笛以及v24吊具物理旋转投影与自动Play门禁已形成；v24已通过逻辑Harness、Unity程序集编译、+3°模型旋转/松手停止/自动回零烟测和30吨无回归完整链。2026-09-04在v24基础上完成控制链两层解耦（动作分发/接口层、输入命令结构层）、五个机构（夹轮器/吊具泵/大车小车锚定/吊具20-40尺）的Rig巨型类拆分、HUD/GUI呈现层抽取和绳索回路数据类型抽取，累计源码提升为`v28-spreader-size-presenter`；解耦层已用Harness全量33项用例复核逐字节一致，v27已获真实Unity编译与Play证据（大车/小车锚定经`YardCraneAnchorPresenter`调用栈确认、吊具泵经`YardCraneSpreaderPumpPresenter`调用栈确认、HUD显示确认正常、大量运动测试无异常），但v28上的吊具尺寸拆分、夹轮器、吊具尺寸拒绝场景、双动作冲突拒绝仍未单独复测。失焦容忍窗口曾被Codex旁路审查发现P1级安全回归（`YC-P0-FOCUS-01`：容忍窗口内会执行失焦前排队的离散动作，且合成零命令可能误判"已回中"从而错误解除SafeHold），已在交互会话修复并通过隔离Harness六项断言复核，但Unity Play专项验证仍未完成，修复前的400ms增量不得视为已放行。实体Xbox、Windows Player、正式模型、实时IPC、设备驱动和平台工单闭环尚未完成
- 目标设备：40.5 吨场桥
- 运行方式：既可直接启动为单机全流程模拟器，也可后续由`PortTraining`托管启动为独立全屏Windows EXE
- PLC 接入：自2026-09-15起由独立`YardCraneS7Integration.unity`中的唯一game4automation `S7Interface`直连；接口默认禁用，真实PLC尚未验收
- 开发构建：Windows x64 + Mono；正式发布切换 IL2CPP 前必须先补齐并验证 Windows SDK/C++ 工具链
- 正式作业场景：`Assets/YardCraneSimulator/Scenes/YardCrane.unity`；场景自带两个Camera和一个Display1 `UIDocument`，运行时不动态创建驾驶UI
- 驾驶HUD：`2026-09-10-yc-driving-hud-v12-navy-operational-skin`；显示布局：`2026-09-10-yc-operational-scene-v2-native-dual-display`

## 当前最高优先级

先完成场桥单机全流程作业：键盘、Xbox和S7点位都必须先转换为同一套统一操作语义。S7只允许存在于`YardCraneS7Integration`独立场景；输入适配器不得直接控制机构，机构运动不得引用键盘键值、Xbox按键或PLC地址。

单机版本依次完成卸船转堆、装船出堆、陆侧收箱和陆侧提箱，并覆盖任务开始、车辆到位、抓箱、转运、落箱、解锁、完成/失败、本地结果和重新开始。

## 一期职责

- 大车、小车、起升、吊具回旋、夹轮器、吊具泵、20/40 尺吊具状态
- 收箱、提箱、海侧内集卡/AGV、陆侧外集卡和堆场箱位作业
- 就地驾驶室与 ROCS 远程视景
- 对位、扫桩、障碍摆箱、碰撞、流程和安全事件
- 向学生端提交运行证据和完成请求，不直接产生正式成绩

## 启动契约

以下是正式`YardCrane` Managed模式的既有兼容契约；`YardCraneS7Integration`当前是独立联调场景，不消费其中的Gateway参数。Gateway退出交付路径后的正式启动契约迁移尚未完成，不能把本段当作S7直连已经闭环的证据。

平台托管的Managed模式必须由学生端提供以下非敏感参数：

```text
--station-id YC-LAB-01
--operation-run-id <uuid>
--scene-instance-id <uuid>
--coordinator-pipe <pipe-name>
--gateway-pipe <pipe-name>
--gateway-lease-id <uuid>
--display-profile SIMULATOR-DUAL-DISPLAY-1920X1080X2-V2
--scene-profile YC-SCENE-DEV
--protocol-version 1.1.0
```

一次性会话令牌通过环境变量 `PORTTRAINING_SESSION_TOKEN` 传递，绝不能进入日志。

Unity编辑器直接运行时使用明确标记的本地开发参数。直接启动Windows Player且完全不提供托管参数时进入`Standalone`本地运行上下文；一旦出现任一托管参数，则必须提供完整参数和会话令牌，缺失时拒绝启动，绝不自动降级Standalone。

## 单机输入与构建

- 键盘：`J/L`大车语义、`A/D`小车、`W/S`起升、`Space`鸣笛、`E`锁箱、`Q`解锁、`C`夹轮器切换、`X`小车锚定切换、`Z`大车锚定切换、`P`吊具泵切换、`1`吊具20尺、`2`吊具40尺、`[`/`]`吊具左/右旋转（维持型，松开即停）、`0`吊具回零、`F8`控制电源、`F9`急停、`R`故障复位、`Esc`暂停。
- Xbox：左摇杆X大车、左摇杆Y起升、右摇杆X小车、右摇杆Y吊具旋转（维持型）、`LB`鸣笛、`X`锁箱、`B`解锁、`Y`夹轮器切换、`D-Pad↓`小车锚定切换、`RB`大车锚定切换、`LT`吊具泵切换、`D-Pad←`吊具20尺、`D-Pad→`吊具40尺、`R3`吊具回零、`D-Pad↑`控制电源、`View`急停、`A`故障复位、`Menu`暂停。
- 键盘、Xbox和S7适配器只生成统一命令；任何输入适配器都不得直接修改场桥机构或绳索物理。
- 大车已接入真实物理驱动（加减速、两端限位与减速区）与夹轮器互锁（合闸时物理钳零、移动中禁止合闸），详见[场桥YC-P0-02开发计划](../PortTraining/LM-Wiki/02-知识层-编译层/场桥YC-P0-02开发计划-大车鸣笛与夹轮器.md)。
- 大车/小车锚定（独立于夹轮器的抗风销机构）、整机控制电源/急停/故障复位链已接入统一命令与联锁，详见`Docs/讨论记录/2026-09-02.md`第2、3条；均为`代码实现完成`，Unity编译与Play验证仍待补齐。
- 吊具泵开/关与20/40尺状态机（尺寸切换需泵已启动、大车小车起升三轴静止且未挂箱）已接入统一命令与联锁，详见`Docs/讨论记录/2026-09-02.md`第5条；`Unity编译通过`（v21，同一程序集），泵开关的请求-接受闭环已有Play证据，尺寸切换的三种拒绝互锁与HUD渲染仍待补证。
- 吊具左右旋转（维持型方向输入，±3°，需泵已启动）与回零已接入统一命令、联锁和HUD。v24把逻辑角度投影到`35_Spreader`真实模型槽、灰盒吊具、碰撞体、传感器与`ContainerAttachment`，锁定箱体会同步跟随；八绳滑轮、上架刚体和绳索解算仍保持独立。逻辑Harness、Unity程序集编译、+3°逻辑/物理同步、松手停止、自动回零以及后续30吨完整链均已通过；负向限位、回零中手动打断、泵关闭拒绝和带载旋转仍待专项Play，详见`../PortTraining/Docs/讨论记录/2026-09-03.md`第4条。
- Editor可执行菜单`Tools/Yard Crane/Tests/Run Spreader Rotation Play Smoke Test`验证单项旋转，也可在Edit模式执行`Tools/Yard Crane/Tests/Run Full Play Verification`自动完成“旋转/回零→复位→30吨完整链”；通过标志分别为`YC_SPREADER_ROTATION_PLAY_SMOKE_PASS`与`YC_FULL_PLAY_VERIFICATION_PASS`。
- 2026-09-04场桥控制链解耦（交互会话，详见`Docs/讨论记录/2026-09-04.md`）：
  1. 动作分发/接口层——`YardCraneOperationController.SubmitAction`原来覆盖9种动作类型的大switch改为按`YardCraneOperationActionType`查表分发到独立`IYardCraneMechanism`实现类（新文件`YardCraneOperationMechanisms.cs`），新增机构动作只需一个新类+一行注册，不再需要改共享switch。
  2. 输入命令结构层——`YardCraneOperatorCommand`/`YardCraneInputSnapshot`从13个位置参数bool改为`YardCraneOperatorIntent`枚举+集合，`YardCraneStandaloneOperationHost`分发逻辑同步用`HashSet<YardCraneOperatorIntent>`重写；两层均已用Harness全量33项用例复核，逐字节结果一致。
  3. Rig巨型类拆分（模板抽取）——夹轮器、吊具泵、大车/小车锚定四个机构从`YardCraneEightRopeMaxPrototypeRig.cs`拆出，各自新增`YardCraneWheelClampPresenter`/`YardCraneSpreaderPumpPresenter`/`YardCraneAnchorPresenter`（承接执行器接口转发和HUD呈现；FixedUpdate内嵌的物理互锁检测/状态日志和跨机构复位门禁仍留在Rig里）。
  4. HUD/GUI呈现层整体抽取——新增`YardCraneDebugHudPresenter`（临时IMGUI测试显示脚本，后续由UI Toolkit正式UI替换），`OnGUI`、两个面板绘制、样式缓存和全部状态行方法搬出，Rig文件从4376行降到3878行。
  5. 绳索回路记账数据类型抽取——新增`YardCraneRopeLoopRuntime.cs`，`LoopDefinition`/`LoopRuntime`/`VisualLegRuntime`三个纯数据类型原样搬出。
  6. 失焦容忍窗口——短暂失焦（默认400ms内，比如切到Console复制一行日志）不再强制进入`SafeHold`；运动仍立即清零，只是不再需要每次都按`R`才能恢复；`sourceLost`（设备真断连）和主动暂停不受影响。
  7. 吊具20/40尺机构拆分——新增`YardCraneSpreaderSizePresenter`，承接执行器接口转发（`TryQueueSpreaderSize`）和HUD状态行，与夹轮器/吊具泵/锚定同一拆分边界；泵开状态由调用方传入，不反向依赖`YardCraneSpreaderPumpPresenter`。
  8. **YC-P0-FOCUS-01修复**——Codex旁路审查用独立Harness（`Tools/StandaloneHostHarness`）复现失焦容忍窗口的P1回归：容忍窗口内会执行失焦前已排队的离散动作（如锁箱），且失焦时的合成零命令可能被`FaultReset`误读为"已回中"从而错误解除`SafeHold`。首版修复曾把窗口内一律作废所有排队动作，用户反馈"限制太死"后收窄为：只保留`FaultReset`一项的强约束（缺乏聚焦后真实新采样前挂起、不消费、不误放行），锁箱/解锁/夹轮器/锚定/吊具泵/尺寸等其余排队动作正常派发（它们派发时都会重新校验真实物理状态，失焦不需要一并取消）。隔离Harness扩充到八项断言全部通过，原33项状态机用例同步复跑无回归；Unity Play专项仍未验证。
  9. 源码`ImplementationVersion`累计提升为`v28-spreader-size-presenter`；两层解耦已用Harness全量验证，v27本身也已获真实Unity编译与Play证据（大车/小车锚定经`YardCraneAnchorPresenter`调用栈确认、吊具泵经`YardCraneSpreaderPumpPresenter`调用栈确认、HUD显示确认正常），但v28上的吊具尺寸拆分、夹轮器、吊具尺寸拒绝场景、双动作冲突拒绝、修复后的失焦容忍窗口仍未在Unity里单独复测。
- Unity菜单`Tools/PortTraining/Yard Crane/Build Windows Development`生成`Builds/Development/YardCraneSimulator.exe`。
- 当前状态为`部分实现`：Editor Play中键盘驱动大车/鸣笛/吊具泵/大车小车锚定/吊具旋转/锁箱解锁均已有人工证据（含`FaultReset`跳闸-恢复不对称逻辑，但该证据产生于更早版本，v27/v28上尚未重新触发）；v28上已确认锚定、吊具泵、夹轮器（首次拿到本轮拆分后的真实证据）、HUD显示和一次完整30吨带载循环正常；v28新增的吊具尺寸Presenter、吊具20/40尺三种拒绝场景、双动作冲突拒绝仍缺Unity验证；**修复后的失焦容忍窗口（YC-P0-FOCUS-01）完全没有Unity Play证据**，是当前最高优先级的待补测项；Windows Player、实体Xbox仍需验收。

## UI Toolkit 入口、教师/学生首页、驾驶与结果 UI（更新至2026-09-12）

- `StudentEntry.unity`只负责学生/教师登录。学生成功登录后切换到独立`StudentHome.unity`；教师以`admin/admin`成功登录后切换到独立`TeacherHome.unity`，入口不再内嵌任何教师首页。
- `TeacherHome`由`YardCraneTeacherSideShell`独立持有两套场景序列化`UIDocument`：Display1使用`Resources/TeacherSide/Display1UI`，Display2使用`Resources/TeacherSide/Display2UI`，不复用学生端UXML或PanelSettings，也不在运行时动态创建文档。
- 打开`StudentEntry`后无需Play即可预览学生登录与教师登录；`StudentHome`和`TeacherHome`由各自场景与壳层在编辑模式显示。预览不执行登录或创建训练会话。
- 学生登录分别填写学号与姓名；教师使用约定的固定本机管理员凭据。口令遮蔽显示，切换身份、登录成功与退出均清空输入。真实登录与训练需要点击 Play。
- `Bootstrap.unity`只负责启动契约和生命周期，然后加载正式`YardCrane.unity`；驾驶HUD由`YardCrane`场景已有的`UIDocumentDisplay1`持有，不跨场景生成或复制UIDocument。
- 正式场景恰好使用两个全屏相机和一个驾驶UIDocument：`Display1Camera → targetDisplay 0`，`Display2Camera → targetDisplay 1`，`UIDocumentDisplay1 → targetDisplay 0`；只启用Display1相机上的一个AudioListener。
- 主配置为Windows/Unity分别识别的两块1920×1080显示器，配置ID`SIMULATOR-DUAL-DISPLAY-1920X1080X2-V2`。旧1920×2160逻辑拼接仅为历史兼容，不再作为当前布局依据。
- 深海蓝HUD在Display1展示计时、任务标题、五状态灯、六项运动遥测、任务/姿态/锁销和导航；七类弹窗独立触发、互斥并居中，弹窗出现时底层HUD继续显示和刷新。
- 生命周期结果仍投影`Finalizing / ExitAllowed / Terminated / Failed`；没有NodeService结果时明确不显示分数。当前尚缺真实任务完成事件和权威成绩闭环。
- 数据通过只读 `YardCraneHudSceneAdapter → YardCraneHudFeed → YardCraneOperatorHud` 单向更新；数值10Hz，状态变化立即通知；无效或中断显示未知，不直接访问PLC或修改运动。
- Build Settings固定顺序为`StudentEntry → Bootstrap → YardCrane → StudentHome → TeacherHome`。
- Unity`6000.0.42f1`中教师登录到独立TeacherHome、教师退出回入口、学生登录到StudentHome以及入口到驾驶全流程Play均已通过；教师页两张卡只展示真实的服务待接入边界。实体双屏Player、用户最终视觉、PLC与正式教师业务尚未验收。
- 保留旧 IMGUI 物理调试显示，正式HUD就绪时隐藏旧显示；Rig上的 `showRuntimePanel` 仍作为整体显示开关。
- 设计、字段语义、验证证据与协作边界见[场桥 UI Toolkit 基线](../PortTraining/LM-Wiki/02-知识层-编译层/场桥UI-Toolkit数据接入与界面基线-v1.0.md)。UI回归菜单：`Tools/Yard Crane/Tests/Verify UI Toolkit HUD`。

## 目录

- `Assets/YardCraneSimulator/Runtime/Bootstrap`：启动参数、状态机和入口
- `Assets/YardCraneSimulator/Runtime/Crane`：场桥机构状态模型
- `Assets/YardCraneSimulator/Runtime/Configuration`：本地场景配置
- `Assets/YardCraneSimulator/Runtime/IPC`：后续接入标准 Named Pipe 契约
- `Assets/YardCraneSimulator/Scenes/StudentEntry.unity`：教师端/学生端轻量入口
- `Assets/YardCraneSimulator/Scenes/StudentHome.unity`：登录后的学生首页与学习训练入口
- `Assets/YardCraneSimulator/Scenes/TeacherHome.unity`：登录后的独立教师工作台；拥有教师侧Display1/Display2 UIDocument
- `Assets/YardCraneSimulator/Scenes/Bootstrap.unity`：启动契约及生命周期入口
- `Assets/YardCraneSimulator/Scenes/YardCrane.unity`：正式场桥物理、双相机及Display1驾驶HUD作业场景
- `Assets/YardCraneSimulator/Scenes/YardCraneS7Integration.unity`：独立S7联调场景；唯一S7连接、点位与安全适配器，默认禁用且暂不加入正式Build Settings
- `Assets/YardCraneSimulator/S7Integration/Runtime`：S7配置校验、`-3000..3000`限幅归一化及断连/超时安全归零
- `Assets/test.unity`：退出Build Settings的历史灰盒场景，不再承接正式开发
- `Assets/StreamingAssets/Config`：场桥本地配置

## 下一阶段

1. 建立共享 `PortTraining.Contracts` 包并接入 `scene.hello` / `operation.start` / `scene.ready`。
2. 取得真实PLC点表，在`YardCraneS7Integration`完成地址/类型、读写方向、通信周期、断连重连、急停、看门狗和安全归零联调，再决定与正式`YardCrane`统一操作语义链的加载方式。
3. 在现有小车/起升/八绳MAX/大车/夹轮器与SafeHold基础上补齐四角锁销、安全事件和故障注入。
4. 导入 1:1 场桥与堆场资产，在两块实体`1920×1080`显示器和Windows Player上验收显示路由、缩放、触控及字体。
