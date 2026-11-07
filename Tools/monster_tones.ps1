# Cuts a monster's frames (Assets/Enemies/<Name>/*.png) to one shared palette of -Colours (default 12: about three
# materials x 3-4 tones + outline) by median cut over all its frames at once, so every frame uses the same tones, and
# closes its 1 px dark outline: a clear pixel next to a solid one that is not dark already takes the darkest colour.
# Originals go to _art_backup/monster_tones_20261107/<Name> first; -Restore puts them back. .meta files untouched.
#   powershell -File Tools/monster_tones.ps1 -Names Slime,Mushroom,Bear [-Colours 12] [-Restore]
param([string[]]$Names = @('Slime', 'Mushroom', 'Bear'), [int]$Colours = 12, [switch]$Restore,
  [switch]$NoOutline) # the mushroom: its spore puffs outlined turned to blots
$Names = @($Names | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$repo = Resolve-Path (Join-Path $PSScriptRoot '..')
$backup = Join-Path $repo '_art_backup\monster_tones_20261107'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Imaging;
public static class MTones {
    static int A(int c) { return (c >> 24) & 255; }
    static int Luma(int c) { return (((c >> 16) & 255) * 3 + ((c >> 8) & 255) * 6 + (c & 255)) / 10; }
    static int[] Read(string p, out int w, out int h) {
        using (var b = new Bitmap(p)) { w = b.Width; h = b.Height;
            var r = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var px = new int[w * h]; System.Runtime.InteropServices.Marshal.Copy(r.Scan0, px, 0, px.Length); b.UnlockBits(r); return px; }
    }
    static void Save(string p, int[] px, int w, int h) {
        using (var b = new Bitmap(w, h, PixelFormat.Format32bppArgb)) {
            var r = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            System.Runtime.InteropServices.Marshal.Copy(px, 0, r.Scan0, px.Length); b.UnlockBits(r); b.Save(p, ImageFormat.Png); }
    }
    public static string Run(string[] files, int colours, bool outline) {
        var frames = new List<int[]>(); var ws = new List<int>(); var hs = new List<int>();
        var all = new List<int>(); // opaque colours of every frame, one entry per pixel
        foreach (var f in files) { int w, h; var px = Read(f, out w, out h); frames.Add(px); ws.Add(w); hs.Add(h);
            foreach (int c in px) if (A(c) >= 128) all.Add(c & 0xFFFFFF); }
        // median cut over the pixels of all frames together
        var boxes = new List<List<int>> { all };
        while (boxes.Count < colours) {
            int best = -1, bestRange = 0, bestCh = 0;
            for (int k = 0; k < boxes.Count; k++) for (int ch = 0; ch < 3; ch++) {
                int lo = 255, hi = 0; foreach (int c in boxes[k]) { int v = (c >> (16 - ch * 8)) & 255; if (v < lo) lo = v; if (v > hi) hi = v; }
                if (hi - lo > bestRange && boxes[k].Count > 1) { bestRange = hi - lo; best = k; bestCh = ch; } }
            if (best < 0) break;
            int sh = 16 - bestCh * 8; var box = boxes[best];
            box.Sort((a, b) => ((a >> sh) & 255).CompareTo((b >> sh) & 255));
            int half = box.Count / 2; boxes[best] = box.GetRange(0, half); boxes.Add(box.GetRange(half, box.Count - half));
        }
        var map = new Dictionary<int, int>(); int darkest = 0, dl = 999;
        foreach (var box in boxes) {
            long r = 0, g = 0, b = 0; foreach (int c in box) { r += (c >> 16) & 255; g += (c >> 8) & 255; b += c & 255; }
            int n = box.Count, m = unchecked((int)0xFF000000) | (int)(r / n) << 16 | (int)(g / n) << 8 | (int)(b / n);
            foreach (int c in box) map[c] = m;
            if (Luma(m) < dl) { dl = Luma(m); darkest = m; }
        }
        int added = 0; int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
        for (int fi = 0; fi < frames.Count; fi++) {
            var px = frames[fi]; int w = ws[fi], h = hs[fi];
            for (int i = 0; i < px.Length; i++) px[i] = A(px[i]) >= 128 ? map[px[i] & 0xFFFFFF] : 0;
            var ring = new List<int>();
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {
                if (A(px[y * w + x]) != 0) continue;
                for (int k = 0; k < 4; k++) { int nx = x + dx[k], ny = y + dy[k];
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int c = px[ny * w + nx]; if (A(c) != 0 && Luma(c) >= 70) { ring.Add(y * w + x); break; } }
            }
            if (outline) { foreach (int i in ring) px[i] = darkest; added += ring.Count; }
            Save(files[fi], px, w, h);
        }
        return files.Length + " frames, " + boxes.Count + " colours, outline +" + added + " px";
    }
}
'@
foreach ($name in $Names) {
  $dir = Join-Path $repo "Assets\Enemies\$name"; $bk = Join-Path $backup $name
  if ($Restore) { Copy-Item (Join-Path $bk '*.png') $dir -Force; "$name restored"; continue }
  New-Item -ItemType Directory -Force $bk | Out-Null
  $files = @(Get-ChildItem $dir -Filter *.png | Sort-Object Name)
  foreach ($f in $files) { $d = Join-Path $bk $f.Name; if (-not (Test-Path $d)) { Copy-Item $f.FullName $d } }
  "$name : $([MTones]::Run([string[]]($files | ForEach-Object FullName), $Colours, -not $NoOutline))"
}
