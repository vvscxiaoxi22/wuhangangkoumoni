# 第三方依赖与许可证

## LibVLCSharp / libvlc (VideoLAN)

- 组件：`LibVLCSharp` 3.10.1（托管绑定）+ `VideoLAN.LibVLC.Windows` 3.0.23.1（原生 `libvlc`/`libvlccore` 运行库及插件）。
- 来源：NuGet.org（`https://api.nuget.org/v3-flatcontainer/{libvlcsharp|videolan.libvlc.windows}/...`），发布方 VideoLAN。
- 许可证：`LGPL-2.1-or-later`（两个包的 `.nuspec` 均确认）。项目以动态链接方式使用（`libvlc.dll`/`libvlccore.dll` 独立 DLL，运行时加载，未静态链接、未修改源码），满足 LGPL 对闭源应用的动态链接豁免条款，本工程本身不需要因此开源。
- 项目中的位置：
  - 托管程序集：`Assets/Plugins/Managed/LibVLCSharp.dll`
  - 原生运行库：`Assets/Plugins/x86_64/libvlc.dll`、`Assets/Plugins/x86_64/libvlccore.dll`（Unity 原生插件，随 Editor/Player 正常打包）
  - VLC 插件模块：`VlcRuntime/plugins/`（项目根目录下、`Assets/` 之外，win64，已裁剪，见下）。放在 `Assets/` 之外是因为 Unity 的原生插件导入会把 `Assets/Plugins/<platform>/` 下的所有 DLL 打平复制到构建输出的同一层目录，不保留 `plugins/access/`、`plugins/codec/` 这类子目录结构，而 libvlc 的模块扫描必须依赖这个子目录结构（已用真实构建验证：打平后 `libvlc_new()` 会以 `VLCException: Failed to perform instanciation on the native side` 失败）。`Assets/HikvisionFourDisplay/Editor/LibVlcBuildPostprocessor.cs` 在每次 Player 构建后把 `VlcRuntime/plugins` 原样复制到 `<Build>_Data/Plugins/x86_64/plugins`（与 `libvlc.dll` 同级），Editor 内直接从 `VlcRuntime/plugins` 读取。
- 均通过仓库根 `.gitattributes` 的 `*.dll filter=lfs` 规则纳入 Git LFS，不会以明文二进制形式膨胀仓库历史。

### 插件裁剪说明

`VideoLAN.LibVLC.Windows` 原始 win64 插件目录约 98MB、27 个分类、323 个 DLL，覆盖 VLC 全部能力（含音频输出、字幕渲染、可视化、重新混流/转码、D3D9/D3D11 硬件窗口输出等）。本工程只需要"解码 RTSP H.264/H.265 视频，写入内存回调缓冲区，不需要音频、不需要独立播放窗口、不需要转码/重新推流"，因此裁剪掉以下与该目标无关的分类以减少约 22MB 体积：

`access_output`、`stream_out`、`text_renderer`、`audio_filter`、`visualization`、`meta_engine`、`services_discovery`、`spu`、`mux`、`audio_output`、`d3d11`、`video_splitter`、`d3d9`、`keystore`、`audio_mixer`

保留：`access`、`codec`、`demux`、`logger`、`lua`、`misc`、`packetizer`、`stream_extractor`、`stream_filter`、`video_chroma`、`video_filter`、`video_output`（`video_output` 内含本工程实际使用的 `vmem`/内存回调输出模块，不可裁剪）。

**这份裁剪未经真实摄像机联调验证**——如果后续真实联调时出现"能连接但解码/显示异常"的问题，第一步应该是从 `extracted/native/build/x64/plugins` 原始目录里把对应分类整体拷回来，而不是先怀疑上层解码器代码。

## 内置 Unity 内容

- `Unlit/Texture` 着色器：Unity 内置，随引擎许可证覆盖，无需额外声明；已通过 `FourDisplaySceneBuilder.EnsureFeedShaderIsBuilt` 加入 GraphicsSettings 的 Always Included Shaders，避免 Player 构建裁剪。
