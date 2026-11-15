using System.Collections.Generic;
using UnityEngine;

// Enemy: patrol, chase, attack, contact damage, hit feel (fields and lifecycle are in Enemy.cs).
public partial class Enemy
{
    // ---------------------------------------------------------------- behaviour
    Vector2 Center => sr != null ? (Vector2)sr.bounds.center : (Vector2)transform.position;
    // The player's root sits at its body centre (the visual is aligned to it); its sprite
    // bounds would drift with wide sword-swing frames.
    Vector2 PlayerCenter => (Vector2)player.position;

    // Body centre X and half width, for the player's melee hit check. Defined enemies have
    // a bottom-centre pivot, so the transform is the body's middle regardless of the frame.
    public float BodyX => definition != null ? transform.position.x : Center.x;
    public float BodyHalfWidth => definition != null ? definition.bodyHalfWidth : hitRadius * 0.5f;

    // From our feet (the transform, bottom-center pivot) to the player's feet. Feet-to-feet
    // works the same for a small rat and a huge bear, unlike sprite centers.
    public Vector2 ToPlayerFeet()
    {
        float playerFeetY = player.position.y - (playerController != null ? playerController.feetOffset : 0f);
        return new Vector2(player.position.x - transform.position.x, playerFeetY - transform.position.y);
    }

    // Bites/claws reach a player level with us or a little above (jumping at us), but not one below our
    // feet: a rat on a ledge can't bite a cat jumping up from underneath.
    static bool CanReachHeight(float playerFeetDy) => playerFeetDy > -0.6f && playerFeetDy < 2f;

    // Touching a monster's body hurts too, at most once per ContactCooldown (the player's hurt-invulnerability
    // still applies, so dashing or plunging through is safe). Pushes the player away from the body.
    const float ContactCooldown = 1f;
    float contactTimer;

    void UpdateContactDamage()
    {
        if (contactTimer > 0f) { contactTimer -= Time.deltaTime; return; }
        if (state == State.Hurt) return; // flinching from the cat's hit: a sword cat standing close isn't punished for it
        if (player == null || PlayerStats.Instance == null || contactDamage <= 0f || PlayerStats.Instance.CurrentHP <= 0f) return;
        Vector2 d = ToPlayerFeet();
        float playerHalfWidth = playerController != null ? playerController.feetHalfWidth : 0.4f;
        float bodyHeight = definition.hitRadius * 1.3f;
        if (Mathf.Abs(d.x) > (BodyHalfWidth + playerHalfWidth) * 0.8f) return;
        // Only from the side or from above: a cat jumping up past a ledge where the monster stands isn't
        // hurt and knocked back down (it couldn't strike up there either, so the ledge was unclimbable).
        if (d.y > bodyHeight || d.y < -0.3f) return;
        if (HitPlayer(contactDamage, knockbackDistance, Mathf.Abs(d.x) > 0.01f ? Mathf.Sign(d.x) : -facing))
            contactTimer = ContactCooldown;
    }

    const float ReactionTime = 0.55f;
    float reactTimer;
    float provoked; // > 0 after being hit: chases from up to 3x its sight range

