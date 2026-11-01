using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Creates the EnemyDefinition assets (Assets/Enemies/Rat, Spider, Slime, Mushroom, Bear, Dummy .asset) and keeps
// their animation frames current: the frames generate_enemy_sprites.ps1 / generate_slime_sprites.ps1 /
// generate_mushroom_sprites.ps1 / generate_bear_sprites.ps1 draw are imported as pixel-art sprites with a
// bottom-center pivot (feet = transform position). Called by StageSceneBuilder before it places enemies.
//
// The .asset files are the source of truth for each enemy's numbers (hp, speed, ranges, sounds...): tune them in
// the inspector. The numbers below are only the defaults an asset gets when it is first created, or when
// "Cats Smash/Reset Enemy Stats To Defaults" is chosen (that backs the old values up first).
public static class EnemyAssets
{
    // Characters are drawn at one shared pixel size (the cat at scale 8 = 0.08 world units per
    // pixel, tiles 0.0625): every enemy is shrunk by the same factor as the player was
    // (15 -> 8) so proportions and combat reach stay as designed.
    public const float W = PlayerVisualScale / 15f;
    public const float PlayerVisualScale = 8f;

    const string Root = "Assets/Enemies";
    public const string RatPath = Root + "/Rat.asset";
    public const string SpiderPath = Root + "/Spider.asset";
    public const string BearPath = Root + "/Bear.asset";
    const string RockSpritePath = "Assets/Tiles/Sprites/forest_rock.png";
    public const string SlimePath = Root + "/Slime.asset";
    public const string MushroomPath = Root + "/Mushroom.asset";
    public const string DummyPath = Root + "/Dummy.asset";

    // Bump when the frames change: the editor re-runs Ensure() once on its next load, so the definitions the
    // stage scenes point at pick up the new frames without rebuilding the scenes. (It never touches the numbers.)
    const int AssetsVersion = 8;
    const string RosterPath = "Assets/Resources/EnemyRoster.asset";
    const string VersionFile = "Library/CatsSmash_EnemyAssetsVersion.txt";

