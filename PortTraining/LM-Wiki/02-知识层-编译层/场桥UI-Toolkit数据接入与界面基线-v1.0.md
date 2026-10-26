# 场桥 UI Toolkit 数据接入与界面基线 V1.0

> 2026-09-15场桥PLC边界更正：本文原有“Unity不得直连S7、只能由PortGatewayService持有”的结论已被[《场桥S7直连接入架构决策》](场桥S7直连接入架构决策-2026-09-15.md)替代。当前只允许`YardCraneS7Integration`独立场景中的唯一game4automation `S7Interface`直连；UI、Rig、`YardCrane`正式场景仍不得认识DB地址或自行建立连接。

- 日期：2026-09-04
- 工程：`YardCraneSimulator`，Unity `6000.0.42f1`
- 实现：`2026-09-04-yc-uitoolkit-hud-v1`，接入当前 v28 灰盒；不是新物理版本。
- 状态：第一版只读驾驶 HUD 已通过主工程编译、Play 数据读回、横竖屏布局、指引点击与失效恢复验证。不是整机产品或 PLC 联调验收。
- 当前基线更正：2026-09-07 已演进为入口壳层 v2 与 Bootstrap 驾驶/结果 UI v2，场景职责及主显示配置以第9节为准。

## 1. 结论：适配器统一读取，界面订阅显示

不是让每一个 UI 控件主动查模型，也不是让物理代码到处调用 `Label.text`。新增只读适配器，把现有状态整理成一份值快照，再单向送给 UI。

```text
现有 Rig / 机构模型 / 单机输入 Host
             ↓ 主线程 LateUpdate，只读采样
YardCraneHudSceneAdapter : IYardCraneHudSource
             ↓ YardCraneHudSnapshot
YardCraneHudFeed
    ├─ 状态变化通知 → 吊具、制动、急停、尺寸、回零等
    └─ 10 Hz 数值通知 → 位置、速度、实际回旋角度
             ↓
YardCraneOperatorHud → UXML / USS / UIDocument
```

本次新增适配代码，没有修改 Rig、Host、输入源、控制器、绳索或机构算法。UI 的 Runtime 程序集不引用 Obi，也不认识模型层级；只有位于 ObiRope 实验程序集的适配器知道当前灰盒结构。

这是一套明确的 C# 单向 Presenter/事件更新实现，**没有启用 UI Toolkit 的双向反射绑定**。如果后续改用 Unity 的 `DataBinding`，显示字段必须使用 `BindingMode.ToTarget`，不能通过控件值反写运动状态。参见 [Unity 6 绑定模式与更新触发器](https://docs.unity3d.com/cn/6000.0/Manual/UIE-runtime-binding-mode-update.html)。

## 2. 读取、刷新与异常语义

| 内容 | 当前规则 |
| --- | --- |
| 采样时机 | UI 在主线程 LateUpdate 通过已绑定引用读取，晚于 Rig 的固定步与显示投影；不在后台线程访问 Unity 对象 |
| 离散状态 | 每帧比较状态值，变化时通知 UI；延迟至下一次 LateUpdate，不宣称硬实时或独立安全回路 |
| 数值 | 每 100ms 发布一次最新值，不排队重放旧值；文本仅变化时赋值 |
| 初始未就绪 | 显示未知或 `—`，不能将零值当真实数据、不能默认亮绿 |
| 采样停止 | 超过 500ms 显示数据中断；数据源禁用、销毁或读取失败时立即清空显示 |
| 过期、倒序、未来时间样本 | 拒绝，不允许旧样本把状态重新变成健康 |
| 恢复 | 新的有效采样恢复显示；UI 无权解除 SafeHold、重新授权运动或复位机构 |
| 性能 | 场景加载时查找一次并缓存引用，不逐帧 Find、不逐帧重建视觉树；数值格式化最多 10Hz |

500ms 是**界面数据源健康提示**，不替代 V1.1 接口的 100ms 告警、250ms 安全处理及控制器/网关看门狗。主线程整个冻结时 UI 本身也不能更新，生产安全仍依赖独立 Gateway/PLC。

## 3. 字段含义和现阶段真实性边界

| 界面字段 | 实际来源 / 含义 |
| --- | --- |
| 大车位置、速度 | `gantryDrive` 反馈；位置沿用其相对原点定义，单位 m、m/s |
| 小车位置 | `20_TrolleyMotion.localPosition.x`，界面明确注明“本地 X”；不是 TOS 箱位坐标 |
| 小车速度 | 当前小车运动反馈 `currentTrolleyVelocity` |
| 吊具高度 | `30_SpreaderPhysics/35_Spreader.position.y`，注明“场景 Y”；不是绳索放出长度或起升目标 |
| 吊具垂直速度 | 上架刚体实际 Y 速度；正数向上、负数向下 |
| 回旋角度 / 回零中 | 实际物理回旋角度 + 机构回零状态，不用目标角冒充模型角 |
| 抓箱 | 当前连接与锁/解锁请求状态；解锁中优先，不能因连接尚未断开而继续显示已抓箱 |
| 落箱确认 | 已带箱时显示真实落稳结果；空载显示“未带箱”，不伪装成独立着箱传感器 |
| 大车制动、夹轮器、大小车锚定 | 现有机构反馈；未知、转换中、稳定状态分别显示 |
| 吊具泵、20/40尺 | 现有状态模型。尺寸状态不证明模型几何已伸缩 |
| 控制电源、急停、待复位 | 单机 Host / 作业相位；界面标注“单机模拟信号 · 非实体反馈” |
| 当前输入源 | 单机路由的实际来源；未取得来源时显示等待输入，不假装手柄或 PLC 已连接 |

四角锁销、真实着箱传感、PLC 点位、工单箱号和目的箱位尚未接入，不制作四盏假正常灯、不填虚构任务。后续坐标原点、地面标高和真实模型变更只应调整适配层/配置，不能让 UI 自行猜测。

## 4. 界面结构

- 顶部：窄胶囊状态条，展示单机/平台上下文、当前作业相位、暂停/急停/联锁提示。暂不是完整工单卡。
- 左上：大车、小车、吊具的三个不重复参数组，突出实际位置、辅以速度。
- 右上：抓箱、尺寸、物理回旋角、落稳，以及制动/夹轮器/锚定/泵/电源/急停/复位。
- 左上工具按钮：操作指引与运动参数相互替换；右侧安全状态始终保留。按当前键盘或 Xbox 来源显示已有映射。
- 1920×1080 和 1080×1920 使用同一套布局。顶部/左右卡片限制在上半屏，中心留空，下半屏无 UI。没有准星、对位框、X/Y偏差或调试物理卡。
- 视觉采用半透明深色面板、细描边、圆角与有限状态色；当前**不是实时背景模糊**，不引入额外抓屏/模糊渲染成本。
- 指引按钮不可获得键盘/手柄导航焦点；空白区域忽略拾取，避免阻断场景操作。

