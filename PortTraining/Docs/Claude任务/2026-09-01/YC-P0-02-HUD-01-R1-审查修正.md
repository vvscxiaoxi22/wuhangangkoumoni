# YC-P0-02-HUD-01-R1审查修正

## 1. 任务身份

- 任务编号：`YC-P0-02-HUD-01-R1`
- 执行模式：限定开发
- 继续会话：`35f37c44-cd74-4229-8bf8-90e2bbbb3a22`
- 主控审查结论：初版不能放行，必须修正以下真实问题。

## 2. 必须修正

### P0：限位停止时错误显示松闸

- 当前`YardCraneGantryDriveModel.cs:239-248`只判断命令有效、非反向、速度未下降。
- 当大车已经静止在正向限位，继续保持正向命令时：`velocityBeforeStep=0`、最终速度仍为0、`isSpeedNonDecreasing=true`，会错误得到`IsBraked=false`。
- 修正要求：制动释放必须建立在“最终存在非零、方向正确的实际驱动速度/有效目标速度”之上；限位保持零速必须显示已制动。不得改变现有位置、速度或限位算法。
- Harness新增“到达限位后持续向限位方向输入，最终仍为制动”的断言。

### P1：解锁中状态被已抓箱遮蔽

- 当前`BuildContainerGrabStatusLine()`先判断`containerLocked`，再判断`containerUnlockRequested`。
- 解锁请求执行期间箱体仍然处于locked，导致UI继续显示“已抓箱”，“解锁中”分支实际上不可见。
- 修正要求：动作中状态优先于稳定状态；至少`containerUnlockRequested`必须优先于`containerLocked`。同时保证锁箱中仍能显示。

### P1：旧HUD仍显示英文枚举

- 原数据行仍使用`$"夹轮器：{WheelClampState}"`，与任务要求“用户不直接看到英文枚举”冲突。
- 修正要求：旧数据行改用同一中文映射或移除重复夹轮器字段；不得出现`Released/Transitioning/Clamped/Fault/Unknown`面向用户显示。

### P1：窄竖屏面板可能重叠

- 左侧面板范围为x=24..634；新增右侧面板在720像素宽时x=446..696，会重叠188像素。
- 修正要求：当屏幕宽度不足以左右并排时，将关键状态区放到左侧主面板下方、仍位于屏幕上半部；宽屏时保持右上角。不得遮挡下半屏。

### P2：命名清晰度

- 将`IsBraked`改为`IsBrakeApplied`，使含义与“制动器已施加”一致，避免与“正在减速”混淆。同步Step、模型、Rig与Harness引用。

## 3. 文件边界

- 允许修改文件仍只有：
  - `YardCraneSimulator/Assets/YardCraneSimulator/Runtime/Crane/YardCraneGantryDriveModel.cs`
  - `YardCraneSimulator/Assets/YardCraneSimulator/Experiments/ObiRope/YardCraneEightRopeMaxPrototypeRig.cs`
  - `YardCraneSimulator/Tools/OperationStateMachineHarness/Program.cs`
- 禁止修改其它文件；不启动Unity、不运行Shell、不访问网络。

## 4. 交付要求

- 精确说明新制动公式为什么覆盖限位零速保持。
- 列出新增Harness断言。
- 说明720×1280和1080×1920两类竖屏布局位置。
- 不声称编译或Unity验证通过，留给Codex执行。
