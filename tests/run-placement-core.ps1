$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$exe = Join-Path $projectRoot 'artifacts\PlacementCoreTests.exe'
& $compilerPath /nologo /target:exe /codepage:65001 ('/out:' + $exe) /reference:System.Drawing.dll (Join-Path $projectRoot 'src\PlacementLogic.cs') (Join-Path $PSScriptRoot 'PlacementCoreTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Placement core test compilation failed.' }
& $exe 2>&1 | Tee-Object -FilePath (Join-Path $projectRoot 'artifacts\placement-core.log')
if ($LASTEXITCODE -ne 0) { throw 'Placement core tests failed.' }
