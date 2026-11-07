# Gives effect sheets back the fade-out Tools/fx_dechecker.ps1 emptied (dot-grid frames cleared, so the effect ended
# a frame or two early and the hit lost its tail): each empty frame after the last drawn one becomes that frame
# shrunk round its bottom centre - 2/3, then 2/5, then 1/5 - nearest neighbour, so it keeps its few tones and its
# outline instead of dithering out. Cells are square, one row. Originals go to _art_backup/fx_fadeshrink_20261108.
#   powershell -File Tools/fx_fadeshrink.ps1 -Files Assets/Effects/Knight/k_tower_fx.png,... [-Restore]
param([string[]]$Files, [switch]$Restore)
$Files = @($Files | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$repo = Resolve-Path (Join-Path $PSScriptRoot '..')
$backup = Join-Path $repo '_art_backup\fx_fadeshrink_20261108'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System; using System.Drawing;
public static class FadeShrink {
    static bool Empty(Bitmap b, int x0, int c) { for (int y = 0; y < c; y++) for (int x = x0; x < x0 + c; x++) if (b.GetPixel(x, y).A > 0) return false; return true; }
    public static int Run(string src, string dst) {
        Bitmap s = new Bitmap(src); Bitmap b = new Bitmap(s); s.Dispose();
        int c = b.Height, n = b.Width / c, last = -1, filled = 0;
        for (int i = 0; i < n; i++) if (!Empty(b, i * c, c)) last = i;
        if (last < 0) { b.Dispose(); return 0; }
        double[] scales = { 0.66, 0.4, 0.2 };
        int k = 0;
        for (int i = last + 1; i < n && k < scales.Length; i++, k++) {
            double f = scales[k]; int cx = c / 2, by = c - 1; // shrink round the bottom centre (effects stand on the ground)
            for (int y = 0; y < c; y++) for (int x = 0; x < c; x++) {
                int sx = cx + (int)Math.Floor((x - cx) / f), sy = by - (int)Math.Floor((by - y) / f);
                if (sx < 0 || sy < 0 || sx >= c || sy >= c) continue;
                var col = b.GetPixel(last * c + sx, sy); if (col.A == 0) continue;
                b.SetPixel(i * c + x, y, col);
            }
            filled++;
        }
        b.Save(dst, System.Drawing.Imaging.ImageFormat.Png); b.Dispose(); return filled;
    }
}
'@
foreach ($rel in $Files) {
  $path = Join-Path $repo $rel; $bk = Join-Path $backup ($rel.Replace([char]92, [char]95).Replace([char]47, [char]95))
  New-Item -ItemType Directory -Force $backup | Out-Null
  if ($Restore) { Copy-Item $bk $path -Force; "$rel restored"; continue }
  if (-not (Test-Path $bk)) { Copy-Item $path $bk }
  "$rel : $([FadeShrink]::Run($bk, $path)) fade frame(s) drawn"
}
