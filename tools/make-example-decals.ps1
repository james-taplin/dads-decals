# Generates the example decal PNGs in examples/Decals/<Category>/ (original artwork, drawn here).
# Re-run after editing to regenerate:  .\tools\make-example-decals.ps1
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
$root = Join-Path $PSScriptRoot "..\examples\Decals"

# ---- helpers --------------------------------------------------------------------------------

function global:C([int]$r, [int]$g, [int]$b, [int]$a = 255) { [System.Drawing.Color]::FromArgb($a, $r, $g, $b) }
$Black = C 20 20 20; $White = C 255 255 255; $Yellow = C 250 204 21; $Red = C 200 16 46
$Orange = C 255 120 0; $Blue = C 0 84 166; $Green = C 0 122 61; $Cream = C 238 228 196

function global:Save-Decal([string]$category, [string]$name, [int]$w, [int]$h, [scriptblock]$draw) {
    $dir = Join-Path $root $category
    New-Item -ItemType Directory -Force $dir | Out-Null
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = "AntiAlias"; $g.TextRenderingHint = "AntiAliasGridFit"; $g.PixelOffsetMode = "HighQuality"
    $g.Clear([System.Drawing.Color]::Transparent)
    & $draw $g $w $h
    $g.Dispose()
    $bmp.Save((Join-Path $dir "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

function global:Brush($c) { New-Object System.Drawing.SolidBrush $c }
function global:Pen($c, [float]$w) { $p = New-Object System.Drawing.Pen $c, $w; $p.LineJoin = "Round"; $p }

function global:RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure(); $p
}

# Draws text centred in a box, shrinking the font until it fits.
function global:Text($g, [string]$text, [string]$family, [System.Drawing.FontStyle]$style, $color, [float]$x, [float]$y, [float]$w, [float]$h, [string]$align = "Center") {
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = $align; $sf.LineAlignment = "Center"
    $size = $h * 0.9
    do {
        $font = New-Object System.Drawing.Font $family, $size, $style, ([System.Drawing.GraphicsUnit]::Pixel)
        $m = $g.MeasureString($text, $font, [int]($w * 4), $sf)
        if ($m.Width -le $w -and $m.Height -le $h) { break }
        $font.Dispose(); $size *= 0.94
    } while ($size -gt 6)
    $g.DrawString($text, $font, (Brush $color), (New-Object System.Drawing.RectangleF $x, $y, $w, $h), $sf)
    $font.Dispose()
}

$Bold = [System.Drawing.FontStyle]::Bold; $Regular = [System.Drawing.FontStyle]::Regular
$BoldItalic = [System.Drawing.FontStyle]::Bold -bor [System.Drawing.FontStyle]::Italic

# Diagonal stripes across a box, clipped to it.
function global:Stripes($g, [float]$x, [float]$y, [float]$w, [float]$h, $c1, $c2, [float]$band, [int]$dir = 1) {
    $old = $g.Clip
    $g.SetClip((New-Object System.Drawing.RectangleF $x, $y, $w, $h))
    $g.FillRectangle((Brush $c1), $x, $y, $w, $h)
    for ($s = $x - $h - $band * 2; $s -lt $x + $w + $h; $s += $band * 2) {
        $pts = if ($dir -gt 0) {
            [System.Drawing.PointF[]]@((New-Object System.Drawing.PointF $s, ($y + $h)), (New-Object System.Drawing.PointF ($s + $band), ($y + $h)),
                (New-Object System.Drawing.PointF ($s + $band + $h), $y), (New-Object System.Drawing.PointF ($s + $h), $y))
        } else {
            [System.Drawing.PointF[]]@((New-Object System.Drawing.PointF $s, $y), (New-Object System.Drawing.PointF ($s + $band), $y),
                (New-Object System.Drawing.PointF ($s + $band + $h), ($y + $h)), (New-Object System.Drawing.PointF ($s + $h), ($y + $h)))
        }
        $g.FillPolygon((Brush $c2), $pts)
    }
    $g.Clip = $old
}

# Lightning bolt inside a box.
function global:Bolt($g, $color, [float]$x, [float]$y, [float]$w, [float]$h) {
    $p = @(@(0.55, 0), @(0.15, 0.55), @(0.45, 0.55), @(0.3, 1), @(0.85, 0.4), @(0.55, 0.4), @(0.75, 0)) |
        ForEach-Object { New-Object System.Drawing.PointF ($x + $_[0] * $w), ($y + $_[1] * $h) }
    $g.FillPolygon((Brush $color), [System.Drawing.PointF[]]$p)
}

# Warning triangle with a black border; $inner draws the symbol inside (gets x,y,w,h of the inner area).
function global:Triangle($g, [float]$w, [float]$h, $fill, [scriptblock]$inner) {
    $m = $w * 0.04
    $pts = [System.Drawing.PointF[]]@((New-Object System.Drawing.PointF ($w / 2), $m), (New-Object System.Drawing.PointF ($w - $m), ($h - $m)), (New-Object System.Drawing.PointF $m, ($h - $m)))
    $g.FillPolygon((Brush $fill), $pts)
    $g.DrawPolygon((Pen $Black ($w * 0.06)), $pts)
    & $inner ($w * 0.3) ($h * 0.36) ($w * 0.4) ($h * 0.5)
}

# ANSI/OSHA-style sign: coloured header word + message panel.
function global:Placard($g, [float]$w, [float]$h, [string]$header, $headerBg, $headerFg, [string]$message, [bool]$oval = $false) {
    $g.FillPath((Brush $White), (RoundRect 4 4 ($w - 8) ($h - 8) 18))
    $headH = $h * 0.32
    $g.FillPath((Brush $(if ($oval) { $Black } else { $headerBg })), (RoundRect 4 4 ($w - 8) $headH 18))
    $g.FillRectangle((Brush $(if ($oval) { $Black } else { $headerBg })), 4, ($headH - 14), ($w - 8), 18)
    if ($oval) {
        $g.FillEllipse((Brush $headerBg), ($w * 0.17), ($headH * 0.14), ($w * 0.66), ($headH * 0.78))
    }
    Text $g $header "Arial Black" $Bold $headerFg ($w * 0.2) ($headH * 0.16) ($w * 0.6) ($headH * 0.72)
    Text $g $message "Arial" $Bold $Black ($w * 0.07) ($headH + $h * 0.05) ($w * 0.86) ($h - $headH - $h * 0.1)
    $g.DrawPath((Pen $Black 8), (RoundRect 4 4 ($w - 8) ($h - 8) 18))
}

# Plain label: border, optional fill, text.
function global:Label($g, [float]$w, [float]$h, [string]$text, $bg, $fg, $border, [string]$family = "Arial Black") {
    $g.FillPath((Brush $bg), (RoundRect 6 6 ($w - 12) ($h - 12) 14))
    $g.DrawPath((Pen $border 10), (RoundRect 6 6 ($w - 12) ($h - 12) 14))
    Text $g $text $family $Bold $fg ($w * 0.06) ($h * 0.12) ($w * 0.88) ($h * 0.76)
}

# ---- warning stripes ------------------------------------------------------------------------

Save-Decal "Warning Stripes" "Stripes yellow-black" 1024 256 { param($g, $w, $h) Stripes $g 0 0 $w $h $Yellow $Black 64 }
Save-Decal "Warning Stripes" "Stripes yellow-black (other way)" 1024 256 { param($g, $w, $h) Stripes $g 0 0 $w $h $Yellow $Black 64 -1 }
Save-Decal "Warning Stripes" "Stripes red-white" 1024 256 { param($g, $w, $h) Stripes $g 0 0 $w $h $White $Red 64 }
Save-Decal "Warning Stripes" "Stripes black-white" 1024 256 { param($g, $w, $h) Stripes $g 0 0 $w $h $White $Black 64 }
Save-Decal "Warning Stripes" "Stripes white (tintable)" 1024 256 { param($g, $w, $h) Stripes $g 0 0 $w $h ([System.Drawing.Color]::Transparent) $White 64 }
Save-Decal "Warning Stripes" "Chevrons yellow-black" 1024 256 {
    param($g, $w, $h)
    $g.FillRectangle((Brush $Black), 0, 0, $w, $h)
    for ($x = -128; $x -lt $w + 128; $x += 160) {
        $pts = [System.Drawing.PointF[]]@((New-Object System.Drawing.PointF $x, 0), (New-Object System.Drawing.PointF ($x + 70), 0), (New-Object System.Drawing.PointF ($x + 70 + 128), ($h / 2)),
            (New-Object System.Drawing.PointF ($x + 70), $h), (New-Object System.Drawing.PointF $x, $h), (New-Object System.Drawing.PointF ($x + 128), ($h / 2)))
        $g.FillPolygon((Brush $Yellow), $pts)
    }
}
Save-Decal "Warning Stripes" "Edge stripe yellow-black (thin)" 1024 64 { param($g, $w, $h) Stripes $g 0 0 $w $h $Yellow $Black 32 }

# ---- OSHA-style placards --------------------------------------------------------------------

Save-Decal "Placards" "DANGER - Keep off" 768 512 { param($g, $w, $h) Placard $g $w $h "DANGER" $Red $White "KEEP OFF`nWHILE ENGINE RUNNING" $true }
Save-Decal "Placards" "DANGER - High voltage" 768 512 { param($g, $w, $h) Placard $g $w $h "DANGER" $Red $White "HIGH VOLTAGE`nAUTHORISED PERSONNEL ONLY" $true }
Save-Decal "Placards" "WARNING - Moving parts" 768 512 { param($g, $w, $h) Placard $g $w $h "WARNING" $Orange $Black "MOVING PARTS`nCAN CRUSH AND CUT" }
Save-Decal "Placards" "WARNING - Automatic start" 768 512 { param($g, $w, $h) Placard $g $w $h "WARNING" $Orange $Black "ENGINE MAY START`nAUTOMATICALLY" }
Save-Decal "Placards" "CAUTION - Hot surface" 768 512 { param($g, $w, $h) Placard $g $w $h "CAUTION" $Yellow $Black "HOT SURFACE`nDO NOT TOUCH" }
Save-Decal "Placards" "CAUTION - Watch your step" 768 512 { param($g, $w, $h) Placard $g $w $h "CAUTION" $Yellow $Black "WATCH YOUR STEP" }
Save-Decal "Placards" "NOTICE - Authorised personnel" 768 512 { param($g, $w, $h) Placard $g $w $h "NOTICE" $Blue $White "AUTHORISED`nPERSONNEL ONLY" }
Save-Decal "Placards" "Keep clear" 768 384 {
    param($g, $w, $h)
    Stripes $g 0 0 $w $h $Yellow $Black 48
    $g.FillRectangle((Brush $Yellow), ($w * 0.08), ($h * 0.2), ($w * 0.84), ($h * 0.6))
    $g.DrawRectangle((Pen $Black 8), ($w * 0.08), ($h * 0.2), ($w * 0.84), ($h * 0.6))
    Text $g "KEEP CLEAR" "Arial Black" $Bold $Black ($w * 0.12) ($h * 0.24) ($w * 0.76) ($h * 0.52)
}

# ---- electrical / voltage -------------------------------------------------------------------

Save-Decal "Electrical" "High voltage triangle" 512 460 { param($g, $w, $h) Triangle $g $w $h $Yellow { param($x, $y, $iw, $ih) Bolt $g $Black $x $y $iw $ih } }
foreach ($v in @("600 V DC", "1500 V DC", "3000 V DC", "25 000 V AC")) {
    Save-Decal "Electrical" "Voltage label $v" 768 256 {
        param($g, $w, $h)
        $g.FillPath((Brush $Yellow), (RoundRect 6 6 ($w - 12) ($h - 12) 16))
        $g.DrawPath((Pen $Black 10), (RoundRect 6 6 ($w - 12) ($h - 12) 16))
        Bolt $g $Black ($w * 0.05) ($h * 0.16) ($h * 0.6) ($h * 0.68)
        Text $g $v "Arial Black" $Bold $Black ($w * 0.25) ($h * 0.14) ($w * 0.7) ($h * 0.72)
    }.GetNewClosure()
}
Save-Decal "Electrical" "DANGER Overhead line" 768 512 { param($g, $w, $h) Placard $g $w $h "DANGER" $Red $White "OVERHEAD LINE`nDO NOT CLIMB" $true }
Save-Decal "Electrical" "Earth point" 512 256 {
    param($g, $w, $h)
    Label $g $w $h "" $White $Black $Green
    $x = $w * 0.12; $cy = $h / 2; $p = Pen $Green 14
    $g.DrawLine($p, ($x + 50), ($cy - 70), ($x + 50), ($cy))
    $g.DrawLine($p, $x, $cy, ($x + 100), $cy); $g.DrawLine($p, ($x + 18), ($cy + 28), ($x + 82), ($cy + 28)); $g.DrawLine($p, ($x + 36), ($cy + 56), ($x + 64), ($cy + 56))
    Text $g "EARTH" "Arial Black" $Bold $Green ($w * 0.4) ($h * 0.2) ($w * 0.52) ($h * 0.6)
}
Save-Decal "Electrical" "Isolate before working" 768 256 { param($g, $w, $h) Label $g $w $h "ISOLATE BEFORE WORKING" $White $Red $Red }

# ---- fuel / fluids --------------------------------------------------------------------------

$fluids = @(
    @("DIESEL", $Yellow, $Black), @("FUEL FILL", $Yellow, $Black), @("WATER", $Blue, $White), @("COAL", $Black, $White),
    @("LUBE OIL", (C 150 90 20), $White), @("COOLANT", $Green, $White), @("SAND", (C 200 170 110), $Black), @("HYDRAULIC OIL", (C 120 40 140), $White)
)
foreach ($f in $fluids) {
    $text, $bg, $fg = $f
    Save-Decal "Fuel and Fluids" $text 768 256 { param($g, $w, $h) Label $g $w $h $text $bg $fg $Black }.GetNewClosure()
}
Save-Decal "Fuel and Fluids" "No smoking - fuel" 768 512 { param($g, $w, $h) Placard $g $w $h "DANGER" $Red $White "FLAMMABLE FUEL`nNO SMOKING OR NAKED FLAMES" $true }

# ---- access panels / service markings -------------------------------------------------------

$access = @(
    @("ACCESS PANEL", $White, $Black, $Black), @("NO STEP", $White, $Red, $Red), @("LIFT HERE", $White, $Black, $Black),
    @("JACKING POINT", $Yellow, $Black, $Black), @("BATTERY", $White, $Black, $Black), @("AIR RESERVOIR", $White, $Black, $Black),
    @("HAND BRAKE", $White, $Black, $Black), @("SAND FILL", $White, $Black, $Black), @("DO NOT OPEN WHILE RUNNING", $White, $Red, $Red),
    @("FIRE EXTINGUISHER", $Red, $White, $Red), @("EMERGENCY FUEL CUT-OFF", $Red, $White, $Red), @("INSPECTION HATCH", $White, $Black, $Black)
)
foreach ($a in $access) {
    $text, $bg, $fg, $bd = $a
    Save-Decal "Access and Service" $text 768 192 { param($g, $w, $h) Label $g $w $h $text $bg $fg $bd "Arial" }.GetNewClosure()
}
Save-Decal "Access and Service" "Jacking point triangle" 512 460 {
    param($g, $w, $h)
    Triangle $g $w $h $Yellow { param($x, $y, $iw, $ih)
        $g.FillRectangle((Brush $Black), ($x + $iw * 0.42), $y, ($iw * 0.16), ($ih * 0.7))
        $pts = [System.Drawing.PointF[]]@((New-Object System.Drawing.PointF ($x + $iw * 0.15), ($y + $ih * 0.55)), (New-Object System.Drawing.PointF ($x + $iw * 0.85), ($y + $ih * 0.55)), (New-Object System.Drawing.PointF ($x + $iw * 0.5), ($y + $ih * 0.95)))
        $g.FillPolygon((Brush $Black), $pts)
    }
}

# ---- example logos (original designs, not the game's artwork) --------------------------------

Save-Decal "Logos" "DVRT block" 1024 384 {
    param($g, $w, $h)
    Text $g "DVRT" "Arial Black" $Bold $Cream 0 ($h * 0.04) $w ($h * 0.74)
    $g.FillRectangle((Brush $Cream), ($w * 0.08), ($h * 0.82), ($w * 0.84), ($h * 0.06))
}
Save-Decal "Logos" "DVRT block (white, tintable)" 1024 384 {
    param($g, $w, $h)
    Text $g "DVRT" "Arial Black" $Bold $White 0 ($h * 0.04) $w ($h * 0.74)
    $g.FillRectangle((Brush $White), ($w * 0.08), ($h * 0.82), ($w * 0.84), ($h * 0.06))
}
Save-Decal "Logos" "DVRT speed lines" 1536 384 {
    param($g, $w, $h)
    for ($i = 0; $i -lt 3; $i++) { $g.FillRectangle((Brush $Cream), 0, ($h * (0.3 + $i * 0.16)), ($w * (0.34 - $i * 0.06)), ($h * 0.08)) }
    Text $g "DVRT" "Arial Black" $BoldItalic $Cream ($w * 0.36) ($h * 0.08) ($w * 0.62) ($h * 0.84)
}
Save-Decal "Logos" "Derail Valley Rail Transportation" 2048 256 {
    param($g, $w, $h) Text $g "DERAIL VALLEY RAIL TRANSPORTATION" "Arial" $Bold $Cream ($w * 0.02) 0 ($w * 0.96) $h
}
Save-Decal "Logos" "Derail Valley Rail Transportation (two lines)" 1024 512 {
    param($g, $w, $h)
    Text $g "DERAIL VALLEY" "Arial Black" $Bold $Cream ($w * 0.04) ($h * 0.05) ($w * 0.92) ($h * 0.5)
    $g.FillRectangle((Brush $Cream), ($w * 0.06), ($h * 0.56), ($w * 0.88), ($h * 0.03))
    Text $g "RAIL TRANSPORTATION" "Arial" $Bold $Cream ($w * 0.04) ($h * 0.62) ($w * 0.92) ($h * 0.3)
}
Save-Decal "Logos" "DVRT herald" 768 768 {
    param($g, $w, $h)
    $g.FillEllipse((Brush (C 30 60 45)), 16, 16, ($w - 32), ($h - 32))
    $g.DrawEllipse((Pen $Cream 18), 24, 24, ($w - 48), ($h - 48))
    $g.DrawEllipse((Pen $Cream 6), ($w * 0.2), ($h * 0.2), ($w * 0.6), ($h * 0.6))
    Text $g "DVRT" "Arial Black" $Bold $Cream ($w * 0.24) ($h * 0.36) ($w * 0.52) ($h * 0.28)
    # Ring text, one character at a time around the circle.
    $ring = "DERAIL VALLEY  RAIL TRANSPORTATION  "
    $font = New-Object System.Drawing.Font "Arial", ($h * 0.065), $Bold, ([System.Drawing.GraphicsUnit]::Pixel)
    $r = $w * 0.355; $cx = $w / 2; $cy = $h / 2
    for ($i = 0; $i -lt $ring.Length; $i++) {
        $ang = -90 + $i * 360.0 / $ring.Length
        $state = $g.Save()
        $g.TranslateTransform($cx, $cy); $g.RotateTransform($ang + 90); $g.TranslateTransform(0, -$r)
        $sf = New-Object System.Drawing.StringFormat; $sf.Alignment = "Center"; $sf.LineAlignment = "Center"
        $g.DrawString([string]$ring[$i], $font, (Brush $Cream), 0, 0, $sf)
        $g.Restore($state)
    }
    $font.Dispose()
}

Write-Host "Done:" (Get-ChildItem $root -Recurse -Filter *.png).Count "PNGs in $root"
