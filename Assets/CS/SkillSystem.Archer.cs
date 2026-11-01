using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// The archer's active skills and arrow helpers (SkillSystem).
public partial class SkillSystem
{
    // ================================================================== 궁수 actives
    Sprite[] ArrowFrames(bool electro)
    {
        var ch = body.character;
        if (ch != null && ch.combo != null)
            foreach (var m in ch.combo)
                if (m.IsRanged && m.projectilePierces == electro) return m.projectileFrames;
        if (ch != null && ch.projectileSprite != null) return new[] { ch.projectileSprite };
        return null;
    }

    Vector3 BowPoint
    {
        get
        {
            var ch = body.character;
            Vector2 spawn = ch != null && ch.combo != null && ch.combo.Length > 0 ? ch.combo[0].projectileSpawn : new Vector2(0.08f, 0f);
            return Me + new Vector3(spawn.x * Facing, spawn.y, 0f) * VisualScale;
        }
    }

    // An arrow (or any prop) from the bow, with the arrow upgrades applied.
    ArrowProjectile Fire(Vector3 origin, Vector2 dir, float mult, Sprite[] frames = null, bool pierce = false,
                         float speedMul = 1f, float scale = 0f, float rangeMul = 1f)
    {
        frames = frames ?? ArrowFrames(pierce);
        if (frames == null || attack == null) return null;
        int dmg = Roll(mult, out bool crit);
        var a = ArrowProjectile.Spawn(frames, 12f, pierce, origin, dir, attack.projectileSpeed * speedMul * ArrowSpeedMultiplier,
                                      attack.projectileRange * stats.ReachMultiplier * rangeMul, dmg, scale > 0f ? scale : VisualScale, crit);
        ApplyArrowMods(a);
        return a;
    }

    // 캣닙 독화살 / 바람의 시위 / 캣타워 관측 apply to every arrow the archer looses.
    void ApplyArrowMods(ArrowProjectile a)
    {
        if (a == null) return;
        int catnip = Lv("a_catnip");
        if (catnip > 0)
        {
            int perTick = Mathf.Max(1, Mathf.RoundToInt(stats.AttackPower * (catnip >= 2 ? 0.35f : 0.25f)));
            float seconds = catnip >= 2 ? 3f : 2f;
            a.onHit += (e, p) => { Vfx.Play("p_acid_hit", p, 1f); e.Poison(perTick, seconds); };
            if (a.Renderer != null) a.Renderer.color = new Color(0.75f, 1f, 0.6f);
        }
        if (Has("a_wind")) a.trailInterval = 0.035f;
        int watch = Lv("a_watch");
        if (watch > 0)
        {
            float bonus = watch >= 2 ? 1.3f : 1.15f;
            float fromX = Me.x;
            a.modifyDamage = (e, d) => Mathf.Abs(e.BodyX - fromX) > 6f ? Mathf.RoundToInt(d * bonus) : d;
        }
    }

    // PlayerAttack: the archer's basic shot, with the arrow upgrades.
    public void ShootBasic(AttackMove move, Vector3 origin, Vector2 dir, int damage, bool crit)
    {
        shotCount++;
        Sprite[] frames = move.projectileFrames;
        float fps = move.projectileFps, scale = VisualScale;
        bool pierce = move.projectilePierces;
        int maxHits = -1;

        int fish = Lv("a_fish");
        if (fish > 0)
        {
            var bone = Vfx.Sprite("sf_fishbone");
            if (bone != null && !move.projectilePierces) { frames = new[] { bone }; fps = 0f; scale = 1f; }
            pierce = true;
            maxHits = fish >= 2 || move.projectilePierces ? -1 : 3;
            damage = Mathf.Max(1, Mathf.RoundToInt(damage * 1.1f));
        }

        var a = ArrowProjectile.Spawn(frames, fps, pierce, origin, dir, attack.projectileSpeed * move.projectileSpeedMultiplier * ArrowSpeedMultiplier,
                                      attack.projectileRange * stats.ReachMultiplier, damage, scale, crit);
        a.opensChest = true;
        a.maxHits = maxHits;
        if (fish > 0) a.onHit += (e, p) => Vfx.Play("spark_mid2", p, 0.6f);
        ApplyArrowMods(a);

        int jelly = Lv("a_jelly");
        if (jelly > 0 && shotCount % (jelly >= 2 ? 2 : 3) == 0)
        {
            if (a.Renderer != null) a.Renderer.color = new Color(1f, 0.6f, 0.85f);
            bool done = false;
            System.Action<Vector3> boom = p => { if (done) return; done = true; Explode(p, 1.8f, 1.2f, "a_jelly_fx"); };
            a.onHit += (e, p) => boom(p);
            a.onEnd += boom;
        }

        int curve = Lv("a_curve");
        if (curve > 0 && shotCount % (curve >= 2 ? 2 : 3) == 0) LobYarn(origin, dir);

        if (Has("a_wind"))
            Vfx.Play("p_wind_hit", origin + (Vector3)(dir.normalized * 0.4f), 1f, flipX: dir.x < 0f); // a puff of wind off the string
    }

