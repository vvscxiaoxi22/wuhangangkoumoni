# PortGatewayService协议扩展决策记录 V1.0

> 2026-09-15 范围更正：本文件改为历史Gateway实现决策记录，不再持续约束当前`YardCraneSimulator`的S7开发。当前场桥直连架构以[场桥S7直连接入架构决策-2026-09-15.md](场桥S7直连接入架构决策-2026-09-15.md)为准；下文保留用于追溯，不得当作直连场景的验收证据。

- 记录版本：1.0.0
- 首次生效日期：2026-09-08（各条目自身另有生效日期，见下）
- 适用范围：`PortGatewayService`与`跨软件接口与参数标准-v1.1.md`之间存在的、文字未覆盖的实现空白
- 状态：历史冻结——保留截至退出当前场桥路径时的实现选择；若未来独立恢复Gateway项目，应另行评审并追加新的生效记录

## 1. 文件目的

`跨软件接口与参数标准-v1.1.md`（下称"V1.1"）定义了`PortGatewayService`必须遵守的传输格式、消息包络和大部分消息字段，但在开发过程中发现若干处V1.1原文没有给出具体字段名、消息名或语义规则的空白。这些空白如果由`PortGatewayService`单方面选择又不留痕迹，会导致`PortTraining`、场景EXE等消费方各自猜测、后续联调返工。

本文件把这些空白处的选择正式记录下来，格式对齐V1.1自身"变更原则：破坏兼容性的修改必须升级主版本并形成架构决策记录"的要求，以及`LM-Wiki/README.md`"架构决策必须写明背景、选择、替代方案、后果和生效日期"的规则。

**本文件记录的是`PortGatewayService`一个软件自己的实现选择，不是V1.1标准本身的修订**——条目里的消息名、字段名在其他软件真正开始消费之前，仍可能因为V1.1正式吸收或另作决定而改变；改变时必须在本文件追加更正记录，不得静默覆盖已有条目（对应根`CLAUDE.md`"若历史结论与新运行证据冲突，追加更正记录，不静默覆盖"）。

## 1.1 快速对齐摘要（供跨软件开发者查阅，不读全文时先看这张表）

2026-09-09整体复查时补的一张总表，把目前两条Named Pipe上已经实现的每一个消息类型列出来，标注payload类型和对应决策条目；不逐条重复决策原因，只做索引。表内容与实际代码同步核对过（`grep`过每一个`MessageType = "..."`和`ErrorCode = "..."`字符串，确认代码、README、决策条目三处一致）。

| 消息类型 | 管道 | 方向 | Payload类型 | 状态 | 相关条目 |
| --- | --- | --- | --- | --- | --- |
| `protocol.hello`（控制） | `pt.gateway.control` | PortTraining→Gateway | 空对象`{}` | 已实现 | 2.1 |
| `protocol.accepted`（控制） | `pt.gateway.control` | Gateway→PortTraining | 空 | 已实现 | - |
| `gateway.status.get` | `pt.gateway.control` | PortTraining→Gateway | 空 | 已实现 | - |
| `gateway.status` | `pt.gateway.control` | Gateway→PortTraining | `GatewayStatusPayload` | 已实现 | 2.2 |
| `gateway.session.prepare` | `pt.gateway.control` | PortTraining→Gateway | `GatewaySessionPreparePayload` | 已实现 | 2.4 |
| `gateway.session.prepared` | `pt.gateway.control` | Gateway→PortTraining | `GatewaySessionLease` | 已实现 | 2.5（`leaseEpoch`语义） |
| `gateway.session.rejected` | `pt.gateway.control` | Gateway→PortTraining | `GatewayErrorPayload` | 已实现 | 2.3 |
| `gateway.lease.release` | `pt.gateway.control` | PortTraining→Gateway | 仅信封`operationRunId` | 已实现 | 2.6 |
| `gateway.lease.released` | `pt.gateway.control` | Gateway→PortTraining | `GatewayLeaseReleasedPayload` | 已实现 | 2.12 |
| `gateway.lease.rejected` | `pt.gateway.control` | Gateway→PortTraining | `GatewayErrorPayload` | 已实现 | 2.8 |
| `protocol.hello`（实时） | `pt.gateway.realtime` | 场景EXE→Gateway | `RealtimeHelloPayload` | 已实现 | 字段V1.1第10.1节已写全，无需决策 |
| `protocol.accepted`（实时） | `pt.gateway.realtime` | Gateway→场景EXE | 空 | 已实现 | - |
| `protocol.rejected`（实时） | `pt.gateway.realtime` | Gateway→场景EXE | `GatewayErrorPayload` | 已实现 | 2.9、2.10、2.11、2.13 |
| `equipment.inputFrame` | `pt.gateway.realtime` | Gateway→场景EXE | `EquipmentInputFrame`（精简包络，不套`MessageEnvelope`） | 已实现 | 字段V1.1第10.2节已写全，无需决策 |
| `equipment.action` | `pt.gateway.realtime` | Gateway→场景EXE | - | **未实现** | 见第3节 |
| `equipment.actionAck` | `pt.gateway.realtime` | 场景EXE→Gateway | - | **未实现** | 见第3节 |
| `SimulatorFeedbackFrame` | `pt.gateway.realtime` | 场景EXE→Gateway | - | **未实现** | 见第3节 |
| `gateway.session.activate`/`gateway.session.active` | `pt.gateway.control` | PortTraining↔Gateway | - | **未实现（暂缓，2026-09-08用户决定）** | 见第3节 |
| `gateway.owner.heartbeat` | `pt.gateway.control` | PortTraining→Gateway | - | **未实现** | 见第3节 |
| `gateway.safeStateReceipt`/`gateway.safetyEvent` | `pt.gateway.control` | Gateway→PortTraining | - | **未实现** | 见第3节 |

