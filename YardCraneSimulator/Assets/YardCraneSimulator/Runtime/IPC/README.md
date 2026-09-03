# IPC 接入边界

本目录后续只放场桥场景对共享 `PortTraining.Contracts` 的适配代码：

- Coordinator Named Pipe：工单、运行状态、事件、完成请求和双心跳。
- Gateway Named Pipe：租约握手、实时输入帧、设备反馈帧和安全状态。

当前尚未实现 IPC。实现前必须先建立共享契约包，禁止在此目录复制一套协议 DTO。

