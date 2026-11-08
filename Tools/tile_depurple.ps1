# The purple dots in the dirt under the grass of the ground and platform tiles competed with the cat and the effects
# (a bright pattern right under where everyone stands). Each purple pixel (R and B well over G) takes the commonest
# non-purple colour round it, so it sinks into the dirt; the grass on top is not touched. Originals go to
# _art_backup/tile_depurple_20261108 first; -Restore puts them back. .meta files untouched.
#   powershell -File Tools/tile_depurple.ps1 [-Restore]
param([switch]$Restore)
$repo = Resolve-Path (Join-Path $PSScriptRoot '..')
$files = 'Assets\Tiles\Sprites\cute_plat_grass_l.png', 'Assets\Tiles\Sprites\cute_plat_grass_m.png', 'Assets\Tiles\Sprites\cute_plat_grass_r.png',
  'Assets\FreeCuteTilesetv1\FreeCuteTileset\Tileset.png'
$backup = Join-Path $repo '_art_backup\tile_depurple_20261108'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System.Collections.Generic; using System.Drawing;
public static class DePurple {
    static bool Purple(Color c) { return c.A > 0 && c.R > c.G + 30 && c.B > c.G + 30; }
    public static int Run(string src, string dst) {
        Bitmap s = new Bitmap(src); Bitmap b = new Bitmap(s); s.Dispose(); int w = b.Width, h = b.Height, n = 0;
        var px = new Color[w, h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[x, y] = b.GetPixel(x, y);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {
            if (!Purple(px[x, y])) continue;
            var count = new Dictionary<int, int>();
            for (int r = 1; r <= 3 && count.Count == 0; r++) // widen until a non-purple neighbour turns up
                for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++) {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    var c = px[nx, ny]; if (c.A == 0 || Purple(c)) continue;
                    int k = c.ToArgb(), m; count.TryGetValue(k, out m); count[k] = m + 1;
                }
            int best = 0, bn = -1; foreach (var kv in count) if (kv.Value > bn) { bn = kv.Value; best = kv.Key; }
            if (bn > 0) { b.SetPixel(x, y, Color.FromArgb(best)); n++; }
        }
        b.Save(dst, System.Drawing.Imaging.ImageFormat.Png); b.Dispose(); return n;
    }
}
'@
New-Item -ItemType Directory -Force $backup | Out-Null
foreach ($rel in $files) {
  $path = Join-Path $repo $rel; $bk = Join-Path $backup (Split-Path $rel -Leaf)
  if ($Restore) { Copy-Item $bk $path -Force; "$rel restored"; continue }
  if (-not (Test-Path $bk)) { Copy-Item $path $bk }
  "$rel : $([DePurple]::Run($bk, $path)) px"
}
