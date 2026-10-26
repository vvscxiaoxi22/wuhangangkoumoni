# PT-IF-007手动联调操作手册 V0.1

> 2026-09-15 范围更正：本手册仅保留为历史Gateway管道联调证据，不适用于当前场桥game4automation直连S7。当前联调入口、配置边界和验收步骤以[场桥S7直连接入架构决策-2026-09-15.md](场桥S7直连接入架构决策-2026-09-15.md)及场景旁`S7Integration/README.md`为准。

- 状态：手动测试流程说明，不是协议标准。给"明天要跟PLC和Unity程序对接"这类现场联调场景使用。
- 覆盖范围：只覆盖"Unity场景EXE ↔ PortGatewayService"这一条腿（场景实时管道`pt.gateway.realtime.{stationId}`）。**不覆盖PLC那一条腿**——`PortGatewayService`目前没有任何经过真实/模拟PLC验证的S7代码，`gateway.status`里的`plcOnline`会一直是`false`。
- 前提：本文档写作时（2026-09-11）没有真实PLC型号/固件/IP信息，所以联调只能验证"Unity能不能正确连上Gateway、握手、收帧"，不能验证"PLC信号能不能正确传到Unity"。2026-09-12已确认PLC产品家族是西门子S7-1500（见下方"3.6 TIA Portal现场配置要求"），但仍未拿到具体CPU型号/固件版本/IP/点位表。

## 1. 为什么要走这条手动流程

正式流程应该是`PortTraining`的场景启动器自动完成：调用`gateway.session.prepare`拿到租约和一次性令牌，再把这些值通过命令行参数传给Unity场景EXE（V1.1第9.1节）。但场景启动器还没实现，所以现场联调时需要人工完成这几步。

## 2. 需要的三个工具

| 工具 | 位置 | 作用 |
| --- | --- | --- |
| `PortGatewayService`本体 | `PortGatewayService/PortGatewayService.csproj` | 被测的服务本身，必须先启动并保持运行 |
| `ManualSessionLauncher` | `PortGatewayService/Tools/ManualSessionLauncher/` | 代替场景启动器，手动跑一次`gateway.session.prepare`，打印出租约和令牌 |
| Unity端连接测试（二选一） | `YardCraneSimulator/Assets/.../Editor/YardCraneGatewayDebugWindow.cs`（Unity内菜单）<br>或`YardCraneSimulator/Tools/GatewayRealtimeManualClient/`（命令行） | 用`ManualSessionLauncher`给出的值去连Gateway的实时管道，验证握手和收帧 |

## 3. 操作步骤

### 3.1 启动PortGatewayService

```bash
cd PortGatewayService
dotnet run --project PortGatewayService.csproj
```

保持这个窗口开着，不要关闭。控制台应该同时打印两行：

```text
[PGS_CONTROL_PIPE_LISTENING] version=..., pipeName=pt.gateway.control.dev-01, stationId=DEV-01, allowSimulationFallback=True
[PGS_REALTIME_PIPE_LISTENING] version=..., pipeName=pt.gateway.realtime.dev-01, stationId=DEV-01
```

`appsettings.json`里默认`stationId=DEV-01`、`allowSimulationFallback=true`。如果现场用别的工位ID，改这个文件，或者等以后支持命令行覆盖。

### 3.2 获取一个租约和令牌

另开一个终端窗口：

```bash
cd PortGatewayService
dotnet run --project Tools/ManualSessionLauncher/ManualSessionLauncher.csproj -- --station-id DEV-01 --control-mode Local
```

成功会打印一个方框，包含`Operation Run Id`/`Lease Id`/`Scene Instance Id`/`Session Token(raw)`四个值。**这四个值只在这一次运行里有效**，重新跑一次会拿到全新的值（旧的会因为工位已被占用而拿不到新租约，除非先释放）。

### 3.3 用Unity验证连接（方式A：Editor窗口）

1. 打开YardCraneSimulator工程（Unity `6000.0.42f1`）。
2. 菜单 `Tools/Yard Crane/Gateway/Realtime Connection Test...`。
3. 把3.2步的四个值填进对应输入框（Station Id保持`DEV-01`，除非现场改了）。
4. 点击`Test Connection`。
5. 看Unity Console，应该看到一串`[YC_GATEWAY_DEBUG]`日志：连接成功→握手接受→逐帧打印`equipment.inputFrame`内容（`plcOnline=False`是预期结果，因为还没接真实PLC）。

### 3.4 用命令行验证连接（方式B：不依赖Unity）

如果Unity还没就绪、或者想在打开Unity之前先确认Gateway这一侧没问题：

```bash
cd YardCraneSimulator
dotnet run --project Tools/GatewayRealtimeManualClient/GatewayRealtimeManualClient.csproj -- --station-id DEV-01 --operation-run-id <上一步的值> --lease-id <上一步的值> --scene-instance-id <上一步的值> --session-token <上一步的值> --frames 5
```

