# Draws BDragon/wind.png: 8 frames of 32 x 32, three gusts sweeping left to right - a 2 px white core with a
# light blue edge, a curl at the head, the tail thinning out - in the effects' clean arcade look (32 px, three
# tones, no glow, hard edges). Faces right (Vfx turns it along the flight). Replaces the shrunk BDragon wind,
# whose 1 px strokes all but vanished at 32 px.
# Usage: powershell -File Tools/fx_wind.ps1
$out = Join-Path $PSScriptRoot '..\Assets\Effects\BDragon\wind.png'
Add-Type -AssemblyName System.Drawing
$FrameCount = 8; $S = 32
$core = [Drawing.Color]::FromArgb(255, 255, 255, 255)
$mid = [Drawing.Color]::FromArgb(255, 191, 232, 255)
$edge = [Drawing.Color]::FromArgb(255, 111, 183, 224)
$bmp = [Drawing.Bitmap]::new($FrameCount * $S, $S, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
function Put($f, $x, $y, $c) { if ($x -ge 0 -and $x -lt $S -and $y -ge 0 -and $y -lt $S) { $bmp.SetPixel($f * $S + $x, $y, $c) } }
# Each gust: its row, its start frame, its length.
$gusts = @(@{ y = 11; start = 0; len = 14 }, @{ y = 17; start = 1; len = 18 }, @{ y = 23; start = 2; len = 12 })
for ($f = 0; $f -lt $FrameCount; $f++) {
  foreach ($g in $gusts) {
    $t = $f - $g.start
    if ($t -lt 0 -or $t -gt 5) { continue }
    $head = 4 + $t * 5                       # the head moves 5 px a frame
    $len = [Math]::Max(3, $g.len - [Math]::Max(0, $t - 2) * 4)   # it thins out over the last frames
    for ($i = 0; $i -lt $len; $i++) {
      $x = $head - $i
      $y = $g.y + [int][Math]::Round([Math]::Sin(($x + $g.y) * 0.35) * 1.2)   # a gentle wave
      $thick = $i -lt $len * 0.6 -and $t -lt 4
      Put $f $x ($y - 1) $edge
      Put $f $x $y ($(if ($i -lt 2) { $core } elseif ($thick) { $core } else { $mid }))
      if ($thick) { Put $f $x ($y + 1) $mid; Put $f $x ($y + 2) $edge } else { Put $f $x ($y + 1) $edge }
    }
    # the curl at the head: up and back, two frames into its life
    if ($t -ge 1 -and $t -le 4) { Put $f ($head + 1) ($g.y - 2) $edge; Put $f ($head + 1) ($g.y - 1) $mid; Put $f $head ($g.y - 3) $edge }
  }
}
$bmp.Save($out, [Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
"wind.png $($FrameCount * $S)x$S"
