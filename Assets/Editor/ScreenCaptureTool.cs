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
// lobby, the in-run HUD, pause, card draft, result. Overlay canvases are drawn through the camera for the shot
// (world sprites with a higher sorting order can then show on top of a panel - in the game the UI is always on top).
public static class ScreenCaptureTool
{
    struct Step { public string scene, shot; public Action act; }
    static readonly List<Step> steps = new List<Step>();
    static int index;
    static double at;
    static bool loaded, batch, savedOptionsEnabled;
    static EnterPlayModeOptions savedOptions;
    static string outDir;

    [MenuItem("Cats Smash/Capture Screens")]
    public static void Run() => Start(false);

    public static void RunBatch() => Start(true);

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
        Add("WorldMapScene", "worldmap");
        Add("LobbyScene", "lobby");
        Add("Stage2", "hud", () => { foreach (var id in cards) PlayerStats.Instance.ApplyCard(id); });
        Add("Stage2", "pause", () => { foreach (var id in cards) PlayerStats.Instance.ApplyCard(id); Find<PauseMenu>().OpenForPlaytest(); });
        Add("Stage2", "draft", () => Find<CardDraftUI>().Show(() => { }));
        Add("Stage2", "result", () => { foreach (var id in cards) PlayerStats.Instance.ApplyCard(id); Find<ResultUI>().Show(false, 12, 1); });

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
                if (s.act != null) { s.act(); steps[index] = new Step { scene = s.scene, shot = s.shot }; at = now + 1.0; return; }
                Shot(s.shot);
                loaded = false;
            }
        }
        catch (Exception e) { Debug.LogError($"[ScreenCapture] {steps[index].shot}: {e}"); loaded = false; }
        index++;
        if (index >= steps.Count) { Finish(); return; }
        GameClock.ResetAll();
        SceneManager.LoadScene(steps[index].scene);
        loaded = true;
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

    // The screen at 3x, with overlay canvases drawn through the camera (they don't render into a camera target).
    static void Shot(string name)
    {
        var cam = Camera.main;
        if (cam == null) return;
        int w = Screen.width * 3, h = Screen.height * 3; // same aspect as the screen the UI was laid out for
        var canvases = UnityEngine.Object.FindObjectsByType<Canvas>();
        var modes = new RenderMode[canvases.Length];
        for (int i = 0; i < canvases.Length; i++)
        {
            modes[i] = canvases[i].renderMode;
            if (canvases[i].isRootCanvas && modes[i] == RenderMode.ScreenSpaceOverlay)
            {
                canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                canvases[i].worldCamera = cam;
                canvases[i].planeDistance = 1f + (32000 - canvases[i].sortingOrder) * 0.0001f;
            }
        }
        var rt = new RenderTexture(w, h, 24);
        cam.targetTexture = rt;
        Canvas.ForceUpdateCanvases();
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(tex);
        for (int i = 0; i < canvases.Length; i++)
            if (canvases[i] != null && canvases[i].isRootCanvas)
            {
                canvases[i].renderMode = modes[i];
                if (modes[i] == RenderMode.ScreenSpaceOverlay) canvases[i].worldCamera = null;
            }
    }
}
