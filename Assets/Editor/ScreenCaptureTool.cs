using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Menu: Cats Smash > Capture Screens   (batch: -executeMethod ScreenCaptureTool.RunBatch)
//
// Plays through the main screens and saves a PNG of each to <project>/Screenshots/ (git-ignored), so a layout
// change can be checked by eye on every screen in a minute: title, village (+ blacksmith, settings), world map,
// lobby, the in-run HUD, pause, card draft, result. Each scene is shown at 16:9 (Width x Height) whatever the editor
// window is (a batch run's screen is 640 x 480): the main camera renders into a texture of that size and the overlay
// canvases are drawn through it, so they lay themselves out for that size - for a few frames before the action and
// the shot, so code that fits the UI to the canvas (Update) has run too. (The canvases are lifted above every world
// sprite's sorting order for the shot, as an overlay is in the game.)
public static class ScreenCaptureTool
{
    struct Step { public string scene, shot; public Action act; public int w, h; public float wait; } // w/h 0: the default 16:9; wait 0: 3 s after the action
    static readonly List<Step> steps = new List<Step>();
    static int index;
    static double at;
    static bool loaded, batch, savedOptionsEnabled;
    static EnterPlayModeOptions savedOptions;
    static string outDir;
    const int DefaultWidth = 1920, DefaultHeight = 1080;
    static int Width = DefaultWidth, Height = DefaultHeight; // this shot's size (a step may ask for 4:3 or 16:10)
    static bool prepared;
    static RenderTexture target;
    static Camera targetCam;
    static readonly List<(Canvas canvas, RenderMode mode, int order)> converted = new List<(Canvas, RenderMode, int)>();

    [MenuItem("Cats Smash/Capture Screens")]
    public static void Run() => Start(false);