    void UpdateMovement()
    {
        if (UpdateVerticalHopWindup()) return;
        if (UpdateVerticalHop()) return;
        if (verticalHopCooldown > 0f) verticalHopCooldown -= Time.deltaTime;
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;
        bool playerAlive = player != null && PlayerStats.Instance != null && PlayerStats.Instance.CurrentHP > 0f;

        float dir = 0f;
        if (alertTimer > 0f) alertTimer -= Time.deltaTime;
        if (provoked > 0f) provoked -= Time.deltaTime;
        if (playerAlive)
        {
            Vector2 toPlayer = ToPlayerFeet();
            bool sameLevel = Mathf.Abs(toPlayer.y) < 2f;
            bool verticalRoom = StageMap.Instance != null && StageMap.Instance.verticalLayout;
            bool sees = engaged ? Mathf.Abs(toPlayer.y) < 4.5f
                : verticalRoom ? Mathf.Abs(toPlayer.x) < definition.aggroRange && Mathf.Abs(toPlayer.y) < 6f
                : sameLevel && Mathf.Abs(toPlayer.x) < definition.aggroRange;
            if (sees)
            {
                // Just noticed the player: its role as an icon over the head (RoleIcon).
                if (!wasAlerted)
                {
                    RoleIcon.Show(new Vector3(transform.position.x, BodyTopY, 0f), definition.combatRole);
                    if (definition.combatRole == EnemyDefinition.CombatRole.Ambusher) ambushTimer = 1.1f;
                    // A beat to react before it charges in (never an instant bite from off-screen).
                    if (!isBoss) reactTimer = ReactionTime;
                    // And a little hop on the spot: the body says "I saw you" even when the icon is missed.
                    if (!isBoss) { hopHeight = 0.3f; hopTimer = HopTime; }
                }
                alertTimer = definition.alertTime;
            }
            wasAlerted = alertTimer > 0f;
            // Once alerted it keeps chasing a while, even when the player hops onto a ledge.
            bool chasing = sees || (alertTimer > 0f && Mathf.Abs(toPlayer.y) < 6f && Mathf.Abs(toPlayer.x) < definition.aggroRange * (provoked > 0f ? 3f : 1.5f));
            if (chasing && verticalRoom && TryStartVerticalHop(toPlayer)) return;
            // Player right above (on a ledge): wait underneath instead of flipping
            // left/right every frame as the tiny x difference changes sign.
            if (chasing && Mathf.Abs(toPlayer.y) >= 2f && Mathf.Abs(toPlayer.x) < 0.5f)
            {
                SetMoving(false);
                return;
            }
            if (chasing)
            {
                Face(toPlayer.x);
                if (reactTimer > 0f)
                {
                    reactTimer -= Time.deltaTime;
                    SetMoving(false);
                    return;
                }
                if (definition.ranged)
                {
                    // Shooter: spit when in range with a clear line, back off while reloading if the cat is close.
                    float ax = Mathf.Abs(toPlayer.x);
                    if (ax <= definition.attackRange && Mathf.Abs(toPlayer.y) < 4.5f && ClearShot())
                    {
                        if (cooldownTimer <= 0f) { StartAttack(); return; }
                        if (ax < definition.keepAway && TryWalk(-Mathf.Sign(toPlayer.x) * definition.moveSpeed * speedMultiplier * Time.deltaTime))
                        {
                            SetMoving(true); // backing away, still facing the cat
                            return;
                        }
                        SetMoving(false);
                        return;
                    }
                    dir = Mathf.Sign(toPlayer.x);
                }
                else if (CanReachHeight(toPlayer.y) && Mathf.Abs(toPlayer.x) <= definition.attackRange + definition.lungeDistance)
                {
                    if (cooldownTimer <= 0f) { StartAttack(); return; }
                    SetMoving(false);
                    return;
                }
                else dir = Mathf.Sign(toPlayer.x);
            }
        }

        if (dir == 0f) dir = PatrolDirection();

        if (dir != 0f && TryWalk(dir * definition.moveSpeed * speedMultiplier * (ambushTimer > 0f ? 1.65f : 1f) * Time.deltaTime))
            SetMoving(true);
        else
        {
            SetMoving(false);
            if (dir != 0f) patrolTargetX = transform.position.x; // blocked: pick a new patrol spot
        }
    }


    bool UpdateVerticalHopWindup()
    {
        if (verticalHopWindup <= 0f) return false;
        verticalHopWindup -= Time.deltaTime;
        SetMoving(false);
        if (verticalHopWindup <= 0f)
        {
            verticalHop = true;
            verticalHopElapsed = 0f;
            verticalHopFrom = transform.position;
            Play(State.Move);
            Vfx.Play("dust_small", transform.position, 0.7f);
        }
        return true;
    }

    bool UpdateVerticalHop()
    {
        if (!verticalHop) return false;
        const float Duration = 0.42f;
        verticalHopElapsed += Time.deltaTime;
        float k = Mathf.Clamp01(verticalHopElapsed / Duration);
        Vector3 p = Vector3.Lerp(verticalHopFrom, verticalHopTo, k);
        p.y += 1.35f * 4f * k * (1f - k);
        transform.position = p;
        SetMoving(true);
        if (k >= 1f)
        {
            verticalHop = false;
            transform.position = verticalHopTo;
            spawnX = patrolTargetX = verticalHopTo.x;
            verticalHopCooldown = RunRng.Range(1.4f, 2.3f);
            Play(State.Idle);
            Vfx.Play("dust_small", transform.position, 0.7f);
        }
        return true;
    }

    bool TryStartVerticalHop(Vector2 toPlayer)
    {
        if (verticalHop || verticalHopCooldown > 0f || isBoss || definition.ranged || Mathf.Abs(toPlayer.y) < 2.2f) return false;
        var map = StageMap.Instance;
        if (map == null || map.spawnSlots == null) return false;
        float direction = Mathf.Sign(toPlayer.y);
        Vector2 best = default;
        float bestScore = float.MaxValue;
        foreach (var slot in map.spawnSlots)
        {
            float rise = (slot.y - transform.position.y) * direction;
            if (rise < 1.5f || rise > 5.2f || Mathf.Abs(slot.x - transform.position.x) > 7f) continue;
            if (map.CrossesHorizontalBarrier(transform.position.y, slot.y)) continue;
            float score = Mathf.Abs(slot.y - (transform.position.y + toPlayer.y)) + Mathf.Abs(slot.x - player.position.x) * 0.25f;
            if (score < bestScore) { bestScore = score; best = slot; }
        }
        if (bestScore == float.MaxValue) return false;
        verticalHopWindup = 0.32f;
        verticalHopTo = new Vector3(best.x, best.y, transform.position.z);
        Face(best.x - transform.position.x);
        squash = 0.22f;
        squashTimer = 0.32f;
        DamagePopup.SpawnLabel(HeadPoint, direction > 0f ? Loc.T("enemy.leap_up") : Loc.T("enemy.leap_down"), new Color(1f, 0.72f, 0.3f));
        return true;
    }

