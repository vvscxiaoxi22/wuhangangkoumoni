# Claude受控协作调用

本目录用于由主控Codex调用本机Claude Code。Claude不是任务放行方；Unity运行验证、代码审查和最终文档状态仍由Codex负责。

默认在每次任务前调用`Get-ClaudeUsageStatus.ps1`读取Claude计划用量。当前会话用量达到90%或状态不可读取时，拒绝启动新Claude任务并转由Codex继续。

## 前置条件

1. 本机已安装Claude Code并完成登录。
2. 从工作区`D:\wuhanxiangmu`范围内使用任务契约。
3. 同一时间只运行一个受控Claude任务。

## 只读审查

```powershell
& "D:\wuhanxiangmu\PortTraining\Tools\ClaudeCollaboration\Invoke-ClaudeTask.ps1" `
  -TaskFile "D:\wuhanxiangmu\PortTraining\Docs\Claude任务\2026-09-01\YC-P0-02-低空防碰逻辑只读审查.md" `
  -Mode Review
```

只读模式只批准`Read/Glob/Grep`，并显式禁用编辑、Shell和网络工具。

## 限定开发

```powershell
& "D:\wuhanxiangmu\PortTraining\Tools\ClaudeCollaboration\Invoke-ClaudeTask.ps1" `
  -TaskFile "<任务契约绝对路径>" `
  -Mode Implement
```

开发模式允许文件读取和编辑，但禁用Shell、网络、Notebook及Unity操作。任务契约必须列出允许修改的确切文件。

## 继续同一会话

```powershell
& "D:\wuhanxiangmu\PortTraining\Tools\ClaudeCollaboration\Invoke-ClaudeTask.ps1" `
  -TaskFile "<修正任务契约绝对路径>" `
  -Mode Implement `
  -ResumeSessionId "<前一次结果中的session_id>"
```

## 完成后唤醒Codex

由定时或后台任务调用时，可以在Claude完成后向指定Codex任务排队回调：

```powershell
& "D:\wuhanxiangmu\PortTraining\Tools\ClaudeCollaboration\Invoke-ClaudeTask.ps1" `
  -TaskFile "<任务契约绝对路径>" `
  -Mode Implement `
  -WakeCodexThreadId "<Codex任务UUID>"
```

回调只通知Codex读取交接并验证，不会把Claude的自述直接标记为完成。

## 用量检查

```powershell
& "D:\wuhanxiangmu\PortTraining\Tools\ClaudeCollaboration\Get-ClaudeUsageStatus.ps1"
```

`/usage`结果是Claude官方提供的计划用量估算。解析失败、认证失败或达到阈值时采用保守策略：不启动Claude，交回Codex。

结构化结果保存在`PortTraining/Docs/Claude交接/YYYY-MM-DD/`。失败结果也会保留，禁止因为调用失败而丢失交接证据。

## 放行规则

- Claude报告“完成”不等于任务完成。
- 主控必须检查实际文件差异，并运行与风险匹配的测试。
- Unity物理和交互任务必须由指定Unity版本实际Play验证。
- 验证完成后更新当天讨论记录，才可交付。
