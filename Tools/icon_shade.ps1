# Gives the card skill icons (Assets/Resources/SkillIcons) the card frame's weight: a 1 px outline of the icon's
# darkest colour where the edge is not dark already, and one shadow tone - the shape's bottom and right edge pixels
# at 70 % (light from the top left, as in the frames). Originals go to _art_backup/icon_shade_20261108 first and every
# run starts from them (no double outline); -Restore puts them back.
#   powershell -File Tools/icon_shade.ps1 [-Restore]
param([switch]$Restore)
$repo = Resolve-Path (Join-Path $PSScriptRoot '..')
$dir = Join-Path $repo 'Assets\Resources\SkillIcons'
$backup = Join-Path $repo '_art_backup\icon_shade_20261108'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System; using System.Collections.Generic; using System.Drawing;
public static class IconShade {
    static int Luma(Color c) { return (c.R * 3 + c.G * 6 + c.B) / 10; }
    public static string Run(string src, string dst) {
        Bitmap s = new Bitmap(src); Bitmap b = new Bitmap(s); s.Dispose(); int w = b.Width, h = b.Height;
        var px = new Color[w, h]; Color dark = Color.Black; int dl = 999;
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) { px[x, y] = b.GetPixel(x, y); if (px[x, y].A >= 128 && Luma(px[x, y]) < dl) { dl = Luma(px[x, y]); dark = px[x, y]; } }
        Func<int, int, bool> solid = (x, y) => x >= 0 && y >= 0 && x < w && y < h && px[x, y].A >= 128;
        int shade = 0, ring = 0;
        // shadow tone first, on the shape as drawn: a light pixel whose right or lower neighbour is clear
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {
            var c = px[x, y]; if (c.A < 128 || Luma(c) < 70) continue;
            if (!solid(x + 1, y) || !solid(x, y + 1)) { b.SetPixel(x, y, Color.FromArgb(c.A, c.R * 7 / 10, c.G * 7 / 10, c.B * 7 / 10)); shade++; }
        }
        // then the outline: a clear pixel next to a solid one that is not dark already
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {
            if (solid(x, y)) continue;
            bool next = false;
            int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
            for (int k = 0; k < 4 && !next; k++) { int nx = x + dx[k], ny = y + dy[k]; if (solid(nx, ny) && Luma(px[nx, ny]) >= 70) next = true; }
            if (next) { b.SetPixel(x, y, Color.FromArgb(255, dark.R, dark.G, dark.B)); ring++; }
        }
        b.Save(dst, System.Drawing.Imaging.ImageFormat.Png); b.Dispose();
        return "shade " + shade + ", outline +" + ring;
    }
}
'@
New-Item -ItemType Directory -Force $backup | Out-Null
foreach ($f in Get-ChildItem $dir -Filter *.png) {
  $bk = Join-Path $backup $f.Name
  if ($Restore) { Copy-Item $bk $f.FullName -Force; continue }
  if (-not (Test-Path $bk)) { Copy-Item $f.FullName $bk }
  "$($f.Name) : $([IconShade]::Run($bk, $f.FullName))"
}
if ($Restore) { 'restored' }