跨管道共享状态：`GatewaySessionState`（`PortGatewayService/GatewaySessionState.cs`）持有当前工位的租约，`GatewayControlPipeServer`（签发/释放）和`GatewayRealtimePipeServer`（验证）共用同一份实例，避免两条管道对"当前租约是什么"各执一词。

跨软件实现现状：`YardCraneSimulator`一侧已经手写了一份只覆盖上表`protocol.hello`（实时）/`protocol.accepted`/`protocol.rejected`/`equipment.inputFrame`四行的Unity端协议代码（2.14条），并用真实分离进程验证过和本项目实际互通（见[PT-IF-007手动联调操作手册](PT-IF-007手动联调操作手册-v0.1.md)）。这份代码是`PortTraining.Contracts`落地前的临时替代，字段名需要两边手动保持一致。

## 2. 决策条目

### 2.1 控制管道`protocol.hello`的payload字段

- 背景：V1.1第3.2节要求所有本机Named Pipe连接（含`pt.gateway.control.{stationId}`）的第一条消息必须是`protocol.hello`，但只有第10.1节给出了"场景↔Gateway实时管道"专属的hello字段表（`sessionToken`/`stationId`/`operationRunId`/`machineType`/`leaseId`/`sceneInstanceId`/`streamId`/`sceneVersion`/`protocolVersion`/`pointMapVersion`/`requestedFrameIntervalMs`），没有给出"PortTraining↔Gateway控制管道"这条连接的hello应该携带什么字段。
- 选择：控制管道的`protocol.hello`只使用通用消息包络（V1.1第4节）本身的字段，`payload`允许为空对象`{}`，不额外要求任何控制管道专属字段。
- 替代方案：
  1. 照搬第10.1节实时管道的字段集合——放弃，因为那些字段（`sessionToken`/`leaseId`/`sceneInstanceId`等）语义上属于"场景实例接入一次具体运行"，控制管道握手时这些概念大多还不存在（还没有运行、没有租约）。
  2. 自创一套控制管道专属握手字段（如`callerRole`/`callerVersion`/`supportedProtocolVersions`）——放弃，当前没有具体业务需求驱动这些字段，属于超前设计。
- 后果：目前控制管道握手只做"消息类型是不是`protocol.hello`"这一层结构校验，不做调用方身份或版本协商。如果未来需要区分"是PortTraining在连接"还是"是别的什么进程在连接"，或需要协议版本协商，需要重新开一条决策并可能升级为破坏性变更。
- 生效日期：2026-09-07（首次实现于`GatewayControlPipeServer`初版）。
- 相关实现：`PortGatewayService/GatewayControlPipeServer.cs`（`protocol.hello`处理分支）。

### 2.2 `gateway.status`的payload字段名

- 背景：V1.1第11节只用文字描述`gateway.status`应包含"版本、PLC在线、点表、周期、数据年龄和故障"，不像其他大多数消息那样给出JSON示例。
- 选择：字段名逐一对应上述六个概念：`gatewayVersion`（string）、`plcOnline`（bool）、`pointMapVersion`（string，可省略）、`pollCycleMs`（number，可省略）、`dataAgeMs`（number，可省略）、`faults`（string数组）；另加`notes`（string，可省略）承载非结构化说明，不属于V1.1列出的六项概念之一，是本项目自行加的诊断信息。
- 替代方案：
  1. 套用通用HTTP健康检查惯例字段（如`status`/`uptime`/`checks`）——放弃，判断贴着V1.1原文列出的六个概念逐一命名，比套用外部惯例更容易被审查者对照原文核实。
  2. 把`faults`设计成对象数组（携带每个故障的错误码+详情）而不是纯字符串数组——本版本暂时选简单的字符串数组（如`"NO_PLC_CONFIGURED"`），需要更结构化的故障信息时再扩展，避免第一版就假设了后续未验证的使用方式。
- 后果：如果V1.1未来正式定义了这个payload的字段名或结构，需要按V1.1第19节的兼容规则判断是否构成破坏性变更（改名或改类型即破坏兼容，需升级主版本）。
- 生效日期：2026-09-07。
- 相关实现：`PortGatewayService/Protocol/GatewayStatusPayload.cs`。

### 2.3 `gateway.session.prepare`失败回复的消息类型名与payload结构

- 背景：V1.1第11节写明`gateway.session.prepare`成功时回`gateway.session.prepared`，但没有给失败情形命名对应的回复消息类型，也没有给出失败payload的结构（第15节的错误结构明确限定在"HTTP错误结构"标题下）。
- 选择：失败回复使用消息类型`gateway.session.rejected`，仿照V1.1第10.1节已经存在的`protocol.hello`→`protocol.accepted`/`protocol.rejected`这一对"成功用完成时、失败用被动式"命名先例；payload复用第15节HTTP错误结构里已经定义好的字段——`errorCode`（string，取值来自第15节错误码表）、`message`（string）、`retryable`（bool）——去掉HTTP专属的外层`{"error": {...}}`包装，因为Named Pipe消息本身已经有信封（`messageType`已经表达了这是一次拒绝），不需要再套一层。
- 替代方案：
  1. 不单独定义失败消息类型，复用`gateway.session.prepared`并加一个`success: false`字段——放弃，违反V1.1其余成对消息"用不同`messageType`表达不同结果"的一贯写法（`protocol.accepted`/`protocol.rejected`、`equipment.actionAck`里的`status`字段是同一motionType下的状态位，不是这里适用的先例）。
  2. 把第15节HTTP错误结构的外层`{"error": {...}}`包装原样保留——放弃，信封的`payload`字段本身已经承担了这层包装作用，重复包装没有必要。
