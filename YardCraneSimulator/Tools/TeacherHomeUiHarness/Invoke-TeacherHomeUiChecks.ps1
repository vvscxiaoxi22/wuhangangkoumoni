[CmdletBinding()]
param(
    [string]$ProjectPath = ''
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Join-Path $PSScriptRoot '../..'
}

$projectRoot = (Resolve-Path -LiteralPath $ProjectPath).Path
$artifactRoot = Join-Path $PSScriptRoot 'artifacts'
[void][System.IO.Directory]::CreateDirectory($artifactRoot)
$resultPath = Join-Path $artifactRoot 'TeacherHomeUiChecks.result.txt'
$report = [System.Collections.Generic.List[string]]::new()
$failures = [System.Collections.Generic.List[string]]::new()
$checkCount = 0

function Record([string]$Text) {
    $report.Add($Text)
    [Console]::WriteLine($Text)
}

function Check([bool]$Condition, [string]$Description) {
    $script:checkCount++
    if ($Condition) {
        Record "PASS $Description"
        return
    }

    $failures.Add($Description)
    Record "FAIL $Description"
}

function AssetPath([string]$RelativePath) {
    return Join-Path $projectRoot $RelativePath
}

function ReadAsset([string]$RelativePath) {
    return [System.IO.File]::ReadAllText((AssetPath $RelativePath))
}

function AssetGuid([string]$RelativePath) {
    $metaPath = AssetPath ($RelativePath + '.meta')
    if (-not (Test-Path -LiteralPath $metaPath -PathType Leaf)) {
        return ''
    }

    $match = [regex]::Match(
        [System.IO.File]::ReadAllText($metaPath),
        '(?m)^guid:\s*(?<guid>[0-9a-f]{32})\s*$')
    return $match.Groups['guid'].Value
}

function ParseUxml([string]$RelativePath) {
    $xml = [xml]::new()
    $xml.Load((AssetPath $RelativePath))
    return $xml
}

function UnityBlocks([string]$Yaml) {
    foreach ($match in [regex]::Matches(
        $Yaml,
        '(?ms)^--- !u!(?<type>\d+) &(?<id>-?\d+)\r?\n(?<body>.*?)(?=^--- !u!|\z)')) {
        [pscustomobject]@{
            Type = $match.Groups['type'].Value
            Id = $match.Groups['id'].Value
            Body = $match.Groups['body'].Value
        }
    }
}

function GameObjectId([string]$Body) {
    return [regex]::Match($Body, '(?m)^  m_GameObject: \{fileID: (?<id>-?\d+)\}').Groups['id'].Value
}

function CheckMetaAsset([string]$RelativePath) {
    Check (Test-Path -LiteralPath (AssetPath $RelativePath) -PathType Leaf) "$RelativePath exists"
    Check (Test-Path -LiteralPath (AssetPath ($RelativePath + '.meta')) -PathType Leaf) "$RelativePath has a meta file"
    $guid = AssetGuid $RelativePath
    Check ($guid.Length -eq 32) "$RelativePath has one valid 32-character Unity GUID"
    return $guid
}

function CheckStyleSheet([string]$RelativePath) {
    $style = ReadAsset $RelativePath
    $withoutComments = [regex]::Replace(
        $style,
        '/\*.*?\*/',
        '',
        [System.Text.RegularExpressions.RegexOptions]::Singleline)
    Check (([regex]::Matches($withoutComments, '\{')).Count -eq
        ([regex]::Matches($withoutComments, '\}')).Count) "$RelativePath has balanced rule braces"
    Check (-not [regex]::IsMatch(
        $withoutComments,
        ':(last-child|first-child|nth-child|nth-of-type)\b')) "$RelativePath avoids unsupported structural pseudo-classes"
}

function ParseFaultAsset([string]$RelativePath) {
    $text = ReadAsset $RelativePath
    $rows = [System.Collections.Generic.List[object]]::new()
    foreach ($entry in [regex]::Matches(
        $text,
        '(?m)^    - Key: (?<id>[0-9]+)\r?\n      Value:\r?\n(?<body>(?:        [^\r\n]*\r?\n){4})')) {
        $fields = @{}
        foreach ($field in [regex]::Matches(
            $entry.Groups['body'].Value,
            '(?m)^        (?<key>"(?:\\.|[^"])*"): (?<value>"(?:\\.|[^"])*")\s*$')) {
            $key = ConvertFrom-Json -InputObject $field.Groups['key'].Value
            $value = ConvertFrom-Json -InputObject $field.Groups['value'].Value
            $fields[$key] = $value
        }
        $rows.Add([pscustomobject]@{
            Id = [int]$entry.Groups['id'].Value
            Phenomenon = $fields['故障现象']
            FaultPoint = $fields['故障点']
            LineTag = $fields['标号']
            Category = $fields['类型']
        })
    }
    return $rows.ToArray()
}

