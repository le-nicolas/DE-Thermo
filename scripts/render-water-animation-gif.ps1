param(
    [string]$InputJson = "results/single_run.json",
    [string]$OutputGif = "results/water_animation.gif",
    [int]$Width = 720,
    [int]$Height = 420,
    [int]$Frames = 72,
    [int]$FrameDelayCs = 7
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Runtime.Serialization

if (-not (Test-Path $InputJson)) {
    throw "Input JSON not found: $InputJson"
}

$sim = Get-Content -Raw $InputJson | ConvertFrom-Json
if (-not $sim.points -or $sim.points.Count -lt 2) {
    throw "Input JSON has insufficient point data."
}

$scenario = $sim.scenario
$points = $sim.points
$milestones = $sim.milestones
$pointCount = $points.Count
$culture = [System.Globalization.CultureInfo]::InvariantCulture

function Clamp([double]$v, [double]$a, [double]$b) {
    if ($v -lt $a) { return $a }
    if ($v -gt $b) { return $b }
    return $v
}

function Normalize([double]$v, [double]$a, [double]$b) {
    if ([math]::Abs($b - $a) -lt 1e-9) { return 0.0 }
    return Clamp (($v - $a) / ($b - $a)) 0.0 1.0
}

function BlendColor($a, $b, [double]$t) {
    $x = Clamp $t 0.0 1.0
    return [System.Drawing.Color]::FromArgb(
        [int]($a.A + ($b.A - $a.A) * $x),
        [int]($a.R + ($b.R - $a.R) * $x),
        [int]($a.G + ($b.G - $a.G) * $x),
        [int]($a.B + ($b.B - $a.B) * $x))
}

function FreezeFraction([double]$timeS, [double]$tempC, $ms) {
    if ($tempC -gt 0.0) { return 0.0 }
    $zero = $ms.reaches_zero_s
    $freeze = $ms.freeze_complete_s
    if ($null -ne $zero -and $null -ne $freeze -and $freeze -gt ($zero + 1e-9)) {
        if ($timeS -le $zero) { return 0.0 }
        if ($timeS -ge $freeze) { return 1.0 }
        return Clamp (($timeS - $zero) / ($freeze - $zero)) 0.0 1.0
    }
    if ($tempC -le 0.0) { return 1.0 }
    return 0.0
}

function MakePropertyItem([int]$id, [int]$type, [byte[]]$value) {
    $pi = [System.Runtime.Serialization.FormatterServices]::GetUninitializedObject([System.Drawing.Imaging.PropertyItem])
    $pi.Id = $id
    $pi.Type = $type
    $pi.Len = $value.Length
    $pi.Value = $value
    return $pi
}

$bitmaps = New-Object System.Collections.Generic.List[System.Drawing.Bitmap]
$hotColor = [System.Drawing.Color]::FromArgb(250, 125, 55)
$coldColor = [System.Drawing.Color]::FromArgb(37, 99, 235)
$textBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(30, 41, 59))
$smallFont = New-Object System.Drawing.Font("Segoe UI", 11, [System.Drawing.FontStyle]::Bold)
$tinyFont = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Regular)

