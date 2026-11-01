#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Opt-in visual smoke test for unattended builds.
// Run with: -backgroundPlaytest -playtestOut <folder>
public class BackgroundPlaytest : MonoBehaviour
{
    string output;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void StartIfRequested()
    {
        var args = System.Environment.GetCommandLineArgs();
        bool requested = false;
        string folder = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-backgroundPlaytest") requested = true;
            if (args[i] == "-playtestOut" && i + 1 < args.Length) folder = args[++i];
        }
        if (!requested) return;
        var go = new GameObject("BackgroundPlaytest");
        DontDestroyOnLoad(go);
        go.AddComponent<BackgroundPlaytest>().output = folder;
    }

    IEnumerator Start()
    {
        Application.runInBackground = true;
        if (string.IsNullOrEmpty(output)) output = Path.Combine(Application.persistentDataPath, "PlaytestShots");
        Directory.CreateDirectory(output);

        yield return CaptureStaticScene("TitleScene", "title_ui", 1.5f);
        yield return CaptureStaticScene("LobbyScene", "lobby_ui", 1.5f);
        yield return CapturePause();
        yield return CaptureStage("Stage1", "stage1_start", 0.12f, null);
        yield return CaptureStage("Stage1", "stage1_middle", 0.52f, null);
        yield return CaptureStage("Stage1", "stage1_end", 0.88f, null);
        yield return CaptureUi("card_draft", false);
        yield return CaptureUi("route_choice", true);
        yield return CaptureRewardChest();
        yield return CaptureSkillHud();
        yield return CaptureStage("Stage2", "stage2_bottom", 0.16f, null);
        yield return CaptureStage("Stage2", "stage2_safe", 0.52f, false);
        yield return CaptureStage("Stage2", "stage2_hunt", 0.52f, true);
        yield return CaptureStage("Stage2", "stage2_top", 0.88f, null);
        yield return CaptureStage("Stage3", "stage3_safe", 0.55f, false);
        yield return CaptureStage("Stage3", "stage3_hunt", 0.55f, true);
        yield return CaptureStage("Stage4", "stage4_bottom", 0.14f, null);
        yield return CaptureStage("Stage4", "stage4_safe", 0.52f, false);
        yield return CaptureStage("Stage4", "stage4_hunt", 0.52f, true);
        yield return CaptureStage("Stage4", "stage4_top", 0.9f, null);
        yield return CaptureStage("Stage5", "stage5_boss", 0.82f, null);

        File.WriteAllText(Path.Combine(output, "complete.txt"), "ok");
        Application.Quit();
    }

    IEnumerator CaptureStaticScene(string scene, string fileName, float delay)
    {
        var load = SceneManager.LoadSceneAsync(scene);
        while (!load.isDone) yield return null;
        GameClock.ResetAll();
        yield return new WaitForSecondsRealtime(delay);
        yield return Save(fileName);
    }

    IEnumerator CapturePause()
    {
        var load = SceneManager.LoadSceneAsync("Stage1");
        while (!load.isDone) yield return null;
        GameClock.ResetAll();
        PlayerStats.Immortal = true;
        yield return new WaitForSecondsRealtime(2.5f);
        var pause = FindAnyObjectByType<PauseMenu>();
        if (pause != null) pause.OpenForPlaytest();
        yield return new WaitForSecondsRealtime(0.25f);
        yield return Save("pause_ui");
        GameClock.ResetAll();
    }

    IEnumerator CaptureRewardChest()
    {
        var load = SceneManager.LoadSceneAsync("Stage1");
        while (!load.isDone) yield return null;
        GameClock.ResetAll();
        PlayerStats.Immortal = true;
        yield return new WaitForSecondsRealtime(0.5f);
        foreach (var enemy in new List<Enemy>(Enemy.All)) if (enemy != null) enemy.enabled = false;
        var run = RunManager.Instance;
        CardRewardChest.Create(() => run.cardDraftUI.Show(() => { }));
        yield return null;
        var chest = FindAnyObjectByType<CardRewardChest>();
        var body = PlayerStats.Instance != null ? PlayerStats.Instance.GetComponent<PlayerController2D>() : null;
        if (chest != null && body != null)
            body.transform.position = chest.transform.position + Vector3.left + Vector3.up * body.feetOffset;
        yield return new WaitForSecondsRealtime(0.2f);
        yield return Save("reward_chest");

        for (int i = 0; i < 2; i++)
        {
            float feet = body != null ? body.Feet.y : chest.transform.position.y;
            float top = feet + (body != null ? body.bodyHeight : 2f);
            CardRewardChest.TryMeleeHit(body != null ? (Vector2)body.transform.position : (Vector2)chest.transform.position + Vector2.left,
                                         1f, 3f, feet, top);
            yield return new WaitForSecondsRealtime(0.35f);
        }
        yield return new WaitForSecondsRealtime(1.4f);
        yield return Save("reward_cards");
    }

    IEnumerator CaptureSkillHud()
    {
        var load = SceneManager.LoadSceneAsync("Stage1");
        while (!load.isDone) yield return null;
        GameClock.ResetAll();
        PlayerStats.Immortal = true;
        yield return new WaitForSecondsRealtime(0.5f);
        var stats = PlayerStats.Instance;
        if (stats != null)
        {
            stats.ApplyCard("k_combo");
            stats.ApplyCard("k_hiss");
            stats.ApplyCard("k_night");
        }
        foreach (var enemy in new List<Enemy>(Enemy.All)) if (enemy != null) enemy.enabled = false;
        yield return new WaitForSecondsRealtime(2.5f);
        yield return Save("skill_hud");
    }

    IEnumerator CaptureUi(string fileName, bool route)
    {
        var load = SceneManager.LoadSceneAsync("Stage1");
        while (!load.isDone) yield return null;
        GameClock.ResetAll();
        PlayerStats.Immortal = true;
        yield return new WaitForSecondsRealtime(3f);
        foreach (var enemy in new List<Enemy>(Enemy.All)) if (enemy != null) enemy.enabled = false;

        var run = RunManager.Instance;
        if (run != null && run.hudText != null && run.hudText.canvas != null)
        {
            if (route) StageRouteChoiceUI.Show(run, run.hudText.canvas.rootCanvas, run.hudText.font, _ => { });
            else if (run.cardDraftUI != null) run.cardDraftUI.Show(() => { });
        }
        yield return new WaitForSecondsRealtime(route ? 0.4f : 1.8f);
        yield return Save(fileName);
    }

    IEnumerator Save(string fileName)
    {
        // Batch mode (no window): WaitForEndOfFrame never fires and nothing can be captured - just finish.
        if (Application.isBatchMode) { Application.Quit(); yield break; }
        yield return new WaitForEndOfFrame();
        string path = Path.Combine(output, fileName + ".png");
        ScreenCapture.CaptureScreenshot(path, 1);
        float until = Time.realtimeSinceStartup + 5f;
        while (!File.Exists(path) && Time.realtimeSinceStartup < until) yield return null;
        yield return new WaitForSecondsRealtime(0.15f);
    }

    IEnumerator CaptureStage(string scene, string fileName, float xRatio, bool? hunt)
    {
        var load = SceneManager.LoadSceneAsync(scene);
        while (!load.isDone) yield return null;
        GameClock.ResetAll();
        PlayerStats.Immortal = true;
        yield return new WaitForSecondsRealtime(0.35f); // runtime route, encounter and HUD setup
        float titleDeadline = Time.realtimeSinceStartup + 4f;
        while (StageTitle.Showing && Time.realtimeSinceStartup < titleDeadline) yield return null;

        if (hunt.HasValue && RunManager.Instance != null)
            RunManager.Instance.ApplyRoute(hunt.Value, false);

        foreach (var enemy in new List<Enemy>(Enemy.All))
            if (enemy != null) enemy.enabled = false;

        var map = StageMap.Instance;
        var cam = Camera.main;
        if (map != null && cam != null)
        {
            var follow = cam.GetComponent<CameraFollow>();
            if (follow != null) follow.enabled = false;
            float targetX = map.mapSize.x * xRatio;
            float targetY = map.mapSize.y * 0.5f;
            float best = float.MaxValue;
            if (map.verticalLayout)
            {
                targetX = map.mapSize.x * 0.5f;
                targetY = map.mapSize.y * xRatio;
                if (map.spawnSlots != null)
                    foreach (var slot in map.spawnSlots)
                    {
                        float d = Mathf.Abs(slot.y - targetY);
                        if (d < best) { best = d; targetX = slot.x; targetY = slot.y + 2.2f; }
                    }
            }
            else if (map.spawnSlots != null)
            {
                foreach (var slot in map.spawnSlots)
                {
                    float d = Mathf.Abs(slot.x - targetX);
                    if (d < best) { best = d; targetY = slot.y + 2.2f; }
                }
            }
            var p = cam.transform.position;
            cam.transform.position = new Vector3(targetX, targetY, p.z);
        }

        yield return Save(fileName);
    }
}
#endif