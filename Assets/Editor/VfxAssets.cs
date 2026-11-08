using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// Menu: Cats Smash > Apply Effects
//
// Slices the effect sheets in Assets/Effects (DryRain 32px strips, the 64px pixel-art strips in BDragon / Knight / Archer / Combo / Custom) into
// frames and builds Assets/Resources/VfxLibrary.asset (id -> frames, fps, scale) for Vfx.Play.
// Pivots: ground effects (dust) stand on the bottom of their drawing, bursts are centred on it, and
// Everything is point-filtered pixel art.
public static class VfxAssets
{
    const string Dir = "Assets/Effects";
    const string LibraryPath = "Assets/Resources/VfxLibrary.asset";

    enum Pivot { Ground, Center, Manual }

    // id, file, cell, grid columns (0 = one horizontal strip), frames (0 = from width), fps, pivot, manual pivot px
    // (from the top-left of the cell), world scale, sorting order
    struct Def
    {
        public string id, file;
        public int cell, cols, frames;
        public float fps, scale;
        public Pivot pivot;
        public Vector2 manual;
        public int order;
        public float ppu;          // 0 = 16 (the tile pixel size)
        public bool pixelSnap;     // scale snapped to x0.5 / x1 / x2 when played
        public bool freeRotation;  // with pixelSnap: any angle allowed (else 90 degree steps)
    }

    // Bump when the effect set changes, so an open project rebuilds the library by itself.
    const int LibraryVersion = 25;
    const float CatPixelsPerUnit = 12.5f; // the cats: 100 px/unit drawn at x8

    static Def BD(string id, float fps, Pivot pivot = Pivot.Center, float scale = 1f, int order = 7, bool turns = false,
                  string folder = "BDragon", int cell = 32, string file = null) => // BDragon sheets are 32 px cells (Tools/fx_to_32.ps1), drawn at x1: the cats' pixel
        new Def { id = id, file = $"{Dir}/{folder}/{file ?? id}.png", cell = cell, cols = 0, frames = 0, fps = fps, pivot = pivot, scale = scale,
                  order = order, ppu = CatPixelsPerUnit, pixelSnap = true, freeRotation = turns };

    static Def Strip(string id, float fps, Pivot pivot, float scale = 1f, int order = 7) =>
        new Def { id = id, file = $"{Dir}/DryRain/{id}.png", cell = 32, cols = 0, frames = 0, fps = fps, pivot = pivot, scale = scale, order = order };

    // Skill props drawn by generate_skill_sprites.ps1: 16px strips, centred (also used as plain sprites).
    static Def Custom(string id, float fps = 8f, int order = 8) =>
        new Def { id = id, file = $"{Dir}/Custom/{id}.png", cell = 16, cols = 0, frames = 0, fps = fps, pivot = Pivot.Center, scale = 1f, order = order };

