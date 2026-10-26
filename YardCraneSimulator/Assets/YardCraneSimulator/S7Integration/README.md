# YardCrane S7 独立联调场景

场景入口：`Assets/YardCraneSimulator/Scenes/YardCraneS7Integration.unity`。

## 当前用途

- 只验证Unity与西门子S7的连接、绝对地址、数据类型、原始值和安全归零。
- 不承载场桥模型、学生/教师UI、训练状态或评分。
- 暂不加入正式五场景Build Settings，也不替代`YardCrane.unity`。
- 当前必须把本场景作为独立Active Scene运行；game4automation控制器在普通次级Additive场景中会自行停用。正式组合加载方式待PLC联调后另行确定。

## 场景层级

```text
YC_S7IntegrationRoot
├─ 00_Game4AutomationController
├─ 10_S7Connection_DISABLED_UNTIL_CONFIGURED
│  ├─ 20_PLC_To_Unity
│  │  ├─ GantryCommandRaw_SET_PLC_ADDRESS
│  │  └─ TrolleyCommandRaw_SET_PLC_ADDRESS
│  └─ 30_Unity_To_PLC_ADD_FEEDBACK_SIGNALS_HERE
├─ 40_CommandSafetyBridge
└─ 90_Diagnostics_NO_CAMERA_NO_UI
```

所有PLC点位必须放在`S7Interface`对象的激活子层级内。game4automation按PLC视角命名：

- `PLCOutput*`：PLC输出，Unity读取；大小车指令使用这一类。
- `PLCInput*`：Unity写回PLC；位置、状态等反馈使用这一类。

## 首次配置

1. 在`10_S7Connection_DISABLED_UNTIL_CONFIGURED`的`S7Interface`中填写真实`Adress`、`Rack`、`Slot`，先保持组件禁用。
2. 为大小车两个`PLCOutputInt`填写真实绝对地址，不能带`%`。例如`QW100`或`DB10.DBW0`；示例不是项目点表。
3. 两个信号的`OriginDataType`保持`INT`，否则负数可能被按`WORD`解释。
4. 在`40_CommandSafetyBridge`上执行配置校验；只有校验通过后才能启用`Connect On Play`或在Play中调用连接方法。
5. 大小车PLC指令范围为`-3000..3000`，桥接器输出为`-1..1`；断线、信号无效或超过0.5秒没有通信周期时输出归零。初次连接或自动重连后，必须完成一整个新读写周期并把新值发布到主线程，命令才重新有效。

## 2026-09-21收到的PLC-To-Twin DB1点表

用户提供的TIA Portal截图确认了以下PLC到数字孪生方向的数据。这里记录的是组件`Name`字段使用的绝对地址；地址不带`%`，`OriginDataType`必须显式填写。

| 名称 | TIA类型/偏移 | game4automation组件 | `OriginDataType` | 组件`Name` |
|---|---|---|---|---|
| 起升电机速度 | `Real / 0.0` | `PLCOutputFloat` | `REAL` | `DB1.DBD0` |
| 大车电机速度 | `Real / 4.0` | `PLCOutputFloat` | `REAL` | `DB1.DBD4` |
| 小车电机速度 | `Real / 8.0` | `PLCOutputFloat` | `REAL` | `DB1.DBD8` |
| 导板电机速度 | `Real / 12.0` | `PLCOutputFloat` | `REAL` | `DB1.DBD12` |
| 前后倾电机速度 | `Real / 16.0` | `PLCOutputFloat` | `REAL` | `DB1.DBD16` |
| 平面回转电机速度 | `Real / 20.0` | `PLCOutputFloat` | `REAL` | `DB1.DBD20` |
| 左右倾电机速度 | `Real / 24.0` | `PLCOutputFloat` | `REAL` | `DB1.DBD24` |
| 吊具开锁 | `Int / 28.0` | `PLCOutputInt` | `INT` | `DB1.DBW28` |
| 吊具闭锁 | `Int / 30.0` | `PLCOutputInt` | `INT` | `DB1.DBW30` |
| 鸣笛 | `Bool / 32.0` | `PLCOutputBool` | `BOOL` | `DB1.DBX32.0` |

这张表与当前场景里的旧占位契约不同：旧桥接器仍按两路`INT`速度、12路瞬动`BOOL`和一路维持型急停读取，不能只替换地址。下一步先把独立联调场景改成上述7个`PLCOutputFloat`、2个`PLCOutputInt`和1个`PLCOutputBool`的只读诊断模式；在以下现场契约补齐前，`CommandFrameValid`、`connectOnPlay`和正式场景`requireS7Link`必须继续保持关闭：

- 7路速度的单位、正负方向和满量程；
- 开锁/闭锁`INT`的有效值、保持或脉冲语义及复位方式；
- 鸣笛是按住电平还是脉冲；
- 急停、控制权、PLC看门狗/心跳及必要反馈点位；
- PLC IP、Rack和Slot。

## 当前禁止事项

- 未确认现场点表前不得猜测并提交真实地址。
- 不得在其他场景再添加`S7Interface`。
- PLC点位不得直接改Transform、Rigidbody或绳索；必须先转换为统一操作语义并经过现有联锁。
- 未完成真实PLC的连接、断连、重连、急停、看门狗与安全清零验证前，不得加入正式考核或发布。
