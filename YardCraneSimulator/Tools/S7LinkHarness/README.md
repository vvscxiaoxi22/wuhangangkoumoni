# S7链路WebSocket回归

执行：`dotnet run --project Tools/S7LinkHarness/S7LinkHarness.csproj -c Release`（场桥工程目录）。

本工具编译`YardCraneMiniWebSocket.cs`（手写最小WebSocket服务端/客户端）和`YardCraneS7LinkProtocol.cs`（帧格式，含2026-09-16新增的12个瞬动按钮位掩码+维持型急停电平），在真实本机回环TCP端口上跑握手、编码/解码、断线检测——不是打桩，是真Socket、真后台线程，两个都是生产源码。

**2026-09-16范围变更（重要）**：本工具曾经短暂地也直接实例化`YardCraneS7InputSource`来测它自己的消费一次/失效安全逻辑，但当天晚些时候给这个类加上`YardCraneDevice`字段（虚拟Input System设备，用于把S7数据镜像进Unity的Input Debugger，见该类自己的类注释）之后，**只要构造`YardCraneS7InputSource`对象就会尝试加载`Unity.InputSystem`程序集**——脱离真实Unity进程时，这个加载在CLR类型加载阶段就失败（`FileNotFoundException`），而且这个失败发生在方法JIT编译期，包在`try/catch`里也捕获不住（已实测确认，不是猜测）。因此本工具**不再编译`YardCraneS7InputSource.cs`/`YardCraneDevice.cs`**，只测传输层+协议层——`YardCraneS7InputSource`自己的行为（12个瞬动按钮的消费一次语义、急停失效安全、链路陈旧判定）**从这天起只能在真实Unity Play里验证，本工具测不到了**。这是本轮改动的一个真实代价，不是遗漏。

五项检查：握手+连接成功、真实帧原样送达客户端、帧全字段（序号/大车/小车/有效位/瞬动位掩码/急停电平）编解码往返正确、`valid=false`+`急停电平=true`这个组合正确往返（失效安全设计最依赖的两个字段）、`YardCraneS7LinkFrame.SafeZero()`本身失效安全的形状正确（急停默认engaged）、服务端消失时客户端能检测到（含一次真实竞态修复：第一版测试等的是`!client.IsConnected`这个代理条件，实际该判断和`Disconnected`事件触发不是原子的，偶发直接检查事件标志位时还没被设置——改成直接等事件标志位本身后连续多次运行稳定）、端口无人监听时`TryConnect`在超时内干净失败。

所有断言通过才返回0。连续多次运行九项全部PASS。

`YardCraneStandaloneOperationHost`原有键盘/Xbox路径（`requireS7Link`默认`false`时的行为）由`Tools/StandaloneHostHarness`覆盖，未受本轮改动影响——该工具十四项既有断言全部保持PASS（该工具虽然编译了`YardCraneS7InputSource.cs`/`YardCraneDevice.cs`并引用了真实Unity DLL，但十四项测试全部走`requireS7Link=false`默认路径，从未真正构造`YardCraneS7InputSource`对象，所以不会触发上述加载失败——但这也意味着那边同样测不到`requireS7Link: true`这条路径，见该工具自己的README）。

本工具不覆盖、仍需Unity真实Play/现场验证的部分：`YardCraneS7InputSource`自身的全部行为（如上所述，2026-09-16起离线不可测）；`YardCraneS7CommandWebSocketServer`这个MonoBehaviour本身（Awake/Update/OnDestroy生命周期、与`YardCraneS7IntegrationBridge`的Inspector引用、独立Player构建）；`YardCraneDevice`虚拟设备本身在真实Unity Input System运行时下是否正确注册、上报、被Input Debugger看到；`YardCraneStandaloneOperationHost.Configure(requireS7Link: true, ...)`这条新分支在真实Unity场景启动顺序下的行为；真实PLC数据流经完整链路后的实际操作手感与延迟；`YardCraneS7IntegrationBridge.cs`里12个`PLCOutputBool`边沿检测+急停电平读取对着真实`game4automation.PLCOutputBool`组件跑起来是否正确。