    [InitializeOnLoadMethod]
    static void AutoUpdate()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            string have = System.IO.File.Exists(VersionFile) ? System.IO.File.ReadAllText(VersionFile).Trim() : "";
            if (have == AssetsVersion.ToString()) return;
            Ensure();
            System.IO.File.WriteAllText(VersionFile, AssetsVersion.ToString());
        };
    }

    [MenuItem("Cats Smash/Update Enemy Frames")]
    static void UpdateFromMenu() => Ensure();

    [MenuItem("Cats Smash/Reset Enemy Stats To Defaults")]
    static void ResetFromMenu()
    {
        if (!EditorUtility.DisplayDialog("Reset enemy stats",
                "Overwrite every enemy's numbers with the defaults in EnemyAssets.cs? Inspector edits are lost " +
                $"(a copy of the current values goes to {BackupDir}/).", "Reset", "Cancel")) return;
        ResetWithBackup();
    }

    const string BackupDir = "Library/CatsSmash_EnemyBackup";
    static readonly string[] AllPaths = { RatPath, SpiderPath, SlimePath, MushroomPath, DummyPath, BearPath };

    static void ResetWithBackup()
    {
        var before = new Dictionary<string, string>();
        System.IO.Directory.CreateDirectory(BackupDir);
        foreach (var path in AllPaths)
        {
            var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (def == null) continue;
            string json = EditorJsonUtility.ToJson(def, true);
            before[path] = json;
            System.IO.File.WriteAllText(System.IO.Path.Combine(BackupDir, def.name + ".json"), json);
        }
        Ensure(resetStats: true);
        var changed = new List<string>();
        foreach (var path in AllPaths)
        {
            var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (def == null) continue;
            if (!before.TryGetValue(path, out var old) || old != EditorJsonUtility.ToJson(def, true)) changed.Add(def.name);
        }
        if (changed.Count > 0)
            Debug.LogWarning($"[EnemyAssets] Reset to the defaults in EnemyAssets.cs: {string.Join(", ", changed)}. " +
                             $"The previous values are in {BackupDir}/.");
        else Debug.Log("[EnemyAssets] Enemy stats already match the defaults.");
    }

    // resetStats: also overwrite the numbers of assets that already exist (otherwise only new assets get them).
    public static void Ensure(bool resetStats = false)
    {
        AssetDatabase.Refresh();
        ImportFrames();

        var rat = LoadOrCreate(RatPath, out bool ratNew);
        Fill(rat, "Rat/rat");
        if (ratNew || resetStats)
        {
            rat.displayName = "흰쥐";
            rat.isBoss = false;
            rat.idleFps = 4f; rat.moveFps = 10f; rat.attackFps = 10f; rat.deathFps = 4f;
            rat.walkStridePixels = 5f;       // the gallop covers ~5 px a frame (walk driven by distance: no sliding feet)
            rat.attackHitFrame = 2;          // crouch, lunge, BITE, back down (sprite sheet, generate_rat_spider_sprites.ps1)
            rat.hp = 8; rat.goldReward = 1; rat.damage = 0.5f;
            rat.moveSpeed = 2.8f;            // quick, but a little slower than the cat (3) so it can be outrun
            rat.scale = 1.7f * W; rat.hitRadius = 1.3f * W; rat.bodyHalfWidth = 0.9f * W;   // 32x32 cells, ~25 px long: about the old rat's size
            rat.aggroRange = 7f; rat.attackRange = 1.7f * W; rat.attackCooldown = 1.2f; // longer than the player's 1 s hurt-invulnerability, so it can't chain bites
            rat.knockback = 0.7f; rat.patrolRange = 4f; rat.superArmor = false;
            rat.lungeDistance = 1.6f * W; rat.alertTime = 3f;   // darts in to bite
            rat.deathEffect = "smoke_big"; rat.deathEffectScale = 0.5f; // small puffs: a full-size cloud hid the cat standing next to it
        }
        EditorUtility.SetDirty(rat);

        var spider = LoadOrCreate(SpiderPath, out bool spiderNew);
        Fill(spider, "Spider/spider");
        if (spiderNew || resetStats)
        {
            spider.displayName = "마족거미";
            spider.isBoss = false;
            spider.idleFps = 4f; spider.moveFps = 8f; spider.attackFps = 7f; spider.deathFps = 4f;
            spider.walkStridePixels = 2.5f;  // short scuttling steps
            spider.attackHitFrame = 2;       // crouch, rear up, SPIT web, web trails off, recover
            spider.ranged = true;            // spits a ball of web (EnemyWeb): a hit slows the cat for a moment
            spider.keepAway = 3f;            // backs off while it reloads if the cat gets close
            spider.hp = 16; spider.goldReward = 2; spider.damage = 0.5f;
            spider.moveSpeed = 1.6f;         // slower than a rat, but not sluggish
            spider.scale = 1.8f * W; spider.hitRadius = 1.7f * W; spider.bodyHalfWidth = 1.2f * W;
            spider.aggroRange = 8f; spider.attackRange = 6f; spider.attackCooldown = 2.2f; // shoots from up to 6 units, a spit every ~2.5 s
            spider.knockback = 1.0f; spider.patrolRange = 3f; spider.superArmor = false;
            spider.lungeDistance = 0f; spider.alertTime = 2.5f;
            spider.deathEffect = "smoke_big"; spider.deathEffectScale = 0.65f;
        }
        EditorUtility.SetDirty(spider);

        // Temple-only (stages 3-4; took the stone guardian's place): a green slime that hops along and
        // pounces - crouches, stretches up and comes down on the cat with a splat. 32x32 frames from
        // generate_slime_sprites.ps1; its pixel is the same size as the other enemies' (x1.5 like the spider).
        var slime = LoadOrCreate(SlimePath, out bool slimeNew);
        Fill(slime, "Slime/slime");
        if (slimeNew || resetStats)
        {
            slime.displayName = "슬라임";
            slime.isBoss = false;
            slime.idleFps = 3f; slime.moveFps = 8f; slime.attackFps = 7f; slime.deathFps = 5f;
            slime.walkStridePixels = 5f;     // one hop (4 frames) a body length
            slime.attackHitFrame = 3;        // crouch, squash, stretch up, SPLAT
            slime.hp = 20; slime.goldReward = 2; slime.damage = 0.5f;
            slime.moveSpeed = 2.0f;          // hops along at a fair pace
            slime.scale = 1.5f * W; slime.hitRadius = 1.4f * W; slime.bodyHalfWidth = 0.9f * W;   // ~24 px wide body
            slime.aggroRange = 7f; slime.attackRange = 2.1f * W; slime.attackCooldown = 1.5f;
            slime.knockback = 1.0f; slime.patrolRange = 3f; slime.superArmor = false;
            slime.lungeDistance = 2.0f * W; slime.alertTime = 2.5f;   // the pounce carries it forward
            slime.attackSound = "land_heavy"; slime.deathSound = "enemy_death";
            slime.attackEffect = "poison_hit"; slime.attackEffectScale = 0.7f; // green splash where it lands
            slime.deathEffect = "poison_hit"; slime.deathEffectScale = 1.0f;
        }
        EditorUtility.SetDirty(slime);

        // Forest (stages 1-2): a little mushroom with a satchel and a kitchen knife - walks up, draws the
        // knife back, raises it and slashes (the arc is drawn in the frame). 44x44 frames from
        // generate_mushroom_sprites.ps1, shown about the cat's height.
        var mushroom = LoadOrCreate(MushroomPath, out bool mushroomNew);
        Fill(mushroom, "Mushroom/mushroom");
        if (mushroomNew || resetStats)
        {
            mushroom.displayName = "버섯 친구";
            mushroom.isBoss = false;
            mushroom.idleFps = 4f; mushroom.moveFps = 8f; mushroom.attackFps = 8f; mushroom.deathFps = 4f;
            mushroom.walkStridePixels = 3.5f; // little feet, quick steps (at 8 fps it slid ~6 px a frame)
            mushroom.attackHitFrame = 2;     // draw back, raise, SLASH, recover
            mushroom.hp = 12; mushroom.goldReward = 1; mushroom.damage = 0.5f;
            mushroom.moveSpeed = 2.2f;
            mushroom.scale = 1.3f * W; mushroom.hitRadius = 1.3f * W; mushroom.bodyHalfWidth = 0.8f * W;
            mushroom.aggroRange = 7f; mushroom.attackRange = 2.0f * W; mushroom.attackCooldown = 1.2f;
            mushroom.knockback = 0.8f; mushroom.patrolRange = 3f; mushroom.superArmor = false;
            mushroom.lungeDistance = 0.6f * W; mushroom.alertTime = 2.5f;
            mushroom.attackSound = "swing"; mushroom.deathSound = "enemy_death";
            mushroom.deathEffect = "smoke_big"; mushroom.deathEffectScale = 0.55f;
        }
        EditorUtility.SetDirty(mushroom);

        // Tutorial training dummy: stands still, never attacks, no gold, no knockback.
        var dummy = LoadOrCreate(DummyPath, out bool dummyNew);
        Fill(dummy, "Dummy/dummy");
        if (dummyNew || resetStats)
        {
            dummy.displayName = "허수아비";
            dummy.isBoss = false;
            dummy.idleFps = 1.5f; dummy.moveFps = 1f; dummy.attackFps = 1f; dummy.deathFps = 4f;
            dummy.hp = 12; dummy.goldReward = 0; dummy.damage = 0f;
            dummy.moveSpeed = 0f; dummy.scale = 1.4f * W; dummy.hitRadius = 1.4f * W; dummy.bodyHalfWidth = 0.8f * W;
            dummy.aggroRange = 0f; dummy.attackRange = 0f; dummy.attackCooldown = 99f;
            dummy.knockback = 0f; dummy.patrolRange = 0f; dummy.superArmor = true;
            dummy.lungeDistance = 0f; dummy.alertTime = 0f;
            dummy.attackSound = ""; dummy.deathSound = "enemy_death";
            dummy.deathEffect = "smoke_big"; dummy.deathEffectScale = 0.6f;
        }
        EditorUtility.SetDirty(dummy);

        // The last boss (Stage5). Frames cut from the horned bear sheet (_art_source/bear_new_sheet, 52x40 cells, drawn
        // facing right): walk 4, swipe 4 (head down, rear up with a paw raised, paw thrown forward, recover), idle 4,
        // death 4 (the first, a stagger, doubles as the hurt pose). ~37 px long, so it is drawn at 2.6 - about the
        // cat's pixel size - to stay as big as the old bear: a heavy swipe that reaches further, slower on its feet.
        // Its specials (BossBrain): a four-legged charge, then from phase 2 rearing up to pound the floor -
        // shockwaves plus rocks falling from the ceiling.
        var bear = LoadOrCreate(BearPath, out bool bearNew);
        Fill(bear, "Bear/bear");
        bear.fallingRock = AssetDatabase.LoadAssetAtPath<Sprite>(RockSpritePath);
        if (bearNew || resetStats)
        {
            bear.displayName = "뿔곰";        // horns and a bell collar (the new sheet) - no longer the moon bear
            bear.bossTitle = "타락한 숲의 지배자";
            bear.isBoss = true;
            bear.idleFps = 4f; bear.moveFps = 4.5f; bear.attackFps = 6f; bear.deathFps = 4f;
            bear.walkStridePixels = 5f;       // a front paw steps back ~5 px a frame: the walk keeps pace with the ground covered (charge too)
            bear.attackHitFrame = 2;          // head down (0), rears up, paw raised (1), the swipe lands as the paw comes forward (2)
            bear.hp = 210; bear.goldReward = 15; bear.damage = 1f; // 270 dragged: three phases of the same moves at 210 are a fight, not a chore
            bear.moveSpeed = 1.9f;            // slow and heavy
            bear.scale = 2.6f * W; bear.hitRadius = 3.2f * W; bear.bodyHalfWidth = 2.6f * W;   // the drawn body is ~1.5 world units each side of centre: a narrower box let the cat stand inside the bear unhurt
            bear.aggroRange = 20f; bear.attackRange = 3.8f * W; bear.attackCooldown = 1.3f;
            bear.knockback = 1.8f; bear.patrolRange = 3f; bear.superArmor = true;
            bear.lungeDistance = 1.0f * W; bear.alertTime = 99f;
            bear.attackSound = "hit_strong"; bear.deathSound = "golem_death";
            bear.attackEffect = "dust_mid"; bear.attackEffectScale = 1.2f; // the paw scrapes the floor
            bear.deathEffect = "explosion|blast_big"; bear.deathEffectScale = 1.4f;
        }
        EditorUtility.SetDirty(bear);

        // The line-up StageRandomizer mixes each run (rats into the other kinds).
        var roster = AssetDatabase.LoadAssetAtPath<EnemyRoster>(RosterPath);
        if (roster == null) { roster = ScriptableObject.CreateInstance<EnemyRoster>(); AssetDatabase.CreateAsset(roster, RosterPath); }
        roster.rat = rat;
        roster.others = new[] { spider, slime, mushroom };
        EditorUtility.SetDirty(roster);

        AssetDatabase.SaveAssets();
    }

    // Reload after any OpenScene(Single) - opening a scene unloads assets held in memory.
    public static EnemyDefinition Load(string path) => AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);

    static void ImportFrames()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            bool ok = ti.textureType == TextureImporterType.Sprite && ti.spriteImportMode == SpriteImportMode.Single &&
                      ti.spritePixelsPerUnit == 16f && ti.filterMode == FilterMode.Point &&
                      ti.textureCompression == TextureImporterCompression.Uncompressed && !ti.mipmapEnabled &&
                      s.spriteAlignment == (int)SpriteAlignment.BottomCenter;
            if (ok) continue;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 16f;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.ReadTextureSettings(s);
            s.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            ti.SetTextureSettings(s);
            ti.SaveAndReimport();
        }
    }

    static void Fill(EnemyDefinition def, string prefix)
    {
        def.idleFrames = Frames(prefix + "_idle");
        def.moveFrames = Frames(prefix + "_move");
        def.attackFrames = Frames(prefix + "_attack");
        def.hurtFrames = Frames(prefix + "_hurt");
        def.deathFrames = Frames(prefix + "_death");
    }

    // prefix_0.png, prefix_1.png, ... until one is missing.
    static Sprite[] Frames(string prefix)
    {
        var list = new List<Sprite>();
        for (int i = 0; ; i++)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/{prefix}_{i}.png");
            if (s == null) break;
            list.Add(s);
        }
        if (list.Count == 0) throw new System.Exception($"No frames found for {Root}/{prefix}_0.png (run generate_enemy_sprites.ps1)");
        return list.ToArray();
    }

    static EnemyDefinition LoadOrCreate(string path, out bool created)
    {
        var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
        created = def == null;
        if (def != null) return def;
        def = ScriptableObject.CreateInstance<EnemyDefinition>();
        AssetDatabase.CreateAsset(def, path);
        return def;
    }
}