中文字体当前使用 Windows 系统字体（优先 Microsoft YaHei），未复制或分发系统字体。后续安装包需验证目标机字体或配置有授权的随包字体；当前不承诺 macOS/iOS 部署。

## 5. 文件、挂载及协作边界

| 文件 / 目录 | 职责 |
| --- | --- |
| `Runtime/Presentation/YardCraneHudState.cs` | 值快照、只读数据源接口、变化通知与刷新节流 |
| `Runtime/Presentation/YardCraneHudText.cs` | 中文文案、状态优先级、无效数值显示 |
| `Runtime/Presentation/YardCraneOperatorHud.cs` | UI Toolkit 元素绑定、刷新、指引、显示缩放 |
| `Experiments/ObiRope/YardCraneHudSceneAdapter.cs` | 当前 Rig 的唯一 UI 读取桥梁、运行时挂载与旧 HUD 兼容 |
| `Resources/YardCraneUI/` | UXML、独立 USS、默认运行主题与 PanelSettings 模板 |
| `Editor/YardCraneHudVerification.cs` | Play 数据读回、指针点击、横竖屏界限、断流恢复和截图验证 |
| `Tools/HudPresentationHarness/` | 22 项不依赖 Unity 的状态/节流测试 |

路径均相对于 `YardCraneSimulator/Assets/YardCraneSimulator`，但 `Tools` 相对于场桥工程根目录。

打开现有 `Assets/test.unity` 并 Play，场景加载回调在 Rig 下生成 `90_OperatorHud_UI Toolkit`。无需修改或保存场景资产。现阶段只覆盖已有 MAX Rig 场景；未来动态生成 Rig 的入口需显式绑定适配器，不能依赖每帧全场搜索。

新 HUD 建立成功时临时禁用 `YardCraneDebugHudPresenter`；保留原文件和 Rig 的 `showRuntimePanel` 开关。禁用 HUD 显示组件可以回看旧调试界面；数据源中断时保留新 HUD 的未知/中断提示，不能叠加两套界面。

Claude 后续拆分机构时应保留快照语义，并同步调整这一处适配器。不要把新 UI 的格式化逻辑放回物理类；不要删除现有只读字段后让界面通过反射访问私有字段。

### 资源命名约束

UXML 与 USS 在 Resources 中使用不同名称：`OperatorHud.uxml` 和 `OperatorHudStyles.uss`。UXML 自身含 StyleSheet 子资源，同名加载曾在本次真实 Play 中误选样式，造成布局完全失效；不得改回同名。

## 6. 操作方向与后续 PLC 接入

当前 HUD 只有界面指引切换，不发运动命令。键盘/Xbox 仍走原有输入路由和作业控制器。

未来虚拟操作按钮必须走：`操作意图 → 统一命令/控制器 → 互锁校验 → 受理/拒绝回执 → 机构反馈 → 快照 → UI`。点击被受理不代表机构动作已完成，不能提前点亮“已锁箱”。连续轴仍归原运动链，不经 10Hz UI 通道。

历史PLC拓扑为：`PLC → PortGatewayService（唯一 S7 持有者）→ Named Pipe 语义帧 → 现有控制/反馈模型 → UI 适配层`；该拓扑自2026-09-15起退出当前场桥交付路径。现行边界改为“独立S7联调场景唯一持有连接 → 原始值安全适配 → 现有控制/反馈模型 → UI适配层”，UI与Rig仍不认识DB地址。

## 7. 已验证与未验证

- 主工程 Unity 6000.0.42f1 编译、真实 `test.unity` Play：通过。
- 22 项状态层测试：通过；状态变化不受数值节流影响，未知/断流/恢复/旧样本等均有断言。
- 最终 UI Play：2026-09-04 21:30:25，`YC_UI_PLAY_PASS`。真实适配器读回、横竖屏、指针命中并点击指引四次、右侧状态保持显示、数据源禁用/恢复均通过。
- 实际检查横屏、竖屏及指引截图，中文正常、信息不进入下半屏。截图位于 `YardCraneSimulator/Logs/UiVerification/`，测试结果位于 `Logs/YC_UiVerification.result.txt`。
- 新 UI 挂载后的既有旋转/回零与 30t 完整链在 21:23:40 通过；当时尚未完成样式命名和布局修正，不能用这一条代替最终 UI 验证。最终界面使用上一条独立证据。
- 实体 Xbox/触屏/模拟机、真实 PLC、正式 Player 打包、字体部署、真实模型坐标、帧耗时分析、工单/任务UI均未验收。输入失焦专项也不因 UI 通过而放行。

测试入口：Unity Edit 模式菜单 `Tools/Yard Crane/Tests/Verify UI Toolkit HUD`。验证会启动自己的 Play、短暂输出横竖屏到 RenderTexture、保存截图、恢复目标并停止 Play，不保存场景。

## 8. 后续快照：场桥双20尺范围清理（2026-09-04 23:38复核）

- 交互式Claude会话按用户要求清理场桥`Twin20`，本HUD的尺寸文案只保留20/40尺及未知兜底；岸桥所需的共享契约值不变。该改动晚于第7节的首版UI Play证据。
- Codex已独立复跑状态机36个结果标志、HUD 22项断言和Host八项断言，均通过。四个改动文件的哈希与当日讨论记录第3.14节一致，详见第10节的复核记录。
- 上述隔离测试没有编译Unity专属的`YardCraneSpreaderSizePresenter`，也没有显式验证`Twin20`请求被拒绝；不得将纯逻辑回归写成整套Unity验收。新快照的Unity编译与20/40尺运行显示仍待补测，21:30的PASS只证明此前快照。

## 9. 2026-09-07 场景职责与 1920×2160 双屏基线更正

本节以新的运行证据更正第4、5、7节中“1920×1080 / 1080×1920为主配置”和“HUD归Rig挂载”的旧快照；旧文字保留用于追溯当时实现，不再代表当前主配置。

