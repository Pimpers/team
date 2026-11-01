# Draws the two status loops shown on monsters as hard-edged pixel art (no partial alpha, game palette),
# 24x24 cells drawn at the cats' pixel size:
#   status_stun.png   - three stars circling over a stunned monster (12 frames)
#   status_poison.png - green bubbles rising and popping on a poisoned monster (12 frames)
# Run from the project root:  powershell -ExecutionPolicy Bypass -File generate_status_fx.ps1
# Unity then rebuilds the effect library by itself (Assets/Editor/VfxAssets.cs).
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System; using System.Drawing; using System.Drawing.Imaging;
public static class StatusFx {
  const int S = 24, F = 12;
  static void Px(Bitmap b, int x, int y, Color c) { if (x >= 0 && y >= 0 && x < b.Width && y < b.Height) b.SetPixel(x, y, c); }

  // A 5x5 star (plus with a bright centre) with a dark outline, at frame offset ox.
  static void Star(Bitmap b, int ox, int cx, int cy, bool front) {
    Color fill = front ? Color.FromArgb(255, 255, 214, 90) : Color.FromArgb(255, 196, 140, 52);
    Color core = front ? Color.FromArgb(255, 255, 250, 220) : Color.FromArgb(255, 236, 190, 96);
    Color ink  = Color.FromArgb(255, 58, 34, 40);
    int[,] shape = { {0,-3},{0,-2},{0,-1},{-3,0},{-2,0},{-1,0},{0,0},{1,0},{2,0},{3,0},{0,1},{0,2},{0,3},{-1,-1},{1,-1},{-1,1},{1,1} }; // 4-point star: fat 3x3 middle, long pointed arms
    for (int i = 0; i < shape.GetLength(0); i++) {
      int x = cx + shape[i,0], y = cy + shape[i,1];
      for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) { var c = b.GetPixel(ox + Clamp(x+dx), Clamp(y+dy)); if (c.A == 0) Px(b, ox + x + dx, y + dy, ink); }
    }
    for (int i = 0; i < shape.GetLength(0); i++) Px(b, ox + cx + shape[i,0], cy + shape[i,1], fill);
    Px(b, ox + cx, cy, core);
  }
  static int Clamp(int v) { return Math.Max(0, Math.Min(S - 1, v)); }

  public static Bitmap Stun() {
    var b = new Bitmap(S * F, S, PixelFormat.Format32bppArgb);
    for (int f = 0; f < F; f++) {
      int ox = f * S;
      // faint orbit dots
      for (int a = 0; a < 360; a += 30) { double r = a * Math.PI / 180; Px(b, ox + 12 + (int)Math.Round(Math.Cos(r) * 9), 12 + (int)Math.Round(Math.Sin(r) * 3), Color.FromArgb(255, 120, 86, 70)); }
      // stars: draw the ones behind (sin < 0) first, so the front ones overlap them
      for (int pass = 0; pass < 2; pass++)
        for (int k = 0; k < 3; k++) {
          double ang = 2 * Math.PI * (f / (double)F + k / 3.0);
          bool front = Math.Sin(ang) >= 0;
          if ((pass == 0) == front) continue;
          int cx = 12 + (int)Math.Round(Math.Cos(ang) * 8), cy = 12 + (int)Math.Round(Math.Sin(ang) * 3);
          Star(b, ox, cx, cy, front);
        }
    }
    return b;
  }

  // Bubble: ring of radius r (r = 0 -> single pixel), with a highlight; popping = a small cross burst.
  static void Bubble(Bitmap b, int ox, int cx, int cy, int r, bool pop) {
    Color ring = Color.FromArgb(255, 86, 168, 60), dark = Color.FromArgb(255, 40, 96, 44), hi = Color.FromArgb(255, 214, 246, 150), fill = Color.FromArgb(255, 150, 222, 90);
    if (pop) { Px(b, ox+cx-2, cy, ring); Px(b, ox+cx+2, cy, ring); Px(b, ox+cx, cy-2, ring); Px(b, ox+cx, cy+2, ring); Px(b, ox+cx-1, cy-1, hi); Px(b, ox+cx+1, cy+1, hi); return; }
    if (r == 0) { Px(b, ox+cx, cy, fill); return; }
    for (int y = -r - 1; y <= r + 1; y++) for (int x = -r - 1; x <= r + 1; x++) {
      double d = Math.Sqrt(x*x + y*y);
      if (d <= r + 0.5 && d > r - 0.6) Px(b, ox+cx+x, cy+y, y > 0 ? dark : ring);
      else if (d <= r - 0.6) Px(b, ox+cx+x, cy+y, fill);
    }
    Px(b, ox+cx-r/2, cy-r/2, hi);
  }

  public static Bitmap Poison() {
    var b = new Bitmap(S * F, S, PixelFormat.Format32bppArgb);
    // three bubble lanes, each rising over 12 frames and popping at the top, phase-shifted so it keeps fizzing
    int[] laneX = { 7, 13, 18 }; int[] phase = { 0, 4, 8 }; int[] maxR = { 3, 3, 2 };
    for (int f = 0; f < F; f++) {
      int ox = f * S;
      for (int l = 0; l < 3; l++) {
        int t = (f + phase[l]) % F;            // 0..11 along the lane
        if (t >= 10) { if (t == 10) Bubble(b, ox, laneX[l], 4, 0, true); continue; } // pop, then a gap
        int y = 19 - t * 15 / 9;                 // rise from the goo to the top
        int r = Math.Min(maxR[l], t / 3);        // grow as it rises
        int wob = (t % 4 == 1) ? 1 : (t % 4 == 3) ? -1 : 0;
        Bubble(b, ox, laneX[l] + wob, y, r, false);
      }
    }
    return b;
  }
}
"@
$out = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')) 'Assets\Effects\Custom'
New-Item -ItemType Directory -Force $out | Out-Null
$s = [StatusFx]::Stun();   $s.Save((Join-Path $out 'status_stun.png'),   [Drawing.Imaging.ImageFormat]::Png); $s.Dispose()
$p = [StatusFx]::Poison(); $p.Save((Join-Path $out 'status_poison.png'), [Drawing.Imaging.ImageFormat]::Png); $p.Dispose()
Write-Output "  status_stun.png, status_poison.png (12 frames of 24 px each)"
