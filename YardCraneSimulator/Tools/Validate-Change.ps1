[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string[]]$Paths,

    [switch]$Full,

    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$workspaceRoot = Split-Path -Parent $projectRoot
$pathComparison = [System.StringComparison]::OrdinalIgnoreCase
$projectPrefix = $projectRoot.TrimEnd([char[]]@('\', '/')) + [System.IO.Path]::DirectorySeparatorChar

function Emit([string]$Text) {
    [Console]::WriteLine($Text)
}

trap {
    Emit "ERROR $($_.Exception.Message)"
    Emit 'RESULT FAIL reason=router-initialization-error'
    exit 1
}

function Quote-CommandArgument([string]$Value) {
    if ($Value -match '^[A-Za-z0-9_./:\\-]+$') {
        return $Value
    }

    return "'" + $Value.Replace("'", "''") + "'"
}

function Format-CommandLine([string]$Executable, [string[]]$Arguments) {
    $parts = [System.Collections.Generic.List[string]]::new()
    $parts.Add((Quote-CommandArgument $Executable))
    foreach ($argument in $Arguments) {
        $parts.Add((Quote-CommandArgument $argument))
    }

    return $parts -join ' '
}

function Normalize-InputPath([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $null
    }

    $candidate = $Value.Trim().Trim('"')
    if ([System.IO.Path]::IsPathRooted($candidate)) {
        $fullPath = [System.IO.Path]::GetFullPath($candidate)
    }
    else {
        $slashPath = $candidate.Replace('\', '/')
        while ($slashPath.StartsWith('./', [System.StringComparison]::Ordinal)) {
            $slashPath = $slashPath.Substring(2)
        }

        $joinPath = $slashPath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
        if ($slashPath.StartsWith('YardCraneSimulator/', $pathComparison)) {
            $fullPath = [System.IO.Path]::GetFullPath((Join-Path $workspaceRoot $joinPath))
        }
        else {
            $fullPath = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $joinPath))
        }
    }

    if (-not $fullPath.StartsWith($projectPrefix, $pathComparison)) {
        throw "Path is outside YardCraneSimulator: $Value"
    }

    return $fullPath.Substring($projectPrefix.Length).Replace('\', '/')
}

function Get-GitDiffPaths {
    if ($null -eq (Get-Command git -ErrorAction SilentlyContinue)) {
        throw 'Git is unavailable. Pass -Paths explicitly.'
    }

    # Windows PowerShell 5.1 can promote a native program's stderr warning to a
    # terminating ErrorRecord when the caller uses Stop. Suppress Git's line-ending
    # warnings locally, then restore the router's fail-fast behavior.
    $savedErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'

        $null = & git -C $projectRoot rev-parse --is-inside-work-tree 2>$null
        $gitExitCode = $LASTEXITCODE
        if ($gitExitCode -ne 0) {
            throw "YardCraneSimulator is not inside a readable Git worktree: $projectRoot"
        }

        $null = & git -C $projectRoot rev-parse --verify HEAD 2>$null
        $gitExitCode = $LASTEXITCODE
        if ($gitExitCode -ne 0) {
            throw 'Git HEAD is unavailable. Pass -Paths explicitly.'
        }

        $changed = @(& git -C $projectRoot diff --name-only --relative --diff-filter=ACDMRTUXB HEAD -- . 2>$null)
        $gitExitCode = $LASTEXITCODE
        if ($gitExitCode -ne 0) {
            throw 'git diff failed. Pass -Paths explicitly.'
        }

        return @($changed | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    }
    finally {
        $ErrorActionPreference = $savedErrorActionPreference
    }
}

function Get-EffectiveExtension([string]$RelativePath) {
    $name = $RelativePath.ToLowerInvariant()
    if ($name.EndsWith('.meta', [System.StringComparison]::Ordinal)) {
        $name = $name.Substring(0, $name.Length - '.meta'.Length)
    }

    return [System.IO.Path]::GetExtension($name)
}

function Get-Classification([string]$RelativePath) {
    $lower = $RelativePath.ToLowerInvariant()
    $extension = Get-EffectiveExtension $RelativePath
    $uiAssetRoot = $lower -match '^assets/yardcranesimulator/(shell/resources/(yardcraneshell|teacherside|yardcraneui)/|art/ui/)'
    $uiArtworkExtensions = @('.png', '.jpg', '.jpeg', '.svg', '.tga', '.psd')

    if ($extension -eq '.md' -or $extension -eq '.txt') {
        return [pscustomobject]@{
            Rank = 1
            Code = 'DOC_ONLY'
            Label = 'documentation only'
            Reason = 'Documentation-only change; text readability and whitespace checks are sufficient.'
        }
    }

    if (($extension -eq '.uss') -or ($extension -eq '.tss') -or
        ($uiAssetRoot -and $uiArtworkExtensions -contains $extension)) {
        return [pscustomobject]@{
            Rank = 1
            Code = 'UI_PURE_STYLE'
            Label = 'UI pure style'
            Reason = 'USS or UI artwork; no runtime code or UI query structure is included.'
        }
    }

    $highRiskCode = $extension -eq '.cs' -and (
        $lower -match '^assets/yardcranesimulator/experiments/obirope/' -or
        $lower -match '^assets/yardcranesimulator/runtime/(bootstrap|configuration|crane|gateway|input|ipc|operations|training)/' -or
        $lower -eq 'assets/yardcranesimulator/shell/yardcranelocaltrainingnavigationbridge.cs')
    if ($highRiskCode) {
        return [pscustomobject]@{
            Rank = 4
            Code = 'HIGH_RISK_RUNTIME'
            Label = 'high-risk runtime logic'
            Reason = 'Matches motion, input, communication, bootstrap, training state, or physics runtime paths.'
        }
    }

    $sceneOrConfig = $lower.StartsWith('projectsettings/', $pathComparison) -or
        $lower.StartsWith('packages/', $pathComparison) -or
        $lower -match '/streamingassets/' -or
        @('.unity', '.prefab', '.asset', '.json', '.asmdef', '.asmref', '.yaml', '.yml', '.xml', '.shader') -contains $extension
    if ($sceneOrConfig) {
        return [pscustomobject]@{
            Rank = 3
            Code = 'SCENE_CONFIG'
            Label = 'scene/configuration'
            Reason = 'Scene, Prefab, ScriptableObject, build setting, or runtime configuration changed.'
        }
    }

    if ($extension -eq '.uxml' -or $extension -eq '.cs' -or $extension -eq '.csproj' -or $extension -eq '.sln' -or
        $extension -eq '.ps1') {
        return [pscustomobject]@{
            Rank = 2
            Code = 'LOCAL_SOURCE'
            Label = 'local source or UI structure'
            Reason = 'Local source, UI structure, Harness, tool, or documentation change outside high-risk runtime paths.'
        }
    }

    return [pscustomobject]@{
        Rank = 3
        Code = 'SCENE_CONFIG'
        Label = 'scene/configuration'
        Reason = 'A non-source project asset is conservatively treated as scene/configuration.'
    }
}

function Get-RelativeProjectPath([string]$FullPath) {
    $normalized = [System.IO.Path]::GetFullPath($FullPath)
    if (-not $normalized.StartsWith($projectPrefix, $pathComparison)) {
        return $null
    }

    return $normalized.Substring($projectPrefix.Length).Replace('\', '/')
}

function Get-ProjectCompileSources([string]$ProjectRelativePath) {
    $projectPath = Join-Path $projectRoot $ProjectRelativePath
    $sourceSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    [void]$sourceSet.Add($ProjectRelativePath.Replace('\', '/'))

    $projectDirectory = Split-Path -Parent $projectPath
    if (Test-Path -LiteralPath $projectDirectory -PathType Container) {
        foreach ($localFile in Get-ChildItem -LiteralPath $projectDirectory -File) {
            if ($localFile.Extension -eq '.cs' -or $localFile.Extension -eq '.csproj') {
                $relative = Get-RelativeProjectPath $localFile.FullName
                if ($null -ne $relative) {
                    [void]$sourceSet.Add($relative)
                }
            }
        }
    }

    if (Test-Path -LiteralPath $projectPath -PathType Leaf) {
        $projectText = [System.IO.File]::ReadAllText($projectPath)
        foreach ($match in [regex]::Matches($projectText, '<Compile\s+Include="(?<path>[^"]+)"')) {
            $includePath = $match.Groups['path'].Value
            if ($includePath.Contains('$(') -or $includePath.Contains('*')) {
                continue
            }

            $fullSourcePath = [System.IO.Path]::GetFullPath((Join-Path $projectDirectory $includePath))
            $relative = Get-RelativeProjectPath $fullSourcePath
            if ($null -ne $relative) {
                [void]$sourceSet.Add($relative)
            }
        }
    }

    return @($sourceSet)
}

$dotnetHarnesses = @(
    [pscustomobject]@{
        Key = 'OperationStateMachine'
        Project = 'Tools/OperationStateMachineHarness/OperationStateMachineHarness.csproj'
        Reason = 'Isolated operation-state, input-routing, mechanism-semantics, and launch-option regression.'
        PassPattern = '\[YC_OPERATION_STATE_MACHINE_SELFTEST_PASS\]'
    },
    [pscustomobject]@{
        Key = 'StandaloneHost'
        Project = 'Tools/StandaloneHostHarness/StandaloneHostHarness.csproj'
        Reason = 'Isolated Standalone Host focus, SafeHold, and input-clearing regression.'
        PassPattern = '\[YC_HOST_FOCUS_PROBE\]\s+failures=0\b'
    },
    [pscustomobject]@{
        Key = 'HudPresentation'
        Project = 'Tools/HudPresentationHarness/HudPresentationHarness.csproj'
        Reason = 'Isolated HUD state, text, throttling, and local-training-progress regression.'
        PassPattern = '\[YC_UI_HARNESS_PASS\]'
    },
    [pscustomobject]@{
        Key = 'StudentShell'
        Project = 'Tools/StudentShellHarness/StudentShellHarness.csproj'
        Reason = 'Unity-independent student identity, catalog, session, and score-isolation regression.'
        PassPattern = '\[YC_STUDENT_SHELL_HARNESS_PASS\]'
    }
)

foreach ($definition in $dotnetHarnesses) {
    $definition | Add-Member -NotePropertyName Sources -NotePropertyValue @(
        Get-ProjectCompileSources $definition.Project)
}

$selectedHarnesses = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$analysis = [System.Collections.Generic.List[object]]::new()
$builtInTextPaths = [System.Collections.Generic.List[string]]::new()
$studentUiCompileNeeded = $false

function Select-Harness([string]$Key, [System.Collections.Generic.List[string]]$PathHarnesses) {
    [void]$selectedHarnesses.Add($Key)
    if (-not $PathHarnesses.Contains($Key)) {
        $PathHarnesses.Add($Key)
    }
}

function Get-PowerShellExecutable {
    $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($null -ne $pwsh) {
        return $pwsh.Source
    }

    $currentProcess = Get-Process -Id $PID
    if (-not [string]::IsNullOrWhiteSpace($currentProcess.Path)) {
        return $currentProcess.Path
    }

    return 'powershell.exe'
}

function Get-HarnessPlan([string]$Key) {
    $powershellExecutable = Get-PowerShellExecutable
    switch ($Key) {
        'StudentHomeUi' {
            $scriptPath = Join-Path $projectRoot 'Tools/StudentHomeUiHarness/Invoke-StudentHomeUiChecks.ps1'
            $arguments = [System.Collections.Generic.List[string]]::new()
            foreach ($argument in @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $scriptPath,
                    '-ProjectPath', $projectRoot)) {
                $arguments.Add($argument)
            }
            if (-not $studentUiCompileNeeded) {
                $arguments.Add('-SkipCompile')
            }

            return [pscustomobject]@{
                Key = $Key
                Executable = $powershellExecutable
                Arguments = $arguments.ToArray()
                Reason = 'Static StudentEntry/StudentHome UXML, USS, resource, query, and scene contracts.'
                PassPattern = '^FINAL PASS checks=\d+ failures=0\b'
            }
        }
        'TeacherHomeUi' {
            $scriptPath = Join-Path $projectRoot 'Tools/TeacherHomeUiHarness/Invoke-TeacherHomeUiChecks.ps1'
            return [pscustomobject]@{
                Key = $Key
                Executable = $powershellExecutable
                Arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $scriptPath,
                    '-ProjectPath', $projectRoot)
                Reason = 'Static TeacherHome dual-document, resource, scene, and Build Settings contracts.'
                PassPattern = '\[YC_TEACHER_HOME_UI_STATIC_PASS\]\s+checks=\d+\s+failures=0\b'
            }
        }
    }

    $definition = $dotnetHarnesses | Where-Object { $_.Key -eq $Key } | Select-Object -First 1
    if ($null -eq $definition) {
        throw "Unknown Harness key: $Key"
    }

    $projectPath = Join-Path $projectRoot $definition.Project
    return [pscustomobject]@{
        Key = $Key
        Executable = 'dotnet'
        Arguments = @('run', '--project', $projectPath, '-c', 'Release')
        Reason = $definition.Reason
        PassPattern = $definition.PassPattern
    }
}

function Invoke-HarnessPlan([object]$Plan) {
    $display = Format-CommandLine $Plan.Executable $Plan.Arguments
    Emit "COMMAND harness=$($Plan.Key) cwd=$(Quote-CommandArgument $projectRoot) :: $display"
    Emit "COMMAND_REASON harness=$($Plan.Key) :: $($Plan.Reason)"

    if ($DryRun) {
        Emit "STATUS harness=$($Plan.Key) DEFERRED reason=dry-run"
        return [pscustomobject]@{ Key = $Plan.Key; Status = 'DEFERRED'; ExitCode = $null }
    }

    if ($null -eq (Get-Command $Plan.Executable -ErrorAction SilentlyContinue)) {
        Emit "STATUS harness=$($Plan.Key) FAIL reason=executable-not-found"
        return [pscustomobject]@{ Key = $Plan.Key; Status = 'FAIL'; ExitCode = 1 }
    }

    $savedErrorActionPreference = $ErrorActionPreference
    Push-Location $projectRoot
    try {
        # Preserve native stderr as evidence instead of letting Windows PowerShell 5.1
        # turn a warning line into a terminating ErrorRecord.
        $ErrorActionPreference = 'Continue'
        $commandOutput = @(& $Plan.Executable @($Plan.Arguments) 2>&1)
        $exitCode = $LASTEXITCODE
        $ErrorActionPreference = $savedErrorActionPreference
        $evidence = @($commandOutput | Where-Object {
            [regex]::IsMatch(([string]$_), $Plan.PassPattern)
        } | Select-Object -Last 1)

        if ($exitCode -eq 0 -and $evidence.Count -eq 1) {
            Emit "EVIDENCE harness=$($Plan.Key) :: $([string]$evidence[0])"
            Emit "STATUS harness=$($Plan.Key) PASS exit=0"
            return [pscustomobject]@{ Key = $Plan.Key; Status = 'PASS'; ExitCode = 0 }
        }

        foreach ($line in @($commandOutput | Select-Object -Last 40)) {
            Emit "HARNESS_OUTPUT harness=$($Plan.Key) :: $([string]$line)"
        }
        $failureReason = if ($exitCode -ne 0) { "exit-$exitCode" } else { 'pass-marker-missing' }
        Emit "STATUS harness=$($Plan.Key) FAIL reason=$failureReason"
        return [pscustomobject]@{ Key = $Plan.Key; Status = 'FAIL'; ExitCode = $exitCode }
    }
    catch {
        Emit "STATUS harness=$($Plan.Key) FAIL reason=$($_.Exception.Message)"
        return [pscustomobject]@{ Key = $Plan.Key; Status = 'FAIL'; ExitCode = 1 }
    }
    finally {
        $ErrorActionPreference = $savedErrorActionPreference
        Pop-Location
    }
}

function Invoke-TextSanity([string[]]$RelativePaths) {
    Emit "COMMAND builtin=TextSanity files=$($RelativePaths.Count) :: read UTF text, reject NUL bytes, run git diff --check"
    if ($DryRun) {
        Emit 'STATUS harness=TextSanity DEFERRED reason=dry-run'
        return [pscustomobject]@{ Key = 'TextSanity'; Status = 'DEFERRED'; ExitCode = $null }
    }

    foreach ($relativePath in $RelativePaths) {
        $fullPath = Join-Path $projectRoot $relativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
        if (Test-Path -LiteralPath $fullPath -PathType Leaf) {
            $text = [System.IO.File]::ReadAllText($fullPath)
            if ($text.IndexOf([char]0) -ge 0) {
                Emit "STATUS harness=TextSanity FAIL reason=nul-byte path=$relativePath"
                return [pscustomobject]@{ Key = 'TextSanity'; Status = 'FAIL'; ExitCode = 1 }
            }
        }
        else {
            $savedErrorActionPreference = $ErrorActionPreference
            try {
                $ErrorActionPreference = 'Continue'
                $null = & git -C $projectRoot ls-files --error-unmatch -- $relativePath 2>$null
                $trackedExitCode = $LASTEXITCODE
            }
            finally {
                $ErrorActionPreference = $savedErrorActionPreference
            }

            if ($trackedExitCode -ne 0) {
                Emit "STATUS harness=TextSanity FAIL reason=path-not-found path=$relativePath"
                return [pscustomobject]@{ Key = 'TextSanity'; Status = 'FAIL'; ExitCode = 1 }
            }
        }
    }

    $savedErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $diffOutput = @(& git -C $projectRoot diff --check HEAD -- @RelativePaths 2>&1)
        $diffExitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $savedErrorActionPreference
    }

    if ($diffExitCode -ne 0) {
        foreach ($line in $diffOutput) {
            Emit "HARNESS_OUTPUT harness=TextSanity :: $([string]$line)"
        }
        Emit "STATUS harness=TextSanity FAIL reason=git-diff-check exit=$diffExitCode"
        return [pscustomobject]@{ Key = 'TextSanity'; Status = 'FAIL'; ExitCode = $diffExitCode }
    }

    Emit "EVIDENCE harness=TextSanity :: files=$($RelativePaths.Count) readable=true diffCheck=true"
    Emit 'STATUS harness=TextSanity PASS exit=0'
    return [pscustomobject]@{ Key = 'TextSanity'; Status = 'PASS'; ExitCode = 0 }
}

try {
    $usingExplicitPaths = $PSBoundParameters.ContainsKey('Paths')
    $rawPaths = if ($usingExplicitPaths) { @($Paths) } else { @(Get-GitDiffPaths) }
    $sourceMode = if ($usingExplicitPaths) { 'explicit-paths' } else { 'git-diff-head-tracked' }

    $uniquePaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($rawPath in $rawPaths) {
        $normalizedPath = Normalize-InputPath ([string]$rawPath)
        if ($null -ne $normalizedPath) {
            [void]$uniquePaths.Add($normalizedPath)
        }
    }

    $changedPaths = @($uniquePaths | Sort-Object)
    Emit "SOURCE mode=$sourceMode project=$(Quote-CommandArgument $projectRoot) files=$($changedPaths.Count)"
    if (-not $usingExplicitPaths) {
        Emit 'NOTICE scope=tracked-only :: Untracked files are intentionally excluded; pass -Paths for every file in the current task.'
    }
    if ($changedPaths.Count -eq 0) {
        Emit 'LEVEL rank=0 code=NONE name=no-change'
        Emit 'COMMAND none'
        if ($Full) {
            Emit 'FULL_PLAY DEFERRED validator="Tools/Yard Crane/Tests/Run Full Play Verification" reason=must-be-run-by-the-existing-Unity-validator; this-script-did-not-start-Unity'
        }
        Emit 'RESULT DEFERRED reason=no-changed-paths'
        exit 2
    }

    foreach ($relativePath in $changedPaths) {
        $classification = Get-Classification $relativePath
        $lower = $relativePath.ToLowerInvariant()
        $pathHarnesses = [System.Collections.Generic.List[string]]::new()
        $coveredByLightHarness = $false
        $operationContractChanged = $false

        if ($classification.Code -eq 'DOC_ONLY') {
            $builtInTextPaths.Add($relativePath)
            $coveredByLightHarness = $true
        }

        foreach ($definition in $dotnetHarnesses) {
            if ($definition.Sources -contains $relativePath) {
                Select-Harness $definition.Key $pathHarnesses
                $coveredByLightHarness = $true
                if ($definition.Key -eq 'OperationStateMachine' -and
                    $lower -ne 'tools/operationstatemachineharness/program.cs') {
                    $operationContractChanged = $true
                }
            }
        }

        # OperationStateMachine is referenced by both consumers. Compiling the
        # dependent lightweight Harnesses catches public-contract breaks without Unity.
        if ($operationContractChanged) {
            Select-Harness 'StandaloneHost' $pathHarnesses
            Select-Harness 'HudPresentation' $pathHarnesses
        }

        $isShellCSharp = $lower -match '^assets/yardcranesimulator/shell/[^/]+\.cs$'
        $isStudentEditorSource = $lower -match '^assets/yardcranesimulator/editor/yardcranestudent(shell|sideshell)editor\.cs$'
        $isStudentCheckedUiAsset =
            $lower -match '^assets/yardcranesimulator/shell/resources/yardcraneshell/display1ui/(studentshellupper\.uxml|studentshellupperstyles\.uss)(\.meta)?$' -or
            $lower -match '^assets/yardcranesimulator/shell/resources/yardcraneshell/studentside/display1ui/(studentsideshell\.uxml|studentsideshellstyles\.uss)(\.meta)?$' -or
            $lower -match '^assets/yardcranesimulator/shell/resources/yardcraneshell/studentside/display2ui/(studentsidelower\.uxml|studentsidelowerstyles\.uss)(\.meta)?$' -or
            $lower -match '^assets/yardcranesimulator/shell/resources/yardcraneshell/(display1ui/studentshellupper|studentside/display1ui/studentsideshell|studentside/display2ui/studentsidelower)panel\.asset(\.meta)?$' -or
            $lower -match '^assets/yardcranesimulator/shell/resources/yardcraneshell/studentside/display1ui/learningthumbnails/drill(gantry|hoist|spreaderrotation|spreaderzeroing|trolley)\.png(\.meta)?$'
        $isStudentHomeScene = $lower -eq 'assets/yardcranesimulator/scenes/studenthome.unity'
        $isBuildSettings = $lower -eq 'projectsettings/editorbuildsettings.asset'

        $needsStudentShellCompilation = ($isShellCSharp -and -not $coveredByLightHarness) -or $isStudentEditorSource
        if ($needsStudentShellCompilation -or $isStudentCheckedUiAsset -or
            $isStudentHomeScene -or $isBuildSettings) {
            Select-Harness 'StudentHomeUi' $pathHarnesses
            if ($needsStudentShellCompilation) {
                $studentUiCompileNeeded = $true
                $coveredByLightHarness = $true
            }
            elseif ($isStudentHomeScene -or $isBuildSettings -or $isStudentCheckedUiAsset) {
                $coveredByLightHarness = $true
            }
        }

        $isTeacherCheckedUiAsset =
            $lower -match '^assets/yardcranesimulator/shell/resources/teacherside/display1ui/(teachersideshell\.uxml|teachersideshellstyles\.uss)(\.meta)?$' -or
            $lower -match '^assets/yardcranesimulator/shell/resources/teacherside/display2ui/(teachersidelower\.uxml|teachersidelowerstyles\.uss)(\.meta)?$' -or
            $lower -match '^assets/yardcranesimulator/shell/resources/teacherside/(display1ui/teachersideshell|display2ui/teachersidelower)panel\.asset(\.meta)?$'
        $isTeacherScene = $lower -eq 'assets/yardcranesimulator/scenes/teacherhome.unity'
        $isTeacherShell = $lower -eq 'assets/yardcranesimulator/shell/yardcraneteachersideshell.cs'
        if ($isTeacherCheckedUiAsset -or $isTeacherScene -or $isTeacherShell -or $isBuildSettings) {
            Select-Harness 'TeacherHomeUi' $pathHarnesses
            $coveredByLightHarness = $true
        }

        $analysis.Add([pscustomobject]@{
            Path = $relativePath
            Classification = $classification
            Harnesses = $pathHarnesses.ToArray()
            Covered = $coveredByLightHarness
        })
    }

    $selectedClassification = $analysis |
        Sort-Object { $_.Classification.Rank } -Descending |
        Select-Object -First 1 -ExpandProperty Classification
    $classCounts = $analysis | Group-Object { $_.Classification.Code } | Sort-Object Name
    $countSummary = @($classCounts | ForEach-Object { "$($_.Name)=$($_.Count)" }) -join ','

    Emit "LEVEL rank=$($selectedClassification.Rank) code=$($selectedClassification.Code) name=$($selectedClassification.Label)"
    Emit "REASON selected-highest-risk counts=$countSummary :: $($selectedClassification.Reason)"
    foreach ($item in $analysis) {
        $harnessText = if ($item.Harnesses.Count -gt 0) { $item.Harnesses -join ',' } else { 'none' }
        $coverageText = if ($item.Covered) { 'matched' } else { 'unmatched' }
        Emit "PATH level=$($item.Classification.Code) coverage=$coverageText harness=$harnessText :: $($item.Path) :: $($item.Classification.Reason)"
    }

    $harnessOrder = @(
        'OperationStateMachine',
        'StandaloneHost',
        'HudPresentation',
        'StudentShell',
        'StudentHomeUi',
        'TeacherHomeUi')
    $results = [System.Collections.Generic.List[object]]::new()
    foreach ($key in $harnessOrder) {
        if ($selectedHarnesses.Contains($key)) {
            $results.Add((Invoke-HarnessPlan (Get-HarnessPlan $key)))
        }
    }
    if ($builtInTextPaths.Count -gt 0) {
        $results.Add((Invoke-TextSanity $builtInTextPaths.ToArray()))
    }

    if ($results.Count -eq 0) {
        Emit 'COMMAND none'
        Emit 'STATUS harness=none DEFERRED reason=no-matching-lightweight-harness'
    }

    $uncoveredCount = @($analysis | Where-Object { -not $_.Covered }).Count
    $failedCount = @($results | Where-Object { $_.Status -eq 'FAIL' }).Count

    if ($Full) {
        Emit 'FULL_PLAY DEFERRED validator="Tools/Yard Crane/Tests/Run Full Play Verification" reason=must-be-run-by-the-existing-Unity-validator; this-script-did-not-start-Unity'
    }

    if ($failedCount -gt 0) {
        Emit "RESULT FAIL reason=lightweight-harness-failure failures=$failedCount"
        exit 1
    }

    if ($DryRun) {
        Emit 'RESULT DEFERRED reason=dry-run-no-command-executed'
        exit 2
    }

    if ($Full) {
        Emit 'RESULT DEFERRED reason=full-play-requested-but-deliberately-not-started'
        exit 2
    }

    if ($uncoveredCount -gt 0) {
        Emit "RESULT DEFERRED reason=paths-without-direct-lightweight-coverage count=$uncoveredCount"
        exit 2
    }

    if ($selectedClassification.Rank -ge 3) {
        Emit "RESULT DEFERRED reason=$($selectedClassification.Code)-exceeds-lightweight-proof"
        exit 2
    }

    Emit 'RESULT PASS scope=matched-lightweight-harnesses'
    exit 0
}
catch {
    Emit "ERROR $($_.Exception.Message)"
    Emit 'RESULT FAIL reason=router-error'
    exit 1
}
