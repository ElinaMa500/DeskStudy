param([switch]$Render, [switch]$CoreOnly, [switch]$Notify, [switch]$Packaged, [string]$PackageExecutable)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$artifactPath = Join-Path $projectRoot 'artifacts'
New-Item -ItemType Directory -Force -Path $artifactPath | Out-Null
if ($Packaged) {
    if (-not $PackageExecutable) { $PackageExecutable = Join-Path $projectRoot 'release-1.3\DeskStudy.exe' }
    $smokeExe = Join-Path $artifactPath 'PackagedSmoke.exe'
    & $compilerPath /nologo /target:exe /codepage:65001 ('/out:' + $smokeExe) ('/win32manifest:' + (Join-Path $projectRoot 'src\app.manifest')) /reference:System.Windows.Forms.dll /reference:System.Core.dll (Join-Path $PSScriptRoot 'PackagedSmoke.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Packaged smoke compilation failed.' }
    $dataPath = Join-Path $artifactPath ('packaged-smoke-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    & $smokeExe $PackageExecutable $dataPath 2>&1 | Tee-Object -FilePath (Join-Path $artifactPath 'packaged-smoke.log')
    if ($LASTEXITCODE -ne 0) { throw 'Packaged smoke test failed.' }
    exit 0
}
if ($CoreOnly) {
    $coreExe = Join-Path $artifactPath 'SettingsCoreTests.exe'
    & $compilerPath /nologo /target:exe /codepage:65001 ('/out:' + $coreExe) /reference:System.Web.Extensions.dll (Join-Path $projectRoot 'src\Core.cs') (Join-Path $projectRoot 'src\SettingsModel.cs') (Join-Path $PSScriptRoot 'SettingsCoreTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Settings core compilation failed.' }
    & $coreExe 2>&1 | Tee-Object -FilePath (Join-Path $artifactPath 'settings-core.log')
    if ($LASTEXITCODE -ne 0) { throw 'Settings core tests failed.' }
    exit 0
}
$testExe = Join-Path $artifactPath 'SettingsIntegrationTests.exe'
$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object FullName
$buildArgs = @('/nologo', '/target:exe', '/main:SettingsIntegrationTests', '/codepage:65001', ('/out:' + $testExe), ('/win32manifest:' + (Join-Path $projectRoot 'src\app.manifest')), '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll', '/reference:System.Web.Extensions.dll')
& $compilerPath @buildArgs @sourceFiles (Join-Path $PSScriptRoot 'SettingsIntegrationTests.cs') (Join-Path $PSScriptRoot 'WidgetSettingsChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Settings integration compilation failed.' }
$dataPath = Join-Path $artifactPath ('settings-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
if ($Notify) { & $testExe $dataPath notify 2>&1 | Tee-Object -FilePath (Join-Path $artifactPath 'settings-notify.log'); if ($LASTEXITCODE -ne 0) { throw 'Native notification test failed.' }; exit 0 }
if ($Render) { & $testExe $dataPath render 2>&1 | Tee-Object -FilePath (Join-Path $artifactPath 'settings-render.log'); if ($LASTEXITCODE -ne 0) { throw 'Settings rendering failed.' }; exit 0 }
& $testExe $dataPath write 2>&1 | Tee-Object -FilePath (Join-Path $artifactPath 'settings-write.log')
if ($LASTEXITCODE -ne 0) { throw 'Settings write tests failed.' }
& $testExe $dataPath read 2>&1 | Tee-Object -FilePath (Join-Path $artifactPath 'settings-read.log')
if ($LASTEXITCODE -ne 0) { throw 'Settings restart tests failed.' }
Write-Output "Settings data: $dataPath"