- `StudentEntry` 是教师端与学生端共用的轻量入口场景。当前具备角色切换、本机身份校验、学生训练入口和教师模块状态页；教师业务服务仍未实现，页面必须显式显示未接入边界。
- `Bootstrap` 是驾驶与结果 UI 的运行时所有者。`YardCraneBootstrap` 下的 `90_DrivingAndResultUI_UI Toolkit` 使用 `DontDestroyOnLoad` 跨越 `test` 作业场景；Rig 适配器只绑定只读反馈。
- 主显示配置改为 `SIMULATOR-DUAL-STACK-1920X2160-V1`：上、下各一块 `1920×1080`，合成一个 `1920×2160` 逻辑桌面。`1920×1080` 与 `1080×1920` 只作为兼容回退，不得再把 1920×2160 判成 9:16 竖屏。
- StudentEntry 的所有管理交互位于上屏；下屏是无交互品牌区。驾驶摄像机可覆盖总画布，但 HUD、操作指引与结果状态卡均限制在上屏，保证下屏视景不被 UI 遮挡。
- 结果卡只显示 `Finalizing / ExitAllowed / Terminated / Failed` 生命周期事实，未收到 NodeService 评分时明确显示“不显示虚假分数”。
- 自动 Play 结果：`YC_STUDENT_SHELL_PLAY_PASS`（入口双角色、上下屏、Bootstrap→test）和 `YC_UI_PLAY_PASS`（Bootstrap所有权、驾驶/指引、断流恢复、结果态）均于 2026-09-07 通过；验证截图位于两个 `Logs/*Verification/` 目录。
- 当前仍缺实体双屏 Windows Player、触控坐标、Windows缩放、正式任务完成事件、NodeService成绩、PLC/TOS及实体Xbox验收。详见 `Docs/讨论记录/2026-09-07.md`。
- 当前版本实际 Play 覆盖主配置和驾驶 HUD 的1920×1080回退；1080×1920只有策略断言与旧版HUD证据，双角色入口新页面尚未在真竖屏复测。

## 10. 2026-09-07 StudentEntry 统一视觉母版 V4

`2026-09-07-yc-entry-shell-v4-prototype` 取代被用户否决的 V3 海报入口，成为当前 StudentEntry 视觉基线：

- 唯一视觉母版为 `teacher-prototype` 在 `http://localhost:3000/` 运行的港口统一界面。Unity入口应复用其海洋蓝、雾化港区背景、左文右卡、青色主按钮、圆角玻璃和侧栏工作台语言，不再发展独立的黑灰色壳层。
- 登录页、学生工作台和教师工作台使用同一套品牌与布局系统；业务能力仍以Unity当前真实实现为边界，浏览器原型存在的页面不能因视觉迁移而被宣称为已经接入。
- `StudentEntry` 必须有面向 Display 1 的有效摄像机输出。当前壳层仅在场景没有可用摄像机时创建清屏相机，消除 Unity Game View 的 `No cameras rendering` 提示；相机不渲染模型，也不改变 UI Toolkit 的数据与交互职责。
- `1920×2160` 的上屏继续承载所有管理交互。下屏为无交互的横向品牌画面：左侧1110像素编辑区，右侧810×1080像素完整显示3:4海报。3:4海报不得强行铺满1920×1080、不得拉伸或裁切。
- 当前海报资产 `QingdaoAutomatedTerminalPoster_v2.png` 为AI概念图，不是真实青岛港影像；正式项目如要求现场真实性，必须替换为有来源和使用授权的原始照片版本。
- 最终Unity Play证据：`YC_STUDENT_SHELL_PLAY_PASS`，时间 `2026-09-07T02:35:39.4574946Z`，其中包含 Display 1 有效相机断言。实体双屏Player和真竖屏回退仍不在该证据范围内。

## 11. 2026-09-07 登录规则 V5

- 版本：`2026-09-07-yc-entry-shell-v5-role-login`。V4视觉母版继续使用，登录规则以本节更正早期“姓名/编号合一”和“教师只登记身份”的描述。
- 学生必须分别输入学号和姓名；保留现有格式及长度校验，姓名不再自动回填学号。启动自由练习后同时保留两项身份数据。
- 教师使用用户指定的固定本机管理员凭据；只有校验成功后才进入教师首页，显示名固定“管理员”。密码输入独立遮蔽，切换身份、成功登录、退出时清空表单，资料及运行会话不保存口令。
- 两种入口支持鼠标点击及输入框回车；错误输入给出可见提示且停留登录页。当前固定管理员校验不代表NodeService远程认证或教师工单业务已接入。
- 证据：51项Harness断言通过；Unity Play `2026-09-07T03:13:43.2879760Z`通过学生双字段、管理员正确/错误凭据、字段隔离、退出、1920×2160布局及Bootstrap→test身份保留。

## 12. 2026-09-07 编辑模式默认预览 V6

- 版本：`2026-09-07-yc-entry-shell-v6-editor-preview`，继承V4视觉与V5登录规则。
- `StudentEntry`打开时，`YardCraneStudentShell`通过`ExecuteAlways`建立默认学生登录UI；未Play也能在Game窗口查看。屏幕空间UI在Game窗口预览，不是Scene窗口内的世界空间模型。
- 选中`YardCraneStudentEntry`，Inspector“界面预览”中的四个按钮可以切换学生登录、教师登录、学生首页、教师首页。首页使用明确的预览身份，实际登录及启动逻辑在编辑模式不执行。
- 编辑模式和运行模式共用同一UXML/USS与绑定。编辑预览的UIDocument位于隐藏且不保存的临时对象，PanelSettings与字体实例也不保存；组件停用、场景切换或脚本重载时释放，进入Play后重新建立运行UI，避免叠加两套。
- 编辑刷新只处理画面布局，不采样输入设备或清理/创建训练会话。切换预览不改变场景资产，未添加占位业务接口。
- 实际证据：`YC_STUDENT_SHELL_EDIT_PREVIEW_PASS`，`2026-09-07T07:50:51.5563923Z`，四页1920×2160实际渲染、编辑态无登录/会话/启动副作用、无重复UI、场景字节及dirty状态不变；`YC_STUDENT_SHELL_PLAY_PASS`，`2026-09-07T07:52:06.4003636Z`，编辑预览清理及V5完整登录启动回归通过。
- 预览验证菜单：`Tools/Yard Crane/Tests/Verify Student Shell Edit Preview`；运行登录验证菜单：`Tools/Yard Crane/Tests/Verify Student Shell`。真实Player、实体双屏与关闭Domain Reload的专项组合未由本轮验收。

## 13. 2026-09-07 晚间当前入口与品牌基线 V8

