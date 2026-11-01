using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// The knight's active skills (SkillSystem: helpers, cooldowns and dispatch are in SkillSystem.cs).
public partial class SkillSystem
{
    // ================================================================== 검사 actives
    // 냥냥 연속 베기: three quick chops forward, each a slash.
    IEnumerator NyangCombo()
    {
        body.skillLock = true;
        body.FaceTowards(MoveDir());
        for (int i = 0; i < 3; i++)
        {
            float w = Windup(i, 1.8f);
            if (w > 0f) yield return new WaitForSeconds(w);
            Sfx.Play("swing", 1f, 1f + 0.1f * i);
            body.TryMove(Facing * 0.45f);
            const float reach = 2.4f;
            Vfx.Play("combo_slash" + (i + 1), new Vector3(Me.x + Facing * reach * 0.45f, Me.y + 0.05f, 0f), 1f,
                     flipX: Facing < 0f);
            HitFront(reach, 1.8f * Power("k_combo"), after: e => Vfx.Play("spark_mid1", e.Renderer.bounds.center, 0.8f));
            yield return new WaitForSeconds(Mathf.Max(0.05f, 0.13f - w));
        }
        yield return new WaitForSeconds(0.1f);
    }

    // 하이점프 상단 베기: spring up, then one big rising slash over the head. The swing starts as the jump slows, and
    // its arc and damage come on the swing's strike frame (riding up with the cat) - one slash, not a swing on the
    // way up and the arc a beat later on its own.
    IEnumerator HighCut()
    {
        body.skillLock = true;
        body.FaceTowards(MoveDir());
        Vfx.Play("land_dust", Feet, 1f);
        Sfx.Play("jump", 1f, 0.9f);
        invulnTimer = Mathf.Max(invulnTimer, 0.4f);
        body.Launch(17f);
        if (body.jumpFrames != null && body.jumpFrames.Length > 0) body.PlayAttack(new[] { body.jumpFrames[body.jumpFrames.Length / 2] }, 1f / 0.12f);
        yield return new WaitForSeconds(0.12f);
        float w = Windup(1, 1.2f);
        if (w > 0f) yield return new WaitForSeconds(w);
        Sfx.Play("swing", 1f, 0.8f);
        Vfx.Play("k_highcut_fx", new Vector3(Me.x + Facing * 0.9f, Me.y + 0.6f, 0f), 1f, flipX: Facing < 0f, parent: transform);
        HitBox(new Vector2(Mathf.Min(Me.x - Facing * 0.5f, Me.x + Facing * 2.4f), Me.y - 1.6f),
               new Vector2(Mathf.Max(Me.x - Facing * 0.5f, Me.x + Facing * 2.4f), Me.y + 3.2f), 3.5f * Power("k_highcut"), Facing);
        CameraFollow.Shake(0.15f, 0.12f);
        yield return new WaitForSeconds(0.12f);
    }

    // 꾹꾹이 베기: three ground ruptures stepping forward, each a short stun.
    IEnumerator Knead()
    {
        body.skillLock = true;
        body.FaceTowards(MoveDir());
        var hit = new HashSet<Enemy>();
        float floor = GroundY(Feet + Vector3.up * 0.2f);
        for (int i = 0; i < 3; i++)
        {
            float w = Windup(i % 2 == 0 ? 2 : 0, 2f);
            if (w > 0f) yield return new WaitForSeconds(w); // the paw comes down, then the ground cracks
            float x = Me.x + Facing * (1.2f + 1.5f * i);
            if (Solid(new Vector2(x, floor + 0.5f))) break; // a wall stops the wave
            var at = new Vector3(x, floor, 0f);
            Vfx.Play("k_knead_fx", at, 1f, flipX: Facing < 0f);
            Vfx.Play("dust_mid", at, 1f);
            Sfx.Play("slam", 0.9f, 1.25f - 0.1f * i);
            CameraFollow.Shake(0.12f, 0.08f);
            hit.Clear(); // each wave hits again
            HitCircle(at + Vector3.up * 0.6f, 1.3f, 2f * Power("k_knead"), hit, e => e.Stun(0.5f));
            yield return new WaitForSeconds(Mathf.Max(0.06f, 0.14f - w)); // the next stomp starts before this pose ends
        }
    }

