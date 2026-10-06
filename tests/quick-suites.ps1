param([string]$Since = '')
# Picks the suites for run-all.ps1 -Quick: the core suites always (no windows, about half a minute), plus the
# desktop suites related to the changed files. Changed files are the uncommitted ones (including new files),
# or with -Since <ref> everything changed since that commit. Writes the chosen suite names to the pipeline and
# explains the choice on the console. A changed source or test file that the table below does not know about
# selects every suite, so a new file can never be skipped by accident.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot

$core = @('core', 'duecore', 'layoutcore', 'placecore', 'settingscore')
$all = $core + @('layouts', 'render', 'due', 'frame', 'place', 'calstyle', 'fold', 'inline', 'lang', 'english', 'ddl', 'taskbar', 'shortcut', 'outlook', 'allday', 'pageturn', 'settings', 'integration', 'restart')
$layoutRunner = @('layouts', 'render', 'due', 'frame', 'place', 'calstyle', 'fold', 'inline', 'lang', 'english', 'ddl', 'taskbar', 'shortcut', 'outlook', 'allday', 'pageturn')

# File -> desktop suites that exercise it. Core suites always run, so pure logic files list only their windows.
$map = @{
    'src/CalendarForm.cs'          = @('calstyle', 'allday', 'ddl', 'outlook', 'render')
    'src/Collapse.cs'              = @('fold', 'place')
    'src/DesktopMode.cs'           = @('frame', 'place', 'fold', 'taskbar', 'shortcut')
    'src/WidgetFrame.cs'           = @('frame', 'place', 'fold', 'render', 'pageturn')
    'src/Placement.cs'             = @('place', 'fold')
    'src/PlacementLogic.cs'        = @('place', 'fold')
    'src/DuePicker.cs'             = @('due', 'ddl')
    'src/NotebookDue.cs'           = @('due', 'ddl')
    'src/Ics.cs'                   = @('outlook', 'allday')
    'src/Outlook.cs'               = @('outlook', 'allday')
    'src/OutlookSync.cs'           = @('outlook')
    'src/Lang.cs'                  = @('lang', 'english')
    'src/Lang.en.cs'               = @('lang', 'english')
    'src/NotebookForm.cs'          = @('layouts', 'render', 'pageturn', 'inline', 'ddl', 'due')
    'src/NotebookLayouts.cs'       = @('layouts', 'render', 'pageturn', 'inline')
    'src/NotebookInlineEdit.cs'    = @('inline', 'pageturn')
    'src/NotebookLayoutPreview.cs' = @('layouts', 'settings')
    'src/SettingsForm.cs'          = @('settings', 'frame', 'taskbar', 'shortcut')
    'src/SettingsController.cs'    = @('settings', 'integration', 'restart', 'pageturn', 'ddl')
    'src/SettingsModel.cs'         = @('settings', 'integration', 'restart', 'layouts')
    'src/Core.cs'                  = @('settings', 'integration', 'restart', 'layouts', 'ddl', 'allday')
    'src/Shell.cs'                 = @('layouts', 'integration', 'restart', 'settings', 'taskbar')
    'src/Shortcuts.cs'             = @('shortcut')
    'src/TaskbarButton.cs'         = @('taskbar')
    'src/AssemblyInfo.cs'          = @('restart')
    'src/app.manifest'             = @('frame', 'restart')
    'src/DeskStudy.exe.config'     = @('restart')
    'tests/NotebookLayoutTests.cs' = @('layouts', 'render', 'due', 'frame')
    'tests/PlacementChecks.cs'     = @('place')
    'tests/CalendarStyleChecks.cs' = @('calstyle')
    'tests/FoldChecks.cs'          = @('fold')
    'tests/InlineEditChecks.cs'    = @('inline')
    'tests/LangChecks.cs'          = @('lang')
    'tests/EnglishChecks.cs'       = @('english')
    'tests/DeadlineChecks.cs'      = @('ddl')
    'tests/TaskbarChecks.cs'       = @('taskbar')
    'tests/ShortcutChecks.cs'      = @('shortcut')
    'tests/OutlookChecks.cs'       = @('outlook')
    'tests/AllDayChecks.cs'        = @('allday')
    'tests/PageTurnChecks.cs'      = @('pageturn')
    'tests/GuideShots.cs'          = @('layouts')
    'tests/SiteShots.cs'           = @('layouts')
    'tests/TitleGapShot.cs'        = @('layouts')
    'tests/run-layouts.ps1'        = $layoutRunner
    'tests/IntegrationTests.cs'    = @('integration')
    'tests/run-integration.ps1'    = @('integration')
    'tests/SettingsIntegrationTests.cs' = @('settings')
    'tests/WidgetSettingsChecks.cs'     = @('settings')
    'tests/PackagedSmoke.cs'       = @('settings')
    'tests/run-settings.ps1'       = @('settings')
    'tests/run-restart.ps1'        = @('restart')
}
# Covered by the core suites alone, or not code that any suite runs.
$coreOnly = '^tests/(CoreTests|DueCoreTests|NotebookLayoutCoreTests|PlacementCoreTests|SettingsCoreTests)\.cs$|^tests/run-(core|due-core|layout-core|placement-core|all)\.ps1$|^tests/(quick-suites|launch-preview)\.ps1$'
$notCode = '\.(md|png|jpg|html|css|txt|zip)$|^docs/|^tools/|^baselines/|^artifacts/|^release-|^\.gitignore$|^LICENSE|^(build|package)\.ps1$|^tests/generate-icon\.js$'

Push-Location $projectRoot
$encoding = [Console]::OutputEncoding
try {
    [Console]::OutputEncoding = [Text.Encoding]::UTF8
    if ($Since -ne '') { $changed = @(git -c core.quotepath=false diff --name-only $Since) + @(git -c core.quotepath=false ls-files --others --exclude-standard) }
    else { $changed = @(git -c core.quotepath=false status --porcelain --untracked-files=all | ForEach-Object { ($_.Substring(3) -split ' -> ')[-1].Trim('"') }) }
    if ($LASTEXITCODE -ne 0) { throw 'git could not list the changed files.' }
}
finally { [Console]::OutputEncoding = $encoding; Pop-Location }
$changed = @($changed | Where-Object { $_ } | ForEach-Object { $_ -replace '\\', '/' } | Sort-Object -Unique)

$chosen = New-Object System.Collections.Generic.List[string]; $core | ForEach-Object { $chosen.Add($_) }
$unknown = @()
foreach ($file in $changed) {
    if ($map.ContainsKey($file)) { $map[$file] | ForEach-Object { $chosen.Add($_) }; Write-Host ('  {0,-34} -> {1}' -f $file, ($map[$file] -join ', ')) }
    elseif ($file -match $coreOnly) { Write-Host ('  {0,-34} -> core suites' -f $file) }
    elseif ($file -match $notCode) { }
    else { $unknown += $file }
}
if ($unknown.Count -gt 0) {
    Write-Host ('  not in the quick table, so running everything: ' + ($unknown -join ', '))
    $chosen = $all
}
if ($changed.Count -eq 0) { Write-Host '  no changed files: core suites only' }
$result = @($all | Where-Object { $chosen -contains $_ })
Write-Host ('Quick: {0} of {1} suites ({2})' -f $result.Count, $all.Count, ($result -join ', '))
$result
