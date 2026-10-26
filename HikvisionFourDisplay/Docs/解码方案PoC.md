# RTSP 解码方案 PoC 对比

状态：**LibVLCSharp方案已下载、集成、编译，并完成现场单路RTSP认证、H.264解码和Unity贴图回调验证**（四路并发、四块实体屏与画面质量仍待验收，见文末“运行时验证结果”）。本文档是 `CLAUDE_TASK.md` 要求的“先做PoC再锁定方案”的产出。

## 候选方案

| 维度 | Unity 原生 `VideoPlayer` | FFmpeg（自行桥接） | LibVLCSharp（VideoLAN） | 商业插件（如 RenderHeads AVPro Video） |
| --- | --- | --- | --- | --- |
| Windows RTSP 支持 | 依赖 Media Foundation，RTSP 源不受官方保证，Unity 官方文档明确不建议用于直播流 | 成熟，`libavformat` 原生支持 RTSP/RTP | 成熟，`libvlc` 原生支持 RTSP，VLC 本身长期用于监控场景 | 成熟，厂商专门做过直播流优化 |
| H.264/H.265 | 不确定，取决于系统解码器 | 支持（`libavcodec`） | 支持（内部走 `libavcodec`） | 支持 |
| 四路并发 | 未验证，`VideoPlayer` 官方定位是本地/点播视频 | 需要自管 4 个 `AVFormatContext` + 线程，可行但代码量大 | 4 个独立 `LibVLC`/`MediaPlayer` 实例是常见用法，VLC 设计上支持多实例 | 官方支持多路 |
| 硬件解码 | 不可控 | 需要手动配置 `AVHWDeviceContext`（DXVA2/D3D11VA） | `libvlc` 内部可启用硬件解码，配置项更少 | 支持，且暴露开关 |
| 原生依赖打包 | 无需额外依赖 | 需要打包约 15-25 个 LGPL 构建的 DLL（`avcodec`/`avformat`/`avutil`/`swscale`…），需选用禁用 GPL 编码器的构建（如 gyan.dev / BtbN 的 `shared`+`lgpl` 版本） | 需要打包 `LibVLCSharp`（托管）+ VideoLAN 官方 `libvlc` 原生运行库（约数十 MB，可裁剪掉不需要的 demux/codec 模块） | 商业包自带，体积和授权由厂商约定 |
| 许可证 | Unity 自带 | LGPL（前提：使用禁用 GPL 组件的构建，动态链接），免费 | LGPL-2.1-or-later，动态链接免费商用无需开源自身代码 | 商业授权，需要付费购买 |
| 到 Unity 纹理的桥接工作量 | 无（若可用） | 大：手写 YUV→RGBA 转换或自定义纹理上传路径 | 小：官方 Unity 样例已提供 `libvlc_video_set_callbacks` 直接写入 `Texture2D` 的参考实现 | 小：插件自带 |
| 退出清理 | Unity 托管 | 需要手动管理线程、`AVFormatContext`、解码器上下文的释放顺序 | `MediaPlayer.Dispose()` / `LibVLC.Dispose()` 封装了释放 | 插件封装 |
| 是否需要用户付费确认 | 否 | 否 | 否 | **是**，未经确认不引入 |

## 结论与建议

- **排除** Unity 原生 `VideoPlayer`：`CLAUDE_TASK.md` 已明确不假定其能稳定播放 RTSP，且缺少四路并发和重连的官方保证。
- **排除（当前阶段）** 商业插件：功能最省心，但属于付费方案，未经用户确认不得购买/引入。
- **推荐方向：LibVLCSharp**。相对 FFmpeg 自行桥接，集成工作量显著更小（官方已有 Unity 纹理回调样例可参考），仍是免费的 LGPL 方案，四路独立实例是 VLC 的常规用法，且更贴合"先跑通、再按需优化硬件解码"的节奏。
- FFmpeg 方案保留作为备选：如果后续需要比 LibVLC 更细粒度的硬件解码控制或更小的原生依赖体积，可以在不改动 `CameraFeedController`/`FeedSurfacePresenter`/`FourDisplayBootstrap` 的前提下，另实现一个 `IVideoStreamDecoder`（如 `FfmpegStreamDecoder`），通过现有接口替换当前LibVLC实现。

