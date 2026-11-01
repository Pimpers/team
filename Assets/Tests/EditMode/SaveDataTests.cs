using NUnit.Framework;

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
    }
}
