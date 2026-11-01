# Shifts the knight's skill effects + icons from the pack's blue to warm amber (the knight's accent: dash trail, paw aura).
# Always reads the untouched blue originals from _art_backup\knight_blue, so it can be re-run with another hue.
# Run from the project root:  powershell -ExecutionPolicy Bypass -File recolor_knight_fx.ps1 [-Shift -170]
# Then Unity picks the changed PNGs up by itself (Cats Smash > Apply Effects is NOT needed: slicing is unchanged).
param([double]$Shift = -170)   # degrees; blue (~200) -170 -> orange (~30)
Add-Type -AssemblyName System.Drawing
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$bak = Join-Path $root '_art_backup\knight_blue'
$targets = @('Assets\Effects\Knight', 'Assets\Effects\Combo', 'Assets\Resources\SkillIcons')
foreach ($t in $targets) {
    $dest = Join-Path $root $t
    $store = Join-Path $bak $t
    New-Item -ItemType Directory -Force $store | Out-Null
    $files = if ($t -like '*SkillIcons') { Get-ChildItem $dest -Filter 'k_*.png' } else { Get-ChildItem $dest -Filter '*.png' }
    foreach ($f in $files) {
        $orig = Join-Path $store $f.Name
        if (-not (Test-Path $orig)) { Copy-Item $f.FullName $orig }
        $src = [Drawing.Bitmap]::FromFile($orig)
        $bmp = New-Object Drawing.Bitmap $src.Width, $src.Height, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
        for ($y = 0; $y -lt $src.Height; $y++) { for ($x = 0; $x -lt $src.Width; $x++) {
            $c = $src.GetPixel($x, $y)
            if ($c.A -eq 0) { continue }
            $max = [Math]::Max($c.R, [Math]::Max($c.G, $c.B)); $min = [Math]::Min($c.R, [Math]::Min($c.G, $c.B))
            $d = ($max - $min) / 255.0
            if ($d -lt 0.04) { $bmp.SetPixel($x, $y, $c); continue }   # white / grey stays
            $r = $c.R / 255.0; $g = $c.G / 255.0; $b = $c.B / 255.0; $mx = $max / 255.0
            if ($max -eq $c.R) { $h = 60 * ((($g - $b) / $d) % 6) } elseif ($max -eq $c.G) { $h = 60 * ((($b - $r) / $d) + 2) } else { $h = 60 * ((($r - $g) / $d) + 4) }
            if ($h -lt 0) { $h += 360 }
            if ($h -ge 140 -and $h -le 290) { $h = (($h + $Shift) % 360 + 360) % 360 }   # only the blues move; yellow / orange accents stay
            $s = $d / $mx; $v = $mx
            $cc = $v * $s; $xx = $cc * (1 - [Math]::Abs((($h / 60) % 2) - 1)); $m = $v - $cc
            switch ([Math]::Floor($h / 60)) { 0 { $o = $cc, $xx, 0 } 1 { $o = $xx, $cc, 0 } 2 { $o = 0, $cc, $xx } 3 { $o = 0, $xx, $cc } 4 { $o = $xx, 0, $cc } default { $o = $cc, 0, $xx } }
            $bmp.SetPixel($x, $y, [Drawing.Color]::FromArgb($c.A, [int](($o[0] + $m) * 255), [int](($o[1] + $m) * 255), [int](($o[2] + $m) * 255)))
        } }
        $src.Dispose(); $bmp.Save($f.FullName, [Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
        Write-Output "  $t\$($f.Name)"
    }
}
