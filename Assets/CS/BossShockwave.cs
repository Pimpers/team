using UnityEngine;

// A crimson shockwave rolling along the floor from the boss's leap slam. Hits a player
// standing on the floor in its path once (jump over it, or dash through it), and breaks
// on walls or after `range` units. The sprite is drawn in code (a flame-like crescent).
public class BossShockwave : MonoBehaviour
{
    const float Speed = 10f;
    const float Damage = 0.5f;

    static Sprite sprite;

    float dir, remaining, floorY;
    bool hit;
    Enemy owner;
    SpriteRenderer sr;

    // Live waves, so the knight's tail guard (SkillSystem) can find and turn them around.
    public static readonly System.Collections.Generic.List<BossShockwave> All = new System.Collections.Generic.List<BossShockwave>();
    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    // Reflected: rolls back the other way, now hurting monsters (each once) instead of the player.
    int reflectDamage;
    readonly System.Collections.Generic.HashSet<Enemy> reflectedHits = new System.Collections.Generic.HashSet<Enemy>();
    public bool Reflected => reflectDamage > 0;

    public void Reflect(int damage)
    {
        if (Reflected) return;
        reflectDamage = Mathf.Max(1, damage);
        dir = -dir;
        remaining = Mathf.Max(remaining, 8f);
        transform.localScale = new Vector3(dir * 1.6f, 1.6f, 1f);
        sr.color = new Color(1f, 0.85f, 0.4f, sr.color.a); // golden: it's ours now
    }

    void HitMonsters()
    {
        Vector2 p = transform.position;
        foreach (var e in Enemy.All.ToArray())
        {
            if (e.IsDead || reflectedHits.Contains(e)) continue;
            if (Mathf.Abs(e.BodyX - p.x) > e.BodyHalfWidth + 0.6f || Mathf.Abs(e.transform.position.y - floorY) > 1.2f) continue;
            reflectedHits.Add(e);
            e.ReceiveDamage(reflectDamage, dir, true);
        }
    }

    public static void Spawn(Vector3 floorPos, float dir, float range, Enemy owner)
    {
        var go = new GameObject("BossShockwave");
        go.transform.position = floorPos;
        go.transform.localScale = new Vector3(dir * 1.6f, 1.6f, 1f); // 16x12 px at 16 ppu -> ~1.6 x 1.2 units
        var wave = go.AddComponent<BossShockwave>();
        wave.dir = dir;
        wave.remaining = range;
        wave.floorY = floorPos.y;
        wave.owner = owner;
        wave.sr = go.AddComponent<SpriteRenderer>();
        wave.sr.sprite = GetSprite();
        wave.sr.sortingOrder = 4;
    }

    void Update()
    {
        float step = Speed * Time.deltaTime;
        var p = transform.position;
        p.x += dir * step;
        transform.position = p;
        remaining -= step;

        // Flicker so it reads as energy, fade over the last stretch.
        var c = sr.color;
        c.a = Mathf.Clamp01(remaining / 1.5f) * (0.75f + 0.25f * Mathf.Sin(Time.time * 40f));
        sr.color = c;

        var map = StageMap.Instance;
        if (remaining <= 0f || (map != null && map.IsSolid(new Vector2(p.x + dir * 0.5f, floorY + 0.5f))))
        {
            Destroy(gameObject);
            return;
        }

        if (Reflected) { HitMonsters(); return; }

        var stats = PlayerStats.Instance;
        if (hit || stats == null || owner == null) return;
        var pc = stats.Body;
        Vector2 to = owner.ToPlayerFeet();
        float playerX = owner.transform.position.x + to.x;
        float playerFeet = owner.transform.position.y + to.y;
        bool onFloor = pc != null && !pc.IsAirborne && Mathf.Abs(playerFeet - floorY) < 0.6f;
        if (onFloor && Mathf.Abs(playerX - p.x) < 0.7f)
            hit = owner.HitPlayer(Damage, 1.5f, dir);
    }

    // 16x12 crescent of crimson flame, drawn once.
    static Sprite GetSprite()
    {
        if (sprite != null) return sprite;
        const int W = 16, H = 12;
        var tex = RuntimeGeneratedAssets.Texture(W, H);
        var px = new Color32[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                // Wave front on the right, tapering to the left and upwards.
                float front = 11f + 3f * Mathf.Sin(y * 0.6f);
                float height = H * (x / (float)W);
                bool inside = x <= front && y <= height;
                bool edge = inside && (x >= front - 2f || y >= height - 1.5f);
                px[y * W + x] = !inside ? new Color32(0, 0, 0, 0)
                    : edge ? new Color32(255, 214, 160, 255)
                    : new Color32(214, 40, 60, (byte)(120 + 100 * x / W));
            }
        tex.SetPixels32(px);
        tex.Apply();
        sprite = RuntimeGeneratedAssets.Sprite(tex, new Vector2(0.5f, 0f), 16f);
        return sprite;
    }
}
