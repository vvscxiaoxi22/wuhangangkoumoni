# Codex与Claude协作邮箱

该目录保存双代理接力的公开状态和规则，不保存账号、令牌或密码。

## 接力规则

1. Codex是任务主控和最终放行方。
2. Claude当前会话用量低于90%且状态正常时，可以接收受控任务。
3. Claude达到阈值、`/usage`不可读取、认证失败或出现限额错误时，由Codex继续开发。
4. Codex只有在产品明确提示额度偏低、任务预算不足或调用出现明确用量限制时才向Claude交接；当前没有公开API可供脚本读取Codex账户剩余配额，禁止伪造百分比。
5. 任一时刻同一个文件只能有一个写入者。交接前必须写任务契约，交接后必须审查文件并运行验证。
6. Claude完成任务后通过`codex queue`唤醒原Codex任务；Codex定时心跳检查新交接、额度状态和未完成任务。
7. 不自动使用Codex用量重置额度，除非用户再次明确授权。

## 当前调度

- Codex心跳：每30分钟。
- Claude新任务阈值：当前会话使用量达到90%时停止派发。
- Claude完成回调目标：当前港口实训开发任务。
- 当前任务拥有者：Codex。
- 共享任务契约：`PortTraining/Docs/Claude任务/YYYY-MM-DD/`。
- Claude结构化交接：`PortTraining/Docs/Claude交接/YYYY-MM-DD/`。
