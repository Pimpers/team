# Turns a Stable Diffusion picture (made with the CatsSmash styles in the WebUI) into game-ready pixel art that keeps
# the art rules (memory art-style-spec): one pass over a file or a whole folder, results as *_px.png next to -Out.
#   1. snap to the real pixel grid: the average of each -Cell x -Cell block (SD draws one "pixel" as ~8 screen px;
#      -Cell 0, the default, measures it from the picture)
#   2. clear the backdrop: flood from the border through colours close to the border's colour (-KeyTol) -> alpha 0,
#      and the grey drop shadow SD paints under things with it (-KeepShadow to keep it)
#   3. cut the colours to -Colours by median cut (no dithering), then move each one to the nearest Tools/palette.png
#      colour unless that is farther than -MaxShift (then the cut colour stays - a new hue is better than a wrong one)
#   4. clean up: lone noisy pixels take their neighbours' colour, alpha is only 0 or 255
#   5. -Outline: a 1 px outline in the darkest colour round anything that has none (characters and objects; not tiles)
#   6. trim to the picture (+ -Pad clear pixels), blow up by -Scale whole pixels
# Usage: powershell -File Tools/sd_cleanup.ps1 -In <png or folder> [-Out <folder>] [-Cell 8] [-Colours 16]
#        [-KeyTol 40] [-NoKey] [-MaxShift 24] [-Outline] [-Pad 1] [-Scale 1] [-NoPalette]
param([Parameter(Mandatory)] [string] $In, [string] $Out = '', [int] $Cell = 0, [int] $Colours = 16,
  [int] $KeyTol = 40, [switch] $NoKey, [double] $MaxShift = 24, [switch] $Outline, [int] $Pad = 1, [int] $Scale = 1,
  [switch] $NoPalette, [switch] $KeepShadow, [int] $MaxH = 0)
