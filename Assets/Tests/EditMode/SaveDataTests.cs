using NUnit.Framework;
using UnityEngine.InputSystem;

public class SaveDataTests
{
    [Test]
    public void Defaults_AreSafe()
    {
        var data = new SaveData();
        Assert.That(data.version, Is.EqualTo(GameSave.CurrentVersion));
        Assert.That(data.gold, Is.Zero);
        Assert.That(data.masterVolume, Is.InRange(0f, 1f));
        Assert.That(data.characterNames, Has.Length.EqualTo(2));
        Assert.That(data.bestClear, Is.EqualTo(-1));
        Assert.That(data.keyBindings, Is.Empty);
    }

    // A version 1 save, written before rerollLevel / seenSetTip / wins / bestClear / smithVisits / keyBindings existed.
    const string V1 = "{\"version\":1,\"gold\":120,\"attackLevel\":2,\"speedLevel\":1,\"hpLevel\":3,\"selectedCharacter\":1," +
                      "\"characterNames\":[\"nabi\",\"\"],\"corruptionUnlocked\":0,\"corruptionSelected\":0,\"masterVolume\":0.5," +
                      "\"musicVolume\":1.0,\"sfxVolume\":1.0,\"shakeStrength\":1.0,\"hitStopEnabled\":true,\"fullscreen\":false,\"tutorialDone\":true}";

    [Test]
    public void V1Save_LoadsWithDefaultsForNewFields()
    {
        var data = GameSave.Parse(V1);
        Assert.That(data, Is.Not.Null);
        Assert.That(data.version, Is.EqualTo(GameSave.CurrentVersion));
        Assert.That(data.gold, Is.EqualTo(120));
        Assert.That(data.attackLevel, Is.EqualTo(2));
        Assert.That(data.characterNames[0], Is.EqualTo("nabi"));
        Assert.That(data.masterVolume, Is.EqualTo(0.5f));
        Assert.That(data.rerollLevel, Is.Zero);
        Assert.That(data.wins, Is.Zero);
        Assert.That(data.bestClear, Is.EqualTo(-1));
        Assert.That(data.smithVisits, Is.Zero);
        Assert.That(data.seenSetTip, Is.True, "a player who finished the tutorial has seen the set tip");
        Assert.That(data.keyBindings, Is.Empty);
    }

    [Test]
    public void V1Save_WithUnlockedTiers_CountsThemAsWins()
    {
        var data = GameSave.Parse(V1.Replace("\"corruptionUnlocked\":0", "\"corruptionUnlocked\":2"));
        Assert.That(data.wins, Is.EqualTo(2));
        Assert.That(data.bestClear, Is.EqualTo(1));
    }

    [Test]
    public void BrokenOrEmptySave_IsRejected()
    {
        Assert.That(GameSave.Parse(""), Is.Null);
        Assert.That(GameSave.Parse("{}"), Is.Null);
        Assert.That(GameSave.Parse("{\"version\":0}"), Is.Null);
    }

    [Test]
    public void CurrentSave_RoundTrips()
    {
        var data = new SaveData { gold = 7, wins = 3, bestClear = 2, smithVisits = 4, rerollLevel = 1, seenSetTip = true, corruptionUnlocked = 3 };
        data.keyBindings = new[] { (int)Key.J, (int)Key.K };
        var back = GameSave.Parse(UnityEngine.JsonUtility.ToJson(data));
        Assert.That(back.wins, Is.EqualTo(3));
        Assert.That(back.bestClear, Is.EqualTo(2));
        Assert.That(back.smithVisits, Is.EqualTo(4));
        Assert.That(back.rerollLevel, Is.EqualTo(1));
        Assert.That(back.keyBindings, Is.EqualTo(new[] { (int)Key.J, (int)Key.K }));
    }

    [Test]
    public void KeyBindings_DefaultSwapAndReset()
    {
        GameSave.ResetForTests();
        Assert.That(GameInput.Binding(GameInput.Action.Jump), Is.EqualTo((int)Key.Space));
        Assert.That(GameInput.Binding(GameInput.Action.Attack), Is.EqualTo(GameInput.MouseLeft));
        GameInput.Rebind(GameInput.Action.Jump, (int)Key.Q); // Q was skill 1: the two swap
        Assert.That(GameInput.Binding(GameInput.Action.Jump), Is.EqualTo((int)Key.Q));
        Assert.That(GameInput.Binding(GameInput.Action.Skill1), Is.EqualTo((int)Key.Space));
        GameInput.Rebind(GameInput.Action.Dash, (int)Key.Escape); // not bindable: ignored
        Assert.That(GameInput.Binding(GameInput.Action.Dash), Is.EqualTo(GameInput.MouseRight));
        GameInput.ResetBindings();
        Assert.That(GameInput.Binding(GameInput.Action.Jump), Is.EqualTo((int)Key.Space));
        GameSave.ResetForTests();
    }

    [Test]
    public void EasyTier_IsSofterAndUnlocksNothing()
    {
        Assert.That(Corruption.HpMultiplier(Corruption.Easy), Is.LessThan(1f));
        Assert.That(Corruption.ExtraDamage(Corruption.Easy), Is.LessThan(0f));
        Assert.That(Corruption.ExtraElites(Corruption.Easy), Is.Zero);
        Assert.That(Corruption.Describe(Corruption.Easy), Does.Not.Contain("corruption."));
        GameSave.ResetForTests();
        Assert.That(Corruption.RegisterClear(Corruption.Easy), Is.EqualTo(-1));
        Assert.That(Corruption.Unlocked, Is.Zero);
        GameSave.ResetForTests();
    }
}
