# IPC 接入边界

> 2026-09-15 场桥范围更正：`PortGatewayService`实时管道已经退出当前场桥S7交付路径。S7点位与连接仅放在独立`YardCraneS7Integration`场景；本目录不实现PLC连接，保留给未来共享业务契约/协调器IPC适配。

本目录后续只放场桥场景对共享 `PortTraining.Contracts` 的适配代码：

- Coordinator Named Pipe：工单、运行状态、事件、完成请求和双心跳。
- Gateway Named Pipe：历史方案中的租约握手、实时输入帧、设备反馈帧和安全状态；不用于当前场桥S7直连。

当前尚未实现 IPC。实现前必须先建立共享契约包，禁止在此目录复制一套协议 DTO。
