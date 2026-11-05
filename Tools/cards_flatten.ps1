# Cleans the Aether card fronts before they go in (Assets/Resources/Cards/card_<tier>.png, 512 x 768):
#   - the cream text box: everything cream-ish reachable from its middle becomes one flat #e9d6ae (the generator
#     left blotches in it, the epic card most)
#   - the top medallion: everything near its centre's colour becomes that colour (faint letter marks in the rare one)
# Flood fills from fixed seeds, so the frame and its ornaments are never touched.
# Usage: powershell -File Tools/cards_flatten.ps1 <card png> [...]
param([Parameter(ValueFromRemainingArguments)] [string[]] $Files)
Add-Type -AssemblyName System.Drawing
function Fill($b, [int]$sx, [int]$sy, [scriptblock]$ok, [Drawing.Color]$to) {
  $w = $b.Width; $h = $b.Height; $seen = New-Object 'bool[]' ($w * $h)
  $q = New-Object 'System.Collections.Generic.Queue[int]'; $q.Enqueue($sy * $w + $sx); $seen[$sy * $w + $sx] = $true; $n = 0
  while ($q.Count -gt 0) {
    $i = $q.Dequeue(); $x = $i % $w; $y = [Math]::Floor($i / $w)
    $c = $b.GetPixel($x, $y); if (-not (& $ok $c)) { continue }
    $b.SetPixel($x, $y, $to); $n++
    foreach ($d in @(@(1, 0), @(-1, 0), @(0, 1), @(0, -1))) {
      $nx = $x + $d[0]; $ny = $y + $d[1]
      if ($nx -lt 0 -or $ny -lt 0 -or $nx -ge $w -or $ny -ge $h) { continue }
      $j = $ny * $w + $nx; if (-not $seen[$j]) { $seen[$j] = $true; $q.Enqueue($j) } } }
  return $n
}
function Dist($a, $b) { [Math]::Abs($a.R - $b.R) + [Math]::Abs($a.G - $b.G) + [Math]::Abs($a.B - $b.B) }
$cream = [Drawing.Color]::FromArgb(255, 0xe9, 0xd6, 0xae)
foreach ($f in $Files) {
  $b = [Drawing.Bitmap]::new($f); $copy = [Drawing.Bitmap]::new($b); $b.Dispose(); $b = $copy
  # the text box: the cream run down the middle column below the picture
  $top = -1; $bot = -1
  for ($y = 440; $y -lt $b.Height; $y++) { $c = $b.GetPixel(256, $y); $isCream = $c.A -gt 0 -and (Dist $c $cream) -lt 110
    if ($isCream -and $top -lt 0) { $top = $y }; if (-not $isCream -and $top -ge 0 -and ($y - $top) -gt 40) { $bot = $y - 1; break } }
  $boxN = Fill $b 256 ([int](($top + $bot) / 2)) { param($c) $c.A -gt 0 -and (Dist $c $cream) -lt 110 } $cream
  # the medallion: its centre is the first wide run of one colour below the top edge on the middle column
  $my = 0; for ($y = 30; $y -lt 140; $y++) { $c = $b.GetPixel(256, $y); $c2 = $b.GetPixel(256, $y + 6); if ($c.A -gt 0 -and (Dist $c $c2) -lt 30 -and (Dist $c $b.GetPixel(256, $y + 12)) -lt 30) { $my = $y + 12; break } }
  $seed = $b.GetPixel(256, $my)
  $medN = Fill $b 256 $my { param($c) $c.A -gt 0 -and (Dist $c $seed) -lt 60 } ([Drawing.Color]::FromArgb(255, $seed.R, $seed.G, $seed.B))
  $b.Save($f, [Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  "$(Split-Path $f -Leaf): text box rows $top-$bot flattened ($boxN px); medallion at y $my ($medN px)"
}
