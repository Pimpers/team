using System.Collections.Generic;
using UnityEngine;

// Things the archer's skills leave in the level (SkillSystem): the bell wire, the sticky mouse trap
// and the churu bait. Same body-box checks against Enemy.All as everything else (no physics).

// 방울 와이어: a wire along the floor with a bell at each end. Every half second it shocks the
// monsters standing on it (a spark, a jingle, a tiny stun).
public class SkillWire : MonoBehaviour
{
    static readonly System.Collections.Generic.List<Enemy> snapshot = new System.Collections.Generic.List<Enemy>(); // reused each tick
    float x0, x1, y, life, age, tickTimer, sparkTimer;
    System.Action<Enemy> onShock;
    SpriteRenderer line;
    readonly List<SpriteRenderer> bells = new List<SpriteRenderer>();

    public static void Spawn(Vector3 from, Vector3 to, float seconds, System.Action<Enemy> onShock)
    {
        var go = new GameObject("BellWire");
        var w = go.AddComponent<SkillWire>();
        w.x0 = Mathf.Min(from.x, to.x);
        w.x1 = Mathf.Max(from.x, to.x);
        w.y = from.y;
        w.life = seconds;
        w.onShock = onShock;
        w.line = SkillFx.Bar(from, to, 0.07f, new Color(0.85f, 0.9f, 1f, 0.95f));
        w.line.transform.SetParent(go.transform, true);
        var bell = Vfx.Sprite("bell");
        foreach (var p in new[] { from, to })
        {
            if (bell == null) break;
            var b = new GameObject("Bell");
            b.transform.SetParent(go.transform, false);
            b.transform.position = p + Vector3.up * 0.35f;
            b.transform.localScale = Vector3.one * SkillFx.PropScale;
            var sr = b.AddComponent<SpriteRenderer>();
            sr.sprite = bell;
            sr.sortingOrder = 5;
            w.bells.Add(sr);
        }
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= life) { Destroy(gameObject); return; }

        // Flicker as it crackles, fade out at the end.
        float a = Mathf.Clamp01((life - age) / 0.4f);
        line.color = new Color(0.85f, 0.9f, 1f, a * (0.75f + 0.25f * Mathf.Sin(age * 30f)));
        foreach (var b in bells) { var c = b.color; c.a = a; b.color = c; b.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(age * 12f) * 12f); }

        if ((sparkTimer -= Time.deltaTime) <= 0f)
        {
            sparkTimer = 0.3f;
            Vfx.Play("spark_mid1", new Vector3(Random.Range(x0, x1), y + 0.1f, 0f), 0.5f, tint: new Color(1f, 0.9f, 0.55f)); // gold, like the thunder hit
        }

        if ((tickTimer -= Time.deltaTime) > 0f) return;
        tickTimer = 0.5f;
        bool any = false;
        snapshot.Clear();
        snapshot.AddRange(Enemy.All); // a hit can remove a monster from the list mid-walk
        foreach (var e in snapshot)
        {
            if (e == null || e.IsDead) continue;
            if (e.BodyX + e.BodyHalfWidth < x0 || e.BodyX - e.BodyHalfWidth > x1) continue;
            if (Mathf.Abs(e.transform.position.y - y) > 0.8f) continue;
            Vfx.Play("p_thunder_hit", new Vector3(e.BodyX, y + 0.4f, 0f), 1f);
            onShock?.Invoke(e);
            e.Stun(0.25f, null);
            any = true;
        }
        if (any) Sfx.Play("ui_click", 0.8f, 2.2f); // the bells jingle
    }
}

// 끈적이 쥐덫: sits on the floor; the first monster to step on it snaps it, gets hurt and is stuck
// for a while. At most three are out at once (a fourth replaces the oldest).
public class SkillTrap : MonoBehaviour
{
    const int MaxTraps = 3;
    static readonly List<SkillTrap> live = new List<SkillTrap>();

    float life, age;
    System.Action<Enemy> onSnap;
    SpriteRenderer sr;
    float glint = 0.6f;

