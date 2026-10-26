# YC-YARD-01 堆场-箱位-集装箱-吊具最小完整闭环

## 1. 任务身份

- 任务编号：YC-YARD-01
- 任务名称：补齐"一个箱子从源箱位被选中→抓取→搬运→落到目标箱位→占用状态正确更新"闭环里，Claude这边代码做不到/需要Unity Play验证的剩余部分
- 执行模式：交互式C#编码 + 场景搭建（需要打开Unity编辑器操作Hierarchy/Inspector，并需要真实Play验证物理行为；不得通过`Invoke-ClaudeTask.ps1`受控限定开发模式执行）
- 主控：Codex
- 执行代理：具备Unity编辑器操作能力的交互式Claude Code或主控Codex

## 1.1 进度状态（2026-09-18更正——上一版"尚未开始执行"已过期，如实更正不静默覆盖）

- **第4.1/4.2节实现已完成，第4.3节部分完成**：Codex于2026-09-18继续推进本任务（额度中途用完，未留下交付说明，以下是Claude事后读代码核实的真实状态，不是Codex自述）。
  - `containerCollider.enabled`搬运碰撞、`UpdateReleasedContainerSettlement()`落箱静止确认+占用状态更新（源箱位锁上即清空、稳定后按容差匹配最近箱位`SetOccupant`、匹配不到如实记"released outside every compatible slot"，含`isKinematic`/空引用等边界情况的fail-closed处理）均已实现，代码读下来是成熟、自洽的实现。
  - `YardCrane.unity`场景已摆好3个`YardCraneContainer`实例（`YC20-001`/`YC40-001`/`YC-REGRESSION-30T`）+ 4个`YardCraneYardSlot`，每个容器都有完整的`LockHoles`四角碰撞体层级。
  - 离线Roslyn重新编译`ObiRopeTest`程序集（覆盖这部分改动）：`EXIT=0`，无错误无警告。
  - **一处未记录的设计变更**：`YardCraneContainer.EnsureLockHoleColliders()`从"运行时自动生成锁孔"改成了"只做校验，锁孔必须提前在场景/预制体里摆好"（新增`TryValidateLockHoleColliders()`）——本文件第3.1节仍描述的是旧的"自动生成"行为，已过期，以代码实际行为为准。
  - 第4.3节第1点未完成：两个独立预制体`Assets/Prefabs/20尺集装箱.prefab`/`40尺.prefab`当时完全没有`LockHoles`结构（场景不引用这两个文件，不是活跃bug，但预制体本身不完整）。Claude已在2026-09-18补齐`20尺集装箱.prefab`的锁孔+`lockHolesRoot`引用（照抄场景里已验证的Ft20样例）；`40尺.prefab`问题更大——完全没有`YardCraneContainer`组件、没有真实碰撞体，根节点还带着未清理的270°旋转和偏移位置，需要用户确认处理方式，尚未处理。
  - 第4.3节第2-4点（堆叠物理假设验证、吊具尺寸切换验证）以及第9节的全部真实Play验收，**完全没有做过**——这是当前最大的缺口，任何"已完成"的表述都不能替代这一步。
- **同一天并行做了一件不相关的事，供了解不影响本任务**：2026-09-17同一会话内还实现了"教师端『任务考核』发布→学生端『任务考核』接收"整条链路（新建`YardCraneAssignedAssessmentTaskStore`任务列表存储、`YardCraneTrainingScoreRecorder`新增`ListAssessmentRuns()`、教师端任务管理/考核统计页接真实数据），2026-09-18又推进了考核结果判定+存储+教师端UI联动。这条工作只碰`Runtime/Training/`和`Shell/`下的教师/学生端文件，**没有触碰本任务第7节列出的任何文件边界**，两条工作线互不依赖，详见`PortTraining/Docs/讨论记录/2026-09-17.md`、`2026-09-18.md`对应章节。

## 2. 背景与设计脉络