- 后果：如果V1.1后续正式给`gateway.session.prepare`定义了失败消息类型名（可能不是`rejected`），需要修改这里的`messageType`字符串——这对已经依赖这个字符串的消费方是破坏性变更，需要走V1.1第19节的主版本升级流程协调。
- 生效日期：2026-09-07。
- 相关实现：`PortGatewayService/Protocol/GatewayErrorPayload.cs`、`GatewayControlPipeServer.cs`（`EvaluateSessionPrepare`分支）。

### 2.4 `gateway.session.prepare`请求里"请求租约时长"的字段名

- 背景：V1.1第11节文字要求`gateway.session.prepare`请求包含"请求租约时长"，但没有给出具体字段名。
- 选择：`requestedLeaseDurationMs`（number，整数毫秒）。字段名遵循V1.1第5.3节"持续时长统一使用整数毫秒字段后缀`Ms`"的既有规则；前缀`requested`用来明确这是调用方期望值，不代表Gateway最终必须按此签发（Gateway未来有权按自身策略缩短，当前实现尚未做这层裁决，直接按请求值签发）。
- 替代方案：`leaseTtlMs`、`leaseDurationMs`（不带`requested`前缀）——放弃，判断保留"requested"前缀能提醒未来读者"这是请求值不是保证值"，一旦以后加入Gateway自主裁决租约时长的逻辑，字段语义不需要改名。
- 后果：如果V1.1正式定义了不同字段名，需要迁移；由于当前没有任何外部消费方依赖这个字段名，此时机改名成本最低。
- 生效日期：2026-09-07。
- 相关实现：`PortGatewayService/Protocol/GatewaySessionPreparePayload.cs`。

### 2.5 `leaseEpoch`的递增语义

- 背景：V1.1第11节只说控制租约字段"至少包含"`leaseEpoch`，没有定义这个字段由什么事件递增、初始值是多少。
- 选择：`leaseEpoch`按"单个工位的租约世代计数器"实现——工位启动时（进程内存，非持久化）从0开始，每次成功签发一个新的`Reserved`租约（无论是因为工位此前从未被占用，还是此前的租约刚被释放或过期清理）时自增1，写入新租约。类比V1.1第2.2节`PortTrainingNodeService`侧`authorityEpoch`"每次权威更替递增"的用法。
- 替代方案：
  1. 用UTC时间戳代替世代计数器——放弃，违反V1.1第4节"进程内超时、租约和数据新鲜度必须使用单调时钟计算；UTC只用于跨系统审计和排序"的精神，时间戳也不满足"整数递增、可比较世代先后"这个更贴近"epoch"一词本意的语义。
  2. 每一次收到`gateway.session.prepare`请求（无论最终是否被拒绝）都递增——放弃，会让epoch膨胀速度和"真正建立的租约世代数"脱节，且无法用epoch差值判断"这中间到底成功换过几次租约"。
- 后果：当前`leaseEpoch`只保存在进程内存，`PortGatewayService`重启后计数器归零，不与之前的世代连续；如果未来需要跨重启保持epoch连续性（例如为了让PortTraining能区分"同一个Gateway进程内的第二次签发"和"Gateway重启后的第一次签发"），需要把这个计数器改为持久化存储，是一处已知的将来扩展点。
- 生效日期：2026-09-07。
- 相关实现：`PortGatewayService/GatewayControlPipeServer.cs`（`leaseEpochCounter`字段、`EvaluateSessionPrepare`）。

### 2.6 `gateway.lease.release`请求的字段契约

- 背景：V1.1提到`gateway.lease.release`/`gateway.lease.released`这对消息名（第11节"关键消息"列表），但没有给出请求本身需要携带哪些字段。
- 选择：只要求信封本身的`operationRunId`字段，与当前持有的租约的`operationRunId`做精确匹配；匹配则清空租约状态并回`gateway.lease.released`；不匹配（包括当前根本没有租约）时按连接层协议错误处理——记录日志并断开连接，不返回结构化拒绝消息。
- 替代方案：
  1. 额外要求请求携带`leaseId`做双重校验（`operationRunId`+`leaseId`同时匹配才放行）——本版本判断在当前"同一工位同时只有一个租约"的实现下，`operationRunId`已经能唯一确定要释放哪个租约，双重校验暂无必要；如果未来允许一个工位并发持有多个逻辑租约，需要重新引入`leaseId`校验。
  2. 释放不匹配时也返回结构化拒绝（类似`gateway.session.rejected`）——本版本判断这是简化实现，先满足"能正确释放"这个主路径，结构化拒绝留给下一次迭代；已在README和本记录中明确标注为已知的粗糙点，不是遗漏。
