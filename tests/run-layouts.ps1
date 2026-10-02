param([switch]$Render, [switch]$CardOnly, [switch]$CompileOnly, [switch]$Due, [switch]$Frame, [switch]$Place, [switch]$CalendarStyle, [switch]$Fold, [switch]$InlineEdit, [switch]$Language)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$artifactPath = Join-Path $projectRoot 'artifacts'
New-Item -ItemType Directory -Force -Path $artifactPath | Out-Null
$testExe = Join-Path $artifactPath 'NotebookLayoutTests.exe'
$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object FullName
$buildArgs = @('/nologo', '/target:exe', '/main:NotebookLayoutTests', '/codepage:65001', ('/out:' + $testExe), ('/win32manifest:' + (Join-Path $projectRoot 'src\app.manifest')), '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll', '/reference:System.Web.Extensions.dll')
& $compilerPath @buildArgs @sourceFiles (Join-Path $PSScriptRoot 'NotebookLayoutTests.cs') (Join-Path $PSScriptRoot 'PlacementChecks.cs') (Join-Path $PSScriptRoot 'CalendarStyleChecks.cs') (Join-Path $PSScriptRoot 'FoldChecks.cs') (Join-Path $PSScriptRoot 'InlineEditChecks.cs') (Join-Path $PSScriptRoot 'LangChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Notebook layout test compilation failed.' }
if ($CompileOnly) { Write-Output "Compiled: $testExe"; exit 0 }
$dataPath = Join-Path $artifactPath ('layouts-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
if ($Due) { & $testExe $dataPath due 2>&1 | Tee-Object -FilePath (Join-Path $artifactPath 'layouts-due.log'); if ($LASTEXITCODE -ne 0) { throw 'Notebook deadline picker checks failed.' }; exit 0 }
if ($Frame) { & $testExe $dataPath frame 2>&1 | Tee-Object -FilePath (Join-Path $artifactPath 'layouts-frame.log'); if ($LASTEXITCODE -ne 0) { throw 'Desktop widget mode checks failed.' }; exit 0 }
if ($Language) { & $testExe $dataPath lang 2>&1 | Tee-Object -FilePath (Join-Path $artifactPath 'layouts-lang.log'); if ($LASTEXITCODE -ne 0) { throw 'Language checks failed.' }; & $testExe ($dataPath + '-en') langfresh 2>&1 | Tee-Object -FilePath (Join-Path $artifactPath 'layouts-langfresh.log'); if ($LASTEXITCODE -ne 0) { throw 'English first-run check failed.' }; exit 0 }
if ($InlineEdit) { & $testExe $dataPath inline 2>&1 | Tee-Object -FilePath (Join-Path $artifactPath 'layouts-inline.log'); if ($LASTEXITCODE -ne 0) { throw 'Inline edit checks failed.' }; & $testExe $dataPath inlineread 2>&1 | Tee-Object -FilePath (Join-Path $artifactPath 'layouts-inlineread.log'); if ($LASTEXITCODE -ne 0) { throw 'Inline edit restart check failed.' }; exit 0 }
if ($Fold) { & $testExe $dataPath fold 2>&1 | Tee-Object -FilePath (Join-Path $artifactPath 'layouts-fold.log'); if ($LASTEXITCODE -ne 0) { throw 'Fold checks failed.' }; & $testExe $dataPath foldread 2>&1 | Tee-Object -FilePath (Join-Path $artifactPath 'layouts-foldread.log'); if ($LASTEXITCODE -ne 0) { throw 'Fold restart check failed.' }; exit 0 }
if ($CalendarStyle) { & $testExe $dataPath calstyle 2>&1 | Tee-Object -FilePath (Join-Path $artifactPath 'layouts-calstyle.log'); if ($LASTEXITCODE -ne 0) { throw 'Calendar style checks failed.' }; & $testExe $dataPath calstyleread 2>&1 | Tee-Object -FilePath (Join-Path $artifactPath 'layouts-calstyleread.log'); if ($LASTEXITCODE -ne 0) { throw 'Calendar style restart check failed.' }; exit 0 }
if ($Place) { & $testExe $dataPath place 2>&1 | Tee-Object -FilePath (Join-Path $artifactPath 'layouts-place.log'); if ($LASTEXITCODE -ne 0) { throw 'Widget placement checks failed.' }; & $testExe $dataPath placeread 2>&1 | Tee-Object -FilePath (Join-Path $artifactPath 'layouts-placeread.log'); if ($LASTEXITCODE -ne 0) { throw 'Widget placement restart check failed.' }; exit 0 }
if ($Render) { & $testExe $dataPath render 2>&1 | Tee-Object -FilePath (Join-Path $artifactPath 'layouts-render.log'); if ($LASTEXITCODE -ne 0) { throw 'Notebook layout rendering failed.' }; exit 0 }
if ($CardOnly) { & $testExe $dataPath card 2>&1 | Tee-Object -FilePath (Join-Path $artifactPath 'layouts-card.log'); if ($LASTEXITCODE -ne 0) { throw 'Notebook card stage failed.' }; exit 0 }
& $testExe $dataPath write 2>&1 | Tee-Object -FilePath (Join-Path $artifactPath 'layouts-write.log')
if ($LASTEXITCODE -ne 0) { throw 'Notebook layout write phase failed.' }
& $testExe $dataPath read 2>&1 | Tee-Object -FilePath (Join-Path $artifactPath 'layouts-read.log')
if ($LASTEXITCODE -ne 0) { throw 'Notebook layout restart phase failed.' }
Write-Output "Notebook layout data: $dataPath"
