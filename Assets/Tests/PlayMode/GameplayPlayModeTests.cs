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
        Assert.That(labels.transform.childCount, Is.EqualTo(12));
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