    // 곡선 털실 화살: a bouncing yarn ball lobbed along with the arrow.
    void LobYarn(Vector3 origin, Vector2 dir)
    {
        var yarn = Vfx.Sprite("a_curve_fx");
        if (yarn == null) return;
        float side = dir.x >= 0f ? 1f : -1f;
        Vector2 launch = Rotate(dir, 25f * side);
        if (launch.y < 0.25f) launch = new Vector2(side * 0.9f, 0.45f).normalized;
        int dmg = Roll(1f, out bool crit);
        var a = ArrowProjectile.Spawn(new[] { yarn }, 0f, true, origin, launch, 11f, 22f, dmg, 1f, crit);
        a.gravity = 18f;
        a.bounces = 2;
        a.maxHits = 3;
        a.onHit += (e, p) => Vfx.Play("yarn_hit", p, 1f);
    }

    // 냥이의 미소: hearts fly out; monsters close by are charmed (stand still) for a while.
    IEnumerator Smile()
    {
        body.skillLock = true;
        var heart = Vfx.Sprite("heart");
        SkillFx.Burst(heart, Head, 10, 5f, 0.9f, SkillFx.PropScale * 0.7f, 2f);
        Vfx.Play("a_smile_fx", Me, 1f);
        Sfx.Play("card_pick", 1f, 1.4f);
        float seconds = Lv("a_smile") >= 2 ? 3.5f : 2.5f;
        foreach (var e in Enemy.All.ToArray())
        {
            if (e.IsDead || Vector2.Distance(e.transform.position, Feet) > 6f) continue;
            e.Stun(seconds, null);
            StartCoroutine(HeartsOver(e, e.isBoss ? seconds * 0.35f : seconds));
        }
        yield return new WaitForSeconds(0.35f);
    }

    IEnumerator HeartsOver(Enemy e, float seconds)
    {
        var heart = Vfx.Sprite("heart");
        for (float t = 0f; t < seconds && e != null && !e.IsDead; t += 0.35f)
        {
            SkillFx.Burst(heart, e.HeadPoint, 1, 1.5f, 0.7f, SkillFx.PropScale * 0.55f, -1.5f);
            yield return new WaitForSeconds(0.35f);
        }
    }

    // 유성 화살비: a rain of arrows over the mouse position.
    IEnumerator MeteorRain()
    {
        body.skillLock = true;
        Vector2 target = MouseWorld();
        target.x = Mathf.Clamp(target.x, Me.x - 12f, Me.x + 12f);
        body.FaceTowards(target.x - Me.x);
        float draw = Windup(0, 1.5f);
        if (draw > 0f) yield return new WaitForSeconds(draw); // the volley goes up when the bow lets go
        Sfx.Play("arrow", 1f, 0.7f);
        float skyY = Mathf.Max(target.y, Feet.y) + 7f;
        // Not inside a ceiling.
        for (int i = 0; i < 40 && Solid(new Vector2(target.x, skyY)); i++) skyY -= 0.25f;
        Vfx.Play("a_meteor_fx", new Vector3(target.x, skyY - 2.5f, 0f), 1f);
        Vfx.Play("ring_small", new Vector3(target.x, GroundY(new Vector2(target.x, target.y + 0.5f)) + 0.15f, 0f), 2f, tint: new Color(1f, 0.85f, 0.45f)); // marks where they will land
        yield return new WaitForSeconds(Mathf.Max(0.05f, 0.25f - draw));
        body.skillLock = false; // free to move while the arrows fall
        var frames = ArrowFrames(false);
        for (int i = 0; i < 12; i++)
        {
            var from = new Vector3(target.x + RunRng.Range(-2f, 2f), skyY + RunRng.Range(0f, 0.6f), 0f);
            var a = Fire(from, new Vector2(RunRng.Range(-0.15f, 0.15f), -1f), 1f * Power("a_meteor"), frames, false, 1.4f, 0f, 1.3f);
            if (a != null)
            {
                a.onEnd += p => Vfx.Play("dust_small", p, 0.8f);
                a.onHit += (e, p) => Vfx.Play("spark_mid1", p, 0.7f);
            }
            if (i % 3 == 0) Sfx.Play("arrow", 0.6f, 1.2f + Random.Range(-0.1f, 0.1f));
            yield return new WaitForSeconds(0.075f);
        }
    }

