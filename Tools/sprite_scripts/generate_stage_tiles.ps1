# Generates the 16x16 pixel-art tile sprites used by the Stage1-3 tilemaps
# into Assets/Tiles/Sprites/. Re-run any time to regenerate (deterministic seeds).
# Unity import settings (PPU 16, Point filter, no compression) are applied by
# Assets/Editor/StageSceneBuilder.cs, not by hand-written .meta files.
$ErrorActionPreference = "Stop"
$outDir = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')) "Assets\Tiles\Sprites"
New-Item -ItemType Directory -Force $outDir | Out-Null

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

public static class TileGen
{
    const int S = 16;
    static string Dir;

    static Color C(string hex) { return ColorTranslator.FromHtml(hex); }

    static Bitmap New()
    {
        var b = new Bitmap(S, S, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(b)) g.Clear(Color.Transparent);
        return b;
    }

    static void Px(Bitmap b, int x, int y, Color c)
    {
        if (x < 0 || y < 0 || x >= S || y >= S) return;
        b.SetPixel(x, y, c);
    }
    static void Px(Bitmap b, int x, int y, string hex) { Px(b, x, y, C(hex)); }

    static void Fill(Bitmap b, string hex)
    {
        var c = C(hex);
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) b.SetPixel(x, y, c);
    }

    static void Noise(Bitmap b, Random r, double p, params string[] hexes)
    {
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
                if (r.NextDouble() < p) b.SetPixel(x, y, C(hexes[r.Next(hexes.Length)]));
    }

    static bool InDisc(int x, int y, double cx, double cy, double rx, double ry)
    {
        double dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry;
        return dx * dx + dy * dy <= 1.0;
    }

    static void Ellipse(Bitmap b, double cx, double cy, double rx, double ry, Color c)
    {
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
                if (InDisc(x, y, cx, cy, rx, ry)) b.SetPixel(x, y, c);
    }
    static void Ellipse(Bitmap b, double cx, double cy, double rx, double ry, string hex) { Ellipse(b, cx, cy, rx, ry, C(hex)); }

    // Darkens every opaque pixel that touches a transparent one - gives obstacle
    // sprites a readable pixel-art outline against the floor.
    static void Outline(Bitmap b, string hex)
    {
        var c = C(hex);
        var src = (Bitmap)b.Clone();
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                if (src.GetPixel(x, y).A < 200) continue;
                bool edge = false;
                for (int k = 0; k < 4 && !edge; k++)
                {
                    int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0);
                    int ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= S || ny >= S || src.GetPixel(nx, ny).A < 200) edge = true;
                }
                if (edge) b.SetPixel(x, y, c);
            }
        src.Dispose();
    }

    static void Shadow(Bitmap b, double cx, double cy, double rx, double ry)
    {
        Ellipse(b, cx, cy, rx, ry, Color.FromArgb(80, 0, 0, 0));
    }

    static void Save(Bitmap b, string name)
    {
        b.Save(Path.Combine(Dir, name + ".png"), ImageFormat.Png);
        b.Dispose();
    }

    // ---------- Stage 1: Forest ----------
    static Bitmap Grass(int seed)
    {
        var r = new Random(seed);
        var b = New();
        Fill(b, "#5b9a3c");
        Noise(b, r, 0.28, "#528d36", "#64a644");
        for (int i = 0; i < 5; i++)
        {
            int x = r.Next(S), y = r.Next(2, S);
            Px(b, x, y, "#467d2e"); Px(b, x, y - 1, "#467d2e");
            if (r.NextDouble() < 0.5) Px(b, x, y - 2, "#74b850");
        }
        return b;
    }

    static void FlowerAt(Bitmap b, int x, int y, string petal)
    {
        Px(b, x - 1, y, petal); Px(b, x + 1, y, petal); Px(b, x, y - 1, petal); Px(b, x, y + 1, petal);
        Px(b, x, y, "#f4d35e");
    }

    static void Forest()
    {
        Save(Grass(11), "forest_grass1");
        Save(Grass(23), "forest_grass2");

        var b = Grass(37);
        FlowerAt(b, 3, 4, "#f7f3e8"); FlowerAt(b, 11, 3, "#f29bb5");
        FlowerAt(b, 7, 10, "#f7f3e8"); FlowerAt(b, 13, 12, "#b7a4f2");
        Save(b, "forest_flowers");

        var r = new Random(41);
        b = New();
        Fill(b, "#a47a4f");
        Noise(b, r, 0.3, "#93693f", "#b58a5b");
        for (int i = 0; i < 3; i++)
        {
            int x = r.Next(1, 15), y = r.Next(1, 14);
            Px(b, x, y, "#c9a57a"); Px(b, x + 1, y, "#c9a57a"); Px(b, x, y + 1, "#7e5a36"); Px(b, x + 1, y + 1, "#7e5a36");
        }
        Save(b, "forest_path");

        // Tall grass tufts (walkable decoration)
        r = new Random(53);
        b = New();
        for (int t = 0; t < 4; t++)
        {
            int bx = r.Next(2, 13), by = r.Next(6, 15);
            for (int k = -1; k <= 1; k++)
            {
                int h = 3 + r.Next(3);
                for (int j = 0; j < h; j++) Px(b, bx + k * 1 + (j > 2 ? k : 0), by - j, j == h - 1 ? "#8cc95c" : "#3f7a2a");
            }
        }
        Save(b, "forest_tallgrass");

        // Tree (top-down canopy)
        b = New();
        Shadow(b, 8.5, 10.5, 7, 5);
        for (int y = 12; y < 16; y++) { Px(b, 7, y, "#5c3d22"); Px(b, 8, y, "#6b4a2b"); }
        Ellipse(b, 8, 7, 7, 6.5, "#2f6b2a");
        Ellipse(b, 7.2, 6.2, 5.2, 4.8, "#3d8233");
        Ellipse(b, 5.8, 4.8, 2.4, 2.0, "#58a043");
        Px(b, 5, 4, "#7cc05a");
        Outline(b, "#1b421a");
        Save(b, "forest_tree");

        // Bush with crimson (corrupted) berries
        b = New();
        Shadow(b, 8.5, 12, 6, 3);
        Ellipse(b, 8, 9.5, 6, 5, "#347a2d");
        Ellipse(b, 7, 8.5, 4, 3.2, "#44913a");
        Px(b, 5, 7, "#6db24a"); Px(b, 6, 7, "#6db24a");
        Outline(b, "#1b421a");
        Px(b, 10, 9, "#c0263e"); Px(b, 5, 11, "#c0263e"); Px(b, 9, 12, "#e04860");
        Save(b, "forest_bush");

        // Rock
        b = New();
        Shadow(b, 8.5, 12.5, 6, 2.5);
        Ellipse(b, 8, 10, 6, 4.5, "#7f7f88");
        Ellipse(b, 7, 8.8, 4, 2.8, "#9a9aa2");
        Ellipse(b, 6, 8, 1.6, 1.0, "#bdbdc4");
        Outline(b, "#3f3f47");
        Save(b, "forest_rock");

        // Water
        r = new Random(67);
        b = New();
        Fill(b, "#3f79c9");
        Noise(b, r, 0.2, "#3a70bb", "#4683d3");
        for (int i = 0; i < 4; i++)
        {
            int x = r.Next(0, 13), y = r.Next(1, 15);
            Px(b, x, y, "#6fa3e3"); Px(b, x + 1, y, "#6fa3e3"); Px(b, x + 2, y, "#6fa3e3");
        }
        Px(b, r.Next(S), r.Next(S), "#d6e8ff");
        Save(b, "forest_water");
    }

    // ---------- Stage 2: Ruins ----------
    static Bitmap Stone(int seed)
    {
        var r = new Random(seed);
        var b = New();
        Fill(b, "#6f6a7a");
        Noise(b, r, 0.25, "#686373", "#787384");
        for (int x = 0; x < S; x++) { Px(b, x, 0, "#4f4b5a"); Px(b, x, 8, "#4f4b5a"); Px(b, x, 1, "#827d8e"); Px(b, x, 9, "#827d8e"); }
        for (int y = 0; y < 8; y++) { Px(b, 4, y, "#4f4b5a"); Px(b, 12, y, "#4f4b5a"); }
        for (int y = 8; y < 16; y++) { Px(b, 0, y, "#4f4b5a"); Px(b, 8, y, "#4f4b5a"); }
        return b;
    }

    static void Crack(Bitmap b, Random r, int x, int y, int len, string hex)
    {
        for (int i = 0; i < len; i++)
        {
            Px(b, x, y, hex);
            x += r.Next(-1, 2); y += 1;
            if (r.NextDouble() < 0.5) x += 1;
        }
    }

    static void Ruins()
    {
        Save(Stone(101), "ruins_floor1");

        var b = Stone(113);
        Px(b, 10, 12, "#57536a"); Px(b, 11, 12, "#57536a"); Px(b, 10, 13, "#57536a");
        Save(b, "ruins_floor2");

        b = Stone(127);
        Crack(b, new Random(3), 3, 1, 13, "#2f2c38");
        Save(b, "ruins_cracked");

        // Crimson corruption moss
        var r = new Random(131);
        b = Stone(137);
        for (int i = 0; i < 4; i++)
        {
            double cx = r.Next(3, 13), cy = r.Next(3, 13);
            Ellipse(b, cx, cy, 2.2, 1.6, "#7a2233");
            Px(b, (int)cx - 1, (int)cy - 1, "#a8324a");
        }
        Px(b, 6, 6, "#e0506a"); Px(b, 11, 10, "#e0506a");
        Save(b, "ruins_moss");

        // Wall (opaque brick block)
        r = new Random(149);
        b = New();
        Fill(b, "#3b3747");
        Noise(b, r, 0.2, "#363241", "#413d4e");
        for (int row = 0; row < 4; row++)
        {
            int y0 = row * 4;
            for (int x = 0; x < S; x++) { Px(b, x, y0, "#2a2733"); Px(b, x, y0 + 1, "#4a4558"); }
            int off = (row % 2 == 0) ? 0 : 4;
            for (int y = y0; y < y0 + 4; y++) { Px(b, off, y, "#2a2733"); Px(b, off + 8, y, "#2a2733"); }
        }
        Save(b, "ruins_wall");

        // Pillar (top-down broken column)
        b = New();
        Shadow(b, 9, 10, 7, 5.5);
        Ellipse(b, 8, 8, 6.5, 6.5, "#8c8799");
        Ellipse(b, 8, 8, 4.5, 4.5, "#a19cae");
        Ellipse(b, 6.5, 6.5, 1.8, 1.5, "#c3bfcf");
        Outline(b, "#3e3a4a");
        Crack(b, new Random(7), 9, 4, 5, "#5a5568");
        Save(b, "ruins_pillar");

        // Rubble pile
        r = new Random(157);
        b = New();
        Shadow(b, 8.5, 11.5, 7, 3.5);
        int[,] stones = { { 5, 10 }, { 10, 11 }, { 8, 7 }, { 12, 7 } };
        for (int i = 0; i < 4; i++)
        {
            double rx = 2.4 + r.NextDouble(), ry = 1.8 + r.NextDouble() * 0.8;
            Ellipse(b, stones[i, 0], stones[i, 1], rx, ry, "#7d788a");
            Px(b, stones[i, 0] - 1, stones[i, 1] - 1, "#a39fb0");
        }
        Outline(b, "#35313f");
        Save(b, "ruins_rubble");
    }

    // ---------- Stage 3: Crimson boss arena ----------
    static Bitmap Dark(int seed)
    {
        var r = new Random(seed);
        var b = New();
        Fill(b, "#30263a");
        Noise(b, r, 0.25, "#2b2234", "#372c42");
        for (int i = 0; i < S; i++) { Px(b, i, 0, "#221a2b"); Px(b, i, 8, "#221a2b"); Px(b, 0, i, "#221a2b"); Px(b, 8, i, "#221a2b"); }
        if (r.NextDouble() < 0.7) Px(b, r.Next(1, 15), r.Next(1, 15), "#5a1a2a");
        return b;
    }

    static void Boss()
    {
        Save(Dark(201), "boss_floor1");
        Save(Dark(211), "boss_floor2");

        // Glowing rune tile (arranged in a ring around the boss)
        var b = Dark(223);
        for (int i = -5; i <= 5; i++)
        {
            int a = 5 - Math.Abs(i);
            Px(b, 8 + i, 8 - a, "#d23a52"); Px(b, 8 + i, 8 + a, "#d23a52");
        }
        Px(b, 8, 8, "#ff8a9a"); Px(b, 7, 8, "#ff6b7f"); Px(b, 9, 8, "#ff6b7f"); Px(b, 8, 7, "#ff6b7f"); Px(b, 8, 9, "#ff6b7f");
        for (int i = 0; i < 4; i++) { Px(b, 8, 1 + i, "#7a2034"); Px(b, 8, 12 + i, "#7a2034"); }
        Save(b, "boss_rune");

        // Corrupted crimson pool
        var r = new Random(229);
        b = New();
        Fill(b, "#7d1227");
        Noise(b, r, 0.25, "#6a0f21", "#93182f");
        for (int i = 0; i < 3; i++)
        {
            int x = r.Next(2, 14), y = r.Next(2, 14);
            Px(b, x, y, "#e0455c"); Px(b, x + 1, y, "#c2324a"); Px(b, x, y - 1, "#ff8a9a");
        }
        Save(b, "boss_pool");

        // Obsidian wall with crimson veins
        r = new Random(233);
        b = New();
        Fill(b, "#1c1522");
        Noise(b, r, 0.2, "#18121d", "#241b2b");
        for (int row = 0; row < 2; row++)
        {
            int y0 = row * 8;
            for (int x = 0; x < S; x++) { Px(b, x, y0, "#120d17"); Px(b, x, y0 + 1, "#2a2131"); }
            int off = row == 0 ? 3 : 11;
            for (int y = y0; y < y0 + 8; y++) Px(b, off, y, "#120d17");
        }
        Crack(b, new Random(5), 5, 2, 11, "#8b1e33");
        Px(b, 6, 6, "#c2324a");
        Save(b, "boss_wall");

        // Brazier with crimson fire
        b = New();
        Shadow(b, 8.5, 14, 5.5, 2);
        Px(b, 5, 14, "#2d2833"); Px(b, 11, 14, "#2d2833"); Px(b, 5, 13, "#2d2833"); Px(b, 11, 13, "#2d2833");
        Ellipse(b, 8, 11, 5.5, 2.6, "#5d5566");
        Ellipse(b, 8, 10.2, 4.2, 1.4, "#2a2230");
        string[] flame = {
            "......R.........",
            ".....RR...R.....",
            "....RRO..RR.....",
            "....ROOR.RO.....",
            "...RROYORRO.....",
            "...ROYYYOOR.....",
            "...ROYWYYOR.....",
            "....OYWWYO......",
            "....OYYYYO......",
        };
        for (int y = 0; y < flame.Length; y++)
            for (int x = 0; x < S; x++)
            {
                char ch = flame[y][x];
                string hex = ch == 'R' ? "#e8364a" : ch == 'O' ? "#ff6a2a" : ch == 'Y' ? "#ffb02e" : ch == 'W' ? "#fff2a8" : null;
                if (hex != null) Px(b, x + 1, y + 1, hex);
            }
        Save(b, "boss_brazier");

        // Scattered bones (walkable decoration)
        b = New();
        Ellipse(b, 5, 6, 2.5, 2.2, "#e8e0cc");
        Px(b, 4, 6, "#3a2f2a"); Px(b, 6, 6, "#3a2f2a"); Px(b, 5, 8, "#bfb49a");
        for (int i = 0; i < 6; i++) Px(b, 8 + i, 11 - i / 2, "#ddd4bd");
        Px(b, 8, 12, "#e8e0cc"); Px(b, 7, 11, "#e8e0cc"); Px(b, 14, 8, "#e8e0cc"); Px(b, 13, 7, "#e8e0cc");
        Save(b, "boss_bones");
    }

    // ================= Side-scroll tiles =================
    // Solid ground (auto top/fill), one-way platforms, background props.

    static Bitmap Dirt(int seed)
    {
        var r = new Random(seed);
        var b = New();
        Fill(b, "#8a5a3b");
        Noise(b, r, 0.3, "#7a4e33", "#9a6844");
        for (int i = 0; i < 3; i++)
        {
            int x = r.Next(1, 14), y = r.Next(6, 15);
            Px(b, x, y, "#b08a62"); Px(b, x + 1, y, "#6b452c");
        }
        return b;
    }

    static void ForestSide()
    {
        // Ground top: grass cap over dirt
        var r = new Random(301);
        var b = Dirt(302);
        for (int x = 0; x < S; x++)
        {
            int depth = 3 + r.Next(3);
            for (int y = 0; y < depth; y++) Px(b, x, y, r.NextDouble() < 0.25 ? "#64a644" : "#5b9a3c");
            Px(b, x, depth, "#3f7a2a");
            if (r.NextDouble() < 0.3) Px(b, x, 0, "#8cc95c");
        }
        Save(b, "side_forest_top");

        b = Dirt(311);
        r = new Random(312);
        for (int i = 0; i < 2; i++)
        {
            int x = r.Next(2, 14), y = r.Next(2, 12);
            Px(b, x, y, "#5e3a24"); Px(b, x + 1, y + 1, "#5e3a24"); Px(b, x + 2, y + 1, "#5e3a24");
        }
        Save(b, "side_forest_fill");

        // One-way wooden plank platform
        b = New();
        for (int x = 0; x < S; x++)
        {
            Px(b, x, 0, "#c08a55");
            for (int y = 1; y < 4; y++) Px(b, x, y, "#9c6b3f");
            Px(b, x, 4, "#5e3c20");
        }
        for (int y = 0; y < 5; y++) { Px(b, 0, y, "#5e3c20"); Px(b, 8, y, "#5e3c20"); }
        Px(b, 2, 2, "#d8c7a0"); Px(b, 10, 2, "#d8c7a0");
        for (int y = 5; y < 8; y++) { Px(b, 3, y, "#5e3c20"); Px(b, 12, y, "#5e3c20"); }
        Save(b, "side_forest_platform");

        // Background tree trunk (dim, tiles vertically)
        r = new Random(321);
        b = New();
        for (int y = 0; y < S; y++)
            for (int x = 5; x <= 10; x++)
                Px(b, x, y, x == 5 ? "#5a4a42" : x == 10 ? "#2e241f" : (r.NextDouble() < 0.2 ? "#3a2d28" : "#4a3a33"));
        Save(b, "side_forest_bg_trunk");

        // Background canopy block
        r = new Random(331);
        b = New();
        Fill(b, "#2f5a3a");
        Noise(b, r, 0.35, "#284f33", "#376844");
        for (int i = 0; i < 4; i++) Px(b, r.Next(S), r.Next(S), "#4a7d55");
        Save(b, "side_forest_bg_leaves");

        // Ground flowers (decor, sit on the tile below)
        b = New();
        int[] fx = { 3, 8, 12 };
        string[] petals = { "#f7f3e8", "#f29bb5", "#b7a4f2" };
        for (int i = 0; i < 3; i++)
        {
            int h = 4 + i % 2 * 2;
            for (int y = 15; y > 15 - h; y--) Px(b, fx[i], y, "#3f7a2a");
            int top = 15 - h;
            FlowerAt(b, fx[i], top, petals[i]);
        }
        Px(b, 4, 13, "#5b9a3c"); Px(b, 9, 14, "#5b9a3c");
        Save(b, "side_forest_flowers");
    }

    static void RuinsSide()
    {
        var r = new Random(401);
        var b = New();
        Fill(b, "#5d586a");
        Noise(b, r, 0.2, "#57526a", "#666176");
        for (int row = 0; row < 4; row++)
        {
            int y0 = row * 4;
            for (int x = 0; x < S; x++) Px(b, x, y0, "#3e3a4a");
            int off = (row % 2 == 0) ? 2 : 10;
            for (int y = y0; y < y0 + 4; y++) Px(b, off, y, "#3e3a4a");
        }
        var fill = (Bitmap)b.Clone();
        Save(fill, "side_ruins_fill");

        for (int x = 0; x < S; x++) { Px(b, x, 0, "#a7a2b4"); Px(b, x, 1, "#8c8799"); Px(b, x, 2, "#4f4b5a"); }
        Px(b, 5, 1, "#6f6a7a"); Px(b, 12, 1, "#6f6a7a");
        Save(b, "side_ruins_top");

        // Stone slab one-way platform
        b = New();
        for (int x = 0; x < S; x++)
        {
            Px(b, x, 0, "#a7a2b4");
            for (int y = 1; y < 4; y++) Px(b, x, y, "#8c8799");
            Px(b, x, 4, "#4f4b5a");
        }
        Px(b, 6, 2, "#6f6a7a"); Px(b, 7, 3, "#6f6a7a");
        for (int y = 5; y < 8; y++) for (int x = 6; x <= 9; x++) Px(b, x, y, y == 7 ? "#3e3a4a" : "#6f6a7a");
        Save(b, "side_ruins_platform");

        // Dim back wall
        r = new Random(411);
        var wall = New();
        Fill(wall, "#2b2733");
        Noise(wall, r, 0.2, "#28242f", "#2f2b38");
        for (int row = 0; row < 2; row++)
        {
            int y0 = row * 8;
            for (int x = 0; x < S; x++) Px(wall, x, y0, "#211e28");
            int off = row == 0 ? 4 : 12;
            for (int y = y0; y < y0 + 8; y++) Px(wall, off, y, "#211e28");
        }
        Save((Bitmap)wall.Clone(), "side_ruins_bg_wall");

        // Background pillar (over the wall, tiles vertically)
        b = (Bitmap)wall.Clone();
        for (int y = 0; y < S; y++)
            for (int x = 3; x <= 12; x++)
                Px(b, x, y, x == 3 ? "#5a5568" : x == 12 ? "#2f2b3a" : (x == 6 || x == 9) ? "#3b3747" : "#4a4658");
        Save(b, "side_ruins_bg_pillar");

        // Arched window with crimson glow
        b = (Bitmap)wall.Clone();
        for (int y = 2; y < S; y++)
            for (int x = 4; x <= 11; x++)
            {
                if (y < 5 && !InDisc(x, y, 8, 5.5, 4, 3.5)) continue;
                bool frame = x == 4 || x == 11 || (y < 5 && !InDisc(x, y, 8, 5.5, 3, 2.5));
                string glow = y > 11 ? "#6a1a2c" : y > 8 ? "#3e1420" : "#120f18";
                Px(b, x, y, frame ? "#4a4658" : glow);
            }
        Save(b, "side_ruins_bg_window");

        // Hanging chain (decor)
        b = New();
        for (int y = 0; y < S; y++)
        {
            bool link = (y % 4) < 2;
            if (link) { Px(b, 7, y, "#6f6a7a"); Px(b, 9, y, "#6f6a7a"); }
            else Px(b, 8, y, "#8c8799");
        }
        Save(b, "side_ruins_chain");

        // Crimson moss mound on the floor (decor)
        r = new Random(421);
        b = New();
        Ellipse(b, 5, 15.5, 4.5, 3, "#7a2233");
        Ellipse(b, 11, 15.8, 3.5, 2.2, "#7a2233");
        Ellipse(b, 5, 15, 2.5, 1.5, "#a8324a");
        Px(b, 4, 13, "#e0506a"); Px(b, 11, 14, "#e0506a");
        Save(b, "side_ruins_moss");
    }

    static void BossSide()
    {
        var r = new Random(501);
        var b = New();
        Fill(b, "#1c1522");
        Noise(b, r, 0.2, "#18121d", "#241b2b");
        for (int x = 0; x < S; x++) { Px(b, x, 0, "#c2324a"); Px(b, x, 1, "#8b1e33"); Px(b, x, 2, "#2a2131"); }
        Crack(b, new Random(9), 6, 2, 6, "#8b1e33");
        Save(b, "side_boss_top");

        // One-way obsidian slab with crimson drips
        b = New();
        for (int x = 0; x < S; x++)
        {
            Px(b, x, 0, "#8b1e33");
            for (int y = 1; y < 4; y++) Px(b, x, y, "#2a2131");
            Px(b, x, 4, "#120d17");
        }
        Px(b, 3, 5, "#7d1227"); Px(b, 3, 6, "#c2324a"); Px(b, 11, 5, "#7d1227"); Px(b, 11, 6, "#7d1227"); Px(b, 11, 7, "#c2324a");
        Save(b, "side_boss_platform");

        // Dim back wall
        r = new Random(511);
        var wall = New();
        Fill(wall, "#17111c");
        Noise(wall, r, 0.2, "#140f18", "#1c1522");
        for (int row = 0; row < 2; row++)
        {
            int y0 = row * 8;
            for (int x = 0; x < S; x++) Px(wall, x, y0, "#0f0b13");
            int off = row == 0 ? 5 : 13;
            for (int y = y0; y < y0 + 8; y++) Px(wall, off, y, "#0f0b13");
        }
        if (r.NextDouble() < 1) Px(wall, 10, 11, "#3a1420");
        Save((Bitmap)wall.Clone(), "side_boss_bg_wall");

        // Crimson banner
        b = (Bitmap)wall.Clone();
        for (int y = 0; y < S; y++)
            for (int x = 4; x <= 11; x++)
            {
                if (y > 12 && Math.Abs(x - 7.5) < (y - 12)) continue; // V notch
                Px(b, x, y, (x == 4 || x == 11) ? "#5a1222" : "#8b1e33");
            }
        Px(b, 7, 6, "#c9a24a"); Px(b, 8, 6, "#c9a24a"); Px(b, 6, 7, "#c9a24a"); Px(b, 9, 7, "#c9a24a"); Px(b, 7, 8, "#c9a24a"); Px(b, 8, 8, "#c9a24a");
        Save(b, "side_boss_banner");

        // Rune panel on the wall
        b = (Bitmap)wall.Clone();
        for (int i = -5; i <= 5; i++)
        {
            int a = 5 - Math.Abs(i);
            Px(b, 8 + i, 8 - a, "#d23a52"); Px(b, 8 + i, 8 + a, "#d23a52");
        }
        Px(b, 8, 8, "#ff8a9a"); Px(b, 7, 8, "#ff6b7f"); Px(b, 9, 8, "#ff6b7f"); Px(b, 8, 7, "#ff6b7f"); Px(b, 8, 9, "#ff6b7f");
        Save(b, "side_boss_bg_rune");

        // Bones lying on the floor (decor)
        b = New();
        Ellipse(b, 4, 13, 2.5, 2.2, "#e8e0cc");
        Px(b, 3, 13, "#3a2f2a"); Px(b, 5, 13, "#3a2f2a"); Px(b, 4, 15, "#bfb49a");
        for (int x = 8; x <= 14; x++) Px(b, x, 15, "#ddd4bd");
        Px(b, 8, 14, "#e8e0cc"); Px(b, 14, 14, "#e8e0cc");
        Save(b, "side_boss_bones");
    }

    public static void Run(string dir)
    {
        Dir = dir;
        Forest();
        Ruins();
        Boss();
        ForestSide();
        RuinsSide();
        BossSide();
    }
}
'@

[TileGen]::Run($outDir)
Get-ChildItem $outDir -Filter *.png | Select-Object -ExpandProperty Name
