using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Menu: Cats Smash > Build Stage Scenes (1-5)
//
// Builds the side-scrolling Stage1..Stage5.unity from GameScene (so the Player,
// UI, card draft, result screen and all their wiring come along untouched), then per stage:
//   - keeps only that stage's enemy group, removes the others + the unused map UI
//   - adds a Grid "StageMap" with Background / Solid / Platforms / Decor tilemaps
//     painted from layouts drawn with MapCanvas below (ground runs, platforms, props).
//     Terrain uses the Free Cute Tileset (see CuteTileset.cs), auto-tiled from each
//     cell's neighbours (grass edges, corners, rounded undersides); trees / bushes /
//     rocks from its Decors sheet are placed as sprites, and its sky / mountain / hill
//     images scroll behind the map (ParallaxBackground on the camera).
//   - stands the Player on 'P' and enemies on 'M' / 'B' (extra monsters are cloned)
//   - sets RunManager.stageNumber / nextSceneName
// Also points TitleScene's "touch to start" at Stage1 and adds the stages to Build
// Settings. Safe to re-run after editing a layout - it rebuilds the stage scenes.
//
// Shared layout symbols:
//   #  solid ground/wall (top edge tile picked automatically)
//   =  one-way platform (jump up through it, land on top, S to drop down)
//   .  empty air
//   P  player start   M  white rat   S  demon spider   L  slime (temple)   U  mushroom (forest)   B  boss (the bear)
//   (each stands on the tile below it)
// Each stage adds its own background (behind everything) and decor (walkable props) symbols.
// Player reach: ~3 tiles with one jump, ~5 with the double jump. ValidateReachable checks
// that every enemy can actually be reached from the start before any scene is built.
public static class StageSceneBuilder
{
    const string SpriteDir = "Assets/Tiles/Sprites";
    const string TileDir = "Assets/Tiles/Tiles";
    const string SourceScene = "Assets/Scenes/GameScene.unity";
    const string TitleScene = "Assets/Scenes/TitleScene.unity";
    const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";
    const string LoadingScenePath = "Assets/Scenes/LoadingScene.unity";
    const string MainScenePath = "Assets/Scenes/MainScene.unity";
    const string AnsimuzForestDir = "Assets/Backgrounds/AnsimuzForest/layers";
    const string WorldMapScenePath = "Assets/Scenes/WorldMapScene.unity";
    // Title -> character select lobby (falls back to Stage1 if the lobby was never built).
    static string FirstSceneAfterTitle => File.Exists(LobbyScenePath) ? "LobbyScene" : "Stage1";

    // Tile names for one terrain look, picked per cell from which neighbours are solid.
    class Terrain
    {
        public string topLeft, top, topRight, left, right, bottomLeft, bottom, bottomRight;
        public string innerTopLeft, innerTopRight; // fill cell whose up-left / up-right is open
        public string[] center;                    // fill variants, picked by position
        public string platLeft, platMid, platRight, platSingle;
    }

    static readonly Terrain Grass = new Terrain
    {
        topLeft = "cute_0_0", top = "cute_1_0", topRight = "cute_2_0",
        left = "cute_0_1", right = "cute_2_1",
        bottomLeft = "cute_0_2", bottom = "cute_1_2", bottomRight = "cute_2_2",
        innerTopLeft = "cute_3_0", innerTopRight = "cute_4_0",
        center = new[] { "cute_1_1", "cute_5_1", "cute_6_1" },
        platLeft = "cute_plat_grass_l", platMid = "cute_plat_grass_m", platRight = "cute_plat_grass_r", platSingle = "cute_5_2",
    };

    // (The brick terrain and cobble wall of the old temple / altar stages are gone: every stage is forest now.)

    // A sprite placed on the ground at a layout symbol (bottom-centre pivot).
    class Prop
    {
        public string sprite;
        public int order;    // sorting order (Solid is -50: lower = behind the ground)
        public float sink;   // pushed this far into the ground so it looks planted
    }

    static readonly Prop TreeProp = new Prop { sprite = "cute_tree", order = -52, sink = 0.15f };
    static readonly Prop BushProp = new Prop { sprite = "cute_bush", order = -47, sink = 0.1f };
    static readonly Prop RockProp = new Prop { sprite = "cute_rock", order = -47, sink = 0.1f };
    // The corruption: crimson copies of the above (hue-shifted by CuteTileset), seeping in from stage 2.
    static readonly Prop RedTreeProp = new Prop { sprite = "corrupt_tree", order = -52, sink = 0.15f };
    static readonly Prop RedBushProp = new Prop { sprite = "corrupt_bush", order = -47, sink = 0.1f };
    static readonly Prop RedRockProp = new Prop { sprite = "corrupt_rock", order = -47, sink = 0.1f };

    class StageDef
    {
        public int number;
        public string nextScene;
        public string keepGroup;
        public Terrain terrain;
        public string[] defaultBackground; // painted behind every cell (null = parallax / sky shows)
        public Color backgroundTint = Color.white;
        // Background props (pillars, banners) on their own layer; "" = leave the cell open.
        public Dictionary<char, string> background = new Dictionary<char, string>();
        public Color backgroundPropTint = Color.white;
        public Dictionary<char, string> decor = new Dictionary<char, string>();
        public Dictionary<char, Prop> props = new Dictionary<char, Prop>();
        public bool parallax;
        // Stage mood: colours multiplied onto the ground / props and the sky, mountains, hills layers,
        // plus the floating atmosphere. Stage 1 is plain morning; each stage is a step darker / redder.
        public Color terrainTint = Color.white;
        public string skyVariant;          // recoloured sky sprite for the first parallax layer (null = the original blue)
        public Color[] parallaxTints;
        public AmbientMotes.Layer[] ambient;
        public Color sky;
        public List<Vector2> spawnSlots = new List<Vector2>(); // filled from ValidateReachable
        public string[] layout;
        public string sceneName;  // null = "Stage{number}"
        public bool tutorial;     // the tutorial stage (TutorialController, no randomizer, no cards)
        public bool verticalLayout; // camera and exit progress upward instead of mainly right
        public float[] verticalGates;
        public float[] horizontalGates;
        public string ScenePath => $"Assets/Scenes/{sceneName ?? $"Stage{number}"}.unity";
    }

    static readonly string[] AllGroups = { "Stage1Monsters", "Stage2Monsters", "Stage3Boss" };
    const int StageCount = 5;
    const float StageCameraSize = 4.4f; // half the view height in world units (tiles)

    // ---------------------------------------------------------------- map canvas
    // Stages are big (Skul-sized rooms several screens wide), so instead of typing every
    // row by hand they are laid out with a few drawing calls - ground runs, one-way
    // platforms, blocks, trees, pillars - and then turned into the same layout strings
    // the rest of the builder paints from. x = column, y = row counted from the BOTTOM.
    // A "stand" height is the y of the empty cell the feet are in (the tile below holds).
    // Player reach (for laying things out): one jump ~3 tiles up, double jump ~5.
    class MapCanvas
    {
        public readonly int W, H;
        readonly char[,] g;
        readonly System.Random rng;

