# Turns Aether's 4 x 4 effect sheets (1024 x 1024, 256 px cells, 16 frames on a chroma ground) into the game's
# frames: the ground keyed out (alpha 0 or 255 only), the cell grid lines and the frame numbers in the corners
# cleared, each frame cut to what the game shows, shrunk by nearest neighbour to the old effect's size and cut
# to a few colours (no dithering). Originals go to _art_backup/aether_20261105 first.
#   volcano   -> Resources/Ambient/volcano/volcano_00..15   only the eruption above the crater (the world map has its
#                own volcano painted; the sheet's cone would sit on top of it), crater lined up frame to frame
#   campfire  -> Resources/Ambient/campfire/campfire_00..15 logs and fire; its purple smoke turned grey-brown
#   chimney   -> Resources/Ambient/chimney/chimney_00..15   the smoke only (the village's roofs have their chimneys)
#   explosion -> Effects/BDragon/explosion.png              a 16 x 1 strip of 32 px cells
# Usage: powershell -File Tools/aether_fx.ps1 -Src <folder with fx_*_raw.png>
param([Parameter(Mandatory)] [string] $Src)
$repo = Resolve-Path (Join-Path $PSScriptRoot '..')
$bk = Join-Path $repo '_art_backup\aether_20261105'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Imaging;
public static class AFx {
    public static int[] Read(string path, out int w, out int h) {
        using (var b = new Bitmap(path)) {
            w = b.Width; h = b.Height; var px = new int[w * h];
            var r = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            System.Runtime.InteropServices.Marshal.Copy(r.Scan0, px, 0, px.Length); b.UnlockBits(r); return px;
        }
    }
    public static void Write(int[] px, int w, int h, string path) {
        using (var b = new Bitmap(w, h, PixelFormat.Format32bppArgb)) {
            var r = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            System.Runtime.InteropServices.Marshal.Copy(px, 0, r.Scan0, px.Length); b.UnlockBits(r); b.Save(path, ImageFormat.Png);
        }
    }
    static int R(int c) { return (c >> 16) & 255; } static int G(int c) { return (c >> 8) & 255; } static int B(int c) { return c & 255; }
    static int A(int c) { return (c >> 24) & 255; }
    static int D(int a, int b) { return Math.Abs(R(a) - R(b)) + Math.Abs(G(a) - G(b)) + Math.Abs(B(a) - B(b)); }

    // One 256 px cell, cleaned: key colour (and near it) out, a 4 px border (grid lines) out, the top-left corner
    // where the frame number sits out.
    public static int[] Cell(int[] sheet, int sw, int cx, int cy, int cell, int key) {
        var o = new int[cell * cell];
        for (int y = 0; y < cell; y++) for (int x = 0; x < cell; x++) {
            int c = sheet[(cy * cell + y) * sw + cx * cell + x];
            bool edge = x < 4 || y < 4 || x >= cell - 4 || y >= cell - 4;
            bool number = x < 56 && y < 46;
            o[y * cell + x] = (edge || number || A(c) < 128 || D(c, key) < 150) ? 0 : (c | unchecked((int)0xFF000000));
        }
        return o;
    }

    // The top of the cone (volcano) or of the chimney (cap and bricks): scanning up from the drawing's bottom, the
    // first row whose count of the base's pixels falls under 45% of the widest such row below it. A count, not a
    // run: the cone's lighter streaks broke every run. Base: anything not bright (the lava, sparks and the smoke
    // are; the ash cone, the slate cap and the bricks are not).
    public static int BaseTop(int[] px, int cell, int kind, int unused) {
        int bottom = -1;
        for (int y = cell - 1; y >= 0 && bottom < 0; y--) for (int x = 0; x < cell; x++) if (A(px[y * cell + x]) > 0) { bottom = y; break; }
        if (bottom < 0) return cell;
        int widest = 0;
        for (int y = bottom; y >= 0; y--) {
            int n = 0;
            for (int x = 0; x < cell; x++) { int c = px[y * cell + x]; if (A(c) > 0 && IsBase(c, kind)) n++; }
            if (n > widest) widest = n;
            if (widest >= 12 && n * 100 < widest * 45) return y + 1;
        }
        return 0;
    }
    static int Lum(int c) { return (R(c) * 3 + G(c) * 6 + B(c)) / 10; }
    static bool IsBase(int c, int kind) {
        if (kind == 1) return Lum(c) < 110 && !(R(c) > 170 && G(c) < 120);            // ash, not lava
        return Lum(c) < 140;                                                          // cap and bricks, not the pale smoke
    }
    static bool Purple(int c) { return B(c) > G(c) + 25 && R(c) > G(c) + 10 && B(c) > 60; }

