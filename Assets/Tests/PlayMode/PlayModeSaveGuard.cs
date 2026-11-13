using NUnit.Framework;

// Runs once before any PlayMode test: the game then works on a throwaway save and never writes the player's real
// file (a test that banks gold or buys an upgrade would otherwise change it).
[SetUpFixture]
public class PlayModeSaveGuard
{
    [OneTimeSetUp]
    public void IsolateSave()
    {
        GameSave.ResetForTests(new SaveData { tutorialDone = true });
        StageExit.AutoEnter = true; // the tests set the cat down at a log's mouth; they hold no key
    }
}
