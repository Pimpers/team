using UnityEngine;

// Permanent wallet and upgrades stored by GameSave.
public static class MetaProgress
{
    public const string AttackKey = "meta_attack_level";
    public const string SpeedKey = "meta_speed_level";
    public const string HPKey = "meta_hp_level";

    // Attack / Speed / Heart make the cat stronger; Reroll (행운의 방울) is a different kind of goal: free card
    // rerolls each run, so later runs shape the build more instead of only hitting harder.
    // Starter (든든한 출발): a card draft before the first fight - somewhere for gold to go once the rest is bought.
    public enum Upgrade { Attack, Speed, Heart, Reroll, Starter }

    public static int Gold => GameSave.Data.gold;

    public static void AddGold(int amount)
    {
        if (amount <= 0) return;
        GameSave.Data.gold += amount;
        GameSave.Save();
    }

    public static int MaxLevel(Upgrade upgrade) =>
        upgrade == Upgrade.Attack ? 3 : upgrade == Upgrade.Speed ? 3 : upgrade == Upgrade.Reroll ? 2 : upgrade == Upgrade.Starter ? 1 : 5;
    // One curve for every upgrade: 10, 20, 30... (the next level costs 10 more; all five hearts 150 in all). A lost run
    // brings ~10-20 gold, so each first level comes within a run and the last ones take a few good runs.
    const int CostStep = 20; // all upgrades 600: about four wins (at 10 two wins bought everything)
    static int BaseCost(Upgrade upgrade) => upgrade == Upgrade.Starter ? 300 : CostStep;

    public static int Level(Upgrade upgrade)
    {
        int value = upgrade == Upgrade.Attack ? GameSave.Data.attackLevel
            : upgrade == Upgrade.Speed ? GameSave.Data.speedLevel
            : upgrade == Upgrade.Reroll ? GameSave.Data.rerollLevel
            : upgrade == Upgrade.Starter ? GameSave.Data.starterLevel : GameSave.Data.hpLevel;
        return Mathf.Clamp(value, 0, MaxLevel(upgrade));
    }

    static void SetLevel(Upgrade upgrade, int value)
    {
        value = Mathf.Clamp(value, 0, MaxLevel(upgrade));
        if (upgrade == Upgrade.Attack) GameSave.Data.attackLevel = value;
        else if (upgrade == Upgrade.Speed) GameSave.Data.speedLevel = value;
        else if (upgrade == Upgrade.Reroll) GameSave.Data.rerollLevel = value;
        else if (upgrade == Upgrade.Starter) GameSave.Data.starterLevel = value;
        else GameSave.Data.hpLevel = value;
    }

    public static bool IsMaxed(Upgrade upgrade) => Level(upgrade) >= MaxLevel(upgrade);
    public static int Cost(Upgrade upgrade) => BaseCost(upgrade) * (Level(upgrade) + 1);

    public static bool TryBuy(Upgrade upgrade)
    {
        if (IsMaxed(upgrade)) return false;
        int cost = Cost(upgrade);
        if (Gold < cost) return false;
        GameSave.Data.gold -= cost;
        SetLevel(upgrade, Level(upgrade) + 1);
        GameSave.Save();
        return true;
    }
}
