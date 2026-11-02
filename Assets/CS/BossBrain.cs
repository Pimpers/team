using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Three-phase boss (the last boss, the bear). Added automatically to any enemy whose definition is a
// boss. The Enemy keeps its normal chase + swipe; this adds special moves on a timer and
// takes over the body while one runs (Enemy.Update asks Tick() first). Specials:
//   CHARGE - head down ("!") then a four-legged rush across the arena; into a wall, it is STAGGERED.
//   THROW  - rears up with a rock over its head and hurls it in an arc at the cat (a red mark where it lands);
//            enraged, three at once. Reaches the cat far away or up on a ledge.
//   POUND  - (phase 2+) rears up, a paw raised, and slams the floor: shockwaves along it, rocks from the ceiling.
//   LEAP   - (phase 2+) crouches, leaps high and comes down where the cat stood (its landing spot marked):
//            a short shockwave each way. Standing still on a ledge is no longer safe from it.
// Which one: picked by distance (close: pound / leap; far or above: throw / leap / charge), never the same one
// twice in a row; enraged, a special sometimes chains straight into another.
// STAGGER - into a wall, or after taking a big chunk of damage in a short while, it sways, dazed (stars
// over its head, "빈틈!"), for about two seconds and takes half again as much damage: the opening to punish.
//   Phase 2 (HP <= 66%): roars, moves/attacks faster, adds POUND and LEAP.
//   Phase 3 (enraged):  roars, glows red, swipes three times in a row, specials come sooner and chain,
//                       the shockwaves run the whole floor and more rocks fall.
// Dodge: jump/dash over the charge, jump the shockwaves (they only hit grounded players), step off the marks.
// Also shows the boss HP bar (name, phase marks at 66% / 33%) at the top of the screen.
public class BossBrain : MonoBehaviour
{
    enum Move { None, Roar, ChargeWindup, Charge, ChargeRecover, PoundWindup, PoundRecover,
                LeapWindup, Leap, LeapRecover, ThrowWindup, ThrowRecover, Stagger }
    enum Special { None, Charge, Pound, Leap, Throw }

    const float RoarTime = 1.1f;
    const float ChargeWindupTime = 0.6f, ChargeSpeed = 12f, ChargeMaxTime = 1.6f, ChargeRecoverTime = 0.7f;
    const float PoundWindupTime = 0.75f, PoundRecoverTime = 0.9f;
    const float SlamRadius = 3f;
    const float LeapWindupTime = 0.55f, LeapTime = 0.7f, LeapHeight = 4f, LeapRecoverTime = 0.7f;
    const float ThrowWindupTime = 0.6f, ThrowRecoverTime = 0.55f;
    const float StaggerTime = 2.2f, StaggerDamage = 1.5f, StaggerAfter = 0.16f; // stagger after losing 16% HP in quick succession

    Enemy enemy;
    Move move;
    float moveTimer;
    int phase = 1;
    float specialTimer = 3.5f;
    Special lastSpecial;
    float poise; int lastHp = -1; // damage taken lately (drains away), toward a stagger
    Vector3 leapFrom; float leapToX;
    SpriteRenderer leapMark, heldRock;
    GameObject stars;
    float chargeDir;
    bool chargeHit;
    float floorY;

    // HP bar
    Image barFill;
    TMP_Text barLabel;

    void Start()
    {
        enemy = GetComponent<Enemy>();
        floorY = transform.position.y;
        // Warm beige lowers the huge white belly without hiding its dark limbs.
        enemy.baseTint = new Color(0.72f, 0.58f, 0.5f, 1f);
        if (enemy.Renderer != null) enemy.Renderer.color = enemy.baseTint;
        BuildHealthBar();
    }

    // The entrance: about a second after the stage loads the boss roars - name banner, a shake, a ring -
    // and from then on hunts the player across the whole arena. Until then it just stands there and its
    // HP bar is hidden, so there is a beat to look around.
    float introTimer = 0.6f; // counted once the screen is open and the stage's title (StageTitle) has gone: one announcement at a time
    bool introDone;

