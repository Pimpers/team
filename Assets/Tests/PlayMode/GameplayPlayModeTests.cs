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
