using NUnit.Framework;
using UnityEngine;

public class RunStateTests
{
    [TearDown]
    public void TearDown()
    {
        var session = Object.FindAnyObjectByType<RunSession>();
        if (session != null) Object.DestroyImmediate(session.gameObject);
    }

    [Test]
    public void Reset_ClearsRunStatsAndRoutes()
    {
        RunStats.Kills = 9;
        RunStats.DamageDealt = 123;
        RouteProgress.ChooseSafe();
        RunStats.Reset();
        RouteProgress.Reset();

        Assert.That(RunStats.Kills, Is.Zero);
        Assert.That(RunStats.DamageDealt, Is.Zero);
        Assert.That(RouteProgress.SafeChoices, Is.Zero);
        Assert.That(RunStats.Seed, Is.Not.Zero);
    }

    [Test]
    public void StarterCard_IsFixedCommonAttackCard()
    {
        var go = new GameObject("PlayerStatsTest");
        var stats = go.AddComponent<PlayerStats>();
        var card = CardPool.DrawStarter(stats);

        Assert.That(card, Is.Not.Null);
        Assert.That(card.id, Is.EqualTo("attack"));
        Assert.That(card.tier, Is.EqualTo(CardPool.Tier.Common));
        Object.DestroyImmediate(go);
    }
}
