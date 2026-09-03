[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$TaskFile,

    [ValidateSet("Review", "Implement")]
    [string]$Mode = "Review",

    [string]$ResumeSessionId = "",

    [decimal]$MaxBudgetUsd = 0,

    [ValidateRange(1, 100)]
    [int]$ClaudeUsageCeilingPercent = 90,

    [ValidatePattern("^[0-9a-fA-F-]{36}$")]
    [string]$WakeCodexThreadId = ""
)

$ErrorActionPreference = "Stop"

function Get-SafeFileName {
    param([Parameter(Mandatory = $true)][string]$Value)

    $invalidCharacters = [System.IO.Path]::GetInvalidFileNameChars()
    $builder = [System.Text.StringBuilder]::new()
    foreach ($character in $Value.ToCharArray()) {
        if ($invalidCharacters -contains $character) {
            [void]$builder.Append("-")
        }
        else {
            [void]$builder.Append($character)
        }
    }

    return $builder.ToString().Trim("-", " ")
}

$workspaceRoot = [System.IO.Path]::GetFullPath(
    (Join-Path -Path $PSScriptRoot -ChildPath "..\..\..")
).TrimEnd("\", "/")
$workspacePrefix = $workspaceRoot + [System.IO.Path]::DirectorySeparatorChar
$resolvedTaskFile = (Resolve-Path -LiteralPath $TaskFile).Path

if (-not $resolvedTaskFile.StartsWith(
        $workspacePrefix,
        [System.StringComparison]::OrdinalIgnoreCase
    )) {
    throw "任务契约必须位于工作区内：$workspaceRoot"
}

if ([System.IO.Path]::GetExtension($resolvedTaskFile) -ne ".md") {
    throw "任务契约必须是Markdown文件：$resolvedTaskFile"
}

$claudeCommand = Get-Command claude -ErrorAction Stop
$usageScript = Join-Path $PSScriptRoot "Get-ClaudeUsageStatus.ps1"
$usageStatus = & $usageScript -SessionUsedCeilingPercent $ClaudeUsageCeilingPercent
if (-not $usageStatus.IsAvailable) {
    throw "Claude用量状态不可用，转由Codex继续开发。reason=$($usageStatus.Reason)"
}

if ($usageStatus.ShouldHandoffToCodex) {
    throw "Claude会话用量已达到接力阈值，转由Codex继续开发。used=$($usageStatus.SessionUsedPercent)%, ceiling=$ClaudeUsageCeilingPercent%, reset=$($usageStatus.SessionResetText)"
}

$taskText = Get-Content -LiteralPath $resolvedTaskFile -Raw
$taskName = Get-SafeFileName ([System.IO.Path]::GetFileNameWithoutExtension($resolvedTaskFile))
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$date = Get-Date -Format "yyyy-MM-dd"
$handoffRoot = Join-Path $workspaceRoot "PortTraining\Docs\Claude交接"
$resultDirectory = Join-Path $handoffRoot $date
$lockPath = Join-Path $handoffRoot ".claude-task.lock"

[System.IO.Directory]::CreateDirectory($resultDirectory) | Out-Null

$lockStream = $null
try {
    try {
        $lockStream = [System.IO.File]::Open(
            $lockPath,
            [System.IO.FileMode]::CreateNew,
            [System.IO.FileAccess]::Write,
            [System.IO.FileShare]::None
        )
    }
    catch [System.IO.IOException] {
        throw "已有Claude受控任务正在运行，或上次异常退出留下锁文件：$lockPath"
    }

    $lockWriter = [System.IO.StreamWriter]::new(
        $lockStream,
        [System.Text.UTF8Encoding]::new($false),
        1024,
        $true
    )
    $lockWriter.WriteLine("pid=$PID")
    $lockWriter.WriteLine("task=$resolvedTaskFile")
    $lockWriter.WriteLine("startedAt=$([DateTimeOffset]::Now.ToString('o'))")
    $lockWriter.Flush()
    $lockWriter.Dispose()

    $controlPrompt = @"
你是港口实训平台工作区中的受控协作开发代理。请先读取工作区根目录CLAUDE.md，以及任务契约点名的规则和资料，然后严格执行下面的任务契约。

控制要求：
1. 不扩大任务范围，不建立平行实现，不改变既有接口和版本链，除非契约明确要求。
2. 只读审查任务不得修改任何文件；实现任务只能修改契约列出的文件。
3. 不启动Unity、不操作GUI、不安装依赖、不提交Git、不访问外部系统。
4. 不把编译成功或静态推断写成运行验证通过。
5. 最终回复必须包含：结论、根因或实现说明、涉及文件与行号、实际修改、实际验证、未验证范围、建议下一步。

任务契约如下：

$taskText
"@

    $claudeArguments = @(
        "-p",
        $controlPrompt,
        "--output-format",
        "json",
        "--name",
        "porttraining-$taskName"
    )

    if ($ResumeSessionId) {
        $claudeArguments += @("--resume", $ResumeSessionId)
    }

    if ($Mode -eq "Review") {
        $claudeArguments += @(
            "--permission-mode",
            "dontAsk",
            "--allowedTools",
            "Read,Glob,Grep",
            "--disallowedTools",
            "Edit,Write,NotebookEdit,Bash,PowerShell,WebFetch,WebSearch"
        )
    }
    else {
        $claudeArguments += @(
            "--permission-mode",
            "acceptEdits",
            "--allowedTools",
            "Read,Glob,Grep,Edit,Write",
            "--disallowedTools",
            "NotebookEdit,Bash,PowerShell,WebFetch,WebSearch"
        )
    }

    if ($MaxBudgetUsd -gt 0) {
        $claudeArguments += @("--max-budget-usd", $MaxBudgetUsd.ToString(
                [System.Globalization.CultureInfo]::InvariantCulture
            ))
    }

    $stderrTemporaryPath = [System.IO.Path]::GetTempFileName()
    try {
        Push-Location $workspaceRoot
        try {
            $standardOutput = & $claudeCommand.Source @claudeArguments 2> $stderrTemporaryPath | Out-String
            $exitCode = $LASTEXITCODE
        }
        finally {
            Pop-Location
        }

        $standardError = Get-Content -LiteralPath $stderrTemporaryPath -Raw -ErrorAction SilentlyContinue
    }
    finally {
        Remove-Item -LiteralPath $stderrTemporaryPath -Force -ErrorAction SilentlyContinue
    }

    $resultPath = Join-Path $resultDirectory "$timestamp-$taskName.json"
    $stderrPath = Join-Path $resultDirectory "$timestamp-$taskName.stderr.txt"
    $utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($resultPath, $standardOutput, $utf8WithoutBom)

    if (-not [string]::IsNullOrWhiteSpace($standardError)) {
        [System.IO.File]::WriteAllText($stderrPath, $standardError, $utf8WithoutBom)
    }

    if ($exitCode -ne 0) {
        throw "Claude任务执行失败，exitCode=$exitCode；结果：$resultPath；stderr：$stderrPath"
    }

    try {
        $result = $standardOutput | ConvertFrom-Json
    }
    catch {
        throw "Claude返回内容不是有效JSON；原始结果已保存：$resultPath"
    }

    [pscustomobject]@{
        TaskName = $taskName
        Mode = $Mode
        SessionId = $result.session_id
        ResultPath = $resultPath
        IsError = $result.is_error
        DurationMilliseconds = $result.duration_ms
        ClaudeSessionUsedPercentBeforeRun = $usageStatus.SessionUsedPercent
        ClaudeWeekUsedPercentBeforeRun = $usageStatus.WeekUsedPercent
        Result = $result.result
    }

    if ($WakeCodexThreadId) {
        $codexCommand = Get-Command codex -ErrorAction Stop
        $wakeMessage = "Claude受控任务已完成：$taskName；mode=$Mode；session=$($result.session_id)；result=$resultPath。请读取交接结果、审查实际文件并执行验证。"
        & $codexCommand.Source queue --thread $WakeCodexThreadId --message $wakeMessage | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Claude任务已经成功，但唤醒Codex失败。result=$resultPath"
        }
    }
}
finally {
    if ($null -ne $lockStream) {
        $lockStream.Dispose()
    }

    Remove-Item -LiteralPath $lockPath -Force -ErrorAction SilentlyContinue
}
