using System.Collections.Generic;
using UnityEngine;

// Enemy: taking damage, the hit reaction, dying.
public partial class Enemy
{
    bool raged;

    // ---------------------------------------------------------------- damage
    // Called only by the player's attacks - bumping into an enemy never calls this.
    // `push` = knockback direction and strength (sign = which way, 1 = a normal hit);
    // 0 pushes away from the player.
    // `quiet` = a damage-over-time tick (poison): just a coloured number, no hit-stop / knockback / flinch.
    // finisher: the last hit of the player's combo - a longer stop, a harder shove, a bigger squash.
    public void ReceiveDamage(int amount, float push = 0f, bool strong = false, bool crit = false,
                              bool quiet = false, Color? quietColor = null, bool finisher = false)
    {
        if (IsDead) return;
        if (!quiet) { verticalHop = false; verticalHopWindup = 0f; }
        // 사냥 세트: whoever is stunned, lured or poisoned takes more from the player's hits (not from poison ticks).
        if (!quiet && PlayerStats.Instance != null && (IsStunned || IsLured || IsPoisoned))
            amount = Mathf.Max(1, Mathf.RoundToInt(amount * PlayerStats.Instance.StatusDamageMultiplier));
        if (damageTakenMultiplier != 1f) amount = Mathf.Max(1, Mathf.RoundToInt(amount * damageTakenMultiplier));
        RunStats.DamageDealt += Mathf.Min(amount, Mathf.Max(0, hp)); // (overkill doesn't count)
        hp -= amount;
        // 타락도 3+: an elite brought to half health rages - faster on its feet and quicker to strike again.
        if (IsElite && !raged && hp > 0 && hp <= MaxHP / 2 && Corruption.EliteRage(Corruption.Level))
        {
            raged = true;
            speedMultiplier *= 1.3f;
            cooldownMultiplier *= 0.7f;
            DamagePopup.SpawnLabel(new Vector3(transform.position.x, BodyTopY + 0.7f, 0f), Loc.T("enemy.rage"), new Color(1f, 0.35f, 0.25f));
        }
        // 사냥 본능 (execute card): a hit that leaves a monster nearly dead finishes it (never the boss).
        if (!quiet && !isBoss && hp > 0 && hp <= MaxHP * SkillSystem.ExecuteThreshold(PlayerStats.Instance))
        {
            hp = 0; // (a kill's own stop and shake follow; no crit weight on top - in a crowd that stuttered)
            DamagePopup.SpawnLabel(new Vector3(transform.position.x, BodyTopY + 0.4f, 0f), Loc.T("skill.execute"), new Color(1f, 0.4f, 0.3f));
        }
        // A health bar under the feet from the first hit on (the boss has its own at the top of the screen).
        if (hpBar == null && !isBoss && definition != null && hp > 0) hpBar = EnemyHPBar.Attach(this);
        Bounds b = sr != null ? sr.bounds : new Bounds(transform.position, Vector3.one);
        if (quiet)
        {
            DamagePopup.SpawnLabel(new Vector3(b.center.x, BodyTopY, 0f), amount.ToString(), quietColor ?? Color.white);
        }
        else
        {
            Sfx.Play(strong || crit ? "hit_strong" : "hit", 1f, crit ? 1.15f : 1f);
            // Hit feel: a tiny freeze, a damage number, and a knockback slide.
            // Graded by weight: kill > crit + finisher > finisher > crit > strong > a plain hit (a crit used to stop as
            // short as a plain hit and only showed in its number).
            HitStop.Do(hp <= 0 ? (isBoss ? 0.2f : crit ? 0.15f : 0.12f) : finisher ? (crit ? 0.16f : 0.13f) : crit ? 0.1f : strong ? 0.09f : 0.05f); // one stop per hit (a second would count as a streak and be cut short)
            HitReaction(strong, finisher);
            DamagePopup.Spawn(new Vector3(b.center.x, BodyTopY, 0f), amount, strong, crit);
            // Hit flash: a pixel star burst - bigger for strong hits, orange for crits.
            if (definition == null || !definition.isBoss)
                Vfx.Play(crit ? "hit_big" : strong ? "hit_mid" : "hit_small", b.center);
            // Shove the view the way the hit flies (more for a strong hit or a crit).
            float kickDir = push != 0f ? Mathf.Sign(push) : (player != null ? Mathf.Sign(Center.x - PlayerCenter.x) : -facing);
            CameraFollow.Kick(new Vector2(kickDir, 0f), finisher ? 0.16f : strong || crit ? 0.1f : 0.04f);
            if (finisher) CameraFollow.Shake(crit ? 0.2f : 0.14f, crit ? 0.15f : 0.12f);
            else if (crit) CameraFollow.Shake(0.08f, 0.08f);
        }
        if (!quiet && definition != null && !definition.superArmor)
        {
            float dir = push != 0f ? Mathf.Sign(push) : (player != null ? Mathf.Sign(Center.x - PlayerCenter.x) : -facing);
            knockVelocity = dir * KnockSpeed * Mathf.Max(1f, Mathf.Abs(push)) * (finisher ? 1.6f : 1f);
            if (finisher && hp > 0 && definition.scale < 1f) { hopHeight = 0.35f; hopTimer = HopTime; } // small ones are popped up
        }

        // 발톱 세트 완성: a critical hit dazes a monster for a moment (not the boss - it has its own stagger).
        if (crit && !quiet && !isBoss && hp > 0 && PlayerStats.Instance != null && PlayerStats.Instance.CritStuns) Stun(0.5f);

        if (hp <= 0)
        {
            if (!isBoss && Corruption.DeathBurst(Corruption.Level)) CorruptionBurst.Spawn(b.center); // 타락도 4+
            // 사냥 세트 완성: a dazed, lured or poisoned monster's death poisons the ones near it (and theirs the next).
            if ((IsStunned || IsLured || IsPoisoned) && PlayerStats.Instance != null && PlayerStats.Instance.HuntChain)
                foreach (var e in All)
                    if (e != null && e != this && !e.IsDead && Vector2.Distance(e.transform.position, transform.position) < 3f) e.Poison(1, 2f);
            // The killing blow lands harder: a longer freeze, and it is flung away with a hop; the boss's last
            // hit is a held freeze, then half a second of slow motion.
            if (!quiet)
            {
                float away = push != 0f ? Mathf.Sign(push) : (player != null ? Mathf.Sign(Center.x - PlayerCenter.x) : -facing);
                if (isBoss)
                {
                    HitStop.Slow(0.7f, 0.3f);
                    CameraFollow.Shake(0.45f, 0.4f);
                }
                else
                {
                    knockVelocity = away * KnockSpeed * 1.8f;
                    CameraFollow.Shake(finisher || crit ? 0.16f : 0.1f, 0.1f); // a kill thumps, a finishing or critical one harder
                    hopHeight = 0.45f;
                    hopTimer = HopTime;
                }
            }
            if (definition != null && !string.IsNullOrEmpty(definition.deathSound)) Sfx.Play(definition.deathSound, 1f, isBoss ? 0.7f : 1f);
            if (definition != null)
                PlayEffects(definition.deathEffect, b.center, isBoss ? 0.5f : definition.deathEffectScale); // 0.5: the pixel-art explosion only draws at x0.5 / x1
            RunStats.Kills++;
            if (IsElite) RunStats.EliteKills++; // elites already pay x4 gold; cards come only from the clear chest
            if (IsElite) SkillSystem.OnEliteKilled(); // 현상금 card
            if (isBoss)
                RunManager.Instance.OnBossDefeated(goldReward);
            else
                RunManager.Instance.OnMonsterDefeated(goldReward);

            if (definition == null) { Destroy(gameObject); return; }
            ClearStatus();
            if (eliteTag != null) eliteTag.SetActive(false);
            sr.color = Color.white;
            flashTimer = 0f;
            deadTimer = 0f;
            Play(State.Dead);
            return;
        }

        // A hit wakes it: it hunts the cat down even from outside its sight range (no free sniping).
        if (definition != null && !quiet) { alertTimer = Mathf.Max(alertTimer, definition.alertTime); wasAlerted = true; reactTimer = 0f; provoked = 4f; }
        if (definition == null) return;
        flashTimer = 0.12f;
        if (!quiet && !definition.superArmor)
        {
            hurtTimer = 0.3f;
            Play(State.Hurt);
        }
    }

}
