# YC-P0-02-HUD-01关键操作状态显示

## 1. 任务身份

- 任务编号：`YC-P0-02-HUD-01`
- 任务名称：场桥灰盒HUD增加制动器、夹轮器和抓箱关键状态
- 执行模式：限定开发
- 当前版本：`2026-09-01-eight-rope-max-v18-gantry-drive`
- 主控：Codex
- 执行代理：Claude Code

## 2. 背景与产品定义

- 用户要求UI明显显示是否松开刹车、是否抓箱。
- 当前HUD只在普通数据行里显示英文`WheelClampState`和`twistlockStatus`，不够醒目，也把“夹轮器”和“大车制动器”混在同一个理解层次。
- 产品定义必须区分：
  1. 大车行车制动器：决定当前大车驱动是在释放驱动还是处于制动/保持状态。
  2. 夹轮器：防风/停车夹轨状态，`Released/Transitioning/Clamped/Fault/Unknown`。
  3. 抓箱：只有`containerLocked == true`才能显示“已抓箱”；请求中、解锁中和未抓箱必须区分，不能根据靠近或接触推断已抓箱。

## 3. 本次目标

1. 在`YardCraneGantryDriveModel`中增加确定性、可测试的行车制动状态，不能由HUD临时猜测。
2. `Reset`后静止保持应为“已制动”。
3. 夹轮器非`Released`时必须为“已制动”。
4. 无运动命令、减速、反向或限位减速阶段应为“已制动”；存在有效同向运行命令且处于加速/匀速驱动时应为“已松闸”。
5. 将制动状态放入`YardCraneGantryDriveStep`并由模型公开只读属性。
6. 在Rig的`OnGUI`上增加紧凑、醒目的状态区，放在屏幕上半部靠右，不占用下半部视野；至少显示：
   - `大车制动：已松闸 / 已制动`
   - `夹轮器：已松开 / 动作中 / 已夹紧 / 故障 / 状态未知`
   - `抓箱：已抓箱 / 锁箱中 / 解锁中 / 未抓箱`
7. 状态区使用有限的绿色、琥珀色、红色和中性色；不修改现有运动参数或物理算法。
8. 在纯C# Harness中覆盖制动器状态的关键转换，保持既有全部测试通过。

## 4. 明确不做

- 不实现制动音效或实体制动动画。
- 不修改夹轮器状态机业务规则。
- 不修改锁箱/解锁物理流程。
- 不处理低空防碰Bug。
- 不调整八绳、质量、阻尼、速度、加速度或限位参数。
- 不启动Unity、不操作场景、不安装依赖、不提交Git。

## 5. 必读资料

- `CLAUDE.md`
- `YardCraneSimulator/AGENTS.md`
- `PortTraining/LM-Wiki/03-规则层/任务验证与文档交付规范.md`
- `PortTraining/Docs/讨论记录/2026-09-01.md`第7、9、11节
- `PortTraining/LM-Wiki/02-知识层-编译层/场桥YC-P0-02开发计划-大车鸣笛与夹轮器.md`

## 6. 文件边界

- 允许修改文件：
  - `YardCraneSimulator/Assets/YardCraneSimulator/Runtime/Crane/YardCraneGantryDriveModel.cs`
  - `YardCraneSimulator/Assets/YardCraneSimulator/Experiments/ObiRope/YardCraneEightRopeMaxPrototypeRig.cs`
  - `YardCraneSimulator/Tools/OperationStateMachineHarness/Program.cs`
- 只读参考文件：
  - `YardCraneSimulator/Assets/YardCraneSimulator/Runtime/Operations/YardCraneOperationController.cs`
  - `YardCraneSimulator/Assets/YardCraneSimulator/Runtime/Crane/YardCraneMechanismState.cs`
- 禁止修改：未列入允许修改文件的全部文件。

## 7. 验收要求

- `YardCraneGantryDriveModel`保持Unity无关。
- Harness必须验证：Reset静止制动、有效起步松闸、松开输入进入制动、反向进入制动、夹轮器抑制时制动、重新有效驱动后松闸。
- 新状态不得改变既有位置、速度、限位或夹轮器结果。
- HUD状态必须使用中文，不直接向用户显示枚举英文。
- 状态区不得遮挡屏幕下半部，不得新增可点击操作按钮。
- Claude只能做静态检查；编译、Harness和Unity Play由Codex执行并记录。

## 8. Claude交付格式

1. 实现结论。
2. 修改文件和精确行号。
3. 制动状态语义与转换说明。
4. HUD布局和颜色说明。
5. 实际执行的静态检查。
6. 未验证范围和建议Codex执行的验证。
