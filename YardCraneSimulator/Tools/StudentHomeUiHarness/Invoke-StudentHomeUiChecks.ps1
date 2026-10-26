[CmdletBinding()]
param(
    [string]$ProjectPath = '',
    [string]$UnityEditorRoot = 'D:/App/6000.0.42f1/Editor',
    [switch]$SkipCompile
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectPath)) { $ProjectPath = Join-Path $PSScriptRoot '../..' }
$projectRoot = (Resolve-Path -LiteralPath $ProjectPath).Path
$outputRoot = Join-Path $PSScriptRoot 'artifacts'
[void][System.IO.Directory]::CreateDirectory($outputRoot)
$report = [System.Collections.Generic.List[string]]::new()
$failures = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()
$checks = 0

function Record([string]$Text) {
    $report.Add($Text)
    Write-Output $Text
}

function Check([bool]$Condition, [string]$Description) {
    $script:checks++
    if ($Condition) { Record "PASS $Description" }
    else { $failures.Add($Description); Record "FAIL $Description" }
}

function Warn([string]$Description) {
    $warnings.Add($Description)
    Record "WARN $Description"
}

function AssetPath([string]$RelativePath) { Join-Path $projectRoot $RelativePath }

function Guid([string]$RelativePath) {
    $meta = AssetPath ($RelativePath + '.meta')
    if (-not (Test-Path -LiteralPath $meta)) { return '' }
    $match = [regex]::Match([System.IO.File]::ReadAllText($meta), '(?m)^guid:\s*([0-9a-f]{32})\s*$')
    return $match.Groups[1].Value
}

function ReadUxml([string]$RelativePath) {
    $xml = [xml]::new()
    $xml.Load((AssetPath $RelativePath))
    $names = @($xml.SelectNodes('//*[@name]') | ForEach-Object { $_.GetAttribute('name') })
    $duplicates = @($names | Group-Object | Where-Object { $_.Count -gt 1 })
    Check ($duplicates.Count -eq 0) "$RelativePath has unique names ($($names.Count))"
    foreach ($style in $xml.SelectNodes('//*[local-name()="Style"]')) {
        $source = $style.GetAttribute('src')
        if ($source.StartsWith('project://database/')) {
            $stylePath = ($source.Substring('project://database/'.Length) -split '\?')[0]
            $styleGuid = [regex]::Match($source, '[?&]guid=([0-9a-f]{32})').Groups[1].Value
            Check (Test-Path -LiteralPath (AssetPath $stylePath)) "$RelativePath stylesheet exists: $stylePath"
            Check ((Guid $stylePath) -eq $styleGuid -and $styleGuid.Length -eq 32) "$RelativePath stylesheet GUID resolves to its actual meta"
        } else {
            $local = Join-Path (Split-Path (AssetPath $RelativePath)) $source
            Check (Test-Path -LiteralPath $local) "$RelativePath relative stylesheet exists: $source"
        }
    }
    return ,$xml
}

function CheckQueryContract([string]$SourcePath, [xml]$Xml) {
    $source = [System.IO.File]::ReadAllText((AssetPath $SourcePath))
    $queries = [regex]::Matches($source, '\broot\.Q(?:<(?<type>[\w.]+)>)?\(\s*"(?<name>[^"]+)"')
    $seen = @{}
    foreach ($query in $queries) {
        $name = $query.Groups['name'].Value
        if ($seen.ContainsKey($name)) { continue }
        $seen[$name] = $true
        $element = $Xml.SelectSingleNode("//*[@name='$name']")
        Check ($null -ne $element) "$SourcePath root query resolves: $name"
        $type = $query.Groups['type'].Value
        if ($null -ne $element -and $type -and $type -ne 'VisualElement') {
            Check ($element.LocalName -eq $type) "$name expected $type, actual $($element.LocalName)"
        }
    }
    Record "SOURCE_SHA256 $SourcePath $((Get-FileHash -LiteralPath (AssetPath $SourcePath)).Hash)"
}