for ($f = 0; $f -lt $Frames; $f++) {
    $idx = [int][math]::Round(($f * ($pointCount - 1)) / [double]($Frames - 1))
    $idx = [int](Clamp $idx 0 ($pointCount - 1))
    $pt = $points[$idx]
    $temp = [double]$pt.temp_c
    $timeS = [double]$pt.t_s
    $freeze = FreezeFraction $timeS $temp $milestones

    $bmp = New-Object System.Drawing.Bitmap $Width, $Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    $bgRect = [System.Drawing.RectangleF]::new(0, 0, $Width, $Height)
    $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $bgRect,
        [System.Drawing.Color]::FromArgb(224, 242, 254),
        [System.Drawing.Color]::FromArgb(226, 232, 240),
        [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $g.FillRectangle($bgBrush, $bgRect)
    $bgBrush.Dispose()

    $areaFactor = Clamp (([double]$scenario.area_m2 - 0.01) / 0.10) 0.0 1.0
    $fillRatio = Clamp (0.30 + [double]$scenario.mass_kg / 1.20) 0.35 0.90
    $cupWidth = 180 + $areaFactor * 90
    $cupHeight = 250
    $cupX = $Width * 0.44 - $cupWidth * 0.5
    $cupY = $Height * 0.53 - $cupHeight * 0.5
    $wall = 8

    $outer = [System.Drawing.RectangleF]::new([single]$cupX, [single]$cupY, [single]$cupWidth, [single]$cupHeight)
    $inner = [System.Drawing.RectangleF]::new([single]($cupX + $wall), [single]($cupY + $wall), [single]($cupWidth - 2 * $wall), [single]($cupHeight - 2 * $wall))
    $fluidHeight = $inner.Height * $fillRatio
    $fluidTop = $inner.Bottom - $fluidHeight
    $iceHeight = $fluidHeight * $freeze
    $liquidTop = $fluidTop + $iceHeight

    $tempNorm = Normalize $temp -20 ([math]::Max(90, [double]$scenario.initial_temp_c))
    $waterColor = BlendColor $coldColor $hotColor $tempNorm
    $waterTop = BlendColor ([System.Drawing.Color]::White) $waterColor 0.45
    $waterBottom = BlendColor $waterColor ([System.Drawing.Color]::FromArgb(15, 23, 42)) 0.25

    $agitation = Clamp (0.20 + [double]$scenario.htc_w_m2k / 26.0 + [math]::Abs([double]$scenario.initial_temp_c - [double]$scenario.ambient_temp_c) / 180.0) 0.2 1.8
    $waveAmp = 2.5 + 7.0 * $agitation * (1.0 - $freeze)
    $wavePhase = $f * 0.35 + [double]$scenario.area_m2 * 20.0
    $waveCycles = 2.0 + $areaFactor * 1.4

    if ($liquidTop -lt ($inner.Bottom - 2)) {
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $segments = 46
        $wavePoints = New-Object 'System.Collections.Generic.List[System.Drawing.PointF]'
        for ($i = 0; $i -le $segments; $i++) {
            $x = $inner.Left + $i * ($inner.Width / $segments)
            $t = $i / [double]$segments
            $wave = [math]::Sin($t * $waveCycles * [math]::PI * 2.0 + $wavePhase)
            $y = $liquidTop + $wave * $waveAmp
            $wavePoints.Add([System.Drawing.PointF]::new([single]$x, [single]$y))
        }

        $path.StartFigure()
        $path.AddLine([single]$inner.Left, [single]$inner.Bottom, [single]$inner.Left, [single]$wavePoints[0].Y)
        $path.AddLines($wavePoints.ToArray())
        $path.AddLine([single]$inner.Right, [single]$wavePoints[$wavePoints.Count - 1].Y, [single]$inner.Right, [single]$inner.Bottom)
        $path.CloseFigure()

        $wb = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            [System.Drawing.PointF]::new([single]$inner.Left, [single]$fluidTop),
            [System.Drawing.PointF]::new([single]$inner.Left, [single]$inner.Bottom),
            $waterTop,
            $waterBottom)
        $g.FillPath($wb, $path)
        $wb.Dispose()
        $path.Dispose()
    }

    if ($freeze -gt 0.0) {
        $iceRect = [System.Drawing.RectangleF]::new([single]$inner.Left, [single]$fluidTop, [single]$inner.Width, [single]$iceHeight)
        $iceBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(175, 230, 245, 255))
        $g.FillRectangle($iceBrush, $iceRect)
        $iceBrush.Dispose()

        $crackPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(130, 148, 163, 184), 1.0)
        $crackCount = 3 + [int]($freeze * 8.0)
        for ($i = 0; $i -lt $crackCount; $i++) {
            $x = $iceRect.Left + ($i + 1) * ($iceRect.Width / ($crackCount + 1))
            $drift = [math]::Sin($i * 1.7 + $f * 0.08) * 6.0
            $g.DrawLine($crackPen, [single]$x, [single]($iceRect.Top + 2), [single]($x + $drift), [single]([math]::Max($iceRect.Top + 4, $iceRect.Bottom - 2)))
        }
        $crackPen.Dispose()
    }

    if ($temp -gt 45 -and $freeze -lt 0.1) {
        $steamPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(95, 203, 213, 225), 2.0)
        for ($i = 0; $i -lt 3; $i++) {
            $x = $outer.Left + $outer.Width * (0.25 + $i * 0.22)
            $yTop = $outer.Top - 58
            $yBottom = $outer.Top + 8
            $x1 = $x + [math]::Sin(($f + $i * 12) * 0.10) * 8.0
            $x2 = $x + [math]::Sin(($f + $i * 17) * 0.09) * 11.0
            $g.DrawBezier(
                $steamPen,
                [single]$x,
                [single]$yBottom,
                [single]($x1 - 10.0),
                [single]($yBottom - 20.0),
                [single]($x2 + 10.0),
                [single]($yTop + 20.0),
                [single]$x2,
                [single]$yTop)
        }
        $steamPen.Dispose()
    }

    $glassPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(84, 100, 116, 139), 3.0)
    $rimPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(140, 148, 163, 184), 2.0)
    $g.DrawRectangle($glassPen, [int]$outer.Left, [int]$outer.Top, [int]$outer.Width, [int]$outer.Height)
    $g.DrawRectangle($rimPen, [int]$inner.Left, [int]$inner.Top, [int]$inner.Width, [int]$inner.Height)
    $glassPen.Dispose()
    $rimPen.Dispose()

    $thermo = [System.Drawing.RectangleF]::new([single]($Width - 82), [single]64, [single]24, [single]($Height - 128))
    $tubeBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(241, 245, 249))
    $tubePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(100, 116, 139), 2.0)
    $g.FillRectangle($tubeBrush, $thermo)
    $g.DrawRectangle($tubePen, [int]$thermo.Left, [int]$thermo.Top, [int]$thermo.Width, [int]$thermo.Height)
    $tubeBrush.Dispose()
    $tubePen.Dispose()

    $norm = Normalize $temp -20 ([math]::Max(90, [double]$scenario.initial_temp_c))
    $fillH = $thermo.Height * $norm
    $fillRect = [System.Drawing.RectangleF]::new([single]($thermo.Left + 3), [single]($thermo.Bottom - $fillH + 1), [single]($thermo.Width - 6), [single]([math]::Max(2, $fillH - 2)))
    $fillColor = BlendColor ([System.Drawing.Color]::FromArgb(59, 130, 246)) ([System.Drawing.Color]::FromArgb(239, 68, 68)) $norm
    $fillBrush = New-Object System.Drawing.SolidBrush $fillColor
    $g.FillRectangle($fillBrush, $fillRect)
    $fillBrush.Dispose()

    $freezePct = [math]::Round($freeze * 100)
    $g.DrawString("Scenario: $($scenario.name)", $smallFont, $textBrush, 16, $Height - 72)
    $g.DrawString(("t = {0:F0} s | T = {1:F1} C | freeze = {2:F0}%" -f $timeS, $temp, $freezePct), $tinyFont, $textBrush, 16, $Height - 46)

    $g.Dispose()
    $bitmaps.Add($bmp)
}

