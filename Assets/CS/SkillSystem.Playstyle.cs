using System.Collections.Generic;
using UnityEngine;

// Stat cards that change how the cat plays rather than a number (CardPool "dash_cut", "execute"):
//   대시 베기 (dash_cut): a dash cuts every monster it passes through, once each per dash, for 40% / 70% of the
//       attack - the dodge becomes a way in, not only a way out, without outdamaging the combo (2-4 dashes in a row).
//   사냥 본능 (execute): a hit that leaves a monster (not the boss) at 15% / 25% of its health or less finishes it -
//       worth switching to the wounded one instead of the fresh one. (Checked in Enemy.ReceiveDamage.)
//   공중 재도약 (air_reset, knight): a melee hit landed in the air gives back an air jump - stay up and juggle.
//   회피 반격 (dodge_strike): the first hit within 1.5 s of a dash deals x1.5 / x2 - dodge in, then punish.
//   현상금 (bounty): an elite killed heals half a heart / a whole one - the elites become worth taking on.
public partial class SkillSystem
{
    readonly HashSet<Enemy> dashCut = new HashSet<Enemy>();
    float dodgeBonusTimer; // 회피 반격: time left for the boosted hit after a dash

    // The share of max health under which a hit finishes the monster (0 = no card).
    public static float ExecuteThreshold(PlayerStats s) { int lv = s != null ? s.CardLevel("execute") : 0; return lv <= 0 ? 0f : lv == 1 ? 0.15f : 0.25f; }

    void UpdateDashCut()
    {
        int lv = Lv("dash_cut");
        if (lv <= 0) return;
        if (!body.IsDashing) { dashCut.Clear(); return; }
        var me = new Bounds(Me, new Vector3(1.4f, 1.4f, 1f));
        int damage = Mathf.Max(1, Mathf.RoundToInt(stats.AttackPower * (lv >= 2 ? 0.7f : 0.4f)));
        foreach (var e in Enemy.All)
        {
            if (e == null || e.IsDead || dashCut.Contains(e)) continue;
            var sr = e.GetComponent<SpriteRenderer>();
            if (sr == null || !sr.bounds.Intersects(me)) continue;
            dashCut.Add(e);
            e.ReceiveDamage(damage, Mathf.Sign(e.transform.position.x - Me.x));
            Vfx.Play("hit_mid", sr.bounds.center);
        }
    }

    void OnPlaystyleMeleeHit()
    {
        if (Has("air_reset") && !body.IsGrounded) body.RefundAirJump();
    }

    void OnPlaystyleDashEnded()
    {
        if (Has("dodge_strike")) dodgeBonusTimer = 1.5f;
    }

    // The damage multiplier for this hit from 회피 반격 (1 when none), used up by the hit.
    public static float ConsumeDodgeBonus(PlayerStats s)
    {
        var sys = Instance;
        if (sys == null || s == null || sys.dodgeBonusTimer <= 0f) return 1f;
        sys.dodgeBonusTimer = 0f;
        return s.CardLevel("dodge_strike") >= 2 ? 2f : 1.5f;
    }

    public static void OnEliteKilled()
    {
        var s = PlayerStats.Instance;
        int lv = s != null ? s.CardLevel("bounty") : 0;
        if (lv > 0) s.Heal(lv >= 2 ? 1f : 0.5f);
    }

    void UpdatePlaystyleTimers(float dt)
    {
        if (dodgeBonusTimer > 0f) dodgeBonusTimer -= dt;
    }
}
