# HikvisionFourDisplay 协作规则

## 工程定位

- 本工程是独立的四屏监控显示程序，不属于 `PortTraining` 学生端或 `YardCraneSimulator` 场桥仿真程序。
- 固定使用 Unity `6000.0.42f1`，目标平台为 Windows 64 位。
- 唯一启动场景为 `Assets/HikvisionFourDisplay/Scenes/Main.unity`。
- 功能范围只有四台物理显示器分别展示四路海康网络摄像机实时画面，不扩展录像、回放、云台、报警、用户管理或 PLC 功能。

## 开始工作前

1. 阅读仓库根目录 `AGENTS.md`。
2. 阅读 `README.md` 与 `CLAUDE_TASK.md`。
3. 阅读 `../PortTraining/LM-Wiki/03-规则层/任务验证与文档交付规范.md`。
4. 阅读当天的 `../PortTraining/Docs/讨论记录/YYYY-MM-DD.md`；完成实际验证后再按交付规范追加验证记录。
5. 检查工作区状态，保护其他工程和用户已有改动。

## 实现约束

- 保持 `Display1Camera`～`Display4Camera` 与 Unity Display 索引 `0`～`3` 的固定映射，不按组件枚举顺序猜测身份。
- 场景只保留一个启用的 `AudioListener`；监控音频默认关闭，除非用户另行确认需求。
- 视频解码、显示路由、配置、状态提示和凭据存取应分离，单路故障不得阻塞其余三路。
- 不假定 Unity 原生 `VideoPlayer` 能稳定播放 RTSP；先以目标设备和候选解码方案完成 PoC，再锁定依赖及许可证。
- 摄像机用户名、密码、令牌不得写入场景、源码、 `StreamingAssets`、普通 JSON、日志、命令行或发布包。配置只保存非敏感连接信息及安全凭据引用。
- 日志中的 RTSP 地址必须脱敏，不能输出用户名、密码或含凭据的完整 URL。
- 不自动升级 Unity 版本，不修改 `PortTraining`、`YardCraneSimulator` 或 `PortGatewayService` 来实现本工程功能。

## 验证门禁

- Unity 编译和场景静态校验只证明工程骨架有效，不等于四屏功能通过。
- 四屏功能必须在 Windows 扩展桌面的四块实体屏上使用 Standalone Player 验证。
- 实时视频必须用真实 DS-2CD3146FWD-IS 设备验证四路同时播放、画面不串屏、单路断开与恢复、认证失败和退出资源释放。
- 验证后按仓库规范记录版本、环境、步骤、日志、结果及未验证范围；只读审查不得修改讨论记录；缺少实体屏或摄像机时只能标记为“实现完成 / 待验证”。