- 后果：调用方如果对一个不存在或不属于自己的租约发起释放，目前只会看到连接被断开，拿不到明确的错误码（比如无法区分"没有权限"和"根本没有这个运行"）；如果`PortTraining`一侧需要更精细的错误反馈，需要回来升级这条路径为结构化拒绝。
- 生效日期：2026-09-07。
- 相关实现：`PortGatewayService/GatewayControlPipeServer.cs`（`gateway.lease.release`分支）。

### 2.7 控制管道Windows ACL里"受信服务账号"的落地范围

- 背景：V1.1第17节明确要求"Named Pipe设置Windows ACL，仅允许当前交互用户、受信服务账号和管理员访问"——这一条本身不是空白，是明确要求；空白在于"受信服务账号"具体指哪个账号，V1.1没有给出`PortGatewayService`的部署形态（是否作为Windows Service运行、用什么账号运行）。
- 选择：当前只对Named Pipe的ACL显式授予两类主体`FullControl`——运行`PortGatewayService`进程的当前Windows用户（`WindowsIdentity.GetCurrent().User`）和`BUILTIN\Administrators`组；不额外添加任何"服务账号"规则，因为本项目目前是普通控制台进程，没有Windows Service部署形态，也就没有一个具体的服务账号身份可以引用。
- 替代方案：
  1. 保持.NET默认的Named Pipe安全描述符、不显式设置ACL——放弃，这样无法确认默认ACL是否满足V1.1"仅允许"这条限制性要求，属于把安全边界交给运行时默认值这个不可控因素。
  2. 提前虚构一个服务账号SID占位（例如硬编码`NT SERVICE\PortGatewayService`）——放弃，这个账号目前根本不存在（没有对应的Windows Service安装动作），提前写入一个不存在账号的规则没有实际保护作用，反而可能造成"以为已经配置好服务账号权限"的误解。
- 后果：如果`PortGatewayService`未来确定以Windows Service形式部署并使用专属服务账号运行，需要回来在`GatewayPipeSecurity.BuildDefault()`里补一条该账号的`PipeAccessRule`；在此之前，只有"运行这个控制台进程的那个Windows用户本人"和管理员能连接这条控制管道。
- 生效日期：2026-09-08。
- 相关实现：`PortGatewayService/Security/GatewayPipeSecurity.cs`。

### 2.8 `gateway.lease.release`失败改为结构化拒绝（升级自2.6条）

- 背景：2.6条记录的"不匹配时断开连接、不返回结构化拒绝"是当时明确标注的简化实现，替代方案2已经预告"结构化拒绝留给下一次迭代"。本条就是那次迭代，不是推翻2.6条的选择本身（"只按`operationRunId`匹配"这个校验规则没变），只是把失败结果的呈现方式从"断开连接"升级为"结构化消息"。
- 选择：新增消息类型`gateway.lease.rejected`，payload复用`GatewayErrorPayload`（`errorCode`/`message`/`retryable`，与`gateway.session.rejected`同一套结构）；`errorCode`固定为`OPERATION_RUN_MISMATCH`——这是V1.1第15节已经定义好的错误码（"消息不属于当前活动运行"，不重试，丢弃并审计来源），语义与"释放请求的`operationRunId`对不上当前租约"完全吻合，不需要新造错误码；`retryable=false`，与该错误码在标准里的定义一致。
- 替代方案：
  1. 继续保持2.6条"断开连接"的行为不变——放弃，因为已经在决策记录和README里明确承诺过这是临时简化，不实现会让文档和代码永久不一致。
  2. 新造一个专属错误码（例如`LEASE_NOT_FOUND`）而不是复用`OPERATION_RUN_MISMATCH`——放弃，`OPERATION_RUN_MISMATCH`的字面定义已经精确覆盖这个场景，新造错误码会制造两个含义重叠的码，增加其他软件的判断分支且没有实际收益。
- 后果：`gateway.lease.release`现在和`gateway.session.prepare`一样，失败时总是能拿到结构化错误而不是被动的连接断开；`PortGatewayService/README.md`"明确没有实现"列表里对应的粗糙点描述需要同步更新（已同步）。
- 生效日期：2026-09-09。
- 相关实现：`PortGatewayService/GatewayControlPipeServer.cs`（`gateway.lease.release`分支，`ImplementationVersion`升到`...-v4`）。

### 2.9 场景实时管道`protocol.rejected`的payload结构

- 背景：V1.1第10.1节明确点名了失败回复的消息类型（"返回protocol.accepted或protocol.rejected"），但和控制管道的`protocol.hello`一样，没有给出`protocol.rejected`的payload字段。
- 选择：复用`GatewayErrorPayload`（`errorCode`/`message`/`retryable`），与`gateway.session.rejected`/`gateway.lease.rejected`同一套结构，不新造payload类型。
- 替代方案：为实时管道单独设计一套payload（例如额外携带`streamId`）——放弃，当前握手失败阶段Gateway还没有接受任何`streamId`，没有额外信息值得携带，保持和其他`.rejected`消息同一结构更利于`PortTraining`/场景EXE统一处理错误。
- 后果：所有`.rejected`类消息（`protocol.rejected`、`gateway.session.rejected`、`gateway.lease.rejected`）现在都用同一个`GatewayErrorPayload`结构，客户端可以写一套通用的错误解析逻辑。
- 生效日期：2026-09-09。
- 相关实现：`PortGatewayService/GatewayRealtimePipeServer.cs`。