function CheckStyle([string]$RelativePath) {
    $css = [System.IO.File]::ReadAllText((AssetPath $RelativePath))
    $css = [regex]::Replace($css, '/\*.*?\*/', '', [System.Text.RegularExpressions.RegexOptions]::Singleline)
    Check (([regex]::Matches($css, '\{')).Count -eq ([regex]::Matches($css, '\}')).Count) "$RelativePath brace balance"
    Check (-not [regex]::IsMatch($css, ':(last-child|first-child|nth-child|nth-of-type)\b')) "$RelativePath avoids known unsupported structural pseudo-classes"
    foreach ($match in [regex]::Matches($css, 'resource\(\s*[''"](?<path>[^''"]+)[''"]\s*\)')) {
        $resource = $match.Groups['path'].Value
        $suffix = '/Resources/' + $resource
        $matches = @(Get-ChildItem -LiteralPath (AssetPath 'Assets') -Recurse -File | Where-Object {
            $_.Extension -ne '.meta' -and (($_.FullName.Replace('\', '/') -replace '\.[^./]+$', '')).EndsWith($suffix)
        })
        Check ($matches.Count -eq 1) "$RelativePath resource resolves once: $resource (matches=$($matches.Count))"
    }
    foreach ($match in [regex]::Matches($css, 'url\(\s*[''"](?<path>[^''"]+)[''"]\s*\)')) {
        $relativeUrl = $match.Groups['path'].Value
        $target = Join-Path (Split-Path (AssetPath $RelativePath)) $relativeUrl
        Check (Test-Path -LiteralPath $target -PathType Leaf) "$RelativePath relative image exists: $relativeUrl"
        Check (Test-Path -LiteralPath ($target + '.meta') -PathType Leaf) "$RelativePath relative image has Unity metadata: $relativeUrl"
    }
    Record "SOURCE_SHA256 $RelativePath $((Get-FileHash -LiteralPath (AssetPath $RelativePath)).Hash)"
}

function CheckFaultDataAsset(
    [string]$RelativePath,
    [string]$CraneName,
    [int]$ExpectedTotal,
    [string[]]$ExpectedCategoryNames,
    [int[]]$ExpectedCategoryCounts
) {
    $path = AssetPath $RelativePath
    $exists = Test-Path -LiteralPath $path
    Check $exists "$CraneName fault ScriptableObject exists: $RelativePath"
    if (-not $exists) { return }

    $yaml = [System.IO.File]::ReadAllText($path)
    $yaml = [regex]::Replace($yaml, '\\u(?<hex>[0-9a-fA-F]{4})', {
        param($match)
        [char][Convert]::ToInt32($match.Groups['hex'].Value, 16)
    })
    $scriptGuid = Guid 'Assets/Data/Creat/故障信息.cs'
    Check ($scriptGuid -ne '' -and $yaml.Contains("m_Script: {fileID: 11500000, guid: $scriptGuid,")) "$CraneName asset uses the existing 故障信息 script"

    $matches = @([regex]::Matches(
        $yaml,
        '(?ms)^    - Key: (?<id>\d+)\r?\n      Value:\r?\n(?<body>.*?)(?=^    - Key:|\z)'))
    Check ($matches.Count -eq $ExpectedTotal) "$CraneName fault asset contains all $ExpectedTotal records"
    if ($matches.Count -eq 0) { return }

    $categoryNames = $ExpectedCategoryNames
    $records = @($matches | ForEach-Object {
        $body = $_.Groups['body'].Value
        [pscustomobject]@{
            Id = [int]$_.Groups['id'].Value
            Phenomenon = [regex]::Match($body, '(?m)^        "故障现象": "(?<value>.*)"\r?$').Groups['value'].Value
            FaultPoint = [regex]::Match($body, '(?m)^        "故障点": "(?<value>.*)"\r?$').Groups['value'].Value
            LineTag = [regex]::Match($body, '(?m)^        "标号": "(?<value>.*)"\r?$').Groups['value'].Value
            Category = [regex]::Match($body, '(?m)^        "类型": "(?<value>.*)"\r?$').Groups['value'].Value
        }
    })
    $actualCounts = @($categoryNames | ForEach-Object {
        $categoryName = $_
        @($records | Where-Object { $_.Category -eq $categoryName }).Count
    })
    Check (($actualCounts -join '|') -eq ($ExpectedCategoryCounts -join '|')) "$CraneName fault asset classifies every record into the eleven UI categories"
    Check (@($records | Where-Object { $_.Category -notin $categoryNames }).Count -eq 0) "$CraneName fault asset has no unsupported or unclassified category"
    Check ((@($records.Id | Sort-Object -Unique).Count -eq $ExpectedTotal) -and
        (($records.Id | Measure-Object -Minimum).Minimum -eq 1) -and
        (($records.Id | Measure-Object -Maximum).Maximum -eq $ExpectedTotal)) "$CraneName fault IDs are unique and contiguous"
    $categoryFieldName = if ($CraneName -eq 'Quay Crane') { 'QuayFaultCategoryNames' } else { 'YardFaultCategoryNames' }
    foreach ($shellName in @('YardCraneStudentSideShell', 'YardCraneTeacherSideShell')) {
        $shellSource = [System.IO.File]::ReadAllText((AssetPath "Assets/YardCraneSimulator/Shell/$shellName.cs"))
        $categoryArray = [regex]::Match($shellSource, "(?s)\b$categoryFieldName\s*=\s*\{(?<names>.*?)\};")
        $supportedCategories = @([regex]::Matches($categoryArray.Groups['names'].Value, '"(?<name>[^"]+)"') |
            ForEach-Object { $_.Groups['name'].Value })
        Check ($categoryArray.Success -and $supportedCategories.Count -eq 11 -and
            @($supportedCategories | Sort-Object -Unique).Count -eq 11) "$shellName exposes eleven distinct $CraneName fault categories"
        Check (($supportedCategories -join '|') -eq ($ExpectedCategoryNames -join '|')) "$shellName $CraneName category order matches the fault asset and UI"
        Check (@($records | Where-Object { $_.Category -notin $supportedCategories }).Count -eq 0) "$shellName accepts every $CraneName asset record without an unsupported category"
    }
    Record "SOURCE_SHA256 $RelativePath $((Get-FileHash -LiteralPath $path).Hash)"
}

function CheckLearningPage([xml]$Xml) {
    $classExpr = "contains(concat(' ', normalize-space(@class), ' '), ' {0} ')"
    # 2026-09-11: scoped to single-drill-row specifically (not the whole learning page)
    # now that linked-drill-row/skill-drill-row sit alongside it - see below for their own
    # card/title checks. This keeps "exactly five" meaning what it always meant: the five
    # single-item drills, not a page-wide total.
    $singleRow = $Xml.SelectSingleNode("//*[@name='single-drill-row']")
    $cards = @($singleRow.SelectNodes(".//*[$($classExpr -f 'drill-card')]"))
    $photos = @($singleRow.SelectNodes(".//*[$($classExpr -f 'drill-card-photo')]"))
    $titles = @($singleRow.SelectNodes(".//*[$($classExpr -f 'drill-card-title')]"))
    Check ($cards.Count -eq 5) 'Learning page contains exactly five single-item drill cards'
    Check ($photos.Count -eq 5) 'Learning page contains exactly five single-item thumbnail elements'
    Check (($titles | ForEach-Object { $_.GetAttribute('text') }) -join '|' -eq
        '大车移动|小车移动|吊具起升|吊具回旋|吊具回零') 'Single-item cards use the five Yard Crane operation titles in order'

    # 2026-09-11: 联动操作 (manual §5.2.5-8, 4 cards) and 能力提升 first item (manual §5.2.9,
    # 1 card) - see linked-drill-row/skill-drill-row in StudentSideShell.uxml. Neither row
    # carries a drill-card-photo (no thumbnail art was sourced for these this round), so only
    # card count and title text are checked here, not photo/GUID/resolution like the single
    # row above.
    $linkedRow = $Xml.SelectSingleNode("//*[@name='linked-drill-row']")
    $linkedCards = @($linkedRow.SelectNodes(".//*[$($classExpr -f 'drill-card')]"))
    $linkedTitles = @($linkedRow.SelectNodes(".//*[$($classExpr -f 'drill-card-title')]"))
    Check ($linkedCards.Count -eq 4) 'Learning page contains exactly four linked drill cards'
    Check (($linkedTitles | ForEach-Object { $_.GetAttribute('text') }) -join '|' -eq
        '大车小车联动|大车起升|小车起升|大车小车起升') 'Linked cards use the four manual §5.2.5-8 operation titles in order'

    $skillRow = $Xml.SelectSingleNode("//*[@name='skill-drill-row']")
    $skillCards = @($skillRow.SelectNodes(".//*[$($classExpr -f 'drill-card')]"))
    $skillTitles = @($skillRow.SelectNodes(".//*[$($classExpr -f 'drill-card-title')]"))
    Check ($skillCards.Count -eq 1) 'Learning page contains exactly one skill drill card'
    Check (($skillTitles | ForEach-Object { $_.GetAttribute('text') }) -join '|' -eq
        '大车直线行走') 'Skill card uses the manual §5.2.9 operation title'

    $faultRow = $Xml.SelectSingleNode("//*[@name='fault-drill-row']")
    $faultCards = @($faultRow.SelectNodes(".//*[$($classExpr -f 'fault-card')]"))
    $faultTitles = @($faultRow.SelectNodes(".//*[$($classExpr -f 'fault-card-title')]"))
    Check ($faultCards.Count -eq 11) 'Learning page contains exactly eleven fault-selection cards'
    Check (($faultTitles | ForEach-Object { $_.GetAttribute('text') }) -join '|' -eq
        '电源故障|起升机构|大车机构|小车机构|导板机构|前后倾机构|平面回转机构|左右倾机构|夹轮器|吊具泵|元器件损坏') 'Fault cards match the eleven quay-crane reference types in order'
    $faultNav = $Xml.SelectSingleNode("//*[@name='learning-nav-fault']")
    Check (-not $faultNav.GetAttribute('class').Contains('disabled-module')) 'Fault navigation is presented as an available category'
    $faultDetail = $Xml.SelectSingleNode("//*[@name='fault-detail-view']")
    $faultHeaders = @($faultDetail.SelectNodes(".//*[$($classExpr -f 'fault-table-header-cell')]"))
    $faultScroll = $faultDetail.SelectSingleNode(".//*[@name='fault-detail-scroll']")
    Check ($null -ne $faultDetail -and $faultDetail.GetAttribute('style').Contains('display: none')) 'Fault detail starts hidden behind the category grid'
    Check ($faultHeaders.Count -eq 4 -and (($faultHeaders | ForEach-Object { $_.GetAttribute('text') }) -join '|') -eq
        '序号|故障点|故障现象|分类') 'Fault detail exposes only the four requested table columns in order'
    Check ($null -ne $faultScroll -and $faultScroll.LocalName -eq 'ScrollView' -and
        $faultScroll.GetAttribute('mode') -eq 'Vertical' -and
        $faultScroll.GetAttribute('vertical-scroller-visibility') -eq 'Auto' -and
        $faultScroll.GetAttribute('horizontal-scroller-visibility') -eq 'Hidden') 'Fault detail shows the vertical scrollbar only on overflow and hides horizontal scrolling'
    foreach ($controlName in @('fault-detail-back-button','fault-detail-title','fault-detail-count','fault-detail-notice','fault-detail-list','fault-simulation-button')) {
        Check ($null -ne $faultDetail.SelectSingleNode(".//*[@name='$controlName']")) "Fault detail contains $controlName"
    }

    $quayFaultCategories = @('电源故障','起升机构','大车机构','小车机构','导板机构','前后倾机构','平面回转机构','左右倾机构','夹轮器','吊具泵','元器件损坏')
    $yardFaultCategories = @('电源故障','起升机构','大车机构','小车机构','导板机构','前后倾机构','平面回转机构','左右倾机构','夹轮器','吊具泵','元器件损坏')
    CheckFaultDataAsset 'Assets/Data/岸桥故障信息.asset' 'Quay Crane' 64 $quayFaultCategories @(2, 4, 5, 5, 5, 3, 3, 3, 3, 1, 30)
    CheckFaultDataAsset 'Assets/Data/场桥故障信息.asset' 'Yard Crane' 64 $yardFaultCategories @(2, 4, 5, 5, 5, 3, 3, 3, 3, 1, 30)

    $learningText = $Xml.SelectSingleNode("//*[@name='student-learning-page']").OuterXml
    Check (-not [regex]::IsMatch($learningText, '岸桥|QUAY BRIDGE|HOOK TILTING')) 'Learning page contains no Quay Crane-only terminology'

    $expected = @(
        'LearningThumbnails/DrillGantry.png',
        'LearningThumbnails/DrillTrolley.png',
        'LearningThumbnails/DrillHoist.png',
        'LearningThumbnails/DrillSpreaderRotation.png',
        'LearningThumbnails/DrillSpreaderZeroing.png'
    )
    $actual = [System.Collections.Generic.List[string]]::new()
    $hashes = [System.Collections.Generic.List[string]]::new()
    Add-Type -AssemblyName System.Drawing
    foreach ($photo in $photos) {
        Check ($photo.GetAttribute('picking-mode') -eq 'Ignore') 'Learning thumbnail ignores pointer picking'
        $relativeUrl = [regex]::Match($photo.GetAttribute('style'), 'url\(\s*[''"](?<path>[^''"]+)[''"]\s*\)').Groups['path'].Value
        $actual.Add($relativeUrl)
        $texturePath = Join-Path (AssetPath 'Assets/YardCraneSimulator/Shell/Resources/YardCraneShell/StudentSide/Display1UI') $relativeUrl
        $matches = @(if (Test-Path -LiteralPath $texturePath -PathType Leaf) { Get-Item -LiteralPath $texturePath })
        Check ($matches.Count -eq 1) "Learning thumbnail relative image resolves once: $relativeUrl"
        if ($matches.Count -ne 1) { continue }
        $relative = $matches[0].FullName.Substring($projectRoot.Length + 1).Replace('\', '/')
        Check ((Guid $relative).Length -eq 32) "Learning thumbnail has a Unity GUID: $relative"
        $image = [System.Drawing.Image]::FromFile($matches[0].FullName)
        try {
            $ratio = [double]$image.Width / [double]$image.Height
            Check ($image.Width -ge 1400 -and $image.Height -ge 900 -and $ratio -ge 1.70 -and $ratio -le 1.82) "Learning thumbnail has high-resolution 16:9 landscape composition: $relative ($($image.Width)x$($image.Height))"
        } finally { $image.Dispose() }
        $hashes.Add((Get-FileHash -LiteralPath $matches[0].FullName -Algorithm SHA256).Hash)
    }
    Check (($actual -join '|') -eq ($expected -join '|')) 'Learning thumbnails use semantic relative image paths in card order'
    Check (($hashes | Select-Object -Unique).Count -eq 5) 'Learning thumbnails are five distinct images'

    # 2026-09-11 real-scale recalibration: values now match the manual's own §5.2 single-item
    # drill numbers where they fit the new reachable range/coordinate convention, and reasoned
    # in-range substitutes elsewhere - see YardCraneStudentSideShell.cs's StartXxxDrill comments
    # for the per-drill reasoning. drill-tilt-options dropped from 3 to 2 choices (the old
    # "左旋1°" was not in the manual); the count check below already only requires >=2.
    # 2026-09-11: the four drill-linked-*-options entries are 联动操作's preset combos - the
    # manual gives no concrete numbers for §5.2.5-8 (unlike the single-item table above), so
    # these are the same reasoned values as YardCraneStudentSideShell.cs's StartLinkedXxxDrill
    # comments, not manual text.
    $expectedOptions = [ordered]@{
        'drill-gantry-options' = @('-50m', '-30m', '30m', '50m')
        'drill-trolley-options' = @('-10m', '5m', '10m')
        'drill-hoist-options' = @('上升5m', '下降5m', '下降10m')
        'drill-tilt-options' = @('左旋3°', '右旋3°')
        'drill-pitch-options' = @('自动回零', '回零后保持')
        'drill-linked-gantry-trolley-options' = @('大车+20m / 小车+5m', '大车-20m / 小车-5m')
        'drill-linked-gantry-hoist-options' = @('大车+20m / 上升5m', '大车-20m / 下降5m')
        'drill-linked-trolley-hoist-options' = @('小车+5m / 上升5m', '小车-5m / 下降5m')
        'drill-linked-gantry-trolley-hoist-options' = @('大车+20m / 小车+5m / 上升5m', '大车-20m / 小车-5m / 下降5m')
    }
    foreach ($groupName in $expectedOptions.Keys) {
        $group = $Xml.SelectSingleNode("//*[@name='$groupName']")
        $toggles = @($group.SelectNodes('.//*[local-name()="Toggle"]'))
        Check ($toggles.Count -ge 2) "$groupName exposes at least two target choices"
        Check (@($toggles | Where-Object { $_.GetAttribute('value') -eq 'true' }).Count -eq 1) "$groupName has exactly one default choice"
        Check ((@($toggles | ForEach-Object { $_.GetAttribute('text') }) -join '|') -eq
            ($expectedOptions[$groupName] -join '|')) "$groupName uses reachable, explicit target semantics"
    }
    # 2026-09-11: 5 single-item + 4 linked + 1 skill = 10.
    Check ($Xml.SelectNodes("//*[starts-with(@name,'drill-') and substring(@name,string-length(@name)-5)='-start']").Count -eq 10) 'Learning page exposes exactly ten start buttons'
}

function CheckScene {
    $scenePath = 'Assets/YardCraneSimulator/Scenes/StudentHome.unity'
    $exists = Test-Path -LiteralPath (AssetPath $scenePath)
    Check $exists 'StudentHome scene exists'
    if (-not $exists) { return }
    $yaml = [System.IO.File]::ReadAllText((AssetPath $scenePath))
    $blocks = @{}
    foreach ($match in [regex]::Matches($yaml, '(?ms)^--- !u!(?<type>\d+) &(?<id>-?\d+)\r?\n(?<body>.*?)(?=^--- !u!|\z)')) {
        $blocks[$match.Groups['id'].Value] = @{ Type = $match.Groups['type'].Value; Body = $match.Groups['body'].Value }
    }
    $sideScriptGuid = Guid 'Assets/YardCraneSimulator/Shell/YardCraneStudentSideShell.cs'
    $sideBlocks = @($blocks.Values | Where-Object { $_.Body.Contains("guid: $sideScriptGuid,") })
    Check ($sideScriptGuid -ne '' -and $sideBlocks.Count -eq 1) 'StudentHome contains exactly one actual StudentSide script reference'
    if ($sideBlocks.Count -ne 1) { return }
    $side = $sideBlocks[0].Body
    $upperObject = [regex]::Match($side, 'm_GameObject: \{fileID: (\d+)\}').Groups[1].Value
    $lowerId = [regex]::Match($side, '(?m)^  lowerDocument: \{fileID: (\d+)\}').Groups[1].Value
    $quayFaultCatalogGuid = Guid 'Assets/Data/岸桥故障信息.asset'
    $yardFaultCatalogGuid = Guid 'Assets/Data/场桥故障信息.asset'
    Check ($side.Contains('YardCraneSceneName: YardCrane')) 'StudentHome serializes the Yard Crane operational scene name'
    Check ($side.Contains('QuayCraneSceneName: "\u6E2F\u53E3\u573A\u666F"') -or
           $side.Contains('QuayCraneSceneName: 港口场景')) 'StudentHome serializes the Quay Crane operational scene name'
    Check ($quayFaultCatalogGuid -ne '' -and $side.Contains("quayFaultCatalogAsset: {fileID: 11400000, guid: $quayFaultCatalogGuid,")) 'StudentHome explicitly references 岸桥故障信息.asset'
    Check ($yardFaultCatalogGuid -ne '' -and $side.Contains("yardFaultCatalogAsset: {fileID: 11400000, guid: $yardFaultCatalogGuid,")) 'StudentHome explicitly references 场桥故障信息.asset'
    $documents = @($blocks.Values | Where-Object { $_.Body.Contains('m_Script: {fileID: 19102,') })
    Check ($documents.Count -eq 2) 'StudentHome contains two actual UIDocument components'
    $upperMatches = @($documents | Where-Object { $_.Body.Contains("m_GameObject: {fileID: $upperObject}") })
    Check ($upperMatches.Count -eq 1) 'StudentSide script and upper UIDocument share the same scene object'
    Check ($lowerId -and $blocks.ContainsKey($lowerId)) 'StudentSide lowerDocument references an existing scene component'
    if ($upperMatches.Count -ne 1 -or -not $blocks.ContainsKey($lowerId)) { return }
    $upper = $upperMatches[0].Body
    $lower = $blocks[$lowerId].Body
    $lowerObject = [regex]::Match($lower, 'm_GameObject: \{fileID: (\d+)\}').Groups[1].Value
    Check ($upperObject -ne $lowerObject -and $lower.Contains('m_Script: {fileID: 19102,')) 'Lower points at an independent UIDocument object'
    foreach ($pair in @(@($upper, 'Display1UI/StudentSideShell', 0), @($lower, 'Display2UI/StudentSideLower', 1))) {
        $body = $pair[0]
        $base = 'Assets/YardCraneSimulator/Shell/Resources/YardCraneShell/StudentSide/' + $pair[1]
        $panelGuid = Guid ($base + 'Panel.asset')
        $treeGuid = Guid ($base + '.uxml')
        Check ($body.Contains("m_PanelSettings: {fileID: 11400000, guid: $panelGuid,")) "Display $($pair[2] + 1) references its actual panel asset"
        Check ([regex]::IsMatch($body, 'sourceAsset: \{fileID: -?\d+, guid: ' + $treeGuid + ',')) "Display $($pair[2] + 1) references its actual UXML asset"
        Check ($body.Contains('m_Enabled: 1')) "Display $($pair[2] + 1) UIDocument enabled in saved scene"
        $panel = [System.IO.File]::ReadAllText((AssetPath ($base + 'Panel.asset')))
        Check ($panel.Contains("m_TargetDisplay: $($pair[2])")) "Display $($pair[2] + 1) panel targets correct display index"
        Check ($panel.Contains('m_ReferenceResolution: {x: 1920, y: 1080}')) "Display $($pair[2] + 1) reference canvas is 1920 x 1080 (stacked pair 1920 x 2160)"
    }
    foreach ($objectId in @($upperObject, $lowerObject)) {
        $transforms = @($blocks.Values | Where-Object { $_.Type -eq '4' -and $_.Body.Contains("m_GameObject: {fileID: $objectId}") })
        Check ($transforms.Count -eq 1 -and $transforms[0].Body.Contains('m_Father: {fileID: 0}')) "UI object $objectId is a separate root (no nested shared panel)"
    }
    $build = [System.IO.File]::ReadAllText((AssetPath 'ProjectSettings/EditorBuildSettings.asset'))
    $homeEnabled = [regex]::IsMatch($build, '- enabled: 1\s+path: Assets/YardCraneSimulator/Scenes/StudentHome\.unity\s+guid: ' + (Guid $scenePath))
    Check $homeEnabled 'StudentHome is enabled in Build Settings with matching scene GUID'
    $enabledScenes = [regex]::Matches($build, '- enabled: 1\s+path: ([^\r\n]+)')
    Check ($enabledScenes.Count -gt 0 -and $enabledScenes[0].Groups[1].Value -eq 'Assets/YardCraneSimulator/Scenes/StudentEntry.unity') 'StudentEntry remains the first enabled scene'
    Record "SOURCE_SHA256 $scenePath $((Get-FileHash -LiteralPath (AssetPath $scenePath)).Hash)"
}

function CompileShell {
    $data = Join-Path $UnityEditorRoot 'Data'
    $compiler = Join-Path $data 'DotNetSdkRoslyn/csc.dll'
    $dotnet = Join-Path $data 'NetCoreRuntime/dotnet.exe'
    $inputAssembly = AssetPath 'Library/ScriptAssemblies/Unity.InputSystem.dll'
    $runtimeAssembly = AssetPath 'Library/ScriptAssemblies/YardCraneSimulator.Runtime.dll'
    foreach ($path in @($compiler, $dotnet, $inputAssembly, $runtimeAssembly)) {
        Check (Test-Path -LiteralPath $path) "real compilation dependency available: $path"
        if (-not (Test-Path -LiteralPath $path)) { return }
    }
    $references = @(
        Get-ChildItem -LiteralPath (Join-Path $data 'NetStandard/ref/2.1.0') -Filter '*.dll'
        Get-ChildItem -LiteralPath (Join-Path $data 'NetStandard/compat/2.1.0/shims/netfx') -Filter '*.dll'
        Get-ChildItem -LiteralPath (Join-Path $data 'Managed/UnityEngine') -Filter '*.dll'
        Get-Item -LiteralPath (Join-Path $data 'Managed/UnityEngine.dll')
        Get-Item -LiteralPath (Join-Path $data 'Managed/UnityEditor.dll')
        Get-Item -LiteralPath $inputAssembly
        Get-Item -LiteralPath $runtimeAssembly
    ) | Sort-Object FullName -Unique
    $sources = @(
        # The generated Runtime project/assembly may lag one Unity import behind a newly added
        # source file. Compile the current question-bank source with the focused contract build
        # so this harness validates the working tree rather than a stale ScriptAssemblies DLL.
        Get-Item -LiteralPath (AssetPath 'Assets/YardCraneSimulator/Runtime/Training/YardCraneAssessmentQuestionBank.cs')
        Get-ChildItem -LiteralPath (AssetPath 'Assets/YardCraneSimulator/Shell') -Filter '*.cs' -Recurse
        Get-Item -LiteralPath (AssetPath 'Assets/YardCraneSimulator/Editor/YardCraneStudentShellEditor.cs')
        Get-Item -LiteralPath (AssetPath 'Assets/YardCraneSimulator/Editor/YardCraneStudentSideShellEditor.cs')
    )
    $response = [System.Collections.Generic.List[string]]::new()
    foreach ($option in @('/nologo', '/noconfig', '/nostdlib+', '/target:library', '/langversion:9.0', '/utf8output',
        '/define:UNITY_EDITOR;UNITY_EDITOR_WIN;UNITY_STANDALONE_WIN;UNITY_6000_0;UNITY_6000_0_OR_NEWER;ENABLE_INPUT_SYSTEM;Mode_Changqiao')) { $response.Add($option) }
    $response.Add('/out:"' + (Join-Path $outputRoot 'StudentHome.UiContractVerification.dll') + '"')
    foreach ($reference in $references) { $response.Add('/reference:"' + $reference.FullName + '"') }
    foreach ($source in $sources) {
        $response.Add('"' + $source.FullName + '"')
        Record "COMPILE_SOURCE $($source.FullName) sha256=$((Get-FileHash -LiteralPath $source.FullName).Hash)"
    }
    $responsePath = Join-Path $outputRoot 'Compile.rsp'
    [System.IO.File]::WriteAllLines($responsePath, $response, [System.Text.UTF8Encoding]::new($false))
    Record "COMPILER $compiler"
    Record "REFERENCE_COUNT $($references.Count); SOURCE_COUNT $($sources.Count); no Unity stubs; Unity Editor not launched"
    $compilerOutput = @(& $dotnet $compiler "@$responsePath" 2>&1)
    $exitCode = $LASTEXITCODE
    [System.IO.File]::WriteAllLines((Join-Path $outputRoot 'Compile.log'), [string[]]$compilerOutput, [System.Text.UTF8Encoding]::new($false))
    foreach ($line in $compilerOutput) { Record ([string]$line) }
    Check ($exitCode -eq 0) "Unity 6000.0.42f1 Roslyn compiler accepts actual shell + editor C# for Mode_Changqiao (exit=$exitCode)"

    $quayResponse = @($response | ForEach-Object {
        $modeLine = $_.Replace('Mode_Changqiao', 'Mode_Anqiao')
        $modeLine.Replace('StudentHome.UiContractVerification.dll', 'StudentHome.UiContractVerification.Mode_Anqiao.dll')
    })
    $quayResponsePath = Join-Path $outputRoot 'Compile.Mode_Anqiao.rsp'
    [System.IO.File]::WriteAllLines($quayResponsePath, [string[]]$quayResponse, [System.Text.UTF8Encoding]::new($false))
    $quayCompilerOutput = @(& $dotnet $compiler "@$quayResponsePath" 2>&1)
    $quayExitCode = $LASTEXITCODE
    [System.IO.File]::WriteAllLines((Join-Path $outputRoot 'Compile.Mode_Anqiao.log'), [string[]]$quayCompilerOutput, [System.Text.UTF8Encoding]::new($false))
    foreach ($line in $quayCompilerOutput) { Record ([string]$line) }
    Check ($quayExitCode -eq 0) "Unity 6000.0.42f1 Roslyn compiler accepts actual shell + editor C# for Mode_Anqiao (exit=$quayExitCode)"
}

try {
    Record "UTC $([DateTime]::UtcNow.ToString('O'))"
    Record "PROJECT $projectRoot"
    Check (([System.IO.File]::ReadAllText((AssetPath 'ProjectSettings/ProjectVersion.txt'))).Contains('m_EditorVersion: 6000.0.42f1')) 'project remains Unity 6000.0.42f1'
    if (-not $SkipCompile) { CompileShell } else { Warn 'Compilation explicitly skipped; only static resource checks executed.' }
    $base = 'Assets/YardCraneSimulator/Shell/Resources/YardCraneShell/'
    # Capture informational check output separately; functions return only XML objects here.
    $upperResult = @(ReadUxml ($base + 'Display1UI/StudentShellUpper.uxml'))
    $sideResult = @(ReadUxml ($base + 'StudentSide/Display1UI/StudentSideShell.uxml'))
    $lowerResult = @(ReadUxml ($base + 'StudentSide/Display2UI/StudentSideLower.uxml'))
    $upperXml = $upperResult | Where-Object { $_ -is [xml] }
    $sideXml = $sideResult | Where-Object { $_ -is [xml] }
    $lowerXml = $lowerResult | Where-Object { $_ -is [xml] }
    $upperResult + $sideResult + $lowerResult | Where-Object { $_ -is [string] } | Write-Output
    CheckQueryContract 'Assets/YardCraneSimulator/Shell/YardCraneStudentShell.cs' $upperXml
    CheckQueryContract 'Assets/YardCraneSimulator/Shell/YardCraneStudentSideShell.cs' $sideXml
    $entrySource = [System.IO.File]::ReadAllText((AssetPath 'Assets/YardCraneSimulator/Shell/YardCraneStudentShell.cs'))
    Check ($entrySource.Contains('#if Mode_Anqiao') -and
           $entrySource.Contains('#elif Mode_Changqiao') -and
           $entrySource.Contains('Mode_Anqiao && Mode_Changqiao') -and
           $entrySource.Contains('ConfiguredCraneKind = YardCraneCraneKind.QuayCrane') -and
           $entrySource.Contains('ConfiguredCraneKind = YardCraneCraneKind.YardCrane')) 'Build Profile defines select the shore or yard crane kind and reject an ambiguous profile'
    Check (-not $entrySource.Contains('public bool IsQuayCrane')) 'Login shell no longer exposes the manual IsQuayCrane Inspector checkbox'
    $entryScene = [System.IO.File]::ReadAllText((AssetPath 'Assets/YardCraneSimulator/Scenes/StudentEntry.unity'))
    Check (-not $entryScene.Contains('IsQuayCrane:')) 'StudentEntry no longer serializes the obsolete manual crane checkbox'
    $studentSideSource = [System.IO.File]::ReadAllText((AssetPath 'Assets/YardCraneSimulator/Shell/YardCraneStudentSideShell.cs'))
    $legacyTaskBridgeSource = [System.IO.File]::ReadAllText((AssetPath 'Assets/YardCraneSimulator/Shell/YardCraneLegacyTaskAssetBridge.cs'))
    $navigationBridgeSource = [System.IO.File]::ReadAllText((AssetPath 'Assets/YardCraneSimulator/Shell/YardCraneLocalTrainingNavigationBridge.cs'))
    $trainingSessionSource = [System.IO.File]::ReadAllText((AssetPath 'Assets/YardCraneSimulator/Runtime/Training/YardCraneLocalTrainingSession.cs'))
    $trainingCoordinatorSource = [System.IO.File]::ReadAllText((AssetPath 'Assets/YardCraneSimulator/Runtime/Training/YardCraneLocalTrainingModeCoordinator.cs'))
    $assessmentQuestionBankPath = 'Assets/YardCraneSimulator/Runtime/Training/YardCraneAssessmentQuestionBank.cs'
    Check (Test-Path -LiteralPath (AssetPath $assessmentQuestionBankPath) -PathType Leaf) 'Multi-question assessment bank runtime source exists'
    Check ((Guid $assessmentQuestionBankPath).Length -eq 32) 'Multi-question assessment bank runtime source has a Unity meta GUID'
    $assessmentQuestionBankSource = [System.IO.File]::ReadAllText((AssetPath $assessmentQuestionBankPath))
    Check ($studentSideSource.Contains('CreateFreePractice(CreateParticipant())') -and
           $studentSideSource.Contains('CreateGuidedLearning(') -and
           $studentSideSource.Contains('CreateAssessment(')) 'StudentHome implements all three explicit local training context factories'
    Check ($studentSideSource.Contains('YardCraneLocalTrainingNavigationBridge.Begin(profile, context.SessionId)') -and
           $studentSideSource.Contains('YardCraneLocalTrainingNavigationBridge.TryAbortLaunch(context.SessionId)') -and
           $navigationBridgeSource.Contains('public bool IsBound => hud != null;') -and
           -not $navigationBridgeSource.Contains('if (owner == null || owner.LaunchOptions == null) return;')) 'StudentHome direct scene routing keeps a return bridge without requiring Bootstrap'
    Check ($studentSideSource.Contains('returnedTrainingSessionId = returnedContext.SessionId;') -and
           $studentSideSource.Contains('profile ??= YardCraneFallbackSession.Student;') -and
           $studentSideSource.Contains('FinalizeReturnedTrainingSession();') -and
           $studentSideSource.Contains('YardCraneFallbackSession.TryClear(sessionId)') -and
           $navigationBridgeSource.Contains('TryReleaseAtStudentHome(System.Guid expectedSessionId)')) 'StudentHome finalizes a returned training session before accepting another launch'
    Check ($legacyTaskBridgeSource.Contains('taskType = taskType.Trim();') -and
           $legacyTaskBridgeSource.Contains('if (!SupportedCategories.Contains(taskType)) continue;')) 'Student task catalogue ignores task types outside the five learning/free-practice categories'
    Check ($studentSideSource.Contains('faultSimulationButton.clicked += StartFaultSimulation;') -and
           $studentSideSource.Contains('TryCreateFaultTrainingAssignment(') -and
           $studentSideSource.Contains('var trainingFaults = category.faults ??') -and
           $studentSideSource.Contains('faultId = fault.number') -and
           $studentSideSource.Contains('faultAssignment: assignment') -and
           $studentSideSource.Contains('faultTraining: true') -and
           $studentSideSource.Contains('isPublished = false') -and
           -not $studentSideSource.Contains('FaultsForPublishedAssignment(') -and
           $studentSideSource.Contains('TryWriteFaultTrainingTaskState(')) 'Learning fault simulation ignores teacher publications, writes the complete selected-category IDs, and launches the configured scene'
    Check ($studentSideSource.Contains('YardCraneLocalTrainingMode.GuidedLearning') -and
           $studentSideSource.Contains('YardCraneLocalTrainingExercise.None') -and
           $studentSideSource.Contains('YardCraneLocalTrainingReturnPage.LearningSelection') -and
           $trainingSessionSource.Contains('var isOpenLearning = exercise == YardCraneLocalTrainingExercise.None') -and
           $trainingCoordinatorSource.Contains('!context.Target.HasTarget') -and
           $trainingCoordinatorSource.Contains('? "故障训练"')) 'Fault simulation uses an open learning session and returns to learning selection instead of free-practice semantics'
    Check ($studentSideSource.Contains('var taskStatePayloadCount =') -and
           $studentSideSource.Contains('if (taskStatePayloadCount != 1') -and
           $studentSideSource.Contains('[YC_STUDENT_TASK_STATE_PAYLOAD_INVALID]') -and
           $studentSideSource.Contains('taskSelection: selection') -and
           $studentSideSource.Contains('assessmentTrainingQuestion: assessmentTrainingQuestion') -and
           $studentSideSource.Contains('faultAssignment: faultAssignment')) 'Every operational-scene handoff requires exactly one task-state payload'
    Check (-not $studentSideSource.Contains('ShowDrillNotReady') -and
           -not $studentSideSource.Contains('任务考核尚未接入')) 'Learning starts and local assessment are no longer placeholder callbacks'
    Check ($studentSideSource.Contains('selectedKind == YardCraneCraneKind.QuayCrane') -and
           $studentSideSource.Contains('? quayFaultCatalogAsset') -and
           $studentSideSource.Contains(': yardFaultCatalogAsset') -and
           -not $studentSideSource.Contains('Resources.Load<TextAsset>(FaultCatalogResourcePath)')) 'Student fault catalog follows the Build Profile-selected crane kind and does not depend on JSON Resources loading'
    Check ($studentSideSource.Contains('? QuayCraneSceneName') -and
           $studentSideSource.Contains(': YardCraneSceneName') -and
           $studentSideSource.Contains('SceneManager.LoadSceneAsync(') -and
           $studentSideSource.Contains('operationalSceneName,') -and
           -not $studentSideSource.Contains('YardCraneBootstrap.SetOperationalSceneForNextLaunch') -and
           -not $studentSideSource.Contains('YardCraneStudentShell.BootstrapSceneName')) 'All three training modes load the crane-specific scene directly from StudentHome without Bootstrap routing'
    Check ($studentSideSource.Contains('YardCraneAssessmentQuestionBankStore.TryLoadCurrentPaper') -and
           $studentSideSource.Contains('foreach (var question in questions)') -and
           $studentSideSource.Contains('StartAssessmentQuestion(selectedQuestion)')) 'Student assessment entry loads and renders every question in the current paper'
    Check ($studentSideSource.Contains('TryWriteAssessmentTrainingTaskState(') -and
           $studentSideSource.Contains('TryWriteFaultTaskState(') -and
           $studentSideSource.Contains('question.questionId') -and
           -not $studentSideSource.Contains('copyPublishedPaper: true') -and
           -not $studentSideSource.Contains('paperStateAsset')) 'Selecting one assessment question writes only that question directly to task state after launch preflight'
    Check ($assessmentQuestionBankSource.Contains('public List<YardCraneAssessmentQuestion> items = new();') -and
           $assessmentQuestionBankSource.Contains('public const string TrainingKind = "training";') -and
           $assessmentQuestionBankSource.Contains('public const string FaultKind = "fault";')) 'Assessment paper JSON supports multiple training and fault questions'
    $assessmentMetaMethod = [regex]::Match(
        $studentSideSource,
        '(?s)private static string AssessmentQuestionMeta\(.*?(?=\r?\n\s*private static string JoinAssessmentText)')
    Check ($assessmentMetaMethod.Success -and
           $assessmentMetaMethod.Value.Contains('YardCraneAssessmentQuestionBankStore.FaultItems(question)') -and
           $assessmentMetaMethod.Value.Contains('faults.Count') -and
           -not $assessmentMetaMethod.Value.Contains('faultPoint') -and
           -not $assessmentMetaMethod.Value.Contains('lineTag')) 'Student question-list metadata exposes only the grouped-fault count, never fault-point or line-tag answers'
    Check ($assessmentQuestionBankSource.Contains('snapshot.fault.faultPoint = string.Empty;') -and
           $assessmentQuestionBankSource.Contains('snapshot.fault.lineTag = string.Empty;') -and
           $assessmentQuestionBankSource.Contains('foreach (var fault in snapshot.faults)') -and
           $assessmentQuestionBankSource.Contains('fault.faultPoint = string.Empty;') -and
           $assessmentQuestionBankSource.Contains('fault.lineTag = string.Empty;') -and
           $assessmentQuestionBankSource.Contains('LegacyPaperFormatVersion') -and
           $assessmentQuestionBankSource.Contains('TryUpgradeLegacyPaper(') -and
           $assessmentQuestionBankSource.Contains('包含仅教师可见的答案字段')) 'Student-facing paper JSON strips every grouped-fault answer and safely upgrades legacy paper snapshots'
    CheckLearningPage $sideXml
    foreach ($name in @('learning-mode-button', 'launch-button', 'assessment-mode-button', 'student-learning-page')) {
        Check ($null -ne $sideXml.SelectSingleNode("//*[@name='$name']")) "StudentSide owns $name"
        Check ($null -eq $upperXml.SelectSingleNode("//*[@name='$name']")) "Login shell does not duplicate $name"
    }
    $assessmentPage = $sideXml.SelectSingleNode("//*[@name='student-assessment-page']")
    Check ($null -ne $assessmentPage) 'StudentSide owns the dedicated assessment-question page'
    foreach ($name in @(
        'assessment-back-button',
        'assessment-paper-title',
        'assessment-paper-count',
        'assessment-paper-status',
        'assessment-question-list')) {
        Check ($null -ne $sideXml.SelectSingleNode("//*[@name='$name']")) "Student assessment page exposes $name"
        Check ($null -eq $upperXml.SelectSingleNode("//*[@name='$name']")) "Login shell does not duplicate $name"
    }
    Check ($null -ne $assessmentPage -and
           -not $assessmentPage.OuterXml.Contains('故障点') -and
           -not $assessmentPage.OuterXml.Contains('线号') -and
           -not $assessmentPage.OuterXml.Contains('标号')) 'Student assessment-question page contains no fault-answer labels'
    foreach ($name in @('login-page', 'teacher-home-page')) {
        Check ($null -eq $sideXml.SelectSingleNode("//*[@name='$name']")) "StudentSide contains no login/teacher page $name"
    }
    Check ($lowerXml.SelectNodes('//*[local-name()="Button" or local-name()="TextField"]').Count -eq 0) 'Display2 is a noninteractive companion display'
    foreach ($style in @('Display1UI/StudentShellUpperStyles.uss', 'StudentSide/Display1UI/StudentSideShellStyles.uss', 'StudentSide/Display2UI/StudentSideLowerStyles.uss')) {
        CheckStyle ($base + $style)
    }
    CheckScene
    Warn 'This is real Unity-reference C# compilation plus static asset/scene validation. It does not execute Unity, import USS through Unity, render UI, or verify clicks/scene transitions.'
    Warn 'Static checks cannot prove visual device identity or replace the Unity StudentHome Play verification and screenshot review.'
} catch {
    $failures.Add($_.Exception.Message)
    Record "FAIL harness exception: $($_.Exception.Message)"
    Record $_.ScriptStackTrace
} finally {
    $result = if ($failures.Count -eq 0) { 'PASS' } else { 'FAIL' }
    Record "FINAL $result checks=$checks failures=$($failures.Count) warnings=$($warnings.Count)"
    [System.IO.File]::WriteAllLines((Join-Path $outputRoot 'StudentHomeUiChecks.result.txt'), $report, [System.Text.UTF8Encoding]::new($false))
}
if ($failures.Count -gt 0) { exit 1 }
exit 0
