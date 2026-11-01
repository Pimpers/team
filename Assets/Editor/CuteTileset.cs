using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

// Imports the "Free Cute Tileset" pack (Assets/FreeCuteTilesetv1) for the stage builder:
//   - Tileset.png (8x6 cells of 16px) is sliced into sprites cute_<col>_<row> (row 0 = top)
//     and a Tile asset is made for each in Assets/Tiles/Tiles (same folder the builder loads).
//   - The pack has no 1-tile-thick ledge, so the one-way platform pieces are composited from
//     the grass top row + the rounded bottom row: Assets/Tiles/Sprites/cute_plat_grass_{l,m,r}.png
//   - Decors.png -> sprites cute_tree / cute_bush / cute_rock (bottom-centre pivot, placed as props)
//   - BG1-3.png (sky / mountains / hills) -> parallax layers.
// Called from StageSceneBuilder.Build before PrepareTiles.
public static class CuteTileset
{
    public const string Dir = "Assets/FreeCuteTilesetv1/FreeCuteTileset";
    public const string TilesetPath = Dir + "/Tileset.png";
    public const string DecorsPath = Dir + "/Decors.png";
    public static readonly string[] BackgroundPaths = { Dir + "/BG1.png", Dir + "/BG2.png", Dir + "/BG3.png" };

    const string SpriteDir = "Assets/Tiles/Sprites";
    const string TileDir = "Assets/Tiles/Tiles";
    const string PropDir = "Assets/Tiles/Props";
    const string SkyDir = "Assets/Tiles/Backgrounds";
    const int Cell = 16, Cols = 8, Rows = 6;

    // Decors.png prop rects in pixels, measured from the TOP-left (x, y, w, h).
    static readonly (string name, int x, int y, int w, int h)[] Props =
    {
        ("cute_tree", 17, 12, 73, 100),
        ("cute_bush", 114, 96, 35, 16),
        ("cute_rock", 177, 92, 27, 20),
    };

    public static void Ensure()
    {
        if (!File.Exists(TilesetPath))
            throw new System.Exception($"[CuteTileset] {TilesetPath} not found.");

        // Tileset: readable so the platform pieces can be composited from it.
        var tileNames = new List<(string name, Rect rect)>();
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
                tileNames.Add(($"cute_{c}_{r}", new Rect(c * Cell, (Rows - 1 - r) * Cell, Cell, Cell)));
        ImportSheet(TilesetPath, tileNames, new Vector2(0.5f, 0.5f), readable: true);

        var propRects = new List<(string, Rect)>();
        int decorHeight = 112;
        foreach (var p in Props) propRects.Add((p.name, new Rect(p.x, decorHeight - p.y - p.h, p.w, p.h)));
        ImportSheet(DecorsPath, propRects, new Vector2(0.5f, 0f), readable: true); // readable: the crimson variants are recoloured from it

        // BG1 (the sky) is readable: the sunset / dusk / crimson skies are recoloured from it.
        for (int i = 0; i < BackgroundPaths.Length; i++) ImportSingle(BackgroundPaths[i], TextureWrapMode.Repeat, readable: i == 0);

        MakeTiles();
        MakePlatformPieces();
        MakeTempleWall();
        MakeCorruptProps();
        MakeSkyVariants();
    }

