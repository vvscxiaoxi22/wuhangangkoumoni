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

## 当前禁止事项

- 未确认现场点表前不得猜测并提交真实地址。
- 不得在其他场景再添加`S7Interface`。
- PLC点位不得直接改Transform、Rigidbody或绳索；必须先转换为统一操作语义并经过现有联锁。
- 未完成真实PLC的连接、断连、重连、急停、看门狗与安全清零验证前，不得加入正式考核或发布。
