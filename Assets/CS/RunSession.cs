using System;
using System.Collections.Generic;
using UnityEngine;

// Explicit owner for one run's cross-scene state. It replaces scattered static data
// while keeping the existing RunStats and RouteProgress APIs as thin facades.
public sealed class RunSession : MonoBehaviour
{
    static RunSession instance;
    public static RunSession Current
    {
        get
        {
            if (instance != null) return instance;
            instance = FindAnyObjectByType<RunSession>();
            if (instance != null) return instance;
            var go = new GameObject("RunSession");
            if (Application.isPlaying) DontDestroyOnLoad(go);
            else go.hideFlags = HideFlags.DontSave;
            instance = go.AddComponent<RunSession>();
            return instance;
        }
    }

    public bool HasCarry;
    public readonly Dictionary<string, int> CarryCards = new Dictionary<string, int>();
    public readonly List<string> CarrySkillOrder = new List<string>();
    public int CarryGold;
    public float CarryHP;

    public int Seed;
    System.Random random;
    public int Stage;
    public float PlayTime;
    public int Kills;
    public int EliteKills;
    public long DamageDealt;
    public int Rerolls;
    public int GoldSpent;
    public int CardDrafts;
    public float DamageTaken;
    public string LastHitBy = "";
    public readonly Dictionary<string, float> DamageBySource = new Dictionary<string, float>();

    public int SafeChoices;
    public int HuntChoices;
    public int RareDraftCharges;
    public bool HasNextRoute;
    public bool NextRouteHunt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() => instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnsureCreated() { _ = Current; }

    public void ResetStats()
    {
        Seed = RequestedSeed();
        random = new System.Random(Seed);
        UnityEngine.Random.InitState(Seed);
        Stage = 0;
        PlayTime = 0f;
        Kills = 0;
        EliteKills = 0;
        DamageDealt = 0;
        Rerolls = 0;
        GoldSpent = 0;
        CardDrafts = 0;
        DamageTaken = 0f;
        LastHitBy = "";
        DamageBySource.Clear();
    }

    public float NextFloat()
    {
        if (random == null) random = new System.Random(Seed == 0 ? 1 : Seed);
        return (float)random.NextDouble();
    }

    public int Range(int minimum, int maximum)
    {
        if (maximum <= minimum) return minimum;
        if (random == null) random = new System.Random(Seed == 0 ? 1 : Seed);
        return random.Next(minimum, maximum);
    }

    public float Range(float minimum, float maximum) => Mathf.Lerp(minimum, maximum, NextFloat());

    public void ResetRoutes()
    {
        SafeChoices = 0;
        HuntChoices = 0;
        RareDraftCharges = 0;
        HasNextRoute = false;
        NextRouteHunt = false;
    }

    public void ClearCarry()
    {
        HasCarry = false;
        CarryCards.Clear();
        CarrySkillOrder.Clear();
        CarryGold = 0;
        CarryHP = 0f;
    }

    static int RequestedSeed()
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-runSeed" && int.TryParse(args[i + 1], out int seed)) return seed;
        // Non-negative, so the 런 씨앗 on the result screen reads as a plain number (and passes back to -runSeed as is).
        return unchecked((int)DateTime.UtcNow.Ticks ^ Environment.TickCount) & int.MaxValue;
    }
}