2026-09-17晚与用户逐步讨论定型的目标：学生能在`YardCrane`场景里，把堆场中一个真实集装箱从源箱位抓起、搬运、放到目标箱位，占用状态正确更新；今晚范围内20尺、40尺两种箱型都要能切换着抓。完整讨论过程见`PortTraining/Docs/讨论记录/2026-09-17.md`（若尚未回写，本任务执行者应先补一份，方法与本文件第7节的"讨论记录回写"一致）。核心设计决定：

1. **全程只有一个真正物理活跃的箱子**。堆场里的箱子默认是静态视觉（真实FBX+`YardCraneContainer`身份数据+真实碰撞体，但不常驻Rigidbody）。唯一"正在被操作"的那个箱子按阶段切换物理状态：搬运中应该有真实碰撞（本任务要补）；解锁瞬间给真实动态Rigidbody（**已经是现状行为**，见第3.4节）；稳定下来后要有"静止确认"（本任务要补），占用状态在那一刻才更新。
2. **抓取判定用物理引擎的射线传感器，不用纯几何计算**——这一条已经实现完（见第3节），本任务不重做，只在此基础上补齐后续环节。
3. **不能从堆的中间/底部抽箱子**——不专门写业务规则，用户判断只要堆场箱子有真实碰撞体，吊具下降时会先被最上面那个箱子的碰撞体挡住，物理上够不着更下面的箱子。**这个判断链条本任务需要用真实Play去验证是否真的成立**（见第4.3节），不能默认它一定对。
4. **落箱以后稳不稳，交给PhysX真实模拟，不由我们算出来**——箱子解锁后已经是真实动态Rigidbody（现状已如此），会不会倒、滑、卡住，是物理引擎跑出来的结果，我们只负责"何时认定它已经稳定"和"稳定后更新占用状态"。
5. **自由模式下**：允许放在任何地方；只有落点恰好落在某个箱位的对位容差内，才触发占用状态更新，放在空地上物理照常，但不进占用数据。
6. **两处按默认值执行，无需再讨论**：源箱位标记为空的时机＝一锁上就立刻标记（不等真正搬走）；堆场占用状态不跨场次持久化，每次进`YardCrane`场景从各`YardCraneYardSlot`的`Initial Occupant`重置。

## 3. 已经完成、本任务不要重做的部分（只读参考，理解现状用）

以下均已实现并做过大括号配平/`git diff --check`静态检查，**没有编译、没有Unity Play验证**——本任务执行时应当把它们一并纳入Play验证范围，不能假设它们已经工作正常。

### 3.1 `YardCraneContainer.cs`（`Experiments/ObiRope/`）

- 箱号/箱型（`YardCraneContainerType.Ft20`/`Ft40`）/皮重/毛重数据组件，`NominalDimensionsMeters`按ISO 668给出真实长宽高。
- `TopCornerLocalOffset(front, left)`：按箱型真实尺寸算出箱子本地坐标系下四个顶角（Y=半高，即真实顶面角件位置）。
- `EnsureLockHoleColliders()`：在`Awake()`自动调用，幂等（已建过不会重建），在四个顶角各建一个`SphereCollider`（`isTrigger=true`，半径0.05m），放在新增的`ContainerLockHole`物理层（第7层，`ProjectSettings/TagManager.asset`已加）。**场景里任何一个挂了`YardCraneContainer`组件的GameObject，进Play后都会自动长出这四个锁孔**，不需要额外操作。

### 3.2 `YardCraneYardSlot.cs`（新建，`Experiments/ObiRope/`）

- 箱位数据组件：`slotId`、`acceptedContainerType`、`initialOccupant`（场景里可选拖一个静态箱子的`YardCraneContainer`引用）。`Awake()`时把运行时占用状态初始化为`initialOccupant`；`SetOccupant(container)`/`Clear()`供运行时更新。**当前完全没有被`YardCraneEightRopeMaxPrototypeRig`引用**——本任务要把它接起来（见第4.2节）。