    public static Sprite LoadSprite(string path, string name)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is Sprite s && s.name == name) return s;
        throw new System.Exception($"[CuteTileset] Sprite '{name}' not found in {path}.");
    }

    public static Sprite LoadProp(string name)
    {
        if (name.StartsWith("corrupt_")) return AssetDatabase.LoadAssetAtPath<Sprite>($"{PropDir}/{name}.png");
        return LoadSprite(DecorsPath, name);
    }

    public static Sprite[] LoadBackgrounds()
    {
        var result = new Sprite[BackgroundPaths.Length];
        for (int i = 0; i < result.Length; i++)
            result[i] = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPaths[i]);
        return result;
    }

    // ---------------------------------------------------------------- importing
    static void SetPixelArt(TextureImporter ti, SpriteImportMode mode, bool readable)
    {
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = mode;
        ti.spritePixelsPerUnit = Cell;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.isReadable = readable;
    }

    static void ImportSingle(string path, TextureWrapMode wrap, bool readable = false)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti.textureType == TextureImporterType.Sprite && ti.spriteImportMode == SpriteImportMode.Single &&
            ti.isReadable == readable &&
            ti.spritePixelsPerUnit == Cell && ti.filterMode == FilterMode.Point && ti.wrapMode == wrap &&
            ti.textureCompression == TextureImporterCompression.Uncompressed && !ti.mipmapEnabled)
            return;
        SetPixelArt(ti, SpriteImportMode.Single, readable);
        ti.wrapMode = wrap;
        ti.SaveAndReimport();
    }

    // Slices a sheet into named rects. Existing sprite IDs are kept by name so the Tile
    // assets / scenes that reference them don't break when this runs again.
    static void ImportSheet(string path, List<(string name, Rect rect)> rects, Vector2 pivot, bool readable)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        SetPixelArt(ti, SpriteImportMode.Multiple, readable);
        ti.SaveAndReimport();

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var dp = factory.GetSpriteEditorDataProviderFromObject(ti);
        dp.InitSpriteEditorDataProvider();

        var existing = new Dictionary<string, SpriteRect>();
        foreach (var sr in dp.GetSpriteRects()) existing[sr.name] = sr;

        bool same = existing.Count == rects.Count;
        var list = new List<SpriteRect>();
        foreach (var (name, rect) in rects)
        {
            if (!existing.TryGetValue(name, out var sr))
            {
                sr = new SpriteRect { name = name, spriteID = GUID.Generate() };
                same = false;
            }
            if (sr.rect != rect || sr.pivot != pivot || sr.alignment != SpriteAlignment.Custom) same = false;
            sr.rect = rect;
            sr.alignment = SpriteAlignment.Custom;
            sr.pivot = pivot;
            sr.border = Vector4.zero;
            list.Add(sr);
        }
        if (same) return;

        dp.SetSpriteRects(list.ToArray());
        var ids = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (ids != null)
        {
            var pairs = new List<SpriteNameFileIdPair>();
            foreach (var sr in list) pairs.Add(new SpriteNameFileIdPair(sr.name, sr.spriteID));
            ids.SetNameFileIdPairs(pairs);
        }
        dp.Apply();
        ti.SaveAndReimport();
    }

    static void MakeTiles()
    {
        if (!AssetDatabase.IsValidFolder(TileDir)) AssetDatabase.CreateFolder("Assets/Tiles", "Tiles");
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(TilesetPath))
        {
            if (!(o is Sprite sprite)) continue;
            string tilePath = $"{TileDir}/{sprite.name}.asset";
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

    // The tileset's dark cobble wall is near-black, so the dark spiders and golems vanished against it.
    // These are lit copies (a "screen" blend with lavender) used for the temple and altar walls,
    // written next to the other generated tiles so PrepareTiles gives them Tile assets.
    static readonly (string name, int col, int row, Color lift)[] WallTiles =
    {
        ("temple_wall_0", 1, 4, new Color(0.32f, 0.28f, 0.42f)),
        ("temple_wall_1", 3, 4, new Color(0.32f, 0.28f, 0.42f)),
        ("temple_wall_2", 4, 4, new Color(0.32f, 0.28f, 0.42f)),
        ("temple_wall_3", 3, 5, new Color(0.32f, 0.28f, 0.42f)),
        ("temple_pillar", 6, 4, new Color(0.10f, 0.10f, 0.18f)),
    };

    static void MakeTempleWall()
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TilesetPath);
        bool wrote = false;
        foreach (var (name, col, row, lift) in WallTiles)
        {
            var src = tex.GetPixels(col * Cell, (Rows - 1 - row) * Cell, Cell, Cell);
            for (int i = 0; i < src.Length; i++)
            {
                var c = src[i];
                if (c.a < 0.01f) continue;
                src[i] = new Color(1f - (1f - c.r) * (1f - lift.r), 1f - (1f - c.g) * (1f - lift.g), 1f - (1f - c.b) * (1f - lift.b), c.a);
            }
            wrote |= WritePng($"{SpriteDir}/{name}.png", src, Cell, Cell);
        }
        if (wrote) AssetDatabase.Refresh();
    }

    // Crimson copies of the tree / bush / rock (the corruption seeping into the forest, and the
    // temple overgrowth): greens are hue-shifted to crimson, the blue sky-rim to pink, the grey
    // rock reddened. Bottom-centre pivot like the originals; kept in their own folder.
    static void MakeCorruptProps()
    {
        if (!AssetDatabase.IsValidFolder(PropDir)) AssetDatabase.CreateFolder("Assets/Tiles", "Props");
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(DecorsPath);
        const int decorHeight = 112;
        foreach (var p in Props)
        {
            var px = tex.GetPixels(p.x, decorHeight - p.y - p.h, p.w, p.h);
            bool rock = p.name == "cute_rock";
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (c.a < 0.01f) continue;
                Color.RGBToHSV(c, out float h, out float s, out float v);
                if (rock) { h = 0.98f; s = Mathf.Clamp01(s * 1.2f + 0.4f); v = Mathf.Min(1f, v * 1.0f + 0.05f); }
                else if (h > 0.17f && h < 0.46f) { h = 0.985f; s = Mathf.Min(1f, s * 0.95f); v *= 0.95f; }   // greens -> crimson
                else if (h > 0.5f && h < 0.68f && s > 0.4f) { h = 0.93f; }                                     // blue rim -> pink
                else continue;
                var n = Color.HSVToRGB(h, s, v); n.a = c.a; px[i] = n;
            }
            string path = $"{PropDir}/{p.name.Replace("cute_", "corrupt_")}.png";
            if (WritePng(path, px, p.w, p.h)) AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            if (ti.textureType != TextureImporterType.Sprite || settings.spriteAlignment != (int)SpriteAlignment.BottomCenter || ti.spritePixelsPerUnit != Cell || ti.filterMode != FilterMode.Point)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = Cell;
                ti.filterMode = FilterMode.Point;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                ti.SetTextureSettings(settings);
                ti.SaveAndReimport();
            }
        }
    }

    // Multiplying a tint onto the blue sky can only darken it (a "sunset" turned murky grey-violet),
    // so the three time-of-day skies are gradient-mapped from BG1 instead: its brightness picks a
    // colour from a 3-stop ramp, keeping the clouds and the pixel structure.
    static readonly (string name, Color[] ramp)[] SkyVariants =
    {
        ("sky_sunset",  new[] { new Color(0.34f, 0.22f, 0.55f), new Color(0.95f, 0.50f, 0.45f), new Color(1.00f, 0.86f, 0.66f) }),
        ("sky_dusk",    new[] { new Color(0.14f, 0.10f, 0.34f), new Color(0.40f, 0.30f, 0.62f), new Color(0.80f, 0.68f, 0.92f) }),
        ("sky_crimson", new[] { new Color(0.16f, 0.04f, 0.12f), new Color(0.55f, 0.12f, 0.24f), new Color(0.95f, 0.42f, 0.42f) }),
    };

    static void MakeSkyVariants()
    {
        if (!AssetDatabase.IsValidFolder(SkyDir)) AssetDatabase.CreateFolder("Assets/Tiles", "Backgrounds");
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPaths[0]);
        foreach (var (name, ramp) in SkyVariants)
        {
            var px = tex.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                float t = Mathf.Clamp01((px[i].grayscale - 0.44f) / 0.48f);
                var c = t < 0.5f ? Color.Lerp(ramp[0], ramp[1], t * 2f) : Color.Lerp(ramp[1], ramp[2], (t - 0.5f) * 2f);
                c.a = px[i].a;
                px[i] = c;
            }
            string path = $"{SkyDir}/{name}.png";
            if (WritePng(path, px, tex.width, tex.height)) AssetDatabase.ImportAsset(path);
            ImportSingle(path, TextureWrapMode.Repeat);
        }
    }

    public static Sprite LoadSky(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{SkyDir}/{name}.png");

    // Writes the pixels as a PNG unless the file already has exactly these bytes.
    static bool WritePng(string path, Color[] pixels, int w, int h)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.SetPixels(pixels);
        t.Apply();
        byte[] png = t.EncodeToPNG();
        Object.DestroyImmediate(t);
        if (File.Exists(path) && System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes(path), png)) return false;
        File.WriteAllBytes(path, png);
        return true;
    }

    // One-way platform pieces: top half of the grass top tiles (0..2, 0) over the bottom
    // half of the rounded underside tiles (0..2, 2). Written as plain PNGs into the
    // builder's sprite folder, so PrepareTiles imports them and makes their Tiles.
    static void MakePlatformPieces()
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TilesetPath);
        string[] suffix = { "l", "m", "r" };
        bool wrote = false;
        for (int c = 0; c < 3; c++)
        {
            string path = $"{SpriteDir}/cute_plat_grass_{suffix[c]}.png";
            var top = tex.GetPixels(c * Cell, (Rows - 1 - 0) * Cell, Cell, Cell);
            var bottom = tex.GetPixels(c * Cell, (Rows - 1 - 2) * Cell, Cell, Cell);
            var px = new Color[Cell * Cell];
            for (int y = 0; y < Cell; y++)
                for (int x = 0; x < Cell; x++)
                    px[y * Cell + x] = y >= Cell / 2 ? top[y * Cell + x] : bottom[y * Cell + x];

            var outTex = new Texture2D(Cell, Cell, TextureFormat.RGBA32, false);
            outTex.SetPixels(px);
            outTex.Apply();
            byte[] png = outTex.EncodeToPNG();
            Object.DestroyImmediate(outTex);

            if (File.Exists(path) && System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes(path), png)) continue;
            File.WriteAllBytes(path, png);
            wrote = true;
        }
        if (wrote) AssetDatabase.Refresh();
    }
}