function CheckUxmlStyleReference([string]$UxmlPath, [xml]$Xml, [string]$ExpectedStylePath) {
    $styles = @($Xml.SelectNodes('//*[local-name()="Style"]'))
    Check ($styles.Count -eq 1) "$UxmlPath declares exactly one stylesheet"
    if ($styles.Count -ne 1) {
        return
    }

    $source = $styles[0].GetAttribute('src')
    if ($source.StartsWith('project://database/', [System.StringComparison]::Ordinal)) {
        $expectedPrefix = 'project://database/' + $ExpectedStylePath + '?'
        $referencedGuid = [regex]::Match($source, '[?&]guid=(?<guid>[0-9a-f]{32})').Groups['guid'].Value
        Check ($source.StartsWith($expectedPrefix, [System.StringComparison]::Ordinal)) "$UxmlPath references its own TeacherSide stylesheet path"
        Check ($referencedGuid -eq (AssetGuid $ExpectedStylePath)) "$UxmlPath stylesheet GUID resolves to the stylesheet meta"
    } else {
        $resolvedPath = [System.IO.Path]::GetFullPath((Join-Path (Split-Path (AssetPath $UxmlPath)) $source))
        $expectedPath = [System.IO.Path]::GetFullPath((AssetPath $ExpectedStylePath))
        Check ($resolvedPath -eq $expectedPath) "$UxmlPath relative reference resolves to its own TeacherSide stylesheet"
        Check ((Test-Path -LiteralPath $resolvedPath -PathType Leaf) -and (AssetGuid $ExpectedStylePath).Length -eq 32) "$UxmlPath relative stylesheet exists with valid Unity metadata"
    }
    $css = [System.IO.File]::ReadAllText((AssetPath $ExpectedStylePath))
    foreach ($match in [regex]::Matches($css, 'url\(\s*[''"](?<path>[^''"]+)[''"]\s*\)')) {
        $relativeUrl = $match.Groups['path'].Value
        $target = Join-Path (Split-Path (AssetPath $ExpectedStylePath)) $relativeUrl
        Check (Test-Path -LiteralPath $target -PathType Leaf) "$ExpectedStylePath relative image exists: $relativeUrl"
        Check (Test-Path -LiteralPath ($target + '.meta') -PathType Leaf) "$ExpectedStylePath relative image has Unity metadata: $relativeUrl"
    }
}

function CheckUniqueNames([string]$UxmlPath, [xml]$Xml) {
    $names = @($Xml.SelectNodes('//*[@name]') | ForEach-Object { $_.GetAttribute('name') })
    $duplicates = @($names | Group-Object | Where-Object { $_.Count -gt 1 })
    Check ($duplicates.Count -eq 0) "$UxmlPath has unique named elements"
}