### 2.10 场景实时管道hello校验失败统一收敛为`AUTHENTICATION_FAILED`

- 背景：V1.1第10.1节要求Gateway验证令牌哈希、租约、场景实例、机型和点表，但没有说明"验证的具体哪一项失败"应该对应第15节错误码表里的哪个码——`AUTHENTICATION_FAILED`（"身份认证失败"）是最贴近的现成码，但表里没有专门为"leaseId不存在"或"sceneInstanceId不符"设计的码。
- 选择：不区分"当前工位根本没有租约"、"leaseId/operationRunId/sceneInstanceId与当前租约不符"、"sessionToken哈希不匹配"这三种子情况，统一返回`AUTHENTICATION_FAILED`（不重试）。
- 替代方案：为每个子情况单独造一个错误码（例如`LEASE_NOT_FOUND`/`SCENE_INSTANCE_MISMATCH`）——放弃，这三种情况下客户端的正确应对都是同一个（回到`gateway.session.prepare`重新走一遍流程），区分错误码对客户端行为没有实际差异，反而增加错误码表的维护成本；且过细的错误提示本身也可能泄露"是租约存在但token不对"还是"租约根本不存在"这类可被利用的信息，笼统为认证失败更保守。
- 后果：如果后续`PortTraining`或场景EXE确实需要区分这几种情况做不同的重试/提示策略，需要回来拆分这个码；目前的实现和自测试都只验证"结果是`AUTHENTICATION_FAILED`"，不验证具体子原因（子原因仍然写在`message`字段的人类可读文本里，供日志排查用，不作为程序可判断的契约）。
- 生效日期：2026-09-09。
- 相关实现：`PortGatewayService/GatewayRealtimePipeServer.cs`（`EvaluateHello`方法）。

### 2.11 场景实时管道hello的`machineType`只做结构校验，不交叉核对

- 背景：V1.1第10.1节要求hello携带`machineType`并暗示Gateway应该验证它，但`gateway.session.prepare`的请求字段（第11节）里根本没有`machineType`，导致没有任何"这个工位/这次运行的机型应该是什么"的既有记录可以拿来交叉核对。
- 选择：只校验`machineType`落在`MachineType`枚举（`QC`/`YC`）范围内，不与租约、工位配置或任何其他记录做交叉比对。
- 替代方案：在`gateway.session.prepare`里补一个`machineType`字段，让两处握手可以交叉核对——放弃，这会改变已经实现并写入决策记录2.x条目的`gateway.session.prepare`契约，属于范围更大的改动，且`StationProfile.machineType`（第18.1节）本身也还没有实现，交叉核对的"基准值"目前并不存在，本次不顺带扩大范围。
- 后果：一个场景EXE理论上可以用`YC`的租约配合`machineType=QC`的hello握手成功，Gateway当前不会拒绝——这是一个真实的校验空白，不是遗漏而是明确推迟，因为修复它需要先把`StationProfile`或`gateway.session.prepare`的契约扩展这个更大的问题解决。
- 生效日期：2026-09-09。
- 相关实现：`PortGatewayService/GatewayRealtimePipeServer.cs`（`EvaluateHello`方法）。

### 2.12 `gateway.lease.released`的payload结构（整体复查中发现的遗漏）

- 背景：2026-09-09做一次跨条目整体复查时，逐条把决策记录里提到的每个消息类型和实际代码字段核对了一遍，发现`gateway.lease.released`的成功回复payload此前一直是一个匿名对象`{ leaseId = ... }`直接内联在`GatewayControlPipeServer.cs`里，是本文件目前唯一一处**没有命名类型、也没有决策条目**的payload——2.6/2.8条只记录了释放请求的字段契约和失败回复的结构，漏了成功回复本身。自测试当时也只检查了`messageType`是不是`gateway.lease.released`，没有检查payload内容，所以这个遗漏一直没被测试暴露出来。
- 选择：新增命名类型`GatewayLeaseReleasedPayload`（`leaseId`：string），替换原来的匿名对象，和其余所有消息payload保持"每个payload都有命名类型+决策记录条目"的一致做法。
- 替代方案：继续留着匿名对象——放弃，这次整体复查的目的就是消除这类"没留痕迹"的空白，匿名对象虽然序列化结果一样，但没有类型意味着以后如果要给这个payload加字段，容易漏改或者散落成多处不一致的匿名写法。
- 后果：`gateway.lease.released`的wire格式（`{"leaseId": "..."}`）没有变化，纯粹是代码内部从匿名类型换成命名类型，属于零行为影响的重构；同时把自测试也补上了对`leaseId`字段值的校验（之前只查了消息类型，没查payload内容）。
- 生效日期：2026-09-09。
- 相关实现：`PortGatewayService/Protocol/GatewayLeaseReleasedPayload.cs`（新增）、`GatewayControlPipeServer.cs`（`gateway.lease.release`分支）。

### 2.13 场景实时管道`streamId`重复使用的拒绝与错误码（补齐第3节此前列出的空白）

