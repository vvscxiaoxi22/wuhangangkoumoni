# PortGatewayService

> 2026-09-15 场桥范围更正：本工程保留为历史Gateway实现与验证证据，但已退出当前`YardCraneSimulator`的S7交付路径。当前场桥只允许在独立`YardCraneS7Integration`场景中使用game4automation直连S7；本README下方的Gateway拓扑、点表和联调说明均为历史范围，不代表当前场桥实现。

历史定位：港口实训平台中集中持有S7 PLC连接的独立后台服务。一期原计划通过本机Windows Named Pipe，向`PortTraining`学生端提供控制租约、诊断和安全事件，向`YardCraneSimulator`/`QuayCraneSimulator`场景EXE提供设备输入帧与仿真反馈帧；对下通过S7 TCP与PLC通信，对单台PLC只维持一条长连接。

## 当前状态（历史实现）

`历史部分实现（协议基础骨架）/退出当前场桥路径`——不是完整服务，不能对外宣称`PortGatewayService`已上线。已经存在并跑通的历史部分：

- 控制管道`pt.gateway.control.{stationId}`（PT-IF-006）的协议骨架：通用消息包络（V1.1第4节）、Named Pipe定长帧编解码和管道命名（3.2节）、`protocol.hello`握手前置校验、只读诊断消息`gateway.status.get`/`gateway.status`。
- `gateway.session.prepare`的请求校验：按V1.1第11节要求的字段做完整性和`controlMode`枚举校验；字段不全回`gateway.session.rejected`+`CONTRACT_VALIDATION_FAILED`（不重试）。
- **模拟设备后端（`allowSimulationFallback`）**：字段齐全后，若该工位`allowSimulationFallback=false`（默认对应"没有真实PLC"的诚实结果），一律回`PLC_OFFLINE`（可重试），不伪造租约；若`allowSimulationFallback=true`（V1.1第18.1节明确允许非正式考核工位使用的模拟兜底），改用`SimulatedDeviceFrameSource`给出固定的"安全就绪、`Local`模式"设备帧——请求的`controlMode`和模拟帧不一致时回`CONTROL_MODE_MISMATCH`（可重试），一致则签发`Reserved`租约并回`gateway.session.prepared`，租约payload标注`isSimulated=true`，`gateway.status`里的`plcOnline`仍然如实保持`false`（模拟不等于真实PLC在线）。
- 租约生命周期：同一工位同时只允许一个`Reserved`/`Active`租约（`gateway.session.prepare`遇到冲突回`CONTROL_LEASE_CONFLICT`，不重试）；`gateway.lease.release`可以释放当前租约并回`gateway.lease.released`，不匹配当前租约（包括根本没有租约、或租约已经自然到期）时回结构化拒绝`gateway.lease.rejected`+`OPERATION_RUN_MISMATCH`（不重试，复用V1.1第15节已有错误码，不是新造的）；释放后同一工位可以再次签发新租约（`leaseEpoch`递增）。租约到期判定使用`Environment.TickCount64`单调时钟（V1.1第4节要求），`issuedAtUtc`/`expiresAtUtc`只用于对外展示的UTC时间戳；到期后同一工位可以再次被签发（同样`leaseEpoch`递增），到期路径和"到期后迟到的release请求被正确拒绝"这两条都已用真实等待验证过，不只是代码审查。
- **Windows ACL（V1.1第17节）**：控制管道的`NamedPipeServerStream`不再使用运行时默认安全描述符，改用显式`PipeSecurity`，只授予"运行本进程的当前Windows用户"和`BUILTIN\Administrators`组`FullControl`；V1.1原文要求的"受信服务账号"暂不落地（本项目目前是普通控制台进程，没有Windows Service部署形态，没有具体服务账号可以引用），已记入决策记录2.7条。
- **场景实时管道`pt.gateway.realtime.{stationId}`（PT-IF-007）骨架**：`protocol.hello`握手（V1.1第10.1节字段齐全，不像控制管道hello那样有缺口）——校验信封`operationRunId`/`stationId`和payload全部字段、校验`leaseId`/`sceneInstanceId`/`operationRunId`与控制管道当前签发的租约一致、用`CryptographicOperations.FixedTimeEquals`对`sessionToken`的SHA-256做**常量时间**比较（V1.1明确要求，不是普通字符串比较，避免时序侧信道泄露哈希匹配进度），同一租约重复使用上一次已接受的`streamId`重连（V1.1明确要求每次重连必须换新streamId）——以上情形统一收敛为`AUTHENTICATION_FAILED`（决策记录2.10/2.13条）；握手成功后按协商的帧间隔（默认20ms）持续推送`equipment.inputFrame`（V1.1第10.2节字段，`sequence`从1递增，`plcOnline`如实为`false`）。租约状态现在由`GatewaySessionState`类统一持有，供控制管道和实时管道共享——一个station只有一份租约真相。
- `GatewaySmokeClient`自测试工具：五个并行的服务端实例（模拟开/模拟关/独立的模式校验工位/独立的到期校验工位/独立的实时管道工位——后者同时跑控制+实时两个服务端共享同一份租约状态），用真实`NamedPipeClientStream`覆盖控制管道的握手、状态查询、`session.prepare`四种结果、`gateway.lease.release`两种结果、完整租约生命周期、真实等待触发的到期路径、ACL构造逻辑本身、三类协议违规，以及实时管道的握手五种结果（缺字段、无租约、token错误、token正确、streamId重用）+两帧`equipment.inputFrame`流验证，共57项断言全部通过（见当天讨论记录的运行日志）。
- **手动联调工具`ManualSessionLauncher`**（`Tools/ManualSessionLauncher/`）：连接一个已经在独立运行的`PortGatewayService`进程（不像`GatewaySmokeClient`那样自己起服务端），跑一次真实`gateway.session.prepare`并打印出`leaseId`/`sessionToken`(原始值)/`sceneInstanceId`/`operationRunId`，供人工/Unity端联调时使用；也支持`--release`释放租约。2026-09-11已用真实分离进程（`PortGatewayService.exe`独立运行+本工具+`YardCraneSimulator`侧的命令行客户端）跑通完整流程，见[PT-IF-007手动联调操作手册](../PortTraining/LM-Wiki/02-知识层-编译层/PT-IF-007手动联调操作手册-v0.1.md)。
- **S7连接生命周期骨架（`S7/`目录）**：`IS7Client`接口把"具体哪个S7库"和"连接失败重试、超时、掉线重连、诚实上报在线状态"这套生命周期逻辑解耦；`RealS7DeviceFrameSource`实现`IDeviceFrameSource`，用可配置退避间隔做重连，`GetCurrentFrame()`保持同步非阻塞（真正的I/O在单独的`RunAsync`后台循环里）。**没有点表寄存器解码**——`GetCurrentFrame()`除了`PlcOnline`如实反映真实连接状态外，其余字段（`emergencyStopReleased`等）目前都是"未确认在线时不敢宣称安全"的保守占位值，不是真实PLC读数；也**没有接入`GatewayControlPipeServer`/`GatewayRealtimePipeServer`**——这两个服务现在仍然用`SimulatedDeviceFrameSource`，替换是明确独立的下一步集成动作，不是本轮范围。用`S7DeviceFrameSourceHarness`+一个可控制"何时失败/何时掉线"的假`IS7Client`验证了连接失败重试、掉线后重连、长退避期间取消能立刻生效（不是等满60秒）、`appsettings.json`里`s7`配置块的解析。
- **S7驱动库暂定选为S7netplus（本项目第一个、也是唯一一个外部NuGet依赖）**：2026-09-12用户确认PLC产品家族是西门子S7-1500后，选定`S7netplus`（0.20.0，MIT协议）并写了真实适配器`S7NetPlusClient`（`CpuType.S71500`），所有方法签名都是直接读该库GitHub源码确认的，不是凭记忆写的。**这仍然是暂定选择，不是决策记录2.15条意义上"PoC通过后的最终锁定"**——`S7NetPlusClient`还没有被`RealS7DeviceFrameSource`以外的任何生产代码引用，也从未连接过任何真实或模拟PLC。用`S7DeviceFrameSourceHarness`验证了这个真实库指向一个明确没有监听的地址（`127.0.0.1`默认S7端口）时，连接失败能被正确捕获、正确重试（真实观察到两轮"超时→重试"循环）、不会让进程崩溃或卡死。**明天现场配置TIA Portal时的关键约束**：S7netplus（和它基于的经典Snap7协议一样）读不到默认开启"优化的块访问"的DB块，Gateway要读写的DB必须在TIA里手动关闭这个选项、改成标准寻址，否则代码本身没问题也读不到数据。