- 本节更正前文历史快照：StudentEntry目前为两个独立1920×1080 Display，上屏交互、下屏装饰，分别有UXML/USS/PanelSettings/UIDocument；Lower必须保持独立根物体，不能嵌套在Upper UIDocument之下。Bootstrap驾驶/结果UI本次没有同步改造成独立双Display。
- Package化已按用户要求撤销。源码使用`Assets/YardCraneSimulator/Runtime/Shell/`，资源使用`Assets/YardCraneSimulator/Runtime/Resources/YardCraneShell/Display1UI/`与`Display2UI/`。`SDK/com.porttraining.studententryui`是历史副本，不是当前修改入口。
- `2026-09-07-yc-entry-shell-v8-yard-crane-branding`明确本软件只做场桥；登录说明为“场桥集装箱起重机仿真系统”，移除登录机型切换及学生首页岸桥chip，保留学生/教师身份切换。继续使用蓝色港区背景、圆角玻璃登录框，不改成另一套主题。
- 左上角Logo直接引用`Assets/YardCraneSimulator/Art/logo.png`（GUID `f72999cf1d81d0649b21059a50b0c45c`）；USS项目资源引用使其成为构建依赖，运行时代码不依赖AssetDatabase或本机文件路径。完整等比显示，图像本体由用户提供，后续原位替换图片即可沿用引用，须保留meta/GUID。
- 后续分工：用户验收画面效果，Codex负责功能开发、UI设计与功能验证；自动测试不代替用户视觉确认。
- 最新功能证据：51项身份/会话Harness通过；Unity `6000.0.42f1` Play于`2026-09-07T13:58:53.1024012Z`通过V8品牌引用、无机型选择、横/竖屏品牌边界、登录成功/失败、角色切换/退出、独立Lower结构与Bootstrap→test身份保留。
- 验证限制：本日编辑模式自动截图两次读取到黑图，尚未定位，不能沿用第12节V6的Edit Preview PASS认定当前版已通过；Display2像素、实体双屏、最终Player及画面效果未验收。完整证据和失败记录见当天讨论记录第21节。

## 14. 2026-09-07 Logo透明展示 V9

- 用户否决第21条讨论记录中的浅色品牌底板。登录Logo直接以透明背景叠加到蓝色港区背景上，不使用底色、边框或圆角底板；仍完整等比引用`Art/logo.png`，不改色、不裁切，保留源Alpha。
- 版本：`2026-09-07-yc-entry-shell-v9-transparent-logo`。Unity `6000.0.42f1` Play于`2026-09-07T14:06:25.3257062Z`通过透明容器、无边框、源Alpha保留及既有登录/启动回归。PNG哈希未改变。
- 画面效果由用户确认；本节不改变此前编辑模式自动截图、实体双屏和Player的未验证边界。

## 15. 2026-09-09 StudentHome三模式首页与浅色驾驶HUD基线

本节取代第9～14节中仍将学生首页放在`StudentEntry`或将驾驶HUD描述为旧深色版的历史快照；登录、教师页等未涉及内容继续沿用原有结论。

- `StudentEntry`继续只负责学生/教师登录与教师端本机占位功能；学生登录成功后切换到独立`StudentHome`场景。`StudentHome`拥有两个互不嵌套的`UIDocument`，Display1和Display2各按1920×1080设计，组合交付分辨率为1920×2160。
- StudentHome Display1当前视觉版本为`2026-09-09-yc-student-home-v5-reference-fidelity`：顶部品牌和面包屑、居中“模式选择”、学习训练/自由练习/任务考核三张360×468卡片、底部编辑性文字。自由练习默认高亮，但三张卡都是真实按钮。
- 入口真实语义：学习训练进入现有学习页并可返回；自由练习沿既有会话/Bootstrap链进入驾驶；任务考核目前只给出“尚未接入”反馈，不得把可点击卡片解释为考核服务或正式评分已经完成。
- 驾驶HUD当前视觉版本为`2026-09-09-yc-driving-hud-v6-vector-icons`：浅色冰蓝毛玻璃，所有HUD组件限制在1920×2160逻辑桌面的上方1080像素，中心驾驶视野保持清空；1920×1080仍作为单屏回退。时钟、任务、目标、位置、锁使用`Painter2D`绘制，避免系统字体缺字显示方框。
- HUD继续遵循“状态主动推送/只读呈现”边界：UI不轮询PLC、不产生运动命令、不持有S7连接。锁定/开锁和已锁/未锁按同一twistlock状态互斥；断流立即清空数值和灯态。没有任务源时任务名显示“等待任务”，箱号/地址显示`—`；没有两轴倾转源时左右/前后倾转显示`—`，禁止写死演示值。
- 可复用设计资产：`Assets/YardCraneSimulator/Art/UI/StudentHome/DesignPackage_v1/`和`Assets/YardCraneSimulator/Art/UI/CraneHud/DesignPackage_v2_LightGlass/`。二者均含参考图、拆分元素、manifest和复刻说明；运行时学生首页另外把三张透明模式图放在`Resources/.../ModeIcons/`。
- 验证入口：
  - `Tools/StudentHomeUiHarness/Invoke-StudentHomeUiChecks.ps1`：编译/资源/场景/查询契约；当前156项通过。
  - Unity菜单`Tools/Yard Crane/Tests/Verify Student Home`：StudentHome真实Play布局与交互；2026-09-09通过。
  - `Tools/HudPresentationHarness`：HUD状态语义；当前22项通过。
  - Unity菜单`Tools/Yard Crane/Tests/Verify UI Toolkit HUD`：Bootstrap驾驶HUD真实Play；2026-09-09通过。
- 尚未放行：任务考核服务与评分、学习训练到Rig的目标驱动、正式工单数据、两轴倾转遥测、实体双屏Player、Windows缩放、实体Xbox/模拟机及用户最终视觉验收。旧“Unity直连禁令/Gateway唯一持有”已废止；现行规则只允许`YardCraneS7Integration`建立唯一S7直连，其他场景继续禁止。

## 16. 2026-09-09 StudentHome/HUD兼容与操作闭环更正

本节取代第15节中的版本号和“导航/成绩”未展开描述；第15节的页面职责、数据只读边界和未实现业务边界继续有效。