- 背景：V1.1第3.2/10.1节要求"新生成的streamId；每次Named Pipe重连都必须改变"，但只是对调用方的行为要求，没有说明Gateway是否需要主动校验、拒绝时用哪个错误码——第3节此前把这个问题列为"待评估"的已知空白。
- 选择：`GatewaySessionState.ActiveLeaseRecord`新增`LastAcceptedStreamId`，记录该租约上一次握手成功接受的`streamId`；下一次`protocol.hello`如果携带相同的`streamId`，视为违反"必须改变"这条要求，拒绝，错误码沿用2.10条已经确定的`AUTHENTICATION_FAILED`（不新造错误码），理由和2.10条一致——客户端的正确应对都是"生成一个真正新的streamId重试"，没有必要为这一种子情况单独区分。
- 替代方案：
  1. 不做这个校验，完全信任客户端——放弃，V1.1用"必须"这个措辞，且这条规则的意义正是防止旧连接的帧被误当成新连接的帧处理，纯客户端自律没有实际约束力。
  2. 为"streamId重复"单独造一个错误码（例如`STREAM_ID_REUSED`）而不是复用`AUTHENTICATION_FAILED`——放弃，理由同2.10条：细分错误码对客户端行为没有实际差异，且会打破"这一类hello校验失败统一收敛"的既有一致性。
- 后果：租约的`LastAcceptedStreamId`只在进程内存里，`PortGatewayService`重启后归零（与`leaseEpoch`归零是同一类已知限制，见2.5条）；只校验"是否等于上一次"，不维护完整的历史`streamId`集合，如果未来需要拒绝"任意一个历史用过的streamId"而不只是"最近一次"，需要扩展这个字段为集合。
- 生效日期：2026-09-09。
- 相关实现：`PortGatewayService/GatewaySessionState.cs`（`ActiveLeaseRecord.LastAcceptedStreamId`）、`GatewayRealtimePipeServer.cs`（`EvaluateHello`方法，`ImplementationVersion`升到`...-v2`）。

### 2.14 Unity端手动实现PT-IF-007协议代码，不等`PortTraining.Contracts`

- 背景：V1.1第4节要求跨软件协议序列化统一走`PortTraining.Contracts`，且明确禁止Unity`JsonUtility`直接序列化协议消息；但`PortTraining.Contracts`本身`未实现`。2026-09-11因为"明天要跟PLC和Unity程序对接"需要今天就能验证Unity↔Gateway这条腿，不能再等`PortTraining.Contracts`落地。
- 选择：在`YardCraneSimulator/Assets/YardCraneSimulator/Runtime/Gateway/`下手写一份**只覆盖PT-IF-007当前已实现部分**（`protocol.hello`/`protocol.accepted`/`protocol.rejected`/`equipment.inputFrame`）的最小协议代码，包括一个不依赖任何包的最小JSON读写器（`MiniJson.cs`）——写之前核实过这个Unity工程的`Packages/manifest.json`里没有`Newtonsoft.Json`，`apiCompatibilityLevel`是`NetStandard`，也没有内置`System.Text.Json`，全项目搜索不到任何地方已经在用这两个库。
- 替代方案：
  1. 现在就把`PortTraining.Contracts`做出来，两边一起用——放弃，这是一个更大范围的独立任务（需要决定包的分发方式、目标框架兼容性等），今天的时间不够，且会议题拖到明天联调之前完不成。
  2. 给Unity工程加`Newtonsoft.Json`包依赖，用它做JSON——放弃，需要改`Packages/manifest.json`，而这个文件目前没有被任何并行会话触碰，改它是可以接受的最小风险选项，但引入一个新包依赖本身超出了"今天只是验证连通性"这个目标的必要范围，且手写JSON处理器代码量不大、已经用真实进程间通信验证过正确性，没有必要为此新增依赖。
  3. 用`UnityEngine.JsonUtility`——直接违反V1.1第4节明文禁止，排除。
- 后果：这份Unity端协议代码和`PortGatewayService`里的C#实现是两份独立维护的代码，字段名靠人工保持一致（已交叉核对过一次，见验证记录）；`PortTraining.Contracts`真正落地后，理论上应该让Unity这边改为引用它、删掉这份手写实现——这是明确的技术债，不是最终形态。这份代码只覆盖握手和`equipment.inputFrame`单向流，不覆盖`equipment.action`/`equipment.actionAck`/`SimulatorFeedbackFrame`（因为Gateway那边这些也还没实现）。
- 生效日期：2026-09-11。
- 相关实现：`YardCraneSimulator/Assets/YardCraneSimulator/Runtime/Gateway/`（`MiniJson.cs`、`GatewayPipeFraming.cs`、`GatewayRealtimeMessages.cs`、`GatewayRealtimeDebugClient.cs`）、`YardCraneSimulator/Assets/YardCraneSimulator/Editor/YardCraneGatewayDebugWindow.cs`、`YardCraneSimulator/Tools/GatewayDebugClientHarness/`、`YardCraneSimulator/Tools/GatewayRealtimeManualClient/`、`PortGatewayService/Tools/ManualSessionLauncher/`。

### 2.15 S7驱动接入：先做连接生命周期抽象，不在今天锁定具体库

