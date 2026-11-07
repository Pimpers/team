# 1 px dark outline (the sheet's darkest colour) round the big pieces of an effect sheet: clear pixels next to a connected
# piece of at least -Min pixels, within the same -Cell wide frame; small sparks and hearts are left as they are
# (outlined they turn to blots). Used for the churu tube (lure) and the catnip cloud. Back up first; no -Restore.
#   powershell -File Tools/fx_outline.ps1 -Png Assets/Effects/Archer/a_catnip_fx.png -Cell 24 [-Min 30]
param([Parameter(Mandatory)] $Png, [Parameter(Mandatory)] [int]$Cell, [int]$Min = 30)
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System; using System.Collections.Generic; using System.Drawing;
public static class OL { public static int Run(string p, int min, int cell) {
  Bitmap s = new Bitmap(p); Bitmap b = new Bitmap(s); s.Dispose(); int w = b.Width, h = b.Height;
  var lab = new int[w*h]; int dark = 0, dl = 999, added = 0; var big = new List<bool>{false};
  for (int y=0;y<h;y++) for (int x=0;x<w;x++){ var c=b.GetPixel(x,y); if(c.A>0){ int l=(c.R*3+c.G*6+c.B)/10; if(l<dl){dl=l;dark=c.ToArgb();} } }
  int id=0; for (int i=0;i<w*h;i++){ if(lab[i]!=0||b.GetPixel(i%w,i/w).A==0) continue; id++; int n=0; var q=new Stack<int>(); q.Push(i); lab[i]=id;
    while(q.Count>0){ int k=q.Pop(); n++; int kx=k%w, ky=k/w; for(int d=0;d<4;d++){ int nx=kx+(d==0?1:d==1?-1:0), ny=ky+(d==2?1:d==3?-1:0);
      if(nx<0||ny<0||nx>=w||ny>=h) continue; int j=ny*w+nx; if(lab[j]==0&&b.GetPixel(nx,ny).A>0){lab[j]=id;q.Push(j);} } } big.Add(n>=min); }
  var ring = new List<int>();
  for (int y=0;y<h;y++) for (int x=0;x<w;x++){ if(b.GetPixel(x,y).A>0) continue; for(int d=0;d<4;d++){ int nx=x+(d==0?1:d==1?-1:0), ny=y+(d==2?1:d==3?-1:0);
      if(nx<0||ny<0||nx>=w||ny>=h|| (nx/cell)!=(x/cell)) continue; int j=ny*w+nx; if(lab[j]>0&&big[lab[j]]){ring.Add(y*w+x);break;} } }
  foreach(int i in ring){ b.SetPixel(i%w,i/w,Color.FromArgb(dark)); added++; }
  b.Save(p, System.Drawing.Imaging.ImageFormat.Png); b.Dispose(); return added; } }
'@
"outline +$([OL]::Run((Resolve-Path $Png).Path, $Min, $Cell)) px"
