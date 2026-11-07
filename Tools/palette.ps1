# The game's reference palette and the recolour to it.
#   powershell -File Tools/palette.ps1 -Build            -> Tools/palette.png (one 8 px swatch per colour, 8 a row)
#   powershell -File Tools/palette.ps1 -Apply <png,...>  -> each pixel to the nearest palette colour (RGB distance)
# Build: up to 24 colours by median cut over the UI wood and cream (panels, name plates) and the two cats' idle frames,
# plus 24 over the Aether skill effects - the UI and the cats have no greens, blues or pinks, and the effects
# recoloured to brown only would lose what they are. Apply keeps alpha; originals go to _art_backup/palette_20261108.
param([switch]$Build, [string[]]$Apply = @(), [switch]$Restore, [double]$MaxShift = 14,
  # colours added by hand after the cut, where a picture's own key colours were too far from it (catnip, blast, bear)
  [string[]]$AddColours = @("85d527", "4e8415", "fdeb7b", "ec6309", "ba6834"))
$Apply = @($Apply | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$repo = Resolve-Path (Join-Path $PSScriptRoot '..')
$out = Join-Path $repo 'Tools\palette.png'
$backup = Join-Path $repo '_art_backup\palette_20261108'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System; using System.Collections.Generic; using System.Drawing;
public static class Pal {
    public static List<int> Collect(string[] files, int step) {
        var all = new List<int>();
        foreach (var f in files) using (var b = new Bitmap(f))
            for (int y = 0; y < b.Height; y += step) for (int x = 0; x < b.Width; x += step) { var c = b.GetPixel(x, y); if (c.A >= 128) all.Add(c.ToArgb() & 0xFFFFFF); }
        return all;
    }
    public static List<int> Cut(List<int> all, int colours) {
        var boxes = new List<List<int>> { all }; var res = new List<int>();
        while (boxes.Count < colours) {
            int bb = -1, br = 0, bc = 0;
            for (int k = 0; k < boxes.Count; k++) for (int ch = 0; ch < 3; ch++) {
                int lo = 255, hi = 0; foreach (int c in boxes[k]) { int v = (c >> (16 - ch * 8)) & 255; if (v < lo) lo = v; if (v > hi) hi = v; }
                if (hi - lo > br && boxes[k].Count > 1) { br = hi - lo; bb = k; bc = ch; } }
            if (bb < 0) break;
            int sh = 16 - bc * 8; var box = boxes[bb]; box.Sort((a, b) => ((a >> sh) & 255).CompareTo((b >> sh) & 255));
            int half = box.Count / 2; boxes[bb] = box.GetRange(0, half); boxes.Add(box.GetRange(half, box.Count - half));
        }
        foreach (var box in boxes) { if (box.Count == 0) continue; long r = 0, g = 0, b = 0; foreach (int c in box) { r += (c >> 16) & 255; g += (c >> 8) & 255; b += c & 255; }
            int n = box.Count; res.Add((int)(r / n) << 16 | (int)(g / n) << 8 | (int)(b / n)); }
        return res;
    }
    // Returns the mean shift of the moved pixels (weighted RGB distance); the caller keeps the file only when small.
    public static double Apply(string path, int[] pal) {
        Bitmap s = new Bitmap(path); Bitmap b = new Bitmap(s); s.Dispose(); int changed = 0; double shift = 0;
        var memo = new Dictionary<int, int>();
        for (int y = 0; y < b.Height; y++) for (int x = 0; x < b.Width; x++) {
            var c = b.GetPixel(x, y); if (c.A == 0) continue; int k = c.ToArgb() & 0xFFFFFF, m;
            if (!memo.TryGetValue(k, out m)) { int best = int.MaxValue; m = k;
                foreach (int p in pal) { int dr = ((p >> 16) & 255) - c.R, dg = ((p >> 8) & 255) - c.G, db = (p & 255) - c.B, d = dr * dr * 3 + dg * dg * 4 + db * db * 2; if (d < best) { best = d; m = p; } }
                memo[k] = m; }
            if (m != k) { b.SetPixel(x, y, Color.FromArgb(c.A, (m >> 16) & 255, (m >> 8) & 255, m & 255)); changed++;
                int er = ((m >> 16) & 255) - c.R, eg = ((m >> 8) & 255) - c.G, eb = (m & 255) - c.B; shift += Math.Sqrt(er * er * 3 + eg * eg * 4 + eb * eb * 2) / 3; }
        }
        b.Save(path, System.Drawing.Imaging.ImageFormat.Png); b.Dispose(); return changed == 0 ? 0 : shift / changed;
    }
}
'@
$fx = 'a_trap_fx','a_wire_fx','a_fish_fx','k_loaf_fx','lure','k_night_fx','k_hiss_fx','a_catnip_fx','charm','a_jelly_fx','trap_snap','a_watch_fx','a_can_fx','beam_end','a_rapid_fx','explosion'
if ($Build) {
  $ui = @('kenney_ui-pack-rpg-expansion\PNG\panel_brown.png', 'kenney_ui-pack-rpg-expansion\PNG\panel_beige.png', 'kenney_ui-pack-rpg-expansion\PNG\panelInset_beigeLight.png',
    'UI\NamePill.png', 'UI\LobbyNameplate.png', 'Meow Knight\Frames\Knight_Idle.png', 'Meowolas\Frames\Archer_Idle.png') |
    ForEach-Object { Join-Path $repo "Assets\$_" } | Where-Object { Test-Path $_ }
  $fxFiles = $fx | ForEach-Object { Get-ChildItem (Join-Path $repo 'Assets\Effects') -Recurse -Filter "$_.png" | Select-Object -First 1 -ExpandProperty FullName }
  $base = [Pal]::Cut([Pal]::Collect([string[]]$ui, 1), 24)
  $extra = [Pal]::Cut([Pal]::Collect([string[]]$fxFiles, 1), 24)
  $pal = @($base) + @($extra) + @($AddColours | ForEach-Object { [Convert]::ToInt32($_, 16) }) | Select-Object -Unique
  $img = [Drawing.Bitmap]::new(64, 8 * [Math]::Ceiling($pal.Count / 8)); $g = [Drawing.Graphics]::FromImage($img)
  for ($i = 0; $i -lt $pal.Count; $i++) { $g.FillRectangle([Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255, ($pal[$i] -shr 16) -band 255, ($pal[$i] -shr 8) -band 255, $pal[$i] -band 255)), ($i % 8) * 8, [Math]::Floor($i / 8) * 8, 8, 8) }
  $img.Save($out); "palette $($pal.Count) colours ($($base.Count) UI/cat + $($extra.Count) effects) from $($ui.Count) UI/cat files -> $out"
}
if ($Apply.Count) {
  $p = [Drawing.Bitmap]::new($out); $pal = @(); for ($y = 0; $y -lt $p.Height; $y += 8) { for ($x = 0; $x -lt 64; $x += 8) { $c = $p.GetPixel($x, $y); if ($c.A) { $pal += ($c.ToArgb() -band 0xFFFFFF) } } }; $p.Dispose()
  foreach ($rel in $Apply) {
    $path = Join-Path $repo $rel; $bk = Join-Path $backup ($rel -replace '[\\/]', '_')
    New-Item -ItemType Directory -Force $backup | Out-Null
    if ($Restore) { Copy-Item $bk $path -Force; "$rel restored"; continue }
    if (-not (Test-Path $bk)) { Copy-Item $path $bk }
    Copy-Item $bk $path -Force # from the original every time
    $shift = [Pal]::Apply($path, [int[]]$pal)
    # a picture far from the palette (a lime cloud, a yellow blast) loses what it is: keep its own colours
    if ($shift -gt $MaxShift) { Copy-Item $bk $path -Force; "{0} : kept (mean shift {1:N1})" -f $rel, $shift } else { "{0} : recoloured (mean shift {1:N1})" -f $rel, $shift }
  }
}