    void PlayIntro()
    {
        introDone = true;
        enemy.engaged = true;
        CameraFollow.Focus(enemy.Renderer.bounds.center, 2f); // it usually roars off screen: the view swings over to it
        enemy.Face(enemy.ToPlayerFeet().x);
        Begin(Move.Roar, RoarTime + 0.5f);
        enemy.AnimHold(Pose(1)); // rears up to roar
        DamagePopup.SpawnLabel(new Vector3(transform.position.x, enemy.BodyTopY, 0f), "!!", new Color(1f, 0.25f, 0.25f));
        CameraFollow.Shake(0.4f, 0.8f);
        Sfx.Play("boss_roar", 1f, 0.55f);
        Vfx.Play("ring_big", enemy.Renderer.bounds.center, 1f, tint: new Color(1f, 0.55f, 0.45f)); // the roar rolls out as a ring
        var floor = new Vector3(transform.position.x, floorY, 0f);
        Vfx.Play("dust_big", floor + Vector3.left * 1.5f, 2f);
        Vfx.Play("dust_big", floor + Vector3.right * 1.5f, 2f, flipX: true);
        if (barRoot != null) barRoot.SetActive(true);
        var hud = RunManager.Instance != null ? RunManager.Instance.hudText : null;
        if (hud != null && hud.canvas != null)
            StageBanner.Show(hud.canvas.rootCanvas, hud.font, enemy.definition.displayName,
                             Loc.T("boss.banner_hint"), 1.25f, compact: true);
    }

    // Returns true while a special move is controlling the boss this frame.
    // A special move on its way or under way (wind-ups, the charge, the leap): what a player dodges. Recoveries,
    // the roar and a stagger are openings. (The full-run playtest's dodge mode reads it.)
    public bool Threatening => move == Move.ChargeWindup || move == Move.Charge || move == Move.PoundWindup ||
                               move == Move.LeapWindup || move == Move.Leap || move == Move.ThrowWindup;

    public bool Tick()
    {
        if (enemy == null) return false;
        if (!introDone)
        {
            if (StageTitle.Showing || LeafWipe.Busy || ScreenFader.Switching) return false;
            introTimer -= Time.deltaTime;
            if (introTimer > 0f) return false;
            PlayIntro();
        }

        // Phase changes (checked even mid-move so a big hit can't skip the roar).
        float hpFrac = enemy.MaxHP > 0 ? (float)enemy.HP / enemy.MaxHP : 1f;
        // (Not in mid-air: the roar waits for the leap to land.)
        if (((phase == 1 && hpFrac <= 0.66f) || (phase == 2 && hpFrac <= 0.33f)) && move != Move.Leap)
        {
            phase++;
            Begin(Move.Roar, RoarTime);
            enemy.AnimHold(Pose(1));
            DamagePopup.SpawnLabel(new Vector3(transform.position.x, enemy.BodyTopY, 0f), phase == 3 ? "!!!" : "!!", new Color(1f, 0.25f, 0.25f));
            CameraFollow.Shake(0.35f, 0.5f);
            Sfx.Play("boss_roar", 1f, phase == 3 ? 0.5f : 0.6f); // low, rumbling roar
            Vfx.Play("ring_big", enemy.Renderer.bounds.center, 1f, tint: new Color(1f, 0.55f, 0.45f)); // the roar rolls out as a ring
            ApplyPhaseStats();
        }

        // Stagger meter: damage piles up and drains away; a big enough chunk in quick succession knocks it dazed
        // (not mid-leap / mid-windup - it finishes those first).
        int hp = enemy.HP;
        if (lastHp >= 0 && hp < lastHp) poise += lastHp - hp;
        lastHp = hp;
        poise = Mathf.Max(0f, poise - enemy.MaxHP * 0.03f * Time.deltaTime);
        bool interruptible = move == Move.None || move == Move.ChargeRecover || move == Move.PoundRecover ||
                             move == Move.LeapRecover || move == Move.ThrowRecover;
        if (poise >= enemy.MaxHP * StaggerAfter && interruptible && !enemy.IsDead) { poise = 0f; StartStagger(StaggerTime); }

        if (move != Move.None)
        {
            UpdateMove(Time.deltaTime);
            return true;
        }

        // Specials only start from a neutral state (not mid-claw) with a living player.
        if (enemy.IsAttacking || PlayerStats.Instance == null || PlayerStats.Instance.CurrentHP <= 0f) return false;
        specialTimer -= Time.deltaTime;
        if (specialTimer > 0f) return false;

        var pick = PickSpecial();
        switch (pick)
        {
            case Special.Pound: StartPound(); break;
            case Special.Leap: StartLeap(); break;
            case Special.Throw: StartThrow(); break;
            default: StartCharge(); break;
        }
        lastSpecial = pick;
        specialTimer = phase == 1 ? RunRng.Range(4.5f, 6.5f) : phase == 2 ? RunRng.Range(3.2f, 4.6f) : RunRng.Range(2.2f, 3.4f);
        return true;
    }

