using System.Collections.Generic;
using UnityEngine;

// Enemy: taking damage, the hit reaction, dying.
public partial class Enemy
{
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
            HitStop.Do(hp <= 0 ? (isBoss ? 0.2f : 0.12f) : finisher ? 0.13f : strong ? 0.09f : 0.05f); // one stop per hit (a second would count as a streak and be cut short)
            HitReaction(strong, finisher);
            DamagePopup.Spawn(new Vector3(b.center.x, BodyTopY, 0f), amount, strong, crit);
            // Hit flash: a pixel star burst - bigger for strong hits, orange for crits.
            if (definition == null || !definition.isBoss)
                Vfx.Play(crit ? "hit_big" : strong ? "hit_mid" : "hit_small", b.center);
            // Shove the view the way the hit flies (more for a strong hit or a crit).
            float kickDir = push != 0f ? Mathf.Sign(push) : (player != null ? Mathf.Sign(Center.x - PlayerCenter.x) : -facing);
            CameraFollow.Kick(new Vector2(kickDir, 0f), finisher ? 0.16f : strong || crit ? 0.1f : 0.04f);
            if (finisher) CameraFollow.Shake(0.14f, 0.12f);
        }
        if (!quiet && definition != null && !definition.superArmor)
        {
            float dir = push != 0f ? Mathf.Sign(push) : (player != null ? Mathf.Sign(Center.x - PlayerCenter.x) : -facing);
            knockVelocity = dir * KnockSpeed * Mathf.Max(1f, Mathf.Abs(push)) * (finisher ? 1.6f : 1f);
            if (finisher && hp > 0 && definition.scale < 1f) { hopHeight = 0.35f; hopTimer = HopTime; } // small ones are popped up
        }

        if (hp <= 0)
        {
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
                    hopHeight = 0.45f;
                    hopTimer = HopTime;
                }
            }
            if (definition != null && !string.IsNullOrEmpty(definition.deathSound)) Sfx.Play(definition.deathSound, 1f, isBoss ? 0.7f : 1f);
            if (definition != null)
                PlayEffects(definition.deathEffect, b.center, isBoss ? 0.65f : definition.deathEffectScale);
            RunStats.Kills++;
            if (IsElite) RunStats.EliteKills++; // elites already pay x4 gold; cards come only from the clear chest
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
