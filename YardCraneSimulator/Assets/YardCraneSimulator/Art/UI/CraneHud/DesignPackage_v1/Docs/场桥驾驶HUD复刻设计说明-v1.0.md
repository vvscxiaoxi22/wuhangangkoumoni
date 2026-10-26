# 场桥驾驶 HUD 复刻设计说明 v1.0

## 1. 目标与边界

本说明用于在 `Bootstrap`/驾驶显示中复刻已确认的场桥驾驶 HUD。它描述的是驾驶视图上的只读信息层，不是 StudentHome、训练中心、教师端，也不是 PLC 通讯层。

`Reference/crane-hud-ui-reference-v1.png` 是最终视觉对照图；`Reference/cockpit-background-reference.png` 是原始驾驶视角。两张图只用于校准布局和色彩，运行时背景应来自 Unity 相机/场景，不应把参考图当作整张 HUD 背景贴图。

实现必须满足：

1. 真实场景作为第一视觉层，所有 HUD 元素让出中间吊具、钢丝绳、集装箱和落箱目标的视野。
2. 运行时代码只读取统一快照，不在 UI 代码中直接访问 S7、Rigidbody、Obi 或场景对象。
3. 状态灯和锁销图标为只读状态，不允许通过点击修改设备状态。
4. 数据缺失、通讯断开或快照超时必须显示 `--`/灰色离线态，不能继续显示上一帧的“已锁/运行中”假状态。

## 2. 画布、缩放与安全区

- 设计分辨率：`1920 × 1080`，16:9。
- 外边距安全区：24 px；右上角按钮的圆心距离右边缘 52 px、上边缘 40 px。
- 根节点等比缩放：`scale = min(screenWidth / 1920, screenHeight / 1080)`。
- 不允许拉伸单个素材；面板使用 9-slice 或保持等比缩放。
- 实体设备的 1920 × 2160 双屏环境中，本 HUD 作为单个 1920 × 1080 驾驶画面使用，另一块屏幕由对应显示场景自行管理，不将两个画面拼成一个 UI。
- 文本建议使用支持中文的 FontAsset；标题 22–24 px，正文 17–19 px，数值 20–22 px，单位 14–16 px。

## 3. 画面区域与锚点

| 区域 | 锚点/尺寸 | 内容 |
|---|---:|---|
| 计时框 | 左上 `(24, 18)`，`280×64` | `用时` + `mm:ss` |
| 任务栏 | 顶部居中，`820×64`，中心 x=960 | `单项操作 · 大车移动` |
| 状态灯条 | 任务栏下方，建议 `(620, 78)`，`680×78` | 制动、锚定、开锁、单箱、双箱 |
| 左遥测 | `(24, 142)`，`312×360` | 大车/小车/吊具位置与速度 |
| 右任务 | 右侧 `(1596, 96)`，`300×160` | 当前任务、箱号、放箱地址 |
| 右姿态 | 右侧 `(1596, 272)`，`300×160` | 左右倾转、前后倾转、水平回转 |
| 右锁销 | 右侧 `(1596, 448)`，`300×56` | 锁销已锁/未锁 |
| 返回/退出 | 右上，`56×56` | `icon-back.svg`、`icon-close.svg` |

上述坐标以左上角为原点。右侧区域应始终贴近屏幕边缘，不能向中央扩展超过 x=1568；中央 x=336–1568 视为操作视野保护区。

## 4. 素材使用契约

### 4.1 玻璃面板

- `Layout/hud-timer-frame.svg`：计时框，运行时在内部放置“用时”和数字。
- `Layout/hud-task-capsule.svg`：顶部任务栏。
- `Layout/hud-panel-left.svg`：左遥测面板；内部按 6 行排列。
- `Layout/hud-panel-task.svg`：右任务面板；内部按 3 行排列。
- `Layout/hud-panel-pose.svg`：右姿态面板；内部按 3 行排列。
- `Layout/hud-panel-lock.svg`：右锁销面板；内部为标题 + 两个状态项。
- `Layout/hud-divider.svg`：必要时用于右侧分隔，但优先使用面板内置浅分隔线。

面板 SVG 只提供底色、圆角、边框和分隔线，禁止在其上再叠加不透明背景。推荐 USS：

```css
--hud-cyan: rgb(73, 238, 255);
--hud-text: rgb(232, 252, 255);
--hud-muted: rgba(190, 226, 238, .72);
--hud-glass: rgba(7, 35, 57, .76);
--hud-border: rgba(165, 245, 255, .78);
--hud-radius: 16px;
```

### 4.2 状态灯

每个状态有 `on`、`off` 两个 SVG，尺寸均为 `34×34`：