try {
    Record "UTC $([DateTime]::UtcNow.ToString('O'))"
    Record "PROJECT $projectRoot"
    Record 'SCOPE static TeacherHome scene/UI contract only; Unity Editor was not launched'

    $scenePath = 'Assets/YardCraneSimulator/Scenes/TeacherHome.unity'
    $teacherScriptPath = 'Assets/YardCraneSimulator/Shell/YardCraneTeacherSideShell.cs'
    $entryScriptPath = 'Assets/YardCraneSimulator/Shell/YardCraneStudentShell.cs'
    $studentSideScriptPath = 'Assets/YardCraneSimulator/Shell/YardCraneStudentSideShell.cs'
    $assessmentQuestionBankPath = 'Assets/YardCraneSimulator/Runtime/Training/YardCraneAssessmentQuestionBank.cs'
    $entryUxmlPath = 'Assets/YardCraneSimulator/Shell/Resources/YardCraneShell/Display1UI/StudentShellUpper.uxml'
    $faultCatalogPath = 'Assets/YardCraneSimulator/Shell/Resources/YardCraneShell/StudentSide/Display1UI/StudentFaultCatalog.json'
    $faultAssetScriptPath = 'Assets/Data/Creat/故障信息.cs'
    $quayFaultAssetPath = 'Assets/Data/岸桥故障信息.asset'
    $yardFaultAssetPath = 'Assets/Data/场桥故障信息.asset'

    $upperUxmlPath = 'Assets/YardCraneSimulator/Shell/Resources/TeacherSide/Display1UI/TeacherSideShell.uxml'
    $upperStylePath = 'Assets/YardCraneSimulator/Shell/Resources/TeacherSide/Display1UI/TeacherSideShellStyles.uss'
    $upperPanelPath = 'Assets/YardCraneSimulator/Shell/Resources/TeacherSide/Display1UI/TeacherSideShellPanel.asset'
    $lowerUxmlPath = 'Assets/YardCraneSimulator/Shell/Resources/TeacherSide/Display2UI/TeacherSideLower.uxml'
    $lowerStylePath = 'Assets/YardCraneSimulator/Shell/Resources/TeacherSide/Display2UI/TeacherSideLowerStyles.uss'
    $lowerPanelPath = 'Assets/YardCraneSimulator/Shell/Resources/TeacherSide/Display2UI/TeacherSideLowerPanel.asset'

    Check (Test-Path -LiteralPath (AssetPath $scenePath) -PathType Leaf) 'TeacherHome scene exists'
    Check (Test-Path -LiteralPath (AssetPath ($scenePath + '.meta')) -PathType Leaf) 'TeacherHome scene has a meta file'
    $sceneGuid = AssetGuid $scenePath
    Check ($sceneGuid.Length -eq 32) 'TeacherHome scene has a valid Unity GUID'

    $teacherGuids = [ordered]@{}
    foreach ($path in @(
        $upperUxmlPath,
        $upperStylePath,
        $upperPanelPath,
        $lowerUxmlPath,
        $lowerStylePath,
        $lowerPanelPath,
        $teacherScriptPath)) {
        $teacherGuids[$path] = CheckMetaAsset $path
    }
    Check ((@($teacherGuids.Values | Where-Object { $_ }) | Select-Object -Unique).Count -eq $teacherGuids.Count) 'Teacher Display1/Display2 UXML, USS, Panel and shell use independent GUIDs'
    [void](CheckMetaAsset $assessmentQuestionBankPath)

    $upperXml = ParseUxml $upperUxmlPath
    $lowerXml = ParseUxml $lowerUxmlPath
    CheckUniqueNames $upperUxmlPath $upperXml
    CheckUniqueNames $lowerUxmlPath $lowerXml
    CheckUxmlStyleReference $upperUxmlPath $upperXml $upperStylePath
    CheckUxmlStyleReference $lowerUxmlPath $lowerXml $lowerStylePath
    CheckStyleSheet $upperStylePath
    CheckStyleSheet $lowerStylePath
    Check ($null -ne $upperXml.SelectSingleNode("//*[@name='teacher-side-shell']")) 'Teacher Display1 UXML exposes its dedicated root'
    foreach ($name in @('teacher-assessment-page', 'teacher-assessment-detail-overlay', 'teacher-assessment-calendar-overlay')) {
        $assessmentElement = $upperXml.SelectSingleNode("//*[@name='$name']")
        Check ($null -ne $assessmentElement -and
               $assessmentElement.ParentNode.GetAttribute('name') -eq 'teacher-side-shell') "$name is independent of hidden task-management pages"
    }
    $calendarOverlay = $upperXml.SelectSingleNode("//*[@name='teacher-assessment-calendar-overlay']")
    Check ($calendarOverlay.GetAttribute('class').Contains('teacher-page-hidden')) 'Calendar starts hidden until the date button is clicked'
    Check ($calendarOverlay.NextSibling -eq $null) 'Calendar is the last shell child so page content cannot cover its popup'
    $calendarPicker = $calendarOverlay.SelectSingleNode(".//*[@name='teacher-assessment-calendar-picker']")
    Check ($null -ne $calendarPicker -and $calendarPicker.LocalName -eq 'DatePicker' -and
           $calendarPicker.NamespaceURI -eq 'OMG.UI.DatePickerUITK') 'Teacher statistics imports the real calendar control through its runtime UXML namespace'
    foreach ($name in @('scrim', 'close', 'all', 'today', 'cancel')) {
        Check ($null -ne $calendarOverlay.SelectSingleNode(".//*[@name='teacher-assessment-calendar-$name']")) "Calendar provides its $name control inside the overlay"
    }
    $calendarVendorPath = 'Assets/YardCraneSimulator/Shell/ThirdParty/UIToolkitDatePicker/'
    $calendarGuids = @('DatePicker.cs', 'GridDayInfo.cs', 'MonthLabelFormat.cs', 'LICENSE.txt', 'README.md') |
        ForEach-Object { CheckMetaAsset ($calendarVendorPath + $_) }
    Check ((@($calendarGuids | Where-Object { $_ } | Select-Object -Unique)).Count -eq 5) 'Calendar vendor files have independent Unity GUIDs'
    Check ((ReadAsset ($calendarVendorPath + 'LICENSE.txt')).Contains('MIT License')) 'Calendar distribution retains its upstream MIT license'
    Check ($null -ne $lowerXml.SelectSingleNode("//*[@name='teacher-side-lower']")) 'Teacher Display2 UXML exposes its dedicated root'
    Check ($null -eq $upperXml.SelectSingleNode("//*[@name='teacher-fault-publish-management-button']")) 'Teacher home no longer exposes a standalone fault-publishing card (entry moved into task management''s 故障排除 category)'
    Check ($null -ne $upperXml.SelectSingleNode("//*[@name='teacher-fault-publish-page']")) 'Teacher Display1 UXML exposes the fault-publishing page'
    Check ($null -ne $upperXml.SelectSingleNode("//*[@name='teacher-fault-publish-back-button']")) 'Fault-publishing page exposes a back button'
    $faultAddButton = $upperXml.SelectSingleNode("//*[@name='teacher-fault-publish-button']")
    Check ($null -ne $faultAddButton -and $faultAddButton.GetAttribute('text').Contains('添加所选故障')) 'Fault selection adds the selected fault group to the shared question list'
    $faultPublishPage = $upperXml.SelectSingleNode("//*[@name='teacher-fault-publish-page']")
    Check ($null -eq $faultPublishPage.SelectSingleNode(".//*[@text='发布范围']") -and
           $null -eq $faultPublishPage.SelectSingleNode(".//*[@text='指定学生']")) 'Fault publishing hides the publication-scope controls'
    Check ($null -eq $faultPublishPage.SelectSingleNode(".//*[@text='训练入口']") -and
           $null -eq $faultPublishPage.SelectSingleNode(".//*[@text='学习训练']")) 'Fault publishing hides the training-entry controls'
    $faultDefaultNotice = $faultPublishPage.SelectSingleNode(".//*[contains(@text, '默认发布给全体学生') and contains(@text, '任务考核')]")
    Check ($null -ne $faultDefaultNotice) 'Fault publishing states the fixed all-students task-assessment destination'
    Check ($null -eq $upperXml.SelectSingleNode("//*[@name='teacher-fault-source-quay']")) 'Fault-publishing page has no manual quay-crane catalog button'
    Check ($null -eq $upperXml.SelectSingleNode("//*[@name='teacher-fault-source-yard']")) 'Fault-publishing page has no manual yard-crane catalog button'
    Check ($null -ne $upperXml.SelectSingleNode("//*[@name='teacher-fault-category-list']")) 'Fault-publishing page exposes the runtime category-list container'
    $teacherCoreCards = @($upperXml.SelectNodes("//*[contains(concat(' ', normalize-space(@class), ' '), ' teacher-core-card ')]"))
    Check ($teacherCoreCards.Count -eq 2) 'Teacher home contains two management cards'
    $taskListScroll = $upperXml.SelectSingleNode("//*[@name='teacher-task-list-scroll']")
    Check ($null -ne $taskListScroll -and $taskListScroll.LocalName -eq 'ScrollView') 'Task management exposes a dynamic scroll container for question rows'
    Check ($null -ne $taskListScroll -and $taskListScroll.ChildNodes.Count -eq 0) 'Task management no longer hard-codes fixed task rows in UXML'
    $faultTaskDetailOverlay = $upperXml.SelectSingleNode("//*[@name='teacher-fault-task-detail-overlay']")
    $faultTaskDetailList = $upperXml.SelectSingleNode("//*[@name='teacher-fault-task-detail-list']")
    Check ($null -ne $faultTaskDetailOverlay) 'Task management exposes a grouped-fault detail overlay'
    Check ($null -ne $upperXml.SelectSingleNode("//*[@name='teacher-fault-task-detail-close']")) 'Grouped-fault detail overlay exposes a close button'
    Check ($null -ne $faultTaskDetailList -and $faultTaskDetailList.LocalName -eq 'ScrollView' -and
           $faultTaskDetailList.GetAttribute('mode') -eq 'Vertical') 'Grouped-fault detail overlay exposes a vertical fault-list scroller'
    Check ($null -ne $upperXml.SelectSingleNode("//*[@name='teacher-task-editor-selection-count']")) 'Task editor exposes its single-selection state'
    Check ($null -ne $upperXml.SelectSingleNode("//*[@name='teacher-task-editor-status']")) 'Task editor exposes in-modal add/error feedback'
    Check ($null -eq $upperXml.SelectSingleNode("//*[@name='teacher-task-editor-publish']")) 'Task editor has no separate single-task publish button'
    Check ($upperXml.SelectSingleNode("//*[@name='teacher-task-editor-save']").GetAttribute('text').Contains('添加项目')) 'Task editor adds one selected training project to the shared question list'
    Check (@($faultPublishPage.SelectNodes(".//*[@text and contains(@text, '跨类别多选')]")).Count -gt 0) 'Fault catalog explicitly supports cross-category multi-selection'
    Check ($upperXml.SelectSingleNode("//*[@name='teacher-task-editor-category-caption']").GetAttribute('text').Contains('单选训练项目')) 'Training-project editor remains single-selection'
    Check ($null -ne $upperXml.SelectSingleNode("//*[contains(@text, '勾选行首选择框即加入此次考核内容')]")) 'Task list explains that each row checkbox controls assessment inclusion'
    Check (Test-Path -LiteralPath (AssetPath $faultCatalogPath) -PathType Leaf) 'Student fault catalog exists'
    $faultCatalog = (ReadAsset $faultCatalogPath) | ConvertFrom-Json
    $quayClassifiedCount = 0
    foreach ($category in $faultCatalog.quayCrane.categories) {
        $quayClassifiedCount += @($category.faults).Count
    }
    $yardClassifiedCount = 0
    foreach ($category in $faultCatalog.yardCrane.categories) {
        $yardClassifiedCount += @($category.faults).Count
    }
    $expectedQuayFaultCategories = @(
        '电源故障', '起升机构', '大车机构', '小车机构', '导板机构', '前后倾机构',
        '平面回转机构', '左右倾机构', '夹轮器', '吊具泵', '元器件损坏')
    $expectedYardFaultCategories = @(
        '电源故障', '起升机构', '大车机构', '小车机构', '俯仰机构', '前后倾机构',
        '平面回转机构', '左右倾机构', '夹轮器', '吊具泵', '元器件损坏')
    Check ((@($faultCatalog.quayCrane.categories | ForEach-Object { $_.name }) -join '|') -eq
        ($expectedQuayFaultCategories -join '|')) 'Quay-crane student catalog exposes the eleven categories in UI order'
    Check ((@($faultCatalog.yardCrane.categories | ForEach-Object { $_.name }) -join '|') -eq
        ($expectedYardFaultCategories -join '|')) 'Yard-crane student catalog exposes the eleven categories in UI order'
    Check ($quayClassifiedCount -eq 64) 'Quay-crane student catalog classifies all 64 faults'
    Check (@($faultCatalog.quayCrane.unclassifiedFaults).Count -eq 0) 'Quay-crane student catalog has no unclassified faults'
    Check ($yardClassifiedCount -eq 100) 'Yard-crane student catalog classifies all 100 faults'
    Check (@($faultCatalog.yardCrane.unclassifiedFaults).Count -eq 0) 'Yard-crane student catalog has no unclassified faults'

    $faultAssetScriptGuid = CheckMetaAsset $faultAssetScriptPath
    $quayFaultAssetGuid = CheckMetaAsset $quayFaultAssetPath
    $yardFaultAssetGuid = CheckMetaAsset $yardFaultAssetPath
    $quayFaultAssetText = ReadAsset $quayFaultAssetPath
    $yardFaultAssetText = ReadAsset $yardFaultAssetPath
    Check ($quayFaultAssetText.Contains("guid: $faultAssetScriptGuid,")) 'Quay-crane fault asset uses the expected ScriptableObject type'
    Check ($yardFaultAssetText.Contains("guid: $faultAssetScriptGuid,")) 'Yard-crane fault asset uses the expected ScriptableObject type'
    $quayFaultRows = @(ParseFaultAsset $quayFaultAssetPath)
    $yardFaultRows = @(ParseFaultAsset $yardFaultAssetPath)
    Check ($quayFaultRows.Count -eq 64) 'Quay-crane fault asset contains 64 records'
    Check ($yardFaultRows.Count -eq 100) 'Yard-crane fault asset contains 100 records'
    Check ((@($quayFaultRows.Id | Sort-Object) -join '|') -eq ((1..64) -join '|')) 'Quay-crane fault asset IDs are unique and complete from 1 to 64'
    Check ((@($yardFaultRows.Id | Sort-Object) -join '|') -eq ((1..100) -join '|')) 'Yard-crane fault asset IDs are unique and complete from 1 to 100'
    Check ((@($quayFaultRows.Category | Sort-Object -Unique).Count) -eq 11) 'Quay-crane fault asset uses all eleven categories'
    Check ((@($yardFaultRows.Category | Sort-Object -Unique).Count) -eq 11) 'Yard-crane fault asset uses all eleven categories'

    foreach ($contract in @(
        @($faultCatalog.quayCrane, $quayFaultRows, 'Quay-crane'),
        @($faultCatalog.yardCrane, $yardFaultRows, 'Yard-crane'))) {
        $jsonById = @{}
        foreach ($category in $contract[0].categories) {
            foreach ($fault in $category.faults) {
                $jsonById[[int]$fault.number] = [pscustomobject]@{
                    Phenomenon = $fault.phenomenon
                    FaultPoint = $fault.faultPoint
                    LineTag = $fault.lineTag
                    Category = $category.name
                }
            }
        }
        $parity = $jsonById.Count -eq @($contract[1]).Count
        foreach ($row in @($contract[1])) {
            $jsonRow = $jsonById[$row.Id]
            if ($null -eq $jsonRow -or
                $jsonRow.Phenomenon -cne $row.Phenomenon -or
                $jsonRow.FaultPoint -cne $row.FaultPoint -or
                $jsonRow.LineTag -cne $row.LineTag -or
                $jsonRow.Category -cne $row.Category) {
                $parity = $false
                break
            }
        }
        Check $parity "$($contract[2]) student JSON and teacher ScriptableObject records match exactly"
    }

    $teacherSource = ReadAsset $teacherScriptPath
    $studentSideSource = ReadAsset $studentSideScriptPath
    $assessmentQuestionBankSource = ReadAsset $assessmentQuestionBankPath
    $upperStyleSource = ReadAsset $upperStylePath
    Check (-not [regex]::IsMatch($teacherSource, 'AddComponent\s*<\s*UIDocument\s*>')) 'Teacher shell never creates a UIDocument dynamically'
    Check ($teacherSource.Contains('IYardCraneFaultCatalogSource')) 'Teacher shell reads fault data through the runtime catalog contract'
    Check (-not $teacherSource.Contains('Resources.Load<TextAsset>(FaultCatalogResourcePath)')) 'Teacher shell no longer reads its fault rows from the student JSON resource'
    Check ($teacherSource.Contains('YardCraneStudentShell.SelectedCraneKind == YardCraneCraneKind.QuayCrane')) 'Teacher fault catalog follows the Build Profile-selected crane kind'
    Check ($teacherSource.Contains('private readonly HashSet<int> selectedFaultIds') -and
           $teacherSource.Contains('selectedFaultIds.Add(fault.number)') -and
           $teacherSource.Contains('selectedFaultIds.Remove(fault.number)')) 'Teacher fault list stores a cross-category set of selected fault ids'
    Check ($teacherSource.Contains('YardCraneAssessmentQuestionBankStore.TryAddTrainingQuestions') -and
           $teacherSource.Contains('YardCraneAssessmentQuestionBankStore.TryAddFaultTask')) 'Teacher adds one training project or one grouped fault task to the local bank'
    Check ($teacherSource.Contains('isFaultTask ? "故障排除" : "实训"') -and
           $teacherSource.Contains('isFaultTask ? $"故障 {faultItems.Count} 条" : QuestionTitle(question)') -and
           $teacherSource.Contains('text = "查看故障"') -and
           $teacherSource.Contains('OpenFaultTaskDetail(question)')) 'Task rows show grouped-fault type/count and expose the selected-fault detail action'
    Check ($teacherSource.Contains('YardCraneAssessmentQuestionBankStore.TrySetIncluded') -and
           $teacherSource.Contains('teacher-task-inclusion-checkbox') -and
           $teacherSource.Contains('text = question.included ? "✓" : string.Empty') -and
           $teacherSource.Contains('question.included ? "已加入" : "未加入"') -and
           -not $teacherSource.Contains('teacher-task-publish-toggle')) 'Teacher rows expose a visible inclusion checkbox that immediately rebuilds the current paper JSON'
    Check ($upperXml.SelectSingleNode("//*[contains(concat(' ', normalize-space(@class), ' '), ' teacher-task-col-status ')]").GetAttribute('text') -eq '本次考核' -and
           $upperStyleSource.Contains('.teacher-task-inclusion-checkbox.selected')) 'Task table labels and styles clearly distinguish current-assessment selection'
    Check ($teacherSource.Contains('Dictionary<string, YardCraneTaskDefinition> selectedTaskDefinitions') -and
           [regex]::IsMatch($teacherSource, '(?s)else\s*\{\s*selectedTaskDefinitions\.Clear\(\);\s*selectedTaskDefinitions\[key\]\s*=\s*task;\s*\}') -and
           $teacherSource.Contains('selectedTaskDefinitions.Count != 1') -and
           -not $teacherSource.Contains('selectedTaskDefinitions.Remove(key)')) 'Teacher task editor replaces the previous project and saves exactly one selection'
    Check (-not $teacherSource.Contains('teacher-task-multi-select-check') -and
           -not $upperStyleSource.Contains('teacher-task-multi-select-check')) 'Training-project multi-select checkbox is removed'
    Check ($assessmentQuestionBankSource.Contains('-assessment-question-bank.json') -and
           $assessmentQuestionBankSource.Contains('-assessment-paper.json')) 'Question bank store persists crane-specific local bank and paper JSON files'
    Check ($assessmentQuestionBankSource.Contains('public List<YardCraneAssessmentFaultQuestionData> faults = new();') -and
           $assessmentQuestionBankSource.Contains('public static bool TryAddFaultTask(') -and
           $assessmentQuestionBankSource.Contains('StableFaultTaskQuestionId(') -and
           $assessmentQuestionBankSource.Contains('public static IReadOnlyList<YardCraneAssessmentFaultQuestionData> FaultItems(')) 'Question-bank model persists one grouped fault task with backward-compatible fault access'
    Check ($assessmentQuestionBankSource.Contains('snapshot.faults') -and
           $assessmentQuestionBankSource.Contains('fault.faultPoint = string.Empty;') -and
           $assessmentQuestionBankSource.Contains('fault.lineTag = string.Empty;') -and
           $assessmentQuestionBankSource.Contains('CloneFaults(source.faults)')) 'Student paper deep-copies grouped faults and strips every answer field'
    Check ($assessmentQuestionBankSource.Contains('HasFaultPayload(question.fault)') -and
           $assessmentQuestionBankSource.Contains('question.fault = null;') -and
           $assessmentQuestionBankSource.Contains('HasTrainingPayload(question.training)') -and
           $assessmentQuestionBankSource.Contains('question.training = null;')) 'Question bank accepts JsonUtility empty cross-kind objects while rejecting populated cross-kind data'
    Check ($assessmentQuestionBankSource.Contains('TryBuildPaper(bank, out paper') -and
           $assessmentQuestionBankSource.Contains('TryWriteBankAndPaper(bank, paper')) 'Publishing a checkbox writes one ordered paper snapshot from all included questions'
    Check ($assessmentQuestionBankSource.Contains('public List<YardCraneAssessmentQuestion> items = new();') -and
           $assessmentQuestionBankSource.Contains('items.Add(snapshot)')) 'Current paper supports multiple independently ordered questions'
    Check ($studentSideSource.Contains('YardCraneAssessmentQuestionBankStore.TryLoadCurrentPaper')) 'Student assessment page reads the current multi-question paper'
    Check ($studentSideSource.Contains('YardCraneAssessmentQuestionBankStore.FaultItems(question)') -and
           $studentSideSource.Contains('publishedFaults.Add(') -and
           $studentSideSource.Contains('faults = publishedFaults')) 'Student projection carries every fault in the selected grouped task into the launch assignment'
    foreach ($query in [regex]::Matches(
        $teacherSource,
        '\b(?<root>root(?:Lower)?)\.Q(?:<(?<type>[\w.]+)>)?\(\s*"(?<name>[^"]+)"')) {
        $name = $query.Groups['name'].Value
        $xml = $upperXml
        if ($query.Groups['root'].Value -eq 'rootLower') {
            $xml = $lowerXml
        }
        $node = $xml.SelectSingleNode("//*[@name='$name']")
        Check ($null -ne $node) "$teacherScriptPath query resolves in its own UXML: $name"
        $expectedType = $query.Groups['type'].Value
        if ($null -ne $node -and $expectedType -and $expectedType -ne 'VisualElement') {
            Check ($node.LocalName -eq $expectedType) "$name is a $expectedType element"
        }
    }

    foreach ($panelContract in @(
        @($upperPanelPath, 0, 'Teacher Display1'),
        @($lowerPanelPath, 1, 'Teacher Display2'))) {
        $panelText = ReadAsset $panelContract[0]
        Check ($panelText.Contains('m_ReferenceResolution: {x: 1920, y: 1080}')) "$($panelContract[2]) PanelSettings reference resolution is 1920 x 1080"
        Check ($panelText.Contains("m_TargetDisplay: $($panelContract[1])")) "$($panelContract[2]) PanelSettings targets display $($panelContract[1])"
    }

    $interactiveNames = @(
        'Button', 'Toggle', 'TextField', 'IntegerField', 'FloatField', 'DoubleField',
        'DropdownField', 'Slider', 'SliderInt', 'MinMaxSlider', 'ListView', 'TreeView',
        'MultiColumnListView', 'MultiColumnTreeView', 'ScrollView', 'Scroller')
    $interactiveNodes = @($lowerXml.SelectNodes('//*') | Where-Object {
        $interactiveNames -contains $_.LocalName
    })
    Check ($interactiveNodes.Count -eq 0) 'Teacher Display2 UXML contains no interactive UI Toolkit controls'
    Check ($teacherSource.Contains('rootLower.pickingMode = PickingMode.Ignore;')) 'Teacher shell disables picking on the Display2 root'
    Check ([regex]::IsMatch(
        $teacherSource,
        'rootLower\.Query<VisualElement>\(\)\.ForEach\(element\s*=>\s*element\.pickingMode\s*=\s*PickingMode\.Ignore\)')) 'Teacher shell disables picking throughout Display2'

    $sceneYaml = ReadAsset $scenePath
    $blocks = @(UnityBlocks $sceneYaml)
    Check ($blocks.Count -gt 0) 'TeacherHome scene YAML has parseable Unity object blocks'

    $uidocuments = @($blocks | Where-Object {
        $_.Type -eq '114' -and
        [regex]::IsMatch($_.Body, '(?m)^  m_Script: \{fileID: 19102, guid: 0000000000000000e000000000000000, type: 0\}$')
    })
    Check ($uidocuments.Count -eq 2) 'TeacherHome contains exactly two UIDocument components'

    $upperDocuments = @($uidocuments | Where-Object {
        $_.Body.Contains("m_PanelSettings: {fileID: 11400000, guid: $($teacherGuids[$upperPanelPath]),") -and
        [regex]::IsMatch($_.Body, 'sourceAsset: \{fileID: -?\d+, guid: ' + [regex]::Escape($teacherGuids[$upperUxmlPath]) + ',')
    })
    $lowerDocuments = @($uidocuments | Where-Object {
        $_.Body.Contains("m_PanelSettings: {fileID: 11400000, guid: $($teacherGuids[$lowerPanelPath]),") -and
        [regex]::IsMatch($_.Body, 'sourceAsset: \{fileID: -?\d+, guid: ' + [regex]::Escape($teacherGuids[$lowerUxmlPath]) + ',')
    })
    Check ($upperDocuments.Count -eq 1) 'TeacherHome Display1 UIDocument references only the dedicated TeacherSide Display1 UXML and PanelSettings'
    Check ($lowerDocuments.Count -eq 1) 'TeacherHome Display2 UIDocument references only the dedicated TeacherSide Display2 UXML and PanelSettings'

    $teacherScriptGuid = $teacherGuids[$teacherScriptPath]
    $teacherShellBlocks = @($blocks | Where-Object {
        $_.Type -eq '114' -and $_.Body.Contains("guid: $teacherScriptGuid,")
    })
    Check ($teacherShellBlocks.Count -eq 1) 'TeacherHome contains exactly one YardCraneTeacherSideShell'

    if ($teacherShellBlocks.Count -eq 1 -and $upperDocuments.Count -eq 1 -and $lowerDocuments.Count -eq 1) {
        $shell = $teacherShellBlocks[0]
        $upper = $upperDocuments[0]
        $lower = $lowerDocuments[0]
        $shellObjectId = GameObjectId $shell.Body
        $upperObjectId = GameObjectId $upper.Body
        $lowerObjectId = GameObjectId $lower.Body
        $lowerReferenceId = [regex]::Match(
            $shell.Body,
            '(?m)^  lowerDocument: \{fileID: (?<id>-?\d+)\}$').Groups['id'].Value

        Check ($shellObjectId -and $shellObjectId -eq $upperObjectId) 'YardCraneTeacherSideShell and Display1 UIDocument are on the same GameObject'
        Check ($lowerReferenceId -eq $lower.Id) 'YardCraneTeacherSideShell.lowerDocument points to the Display2 UIDocument component'
        Check ($lowerReferenceId -ne $upper.Id -and $lowerObjectId -ne $upperObjectId) 'Display2 is a different UIDocument on a different GameObject'
        Check ($shell.Body.Contains("panelTemplate: {fileID: 11400000, guid: $($teacherGuids[$upperPanelPath]),")) 'Teacher shell serializes its dedicated Display1 PanelSettings template'
        Check ($shell.Body.Contains("lowerPanelTemplate: {fileID: 11400000, guid: $($teacherGuids[$lowerPanelPath]),")) 'Teacher shell serializes its dedicated Display2 PanelSettings template'
        Check ($shell.Body.Contains("quayFaultCatalogAsset: {fileID: 11400000, guid: $quayFaultAssetGuid,")) 'Teacher shell references the quay-crane fault ScriptableObject'
        Check ($shell.Body.Contains("yardFaultCatalogAsset: {fileID: 11400000, guid: $yardFaultAssetGuid,")) 'Teacher shell references the yard-crane fault ScriptableObject'

        foreach ($documentContract in @(
            @($upper, $upperObjectId, 'Display1'),
            @($lower, $lowerObjectId, 'Display2'))) {
            $document = $documentContract[0]
            $objectId = $documentContract[1]
            $label = $documentContract[2]
            $transforms = @($blocks | Where-Object {
                $_.Type -eq '4' -and (GameObjectId $_.Body) -eq $objectId
            })
            Check ($document.Body.Contains('m_ParentUI: {fileID: 0}')) "Teacher $label UIDocument has no parent UI document"
            Check ($transforms.Count -eq 1 -and $transforms[0].Body.Contains('m_Father: {fileID: 0}')) "Teacher $label UIDocument GameObject is a scene root"
            Check ($document.Body.Contains('m_Enabled: 1')) "Teacher $label UIDocument is enabled in the saved scene"
        }
    }

    $forbiddenScriptPaths = @($entryScriptPath, $studentSideScriptPath)
    foreach ($forbiddenPath in $forbiddenScriptPaths) {
        $guid = AssetGuid $forbiddenPath
        Check ($guid.Length -eq 32) "$forbiddenPath GUID is available for the exclusion check"
        Check (-not $sceneYaml.Contains("guid: $guid,")) "TeacherHome contains no $([System.IO.Path]::GetFileNameWithoutExtension($forbiddenPath)) component"
    }

    $forbiddenUiRoots = @(
        'Assets/YardCraneSimulator/Shell/Resources/YardCraneShell/Display1UI',
        'Assets/YardCraneSimulator/Shell/Resources/YardCraneShell/Display2UI',
        'Assets/YardCraneSimulator/Shell/Resources/YardCraneShell/StudentSide')
    $forbiddenUiAssets = [System.Collections.Generic.List[string]]::new()
    foreach ($rootPath in $forbiddenUiRoots) {
        $absoluteRoot = AssetPath $rootPath
        if (-not (Test-Path -LiteralPath $absoluteRoot -PathType Container)) {
            continue
        }
        foreach ($asset in Get-ChildItem -LiteralPath $absoluteRoot -Recurse -File) {
            if ($asset.Extension -eq '.uxml' -or $asset.Name.EndsWith('Panel.asset', [System.StringComparison]::Ordinal)) {
                $forbiddenUiAssets.Add($asset.FullName.Substring($projectRoot.Length + 1).Replace('\', '/'))
            }
        }
    }
    Check ($forbiddenUiAssets.Count -gt 0) 'StudentSide and StudentEntry UXML/Panel assets were discovered for exclusion checks'
    foreach ($forbiddenPath in $forbiddenUiAssets) {
        $guid = AssetGuid $forbiddenPath
        Check ($guid.Length -eq 32) "$forbiddenPath GUID is available for the exclusion check"
        if ($guid) {
            Check (-not $sceneYaml.Contains("guid: $guid,")) "TeacherHome does not reference student/entry UI asset: $forbiddenPath"
        }
    }
    $forbiddenGuids = @($forbiddenUiAssets | ForEach-Object { AssetGuid $_ } | Where-Object { $_ })
    Check (@($teacherGuids.Values | Where-Object { $forbiddenGuids -contains $_ }).Count -eq 0) 'Teacher UXML, USS, Panel and shell GUIDs do not collide with student/entry UI GUIDs'

    $cameras = @($blocks | Where-Object { $_.Type -eq '20' })
    Check ($cameras.Count -eq 2) 'TeacherHome contains exactly two Camera components'
    $cameraTargets = @($cameras | ForEach-Object {
        [regex]::Match($_.Body, '(?m)^  m_TargetDisplay: (?<display>\d+)\s*$').Groups['display'].Value
    } | Sort-Object)
    Check (($cameraTargets -join '|') -eq '0|1') 'TeacherHome cameras target Display 1 and Display 2 exactly once each'
    Check (@($cameras | Where-Object { $_.Body.Contains('m_Enabled: 1') }).Count -eq 2) 'Both TeacherHome cameras are enabled'

    $buildSettingsPath = 'ProjectSettings/EditorBuildSettings.asset'
    $buildSettings = ReadAsset $buildSettingsPath
    $sceneEntries = @([regex]::Matches(
        $buildSettings,
        '(?ms)^  - enabled: (?<enabled>[01])\r?\n    path: (?<path>[^\r\n]+)\r?\n    guid: (?<guid>[0-9a-f]{32})\s*$'))
    $expectedScenes = @(
        'Assets/YardCraneSimulator/Scenes/StudentEntry.unity',
        'Assets/YardCraneSimulator/Scenes/Bootstrap.unity',
        'Assets/YardCraneSimulator/Scenes/YardCrane.unity',
        'Assets/YardCraneSimulator/Scenes/StudentHome.unity',
        'Assets/YardCraneSimulator/Scenes/TeacherHome.unity')
    Check ($sceneEntries.Count -eq 5) 'Build Settings contains exactly five scenes'
    Check ((@($sceneEntries | ForEach-Object { $_.Groups['path'].Value }) -join '|') -eq
        ($expectedScenes -join '|')) 'Build Settings scene order is StudentEntry, Bootstrap, YardCrane, StudentHome, TeacherHome'
    Check (@($sceneEntries | Where-Object { $_.Groups['enabled'].Value -ne '1' }).Count -eq 0) 'All five required Build Settings scenes are enabled'
    foreach ($entry in $sceneEntries) {
        $path = $entry.Groups['path'].Value
        Check ((AssetGuid $path) -eq $entry.Groups['guid'].Value) "Build Settings GUID resolves for $path"
    }
    Check ($sceneEntries.Count -eq 5 -and $sceneEntries[4].Groups['guid'].Value -eq $sceneGuid) 'Build Settings TeacherHome entry uses the actual scene GUID'

    $maintenanceFiles = @(
        'Assets/YardCraneSimulator/Editor/YardCraneBuildAutomation.cs',
        'Assets/YardCraneSimulator/Editor/ProjectScaffolder.cs',
        'Assets/YardCraneSimulator/Editor/YardCraneOperationalSceneBuilder.cs')
    foreach ($maintenancePath in $maintenanceFiles) {
        Check (Test-Path -LiteralPath (AssetPath $maintenancePath) -PathType Leaf) "$maintenancePath exists"
        $source = ReadAsset $maintenancePath
        Check ($source.Contains('Assets/YardCraneSimulator/Scenes/TeacherHome.unity')) "$maintenancePath maintains the TeacherHome build-scene contract"
    }
    $buildAutomationSource = ReadAsset 'Assets/YardCraneSimulator/Editor/YardCraneBuildAutomation.cs'
    Check ($buildAutomationSource.Contains('EditorBuildSettings.globalScenes') -and
           -not $buildAutomationSource.Contains('EditorBuildSettings.scenes =') -and
           -not $buildAutomationSource.Contains('[InitializeOnLoad]')) 'Package scene registration updates only the global Scene List and does not run on every domain reload'
    Check ($buildAutomationSource.Contains('Export UnityPackage Without Build Profiles') -and
           $buildAutomationSource.Contains('AssetDatabase.ExportPackage') -and
           $buildAutomationSource.Contains('!IsBuildProfilePath(path)')) 'UnityPackage export explicitly excludes Build Profile assets'
    Check ($buildAutomationSource.Contains('ShouldQueueRequiredSceneRegistration') -and
           $buildAutomationSource.Contains('movedAssets.Any(IsRequiredScenePath)') -and
           $buildAutomationSource.Contains('SessionState.GetBool(SessionRegistrationCheckKey, false)')) 'Package postprocessor handles scene imports and performs only one fallback check per Editor session after domain reload'

    Check (Test-Path -LiteralPath (AssetPath $entryUxmlPath) -PathType Leaf) 'StudentEntry Display1 UXML exists'
    $entryUxml = ReadAsset $entryUxmlPath
    Check (-not $entryUxml.Contains('teacher-home-page')) 'StudentEntry UXML no longer embeds teacher-home-page'
    foreach ($teacherCardMarker in @(
        'teacher-task-management-button',
        'teacher-assessment-statistics-button',
        'teacher-fault-publish-management-button',
        'teacher-management-grid',
        'teacher-core-card')) {
        Check (-not $entryUxml.Contains($teacherCardMarker)) "StudentEntry UXML contains no embedded teacher card marker: $teacherCardMarker"
    }

    Record "SOURCE_SHA256 $scenePath $((Get-FileHash -LiteralPath (AssetPath $scenePath) -Algorithm SHA256).Hash)"
    Record "SOURCE_SHA256 $teacherScriptPath $((Get-FileHash -LiteralPath (AssetPath $teacherScriptPath) -Algorithm SHA256).Hash)"
    Record "SOURCE_SHA256 $upperUxmlPath $((Get-FileHash -LiteralPath (AssetPath $upperUxmlPath) -Algorithm SHA256).Hash)"
    Record "SOURCE_SHA256 $lowerUxmlPath $((Get-FileHash -LiteralPath (AssetPath $lowerUxmlPath) -Algorithm SHA256).Hash)"
}
catch {
    $detail = $_.Exception.ToString()
    $failures.Add("Unhandled harness exception: $detail")
    Record "FAIL Unhandled harness exception: $detail"
}

if ($failures.Count -eq 0) {
    Record "[YC_TEACHER_HOME_UI_STATIC_PASS] checks=$checkCount failures=0"
} else {
    Record "[YC_TEACHER_HOME_UI_STATIC_FAIL] checks=$checkCount failures=$($failures.Count)"
    foreach ($failure in $failures) {
        Record "FAILED_CHECK $failure"
    }
}

[System.IO.File]::WriteAllLines($resultPath, $report, [System.Text.UTF8Encoding]::new($false))
[Console]::WriteLine("RESULT $resultPath")

if ($failures.Count -gt 0) {
    exit 1
}

exit 0