- StudentHome当前版本为`2026-09-09-yc-student-home-v7-session-safe-navigation`，视觉沿用最终参考图与三卡布局。驾驶HUD当前版本为`2026-09-09-yc-driving-hud-v8-owned-navigation`，成绩格式为`2026-09-09-yc-training-score-v3-outcome-aware`。
- Unity 6000.0.42f1 USS不支持网页CSS的`box-shadow`和结构伪类`:last-child`。当前所有光晕由显式UXML嵌套层、半透明填充、边框和支持的状态类实现；相关资源中这两个不支持项为零命中。
- HUD导航采用“能力与执行动作原子绑定”：默认能力为`None`；按钮禁用并显示原因。StudentHome本地自由练习桥接器持有并同时绑定`Back|Exit`，解绑时必须匹配同一所有者。Managed协调器、直接Standalone/Editor在没有执行器时不得仅因启动模式或事件订阅而显示为可用。
- 本地往返顺序固定为：HUD请求返回 → 校验本地会话所有权 → 写入一次性StudentHome身份交接 → 生命周期标记`Terminated` → 解绑HUD → 销毁持久Bootstrap/HUD → 单场景加载StudentHome（旧Rig先完成`OnDestroy`保存）→ 清理FallbackSession与TrainingIdentity → 销毁桥接器。不可提前清身份，也不可留下第二个Bootstrap/HUD/Bridge。
- 成绩结果语义为`None / InProgress / Completed / Terminated / Failed`。运行文件是审计层，始终可写；学生正式索引只接受`Completed`且身份有效的记录。`Terminated`、`Failed`和仍为`InProgress`的异常中断均不得按默认分数进入正式成绩。旧v2记录缺少`outcome/countsTowardResults`，未来读取时只能按`legacy/unknown`处理，不能默认推断为完成。
- Editor自动验证必须设置`YardCraneTrainingScoreRecorder.EditorOutputDirectoryOverride`到每次唯一的`Temp/YardCraneVerificationScores/*`目录，并通过`SessionState`跨域重载恢复；只能在`EnteredEditMode`、场景销毁与Rig保存之后释放。验证学生ID禁止写入正式`Application.persistentDataPath`。
- 2026-09-09最终证据：StudentHome Play于`13:51:44Z`通过“中止只写隔离审计、不入索引”；HUD Play于`13:57:26Z`通过“完成写隔离审计并进入隔离索引”。两个验证器的失败收尾均使用空值保护与`finally`强制停Play。StudentHome Harness 157项、HUD语义Harness 22项均通过。
- 仍需关注但不阻断当前单机一期的风险：同一进程出现多个Rig可能为同一会话重复保存；毫秒级文件名理论上可能碰撞；学生索引仍按单站单进程做非并发的读改写。若未来允许一台机器多Player或并行训练，必须把成绩写入改成唯一文件名和原子/服务端聚合。

## 17. 2026-09-10 驾驶 HUD 七类独立提示框基线

本节追加并更正此前提示页数量的历史描述。以用户最新效果图为准，驾驶 HUD 固定采用七类提示：`任务提示`、`倒计时`、`提示信息（确定/取消）`、`错误提示`、`任务完成`、`考核详情`、`成绩提交`。“提示信息”本身已经承担确认与取消，不再拆分为第八类确认页。

- 当前版本：`2026-09-09-yc-driving-hud-v9-independent-prompts`。七类提示共用现有 `OperatorHud.uxml` 内的一个 `prompt-layer`，没有增加 UIDocument 或新的显示所有者。
- 互斥规则：同一时刻最多显示一个提示。显示新提示必须先移除上一提示的事件回调、倒计时和提交等待状态；关闭提示恢复常驻 HUD。生命周期结果卡与七类提示不能叠加显示。
- 终态优先：生命周期进入 `Finalizing / ExitAllowed / Terminated / Failed` 时，必须立即使此前活动或延迟的提示失效并清除回调，防止旧倒计时或旧确认动作作用于已结束的运行。协调器如需显示本轮“任务完成”，应在终态转换完成后用本轮数据重新触发。
- 触发方式：业务层分别调用 `ShowTaskPrompt`、`ShowCountdownPrompt`、`ShowConfirmationPrompt`、`ShowErrorPrompt`、`ShowCompletionPrompt`、`ShowAssessmentPrompt`、`ShowScoreSubmissionPrompt`；统一使用 `ClosePrompt` 关闭。调用发生在 HUD 根节点就绪前时只保留最后一项待显示提示，待界面完成建立后再显示。
- 数据边界：七类模型只携带展示数据和本地意图回调。HUD 不轮询场景对象，不推断任务状态，不直接读 PLC/S7，也不通过弹窗取得运动控制权。空任务、空成绩、空身份必须显示未知/不可用，不允许写死演示值。
- 倒计时：使用单调实时推进，只负责视觉倒计时和一次性到期回调；暂停/恢复、作业状态门禁和机构解锁必须由任务协调器决定，不能由 UI 自行实现。
- 关闭语义：普通展示页的 X 只关闭；“提示信息”的 X 必须等价于取消回调；“错误提示”的 X 必须等价于已读回调，不能让上游等待状态悬空。考核详情表必须保留 ScrollView 命中，明细超过三行后仍可访问。
- 成绩提交：只有姓名、学号有效且存在提交执行器时可请求提交；点击后按钮进入等待平台回执，不能先显示“提交成功”。默认身份字段只读，防止学生在提交页改写登录身份；只有明确业务需求并由调用方开启时才允许编辑。
- 正式接入建议：后续持久提示源/操作汇应携带 `promptId`、`operationRunId`、`revision/requestId` 和结果回执，以拒绝旧运行、旧版本或重复动作。当前直接 `Show...Prompt`/回调接口只完成本机展示桥，不等于 NodeService 或教师平台的权威闭环。
- 屏幕边界：七类窗口均按 1920×1080 上屏坐标设计；1920×2160 双屏时不得进入下方 Display2 区域。弹窗显示时隐藏常驻 HUD 卡片以避免半透明叠字，但中央驾驶区域必须继续可见。
- 指针边界：提示存在期间 `prompt-layer` 在上屏内作为模态指针层，空白区域也不把鼠标/触摸事件穿透给提示框后的 UI；这只约束 UI 指针，不暂停键盘、手柄、物理或机构运动。需要冻结操作时必须由任务/安全协调器显式下达，不能靠 UI 遮罩冒充联锁。
- 验证证据：Unity `6000.0.42f1` Play 于本地 2026-09-10 01:29:16 通过，日志为 `[YC_UI_PLAY_PASS] version=2026-09-09-yc-driving-hud-v9-independent-prompts`；包含七类互斥、终态清旧提示、X 回调语义、六行考核滚动、旧回调拒绝、上屏边界、中心净空、无服务提交、双屏/单屏回退和原 HUD 回归。七张实际截图位于 `YardCraneSimulator/Logs/UiVerification/prompt-*.png`，每张均为 1920×2160 输出且 UI 限制在上方 1080。
- 未放行范围：真实任务源、官方运行时钟、正式评分、NodeService/教师平台提交回执、实体双屏 Player 和用户最终视觉验收。

## 18. 2026-09-10 StudentHome 三模式卡初始状态更正

本节更正第15节“自由练习默认高亮”以及第16节沿用旧视觉的描述；页面结构和三条入口的业务语义继续有效。

