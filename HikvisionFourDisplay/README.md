# HikvisionFourDisplay

这是一个独立的 Unity 6 Windows 工程，用于让一台主机连接四块物理显示器，并让每块屏幕全屏展示一路海康威视网络摄像机画面。

## 当前状态

工程骨架与运行时架构已实现，现场第一路摄像机已经在 Unity Editor 中显示真实画面且方向正确；当前把四个Display默认配置为同一台`.71`摄像机，用于四路并发与四屏映射验收，后续再替换为四台独立摄像机：

- Unity 版本固定为 `6000.0.42f1`。
- 主场景为 `Assets/HikvisionFourDisplay/Scenes/Main.unity`。
- 场景预置四个 Display 分组、四台输出相机和四个视频纹理占位面，四台相机分别指向 Unity Display 索引 `0`、`1`、`2`、`3`。
- 运行时架构已实现（`Assets/HikvisionFourDisplay/Runtime/`）：`FourDisplayBootstrap` 激活 Display 1～3 并按固定映射把配置分发给四个 `CameraFeedController`；`FeedConfiguration` 读取版本化的非敏感 JSON 配置；`CredentialProvider` 通过 Windows 凭据管理器（DPAPI 保护，当前用户级）按 `credentialKey` 存取账号密码；`IVideoStreamDecoder` 隔离模拟源与 LibVLC 实流解码，当前默认使用 `LibVlcStreamDecoder`；`FeedSurfacePresenter` 用独立材质/纹理绑定每路画面，保持宽高比、必要时留黑边，并在对应屏幕上显示未配置/连接中/认证失败/网络中断/格式不支持/重连中等状态。
- 断线重连使用有上限的指数退避；四路 Controller 各自独立，单路故障不阻塞其余三路；退出、销毁、重连前都会释放解码器持有的纹理和句柄。
- 解码方案 PoC 已完成，见 [`Docs/解码方案PoC.md`](Docs/解码方案PoC.md)：已下载并集成 LibVLCSharp（免费、LGPL），实现了 `LibVlcStreamDecoder`。2026-09-21 已在现场`.71`摄像机上完成RTSP Digest认证，`101/102`均返回H.264 SDP；`102`子码流在测试Player中稳定进入`Playing`并触发Unity纹理回调，随后Unity Editor用主码流完成可见画面验证，日志得到`HFD_FEED_TEXTURE_READY width=2560 height=1442`。该设备在当前LibVLC版本中必须把凭据作为每路独立`LibVLC`实例参数传入，Media级凭据选项不会完成认证。
- 启动及未获取视频时显示 `Assets/StreamingAssets/默认图片.png`；视频等比适配，必要时留黑边。每路可独立旋转，默认图始终保持正向。

已验证：一块物理屏环境下`.71`单路在Unity Editor中从`Connecting`进入`Playing`、创建纹理并显示连续实景。首次画面上下倒置的根因是LibVLC顶行优先的帧缓冲被直接交给Unity底部为`V=0`的标准Quad；`FeedSurfacePresenter`现已用材质UV做GPU端Y翻转，用户重新Play后的截图确认方向恢复正常。最新测试包又完成四路同源短跑：四个feed均进入`Playing`并各自产生纹理，0个Feed故障、0个转换器错误、0个RTP帧缓冲警告。10秒性能样本为整机逻辑CPU约`11.8%`、工作集约`1.32GB`、私有内存约`1.96GB`。尚未验证：长时间稳定性、端到端延迟、四台独立摄像机和真实四块物理屏映射；不能描述为四路监控已经完整交付。

## 映射约定

| 业务名称 | 场景相机 | Unity Display 索引 | 预留视频面 |
| --- | --- | ---: | --- |
| Display 1 | `Display1Camera` | 0 | `Display1VideoSurface` |
| Display 2 | `Display2Camera` | 1 | `Display2VideoSurface` |
| Display 3 | `Display3Camera` | 2 | `Display3VideoSurface` |
| Display 4 | `Display4Camera` | 3 | `Display4VideoSurface` |

用户口中的 Display 1～4 对应 Unity 的零基索引 0～3。运行时已经在`Awake`阶段显式激活可用的索引1～3；真实四块实体屏的排列与映射仍待现场验收。

## 场景层级

```text
Application
Displays
├─ Display1
│  ├─ Display1Camera
│  ├─ Display1VideoSurface
│  └─ Display1Placeholder
├─ Display2
├─ Display3
└─ Display4
```

Display 2～4 与 Display 1 使用相同结构。`VideoSurface` 挂载 `FeedSurfacePresenter`；`DisplayN` 分组根节点挂载 `CameraFeedController`，`feedId` 固定为 `feed1`～`feed4`，与 `displayIndex 0～3` 一一对应；`Application` 根节点挂载 `FourDisplayBootstrap`，持有四个 Controller 的有序引用。场景仅在 `Display1Camera` 上保留一个 `AudioListener`。用户添加的单个 `Canvas` 由配置中的 `canvasDisplayIndex` 路由到一块屏幕，`-1` 时隐藏。

## 配置与凭据

- 非敏感配置模板：[`Assets/StreamingAssets/HikvisionFourDisplay/feeds.template.json`](Assets/StreamingAssets/HikvisionFourDisplay/feeds.template.json)（随包发布）。当前现场测试默认四路均启用并指向`192.168.199.71`的逻辑通道`1`主码流，四路共享`feed1`凭据键；模板不包含账号或密码。
- 运行时实际配置：`Config/feeds.json`，位置为可执行文件同级目录（Editor 下是 `HikvisionFourDisplay/Config/feeds.json`，与 `Assets/` 同级）。首次运行若不存在会从模板种子生成；每次Windows Player构建结束后，构建后处理器还会把工程当前配置按`FeedConfigurationDocument`字段净化，再写到构建目录的`Config/feeds.json`，防止复用旧输出目录时继续读取旧IP。该文件已加入`.gitignore`，不会被提交。
- 凭据存取：`Tools/Hikvision Four Display/Manage Feed Credentials` 菜单打开编辑器工具，把账号密码写入 Windows 凭据管理器（`CRED_TYPE_GENERIC`，DPAPI 保护，仅当前 Windows 用户可读）。账号密码不会出现在场景、源码、`StreamingAssets`、普通 JSON、日志或发布包中。

