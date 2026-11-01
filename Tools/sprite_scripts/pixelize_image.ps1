# Local pixel-art conversion (no API): shrinks an illustration by -Factor (each output pixel = the most
# common colour of its block, so edges stay crisp instead of blurring), then reduces the whole image to
# -Colors colours with k-means. A rough, free stand-in for PixelLab's image-to-pixelart.
# Example:
#   powershell -ExecutionPolicy Bypass -File pixelize_image.ps1 -In Assets/Backgrounds/WorldMap.png -Out _art_source/pixellab_examples/WorldMap_local.png -Factor 4 -Colors 48
param([Parameter(Mandatory)][string]$In, [Parameter(Mandatory)][string]$Out, [int]$Factor = 4, [int]$Colors = 48)
Add-Type -AssemblyName System.Drawing
if (-not ('Pixelize' -as [type])) {
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Imaging;
public static class Pixelize {
    public static Bitmap Run(Bitmap src, int f, int k) {
        int w = src.Width / f, h = src.Height / f;
        var small = new int[w * h];
        for (int by = 0; by < h; by++) for (int bx = 0; bx < w; bx++) {
            var count = new Dictionary<int, int>(); var sum = new Dictionary<int, long[]>();
            for (int y = by * f; y < by * f + f; y++) for (int x = bx * f; x < bx * f + f; x++) {
                Color c = src.GetPixel(x, y);
                int key = (c.R / 20) * 10000 + (c.G / 20) * 100 + c.B / 20;
                int n; count.TryGetValue(key, out n); count[key] = n + 1;
                long[] s; if (!sum.TryGetValue(key, out s)) sum[key] = s = new long[3];
                s[0] += c.R; s[1] += c.G; s[2] += c.B;
            }
            int best = 0, bestN = -1; foreach (var kv in count) if (kv.Value > bestN) { best = kv.Key; bestN = kv.Value; }
            var t = sum[best];
            small[by * w + bx] = ((int)(t[0] / bestN) << 16) | ((int)(t[1] / bestN) << 8) | (int)(t[2] / bestN);
        }
        // k-means palette (seeded from evenly spaced pixels, 12 rounds)
        var cen = new double[k, 3];
        for (int i = 0; i < k; i++) { int p = small[(int)((long)i * small.Length / k)]; cen[i, 0] = (p >> 16) & 255; cen[i, 1] = (p >> 8) & 255; cen[i, 2] = p & 255; }
        var lab = new int[small.Length];
        for (int it = 0; it < 12; it++) {
            var acc = new double[k, 4];
            for (int i = 0; i < small.Length; i++) {
                int p = small[i]; double r = (p >> 16) & 255, g = (p >> 8) & 255, b = p & 255;
                int bi = 0; double bd = double.MaxValue;
                for (int c = 0; c < k; c++) { double dr = r - cen[c, 0], dg = g - cen[c, 1], db = b - cen[c, 2]; double d = dr * dr * 0.3 + dg * dg * 0.59 + db * db * 0.11; if (d < bd) { bd = d; bi = c; } }
                lab[i] = bi; acc[bi, 0] += r; acc[bi, 1] += g; acc[bi, 2] += b; acc[bi, 3]++;
            }
            for (int c = 0; c < k; c++) if (acc[c, 3] > 0) { cen[c, 0] = acc[c, 0] / acc[c, 3]; cen[c, 1] = acc[c, 1] / acc[c, 3]; cen[c, 2] = acc[c, 2] / acc[c, 3]; }
        }
        var o = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        for (int i = 0; i < small.Length; i++) { int c = lab[i]; o.SetPixel(i % w, i / w, Color.FromArgb(255, (int)cen[c, 0], (int)cen[c, 1], (int)cen[c, 2])); }
        return o;
    }
}
"@
}
$src = [Drawing.Bitmap]::FromFile((Resolve-Path $In))
$o = [Pixelize]::Run($src, $Factor, $Colors)
$o.Save($Out, [Drawing.Imaging.ImageFormat]::Png)
Write-Output "  $Out  $($o.Width)x$($o.Height), $Colors colours"
$o.Dispose(); $src.Dispose()