## 已完成 / 未完成

- 已完成：
  - 架构层面把解码器隔离成 `IVideoStreamDecoder` 接口（`Assets/HikvisionFourDisplay/Runtime/Decoding/IVideoStreamDecoder.cs`），`MockPatternDecoder` 模拟源跑通配置读取、凭据获取、连接状态机、断线重连退避、纹理独立绑定、资源释放的完整链路；现场版本的默认解码器已经切换为`LibVlcDecoderFactory`。
  - 用户已批准下载并实际下载了 `LibVLCSharp` 3.10.1（NuGet，2.8MB）+ `VideoLAN.LibVLC.Windows` 3.0.23.1（NuGet，134,279,001 字节，即约 128MB，比最初估计的"数十MB"大得多，已在下载前向用户重新确认过实际大小）。
  - 已实现 `LibVlcStreamDecoder`（`Assets/HikvisionFourDisplay/Runtime/Decoding/LibVlcStreamDecoder.cs`）：真实 libvlc 回调（`SetVideoCallbacks`/`SetVideoFormatCallbacks`）、跨线程安全交接（libvlc 解码线程只写入锁保护的缓冲区，`Tick()` 才在 Unity 主线程读取/上传纹理/触发状态事件）、`Connect`/`Disconnect`/`Dispose` 生命周期。
  - 已实现并用真实构建验证 `Assets/HikvisionFourDisplay/Editor/LibVlcBuildPostprocessor.cs`：把 VLC 插件树从 `VlcRuntime/plugins`（项目根目录、`Assets/` 之外）复制到 `<Build>_Data/Plugins/x86_64/plugins`。
  - 已在 Windows Standalone Player（非 Editor）里用真实 libvlc 完整跑通占位地址的失败/重连路径。该测试曾把`:rtsp-user/:rtsp-pwd`作为Media选项传入，但服务端没有接受认证，因此它只能证明选项调用没有同步抛错，不能证明live555实际消费了凭据。
  - 过程中发现并修复一个真实原生集成 Bug：libvlc 的模块目录必须是 `libvlc.dll` 的同级 `plugins/` 子目录，放在别处（哪怕设置了 `VLC_PLUGIN_PATH`）会导致 `libvlc_new()` 抛 `VLCException: Failed to perform instanciation on the native side`；又发现 Unity 原生插件导入会打平 `Assets/Plugins/<platform>/` 下的多级子目录，因此把插件树移出 `Assets/` 并改用构建后处理脚本拷贝，见 `THIRD-PARTY-NOTICES.md`。
  - 出于体积考虑裁剪了约 22MB 明显无关的插件分类（音频输出、字幕、可视化、转码/重新推流、D3D9/D3D11 窗口输出等），裁剪后的分类清单同样记录在 `THIRD-PARTY-NOTICES.md`。裁剪后的插件集已通过上述连接/失败/重连全流程验证（说明裁剪没有影响到这条路径依赖的模块）。
- 已完成（补充：公网可达性与真实服务器 RTSP 协商验证，2026-09-13 第二轮）：
  - 出站网络在本环境不是统一放行/阻断，而是按主机各不相同：逐一实测了 6 个公开 RTSP 主机（`wowzaec2demo.streamlock.net`、`freja.hiof.no`、`rtsp.stream`、`demo.insta360.com`、`184.72.239.149`、`62.109.19.230`、`807e9439d5ca.entrypoint.cloud.wowza.com`、`ipvmdemo.dyndns.org`），只有 `170.93.143.139:554` 和 `9627b0bf2a7b.entrypoint.cloud.wowza.com:1935` 两个 TCP 可达，其余全部超时/拒绝。
  - 打开 `LibVLC.Log`（`enableDebugLogs=true`）拿到 libvlc 内部真实日志后确认：对这两个可达主机，`Connect()` 全流程（URL 解析 → `access=rtsp` 模块选择 → `live555` demux 实际发起 TCP 连接 → DESCRIBE → 解析 SDP）**都正常工作**；其中一个主机上 `live555` 清楚地报出 `RTP subsession 'video/H264'`（说明 SDP 里的 H.264 视频轨道被正确识别），最终在 SETUP 阶段收到服务端 `403 Forbidden` 才失败（`SETUP of 'video/H264' failed 403 Forbidden`），另一个主机在 DESCRIBE 阶段本身就 `connection timeout`。两者都是**对方服务端**的行为（过期的临时演示流、限制访问），不是本工程代码或 libvlc 集成的问题——这把"连接/失败/重连路径"的验证范围从"仅对着必然不可达的占位地址"扩展到了"对真实可达服务器走完 RTSP DESCRIBE 协商，直到 SETUP 这一步"。
  - 结论：libvlc的无凭据URL构造、demux/access模块选择和SDP解析均已证明可用；此前没有证据支持“Media级凭据选项已验证”的说法，该结论由下面的真实设备A/B测试更正。
