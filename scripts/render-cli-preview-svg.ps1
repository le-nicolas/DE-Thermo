param(
    [string]$InputText = "results/cli_preview.txt",
    [string]$OutputSvg = "results/cli_preview.svg"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $InputText)) {
    throw "Input text not found: $InputText"
}

$lines = Get-Content $InputText
if ($lines.Count -eq 0) {
    $lines = @("No output captured.")
}

function EscapeXml([string]$text) {
    return $text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
}

$fontSize = 18
$lineGap = 30
$left = 28
$top = 64
$header = 42
$width = 1220
$height = $top + ($lines.Count * $lineGap) + 36

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('<?xml version="1.0" encoding="UTF-8"?>')
[void]$sb.AppendLine("<svg xmlns=`"http://www.w3.org/2000/svg`" viewBox=`"0 0 $width $height`" width=`"$width`" height=`"$height`">")
[void]$sb.AppendLine("  <defs>")
[void]$sb.AppendLine("    <linearGradient id=`"terminalBg`" x1=`"0`" y1=`"0`" x2=`"0`" y2=`"1`">")
[void]$sb.AppendLine("      <stop offset=`"0%`" stop-color=`"#111827`"/>")
[void]$sb.AppendLine("      <stop offset=`"100%`" stop-color=`"#020617`"/>")
[void]$sb.AppendLine("    </linearGradient>")
[void]$sb.AppendLine("  </defs>")
[void]$sb.AppendLine("  <rect x=`"0`" y=`"0`" width=`"$width`" height=`"$height`" fill=`"url(#terminalBg)`" rx=`"18`"/>")
[void]$sb.AppendLine("  <rect x=`"0`" y=`"0`" width=`"$width`" height=`"$header`" fill=`"#0f172a`" rx=`"18`"/>")
[void]$sb.AppendLine("  <circle cx=`"24`" cy=`"21`" r=`"6.5`" fill=`"#ef4444`"/>")
[void]$sb.AppendLine("  <circle cx=`"46`" cy=`"21`" r=`"6.5`" fill=`"#f59e0b`"/>")
[void]$sb.AppendLine("  <circle cx=`"68`" cy=`"21`" r=`"6.5`" fill=`"#22c55e`"/>")
[void]$sb.AppendLine("  <text x=`"96`" y=`"27`" fill=`"#cbd5e1`" font-size=`"15`" font-family=`"Consolas, Menlo, monospace`">de-thermo simulate preview</text>")

for ($i = 0; $i -lt $lines.Count; $i++) {
    $y = $top + ($i * $lineGap)
    $text = EscapeXml $lines[$i]
    [void]$sb.AppendLine("  <text x=`"$left`" y=`"$y`" fill=`"#e2e8f0`" font-size=`"$fontSize`" font-family=`"Consolas, Menlo, monospace`">$text</text>")
}

[void]$sb.AppendLine("</svg>")

$parent = Split-Path -Parent $OutputSvg
if ($parent) {
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
}

$sb.ToString() | Set-Content -Encoding UTF8 $OutputSvg
Write-Output "Wrote $OutputSvg"
