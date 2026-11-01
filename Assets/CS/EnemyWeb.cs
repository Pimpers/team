using UnityEngine;

// The demon spider's spit (Enemy with definition.ranged): a ball of web that flies straight at where the cat was.
// A hit does the spider's damage and leaves web stuck on the cat - it moves at 60% speed for a moment (a net drawn
// over it meanwhile). Walls and the floor stop it. Dodge: jump over it, dash through it, or hit it out of the air.
// Art: pxl (Tools/pxl/pxl_spider_web.px.js) in Resources/EnemyFx, 16 px per unit like the enemies.
public class EnemyWeb : MonoBehaviour
{
    const float Speed = 7f, Range = 9f, HitRadius = 0.45f, SlowTime = 1.4f, SlowFactor = 0.6f, Scale = 1.4f;

    static Sprite[] frames;
    static Sprite stuck;

    Vector2 velocity;
    float travelled, damage, frameTimer;
    int frame;
    Enemy owner;
    SpriteRenderer sr;

    public static void Spawn(Vector3 from, Vector2 target, Enemy owner, float damage)
    {
        if (frames == null)
        {
            frames = new[] { Resources.Load<Sprite>("EnemyFx/web_ball_0"), Resources.Load<Sprite>("EnemyFx/web_ball_1") };
            stuck = Resources.Load<Sprite>("EnemyFx/web_stuck");
        }
        if (frames[0] == null) return;
        Vector2 dir = target - (Vector2)from;
        dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector2.right;
        var go = new GameObject("EnemyWeb");
        go.transform.position = from;
        go.transform.localScale = Vector3.one * Scale;
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg); // drawn flying right
        var w = go.AddComponent<EnemyWeb>();
        w.velocity = dir * Speed;
        w.owner = owner;
        w.damage = damage;
        w.sr = go.AddComponent<SpriteRenderer>();
        w.sr.sprite = frames[0];
        w.sr.sortingOrder = 7;
    }

    // Player attacks can swat it out of the air (PlayerAttack / arrows call this for anything nearby).
    public static void SwatAround(Vector2 centre, float radius)
    {
        foreach (var w in FindObjectsByType<EnemyWeb>())
            if (Vector2.Distance(w.transform.position, centre) < radius) w.Burst();
    }

    void Update()
    {
        frameTimer += Time.deltaTime;
        if (frameTimer > 0.08f) { frameTimer = 0f; frame = 1 - frame; sr.sprite = frames[frame] ?? frames[0]; }
        Vector2 step = velocity * Time.deltaTime;
        transform.position += (Vector3)step;
        travelled += step.magnitude;

        var stats = PlayerStats.Instance;
        if (stats != null)
        {
            var body = stats.Body;
            Vector2 cat = stats.transform.position; // the player's root is its body centre
            float half = body != null ? body.feetHalfWidth + HitRadius : 0.8f;
            float tall = body != null ? body.bodyHeight * 0.5f + HitRadius : 0.9f;
            Vector2 d = (Vector2)transform.position - cat;
            if (Mathf.Abs(d.x) < half && Mathf.Abs(d.y) < tall)
            {
                if (owner != null && owner.HitPlayer(damage, 0.6f, Mathf.Sign(velocity.x)))
                {
                    stats.Slow(SlowTime, SlowFactor);
                    StickTo(stats.transform);
                    DamagePopup.SpawnLabel(stats.transform.position + Vector3.up * 1.2f, Loc.T("enemy.web"), new Color(0.85f, 0.8f, 1f));
                }
                Burst();
                return;
            }
        }
        var map = StageMap.Instance;
        if (travelled > Range || (map != null && map.IsSolid(transform.position))) Burst();
    }

    // The net over the cat while it is slowed.
    static void StickTo(Transform cat)
    {
        if (stuck == null) return;
        var old = cat.Find("WebStuck");
        if (old != null) Destroy(old.gameObject);
        var go = new GameObject("WebStuck");
        go.transform.SetParent(cat, false);
        go.transform.localPosition = Vector3.zero;
        float s = cat.lossyScale.x != 0f ? 1.2f / Mathf.Abs(cat.lossyScale.x) : 1.2f;
        go.transform.localScale = Vector3.one * s;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = stuck;
        sr.sortingOrder = 20;
        Destroy(go, SlowTime);
    }

    void Burst()
    {
        Vfx.Play("dust_small", transform.position, 0.8f, tint: new Color(0.95f, 0.92f, 1f));
        Destroy(gameObject);
    }
}
