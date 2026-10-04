# Converts the user guide (a small Markdown subset) into one standalone HTML page for the release zip.
param([Parameter(Mandatory = $true)][string]$Source, [Parameter(Mandatory = $true)][string]$Output, [string]$ImagePrefixFrom = 'docs/images/', [string]$ImagePrefixTo = 'images/',
      [string]$Language = 'zh-CN', [hashtable]$Links = @{})
$ErrorActionPreference = 'Stop'
function Escape([string]$text) { $text.Replace('&', '&amp;').Replace('<', '&lt;').Replace('>', '&gt;') }
function Inline([string]$text) {
    $t = Escape $text
    $t = [regex]::Replace($t, '!\[([^\]]*)\]\(([^)]+)\)', { param($m) '<img alt="' + $m.Groups[1].Value + '" src="' + $m.Groups[2].Value.Replace($ImagePrefixFrom, $ImagePrefixTo) + '">' })
    # Links; the other guide's Markdown file becomes its HTML page in the zip.
    $t = [regex]::Replace($t, '\[([^\]]+)\]\(([^)]+)\)', { param($m) $target = $m.Groups[2].Value; if ($Links.ContainsKey($target)) { $target = $Links[$target] }; '<a href="' + $target + '">' + $m.Groups[1].Value + '</a>' })
    $t = [regex]::Replace($t, '\*\*([^*]+)\*\*', '<strong>$1</strong>')
    [regex]::Replace($t, '`([^`]+)`', '<code>$1</code>')
}
$lines = [IO.File]::ReadAllLines($Source, [Text.Encoding]::UTF8)
$html = New-Object System.Collections.Generic.List[string]
$title = 'Guide'; $list = ''; $table = $false; $para = New-Object System.Collections.Generic.List[string]
function FlushPara { if ($para.Count) { $html.Add('<p>' + ($para -join '<br>') + '</p>'); $para.Clear() } }
function CloseList { if ($script:list) { $html.Add('</' + $script:list + '>'); $script:list = '' } }
function CloseTable { if ($script:table) { $html.Add('</tbody></table>'); $script:table = $false } }
foreach ($line in $lines) {
    if ($line -match '^\s*$') { FlushPara; CloseList; CloseTable; continue }
    if ($line -match '^-{3,}\s*$') { FlushPara; CloseList; CloseTable; $html.Add('<hr>'); continue }
    if ($line -match '^(#{1,3})\s+(.*)$') {
        FlushPara; CloseList; CloseTable
        $level = $Matches[1].Length; if ($level -eq 1) { $title = $Matches[2] }
        $html.Add("<h$level>" + (Inline $Matches[2]) + "</h$level>"); continue
    }
    if ($line -match '^\|') {
        FlushPara; CloseList
        if ($line -match '^\|\s*-') { continue }
        $cells = $line.Trim().Trim('|').Split('|') | ForEach-Object { $_.Trim() }
        if (-not $table) { $html.Add('<table><thead><tr>' + (($cells | ForEach-Object { '<th>' + (Inline $_) + '</th>' }) -join '') + '</tr></thead><tbody>'); $table = $true }
        else { $html.Add('<tr>' + (($cells | ForEach-Object { '<td>' + (Inline $_) + '</td>' }) -join '') + '</tr>') }
        continue
    }
    if ($line -match '^\s*(-|\d+\.)\s+(.*)$') {
        FlushPara; CloseTable
        $kind = if ($Matches[1] -eq '-') { 'ul' } else { 'ol' }
        if ($list -ne $kind) { CloseList; $html.Add("<$kind>"); $list = $kind }
        $html.Add('<li>' + (Inline $Matches[2]) + '</li>'); continue
    }
    CloseList; CloseTable
    $para.Add((Inline $line))
}
FlushPara; CloseList; CloseTable
$style = @'
body{font-family:"Microsoft YaHei UI","Segoe UI",sans-serif;line-height:1.75;color:#283732;background:#f7f8fb;margin:0}
main{max-width:860px;margin:0 auto;padding:32px 28px 64px;background:#fff;min-height:100vh;box-sizing:border-box}
h1{font-size:28px;margin:0 0 12px}h2{font-size:20px;margin:36px 0 10px;padding-top:14px;border-top:1px solid #e8ece8}h3{font-size:16px;margin:22px 0 6px}
img{max-width:100%;height:auto;border:1px solid #e8ece8;border-radius:8px;margin:8px 0}
table{border-collapse:collapse;width:100%;margin:10px 0}th,td{border:1px solid #e1e6e2;padding:7px 10px;text-align:left;vertical-align:top}th{background:#f3f6f2}
code{background:#f3f6f2;border-radius:4px;padding:1px 5px;font-family:Consolas,"Microsoft YaHei UI",monospace}
li{margin:3px 0}hr{border:0;border-top:1px solid #e8ece8;margin:28px 0}a{color:#3f6f5c}
@media(prefers-color-scheme:dark){body{background:#1d2022;color:#e3e9e5}main{background:#262a2d}h2{border-color:#424844}th{background:#313a33}th,td,img{border-color:#424844}code{background:#313a33}}
'@
$page = "<!doctype html>`n<html lang=`"$Language`"><head><meta charset=`"utf-8`"><meta name=`"viewport`" content=`"width=device-width,initial-scale=1`"><title>" + (Escape $title) + "</title><style>$style</style></head><body><main>`n" + ($html -join "`n") + "`n</main></body></html>`n"
[IO.File]::WriteAllText($Output, $page, (New-Object Text.UTF8Encoding $false))
Write-Output $Output
