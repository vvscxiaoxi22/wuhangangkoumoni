# 港口实训平台

本目录是港口实训平台统一源码仓库，后续需求、代码、验证和文档均以根仓库Git历史为准。

当前版本、活动任务和下一步统一从[`CURRENT_STATUS.md`](CURRENT_STATUS.md)进入；连续小任务不再重复遍历全部历史文档。

## 工程组成

- `PortTraining/`：学生端Unity工程、LM-Wiki、接口规范、讨论记录和协作工具。
- `YardCraneSimulator/`：场桥独立Unity仿真程序，固定使用Unity `6000.0.42f1`。
- `HikvisionFourDisplay/`：独立Unity `6000.0.42f1` 四屏监控显示工程；当前只完成四相机/四视频面场景骨架，RTSP拉流与实体四屏验收待后续开发。
- `teacher-prototype/`：教师端Web交互与视觉原型；场桥Unity内的本机教师入口已迁入`YardCraneSimulator/Assets/YardCraneSimulator/Scenes/TeacherHome.unity`，真实平台业务仍待后续接入。
- `PortGatewayService/`：历史协议与验证代码保留。自2026-09-15起它不再作为当前场桥S7交付路径；既有Gateway测试证据不能证明新的Unity直连链已经验收。
- `YardCraneSimulator/Assets/YardCraneSimulator/Scenes/YardCraneS7Integration.unity`：场桥唯一S7直联场景，使用game4automation组件；默认禁用且不在正式Build Settings中，等待真实点表与PLC联调。
- `AGENTS.md`、`CLAUDE.md`：根级协作和交付规则。

## Git维护约定

- 主分支：`main`。
- 首个统一基线标签：`baseline-2026-09-03-v1`。
- 教师端合并前的独立历史保存在`legacy/teacher-prototype-main`分支；工作区迁移备份位于受忽略的`Temp/GitMigration/`。
- Unity的`Library`、`Temp`、`Build/Builds`、`Logs`和`UserSettings`，Web依赖/构建目录、本地`.env`及根目录运行日志不入库。
- FBX、二进制插件、Office/PDF等不可有效文本合并的资产通过Git LFS维护；克隆后先确认已安装Git LFS。
- 工作按L1快速通道、L2聚焦验证、L3完整门禁分级执行；提交信息应说明软件范围和实际验证结果。

## 开始开发

1. 阅读`CURRENT_STATUS.md`；连续任务状态未变化时无需重复读取。
2. 连续任务首次进入时阅读根目录及当前工程的`AGENTS.md`。
3. 只读取目标文件、直接依赖和该风险等级要求的相关文档。
4. 运行`git status --short --branch`，确认并保护已有改动。
5. 完成风险匹配的验证；同批L1微调统一记录，L2/L3收口后再提交。
6. 场桥任务优先显式传入本次改动路径运行轻量验证器；它不会启动Unity：

```powershell
pwsh -NoProfile -File YardCraneSimulator/Tools/Validate-Change.ps1 -Paths Assets/YardCraneSimulator/Shell/Resources/YardCraneShell/StudentSide/Display1UI/StudentSideShellStyles.uss
```

不传`-Paths`时只审计Git已跟踪改动，新建文件必须显式列出。返回码：`0`为轻量门禁通过，`1`为失败，`2`为需要用户验收或更高等级验证。
