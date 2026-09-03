# YC 场桥模型层级使用说明

本文件是工程内快速操作说明。规范性依据为`F:/wuhanxiangmu/PortTraining/LM-Wiki/02-知识层-编译层/场桥真实模型接入契约-v1.0.md`；两者冲突时以该契约为准。

场景根节点为 `YC_RuntimeRoot`。美术模型只能拖入各机构的 `Visual` 节点，控制脚本、刚体、碰撞体、传感器和绳索锚点不要挂到 FBX 内部节点。

## 模型放置规则

- 整体原始 FBX 暂存：`00_ScenePlacement/ImportedModel_Source`
- 不随小车运动的门架、支腿、大车和车轮：`10_GantryMotion/Visual`
- 随小车横移的结构：`20_TrolleyMotion/Visual`
- 随吊具升降、摆动和回转的结构：`30_SpreaderPhysics/Visual`
- 箱体模型：`50_ContainerWorkspace/ActiveContainer`

## 新八绳 MAX 命名

本工程不再用四根独立直绳代表正式场桥。每个 MAX 回路都按“卷筒出绳点 → 吊具上架滑轮 → 小车固定端”构成一个三角形，八个回路分为两个卷筒组，每组四个回路。

- 卷筒出绳点：`20_TrolleyMotion/Hoist/Drum_A|B/RopeExits`
- 小车固定端：`20_TrolleyMotion/RopeDeadEnds`
- 吊具上架滑轮：`30_SpreaderPhysics/RopeSheaves`
- 运行时可视绳段：`40_RopeRig/EightRopeLoops`
- `L01-L04` 当前为 X 向防摇三角回路，`L05-L08` 为 Z 向防摇三角回路。
- `30_SpreaderPhysics` 在本阶段代表“吊具上架 + 锁定吊具”的组合刚体；真实模型的上架和吊具分别放入 `Headblock/Visual` 与 `35_Spreader/Visual`。

当前点位是验证物理拓扑的参数化灰盒，不是青岛港设备施工图尺寸。导入真实模型后必须按卷筒、固定端和上架滑轮中心重新标定，不能直接把默认坐标作为最终数据。

## 操作顺序

1. 先把完整 FBX 拖入 `ImportedModel_Source`，保持位置和旋转归零、缩放为 1。
2. 将明确属于门架、小车、吊具的可见节点分别拖入对应的 `Visual`。
3. 如果原 FBX 无法拆开，不要复制同一模型三次；先保留在 `ImportedModel_Source`，再回 Blender/3ds Max 按刚体机构重新拆分导出。
4. 八组出绳点、固定端和上架滑轮当前只是 MAX 拓扑参考位置，必须根据最终模型实测调整。
5. 不要把 `SpreaderPhysics` 放到 `TrolleyMotion` 下，否则吊具无法形成独立摆动。

## 灰盒替换规则

- `Graybox_Placeholder` 下的 Cube 仅用于模型到位前的机构辨识：蓝色为门架、黄色为小车、青色为吊具、橙色为集装箱。
- 真实模型仍拖入对应的 `Visual`，不要拖入 `Graybox_Placeholder`。
- 真实模型、锚点和碰撞体标定完成并通过v13回归后，在 `YC_RuntimeRoot` 的 `YardCraneEightRopeMaxPrototypeRig` 组件中取消 `Show Graybox`；运行时也可按 `G` 切换。
- 隐藏或删除灰盒前，不要删除 `Visual`、`Physics`、`Hoist`、`RopeDeadEnds`、`RopeSheaves` 和 `40_RopeRig` 等稳定机构节点。
- 运行测试：`A/D` 控制小车、`W/S` 控制双卷筒同步起升、`Space` 施加摆动扰动、`R` 重置。