    // 연속 냥냥 사격: four arrows in a quick burst.
    IEnumerator RapidShot()
    {
        body.skillLock = true;
        Vector2 aim = Aim();
        body.FaceTowards(aim.x);
        Vfx.Play("p_wind_breath", BowPoint + (Vector3)(aim * 1.2f), 1f, flipX: aim.x < 0f, // a gust rolls out ahead of the burst
                 rotationZ: Mathf.Atan2(aim.y, Mathf.Abs(aim.x)) * Mathf.Rad2Deg * Mathf.Sign(aim.x));
        for (int i = 0; i < 4; i++)
        {
            float w = Windup(0, 2.5f, 0.09f);
            if (w > 0f) yield return new WaitForSeconds(w); // release on the bow's draw frame
            Sfx.Play("arrow", 1f, 1f + 0.08f * i);
            Fire(BowPoint, Rotate(aim, RunRng.Range(-4f, 4f)), 1.3f * Power("a_rapid"));
            yield return new WaitForSeconds(Mathf.Max(0.04f, 0.09f - w));
        }
        yield return new WaitForSeconds(0.1f);
    }

    // 백스텝 샷: hop back and loose a piercing electro arrow mid-air.
    IEnumerator BackstepShot()
    {
        body.skillLock = true;
        Vector2 aim = Aim();
        body.FaceTowards(aim.x);
        float back = -Facing;
        invulnTimer = Mathf.Max(invulnTimer, 0.45f);
        Vfx.Play("smoke", Feet + Vector3.up * 0.3f, 1f);
        Sfx.Play("jump", 1f, 1.1f);
        body.Launch(9f);
        float release = Windup(2, 2.5f, 0.2f); // drawn from the hop's start, so the arrow leaves on the release frame
        bool fired = false;
        const float time = 0.26f, distance = 3.2f;
        float ghostIn = 0f;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            body.TryMove(back * distance / time * Time.deltaTime);
            if ((ghostIn -= Time.deltaTime) <= 0f) { SkillFx.Ghost(body.Renderer, new Color(0.7f, 0.95f, 1f), 0.2f); ghostIn = 0.06f; }
            if (!fired && t >= release) // mid-hop, on the bow's release frame
            {
                fired = true;
                Sfx.Play("arrow", 1f, 0.8f);
                Vfx.Play("p_thunder_hit", BowPoint, 0.5f); // a crackle at the bow
                var bolt = Vfx.Frames("p_thunder_bolt"); // the arrow is a bolt of lightning
                var a = Fire(BowPoint, aim, 3.5f * Power("a_backstep"), bolt != null && bolt.Length > 0 ? bolt : ArrowFrames(true), true, 1.25f, bolt != null && bolt.Length > 0 ? 1f : 0f);
                if (a != null) a.onHit += (e, p) => Vfx.Play("p_thunder_hit", p, 1f);
            }
            yield return null;
        }
    }

    // 방울 와이어: a jingling wire along the floor ahead that shocks whoever walks on it.
    IEnumerator BellWire()
    {
        body.skillLock = true;
        body.FaceTowards(Aim().x);
        float floor = GroundY(Feet + Vector3.up * 0.2f);
        float x0 = Me.x + Facing * 0.6f, x1 = x0;
        // Run it forward until a wall or the end of the floor (max 5 units).
        for (float d = 0.25f; d <= 5f; d += 0.25f)
        {
            float x = x0 + Facing * d;
            if (Solid(new Vector2(x, floor + 0.4f))) break;
            var map = StageMap.Instance;
            if (map != null && !map.IsSolid(new Vector2(x, floor - 0.2f)) && !map.IsPlatform(new Vector2(x, floor - 0.2f))) break;
            x1 = x;
        }
        if (Mathf.Abs(x1 - x0) < 0.5f) x1 = x0 + Facing * 0.5f;
        float seconds = 5f;
        float tick = 0.8f * Power("a_wire");
        SkillWire.Spawn(new Vector3(x0, floor + 0.12f, 0f), new Vector3(x1, floor + 0.12f, 0f), seconds,
                        e => Damage(e, tick, Facing, false));
        Sfx.Play("ui_click", 1f, 2f);
        Vfx.Play("p_thunder_hit", new Vector3((x0 + x1) * 0.5f, floor + 0.3f, 0f), 1f);
        Vfx.Play("a_wire_fx", new Vector3((x0 + x1) * 0.5f, floor + 0.5f, 0f), 1f, flipX: Facing < 0f);
        yield return new WaitForSeconds(0.2f);
    }

    // 끈적이 쥐덫: a trap at the feet; the first monster on it is stuck (and hurt).
    IEnumerator StickyTrap()
    {
        // Just in front of the feet: the grey-purple trap matches the archer's own colours and hid the cat when set right underneath.
        float x = Solid(new Vector2(Me.x + Facing * 1.2f, Feet.y + 0.3f)) ? Me.x : Me.x + Facing * 1.2f;
        float floor = GroundY(new Vector2(x, Feet.y + 0.2f));
        float mult = 3f * Power("a_trap");
        SkillTrap.Spawn(new Vector3(x, floor, 0f), 20f, e => Damage(e, mult, 0f, true));
        Sfx.Play("ui_click", 1f, 0.8f);
        Vfx.Play("dust_small", new Vector3(x, floor, 0f), 0.8f);
        yield break;
    }

    // 츄르 유인책: throw a churu; monsters around it come running and forget the cat for a while.
    IEnumerator Churu()
    {
        body.skillLock = true;
        Vector2 target = MouseWorld();
        float side = target.x >= Me.x ? 1f : -1f;
        body.FaceTowards(side);
        Pose(1, 1.6f);
        yield return new WaitForSeconds(0.12f);
        var churu = Vfx.Sprite("a_churu_fx");
        if (churu == null) yield break;
        const float g = 20f, angle = 45f * Mathf.Deg2Rad;
        float dist = Mathf.Clamp(Mathf.Abs(target.x - Me.x), 1.5f, 12f);
        float speed = Mathf.Clamp(Mathf.Sqrt(dist * g / Mathf.Sin(2f * angle)), 6f, 17f);
        var a = ArrowProjectile.Spawn(new[] { churu }, 0f, true, BowPoint, new Vector2(side * Mathf.Cos(angle), Mathf.Sin(angle)), speed, 40f, 0, 0.5f); // the 20 px tube drawn at half size: about 0.8 units long
        a.harmless = true;
        a.gravity = g;
        a.spin = -side * 600f;
        float seconds = Lv("a_churu") >= 2 ? 6f : 4f;
        a.onEnd += p =>
        {
            float floor = GroundY(new Vector2(p.x, p.y + 1.2f), 3f);
            SkillChuru.Spawn(new Vector3(p.x, floor, 0f), seconds);
        };
        Sfx.Play("swing", 0.8f, 1.5f);
        yield return new WaitForSeconds(0.15f);
    }

    // 유도 털실 화살: three yarn balls that chase monsters.
    IEnumerator HomingYarn()
    {
        body.skillLock = true;
        Vector2 aim = Aim();
        body.FaceTowards(aim.x);
        float draw = Windup(1, 1.8f);
        if (draw > 0f) yield return new WaitForSeconds(draw);
        var yarn = Vfx.Sprite("a_homing_fx");
        Vfx.Play("yarn_hit", BowPoint, 1f);
        Sfx.Play("arrow", 1f, 1.3f);
        for (int i = 0; i < 3; i++)
        {
            var a = Fire(BowPoint, Rotate(aim, (i - 1) * 25f), 2.2f * Power("a_homing"), yarn != null ? new[] { yarn } : null, false, 0.65f, 1f, 1.6f);
            if (a == null) continue;
            a.homing = true;
            a.trailInterval = 0.04f;                       // a pink wake so the three balls can be followed
            a.trailColor = new Color(1f, 0.6f, 0.85f, 0.55f);
            a.onHit += (e, p) => Vfx.Play("yarn_hit", p, 1f);
            yield return new WaitForSeconds(0.06f);
        }
        yield return new WaitForSeconds(0.1f);
    }

    // 전설의 캔따개 피어싱: charge up, then a giant can that goes through everything.
    IEnumerator CanPiercer()
    {
        body.skillLock = true;
        body.skillHover = true;
        Vector2 aim = Aim();
        body.FaceTowards(aim.x);
        invulnTimer = Mathf.Max(invulnTimer, 0.7f);
        float draw = Windup(2, 0.86f, 0.6f); // a slow, full draw: the can flies on the release frame
        Vfx.Play("charge", BowPoint, 1f);
        Sfx.Play("dash", 1f, 0.5f);
        yield return new WaitForSeconds(draw);

        var can = Vfx.Sprite("a_can_fx");
        float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
        var a = Fire(BowPoint, aim, 6f * Power("a_can"), can != null ? new[] { can } : null, true, 0.95f, 1f, 1.7f);
        if (a != null)
        {
            a.hitRadius = 0.9f;
            a.strong = true;
            a.onHit += (e, p) => { Vfx.Play("explosion", p, 0.5f); CameraFollow.Shake(0.15f, 0.1f); };
            a.onEnd += p =>
            {
                Vfx.Play("beam_end", p, 1f, rotationZ: angle);
                Explode(p, 2.2f, 2.5f * Power("a_can"));
            };
        }
        Sfx.Play("slam", 1f, 0.8f);
        CameraFollow.Shake(0.25f, 0.2f);
        body.TryMove(-Facing * 0.6f); // recoil
        body.skillHover = false;
        yield return new WaitForSeconds(0.2f);
    }

}