这个命令行工具和Unity Editor窗口用的是**完全同一份**连接代码（`Assets/YardCraneSimulator/Runtime/Gateway/*.cs`），已经在2026-09-11用真实分离进程跑通过（见当天讨论记录）：一个真实运行的`PortGatewayService`进程 + 这个命令行工具，通过真实Named Pipe完成握手并收到3帧真实数据。

### 3.5 用完释放租约

```bash
cd PortGatewayService
dotnet run --project Tools/ManualSessionLauncher/ManualSessionLauncher.csproj -- --station-id DEV-01 --release <Operation Run Id>
```

不释放的话，这个工位要等租约自然到期（默认30分钟，`--lease-duration-ms`可调）才能再签发新的。

### 3.6 TIA Portal现场配置要求（S7-1500，2026-09-12新增）

2026-09-12确认现场PLC是西门子S7-1500系列（具体CPU型号/固件版本/IP/点位表仍待明天现场确认，见本文档第1节）。`PortGatewayService`选定的S7驱动库是[S7netplus](https://github.com/S7NetPlus/s7netplus)（决策记录2.17条），这个库和Snap7/Sharp7同属"经典S7comm"协议族，有一个**必须在现场TIA Portal里手动配置**的强制前提：

> **需要读写的每一个数据块（DB）都必须关闭"优化的块访问"（Optimized block access）。**

- 原因：TIA Portal新建DB默认开启"优化的块访问"，这种模式下PLC不再对外暴露固定的字节偏移量，经典S7comm协议库（包括S7netplus、Snap7、Sharp7）都无法按偏移量读写这类DB。这不是代码bug，是协议库家族的通用限制（社区在S7netplus/Snap7的GitHub issue里都有对应记录）。
- 操作方法：TIA Portal中打开目标DB的属性，取消勾选"优化的块访问"（Optimized block access），重新编译并下载到PLC。
- 现场检查建议：明天到场后，第一步先确认将要读写的DB是否已关闭优化访问，再进行任何联调，避免把"协议库读不到数据"误判为网络或代码问题。
- 这一条目前只是**库选型阶段的已知限制记录**，`PortGatewayService`还没有针对具体点位表的真实字节读写代码（`ReadBytesAsync`/`WriteBytesAsync`已声明但未被调用），所以现场还不能验证这一条是否已经满足——只能提前告知配置PLC的人员，避免明天到现场才发现要重新编译下载。

## 4. 常见失败和对应含义

| 现象 | 原因 |
| --- | --- |
| `ManualSessionLauncher`连接控制管道失败 | `PortGatewayService`没启动，或`stationId`拼写不一致 |
| Unity/命令行客户端握手被拒绝，`errorCode=AUTHENTICATION_FAILED` | `leaseId`/`sceneInstanceId`/`operationRunId`/`sessionToken`有一个填错了，或者租约已经到期/被释放——回到3.2重新跑一次拿新值 |
| `gateway.session.prepare`本身被拒绝，`errorCode=CONTROL_LEASE_CONFLICT` | 工位已经有一个未释放的租约——先用3.5释放，或者等到期 |
| `equipment.inputFrame`里`plcOnline`一直是`false` | **预期行为，不是bug**——`gateway.status`/`gateway.session.prepare`实际使用的设备源仍是`SimulatedDeviceFrameSource`，S7netplus适配器（2026-09-12新增）还没有接入这两个消息，这个字段只有真正切换到真实设备源后才可能变`true` |

## 5. 明确没有覆盖的部分

- 真实S7/PLC连接——`PortGatewayService`有一套连接生命周期骨架（`S7/`目录，重试/退避/超时/诚实上报在线状态），2026-09-12新增了对S7netplus 0.20.0（暂定，见决策记录2.17条）的真实适配器`S7NetPlusClient`，并用真实库对一个不可达地址跑通了连接失败/重试/取消的完整链路（无真实PLC参与）。但**没有真实字节级读写、也没有接入`gateway.status`/`gateway.session.prepare`实际使用的设备源**（那两个消息现在仍然用`SimulatedDeviceFrameSource`）——真正的PoC锁定要等明天现场拿到具体CPU型号/固件/IP/点位表后才能做，届时也要按第3.6节确认TIA Portal里的DB已关闭"优化的块访问"。
- `equipment.action`/`equipment.actionAck`（锁箱、吊具尺寸等离散动作）——没有实现。
- 场景反馈帧`SimulatorFeedbackFrame`（Unity→Gateway方向）——没有实现，Unity目前不会往回发任何东西。
- 正式驾驶输入链路——本手册的Unity测试窗口和命令行工具都是独立的调试工具，不接入`YardCraneOperationController`等正式驾驶代码。

## 6. 相关文档

- [跨软件接口与参数标准 V1.1](跨软件接口与参数标准-v1.1.md) 第9.1、10.1、10.2节。
- [PortGatewayService协议扩展决策记录 V1.0](PortGatewayService协议扩展决策记录-v1.0.md)。
- [PortGatewayService点表配置格式草案 V0.1](PortGatewayService点表配置格式草案-v0.1.md)（S7接入后才会用到）。
- 当天讨论记录：`../Docs/讨论记录/2026-09-11.md`、`../Docs/讨论记录/2026-09-12.md`。
