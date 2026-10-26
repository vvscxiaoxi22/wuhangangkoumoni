# Claude 开发任务：海康四路监控四屏显示

## 任务目标

在现有 `HikvisionFourDisplay` Unity 工程中完成一个 Windows 64 位程序：同一台主机连接四块物理显示器，程序同时拉取四台海康威视 `DS-2CD3146FWD-IS (2.8 mm)` 网络摄像机的实时视频，每路画面固定全屏显示到一块物理屏幕。

功能保持最小化，不增加录像、回放、云台控制、报警、用户管理、九宫格、PLC 或其他业务功能。

## 已有基线

- Unity：`6000.0.42f1`，禁止自动升级。
- 目标平台：Windows 64 位。
- 唯一启动场景：`Assets/HikvisionFourDisplay/Scenes/Main.unity`。
- 场景已经有 `Display1`～`Display4`、`Display1Camera`～`Display4Camera` 和四个 `VideoSurface` 占位面。
- 相机的 `targetDisplay` 已固定为 `0`～`3`，视口均为全屏；只有第一台相机有 `AudioListener`。
- 当前仅为可辨识的色块占位骨架，没有任何 RTSP 或运行时多屏实现。

修改范围是硬边界：只允许修改 `HikvisionFourDisplay/**`，以及在完成实际验证后按交付规范追加当天的 `PortTraining/Docs/讨论记录/YYYY-MM-DD.md`。不要修改另外两个 Unity 工程来完成本任务；只读审查不得修改任何文件。

## 执行方式边界

本文档的完整任务需要一个能够打开 Unity、评估并接入已获授权的解码依赖、构建 Windows Player，并在目标机访问四块实体屏和四台摄像机的开发会话。

如果任务通过 `PortTraining/Tools/ClaudeCollaboration/Invoke-ClaudeTask.ps1` 的受控模式执行，必须遵守该工具禁止启动 Unity、安装依赖、访问网络和连接外部设备的限制。此时把工作拆开：受控 Claude 只实现合同允许文件内的多屏激活代码、非敏感配置模型、凭据接口、状态/重连状态机、解码器抽象和模拟源；解码方案 PoC、依赖安装、Unity 编译/构建、实体四屏和真实摄像机验收由具备相应权限的主控会话另行完成。任何一方都不得把未实际运行的部分写成已验证。

## 功能要求

### 1. 四块物理屏幕

- Windows 使用“扩展这些显示器”，不能使用复制模式。
- 保持以下固定映射：
  - Camera 1 → `Display1Camera` → Unity Display 0 → 物理屏 1。
  - Camera 2 → `Display2Camera` → Unity Display 1 → 物理屏 2。
  - Camera 3 → `Display3Camera` → Unity Display 2 → 物理屏 3。
  - Camera 4 → `Display4Camera` → Unity Display 3 → 物理屏 4。
- Standalone Player 启动时显式激活可用的 Display 1～3（Unity 索引），每块额外 Display 在一次应用生命周期内只激活一次。显示器少于四块时不得崩溃，应保留已有输出并给出清晰、脱敏的告警。
- 不按 `FindObjectsOfType` 的返回顺序推断摄像机或屏幕身份；使用序列化引用或稳定 ID。
- 记录检测到的屏幕数量、激活结果、分辨率和最终映射，便于现场排障。

### 2. 四路实时视频

- 四路连接彼此独立；一路连接、解码或认证失败不能卡住或清空其余三路。
- 每路视频输出到场景中对应的 `DisplayNVideoSurface`，不得串屏。当前四个占位面共享 Unity 内置默认材质；接流时必须为每路创建独立 Unlit 材质，或使用不会修改 `sharedMaterial` 的独立纹理绑定方式。
- 保持原始宽高比，必要时使用黑边，不得拉伸变形。
- 音频默认关闭，避免四路声音同时播放；如后续确认需要声音，再单独设计选择策略。
- 正常退出、场景卸载和重连时必须释放解码线程、原生句柄、纹理和网络连接。

设备官方资料确认该型号支持 RTSP，主码流支持 H.265/H.264，子码流支持 H.265/H.264/MJPEG。常见通道路径为：

- 主码流：`rtsp://<host>:<port>/Streaming/Channels/101`
- 子码流：`rtsp://<host>:<port>/Streaming/Channels/102`

用户名和密码应通过解码器的认证参数或只在内存中组合，不能把含凭据 URL 写入文件或日志。先用 VLC 或厂商工具逐台确认实际地址、端口、账号权限、编码和码流，再在 Unity 中联调。