    static readonly Def[] Defs =
    {
        // DryRain: dust stands on the ground, the rest bursts around its centre.
        Strip("dust_small", 14f, Pivot.Ground), Strip("dust_mid", 14f, Pivot.Ground),
        Strip("dust_big", 14f, Pivot.Ground),   Strip("dust_side", 16f, Pivot.Ground),
        Strip("pulse_small", 18f, Pivot.Center), Strip("pulse_mid", 18f, Pivot.Center), Strip("pulse_big", 16f, Pivot.Center),
        Strip("smoke_small", 12f, Pivot.Center), Strip("smoke_big", 12f, Pivot.Center),
        Strip("blast_small", 14f, Pivot.Center), Strip("blast_big", 14f, Pivot.Center),
        Strip("debris", 14f, Pivot.Center),
        Strip("spark_mid1", 20f, Pivot.Center), Strip("spark_mid2", 20f, Pivot.Center), Strip("spark_big", 20f, Pivot.Center),
        // BDragon1727 "Effect and FX Pixel" (free): hand-drawn 64px pixel art, one colour row per effect
        // (cropped into Assets/Effects/BDragon), imported at the cats' pixel size (12.5 px/unit: 1 px = 0.08
        // units). Drawn at x0.5 (crisp half-size pixels; at x1 they dwarf the 15 px cats) unless they cover a
        // big area; played at x0.5 / x1 / x2 and turned in 90 degree steps only (Vfx pixel snap), so pixels
        // stay whole. `turns` = keeps any angle (things that point along a flight).
        // The knight's 3-hit combo (Assets/Effects/Combo: one drawn slash per hit, flash then fade).
        // pimen "Battle VFX: Slashes and Thrusts" free sample (Smear VFX 01, 48 px, commercial use OK, credit appreciated):
        // the basic swing's arc (down / up) and thrust, white; gold with 날카로운 솜방망이.
        BD("smear_arc", 20f, scale: 1f, folder: "Pimen", cell: 48), BD("smear_arc_gold", 20f, scale: 1f, folder: "Pimen", cell: 48),
        BD("smear_thrust", 22f, scale: 1f, folder: "Pimen", cell: 48), BD("smear_thrust_gold", 22f, scale: 1f, folder: "Pimen", cell: 48),
        // pimen's free Thunder Spell Effect 01 / Wind Spell Effect 01 / Acid VFX 01 (32 px, commercial use OK, credit appreciated;
        // _art_source/pimen_free): the archer's electric arrow (a bolt pointing right) and its hit, the wind blast and puff, the poison splash.
        BD("p_thunder_bolt", 16f, scale: 1f, turns: true, folder: "Pimen", cell: 32), BD("p_thunder_hit", 20f, scale: 1f, folder: "Pimen", cell: 32),
        BD("p_wind_breath", 30f, scale: 1f, turns: true, folder: "Pimen", cell: 32), BD("p_wind_hit", 16f, scale: 1f, folder: "Pimen", cell: 32),
        BD("p_acid_hit", 16f, scale: 1f, folder: "Pimen", cell: 32),
        BD("combo_slash1", 16f, scale: 1f, folder: "Combo", cell: 32), BD("combo_slash2", 16f, scale: 1f, folder: "Combo", cell: 32), BD("combo_slash3", 16f, scale: 1f, folder: "Combo", cell: 32),
        // Archer skills (Assets/Effects/Archer: one drawn effect each, flash then fade; the fish / yarn / churu / can
        // cells are also the projectile sprites, the trap cell the trap on the floor).
        BD("a_jelly_fx", 16f, scale: 1f, folder: "Archer", cell: 64), BD("a_smile_fx", 12f, scale: 1f, folder: "Archer", cell: 64), // a_jelly_fx: Aether (기폭 젤리)
        BD("a_catnip_fx", 24f, scale: 1f, folder: "Archer", cell: 24), BD("a_meteor_fx", 12f, scale: 1f, folder: "Archer", cell: 64), // a_catnip_fx: Aether (캣닙), halved like lure; fps x1.5 (on screen a third shorter)
        BD("a_rapid_fx", 22f, scale: 1f, folder: "Archer", cell: 64), BD("a_backstep_fx", 22f, scale: 1f, folder: "Archer", cell: 32), // a_rapid_fx: Aether (연속 사격)
        BD("a_wire_fx", 14f, scale: 1f, folder: "Archer", cell: 48), BD("a_fish_fx", 16f, scale: 1f, folder: "Archer", cell: 48), // both Aether (방울 와이어, 생선가시), 9 frames (48: the long shapes needed it to read)
        // Sprite Fusion (generated on the owner's account): the fishbone arrow, 16 px at the cats' pixel size.
        BD("sf_fishbone", 8f, scale: 1f, folder: "SpriteFusion", cell: 16),
        BD("a_trap_fx", 16f, Pivot.Ground, scale: 1f, folder: "Archer", cell: 48), BD("a_churu_fx", 16f, scale: 1f, folder: "Archer", cell: 32), // a_trap_fx: Aether (쥐덫 끈끈이)
        BD("a_curve_fx", 16f, scale: 1f, folder: "Archer", cell: 32), BD("a_watch_fx", 12f, Pivot.Ground, scale: 1f, folder: "Archer", cell: 48), // a_watch_fx: Aether (캣타워 관측)
        BD("a_wind_fx", 16f, scale: 1f, turns: true, folder: "Archer", cell: 32), BD("a_homing_fx", 16f, scale: 1f, folder: "Archer", cell: 32),
        BD("a_can_fx", 16f, scale: 1f, folder: "Archer", cell: 64), // Aether (캔따개), 3 frames
        // Knight skills (Assets/Effects/Knight: one drawn effect each, flash then fade; the wheel ring spins).
        BD("k_highcut_fx", 24f, scale: 1f, folder: "Knight", cell: 32), BD("k_knead_fx", 16f, Pivot.Ground, scale: 1f, folder: "Knight", cell: 32),
        BD("k_tower_fx", 16f, Pivot.Ground, scale: 1f, folder: "Knight", cell: 64), BD("k_loaf_fx", 14f, scale: 1f, order: -1, folder: "Knight", cell: 32), // k_loaf_fx: Aether (식빵 자세); behind the cat (a buff: the cat stays readable)
        BD("k_groom_fx", 14f, scale: 1f, order: -1, folder: "Knight", cell: 32), // behind the cat (a buff)
        BD("k_ilseom_fx", 16f, scale: 1f, folder: "Knight", cell: 32),
        BD("k_rush_fx", 16f, scale: 1f, folder: "Knight", cell: 32), BD("k_leap_fx", 16f, scale: 1f, folder: "Knight", cell: 32),
        BD("k_wheel_fx", 20f, scale: 1f, folder: "Knight", cell: 64), BD("k_paw_fx", 22f, scale: 1f, folder: "Knight", cell: 32),
        BD("k_hiss_fx", 18f, scale: 1f, folder: "Knight", cell: 32), BD("k_night_fx", 12f, scale: 1f, folder: "Knight", cell: 32), // both Aether (하악질, 야간 시야); hiss halved like lure, fps x1.5
        BD("k_tail_fx", 16f, scale: 1f, folder: "Knight", cell: 64), BD("k_whisker_fx", 20f, scale: 1f, folder: "Knight", cell: 32),
        BD("slash", 30f),
        BD("slash_gold", 30f),
        BD("xcut", 24f),
        BD("heal", 16f, Pivot.Ground, scale: 1f, order: -1, folder: "RetroDiffusion", cell: 48, file: "rd_heal"), // behind the cat (a buff)
        BD("rupture", 26f, Pivot.Ground),
        BD("explosion", 20f, scale: 1f), // Aether (폭발)
        BD("parry", 26f),
        BD("blink", 22f, scale: 1f, folder: "Custom", cell: 32, file: "fx_blink"),  // pxl: the whisker counter's teleport poof
        // pxl: hit flashes (replace the soft pulse_* glows), the cat's hurt flash and two widening rings
        BD("hit_small", 24f, scale: 1f, folder: "Custom", cell: 32, file: "fx_hit_small"),
        BD("hit_mid", 24f, scale: 1f, folder: "Custom", cell: 32, file: "fx_hit_mid"),
        BD("hit_big", 24f, scale: 1f, folder: "Custom", cell: 32, file: "fx_hit_big"),
        BD("hurt", 22f, scale: 1f, folder: "Custom", cell: 32, file: "fx_hurt"),
        BD("ring_small", 22f, scale: 1f, folder: "Custom", cell: 32, file: "fx_ring_small"),
        BD("ring_big", 20f, scale: 1f, folder: "Custom", cell: 64, file: "fx_ring_big"),
        BD("charge", 30f),
        BD("poison_hit", 22f),
        BD("poison_loop", 14f, scale: 1f, folder: "Custom", cell: 24, file: "status_poison"), // generate_status_fx.ps1
        BD("zap", 22f, scale: 1f, folder: "Custom", cell: 32, file: "fx_zap"),      // pxl: electric hit spark
        BD("trap_snap", 24f, Pivot.Ground, cell: 48), // Aether (쥐덫)
        BD("lure", 16f, cell: 24), // Aether (츄르 유인), halved (Tools/fx_half.ps1): it was 3x the cat
        BD("wind", 22f, turns: true),
        BD("land_dust", 22f, Pivot.Ground),
        BD("tower_slam", 22f, Pivot.Ground, scale: 1f),
        BD("beam", 18f, scale: 1f, turns: true),
        BD("beam_end", 33f, cell: 24), // Aether (캔따개 명중), halved like lure; fps x1.5 (on screen a third shorter)
        BD("stun", 12f, scale: 1f, order: 8, folder: "Custom", cell: 24, file: "status_stun"), // generate_status_fx.ps1
        BD("hiss_wave", 26f, scale: 1f),
        BD("charm", 20f, scale: 1f, cell: 32), // Aether (츄르 착지)
        BD("aura_hit", 20f),
        BD("yarn_hit", 26f),
        BD("smoke", 22f),
        BD("sparkle", 22f),
        BD("watch", 20f, Pivot.Ground),
        Custom("heart"), Custom("exclaim"), Custom("fishbone"), Custom("yarn"), Custom("can"),
        Custom("churu"), Custom("trap"), Custom("bell"), Custom("glint", 10f, 9),
    };

