# YardCraneSimulator

港口实训平台的场桥独立 Unity 场景程序。

## 当前状态

- Unity：`6000.0.42f1`
- 工程阶段：八绳MAX灰盒物理、30吨载荷完整动作回归、v15配置解耦、v16场景内部作业状态机、v17单机输入链、v18大车物理驱动/夹轮器互锁/鸣笛以及v23吊具物理旋转投影已形成；v23已通过逻辑Harness、Unity程序集编译和Play初始化绑定，非零角度/回零烟测、30吨组合回归、实体Xbox、Windows Player、正式模型、实时IPC、设备驱动和平台工单闭环尚未完成
- 目标设备：40.5 吨场桥
- 运行方式：既可直接启动为单机全流程模拟器，也可后续由`PortTraining`托管启动为独立全屏Windows EXE
- PLC 接入：通过独立 `PortGatewayService`，本工程不直接连接 S7 PLC
- 开发构建：Windows x64 + Mono；正式发布切换 IL2CPP 前必须先补齐并验证 Windows SDK/C++ 工具链

## 当前最高优先级

先完成场桥单机全流程作业：同一套统一操作语义同时适配键盘和Xbox手柄，后续再由`PortGatewayService`把PLC输入转换成同样的操作语义。输入适配器不得直接控制机构，机构运动不得引用键盘键值、Xbox按键或PLC地址。

单机版本依次完成卸船转堆、装船出堆、陆侧收箱和陆侧提箱，并覆盖任务开始、车辆到位、抓箱、转运、落箱、解锁、完成/失败、本地结果和重新开始。

## 一期职责

- 大车、小车、起升、吊具回旋、夹轮器、吊具泵、20/40 尺吊具状态
- 收箱、提箱、海侧内集卡/AGV、陆侧外集卡和堆场箱位作业
- 就地驾驶室与 ROCS 远程视景
- 对位、扫桩、障碍摆箱、碰撞、流程和安全事件
- 向学生端提交运行证据和完成请求，不直接产生正式成绩

## 启动契约

平台托管的Managed模式必须由学生端提供以下非敏感参数：

```text
--station-id YC-LAB-01
--operation-run-id <uuid>
--scene-instance-id <uuid>
--coordinator-pipe <pipe-name>
--gateway-pipe <pipe-name>
--gateway-lease-id <uuid>
--display-profile DESKTOP-16X9-V1
--scene-profile YC-SCENE-DEV
--protocol-version 1.1.0
```

一次性会话令牌通过环境变量 `PORTTRAINING_SESSION_TOKEN` 传递，绝不能进入日志。

Unity编辑器直接运行时使用明确标记的本地开发参数。直接启动Windows Player且完全不提供托管参数时进入`Standalone`本地运行上下文；一旦出现任一托管参数，则必须提供完整参数和会话令牌，缺失时拒绝启动，绝不自动降级Standalone。

## 单机输入与构建

- 键盘：`J/L`大车语义、`A/D`小车、`W/S`起升、`Space`鸣笛、`E`锁箱、`Q`解锁、`C`夹轮器切换、`X`小车锚定切换、`Z`大车锚定切换、`P`吊具泵切换、`1`吊具20尺、`2`吊具40尺、`[`/`]`吊具左/右旋转（维持型，松开即停）、`0`吊具回零、`F8`控制电源、`F9`急停、`R`故障复位、`Esc`暂停。
- Xbox：左摇杆X大车、左摇杆Y起升、右摇杆X小车、右摇杆Y吊具旋转（维持型）、`LB`鸣笛、`X`锁箱、`B`解锁、`Y`夹轮器切换、`D-Pad↓`小车锚定切换、`RB`大车锚定切换、`LT`吊具泵切换、`D-Pad←`吊具20尺、`D-Pad→`吊具40尺、`R3`吊具回零、`D-Pad↑`控制电源、`View`急停、`A`故障复位、`Menu`暂停。
- 键盘、Xbox和未来Gateway只生成统一命令；任何输入适配器都不得直接修改场桥机构或绳索物理。
- 大车已接入真实物理驱动（加减速、两端限位与减速区）与夹轮器互锁（合闸时物理钳零、移动中禁止合闸），详见[场桥YC-P0-02开发计划](../PortTraining/LM-Wiki/02-知识层-编译层/场桥YC-P0-02开发计划-大车鸣笛与夹轮器.md)。
- 大车/小车锚定（独立于夹轮器的抗风销机构）、整机控制电源/急停/故障复位链已接入统一命令与联锁，详见`Docs/讨论记录/2026-09-02.md`第2、3条；均为`代码实现完成`，Unity编译与Play验证仍待补齐。
- 吊具泵开/关与20/40尺状态机（尺寸切换需泵已启动、大车小车起升三轴静止且未挂箱）已接入统一命令与联锁，详见`Docs/讨论记录/2026-09-02.md`第5条；`Unity编译通过`（v21，同一程序集），泵开关的请求-接受闭环已有Play证据，尺寸切换的三种拒绝互锁与HUD渲染仍待补证。
- 吊具左右旋转（维持型方向输入，±3°，需泵已启动）与回零已接入统一命令、联锁和HUD。v23把逻辑角度投影到`35_Spreader`真实模型槽、灰盒吊具、碰撞体、传感器与`ContainerAttachment`，锁定箱体会同步跟随；八绳滑轮、上架刚体和绳索解算仍保持独立。逻辑Harness、三个Unity程序集编译及Play初始化绑定通过；非零角度和自动回零烟测、挂30吨箱组合回归仍待执行，详见`../PortTraining/Docs/讨论记录/2026-09-03.md`第2条。
- Editor Play就绪后可执行菜单`Tools/Yard Crane/Tests/Run Spreader Rotation Play Smoke Test`，通过标志为`YC_SPREADER_ROTATION_PLAY_SMOKE_PASS`。
- Unity菜单`Tools/PortTraining/Yard Crane/Build Windows Development`生成`Builds/Development/YardCraneSimulator.exe`。
- 当前状态为`部分实现`：Editor Play中键盘驱动大车/夹轮器/鸣笛/吊具泵已有人工证据；控制电源/急停/故障复位、大车/小车锚定和吊具20/40尺仍缺完整分项验收；吊具旋转已完成v23模型投影并通过逻辑Harness、Unity编译和Play初始化绑定，但实际±3°运动、自动回零、30吨带载回归、Windows Player、实体Xbox、拔出及窗口失焦仍需验收。

## 目录

- `Assets/YardCraneSimulator/Runtime/Bootstrap`：启动参数、状态机和入口
- `Assets/YardCraneSimulator/Runtime/Crane`：场桥机构状态模型
- `Assets/YardCraneSimulator/Runtime/Configuration`：本地场景配置
- `Assets/YardCraneSimulator/Runtime/IPC`：后续接入标准 Named Pipe 契约
- `Assets/YardCraneSimulator/Scenes/Bootstrap.unity`：唯一启动场景
- `Assets/StreamingAssets/Config`：场桥本地配置

## 下一阶段

1. 建立共享 `PortTraining.Contracts` 包并接入 `scene.hello` / `operation.start` / `scene.ready`。
2. 实现 Coordinator 与 Gateway 双 Named Pipe 客户端、双心跳和序号校验。
3. 在现有小车/起升/八绳MAX/大车/夹轮器与SafeHold基础上补齐四角锁销、安全事件和故障注入。
4. 导入 1:1 场桥与堆场资产，完成桌面与 9:16 模拟机显示配置。