    // The campfire's purple smoke to a grey-brown ramp, by brightness.
    public static void SmokeToAsh(int[] px) {
        int[] ramp = { unchecked((int)0xFF5a4e48), unchecked((int)0xFF7a6c62), unchecked((int)0xFF9a8a7e) };
        for (int i = 0; i < px.Length; i++) { int c = px[i]; if (A(c) == 0 || !Purple(c)) continue;
            int l = (R(c) * 3 + G(c) * 6 + B(c)) / 10; px[i] = ramp[Math.Min(ramp.Length - 1, l * ramp.Length / 200)]; }
    }

    public static void ClearBelow(int[] px, int cell, int row) { for (int i = Math.Max(0, row) * cell; i < px.Length; i++) px[i] = 0; }

    // Nearest neighbour: the pixel at each destination cell's centre.
    public static int[] Shrink(int[] px, int w, int h, int sx, int sy, int sw, int sh, int dw, int dh) {
        var o = new int[dw * dh];
        for (int y = 0; y < dh; y++) for (int x = 0; x < dw; x++) {
            int fx = sx + (int)((x + 0.5) * sw / dw), fy = sy + (int)((y + 0.5) * sh / dh);
            if (fx >= 0 && fy >= 0 && fx < w && fy < h) o[y * dw + x] = px[fy * w + fx];
        }
        return o;
    }

