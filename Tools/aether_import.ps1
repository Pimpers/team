# Brings the Aether / 8 px-grid art in (originals first to _art_backup/aether_20261105; .meta files untouched):
#   - Resources/Cards/back_<tier>.png: the card back stretched (nearest neighbour only) so its card fills the same
#     box as that tier's front - the backs were drawn a little narrower and shorter, and the card jumped in size
#     when it turned over
#   - Backgrounds/Lobby.png: the 1672 x 936 picture padded back to 1672 x 941 with copies of its last row
#   - Backgrounds/WorldMap.png: the painted English labels (CAT ISLAND and the region names, smeared by the 8 px
#     grid) painted over with the colour round each one - the boxes WorldMapLabels lays its Korean plates on, so
#     no coast or island moves
# Usage: powershell -File Tools/aether_import.ps1 -Src <folder with back_*_px.png, lobby_px8.png, worldmap_px8.png>
param([Parameter(Mandatory)] [string] $Src)
Add-Type -AssemblyName System.Drawing
$repo = Resolve-Path (Join-Path $PSScriptRoot '..')
$bk = Join-Path $repo '_art_backup\aether_20261105'
function Backup($rel) { $d = Join-Path $bk $rel; New-Item -ItemType Directory -Force (Split-Path $d) | Out-Null; if (-not (Test-Path $d)) { Copy-Item (Join-Path $repo "Assets\$rel") $d } }
function Load($p) { $b = [Drawing.Bitmap]::new($p); $c = [Drawing.Bitmap]::new($b); $b.Dispose(); $c }
function Box($b) {
  $x0 = $b.Width; $y0 = $b.Height; $x1 = -1; $y1 = -1
  for ($y = 0; $y -lt $b.Height; $y++) { for ($x = 0; $x -lt $b.Width; $x++) { if ($b.GetPixel($x, $y).A -gt 0) {
    if ($x -lt $x0) { $x0 = $x }; if ($x -gt $x1) { $x1 = $x }; if ($y -lt $y0) { $y0 = $y }; if ($y -gt $y1) { $y1 = $y } } } }
  @($x0, $y0, $x1, $y1)
}

foreach ($t in 'common', 'rare', 'epic') {
  Backup "Resources\Cards\back_$t.png"
  $front = Load (Join-Path $repo "Assets\Resources\Cards\card_$t.png"); $fb = Box $front
  $back = Load (Join-Path $Src "back_${t}_px.png"); $bb = Box $back
  $o = [Drawing.Bitmap]::new($front.Width, $front.Height, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $fw = $fb[2] - $fb[0] + 1; $fh = $fb[3] - $fb[1] + 1; $bw = $bb[2] - $bb[0] + 1; $bh = $bb[3] - $bb[1] + 1
  for ($y = 0; $y -lt $fh; $y++) { for ($x = 0; $x -lt $fw; $x++) {
    $sx = $bb[0] + [Math]::Floor(($x + 0.5) * $bw / $fw); $sy = $bb[1] + [Math]::Floor(($y + 0.5) * $bh / $fh)
    $c = $back.GetPixel($sx, $sy); if ($c.A -gt 0) { $o.SetPixel($fb[0] + $x, $fb[1] + $y, $c) } } }
  $o.Save((Join-Path $repo "Assets\Resources\Cards\back_$t.png"), [Drawing.Imaging.ImageFormat]::Png)
  $o.Dispose(); $front.Dispose(); $back.Dispose()
  "back_$t : ${bw}x$bh -> ${fw}x$fh at $($fb[0]),$($fb[1])"
}

Backup 'Backgrounds\Lobby.png'
$l = Load (Join-Path $Src 'lobby_px8.png'); $o = [Drawing.Bitmap]::new($l.Width, 941, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [Drawing.Graphics]::FromImage($o); $g.DrawImageUnscaled($l, 0, 0); $g.Dispose()
for ($y = $l.Height; $y -lt 941; $y++) { for ($x = 0; $x -lt $l.Width; $x++) { $o.SetPixel($x, $y, $l.GetPixel($x, $l.Height - 1)) } }
$o.Save((Join-Path $repo 'Assets\Backgrounds\Lobby.png'), [Drawing.Imaging.ImageFormat]::Png); "Lobby $($l.Width)x$($l.Height) -> $($o.Width)x$($o.Height)"; $o.Dispose(); $l.Dispose()

Backup 'Backgrounds\WorldMap.png'
$m = Load (Join-Path $Src 'worldmap_px8.png'); $W = $m.Width; $H = $m.Height
# WorldMapLabels' boxes (centre x, y, width, height as fractions of the map), a little bigger so the painted
# label's edges go too
$labels = @(@(0.5015, 0.047, 0.165, 0.055), @(0.134, 0.441, 0.145, 0.078), @(0.289, 0.318, 0.095, 0.055), @(0.5565, 0.191, 0.085, 0.055),
  @(0.669, 0.211, 0.085, 0.055), @(0.37, 0.479, 0.07, 0.055), @(0.6265, 0.479, 0.065, 0.055), @(0.8275, 0.538, 0.075, 0.055),
  @(0.6975, 0.621, 0.075, 0.055), @(0.309, 0.675, 0.09, 0.061), @(0.4465, 0.665, 0.08, 0.055), @(0.9215, 0.715, 0.075, 0.055))
foreach ($L in $labels) {
  $x0 = [int](($L[0] - $L[2] * 0.58) * $W); $x1 = [int](($L[0] + $L[2] * 0.58) * $W)
  $y0 = [int](($L[1] - $L[3] * 0.62) * $H); $y1 = [int](($L[1] + $L[3] * 0.62) * $H)
  $x0 = [Math]::Max(0, $x0); $y0 = [Math]::Max(0, $y0); $x1 = [Math]::Min($W - 1, $x1); $y1 = [Math]::Min($H - 1, $y1)
  # the colour round it: the commonest colour on a ring 4 px outside the box
  $count = @{}
  for ($x = $x0 - 4; $x -le $x1 + 4; $x += 2) { foreach ($y in @(($y0 - 4), ($y1 + 4))) { if ($x -ge 0 -and $x -lt $W -and $y -ge 0 -and $y -lt $H) { $k = $m.GetPixel($x, $y).ToArgb(); $count[$k] = 1 + $count[$k] } } }
  for ($y = $y0 - 4; $y -le $y1 + 4; $y += 2) { foreach ($x in @(($x0 - 4), ($x1 + 4))) { if ($x -ge 0 -and $x -lt $W -and $y -ge 0 -and $y -lt $H) { $k = $m.GetPixel($x, $y).ToArgb(); $count[$k] = 1 + $count[$k] } } }
  $fill = [Drawing.Color]::FromArgb(($count.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 1).Key)
  $g = [Drawing.Graphics]::FromImage($m); $g.FillRectangle([Drawing.SolidBrush]::new($fill), $x0, $y0, $x1 - $x0 + 1, $y1 - $y0 + 1); $g.Dispose()
}
$m.Save((Join-Path $repo 'Assets\Backgrounds\WorldMap.png'), [Drawing.Imaging.ImageFormat]::Png); "WorldMap ${W}x$H, $($labels.Count) labels painted over"; $m.Dispose()
