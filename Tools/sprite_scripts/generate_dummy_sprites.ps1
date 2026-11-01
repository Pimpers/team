# Draws the tutorial's training dummy (dummy_*) - a straw scarecrow on a post - as 32x40
# pixel-art frames into Assets/Enemies/Dummy/. Black outline like the other enemies, feet
# (the post) on the bottom row. It never moves or attacks, so move/attack reuse the idle pose.
# Run:  powershell -ExecutionPolicy Bypass -File generate_dummy_sprites.ps1
param([string]$OutDir = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')) "Assets/Enemies/Dummy"))

$code = @"
using System;
using System.Drawing;
using System.IO;

public class DummyArt
{
    const int W = 32, H = 40;
    Color[,] p = new Color[W, H];

    static Color C(string hex) { return ColorTranslator.FromHtml(hex); }
    static readonly Color Outline = C("#1f1a24"), Wood = C("#8a5a32"), WoodDark = C("#5e3a1e"),
        Straw = C("#e8c15a"), StrawDark = C("#b98f2e"), Sack = C("#d9c3a0"), SackDark = C("#a88f6a"),
        Stitch = C("#5a3a2a"), Rope = C("#c0392b"), Target = C("#e0193a"), TargetIn = C("#fff1d2");

    bool Empty(int x, int y) { return x < 0 || y < 0 || x >= W || y >= H || p[x, y].A == 0; }
    void Px(int x, int y, Color c) { if (x >= 0 && y >= 0 && x < W && y < H) p[x, y] = c; }
    void Rect(int x0, int y0, int x1, int y1, Color c) { for (int x = x0; x <= x1; x++) for (int y = y0; y <= y1; y++) Px(x, y, c); }

    void Outlined()
    {
        var o = new bool[W, H];
        for (int x = 0; x < W; x++) for (int y = 0; y < H; y++)
            if (Empty(x, y) && (!Empty(x - 1, y) || !Empty(x + 1, y) || !Empty(x, y - 1) || !Empty(x, y + 1))) o[x, y] = true;
        for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) if (o[x, y]) p[x, y] = Outline;
    }

    // lean: body offset in px (sway / hit), down: body lowered.
    public static DummyArt Standing(int lean, int down, bool hit)
    {
        var g = new DummyArt();
        // post into the ground
        g.Rect(15, 26 + down, 16, 38, Wood); g.Rect(16, 26 + down, 16, 38, WoodDark);
        int lx = lean, dy = down;
        // crossbar arms with straw tufts
        g.Rect(4 + lx, 15 + dy, 27 + lx, 16 + dy, Wood); g.Rect(4 + lx, 16 + dy, 27 + lx, 16 + dy, WoodDark);
        g.Rect(2 + lx, 14 + dy, 4 + lx, 18 + dy, Straw); g.Px(2 + lx, 18 + dy, StrawDark);
        g.Rect(27 + lx, 14 + dy, 29 + lx, 18 + dy, Straw); g.Px(29 + lx, 18 + dy, StrawDark);
        // straw body (sack shirt) with a target painted on
        g.Rect(10 + lx, 13 + dy, 21 + lx, 27 + dy, Straw);
        for (int y = 13; y <= 27; y++) g.Px(21 + lx, y + dy, StrawDark);
        for (int x = 10; x <= 21; x++) g.Px(x + lx, 27 + dy, StrawDark);
        g.Rect(10 + lx, 20 + dy, 21 + lx, 20 + dy, Rope); // belt rope
        g.Rect(13 + lx, 15 + dy, 18 + lx, 19 + dy, Target); g.Rect(14 + lx, 16 + dy, 17 + lx, 18 + dy, TargetIn); g.Rect(15 + lx, 17 + dy, 16 + lx, 17 + dy, Target);
        // sack head with stitched face
        g.Rect(11 + lx, 3 + dy, 20 + lx, 12 + dy, Sack);
        for (int y = 3; y <= 12; y++) g.Px(20 + lx, y + dy, SackDark);
        g.Rect(12 + lx, 1 + dy, 19 + lx, 2 + dy, Straw); // straw hair
        g.Px(14 + lx, 7 + dy, Stitch); g.Px(17 + lx, 7 + dy, Stitch);   // X eyes when hit, dots otherwise
        if (hit) { g.Px(13 + lx, 6 + dy, Stitch); g.Px(15 + lx, 8 + dy, Stitch); g.Px(18 + lx, 6 + dy, Stitch); g.Px(16 + lx, 8 + dy, Stitch); }
        for (int x = 14; x <= 17; x++) g.Px(x + lx, 10 + dy, (x % 2 == 0) ? Stitch : SackDark); // stitched mouth
        g.Outlined();
        return g;
    }

    // Knocked off the post: the straw body lying on the ground.
    public static DummyArt Fallen(int stage)
    {
        var g = new DummyArt();
        g.Rect(15, 30, 16, 38, Wood); g.Rect(16, 30, 16, 38, WoodDark); // broken post stump
        if (stage == 0)
        {
            g.Rect(4, 24, 27, 25, Wood);
            g.Rect(9, 22, 22, 33, Straw);
            g.Rect(12, 25, 18, 29, Target); g.Rect(13, 26, 17, 28, TargetIn);
        }
        else
        {
            g.Rect(2, 36, 13, 38, Straw); g.Rect(18, 35, 30, 38, Straw);
            g.Rect(20, 31, 27, 35, Sack); g.Px(22, 33, Stitch); g.Px(25, 33, Stitch);
            g.Rect(5, 34, 9, 35, StrawDark);
        }
        g.Outlined();
        return g;
    }

    public void Save(string path)
    {
        using (var bmp = new Bitmap(W, H, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
        {
            for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) bmp.SetPixel(x, y, p[x, y]);
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
    }

    public static void All(string dir)
    {
        Directory.CreateDirectory(dir);
        Standing(0, 0, false).Save(Path.Combine(dir, "dummy_idle_0.png"));
        Standing(1, 0, false).Save(Path.Combine(dir, "dummy_idle_1.png"));
        Standing(0, 0, false).Save(Path.Combine(dir, "dummy_move_0.png"));
        Standing(0, 0, false).Save(Path.Combine(dir, "dummy_attack_0.png"));
        Standing(2, 1, true).Save(Path.Combine(dir, "dummy_hurt_0.png"));
        Fallen(0).Save(Path.Combine(dir, "dummy_death_0.png"));
        Fallen(1).Save(Path.Combine(dir, "dummy_death_1.png"));
    }
}
"@

Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing
[DummyArt]::All($OutDir)
Write-Host "Dummy frames written to $OutDir"