| 状态 | On 文件 | Off 文件 | 语义 |
|---|---|---|---|
| 制动 | `StatusLamps/brake-on.svg` | `brake-off.svg` | 制动器已施加/未施加 |
| 锚定 | `StatusLamps/anchor-on.svg` | `anchor-off.svg` | 大车锚定已锁/已释放 |
| 开锁 | `StatusLamps/unlock-on.svg` | `unlock-off.svg` | 吊具锁销处于开锁状态 |
| 单箱 | `StatusLamps/single-on.svg` | `single-off.svg` | 单箱模式 |
| 双箱 | `StatusLamps/double-on.svg` | `double-off.svg` | 双箱模式 |

颜色只表达状态，不表达“好/坏”：制动使用琥珀色、锚定红色、开锁绿色、单箱白色、双箱蓝色。互斥模式（单箱/双箱）只能有一个亮；未知时两者都使用 off。

### 4.3 图标

- `Icons/icon-back.svg`：返回上一级；只做导航，不结束运行。
- `Icons/icon-close.svg`：退出驾驶界面；点击前由业务层决定是否弹出确认。
- `Icons/icon-cyan-dot.svg`：遥测行前缀，表示字段在线；离线时改为低透明度圆点或直接隐藏。
- `Icons/icon-lock-pin-on.svg` / `icon-lock-pin-off.svg`：锁销状态，必须与 `spreader.lockPins` 一致。

## 5. 运行时文字与字段

建议所有文本由一个 `YardCraneHudView` 统一绑定，字段命名如下（字段值由现有 UI 快照适配器提供）：

```text
elapsedSeconds                 // 任务计时
task.title                     // 当前任务，如“装船作业”
task.operationLabel            // 顶部任务栏，如“单项操作 · 大车移动”
task.targetContainerId         // 目标箱号，如“XKSZ01”
task.targetSlot                // 放箱地址，如“0302090”
gantry.positionMeters          // 大车位置
gantry.speedMetersPerSecond    // 大车速度
trolley.positionMeters         // 小车位置
trolley.speedMetersPerSecond   // 小车速度
spreader.heightMeters          // 吊具高度（不要再显示“起升高度”重复字段）
spreader.speedMetersPerSecond  // 吊具速度
spreader.leftRightTrimDeg      // 吊具左右倾转
spreader.frontBackTrimDeg      // 吊具前后倾转
spreader.horizontalRotationDeg // 吊具水平回转
spreader.lockPinsLocked        // 锁销是否已锁
equipment.brakeApplied         // 制动状态
equipment.anchorApplied        // 锚定状态
equipment.unlockActive         // 开锁状态
equipment.loadMode              // Single / Twin / Unknown
snapshot.isOnline              // 快照是否在线
snapshot.ageSeconds             // 快照年龄
```

显示格式：

- 位置：`0.00 m`，速度：`0.00 m/s`，角度：`0.0°`。
- 计时：`mm:ss`；超过 59:59 时使用 `hh:mm:ss`。
- 缺失数值统一显示 `--`，不要显示 0。
- 数值右对齐，标签左对齐；单位使用较小字号和 `--hud-muted`。

## 6. 绑定与刷新策略

1. `YardCraneUiStateSnapshot` 由场景/网关适配层产生，UI 每帧最多刷新一次，建议使用 `LateUpdate` 或定时 10–20 Hz。
2. 只比较快照字段的变化；静态文本和 SVG 不重复创建。
3. 图标切换采用缓存的 `VisualElement`，只更换 `backgroundImage`/`VectorImage`，不销毁重建节点。
4. `snapshot.ageSeconds > 0.5` 或 `isOnline == false` 时：数值变为 `--`，状态灯统一 off 并在任务栏下方显示小型“数据离线”提示；恢复在线后自动清除提示。
5. 该 UI 不直接调用 `PortGatewayService`、PLC、Obi、Rigidbody 或 `InputSystem`。

## 7. 交互契约

- 返回按钮：触发 `HudNavigation.RequestBack()`。
- 退出按钮：触发 `HudNavigation.RequestExit()`；退出确认由上层处理。
- 面板、状态灯、锁销图标均不可点击，不改变设备命令。
- 驾驶控制仍由实体控制台、键盘或手柄输入层处理，HUD 仅显示结果。

## 8. 验收清单

- [ ] 1920×1080 下与设计稿的锚点误差不超过 4 px。
- [ ] 中央吊具、钢丝绳、目标箱区域没有 HUD 覆盖。
- [ ] 制动黄灯能在 on/off 两个 SVG 间切换，默认状态由快照决定。
- [ ] 单箱/双箱互斥，未知状态全部为 off。
- [ ] 锁销已锁/未锁不会与单箱/双箱混淆，且单独位于右下状态条。
- [ ] 快照断开后不残留上一帧“已锁”“制动”等状态。
- [ ] 返回/退出按钮可触发上层事件，但 HUD 不直接关闭 Unity 场景。
- [ ] 1920×2160 双屏环境下不会把 HUD 拉伸到另一块显示屏。
- [ ] 在 Unity Editor 与打包运行环境中，中文 FontAsset 均能正确显示。
