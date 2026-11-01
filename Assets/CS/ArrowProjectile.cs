using System.Collections.Generic;
using UnityEngine;

// Meowolas's arrow. Flies straight, damages the first Enemy it reaches (or every enemy it
// passes through, for piercing shots), and disappears on hitting a solid tile or after its
// max range. Same distance-based hit checks as the rest of the project (no colliders).
// The archer's run skills (SkillSystem) reuse it for everything that flies: they set the extra
// fields on the returned instance - gravity + bounces (the yarn lob), homing, spin, a hit callback
// (poison, explosions), a pierce limit, a bigger hit box (the giant can) and a wind trail.
public class ArrowProjectile : MonoBehaviour
{
    Vector2 velocity;
    float speed;
    float remaining;
    int damage;
    bool crit; // rolled when fired (급소 찌르기 card)
    bool pierces;
    readonly HashSet<Enemy> alreadyHit = new HashSet<Enemy>();

    SpriteRenderer sr;
    Sprite[] frames;
    float fps;
    float frameTimer;
    int frame;

    // ---- Skill options (all off for a plain arrow)
    public System.Action<Enemy, Vector3> onHit;      // after the damage lands
    public System.Action<Vector3> onEnd;             // hit a wall / ran out of range / stopped by a target
    public System.Func<Enemy, int, int> modifyDamage; // e.g. more damage far away (캣타워 관측)
    public float gravity;          // units/s^2 pulling it down (lobbed yarn)
    public int bounces;            // times it hops off the floor before breaking
    public bool homing;            // steers toward the nearest monster
    public float homingTurn = 7f;  // radians per second
    public float spin;             // degrees per second (yarn ball) - otherwise it points along its flight
    public int maxHits = -1;       // piercing shots: stop after this many monsters (-1 = no limit)
    public float hitRadius;        // > 0: hits anything whose sprite box comes this close (the giant can)
    public float trailInterval;    // > 0: leaves fading copies of itself (바람의 시위)
    public Color trailColor = new Color(0.8f, 1f, 0.95f, 0.5f);
    public bool strong;            // counts as a strong hit (bigger flash, knockback)
    public bool harmless;          // flies through monsters without touching them (the thrown churu)
    public bool opensChest;        // true only for the archer's basic arrow
    float trailTimer;
    float homingRetarget;
    Enemy homingTarget;

    public Vector2 Velocity => velocity;
    public int Damage => damage;

    public static ArrowProjectile Spawn(Sprite sprite, Vector3 position, Vector2 direction, float speed, float range, int damage, float spriteScale)
    {
        return Spawn(new[] { sprite }, 0f, false, position, direction, speed, range, damage, spriteScale);
    }

    public static ArrowProjectile Spawn(Sprite[] frames, float fps, bool pierces, Vector3 position, Vector2 direction, float speed, float range, int damage, float spriteScale, bool crit = false)
    {
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;

        var go = new GameObject("Arrow");
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        // Put the sprite on a child offset by its own center, so rotating the parent spins
        // the arrow around its middle whatever the sprite's pivot is.
        var visual = new GameObject("Sprite");
        visual.transform.SetParent(go.transform, false);
        visual.transform.localScale = Vector3.one * spriteScale;
        var sr = visual.AddComponent<SpriteRenderer>();
        Sprite first = frames != null && frames.Length > 0 ? frames[0] : null;
        sr.sprite = first;
        sr.sortingOrder = 5;
        if (first != null)
            visual.transform.localPosition = -first.bounds.center * spriteScale;

        var arrow = go.AddComponent<ArrowProjectile>();
        arrow.velocity = direction * speed;
        arrow.speed = speed;
        arrow.remaining = range;
        arrow.damage = damage;
        arrow.crit = crit;
        arrow.pierces = pierces;
        arrow.sr = sr;
        arrow.frames = frames;
        arrow.fps = fps;
        return arrow;
    }

    public SpriteRenderer Renderer => sr;