### 3.3 `YardCraneEightRopeMaxPrototypeRig.cs` 射线锁定机制

- 新增字段：`twistlockFrontLeft/FrontRight/RearLeft/RearRight`（`ResolveHierarchy()`里通过`RequireTransform`解析`30_SpreaderPhysics/ContainerAttachment/Twistlock_FL|FR|RL|RR`——这四个节点场景里已经存在，是`YardCraneModelHierarchyBuilder.cs`建的占位骨架，本次改动前局部坐标一直是`(0,0,0)`，现在被代码接管、每帧按`CurrentSpreaderSize`重新定位）、`detectedPickupContainer`（当前四条射线共同认定的候选箱子，`null`表示没有一致候选）。
- `ComputeContainerCornerAlignment()`（整体重写，替换掉之前那版纯几何距离计算）：先调`ApplyTwistlockPositionsForSpreaderSize()`把四个射线起点摆到跟当前`CurrentSpreaderSize`（Ft20/Ft40）匹配的真实位置，再朝下（`-twistlockPoint.up`方向，探测距离`TwistlockSensorRangeMeters=0.35m`）各发一条射线，命中`ContainerLockHole`层的碰撞体才算命中；四条射线必须指向同一个`YardCraneContainer`实例（以FrontLeft的命中结果为基准，其余三条必须命中同一个实例）才算对齐。
- `IsContainerAlignedForLock()`：在原有"四角必须都对齐"基础上新增两条前置条件——`spreaderSize.State == Transitioning`时直接拒绝（吊具正在切换尺寸不能锁）；`IsDetectedContainerTypeCompatibleWithSpreaderSize()`检查`detectedPickupContainer.ContainerType`是否匹配`CurrentSpreaderSize`，不匹配会打印`[YC_CONTAINER_TYPE_SPREADER_SIZE_MISMATCH]`并拒绝（不是单纯指望射线因为间距不对而"自然够不着"，这个检查是明确兜底）。
- `TryLockContainer()`内部原本重复的对齐判断已经改成直接调用`IsContainerAlignedForLock()`，不会跟上面这条判断逻辑走岔。
- `ConfigureContainerLoad()`：`container != null`分支新增调用`container.EnsureLockHoleColliders()`；`container == null`分支（场景里`ActiveContainer`还没挂`YardCraneContainer`组件的情况）会打印`[YC_CONTAINER_LOCK_HOLES_MISSING]`警告——**这是一个已知的、故意引入的行为改变**：`YardCraneContainer`组件从"可选、不装不影响现状"变成了"锁箱这条路能不能走通的硬性前提"，没有锁孔碰撞体射线永远打不到东西。

## 4. 本次目标（Codex要做的部分）

### 4.1 搬运途中的真实碰撞（补齐"飞太低会穿过箱子"这个缺口）

**现状事实**（已读代码确认，不是猜测）：

- `TryLockContainer()`（约2440行起）：`containerCollider.enabled = false`——锁箱那一刻箱子自己的碰撞体被关掉，直到解锁前都不会再打开，搬运全程箱子在物理世界里不存在。
- `TryUnlockContainer()`（约2507行起）：解锁那一刻`containerCollider.enabled = true`重新打开，同时`SetContainerHeadblockCollisionIgnored(true)`（用`Physics.IgnoreCollision(containerCollider, headblockCollider, true)`让箱子跟吊具自己的碰撞体互相不撞——这一步说明`containerCollider`跟`headblockCollider`默认状态下**是会互相检测碰撞的**，不是天然被排除的复合刚体子物体，这是判断"简单打开开关够不够"时的一个关键既有事实）。
- **`activeContainer`被锁定期间挂在`containerAttachmentRoot`底下，而`containerAttachmentRoot`是`30_SpreaderPhysics`（也就是拥有`headblockBody`这个Rigidbody的那个GameObject）的子级**——按Unity的常规规则，一个Transform层级下没有被其他Rigidbody打断的子物体碰撞体，会被PhysX当成父级Rigidbody复合形状的一部分。这意味着"只要打开`containerCollider.enabled`，箱子就能正确参与吊具整体的运动碰撞"这个假设**理论上有依据**，但因为上面那条`SetContainerHeadblockCollisionIgnored`的存在，说明真实情况不是自动排除，需要显式调用才能避免箱子撞到吊具自己身上——**这一点必须在Unity里实测验证，不能凭这段推理直接下结论**。

