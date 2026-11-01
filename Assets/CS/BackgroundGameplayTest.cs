#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Opt-in unattended gameplay test. It feeds virtual keyboard and mouse input, so it
// exercises the normal movement, jump, dash and attack code without moving the real cursor.
public class BackgroundGameplayTest : MonoBehaviour
{
    [System.Serializable]
    class Report
    {
        public string scene;
        public float seconds;
        public float startX;
        public float endX;
        public float farthestX;
        public float hpLeft;
        public int kills;
        public long damage;
        public int openingEnemyCount;
        public int openingRatCount;
        public int openingRangedCount;
        public float openingHighestY;
        public string openingEnemies;
        public float longestStuck;
        public int largeBacktracks;
        public int frames;
        public float averageFps;
        public string stopReason;
    }

    string output;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void StartIfRequested()
    {
        var args = System.Environment.GetCommandLineArgs();
        bool requested = false;
        string folder = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-gameplayPlaytest") requested = true;
            if (args[i] == "-playtestOut" && i + 1 < args.Length) folder = args[++i];
        }
        if (!requested) return;
        var go = new GameObject("BackgroundGameplayTest");
        DontDestroyOnLoad(go);
        go.AddComponent<BackgroundGameplayTest>().output = folder;
    }

    IEnumerator Start()
    {
        Application.runInBackground = true;
        Application.targetFrameRate = 60;
        if (string.IsNullOrEmpty(output)) output = Path.Combine(Application.persistentDataPath, "GameplayPlaytest");
        Directory.CreateDirectory(output);

        var load = SceneManager.LoadSceneAsync("Stage1");
        while (!load.isDone) yield return null;
        GameClock.ResetAll();
        yield return new WaitForSecondsRealtime(2.5f);

        var stats = PlayerStats.Instance;
        var body = stats != null ? stats.GetComponent<PlayerController2D>() : null;
        if (stats == null || body == null)
        {
            File.WriteAllText(Path.Combine(output, "report.json"), "{\"stopReason\":\"player missing\"}");
            Application.Quit(2);
            yield break;
        }

        PlayerStats.Immortal = false;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var mouse = InputSystem.AddDevice<Mouse>();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(Screen.width * 0.78f, Screen.height * 0.5f) });
        yield return null;

        var report = new Report { scene = "Stage1", startX = body.transform.position.x, farthestX = body.transform.position.x };
        var roster = EnemyRoster.Load();
        foreach (var enemy in FindObjectsByType<Enemy>())
        {
            if (enemy == null || enemy.isBoss || !enemy.gameObject.activeInHierarchy) continue;
            report.openingEnemyCount++;
            if (roster != null && enemy.definition == roster.rat) report.openingRatCount++;
            if (roster != null && roster.others != null && roster.others.Length > 0 && enemy.definition == roster.others[0])
                report.openingRangedCount++;
            report.openingHighestY = Mathf.Max(report.openingHighestY, enemy.transform.position.y);
            if (!string.IsNullOrEmpty(report.openingEnemies)) report.openingEnemies += ", ";
            report.openingEnemies += $"{enemy.definition.name}@({enemy.transform.position.x:0.0},{enemy.transform.position.y:0.0})";
        }
        float started = Time.realtimeSinceStartup;
        float lastProgressX = body.transform.position.x;
        float stuck = 0f;
        float lastX = body.transform.position.x;
        int lastJump = -1, lastAttack = -1, lastDash = -1;
        bool shot5 = false, shot15 = false, shot30 = false;
        string stop = "time limit";

        while (Time.realtimeSinceStartup - started < 38f)
        {
            float elapsed = Time.realtimeSinceStartup - started;
            int jumpCycle = Mathf.FloorToInt(elapsed / 1.7f);
            int attackCycle = Mathf.FloorToInt(elapsed / 0.42f);
            int dashCycle = Mathf.FloorToInt(elapsed / 3.1f);
            bool jump = jumpCycle != lastJump;
            bool attack = attackCycle != lastAttack;
            bool dash = dashCycle != lastDash;
            if (jump) lastJump = jumpCycle;
            if (attack) lastAttack = attackCycle;
            if (dash) lastDash = dashCycle;

            InputSystem.QueueStateEvent(keyboard, jump ? new KeyboardState(Key.D, Key.Space) : new KeyboardState(Key.D));
            var mouseState = new MouseState { position = new Vector2(Screen.width * 0.78f, Screen.height * 0.5f) };
            if (attack) mouseState = mouseState.WithButton(MouseButton.Left);
            if (dash) mouseState = mouseState.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(mouse, mouseState);

            yield return null;
            report.frames++;
            float x = body.transform.position.x;
            report.farthestX = Mathf.Max(report.farthestX, x);
            if (Mathf.Abs(x - lastProgressX) >= 0.2f)
            {
                lastProgressX = x;
                stuck = 0f;
            }
            else
            {
                stuck += Time.unscaledDeltaTime;
                report.longestStuck = Mathf.Max(report.longestStuck, stuck);
            }
            if (lastX - x > 5f) report.largeBacktracks++;
            lastX = x;

            if (!shot5 && elapsed >= 5f) { shot5 = true; yield return Save("combat_05s"); }
            if (!shot15 && elapsed >= 15f) { shot15 = true; yield return Save("combat_15s"); }
            if (!shot30 && elapsed >= 30f) { shot30 = true; yield return Save("combat_30s"); }

            if (stats.CurrentHP <= 0f) { stop = "player died"; break; }
            if (RunManager.Instance != null && RunManager.Instance.RunEnded) { stop = "run ended"; break; }
            if (Time.timeScale == 0f) { stop = "stage cleared or popup opened"; break; }
        }

        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        InputSystem.QueueStateEvent(mouse, new MouseState());
        yield return Save("combat_end");
        report.seconds = Time.realtimeSinceStartup - started;
        report.endX = body.transform.position.x;
        report.hpLeft = stats.CurrentHP;
        report.kills = RunStats.Kills;
        report.damage = RunStats.DamageDealt;
        report.averageFps = report.seconds > 0f ? report.frames / report.seconds : 0f;
        report.stopReason = stop;
        File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
        File.WriteAllText(Path.Combine(output, "complete.txt"), "ok");
        InputSystem.RemoveDevice(mouse);
        InputSystem.RemoveDevice(keyboard);
        Application.Quit();
    }

    IEnumerator Save(string name)
    {
        // Batch mode (no window): WaitForEndOfFrame never fires and nothing can be captured - carry on without.
        if (Application.isBatchMode) { yield return null; yield break; }
        yield return new WaitForEndOfFrame();
        string path = Path.Combine(output, name + ".png");
        ScreenCapture.CaptureScreenshot(path, 1);
        float until = Time.realtimeSinceStartup + 5f;
        while (!File.Exists(path) && Time.realtimeSinceStartup < until) yield return null;
    }
}
#endif