    void Update()
    {
        if (frames != null && frames.Length > 1 && fps > 0f)
        {
            frameTimer += Time.deltaTime;
            if (frameTimer >= 1f / fps)
            {
                frameTimer -= 1f / fps;
                frame = (frame + 1) % frames.Length;
                sr.sprite = frames[frame];
                sr.transform.localPosition = -frames[frame].bounds.center * sr.transform.localScale.x;
            }
        }

        float dt = Time.deltaTime;
        if (homing) Steer(dt);
        if (gravity > 0f) velocity.y -= gravity * dt;

        Vector2 before = transform.position;
        Vector2 step = velocity * dt;
        transform.position += (Vector3)step;
        remaining -= step.magnitude;

        if (spin != 0f) transform.Rotate(0f, 0f, spin * dt);
        else if (velocity.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);

        if (trailInterval > 0f && (trailTimer -= dt) <= 0f)
        {
            trailTimer = trailInterval;
            SkillFx.Ghost(sr, trailColor, 0.18f);
        }

        Vector2 p = transform.position;
        bool solid = StageMap.Instance != null && StageMap.Instance.IsSolid(p);
        if (solid && bounces > 0 && velocity.y < 0f)
        {
            // Hop back up off the floor, losing some speed.
            bounces--;
            transform.position = new Vector3(p.x, before.y, 0f);
            velocity.y = Mathf.Abs(velocity.y) * 0.65f;
            velocity.x *= 0.85f;
            Vfx.Play("dust_small", new Vector3(p.x, before.y - 0.2f, 0f), 0.7f);
            solid = false;
        }
        if (remaining <= 0f || solid)
        {
            End();
            return;
        }

        if (harmless) return;
        if (opensChest && CardRewardChest.TryProjectileHit(p))
        {
            End();
            return;
        }
        var enemies = Enemy.All;
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            var enemy = enemies[i];
            if (enemy.IsDead || alreadyHit.Contains(enemy)) continue;
            if (!Touches(enemy, p)) continue;

            int dmg = modifyDamage != null ? modifyDamage(enemy, damage) : damage;
            enemy.ReceiveDamage(dmg, Mathf.Sign(velocity.x), pierces || crit || strong, crit, finisher: pierces); // the charged (piercing) shot counts as strong, and lands like the knight's finisher
            if (pierces && onHit == null) Vfx.Play("spark_big", transform.position, 1.1f); // the electro arrow crackles on impact
            onHit?.Invoke(enemy, transform.position);
            alreadyHit.Add(enemy);
            if (!pierces || (maxHits > 0 && alreadyHit.Count >= maxHits))
            {
                End();
                return;
            }
        }
    }

    bool Touches(Enemy enemy, Vector2 p)
    {
        var esr = enemy.Renderer;
        if (hitRadius > 0f)
        {
            Vector2 closest = esr != null ? (Vector2)esr.bounds.ClosestPoint(p) : (Vector2)enemy.transform.position;
            return Vector2.Distance(closest, p) <= hitRadius;
        }
        if (esr != null)
        {
            // Inside the enemy's sprite box, trimmed a bit at the sides/top where the
            // canvas is mostly empty - works for a tiny rat and a big bear alike.
            Bounds b = esr.bounds;
            return p.x > b.min.x + b.size.x * 0.15f && p.x < b.max.x - b.size.x * 0.15f &&
                   p.y > b.min.y && p.y < b.max.y - b.size.y * 0.15f;
        }
        return Vector2.Distance(enemy.transform.position, p) <= enemy.hitRadius * 0.6f;
    }

    void Steer(float dt)
    {
        homingRetarget -= dt;
        if (homingTarget == null || homingTarget.IsDead || homingRetarget <= 0f)
        {
            homingRetarget = 0.25f;
            homingTarget = SkillSystem.NearestEnemy(transform.position, 14f, alreadyHit);
        }
        if (homingTarget == null || homingTarget.Renderer == null) return;
        Vector2 to = (Vector2)homingTarget.Renderer.bounds.center - (Vector2)transform.position;
        float want = Mathf.Atan2(to.y, to.x);
        float have = Mathf.Atan2(velocity.y, velocity.x);
        float turned = Mathf.MoveTowardsAngle(have * Mathf.Rad2Deg, want * Mathf.Rad2Deg, homingTurn * Mathf.Rad2Deg * dt) * Mathf.Deg2Rad;
        float s = velocity.magnitude;
        velocity = new Vector2(Mathf.Cos(turned), Mathf.Sin(turned)) * s;
    }

    void End()
    {
        onEnd?.Invoke(transform.position);
        Destroy(gameObject);
    }
}
