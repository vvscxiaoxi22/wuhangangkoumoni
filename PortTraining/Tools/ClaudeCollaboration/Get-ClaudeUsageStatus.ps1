[CmdletBinding()]
param(
    [ValidateRange(1, 100)]
    [int]$SessionUsedCeilingPercent = 90
)

$ErrorActionPreference = "Stop"

try {
    $claudeCommand = Get-Command claude -ErrorAction Stop
    $standardOutput = & $claudeCommand.Source `
        -p "/usage" `
        --output-format json `
        --permission-mode dontAsk `
        --disallowedTools "Read,Glob,Grep,Edit,Write,NotebookEdit,Bash,PowerShell,WebFetch,WebSearch" |
        Out-String

    if ($LASTEXITCODE -ne 0) {
        throw "claude /usage exitCode=$LASTEXITCODE"
    }

    $payload = $standardOutput | ConvertFrom-Json
    if ($payload.is_error) {
        throw "claude /usage returned is_error=true"
    }

    $usageText = [string]$payload.result
    $sessionMatch = [regex]::Match(
        $usageText,
        "Current session:\s*(?<used>\d+)% used(?:\s*·\s*resets (?<reset>[^\r\n]+))?",
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase
    )
    $weekMatch = [regex]::Match(
        $usageText,
        "Current week[^:]*:\s*(?<used>\d+)% used(?:\s*·\s*resets (?<reset>[^\r\n]+))?",
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase
    )

    if (-not $sessionMatch.Success) {
        throw "无法从Claude /usage结果解析当前会话用量"
    }

    $sessionUsedPercent = [int]$sessionMatch.Groups["used"].Value
    $weekUsedPercent = if ($weekMatch.Success) {
        [int]$weekMatch.Groups["used"].Value
    }
    else {
        $null
    }

    [pscustomobject]@{
        CheckedAt = [DateTimeOffset]::Now.ToString("o")
        IsAvailable = $true
        SessionUsedPercent = $sessionUsedPercent
        SessionRemainingPercent = 100 - $sessionUsedPercent
        SessionResetText = $sessionMatch.Groups["reset"].Value.Trim()
        WeekUsedPercent = $weekUsedPercent
        WeekResetText = if ($weekMatch.Success) { $weekMatch.Groups["reset"].Value.Trim() } else { "" }
        CeilingPercent = $SessionUsedCeilingPercent
        ShouldHandoffToCodex = $sessionUsedPercent -ge $SessionUsedCeilingPercent
        Reason = if ($sessionUsedPercent -ge $SessionUsedCeilingPercent) {
            "SESSION_USAGE_AT_OR_ABOVE_CEILING"
        }
        else {
            "CLAUDE_AVAILABLE"
        }
    }
}
catch {
    [pscustomobject]@{
        CheckedAt = [DateTimeOffset]::Now.ToString("o")
        IsAvailable = $false
        SessionUsedPercent = $null
        SessionRemainingPercent = $null
        SessionResetText = ""
        WeekUsedPercent = $null
        WeekResetText = ""
        CeilingPercent = $SessionUsedCeilingPercent
        ShouldHandoffToCodex = $true
        Reason = $_.Exception.Message
    }
}