## 运行时配置面板

- Display 1 右上角的“视频源配置”按钮打开蓝色配置面板。每路独立填写地址、RTSP端口、逻辑通道、主/子码流及登录信息；本路“保存并连接”只调用 `ReloadFeed(index)`，其余画面保持播放。密码留空保留原值，修改登录信息创建新的安全凭据引用，避免影响其他画面及旧方案。
- “选择摄像头”列出新增的12路：视频监控主机 `.200`、机房 `.202/.203`、港口实训中心 `.204`、沙盘 `.201`、场桥1 `.211/.212`、场桥2 `.213/.214`、岸桥 `.215/.216`、岸桥监控 `.100`。默认逻辑通道为 `1`、RTSP端口 `554`、主码流；监控主机可按实际接入通道修改。目录为 `Config/cameras.json`，缺省时使用随包的同名文件。目录仅包含安全凭据引用；在其他电脑或其他Windows账号运行时，需要在配置面板输入登录信息。
- “方案名称”填写后点击“保存四屏方案”，将当前面板的四路草稿保存为 `Config/schemes.json` 中的一套方案，并生成选择按钮。点击方案按钮才应用，方案按钮仅在配置面板内显示，关闭面板后隐藏；同名保存更新原方案。每个方案右侧有“删除”按钮；删除后立即刷新列表，当前播放配置与安全凭据保持，删除正在使用的方案只清除方案选中标记，不切换或重连画面。方案保存四路地址、启用状态、码流、通道、安全凭据引用、旋转角度和唯一Canvas目标屏。切换只重连连接参数变化的画面；最后选中的配置在下次启动时恢复。损坏或不足四路的方案拒绝读取/覆盖，保留原文件。
- 每路“左转90°”“右转90°”按钮立即旋转当前画面并保存角度，不重连视频。90°/270°自动按旋转后的宽高比适配；启动、断流、认证失败时的默认图保持正向。
- 配置面板使用 IMGUI（`OnGUI`），在多屏 Standalone 中只渲染到主显示器（索引0）。用户新增的场景Canvas和 `UiDemo` 则依赖工程已有的UGUI包；当前完整验证及构建必须正常加载包管理器。
- 这是给现场只有 Standalone Player、没有 Unity 编辑器的操作人员用的运行时工具，与仅限编辑器内使用的 `Tools/Hikvision Four Display/Manage Feed Credentials` 互补。

2026-10-06 已完成正常Unity导入、完整Windows构建及274项配置/显示/UI入口验证。新包为 `Builds/2026-10-06/Hikvision Four Display.exe`，保留已保存的Main及Canvas；原HKV包未覆盖。当前主机没有接入192.168.199现场网段，12路新设备登录取流及四块实体屏仍需现场验收。详细证据见 `../PortTraining/Docs/讨论记录/2026-10-06.md`。

## RTSP 解码依赖（LibVLCSharp）

- 托管/原生文件位置、许可证（LGPL-2.1-or-later）、插件裁剪清单见 [`Docs/THIRD-PARTY-NOTICES.md`](Docs/THIRD-PARTY-NOTICES.md)；PoC 对比与运行时验证结果见 [`Docs/解码方案PoC.md`](Docs/解码方案PoC.md)。
- `VlcRuntime/` 在项目根目录、`Assets/` 之外，Editor使用其中同级的`libvlc.dll`、`libvlccore.dll`和`plugins/`完整运行库。不要把插件树移进 `Assets/`，Unity 的原生插件导入会打平其中的子目录结构，导致libvlc模块扫描失败（已实测复现并记录根因）。`Assets/HikvisionFourDisplay/Editor/LibVlcBuildPostprocessor.cs` 会在每次Player构建后把插件树复制到构建输出里`libvlc.dll`同级的`plugins/`子目录。
- 当前默认解码器已切换为 `LibVlcDecoderFactory`；同时修复了libvlc先报告`Playing`、后创建纹理时显示层可能一直隐藏的问题。真实`102`子码流已触发解码/贴图回调，Editor主码流也已完成可见画面验收；针对首次画面的上下倒置，显示材质增加的Y轴UV翻转已经通过重新Play截图验收。

## 打开与重建场景

1. 使用 Unity Hub 或编辑器 `C:/Program Files/Unity 6000.0.42f1/Editor/Unity.exe` 打开本目录。
2. 打开 `Assets/HikvisionFourDisplay/Scenes/Main.unity`。
3. 如需重新生成骨架，执行菜单 `Tools/Hikvision Four Display/Create Scene Scaffold`。
4. 静态检查入口为 `Tools/Hikvision Four Display/Validate Scene Scaffold`。

早期PoC曾使用 `-noUpm` 绕开包管理器初始化问题。当前已保存的Canvas使用UGUI，禁用UPM会使 `UiDemo.cs` 找不到 `UnityEngine.UI.Text`，不能用于完整验证或打包。2026-10-06 已通过正常UPM加载完成验证和完整Windows构建：

```powershell
& "C:\Program Files\Unity 6000.0.42f1\Editor\Unity.exe" -projectPath "D:\wuhanxiangmu\HikvisionFourDisplay"
```

后续完整开发要求、验收条件和现场待确认信息见 `CLAUDE_TASK.md`。