    // Weighted by where the cat is: close - the pound and the leap; far - the throw, the leap, the charge; up on a
    // ledge - the throw and the leap (the charge and the swipe can't reach it). Never the same one twice running.
    Special PickSpecial()
    {
        Vector2 to = enemy.ToPlayerFeet();
        float dx = Mathf.Abs(to.x);
        bool above = to.y > 1.5f;
        var w = new System.Collections.Generic.Dictionary<Special, float>
        {
            { Special.Charge, above ? 0.4f : dx < 4f ? 1f : 2f },
            { Special.Throw, (dx > 8f ? 3f : dx > 4.5f ? 1.5f : 0.4f) + (above ? 2f : 0f) },
        };
        if (phase >= 2)
        {
            w[Special.Pound] = dx < 4.5f ? 3f : 1f;
            w[Special.Leap] = (dx < 4f ? 1f : 2f) + (above ? 1.5f : 0f);
        }
        w.Remove(lastSpecial);
        float total = 0f;
        foreach (var kv in w) total += kv.Value;
        float r = RunRng.Value * total;
        foreach (var kv in w) { r -= kv.Value; if (r <= 0f) return kv.Key; }
        return Special.Charge;
    }

    void ApplyPhaseStats()
    {
        if (phase == 2)
        {
            enemy.speedMultiplier = 1.25f;
            enemy.cooldownMultiplier = 0.8f;
        }
        else if (phase == 3)
        {
            enemy.speedMultiplier = 1.45f;
            enemy.cooldownMultiplier = 0.6f;
            enemy.extraStrikes = 2; // three swipes in a row
            enemy.baseTint = new Color(1f, 0.62f, 0.62f); // enraged
            enemy.Renderer.color = enemy.baseTint;
        }
    }

    void Begin(Move m, float time)
    {
        if (move == Move.Stagger && m != Move.Stagger) EndStagger();
        if (m != Move.Leap && leapMark != null) { Destroy(leapMark.gameObject); leapMark = null; }
        if (m != Move.ThrowWindup && heldRock != null) { Destroy(heldRock.gameObject); heldRock = null; }
        move = m;
        moveTimer = time;
    }

    void StartCharge()
    {
        chargeDir = Mathf.Sign(enemy.ToPlayerFeet().x);
        if (chargeDir == 0f) chargeDir = enemy.Facing;
        enemy.Face(chargeDir);
        Sfx.Play("dash", 1f, 0.6f); // charge rush
        Vfx.Play("dust_side", transform.position, 1.8f, flipX: chargeDir < 0f); // dust kicked up as it starts to run
        enemy.AnimHold(Pose(0)); // head down, ready to rush
        DamagePopup.SpawnLabel(new Vector3(transform.position.x, enemy.BodyTopY, 0f), "!", UiTheme.Alert);
        Begin(Move.ChargeWindup, ChargeWindupTime);
    }

