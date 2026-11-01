# Shared by generate_slime_sprites.ps1 / generate_mushroom_sprites.ps1 (dot-sourced): cuts one cell of a
# square sprite sheet whose transparency reads back as pure black (the .webp exports), and reduces it to
# an N x N pixel-art frame.
#  - Background: only near-black connected to the cell border is cleared, so black eyes inside stay.
#  - Reduction: each output pixel takes the most common colour of its block of the cell (cell / N source
#    pixels, need not be whole), sampling the block's inner part ($Inset of a block in from each side)
#    so compression fringes don't count.
#    Colours are bucketed first, so noise doesn't split one pixel into several near-identical colours.
#  - The outer columns are cleared (they only catch specks of the neighbouring cell), and the lowest
#    drawn row is moved onto the bottom row: feet = the sprite's bottom-centre pivot.
Add-Type -AssemblyName System.Drawing
if (-not ('SheetCut' -as [type])) {
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Imaging;
public static class SheetCut {
    public static int[] Cut(Bitmap sheet, int row, int col, int cell, int n, double insetFrac) {
        int x0 = col * cell, y0 = row * cell;
        var px = new int[cell * cell];
        for (int y = 0; y < cell; y++) for (int x = 0; x < cell; x++) px[y * cell + x] = sheet.GetPixel(x0 + x, y0 + y).ToArgb();
        var bg = new bool[cell * cell];
        var q = new Queue<int>();
        for (int i = 0; i < cell; i++) { Seed(px, bg, q, cell, i, 0); Seed(px, bg, q, cell, i, cell - 1); Seed(px, bg, q, cell, 0, i); Seed(px, bg, q, cell, cell - 1, i); }
        while (q.Count > 0) {
            int p = q.Dequeue(), x = p % cell, y = p / cell;
            if (x > 0) Seed(px, bg, q, cell, x - 1, y); if (x < cell - 1) Seed(px, bg, q, cell, x + 1, y);
            if (y > 0) Seed(px, bg, q, cell, x, y - 1); if (y < cell - 1) Seed(px, bg, q, cell, x, y + 1);
        }
        double block = cell / (double)n, inset = block * insetFrac;
        var o = new int[n * n];
        for (int by = 0; by < n; by++) for (int bx = 0; bx < n; bx++) {
            var count = new Dictionary<int, int>(); var sum = new Dictionary<int, long[]>(); int clear = 0, total = 0;
            int ys = (int)Math.Ceiling(by * block + inset), ye = (int)Math.Floor((by + 1) * block - inset);
            int xs = (int)Math.Ceiling(bx * block + inset), xe = (int)Math.Floor((bx + 1) * block - inset);
            for (int y = ys; y < ye; y++) for (int x = xs; x < xe; x++) {
                int i = y * cell + x; total++;
                if (bg[i]) { clear++; continue; }
                int c = px[i], r = (c >> 16) & 255, g = (c >> 8) & 255, b = c & 255;
                int key = (r / 24) * 10000 + (g / 24) * 100 + (b / 24);
                int k; count.TryGetValue(key, out k); count[key] = k + 1;
                long[] s; if (!sum.TryGetValue(key, out s)) sum[key] = s = new long[3];
                s[0] += r; s[1] += g; s[2] += b;
            }
            if (total == 0 || clear * 2 >= total || count.Count == 0) continue;
            int best = -1, bestN = 0; foreach (var kv in count) if (kv.Value > bestN) { best = kv.Key; bestN = kv.Value; }
            var sb = sum[best];
            o[by * n + bx] = unchecked((int)0xFF000000) | ((int)(sb[0] / bestN) << 16) | ((int)(sb[1] / bestN) << 8) | (int)(sb[2] / bestN);
        }
        for (int y = 0; y < n; y++) { o[y * n] = 0; o[y * n + n - 1] = 0; }
        int low = -1;
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) if (o[y * n + x] != 0) low = y;
        int dy = n - 1 - low; var g2 = new int[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { int sy = y - dy; if (sy >= 0 && sy < n) g2[y * n + x] = o[sy * n + x]; }
        return g2;
    }
    static void Seed(int[] px, bool[] bg, Queue<int> q, int cell, int x, int y) {
        int i = y * cell + x; if (bg[i]) return;
        int c = px[i]; if (((c >> 16) & 255) + ((c >> 8) & 255) + (c & 255) > 24) return;
        bg[i] = true; q.Enqueue(i);
    }
    public static void Save(int[] o, int n, string path) {
        var bmp = new Bitmap(n, n, PixelFormat.Format32bppArgb);
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) bmp.SetPixel(x, y, Color.FromArgb(o[y * n + x]));
        bmp.Save(path, ImageFormat.Png); bmp.Dispose();
    }
}
"@
}

# Cuts sheet cell (row, col) into $OutDir/$name.png. $Cell = cell size on the sheet, $N = output size.
function Cut-Frame($sheetBmp, [int]$row, [int]$col, [int]$Cell, [int]$N, [string]$OutDir, [string]$name, [double]$Inset = 0.18) {
    [SheetCut]::Save([SheetCut]::Cut($sheetBmp, $row, $col, $Cell, $N, $Inset), $N, (Join-Path $OutDir "$name.png"))
    Write-Output "  $name.png  <- row $row col $col"
}
