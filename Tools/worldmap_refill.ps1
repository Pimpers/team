# Re-fills the boxes aether_import.ps1 painted flat over WorldMap.png's English labels with the map round them:
# the rim round each Korean plate mirrors the map just outside the box (nearest edge), so coast, sea and
# grass run on into the box instead of a flat rectangle showing round the Korean plates. Same box table as
# aether_import.ps1. The current file goes to _art_backup/worldmap_refill_20261108 first.
#   powershell -File Tools/worldmap_refill.ps1
Add-Type -AssemblyName System.Drawing
$repo = Resolve-Path (Join-Path $PSScriptRoot '..')
$path = Join-Path $repo 'Assets\Backgrounds\WorldMap.png'
$bk = Join-Path $repo '_art_backup\worldmap_refill_20261108'
New-Item -ItemType Directory -Force $bk | Out-Null
if (-not (Test-Path (Join-Path $bk 'WorldMap.png'))) { Copy-Item $path $bk }
$src = [Drawing.Bitmap]::new($path); $m = [Drawing.Bitmap]::new($src); $src.Dispose()
$W = $m.Width; $H = $m.Height
$labels = @(@(0.5015, 0.047, 0.165, 0.055), @(0.134, 0.441, 0.145, 0.078), @(0.289, 0.318, 0.095, 0.055), @(0.5565, 0.191, 0.085, 0.055),
  @(0.669, 0.211, 0.085, 0.055), @(0.37, 0.479, 0.07, 0.055), @(0.6265, 0.479, 0.065, 0.055), @(0.8275, 0.538, 0.075, 0.055),
  @(0.6975, 0.621, 0.075, 0.055), @(0.309, 0.675, 0.09, 0.061), @(0.4465, 0.665, 0.08, 0.055), @(0.9215, 0.715, 0.075, 0.055))
# Only the rim between the plate (the label box itself) and the painted box is re-filled: mirroring the whole box
# copied coasts and old letter edges into symmetric blots, and under the plate nothing shows anyway. The title
# scroll (first box) stays flat: its fill is the scroll's own colour.
foreach ($L in $labels[1..($labels.Count - 1)]) {
  $x0 = [Math]::Max(0, [int](($L[0] - $L[2] * 0.58) * $W)); $x1 = [Math]::Min($W - 1, [int](($L[0] + $L[2] * 0.58) * $W))
  $y0 = [Math]::Max(0, [int](($L[1] - $L[3] * 0.62) * $H)); $y1 = [Math]::Min($H - 1, [int](($L[1] + $L[3] * 0.62) * $H))
  $px0 = [int](($L[0] - $L[2] * 0.5) * $W); $px1 = [int](($L[0] + $L[2] * 0.5) * $W)
  $py0 = [int](($L[1] - $L[3] * 0.5) * $H); $py1 = [int](($L[1] + $L[3] * 0.5) * $H)
  for ($y = $y0; $y -le $y1; $y++) { for ($x = $x0; $x -le $x1; $x++) {
    if ($x -ge $px0 -and $x -le $px1 -and $y -ge $py0 -and $y -le $py1) { continue }
    # mirror across the nearest box edge (one the map has room beyond)
    $d = @(@(($x - $x0), 'l'), @(($x1 - $x), 'r'), @(($y - $y0), 't'), @(($y1 - $y), 'b')) |
      Where-Object { ($_[1] -ne 'l' -or $x0 -gt 0) -and ($_[1] -ne 't' -or $y0 -gt 0) -and ($_[1] -ne 'r' -or $x1 -lt $W - 1) -and ($_[1] -ne 'b' -or $y1 -lt $H - 1) } |
      Sort-Object { $_[0] } | Select-Object -First 1
    $sx = $x; $sy = $y
    switch ($d[1]) { 'l' { $sx = $x0 - 1 - $d[0] } 'r' { $sx = $x1 + 1 + $d[0] } 't' { $sy = $y0 - 1 - $d[0] } 'b' { $sy = $y1 + 1 + $d[0] } }
    $m.SetPixel($x, $y, $m.GetPixel([Math]::Min($W - 1, [Math]::Max(0, $sx)), [Math]::Min($H - 1, [Math]::Max(0, $sy))))
  } }
}
$m.Save($path, [Drawing.Imaging.ImageFormat]::Png); $m.Dispose(); "WorldMap ${W}x$H, $($labels.Count) boxes re-filled"
