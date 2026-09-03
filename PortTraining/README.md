# PortTraining

港口实训平台学生端 Unity 工程。

> 港口实训平台全部软件的当前开发基线见[项目开发基线](LM-Wiki/02-知识层-编译层/项目开发基线-2026-08-28.md)。本README只描述当前学生端工程。

> 截止当前的真实实现状态、未完成项和开发优先级见[项目开发现状与待开发清单](LM-Wiki/02-知识层-编译层/项目开发现状与待开发清单.md)。

> 所有跨软件开发必须遵循[接口与参数标准V1.1](LM-Wiki/02-知识层-编译层/跨软件接口与参数标准-v1.1.md)，不得继续按V1.0草案生成代码。

## 当前阶段

- Unity：6000.5.5f1
- 目标平台：Windows 64 位
- 已完成：工位识别、QC/YC模块选择、Gateway模拟、学生登录、启动诊断和工作台
- 当前主流程：港口业务工单接收 → 设备准备检查 → 进入岸桥/场桥驾驶室
- 自由练习作为辅助入口保留，不占用正式工单
- 三维岸桥/场桥驾驶室将在下一阶段接入

## 工单开发数据

开发工单位于 `Assets/StreamingAssets/WorkOrders/work-orders.json`，同时提供 QC、YC 联合作业子工单。正式环境中，同一 `WorkOrderSnapshot` 可由教师端或 TOS 适配器生成，学生端不直接访问生产 TOS。

## 工位配置

开发配置位于 `Assets/StreamingAssets/Config/station.json`。部署时每个工位使用自己的配置文件：

- 岸桥：`machineType = QC`
- 场桥：`machineType = YC`

当前`Control/EmbeddedGateway`只用于模拟输入和接口原型。正式架构采用独立本机后台进程`PortGatewayService`独占S7连接；学生端和外部驾驶场景只读取标准设备语义，不直接引用PLC地址。

同一个 `PortTraining.exe` 支持两种部署模式：

- `DesktopSimulation`：电脑开发和教学演示，使用键鼠/虚拟输入与模拟 Gateway。
- `SimulatorCabin`：实体模拟机，使用岸桥或场桥操控台、PLC、S7 Gateway 与多屏显示配置。

三维设备、工单、评分和场景脚本只能读取 `ControlInputRouter` 输出的统一操作语义，不得直接判断键盘、手柄型号或 PLC 地址。部署切换只修改 `station.json`，不重新制作客户端。