    // Rears up on its hind legs, claws raised (the swipe's raised-claws frame held), then pounds.
    void StartPound()
    {
        enemy.Face(enemy.ToPlayerFeet().x);
        enemy.AnimHold(Pose(1));
        DamagePopup.SpawnLabel(new Vector3(transform.position.x, enemy.BodyTopY, 0f), "!", UiTheme.Alert);
        Sfx.Play("boss_roar", 0.6f, 0.9f); // a short growl as it rises
        Begin(Move.PoundWindup, phase >= 3 ? PoundWindupTime * 0.75f : PoundWindupTime);
    }

    void UpdateMove(float dt)
    {
        moveTimer -= dt;
        switch (move)
        {
            case Move.Roar:
                if (moveTimer <= 0f) End();
                break;

            case Move.ChargeWindup:
                if (moveTimer <= 0f)
                {
                    chargeHit = false;
                    enemy.AnimMove();
                    Begin(Move.Charge, ChargeMaxTime);
                }
                break;

            case Move.Charge:
            {
                bool moved = enemy.TryWalk(chargeDir * ChargeSpeed * enemy.speedMultiplier * dt);
                // Body-check: running through the player hurts once (dodge by jump/dash).
                Vector2 to = enemy.ToPlayerFeet();
                if (!chargeHit && Mathf.Abs(to.x) < enemy.definition.bodyHalfWidth + 0.7f && Mathf.Abs(to.y) < 2.2f)
                    chargeHit = enemy.HitPlayer(1f, 2f, chargeDir) || chargeHit;
                if (!moved || moveTimer <= 0f)
                {
                    // Hit the wall / ran out: dazed for a moment - the opening to punish it.
                    if (!moved)
                    {
                        CameraFollow.Shake(0.15f, 0.15f);
                        Sfx.Play("land_heavy", 1f, 0.8f);
                        Vfx.Play("dust_big", transform.position + Vector3.right * chargeDir * 0.8f, 1.8f); // crashes into the wall
                        Vfx.Play("debris", transform.position + new Vector3(chargeDir * 0.8f, 1f, 0f), 1.6f);
                        StartStagger(StaggerTime); // crashed: dazed, the opening to punish
                        break;
                    }
                    enemy.AnimIdle();
                    Begin(Move.ChargeRecover, ChargeRecoverTime);
                }
                break;
            }

            case Move.PoundWindup:
                if (moveTimer <= 0f)
                {
                    enemy.AnimHold(Pose(2)); // the raised paw comes down
                    Pound();
                    Begin(Move.PoundRecover, PoundRecoverTime);
                }
                break;

            case Move.PoundRecover:
                // Paw down for a beat, then it pulls back (the swipe's recover frame) before standing again.
                if (moveTimer <= PoundRecoverTime - 0.35f) enemy.AnimHold(Pose(3));
                if (moveTimer <= 0f) End();
                break;

            case Move.ChargeRecover:
                if (moveTimer <= 0f) End();
                break;

            case Move.LeapWindup:
                FlickerMark();
                if (moveTimer <= 0f)
                {
                    leapFrom = transform.position;
                    enemy.AnimHold(Pose(1)); // up on its hind legs through the air
                    Sfx.Play("jump", 1f, 0.55f);
                    Vfx.Play("dust_big", transform.position, 1.6f);
                    Begin(Move.Leap, LeapTime);
                }
                break;

            case Move.Leap:
            {
                FlickerMark();
                float t = Mathf.Clamp01(1f - moveTimer / LeapTime);
                float x = Mathf.Lerp(leapFrom.x, leapToX, t);
                transform.position = new Vector3(x, floorY + 4f * LeapHeight * t * (1f - t), transform.position.z);
                if (t > 0.6f) enemy.AnimHold(Pose(2)); // paws out for the landing
                if (moveTimer <= 0f)
                {
                    transform.position = new Vector3(leapToX, floorY, transform.position.z);
                    LeapSlam();
                    Begin(Move.LeapRecover, LeapRecoverTime);
                }
                break;
            }

            case Move.LeapRecover:
                if (moveTimer <= LeapRecoverTime - 0.3f) enemy.AnimHold(Pose(3));
                if (moveTimer <= 0f) End();
                break;

            case Move.ThrowWindup:
                if (heldRock != null) heldRock.transform.position = RockHoldPoint();
                if (moveTimer <= 0f)
                {
                    enemy.AnimHold(Pose(2)); // the arm comes over
                    Throw();
                    Begin(Move.ThrowRecover, ThrowRecoverTime);
                }
                break;

            case Move.ThrowRecover:
                if (moveTimer <= ThrowRecoverTime - 0.25f) enemy.AnimHold(Pose(3));
                if (moveTimer <= 0f) End();
                break;

            case Move.Stagger:
                if (moveTimer <= 0f) End();
                break;
        }
    }

