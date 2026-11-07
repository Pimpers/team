using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Passive skills and the enhancements of basic moves (SkillSystem).
public partial class SkillSystem
{
    // ================================================================== passives & enhancements
    void UpdatePassives(float dt)
    {
        // 식빵 자세: the shield comes back after a while; a faint shimmer while it's up.
        if (Has("k_loaf"))
        {
            if (!shieldUp && (shieldTimer -= dt) <= 0f)
            {
                shieldUp = true;
                Vfx.Play("k_loaf_fx", Me, 1f, parent: transform);
                Sfx.Play("ui_open", 0.5f, 1.6f);
            }
            if (shieldUp && (shieldGlowTimer -= dt) <= 0f)
            {
                shieldGlowTimer = 2.5f;
                Vfx.Play("ring_small", Me, 1f, tint: new Color(0.6f, 0.95f, 1f), parent: transform);
            }
        }

        // 그루밍: a quiet stretch without being hit licks half a heart back.
        if (Has("k_groom"))
        {
            groomTimer += dt;
            if (groomTimer >= GroomTime)
            {
                groomTimer = 0f;
                if (stats.CurrentHP < stats.MaxHP)
                {
                    stats.Heal(0.5f);
                    Vfx.Play("k_groom_fx", Head, 1f, parent: transform);
                    Sfx.Play("fx_groom");
                    SkillFx.Burst(Vfx.Sprite("heart"), Head, 2, 1.5f, 0.7f, SkillFx.PropScale * 0.5f, -1f);
                    DamagePopup.SpawnLabel(Head, Loc.T("skill.groom"), new Color(1f, 0.7f, 0.85f));
                }
            }
        }

        if (counterTimer > 0f) counterTimer -= dt;

        // 야간 시야: the eyes glint now and then.
        if (Has("k_night") && (glintTimer -= dt) <= 0f)
        {
            glintTimer = 3.5f;
            var eye = Me + new Vector3(Facing * 0.3f, body.bodyHeight * 0.22f, 0f);
            Vfx.Play("k_night_fx", eye, 1f, parent: transform);
        }

        // 날카로운 솜방망이: golden motes around the paw.
        if (Has("k_paw") && (auraTimer -= dt) <= 0f)
        {
            auraTimer = 0.3f;
            var paw = Me + new Vector3(Facing * Random.Range(0.3f, 0.7f), Random.Range(-0.3f, 0.3f), 0f);
            Vfx.Play("spark_mid1", paw, 0.45f, tint: new Color(1f, 0.85f, 0.4f, 0.7f));
        }
    }

    // PlayerStats.TakeDamage: true = the hit is eaten by a skill (counter first, then the shield).
    public bool TryBlockHit()
    {
        if (Has("k_whisker") && counterTimer <= 0f)
        {
            var target = NearestEnemy(Me, 7f, null);
            if (target != null)
            {
                counterTimer = CounterTime;
                invulnTimer = Mathf.Max(invulnTimer, 0.7f);
                StopCast();
                if (attack != null) attack.CancelCombo();
                StartCast(Counter(target));
                return true;
            }
        }
        if (Has("k_loaf") && shieldUp)
        {
            shieldUp = false;
            shieldTimer = ShieldTime;
            invulnTimer = Mathf.Max(invulnTimer, 0.4f);
            Vfx.Play("k_loaf_fx", Me, 1f);
            Vfx.Play("hit_mid", Me + Vector3.right * Facing * 0.5f, 1f, tint: new Color(0.7f, 0.95f, 1f)); // the blow glances off
            Sfx.Play("hit_strong", 0.8f, 1.5f);
            DamagePopup.SpawnLabel(Head, Loc.T("skill.blocked"), new Color(0.6f, 0.95f, 1f));
            return true;
        }
        return false;
    }

    // PlayerAttack: a basic sword swing (after its normal slash effect).
    public void OnMeleeSwing(Vector3 slashAt, float size, bool flipX, float rotationZ)
    {
        if (!Has("k_paw")) return;
        // (the swing's smear itself turns gold in PlayerAttack; k_paw_fx on top of it doubled the arc)
        Vfx.Play("aura_hit", slashAt + new Vector3((flipX ? -1f : 1f) * size * 0.5f, 0f, 0f), 0.6f);
    }

    public void OnMeleeHit(Enemy e)
    {
        if (Has("k_paw") && e.Renderer != null) Vfx.Play("spark_mid2", e.Renderer.bounds.center, 0.8f, tint: Gold);
        OnPlaystyleMeleeHit();
    }

    // PlayerAttack: the jump attack landed (캣타워 점프 찍기).
    public void OnJumpAttackImpact()
    {
        int lv = Lv("k_tower");
        if (lv <= 0) return;
        float size = lv >= 2 ? 1.25f : 1f;
        Vector3 at = Feet;
        Vfx.Play("k_tower_fx", at, 1f);
        Vfx.Play("rupture", at + Vector3.right * 1.2f * size, lv >= 2 ? 1f : 0.5f); // whole steps: 0.7 x 1.25 jumped from x0.5 to x1
        Vfx.Play("rupture", at - Vector3.right * 1.2f * size, lv >= 2 ? 1f : 0.5f, flipX: true);
        Vfx.Play("dust_big", at, 1.6f * size);
        Sfx.Play("slam", 1f, 0.8f);
        CameraFollow.Shake(0.3f, 0.2f);
        HitCircle(at + Vector3.up * 0.5f, 3f * size, (lv >= 2 ? 1.8f : 1.2f), after: e => e.Stun(0.6f));
    }

    // 냥이의 도약: afterimages and smoke on every dash, a smoke burst at the end.
    void OnDashStarted(float dir)
    {
        if (!Has("k_leap")) return;
        Vfx.Play("k_leap_fx", Feet + Vector3.up * 0.4f, 1f, flipX: dir < 0f);
        StartCoroutine(Trail(body.dashDuration, Orange, 0.025f));
    }

    void OnDashEnded()
    {
        OnPlaystyleDashEnded();
        int lv = Lv("k_leap");
        if (lv <= 0) return;
        Vfx.Play("k_leap_fx", Me, 1f);
        HitCircle(Me, 1.6f, lv >= 2 ? 1.2f : 0.6f, strong: false);
    }
}
