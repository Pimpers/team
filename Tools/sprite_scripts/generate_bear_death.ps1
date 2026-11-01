# Cuts the bear's death animation (bottom row of the walk/attack/death sheet: falling back, lying, flat; the
# first of the four bottom poses is the flinch and is not used) into Assets/Enemies/Bear/bear_death_0..2.png.
# Black background removed (flood fill from the border), all three frames cropped by the SAME window so they
# stay aligned, mirrored (the sheet's bear lies head-left; the game's sprites face right), box-averaged to an
# 80x64 canvas at the same pixel size as the 64x64 live frames (6.3 source px per bear pixel), 1 px slate rim
# on the top and sides like the other frames. Feet / belly on the bottom row.
# Run from the project root:  powershell -ExecutionPolicy Bypass -File generate_bear_death.ps1
# Then Unity: Cats Smash > Rebuild Stages (EnemyAssets picks the frames up).
param([string]$Sheet = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')) "_art_source/bear/bear_sheet_walk_attack_death.png"), [string]$OutDir = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')) "Assets/Enemies/Bear"), [int]$Threshold = 22, [double]$SrcPerPixel = 6.3)
Add-Type -AssemblyName System.Drawing
if (-not ('BearDeath' -as [type])) {
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Imaging;
public static class BearDeath {
    const int CW = 80, CH = 64;
    public static void Run(string sheet, string outDir, int t, double srcPerPixel) {
        int W, H; int[] src;
        using (var b = new Bitmap(sheet)) {
            W = b.Width; H = b.Height;
            var bd = b.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            src = new int[W * H]; System.Runtime.InteropServices.Marshal.Copy(bd.Scan0, src, 0, src.Length); b.UnlockBits(bd);
        }
        var bg = new bool[W * H]; var q = new Queue<int>();
        Func<int, bool> dark = k => { int p = src[k]; return ((p >> 16) & 255) <= t && ((p >> 8) & 255) <= t && (p & 255) <= t; };
        Action<int, int> seed = (x, y) => { int k = y * W + x; if (!bg[k] && dark(k)) { bg[k] = true; q.Enqueue(k); } };
        for (int x = 0; x < W; x++) { seed(x, 0); seed(x, H - 1); } for (int y = 0; y < H; y++) { seed(0, y); seed(W - 1, y); }
        while (q.Count > 0) { int k = q.Dequeue(), x = k % W, y = k / W; if (x > 0) seed(x - 1, y); if (x < W - 1) seed(x + 1, y); if (y > 0) seed(x, y - 1); if (y < H - 1) seed(x, y + 1); }
        var m = new bool[W * H]; for (int k = 0; k < m.Length; k++) m[k] = !bg[k];
        var seen = new bool[W * H]; var owner = new int[W * H]; for (int k = 0; k < owner.Length; k++) owner[k] = -1; int cellW0 = W / 4;
        for (int k = 0; k < m.Length; k++) {   // drop specks: keep blobs of at least 150 px
            if (!m[k] || seen[k]) continue;
            var blob = new List<int>(); q.Enqueue(k); seen[k] = true;
            while (q.Count > 0) { int a = q.Dequeue(); blob.Add(a); int x = a % W, y = a / W;
                foreach (var nb in new[] { x > 0 ? a - 1 : -1, x < W - 1 ? a + 1 : -1, y > 0 ? a - W : -1, y < H - 1 ? a + W : -1 }) if (nb >= 0 && m[nb] && !seen[nb]) { seen[nb] = true; q.Enqueue(nb); } }
            if (blob.Count < 150) { foreach (var a in blob) m[a] = false; continue; }
            long sx = 0; foreach (var a in blob) sx += a % W; int cell = (int)(sx / blob.Count) / cellW0;   // a blob belongs to the pose cell holding its centre
            foreach (var a in blob) owner[a] = cell;
        }
        // bottom row (third of three), poses 2-4 of 4 columns
        int rowY0 = H * 2 / 3, rowY1 = H, cellW = W / 4;
        int[] cols = { 1, 2, 3 };
        int ux0 = int.MaxValue, uy0 = int.MaxValue, ux1 = -1, uy1 = -1;
        foreach (int c in cols) for (int y = rowY0; y < rowY1; y++) for (int x = c * cellW; x < (c + 1) * cellW; x++) if (owner[y * W + x] == c) {
            int rx = x - c * cellW; ux0 = Math.Min(ux0, rx); ux1 = Math.Max(ux1, rx); uy0 = Math.Min(uy0, y); uy1 = Math.Max(uy1, y); }
        int uw = ux1 - ux0 + 1, uh = uy1 - uy0 + 1;
        double s = 1.0 / srcPerPixel;
        double offX = (CW - uw * s) / 2.0, offY = CH - uh * s;
        Console.WriteLine("union window " + uw + "x" + uh + " px -> " + (uw * s).ToString("0.0") + "x" + (uh * s).ToString("0.0") + " bear pixels on an " + CW + "x" + CH + " canvas");
        if (uw * s > CW || uh * s > CH) Console.WriteLine("WARNING: the union window does not fit the canvas - raise -SrcPerPixel");
        for (int i = 0; i < 3; i++) {
            int c = cols[i]; var f = new int[CW * CH];
            for (int oy = 0; oy < CH; oy++) for (int ox = 0; ox < CW; ox++) {
                double sx0 = (ox - offX) / s, sx1 = (ox + 1 - offX) / s, sy0 = uy0 + (oy - offY) / s, sy1 = uy0 + (oy + 1 - offY) / s;   // sx = mirrored window x (0 = right edge of the union)
                int ix0 = (int)Math.Floor(sx0), ix1 = (int)Math.Ceiling(sx1), iy0 = (int)Math.Floor(sy0), iy1 = (int)Math.Ceiling(sy1);
                long sa = 0, sr = 0, sg = 0, sb = 0, n = 0;
                for (int y = iy0; y < iy1; y++) for (int x = ix0; x < ix1; x++) {
                    n++; int rx = ux0 + uw - 1 - x;   // mirror inside the union window
                    int gx = c * cellW + rx; if (x < 0 || x >= uw || y < rowY0 || y >= rowY1 || gx < 0 || gx >= W || owner[y * W + gx] != c) continue;
                    int p = src[y * W + gx]; sa++; sr += (p >> 16) & 255; sg += (p >> 8) & 255; sb += p & 255;
                }
                if (n > 0 && sa * 2 >= n) f[oy * CW + ox] = unchecked((int)0xFF000000) | ((int)(sr / sa) << 16) | ((int)(sg / sa) << 8) | (int)(sb / sa);
            }
            Save(Rim(f), outDir, "death_" + i);
        }
    }
    // 1 px slate rim on the top and sides (an opaque cell above an empty one is the floor side: skipped).
    static int[] Rim(int[] f) {
        var o = (int[])f.Clone(); int rim = unchecked((int)0xFF000000) | (96 << 16) | (100 << 8) | 122;
        for (int y = 0; y < CH; y++) for (int x = 0; x < CW; x++) {
            if (f[y * CW + x] != 0) continue;
            bool up = y < CH - 1 && f[(y + 1) * CW + x] != 0, l = x < CW - 1 && f[y * CW + x + 1] != 0, r = x > 0 && f[y * CW + x - 1] != 0;
            if (up || l || r) o[y * CW + x] = rim;
        }
        return o;
    }
    static void Save(int[] f, string dir, string name) {
        using (var b = new Bitmap(CW, CH, PixelFormat.Format32bppArgb)) {
            for (int i = 0; i < f.Length; i++) b.SetPixel(i % CW, i / CW, f[i] == 0 ? Color.FromArgb(0, 0, 0, 0) : Color.FromArgb(f[i]));
            b.Save(System.IO.Path.Combine(dir, "bear_" + name + ".png"), ImageFormat.Png);
        }
    }
}
"@
}
[BearDeath]::Run((Resolve-Path $Sheet).Path, (Resolve-Path $OutDir).Path, $Threshold, $SrcPerPixel)
Write-Output "bear death frames written to $OutDir"