    void FlickerMark()
    {
        if (leapMark != null) leapMark.color = new Color(1f, 0.25f, 0.2f, 0.55f + 0.4f * Mathf.Sin(Time.time * 25f));
    }

    // ------------------------------------------------------------------ leap
    // Crouches, eyes on the cat; its landing spot - where the cat stands now - is marked red on the floor.
    void StartLeap()
    {
        Vector2 to = enemy.ToPlayerFeet();
        enemy.Face(to.x);
        leapToX = LandingX(transform.position.x + to.x);
        enemy.AnimHold(Pose(0));
        DamagePopup.SpawnLabel(new Vector3(transform.position.x, enemy.BodyTopY, 0f), "!", UiTheme.Alert);
        Sfx.Play("boss_roar", 0.5f, 1.1f);
        Begin(Move.LeapWindup, phase >= 3 ? LeapWindupTime * 0.8f : LeapWindupTime);
        float half = enemy.definition.bodyHalfWidth + 0.6f;
        leapMark = SkillFx.Bar(new Vector3(leapToX - half, floorY + 0.06f, 0f), new Vector3(leapToX + half, floorY + 0.06f, 0f), 0.16f, new Color(1f, 0.25f, 0.2f, 0.8f), 5);
    }

    // A spot on the arena floor with room for the bear, as close to x as there is (stepping back toward it).
    float LandingX(float x)
    {
        var map = StageMap.Instance;
        float half = enemy.definition.bodyHalfWidth;
        float home = transform.position.x;
        if (map == null) return x;
        x = Mathf.Clamp(x, 1.5f + half, map.mapSize.x - 1.5f - half);
        float step = Mathf.Sign(home - x) * 0.5f;
        for (int i = 0; i < 60; i++)
        {
            bool clear = !map.IsSolid(new Vector2(x - half, floorY + 0.5f)) && !map.IsSolid(new Vector2(x + half, floorY + 0.5f)) &&
                         !map.IsSolid(new Vector2(x, floorY + 1.5f));
            bool floor = map.IsSolid(new Vector2(x, floorY - 0.2f)) || map.IsPlatform(new Vector2(x, floorY - 0.2f));
            if (clear && floor) return x;
            if (Mathf.Abs(x - home) < 0.5f) return home;
            x += step;
        }
        return home;
    }

