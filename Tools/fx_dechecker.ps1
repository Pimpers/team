# Clears the checkerboard dither an old colour cut left in some effect sheets (single opaque pixels with all four
# sides clear but at least two corners filled - a lattice that reads as a grey net over the background), and in a
# fade-out frame drawn as a dot grid (over 30% lone pixels) every lone pixel: the effect then ends a frame sooner.
# Originals go to _art_backup/fx_dechecker_20261108 first; -Restore puts them back.
#   powershell -File Tools/fx_dechecker.ps1 -Files Assets/Effects/Knight/k_tower_fx.png,... [-Restore]
param([string[]]$Files, [switch]$Restore)
$Files = @($Files | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$repo = Resolve-Path (Join-Path $PSScriptRoot '..')
$backup = Join-Path $repo '_art_backup\fx_dechecker_20261108'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System.Collections.Generic; using System.Drawing;
public static class DeChk { public static int Run(string src, string dst) {
  Bitmap s = new Bitmap(src); Bitmap b = new Bitmap(s); s.Dispose(); int w = b.Width, h = b.Height; var a = new bool[w * h]; var kill = new List<int>();
  for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) a[y * w + x] = b.GetPixel(x, y).A >= 128;
  // a fade frame (cells are square, one row): over 30% of its pixels stand alone - clear every lone pixel there
  int cells = w / h; var fade = new bool[cells + 1]; for (int c = 0; c < cells; c++) { int lone = 0, all = 0;
    for (int y = 1; y < h - 1; y++) for (int x = c * h + 1; x < (c + 1) * h - 1; x++) { if (!a[y * w + x]) continue; all++;
      if (!a[y * w + x - 1] && !a[y * w + x + 1] && !a[(y - 1) * w + x] && !a[(y + 1) * w + x]) lone++; }
    fade[c] = all > 0 && lone * 10 > all * 3; }
  for (int y = 1; y < h - 1; y++) for (int x = 1; x < w - 1; x++) { if (!a[y * w + x]) continue;
    int orth = (a[y * w + x - 1] ? 1 : 0) + (a[y * w + x + 1] ? 1 : 0) + (a[(y - 1) * w + x] ? 1 : 0) + (a[(y + 1) * w + x] ? 1 : 0);
    int diag = (a[(y - 1) * w + x - 1] ? 1 : 0) + (a[(y - 1) * w + x + 1] ? 1 : 0) + (a[(y + 1) * w + x - 1] ? 1 : 0) + (a[(y + 1) * w + x + 1] ? 1 : 0);
    if (orth == 0 && (diag >= 2 || fade[x / h])) kill.Add(y * w + x); }
  foreach (int i in kill) b.SetPixel(i % w, i / w, Color.Transparent);
  b.Save(dst, System.Drawing.Imaging.ImageFormat.Png); b.Dispose(); return kill.Count; } }
'@
foreach ($rel in $Files) {
  $path = Join-Path $repo $rel; $bk = Join-Path $backup ($rel.Replace([char]92, [char]95).Replace([char]47, [char]95))
  New-Item -ItemType Directory -Force $backup | Out-Null
  if ($Restore) { Copy-Item $bk $path -Force; "$rel restored"; continue }
  if (-not (Test-Path $bk)) { Copy-Item $path $bk }
  "$rel : $([DeChk]::Run($bk, $path)) checker px cleared"
}
