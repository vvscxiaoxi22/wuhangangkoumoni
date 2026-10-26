# YardCraneSimulator 开发约束

## 工程定位

- 本工程是港口实训平台的场桥实时仿真场景，固定使用 Unity `6000.0.42f1`。
- 本工程只负责场桥三维仿真、设备状态、作业过程和运行证据，不承载学生管理平台、教师端、正式评分或 TOS 业务。
- 自2026-09-15起，S7 PLC通信由`YardCraneS7Integration`独立场景中的唯一game4automation `S7Interface`负责；`YardCrane`、学生端、教师端及其他场景不得另建S7连接。

## 开发前读取路由

- 先读`../CURRENT_STATUS.md`；连续任务且状态未变化时无需重复读取。
- 纯UI样式只读目标UXML/USS、引用素材及对应UI契约，不加载运动、PLC、协议等无关文档。
- 修改跨软件协议时才读`跨软件接口与参数标准-v1.1.md`；修改场桥运行架构时才读`项目开发基线-2026-08-28.md`。
- 当天讨论记录在继续历史证据或阶段收口时读取，连续小改动统一记录一次。

## 高频修改直达路径

- 学生首页：`Shell/Resources/YardCraneShell/StudentSide/Display1UI/StudentSideShell.uxml`与同目录USS。
- 教师首页：`Shell/Resources/TeacherSide/Display1UI`、`Display2UI`及`Shell/YardCraneTeacherSideShell.cs`。
- 驾驶HUD样式：`Shell/Resources/YardCraneUI/OperatorHud.uxml`、`OperatorHudStyles.uss`、`OperatorHudPanel.asset`。
- 驾驶HUD数据绑定：`Runtime/Presentation/YardCraneOperatorHud.cs` → `Experiments/ObiRope/YardCraneHudSceneAdapter.cs` → `YardCraneEightRopeMaxPrototypeRig.cs`。
- 已知路径的小改动直接进入目标，不再通过全仓搜索重新发现上述链路。

## 必须遵守

- 不自动升级 Unity 编辑器或工程格式。
- 跨软件消息模型最终统一引用 `PortTraining.Contracts`；不得在本工程另造一套同名契约。
- 跨软件协议不得使用 `JsonUtility`；它只允许用于本工程本地配置。
- 一次性令牌只从环境变量 `PORTTRAINING_SESSION_TOKEN` 读取，禁止写入日志、命令行、配置文件或崩溃信息。
- 场景不能直接把训练标记为正式完成，也不能直接修改学生成绩；只提交完成请求、事件链和本地估分。
- S7组件和点位对象必须由`YardCraneS7Integration`场景序列化持有，不得运行时动态创建；真实IP、Rack、Slot、点表和安全链未确认前接口保持禁用。
- PLC原始输入必须先转换为统一操作语义，再进入现有运动控制链；不得由PLC点位直接修改Transform、Rigidbody或绳索状态。大车、小车伺服指令原始量程为`-3000..3000`，限幅后归一化到`-1..1`；PLC数据类型和绝对地址仍以最终点表为准。
- L2/L3任务的新需求、架构决策、问题和验证结果必须追加到当天讨论记录；连续L1视觉微调在一批工作收口时统一追加。
- 微型L2若只复用既有状态、未改变状态机/物理判定，可先在`CURRENT_STATUS.md`保留一条结果并与同批改动合并记录；不为单个小改同时更新状态页、设计包和讨论记录。

## 分级验证底线

- 仅USS/UXML样式变化：执行样式语法、资源路径、节点绑定静态检查；画面交给用户预览确认，不触发C#全编译和完整Play。
- 局部C#变化：使用对应Harness或目标程序集编译；影响运行交互时补充聚焦Play。
- 场景、登录、训练状态、运动或发布变化：使用 Unity `6000.0.42f1` 执行真实入口Play验证。
- 启动参数、协议版本、machineType、配置文件和 Bootstrap 场景必须通过启动校验。
- 涉及 PLC 或运动控制时，必须覆盖断连、陈旧帧、急停、租约失效和安全释放路径。
- S7直连验收必须覆盖连接失败、断连、重连、通信超时、陈旧数据、急停、安全归零和显式恢复；未取得真实PLC证据前不得进入正式考核或发布。
- 编辑器有未保存场景时保留现场并标记`运行待验证`；不得自动保存、丢弃或复制数GB工程作为默认替代方案。

## 任务交付门禁

- 每个任务按风险使用快速通道、聚焦验证或完整门禁；不得把纯样式修改升级成发布级验证。
- Unity编译成功只证明源码可编译；运动、绳索、碰撞和交互改动必须取得对应Play运行日志和观察结果后才能标记为`已验证`。
- 如果只能完成编译而尚未Play，状态必须写为`实现完成 / 运行待验证`，不得写为`已完成`。
- 快速通道可在同一批视觉调整结束时统一更新状态；完整门禁验证后立即更新当天讨论记录。
- 交接记录必须包含版本标识、修改文件、验证环境、实际步骤、关键日志、验收结论和未验证项，格式遵循`../PortTraining/LM-Wiki/03-规则层/任务验证与文档交付规范.md`。