- 已完成（现场摄像机真实认证与首帧，2026-09-21）：
  1. 对`.71`的`101`主码流与`102`子码流在同一TCP连接内完成RTSP Digest挑战，均取得`200 OK` SDP；两路视频均为H.264，另含PCMA音频。
  2. 相同凭据、URL和native运行库下，Media级`:rtsp-user/:rtsp-pwd`立即进入`Ended`且无视频轨道；改为每路独立`LibVLC`实例的`--rtsp-user/--rtsp-pwd`后进入并持续保持`Playing`，H.264解码器启动。工程已采用现场有效方式；凭据不进入URL或日志。
  3. 单路实流测试Player实际触发`OnVideoFormat`/`OnVideoLock`/`OnVideoDisplay`与Unity主线程纹理上传，日志出现`HFD_FEED_TEXTURE_READY`且连续观察无Fault或重连。`102`子码流的播放器可见尺寸为`640x360`，格式回调缓冲尺寸先后为`640x368`、`640x386`。
  4. Editor曾因native DLL与`VlcRuntime/plugins`不在同级目录而在`libvlc_new`阶段报`unknown option --rtsp-tcp`。把native DLL与`plugins/`统一到完整的`VlcRuntime/`目录后，用户重启Editor并实际Play，状态从`Connecting`进入`Playing`，日志得到`HFD_FEED_TEXTURE_READY width=2560 height=1442`，Game窗口显示连续实景，证明Editor原生运行库问题已解决。
  5. 首次可见画面上下倒置。代码审查确认libvlc的`RV32`缓冲从视频顶行开始，工程原先直接`LoadRawTextureData`且标准Quad使用Unity底部为`V=0`的UV；场景相机与四个Quad均为单位旋转。现已在`FeedSurfacePresenter`实例材质上设置`mainTextureScale=(1,-1)`、`mainTextureOffset=(0,1)`，通过GPU采样翻转而不增加四路CPU拷贝；Editor与Player程序集离线Roslyn编译通过，用户重新Play后的截图确认文字和场景方向均恢复正常。
  6. 主码流冷启动日志曾出现自动硬解/视频输出与`RV32`内存回调协商失败，随后回退`swscale`并成功出图；还出现两次live555初始RTP帧缓冲从`250000`、`500000`字节增长前丢弃帧尾。实例参数现增加`--avcodec-hw=none`，避免无效的硬解探测，并设置`--rtsp-frame-buffer-size=2000000`。参数已由当前VLC 3.0.23插件识别；四路同源Standalone冷启动实测四路均`Playing`并产生纹理，转换器错误、递归错误和RTP缓冲警告均为0。
  7. 发布默认配置现为四路全部指向`.71`、逻辑通道`1`主码流并共享`feed1`凭据键。最新测试包实际读取包根`Config/feeds.json`，四路分别取得`2560x1440`后稳定到`2560x1442`；10秒样本为整机逻辑CPU约`11.8%`、工作集约`1.32GB`、私有内存约`1.96GB`。该样本证明短时可运行，不替代长期内存与帧率测试。
- 未完成：
  1. 测量第一路端到端延迟，并修正设备OSD当前显示的`1970-01-01`时间。
  2. 四台独立摄像机的逐路认证与首帧，以及四路长期内存/帧率稳定性和断线恢复实测；四路同一摄像机的短时并发已通过。
  3. 用真实四块物理屏验证`FourDisplayBootstrap`的Display 1-3激活与映射；当前主机只检测到一块显示器。

参考资料同 `CLAUDE_TASK.md` 末尾列出的海康规格书、RTSP 路径说明和 Unity Multi-display 文档。