    public static void Spawn(Vector3 floor, float seconds, System.Action<Enemy> onSnap)
    {
        live.RemoveAll(t => t == null);
        if (live.Count >= MaxTraps) { Destroy(live[0].gameObject); live.RemoveAt(0); }
        var sprite = Vfx.Sprite("a_trap_fx");
        var go = new GameObject("StickyTrap");
        go.transform.position = floor; // the trap sprite's pivot is the bottom of its drawing
        go.transform.localScale = Vector3.one; // 32 px trap drawn at the cats' pixel size
        var t = go.AddComponent<SkillTrap>();
        t.life = seconds;
        t.onSnap = onSnap;
        t.sr = go.AddComponent<SpriteRenderer>();
        t.sr.sprite = sprite;
        t.sr.sortingOrder = 3;
        live.Add(t);
        // Make the placement read: a gold ring and a few sparks where it lands.
        Vfx.Play("ring_small", floor + Vector3.up * 0.25f, 1f, tint: new Color(1f, 0.9f, 0.5f));
        SkillFx.Burst(Vfx.Sprite("spark_mid1", 2), floor + Vector3.up * 0.2f, 5, 2.5f, 0.4f, 0.35f, 6f, new Color(1f, 0.9f, 0.4f));
    }

    void OnDestroy() => live.Remove(this);

    void Update()
    {
        age += Time.deltaTime;
        if (age >= life) { Destroy(gameObject); return; }
        // Drops in a little big, then settles; glints now and then so it stays easy to spot on the floor.
        float settle = Mathf.Clamp01(age / 0.15f);
        transform.localScale = Vector3.one * Mathf.Lerp(1.35f, 1f, settle);
        if ((glint -= Time.deltaTime) <= 0f)
        {
            glint = 1.2f;
            SkillFx.Pop(Vfx.Sprite("glint"), transform.position + Vector3.up * 0.55f, 0.35f, SkillFx.PropScale * 0.8f);
        }
        if (life - age < 1f && sr != null) sr.color = new Color(1f, 1f, 1f, (life - age) * (Mathf.FloorToInt(age * 10f) % 2 == 0 ? 1f : 0.4f));

        Vector2 p = transform.position;
        foreach (var e in Enemy.All)
        {
            if (e.IsDead || Mathf.Abs(e.BodyX - p.x) > e.BodyHalfWidth + 0.35f || Mathf.Abs(e.transform.position.y - p.y) > 0.9f) continue;
            Vfx.Play("trap_snap", p + Vector2.up * 0.2f, 1f);
            SkillFx.Burst(Vfx.Sprite("spark_mid1", 2), p, 6, 3f, 0.5f, 0.4f, 6f, new Color(1f, 0.9f, 0.4f));
            Sfx.Play("slam", 0.9f, 1.6f);
            onSnap?.Invoke(e);
            e.Stun(2.5f);
            Destroy(gameObject);
            return;
        }
    }
}

// 츄르 유인책: a churu on the floor. Monsters nearby (not bosses) walk to it and ignore the cat
// until it's gone.
public class SkillChuru : MonoBehaviour
{
    float life, age, pulse;
    float heart;
    GameObject loopFx;

    public static void Spawn(Vector3 floor, float seconds)
    {
        var go = new GameObject("Churu");
        go.transform.position = floor + Vector3.up * 0.3f;
        go.transform.rotation = Quaternion.Euler(0f, 0f, -35f); // lying on its side
        go.transform.localScale = Vector3.one * SkillFx.PropScale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Vfx.Sprite("churu");
        sr.sortingOrder = 4;
        var c = go.AddComponent<SkillChuru>();
        c.life = seconds;
        c.loopFx = Vfx.Loop("lure", floor + Vector3.up * 0.6f, seconds, 1f);
        Vfx.Play("charm", floor + Vector3.up * 0.6f, 0.5f); // a pink burst so the bait is seen landing (1.4 drew at x1: twice the cat)
        Vfx.Play("smoke", floor + Vector3.up * 0.3f, 0.5f);
        Sfx.Play("card_pick", 0.8f, 1.8f);
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= life)
        {
            Vfx.Play("smoke", transform.position, 0.5f);
            if (loopFx != null) Destroy(loopFx);
            Destroy(gameObject);
            return;
        }
        // Hearts keep rising from the bait while it lasts (the "smell" that pulls the monsters in).
        if ((heart -= Time.deltaTime) <= 0f)
        {
            heart = 0.6f;
            SkillFx.Burst(Vfx.Sprite("heart"), transform.position + Vector3.up * 0.4f, 1, 1.2f, 0.8f, SkillFx.PropScale * 0.6f, -1.2f);
        }
        if ((pulse -= Time.deltaTime) > 0f) return;
        pulse = 0.25f;
        Vector2 p = transform.position;
        foreach (var e in Enemy.All)
        {
            if (e.IsDead || e.isBoss || Vector2.Distance(e.transform.position, p) > 8f) continue;
            if (!e.IsLured) SkillFx.Burst(Vfx.Sprite("heart"), e.HeadPoint, 1, 1.5f, 0.6f, SkillFx.PropScale * 0.5f, -1.5f);
            e.Lure(p, 0.4f);
        }
    }
}
