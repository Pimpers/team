using UnityEngine;

// 타락도 (corruption) - difficulty tiers for replaying the chapter after it's beaten.
// Clearing the boss at the highest unlocked tier unlocks the next one (up to MaxLevel);
// the tier is picked on the world map before setting sail and applies to the whole run:
// tougher, faster, harder-hitting monsters, more elites - and more gold to make it worth it.
// Saved in PlayerPrefs next to MetaProgress.
public static class Corruption
{
    public const int MaxLevel = 5;
    public const int Easy = -1; // 편하게: below the normal tier, always open - softer enemy hits and less HP, unlocks nothing

    public static int Unlocked => Mathf.Clamp(GameSave.Data.corruptionUnlocked, 0, MaxLevel);

    // The tier the next / current run is played at (0 = normal, Easy = -1).
    public static int Level
    {
        get => Mathf.Clamp(GameSave.Data.corruptionSelected, Easy, Unlocked);
        set
        {
            GameSave.Data.corruptionSelected = Mathf.Clamp(value, Easy, Unlocked);
            GameSave.Save();
        }
    }

    // Per-tier modifiers (level 0 = all neutral).
    public static float HpMultiplier(int lv) => lv < 0 ? 0.7f : 1f + 0.3f * lv;   // +30% per tier, Easy -30%
    public static float SpeedMultiplier(int lv) => lv < 0 ? 1f : 1f + 0.06f * lv; // +6% per tier
    public static float ExtraDamage(int lv) => lv >= 4 ? 1f : lv >= 2 ? 0.5f : lv < 0 ? -0.5f : 0f; // hearts per hit (Easy: half less)
    public static int ExtraElites(int lv) => Mathf.Max(0, (lv + 1) / 2);         // +1 at 1, +2 at 3, +3 at 5
    public static float GoldMultiplier(int lv) => lv < 0 ? 1f : 1f + 0.25f * lv;  // +25% per tier

    // Shown on the world map selector and the stage HUD.
    public static string Describe(int lv)
    {
        if (lv < 0) return Loc.T("corruption.easy");
        if (lv == 0) return Loc.T("corruption.none");
        string s = Loc.F("corruption.enemy_hp_speed", 30 * lv, 6 * lv);
        float dmg = ExtraDamage(lv);
        if (dmg > 0f) s += Loc.T(dmg >= 1f ? "corruption.enemy_damage_full" : "corruption.enemy_damage_half");
        s += Loc.F("corruption.elites", ExtraElites(lv));
        s += Loc.F("corruption.gold", 25 * lv);
        return s;
    }

    // Applies the current tier to a freshly set-up enemy (called from Enemy.Awake).
    public static void ApplyTo(Enemy e)
    {
        int lv = Level;
        if (lv == 0 || e.definition == null) return;
        e.hp = Mathf.Max(1, Mathf.CeilToInt(e.hp * HpMultiplier(lv)));
        // Easy takes half a heart off a hit, but a hit never drops below half a heart (nor rises when it was less).
        e.contactDamage = Mathf.Max(Mathf.Min(e.contactDamage, 0.5f), e.contactDamage + ExtraDamage(lv));
        e.speedMultiplier *= SpeedMultiplier(lv);
        e.goldReward = Mathf.Max(e.goldReward + 1, Mathf.RoundToInt(e.goldReward * GoldMultiplier(lv)));
    }

    // Boss beaten at `lv`: unlock the next tier if this was the highest one. Returns the newly
    // unlocked tier, or -1.
    public static int RegisterClear(int lv)
    {
        if (lv < 0 || lv < Unlocked || Unlocked >= MaxLevel) return -1;
        int next = Unlocked + 1;
        GameSave.Data.corruptionUnlocked = next;
        GameSave.Save();
        return next;
    }
}

// Monsters get tougher the deeper the run goes (the boss is tuned directly in EnemyAssets):
// +20% HP per stage after the first, read from the scene name (Stage1..Stage5; the tutorial is 1).
public static class StageScaling
{
    public static float HpMultiplier(bool boss) => HpMultiplier(CurrentStage(), boss);

    // +12% a stage after the first (was +20%: stage 4 took ~9 basic hits a monster); the boss keeps its own numbers.
    public static float HpMultiplier(int stage, bool boss) => boss || stage < 1 ? 1f : 1f + 0.12f * Mathf.Clamp(stage - 1, 0, 4);

    // The stage being played (0 outside the stages), from the scene's RunManager - not parsed out of the scene's
    // name, so renaming a scene can't quietly change the balance. Works in Awake too, before RunManager's own Awake.
    public static int CurrentStage()
    {
        var run = RunManager.Instance != null ? RunManager.Instance : Object.FindAnyObjectByType<RunManager>();
        return run != null && !run.tutorial ? run.stageNumber : 0;
    }
}
