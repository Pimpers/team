# Brings the BDragon effects to the cats' 32 px pixel (most were 64 px cells drawn at x0.5: pixels half the cats') -
# cats' pixel: each sheet shrunk to 32 px cells (a 2 x 2 block keeps its most solid pixel, so thin strokes
# survive), alpha cut at 128, at most 6 colours (median cut, no dithering) - the clean arcade look of the
# Archer / Knight / Combo effects. VfxAssets then draws them at x1 (same size on screen).
# Usage: powershell -File Tools/fx_to_32.ps1    (originals are copied to _art_backup/fx_to_32 first)
$repo = Resolve-Path (Join-Path $PSScriptRoot '..')
$dir = Join-Path $repo 'Assets\Effects\BDragon'
$backup = Join-Path $repo '_art_backup\fx_to_32'
$ids = 'slash','slash_gold','xcut','rupture','parry','charge','poison_hit','trap_snap','lure','land_dust',
       'beam_end','aura_hit','yarn_hit','smoke','sparkle','watch',
       'explosion','tower_slam','beam','hiss_wave','charm' # these five drew 64 px at x1 (big and busier than the rest): now 32 px at x1, half the size
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Imaging;
public static class Fx32 {
    static int A(int c) { return (c >> 24) & 255; }
    public static void Run(string path, int colours) {
        int w, h; int[] px;
        using (var b = new Bitmap(path)) {
            w = b.Width; h = b.Height; px = new int[w * h];
            var r = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            System.Runtime.InteropServices.Marshal.Copy(r.Scan0, px, 0, px.Length); b.UnlockBits(r);
        }
        int dw = w / 2, dh = h / 2; var o = new int[dw * dh];
        for (int y = 0; y < dh; y++) for (int x = 0; x < dw; x++) {
            int best = 0;
            for (int j = 0; j < 2; j++) for (int i = 0; i < 2; i++) { int c = px[(y * 2 + j) * w + x * 2 + i]; if (A(c) > A(best)) best = c; }
            o[y * dw + x] = A(best) >= 128 ? best | unchecked((int)0xFF000000) : 0;
        }
        var idx = new List<int>(); for (int i = 0; i < o.Length; i++) if (o[i] != 0) idx.Add(i);
        if (idx.Count > 0) {
            var boxes = new List<List<int>> { idx };
            while (boxes.Count < colours) {
                int bk = -1, br = 0, bc = 0;
                for (int k = 0; k < boxes.Count; k++) for (int ch = 0; ch < 3; ch++) {
                    int lo = 255, hi = 0; foreach (int i in boxes[k]) { int v = (o[i] >> (16 - ch * 8)) & 255; if (v < lo) lo = v; if (v > hi) hi = v; }
                    if (hi - lo > br && boxes[k].Count > 1) { br = hi - lo; bk = k; bc = ch; }
                }
                if (bk < 0) break;
                int sh = 16 - bc * 8; var box = boxes[bk];
                box.Sort((a, b2) => ((o[a] >> sh) & 255).CompareTo((o[b2] >> sh) & 255));
                int half = box.Count / 2; boxes[bk] = box.GetRange(0, half); boxes.Add(box.GetRange(half, box.Count - half));
            }
            var q = (int[])o.Clone();
            foreach (var box in boxes) {
                long rr = 0, gg = 0, bb = 0; foreach (int i in box) { rr += (o[i] >> 16) & 255; gg += (o[i] >> 8) & 255; bb += o[i] & 255; }
                int n = box.Count; int c = unchecked((int)0xFF000000) | (int)(rr / n) << 16 | (int)(gg / n) << 8 | (int)(bb / n);
                foreach (int i in box) q[i] = c;
            }
            o = q;
        }
        using (var nb = new Bitmap(dw, dh, PixelFormat.Format32bppArgb)) {
            var r = nb.LockBits(new Rectangle(0, 0, dw, dh), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            System.Runtime.InteropServices.Marshal.Copy(o, 0, r.Scan0, o.Length); nb.UnlockBits(r);
            nb.Save(path, ImageFormat.Png);
        }
    }
}
'@
New-Item -ItemType Directory -Force $backup | Out-Null
foreach ($id in $ids) {
    $f = Join-Path $dir "$id.png"
    if (-not (Test-Path $f)) { "missing $id"; continue }
    $bk = Join-Path $backup "$id.png"
    if (-not (Test-Path $bk)) { Copy-Item $f $bk }
    elseif ((Get-Item $f).Length -ne (Get-Item $bk).Length) { "skip $id (already converted: restore it from the backup to redo)"; continue }
    [Fx32]::Run($f, 6)
    "$id ok"
}