- StudentHome当前版本为`2026-09-10-yc-student-home-v8-equal-mode-cards`。学习训练、自由练习、任务考核三张卡打开页面时均使用同一个`mode-card`普通态，不再给自由练习附加`mode-card-selected`，运行时样式也不再保留永久选中选择器。
- 悬停和按下反馈继续对三张卡统一生效；移开指针后恢复普通态。此更正只改变初始视觉优先级，不改变按钮ID、点击路由或会话所有权。
- 三个入口仍保持真实语义：学习训练进入学习页并可返回；自由练习沿现有会话链进入Bootstrap/驾驶HUD；任务考核可点击但在业务未接入时只显示诚实提示。
- 静态验证：`Tools/StudentHomeUiHarness/Invoke-StudentHomeUiChecks.ps1`通过157项，Unity真实程序集引用编译、UXML/USS、资源、查询契约、独立双UIDocument场景和Build Settings均通过。
- Unity `6000.0.42f1` Play于本地2026-09-10 07:36:35通过，日志为`[YC_STUDENT_HOME_PLAY_PASS] version=2026-09-10-yc-student-home-v8-equal-mode-cards`；覆盖三卡相同初始样式、学习打开/返回、考核反馈、自由练习实际启动、HUD返回、身份延迟清理、单一Bootstrap/HUD/桥接器所有权及中止成绩隔离。实际截图为`YardCraneSimulator/Logs/StudentHomeVerification/student-home-reference-fidelity.png`。
- 未放行范围不变：任务考核服务、正式评分、学习训练到Rig的目标驱动、实体双屏Player、Windows缩放、实体输入设备和用户最终视觉确认。

## 19. 2026-09-10 驾驶 HUD 七类提示框统一居中更正

本节更正第17节“中央驾驶区域继续可见”以及七类提示分散布局的历史结论；常驻HUD无提示时的中央净空规则不变。

- 当前版本为`2026-09-10-yc-driving-hud-v10-centered-prompts`。任务提示、倒计时、提示信息、错误提示、任务完成、考核详情、成绩提交统一以1920×1080上屏中心`(960, 540)`定位，各自尺寸和内容结构不变。
- 布局由唯一`prompt-layer`通过Flex统一居中，提示视图不再携带分散的`left/top`坐标。同一时刻仍只显示一个提示，不增加UIDocument或并行显示所有者。
- 1920×2160双屏的“居中”只指上方Display1中心，不是整个2160高度中心；1920×1080单屏回退使用相同逻辑坐标。模态指针、回调清理、终态优先、数据只读和PLC/S7边界均不改变。
- 静态证据：HUD语义Harness 22项通过；UXML严格解析通过、184个`name`唯一、已知不兼容USS语法零命中；`dotnet build`为0警告、0错误。
- Unity `6000.0.42f1` Play于本地2026-09-10 07:48:48通过，日志为`[YC_UI_PLAY_PASS] version=2026-09-10-yc-driving-hud-v10-centered-prompts`。七张提示逐一验证中心X/Y误差不超过2像素、完整位于上屏，并继续回归互斥、回调、滚动、断流、导航、结果与成绩隔离；实际截图位于`YardCraneSimulator/Logs/UiVerification/prompt-*.png`。
- 未放行范围：真实模型最终视觉效果、实体双屏Player、Windows缩放、触摸、实体输入设备、真实任务源和正式平台回执仍由后续验收确认。

## 20. 2026-09-10 弹窗与常驻驾驶 HUD 叠层规则更正

本节取代第17节“弹窗显示时隐藏常驻HUD卡片”和第19节“关闭后恢复常驻HUD”的旧显隐规则；第19节的上屏居中、第17节的七类互斥、回调清理与终态优先继续有效。

- 当前版本为`2026-09-10-yc-driving-hud-v11-live-hud-under-prompts`。七类弹窗打开时，用时、任务标题、五状态灯、左侧六项遥测、右侧任务/姿态/锁销卡及返回/退出导航继续显示；真实条件告警如存在也不再因弹窗活动而被强制关闭。
- 七类弹窗仍共用唯一`prompt-layer`，打开新弹窗前先隐藏全部七类视图并清除旧回调，随后只显示目标视图。这里的“常驻HUD同时可见”不表示允许多弹窗并排，也不允许生命周期结果卡与业务弹窗叠加。
- 提示层继续作为1920×1080上屏模态指针层，半透明遮罩只降低背景视觉权重。背景HUD保持可见和实时刷新，但鼠标/触摸不能穿透到返回、退出或其他HUD控件；该规则不拦截键盘、Xbox、PLC命令或物理更新，运动门禁仍属于任务/安全控制链。
- Unity`6000.0.42f1` Play于本地2026-09-10 08:14:47通过，日志为`[YC_UI_PLAY_PASS] version=2026-09-10-yc-driving-hud-v11-live-hud-under-prompts`。验证器对每一种弹窗断言七类中恰有一张可见，同时九个常驻显示节点/区域保持`Flex`、结果卡不叠层、弹窗中心与上屏中心误差不超过2像素。
- HUD Harness 22项通过，Unity程序集构建0警告0错误，UXML 184个具名节点无重复。七张本轮实际截图位于`YardCraneSimulator/Logs/UiVerification/prompt-*.png`，人工检查常驻HUD均在弹窗后方可见，且用时持续推进。
- 当前只放行灰盒场景中的布局和行为；真实模型最终视觉、实体双屏Player、Windows缩放、触摸、实体输入设备、真实任务源和正式平台回执仍未放行。

## 21. 2026-09-10 正式 `YardCrane` 场景与原生双显示更正

本节取代第15～20节仍把两块屏幕描述成单个1920×2160逻辑桌面、把Bootstrap描述成驾驶HUD所有者，以及把常驻HUD描述为浅色皮肤的当前实现结论。旧结论只保留为历史版本说明，不再作为新代码和Claude协作任务的实现依据。