    // 일섬: 냥냥 오의 - gather, flash through everything ahead, then the X cuts land.
    IEnumerator Ilseom()
    {
        body.skillLock = true;
        body.skillHover = true;
        body.FaceTowards(MoveDir());
        invulnTimer = Mathf.Max(invulnTimer, 1.1f);
        Vfx.Play("charge", Me, 1f);
        Sfx.Play("dash", 1f, 0.6f);
        Pose(0, 0.05f); // hold the drawn-back first frame while it gathers
        yield return new WaitForSeconds(0.35f);

        float startX = Me.x;
        Pose(0, 1.6f); // the one draw-and-cut happens during the dash; the X cuts land after it has passed
        Sfx.Play("dash", 1f, 1.4f);
        Vfx.Play("dust_side", Feet, 1.3f, flipX: Facing < 0f);
        for (int i = 0; i < 6; i++)
        {
            if (i % 2 == 0) SkillFx.Ghost(body.Renderer, new Color(0.85f, 0.95f, 1f), 0.3f);
            body.TryMove(Facing * 1f);
            yield return null;
        }
        float endX = Me.x;
        float feet = Feet.y;
        var cut = new List<Enemy>();
        foreach (var e in Enemy.All)
        {
            if (e.IsDead) continue;
            if (e.BodyX + e.BodyHalfWidth < Mathf.Min(startX, endX) - 0.3f || e.BodyX - e.BodyHalfWidth > Mathf.Max(startX, endX) + 0.3f) continue;
            var b = e.Renderer != null ? e.Renderer.bounds : new Bounds(e.transform.position, Vector3.one);
            if (b.max.y < feet - 0.3f || b.min.y > feet + body.bodyHeight + 0.6f) continue;
            cut.Add(e);
        }
        yield return new WaitForSeconds(0.14f);
        Vfx.Play("ring_big", Me, 0.5f, tint: new Color(0.75f, 0.9f, 1f)); // the flash (blue like the rest of the knight); x1 was twice the cat
        Vfx.Play("dust_mid", Feet, 1f);
        HitStop.Do(0.12f);
        CameraFollow.Shake(0.35f, 0.25f);
        Sfx.Play("hit_strong", 1f, 0.7f);
        foreach (var e in cut)
        {
            if (e == null || e.IsDead) continue;
            Vector3 c = e.Renderer != null ? e.Renderer.bounds.center : e.transform.position;
            Vfx.Play("k_ilseom_fx", c, 1f);
            Damage(e, 7f * Power("k_ilseom"), Facing, true);
        }
        body.skillHover = false;
        yield return new WaitForSeconds(0.15f);
    }

