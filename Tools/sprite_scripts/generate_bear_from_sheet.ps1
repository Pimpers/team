# Cuts the 8-pose bear sprite sheet (2 rows x 4 columns, black background, bear faces right) into the boss frames
# in Assets/Enemies/Bear/: black background removed (flood fill from the cell border), every frame cropped by the SAME
# window (so the body stays put from frame to frame), box-averaged down to a 64x64 canvas (alpha cut at 50%),
# feet on the bottom row.
#   idle   = poses 1,2          (top row)          move   = poses 1-4 (prowling walk)
#   attack = poses 5-8 (bottom row: paw raised, raised higher, swipe out, swipe through) - the hit lands on pose 7
#   hurt   = pose 4             death  = pose 2 darkened, then slumped (squashed) and darker
# Run from the project root (the sheet path is the only argument):
#   powershell -ExecutionPolicy Bypass -File generate_bear_from_sheet.ps1 -Sheet _art_source/bear/bear_sheet_8.png
# Then Unity: Cats Smash > Rebuild Stages (EnemyAssets picks the frames up).
param([string]$Sheet = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')) "_art_source/bear/bear_sheet_8.png"), [string]$OutDir = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')) "Assets/Enemies/Bear"), [int]$Threshold = 22)
Add-Type -AssemblyName System.Drawing
if (-not ('BearCut' -as [type])) {
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Imaging;
public static class BearCut {
    const int N = 64;
    static int[] Load(string path, out int w, out int h) {
        using (var b = new Bitmap(path)) {
            w = b.Width; h = b.Height;
            var bd = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var px = new int[w * h]; System.Runtime.InteropServices.Marshal.Copy(bd.Scan0, px, 0, px.Length); b.UnlockBits(bd); return px;
        }
    }
    static bool Dark(int p, int t) { return ((p >> 16) & 255) <= t && ((p >> 8) & 255) <= t && (p & 255) <= t; }

    // Returns the 8 frames as int[N*N] (0 = transparent, else 0xFFRRGGBB) and writes the PNGs.
    public static void Run(string sheet, string outDir, int t) {
        int W, H; var src = Load(sheet, out W, out H);
        var masks = new bool[8][]; var cx0 = new int[8]; var cy0 = new int[8]; var cw = new int[8]; var ch = new int[8];
        int ux0 = int.MaxValue, uy0 = int.MaxValue, ux1 = -1, uy1 = -1;
        for (int i = 0; i < 8; i++) {
            int x0 = (int)(W * (i % 4) / 4.0), x1 = (int)(W * (i % 4 + 1) / 4.0), y0 = (int)(H * (i / 4) / 2.0), y1 = (int)(H * (i / 4 + 1) / 2.0);
            int w = x1 - x0, h = y1 - y0; cx0[i] = x0; cy0[i] = y0; cw[i] = w; ch[i] = h;
            var bg = new bool[w * h]; var q = new Queue<int>();
            Action<int, int> seed = (x, y) => { int k = y * w + x; if (!bg[k] && Dark(src[(y0 + y) * W + x0 + x], t)) { bg[k] = true; q.Enqueue(k); } };
            for (int x = 0; x < w; x++) { seed(x, 0); seed(x, h - 1); } for (int y = 0; y < h; y++) { seed(0, y); seed(w - 1, y); }
            while (q.Count > 0) {
                int k = q.Dequeue(), x = k % w, y = k / w;
                if (x > 0) seed(x - 1, y); if (x < w - 1) seed(x + 1, y); if (y > 0) seed(x, y - 1); if (y < h - 1) seed(x, y + 1);
            }
            var m = new bool[w * h]; for (int k = 0; k < m.Length; k++) m[k] = !bg[k];
            // drop specks: keep connected blobs of at least 150 px
            var seen = new bool[w * h];
            for (int k = 0; k < m.Length; k++) {
                if (!m[k] || seen[k]) continue;
                var blob = new List<int>(); q.Enqueue(k); seen[k] = true;
                while (q.Count > 0) { int a = q.Dequeue(); blob.Add(a); int x = a % w, y = a / w;
                    foreach (var nb in new[] { x > 0 ? a - 1 : -1, x < w - 1 ? a + 1 : -1, y > 0 ? a - w : -1, y < h - 1 ? a + w : -1 })
                        if (nb >= 0 && m[nb] && !seen[nb]) { seen[nb] = true; q.Enqueue(nb); } }
                if (blob.Count < 150) foreach (var a in blob) m[a] = false;
            }
            masks[i] = m;
            for (int k = 0; k < m.Length; k++) if (m[k]) { int x = k % w, y = k / w; ux0 = Math.Min(ux0, x); ux1 = Math.Max(ux1, x); uy0 = Math.Min(uy0, y); uy1 = Math.Max(uy1, y); }
        }
        int uw = ux1 - ux0 + 1, uh = uy1 - uy0 + 1;
        double s = Math.Min((double)N / uw, (double)N / uh); // out px per source px
        double offX = (N - uw * s) / 2.0, offY = N - uh * s;
        Console.WriteLine("union window " + uw + "x" + uh + " px -> scale " + s.ToString("0.000") + " (" + (1.0 / s).ToString("0.0") + " source px per bear pixel)");
        var frames = new int[8][];
        for (int i = 0; i < 8; i++) {
            var f = new int[N * N];
            for (int oy = 0; oy < N; oy++) for (int ox = 0; ox < N; ox++) {
                double sx0 = ux0 + (ox - offX) / s, sx1 = ux0 + (ox + 1 - offX) / s, sy0 = uy0 + (oy - offY) / s, sy1 = uy0 + (oy + 1 - offY) / s;
                int ix0 = (int)Math.Floor(sx0), ix1 = (int)Math.Ceiling(sx1), iy0 = (int)Math.Floor(sy0), iy1 = (int)Math.Ceiling(sy1);
                long sa = 0, sr = 0, sg = 0, sb = 0, n = 0;
                for (int y = iy0; y < iy1; y++) for (int x = ix0; x < ix1; x++) {
                    n++; if (x < 0 || y < 0 || x >= cw[i] || y >= ch[i] || !masks[i][y * cw[i] + x]) continue;
                    int p = src[(cy0[i] + y) * W + cx0[i] + x]; sa++; sr += (p >> 16) & 255; sg += (p >> 8) & 255; sb += p & 255;
                }
                if (n > 0 && sa * 2 >= n) f[oy * N + ox] = unchecked((int)0xFF000000) | ((int)(sr / sa) << 16) | ((int)(sg / sa) << 8) | (int)(sb / sa);
            }
            frames[i] = f;
        }
        // Every pose of the sheet is mid-roar; the prowling / idle ones get the mouth shut so the bear is not screaming all the time.
        var calm = new int[4][]; for (int i = 0; i < 4; i++) calm[i] = CloseMouth(frames[i]);
        Save(calm[0], outDir, "idle_0"); Save(calm[1], outDir, "idle_1");
        Save(calm[0], outDir, "move_0"); Save(calm[1], outDir, "move_1"); Save(calm[2], outDir, "move_2"); Save(calm[3], outDir, "move_3");
        for (int i = 0; i < 4; i++) Save(frames[4 + i], outDir, "attack_" + i);
        Save(frames[3], outDir, "hurt_0");
        Save(Tint(calm[1], 0.55), outDir, "death_0");
        Save(Tint(Squash(calm[1], 0.55), 0.38), outDir, "death_1");
    }
    static bool IsRed(int p) { int r = (p >> 16) & 255, g = (p >> 8) & 255, b = p & 255; return r >= 110 && r > g * 3 / 2 && r > b * 3 / 2; }
    static bool IsLight(int p) { int r = (p >> 16) & 255, g = (p >> 8) & 255, b = p & 255; return r >= 170 && g >= 150 && b >= 150; }
    // Paints over the open mouth (red tongue + white teeth around it) with fur, and draws a dark mouth line along the bottom.
    static int[] CloseMouth(int[] f) {
        var o = (int[])f.Clone(); var mouth = new bool[N * N];
        for (int i = 0; i < f.Length; i++) if (f[i] != 0 && IsRed(f[i]) && (i / N) < N * 0.62) {
            int x = i % N, y = i / N;
            for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++) { int nx = x + dx, ny = y + dy; if (nx < 0 || ny < 0 || nx >= N || ny >= N) continue; int k = ny * N + nx; if (f[k] != 0 && (IsRed(f[k]) || IsLight(f[k]))) mouth[k] = true; }
        }
        int fur = unchecked((int)0xFF000000) | (46 << 16) | (46 << 8) | 54, line = unchecked((int)0xFF000000) | (20 << 16) | (18 << 8) | 22;
        for (int i = 0; i < o.Length; i++) if (mouth[i]) o[i] = fur;
        // mouth line: the lowest painted pixel of every painted column
        for (int x = 0; x < N; x++) { int low = -1; for (int y = 0; y < N; y++) if (mouth[y * N + x]) low = y; if (low >= 0) o[low * N + x] = line; }
        return o;
    }
    static int[] Tint(int[] f, double k) {
        var o = new int[f.Length];
        for (int i = 0; i < f.Length; i++) { if (f[i] == 0) continue; int r = (int)(((f[i] >> 16) & 255) * k), g = (int)(((f[i] >> 8) & 255) * k), b = (int)((f[i] & 255) * k); o[i] = unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b; }
        return o;
    }
    // Vertical squash toward the floor (the slumped corpse).
    static int[] Squash(int[] f, double k) {
        var o = new int[f.Length];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) { int sy = (int)(N - 1 - (N - 1 - y) / k); if (sy >= 0 && sy < N) o[y * N + x] = f[sy * N + x]; }
        return o;
    }
    // 1 px slate rim on the top and sides (not the floor side): the near-black bear vanished against the dark crimson stage.
    static int[] Rim(int[] f) {
        var o = (int[])f.Clone(); int rim = unchecked((int)0xFF000000) | (96 << 16) | (100 << 8) | 122;
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) {
            if (f[y * N + x] != 0) continue;
            bool up = y < N - 1 && f[(y + 1) * N + x] != 0, down = y > 0 && f[(y - 1) * N + x] != 0, l = x < N - 1 && f[y * N + x + 1] != 0, r = x > 0 && f[y * N + x - 1] != 0;
            if (up || l || r) o[y * N + x] = rim; // an opaque cell ABOVE the empty one is the floor side: skipped
        }
        return o;
    }
    static void Save(int[] f0, string dir, string name) {
        var f = Rim(f0);
        using (var b = new Bitmap(N, N, PixelFormat.Format32bppArgb)) {
            for (int i = 0; i < f.Length; i++) b.SetPixel(i % N, i / N, f[i] == 0 ? Color.FromArgb(0, 0, 0, 0) : Color.FromArgb(f[i]));
            b.Save(System.IO.Path.Combine(dir, "bear_" + name + ".png"), ImageFormat.Png);
        }
    }
}
"@
}
New-Item -ItemType Directory -Force $OutDir | Out-Null
[BearCut]::Run((Resolve-Path $Sheet).Path, (Resolve-Path $OutDir).Path, $Threshold)
Write-Output "bear frames written to $OutDir"
