# PortGatewayService点表配置格式草案 V0.1

> 2026-09-15 范围更正：本文件是历史Gateway点表草案，不用于当前`YardCraneS7Integration`场景配置。场桥直连的IP、Rack、Slot和绝对地址必须按[场桥S7直连接入架构决策-2026-09-15.md](场桥S7直连接入架构决策-2026-09-15.md)重新取得、校验和运行验收，不能直接沿用本文占位值或旧Schema。

- 状态：**草案**，字段名未最终锁定，等待真实PLC型号、固件基线和TIA Portal点位表确认后才能升级为正式版本。
- 生效范围：仅`PortGatewayService`内部使用，不是`跨软件接口与参数标准`的一部分，不需要`PortTraining`/场景EXE等其它软件感知这份文件的具体字段。
- 对应`跨软件接口与参数标准-v1.1.md`第18.4节`PointMapManifest`：该节只有一句文字描述（"必须包含PLC型号、固件基线、Rack/Slot、DB版本、读取区、白名单写入区、数据类型、缩放、死区、语义字段、默认安全值、配置SHA-256和发行方签名"），没有给出字段名或JSON示例——这份草案是本项目对这句话的具体落地尝试，不是V1.1原文本身。
- 配套文件：
  - [point-map-manifest.schema.json](point-map-manifest.schema.json)——JSON Schema（draft-07）。
  - [point-map-manifest.example.json](point-map-manifest.example.json)——一份完全用占位值填充的示例实例，用于验证Schema本身可用，不代表任何真实PLC点位。

## 1. 为什么现在只能是草案

V1.1第3.3节要求"真实S7库必须在目标PLC型号、固件和TIA配置上完成PoC后锁定"；点表本身是S7通信的直接映射（PLC地址、DB号、偏移只存在于点表配置中），在没有确认真实PLC型号/固件/TIA点位表之前，任何具体的DB号、偏移、Rack/Slot都只能是占位值。这份草案的目的是先把**结构**定下来、把每个字段的**设计依据**追溯到V1.1原文，等真实硬件信息到位后，只需要替换占位值、不需要重新设计结构。

## 2. 字段设计依据（逐条追溯V1.1原文）

| 本草案字段 | V1.1依据 | 说明 |
| --- | --- | --- |
| `manifestVersion` | 无直接对应，本项目新增 | 区分"这份点表文件格式本身的版本"（这份草案是`0.1.0`）和下面`pointMapVersion`（"这份点表内容的版本"），避免两种版本号混淆。 |
| `pointMapVersion` | 第18.1节`StationProfile.pointMapVersion`、第7.1节`WorkOrderSnapshot.pointMapVersion`、第11节`gateway.session.prepare`payload、第15节`POINT_MAP_VERSION_MISMATCH`错误码 | 沿用V1.1已经在多处使用的命名和格式（如`QC-IO-1.0.0`），这份点表的版本号必须能被其它已经引用`pointMapVersion`字段的消息比对。 |
| `machineType` | 第6节公共枚举`MachineType: QC \| YC` | 直接复用已有枚举，不新造。 |
| `plc.model`/`firmwareBaseline`/`rack`/`slot`/`dbVersion` | 第18.4节原文逐字对应 | 全部为占位值，见第4节"待确认事项"。 |
| `readableRegions`（含`dbNumber`/`startByte`/`lengthBytes`） | 第3.3节"必须批量读写连续数据区，禁止按每个点位建立独立请求" | 用"区域"（连续字节范围）而不是"逐点位列表"组织，直接对应这条批量读写约束——一个区域就是一次批量S7读请求。 |
| `writableWhitelistRegions` | 第10.3节"Gateway只允许向点表白名单写入反馈字段。评分和训练档案字段不得写入PLC" | 与`readableRegions`结构相同，但语义上是独立的白名单，本草案的校验脚本专门检查这个区域里不能出现评分/训练相关字段名（见第3节）。 |
| `point.semanticField` | 第10.2节`EquipmentInputFrame`、第10.3节`SimulatorFeedbackFrame`字段列表 | 点表的语义字段应该直接对应这两个消息里已经定义的字段名（如`emergencyStopReleased`/`controlPowerOn`/`handlesCentered`/`gantryCommand`/`trolleyCommand`/`hoistCommand`/`hornPressed`），让点表和已经定义好的wire协议保持同一套命名，而不是另起一套。 |
| `point.dataType` | 第18.4节"数据类型" | 使用S7惯用的原始类型名（`Bool`/`Byte`/`Int16`/`Int32`/`Real`），不绑定某个具体S7驱动库（PortGatewayService尚未选型S7驱动，见`PortGatewayService/README.md`"尚待确定"）。 |
| `point.scale`/`deadband` | 第18.4节"缩放"、"死区" | 原文逐字对应；`deadband`用于避免带噪声的模拟量在阈值附近抖动触发"数据新鲜度"相关逻辑。 |
| `point.unit` | 第5.4节工程单位表 | 只是人类可读注释，不是真正的字段名后缀规则本身——字段名后缀规则体现在`semanticField`直接沿用wire协议已有命名（如`gantryCommand`本身就是V1.1规定的归一化手柄字段，取值范围`-1.0~1.0`）。 |
| `safeDefaults` | 第18.4节"默认安全值" | 键必须是已经在`readableRegions`/`writableWhitelistRegions`声明过的`semanticField`，脚本已验证这条约束（见第3节）。 |
| `configSha256`/`signatureAlgorithm`/`keyId`/`signature` | 第18.4节"配置SHA-256和发行方签名"、第17节"SceneManifest、PointMapManifest...使用ECDSA-P256-SHA256签名，并通过keyId支持密钥轮换" | `signatureAlgorithm`直接锁定为`ECDSA-P256-SHA256`，与`OfflineExecutionGrant`（第7.3节）用的是同一套签名字段命名（`signatureAlgorithm`/`keyId`/`signature`），保持跨文档一致。 |

