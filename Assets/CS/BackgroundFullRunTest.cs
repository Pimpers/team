#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Opt-in end-to-end run test. It uses real enemy damage, clear, chest, card and route
// callbacks, but teleports between fights so it can finish unattended.
public class BackgroundFullRunTest : MonoBehaviour
{
    [System.Serializable]
    class StageResult
    {
        public int stage;
        public int enemies;
        public int attackHits;
        public bool chestAppeared;
        public bool cardsAppeared;
        public bool routeAppeared;
        // The numbers balance is judged by: how long the stage took, what it cost, what it paid.
        public float seconds;
        public float damageTaken;
        public float heartsLeft;
        public int goldAfter;
        public int kills;
        public int dodges; // dodge mode: boss wind-ups backed away from
        public int bossSpecials, bossStaggers; // what the boss got to do, and how often it was dazed
        public float bossFight, bossNeutral;   // seconds after its intro, and of those with no move running
    }

    float stageBegan, damageAtStart;
    int killsAtStart;

    StageResult Close(StageResult r)
    {
        r.seconds = Time.realtimeSinceStartup - stageBegan;
        r.damageTaken = RunStats.DamageTaken - damageAtStart;
        r.kills = RunStats.Kills - killsAtStart;
        var p = PlayerStats.Instance;
        if (p != null) { r.heartsLeft = p.CurrentHP; r.goldAfter = p.Gold; }
        return r;
    }

    [System.Serializable]
    class Report
    {
        public string mode = "plain"; // "plain": stands and hits; "dodge" (-playtestDodge): steps off the boss's wind-ups
        public List<StageResult> stages = new List<StageResult>();
        public bool victoryScreen;
        public float seconds;
        public string error = "";
    }

