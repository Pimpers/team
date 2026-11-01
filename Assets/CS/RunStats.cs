// Existing public API backed by the explicit RunSession owner.
public static class RunStats
{
    static RunSession S => RunSession.Current;

    public static int Seed => S.Seed;
    public static int Stage { get => S.Stage; set => S.Stage = value; }
    public static float PlayTime { get => S.PlayTime; set => S.PlayTime = value; }
    public static int Kills { get => S.Kills; set => S.Kills = value; }
    public static int EliteKills { get => S.EliteKills; set => S.EliteKills = value; }
    public static long DamageDealt { get => S.DamageDealt; set => S.DamageDealt = value; }
    public static int Rerolls { get => S.Rerolls; set => S.Rerolls = value; }
    public static int GoldSpent { get => S.GoldSpent; set => S.GoldSpent = value; }
    public static int CardDrafts { get => S.CardDrafts; set => S.CardDrafts = value; }
    public static float DamageTaken { get => S.DamageTaken; set => S.DamageTaken = value; }
    public static string LastHitBy { get => S.LastHitBy; set => S.LastHitBy = value ?? ""; }

    public static void RecordDamage(string source, float amount)
    {
        if (amount <= 0f) return;
        DamageTaken += amount;
        if (string.IsNullOrEmpty(source)) source = Loc.T("damage.unknown");
        LastHitBy = source;
        S.DamageBySource[source] = S.DamageBySource.TryGetValue(source, out float total) ? total + amount : amount;
    }

    public static string MostDamageSource
    {
        get
        {
            string source = "";
            float highest = 0f;
            foreach (var pair in S.DamageBySource)
                if (pair.Value > highest) { source = pair.Key; highest = pair.Value; }
            return source;
        }
    }

    public static string DefeatTip
    {
        get
        {
            string source = MostDamageSource;
            if (source.Contains(Loc.T("tip.1.match"))) return Loc.T("tip.1");
            if (source.Contains(Loc.T("tip.2.match"))) return Loc.T("tip.2");
            if (source.Contains(Loc.T("tip.3.match"))) return Loc.T("tip.3");
            if (source.Contains(Loc.T("tip.4.match"))) return Loc.T("tip.4");
            if (source.Contains(Loc.T("tip.5.match"))) return Loc.T("tip.5");
            if (source.Contains(Loc.T("tip.6.match"))) return Loc.T("tip.6");
            return Loc.T("tip.default");
        }
    }

    public static void Reset() => S.ResetStats();

    public static string TimeText
    {
        get
        {
            int seconds = UnityEngine.Mathf.FloorToInt(PlayTime);
            return seconds >= 60 ? Loc.F("time.min_sec", seconds / 60, seconds % 60) : Loc.F("time.sec", seconds);
        }
    }
}