## 3. 已经验证的部分（本机静态校验，非Unity/S7运行验证）

用Node.js（`v24.13.1`，本机已安装，未新增依赖）写了一个手工校验脚本（未纳入仓库，属于验证过程的临时脚本），对`point-map-manifest.example.json`做了以下检查，全部通过（`[POINT_MAP_MANIFEST_DRAFT_VALIDATION_PASS] checks=ok, readFields=8, writeFields=2`）：

1. Schema文件和示例文件均为合法JSON。
2. 示例文件包含Schema要求的全部顶层必填字段，且没有Schema之外的多余顶层字段。
3. `machineType`、`signatureAlgorithm`、每个`point.dataType`都落在各自的枚举范围内。
4. 每个区域（`readableRegions`/`writableWhitelistRegions`）和每个点位都包含各自的必填字段；`dataType=Bool`的点位都带有合法的`bitIndex`（0-7）。
5. 同一区域组内没有重复的`semanticField`。
6. **`writableWhitelistRegions`里没有任何字段名包含`score`/`training`/`result`/`grade`/`assessment`**——直接验证第10.3节"评分和训练档案字段不得写入PLC"这条规则在当前示例里成立。
7. `safeDefaults`的每个键都能在已声明的`semanticField`集合里找到对应项，不存在"给一个不存在的字段设默认值"的悬空引用。

这些校验只能证明"这份草案文件内部结构自洽、且遵守了V1.1里能被机器检查的规则"，**不能证明**：真实PLC支持这些DB号/偏移/数据类型、真实S7驱动能正确解析这份配置、或者这套区域划分方式在真实网络条件下真的能满足"20毫秒批量轮询"的性能目标。

## 4. 待确认事项（锁定前必须解决）

1. 真实PLC型号、固件版本、TIA Portal点位表——目前`plc.model`/`plc.firmwareBaseline`/`plc.dbVersion`全部是占位字符串。
2. 真实的Rack/Slot/DB号/字节偏移——目前示例里的`dbNumber=10`/`20`、各偏移量都是虚构值，不对应任何真实硬件。
3. 真实S7驱动库选型（本身也在`PortGatewayService/README.md`"尚待确定"列表里）——`dataType`枚举目前只是S7通用类型名，选定驱动库后需要确认这些名字能不能直接映射到库自己的类型系统，或者需要一层转换。
4. `configSha256`/`keyId`/`signature`的签发流程——目前只是占位字符串，谁来签、私钥怎么保管、`keyId`轮换机制都还没有设计，涉及第17节安全标准里"密码、API密钥和证书不得存入StreamingAssets、源码或普通JSON配置"这条约束，需要专门设计密钥管理方案，不能顺带在这份草案里简单带过。
5. `readableRegions`/`writableWhitelistRegions`目前只覆盖了`EquipmentInputFrame`/一小部分反馈指示灯的示例字段，`SimulatorFeedbackFrame`列出的位置、速度、锁销状态等字段是否也需要对应的PLC点位（例如驱动真实操作台上的物理仪表），需要和场桥/岸桥的真实操作台设计一起确认，本草案没有覆盖。
6. 这份文件由`PortGatewayService`在启动时如何加载、校验签名、处理版本不匹配（`POINT_MAP_VERSION_MISMATCH`）——目前`PortGatewayService`代码里还没有任何读取点表文件的逻辑（`GatewayControlPipeServer`目前完全不引用这份草案），是刻意的：先把格式定下来讨论，等格式确认后再写加载代码，避免在格式还会变的阶段就往真实调用路径里塞代码。

## 5. 不在本草案范围内

- 没有实现任何S7通信代码（读写PLC、驱动库选型）。
- 没有实现`PortGatewayService`加载/解析/校验签名这份文件的代码。
- 没有让`SimulatedDeviceFrameSource`（`PortGatewayService/Devices/`）读取这份点表——它目前仍然是硬编码的固定安全帧，和这份草案完全独立，是两件不同的事：草案是"未来真实S7要用的格式"，`SimulatedDeviceFrameSource`是"现在没有PLC时的开发占位"。
