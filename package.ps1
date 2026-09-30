# Builds the app and produces the portable zip: the program, its config, and the user guide with images.
# -SkipBuild packages the existing exe, for when it is running and cannot be overwritten.
param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$version = '1.5.1'
if (-not $SkipBuild) { & (Join-Path $projectRoot 'build.ps1') }
$releasePath = Join-Path $projectRoot "release-$version"
foreach ($name in 'README.md', 'LAYOUTS.md', 'VERIFICATION.md', 'DEVELOPMENT.md', 'USER_GUIDE.md') {
    Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination (Join-Path $releasePath $name) -Force
}
$screenshotPath = Join-Path $projectRoot 'docs\screenshots'
foreach ($name in 'original-desktop.png', 'card-desktop.png', 'paper-desktop.png', 'clean-desktop.png', 'journal-desktop.png', 'settings-desktop.png') {
    if (-not (Test-Path -LiteralPath (Join-Path $screenshotPath $name))) { throw "Missing layout screenshot: $name" }
}
# The zip is what end users receive: keep it to the program and a guide they can open by double-clicking.
$stage = Join-Path $releasePath 'package'
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'images') | Out-Null
Copy-Item -LiteralPath (Join-Path $releasePath 'DeskStudy.exe'), (Join-Path $releasePath 'DeskStudy.exe.config') -Destination $stage
# Only the images the guide shows; docs\images also holds the landing page's screenshots.
$guideText = [IO.File]::ReadAllText((Join-Path $projectRoot 'USER_GUIDE.md'), [Text.Encoding]::UTF8)
foreach ($match in [regex]::Matches($guideText, 'docs/images/([\w\-]+\.png)')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot ('docs\images\' + $match.Groups[1].Value)) -Destination (Join-Path $stage 'images') -Force
}
$guideName = -join ([char[]](0x4F7F, 0x7528, 0x6307, 0x5357)) + '.html'   # "使用指南.html"
& (Join-Path $projectRoot 'tools\build-guide.ps1') -Source (Join-Path $projectRoot 'USER_GUIDE.md') -Output (Join-Path $stage $guideName) | Out-Null
$zipPath = Join-Path $projectRoot "DeskStudy-$version-Windows.zip"
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zipPath
Get-FileHash -LiteralPath $zipPath -Algorithm SHA256 | Format-List