        public MapCanvas(int w, int h, int seed)
        {
            W = w; H = h;
            g = new char[w, h];
            for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) g[x, y] = '.';
            rng = new System.Random(seed);
        }

        // Outside the map: walls left/right and below, open sky above (like StageMap).
        public char At(int x, int y) => x < 0 || x >= W || y < 0 ? '#' : y >= H ? '.' : g[x, y];
        public bool Solid(int x, int y) => At(x, y) == '#';
        public bool Support(int x, int y) => At(x, y) == '#' || At(x, y) == '=';

        public void Fill(int x0, int x1, int y0, int y1, char ch)
        {
            for (int x = Mathf.Max(0, x0); x <= Mathf.Min(W - 1, x1); x++)
                for (int y = Mathf.Max(0, y0); y <= Mathf.Min(H - 1, y1); y++)
                    g[x, y] = ch;
        }

        // Fills only empty cells (background/decor never cover terrain or markers).
        void Soft(int x, int y, char ch)
        {
            if (x >= 0 && x < W && y >= 0 && y < H && g[x, y] == '.') g[x, y] = ch;
        }

        // Solid ground from the bottom up to (not including) `top`: you stand at y = top.
        public void Ground(int x0, int x1, int top) { Fill(x0, x1, 0, H - 1, '.'); Fill(x0, x1, 0, top - 1, '#'); }
        public void Block(int x0, int x1, int y0, int y1) => Fill(x0, x1, y0, y1, '#');
        // One-way platform you stand on at height `stand` (the tile sits just below).
        public void Plat(int x0, int len, int stand) => Fill(x0, x0 + len - 1, stand - 1, stand - 1, '=');
        public void Ceiling(int rows) => Fill(0, W - 1, H - rows, H - 1, '#');

        // Height you stand at on the ground column at x (first empty cell from the bottom).
        public int Surface(int x)
        {
            int y = 0;
            while (y < H && g[x, y] == '#') y++;
            return y;
        }

        // P / M / S / B marker standing at (x, stand) - must be on something solid.
        public void Spawn(char who, int x, int stand)
        {
            if (!Support(x, stand - 1) || At(x, stand) == '#')
                throw new System.Exception($"[StageSceneBuilder] '{who}' at ({x},{stand}) has nothing to stand on.");
            g[x, stand] = who;
        }
        public void SpawnGround(char who, int x) => Spawn(who, x, Surface(x));

        // Prop marker (tree / bush / rock sprite) standing on the ground column at x.
        public void PropAt(int x, char ch) => Soft(x, Surface(x), ch);
        public void PropAt(int x, int stand, char ch)
        {
            if (!Support(x, stand - 1) || At(x, stand) == '#')
                throw new System.Exception($"[StageSceneBuilder] prop '{ch}' at ({x},{stand}) has nothing to stand on.");
            Soft(x, stand, ch);
        }

        // Background columns (temple pillars) from the floor to `top`, every `step` columns.
        public void Columns(char ch, int start, int step, int top)
        {
            for (int x = start; x < W; x += step)
                for (int y = Surface(x); y < top; y++) Soft(x, y, ch);
        }

        public void Window(int x, int y0, int height, char ch)
        {
            for (int y = y0; y < y0 + height; y++) Soft(x, y, ch);
        }

        // Random props on the tops of ground (not platforms), `chance` per cell, at least
        // `spacing` columns apart and only where the ground is flat under the prop.
        public void Scatter(string props, float chance, int spacing)
        {
            int last = -spacing;
            for (int x = 1; x < W - 1; x++)
            {
                int s = Surface(x);
                if (x - last < spacing || s >= H || g[x, s] != '.') continue;
                if (Surface(x - 1) != s || Surface(x + 1) != s || rng.NextDouble() > chance) continue;
                g[x, s] = props[rng.Next(props.Length)];
                last = x;
            }
        }

