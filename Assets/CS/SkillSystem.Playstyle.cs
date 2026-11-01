using System.Collections.Generic;
using UnityEngine;

// Stat cards that change how the cat plays rather than a number (CardPool "dash_cut", "execute"):
//   대시 베기 (dash_cut): a dash cuts every monster it passes through, once each per dash - the dodge becomes
//       a way in, not only a way out.
//   사냥 본능 (execute): a hit that leaves a monster (not the boss) at 15% / 25% of its health or less finishes it -
//       worth switching to the wounded one instead of the fresh one. (Checked in Enemy.ReceiveDamage.)
public partial class SkillSystem
{
    readonly HashSet<Enemy> dashCut = new HashSet<Enemy>();

    // The share of max health under which a hit finishes the monster (0 = no card).
    public static float ExecuteThreshold(PlayerStats s) { int lv = s != null ? s.CardLevel("execute") : 0; return lv <= 0 ? 0f : lv == 1 ? 0.15f : 0.25f; }

    void UpdateDashCut()
    {
        int lv = Lv("dash_cut");
        if (lv <= 0) return;
        if (!body.IsDashing) { dashCut.Clear(); return; }
        var me = new Bounds(Me, new Vector3(1.4f, 1.4f, 1f));
        int damage = Mathf.Max(1, Mathf.RoundToInt(stats.AttackPower * (lv >= 2 ? 1f : 0.6f)));
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
}