    // 냥이 돌진: an invulnerable charge that bowls monsters over.
    IEnumerator Rush()
    {
        body.skillLock = true;
        body.FaceTowards(MoveDir());
        invulnTimer = Mathf.Max(invulnTimer, 0.4f);
        Sfx.Play("dash", 1f, 0.85f);
        Vfx.Play("k_rush_fx", Me, 1f, flipX: Facing < 0f);
        Vfx.Play("dust_side", Feet, 1f, flipX: Facing < 0f);
        if (body.dodgeFrames != null && body.dodgeFrames.Length > 0) body.PlayAttack(body.dodgeFrames, 16f);
        var hit = new HashSet<Enemy>();
        const float distance = 5f, time = 0.25f;
        float ghostIn = 0f;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            body.TryMove(Facing * distance / time * Time.deltaTime);
            if ((ghostIn -= Time.deltaTime) <= 0f) { SkillFx.Ghost(body.Renderer, Orange, 0.2f); ghostIn = 0.055f; }
            HitFront(0.9f, 3f * Power("k_rush"), skip: hit, after: e => e.Stun(0.3f, null));
            yield return null;
        }
    }

    // 난폭한 캣휠 검무: spin for 1.6 s, slashes all around. The cat can still walk meanwhile.
    IEnumerator CatWheel()
    {
        var lastHit = new Dictionary<Enemy, float>();
        float angle = 0f;
        Sfx.Play("swing", 1f, 1.3f);
        Vfx.Loop("k_wheel_fx", Me, 1.6f, 0.5f, parent: transform); // the drawn ring, spun in quarter turns
        for (float t = 0f; t < 1.6f; t += 0.1f)
        {
            angle += 72f;
            body.FaceTowards(Mathf.Cos(angle * Mathf.Deg2Rad));
            if (Mathf.Repeat(t, 0.2f) < 0.1f) Sfx.Play("swing", 0.7f, 1.2f + Random.Range(-0.1f, 0.1f));
            var skip = new HashSet<Enemy>();
            foreach (var kv in lastHit) if (Time.time - kv.Value < 0.3f) skip.Add(kv.Key);
            HitCircle(Me, 2.3f, 0.8f * Power("k_wheel"), skip, e => lastHit[e] = Time.time, strong: false);
            yield return new WaitForSeconds(0.1f);
        }
    }

    // 위협의 하악질: "!" and a pressure wave - shoves everyone near away and stuns them.
    IEnumerator Hiss()
    {
        body.skillLock = true;
        Sfx.Play("boss_roar", 0.6f, 1.9f);
        Vfx.Play("k_hiss_fx", Me + Vector3.up * 0.3f, 1f);
        Vfx.Play("ring_big", Me, 1f, tint: new Color(1f, 0.95f, 0.85f));
        Vfx.Play("dust_side", Feet, 1f); Vfx.Play("dust_side", Feet, 1f, flipX: true); // the pressure kicks dust out both ways
        CameraFollow.Shake(0.2f, 0.18f);
        float stun = Lv("k_hiss") >= 2 ? 2.6f : 1.8f;
        HitCircle(Me, 5f, 1f * Power("k_hiss"), after: e => e.Stun(stun), strong: true);
        yield return new WaitForSeconds(0.4f);
    }

    // 꼬리 휘두르기 방어: one full circle of the tail - hits all around, turns boss shockwaves back.
    IEnumerator TailGuard()
    {
        body.skillLock = true;
        invulnTimer = Mathf.Max(invulnTimer, 0.5f);
        float spin = Windup(0, 2f);
        if (spin > 0f) yield return new WaitForSeconds(spin);
        Sfx.Play("swing", 1f, 0.9f);
        Vfx.Play("k_tail_fx", Me, 1f); // the drawn circular tail slash
        Vfx.Play("dust_small", Feet + Vector3.left * 0.8f, 1f); Vfx.Play("dust_small", Feet + Vector3.right * 0.8f, 1f, flipX: true);
        HitCircle(Me, 2.4f, 2.5f * Power("k_tail"));
        int reflected = 0;
        foreach (var wave in BossShockwave.All.ToArray())
        {
            if (wave == null || wave.Reflected || Vector2.Distance(wave.transform.position, Feet) > 5f) continue;
            wave.Reflect(Roll(2f * Power("k_tail"), out _));
            Vfx.Play("ring_small", wave.transform.position + Vector3.up * 0.5f, 1f, tint: new Color(1f, 0.9f, 0.5f));
            reflected++;
        }
        if (reflected > 0) DamagePopup.SpawnLabel(Head, Loc.T("skill.reflect"), new Color(1f, 0.85f, 0.4f));
        yield return new WaitForSeconds(0.35f);
    }

    // 수염 센서 반격 (passive, from TryBlockHit): blink behind the attacker and cut.
    IEnumerator Counter(Enemy target)
    {
        body.skillLock = true;
        body.CancelActions();
        Vfx.Play("ring_small", Me, 1f, tint: new Color(0.6f, 0.9f, 1f));
        Vfx.Play("blink", Me, 1f);
        Sfx.Play("dash", 1f, 1.5f);
        DamagePopup.SpawnLabel(Head, Loc.T("skill.counter"), new Color(0.6f, 0.9f, 1f));
        yield return new WaitForSeconds(0.05f);
        if (target != null && !target.IsDead)
        {
            float side = Mathf.Sign(target.BodyX - Me.x);
            if (side == 0f) side = Facing;
            float x = target.BodyX + side * (target.BodyHalfWidth + 0.9f);
            float feet = Feet.y;
            bool free = !Solid(new Vector2(x, feet + 0.3f)) && !Solid(new Vector2(x, feet + body.bodyHeight * 0.6f));
            if (free && Mathf.Abs(target.transform.position.y - feet) < 2.5f)
                transform.position = new Vector3(x, transform.position.y, transform.position.z);
            Vfx.Play("blink", Me, 1f);
            body.FaceTowards(target.BodyX - Me.x);
        }
        float cut = Windup(2, 2f, 0.12f);
        if (cut > 0f) yield return new WaitForSeconds(cut); // the slash follows the paw
        Vfx.Play("k_whisker_fx", new Vector3(Me.x + Facing * 1f, Me.y, 0f), 1f, flipX: Facing < 0f);
        Sfx.Play("swing", 1f, 0.9f);
        HitFront(2.2f, 3f);
        yield return new WaitForSeconds(0.2f);
    }

}
