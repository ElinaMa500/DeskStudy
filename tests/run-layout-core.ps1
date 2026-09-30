$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$exe = Join-Path $projectRoot 'artifacts\NotebookLayoutCoreTests.exe'
& $compilerPath /nologo /target:exe /codepage:65001 ('/out:' + $exe) /reference:System.Web.Extensions.dll (Join-Path $projectRoot 'src\Core.cs') (Join-Path $projectRoot 'src\SettingsModel.cs') (Join-Path $PSScriptRoot 'NotebookLayoutCoreTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Layout core test compilation failed.' }
& $exe 2>&1 | Tee-Object -FilePath (Join-Path $projectRoot 'artifacts\layout-core.log')
if ($LASTEXITCODE -ne 0) { throw 'Layout core tests failed.' }