明确没有实现、不得误认为已具备的部分：

- `gateway.session.activate`（场景令牌验证后把`Reserved`提升为`Active`）、`gateway.owner.heartbeat`、`gateway.safeStateReceipt`、`gateway.safetyEvent`——这些依赖真实心跳循环或还需要更多设计，本轮没有实现，租约目前永远停留在`Reserved`。
- `equipment.action`/`equipment.actionAck`（动作去重、执行阶段、互锁校验）和`SimulatorFeedbackFrame`（场景→Gateway方向的反馈帧）——实时管道目前只有Gateway→场景单向的`equipment.inputFrame`流，不读取场景发回的任何内容；`preflightInputSequence`字段目前恒为`0`占位。
- 实时管道hello的`machineType`只做枚举格式校验，不与租约或任何其他记录交叉核对（决策记录2.11条，已知的真实校验空白，不是遗漏）。
- 任何真实S7/PLC通信；`gateway.status`里的`plcOnline`恒为`false`；没有配置`allowSimulationFallback=true`的工位上，`gateway.session.prepare`恒被拒绝。`RealS7DeviceFrameSource`+`S7NetPlusClient`存在，但只验证过"连接一个明确不存在的目标会正确失败重试"，从未连接过任何真实或模拟PLC，`ReadBytesAsync`/`WriteBytesAsync`一次都没有被真正调用过，也没有被`GatewayControlPipeServer`/`GatewayRealtimePipeServer`引用。
- 点表配置读取（草案已有，代码未消费）、点表寄存器解码（把原始字节按点表规则转成`DeviceFrameSnapshot`字段）、`PortTraining.Contracts`集成。
- Named Pipe ACL的**实际OS级隔离效果**没有跨账号验证过——本机开发环境只有一个交互用户，无法真实测试"另一个Windows账号连接被拒绝"；已验证的只是ACL构造逻辑本身（该有的规则都在、`Everyone`没有被授权）和"没有意外把当前用户自己也锁在外面"（其余全部测试仍然连接成功）。

