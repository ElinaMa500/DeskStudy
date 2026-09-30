param([switch]$Render)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$artifactPath = Join-Path $projectRoot 'artifacts'
New-Item -ItemType Directory -Force -Path $artifactPath | Out-Null
$testExe = Join-Path $artifactPath 'IntegrationTests.exe'
$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object FullName
$buildArgs = @('/nologo', '/target:exe', '/main:IntegrationTests', '/codepage:65001', ('/out:' + $testExe), ('/win32manifest:' + (Join-Path $projectRoot 'src\app.manifest')), '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll', '/reference:System.Web.Extensions.dll')
& $compilerPath @buildArgs @sourceFiles (Join-Path $PSScriptRoot 'IntegrationTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Integration test compilation failed.' }
$dataPath = Join-Path $artifactPath ('integration-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
if ($Render) {
    $renderPath = Join-Path $artifactPath ('render-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    & $testExe $renderPath render
    if ($LASTEXITCODE -ne 0) { throw 'Rendering failed.' }
    exit 0
}
& $testExe $dataPath write 2>&1 | Tee-Object -FilePath (Join-Path $artifactPath 'integration-write.log')
if ($LASTEXITCODE -ne 0) { throw 'Integration write phase failed.' }
& $testExe $dataPath read 2>&1 | Tee-Object -FilePath (Join-Path $artifactPath 'integration-read.log')
if ($LASTEXITCODE -ne 0) { throw 'Integration restart phase failed.' }
Write-Output "Integration data: $dataPath"