    float PatrolDirection()
    {
        float x = transform.position.x;
        if (Mathf.Abs(patrolTargetX - x) < 0.1f)
        {
            patrolWait -= Time.deltaTime;
            if (patrolWait > 0f) return 0f;
            patrolTargetX = spawnX + RunRng.Range(-definition.patrolRange, definition.patrolRange);
            patrolWait = RunRng.Range(0.3f, 1.2f);
            return 0f;
        }
        float d = Mathf.Sign(patrolTargetX - x);
        Face(d);
        // Patrol at a stroll; chasing uses full speed.
        return d * 0.7f;
    }

    // Moves horizontally if there is ground under the leading edge and no wall in front.
    public bool TryWalk(float dx)
    {
        var map = StageMap.Instance;
        float newX = transform.position.x + dx;
        if (map != null)
        {
            float lead = newX + Mathf.Sign(dx) * definition.bodyHalfWidth;
            float feet = transform.position.y;
            bool ground = map.IsSolid(new Vector2(lead, feet - 0.5f)) || map.IsPlatform(new Vector2(lead, feet - 0.5f));
            bool wall = map.IsSolid(new Vector2(lead, feet + 0.5f));
            if (!ground || wall) return false;
        }
        var p = transform.position;
        p.x = newX;
        transform.position = p;
        return true;
    }

    // A route may move this enemy after StageRandomizer has placed it. Reset its patrol home too,
    // so it does not walk back to its old location before the player reaches it.
    public void Relocate(Vector2 feet)
    {
        transform.position = new Vector3(feet.x, feet.y, transform.position.z);
        spawnX = patrolTargetX = feet.x;
        patrolWait = RunRng.Range(0.2f, 1f);
    }

    public void Face(float dx)
    {
        if (Mathf.Abs(dx) < 0.01f) return;
        facing = dx > 0f ? 1 : -1;
        sr.flipX = facing < 0;
    }

    void SetMoving(bool moving)
    {
        var want = moving ? State.Move : State.Idle;
        if (state != want) Play(want);
    }

    void StartAttack()
    {
        attackLanded = false;
        strikesLeft = extraStrikes;
        Play(State.Attack);
    }

    void UpdateAttack()
    {
        // Lunge at the player during the wind-up, so the bite/stab closes the gap.
        float lunge = definition.lungeDistance;
        if (definition.combatRole == EnemyDefinition.CombatRole.Ambusher) lunge = Mathf.Max(lunge, 1.25f);
        if (!attackLanded && lunge > 0f)
        {
            float windup = Mathf.Max(1, definition.attackHitFrame) / definition.attackFps;
            TryWalk(facing * lunge / windup * Time.deltaTime);
        }
        if (!attackLanded && frame >= definition.attackHitFrame)
        {
            attackLanded = true;
            TryHitPlayer();
        }
        if (frame >= frames.Length - 1 && frameTimer >= 1f / fps * 0.99f)
        {
            if (strikesLeft > 0)
            {
                // Chained claw (enraged boss): turn to the player and swing again.
                strikesLeft--;
                attackLanded = false;
                if (player != null) Face(ToPlayerFeet().x);
                Play(State.Attack);
                return;
            }
            cooldownTimer = definition.attackCooldown * cooldownMultiplier;
            Play(State.Idle);
        }
    }

    // "a|b" plays each id at the point (several layers of one effect).
    static void PlayEffects(string ids, Vector3 at, float scale)
    {
        if (string.IsNullOrEmpty(ids)) return;
        foreach (var id in ids.Split('|')) Vfx.Play(id, at, scale);
    }

    // The bite/stab/claw connects if the player is in front of us and within reach.
    // The spit's mouth: the front of the body, a little above the middle.
    Vector3 Mouth => new Vector3(transform.position.x + facing * definition.bodyHalfWidth * 0.9f, Center.y, 0f);

    // Nothing solid between the mouth and the player's body (or its head, over a low step).
    bool ClearShot() => ClearLine(Mouth, PlayerCenter) || ClearLine(Mouth, PlayerCenter + Vector2.up * 0.45f);

