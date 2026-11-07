# Cuts Aether's 3 x 3 skill-effect sheets (1024 x 1024, ~341 px cells, 9 frames) into the game's strips
# (Assets/Effects/<folder>/<id>.png, 9 square cells in a row): the ground keyed out (alpha 0 or 255), the cell
# borders cleared, one window for all nine frames (the union of what is drawn) shrunk by nearest neighbour into
# the cell, one palette per effect (no dithering). Originals to _art_backup/aether_20261105 first.
#   a_trap_fx (48 px, sitting on the cell's floor: its first frame is also the trap lying on the ground)
#   a_wire_fx (48 px), a_fish_fx (48 px; dark-grey ground - only near-identical greys go, the white bone stays),
#   k_loaf_fx (32 px)
# Usage: powershell -File Tools/aether_skillfx.ps1 -Src <folder with the *_raw.png sheets> -Set <n>
#   set 2: lure (48, the churu tube with its label - a real brand - painted out), k_night_fx (32), k_hiss_fx (64,
#   the waves only, no head), a_catnip_fx (48)
param([Parameter(Mandatory)] [string] $Src, [int] $Set = 1)   # -Set 1: trap, wire, fish, loaf; 2: lure, night, hiss, catnip
$repo = Resolve-Path (Join-Path $PSScriptRoot '..')
$bk = Join-Path $repo '_art_backup\aether_20261105'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Imaging;
public static class SFx {
    static int R(int c) { return (c >> 16) & 255; } static int G(int c) { return (c >> 8) & 255; } static int B(int c) { return c & 255; }
    static int A(int c) { return (c >> 24) & 255; }
    static int D(int a, int b) { return Math.Abs(R(a) - R(b)) + Math.Abs(G(a) - G(b)) + Math.Abs(B(a) - B(b)); }
    static int Lum(int c) { return (R(c) * 3 + G(c) * 6 + B(c)) / 10; }
    static bool Pink(int c) { return R(c) > 190 && G(c) < 150 && B(c) > 110; }