完整现状与待开发项以下表为准：

- [项目开发现状与待开发清单](../PortTraining/LM-Wiki/02-知识层-编译层/项目开发现状与待开发清单.md)
- [系统架构说明](../PortTraining/LM-Wiki/02-知识层-编译层/系统架构说明.md)

已经决定的技术选型（不再是开放问题）：

- 目标框架`net10.0-windows`（2026-09-08从`net10.0`改过来——`System.IO.Pipes`的ACL相关API`NamedPipeServerStreamAcl`只在Windows专属TFM下可见，编译期就会提示，不是运行时才发现），纯控制台+手写异步循环，不引入`Microsoft.Extensions.Hosting`——和仓库里其余`.NET`工具项目（`YardCraneSimulator/Tools/*Harness`）保持同一"能不加依赖就不加"的风格，只是TFM从跨平台改成了显式Windows专属；这本来就符合现实（Named Pipe、S7、这整个平台从一开始就只在Windows上有意义），只是让编译器也知道这件事。真实的多循环调度需求（控制管道+实时管道+S7轮询+心跳）出现后可以再引入Generic Host，目前判断为不必要的复杂度。**唯一的例外是S7驱动**（决策记录2.17条）——工业协议不适合在没有真实硬件验证能力时手写，用了成熟开源库`S7netplus`，这是本项目至今唯一一个外部NuGet依赖。

尚待确定、不得在未讨论前默认的事项：