    // Comes down hard: whoever is under it (or on the floor close by) is hit, a short shockwave runs each way.
    void LeapSlam()
    {
        Sfx.Play("slam", 1f, 0.7f);
        Sfx.Play("land_heavy", 1f, 0.7f);
        var at = new Vector3(transform.position.x, floorY, 0f);
        Vfx.Play("rupture", at, 2f);
        Vfx.Play("dust_big", at + Vector3.left * 1.2f, 2f);
        Vfx.Play("dust_big", at + Vector3.right * 1.2f, 2f, flipX: true);
        CameraFollow.Shake(0.45f, 0.3f);
        HitStop.Do(0.07f);
        var player = PlayerStats.Instance != null ? PlayerStats.Instance.Body : null;
        Vector2 to = enemy.ToPlayerFeet();
        bool under = Mathf.Abs(to.x) < enemy.definition.bodyHalfWidth + 0.5f && to.y < 2.5f && to.y > -0.5f;
        bool close = player != null && !player.IsAirborne && Mathf.Abs(to.x) < 2.6f && Mathf.Abs(to.y) < 1.2f;
        if (under || close) enemy.HitPlayer(1f, 2.5f, Mathf.Abs(to.x) > 0.05f ? Mathf.Sign(to.x) : enemy.Facing);
        float range = phase >= 3 ? 9f : 4f;
        BossShockwave.Spawn(at, -1f, range, enemy);
        BossShockwave.Spawn(at, 1f, range, enemy);
    }

    // ------------------------------------------------------------------ throw
    Vector3 RockHoldPoint() => new Vector3(transform.position.x - enemy.Facing * 0.3f, enemy.BodyTopY + 0.5f, 0f);

    // Rears up with a rock lifted over its head.
    void StartThrow()
    {
        enemy.Face(enemy.ToPlayerFeet().x);
        enemy.AnimHold(Pose(1));
        DamagePopup.SpawnLabel(new Vector3(transform.position.x, enemy.BodyTopY + 1f, 0f), "!", UiTheme.Alert);
        Sfx.Play("boss_roar", 0.45f, 1.2f);
        Begin(Move.ThrowWindup, phase >= 3 ? ThrowWindupTime * 0.8f : ThrowWindupTime);
        var sprite = enemy.definition.fallingRock;
        if (sprite != null)
        {
            var go = new GameObject("HeldRock");
            go.transform.position = RockHoldPoint();
            go.transform.localScale = Vector3.one * 1.5f;
            heldRock = go.AddComponent<SpriteRenderer>();
            heldRock.sprite = sprite;
            heldRock.sortingOrder = enemy.Renderer.sortingOrder + 1;
        }
    }

    // At the cat - and, enraged, two more either side of it, so there's a gap to stand in but not much.
    void Throw()
    {
        Sfx.Play("swing", 1f, 0.6f);
        var sprite = enemy.definition.fallingRock;
        if (sprite == null || PlayerStats.Instance == null) return;
        Vector3 from = RockHoldPoint() + Vector3.right * enemy.Facing * 0.5f;
        Vector2 to = enemy.ToPlayerFeet();
        float targetX = transform.position.x + to.x, targetY = transform.position.y + to.y;
        var cat = PlayerStats.Instance.Body;
        if (cat != null && cat.IsAirborne) targetY = floorY; // mid-jump: at the floor under it (a ledge stand stays a ledge)
        BossThrownRock.Spawn(from, targetX, targetY, 0.85f, sprite, enemy);
        if (phase >= 3)
        {
            BossThrownRock.Spawn(from, targetX - 2.4f, floorY, 0.95f, sprite, enemy);
            BossThrownRock.Spawn(from, targetX + 2.4f, floorY, 0.95f, sprite, enemy);
        }
    }

    // ------------------------------------------------------------------ stagger
    void StartStagger(float seconds)
    {
        var dazed = enemy.definition.hurtFrames;
        enemy.AnimHold(dazed != null && dazed.Length > 0 ? dazed[0] : Pose(3));
        Begin(Move.Stagger, seconds);
        transform.position = new Vector3(transform.position.x, floorY, transform.position.z);
        enemy.damageTakenMultiplier = StaggerDamage;
        if (stars != null) Destroy(stars);
        stars = Vfx.Loop("stun", enemy.HeadPoint, seconds, 1f, parent: transform);
        DamagePopup.SpawnLabel(new Vector3(transform.position.x, enemy.BodyTopY + 0.6f, 0f), Loc.T("boss.opening"), new Color(1f, 0.85f, 0.35f));
        Sfx.Play("land_heavy", 0.8f, 0.6f);
        CameraFollow.Shake(0.2f, 0.2f);
    }

