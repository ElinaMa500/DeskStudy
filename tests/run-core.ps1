$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$artifactPath = Join-Path $projectRoot 'artifacts'
New-Item -ItemType Directory -Force -Path $artifactPath | Out-Null
$testExe = Join-Path $artifactPath 'CoreTests.exe'
& $compilerPath /nologo /target:exe /codepage:65001 ('/out:' + $testExe) /reference:System.Web.Extensions.dll (Join-Path $projectRoot 'src\Core.cs') (Join-Path $projectRoot 'src\Lang.cs') (Join-Path $projectRoot 'src\Lang.en.cs') (Join-Path $projectRoot 'src\Outlook.cs') (Join-Path $projectRoot 'src\Ics.cs') (Join-Path $projectRoot 'src\SettingsModel.cs') (Join-Path $PSScriptRoot 'CoreTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Core test compilation failed.' }
& $testExe 2>&1 | Tee-Object -FilePath (Join-Path $artifactPath 'core-tests.log')
if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
