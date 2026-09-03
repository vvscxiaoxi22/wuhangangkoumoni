# 港口实训平台

本目录是港口实训平台统一源码仓库，后续需求、代码、验证和文档均以根仓库Git历史为准。

## 工程组成

- `PortTraining/`：学生端Unity工程、LM-Wiki、接口规范、讨论记录和协作工具。
- `YardCraneSimulator/`：场桥独立Unity仿真程序，固定使用Unity `6000.0.42f1`。
- `teacher-prototype/`：教师端交互原型，后续正式教师端开发仍在本目录演进。
- `AGENTS.md`、`CLAUDE.md`：根级协作和交付规则。

## Git维护约定

- 主分支：`main`。
- 首个统一基线标签：`baseline-2026-09-03-v1`。
- 教师端合并前的独立历史保存在`legacy/teacher-prototype-main`分支；工作区迁移备份位于受忽略的`Temp/GitMigration/`。
- Unity的`Library`、`Temp`、`Build/Builds`、`Logs`和`UserSettings`，Web依赖/构建目录、本地`.env`及根目录运行日志不入库。
- FBX、二进制插件、Office/PDF等不可有效文本合并的资产通过Git LFS维护；克隆后先确认已安装Git LFS。
- 每项工作仍执行“实现 → 验证 → 文档更新 → 提交”，提交信息应说明软件范围和可验证结果。

## 开始开发

1. 阅读根目录及当前工程的`AGENTS.md`。
2. 阅读`PortTraining/LM-Wiki/README.md`、相关知识层文档和当天讨论记录。
3. 运行`git status --short --branch`，确认并保护已有改动。
4. 完成相称验证并更新当天讨论记录后再提交。

