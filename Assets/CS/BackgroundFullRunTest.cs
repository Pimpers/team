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
    }

    [System.Serializable]
    class Report
    {
        public List<StageResult> stages = new List<StageResult>();
        public bool victoryScreen;
        public float seconds;
        public string error = "";
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
            if (args[i] == "-fullRunPlaytest") requested = true;
            if (args[i] == "-playtestOut" && i + 1 < args.Length) folder = args[++i];
        }
        if (!requested) return;
        var go = new GameObject("BackgroundFullRunTest");
        DontDestroyOnLoad(go);
        go.AddComponent<BackgroundFullRunTest>().output = folder;
    }

    IEnumerator Start()
    {
        Application.runInBackground = true;
        if (string.IsNullOrEmpty(output)) output = Path.Combine(Application.persistentDataPath, "FullRunPlaytest");
        Directory.CreateDirectory(output);
        var report = new Report();
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
            var map = StageMap.Instance;
            bool vertical = map != null && map.verticalLayout;
            bool horizontalRooms = !vertical && (stage == 1 || stage == 3);
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
                    while (enemy != null && !enemy.IsDead)
                    {
                        enemy.ReceiveDamage(stats.AttackPower, 1f);
                        result.attackHits++;
                        yield return new WaitForSecondsRealtime(0.08f);
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
                float until = Time.realtimeSinceStartup + 5f;
                while ((run.resultUI == null || run.resultUI.panel == null || !run.resultUI.panel.activeSelf) && Time.realtimeSinceStartup < until)
                    yield return null;
                result.cardsAppeared = false;
                report.victoryScreen = run.resultUI != null && run.resultUI.panel != null && run.resultUI.panel.activeSelf;
                yield return Save("victory");
                report.stages.Add(result);
                break;
            }

            float chestDeadline = Time.realtimeSinceStartup + 6f;
            while (CardRewardChest.Instance == null && Time.realtimeSinceStartup < chestDeadline) yield return null;
            result.chestAppeared = CardRewardChest.Instance != null;
            if (!result.chestAppeared)
            {
                report.error = $"Stage{stage}: clear chest missing";
                report.stages.Add(result);
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
                report.stages.Add(result);
                break;
            }
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Save($"stage{stage}_cards");
            run.cardDraftUI.ChooseAttack();

            if (stage <= 3)
            {
                float routeDeadline = Time.realtimeSinceStartup + 4f;
                StageRouteChoiceUI route = null;
                while ((route = FindAnyObjectByType<StageRouteChoiceUI>()) == null && Time.realtimeSinceStartup < routeDeadline) yield return null;
                result.routeAppeared = route != null;
                if (route == null)
                {
                    report.error = $"Stage{stage}: route choice missing";
                    report.stages.Add(result);
                    break;
                }
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Save($"stage{stage}_route");
                string choice = stage % 2 == 1 ? "Hunt" : "Safe";
                var buttonTransform = route.transform.Find("Panel/" + choice);
                var button = buttonTransform != null ? buttonTransform.GetComponent<Button>() : null;
                if (button == null)
                {
                    report.error = $"Stage{stage}: {choice} button missing";
                    report.stages.Add(result);
                    break;
                }
                button.onClick.Invoke();
            }

            report.stages.Add(result);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Save($"stage{stage}_exit");
            var next = SceneManager.LoadSceneAsync($"Stage{stage + 1}");
            while (!next.isDone) yield return null;
        }

        report.seconds = Time.realtimeSinceStartup - began;
        File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
        File.WriteAllText(Path.Combine(output, "complete.txt"), "ok");
        Application.Quit(string.IsNullOrEmpty(report.error) && report.victoryScreen ? 0 : 3);
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