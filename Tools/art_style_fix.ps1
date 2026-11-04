# Brings art that fails the style rules (memory art-style-spec: 16-bit, no blur, a few tones per material, 1 px
# dark outline on characters/objects) back in line, in place. PNG content only: the .meta files (GUIDs, import
# settings) are left alone. Input: artscan2.csv (Folder, File, Verdict, Why); BackgroundGif and the HP icon
# (HealthHeartSystem) are never touched. Every file is copied to _art_backup/style_fix_20261104/ first.
#   blur  -> alpha to 0 or 255 (cut at 128)
#   tones -> the image's colours cut to 24 by median cut (the scan allows 24; whole sheets of several UI styles need them), no dithering (gradients become flat bands)
#   outline (characters, critters, enemies) -> 1 px of the darkest colour round the silhouette, if it has none yet
#   painted art (Cards, Logo, KenneyRpgUI) -> shrunk by Factor, quantized, blown back up by whole pixels
#   bg-not-flat -> not touched here (reported: a flat colour in place of a map is a decision for the user)
# Usage: powershell -File Tools/art_style_fix.ps1 -Csv <artscan2.csv> [-DryRun]
param([Parameter(Mandatory)] [string] $Csv, [switch] $DryRun)
$repo = Resolve-Path (Join-Path $PSScriptRoot '..')
$assets = Join-Path $repo 'Assets'
$backup = Join-Path $repo '_art_backup\style_fix_20261104'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Imaging;
public static class ArtFix {
    static int[] Read(Bitmap b) {
        var r = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var px = new int[b.Width * b.Height];
        System.Runtime.InteropServices.Marshal.Copy(r.Scan0, px, 0, px.Length); b.UnlockBits(r); return px;
    }
    static Bitmap Write(int[] px, int w, int h) {
        var b = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        var r = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        System.Runtime.InteropServices.Marshal.Copy(px, 0, r.Scan0, px.Length); b.UnlockBits(r); return b;
    }
    static int A(int c) { return (c >> 24) & 255; }
    static int Luma(int c) { return (((c >> 16) & 255) * 3 + ((c >> 8) & 255) * 6 + (c & 255)) / 10; }

    public static void Alpha(int[] px) { for (int i = 0; i < px.Length; i++) px[i] = A(px[i]) >= 128 ? px[i] | unchecked((int)0xFF000000) : 0; }

    // Median cut over the opaque pixels to `colours` boxes; each pixel takes its box's mean. No dithering.
    public static void Quantize(int[] px, int colours) {
        var idx = new List<int>(); for (int i = 0; i < px.Length; i++) if (A(px[i]) > 0) idx.Add(i);
        if (idx.Count == 0) return;
        var boxes = new List<List<int>> { idx };
        while (boxes.Count < colours) {
            int best = -1, bestRange = 0, bestCh = 0;
            for (int k = 0; k < boxes.Count; k++) for (int ch = 0; ch < 3; ch++) {
                int lo = 255, hi = 0; foreach (int i in boxes[k]) { int v = (px[i] >> (16 - ch * 8)) & 255; if (v < lo) lo = v; if (v > hi) hi = v; }
                if (hi - lo > bestRange && boxes[k].Count > 1) { bestRange = hi - lo; best = k; bestCh = ch; }
            }
            if (best < 0) break;
            int sh = 16 - bestCh * 8; var box = boxes[best];
            box.Sort((a, b) => ((px[a] >> sh) & 255).CompareTo((px[b] >> sh) & 255));
            int half = box.Count / 2;
            boxes[best] = box.GetRange(0, half); boxes.Add(box.GetRange(half, box.Count - half));
        }
        var outPx = (int[])px.Clone();
        foreach (var box in boxes) {
            long r = 0, g = 0, bl = 0; foreach (int i in box) { r += (px[i] >> 16) & 255; g += (px[i] >> 8) & 255; bl += px[i] & 255; }
            int n = box.Count, c = (int)(r / n) << 16 | (int)(g / n) << 8 | (int)(bl / n);
            foreach (int i in box) outPx[i] = (px[i] & unchecked((int)0xFF000000)) | c;
        }
        Array.Copy(outPx, px, px.Length);
    }