    // Opening the project after new effect sheets were added (or on a fresh clone) builds the
    // library by itself, so a skill never plays an effect id the library doesn't have yet.
    [InitializeOnLoadMethod]
    static void EnsureOnLoad()
    {
        if (Application.isBatchMode) return;
        EditorApplication.delayCall += () =>
        {
            var lib = AssetDatabase.LoadAssetAtPath<VfxLibrary>(LibraryPath);
            var have = new HashSet<string>();
            if (lib != null && lib.entries != null)
                foreach (var e in lib.entries) if (e != null && e.frames != null && e.frames.Length > 0) have.Add(e.id);
            if (lib == null || lib.version != LibraryVersion) { Ensure(); return; }
            foreach (var def in Defs)
                if (!have.Contains(def.id)) { Ensure(); return; }
        };
    }

    [MenuItem("Cats Smash/Apply Effects")]
    public static void ApplyMenu() => Ensure();

    // Unity.exe -batchmode -quit -projectPath <project> -executeMethod VfxAssets.ApplyBatch
    public static void ApplyBatch()
    {
        try { Ensure(); }
        catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    public static void Ensure()
    {
        AssetDatabase.Refresh();
        foreach (var def in Defs) Slice(def);

        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        var lib = AssetDatabase.LoadAssetAtPath<VfxLibrary>(LibraryPath);
        if (lib == null)
        {
            lib = ScriptableObject.CreateInstance<VfxLibrary>();
            AssetDatabase.CreateAsset(lib, LibraryPath);
        }
        var entries = new List<VfxLibrary.Entry>();
        foreach (var def in Defs)
        {
            var frames = new List<Sprite>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(def.file))
                if (o is Sprite s) frames.Add(s);
            frames.Sort((a, b) => FrameIndex(a.name).CompareTo(FrameIndex(b.name)));
            if (frames.Count == 0) throw new System.Exception($"[VfxAssets] No frames for {def.id} ({def.file}).");
            entries.Add(new VfxLibrary.Entry { id = def.id, frames = frames.ToArray(), fps = def.fps, scale = def.scale, sortingOrder = def.order,
                                             pixelSnap = def.pixelSnap, freeRotation = def.freeRotation });
        }
        lib.entries = entries.ToArray();
        lib.version = LibraryVersion;
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        Debug.Log($"[VfxAssets] {entries.Count} effects ready.");
    }

