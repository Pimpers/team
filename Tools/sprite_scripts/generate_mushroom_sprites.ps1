# Builds the "버섯 친구" enemy frames (mushroom_*) for Assets/Enemies/Mushroom/ from the 4x4 sheet in
# _art_source/mushroom/mushroom_sheet.png (1920x1920, 480 px cells, art drawn with ~11 px pixels), reduced
# to 44x44 frames by sheet_cut.ps1 (black-background removal, most-common-colour per block, grounded).
# Cells (row, col):  row 0 idle (col 2 blinks; used once per idle loop) | row 1 walk, facing right |
#                    row 2 knife swing (draw back, raise, SLASH with an arc, recover) |
#                    row 3 hurt (stars), dizzy, falling over, down (x eyes).
# Run from the project root:  powershell -ExecutionPolicy Bypass -File generate_mushroom_sprites.ps1
# Then Unity: Cats Smash > Rebuild Stages (EnemyAssets picks the frames up).
param([string]$Sheet = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')) "_art_source/mushroom/mushroom_sheet.png"), [string]$OutDir = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')) "Assets/Enemies/Mushroom"))
. (Join-Path $PSScriptRoot "sheet_cut.ps1")
New-Item -ItemType Directory -Force $OutDir | Out-Null
$sheetBmp = [Drawing.Bitmap]::FromFile($Sheet)
function F([int]$r, [int]$c, [string]$name) { Cut-Frame $sheetBmp $r $c 480 44 $OutDir $name }
# Idle mostly holds the plain pose and blinks (col 2) only once per loop.
F 0 0 "mushroom_idle_0";   F 0 1 "mushroom_idle_1";   F 0 0 "mushroom_idle_2";   F 0 3 "mushroom_idle_3"
F 0 0 "mushroom_idle_4";   F 0 2 "mushroom_idle_5"
F 1 0 "mushroom_move_0";   F 1 1 "mushroom_move_1";   F 1 2 "mushroom_move_2";   F 1 3 "mushroom_move_3"
F 2 0 "mushroom_attack_0"; F 2 1 "mushroom_attack_1"; F 2 2 "mushroom_attack_2"; F 2 3 "mushroom_attack_3"
F 3 0 "mushroom_hurt_0"
F 3 1 "mushroom_death_0";  F 3 2 "mushroom_death_1";  F 3 3 "mushroom_death_2"
$sheetBmp.Dispose()