- 正式作业场景唯一入口为`Assets/YardCraneSimulator/Scenes/YardCrane.unity`；`Assets/test.unity`不再进入Build Settings，但作为历史灰盒资产保留，不能继续向它新增正式功能。
- 交付设备是两个由Windows/Unity分别识别的1920×1080显示器，不是一个1920×2160虚拟桌面。当前显示配置ID为`SIMULATOR-DUAL-DISPLAY-1920X1080X2-V2`；旧`SIMULATOR-DUAL-STACK-1920X2160-V1`只为兼容旧启动参数保留。
- `YardCrane`场景所有权是硬约束：恰好两个Camera、恰好一个`UIDocument`、恰好一个HUD、恰好一个场景适配器。`Display1Camera`投向Unity Display 1（`targetDisplay=0`）并使用全屏viewport；`Display2Camera`投向Unity Display 2（`targetDisplay=1`）并使用全屏viewport；只有Display1Camera保留一个启用的AudioListener。
- 唯一驾驶文档命名为`UIDocumentDisplay1`，Panel Settings投向`targetDisplay=0`、参考分辨率1920×1080。Display2只显示第二相机画面，当前不创建第二套驾驶HUD。`YardCraneDisplayCameraLayout`在Play时仅负责应用两相机目标并在可用时激活第二Display。
- 禁止运行时兜底创建驾驶文档：`YardCraneOperatorHud`、`YardCraneBootstrap`和`YardCraneHudSceneAdapter`都只能查找、绑定场景已经序列化的文档/HUD；工程范围不得出现`AddComponent<UIDocument>`。缺失时应明确报配置错误，由场景修复流程处理，不能在Play时悄悄生成重复UI。
- 编辑器绑定器可为现有场景对象补挂显示布局、HUD和适配器组件并写回序列化引用，但它不得创建/替换Camera或UIDocument。相机必须精确命名为`Display1Camera`、`Display2Camera`，不允许用数组顺序猜测身份。
- 当前视觉版本为`2026-09-10-yc-driving-hud-v12-navy-operational-skin`：深海蓝半透明玻璃、青色描边/光晕、左上计时与六项遥测、顶部任务条和五状态灯、右侧任务/姿态/锁销、右上圆形返回/退出。七类弹窗继续统一居中、互斥，弹窗下方的常驻HUD继续显示和刷新。
- 数据链仍固定为`YardCraneHudSceneAdapter → YardCraneHudFeed → YardCraneOperatorHud`。UI只读展示状态和发出导航/业务意图，不轮询PLC、不连接S7、不直接驱动大车、小车、起升、吊具或绳索。
- Build Settings当前顺序为`StudentEntry → Bootstrap → YardCrane → StudentHome`。Bootstrap负责启动契约与生命周期，不再创建或跨场景持有驾驶UIDocument；驾驶HUD由`YardCrane`场景自身拥有。
- Unity`6000.0.42f1`实际证据：场景绑定`status=PASS`；HUD Play为`[YC_UI_PLAY_PASS] version=2026-09-10-yc-driving-hud-v12-navy-operational-skin`；StudentHome往返为`[YC_STUDENT_HOME_PLAY_PASS]`；入口到驾驶全链为`[YC_STUDENT_SHELL_PLAY_PASS]`。HUD 22项、StudentShell 51项、StandaloneHost 8项和操作状态机隔离回归均通过。
- 尚未放行：实体双1920×1080 Windows Player、Windows缩放、用户最终视觉确认、真实模型、实体输入设备、真实任务源与正式平台回执。

## 22. 2026-09-10 StudentHome 学习训练页与场桥缩略图基线

本节取代第15节仍沿用参考图中“吊具倾斜 / 岸桥俯仰”以及旧岸桥小图的历史描述；StudentHome三模式首页、Display2和正式`YardCrane`驾驶场景职责不变。

- 当前版本：`2026-09-10-yc-student-home-v9-yard-crane-learning-ui`。学习训练页继续位于`StudentHome`的Display1上屏文档中，不新增UIDocument、场景或并行页面。
- 1920×1080布局固定为：顶部身份/面包屑与返回/退出，左侧235×440短玻璃导航，标题图标及标题，右侧本地/远程分段选择，主区五张等宽、680高的训练卡；每卡图片区域160高、开始按钮64高，底部英文与中文编辑性说明脱离左侧玻璃框独立排版。
- 背景继续使用`StudentLearningSkin_v1.png`，取消旧的图片0.55透明度与过重0.58蓝罩叠加，改为完整背景配0.42深蓝统一遮罩。UI Toolkit不使用`box-shadow`、`:last-child`等Unity不支持的网页CSS。
- 五项场桥语义统一为：`大车移动 / 小车移动 / 吊具起升 / 吊具回旋 / 吊具回零`；英文分别为`GANTRY MOVEMENT / TROLLEY MOVEMENT / SPREADER HOISTING / SPREADER ROTATION / SPREADER ZEROING`。活动页面中`岸桥 / QUAY BRIDGE / HOOK TILTING`为零命中。
- 五张缩略图位于`Resources/YardCraneShell/StudentSide/Display1UI/LearningThumbnails/`，文件名分别为`DrillGantry`、`DrillTrolley`、`DrillHoist`、`DrillSpreaderRotation`、`DrillSpreaderZeroing`。均为1672×941、sRGB、2048最大导入尺寸的独立场桥图；活动UXML不再引用数字命名的旧岸桥图。
- 为避免破坏既有场景序列化与查询，第四、第五项内部暂保留`drill-tilt-*`、`drill-pitch-*`历史ID，显示文本、状态反馈和资源均已校正为回旋/回零。后续若重命名内部ID，必须同时更新C#、静态Harness和Unity Play验证器。
- 当前五个“开始”仍是诚实占位：可选参数且每组始终恰有一项选中，但尚未把目标值连接到Rig或训练任务源；点击只显示对应项目“尚未接入”，不创建FallbackSession、Bootstrap、HUD、成绩或运动命令。远程模式及其余四个侧栏类别同样只给出未接入提示。
- 静态证据：`Tools/StudentHomeUiHarness/Invoke-StudentHomeUiChecks.ps1`为`FINAL PASS checks=195 failures=0 warnings=2`，包含真实Unity程序集引用编译、五卡层级/术语/资源/GUID/尺寸/哈希/默认选项及既有双Display场景契约。
- Unity `6000.0.42f1` Play证据：`[YC_STUDENT_HOME_PLAY_PASS] version=2026-09-10-yc-student-home-v9-yard-crane-learning-ui`，UTC`2026-09-10T13:52:12.3940000Z`。实际验证五卡等宽等高与按钮基线、五张纹理导入、场桥术语、单选互斥、全部未接入入口无副作用、面包屑与右上返回，以及自由练习进入正式`YardCrane`后返回StudentHome的完整回归。
- 实际截图：`Logs/StudentHomeVerification/student-learning-yard-crane-reference.png`（干净初态）、`student-learning-yard-crane-interactions.png`（交互状态）和更新后的`student-home-reference-fidelity.png`。代码侧已人工检查初态截图无越界、遮挡和下屏侵入；用户最终视觉验收仍未完成。
- 未放行：五项学习训练到Rig的目标驱动、远程训练、其他侧栏内容、正式任务/评分、实体双屏Player、Windows缩放、触摸、实体Xbox/模拟机和用户最终画面确认。UI继续只读展示，不直接连接PLC/S7。

## 23. 2026-09-11 StudentHome 三入口本地训练闭环 V11

本节更正第18节仍将任务考核描述为“业务未接入”、以及第22节仍将五项学习训练描述为“只显示尚未接入”的历史结论。更正范围只包括本机训练闭环；远程训练、教师平台正式任务与权威评分仍未接入。