    // The drawn box of a cell shifted down by dy: x0, y0, x1, y1 (x1 < 0 when empty).
    public static int[] Bounds(int[] px, int cell, int dy) {
        int x0 = cell, y0 = int.MaxValue, x1 = -1, y1 = -1;
        for (int i = 0; i < px.Length; i++) if (px[i] != 0) { int x = i % cell, y = i / cell + dy;
            if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
        return new[] { x0, y0, x1, y1 };
    }

    // Median cut over every frame together (one palette for the whole effect), no dithering.
    public static void Quantize(List<int[]> frames, int colours) {
        var all = new List<long>();
        for (int f = 0; f < frames.Count; f++) for (int i = 0; i < frames[f].Length; i++) if (A(frames[f][i]) > 0) all.Add(((long)f << 32) | (uint)i);
        if (all.Count == 0) return;
        Func<long, int> col = k => frames[(int)(k >> 32)][(int)(k & 0xFFFFFFFF)];
        var boxes = new List<List<long>> { all };
        while (boxes.Count < colours) {
            int bk = -1, br = 0, bc = 0;
            for (int k = 0; k < boxes.Count; k++) for (int ch = 0; ch < 3; ch++) {
                int lo = 255, hi = 0; foreach (var e in boxes[k]) { int v = (col(e) >> (16 - ch * 8)) & 255; if (v < lo) lo = v; if (v > hi) hi = v; }
                if (hi - lo > br && boxes[k].Count > 1) { br = hi - lo; bk = k; bc = ch; }
            }
            if (bk < 0) break;
            int sh = 16 - bc * 8; var box = boxes[bk];
            box.Sort((a, b) => ((col(a) >> sh) & 255).CompareTo((col(b) >> sh) & 255));
            int half = box.Count / 2; boxes[bk] = box.GetRange(0, half); boxes.Add(box.GetRange(half, box.Count - half));
        }
        var outFrames = new List<int[]>(); foreach (var fr in frames) outFrames.Add((int[])fr.Clone());
        foreach (var box in boxes) {
            long r = 0, g = 0, b = 0; foreach (var e in box) { int c = col(e); r += R(c); g += G(c); b += B(c); }
            int n = box.Count; int m = unchecked((int)0xFF000000) | (int)(r / n) << 16 | (int)(g / n) << 8 | (int)(b / n);
            foreach (var e in box) outFrames[(int)(e >> 32)][(int)(e & 0xFFFFFFFF)] = m;
        }
        for (int f = 0; f < frames.Count; f++) Array.Copy(outFrames[f], frames[f], frames[f].Length);
    }
}
'@

function Process($name, [int]$targetH, [int]$colours, [int]$cutKind, [int]$cellOut) {
  $sw = 0; $sh = 0
  $sheet = [AFx]::Read((Join-Path $Src "fx_${name}_raw.png"), [ref]$sw, [ref]$sh)
  $key = $sheet[60 * $sw + 128]   # the chroma ground, inside the first cell below its number, clear of the grid
  $cells = New-Object 'System.Collections.Generic.List[int[]]'; $base = @()
  for ($cy = 0; $cy -lt 4; $cy++) { for ($cx = 0; $cx -lt 4; $cx++) {
    $c = [AFx]::Cell($sheet, $sw, $cx, $cy, 256, $key)
    $t = 256; if ($cutKind -gt 0) { $t = [AFx]::BaseTop($c, 256, $cutKind, $(if ($cutKind -eq 1) { 40 } else { 30 })); [AFx]::ClearBelow($c, 256, $t) }
    if ($name -eq 'campfire' -or $name -eq 'volcano') { [AFx]::SmokeToAsh($c) }   # purple smoke (and the magenta key's tint) to ash
    $cells.Add($c); $base += $t } }
  # one window for every frame: the union of what is drawn, each frame shifted so its cut line sits on one row
  $line = ($base | Measure-Object -Maximum).Maximum
  $x0 = 256; $y0 = 99999; $x1 = -1; $y1 = -1
  for ($f = 0; $f -lt 16; $f++) { $bb = [AFx]::Bounds($cells[$f], 256, $line - $base[$f]); if ($bb[2] -lt 0) { continue }
    $x0 = [Math]::Min($x0, $bb[0]); $y0 = [Math]::Min($y0, $bb[1]); $x1 = [Math]::Max($x1, $bb[2]); $y1 = [Math]::Max($y1, $bb[3]) }
  $ww = $x1 - $x0 + 1; $hh = $y1 - $y0 + 1
  if ($cellOut -gt 0) { $side = [Math]::Max($ww, $hh); $x0 -= [int](($side - $ww) / 2); $y0 -= [int](($side - $hh) / 2); $ww = $side; $hh = $side; $dw = $cellOut; $dh = $cellOut }
  else { $dh = $targetH; $dw = [Math]::Max(1, [int][Math]::Round($ww * $targetH / $hh)) }
  $frames = New-Object 'System.Collections.Generic.List[int[]]'
  for ($f = 0; $f -lt 16; $f++) { $frames.Add([AFx]::Shrink($cells[$f], 256, 256, $x0, $y0 - ($line - $base[$f]), $ww, $hh, $dw, $dh)) }
  [AFx]::Quantize($frames, $colours)
  return @{ frames = $frames; w = $dw; h = $dh }
}

foreach ($spec in @(@('volcano', 96, 8, 1), @('campfire', 96, 8, 0), @('chimney', 48, 4, 2))) {
  $name = $spec[0]; $dir = Join-Path $repo "Assets\Resources\Ambient\$name"
  $b = Join-Path $bk "Ambient\$name"; if (-not (Test-Path $b)) { New-Item -ItemType Directory -Force $b | Out-Null; Copy-Item (Join-Path $dir '*') $b }
  $r = Process $name $spec[1] $spec[2] $spec[3] 0
  for ($f = 0; $f -lt 16; $f++) { [AFx]::Write($r.frames[$f], $r.w, $r.h, (Join-Path $dir ('{0}_{1:00}.png' -f $name, $f))) }
  # the old frames past 15 go (with their .meta); they are in the backup
  Get-ChildItem $dir -Filter "${name}_*.png" | Where-Object { [int]($_.BaseName -replace '.*_', '') -ge 16 } | ForEach-Object { Remove-Item $_.FullName; Remove-Item "$($_.FullName).meta" -ErrorAction SilentlyContinue }
  "$name : 16 frames of $($r.w)x$($r.h)"
}

$ex = Join-Path $repo 'Assets\Effects\BDragon\explosion.png'
$b = Join-Path $bk 'BDragon'; New-Item -ItemType Directory -Force $b | Out-Null; if (-not (Test-Path (Join-Path $b 'explosion.png'))) { Copy-Item $ex $b }
$r = Process 'explosion' 0 6 0 32
$strip = New-Object 'int[]' (32 * 16 * 32)
for ($f = 0; $f -lt 16; $f++) { for ($y = 0; $y -lt 32; $y++) { for ($x = 0; $x -lt 32; $x++) { $strip[$y * 512 + $f * 32 + $x] = $r.frames[$f][$y * 32 + $x] } } }
[AFx]::Write($strip, 512, 32, $ex)
"explosion : 16 frames of 32x32 (512x32 strip)"
