# Slice Zro Water column 6 (Cartoon Solid + Bubbles) — 24 frames, no red grid pixels.
# Source: zro_t3_4w_01.png column 6 (4 tiles wide × 2 tile rows per frame).
Add-Type -AssemblyName System.Drawing

$scale = 8
$sourcePath = "E:\CrazyChat\docs\scope\fishing-preview-gallery\zro_t3_4w_01.png"
$outDirs = @(
    "E:\CrazyChat\Project\Assets\Resources\Art\Generated\Fishing"
    "E:\CrazyChat\Project\Assets\Art\Generated\Fishing"
)
$galleryDir = "E:\CrazyChat\docs\scope\fishing-preview-gallery"

function Test-Red([System.Drawing.Color]$c) {
    return ($c.R -ge 200 -and $c.G -le 80 -and $c.B -le 80)
}

function Test-Black([System.Drawing.Color]$c) {
    return ($c.R -le 30 -and $c.G -le 30 -and $c.B -le 30)
}

function Test-Waterish([System.Drawing.Color]$c) {
    if (Test-Black $c) { return $false }
    if (Test-Red $c) { return $false }
    return ($c.B -ge 40 -or $c.G -ge 40 -or $c.R -ge 20)
}

function Should-Keep([System.Drawing.Color]$c) {
    # Keep black/navy outlines and depth bands — discarding them punched a
    # transparent seam through the middle of each stitched frame.
    if (Test-Red $c) { return $false }
    if (Test-Black $c) { return $true }
    return Test-Waterish $c
}

function Get-FillBlue([System.Drawing.Bitmap]$bmp) {
    # Sample the most common opaque blue in the lower half for hole-fill.
    $best = [System.Drawing.Color]::FromArgb(255, 46, 88, 166)
    $counts = @{}
    $y0 = [int]($bmp.Height * 0.55)
    for ($y = $y0; $y -lt $bmp.Height; $y++) {
        for ($x = 0; $x -lt $bmp.Width; $x++) {
            $c = $bmp.GetPixel($x, $y)
            if ($c.A -lt 200) { continue }
            if ($c.B -lt 80) { continue }
            $key = "$($c.R),$($c.G),$($c.B)"
            if ($counts.ContainsKey($key)) { $counts[$key]++ } else { $counts[$key] = 1 }
        }
    }
    $top = $counts.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 1
    if ($null -ne $top) {
        $parts = $top.Key.Split(',')
        $best = [System.Drawing.Color]::FromArgb(255, [int]$parts[0], [int]$parts[1], [int]$parts[2])
    }
    return $best
}

function Fill-InteriorHoles([System.Drawing.Bitmap]$bmp) {
    $fill = Get-FillBlue $bmp
    # Any fully transparent pixel that has opaque neighbors on both left+right
    # or both above+below is an interior seam hole — fill solid.
    for ($y = 0; $y -lt $bmp.Height; $y++) {
        for ($x = 0; $x -lt $bmp.Width; $x++) {
            $c = $bmp.GetPixel($x, $y)
            if ($c.A -gt 0) { continue }
            $left = $false; $right = $false; $up = $false; $down = $false
            for ($i = $x - 1; $i -ge 0; $i--) {
                if ($bmp.GetPixel($i, $y).A -gt 0) { $left = $true; break }
            }
            for ($i = $x + 1; $i -lt $bmp.Width; $i++) {
                if ($bmp.GetPixel($i, $y).A -gt 0) { $right = $true; break }
            }
            for ($i = $y - 1; $i -ge 0; $i--) {
                if ($bmp.GetPixel($x, $i).A -gt 0) { $up = $true; break }
            }
            for ($i = $y + 1; $i -lt $bmp.Height; $i++) {
                if ($bmp.GetPixel($x, $i).A -gt 0) { $down = $true; break }
            }
            if (($left -and $right) -or ($up -and $down)) {
                # Don't paint sky notches between wave peaks (top quarter).
                if ($y -lt [int]($bmp.Height * 0.28) -and ($left -and $right) -and -not ($up -and $down)) {
                    continue
                }
                $bmp.SetPixel($x, $y, $fill)
            }
        }
    }
}