    string output;
    bool dodge;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void StartIfRequested()
    {
        var args = System.Environment.GetCommandLineArgs();
        bool requested = false, dodgeMode = false;
        string folder = null;
        int fps = 0; // -playtestFps 30: the run at a fixed low frame rate (long frames are where movement bugs hide)
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-fullRunPlaytest") requested = true;
            if (args[i] == "-playtestOut" && i + 1 < args.Length) folder = args[++i];
            if (args[i] == "-playtestDodge") dodgeMode = true;
            if (args[i] == "-playtestFps" && i + 1 < args.Length) int.TryParse(args[++i], out fps);
        }
        if (!requested) return;
        if (fps > 0) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = fps; }
        var go = new GameObject("BackgroundFullRunTest");
        DontDestroyOnLoad(go);
        var test = go.AddComponent<BackgroundFullRunTest>();
        test.output = folder;
        test.dodge = dodgeMode;
        StageExit.AutoEnter = true; // the harness walks into the logs; it holds no key
    }

    IEnumerator Start()
    {
        Application.runInBackground = true;
        if (string.IsNullOrEmpty(output)) output = Path.Combine(Application.persistentDataPath, "FullRunPlaytest");
        Directory.CreateDirectory(output);
        var report = new Report { mode = dodge ? "dodge" : "plain" };
        float began = Time.realtimeSinceStartup;

        var first = SceneManager.LoadSceneAsync("Stage1");
        while (!first.isDone) yield return null;

        for (int stage = 1; stage <= 5; stage++)
        {
            yield return new WaitForSecondsRealtime(0.8f);
            var run = RunManager.Instance;
            var stats = PlayerStats.Instance;
            var body = stats != null ? stats.GetComponent<PlayerController2D>() : null;
            if (run == null || stats == null || body == null)
            {
                report.error = $"Stage{stage}: player or run manager missing";
                break;
            }
            PlayerStats.Immortal = true;
            yield return Save($"stage{stage}_start");

            var result = new StageResult { stage = stage };
            stageBegan = Time.realtimeSinceStartup; damageAtStart = RunStats.DamageTaken; killsAtStart = RunStats.Kills;
            var map = StageMap.Instance;
            bool vertical = map != null && map.verticalLayout;
            bool horizontalRooms = !vertical && stage <= 4; // (2 and 4 too, when the run drew their sideways scene)
            bool middleSaved = false;
            int room = 0;
            while (room < 5)
            {
                yield return new WaitForSecondsRealtime(0.15f);
                var foes = new List<Enemy>();
                foreach (var enemy in Enemy.All)
                    if (enemy != null && !enemy.IsDead) foes.Add(enemy);
                foes.Sort((a, b) => vertical
                    ? a.transform.position.y.CompareTo(b.transform.position.y)
                    : a.transform.position.x.CompareTo(b.transform.position.x));
                result.enemies += foes.Count;

                foreach (var enemy in foes)
                {
                    if (enemy == null || enemy.IsDead) continue;
                    body.transform.position = enemy.transform.position + Vector3.left * (enemy.BodyHalfWidth + 0.8f) + Vector3.up * body.feetOffset;
                    body.FaceTowards(1f);
                    yield return null;
                    var brain = dodge ? enemy.GetComponent<BossBrain>() : null;
                    while (enemy != null && !enemy.IsDead)
                    {
                        if (brain != null && brain.Threatening)
                        {
                            // Dodge mode: on a wind-up, back well off (the cat's side of the boss) and wait it out;
                            // come back in and hit only in the openings.
                            result.dodges++;
                            float away = Mathf.Sign(body.transform.position.x - enemy.transform.position.x);
                            if (away == 0f) away = -1f;
                            body.transform.position = new Vector3(enemy.transform.position.x + away * (enemy.BodyHalfWidth + 7f),
                                                                  body.transform.position.y, body.transform.position.z);
                            while (enemy != null && !enemy.IsDead && brain.Threatening) yield return null;
                            if (enemy == null || enemy.IsDead) break;
                            body.transform.position = enemy.transform.position + Vector3.left * (enemy.BodyHalfWidth + 0.8f) + Vector3.up * body.feetOffset;
                            body.FaceTowards(1f);
                        }
                        var boss = enemy.GetComponent<BossBrain>();
                        if (boss != null) { result.bossSpecials = boss.Specials; result.bossStaggers = boss.Staggers; result.bossFight = boss.FightTime; result.bossNeutral = boss.NeutralTime; }
                        enemy.ReceiveDamage(stats.AttackPower, 1f);
                        result.attackHits++;
                        // Dodge mode swings at the player's real rate (PlayerAttack's cooldown over attack speed), so the
                        // boss time is a player's, not the plain run's 12 hits a second.
                        var swing = dodge ? stats.GetComponent<PlayerAttack>() : null;
                        yield return new WaitForSecondsRealtime(swing != null ? swing.attackCooldown / Mathf.Max(0.1f, stats.AttackSpeedMultiplier) : 0.08f);
                    }
                }

                if (!middleSaved && (room >= 1 || stage == 5))
                {
                    middleSaved = true;
                    yield return Save($"stage{stage}_middle");
                }

                bool hasNext = vertical
                    ? map != null && map.verticalGateHeights != null && room < map.verticalGateHeights.Length
                    : horizontalRooms && map != null && map.horizontalGatePositions != null && room < map.horizontalGatePositions.Length;
                if (!hasNext) break;
                yield return new WaitForSecondsRealtime(0.4f);
                if (vertical)
                {
                    float y = map.verticalGateHeights[room] + 1f + body.feetOffset;
                    body.transform.position = new Vector3(body.transform.position.x, y, body.transform.position.z);
                }
                else
                {
                    float x = map.horizontalGatePositions[room] + 1f;
                    body.transform.position = new Vector3(x, body.transform.position.y, body.transform.position.z);
                }
                room++;
                yield return new WaitForSecondsRealtime(0.4f);
            }

            if (stage == 5)
            {
                // The boss room's crimson log opens on the win: walk in, the result board opens behind the iris.
                float logDeadline = Time.realtimeSinceStartup + 6f;
                while ((StageExit.Instance == null || !StageExit.Instance.IsOpen) && Time.realtimeSinceStartup < logDeadline) yield return null;
                if (StageExit.Instance != null && StageExit.Instance.IsOpen && PlayerStats.Instance != null)
                {
                    var bossBody = PlayerStats.Instance.Body;
                    var bossFeet = bossBody.Feet;
                    var bossExit = StageExit.Instance.ExitPoint;
                    bossBody.transform.position += new Vector3(bossExit.x - bossFeet.x, bossExit.y - 0.5f - bossFeet.y, 0f);
                }
                float until = Time.realtimeSinceStartup + 8f;
                while ((run.resultUI == null || run.resultUI.panel == null || !run.resultUI.panel.activeSelf || StageWipe.Busy) && Time.realtimeSinceStartup < until)
                    yield return null;
                result.cardsAppeared = false;
                report.victoryScreen = run.resultUI != null && run.resultUI.panel != null && run.resultUI.panel.activeSelf;
                yield return Save("victory");
                report.stages.Add(Close(result));
                break;
            }

            float chestDeadline = Time.realtimeSinceStartup + 6f;
            while (CardRewardChest.Instance == null && Time.realtimeSinceStartup < chestDeadline) yield return null;
            result.chestAppeared = CardRewardChest.Instance != null;
            if (!result.chestAppeared)
            {
                report.error = $"Stage{stage}: clear chest missing";
                report.stages.Add(Close(result));
                break;
            }
            yield return Save($"stage{stage}_chest");

            var chest = CardRewardChest.Instance;
            body.transform.position = chest.transform.position + Vector3.left + Vector3.up * body.feetOffset;
            body.FaceTowards(1f);
            yield return null;
            float feet = body.Feet.y;
            CardRewardChest.TryMeleeHit(body.transform.position, 1f, 3f, feet, feet + body.bodyHeight);
            yield return new WaitForSecondsRealtime(0.35f);
            CardRewardChest.TryMeleeHit(body.transform.position, 1f, 3f, feet, feet + body.bodyHeight);

            float cardDeadline = Time.realtimeSinceStartup + 5f;
            while ((run.cardDraftUI == null || run.cardDraftUI.panel == null || !run.cardDraftUI.panel.activeSelf) && Time.realtimeSinceStartup < cardDeadline)
                yield return null;
            result.cardsAppeared = run.cardDraftUI != null && run.cardDraftUI.panel != null && run.cardDraftUI.panel.activeSelf;
            if (!result.cardsAppeared)
            {
                report.error = $"Stage{stage}: card draft missing";
                report.stages.Add(Close(result));
                break;
            }
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Save($"stage{stage}_cards");
            run.cardDraftUI.ChooseAttack();

            if (stage <= 3)
            {
                // The fork: two logs (the hard way and the easy way), or the route window where a stage has no room for
                // two. Plain mode takes the easy way, dodge mode the hard way, so both routes are played to the end.
                float routeDeadline = Time.realtimeSinceStartup + 4f;
                StageRouteChoiceUI route = null;
                bool forkOpen = false;
                while (!(forkOpen = StageExit.Instance != null && StageExit.Instance.IsFork && StageExit.Instance.IsOpen)
                       && (route = FindAnyObjectByType<StageRouteChoiceUI>()) == null && Time.realtimeSinceStartup < routeDeadline) yield return null;
                result.routeAppeared = forkOpen || route != null;
                if (forkOpen) yield return Save($"stage{stage}_route");
                else if (route == null)
                {
                    report.error = $"Stage{stage}: route choice missing";
                    report.stages.Add(Close(result));
                    break;
                }
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Save($"stage{stage}_route");
                string choice = dodge ? "Hunt" : "Safe";
                var buttonTransform = forkOpen ? null : route.transform.Find("Panel/" + choice);
                var button = buttonTransform != null ? buttonTransform.GetComponent<Button>() : null;
                if (button == null && !forkOpen)
                {
                    report.error = $"Stage{stage}: {choice} button missing";
                    report.stages.Add(Close(result));
                    break;
                }
                if (button != null) button.onClick.Invoke();
            }

            report.stages.Add(Close(result));
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Save($"stage{stage}_exit");
            // Through the exit log, like a player: wait for it to open, step into its mouth, ride the iris to the next stage.
            string wanted = $"Stage{stage + 1}";
            float exitDeadline = Time.realtimeSinceStartup + 4f;
            while ((StageExit.Instance == null || !StageExit.Instance.IsOpen) && Time.realtimeSinceStartup < exitDeadline) yield return null;
            var exitLog = StageExit.Instance;
            if (exitLog != null && exitLog.IsOpen && PlayerStats.Instance != null)
            {
                var exitBody = PlayerStats.Instance.Body;
                var exitFeet = exitBody.Feet;
                Vector2 way = exitLog.IsFork ? exitLog.RoutePoint(dodge) : exitLog.ExitPoint;
                exitBody.transform.position += new Vector3(way.x - exitFeet.x, way.y - 0.5f - exitFeet.y, 0f);
            }
            float sceneDeadline = Time.realtimeSinceStartup + 8f;
            while (SceneManager.GetActiveScene().name != wanted && Time.realtimeSinceStartup < sceneDeadline) yield return null;
            if (SceneManager.GetActiveScene().name != wanted)
            {
                report.error = $"Stage{stage}: the exit log did not lead to {wanted}";
                break;
            }
            while (StageWipe.Busy && Time.realtimeSinceStartup < sceneDeadline + 4f) yield return null;
        }

        report.seconds = Time.realtimeSinceStartup - began;
        File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
        File.WriteAllText(Path.Combine(output, "complete.txt"), "ok");
        Application.Quit(string.IsNullOrEmpty(report.error) && report.victoryScreen ? 0 : 3);
    }

    IEnumerator Save(string name)
    {
        // A 1920 x 1080 shot of each step, in batch mode too (no window there): the main camera draws into a texture
        // that size, the overlay canvases drawn through it (above every world sprite, as in the game) for two frames
        // so they lay out for it, then everything goes back.
        var cam = Camera.main;
        if (cam == null) { yield return null; yield break; }
        var rt = new RenderTexture(1920, 1080, 24);
        var canvases = Object.FindObjectsByType<Canvas>();
        var modes = new RenderMode[canvases.Length];
        var orders = new int[canvases.Length];
        for (int i = 0; i < canvases.Length; i++)
        {
            var c = canvases[i];
            modes[i] = c.renderMode; orders[i] = c.sortingOrder;
            if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            c.renderMode = RenderMode.ScreenSpaceCamera;
            c.worldCamera = cam;
            c.planeDistance = 1f + (32000 - c.sortingOrder) * 0.0001f;
            c.sortingOrder = Mathf.Min(32767, 30000 + c.sortingOrder);
        }
        cam.targetTexture = rt;
        yield return null;
        yield return null;
        if (cam != null)
        {
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(output, name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
            cam.targetTexture = null;
        }
        for (int i = 0; i < canvases.Length; i++)
            if (canvases[i] != null && modes[i] == RenderMode.ScreenSpaceOverlay)
            { canvases[i].renderMode = modes[i]; canvases[i].sortingOrder = orders[i]; canvases[i].worldCamera = null; }
        Destroy(rt);
    }
}
#endif