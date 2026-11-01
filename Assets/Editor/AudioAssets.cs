using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menu: Cats Smash > Apply Audio
//
// - Sound effects: every clip in Assets/Audio/Sfx is imported small (decompress on load,
//   mono) and listed in Assets/Resources/SfxLibrary.asset by id (file name minus "_N").
// - Music: each scene gets its looping "Music" object (SceneMusic) per the table below.
// - Every UI Button gets a ButtonSound (click).
// UISkinApplier.ApplyAndSave calls ApplyToScene, so scenes rebuilt by the builders keep it.
//
// Sources (all CC0): Kenney RPG Audio / Impact Sounds / Interface Sounds / Music Jingles;
// OpenGameArt - "A sailor's chant" (Thimras), "GrassLands Theme" (DST), "Dark Shrine Loop"
// (qubodup, remix of yd), "Heavy Boss Battle 1" (MintoDog). Title/lobby tracks were already in.
public static class AudioAssets
{
    const string SfxDir = "Assets/Audio/Sfx";
    const string LibraryPath = "Assets/Resources/SfxLibrary.asset";

    const string TitleMusic = "Assets/music/alex-morgan-fantasy-adventure-quest-537478.mp3";
    const string VillageMusic = "Assets/music/thimras-a-sailors-chant-loop.ogg";
    const string ForestMusic = "Assets/music/dst-grasslands-theme.mp3";
    const string TempleMusic = "Assets/music/qubodup-dark-shrine-loop.ogg";
    const string BossMusic = "Assets/music/emma-ma-determined-pursuit-loop.wav"; // CC0, orchestral (was a metal track that clashed with the cute concept)

    // Scenes that get music here (Title / Lobby already have theirs from LobbySceneBuilder).
    static readonly Dictionary<string, (string clip, float volume)> SceneMusicTable = new Dictionary<string, (string, float)>
    {
        { "MainScene", (VillageMusic, 0.5f) },
        { "WorldMapScene", (TitleMusic, 0.5f) },
        { "Tutorial", (ForestMusic, 0.4f) },
        { "Stage1", (ForestMusic, 0.45f) },
        { "Stage2", (ForestMusic, 0.45f) },
        { "Stage3", (TempleMusic, 0.55f) },
        { "Stage4", (TempleMusic, 0.55f) },
        { "Stage5", (BossMusic, 0.4f) },
    };

    // Per-sound loudness (the Kenney files differ a lot); anything not listed plays at 0.7.
    static readonly Dictionary<string, float> Volumes = new Dictionary<string, float>
    {
        { "swing", 0.55f }, { "arrow", 0.5f }, { "jump", 0.45f }, { "dash", 0.5f },
        { "hit", 0.7f }, { "hit_strong", 0.85f }, { "enemy_death", 0.7f }, { "golem_death", 0.8f },
        { "player_hurt", 0.75f }, { "land_heavy", 0.8f }, { "slam", 0.85f }, { "boss_roar", 0.7f },
        { "coin", 0.45f }, { "step_grass", 0.25f }, { "step_stone", 0.25f },
        { "ui_click", 0.6f }, { "ui_open", 0.6f }, { "ui_close", 0.6f }, { "ui_error", 0.6f }, { "ui_buy", 0.7f },
        { "card_pick", 0.7f }, { "stage_clear", 0.7f }, { "victory", 0.8f }, { "defeat", 0.8f },
    };

    // Base pitch per sound: Kenney's foley is realistic (punches, plates, cloth); raising it a little
    // makes hits and pops lighter, which suits cute pixel combat better.
    static readonly Dictionary<string, float> Pitches = new Dictionary<string, float>
    {
        { "hit", 1.22f }, { "hit_strong", 1.1f }, { "enemy_death", 1.3f }, { "player_hurt", 1.15f },
        { "swing", 1.1f }, { "jump", 1.15f }, { "dash", 1.1f }, { "coin", 1.1f },
        { "golem_death", 0.95f }, { "land_heavy", 1.0f },
    };

    static readonly string[] Scenes =
    {
        "Assets/Scenes/TitleScene.unity", "Assets/Scenes/LobbyScene.unity", "Assets/Scenes/LoadingScene.unity",
        "Assets/Scenes/MainScene.unity", "Assets/Scenes/WorldMapScene.unity",
        "Assets/Scenes/Stage1.unity", "Assets/Scenes/Stage2.unity", "Assets/Scenes/Stage3.unity",
        "Assets/Scenes/Stage4.unity", "Assets/Scenes/Stage5.unity", "Assets/Scenes/Tutorial.unity",
    };

