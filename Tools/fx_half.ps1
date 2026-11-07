# Halves effect sheets in place: every 2x2 block becomes one pixel (its commonest opaque colour; clear when at most
# one of the four is opaque), so the effect draws at half the size with the same pixel size as the rest of the game.
# The cell in Assets/Editor/VfxAssets.cs must be halved to match, then Cats Smash > Apply Effects rebuilds the slices.
# Originals go to _art_backup/fx_half_20261108 first; -Restore puts them back.
#   powershell -File Tools/fx_half.ps1 -Files Assets/Effects/BDragon/lure.png,... [-Restore]
# Why: lure, catnip, hiss and the can-opener hit were 2.5-3x the cat's height and hid the fight.
param([string[]]$Files, [switch]$Restore)
$Files = @($Files | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$repo = Resolve-Path (Join-Path $PSScriptRoot '..')
$backup = Join-Path $repo '_art_backup\fx_half_20261108'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System; using System.Collections.Generic; using System.Drawing;
public static class FxHalf {
    public static Bitmap Run(Bitmap s) {
        int w = s.Width / 2, h = s.Height / 2; var o = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {
            var count = new Dictionary<int, int>(); int solid = 0;
            for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++) { var c = s.GetPixel(x * 2 + dx, y * 2 + dy);
                if (c.A >= 128) { solid++; int k = c.ToArgb(), n; count.TryGetValue(k, out n); count[k] = n + 1; } }
            if (solid < 2) continue;
            int best = 0, bn = -1; foreach (var kv in count) if (kv.Value > bn) { bn = kv.Value; best = kv.Key; }
            o.SetPixel(x, y, Color.FromArgb(best));
        }
        return o;
    }
}
'@
foreach ($rel in $Files) {
  $path = Join-Path $repo $rel; $bk = Join-Path $backup ($rel.Replace([char]92, [char]95).Replace([char]47, [char]95))
  New-Item -ItemType Directory -Force $backup | Out-Null
  if ($Restore) { Copy-Item $bk $path -Force; "$rel restored"; continue }
  if (-not (Test-Path $bk)) { Copy-Item $path $bk }
  $s = [Drawing.Bitmap]::new($bk); $o = [FxHalf]::Run($s); "$rel : $($s.Width)x$($s.Height) -> $($o.Width)x$($o.Height)"; $s.Dispose()
  $o.Save($path, [Drawing.Imaging.ImageFormat]::Png); $o.Dispose()
}
