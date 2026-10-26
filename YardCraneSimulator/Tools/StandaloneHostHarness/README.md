# Standalone Host边界回归

执行：`dotnet run --project Tools/StandaloneHostHarness/StandaloneHostHarness.csproj -c Release`（场桥工程目录）。

本工具直接编译当前`YardCraneStandaloneOperationHost.cs`，复用`OperationStateMachineHarness`的真实Controller、Router、Command、Mechanism及其FakeActuator。仅Unity生命周期、时间和键盘/Xbox采样使用测试替身；不启动Unity、不连接真实设备、不修改生产源码。

固定次序模拟`Update → OnApplicationFocus(false) → FixedUpdate`，检验400ms失焦窗口不执行失焦前排队动作，也不使用合成零轴绕过显式安全复位的真实回中条件；另有正常锁箱、长失焦SafeHold和运动清零对照。

所有断言通过才返回0。2026-09-04初次针对Host SHA-256 `9800DDBD93505158C10D8C911B14652738D1FD8BDE0146D453830C974534406A`运行，六项中两项失败，返回1；这是真实源码在隔离环境中的反例，不是Unity Play时序或物理验证。修复后仍需Unity 6000.0.42f1真实Play、暂停/设备断连和完整作业链回归。

2026-09-04 16:39后Codex独立复核：交互式Claude会话已在失焦回调中清除离散队列并作废采样新鲜度；Host SHA-256为`353CB9FE9F07247BB8F72752E737A5302C6A8F2948D6E30211EFFDE6DF044990`。重新运行本工具六项全部PASS、退出码0，原OperationStateMachineHarness也退出0。此前两个源码反例已消除；Unity真实焦点回调、设备与物理专项仍待验证，不视为完整放行。

2026-09-16：`YardCraneStandaloneOperationHost.Configure(...)`新增`requireS7Link`参数后，本工具补充编译`YardCraneS7InputSource.cs`/`YardCraneDevice.cs`并引用真实`Unity.InputSystem.dll`/`UnityEngine.CoreModule.dll`（从Unity自己的Bee构建缓存里取，非打桩——`.dag`那段路径是构建缓存哈希，Unity重新构建后可能失效，届时用`find Library/Bee/artifacts -iname "Unity.InputSystem.dll"`重新定位），十四项既有断言全部保持PASS。**但这十四项全部走`Configure(...)`的默认签名（`requireS7Link=false`），从未真正构造过`YardCraneS7InputSource`对象**——该类一旦被构造就会尝试加载`Unity.InputSystem`（用于内部的`YardCraneDevice`镜像），脱离真实Unity进程时这个加载在CLR类型加载阶段失败，包`try/catch`也捕获不住（`Tools/S7LinkHarness`已实测确认并因此收窄了自己的测试范围，见该工具README）。也就是说：`requireS7Link: true`这条路径——包括S7链接失败时`Configure()`是否正确抛异常并释放已激活的运行——本工具同样测不到，仍需真实Unity Play验证。