    static bool ClearLine(Vector2 a, Vector2 b)
    {
        var map = StageMap.Instance;
        if (map == null) return true;
        int steps = Mathf.CeilToInt(Vector2.Distance(a, b) / 0.25f);
        for (int i = 1; i < steps; i++)
            if (map.IsSolid(Vector2.Lerp(a, b, (float)i / steps))) return false;
        return true;
    }

    void TryHitPlayer()
    {
        if (definition != null && definition.ranged)
        {
            if (definition != null && !string.IsNullOrEmpty(definition.attackSound)) Sfx.Play(definition.attackSound);
            else Sfx.Play("swing", 0.7f, 1.6f);
            if (player != null) EnemyWeb.Spawn(Mouth, PlayerCenter, this, contactDamage);
            return;
        }
        if (definition != null && !string.IsNullOrEmpty(definition.attackEffect))
            PlayEffects(definition.attackEffect, new Vector3(transform.position.x + facing * definition.attackRange * 0.9f, transform.position.y, 0f), definition.attackEffectScale);
        if (definition != null && !string.IsNullOrEmpty(definition.attackSound)) Sfx.Play(definition.attackSound);
        if (player == null || PlayerStats.Instance == null) return;
        Vector2 toPlayer = ToPlayerFeet();
        bool inFront = Mathf.Sign(toPlayer.x) == facing || Mathf.Abs(toPlayer.x) < 0.3f;
        if (!inFront || Mathf.Abs(toPlayer.x) > definition.attackRange + 0.4f || toPlayer.y > 1.5f || !CanReachHeight(toPlayer.y)) return;
        HitPlayer(contactDamage, knockbackDistance, facing);
    }

    // Damages the player and pushes them along `dirX`; does nothing if the hit was dodged.
    public bool HitPlayer(float damage, float knock, float dirX)
    {
        string source = definition != null ? (IsElite ? Loc.F("enemy.elite_name", definition.displayName) : definition.displayName) : Loc.T("damage.enemy_attack");
        if (PlayerStats.Instance == null || !PlayerStats.Instance.TakeDamage(damage, source)) return false;
        CameraFollow.Kick(new Vector2(dirX >= 0f ? 1f : -1f, 0.35f), 0.16f); // the view lurches with the knockback
        if (playerController != null)
            playerController.ApplyKnockback(new Vector2(dirX >= 0f ? 1f : -1f, 0f), knock);
        return true;
    }

    // Animation control for BossBrain's special moves (restarts only on a change).
    public void AnimIdle() { if (state != State.Idle) Play(State.Idle); }
    public void AnimMove() { if (state != State.Move) Play(State.Move); }
    public void AnimAttack() { if (state != State.Attack) Play(State.Attack); }
    // Holds one pose until the next Anim* call (the bear rearing up, claws raised, before it pounds).
    public void AnimHold(Sprite pose)
    {
        if (pose == null) return;
        state = State.Attack;
        frames = new[] { pose };
        frame = 0;
        frameTimer = 0f;
        sr.sprite = pose;
    }
    public bool IsAttacking => state == State.Attack;
    public int Facing => facing;

    void LateUpdate()
    {
        if (definition == null) return;
        float k = squashTimer > 0f ? squashTimer / SquashTime : 0f;
        k *= k; // snaps back fast at the end
        transform.localScale = new Vector3(baseScale.x * (1f + squash * k), baseScale.y * (1f - squash * k), 1f); // feet stay put (bottom pivot)
        var off = Vector3.zero;
        if (Time.unscaledTime < jitterUntil)
        {
            float pixel = baseScale.x / 16f; // one of its own pixels (16 px per unit)
            off.x = ((int)(Time.unscaledTime * 40f) % 2 == 0 ? 1f : -1f) * pixel * Mathf.Max(0.5f, GameSettings.ShakeStrength);
        }
        if (hopTimer > 0f)
        {
            float t = 1f - hopTimer / HopTime;
            off.y = 4f * hopHeight * t * (1f - t);
        }
        transform.position += off;
        drawnOffset = off;
    }

    // The body's reaction to a hit: squash (more for a strong one), a shake while frozen.
    void HitReaction(bool strong, bool finisher)
    {
        float amount = finisher ? 0.32f : strong ? 0.24f : 0.16f;
        if (isBoss || (definition != null && definition.superArmor)) amount *= 0.5f; // the bear barely gives
        squash = amount;
        squashTimer = SquashTime;
        jitterUntil = Time.unscaledTime + (finisher ? 0.16f : strong ? 0.11f : 0.07f);
    }

    void UpdateDead()
    {
        deadTimer += Time.deltaTime;
        // Lie there briefly after the death animation, then fade out.
        float animTime = definition.deathFrames.Length / definition.deathFps;
        if (deadTimer > animTime + 0.6f)
        {
            float a = 1f - (deadTimer - animTime - 0.6f) / 0.5f;
            sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            if (a <= 0f) Destroy(gameObject);
        }
    }

}