**要做的事**：

1. 在`TryLockContainer()`里，把`containerCollider.enabled = false`这一行改成`containerCollider.enabled = true`（跟`SetContainerHeadblockCollisionIgnored(true)`配一起用，镜像`TryUnlockContainer()`里已经在用的同一套调用——即锁箱时也要显式忽略箱子和吊具自己碰撞体之间的碰撞，只允许箱子跟*外部*的东西（堆场箱子、地面）真实碰撞）。
2. **进Play验证**：吊着一个箱子正常搬运，确认：①不会跟吊具自己的部件产生诡异碰撞/卡顿/弹飞；②飞得足够低时，真的会撞到堆场里摆着的静态箱子（需要先按第4.3节把至少一个静态箱子摆进场景才能测）；③正常高度搬运、正常锁箱/解锁流程完全不受影响（回归）。
3. **如果第2步发现简单打开碰撞体不够**（比如出现"穿过太快漏检"或"跟随不跟手/物理表现诡异"这类问题）——大概率原因是这个collider虽然结构上是复合刚体的一部分，但Unity/PhysX在**运行时才通过`SetParent`把它挂上去**这种动态重新父级化的场景下，是否严格等价于"从一开始就是子物体"存在实现细节上的差异，需要进一步实测判断是否需要给箱子单独挂一个`Rigidbody`（`isKinematic = true`，靠`Rigidbody.MovePosition/MoveRotation`而不是单纯`Transform.SetParent`+`localPosition`赋值去驱动）。**这条路径不是本任务默认要做的方案，只有在第2步验证方案一失败时才启用，请如实记录采用了哪条路径、为什么。**

### 4.2 落箱静止确认 + 堆场占用状态更新（含叠箱）

**要做的事**：

1. 在`YardCraneEightRopeMaxPrototypeRig`里新增"静止确认"逻辑：解锁后（`TryUnlockContainer()`已经给了箱子真实动态Rigidbody，这部分是现状，不用改），持续监测`containerBody.linearVelocity.magnitude`和`containerBody.angularVelocity.magnitude`，两者都低于某个阈值（可以直接复用现有`ResetSafeSpeedThresholdMetersPerSecond`，或者新开一个专门给"落箱静止确认"用的阈值——如果新开，加进`YardCraneEightRopeMaxPrototypeProfile`的`geometryAndSafety`段，别写死在代码里）并持续一段时间（同样建议做成可配置项，不要写死数字），才认定"已经稳定"。
2. **稳定后的结果分两支**：
   - 箱子最终停靠的位置落在某个`YardCraneYardSlot`的对位容差内（可以复用`containerHorizontalLockToleranceMeters`/`containerVerticalLockToleranceMeters`这套现成容差，不用新造）→ 调用该`YardCraneYardSlot.SetOccupant(...)`把这个箱子的`YardCraneContainer`身份记进去，同时把它之前抓取时来源的那个箱位调`Clear()`标记为空（"源箱位一锁上就立刻标记空"——这一步应该在锁箱成功的那一刻做，不用等到放置完成，见下面第3点）。
   - 没有落在任何箱位容差内→不更新任何`YardCraneYardSlot`占用记录，箱子物理上就留在它实际停下来的地方（自由模式允许放在任何地方，这是预期行为，不是bug）。