function Count-MidSeamHoles([System.Drawing.Bitmap]$bmp) {
    # Count transparent pixels along the middle third of the vertical midline band.
    $y0 = [int]($bmp.Height * 0.40)
    $y1 = [int]($bmp.Height * 0.60)
    $holes = 0
    $total = 0
    for ($y = $y0; $y -le $y1; $y++) {
        for ($x = 0; $x -lt $bmp.Width; $x++) {
            $total++
            if ($bmp.GetPixel($x, $y).A -lt 128) { $holes++ }
        }
    }
    return @{ holes = $holes; total = $total }
}

function Clear-SkyBlack([System.Drawing.Bitmap]$bmp) {
    # Black above the water silhouette is sheet background — make it transparent.
    # Keep black that sits at/below the first water-colored pixel in each column
    # (outlines and depth bands inside the pool).
    $clear = [System.Drawing.Color]::FromArgb(0, 0, 0, 0)
    for ($x = 0; $x -lt $bmp.Width; $x++) {
        $surfaceY = -1
        for ($y = 0; $y -lt $bmp.Height; $y++) {
            $c = $bmp.GetPixel($x, $y)
            if ($c.A -lt 128) { continue }
            if (Test-Black $c) { continue }
            if (Test-Waterish $c) { $surfaceY = $y; break }
        }
        if ($surfaceY -lt 0) { continue }
        for ($y = 0; $y -lt $surfaceY; $y++) {
            $c = $bmp.GetPixel($x, $y)
            if ($c.A -lt 128) { continue }
            if (Test-Black $c) {
                $bmp.SetPixel($x, $y, $clear)
            }
        }
    }
}

function Count-SkyBlack([System.Drawing.Bitmap]$bmp) {
    # Opaque near-black in the top 20% — should be 0 after Clear-SkyBlack.
    $y1 = [Math]::Max(0, [int]($bmp.Height * 0.20) - 1)
    $n = 0
    for ($y = 0; $y -le $y1; $y++) {
        for ($x = 0; $x -lt $bmp.Width; $x++) {
            $c = $bmp.GetPixel($x, $y)
            if ($c.A -ge 128 -and (Test-Black $c)) { $n++ }
        }
    }
    return $n
}

function Get-NearestUpscale([System.Drawing.Bitmap]$src, [int]$scale) {
    $w = $src.Width * $scale
    $h = $src.Height * $scale
    $dst = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $dst.SetResolution($src.HorizontalResolution, $src.VerticalResolution)
    $g = [System.Drawing.Graphics]::FromImage($dst)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $g.DrawImage($src, 0, 0, $w, $h)
    $g.Dispose()
    return $dst
}

function Save-Png([System.Drawing.Bitmap]$bmp, [string]$path) {
    $dir = Split-Path $path -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
}

function Count-RedPixels([System.Drawing.Bitmap]$bmp) {
    $n = 0
    for ($y = 0; $y -lt $bmp.Height; $y++) {
        for ($x = 0; $x -lt $bmp.Width; $x++) {
            $c = $bmp.GetPixel($x, $y)
            if ($c.R -ge 200 -and $c.G -le 80 -and $c.B -le 80) { $n++ }
        }
    }
    return $n
}

function Find-TileColumnsInBand([System.Drawing.Bitmap]$bmp, [int]$x0, [int]$x1, [int]$yStart, [int]$yEnd) {
    $strips = @()
    $inCol = $false
    $rs = 0
    for ($x = $x0; $x -le $x1; $x++) {
        $water = 0
        for ($y = $yStart; $y -le $yEnd; $y++) {
            if (Test-Waterish ($bmp.GetPixel($x, $y))) { $water++ }
        }
        $active = $water -gt (($yEnd - $yStart + 1) * 0.12)
        if ($active -and -not $inCol) { $rs = $x; $inCol = $true }
        elseif (-not $active -and $inCol) {
            $strips += ,@($rs, ($x - 1))
            $inCol = $false
        }
    }
    if ($inCol) { $strips += ,@($rs, $x1) }
    return $strips
}

