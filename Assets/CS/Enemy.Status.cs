using System.Collections.Generic;
using UnityEngine;

// Enemy: the status effects the player's skills put on it (stun, lure, poison).
public partial class Enemy
{
    // ---------------------------------------------------------------- skill status effects
    // Set by the player's run skills (SkillSystem). Bosses shrug most of it off: stuns last a third
    // as long, lures don't work, and a special move (charge, leap) is never interrupted.
    float stunTimer, lureTimer, poisonTimer, poisonTick;
    int poisonDamage;
    Vector2 lurePoint;
    GameObject stunFx, poisonFx;

    public bool IsStunned => stunTimer > 0f;
    public bool IsLured => lureTimer > 0f;
    public bool IsPoisoned => poisonTimer > 0f;
    // Just above the head, where status icons float.
    // Top of the body itself. The sprite canvas has empty space above most monsters (a rat fills only the
    // bottom of its 32x32 cell), so bounds.max.y put the "!" and the elite tag a couple of tiles up in the air.
    public float BodyTopY => definition != null
        ? transform.position.y + definition.hitRadius * 1.3f * transform.localScale.x / definition.scale
        : (sr != null ? sr.bounds.max.y : transform.position.y + 1f);
    public Vector3 HeadPoint => new Vector3(transform.position.x, BodyTopY + 0.25f, 0f);

    // Can't move or attack for `seconds`. `fx` loops over the head meanwhile (null = none).
    public void Stun(float seconds, string fx = "stun")
    {
        if (IsDead || definition == null || seconds <= 0f) return;
        if (isBoss) seconds *= 0.35f;
        if (PlayerStats.Instance != null) seconds *= PlayerStats.Instance.StatusDurationMultiplier; // 사냥 세트
        if (seconds <= stunTimer) return;
        stunTimer = seconds;
        lureTimer = 0f;
        if (stunFx != null) Destroy(stunFx);
        if (!string.IsNullOrEmpty(fx)) stunFx = Vfx.Loop(fx, HeadPoint, seconds, 1f, parent: transform);
    }

    // Walks to `point` (and doesn't attack) for `seconds`.
    public void Lure(Vector2 point, float seconds)
    {
        if (IsDead || isBoss || definition == null) return;
        lurePoint = point;
        if (PlayerStats.Instance != null) seconds *= PlayerStats.Instance.StatusDurationMultiplier; // 사냥 세트
        lureTimer = Mathf.Max(lureTimer, seconds);
    }

    // `perTick` damage every half second for `seconds`, green bubbles meanwhile.
    public void Poison(int perTick, float seconds)
    {
        if (IsDead || perTick <= 0) return;
        poisonDamage = Mathf.Max(poisonDamage, perTick);
        if (poisonTimer <= 0f) poisonTick = 0.5f;
        poisonTimer = Mathf.Max(poisonTimer, seconds);
        if (poisonFx != null) Destroy(poisonFx);
        Vector3 at = sr != null ? sr.bounds.center : transform.position;
        poisonFx = Vfx.Loop("poison_loop", at, poisonTimer, isBoss ? 1.6f : 1f, tint: new Color(1f, 1f, 1f, 0.85f), parent: transform);
    }

    void UpdatePoison()
    {
        if (poisonTimer <= 0f) return;
        poisonTimer -= Time.deltaTime;
        poisonTick -= Time.deltaTime;
        if (poisonTick > 0f) return;
        poisonTick = 0.5f;
        ReceiveDamage(poisonDamage, quiet: true, quietColor: new Color(0.55f, 0.95f, 0.35f));
        if (poisonTimer <= 0f) poisonDamage = 0;
    }

    bool UpdateControlStatus()
    {
        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            if (state == State.Hurt)
            {
                hurtTimer -= Time.deltaTime;
                if (hurtTimer <= 0f) Play(State.Idle);
            }
            else if (state != State.Idle) Play(State.Idle);
            return true;
        }
        if (lureTimer > 0f)
        {
            lureTimer -= Time.deltaTime;
            if (state == State.Hurt) return false; // let the flinch play out first
            float dx = lurePoint.x - transform.position.x;
            bool walked = Mathf.Abs(dx) > 0.35f && TryWalk(Mathf.Sign(dx) * definition.moveSpeed * 1.2f * speedMultiplier * Time.deltaTime);
            if (Mathf.Abs(dx) > 0.35f) Face(dx);
            SetMoving(walked);
            return true;
        }
        return false;
    }

    void ClearStatus()
    {
        stunTimer = lureTimer = poisonTimer = 0f;
        if (stunFx != null) Destroy(stunFx);
        if (poisonFx != null) Destroy(poisonFx);
    }

}
