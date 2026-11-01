using UnityEngine;

// Permanent wallet and upgrades stored by GameSave.
public static class MetaProgress
{
    public const string AttackKey = "meta_attack_level";
    public const string SpeedKey = "meta_speed_level";
    public const string HPKey = "meta_hp_level";

    // Attack / Speed / Heart make the cat stronger; Reroll (행운의 방울) is a different kind of goal: free card
    // rerolls each run, so later runs shape the build more instead of only hitting harder.
    public enum Upgrade { Attack, Speed, Heart, Reroll }

    public static int Gold => GameSave.Data.gold;

    public static void AddGold(int amount)
    {
        if (amount <= 0) return;
        GameSave.Data.gold += amount;
        GameSave.Save();
    }

    public static int MaxLevel(Upgrade upgrade) =>
        upgrade == Upgrade.Attack ? 3 : upgrade == Upgrade.Speed ? 3 : upgrade == Upgrade.Reroll ? 2 : 5;
    static int BaseCost(Upgrade upgrade) =>
        upgrade == Upgrade.Attack ? 15 : upgrade == Upgrade.Speed ? 12 : upgrade == Upgrade.Reroll ? 20 : 8; // a lost run brings ~10-20 gold: the first upgrades come within a run or two

    public static int Level(Upgrade upgrade)
    {
        int value = upgrade == Upgrade.Attack ? GameSave.Data.attackLevel
            : upgrade == Upgrade.Speed ? GameSave.Data.speedLevel
            : upgrade == Upgrade.Reroll ? GameSave.Data.rerollLevel : GameSave.Data.hpLevel;
        return Mathf.Clamp(value, 0, MaxLevel(upgrade));
    }

    static void SetLevel(Upgrade upgrade, int value)
    {
        value = Mathf.Clamp(value, 0, MaxLevel(upgrade));
        if (upgrade == Upgrade.Attack) GameSave.Data.attackLevel = value;
        else if (upgrade == Upgrade.Speed) GameSave.Data.speedLevel = value;
        else if (upgrade == Upgrade.Reroll) GameSave.Data.rerollLevel = value;
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
