# Builds the "슬라임" enemy frames (slime_*) for Assets/Enemies/Slime/ from the 4x4 sheet in
# _art_source/slime/slime_sheet.png (1920x1920, 480 px cells, art drawn with ~15 px pixels), reduced to
# 32x32 frames by sheet_cut.ps1 (black-background removal, most-common-colour per block, grounded).
# Cells (row, col):  row 0 idle wobble | row 1 hop (squash, stretch, lean, land) |
#                    row 2 pounce (crouch, stretch, -, splat) | row 3 hurt, splatter, melt, dead.
# Run from the project root:  powershell -ExecutionPolicy Bypass -File generate_slime_sprites.ps1
# Then Unity: Cats Smash > Rebuild Stages (EnemyAssets picks the frames up).
param([string]$Sheet = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')) "_art_source/slime/slime_sheet.png"), [string]$OutDir = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')) "Assets/Enemies/Slime"))
. (Join-Path $PSScriptRoot "sheet_cut.ps1")
New-Item -ItemType Directory -Force $OutDir | Out-Null
$sheetBmp = [Drawing.Bitmap]::FromFile($Sheet)
function F([int]$r, [int]$c, [string]$name) { Cut-Frame $sheetBmp $r $c 480 32 $OutDir $name (2.0 / 15) }
F 0 0 "slime_idle_0";   F 0 3 "slime_idle_1"
F 1 0 "slime_move_0";   F 1 1 "slime_move_1";   F 1 2 "slime_move_2";   F 1 3 "slime_move_3"
F 2 0 "slime_attack_0"; F 1 0 "slime_attack_1"; F 2 1 "slime_attack_2"; F 2 3 "slime_attack_3"
F 3 0 "slime_hurt_0"
F 3 1 "slime_death_0";  F 3 2 "slime_death_1";  F 3 3 "slime_death_2"
$sheetBmp.Dispose()
