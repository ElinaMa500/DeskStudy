param(
    # Run only these suites (names as in the table below), e.g. -Only core,outlook
    [string[]]$Only = @(),
    # A suite that runs longer than this is stopped and counted as failed.
    [int]$TimeoutSeconds = 600,
    # Also build the package and run the packaged smoke test.
    [switch]$Packaged
)
# Runs every test suite one after another (the desktop suites open real windows, so never in parallel)
# and prints one line per suite. A suite fails when its script throws or a test runner exits non-zero;
# the words in its output do not decide it. Exit code 1 when anything failed or timed out.
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$projectRoot = Split-Path -Parent $here
$artifactPath = Join-Path $projectRoot 'artifacts'
New-Item -ItemType Directory -Force -Path $artifactPath | Out-Null
$log = Join-Path $artifactPath ('run-all-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')

$suites = @(
    @{ Name = 'core';         Script = 'run-core.ps1';            Args = @{} },
    @{ Name = 'duecore';      Script = 'run-due-core.ps1';        Args = @{} },
    @{ Name = 'layoutcore';   Script = 'run-layout-core.ps1';     Args = @{} },
    @{ Name = 'placecore';    Script = 'run-placement-core.ps1';  Args = @{} },
    @{ Name = 'settingscore'; Script = 'run-settings.ps1';        Args = @{ CoreOnly = $true } },
    @{ Name = 'layouts';      Script = 'run-layouts.ps1';         Args = @{} },
    @{ Name = 'render';       Script = 'run-layouts.ps1';         Args = @{ Render = $true } },
    @{ Name = 'due';          Script = 'run-layouts.ps1';         Args = @{ Due = $true } },
    @{ Name = 'frame';        Script = 'run-layouts.ps1';         Args = @{ Frame = $true } },
    @{ Name = 'place';        Script = 'run-layouts.ps1';         Args = @{ Place = $true } },
    @{ Name = 'calstyle';     Script = 'run-layouts.ps1';         Args = @{ CalendarStyle = $true } },
    @{ Name = 'fold';         Script = 'run-layouts.ps1';         Args = @{ Fold = $true } },
    @{ Name = 'inline';       Script = 'run-layouts.ps1';         Args = @{ InlineEdit = $true } },
    @{ Name = 'lang';         Script = 'run-layouts.ps1';         Args = @{ Language = $true } },
    @{ Name = 'english';      Script = 'run-layouts.ps1';         Args = @{ English = $true } },
    @{ Name = 'ddl';          Script = 'run-layouts.ps1';         Args = @{ Deadline = $true } },
    @{ Name = 'taskbar';      Script = 'run-layouts.ps1';         Args = @{ Taskbar = $true } },
    @{ Name = 'outlook';      Script = 'run-layouts.ps1';         Args = @{ Outlook = $true } },
    @{ Name = 'allday';       Script = 'run-layouts.ps1';         Args = @{ AllDay = $true } },
    @{ Name = 'pageturn';     Script = 'run-layouts.ps1';         Args = @{ PageTurn = 'check' } },
    @{ Name = 'settings';     Script = 'run-settings.ps1';        Args = @{} },
    @{ Name = 'integration';  Script = 'run-integration.ps1';     Args = @{} },
    @{ Name = 'restart';      Script = 'run-restart.ps1';         Args = @{} }
)
if ($Packaged) { $suites += @{ Name = 'packaged'; Script = 'run-settings.ps1'; Args = @{ Packaged = $true }; Before = 'package' } }
if ($Only.Count -gt 0) { $suites = @($suites | Where-Object { $Only -contains $_.Name }) }

# Test runners and test copies of the app all live under artifacts; the installed app is never touched.
function Stop-TestProcesses {
    Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($artifactPath, [StringComparison]::OrdinalIgnoreCase) } |
        ForEach-Object { try { Stop-Process -Id $_.ProcessId -Force -ErrorAction Stop } catch { } }
}

$results = @()
foreach ($suite in $suites) {
    if ($suite.Before -eq 'package') { & (Join-Path $projectRoot 'package.ps1') *> (Join-Path $artifactPath 'run-all-package.log') }
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $job = Start-Job -ScriptBlock { param($script, $arguments) & $script @arguments 2>&1 | ForEach-Object { "$_" } } -ArgumentList (Join-Path $here $suite.Script), $suite.Args
    $finished = Wait-Job $job -Timeout $TimeoutSeconds
    if (-not $finished) { Stop-TestProcesses; Stop-Job $job }
    $output = @(Receive-Job $job -ErrorAction SilentlyContinue -ErrorVariable failures)
    $failed = (-not $finished) -or $job.State -eq 'Failed' -or $failures.Count -gt 0 -or $job.ChildJobs[0].Error.Count -gt 0
    Remove-Job $job -Force
    $passes = @($output | Where-Object { $_ -match '^PASS ' }).Count
    $status = if (-not $finished) { 'TIMEOUT' } elseif ($failed) { 'FAILED' } else { 'ok' }
    $reason = ''
    if ($failed) {
        $reason = (@($failures | ForEach-Object { "$_" }) + @($output | Where-Object { $_ -match 'Exception|FAIL|error CS|failed' })) | Select-Object -First 3
        $reason = ($reason -join ' | ')
    }
    $line = '{0,-13} {1,-8} {2,4} passed  {3,5:N0} s  {4}' -f $suite.Name, $status, $passes, $clock.Elapsed.TotalSeconds, $reason
    Write-Output $line
    Add-Content -Path $log -Value $line -Encoding UTF8
    Add-Content -Path $log -Value ($output | ForEach-Object { '    ' + $_ }) -Encoding UTF8
    $results += [pscustomobject]@{ Suite = $suite.Name; Status = $status; Passed = $passes }
}

$bad = @($results | Where-Object { $_.Status -ne 'ok' })
$total = ($results | Measure-Object -Property Passed -Sum).Sum
Write-Output ''
Write-Output ('{0} suites, {1} checks passed, {2} failed. Full log: {3}' -f $results.Count, $total, $bad.Count, $log)
if ($bad.Count -gt 0) { exit 1 }
exit 0
