$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$exe = Join-Path $projectRoot 'artifacts\DueCoreTests.exe'
& $compilerPath /nologo /target:exe /codepage:65001 ('/out:' + $exe) /reference:System.Web.Extensions.dll (Join-Path $projectRoot 'src\Core.cs') (Join-Path $projectRoot 'src\SettingsModel.cs') (Join-Path $PSScriptRoot 'DueCoreTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Due core test compilation failed.' }
& $exe 2>&1 | Tee-Object -FilePath (Join-Path $projectRoot 'artifacts\due-core.log')
if ($LASTEXITCODE -ne 0) { throw 'Due core tests failed.' }