    void EndStagger()
    {
        enemy.damageTakenMultiplier = 1f;
        if (stars != null) Destroy(stars);
        stars = null;
    }

    // A frame of the swipe, held as a pose: 0 head down, 1 reared up with a paw raised, 2 paw thrown forward, 3 recover.
    Sprite Pose(int i)
    {
        var frames = enemy.definition.attackFrames;
        return frames != null && frames.Length > 0 ? frames[Mathf.Clamp(i, 0, frames.Length - 1)] : null;
    }

    void End()
    {
        bool special = move != Move.Roar && move != Move.Stagger;
        Begin(Move.None, 0f);
        enemy.AnimIdle();
        // Enraged: sometimes it goes straight into the next special.
        if (special && phase >= 3 && RunRng.Value < 0.4f) specialTimer = Mathf.Min(specialTimer, 0.35f);
    }

    // Both front paws come down: hurts a grounded player close by, sends shockwaves along the floor
    // (short in phase 2, across the whole arena when enraged) and shakes rocks loose from the ceiling
    // around the player - one right above them, the rest scattered a few steps either side.
    void Pound()
    {
        Sfx.Play("slam", 1f, 0.75f);
        // Where the paw lands: the front edge of the paw-down pose.
        var b = enemy.Renderer.bounds;
        var slamAt = new Vector3(enemy.Facing > 0 ? b.max.x - 0.35f : b.min.x + 0.35f, floorY, 0f);
        Vfx.Play("rupture", slamAt, 2f);                            // the floor splits where it lands
        Vfx.Play("ring_small", slamAt + Vector3.up * 0.2f, 2f, tint: new Color(1f, 0.8f, 0.6f)); // small: the dust + cracks carry the hit
        Vfx.Play("dust_big", slamAt + Vector3.left * 1.5f, 2f);
        Vfx.Play("dust_big", slamAt + Vector3.right * 1.5f, 2f, flipX: true);
        CameraFollow.Shake(0.4f, 0.3f);
        HitStop.Do(0.06f);
        var player = PlayerStats.Instance != null ? PlayerStats.Instance.Body : null;
        Vector2 to = enemy.ToPlayerFeet();
        if (player != null && !player.IsAirborne && Mathf.Abs(to.x) < SlamRadius && Mathf.Abs(to.y) < 1.5f)
            enemy.HitPlayer(1f, 2.5f, Mathf.Sign(to.x));

        float range = phase >= 3 ? 40f : 6f;
        BossShockwave.Spawn(slamAt, -1f, range, enemy);
        BossShockwave.Spawn(slamAt, 1f, range, enemy);
        DropRocks(phase >= 3 ? 5 : 3);
    }

    void DropRocks(int count)
    {
        var sprite = enemy.definition.fallingRock;
        if (sprite == null || PlayerStats.Instance == null) return;
        float playerX = transform.position.x + enemy.ToPlayerFeet().x;
        var map = StageMap.Instance;
        float minX = 1.5f, maxX = map != null ? map.mapSize.x - 1.5f : playerX + 10f;
        for (int i = 0; i < count; i++)
        {
            // 0 = on the player; then alternating sides, ~1.8 apart, so there is always a gap to step into.
            float offset = i == 0 ? 0f : ((i + 1) / 2) * 1.8f * (i % 2 == 0 ? 1f : -1f) + RunRng.Range(-0.3f, 0.3f);
            float x = Mathf.Clamp(playerX + offset, minX, maxX);
            BossRock.Spawn(x, floorY, i * 0.12f, sprite, enemy);
        }
    }

