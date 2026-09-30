param([string]$Layout = 'Card')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$previewPath = Join-Path $projectRoot 'artifacts\layout-preview-data'
$previewApp = Join-Path $projectRoot 'artifacts\layout-preview-app'
New-Item -ItemType Directory -Force -Path $previewPath,$previewApp | Out-Null
$fixture = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'artifacts') -Directory -Filter 'layouts-*' | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'card-todo.png') } | Sort-Object Name -Descending | Select-Object -First 1
if (-not $fixture) { throw 'Run the isolated layout rendering first.' }
$data = Get-Content -LiteralPath (Join-Path $fixture.FullName 'data.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$data.Settings.NotebookLayout = $Layout
$data.Settings.GlobalAppearance.FontSize = 9
$data.Windows.calendar.Visible = $false
$index = 0
foreach ($key in @('todo','ddl')) {
    $book = $data.Books | Where-Object { $_.Id -eq $key }
    $book.CurrentPageId = $book.Pages[0].Id
    $data.Windows.$key.Visible = $true
    $data.Windows.$key.TopMost = $false
    $data.Windows.$key.PositionLocked = $false
    $data.Windows.$key.X = 60 + $index * 520
    $data.Windows.$key.Y = 70
    $data.Windows.$key.Width = 500
    $data.Windows.$key.Height = 760
    $index++
}
$data | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $previewPath 'data.json') -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $projectRoot 'release-1.3\DeskStudy.exe'),(Join-Path $projectRoot 'release-1.3\DeskStudy.exe.config') -Destination $previewApp -Force
Start-Process -FilePath (Join-Path $previewApp 'DeskStudy.exe') -ArgumentList @('--data-dir', ('"' + $previewPath + '"')) -WindowStyle Normal -PassThru | Select-Object Id,ProcessName
