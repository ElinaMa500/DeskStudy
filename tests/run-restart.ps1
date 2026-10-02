$ErrorActionPreference = 'Stop'
# "Restart now" after choosing a language: the old instance saves and exits, the new one waits for it and takes over.
$projectRoot = Split-Path -Parent $PSScriptRoot
$artifactPath = Join-Path $projectRoot 'artifacts'
$appDir = Join-Path $artifactPath 'restart-app'
& (Join-Path $projectRoot 'build.ps1') -OutputDirectory 'artifacts\restart-app' | Out-Null
& (Join-Path $PSScriptRoot 'run-layouts.ps1') -CompileOnly | Out-Null
$dataPath = Join-Path $artifactPath ('restart-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$exe = Join-Path $appDir 'DeskStudy.exe'
& (Join-Path $artifactPath 'NotebookLayoutTests.exe') $dataPath restart $exe
if ($LASTEXITCODE -ne 0) { throw 'Restart request failed.' }
Start-Sleep -Seconds 6
$running = @(Get-CimInstance Win32_Process -Filter "Name='DeskStudy.exe'" | Where-Object { $_.CommandLine -like ('*' + $dataPath + '*') })
try {
    $data = Get-Content (Join-Path $dataPath 'data.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($running.Count -ne 1) { throw ('Expected one restarted instance, found ' + $running.Count) }
    if ($running[0].CommandLine -notlike '*--restart-after*') { throw 'The running instance was not started by the restart.' }
    if ($data.Settings.Language -ne 'en') { throw 'The chosen language was not saved.' }
    'PASS restart: one new instance on the same data folder, language saved as en'
}
finally { $running | ForEach-Object { Stop-Process -Id $_.ProcessId -ErrorAction SilentlyContinue } }