    void LateUpdate()
    {
        if (enemy == null) return;
        UpdateHealthBar();
    }

    // ------------------------------------------------------------------ HP bar
    GameObject barRoot;

    void BuildHealthBar()
    {
        var hud = RunManager.Instance != null ? RunManager.Instance.hudText : null;
        var canvas = hud != null && hud.canvas != null ? hud.canvas.rootCanvas : null;
        if (canvas == null) return;

        var root = barRoot = new GameObject("BossHealthBar", typeof(RectTransform));
        var rt = (RectTransform)root.transform;
        rt.SetParent(canvas.transform, false);
        rt.SetAsFirstSibling(); // under popups like the result screen
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -20f);
        rt.sizeDelta = new Vector2(560f, 84f); // name on top, the 48-tall bar under it

        barLabel = new GameObject("Name", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        barLabel.rectTransform.SetParent(rt, false);
        barLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        barLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        barLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
        barLabel.rectTransform.sizeDelta = new Vector2(0f, 34f);
        barLabel.font = hud.font;
        barLabel.fontSize = 28f;
        barLabel.alignment = TextAlignmentOptions.Center;
        barLabel.color = new Color(1f, 0.85f, 0.75f);
        barLabel.raycastTarget = false;

        // The medieval health bar from ToffeeCraft's UI bundle (Resources/UIBundle): a framed dark groove, 4 canvas
        // units per art pixel (48 tall), and its red as the fill. The groove is inset 6 / 5 px left / right, 3 px top / bottom.
        const float Px = 4f, BarW = 572f, FillW = BarW - (6f + 5f) * Px, FillH = 6f * Px;
        var back = MakeBar(rt, "Back", Color.white, new Vector2(0f, 0f), new Vector2(BarW, 12f * Px));
        BookUI.Slice(back.GetComponent<Image>(), "UIBundle/MedievalBarFrame", Px);
        var fillBg = MakeBar(back, "Empty", new Color(0f, 0f, 0f, 0f), new Vector2((6f - 5f) * Px * 0.5f, 0f), new Vector2(FillW, FillH));
        var fill = MakeBar(fillBg, "Fill", Color.white, Vector2.zero, new Vector2(FillW, FillH));
        barFill = fill.GetComponent<Image>();
        barFill.sprite = BookUI.Get("UIBundle/MedievalBarFill");
        // Image.fillAmount needs a sprite; a plain white one (tinted red) if the bundle piece is missing.
        if (barFill.sprite == null) { barFill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f)); barFill.color = new Color(0.86f, 0.16f, 0.2f, 1f); }
        barFill.type = Image.Type.Filled;
        barFill.fillMethod = Image.FillMethod.Horizontal;
        // Phase marks at 66% and 33%.
        foreach (float f in new[] { 0.66f, 0.33f })
            MakeBar(fillBg, "PhaseMark", new Color(1f, 0.9f, 0.6f, 0.9f), new Vector2((f - 0.5f) * FillW, 0f), new Vector2(Px, FillH));
        UpdateHealthBar();
        barRoot.SetActive(false); // appears with the entrance roar
    }

    static RectTransform MakeBar(Transform parent, string name, Color color, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, name == "Back" ? 0f : 0.5f);
        rt.pivot = new Vector2(0.5f, name == "Back" ? 0f : 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    void UpdateHealthBar()
    {
        if (barFill == null || enemy == null) return;
        barFill.fillAmount = enemy.MaxHP > 0 ? Mathf.Clamp01((float)enemy.HP / enemy.MaxHP) : 0f;
        string name = enemy.definition != null ? enemy.definition.displayName : Loc.T("boss.default_name");
        barLabel.text = phase >= 3 ? Loc.F("boss.enraged", name) : phase == 2 ? Loc.F("boss.phase2", name) : name;
        if (enemy.IsDead && barRoot != null) barRoot.SetActive(false);
    }
}