不要直接假定 Unity 原生 `VideoPlayer` 能满足 RTSP 的稳定性和低延迟。先对可用方案做最小 PoC（例如 FFmpeg、LibVLC 或已授权的专业 Unity 视频插件），记录：Unity 6/Windows 支持、H.264/H.265、四路并发、硬件解码、延迟、原生依赖打包方式、许可证和退出清理，再锁定一个方案。未经用户确认不要购买或引入收费插件。

### 3. 配置与凭据

建立版本化的非敏感配置，至少为每路保存：

- `feedId`
- `displayIndex`
- `host`
- `rtspPort`
- `channel`
- `streamType`（Main/Sub）
- `credentialKey`
- 是否启用

配置中不得出现明文用户名或密码。凭据使用 Windows 凭据管理器、DPAPI 保护的用户级文件或等价安全方式保存；选择一种后记录原因、初始化步骤和恢复方式。任何示例配置只能使用明显的占位地址。

### 4. 状态与恢复

每块屏幕至少能区分：未配置、连接中、播放中、认证失败、网络中断、格式不支持和正在重连。提示不得覆盖其他屏幕，也不得泄露凭据。

- 连接失败使用有上限的退避重试，避免高频死循环。
- 摄像机或交换机短时断开后应自动恢复。
- 配置错误或不支持的编码应持续显示明确故障，而不是黑屏无说明。
- 日志应带 `feedId` 和 `displayIndex`，但必须对地址及凭据做必要脱敏。

## 建议结构

把职责拆开，不要写成一个超大 MonoBehaviour：

```text
FourDisplayBootstrap       激活屏幕、绑定稳定映射、应用生命周期
FeedConfiguration          读取并校验非敏感配置
CredentialProvider         获取用户级安全凭据
IVideoStreamDecoder        解码器抽象与状态事件
CameraFeedController       单路连接、重连、释放
FeedSurfacePresenter       将纹理和状态显示到指定视频面
```

四路 Controller 使用四个独立实例，共享的原生运行库可以集中初始化，但不能形成“一路失败导致全部重启”的耦合。

## 验收条件

### 工程与静态检查

- Unity `6000.0.42f1` 无编译错误。
- 主场景恰好四台目标相机，名称和 `targetDisplay=0～3` 映射正确，全部为全屏视口且不残留 RenderTexture。
- 只有一个启用的 `AudioListener`。
- Build Settings 只有 `Main.unity`。
- 项目中没有真实 IP、账号、密码、令牌或含凭据的 RTSP URL。

### 四屏目标机

- 使用 Windows Standalone Player 在四块实体屏上验证，而不是只看 Editor Game View。
- 启动时四块屏分别出现唯一编号，随后各自显示正确摄像机画面。
- Windows 缩放、分辨率及屏幕排列被记录；程序重启后映射仍一致。
- 少接一块显示器时程序不崩溃，并能从日志和屏幕提示中判断缺少哪个输出。

### 真实摄像机

- 四路同时连续播放，画面不互换、不拉伸、无共享纹理串流。
- 分别验证单台摄像机断网、恢复、设备重启、错误密码和不支持编码；另外三路持续播放。
- 记录主/子码流、分辨率、帧率、编码、RTSP 传输方式、端到端延迟以及 CPU/GPU/内存占用，不用主观“很流畅”代替数据。
- 退出 Player 后不存在残留解码进程、线程或被占用的原生文件。

## 当前仍需现场提供

- 四台摄像机的 IP、RTSP 端口和安全凭据。
- 使用主码流还是子码流，以及设备当前实际编码设置。
- 四块屏幕的分辨率、Windows 编号、相对排列和缩放比例。
- 可接受的延迟、帧率及是否需要任一路音频。

这些信息未提供前可以完成架构、配置模板、占位状态和模拟流验证，但不能宣称真实设备与四屏功能已经验收。

## 交付要求

严格执行“实现完成 → 验证通过 → 文档更新 → 任务交付”。交付时必须分别列出：实际修改、实际执行的验证、仅静态推断、未验证范围和下一步，并把关键证据追加到当天讨论记录。

参考资料：

- 海康设备规格：`https://dealer-static.hikvision.com/upload/file/doc/DOC000068986-DS-2CD3146FWD-I(S).pdf`
- 海康 RTSP 通道说明：`https://www.hikvision.com/content/dam/hikvision/de/quick-start-guide/RTSP_und_HTTP_Pfade_fuer_Bilder_und_Videostreams_bei_IP-Kameras.pdf`
- Unity Multi-display：`https://docs.unity3d.com/6000.0/Documentation/Manual/MultiDisplay.html`