    // mode "label": the churu tube's label (a real brand's lettering and cat face) painted out in the tube's pink -
    //   the band between 22% and 72% of the pink tube's length, outline (dark) kept
    // mode "nohead": everything left of the drawn head's snout cleared (the sheet drew a whole black cat head; only
    //   the waves and sparks it breathes out are kept) - the window still spans the head, so they stay out in front
    public static string Run(string src, string dst, int cellOut, bool floor, int keyTol, int colours, int grid, string mode) {
        int w, h; int[] px;
        using (var b = new Bitmap(src)) { w = b.Width; h = b.Height; px = new int[w * h];
            var r = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            System.Runtime.InteropServices.Marshal.Copy(r.Scan0, px, 0, px.Length); b.UnlockBits(r); }
        int key = px[60 * w + 60];
        var cells = new List<int[]>(); int cs = w / grid;
        for (int cy = 0; cy < grid; cy++) for (int cx = 0; cx < grid; cx++) {
            int ox = cx * w / grid, oy = cy * h / grid; var c = new int[cs * cs];
            for (int y = 0; y < cs; y++) for (int x = 0; x < cs; x++) {
                int p = px[(oy + y) * w + ox + x];
                bool edge = x < 8 || y < 8 || x >= cs - 8 || y >= cs - 8;
                c[y * cs + x] = (edge || A(p) < 128 || D(p, key) < keyTol) ? 0 : (p | unchecked((int)0xFF000000));
            }
            cells.Add(c);
        }
        if (mode == "label" || mode == "flood") {   // a pale ground: only ground joined to the cell's edge goes (the tube's light parts are as pale)
            cells.Clear();
            for (int cy = 0; cy < grid; cy++) for (int cx = 0; cx < grid; cx++) {
                int ox = cx * w / grid, oy = cy * h / grid; var c = new int[cs * cs]; var gone = new bool[cs * cs]; var q = new Queue<int>();
                for (int y = 0; y < cs; y++) for (int x = 0; x < cs; x++) c[y * cs + x] = px[(oy + y) * w + ox + x] | unchecked((int)0xFF000000);
                for (int i = 0; i < cs; i++) foreach (int s in new[] { i, (cs - 1) * cs + i, i * cs, i * cs + cs - 1 }) if (!gone[s]) { gone[s] = true; q.Enqueue(s); }
                while (q.Count > 0) { int i = q.Dequeue(); if (D(c[i], key) >= keyTol && i % cs >= 8 && i % cs < cs - 8 && i / cs >= 8 && i / cs < cs - 8) { gone[i] = false; continue; }
                    int x = i % cs, y = i / cs;
                    foreach (int n in new[] { x > 0 ? i - 1 : -1, x < cs - 1 ? i + 1 : -1, y > 0 ? i - cs : -1, y < cs - 1 ? i + cs : -1 })
                        if (n >= 0 && !gone[n]) { gone[n] = true; q.Enqueue(n); } }
                for (int i = 0; i < c.Length; i++) if (gone[i]) c[i] = 0;
                cells.Add(c);
            }
        }
        if (mode == "droplast") cells.RemoveAt(cells.Count - 1);   // the sheet's last frame faded out half-transparent
        if (mode == "defringe") foreach (var c in cells)   // the magenta ground's blend into the soft cloud's edge
            for (int i = 0; i < c.Length; i++) if (c[i] != 0 && R(c[i]) > G(c[i]) + 20 && B(c[i]) > G(c[i]) + 10) c[i] = 0;
        if (mode == "label") foreach (var c in cells) {
            int px0 = cs, px1 = -1, py0 = cs, py1 = -1; long pr = 0, pg = 0, pb = 0, pn = 0;
            for (int i = 0; i < c.Length; i++) if (c[i] != 0 && Pink(c[i])) { int x = i % cs, y = i / cs;
                if (x < px0) px0 = x; if (x > px1) px1 = x; if (y < py0) py0 = y; if (y > py1) py1 = y; pr += R(c[i]); pg += G(c[i]); pb += B(c[i]); pn++; }
            if (pn == 0) continue;
            int pink = unchecked((int)0xFF000000) | (int)(pr / pn) << 16 | (int)(pg / pn) << 8 | (int)(pb / pn);
            int a = px0 + (px1 - px0) * 22 / 100, z = px0 + (px1 - px0) * 72 / 100;
            for (int y = py0; y <= py1; y++) for (int x = a; x <= z; x++) { int i = y * cs + x; if (c[i] != 0 && Lum(c[i]) >= 60) c[i] = pink; }
        }
        int[] full = null;
        if (mode == "nohead") {
            full = Union(cells, cs);
            foreach (var c in cells) {
                int snout = -1;
                for (int i = 0; i < c.Length; i++) if (c[i] != 0 && Lum(c[i]) < 90 && !(R(c[i]) > 150)) snout = Math.Max(snout, i % cs);
                for (int i = 0; i < c.Length; i++) if (i % cs <= snout + 2) c[i] = 0;
            }
        }
        if (floor) {   // each frame dropped so its own bottom sits on the lowest one's: the sheet's rows drew it at three heights
            var bottoms = new List<int>(); int lowest = 0;
            foreach (var c in cells) { int bt = -1; for (int i = c.Length - 1; i >= 0 && bt < 0; i--) if (c[i] != 0) bt = i / cs; bottoms.Add(bt); lowest = Math.Max(lowest, bt); }
            for (int k = 0; k < cells.Count; k++) { int dy = lowest - bottoms[k]; if (bottoms[k] < 0 || dy == 0) continue;
                var c = cells[k]; var o = new int[c.Length];
                for (int i = 0; i < c.Length; i++) if (c[i] != 0) { int y = i / cs + dy; if (y < cs) o[y * cs + i % cs] = c[i]; }
                cells[k] = o; }
        }
        var u = full ?? Union(cells, cs); int x0 = u[0], y0 = u[1], x1 = u[2], y1 = u[3];
        int side = Math.Max(x1 - x0 + 1, y1 - y0 + 1);
        int sx = x0 - (side - (x1 - x0 + 1)) / 2;
        int sy = floor ? y1 + 1 - side : y0 - (side - (y1 - y0 + 1)) / 2;   // floor: the drawing's bottom on the cell's bottom
        var frames = new List<int[]>();
        foreach (var c in cells) { var o = new int[cellOut * cellOut];
            for (int y = 0; y < cellOut; y++) for (int x = 0; x < cellOut; x++) {
                int fx = sx + (int)((x + 0.5) * side / cellOut), fy = sy + (int)((y + 0.5) * side / cellOut);
                if (fx >= 0 && fy >= 0 && fx < cs && fy < cs) o[y * cellOut + x] = c[fy * cs + fx]; }
            frames.Add(o); }
        Quantize(frames, colours);
        int nf = frames.Count;
        var strip = new int[cellOut * nf * cellOut];
        for (int f = 0; f < nf; f++) for (int y = 0; y < cellOut; y++) for (int x = 0; x < cellOut; x++) strip[y * cellOut * nf + f * cellOut + x] = frames[f][y * cellOut + x];
        using (var b = new Bitmap(cellOut * nf, cellOut, PixelFormat.Format32bppArgb)) {
            var r = b.LockBits(new Rectangle(0, 0, cellOut * nf, cellOut), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            System.Runtime.InteropServices.Marshal.Copy(strip, 0, r.Scan0, strip.Length); b.UnlockBits(r); b.Save(dst, ImageFormat.Png); }
        return (cellOut * nf) + "x" + cellOut + " (drawing " + (x1 - x0 + 1) + "x" + (y1 - y0 + 1) + " of " + cs + ")";
    }
    static int[] Union(List<int[]> cells, int cs) {
        int x0 = cs, y0 = cs, x1 = -1, y1 = -1;
        foreach (var c in cells) for (int i = 0; i < c.Length; i++) if (c[i] != 0) { int x = i % cs, y = i / cs;
            if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
        return new[] { x0, y0, x1, y1 };
    }
    static void Quantize(List<int[]> frames, int colours) {
        var all = new List<long>();
        for (int f = 0; f < frames.Count; f++) for (int i = 0; i < frames[f].Length; i++) if (A(frames[f][i]) > 0) all.Add(((long)f << 32) | (uint)i);
        if (all.Count == 0) return;
        var src = new List<int[]>(); foreach (var fr in frames) src.Add((int[])fr.Clone());
        Func<long, int> col = k => src[(int)(k >> 32)][(int)(k & 0xFFFFFFFF)];
        var boxes = new List<List<long>> { all };
        while (boxes.Count < colours) {
            int bk = -1, br = 0, bc = 0;
            for (int k = 0; k < boxes.Count; k++) for (int ch = 0; ch < 3; ch++) {
                int lo = 255, hi = 0; foreach (var e in boxes[k]) { int v = (col(e) >> (16 - ch * 8)) & 255; if (v < lo) lo = v; if (v > hi) hi = v; }
                if (hi - lo > br && boxes[k].Count > 1) { br = hi - lo; bk = k; bc = ch; } }
            if (bk < 0) break;
            int sh = 16 - bc * 8; var box = boxes[bk];
            box.Sort((a, b) => ((col(a) >> sh) & 255).CompareTo((col(b) >> sh) & 255));
            int half = box.Count / 2; boxes[bk] = box.GetRange(0, half); boxes.Add(box.GetRange(half, box.Count - half));
        }
        foreach (var box in boxes) {
            long r = 0, g = 0, b = 0; foreach (var e in box) { int c = col(e); r += R(c); g += G(c); b += B(c); }
            int n = box.Count; int m = unchecked((int)0xFF000000) | (int)(r / n) << 16 | (int)(g / n) << 8 | (int)(b / n);
            foreach (var e in box) frames[(int)(e >> 32)][(int)(e & 0xFFFFFFFF)] = m;
        }
    }
}
'@
# sheet, folder, id, cell, on the floor, key tolerance, colours, grid, mode
$sets = @{
  1 = @(@('skill_trap', 'Archer', 'a_trap_fx', 48, $true, 150, 6, 3, ''), @('skill_wire', 'Archer', 'a_wire_fx', 48, $false, 150, 6, 3, ''),
        @('skill_fish', 'Archer', 'a_fish_fx', 48, $false, 40, 4, 3, ''), @('skill_loaf', 'Knight', 'k_loaf_fx', 32, $false, 150, 6, 3, ''))
  2 = @(@('skill2_lure', 'BDragon', 'lure', 48, $true, 40, 6, 3, 'label'), @('skill2_night', 'Knight', 'k_night_fx', 32, $false, 150, 4, 3, ''),
        @('skill2_hiss', 'Knight', 'k_hiss_fx', 64, $false, 60, 4, 3, 'nohead'), @('skill2_catnip', 'Archer', 'a_catnip_fx', 48, $false, 150, 6, 3, 'defringe'))
  3 = @(@('skill3_charm', 'BDragon', 'charm', 32, $false, 40, 6, 3, 'flood'), @('skill3_jelly', 'Archer', 'a_jelly_fx', 64, $false, 40, 4, 3, 'flood'),
        @('skill3_snap', 'BDragon', 'trap_snap', 48, $true, 150, 4, 3, ''))   # k_groom_fx left out: without its nose and whiskers only a floating tongue was left
  4 = @(@('skill4_watch', 'Archer', 'a_watch_fx', 48, $true, 150, 6, 3, 'defringe'), @('skill4_can', 'Archer', 'a_can_fx', 64, $false, 60, 6, 2, 'droplast'),
        @('skill4_canhit', 'BDragon', 'beam_end', 48, $false, 150, 6, 3, ''), @('skill4_rapid', 'Archer', 'a_rapid_fx', 64, $false, 150, 4, 3, ''))
}
foreach ($j in $sets[$Set]) {
  $dst = Join-Path $repo "Assets\Effects\$($j[1])\$($j[2]).png"
  $b = Join-Path $bk $j[1]; New-Item -ItemType Directory -Force $b | Out-Null
  if (-not (Test-Path (Join-Path $b "$($j[2]).png"))) { Copy-Item $dst $b }
  "$($j[2]) : " + [SFx]::Run((Join-Path $Src "$($j[0])_raw.png"), $dst, $j[3], $j[4], $j[5], $j[6], $j[7], $j[8])
}