- 背景：`PortGatewayService/README.md`一直把"真实S7驱动/库选型"列为"尚待确定、需PoC后才能锁定"；2026-09-11用户明确"继续做"S7驱动骨架，但同一天已经确认拿不到真实PLC型号/固件信息（要等第二天现场）。这意味着即使今天选定一个库（如S7NetPlus/Sharp7），也完全无法用真实硬件验证它的实际行为，等于是在没有验证能力的情况下做一次事实上的"锁定"。
- 选择：新增`IS7Client`接口（`ConnectAsync`/`Disconnect`/`IsConnected`/`ReadBytesAsync`/`WriteBytesAsync`），把"连接生命周期"（重试、退避、超时、诚实上报在线状态）和"具体用哪个S7库"彻底解耦；`RealS7DeviceFrameSource`只依赖这个接口，用一个可编程控制"何时连接失败/何时掉线"的假实现（`FakeS7Client`，仅存在于`Tools/S7DeviceFrameSourceHarness/`测试项目里）验证生命周期逻辑本身。今天不添加任何真实S7驱动的NuGet包引用。
- 替代方案：
  1. 现在就加一个真实S7库（如S7NetPlus）并锁定——放弃，没有真实PLC/固件信息，锁定这个决定本身缺乏PoC依据，且"README已经写明需要PoC才能锁定"这条规则不应该因为"手头没有硬件、干脆先随便定一个"而被绕过。
  2. 完全不碰S7相关代码，等明天拿到PLC信息再一次性做——放弃，这样会浪费今天原本可以提前做、且确实和"选哪个库"无关的连接生命周期/重试/退避逻辑的开发和测试时间，明天现场时间通常更紧张，能提前做的应该提前做。
- 后果：`RealS7DeviceFrameSource`目前只验证了连接生命周期（重试、退避、掉线重连、取消响应性、`appsettings.json`解析），完全没有验证过任何真实字节级S7读写、也没有连接过任何真实或模拟的S7设备；也没有接入`GatewayControlPipeServer`/`GatewayRealtimePipeServer`（这两个服务仍然用`SimulatedDeviceFrameSource`）。真正选定库时只需要写一个实现`IS7Client`的薄适配器类，不需要改动这次写的生命周期逻辑。
- 生效日期：2026-09-11。
- 相关实现：`PortGatewayService/S7/`（`IS7Client.cs`、`S7ConnectionOptions.cs`、`RealS7DeviceFrameSource.cs`）、`PortGatewayService/Tools/S7DeviceFrameSourceHarness/`。

### 2.16 PLC网络端点（IP/Rack/Slot）配置临时放进`appsettings.json`的`s7`块

- 背景：V1.1第18.1节`StationProfile`必须包含的字段列表和第18.4节`PointMapManifest`必须包含的字段列表，都没有提到PLC的网络地址（IP）；`StationProfile`唯一相关的是一个未展开定义的`plcProfileId`引用。这意味着"PLC连接端点应该存在哪个配置对象里"本身是V1.1没有回答的问题，不只是"字段名"层面的空白。
- 选择：临时把`ipAddress`/`rack`/`slot`/`connectTimeoutMs`/`retryBackoffMs`放进`PortGatewayService`自己的`appsettings.json`一个新的`s7`块里（`S7ConnectionOptions`），和已有的`stationId`/`allowSimulationFallback`同一份文件、同一种最小化手写解析方式（不引入配置框架）。
- 替代方案：把这些字段塞进已有的[点表配置格式草案](PortGatewayService点表配置格式草案-v0.1.md)的`plc`对象里（草案里已经有`model`/`firmwareBaseline`/`rack`/`slot`/`dbVersion`）——放弃，点表描述的是"PLC内存里有什么、怎么读写"，是一份可能需要签名分发、随点位表版本管理的制品；网络端点更像是"这台Gateway进程部署在哪里、连哪个IP"这种部署时配置，混进点表文件会让"点表版本变化"和"部署环境变化"这两件不相关的事绑在一起，之后如果要给点表文件签名（第18.4节要求`发行方签名`），部署环境信息也不应该被这份签名覆盖。
- 后果：这是一个未来可能需要推翻重来的临时选择——如果V1.1后续正式定义了`plcProfileId`对应的"PLC Profile"是什么、或者`StationProfile`补充了网络端点字段，`S7ConnectionOptions`这部分应该迁移过去，不应该继续独立存在于`PortGatewayService`自己的配置文件里。
- 生效日期：2026-09-11。
- 相关实现：`PortGatewayService/S7/S7ConnectionOptions.cs`、`PortGatewayService/appsettings.json`（新增`s7`块）。

### 2.17 S7驱动库暂定选为S7netplus，目标CPU为S7-1500（更正/细化2.15条，非推翻）