    // 1 px of the darkest colour on every clear pixel touching the silhouette - unless most of the edge is dark already.
    public static bool Outline(int[] px, int w, int h) {
        int darkest = 0, dl = 999, edge = 0, darkEdge = 0;
        for (int i = 0; i < px.Length; i++) if (A(px[i]) == 255 && Luma(px[i]) < dl) { dl = Luma(px[i]); darkest = px[i]; }
        var ring = new List<int>();
        int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {
            int i = y * w + x; bool solid = A(px[i]) == 255;
            bool nearClear = false, nearSolid = false;
            for (int k = 0; k < 4; k++) {
                int nx = x + dx[k], ny = y + dy[k];
                bool clear = nx < 0 || ny < 0 || nx >= w || ny >= h || A(px[ny * w + nx]) == 0;
                if (clear) nearClear = true; else nearSolid = true;
            }
            if (solid && nearClear) { edge++; if (Luma(px[i]) < 70) darkEdge++; }
            if (!solid && nearSolid) ring.Add(i);
        }
        if (edge == 0 || darkEdge * 10 >= edge * 6) return false;
        foreach (int i in ring) px[i] = darkest | unchecked((int)0xFF000000);
        return true;
    }

    // Shrink by `f` (each block takes its middle pixel); Up blows it back to the original size by whole pixels.
    public static int[] Down(int[] px, int w, int h, int f, out int dw, out int dh) {
        dw = Math.Max(1, w / f); dh = Math.Max(1, h / f); var o = new int[dw * dh];
        for (int y = 0; y < dh; y++) for (int x = 0; x < dw; x++) o[y * dw + x] = px[Math.Min(h - 1, y * f + f / 2) * w + Math.Min(w - 1, x * f + f / 2)];
        return o;
    }
    public static int[] Up(int[] px, int dw, int dh, int f, int w, int h) {
        var o = new int[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) o[y * w + x] = px[Math.Min(dh - 1, y / f) * dw + Math.Min(dw - 1, x / f)];
        return o;
    }

    public static string Fix(string path, bool blur, bool tones, bool outline, int factor, bool dry) {
        int[] px; int w, h;
        using (var src = new Bitmap(path)) { w = src.Width; h = src.Height; px = Read(src); }
        string done = "";
        int dw = w, dh = h; int[] work = px;
        if (factor > 1) { work = Down(px, w, h, factor, out dw, out dh); done += "down" + factor + " "; }
        if (blur || factor > 1) { Alpha(work); done += "alpha "; }
        if (tones || factor > 1) { Quantize(work, 24); done += "24col "; }
        if (outline && Outline(work, dw, dh)) done += "outline ";
        if (factor > 1) work = Up(work, dw, dh, factor, w, h);
        if (!dry) using (var b = Write(work, w, h)) b.Save(path, ImageFormat.Png);
        return done.Trim();
    }
}
'@

$outlined = '^(Enemies|Meowolas|Meow Knight|Resources\\Ambient\\(fish|butterfly|birds))'
$painted = @{ 'Resources\Cards' = 4; 'Resources\KenneyRpgUI' = 2 }
$rows = Import-Csv $Csv | Where-Object { $_.Verdict -eq 'FAIL' -and $_.Folder -notlike '*BackgroundGif*' -and $_.Folder -notlike '*HealthHeartSystem*' }
$n = 0; $skipped = @()
foreach ($r in $rows) {
    $rel = Join-Path $r.Folder $r.File
    $path = Join-Path $assets $rel
    if (-not (Test-Path -LiteralPath $path)) { $skipped += "$rel (missing)"; continue }
    if ($r.File -like 'Speech*') { continue } # the blacksmith's bubble stays 2 tones (user OK)
    $why = $r.Why
    $blur = $why -like '*blur*'; $tones = $why -like '*tones*'
    $factor = 1; foreach ($k in $painted.Keys) { if ($r.Folder -eq $k) { $factor = $painted[$k] } }
    if ($r.File -like 'Logo*') { $factor = 4 }
    if (-not ($blur -or $tones -or $factor -gt 1)) { $skipped += "$rel ($why)"; continue }
    if (-not $DryRun) {
        $dest = Join-Path $backup $rel
        New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null
        if (-not (Test-Path -LiteralPath $dest)) { Copy-Item -LiteralPath $path $dest }
    }
    $did = [ArtFix]::Fix($path, $blur, $tones, ($r.Folder -match $outlined), $factor, [bool]$DryRun)
    "$rel : $did"
    $n++
}
"fixed $n"
if ($skipped) { "skipped:"; $skipped }