$ErrorActionPreference = 'Stop'
$palettePath = Join-Path $PSScriptRoot 'palette.png'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Imaging; using System.Runtime.InteropServices;
public static class SdClean {
  static int[] Read(Bitmap b) { var d = b.LockBits(new Rectangle(0,0,b.Width,b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    var p = new int[b.Width*b.Height]; Marshal.Copy(d.Scan0, p, 0, p.Length); b.UnlockBits(d); return p; }
  static int R(int c){return (c>>16)&255;} static int G(int c){return (c>>8)&255;} static int B(int c){return c&255;} static int A(int c){return (c>>24)&255;}
  static int Dist(int a, int b){ int r=R(a)-R(b), g=G(a)-G(b), bl=B(a)-B(b); return (int)Math.Sqrt(r*r+g*g+bl*bl); }
  static int Luma(int c){ return (R(c)*3+G(c)*6+B(c))/10; }

  // palette.png: one 8 px swatch per colour, 8 a row; read the centre of every opaque swatch
  public static int[] LoadPalette(string path) {
    var list = new List<int>(); using (var b = new Bitmap(path)) {
      for (int y = 4; y < b.Height; y += 8) for (int x = 4; x < b.Width; x += 8) { var c = b.GetPixel(x, y); if (c.A == 255) list.Add(c.ToArgb()); } }
    return list.ToArray(); }

  // SD pixel-art LoRAs draw one art pixel as a block of near-equal screen pixels whose size drifts from picture to
  // picture: take the most common run length of near-equal neighbours (3..20) along rows and columns
  public static int DetectCell(int[] sp, int sw, int sh) {
    var hist = new int[21];
    for (int y = 0; y < sh; y += 7) { int run = 1; for (int x = 1; x < sw; x++) { if (Dist(sp[y*sw+x], sp[y*sw+x-1]) < 18) run++; else { if (run >= 3 && run <= 20) hist[run]++; run = 1; } } }
    for (int x = 0; x < sw; x += 7) { int run = 1; for (int y = 1; y < sh; y++) { if (Dist(sp[y*sw+x], sp[(y-1)*sw+x]) < 18) run++; else { if (run >= 3 && run <= 20) hist[run]++; run = 1; } } }
    int best = 8, bc = -1; for (int c = 3; c <= 20; c++) { int v = hist[c] + hist[Math.Min(20, c*2)] / 2; if (v > bc) { bc = v; best = c; } }
    return best; }

  public static int LastCell;

  public static Bitmap Run(string path, int cell, int colours, int keyTol, bool key, int[] pal, double maxShift, bool outline, int pad, int scale, bool shadow, int maxH) {
    int sw, sh; int[] sp; using (var src = new Bitmap(path)) { sw = src.Width; sh = src.Height; sp = Read(src); }
    if (cell <= 0) cell = DetectCell(sp, sw, sh); LastCell = cell;
    int W = sw / cell, H = sh / cell; var px = new int[W*H];
    // 1. block average (alpha too, so a clear source stays clear)
    for (int y=0;y<H;y++) for (int x=0;x<W;x++) { long r=0,g=0,b=0,a=0; int n=0;
      for (int j=0;j<cell;j++) for (int i=0;i<cell;i++) { int c=sp[(y*cell+j)*sw + x*cell+i]; r+=R(c); g+=G(c); b+=B(c); a+=A(c); n++; }
      px[y*W+x] = (int)((a/n)<<24) | (int)(r/n)<<16 | (int)(g/n)<<8 | (int)(b/n); }
    // 2. backdrop -> clear: flood from the border through colours near the border's most common colour
    //    (not the corners: SD often runs the subject into a corner)
    var clear = new bool[W*H];
    for (int i=0;i<px.Length;i++) if (A(px[i]) < 128) clear[i] = true;
    if (key) {
      var border = new List<int>(); for (int x=0;x<W;x++){border.Add(px[x]);border.Add(px[(H-1)*W+x]);} for (int y=1;y<H-1;y++){border.Add(px[y*W]);border.Add(px[y*W+W-1]);}
      int bg = border[0], best = -1;
      for (int k=0;k<border.Count;k+=3) { int c=border[k], votes=0; foreach (var d in border) if (Dist(c,d) < keyTol) votes++; if (votes > best) { best = votes; bg = c; } }
      var st = new Stack<int>(); for (int x=0;x<W;x++){st.Push(x);st.Push((H-1)*W+x);} for (int y=0;y<H;y++){st.Push(y*W);st.Push(y*W+W-1);}
      var seen = new bool[W*H];
      while (st.Count>0) { int i=st.Pop(); if (seen[i]) continue; seen[i]=true; if (!clear[i] && Dist(px[i],bg) > keyTol) continue; clear[i]=true;
        int x=i%W, y=i/W; if(x>0)st.Push(i-1); if(x<W-1)st.Push(i+1); if(y>0)st.Push(i-W); if(y<H-1)st.Push(i+W); }
      // drop shadows SD paints despite the negative prompt: the backdrop's hue but darker and grey, touching the clear area
      if (shadow) { int bgL = Luma(bg); var q = new Stack<int>(); for (int i=0;i<px.Length;i++) if (clear[i]) q.Push(i);
        while (q.Count>0) { int i=q.Pop(); int x=i%W, y=i/W;
          foreach (int n in new[]{ x>0?i-1:-1, x<W-1?i+1:-1, y>0?i-W:-1, y<H-1?i+W:-1 }) { if (n<0 || clear[n]) continue; int c=px[n];
            int mx=Math.Max(R(c),Math.Max(G(c),B(c))), mn=Math.Min(R(c),Math.Min(G(c),B(c)));
            bool grey = mx - mn < 28 && Math.Abs((R(c)-G(c)) - (R(bg)-G(bg))) < 20 && Math.Abs((G(c)-B(c)) - (G(bg)-B(bg))) < 20;
            if (grey && Luma(c) < bgL && Luma(c) > bgL - 90) { clear[n]=true; q.Push(n); } } } } }
    // 3. median cut over the opaque pixels, then to the nearest palette colour
    var boxes = new List<List<int>>{ new List<int>() }; for (int i=0;i<px.Length;i++) if (!clear[i]) boxes[0].Add(i);
    if (boxes[0].Count == 0) throw new Exception("nothing left after clearing the backdrop - try -NoKey or a smaller -KeyTol");
    while (boxes.Count < colours) { int bi=-1, br=0, bch=0;
      for (int b=0;b<boxes.Count;b++) for (int cn=0;cn<3;cn++) { int lo=255,hi=0; foreach (int i in boxes[b]) { int v=(px[i]>>(16-8*cn))&255; if(v<lo)lo=v; if(v>hi)hi=v; } if (hi-lo>br){br=hi-lo;bi=b;bch=cn;} }
      if (bi<0 || boxes[bi].Count < 2) break; int sh2=16-8*bch; var bx=boxes[bi]; bx.Sort((p,q)=>((px[p]>>sh2)&255).CompareTo((px[q]>>sh2)&255));
      int m=bx.Count/2; boxes[bi]=bx.GetRange(0,m); boxes.Add(bx.GetRange(m,bx.Count-m)); }
    foreach (var bx in boxes) { if (bx.Count==0) continue; long r=0,g=0,b=0; foreach(int i in bx){r+=R(px[i]);g+=G(px[i]);b+=B(px[i]);}
      int c = unchecked((int)0xFF000000) | (int)(r/bx.Count)<<16 | (int)(g/bx.Count)<<8 | (int)(b/bx.Count);
      if (pal != null && pal.Length > 0) { int bestC=c, bd=int.MaxValue; foreach (var p in pal) { int d=Dist(c,p); if (d<bd){bd=d;bestC=p;} } if (bd <= maxShift) c = bestC | unchecked((int)0xFF000000); }
      foreach(int i in bx) px[i]=c; }
    // 4. lone pixels take the colour all four neighbours agree on
    for (int pass=0; pass<2; pass++) { var cp=(int[])px.Clone(); var cc=(bool[])clear.Clone();
      for (int y=1;y<H-1;y++) for (int x=1;x<W-1;x++) { int i=y*W+x; int l=i-1, r=i+1, u=i-W, d=i+W;
        if (cc[l]&&cc[r]&&cc[u]&&cc[d]) { clear[i]=true; continue; }
        if (cc[i] || cc[l]||cc[r]||cc[u]||cc[d]) continue;
        if (cp[i]!=cp[l] && cp[l]==cp[r] && cp[l]==cp[u] && cp[l]==cp[d]) px[i]=cp[l]; } }
    // 5. outline in the darkest colour where an opaque pixel touches the clear backdrop and is not already dark
    if (outline) { int dark = 0, dl = 999; for (int i=0;i<px.Length;i++) if (!clear[i] && Luma(px[i])<dl){dl=Luma(px[i]);dark=px[i];}
      var add = new List<int>();
      for (int y=0;y<H;y++) for (int x=0;x<W;x++) { int i=y*W+x; if (!clear[i]) continue;
        bool touch = (x>0&&!clear[i-1])||(x<W-1&&!clear[i+1])||(y>0&&!clear[i-W])||(y<H-1&&!clear[i+W]); if (touch) add.Add(i); }
      bool already = true; foreach (int i in add) { int x=i%W, y=i/W; foreach (int n in new[]{ x>0?i-1:-1, x<W-1?i+1:-1, y>0?i-W:-1, y<H-1?i+W:-1 })
        if (n>=0 && !clear[n] && Luma(px[n]) > dl + 40) { already = false; break; } if (!already) break; }
      if (!already) foreach (int i in add) { clear[i]=false; px[i]=dark; } }
    // 6. trim to the picture plus pad, then scale by whole pixels
    int x0=W, y0=H, x1=-1, y1=-1; for (int y=0;y<H;y++) for (int x=0;x<W;x++) if (!clear[y*W+x]) { if(x<x0)x0=x; if(x>x1)x1=x; if(y<y0)y0=y; if(y>y1)y1=y; }
    x0=Math.Max(0,x0-pad); y0=Math.Max(0,y0-pad); x1=Math.Min(W-1,x1+pad); y1=Math.Min(H-1,y1+pad);
    // -MaxH: shrink (nearest neighbour, whole art pixels) so the picture is at most maxH art pixels tall - the game's
    // sizes are counted in art pixels (a cat is ~20), and a 60-pixel-tall chest would dwarf it
    int cw = x1-x0+1, ch = y1-y0+1; double fit = (maxH > 0 && ch > maxH) ? (double)maxH / ch : 1.0;
    int tw = Math.Max(1,(int)Math.Round(cw*fit)), th = Math.Max(1,(int)Math.Round(ch*fit));
    int ow=tw*scale, oh=th*scale; var o=new Bitmap(ow,oh,PixelFormat.Format32bppArgb);
    var od=o.LockBits(new Rectangle(0,0,ow,oh), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb); var op=new int[ow*oh];
    for (int y=0;y<oh;y++) for (int x=0;x<ow;x++) { int sx = x0 + Math.Min(cw-1,(int)((x/scale + 0.5) * cw / tw)), sy = y0 + Math.Min(ch-1,(int)((y/scale + 0.5) * ch / th));
      int i=sy*W + sx; op[y*ow+x] = clear[i] ? 0 : (px[i] | unchecked((int)0xFF000000)); }
    Marshal.Copy(op,0,od.Scan0,op.Length); o.UnlockBits(od); return o; }
}
'@
$pal = $null
if (-not $NoPalette) { if (Test-Path $palettePath) { $pal = [SdClean]::LoadPalette($palettePath) } else { Write-Warning "no Tools/palette.png - colours stay as cut" } }
$files = if (Test-Path $In -PathType Container) { Get-ChildItem $In -Filter *.png | Where-Object { $_.Name -notlike '*_px.png' } } else { @(Get-Item $In) }
if (-not $Out) { $Out = if (Test-Path $In -PathType Container) { $In } else { Split-Path $In } }
New-Item -ItemType Directory -Force $Out | Out-Null
foreach ($f in $files) {
  $b = [SdClean]::Run($f.FullName, $Cell, $Colours, $KeyTol, -not $NoKey, $pal, $MaxShift, $Outline.IsPresent, $Pad, $Scale, -not $KeepShadow, $MaxH)
  $dst = Join-Path $Out ($f.BaseName + '_px.png'); $b.Save($dst); "{0} -> {1} ({2}x{3}, cell {4})" -f $f.Name, (Split-Path $dst -Leaf), $b.Width, $b.Height, [SdClean]::LastCell; $b.Dispose()
}