3. **抓取时机**：射线命中`detectedPickupContainer`且学员触发锁箱成功的那一刻，遍历场景里所有`YardCraneYardSlot`，找到`CurrentOccupant == detectedPickupContainer`的那个箱位，调用它的`Clear()`。
4. **叠箱场景**：不需要额外代码判断"上面是不是空的"——按第2节背景里的设计决定，这条限制应该是堆场箱子有真实碰撞体之后物理自然产生的。**但本任务必须用真实Play去验证这个假设是否成立**（见4.3节第3点），如果验证下来发现物理并没有天然挡住（比如吊具能够绕过/穿过最上面那个箱子够到下面那个），必须如实报告，不能因为"设计上应该这样"就假装它已经生效。

### 4.3 场景搭建与真实性验证

1. 把两个真实集装箱模型接入`YardCraneContainer`：`Assets/Prefabs/40尺.prefab`（或`Assets/Models/集装箱/40寸集装箱.fbx`，两者选其一，如果`40尺.prefab`内部结构不干净建议直接从fbx重新摆一个）、以及20尺箱（目前只有`Assets/Models/集装箱/20寸集装箱.fbx`，没有对应prefab，需要新建一个）。每个都要：挂`YardCraneContainer`组件（填对应箱号/箱型/毛重）、有真实碰撞体（`BoxCollider`，按模型实际外形，不能用默认Unity内建Cube代替）、不要手动加`Rigidbody`（`YardCraneContainer.EnsureLockHoleColliders()`会在`Awake()`自动生成锁孔，不需要额外操作；Rigidbody的添加/移除完全由`YardCraneEightRopeMaxPrototypeRig`在运行时管理，堆场里的静态箱子本身不应该带Rigidbody）。
2. 在`YardCrane.unity`场景里摆至少3个`YardCraneYardSlot`：2个源箱位（各自`initialOccupant`指向一个静态箱子实例，一个40尺一个20尺）+ 1个空的目标箱位（`initialOccupant`留空）。位置摆在吊具正常能开到、且离现有单项/联动练习目标点不冲突的地方。
3. **验证第2节第3条的物理假设**：进Play，尝试用吊具去够被压在另一个箱子下面的箱子（可以临时手动摆一个"压着"的测试布局，测完可以再挪回正常铺装），确认射线/吊具本身是否真的够不着下层箱子。如实记录验证结果，不管成立还是不成立。
4. **验证吊具尺寸切换**：`CurrentSpreaderSize`在Ft20/Ft40之间切换后，用第1步摆的两种箱型分别测试锁箱——确认20尺箱型只能在吊具切到Ft20时锁上，40尺同理；切换中途（`Transitioning`状态）尝试锁箱应该被拒绝。

## 5. 明确不在本任务范围内

- 完整Bay/Row/Tier贝位编址、堆场网格铺满、超过2层的堆叠限制。
- 接入学生端"作业操作"训练菜单、任何HUD新增显示——`YardCraneLocalTrainingTarget`是连续数值框架，装不进"抓对/放对"这种离散判断，这层设计本任务不做，只让底层机制能跑起来。
- 静态箱子之间互相碰撞检测（今晚只需要唯一那个动态箱子能撞到静态箱子，静态箱子彼此不需要互相检测）。
- 真实ISO角件的精确内缩尺寸（`TopCornerLocalOffset`用的是箱子真实几何角点，不是内缩后的角件位置，这是本次的既有简化，不在本任务范围内精确化）。
- 除本文件列出的文件外，不顺手改动S7、教师端、学生端UI等其他今天/近期正在并行推进的工作。

## 6. 必读资料