        public string[] ToLayout()
        {
            var rows = new string[H];
            var sb = new System.Text.StringBuilder(W);
            for (int r = 0; r < H; r++)
            {
                sb.Clear();
                for (int x = 0; x < W; x++) sb.Append(g[x, H - 1 - r]);
                rows[r] = sb.ToString();
            }
            return rows;
        }
    }

    // Every enemy must be somewhere the player can actually get to (or the stage could never
    // be cleared). Walks/falls/jumps from 'P' over standable cells - stepping sideways,
    // dropping off edges, and jumping up to 5 tiles (double jump) across up to 4 columns.
    static HashSet<(int, int)> ValidateReachable(StageDef def)
    {
        string[] L = def.layout;
        int H = L.Length, W = L[0].Length;
        char C(int x, int y) => x < 0 || x >= W || y < 0 ? '#' : y >= H ? '.' : L[H - 1 - y][x];
        bool Solid(int x, int y) => C(x, y) == '#';
        bool Clear(int x, int y0, int y1) { for (int y = y0; y <= y1; y++) if (Solid(x, y)) return false; return true; }
        // Feet at (x, s): empty enough for a 3-tile-tall body, with something to stand on.
        bool Stand(int x, int s) => x >= 0 && x < W && s > 0 && s < H && (C(x, s - 1) == '#' || C(x, s - 1) == '=') && Clear(x, s, s + 2);

        var start = (-1, -1);
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if (C(x, y) == 'P') start = (x, y);
        if (start.Item1 < 0) throw new System.Exception($"[StageSceneBuilder] Stage{def.number}: no 'P'.");

        var seen = new HashSet<(int, int)> { start };
        var queue = new Queue<(int, int)>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var (x, s) = queue.Dequeue();
            for (int nx = x - 4; nx <= x + 4; nx++)
                for (int ns = s - 12; ns <= s + 5; ns++)
                {
                    if (!Stand(nx, ns) || seen.Contains((nx, ns))) continue;
                    int dx = Mathf.Abs(nx - x);
                    // Path: up from the start column to the higher of the two heights + head
                    // room, then across and down at the target column.
                    int peak = Mathf.Max(s, ns);
                    if (!Clear(x, s, peak + 2) || !Clear(nx, ns, peak + 2)) continue;
                    bool across = true;
                    for (int cx = Mathf.Min(x, nx) + 1; cx < Mathf.Max(x, nx); cx++)
                        if (!Clear(cx, peak, peak + 2)) { across = false; break; }
                    if (!across) continue;
                    // Higher than one jump (~3) needs the double jump, which covers less ground.
                    if (ns - s > 3 && dx > 3) continue;
                    seen.Add((nx, ns));
                    queue.Enqueue((nx, ns));
                }
        }

        var missing = new List<string>();
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                if ("MSGB".IndexOf(C(x, y)) >= 0 && !seen.Contains((x, y))) missing.Add($"{C(x, y)}({x},{y})");
        if (missing.Count > 0)
            throw new System.Exception($"[StageSceneBuilder] Stage{def.number}: unreachable enemies: {string.Join(", ", missing)}");
        return seen;
    }

    // Spots StageRandomizer may move a monster to each run: reachable floor (from the search
    // above) that is also standable one column to each side, so a wide body fits.
    static List<Vector2> SpawnSlots(HashSet<(int, int)> reachable)
    {
        var slots = new List<Vector2>();
        foreach (var (x, s) in reachable)
            if (reachable.Contains((x - 1, s)) && reachable.Contains((x + 1, s)))
                slots.Add(new Vector2(x + 0.5f, s));
        slots.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y)); // stable order in the scene file
        return slots;
    }

    // ---------------------------------------------------------------- stages
    // Symbols: # solid  = one-way platform  P player  M white rat  S demon spider  L slime  U mushroom  B boss.
    // Props (sprites): T tree  b bush  o rock  (y r x: their crimson, corrupted versions)
    // Boss:   k bones (decor)
    // All five stages are one forest, each a step deeper: morning edge -> sunset -> twilight deep forest ->
    // crimson night -> the corrupted heart (boss). Darker ground and sky, denser and redder trees as you go.

    static StageDef Forest(int number, MapCanvas m, Color sky) => new StageDef
    {
        number = number,
        nextScene = number < StageCount ? $"Stage{number + 1}" : "",
        keepGroup = "Stage1Monsters",
        terrain = Grass,
        parallax = true,
        sky = sky,
        props = { { 'T', TreeProp }, { 'b', BushProp }, { 'o', RockProp }, { 'y', RedTreeProp }, { 'r', RedBushProp }, { 'x', RedRockProp } },
        layout = m.ToLayout(),
    };

    // red = how much of the planting has turned crimson (0 = none, 0.35 = stage 2: the corruption
    // is just starting): that share of the trees, and of the scattered bushes / rocks.
    static void ForestDressing(MapCanvas m, float red, params int[] trees)
    {
        var rng = new System.Random(m.W * 31 + trees.Length);
        foreach (int x in trees) m.PropAt(x, rng.NextDouble() < red ? 'y' : 'T');
        m.Scatter(ScatterPool("bbo", "rrx", red), 0.3f, 5);
    }

    // "bbo" plus a few crimson tokens in proportion to `red`.
    static string ScatterPool(string normal, string crimson, float red)
    {
        int n = Mathf.RoundToInt(red * 6f);
        var sb = new System.Text.StringBuilder(normal);
        for (int i = 0; i < n; i++) sb.Append(crimson[i % crimson.Length]);
        return sb.ToString();
    }

    // Stage mood in one call: ground/prop tint, the three sky layers (sky, mountains, hills) and the
    // floating atmosphere.
    static StageDef Mood(StageDef d, Color terrain, string skyVariant, Color[] sky, params AmbientMotes.Layer[] ambient)
    {
        d.terrainTint = terrain;
        d.skyVariant = skyVariant;
        d.parallaxTints = sky;
        d.ambient = ambient;
        return d;
    }

    static StageDef Vertical(StageDef d, params float[] gates)
    {
        d.verticalLayout = true;
        d.verticalGates = gates;
        return d;
    }

    static StageDef HorizontalRooms(StageDef d, params float[] gates)
    {
        d.horizontalGates = gates;
        return d;
    }

    static AmbientMotes.Layer Motes(AmbientMotes.Kind kind, int count) => new AmbientMotes.Layer { kind = kind, count = count };
    static Color[] Sky(Color a, Color b, Color c) => new[] { a, b, c };

    // Stage 1 - Forest Edge (120 x 16): rolling ground and low branch platforms. 13 monsters.
    static StageDef Stage1()
    {
        var m = new MapCanvas(120, 16, 11);
        m.Ground(0, 19, 3); m.Ground(20, 29, 4); m.Ground(30, 39, 3); m.Ground(40, 46, 1); m.Ground(47, 47, 2);
        m.Ground(48, 58, 3); m.Ground(59, 61, 4); m.Ground(62, 71, 5); m.Ground(72, 85, 3); m.Ground(86, 93, 2); m.Ground(94, 119, 4);
        m.Plat(8, 5, 6); m.Plat(14, 5, 9); m.Plat(22, 6, 8); m.Plat(33, 6, 7);
        m.Plat(50, 6, 6); m.Plat(56, 5, 9); m.Plat(64, 6, 9); m.Plat(75, 6, 7);
        m.Plat(88, 5, 6); m.Plat(97, 6, 8); m.Plat(106, 6, 7); m.Plat(112, 5, 10);

        m.SpawnGround('P', 3);
        m.SpawnGround('M', 12); m.SpawnGround('M', 26);
        m.SpawnGround('S', 37); m.Spawn('M', 35, 7);
        m.SpawnGround('M', 54); m.SpawnGround('S', 60);
        m.SpawnGround('M', 67); m.SpawnGround('S', 80); m.Spawn('M', 77, 7);
        m.SpawnGround('M', 100); m.SpawnGround('S', 109);
        m.SpawnGround('U', 45); m.SpawnGround('U', 83);   // mushrooms
        ForestDressing(m, 0f, 5, 36, 54, 88, 114);
        // Morning: plain bright, a little pollen in the air.
        return HorizontalRooms(Mood(Forest(1, m, new Color32(0x8f, 0xc8, 0xd8, 0xff)), Color.white, null, null, Motes(AmbientMotes.Kind.Pollen, 26)), 30f, 60f, 90f);
    }

    // Stage 2 - Sunset Canopy (54 x 56): a vertical zigzag climb. Broad branches overlap
    // by a few tiles, so every four-tile rise works with the double jump. 14 monsters.
    static StageDef Stage2()
    {
        var m = new MapCanvas(54, 56, 22);
        m.Ground(0, 53, 3);
        m.Plat(4, 15, 7); m.Plat(16, 16, 11); m.Plat(29, 16, 15); m.Plat(42, 12, 19);
        m.Plat(8, 38, 23); m.Plat(18, 15, 27); m.Plat(6, 15, 31); m.Plat(18, 15, 35);
        m.Plat(8, 38, 39); m.Plat(42, 12, 43); m.Plat(30, 15, 47); m.Plat(16, 16, 51);

        m.SpawnGround('P', 6);
        m.SpawnGround('M', 18); m.SpawnGround('S', 42);
        m.Spawn('M', 10, 7); m.Spawn('S', 22, 11); m.Spawn('M', 35, 15); m.Spawn('S', 48, 19);
        m.Spawn('M', 36, 23); m.Spawn('M', 24, 27); m.Spawn('S', 12, 31); m.Spawn('M', 24, 35);
        m.Spawn('S', 36, 39); m.Spawn('M', 48, 43); m.Spawn('S', 36, 47); m.Spawn('M', 22, 51);
        ForestDressing(m, 0.4f, 3, 49);
        // Sunset: the climb starts warm, then rises through leaves and crimson spores.
        return Vertical(Mood(Forest(2, m, new Color32(0x6f, 0x9c, 0xb8, 0xff)), new Color(1f, 0.89f, 0.81f), "sky_sunset",
            Sky(Color.white, new Color(1f, 0.8f, 0.8f), new Color(0.92f, 0.72f, 0.64f)),
            Motes(AmbientMotes.Kind.Leaves, 18), Motes(AmbientMotes.Kind.Spores, 8)), 21.5f, 37.5f);
    }

    // Stage 3 - Deep Forest (130 x 22): the trees close in. Two routes side by side - a low
    // forest floor with dips and mounds, and a canopy walk of branches 3 tiles up, stacked a
    // third tier high in places. Jump between them to pick your fights. 16 monsters.
    static StageDef Stage3()
    {
        var m = new MapCanvas(130, 22, 33);
        m.Ground(0, 15, 2); m.Ground(16, 21, 5); m.Ground(22, 39, 2); m.Ground(40, 43, 1);
        m.Ground(44, 53, 4); m.Ground(54, 73, 2); m.Ground(74, 79, 6); m.Ground(80, 99, 2);
        m.Ground(100, 103, 1); m.Ground(104, 129, 3);
        // (No ceiling: the highest platform + a double jump stays under the map top.)
        m.Plat(24, 8, 6); m.Plat(30, 10, 10); m.Plat(42, 6, 13);       // canopy 1: low -> mid -> top
        m.Plat(56, 6, 6); m.Plat(62, 12, 10);                          // canopy 2
        m.Plat(76, 4, 9); m.Plat(82, 6, 6); m.Plat(86, 12, 10); m.Plat(98, 6, 13); // canopy 3
        m.Plat(108, 6, 7); m.Plat(116, 8, 10);                         // last climb

        m.SpawnGround('P', 3);
        m.SpawnGround('M', 10); m.SpawnGround('S', 19); m.SpawnGround('M', 34);
        m.Spawn('M', 27, 6); m.Spawn('S', 35, 10); m.Spawn('M', 45, 13); m.SpawnGround('M', 50);
        m.SpawnGround('L', 64); m.Spawn('M', 67, 10); m.SpawnGround('S', 77);
        m.SpawnGround('M', 90); m.Spawn('M', 92, 10); m.Spawn('S', 100, 13);
        m.Spawn('M', 110, 7); m.SpawnGround('L', 124);
        // Denser than stage 2 (12 trees), and most of them have turned crimson.
        ForestDressing(m, 0.6f, 4, 13, 21, 31, 47, 60, 72, 81, 94, 103, 115, 126);
        // Twilight: the light is going violet and the ground dims; fewer leaves, more spores.
        return HorizontalRooms(Mood(Forest(3, m, new Color32(0x3a, 0x3c, 0x5c, 0xff)), new Color(0.8f, 0.82f, 0.95f), "sky_dusk",
            Sky(new Color(0.88f, 0.86f, 0.96f), new Color(0.58f, 0.56f, 0.86f), new Color(0.4f, 0.42f, 0.68f)),
            Motes(AmbientMotes.Kind.Leaves, 10), Motes(AmbientMotes.Kind.Spores, 16)), 32.5f, 65f, 97.5f);
    }

    // Stage 4 - Crimson Crown (60 x 72): three broad combat floors joined by narrow
    // climbing branches, then a final ascent toward the boss. 14 monsters.
    static StageDef Stage4()
    {
        var m = new MapCanvas(60, 72, 44);
        m.Ground(0, 59, 3);
        m.Plat(4, 14, 7); m.Plat(15, 14, 11); m.Plat(12, 36, 15);                  // first combat floor
        m.Plat(38, 14, 20); m.Plat(27, 14, 24); m.Plat(16, 14, 28); m.Plat(5, 14, 32);
        m.Plat(10, 40, 35);                                                       // second combat floor
        m.Plat(38, 14, 40); m.Plat(27, 14, 44); m.Plat(16, 14, 48); m.Plat(5, 14, 52);
        m.Plat(10, 40, 55);                                                       // final combat floor
        m.Plat(38, 14, 60); m.Plat(27, 14, 64); m.Plat(16, 14, 68);
        // Small crimson landmarks keep the upper climb readable without hiding enemies.
        m.PropAt(12, 15, 'r'); m.PropAt(47, 15, 'x');
        m.PropAt(12, 35, 'x'); m.PropAt(47, 35, 'r');
        m.PropAt(48, 40, 'r'); m.PropAt(29, 44, 'x');
        m.PropAt(18, 48, 'r'); m.PropAt(7, 52, 'x');
        m.PropAt(12, 55, 'r'); m.PropAt(29, 55, 'x'); m.PropAt(47, 55, 'x');
        m.PropAt(48, 60, 'r'); m.PropAt(29, 64, 'x'); m.PropAt(38, 64, 'r');
        m.PropAt(18, 68, 'x'); m.PropAt(27, 68, 'r');

        m.SpawnGround('P', 6);
        m.SpawnGround('M', 14);
        m.Spawn('M', 10, 7); m.Spawn('S', 22, 11);
        m.Spawn('M', 18, 15); m.Spawn('S', 30, 15); m.Spawn('L', 42, 15);
        m.Spawn('M', 44, 20);
        m.Spawn('L', 16, 35); m.Spawn('S', 30, 35); m.Spawn('M', 44, 35);
        m.Spawn('M', 44, 40); m.Spawn('L', 22, 48);
        m.Spawn('M', 18, 55); m.Spawn('S', 42, 55);
        ForestDressing(m, 0.85f, 3, 27, 56);
        // Night: a long crimson climb with spores thickening toward the crown.
        return Vertical(Mood(Forest(4, m, new Color32(0x2a, 0x16, 0x26, 0xff)), new Color(0.66f, 0.6f, 0.78f), "sky_crimson",
            Sky(new Color(0.8f, 0.7f, 0.8f), new Color(0.62f, 0.34f, 0.5f), new Color(0.4f, 0.22f, 0.36f)),
            Motes(AmbientMotes.Kind.Spores, 26), Motes(AmbientMotes.Kind.Leaves, 5)), 17.5f, 37.5f);
    }

    // Tutorial (112 x 16): one short forest path where each section needs the next control.
    // Goal positions are shared with TutorialController (set on it when the scene is built).
    const float TutorialMoveGoalX = 14f;
    const float TutorialJumpGoalX = 28f;   // past the 2-high ledge at x 22-27
    const float TutorialWallX = 32.5f;     // on top of the 5-high wall (double jump)
    const float TutorialWallTop = 8f;
    const float TutorialDropGoalX = 55.5f; // under the wall at x 53-54, only reachable by dropping (S)

    static StageDef Tutorial()
    {
        var m = new MapCanvas(112, 16, 7);
        m.Ground(0, 21, 3);                 // move
        m.Ground(22, 27, 5);                // jump: 2-high ledge
        m.Ground(28, 31, 3);
        m.Ground(32, 44, 8);                // double jump: 5-high wall, then a plateau
        m.Ground(45, 70, 2);                // low floor under the plank walk
        m.Plat(45, 8, 8);                   // plank walk at plateau height...
        m.Block(53, 54, 5, 15);             // ...ends in a wall: S to drop, walk under it
        m.Ground(71, 111, 3);               // dash, then the dummies and the rats
        m.Plat(82, 4, 7);                   // a ledge to jump-attack from
        m.SpawnGround('P', 3);
        m.SpawnGround('D', 76);             // combo dummy
        m.SpawnGround('D', 89);             // jump-attack dummy
        m.SpawnGround('M', 99); m.SpawnGround('M', 106);
        ForestDressing(m, 0f, 9, 60, 94);
        var def = Mood(Forest(1, m, new Color32(0x8f, 0xc8, 0xd8, 0xff)), Color.white, null, null, Motes(AmbientMotes.Kind.Pollen, 26));
        def.sceneName = "Tutorial";
        def.tutorial = true;
        def.nextScene = "Stage1";
        return def;
    }

    // Stage 5 - The Corrupted Heart (boss, 56 x 18): a clearing at the very bottom of the forest, walled
    // in by earth banks, every tree crimson. Flat floor for the bear's charge; two tiers of ledges on
    // each side and a high branch in the middle so there is always somewhere to jump to.
    static StageDef Stage5()
    {
        var m = new MapCanvas(56, 18, 55);
        m.Ground(0, 55, 2);
        m.Block(0, 0, 0, 17); m.Block(55, 55, 0, 17);
        m.Plat(5, 7, 6); m.Plat(44, 7, 6);     // low side ledges
        m.Plat(14, 5, 8); m.Plat(37, 5, 8);    // steps up
        m.Plat(22, 12, 10);                    // the high branch
        m.SpawnGround('P', 4); m.SpawnGround('B', 48);
        foreach (int x in new[] { 18, 28, 38 }) m.Spawn('k', x, 2);
        ForestDressing(m, 1f, 3, 10, 24, 31, 45, 52);
        var heart = Forest(5, m, new Color32(0x1c, 0x0a, 0x12, 0xff));
        heart.keepGroup = "Stage3Boss";
        heart.decor = new Dictionary<char, string> { { 'k', "side_boss_bones" } };
        // The final arena turns cold violet-blue, clearly separating it from Stage 4's red tower.
        return Mood(heart, new Color(0.68f, 0.68f, 0.88f), "sky_dusk",
            Sky(new Color(0.62f, 0.72f, 0.92f), new Color(0.34f, 0.3f, 0.62f), new Color(0.16f, 0.12f, 0.38f)),
            Motes(AmbientMotes.Kind.Spores, 30), Motes(AmbientMotes.Kind.Embers, 14));
    }

    // ---------------------------------------------------------------- entry
    [MenuItem("Cats Smash/Build Stage Scenes (1-5)")]
    public static void BuildAll()
    {
        Build(true);
    }

    // Command-line entry point (no dialogs), for running with the Editor closed:
    // Unity.exe -batchmode -quit -projectPath <project> -executeMethod StageSceneBuilder.BuildBatch [-stages Stage3,Stage4,Stage5]
    // (-stages: rebuild only those scenes; the others are left exactly as they are.)
    public static void BuildBatch()
    {
        try
        {
            string[] only = null;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-stages") only = args[i + 1].Split(',');
            Build(false, only);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    public static void Build(bool interactive, string[] onlyScenes = null)
    {
        if (interactive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var stages = new[] { Stage1(), Stage2(), Stage3(), Stage4(), Stage5(), Tutorial() };
        foreach (var s in stages) s.spawnSlots = SpawnSlots(ValidateReachable(s)); // before touching any scene

        bool anyExists = false;
        foreach (var s in stages) anyExists |= File.Exists(s.ScenePath);
        if (interactive && anyExists && !EditorUtility.DisplayDialog("Build Stage Scenes",
                "Stage1-5.unity will be rebuilt from GameScene.\nAny manual edits in those scenes will be lost. Continue?",
                "Rebuild", "Cancel"))
            return;

        try
        {
            EditorUtility.DisplayProgressBar("Build Stage Scenes", "Importing tile sprites...", 0f);
            CuteTileset.Ensure();
            PrepareTiles();
            CharacterAssets.Ensure();
            KoreanFont.Ensure();
            EnemyAssets.Ensure();
            CurrencyIcon.Ensure();

            for (int i = 0; i < stages.Length; i++)
            {
                if (onlyScenes != null && System.Array.IndexOf(onlyScenes, Path.GetFileNameWithoutExtension(stages[i].ScenePath)) < 0) continue;
                EditorUtility.DisplayProgressBar("Build Stage Scenes", $"Building Stage {stages[i].number}...", (i + 1f) / (stages.Length + 2));
                BuildStage(stages[i]);
            }

            PointTitleAtLobby();
            UpdateBuildSettings(stages);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (interactive) EditorSceneManager.OpenScene(stages[0].ScenePath, OpenSceneMode.Single);
        Debug.Log("[StageSceneBuilder] Built Stage1-5 and updated Build Settings / TitleScene.");
    }

    // ---------------------------------------------------------------- tiles
    // Imports the sprites and makes sure a Tile asset exists for each one.
    static void PrepareTiles()
    {
        AssetDatabase.Refresh();
        if (!AssetDatabase.IsValidFolder("Assets/Tiles/Tiles"))
            AssetDatabase.CreateFolder("Assets/Tiles", "Tiles");

        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            // Pixel art: 16px = 1 world unit, no filtering, no compression.
            if (ti.textureType != TextureImporterType.Sprite || ti.spritePixelsPerUnit != 16f ||
                ti.filterMode != FilterMode.Point || ti.textureCompression != TextureImporterCompression.Uncompressed)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = 16f;
                ti.filterMode = FilterMode.Point;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.SaveAndReimport();
            }

            string name = Path.GetFileNameWithoutExtension(path);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            string tilePath = $"{TileDir}/{name}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.colliderType = Tile.ColliderType.None;
                AssetDatabase.CreateAsset(tile, tilePath);
            }
            else if (tile.sprite != sprite)
            {
                tile.sprite = sprite;
                EditorUtility.SetDirty(tile);
            }
        }
        AssetDatabase.SaveAssets();
    }

    // Must be called AFTER OpenScene: opening a scene in Single mode unloads unused
    // assets, which turns Tile references loaded earlier into destroyed (null) objects -
    // that silently left every tilemap empty in the first version of this builder.
    static Dictionary<string, Tile> LoadTiles()
    {
        var tiles = new Dictionary<string, Tile>();
        foreach (var guid in AssetDatabase.FindAssets("t:Tile", new[] { TileDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile != null) tiles[Path.GetFileNameWithoutExtension(path)] = tile;
        }
        return tiles;
    }

    static Tile Get(Dictionary<string, Tile> tiles, string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (!tiles.TryGetValue(name, out var t) || t == null)
            throw new System.Exception($"[StageSceneBuilder] Missing tile '{name}' (sprite in {SpriteDir}, run generate_stage_tiles.ps1).");
        return t;
    }

    // ---------------------------------------------------------------- scenes
    static void BuildStage(StageDef def)
    {
        if (File.Exists(def.ScenePath)) AssetDatabase.DeleteAsset(def.ScenePath);
        if (!AssetDatabase.CopyAsset(SourceScene, def.ScenePath))
            throw new System.Exception($"Could not copy {SourceScene} to {def.ScenePath}");

        Scene scene = EditorSceneManager.OpenScene(def.ScenePath, OpenSceneMode.Single);
        var tiles = LoadTiles();

        // Remove what doesn't belong in this stage.
        foreach (var g in AllGroups)
            if (g != def.keepGroup) DestroyByName(scene, g);
        DestroyByName(scene, "MapController");
        DestroyByName(scene, "MapPanel");

        // Tilemaps
        int rows = def.layout.Length;
        int cols = 0;
        foreach (var line in def.layout) cols = Mathf.Max(cols, line.Length);

        var gridGo = new GameObject("StageMap");
        gridGo.AddComponent<Grid>();
        var stageMap = gridGo.AddComponent<StageMap>();
        var background = MakeLayer(gridGo, "Background", -60);
        background.color = def.backgroundTint;
        var backgroundProps = MakeLayer(gridGo, "BackgroundProps", -58);
        backgroundProps.color = def.backgroundPropTint;
        var propRoot = new GameObject("Props").transform;
        propRoot.SetParent(gridGo.transform, false);
        var solid = MakeLayer(gridGo, "Solid", -50);
        var platforms = MakeLayer(gridGo, "Platforms", -48);
        var decor = MakeLayer(gridGo, "Decor", -45);
        solid.color = platforms.color = decor.color = def.terrainTint; // stage mood
        stageMap.solid = solid;
        stageMap.platforms = platforms;
        stageMap.mapSize = new Vector2Int(cols, rows);
        stageMap.verticalLayout = def.verticalLayout;
        stageMap.verticalGateHeights = def.verticalGates;
        stageMap.horizontalGatePositions = def.horizontalGates;

        var playerSpawns = new List<Vector3>();
        var enemySpawns = new List<(Vector3, EnemyDefinition)>();
        var rat = EnemyAssets.Load(EnemyAssets.RatPath);
        var spider = EnemyAssets.Load(EnemyAssets.SpiderPath);
        var boss = EnemyAssets.Load(EnemyAssets.BearPath); // the last boss (was the werewolf)
        var slime = EnemyAssets.Load(EnemyAssets.SlimePath);
        var mushroom = EnemyAssets.Load(EnemyAssets.MushroomPath);
        var dummy = EnemyAssets.Load(EnemyAssets.DummyPath);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                char ch = At(def, r, c);
                var pos = new Vector3Int(c, rows - 1 - r, 0);

                if (def.background.TryGetValue(ch, out var bg))
                {
                    if (bg != "") backgroundProps.SetTile(pos, Get(tiles, bg));
                }
                if (def.defaultBackground != null && !(def.background.TryGetValue(ch, out var open) && open == ""))
                    background.SetTile(pos, Get(tiles, Variant(def.defaultBackground, c, r)));

                if (ch == '#')
                    solid.SetTile(pos, Get(tiles, SolidTile(def, r, c, cols, rows)));
                else if (ch == '=')
                    platforms.SetTile(pos, Get(tiles, PlatformTile(def, r, c)));
                else if (def.decor.TryGetValue(ch, out var dc))
                    decor.SetTile(pos, Get(tiles, dc));
                else if (def.props.TryGetValue(ch, out var prop))
                    PlaceProp(propRoot, prop, new Vector3(c + 0.5f, rows - 1 - r, 0f), def.terrainTint);

                // Markers: the bottom-center of the cell is the floor they stand on.
                Vector3 feet = new Vector3(c + 0.5f, rows - 1 - r, 0f);
                if (ch == 'P') playerSpawns.Add(feet);
                else if (ch == 'M') enemySpawns.Add((feet, rat));
                else if (ch == 'S') enemySpawns.Add((feet, spider));
                else if (ch == 'L') enemySpawns.Add((feet, slime));
                else if (ch == 'U') enemySpawns.Add((feet, mushroom));
                else if (ch == 'D') enemySpawns.Add((feet, dummy));
                else if (ch == 'B') enemySpawns.Add((feet, boss));
            }
        }

        if (def.number == 4) PlaceAscentSigns(propRoot);

        // Player: feet on the 'P' cell's floor (+ a hair so the first frame lands cleanly).
        var player = FindByName(scene, "Player");
        if (player != null && playerSpawns.Count > 0)
        {
            var pc = player.GetComponent<PlayerController2D>();
            if (pc != null)
            {
                // The cat is drawn at scale 8 (0.08 units per pixel, ~1.4 units tall) so its pixels sit
                // close to the tiles' and the enemies' - it used to be 15, which made it twice as chunky
                // as everything around it. Collision follows the same factor (it was tuned at 15).
                float k = EnemyAssets.W;
                if (pc.visual != null)
                {
                    pc.visual.localScale = new Vector3(EnemyAssets.PlayerVisualScale, EnemyAssets.PlayerVisualScale, 1f);
                    EditorUtility.SetDirty(pc.visual);
                }
                pc.feetOffset = 1.25f * k;
                pc.bodyHeight = 2.4f * k;
                pc.feetHalfWidth = 0.4f * k;
                EditorUtility.SetDirty(pc);
            }
            float feetOffset = pc != null ? pc.feetOffset : 1.25f * EnemyAssets.W;
            player.transform.position = playerSpawns[0] + new Vector3(0f, feetOffset + 0.01f, 0f);

            // Plays as whichever mercenary was picked in the lobby (both cats are about
            // the same pixel size, so the same scale/collision values work for each).
            var loader = player.GetComponent<PlayerCharacterLoader>();
            if (loader == null) loader = player.AddComponent<PlayerCharacterLoader>();
            loader.characters = CharacterAssets.Load();
            EditorUtility.SetDirty(loader);
        }

        // Enemies
        var group = FindByName(scene, def.keepGroup);
        group.transform.position = Vector3.zero;
        PlaceEnemies(group, enemySpawns);

        // Different every run: monsters move to random reachable spots, some become elites.
        stageMap.spawnSlots = def.spawnSlots.ToArray();
        if (playerSpawns.Count > 0) stageMap.playerStart = playerSpawns[0];
        if (def.number < StageCount && !def.tutorial)
        {
            var randomizer = gridGo.AddComponent<StageRandomizer>();
            randomizer.enemyGroup = group;
            randomizer.eliteCount = def.number <= 2 ? 1 : 2;
        }

        // Tutorial: step-by-step prompts, goals matching the layout in Tutorial().
        if (def.tutorial)
        {
            var tutorialGo = new GameObject("Tutorial");
            var tc = tutorialGo.AddComponent<TutorialController>();
            tc.moveGoalX = TutorialMoveGoalX;
            tc.jumpGoalX = TutorialJumpGoalX;
            tc.wallGoalX = TutorialWallX;
            tc.wallTopY = TutorialWallTop;
            tc.dropGoalX = TutorialDropGoalX;
            tc.nextSceneName = "Stage1";
        }

        // Camera
        var camGo = FindByName(scene, "Main Camera");
        if (camGo != null)
        {
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = def.sky;
            cam.orthographicSize = StageCameraSize; // closer than the old 5: the cat is smaller now, keep it readable
            var follow = camGo.GetComponent<CameraFollow>();
            if (follow != null)
            {
                follow.yOffset = 0.9f;
                follow.deadZoneUp = 1.8f;
                follow.deadZoneDown = 1.2f;
                EditorUtility.SetDirty(follow);
            }
            if (player != null)
                camGo.transform.position = new Vector3(player.transform.position.x, player.transform.position.y, camGo.transform.position.z);
            EditorUtility.SetDirty(cam);

            var parallax = camGo.GetComponent<ParallaxBackground>();
            if (def.parallax)
            {
                if (parallax == null) parallax = camGo.AddComponent<ParallaxBackground>();
                var bgs = CuteTileset.LoadBackgrounds();
                var forest = LoadAnsimuzForest();
                parallax.layers = new[]
                {
                    new ParallaxBackground.Layer { sprite = def.skyVariant != null ? CuteTileset.LoadSky(def.skyVariant) : bgs[0], follow = 0.97f, sortingOrder = -100, tint = SkyTint(def, 0) }, // sky
                    new ParallaxBackground.Layer { sprite = forest[0] != null ? forest[0] : bgs[1], follow = 0.92f, sortingOrder = -99, tint = Alpha(SkyTint(def, 1), 0.7f) }, // distant trees
                    new ParallaxBackground.Layer { sprite = forest[1] != null ? forest[1] : bgs[2], follow = 0.82f, sortingOrder = -98, tint = Alpha(SkyTint(def, 2), 0.62f) }, // middle trees
                    new ParallaxBackground.Layer { sprite = forest[2], follow = 0.72f, sortingOrder = -97, tint = Alpha(SkyTint(def, 2), 0.5f) }, // light shafts
                    new ParallaxBackground.Layer { sprite = forest[3], follow = 0.64f, sortingOrder = -96, tint = Alpha(def.terrainTint, 0.55f) }, // close trees
                };
                EditorUtility.SetDirty(parallax);
            }
            else if (parallax != null)
            {
                Object.DestroyImmediate(parallax);
            }
        }

        // Atmosphere: pollen / leaves / dust / crimson spores / embers drifting through the view.
        if (def.ambient != null && def.ambient.Length > 0)
        {
            var ambientGo = new GameObject("Ambient");
            ambientGo.AddComponent<AmbientMotes>().layers = def.ambient;
        }

        // RunManager
        var rmGo = FindByName(scene, "RunManager");
        var rm = rmGo != null ? rmGo.GetComponent<RunManager>() : null;
        if (rm != null)
        {
            rm.stageNumber = def.number;
            rm.tutorial = def.tutorial;
            rm.nextSceneName = def.nextScene;
            rm.totalStages = StageCount;
            rm.stageGroups = new[] { group };
            EditorUtility.SetDirty(rm);
        }
        else
        {
            Debug.LogError($"[StageSceneBuilder] No RunManager found in Stage{def.number}.");
        }

        // Korean font for the HUD / card draft / result screen texts.
        KoreanFont.ApplyToScene(scene);

        AddHeartHUD(scene, player);

        EditorSceneManager.MarkSceneDirty(scene);
        UISkinApplier.ApplyAndSave(scene); // re-skins the UI (Kenney Pixel Adventure) and saves
    }

    const string HeartSpritePath = "Assets/HealthHeartSystem/Graphics/mini-heart.png";

    // HP as hearts (Simple Heart asset): 5 hearts to start, top-left of the HUD, with
    // the name/stage/gold text moved underneath.
    static void AddHeartHUD(Scene scene, GameObject player)
    {
        if (player != null)
        {
            var stats = player.GetComponent<PlayerStats>();
            if (stats != null)
            {
                stats.baseMaxHP = 5f;
                EditorUtility.SetDirty(stats);
            }
        }

        // The asset ships with mipmaps + compression, which smears an 8x7 pixel heart.
        var ti = (TextureImporter)AssetImporter.GetAtPath(HeartSpritePath);
        if (ti != null && (ti.mipmapEnabled || ti.textureCompression != TextureImporterCompression.Uncompressed || ti.filterMode != FilterMode.Point))
        {
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.filterMode = FilterMode.Point;
            ti.SaveAndReimport();
        }

        var sprites = AssetDatabase.LoadAllAssetsAtPath(HeartSpritePath);
        Sprite full = null, empty = null;
        foreach (var o in sprites)
        {
            if (o is Sprite s && s.name == "mini-heart_0") full = s;
            if (o is Sprite s2 && s2.name == "mini-heart_1") empty = s2;
        }
        if (full == null || empty == null)
        {
            Debug.LogError($"[StageSceneBuilder] Heart sprites not found in {HeartSpritePath}");
            return;
        }

        var hud = FindByName(scene, "HudText");
        if (hud == null) return;
        var canvas = hud.transform.parent;

        var hudRt = (RectTransform)hud.transform;
        hudRt.anchoredPosition = new Vector2(20f, -66f);

        var heartsGo = new GameObject("Hearts", typeof(RectTransform));
        heartsGo.transform.SetParent(canvas, false);
        var rt = (RectTransform)heartsGo.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(20f, -24f);
        rt.sizeDelta = new Vector2(400f, 40f);
        var hearts = heartsGo.AddComponent<HeartHUD>();
        hearts.fullHeart = full;
        hearts.emptyHeart = empty;
        heartsGo.transform.SetSiblingIndex(hud.transform.GetSiblingIndex());
    }

    static char At(StageDef def, int r, int c)
    {
        if (r < 0 || r >= def.layout.Length) return '.';
        string line = def.layout[r];
        return c < line.Length ? line[c] : '.';
    }

    // Same pick for the same cell every build, so variants don't reshuffle on rebuild.
    static string Variant(string[] names, int c, int r)
    {
        int h = (c * 73856093) ^ (r * 19349663);
        return names[(h & 0x7fffffff) % names.Length];
    }

    // Outside the map counts as solid on the left/right/bottom (like StageMap), open above.
    static bool SolidAt(StageDef def, int r, int c, int cols, int rows) =>
        c < 0 || c >= cols || r >= rows || At(def, r, c) == '#';

    // Auto-tile: edge / corner / underside tile from which neighbours are solid.
    static string SolidTile(StageDef def, int r, int c, int cols, int rows)
    {
        var t = def.terrain;
        bool up = SolidAt(def, r - 1, c, cols, rows), down = SolidAt(def, r + 1, c, cols, rows);
        bool left = SolidAt(def, r, c - 1, cols, rows), right = SolidAt(def, r, c + 1, cols, rows);
        if (!up) return !left && right ? t.topLeft : left && !right ? t.topRight : t.top;
        if (!down) return !left ? t.bottomLeft : !right ? t.bottomRight : t.bottom;
        if (!left) return t.left;
        if (!right) return t.right;
        if (!SolidAt(def, r - 1, c - 1, cols, rows)) return t.innerTopLeft;
        if (!SolidAt(def, r - 1, c + 1, cols, rows)) return t.innerTopRight;
        return Variant(t.center, c, r);
    }

    static string PlatformTile(StageDef def, int r, int c)
    {
        var t = def.terrain;
        bool left = At(def, r, c - 1) == '=', right = At(def, r, c + 1) == '=';
        return left && right ? t.platMid : right ? t.platLeft : left ? t.platRight : t.platSingle;
    }

    static Sprite[] LoadAnsimuzForest()
    {
        string[] names = { "parallax-forest-back-trees", "parallax-forest-middle-trees", "parallax-forest-lights", "parallax-forest-front-trees" };
        var layers = new Sprite[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            string path = $"{AnsimuzForestDir}/{names[i]}.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
                importer.filterMode != FilterMode.Point || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 16f;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            layers[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        return layers;
    }

    static Color Alpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    static Color SkyTint(StageDef def, int layer) => def.parallaxTints != null && layer < def.parallaxTints.Length ? def.parallaxTints[layer] : Color.white;

    static void PlaceAscentSigns(Transform parent)
    {
        var signSprite = Resources.Load<Sprite>("WorldProps/SunnyLand/sign");
        var arrowSprite = Resources.Load<Sprite>("KenneyRpgUI/arrowBeige_left");
        if (signSprite == null && arrowSprite == null) return;
        foreach (var position in new[]
                 {
                     new Vector2(29.5f, 44f),
                     new Vector2(29.5f, 55f),
                     new Vector2(29.5f, 64f),
                     new Vector2(18.5f, 68f),
                 })
        {
            var root = new GameObject("AscentSign");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            if (signSprite != null)
            {
                var sign = new GameObject("ForestSign");
                sign.transform.SetParent(root.transform, false);
                var signRenderer = sign.AddComponent<SpriteRenderer>();
                signRenderer.sprite = signSprite;
                signRenderer.sortingOrder = 2;
            }
            if (arrowSprite != null)
            {
                var arrow = new GameObject("UpArrow");
                arrow.transform.SetParent(root.transform, false);
                arrow.transform.localPosition = new Vector3(0f, 0.95f, 0f);
                arrow.transform.localScale = Vector3.one * 2.45f;
                arrow.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
                var arrowRenderer = arrow.AddComponent<SpriteRenderer>();
                arrowRenderer.sprite = arrowSprite;
                arrowRenderer.color = new Color(1f, 0.82f, 0.28f);
                arrowRenderer.sortingOrder = 4;
            }
        }
    }

    static void PlaceProp(Transform parent, Prop prop, Vector3 feet, Color tint)
    {
        var go = new GameObject(prop.sprite);
        go.transform.SetParent(parent, false);
        go.transform.position = feet + new Vector3(0f, -prop.sink, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CuteTileset.LoadProp(prop.sprite);
        sr.sortingOrder = prop.order;
        sr.color = tint;
    }

    static Tilemap MakeLayer(GameObject grid, string name, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(grid.transform, false);
        var tm = go.AddComponent<Tilemap>();
        var tr = go.AddComponent<TilemapRenderer>();
        tr.sortingOrder = order;
        return tm;
    }

    // Replaces the group's old placeholder enemies (Meowolas sprites from GameScene) with
    // animated ones built from EnemyDefinitions. Sprites have a bottom-center pivot, so
    // each enemy's position is exactly the marker's floor point.
    static void PlaceEnemies(GameObject group, List<(Vector3 feet, EnemyDefinition def)> spawns)
    {
        foreach (var old in group.GetComponentsInChildren<Enemy>(true))
            Object.DestroyImmediate(old.gameObject);

        var counts = new Dictionary<EnemyDefinition, int>();
        foreach (var (feet, def) in spawns)
        {
            counts.TryGetValue(def, out int n);
            counts[def] = ++n;

            var go = new GameObject($"{def.name}{n}");
            go.transform.SetParent(group.transform, false);
            go.transform.position = feet;
            go.transform.localScale = new Vector3(def.scale, def.scale, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = def.idleFrames[0];
            sr.sortingOrder = 1;

            var e = go.AddComponent<Enemy>();
            e.definition = def;
            e.isBoss = def.isBoss;
            e.hp = def.hp;
            e.goldReward = def.goldReward;
            e.contactDamage = def.damage;
            e.hitRadius = def.hitRadius;
            e.knockbackDistance = def.knockback;
        }
    }

    static GameObject FindByName(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        return null;
    }

    static void DestroyByName(Scene scene, string name)
    {
        var go = FindByName(scene, name);
        if (go != null) Object.DestroyImmediate(go);
    }

    // ---------------------------------------------------------------- title + build settings
    static void PointTitleAtLobby()
    {
        if (!File.Exists(TitleScene)) return;
        Scene scene = EditorSceneManager.OpenScene(TitleScene, OpenSceneMode.Single);
        bool changed = false;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var click in root.GetComponentsInChildren<ClickAnywhereToStart>(true))
            {
                if (click.gameSceneName == FirstSceneAfterTitle) continue;
                click.gameSceneName = FirstSceneAfterTitle;
                EditorUtility.SetDirty(click);
                changed = true;
            }
        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            UISkinApplier.ApplyAndSave(scene); // re-skins the UI (Kenney Pixel Adventure) and saves
        }
    }

    static void UpdateBuildSettings(StageDef[] stages)
    {
        var stagePaths = new HashSet<string>();
        foreach (var s in stages) stagePaths.Add(s.ScenePath);

        var result = new List<EditorBuildSettingsScene>();
        result.Add(new EditorBuildSettingsScene(TitleScene, true));
        if (File.Exists(MainScenePath)) result.Add(new EditorBuildSettingsScene(MainScenePath, true));
        if (File.Exists(WorldMapScenePath)) result.Add(new EditorBuildSettingsScene(WorldMapScenePath, true));
        if (File.Exists(LobbyScenePath)) result.Add(new EditorBuildSettingsScene(LobbyScenePath, true));
        if (File.Exists(LoadingScenePath)) result.Add(new EditorBuildSettingsScene(LoadingScenePath, true));
        foreach (var s in stages) result.Add(new EditorBuildSettingsScene(s.ScenePath, true));
        foreach (var existing in EditorBuildSettings.scenes)
            if (existing.path != TitleScene && existing.path != MainScenePath && existing.path != WorldMapScenePath && existing.path != LobbyScenePath && existing.path != LoadingScenePath && !stagePaths.Contains(existing.path))
                result.Add(existing);

        EditorBuildSettings.scenes = result.ToArray();
    }
}