function Get-WaterSegments([System.Drawing.Bitmap]$bmp, [int]$sx, [int]$ex, [int]$yStart, [int]$yEnd) {
    $colW = $ex - $sx + 1
    $segments = @()
    $inSeg = $false
    $st = 0
    for ($y = $yStart; $y -le $yEnd; $y++) {
        $red = 0; $water = 0
        for ($x = $sx; $x -le $ex; $x++) {
            $c = $bmp.GetPixel($x, $y)
            if (Test-Red $c) { $red++ }
            elseif (Test-Waterish $c) { $water++ }
        }
        $isWater = ($water -gt ($colW * 0.25)) -and ($red -lt ($colW * 0.12))
        if ($isWater -and -not $inSeg) { $st = $y; $inSeg = $true }
        elseif (-not $isWater -and $inSeg) {
            $segments += ,@($st, ($y - 1))
            $inSeg = $false
        }
    }
    if ($inSeg) { $segments += ,@($st, $yEnd) }
    return $segments
}

function Build-FrameBitmap([System.Drawing.Bitmap]$src, [object[]]$tileCols, [int[]]$segA, [int[]]$segB, [int]$tileW, [int]$tileH) {
    $outW = $tileCols.Count * $tileW
    $outH = $tileH * 2
    $dst = New-Object System.Drawing.Bitmap $outW, $outH, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($y = 0; $y -lt $outH; $y++) {
        for ($x = 0; $x -lt $outW; $x++) {
            $dst.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, 0, 0, 0))
        }
    }

    $rowSegs = @($segA, $segB)
    for ($ri = 0; $ri -lt 2; $ri++) {
        $seg = $rowSegs[$ri]
        $sy0 = $seg[0]
        $sy1 = $seg[1]
        $segH = $sy1 - $sy0 + 1
        for ($ti = 0; $ti -lt $tileCols.Count; $ti++) {
            $tx0 = $tileCols[$ti][0]
            $tx1 = $tileCols[$ti][1]
            for ($dy = 0; $dy -lt $segH -and $dy -lt $tileH; $dy++) {
                $sy = $sy0 + $dy
                $destY = $ri * $tileH + $dy
                for ($sx = $tx0; $sx -le $tx1; $sx++) {
                    $c = $src.GetPixel($sx, $sy)
                    if (-not (Should-Keep $c)) { continue }
                    $destX = $ti * $tileW + ($sx - $tx0)
                    if ($destX -ge $outW) { continue }
                    $dst.SetPixel($destX, $destY, [System.Drawing.Color]::FromArgb(255, $c.R, $c.G, $c.B))
                }
            }
        }
    }
    Fill-InteriorHoles $dst
    Clear-SkyBlack $dst
    return $dst
}

if (-not (Test-Path -LiteralPath $sourcePath)) {
    throw "Source not found: $sourcePath"
}

$bmp = [System.Drawing.Bitmap]::FromFile($sourcePath)
Write-Host "Loaded: $sourcePath ($($bmp.Width)x$($bmp.Height))"

# Column 6 band (includes red vertical separators between 4 tile columns)
$colBandX0 = 592
$colBandX1 = 658
$yScanStart = 0
$yScanEnd = $bmp.Height - 1

$tileStrips = Find-TileColumnsInBand -bmp $bmp -x0 $colBandX0 -x1 $colBandX1 -yStart 80 -yEnd ([int]($bmp.Height * 0.98))
if ($tileStrips.Count -lt 4) {
    throw "Expected 4 tile columns in column 6, found $($tileStrips.Count)"
}
$tileCols = $tileStrips | Select-Object -First 4
$tileW = ($tileCols[0][1] - $tileCols[0][0] + 1)
Write-Host "Tile columns (4): $(($tileCols | ForEach-Object { "$($_[0])-$($_[1])" }) -join ' | ') tileW=$tileW"

$segments = Get-WaterSegments -bmp $bmp -sx $colBandX0 -ex $colBandX1 -yStart $yScanStart -yEnd $yScanEnd
Write-Host "Water row segments: $($segments.Count)"

$frameSegPairs = @()
for ($i = 0; $i + 1 -lt $segments.Count; $i += 2) {
    $a = $segments[$i]
    $b = $segments[$i + 1]
    $ha = $a[1] - $a[0] + 1
    $hb = $b[1] - $b[0] + 1
    if ($ha -ge 8 -and $ha -le 20 -and $hb -ge 8 -and $hb -le 20) {
        $frameSegPairs += ,@($a, $b)
    }
    else {
        Write-Host "WARN: skip unpaired segment pair at index $i (heights $ha, $hb)"
    }
}

