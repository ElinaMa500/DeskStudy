param([string]$OutputDirectory = 'release-1.3')
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$releasePath = Join-Path $projectRoot $OutputDirectory
New-Item -ItemType Directory -Force -Path $releasePath | Out-Null
$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object FullName
$buildArgs = @('/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/codepage:65001', ('/out:' + (Join-Path $releasePath 'DeskStudy.exe')), ('/win32manifest:' + (Join-Path $projectRoot 'src\app.manifest')), '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll', '/reference:System.Web.Extensions.dll')
$iconPath = Join-Path $projectRoot 'src\app.ico'
if (Test-Path -LiteralPath $iconPath) { $buildArgs += '/win32icon:' + $iconPath }
& $compilerPath @buildArgs @sourceFiles
if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\DeskStudy.exe.config') -Destination $releasePath -Force
Write-Output (Join-Path $releasePath 'DeskStudy.exe')
