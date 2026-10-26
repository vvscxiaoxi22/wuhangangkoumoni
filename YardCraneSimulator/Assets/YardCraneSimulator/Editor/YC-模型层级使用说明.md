# YC 场桥模型层级使用说明

本文件是工程内快速操作说明。规范性依据为`D:/wuhanxiangmu/PortTraining/LM-Wiki/02-知识层-编译层/场桥真实模型接入契约-v1.0.md`；两者冲突时以该契约为准。

场景根节点为 `YC_RuntimeRoot`。美术模型只能拖入各机构的 `Visual` 节点，控制脚本、刚体、碰撞体、传感器和绳索锚点不要挂到 FBX 内部节点。

## 模型放置规则

- 整体原始 FBX 暂存：`00_ScenePlacement/ImportedModel_Source`
- 不随小车运动的门架、支腿、大车和车轮：`10_GantryMotion/Visual`
- 随小车横移的结构：`20_TrolleyMotion/Visual`
- 随吊具升降、摆动和回转的结构：`30_SpreaderPhysics/Visual`
- 箱体模型：`50_ContainerWorkspace/ActiveContainer`

## 八根独立直绳命名

2026-09-14按当前真实吊具模型修正：运行时采用8根独立直绳，每根只连接一个小车侧上锚点和一个吊具上架侧下锚点，并且只生成一个Obi Rope。旧版三角回路的两组上端节点继续保留为兼容候选点，但不会再同时连接到同一根绳。

- 上锚点候选A：`20_TrolleyMotion/Hoist/Drum_A|B/RopeExits`
- 上锚点候选B（兼容旧层级）：`20_TrolleyMotion/RopeDeadEnds`
- 吊具端下锚点：`30_SpreaderPhysics/RopeSheaves`
- 运行时8根可视绳：`40_RopeRig/EightRopeLoops`
- `L01-L04`用于X向成对交叉，`L05-L08`用于Z向成对交叉；配置中的`topAnchorPath`决定每根绳唯一采用哪个上锚点。
- 当前平衡映射：`L01=Exit01`、`L02=DeadEnd02`、`L03=Exit03`、`L04=DeadEnd04`、`L05=Exit05`、`L06=Exit06`、`L07=DeadEnd07`、`L08=DeadEnd08`。
- `30_SpreaderPhysics` 在本阶段代表“吊具上架 + 锁定吊具”的组合刚体；真实模型的上架和吊具分别放入 `Headblock/Visual` 与 `35_Spreader/Visual`。
- 根据2026-09-14上传的吊具结构图，8个`Loop##_Sheave`应放在中央上架机构真实钢丝绳落点；不要放到伸缩梁最外端、锁销或四个吊具角点上。它们是独立的不可见语义点，移动时不需要改FBX节点。

当前点位是验证物理拓扑的参数化灰盒，不是青岛港设备施工图尺寸。导入真实模型后必须按8根真实绳索的上下端点重新标定，不能直接把默认坐标作为最终数据。模型自带的8根静态绳网格与运行时8根Obi绳只能显示一套；同时显示会再次看成16根。

## 操作顺序

1. 先把完整 FBX 拖入 `ImportedModel_Source`，保持位置和旋转归零、缩放为 1。
2. 将明确属于门架、小车、吊具的可见节点分别拖入对应的 `Visual`。
3. 如果原 FBX 无法拆开，不要复制同一模型三次；先保留在 `ImportedModel_Source`，再回 Blender/3ds Max 按刚体机构重新拆分导出。
4. 按真实模型逐根校准8个`topAnchorPath`对应的上锚点和8个`RopeSheaves`下锚点，并检查X/Z正负方向受力成对平衡。
5. 不要把 `SpreaderPhysics` 放到 `TrolleyMotion` 下，否则吊具无法形成独立摆动。

## 灰盒替换规则

- `Graybox_Placeholder` 下的 Cube 仅用于模型到位前的机构辨识：蓝色为门架、黄色为小车、青色为吊具、橙色为集装箱。
- 真实模型仍拖入对应的 `Visual`，不要拖入 `Graybox_Placeholder`。
- 真实模型、锚点和碰撞体标定完成并通过v30直绳拓扑回归后，在 `YC_RuntimeRoot` 的 `YardCraneEightRopeMaxPrototypeRig` 组件中取消 `Show Graybox`；运行时也可按 `G` 切换。
- 隐藏或删除灰盒前，不要删除 `Visual`、`Physics`、`Hoist`、`RopeDeadEnds`、`RopeSheaves` 和 `40_RopeRig` 等稳定机构节点。
- 运行测试：先确认日志为`visualRopes=8, legacyDeadEndLegs=0`；`A/D`控制小车、`W/S`控制同步起升、`[/]`控制吊具旋转、`0`执行吊具回零，完整验证使用`Tools/Yard Crane/Tests/Run Full Play Verification`。
