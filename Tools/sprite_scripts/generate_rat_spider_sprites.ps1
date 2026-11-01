# Cuts the white rat (rat_*) and demon spider (spider_*) frames out of their sprite sheets into
# Assets/Enemies/Rat and Assets/Enemies/Spider. The sheets are native pixel art in 32x32 cells with real
# transparency, so cells are copied as they are (minus the near-invisible haze) - only moved down so the lowest visible pixel sits on the bottom
# row (feet = the bottom-centre pivot). Leftover frames from an older, longer animation are removed so
# EnemyAssets (which loads name_0, name_1, ... until one is missing) only picks up these.
#   _art_source/rat/rat_sheet.png        3 x 4 cells: idle x3 | run x3 | crouch, lunge, BITE | hop back, dizzy, dead
#   _art_source/spider/spider_sheet.png  4 x 4 cells: idle x4 | walk x4 | crouch, rear up, SPIT web, recover |
#                                                     second web shot, hurt, flipped over, dead
# Run from the project root:  powershell -ExecutionPolicy Bypass -File generate_rat_spider_sprites.ps1
# (generate_enemy_sprites.ps1 runs this at its end, so its old drawn rat / spider never come back.)
Add-Type -AssemblyName System.Drawing
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$Cell = 32

function Cut-Cell($sheet, [int]$col, [int]$row) {
    $o = New-Object Drawing.Bitmap $Cell, $Cell, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $low = -1
    # The sheets carry a haze of nearly invisible pixels (alpha < 32) under the drawings: ground on what
    # can actually be seen, and leave the haze out - otherwise most poses hovered a few pixels up.
    for ($y = 0; $y -lt $Cell; $y++) { for ($x = 0; $x -lt $Cell; $x++) { if ($sheet.GetPixel($col * $Cell + $x, $row * $Cell + $y).A -ge 128) { $low = $y } } }
    $dy = $Cell - 1 - $low
    for ($y = 0; $y -lt $Cell; $y++) { for ($x = 0; $x -lt $Cell; $x++) {
        $c = $sheet.GetPixel($col * $Cell + $x, $row * $Cell + $y)
        if ($c.A -ge 64 -and $y + $dy -lt $Cell) { $o.SetPixel($x, $y + $dy, $c) }
    } }
    return ,$o
}

# $frames: kind -> list of @(col, row). Writes <name>_<kind>_<i>.png and removes stale higher indices.
function Write-Creature([string]$sheetPath, [string]$outDir, [string]$name, $frames) {
    $sheet = [Drawing.Bitmap]::FromFile($sheetPath)
    foreach ($kind in $frames.Keys) {
        $list = $frames[$kind]
        for ($i = 0; $i -lt $list.Count; $i++) {
            $bmp = Cut-Cell $sheet $list[$i][0] $list[$i][1]
            $bmp.Save((Join-Path $outDir "${name}_${kind}_$i.png"), [Drawing.Imaging.ImageFormat]::Png)
            $bmp.Dispose()
        }
        for ($i = $list.Count; Test-Path (Join-Path $outDir "${name}_${kind}_$i.png"); $i++) {
            Remove-Item (Join-Path $outDir "${name}_${kind}_$i.png"), (Join-Path $outDir "${name}_${kind}_$i.png.meta") -ErrorAction SilentlyContinue
            Write-Output "  removed stale ${name}_${kind}_$i"
        }
        Write-Output "  ${name}_${kind}: $($list.Count) frame(s)"
    }
    $sheet.Dispose()
}

Write-Creature (Join-Path $root "_art_source/rat/rat_sheet.png") (Join-Path $root "Assets/Enemies/Rat") "rat" ([ordered]@{
    idle   = @(@(0, 0), @(1, 0), @(2, 0))
    move   = @(@(0, 1), @(1, 1), @(2, 1))
    attack = @(@(0, 2), @(1, 2), @(2, 2), @(0, 2))   # crouch, lunge, BITE (hit frame 2), back down
    hurt   = @(, @(1, 3))                             # dizzy, sitting (cell 0,3 is a hop back in mid-air: looked like floating)
    death  = @(@(1, 3), @(2, 3))                      # dizzy, dead
})

Write-Creature (Join-Path $root "_art_source/spider/spider_sheet.png") (Join-Path $root "Assets/Enemies/Spider") "spider" ([ordered]@{
    idle   = @(@(0, 0), @(1, 0), @(2, 0), @(3, 0))
    move   = @(@(0, 1), @(1, 1), @(2, 1), @(3, 1))
    attack = @(@(0, 2), @(1, 2), @(2, 2), @(0, 3), @(3, 2))   # crouch, rear up, SPIT (hit frame 2), web trails off, recover
    hurt   = @(, @(1, 3))
    death  = @(@(2, 3), @(3, 3))                               # flipped over, dead
})
