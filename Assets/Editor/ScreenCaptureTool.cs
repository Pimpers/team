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
    struct Step { public string scene, shot; public Action act; }
    static readonly List<Step> steps = new List<Step>();
    static int index;
    static double at;
    static bool loaded, batch, savedOptionsEnabled;
    static EnterPlayModeOptions savedOptions;
    static string outDir;
    const int Width = 1920, Height = 1080;
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
            Find<ResultUI>().Show(false, 12, 1);
        });
        // 큰 글씨 + 색약 모드 (last: the settings stay on): the stage reloaded with both, the Tab sheet held open.
        Add("Stage1", "a11y", () =>
        {
            GameSave.Data.largeText = true; GameSave.Data.colorblind = true; Loc.Reload();
            BuildSheet.ForceShow = true;
            SceneManager.LoadScene("Stage1");
        });

        savedOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        savedOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true; // keep this class's state across entering play mode
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
        index = -1; loaded = false; at = 0;
        EditorSceneManager.OpenScene("Assets/Scenes/TitleScene.unity");
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    static void Add(string scene, string shot, Action act = null) => steps.Add(new Step { scene = scene, shot = shot, act = act });

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
                if (!prepared) { Prepare(); prepared = true; at = now + 0.6; return; }
                if (s.act != null) { s.act(); steps[index] = new Step { scene = s.scene, shot = s.shot }; at = now + 3.0; return; } // past a set banner (fade in, 1.45 s, fade out) the cards may raise
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
            c.sortingOrder = Mathf.Min(32000, 20000 + c.sortingOrder);
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
