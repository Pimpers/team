# Places the skill icons on their 64 x 64 canvases (Assets/Resources/SkillIcons):
#   -Import <dir> -Names a,b: pxl's 32 px sprites from <dir>, blown up x2 by whole pixels
#   then every icon is moved so its weight (the mean of its solid pixels) sits on the canvas's centre, kept
#   inside the canvas - a box centre put lopsided drawings (an arrow's long shaft, a claw's tip) off to one side.
# Usage: powershell -File Tools/icons_place.ps1 [-Import <dir> -Names a,b,c]
param([string]$Import, [string[]]$Names = @())
Add-Type -AssemblyName System.Drawing
$Names = @($Names | ForEach-Object { $_ -split "," } | Where-Object { $_ })   # -File passes "a,b" as one string
$dst = Join-Path $PSScriptRoot '..\Assets\Resources\SkillIcons'
foreach ($n in $Names) {
  $b = [Drawing.Bitmap]::new((Join-Path $Import "$n.png"))
  $o = [Drawing.Bitmap]::new(64, 64, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $ox = [Math]::Floor((64 - 2 * $b.Width) / 2); $oy = [Math]::Floor((64 - 2 * $b.Height) / 2)
  for ($y = 0; $y -lt $b.Height; $y++) { for ($x = 0; $x -lt $b.Width; $x++) {
    $c = $b.GetPixel($x, $y); if ($c.A -lt 128) { continue }
    $c = [Drawing.Color]::FromArgb(255, $c.R, $c.G, $c.B)
    for ($j = 0; $j -lt 2; $j++) { for ($i = 0; $i -lt 2; $i++) {
      $px = $ox + 2 * $x + $i; $py = $oy + 2 * $y + $j
      if ($px -ge 0 -and $px -lt 64 -and $py -ge 0 -and $py -lt 64) { $o.SetPixel($px, $py, $c) } } } } }
  $b.Dispose(); $o.Save((Join-Path $dst "$n.png"), [Drawing.Imaging.ImageFormat]::Png); $o.Dispose()
  "imported $n"
}
Get-ChildItem $dst -Filter *.png | ForEach-Object {
  $b = [Drawing.Bitmap]::new($_.FullName)
  $sx = 0; $sy = 0; $n = 0; $x0 = 64; $y0 = 64; $x1 = -1; $y1 = -1
  for ($y = 0; $y -lt 64; $y++) { for ($x = 0; $x -lt 64; $x++) { if ($b.GetPixel($x, $y).A -gt 0) {
    $sx += $x; $sy += $y; $n++
    if ($x -lt $x0) { $x0 = $x }; if ($x -gt $x1) { $x1 = $x }; if ($y -lt $y0) { $y0 = $y }; if ($y -gt $y1) { $y1 = $y } } } }
  $dx = [Math]::Round(31.5 - $sx / $n); $dy = [Math]::Round(31.5 - $sy / $n)
  $dx = [Math]::Max(-$x0, [Math]::Min(63 - $x1, $dx)); $dy = [Math]::Max(-$y0, [Math]::Min(63 - $y1, $dy))
  if ($dx -eq 0 -and $dy -eq 0) { $b.Dispose(); return }
  $o = [Drawing.Bitmap]::new(64, 64, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
  for ($y = 0; $y -lt 64; $y++) { for ($x = 0; $x -lt 64; $x++) { $c = $b.GetPixel($x, $y); if ($c.A -gt 0) { $o.SetPixel($x + $dx, $y + $dy, $c) } } }
  $b.Dispose(); $o.Save($_.FullName, [Drawing.Imaging.ImageFormat]::Png); $o.Dispose()
  "$($_.BaseName) moved $dx,$dy"
}
