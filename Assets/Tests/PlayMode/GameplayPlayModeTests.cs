using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// The game running: the screens load and build their runtime parts without errors (an error log fails the test),
// and the pieces that only work frame by frame (banner queue, role icons) behave.
public class GameplayPlayModeTests
{
    [TearDown]
    public void Unpause() => GameClock.ResetAll();

    [UnityTest]
    public IEnumerator Stage1_LoadsWithPlayerRunAndHud()
    {
        SceneManager.LoadScene("Stage1");
        yield return new WaitForSecondsRealtime(2f);
        Assert.That(PlayerStats.Instance, Is.Not.Null, "no player");
        Assert.That(RunManager.Instance, Is.Not.Null, "no RunManager");
        Assert.That(Object.FindAnyObjectByType<SkillHUD>(), Is.Not.Null, "no skill bar");
        Assert.That(Object.FindAnyObjectByType<SetHUD>(FindObjectsInactive.Include), Is.Not.Null, "no set chips");
    }

    // The exit log: open it, walk into its mouth, and the next stage loads behind the iris with the cat out and free.
    [UnityTest]
    public IEnumerator ExitLog_Entered_StartsNextStage()
    {
        SceneManager.LoadScene("Stage1");
        yield return new WaitForSecondsRealtime(2f);
        var exit = StageExit.Instance;
        Assert.That(exit, Is.Not.Null, "no exit");
        Assert.That(exit.Open("Stage2"), Is.True, "exit did not open");
        var body = PlayerStats.Instance.Body;
        var feet = body.Feet;
        body.transform.position += new Vector3(exit.ExitPoint.x - feet.x, exit.ExitPoint.y - 0.5f - feet.y, 0f);

        float until = Time.realtimeSinceStartup + 6f;
        while (SceneManager.GetActiveScene().name != "Stage2" && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Stage2"), "never left through the log");

        until = Time.realtimeSinceStartup + 5f;
        while ((StageWipe.Busy || PlayerStats.Instance == null || PlayerStats.Instance.Body.skillLock) && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(StageWipe.Busy, Is.False, "the iris stayed shut");
        Assert.That(PlayerStats.Instance.Body.Renderer.enabled, Is.True, "the cat never came out");
        Assert.That(PlayerStats.Instance.Body.skillLock, Is.False, "the cat is still locked");
    }

    // The fork at a stage's end: the red log is the hard way (사냥 길), the green one the easy way (안전 길). Walking into
    // one sets up the next stage on that route, and the other log shuts at once.
    [UnityTest]
    public IEnumerator ForkHardLog_Entered_NextStageIsHunt() => ForkEntered(true);

    [UnityTest]
    public IEnumerator ForkEasyLog_Entered_NextStageIsSafe() => ForkEntered(false);

    IEnumerator ForkEntered(bool hunt)
    {
        RouteProgress.Reset();
        SceneManager.LoadScene("Stage1");
        yield return new WaitForSecondsRealtime(2f);
        var exit = StageExit.Instance;
        Assert.That(exit, Is.Not.Null, "no exit");
        PlayerStats.Instance.SaveForNextStage(); // as the card pick does: the run carries on (without it Stage2 starts a new run)
        Assert.That(exit.OpenFork("Stage2", RouteProgress.SetNextRoute), Is.True, "no fork");
        Assert.That(exit.IsFork && exit.WayCount == 2, Is.True, "the fork has not two logs");
        var body = PlayerStats.Instance.Body;
        var feet = body.Feet;
        var way = exit.RoutePoint(hunt);
        body.transform.position += new Vector3(way.x - feet.x, way.y - 0.5f - feet.y, 0f);
        float settle = Time.realtimeSinceStartup + 1f; // set down by the mouth: a frame or a few to land
        while (!StageExit.Entering && Time.realtimeSinceStartup < settle) yield return null;
        Assert.That(StageExit.Entering, Is.True, "not drawn in");
        int other = exit.WayIsHunt(0) == hunt ? 1 : 0;
        Assert.That(exit.WayOpen(other), Is.False, "the other log stayed open");

        float until = Time.realtimeSinceStartup + 8f;
        while ((SceneManager.GetActiveScene().name != "Stage2" || StageWipe.Busy) && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Stage2"), "never left through the log");
        yield return new WaitForSecondsRealtime(1f); // the next stage applies the route
        Assert.That(hunt ? RouteProgress.HuntChoices : RouteProgress.SafeChoices, Is.EqualTo(1), "the next stage is not on the chosen route");
        Assert.That(hunt ? RouteProgress.SafeChoices : RouteProgress.HuntChoices, Is.Zero, "the other route was taken too");
    }

    // Giving up after the card pick (the carry is already saved for the next stage) must not seed the next run.
    [UnityTest]
    public IEnumerator GiveUpAfterCardPick_NextRunStartsClean()
    {
        GameSave.ResetForTests(); // giving up banks gold: never into the player's real save
        SceneManager.LoadScene("Stage1");
        yield return new WaitForSecondsRealtime(2f);
        PlayerStats.Instance.AddGold(50);
        PlayerStats.Instance.SaveForNextStage();
        RunManager.Instance.GiveUp();
        Assert.That(RunSession.Current.HasCarry, Is.False, "the given-up run is still carried");
        Assert.That(RunSession.Current.CarryGold, Is.Zero);
        float until = Time.realtimeSinceStartup + 10f; // let the fade and the scene switch finish before the next test
        while (SceneManager.GetActiveScene().name == "Stage1" && Time.realtimeSinceStartup < until) yield return null;
    }

    // The boss room's log: walking in irises shut, the result board is shown behind it, and the iris opens again.
    [UnityTest]
    public IEnumerator BossRoomLog_Entered_OpensOnResult()
    {
        SceneManager.LoadScene("Stage5");
        yield return new WaitForSecondsRealtime(2f);
        var exit = StageExit.Instance;
        Assert.That(exit, Is.Not.Null, "no exit");
        bool shown = false;
        Assert.That(exit.OpenToResult(() => shown = true), Is.True, "exit did not open");
        var body = PlayerStats.Instance.Body;
        var feet = body.Feet;
        body.transform.position += new Vector3(exit.ExitPoint.x - feet.x, exit.ExitPoint.y - 0.5f - feet.y, 0f);

        float until = Time.realtimeSinceStartup + 8f;
        while ((!shown || StageWipe.Busy) && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(shown, Is.True, "the result was never shown");
        Assert.That(StageWipe.Busy, Is.False, "the iris stayed shut");
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Stage5"), "left the scene");
    }

    [UnityTest]
    public IEnumerator Title_AddsSettingsAndCreditsButtons()
    {
        SceneManager.LoadScene("TitleScene");
        yield return new WaitForSecondsRealtime(0.5f);
        Assert.That(GameObject.Find("SettingsButton"), Is.Not.Null);
        Assert.That(GameObject.Find("CreditsButton"), Is.Not.Null);
    }

    [UnityTest]
    public IEnumerator WorldMap_CoversEveryRegionNameInKorean()
    {
        SceneManager.LoadScene("WorldMapScene");
        yield return new WaitForSecondsRealtime(0.5f);
        var labels = GameObject.Find("MapLabels");
        Assert.That(labels, Is.Not.Null);
        // Progressive reveal: the title, the open forest and the next region ("?") carry plates; the other nine
        // painted English names (the realm's joins the title) are under cloud - none of the twelve left bare.
        int plates = 0, fog = 0;
        foreach (Transform t in labels.transform) { if (t.name.StartsWith("Label_")) plates++; else if (t.name == "Fog") fog++; }
        Assert.That(plates, Is.EqualTo(3), "title, open region, next region");
        Assert.That(fog, Is.GreaterThanOrEqualTo(9), "a cloud for every hidden region");
    }

    [UnityTest]
    public IEnumerator Banners_RaisedTogether_WaitTheirTurn()
    {
        var canvas = new GameObject("TestCanvas", typeof(Canvas)).GetComponent<Canvas>();
        StageBanner.Show(canvas, null, "A", "a", 1f);
        StageBanner.Show(canvas, null, "B", "b", 1f);
        yield return null;
        var fades = canvas.GetComponentsInChildren<FadeBanner>(true);
        Assert.That(fades.Length, Is.EqualTo(2));
        Assert.That(fades[0].delay, Is.EqualTo(0f));
        Assert.That(fades[1].delay, Is.GreaterThan(1f), "the second banner should wait for the first");
        Object.Destroy(canvas.gameObject);
    }

    [UnityTest]
    public IEnumerator RoleIcons_ShowAtMostTwo_AndClearUp()
    {
        for (int i = 0; i < 4; i++) RoleIcon.Show(Vector3.right * i, EnemyDefinition.CombatRole.Chaser);
        yield return null;
        Assert.That(Object.FindObjectsByType<RoleIcon>().Length, Is.EqualTo(2));
        yield return new WaitForSeconds(1.4f);
        Assert.That(Object.FindObjectsByType<RoleIcon>().Length, Is.Zero);
    }
}