if ($bitmaps.Count -lt 2) {
    throw "Could not produce enough frames for animation."
}

$first = $bitmaps[0]
$delayBytes = New-Object byte[] ($Frames * 4)
for ($i = 0; $i -lt $Frames; $i++) {
    $offset = $i * 4
    $delayBytes[$offset] = [byte]$FrameDelayCs
    $delayBytes[$offset + 1] = 0
    $delayBytes[$offset + 2] = 0
    $delayBytes[$offset + 3] = 0
}
$loopBytes = [byte[]](0, 0)

$first.SetPropertyItem((MakePropertyItem 0x5100 4 $delayBytes))
$first.SetPropertyItem((MakePropertyItem 0x5101 3 $loopBytes))

$codec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq "image/gif" } | Select-Object -First 1
$saveFlag = [System.Drawing.Imaging.Encoder]::SaveFlag
$ep = [System.Drawing.Imaging.EncoderParameters]::new(1)
$ep.Param[0] = [System.Drawing.Imaging.EncoderParameter]::new($saveFlag, [long][System.Drawing.Imaging.EncoderValue]::MultiFrame)

$parent = Split-Path -Parent $OutputGif
if ($parent) {
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
}

$first.Save($OutputGif, $codec, $ep)
for ($i = 1; $i -lt $bitmaps.Count; $i++) {
    $ep.Param[0] = [System.Drawing.Imaging.EncoderParameter]::new($saveFlag, [long][System.Drawing.Imaging.EncoderValue]::FrameDimensionTime)
    $first.SaveAdd($bitmaps[$i], $ep)
}
$ep.Param[0] = [System.Drawing.Imaging.EncoderParameter]::new($saveFlag, [long][System.Drawing.Imaging.EncoderValue]::Flush)
$first.SaveAdd($ep)

foreach ($b in $bitmaps) { $b.Dispose() }
$smallFont.Dispose()
$tinyFont.Dispose()
$textBrush.Dispose()

Write-Output "Wrote $OutputGif"