- `CLAUDE.md`、`AGENTS.md`
- `PortTraining/Docs/讨论记录/2026-09-17.md`（今晚完整设计讨论，若未回写需先补）
- `PortTraining/LM-Wiki/02-知识层-编译层/场桥真实模型接入契约-v1.0.md`（碰撞体/Rigidbody归属的既有约定）
- `YardCraneSimulator/Assets/YardCraneSimulator/Experiments/ObiRope/YardCraneContainer.cs`
- `YardCraneSimulator/Assets/YardCraneSimulator/Experiments/ObiRope/YardCraneYardSlot.cs`
- `YardCraneSimulator/Assets/YardCraneSimulator/Experiments/ObiRope/YardCraneEightRopeMaxPrototypeRig.cs`（`ComputeContainerCornerAlignment`/`ApplyTwistlockPositionsForSpreaderSize`/`IsContainerAlignedForLock`/`TryLockContainer`/`TryUnlockContainer`/`ConfigureContainerLoad`）
- `YardCraneSimulator/Assets/YardCraneSimulator/Experiments/ObiRope/YardCraneContainerImpactSensor.cs`（`HardImpact`违规检测，落箱静止确认要跟这个协同，不要重复造检测碰撞的逻辑）
- `YardCraneSimulator/ProjectSettings/TagManager.asset`（`ContainerLockHole`层，第7号槽位）

## 7. 文件边界

- 允许修改文件：
  - `YardCraneSimulator/Assets/YardCraneSimulator/Experiments/ObiRope/YardCraneEightRopeMaxPrototypeRig.cs`
  - `YardCraneSimulator/Assets/YardCraneSimulator/Experiments/ObiRope/YardCraneContainer.cs`（如需微调，比如锁孔容差、碰撞体细节）
  - `YardCraneSimulator/Assets/YardCraneSimulator/Experiments/ObiRope/YardCraneYardSlot.cs`
  - `YardCraneSimulator/Assets/YardCraneSimulator/Runtime/Configuration/YardCraneEightRopeMaxProfile.cs`（如需新增静止确认阈值等配置项）
  - `YardCraneSimulator/Assets/YardCraneSimulator/Scenes/YardCrane.unity`
  - `YardCraneSimulator/Assets/Prefabs/40尺.prefab`（如需调整）、新建20尺箱prefab
  - `PortTraining/Docs/讨论记录/2026-09-17.md`（回写实际完成内容）
- 只读参考文件：本文件第6节列出的其余文件。
- 禁止修改：未列出的S7相关文件（`S7Integration/`、`SharedNet/`）、教师端/学生端Shell文件、除`YardCrane.unity`外的其他场景文件。

## 8. 约束与安全规则

- 这次改动直接影响"什么时候允许锁箱""落箱之后如何判定"这两条安全相关的联锁逻辑，按项目风险分级属于L3，必须执行真实Unity Play链路验证，不能只凭编译通过就报告完成。
- 不要把"编译通过"或"静态检查通过"描述为"功能验证通过"。
- 第4.1节第3点提到的Rigidbody方案是有条件启用的备选项，不是默认要做的事，如果第2步验证下来简单方案就够用，不要画蛇添足去加多余的Rigidbody。
- 涉及`YardCraneEightRopeMaxPrototypeRig.cs`改动前，先确认现有单项练习（大车/小车/吊具移动等，不涉及堆场）在改动后依然完全不受影响——这些练习走的是同一套锁箱入口。

## 9. 验收要求

- 编译通过（`YardCraneSimulator.ObiRopeTest`及其依赖的`YardCraneSimulator.Runtime`）。
- 真实Unity Play验证第4.1、4.2、4.3节列出的每一项，并如实记录哪些成立、哪些不成立、走了哪条备选方案。
- 回归确认：现有单项/联动练习、既有的30吨完整锁箱回归（如果近期有相关Harness/流程）未被这次改动破坏。

## 10. Claude/Codex交付格式

1. 结论。
2. 实际修改的文件与核心改动点。
3. 实际执行的验证（编译结果、真实Play过程与观察到的现象，尤其是第4.1节碰撞方案走了哪条路、第4.3节堆叠物理假设是否成立）。
4. 未验证范围和已知风险。
5. 建议下一步。
