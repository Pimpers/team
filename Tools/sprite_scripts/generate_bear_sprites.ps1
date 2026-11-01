# Builds the last boss "반달가슴곰" (bear_*) frames for Assets/Enemies/Bear/ from the 256x256 source art in
# _art_source/bear (idle, walk A, walk B, attack). The source is smooth hi-res art, so each frame is
# box-averaged down 4x to 64x64 (alpha cut at 50%): one bear pixel then matches the other enemies' pixel
# size (the werewolf it replaces is also a 64x64 canvas). Faces right, feet on the bottom row.
# The missing poses are derived: a breathing idle, a lunge for the swipe, a flinch, a two-step collapse.
# Run from the project root:  powershell -ExecutionPolicy Bypass -File generate_bear_sprites.ps1
# Then Unity: Cats Smash > Rebuild Stages (EnemyAssets picks the frames up).
param([string]$Src = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')) "_art_source/bear"), [string]$OutDir = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')) "Assets/Enemies/Bear"))
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force $OutDir | Out-Null
$N = 64

# 256 -> 64: average each 4x4 block (premultiplied), opaque where at least half covered.
function Load-Small([string]$file) {
    $bmp = [Drawing.Bitmap]::FromFile((Join-Path $Src $file))
    $f = 4
    $px = New-Object 'object[,]' $N, $N
    for ($y = 0; $y -lt $N; $y++) {
        for ($x = 0; $x -lt $N; $x++) {
            $sa = 0; $sr = 0; $sg = 0; $sb = 0
            for ($dy = 0; $dy -lt $f; $dy++) { for ($dx = 0; $dx -lt $f; $dx++) {
                $c = $bmp.GetPixel($x * $f + $dx, $y * $f + $dy)
                $sa += $c.A; $sr += $c.R * $c.A; $sg += $c.G * $c.A; $sb += $c.B * $c.A
            } }
            if ($sa / ($f * $f) -ge 128) { $px[$x, $y] = [int[]]@([int]($sr / $sa), [int]($sg / $sa), [int]($sb / $sa)) } else { $px[$x, $y] = $null }
        }
    }
    $bmp.Dispose()
    return ,(Ground $px)
}

# Moves the drawing down so its lowest pixel sits on the bottom row (the pivot is bottom-centre).
function Ground($px) {
    $low = -1
    for ($y = 0; $y -lt $N; $y++) { for ($x = 0; $x -lt $N; $x++) { if ($px[$x, $y] -ne $null) { $low = $y } } }
    return ,(Shift $px 0 ($N - 1 - $low))
}

function Shift($px, [int]$ox, [int]$oy) {
    $o = New-Object 'object[,]' $N, $N
    for ($y = 0; $y -lt $N; $y++) { for ($x = 0; $x -lt $N; $x++) {
        $sx = $x - $ox; $sy = $y - $oy
        if ($sx -ge 0 -and $sy -ge 0 -and $sx -lt $N -and $sy -lt $N) { $o[$x, $y] = $px[$sx, $sy] }
    } }
    return ,$o
}

# Everything above the paws (bottom `keep` rows) sinks by one pixel: a breath.
function Breathe($px, [int]$keep) {
    $o = New-Object 'object[,]' $N, $N
    for ($y = 0; $y -lt $N; $y++) { for ($x = 0; $x -lt $N; $x++) {
        $o[$x, $y] = if ($y -ge $N - $keep) { $px[$x, $y] } elseif ($y -ge 1) { $px[$x, ($y - 1)] } else { $null }
    } }
    return ,$o
}

# Squashed towards the floor to `k` of its height (and darkened by `dim`): the collapse.
function Squash($px, [double]$k, [double]$dim) {
    $o = New-Object 'object[,]' $N, $N
    for ($y = 0; $y -lt $N; $y++) { for ($x = 0; $x -lt $N; $x++) {
        $sy = [int][math]::Floor(($N - 1) - (($N - 1) - $y) / $k)
        if ($sy -ge 0 -and $px[$x, $sy] -ne $null) { $c = $px[$x, $sy]; $o[$x, $y] = [int[]]@([int]($c[0] * $dim), [int]($c[1] * $dim), [int]($c[2] * $dim)) }
    } }
    return ,$o
}

function Save($px, [string]$name) {
    $bmp = New-Object Drawing.Bitmap $N, $N, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($y = 0; $y -lt $N; $y++) { for ($x = 0; $x -lt $N; $x++) {
        $c = $px[$x, $y]
        if ($c -ne $null) { $bmp.SetPixel($x, $y, [Drawing.Color]::FromArgb(255, $c[0], $c[1], $c[2])) }
    } }
    $bmp.Save((Join-Path $OutDir "$name.png"), [Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output "  $name.png"
}

$idle = Load-Small "bear_idle_256.png"
$walkA = Load-Small "bear_walk_a_256.png"
$walkB = Load-Small "bear_walk_b_256.png"
$attack = Load-Small "bear_attack_256.png"

Save $idle "bear_idle_0"
Save (Breathe $idle 10) "bear_idle_1"
Save $walkA "bear_move_0"
Save $walkB "bear_move_1"
# Swipe: rears up with claws raised, then lunges a few pixels forward on the hit frame (2).
Save $idle "bear_attack_0"
Save $attack "bear_attack_1"
Save (Shift $attack 3 0) "bear_attack_2"
Save $idle "bear_attack_3"
Save (Shift $idle -2 0) "bear_hurt_0"
Save (Squash $idle 0.75 0.85) "bear_death_0"
Save (Squash $idle 0.45 0.65) "bear_death_1"