    static int FrameIndex(string name)
    {
        int us = name.LastIndexOf('_');
        return us >= 0 && int.TryParse(name.Substring(us + 1), out int i) ? i : 0;
    }

    static void Slice(Def def)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(def.file);
        if (ti == null) throw new System.Exception($"[VfxAssets] {def.file} not found.");
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.spritePixelsPerUnit = def.ppu > 0f ? def.ppu : 16f;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.isReadable = true; // pivots are measured from the pixels
        ti.SaveAndReimport();

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(def.file);
        int frames = def.frames > 0 ? def.frames : tex.width / def.cell;
        var rects = new List<Rect>();
        for (int i = 0; i < frames; i++)
        {
            int col = def.cols == 0 ? i : i % def.cols, row = def.cols == 0 ? 0 : i / def.cols;
            rects.Add(new Rect(col * def.cell, tex.height - (row + 1) * def.cell, def.cell, def.cell));
        }

        Vector2 pivot = def.pivot == Pivot.Manual
            ? new Vector2(def.manual.x / def.cell, 1f - def.manual.y / def.cell)
            : MeasurePivot(tex, rects, def);

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var dp = factory.GetSpriteEditorDataProviderFromObject(ti);
        dp.InitSpriteEditorDataProvider();
        var existing = new Dictionary<string, SpriteRect>();
        foreach (var sr in dp.GetSpriteRects()) existing[sr.name] = sr;

        var list = new List<SpriteRect>();
        for (int i = 0; i < rects.Count; i++)
        {
            string name = $"{def.id}_{i}";
            if (!existing.TryGetValue(name, out var sr)) sr = new SpriteRect { name = name, spriteID = GUID.Generate() };
            sr.rect = rects[i];
            sr.alignment = SpriteAlignment.Custom;
            sr.pivot = pivot;
            sr.border = Vector4.zero;
            list.Add(sr);
        }
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

    // The union of everything drawn in any frame: ground effects stand on its bottom edge, bursts are
    // centred on it. Normalised to the cell (y up), as sprite pivots are.
    static Vector2 MeasurePivot(Texture2D tex, List<Rect> rects, Def def)
    {
        var px = tex.GetPixels32();
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        foreach (var r in rects)
            for (int y = 0; y < def.cell; y++)
                for (int x = 0; x < def.cell; x++)
                {
                    if (px[((int)r.y + y) * tex.width + (int)r.x + x].a < 16) continue;
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
        if (maxX < 0) return new Vector2(0.5f, 0.5f);
        float cx = (minX + maxX + 1) * 0.5f / def.cell;
        float cy = def.pivot == Pivot.Ground ? minY / (float)def.cell : (minY + maxY + 1) * 0.5f / def.cell;
        return new Vector2(cx, cy);
    }
}