    // Batch: the shots use a copy of the save (the blacksmith visit, the easy tier...) and never write the player's
    // real save file - a copy of the project in a temp folder still shares it (same company / product name).
    public static void RunBatch()
    {
        GameSave.ResetForTests(GameSave.Parse(JsonUtility.ToJson(GameSave.Data)));
        Start(true);
    }

    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);

    static void Start(bool isBatch)
    {
        if (EditorApplication.isPlaying) return;
        if (!isBatch && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        batch = isBatch;
        outDir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Screenshots");
        Directory.CreateDirectory(outDir);
        steps.Clear();
        string[] cards = { "attack", "k_combo", "k_paw", "crit" };
        Add("TitleScene", "title");
        Add("TitleScene", "title_16x10", null, 1680, 1050); // the logo against the buttons and the prompt on narrower screens
        Add("TitleScene", "title_4x3", null, 1440, 1080);
        Add("MainScene", "village");
        Add("MainScene", "blacksmith_talk", () => Find<BlacksmithUI>().Open());
        Add("MainScene", "blacksmith", () => Find<BlacksmithUI>().OpenShop());
        Add("MainScene", "options", () => Find<OptionsUI>().Open());
        Add("MainScene", "keybinds", () => { Find<OptionsUI>().Open(); KeyBindPanel.Show(Find<OptionsUI>().panel.GetComponentInParent<Canvas>(), Find<OptionsUI>().volumeValue.font); });
        Add("TitleScene", "title_settings", () => TitleSettings.Show(Find<Canvas>().rootCanvas, Find<TMPro.TMP_Text>().font));
        Add("WorldMapScene", "worldmap");
        Add("WorldMapScene", "worldmap_easy", () => { Corruption.Level = Corruption.Easy; Find<CorruptionSelector>().Refresh(); });
        Add("Tutorial", "tutorial", () => Find<TutorialController>().JumpToStepForCapture(7));
        Add("LobbyScene", "lobby");
        Add("Stage1", "legend"); // the first stage shows the minimap legend for its first seconds
        Add("Stage2", "tip_skills", () => { GameSave.Data.seenTips = ""; OneTimeTip.ShowInRun("skills"); }); // a one-time tip in a run
        Add("Stage2", "hud", () => { foreach (var id in cards) PlayerStats.Instance.ApplyCard(id); });
        Add("Stage2", "pause", () => { foreach (var id in cards) PlayerStats.Instance.ApplyCard(id); Find<PauseMenu>().OpenForPlaytest(); });
        Add("Stage2", "draft", () => Find<CardDraftUI>().Show(() => { }));
        // The first draft (set tip shown) with a card under the selection, so its preview line shows.
        Add("Stage2", "draft_first", () =>
        {
            GameSave.Data.seenSetTip = false;
            var draft = Find<CardDraftUI>();
            draft.Show(() => { });
            var card = draft.card2Label.GetComponentInParent<UnityEngine.UI.Button>();
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(card.gameObject);
        });
        Add("Stage2", "buildsheet", () => { foreach (var id in cards) PlayerStats.Instance.ApplyCard(id); BuildSheet.ForceShow = true; });
        Add("Stage2", "result", () =>
        {
            foreach (var id in cards) PlayerStats.Instance.ApplyCard(id);
            // (The set banners the cards just raised would still be up: in a real run the result comes long after.)
            foreach (var t in UnityEngine.Object.FindObjectsByType<RectTransform>()) if (t != null && t.name == "StageBanner") UnityEngine.Object.Destroy(t.gameObject);
            Find<ResultUI>().Show(false, 12, -1); // a lost run: no tier unlocked (a loss never unlocks one)
        });
        // 큰 글씨 + 색약 모드 (last: the settings stay on): the stage reloaded with both, the Tab sheet held open.
        Add("Stage2", "result_win", () => { foreach (var id in cards) PlayerStats.Instance.ApplyCard(id); Find<ResultUI>().Show(true, 48, 1); });
        Add("WorldMapScene", "worldmap_corruption", () => { GameSave.Data.corruptionUnlocked = 2; Corruption.Level = 1; Find<CorruptionSelector>().Refresh(); });
        // Skill effects at their middle frame beside the cat, in their in-game size: the Aether sheets, then the
        // older Knight / Pimen ones to judge their palette against.
        Add("Stage2", "fx_aether1", () => FxRow("a_trap_fx", "a_wire_fx", "a_fish_fx", "k_loaf_fx", "lure", "k_night_fx", "k_hiss_fx", "a_catnip_fx"));
        Add("Stage2", "fx_aether2", () => FxRow("charm", "a_jelly_fx", "trap_snap", "a_watch_fx", "a_can_fx", "beam_end", "a_rapid_fx", "explosion"));
        Add("Stage2", "fx_compare", () => FxRow("k_tower_fx", "a_smile_fx", "a_meteor_fx", "a_backstep_fx", "a_churu_fx", "a_curve_fx", "k_hiss_fx", "a_catnip_fx"));
        // The HUD, the draft and the village at the narrower screens too (16:10, 4:3): anchors, bands, plates.
        Add("Stage2", "hud_16x10", () => { foreach (var id in cards) PlayerStats.Instance.ApplyCard(id); }, 1680, 1050);
        Add("Stage2", "hud_4x3", () => { foreach (var id in cards) PlayerStats.Instance.ApplyCard(id); }, 1440, 1080);
        Add("Stage2", "hud_21x9", () => { foreach (var id in cards) PlayerStats.Instance.ApplyCard(id); }, 2560, 1080);
        Add("Stage2", "draft_16x10", () => Find<CardDraftUI>().Show(() => { }), 1680, 1050);
        Add("Stage2", "draft_4x3", () => Find<CardDraftUI>().Show(() => { }), 1440, 1080);
        Add("MainScene", "village_16x10", null, 1680, 1050);
        Add("MainScene", "village_4x3", null, 1440, 1080);
        // The recoloured art in a real fight: the cat beside the bear boss (Stage5), beside a mushroom (Stage4 mixes them
        // in), and the catnip cloud going off in front of it.
        Add("Stage5", "fight_bear", () => NextTo(e => e.isBoss));
        Add("Stage4", "fight_mushroom", MushroomBeside); // forced: the line-up mix is random
        Add("Stage2", "fight_catnip", () => Vfx.Play("a_catnip_fx", PlayerStats.Instance.transform.position + Vector3.right * 1.8f, 1f), 0, 0, 0.15f); // 9 frames at 24 fps: the cloud is at its fullest about now
        // The result board with a long build (20 cards: past two rows the strip ends in "+N").
        Add("Stage2", "result_many", () => { int n = 0; foreach (var c in CardPool.All) { if (n >= 20) break; if (c.job == PlayerStats.Instance.Job || c.job == CardPool.Job.Any) { PlayerStats.Instance.ApplyCard(c.id); n++; } } Find<ResultUI>().Show(true, 48, -1); });
        // The set banner while it is up (0.7 s after the cards that finish 발톱: fully in, before it fades), 16:9 and 4:3.
        Add("Stage2", "set_banner", () => { foreach (var id in cards) PlayerStats.Instance.ApplyCard(id); }, 0, 0, 0.7f);
        Add("Stage2", "set_banner_4x3", () => { foreach (var id in cards) PlayerStats.Instance.ApplyCard(id); }, 1440, 1080, 0.7f);
        // The way out open, so the minimap's exit marker (cyan) shows.
        Add("Stage2", "minimap_exit", () => { var e = StageExit.Instance != null ? StageExit.Instance : StageExit.Create(2); if (e != null) e.Open("Stage3"); });
        // The exit log: shut while monsters are left, open after the card, the cat pulled in, the iris shut, out again.
        Add("Stage1", "exit_closed", () => CatBeforeExit(1.5f)); // close enough that the whole log is in the frame
        Add("Stage1", "exit_open", () => { ClearEnemies(); StageExit.Instance.Open("Stage2"); CatBeforeExit(1.5f); }, wait: 0.6f);
        Add("Stage1", "exit_pull", () => { ClearEnemies(); StageExit.Instance.Open("Stage2"); StageExit.Instance.ForCapture(true); }, wait: 0.4f);
        // Every stage: where its log lands once open (headroom above it, not jammed under a ledge).
        for (int n = 1; n <= 5; n++)
            Add("Stage" + n, "exit_stage" + n, () => { ClearEnemies(); StageExit.Instance.Open("Stage1"); CatBeforeExit(1.5f); });
        // The fork: the hard way (red, skull) and the easy way (green, sprout), shut, open, and each one taking the cat.
        Add("Stage1", "fork_closed", () => CatBeforeExit(4f));
        Add("Stage1", "fork_open", () => { ClearEnemies(); StageExit.Instance.OpenFork("Stage2", _ => { }); CatBeforeExit(4f); }, wait: 1f);
        Add("Stage1", "fork_pull_hunt", () => { ClearEnemies(); StageExit.Instance.OpenFork("Stage2", _ => { }); StageExit.Instance.ForCapture(true, true); }, wait: 0.4f);
        Add("Stage1", "fork_pull_safe", () => { ClearEnemies(); StageExit.Instance.OpenFork("Stage2", _ => { }); StageExit.Instance.ForCapture(true, false); }, wait: 0.4f);
        Add("Stage5", "portal_victory_closed", () => CatBeforeExit(3f)); // the boss room's gold portal, shut while the boss stands
        Add("Stage4", "portal_boss_open", () => { ClearEnemies(); StageExit.Instance.Open("Stage5"); CatBeforeExit(-3f); }, wait: 1.5f); // the cat beside it on its floor: the whole arch in view
        // the boss beaten for real: the gold portal opens on the win (and the boss's own icons are gone with it)
        Add("Stage5", "portal_victory_open", () =>
        {
            foreach (var e in Enemy.All.ToArray()) if (e != null && e.isBoss) e.ReceiveDamage(999999, strong: true);
            EditorApplication.CallbackFunction near = null;
            near = () => { if (StageExit.Instance != null && StageExit.Instance.IsOpen) { EditorApplication.update -= near; CatBeforeExit(-3f); } };
            EditorApplication.update += near;
        }, wait: 7f);
        Add("Stage1", "fork_camera", () => { ClearEnemies(); StageExit.Instance.OpenFork("Stage2", _ => { }); }, wait: 0.7f); // mid-way: the camera framing both
        Add("Stage1", "exit_iris", () => StageWipe.HoldShutForCapture());
        Add("Stage1", "exit_popout", () => { StageWipe.ArrivedThroughExit = true; SceneManager.LoadScene("Stage2"); }, wait: 0.45f);
        Add("MainScene", "village_return"); // back from a run, the goals board reading the new tier
        Add("Stage1", "a11y", () =>
        {
            GameSave.Data.largeText = true; GameSave.Data.colorblind = true; Loc.Reload();
            BuildSheet.ForceShow = true;
            SceneManager.LoadScene("Stage1");
        });
        Add("MainScene", "a11y_smith", () => Find<BlacksmithUI>().Open()); // the blacksmith's bubble with 큰 글씨 still on
        Add("MainScene", "a11y_smith_16x10", () => Find<BlacksmithUI>().Open(), 1680, 1050); // narrower screens: the bubble wraps sooner
        Add("MainScene", "a11y_smith_4x3", () => Find<BlacksmithUI>().Open(), 1440, 1080);
        // 큰 글씨 + 색약 still on: the closed log's message, and the exit's edge arrow in the colour-blind colour.
        Add("Stage1", "a11y_fork", () => { BuildSheet.ForceShow = false; ClearEnemies(); StageExit.Instance.OpenFork("Stage2", _ => { }); CatBeforeExit(4f); }, wait: 1f);
        Add("Stage1", "a11y_exit_closed", () => { BuildSheet.ForceShow = false; CatBeforeExit(0.3f); }, wait: 0.35f); // (the word lasts 0.7 s)
        Add("Stage1", "a11y_exit_arrow", () => { BuildSheet.ForceShow = false; ClearEnemies(); StageExit.Instance.Open("Stage2"); CatBeforeExit(-12f); }, wait: 4f); // the cat past the log, the log off screen: its arrow shows

        savedOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        savedOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true; // keep this class's state across entering play mode
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
        index = -1; loaded = false; at = 0;
        EditorSceneManager.OpenScene("Assets/Scenes/TitleScene.unity");
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    // Still sprites (not Vfx.Play: a one-shot is gone before the shot) in two rows of four around the player.
    static void FxRow(params string[] ids)
    {
        var player = PlayerStats.Instance.transform;
        var sr = player.GetComponentInChildren<SpriteRenderer>();
        for (int i = 0; i < ids.Length; i++)
        {
            var frames = Vfx.Frames(ids[i]);
            if (frames == null) { Debug.LogWarning($"[ScreenCapture] no effect '{ids[i]}'"); continue; }
            var go = new GameObject("FxShot_" + ids[i]);
            go.transform.position = player.position + new Vector3((i % 4 - 1.5f) * 2.2f, i < 4 ? 2.2f : 0f, 0f);
            go.transform.localScale = Vector3.one * Vfx.Scale(ids[i]);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = frames[frames.Length / 2];
            if (sr != null) { r.sortingLayerID = sr.sortingLayerID; r.sortingOrder = 5000; } // over the foreground decor too
        }
    }

    // The enemy nearest the player becomes a mushroom (as StageRandomizer's mix does) two units to its right.
    static void MushroomBeside()
    {
        var roster = EnemyRoster.Load();
        EnemyDefinition mush = null;
        if (roster != null && roster.others != null) foreach (var d in roster.others) if (d != null && d.name.Contains("Mushroom")) mush = d;
        var p = PlayerStats.Instance.transform.position;
        Enemy near = null; float best = float.MaxValue;
        foreach (var e in Enemy.All) if (e != null && !e.IsDead && !e.isBoss) { float d = (e.transform.position - p).sqrMagnitude; if (d < best) { best = d; near = e; } }
        if (mush == null || near == null) { Debug.LogWarning("[ScreenCapture] no mushroom to show"); return; }
        near.definition = mush;
        near.name = mush.name;
        near.transform.position = p + Vector3.right * 2f;
        // the swap comes after its Awake (the line-up mix comes before it): run Awake again for the new size and stats,
        // then restart its idle animation, whose frames were read from the old definition
        near.SendMessage("Awake", SendMessageOptions.DontRequireReceiver);
        var state = typeof(Enemy).GetNestedType("State", System.Reflection.BindingFlags.NonPublic);
        typeof(Enemy).GetMethod("Play", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(near, new[] { System.Enum.ToObject(state, 0) });
    }

    // Puts the player two units left of the first enemy that matches (the camera follows the player).
    static void NextTo(Func<Enemy, bool> match)
    {
        foreach (var e in Enemy.All)
            if (e != null && !e.IsDead && match(e)) { PlayerStats.Instance.transform.position = e.transform.position + Vector3.left * 2f; return; }
        Debug.LogWarning("[ScreenCapture] no enemy to stand by");
    }

    // Puts the cat on the exit log's floor, `gap` units before its mouth.
    // The game opens the exit only once every monster is gone (RunManager.StageCleared): the shots that open it
    // by hand clear them first, so the picture matches play.
    static void ClearEnemies()
    {
        foreach (var e in Enemy.All.ToArray()) if (e != null) UnityEngine.Object.Destroy(e.gameObject);
    }

    static void CatBeforeExit(float gap)
    {
        var body = PlayerStats.Instance.Body;
        var exit = StageExit.Instance.ExitPoint;
        var feet = body.Feet;
        body.transform.position += new Vector3(exit.x - gap - feet.x, exit.y - 0.5f - feet.y, 0f);
    }

    static void Add(string scene, string shot, Action act = null, int w = 0, int h = 0, float wait = 0f) => steps.Add(new Step { scene = scene, shot = shot, act = act, w = w, h = h, wait = wait });

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        double now = EditorApplication.timeSinceStartup;
        if (now < at) return;
        try
        {
            if (index >= 0 && loaded)
            {
                var s = steps[index];
                if (!prepared) { Width = s.w > 0 ? s.w : DefaultWidth; Height = s.h > 0 ? s.h : DefaultHeight; Prepare(); prepared = true; at = now + 0.6; return; }
                if (s.act != null) { s.act(); steps[index] = new Step { scene = s.scene, shot = s.shot, w = s.w, h = s.h }; at = now + (s.wait > 0f ? s.wait : 3.0); return; } // 3 s: past a set banner the cards may raise (or a step's own wait, to catch one)
                // An action that loaded another scene took the camera with it: set up on the new one first.
                if (targetCam == null) { Release(); Prepare(); at = now + 0.6; return; }
                Shot(s.shot);
                Release();
                BuildSheet.ForceShow = false;
                loaded = false;
            }
        }
        catch (Exception e) { Debug.LogError($"[ScreenCapture] {steps[index].shot}: {e}"); Release(); loaded = false; }
        index++;
        if (index >= steps.Count) { Finish(); return; }
        GameClock.ResetAll();
        SceneManager.LoadScene(steps[index].scene);
        loaded = true;
        prepared = false;
        at = now + 2.5;
    }

    static void Finish()
    {
        GameSave.Data.largeText = false; GameSave.Data.colorblind = false; // (the a11y shots switched them on in the copy)
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged += Restore;
        EditorApplication.isPlaying = false;
        Debug.Log($"[ScreenCapture] {steps.Count} screens saved to {outDir}");
    }

    static void Restore(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= Restore;
        EditorSettings.enterPlayModeOptionsEnabled = savedOptionsEnabled;
        EditorSettings.enterPlayModeOptions = savedOptions;
        if (!batch) return;
        EditorApplication.Exit(0);
        // Unity 6000.5 can hang in its own shutdown after a batch run (seen on every run here): don't let a CI job
        // wait forever on it once the work is saved.
        new System.Threading.Thread(() => { System.Threading.Thread.Sleep(15000); System.Diagnostics.Process.GetCurrentProcess().Kill(); }) { IsBackground = true }.Start();
    }

    // The main camera draws into a Width x Height texture from now on, and the overlay canvases through it.
    static void Prepare()
    {
        targetCam = Camera.main;
        if (targetCam == null) return;
        target = new RenderTexture(Width, Height, 24);
        targetCam.targetTexture = target;
        ConvertCanvases();
    }

    // Overlay canvases (also ones opened since Prepare) onto the camera, in their sorting order.
    static void ConvertCanvases()
    {
        foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>())
        {
            if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            converted.Add((c, c.renderMode, c.sortingOrder));
            c.renderMode = RenderMode.ScreenSpaceCamera;
            c.worldCamera = targetCam;
            c.planeDistance = 1f + (32000 - c.sortingOrder) * 0.0001f;
            // Over every world sprite, as an overlay is in the game (a camera canvas sorts with the sprites).
            c.sortingOrder = Mathf.Min(32767, 30000 + c.sortingOrder); // over world sprites and world-space text alike
        }
    }

    static void Shot(string name)
    {
        if (targetCam == null || target == null) return;
        ConvertCanvases();
        Canvas.ForceUpdateCanvases();
        targetCam.Render();
        RenderTexture.active = target;
        var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        tex.Apply();
        File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
        RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(tex);
    }

    // Back to the screen (canvases kept across scenes, like the fader, must not stay on a camera that is unloaded).
    static void Release()
    {
        foreach (var (c, mode, order) in converted)
            if (c != null) { c.renderMode = mode; c.sortingOrder = order; if (mode == RenderMode.ScreenSpaceOverlay) c.worldCamera = null; }
        converted.Clear();
        if (targetCam != null) targetCam.targetTexture = null;
        if (target != null) UnityEngine.Object.DestroyImmediate(target);
        target = null; targetCam = null;
    }
}
