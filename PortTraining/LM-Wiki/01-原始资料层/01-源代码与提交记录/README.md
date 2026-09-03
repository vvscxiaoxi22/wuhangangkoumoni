# 源代码与提交记录

## 当前状态

`F:/wuhanxiangmu`已于2026-09-03建立统一根Git仓库，主分支为`main`。学生端、场桥仿真工程、教师端原型、LM-Wiki和协作规则均纳入同一工作树；Unity/Web生成目录、本地配置、运行日志和构建产物不入库。

## 首个统一基线

| 字段 | 内容 |
| --- | --- |
| 基线标签 | `baseline-2026-09-03-v1` |
| 初始内容提交 | `b0e27f5952e3b46a480aacd105275836190d6d36`（`chore(repo): establish unified project baseline`） |
| 主分支 | `main` |
| 纳入范围 | `PortTraining/`、`YardCraneSimulator/`、`teacher-prototype/`及根级规范/设计文档 |
| 大文件策略 | Git LFS；首批69个FBX/PSD/PDF/DOCX等二进制资产 |
| 教师端旧历史 | `legacy/teacher-prototype-main`分支，合并前提交`0d249b00a8dc675c5aa5082d2fb15c6fcf328a58` |
| 本地恢复备份 | 受忽略目录`Temp/GitMigration/teacher-prototype-before-monorepo.bundle`；SHA-256 `54F13F6A9728AC50074319ADDB60282C3FFD4133A3896823B3045F8260ADB93A` |
| 远程仓库 | 尚未配置；当前基线只在本机，配置正式远程并推送前不能视为异机备份 |

基线提交后的代码和文档变更必须先用`git status --short --branch`确认范围。发布级节点创建带说明标签，并在本目录记录标签、关键提交、验证证据和回滚边界。

## 建议记录字段

- 日期和提交ID
- 变更目标
- 影响模块
- 数据或协议兼容性
- 验证结果
- 回滚方式
- 关联需求、Bug和讨论记录

本目录只保存发布级变更摘要和重要提交索引，不复制完整源代码。