- 当前版本为`2026-09-11-yc-student-home-v11-resilient-three-mode-loop`，审计格式为`2026-09-11-yc-training-score-v5-identity-correlated`。StudentHome的`学习训练 / 自由练习 / 任务考核`三个入口现在都沿同一条`StudentHome → Bootstrap → YardCrane`链进入正式场桥作业场景，不再使用`test`场景或未接入占位反馈。
- 正式运行继续遵守第21节的单场景所有权：`YardCrane`场景恰好两个Camera和一个投向Display1的`UIDocument`；Display2只显示第二相机画面。训练协调器只绑定场景中已有的HUD和文档，禁止运行时动态创建`UIDocument`。
- 学习训练由入口冻结项目、目标值、参考系、容差和返回页。大车、小车、起升、吊具回旋均要求真实机构动作进入目标容差并稳定保持后才完成；吊具回零额外要求先发生有效偏离，再进入返回过程并在零位稳定，避免初始零位直接误判完成。学习训练结束或返回时回到`LearningSelection`。
- 自由练习不设置目标和时限，开放全部已有本机操作并使用正计时；只有真实完成的本地估算运行才允许携带估算分数。通过HUD返回或其他中止路径结束的自由练习记录为`Terminated`且`hasScore=false / totalScore=0`，不得写入学生正式成绩索引。
- 本地任务考核采用10分钟倒计时和固定顺序流程：锁箱、起升、行走、落箱、解锁。它用于一期本机流程演练和通过/失败反馈，不是教师平台下发的正式考试；成绩信任级别固定为`Unscored`，`isOfficial=false`，不得生成或上传权威分数。任务考核返回StudentHome首页。
- 三种模式进入作业场景前统一执行3秒倒计时门禁。倒计时期间大车、小车、起升、吊具旋转等业务运动输入被拦截，急停、复位、控制电源及必要心跳等安全通道继续可用。倒计时结束后协调器与HUD共用同一个单调时间起点，避免提示倒计时结束后HUD用时提前累计或出现双时钟漂移。
- Bootstrap、导航桥、训练身份、协调器和审计记录均按`sessionId`关联。Bootstrap使用显式`Active`单例并只允许与自身捕获会话一致的生命周期写入；旧场景、旧回调或旧Bootstrap不能完成、终止或覆盖新会话。审计v5将归属身份与实际观察身份分开记录，并提供`identityContextMatched`；身份不匹配时清空主要归属字段，只保留观察字段供排查，防止错记学生或错记训练模式。
- 启动和返回均采用事务式顺序。入口加载失败时回滚本次上下文、身份与持久对象；Bootstrap只有在明确检测到作业场景不在Build Settings或`LoadSceneAsync`失败时才标记启动失败。导航桥捕获该显式失败后自动返回本次入口原页面并显示一次性错误提示，不会把任务考核自身的正常失败误判为场景启动失败。HUD返回则先确认StudentHome可加载，再冻结输入、结束会话、卸载作业场景并激活原页面，避免中途失败留下可操作的孤立Rig。
- Unity `6000.0.42f1` Play于本地`2026-09-11 02:51:18`通过，日志为`[YC_STUDENT_HOME_PLAY_PASS] version=2026-09-11-yc-student-home-v11-resilient-three-mode-loop`。实际覆盖学习训练、自由练习、本地任务考核三条启动链、HUD返回往返、计时同源、模式审计，以及强制指定缺失作业场景后的自动恢复与原页面回返。
- 静态与灰盒回归结果：StudentShell Harness 82项通过；HUD Presentation Harness 45项通过；StandaloneHost Harness 14项通过；StudentHome UI Harness `FINAL PASS checks=203 failures=0 warnings=2`。本轮还检查了三类v5隔离审计，学习训练和本地任务考核均为`Unscored`，中止的自由练习无分数且没有进入学生成绩索引。
- 尚未验证或放行：用户最终画面效果；真人按实体设备完整完成每一个学习目标和整套10分钟考核；实体双1920×1080 Windows Player、Windows缩放、触摸、实体Xbox/模拟机；正式教师工单、NodeService/教师平台回执与权威评分；真实PLC/S7链路。S7直连仅在独立`YardCraneS7Integration`场景中放行，其他Unity场景仍不得自行连接PLC。

## 24. 2026-09-12 TeacherHome 独立场景与双 UIDocument 基线

本节取代第9、12、15、21节中“教师首页位于StudentEntry”以及四场景Build Settings的旧结论；学生端和驾驶HUD的既有职责不变。

- `StudentEntry`现在只拥有角色中立的登录页。学生身份交给`StudentHome`，教师`admin/admin`身份通过一次性`YardCraneTeacherHomeHandoff`交给独立`TeacherHome`；身份只能消费一次，教师路径不会创建学生FallbackSession或训练身份。
- `TeacherHome`由`YardCraneTeacherSideShell`持有。场景内恰好两个互不嵌套的根`UIDocument`和两台相机：Display1文档/相机指向`targetDisplay=0`，Display2文档/相机指向`targetDisplay=1`，两套Panel参考分辨率均为1920×1080。
- 教师上、下屏源资产分别限定在`Resources/TeacherSide/Display1UI`和`Resources/TeacherSide/Display2UI`。它们拥有独立UXML、USS、PanelSettings及GUID；不得引用StudentEntry或StudentSide的UXML/PanelSettings。背景位图可以作为只读美术资源复用，不等于共享UIDocument所有权。
- 教师Display1保留“任务管理 / 考核统计”两个核心入口及管理员身份；两个入口没有默认选中，点击后互斥选中，并明确显示平台任务/正式成绩服务尚未接入，不伪造创建、发布、统计或成绩。Display2是无Button/TextField的非交互展示页。
- 直接Play `TeacherHome`而没有新鲜教师交接时必须清理教师、学生与训练残留并返回`StudentEntry`；教师退出同样清理状态并加载全新的入口登录页。
- Build Settings及三个维护器统一固定为`StudentEntry → Bootstrap → YardCrane → StudentHome → TeacherHome`，后续场景绑定或脚手架操作不得移除TeacherHome。
- 验证证据：StudentShell纯逻辑Harness为92项通过；StudentHome UI Harness为`FINAL PASS checks=228 failures=0 warnings=2`；TeacherHome独立静态门禁为`[YC_TEACHER_HOME_UI_STATIC_PASS] checks=129 failures=0`。Unity `6000.0.42f1`真实Play于UTC 2026-09-12 10:43:55通过，日志为`[YC_STUDENT_SHELL_PLAY_PASS] version=2026-09-12-yc-entry-shell-v27-role-scene-routing`，覆盖学生/教师真实场景切换、教师双文档源路径、模块互斥、无训练会话副作用及教师退出回入口。截图为`Logs/StudentShellVerification/teacher-home-dedicated-display1.png`。
- 未放行：教师NodeService、真实任务新增/编辑/发布、工位监控、权威考核统计/导出、实体双显示器Windows Player以及用户最终视觉验收。当前教师工作台仍是诚实的本机UI骨架，不得描述为教师业务闭环完成。