- 真实S7驱动/库选型——2026-09-12已暂定`S7netplus`（见"当前状态"），但仍需在目标PLC具体型号、固件和TIA配置上完成PoC后才能真正锁定；2026-09-11已经把"连接生命周期"和"具体用哪个库"解耦（`IS7Client`接口），如果PoC发现`S7netplus`不合适，只需要换一个实现这个接口的适配器，不需要改`RealS7DeviceFrameSource`。
- PLC网络端点（IP/Rack/Slot）配置归属——V1.1的`StationProfile`（18.1节）和`PointMapManifest`（18.4节）都没有列出IP地址字段，`StationProfile`只提到一个没有展开定义的`plcProfileId`。目前临时放在`appsettings.json`的`s7`块里（`S7ConnectionOptions`），是否应该归入某个正式的"PLC Profile"概念、和`StationProfile`/点表是什么关系，还没有讨论过。
- 点表配置格式与版本化方式——已有一份草案供讨论：[PortGatewayService点表配置格式草案 V0.1](../PortTraining/LM-Wiki/02-知识层-编译层/PortGatewayService点表配置格式草案-v0.1.md)；草案本身已通过JSON Schema结构校验，但DB号/偏移/PLC型号等全部是占位值，字段名也未最终锁定，代码里没有任何地方读取或依赖这份草案。
- 与`PortTraining.Contracts`（同样`未实现`）之间的消息模型对齐方式。
- `gateway.session.activate`/`gateway.session.active`的落地方式——是控制管道上PortTraining主动发起的消息，还是场景侧实时管道（PT-IF-007）握手本身触发的效果，V1.1原文两种读法都说得通；2026-09-08已与用户确认暂不推进这部分实现，等后续需要时再决定方向。

本项目已经做出、但仍属于"V1.1原文没写清楚、本项目自行选择"的协议解释（消息名、字段名、语义），已经从这里搬到专门的决策记录集中维护，不在本README重复：

- [PortGatewayService协议扩展决策记录 V1.0](../PortTraining/LM-Wiki/02-知识层-编译层/PortGatewayService协议扩展决策记录-v1.0.md)——控制管道`protocol.hello`的payload字段、`gateway.status`的payload字段名、`gateway.session.prepare`失败回复的消息名与payload结构、"请求租约时长"字段名、`leaseEpoch`递增语义、`gateway.lease.release`请求字段契约、Windows ACL里"受信服务账号"的落地范围、`gateway.lease.release`失败改为结构化拒绝、实时管道`protocol.rejected`的payload结构、实时管道hello校验失败统一收敛为`AUTHENTICATION_FAILED`、实时管道hello的`machineType`不交叉核对、`gateway.lease.released`的payload结构、`streamId`重复使用的拒绝与错误码、Unity端手动实现PT-IF-007协议代码（不等`PortTraining.Contracts`）、S7驱动接入先做连接生命周期抽象不锁定具体库、PLC网络端点配置临时放进`appsettings.json`、S7驱动库暂定选为S7netplus目标CPU为S7-1500，共17条；文档开头新增一张"快速对齐摘要"总表，把两条管道上所有消息类型、payload类型和状态列成一张表，不想读全部决策条目细节时可以先看这张表。

## 开发前必读

1. 根目录[`AGENTS.md`](../AGENTS.md) / [`CLAUDE.md`](../CLAUDE.md)。
2. 本目录[`AGENTS.md`](AGENTS.md)。
3. [跨软件接口与参数标准 V1.1](../PortTraining/LM-Wiki/02-知识层-编译层/跨软件接口与参数标准-v1.1.md)——接口矩阵`PT-IF-006`/`PT-IF-007`/`PT-IF-008`、Named Pipe传输标准（3.2节）、S7通信标准（3.3节）。
4. 当天讨论记录：`../PortTraining/Docs/讨论记录/YYYY-MM-DD.md`。

## 历史核心边界（自2026-09-15起不适用于当前场桥）

```text
PortTraining ──Named Pipe── PortGatewayService
场景EXE      ──Named Pipe── PortGatewayService
PortGatewayService ──S7 TCP（单条长连接）── PLC
```

- 在历史Gateway方案中，PLC只允许有一个S7连接持有者：本服务；学生端、教师端和场景EXE禁止直接访问PLC。
- 在历史Gateway方案中，点表、租约、白名单写入、心跳、重连和安全清零逻辑均属于本服务职责，不得下放给客户端。

## 任务交付门禁

按仓库统一要求执行`实现完成 → 验证通过 → 文档更新 → 才能交付`，具体状态定义和记录格式见[任务验证与文档交付规范](../PortTraining/LM-Wiki/03-规则层/任务验证与文档交付规范.md)。编译成功不代表功能验证通过。