$frameCount = $frameSegPairs.Count
Write-Host "Animation frames detected: $frameCount"
if ($frameCount -lt 1) { throw "No frames detected" }

$tileH = 16
$rawFrames = @()
for ($fi = 0; $fi -lt $frameCount; $fi++) {
    $pair = $frameSegPairs[$fi]
    $raw = Build-FrameBitmap -src $bmp -tileCols $tileCols -segA $pair[0] -segB $pair[1] -tileW $tileW -tileH $tileH
    $rawFrames += $raw
}
$bmp.Dispose()

$upFrames = @()
foreach ($raw in $rawFrames) {
    $upFrames += (Get-NearestUpscale -src $raw -scale $scale)
    $raw.Dispose()
}

$targetCount = 24
if ($frameCount -gt $targetCount) {
    Write-Host "WARN: truncating to $targetCount frames"
    while ($upFrames.Count -gt $targetCount) {
        $last = $upFrames[$upFrames.Count - 1]
        $last.Dispose()
        $upFrames = $upFrames[0..($targetCount - 1)]
    }
    $frameCount = $targetCount
}
elseif ($frameCount -lt $targetCount) {
    Write-Host "WARN: only $frameCount clean frames; duplicating last frame to fill $targetCount"
    $last = $upFrames[$upFrames.Count - 1]
    while ($upFrames.Count -lt $targetCount) {
        $dup = New-Object System.Drawing.Bitmap $last.Width, $last.Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($dup)
        $g.DrawImage($last, 0, 0)
        $g.Dispose()
        $upFrames += $dup
    }
    $frameCount = $targetCount
}

$rawW = $tileW * 4
$rawH = $tileH * 2
$outW = $upFrames[0].Width
$outH = $upFrames[0].Height

foreach ($dir in $outDirs) {
    for ($i = 0; $i -lt $targetCount; $i++) {
        $name = "water_zro_{0:D2}.png" -f $i
        $path = Join-Path $dir $name
        Save-Png -bmp $upFrames[$i] -path $path
        Write-Host "WROTE $path ${outW}x${outH}"
    }
}

$f0Red = Count-RedPixels $upFrames[0]
$f0Seam = Count-MidSeamHoles $upFrames[0]
$f0Sky = Count-SkyBlack $upFrames[0]
Write-Host "Frame0 bright-red pixel count: $f0Red (must be 0)"
Write-Host "Frame0 mid-band transparent: $($f0Seam.holes)/$($f0Seam.total) (must be 0)"
Write-Host "Frame0 top-sky opaque black: $f0Sky (must be 0)"

$f0path = Join-Path $galleryDir "zro_water_frame0.png"
Save-Png -bmp $upFrames[0] -path $f0path
Write-Host "WROTE $f0path"

$stripIndices = @(0, 4, 8, 12, 16, 20)
$stripW = $outW * $stripIndices.Count
$stripH = $outH
$strip = New-Object System.Drawing.Bitmap $stripW, $stripH, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$sg = [System.Drawing.Graphics]::FromImage($strip)
$ox = 0
foreach ($idx in $stripIndices) {
    if ($idx -lt $upFrames.Count) {
        $sg.DrawImage($upFrames[$idx], $ox, 0)
    }
    $ox += $outW
}
$sg.Dispose()
$stripPath = Join-Path $galleryDir "zro_water_anim_preview.png"
Save-Png -bmp $strip -path $stripPath
$strip.Dispose()
Write-Host "WROTE $stripPath ${stripW}x${stripH}"

foreach ($f in $upFrames) { $f.Dispose() }

Write-Host "--- SUMMARY ---"
Write-Host "Source: $sourcePath"
Write-Host "Column 6 tiles: $(($tileCols | ForEach-Object { "$($_[0])-$($_[1])" }) -join ', ')"
Write-Host "Frames written: $targetCount (detected $($frameSegPairs.Count) from sheet)"
Write-Host "Raw frame size: ${rawW}x${rawH}"
Write-Host "Upscaled x${scale}: ${outW}x${outH}"
Write-Host "Frame0 red pixels: $f0Red"