- 背景：2.15条记录的是"没有真实PLC/固件信息，不在2026-09-11当天锁定具体S7库"。2026-09-12用户明确"我们就按照西门子1500来进行对接就行"，确认了PLC**产品家族**（S7-1500），但仍然没有给出具体CPU型号、固件版本、真实IP或TIA点位表。这不是"新证据推翻旧结论"，而是2.15条本身就预留的触发条件之一（"等真实PLC型号确认后"）部分满足——家族已知，具体型号/固件仍未知。
- 选择：
  1. 选定NuGet包`S7netplus`（0.20.0，https://github.com/S7NetPlus/s7netplus ，MIT协议）作为S7驱动库，写`IS7Client`的真实适配器`S7NetPlusClient`。写代码前先直接读了该库GitHub仓库的源码（`PLC.cs`/`PlcAsynchronous.cs`/`Enums.cs`）确认构造函数签名、`CpuType.S71500`、`DataType.DataBlock`、`OpenAsync`/`Close`/`IsConnected`/`ReadBytesAsync`/`WriteBytesAsync`的真实签名，不是凭记忆猜的；用真实NuGet还原+编译验证了这些签名确实对得上。
  2. 这是本项目第一个、也是目前唯一一个外部NuGet依赖——`PortGatewayService.csproj`此前及其余全部代码都保持零依赖，这里破例的理由是S7是真实工业协议（COTP+S7 PDU分帧），在没有真实硬件验证能力的情况下手写这套协议风险远高于本项目其余"仓库自己拥有、可以自己测"的协议代码（如PT-IF-007的JSON帧），选用一个有1.8万+版本累计下载量的成熟开源库更负责任。
  3. **明确标注"provisional，不是2.15条意义上的最终锁定"**——`RealS7DeviceFrameSource`/`GatewayControlPipeServer`/`GatewayRealtimePipeServer`都还没有引用`S7NetPlusClient`；真正的"锁定"仍然要等明天现场用真实PLC完成PoC（能连上、能读写、行为符合预期）之后才算数，本条决策只是"选了一个候选去做PoC"，不是"PoC已经通过"。
  4. 记录一条对明天现场有直接操作意义的技术约束：S7NetPlus（和它所基于的经典Snap7协议一样）**读不到TIA Portal里默认开启"优化的块访问"（Optimized block access）的DB块**——这不是本会话的推测，是该库自己GitHub Issue列表里明确存在的问题（"Support for Optimized block Access for S7-1500?"、"Is there any way to access the optimized DB blocks (symbolically)???"）。明天现场如果要让这条驱动链路真正读到数据，负责TIA Portal配置的人需要把Gateway要读写的DB块的"优化的块访问"勾选关掉，改成标准（绝对）寻址。
- 替代方案：
  1. 继续保持"不选任何库，等具体型号/固件/IP都到位再选"——放弃，产品家族这个信息本身已经足以缩小选择范围并开始写一个真实、可编译、可用假目标验证连接失败路径的适配器，没有必要为了"型号还没到位"这一个理由把所有相关工作都推迟到明天。
  2. 选`Sharp7`（Snap7的C#移植版）而不是`S7netplus`——两者都支持S7-1500、都有同样的"优化访问"限制，`S7netplus`的选择理由是：纯C#无原生依赖（`Sharp7`底层是P/Invoke调用原生Snap7库，需要额外部署一个平台相关的原生DLL/so，在这个仓库"尽量少额外部署产物"的整体风格下不如纯托管代码简单）、异步API（`OpenAsync`/`ReadBytesAsync`/`WriteBytesAsync`）天然贴合本项目已经全面异步化的代码风格、方法签名和已有的`IS7Client`接口几乎一一对应。
  3. 现在就把点表草案的占位值和这个选择关联起来（比如往草案里加"驱动库=S7netplus"字段）——放弃，点表草案描述的是PLC内存布局，不是软件实现选型，两者不应该混在一份文件里，这条选型只需要记在决策记录里。
- 后果：`PortGatewayService.csproj`从"零外部依赖"变成"一个外部依赖"，`dotnet restore`现在需要能访问`nuget.org`（本机已验证可访问）；明天如果PoC发现S7netplus在目标固件版本上有问题，需要回来在这条决策基础上追加更正记录，而不是假装2.15/2.17条从未存在过。
- 生效日期：2026-09-12。
- 相关实现：`PortGatewayService/PortGatewayService.csproj`（新增`PackageReference Include="S7netplus" Version="0.20.0"`）、`PortGatewayService/S7/S7NetPlusClient.cs`（新增）、`PortGatewayService/S7/S7ConnectionOptions.cs`（`Slot`默认值从占位的`1`改成S7netplus文档确认的S7-1200/1500默认值`0`）、`PortGatewayService/Tools/S7DeviceFrameSourceHarness/`（新增用真实库连不存在目标的测试）。

## 3. 尚未记录、需要以后补充决策的已知空白

以下空白已经在`PortGatewayService/README.md`"尚待确定"提及，但因为还没有实际写代码固化任何选择，暂不在本文件建立正式条目，等到真正实现时再补：

- `gateway.session.activate`/`gateway.session.active`：是控制管道上PortTraining主动发起的一条消息，还是场景侧实时管道握手（PT-IF-007）本身触发的效果，V1.1原文两种读法都说得通。2026-09-08已与用户确认：暂不推进这部分实现，等后续需要时再决定方向，因此暂不在此建立决策条目。租约目前永远停留在`Reserved`，就是这个决定的直接后果。
- `equipment.action`/`equipment.actionAck`（动作去重、执行阶段、互锁校验）和`SimulatorFeedbackFrame`（场景→Gateway方向）尚未实现——2026-09-09只做了`protocol.hello`握手和`equipment.inputFrame`单向流，具体落地细节届时如有偏离V1.1文字的空白，再追加条目。
- ~~`streamId`"每次重连必须改变"目前只是文档要求~~——2026-09-09已实现并记入2.13条，从本清单移除。

## 4. 维护规则

- 每新增一处需要本项目自行选择的空白，在"2. 决策条目"追加一条，编号顺延，不修改已有编号。
- 已经记录的选择如果后续变更（例如V1.1正式吸收、或发现原选择有问题），追加新条目并在其中注明"更正自2.x条目"，不得直接编辑或删除旧条目内容（对应根`CLAUDE.md`"若历史结论与新运行证据冲突，追加更正记录，不静默覆盖"）。
- 本文件由`PortGatewayService`开发过程驱动，但记录的是跨软件消费方也需要知道的协议选择，因此放在LM-Wiki知识层而不是`PortGatewayService/`本地目录，方便`PortTraining`、场景EXE等其他软件的开发者查阅。
