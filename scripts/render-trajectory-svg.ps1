param(
    [string]$InputCsv = "results/single_trajectory.csv",
    [string]$OutputSvg = "results/single_trajectory.svg"
)

$ErrorActionPreference = "Stop"
$culture = [System.Globalization.CultureInfo]::InvariantCulture

if (-not (Test-Path $InputCsv)) {
    throw "Input CSV not found: $InputCsv"
}

$raw = Import-Csv $InputCsv
if ($raw.Count -lt 2) {
    throw "CSV must contain at least 2 data points."
}

$data = foreach ($row in $raw) {
    [pscustomobject]@{
        t = [double]::Parse($row.t_s, $culture)
        temp = [double]::Parse($row.temp_c, $culture)
    }
}

$w = 1080.0
$h = 620.0
$left = 90.0
$right = 40.0
$top = 40.0
$bottom = 90.0
$plotW = $w - $left - $right
$plotH = $h - $top - $bottom

$tMin = ($data | Measure-Object -Property t -Minimum).Minimum
$tMax = ($data | Measure-Object -Property t -Maximum).Maximum
$tempMin = [math]::Floor((($data | Measure-Object -Property temp -Minimum).Minimum - 5.0) / 5.0) * 5.0
$tempMax = [math]::Ceiling((($data | Measure-Object -Property temp -Maximum).Maximum + 5.0) / 5.0) * 5.0

function MapX([double]$t) {
    if ([math]::Abs($tMax - $tMin) -lt 1e-9) { return $left }
    return $left + (($t - $tMin) / ($tMax - $tMin)) * $plotW
}

function MapY([double]$temp) {
    if ([math]::Abs($tempMax - $tempMin) -lt 1e-9) { return $top + $plotH / 2.0 }
    return $top + (($tempMax - $temp) / ($tempMax - $tempMin)) * $plotH
}

$points = ($data | ForEach-Object {
    $x = MapX $_.t
    $y = MapY $_.temp
    [string]::Format($culture, "{0:F2},{1:F2}", $x, $y)
}) -join " "

$xTicks = 0..6 | ForEach-Object { $tMin + ($_ * ($tMax - $tMin) / 6.0) }
$yTicks = 0..6 | ForEach-Object { $tempMin + ($_ * ($tempMax - $tempMin) / 6.0) }

$target = -12.0
$freezeLine = 0.0
$targetY = MapY $target
$freezeY = MapY $freezeLine

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('<?xml version="1.0" encoding="UTF-8"?>')
[void]$sb.AppendLine([string]::Format($culture, '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {0} {1}" width="{0}" height="{1}">', [int]$w, [int]$h))
[void]$sb.AppendLine('  <defs>')
[void]$sb.AppendLine('    <linearGradient id="bg" x1="0" y1="0" x2="0" y2="1">')
[void]$sb.AppendLine('      <stop offset="0%" stop-color="#0f172a"/>')
[void]$sb.AppendLine('      <stop offset="100%" stop-color="#111827"/>')
[void]$sb.AppendLine('    </linearGradient>')
[void]$sb.AppendLine('    <linearGradient id="line" x1="0" y1="0" x2="1" y2="0">')
[void]$sb.AppendLine('      <stop offset="0%" stop-color="#22d3ee"/>')
[void]$sb.AppendLine('      <stop offset="100%" stop-color="#10b981"/>')
[void]$sb.AppendLine('    </linearGradient>')
[void]$sb.AppendLine('  </defs>')
[void]$sb.AppendLine([string]::Format($culture, '  <rect x="0" y="0" width="{0}" height="{1}" fill="url(#bg)" rx="18"/>', [int]$w, [int]$h))
[void]$sb.AppendLine([string]::Format($culture, '  <rect x="{0:F2}" y="{1:F2}" width="{2:F2}" height="{3:F2}" fill="#0b1220" stroke="#334155" stroke-width="1.2" rx="10"/>', $left, $top, $plotW, $plotH))

foreach ($t in $xTicks) {
    $x = MapX $t
    [void]$sb.AppendLine([string]::Format($culture, '  <line x1="{0:F2}" y1="{1:F2}" x2="{0:F2}" y2="{2:F2}" stroke="#1f2937" stroke-width="1"/>', $x, $top, ($top + $plotH)))
    [void]$sb.AppendLine([string]::Format($culture, '  <text x="{0:F2}" y="{1:F2}" fill="#cbd5e1" font-size="13" text-anchor="middle">{2:F0}</text>', $x, ($top + $plotH + 24), $t))
}

foreach ($temp in $yTicks) {
    $y = MapY $temp
    [void]$sb.AppendLine([string]::Format($culture, '  <line x1="{0:F2}" y1="{1:F2}" x2="{2:F2}" y2="{1:F2}" stroke="#1f2937" stroke-width="1"/>', $left, $y, ($left + $plotW)))
    [void]$sb.AppendLine([string]::Format($culture, '  <text x="{0:F2}" y="{1:F2}" fill="#cbd5e1" font-size="13" text-anchor="end">{2:F0}</text>', ($left - 12), ($y + 4), $temp))
}

[void]$sb.AppendLine([string]::Format($culture, '  <line x1="{0:F2}" y1="{1:F2}" x2="{2:F2}" y2="{1:F2}" stroke="#f59e0b" stroke-width="1.6" stroke-dasharray="7 5"/>', $left, $freezeY, ($left + $plotW)))
[void]$sb.AppendLine([string]::Format($culture, '  <line x1="{0:F2}" y1="{1:F2}" x2="{2:F2}" y2="{1:F2}" stroke="#ef4444" stroke-width="1.6" stroke-dasharray="7 5"/>', $left, $targetY, ($left + $plotW)))

[void]$sb.AppendLine([string]::Format($culture, '  <polyline fill="none" stroke="url(#line)" stroke-width="3.2" points="{0}"/>', $points))

[void]$sb.AppendLine([string]::Format($culture, '  <text x="{0:F2}" y="28" fill="#e2e8f0" font-size="22" font-weight="700">DE-Thermo: Cooling and Freezing Trajectory</text>', $left))
[void]$sb.AppendLine([string]::Format($culture, '  <text x="{0:F2}" y="{1:F2}" fill="#93c5fd" font-size="14">x-axis: time (s)</text>', ($left + $plotW - 140), ($top + $plotH + 55)))
[void]$sb.AppendLine([string]::Format($culture, '  <text x="30" y="{0:F2}" fill="#93c5fd" font-size="14" transform="rotate(-90 30,{0:F2})">temperature (C)</text>', ($top + $plotH / 2.0)))
[void]$sb.AppendLine([string]::Format($culture, '  <text x="{0:F2}" y="{1:F2}" fill="#f59e0b" font-size="13">0 C freeze line</text>', ($left + 12), ($freezeY - 8)))
[void]$sb.AppendLine([string]::Format($culture, '  <text x="{0:F2}" y="{1:F2}" fill="#ef4444" font-size="13">-12 C target</text>', ($left + 12), ($targetY - 8)))
[void]$sb.AppendLine('</svg>')

$parent = Split-Path -Parent $OutputSvg
if ($parent) {
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
}

$sb.ToString() | Set-Content -Encoding UTF8 $OutputSvg
Write-Output "Wrote $OutputSvg"
