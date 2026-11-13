using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// The numbers the balance rests on: card data, set steps, stage / corruption scaling, the HitStop / GameClock
// handshake. Pure functions only - no scene needed.
public class BalanceTests
{
    [Test]
    public void CardIds_AreUnique_AndEveryCardIsFound()
    {
        var ids = new HashSet<string>();
        foreach (var c in CardPool.All)
        {
            Assert.That(ids.Add(c.id), Is.True, $"duplicate card id {c.id}");
            Assert.That(CardPool.Get(c.id), Is.SameAs(c), $"Get({c.id})");
        }
    }

    [Test]
    public void EverySkillCard_HasAtLeastOneTag()
    {
        foreach (var c in CardPool.Skills)
            Assert.That(CardPool.TagsOf(c.id), Is.Not.Empty, $"{c.id} has no set tag");
    }

    [Test]
    public void SetLevel_StepsAtTwoAndThreeCards()
    {
        Assert.That(CardPool.SetLevel(0), Is.EqualTo(0));
        Assert.That(CardPool.SetLevel(1), Is.EqualTo(0));
        Assert.That(CardPool.SetLevel(CardPool.SetStep1), Is.EqualTo(1));
        Assert.That(CardPool.SetLevel(CardPool.SetStep2), Is.EqualTo(2));
        Assert.That(CardPool.SetLevel(CardPool.SetStep2 + 3), Is.EqualTo(2));
    }

    [Test]
    public void SetText_HasBothStepsForEveryTag()
    {
        Assert.That(CardPool.SetText.Length, Is.EqualTo(CardPool.TagNames.Length));
        foreach (var steps in CardPool.SetText) Assert.That(steps.Length, Is.EqualTo(2));
    }

    [Test]
    public void StageHp_GrowsEighteenPercentAStage_ToStageFour_BossUnchanged()
    {
        Assert.That(StageScaling.HpMultiplier(1, false), Is.EqualTo(1f).Within(1e-5f));
        Assert.That(StageScaling.HpMultiplier(2, false), Is.EqualTo(1.18f).Within(1e-5f));
        Assert.That(StageScaling.HpMultiplier(3, false), Is.EqualTo(1.36f).Within(1e-5f));
        Assert.That(StageScaling.HpMultiplier(4, false), Is.EqualTo(1.54f).Within(1e-5f));
        Assert.That(StageScaling.HpMultiplier(9, false), Is.EqualTo(1.54f).Within(1e-5f)); // no more after stage 4
        Assert.That(StageScaling.HpMultiplier(4, true), Is.EqualTo(1f));
        Assert.That(StageScaling.HpMultiplier(0, false), Is.EqualTo(1f)); // outside the stages
    }

    [Test]
    public void Corruption_ScalesUpEveryTier()
    {
        for (int lv = 1; lv <= Corruption.MaxLevel; lv++)
        {
            Assert.That(Corruption.HpMultiplier(lv), Is.GreaterThan(Corruption.HpMultiplier(lv - 1)));
            Assert.That(Corruption.GoldMultiplier(lv), Is.GreaterThan(Corruption.GoldMultiplier(lv - 1)));
            Assert.That(Corruption.ExtraElites(lv), Is.GreaterThanOrEqualTo(Corruption.ExtraElites(lv - 1)));
        }
    }

    [Test]
    public void Lifesteal_IsZeroWithoutTheCard_AndRisesWithLevel()
    {
        Assert.That(CardPool.Lifesteal(0), Is.Zero);
        Assert.That(CardPool.Lifesteal(2), Is.GreaterThan(CardPool.Lifesteal(1)));
        Assert.That(CardPool.Lifesteal(3), Is.LessThan(1f));
    }

    [Test]
    public void GameClock_StaysPausedUntilEveryHolderResumes()
    {
        GameClock.ResetAll();
        GameClock.Pause("draft");
        GameClock.Pause("pause");
        GameClock.Resume("pause");
        Assert.That(GameClock.Paused, Is.True, "the draft still holds the game");
        Assert.That(Time.timeScale, Is.Zero);
        GameClock.Resume("draft");
        Assert.That(GameClock.Paused, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        GameClock.ResetAll();
    }

    [Test]
    public void HitStop_NeverStartsDuringAPause()
    {
        GameClock.ResetAll();
        GameClock.Pause("test");
        HitStop.Do(0.2f);
        HitStop.Slow(1f, 0.3f);
        Assert.That(Time.timeScale, Is.Zero, "a hit stop must not unpause the game");
        GameClock.ResetAll();
        Assert.That(Time.timeScale, Is.EqualTo(1f));
    }
}
