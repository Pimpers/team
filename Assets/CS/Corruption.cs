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
    public static float HpMultiplier(int lv) => lv < 0 ? 0.7f : 1f + 0.15f * lv;  // +15% per tier (was +30%: a tier mostly made fights longer), Easy -30%
    public static float SpeedMultiplier(int lv) => lv < 0 ? 1f : 1f + 0.06f * lv; // +6% per tier
    public static float ExtraDamage(int lv) => lv >= 4 ? 1f : lv >= 2 ? 0.5f : lv < 0 ? -0.5f : 0f; // hearts per hit (Easy: half less)
    public static int ExtraElites(int lv) => Mathf.Max(0, (lv + 1) / 2);         // +1 at 1, +2 at 3, +3 at 5
    public static bool EliteRage(int lv) => lv >= 3; // a rule, not a number: an elite at half health turns faster and strikes sooner (Enemy.ReceiveDamage)
    public static float GoldMultiplier(int lv) => lv < 0 ? 1f : 1f + 0.25f * lv;  // +25% per tier
    // More rules (each tier keeps the ones below it): the tiers used to differ only by their numbers.
    public static bool DoubleTraps(int lv) => lv >= 1;             // twice the traps in stages 2-4 (TrapHazard)
    public static float ClearHeal(int lv) => lv >= 2 ? 0.5f : 1f;  // the heal for clearing a stage, halved (RunManager)
    public static bool DeathBurst(int lv) => lv >= 4;              // a dead monster bursts a moment later (CorruptionBurst)
    public static bool BossPhaseTwo(int lv) => lv >= 5;            // the boss opens in its second phase (BossBrain)

    // The rules a tier turns on, on one line (the Tab sheet shows it mid-run; Describe has the numbers too).
    public static string Rules(int lv)
    {
        var r = new System.Collections.Generic.List<string>();
        if (DoubleTraps(lv)) r.Add(Loc.T("corruption.traps").Trim());
        if (ClearHeal(lv) < 1f) r.Add(Loc.T("corruption.clear_heal").Trim());
        if (EliteRage(lv)) r.Add(Loc.T("corruption.elite_rage").Trim());
        if (DeathBurst(lv)) r.Add(Loc.T("corruption.death_burst").Trim());
        if (BossPhaseTwo(lv)) r.Add(Loc.T("corruption.boss_phase").Trim());
        return string.Join(" · ", r);
    }

    // Shown on the world map selector and the stage HUD.
    public static string Describe(int lv)
    {
        if (lv < 0) return Loc.T("corruption.easy");
        if (lv == 0) return Loc.T("corruption.none");
        string s = Loc.F("corruption.enemy_hp_speed", 15 * lv, 6 * lv);
        float dmg = ExtraDamage(lv);
        if (dmg > 0f) s += Loc.T(dmg >= 1f ? "corruption.enemy_damage_full" : "corruption.enemy_damage_half");
        s += Loc.F("corruption.elites", ExtraElites(lv));
        if (DoubleTraps(lv)) s += Loc.T("corruption.traps");
        if (ClearHeal(lv) < 1f) s += Loc.T("corruption.clear_heal");
        if (EliteRage(lv)) s += Loc.T("corruption.elite_rage");
        if (DeathBurst(lv)) s += Loc.T("corruption.death_burst");
        if (BossPhaseTwo(lv)) s += Loc.T("corruption.boss_phase");
        s += Loc.F("corruption.gold", GoldMultiplier(lv).ToString("0.##"), 25 * lv); // the reward, as a number next to the cost
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

// 타락도 4+: where a monster died, a "!" and - Delay later - a burst that takes half a heart from a cat still within
// Radius. Step away after a kill; killing in a crowd at point-blank costs.
public class CorruptionBurst : MonoBehaviour
{
    const float Delay = 0.8f, Radius = 1.6f;
    float timer = Delay;

    public static void Spawn(Vector3 at)
    {
        var go = new GameObject("CorruptionBurst");
        go.transform.position = at;
        go.AddComponent<CorruptionBurst>();
        DamagePopup.SpawnLabel(at + Vector3.up * 0.8f, "!", new Color(0.85f, 0.35f, 0.95f));
        Vfx.Play("blink", at, 0.6f);
    }

    void Update()
    {
        if ((timer -= Time.deltaTime) > 0f) return;
        Vfx.Play("explosion", transform.position, 1f);
        Sfx.Play("slam", 0.6f, 1.4f);
        var cat = PlayerStats.Instance;
        if (cat != null && Vector2.Distance(cat.transform.position, transform.position) < Radius)
            cat.TakeDamage(0.5f, Loc.T("damage.corruption_burst"));
        Destroy(gameObject);
    }
}

// Monsters get tougher the deeper the run goes (the boss is tuned directly in EnemyAssets):
// +20% HP per stage after the first, read from the scene name (Stage1..Stage5; the tutorial is 1).
public static class StageScaling
{
    public static float HpMultiplier(bool boss) => HpMultiplier(CurrentStage(), boss);

    // +12% a stage after the first (was +20%: stage 4 took ~9 basic hits a monster); the boss keeps its own numbers.
    // (stops growing after stage 3: stage 4 took 113 hits for 14 monsters and no more damage than stage 3 - a
    // sponge, not a step up; its step up is an extra elite, StageRandomizer)
    public static float HpMultiplier(int stage, bool boss) => boss || stage < 1 ? 1f : 1f + 0.18f * Mathf.Clamp(stage - 1, 0, 3); // 1 / 1.18 / 1.36 / 1.54 (was +12% to stage 3: the forge's attack alone is +75%, and stage 4 was no tougher than 3)

    // The stage being played (0 outside the stages), from the scene's RunManager - not parsed out of the scene's
    // name, so renaming a scene can't quietly change the balance. Works in Awake too, before RunManager's own Awake.
    public static int CurrentStage()
    {
        var run = RunManager.Instance != null ? RunManager.Instance : Object.FindAnyObjectByType<RunManager>();
        return run != null && !run.tutorial ? run.stageNumber : 0;
    }
}
