# Brings a too-fine UI picture to the game's pixel size without redrawing it: shrinks it to -Px pixels across (each
# block takes its commonest opaque colour, clear when mostly clear), cuts it to -Colours by median cut, closes a 1 px
# outline of its darkest colour, and blows it back up to the original size by whole pixels (same file size, so the
# .meta and the layout stay). Originals go to _art_backup/icon_rescale_20261108 first; -Restore puts them back.
#   powershell -File Tools/icon_rescale.ps1 -Files Assets/UI/HammerIcon.png,... [-Px 16] [-Colours 10] [-Preview out.png]
# Why: the village menu icons were 128 px pictures shown at ~150 px (1.2 screen px per pixel) beside a world drawn at
# ~9 screen px per pixel - they read as a different game.
param([string[]]$Files, [int]$Px = 16, [int]$Colours = 10, [switch]$Restore, [string]$Preview)
$Files = @($Files | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$repo = Resolve-Path (Join-Path $PSScriptRoot '..')
$backup = Join-Path $repo '_art_backup\icon_rescale_20261108'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System; using System.Collections.Generic; using System.Drawing;
public static class IconRe {
    static int A(int c) { return (c >> 24) & 255; }
    static int Luma(int c) { return (((c >> 16) & 255) * 3 + ((c >> 8) & 255) * 6 + (c & 255)) / 10; }
    public static Bitmap Run(Bitmap src, int px, int colours) {
        int W = src.Width, H = src.Height, w = px, h = Math.Max(1, (int)Math.Round((double)px * H / W));
        var o = new int[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {
            int x0 = x * W / w, x1 = (x + 1) * W / w, y0 = y * H / h, y1 = (y + 1) * H / h, solid = 0, all = 0;
            var count = new Dictionary<int, int>();
            for (int sy = y0; sy < y1; sy++) for (int sx = x0; sx < x1; sx++) { all++; int c = src.GetPixel(sx, sy).ToArgb();
                if (A(c) >= 128) { solid++; c |= unchecked((int)0xFF000000); int n; count.TryGetValue(c, out n); count[c] = n + 1; } }
            if (solid * 2 < all) continue;
            int best = 0, bn = -1; foreach (var kv in count) if (kv.Value > bn) { bn = kv.Value; best = kv.Key; }
            o[y * w + x] = best;
        }
        // median cut
        var idx = new List<int>(); for (int i = 0; i < o.Length; i++) if (A(o[i]) > 0) idx.Add(i);
        var boxes = new List<List<int>> { idx };
        while (boxes.Count < colours) {
            int bb = -1, br = 0, bc = 0;
            for (int k = 0; k < boxes.Count; k++) for (int ch = 0; ch < 3; ch++) {
                int lo = 255, hi = 0; foreach (int i in boxes[k]) { int v = (o[i] >> (16 - ch * 8)) & 255; if (v < lo) lo = v; if (v > hi) hi = v; }
                if (hi - lo > br && boxes[k].Count > 1) { br = hi - lo; bb = k; bc = ch; } }
            if (bb < 0) break;
            int sh = 16 - bc * 8; var box = boxes[bb]; box.Sort((a, b) => ((o[a] >> sh) & 255).CompareTo((o[b] >> sh) & 255));
            int half = box.Count / 2; boxes[bb] = box.GetRange(0, half); boxes.Add(box.GetRange(half, box.Count - half));
        }
        int dark = 0, dl = 999;
        foreach (var box in boxes) { if (box.Count == 0) continue; long r = 0, g = 0, b = 0; foreach (int i in box) { r += (o[i] >> 16) & 255; g += (o[i] >> 8) & 255; b += o[i] & 255; }
            int n = box.Count, m = unchecked((int)0xFF000000) | (int)(r / n) << 16 | (int)(g / n) << 8 | (int)(b / n);
            foreach (int i in box) o[i] = m; if (Luma(m) < dl) { dl = Luma(m); dark = m; } }
        // outline: clear pixels touching a solid one that is not dark already
        var ring = new List<int>();
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) { if (A(o[y * w + x]) != 0) continue;
            for (int d = 0; d < 4; d++) { int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue; int c = o[ny * w + nx]; if (A(c) != 0 && Luma(c) >= 70) { ring.Add(y * w + x); break; } } }
        foreach (int i in ring) o[i] = dark;
        var res = new Bitmap(W, H, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) res.SetPixel(x, y, Color.FromArgb(o[Math.Min(h - 1, y * h / H) * w + Math.Min(w - 1, x * w / W)]));
        return res;
    }
}
'@
$done = @()
foreach ($rel in $Files) {
  $path = Join-Path $repo $rel; $bk = Join-Path $backup ($rel -replace '[\\/]', '_')
  if ($Restore) { Copy-Item $bk $path -Force; "$rel restored"; continue }
  New-Item -ItemType Directory -Force $backup | Out-Null
  if (-not (Test-Path $bk)) { Copy-Item $path $bk }
  $src = [Drawing.Bitmap]::new($bk) # always from the original, so a rerun with another -Px never shrinks twice
  $out = [IconRe]::Run($src, $Px, $Colours); $src.Dispose()
  if ($Preview) { $done += $out } else { $out.Save($path, [Drawing.Imaging.ImageFormat]::Png); $out.Dispose(); "$rel : ${Px} px, $Colours colours" }
}
if ($Preview -and $done) {
  $sheet = [Drawing.Bitmap]::new(160 * $done.Count, 160); $g = [Drawing.Graphics]::FromImage($sheet); $g.Clear([Drawing.Color]::FromArgb(40, 120, 150))
  for ($i = 0; $i -lt $done.Count; $i++) { $g.DrawImageUnscaled($done[$i], $i * 160 + 16, 16) }
  $sheet.Save($Preview); "preview $Preview"
}