    [MenuItem("Cats Smash/Apply Audio")]
    public static void ApplyMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string current = SceneManager.GetActiveScene().path;
        ApplyAll();
        if (!string.IsNullOrEmpty(current)) EditorSceneManager.OpenScene(current, OpenSceneMode.Single);
    }

    // Unity.exe -batchmode -quit -projectPath <project> -executeMethod AudioAssets.ApplyBatch
    public static void ApplyBatch()
    {
        try { ApplyAll(); }
        catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static void ApplyAll()
    {
        Ensure();
        foreach (var path in Scenes)
        {
            if (!File.Exists(path)) continue;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            ApplyToScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        Debug.Log("[AudioAssets] Sound effects + music applied.");
    }

    // ---------------------------------------------------------------- library
    public static void Ensure()
    {
        AssetDatabase.Refresh();
        var groups = new SortedDictionary<string, List<AudioClip>>();
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { SfxDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var s = importer.defaultSampleSettings;
            if (s.loadType != AudioClipLoadType.DecompressOnLoad || !importer.forceToMono)
            {
                s.loadType = AudioClipLoadType.DecompressOnLoad; // short and played often
                importer.defaultSampleSettings = s;
                importer.forceToMono = true;
                importer.SaveAndReimport();
            }
            string name = Path.GetFileNameWithoutExtension(path);
            int us = name.LastIndexOf('_');
            string id = us > 0 && int.TryParse(name.Substring(us + 1), out _) ? name.Substring(0, us) : name;
            if (!groups.TryGetValue(id, out var list)) groups[id] = list = new List<AudioClip>();
            list.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        var lib = AssetDatabase.LoadAssetAtPath<SfxLibrary>(LibraryPath);
        if (lib == null)
        {
            lib = ScriptableObject.CreateInstance<SfxLibrary>();
            AssetDatabase.CreateAsset(lib, LibraryPath);
        }
        var entries = new List<SfxLibrary.Entry>();
        foreach (var kv in groups)
        {
            bool music = kv.Key == "victory" || kv.Key == "defeat" || kv.Key == "stage_clear";
            entries.Add(new SfxLibrary.Entry
            {
                id = kv.Key,
                clips = kv.Value.ToArray(),
                volume = Volumes.TryGetValue(kv.Key, out float v) ? v : 0.7f,
                pitch = Pitches.TryGetValue(kv.Key, out float pitch) ? pitch : 1f,
                pitchJitter = music ? 0f : 0.06f, // jingles stay in tune
            });
        }
        lib.entries = entries.ToArray();
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
    }

    // ---------------------------------------------------------------- scenes
    public static void ApplyToScene(Scene scene)
    {
        if (SceneMusicTable.TryGetValue(scene.name, out var m)) SetMusic(scene, m.clip, m.volume);

        foreach (var root in scene.GetRootGameObjects())
            foreach (var button in root.GetComponentsInChildren<Button>(true))
                if (button.GetComponent<ButtonSound>() == null)
                    button.gameObject.AddComponent<ButtonSound>();
    }

    // Same "Music" object LobbySceneBuilder makes for the title / lobby.
    static void SetMusic(Scene scene, string clipPath, float volume)
    {
        var importer = (AudioImporter)AssetImporter.GetAtPath(clipPath);
        if (importer == null) throw new System.Exception($"[AudioAssets] {clipPath} not found");
        var settings = importer.defaultSampleSettings;
        // A raw WAV is huge (the boss loop is 19 MB): ship it as Vorbis. Other formats are left alone.
        bool wav = clipPath.EndsWith(".wav", System.StringComparison.OrdinalIgnoreCase);
        if (wav && (settings.compressionFormat != AudioCompressionFormat.Vorbis || Mathf.Abs(settings.quality - 0.6f) > 0.01f))
        {
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            settings = importer.defaultSampleSettings;
        }
        if (settings.loadType != AudioClipLoadType.Streaming)
        {
            settings.loadType = AudioClipLoadType.Streaming; // long BGM: stream it
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        foreach (var root in scene.GetRootGameObjects())
            if (root.name == "Music") Object.DestroyImmediate(root);
        var go = new GameObject("Music");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<AudioSource>();
        var music = go.AddComponent<SceneMusic>();
        music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
        music.volume = volume;
    }
}
