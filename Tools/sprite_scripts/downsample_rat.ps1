# One-time: halves the white rat's resolution (72x64 -> 36x32) so its pixels match the other
# characters (the original PixelLab art was ~3x finer than the cat and tiles). Originals are
# copied to _art_backup/Rat_hires first (once); every run reads from that backup, so it can be
# re-run safely after tweaking the rules below.
# 2x2 block rule: opaque if >= 2 of 4 pixels are opaque; colour = a dark outline pixel if the
# block has one (keeps the 1px outline as a 1px outline), else the most common colour.
param([string]$Dir = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')) "Assets/Enemies/Rat"), [string]$Backup = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')) "_art_backup/Rat_hires"))
if (-not (Test-Path $Backup)) {
  New-Item -ItemType Directory -Force $Backup | Out-Null
  foreach ($f in Get-ChildItem $Dir -Filter *.png) { Copy-Item $f.FullName (Join-Path $Backup $f.Name) }
}
Add-Type -AssemblyName System.Drawing
$code = @"
using System; using System.Drawing; using System.Collections.Generic;
public static class RatDown {
  static double Lum(Color c) { return 0.299*c.R + 0.587*c.G + 0.114*c.B; }
  public static Bitmap Run(Bitmap src) {
    int w = src.Width / 2, h = src.Height / 2; var o = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
    for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {
      var px = new List<Color>();
      for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++) { var c = src.GetPixel(x*2+dx, y*2+dy); if (c.A > 128) px.Add(c); }
      if (px.Count < 2) { o.SetPixel(x, y, Color.FromArgb(0,0,0,0)); continue; }
      foreach (var c in px) if (c.R > 170 && c.G < 100 && c.B < 120) { o.SetPixel(x, y, c); goto next; }   // the red eye wins
      Color best = px[0]; double minL = 1e9; foreach (var c in px) { double l = Lum(c); if (l < minL) { minL = l; best = c; } }
      if (minL < 60) { o.SetPixel(x, y, best); continue; }               // outline wins
      var counts = new Dictionary<int,int>(); int top = 0; Color pick = px[0];
      foreach (var c in px) { int k = c.ToArgb(); int n; counts.TryGetValue(k, out n); n++; counts[k] = n; if (n > top) { top = n; pick = c; } }
      o.SetPixel(x, y, Color.FromArgb(255, pick.R, pick.G, pick.B));
      next:;
    }
    return o;
  }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing
foreach ($f in Get-ChildItem $Backup -Filter *.png) {
  $src = [System.Drawing.Bitmap]::FromFile($f.FullName); $dst = [RatDown]::Run($src); $src.Dispose()
  $dst.Save((Join-Path $Dir $f.Name), [System.Drawing.Imaging.ImageFormat]::Png); $dst.Dispose()
}
Write-Host "Downsampled $((Get-ChildItem $Dir -Filter *.png).Count) rat frames."
