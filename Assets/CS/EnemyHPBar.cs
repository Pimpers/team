using UnityEngine;

// A small health bar under a monster's feet (not the boss: it has its own at the top of the screen). It appears with
// the first hit and stays until the monster dies: a dark rim, the red that's left (gold for an elite), and a pale
// trail that drains after it a moment later so a hit reads as a chunk taken out. Pixel art: 1/16-unit steps like the
// sprites, its own object (the monster's body squashes when hit; the bar shouldn't).
public class EnemyHPBar : MonoBehaviour
{
    const float Px = 1f / 16f, Height = 2f * Px, Above = 3f * Px, TrailDelay = 0.35f, TrailSpeed = 1.6f;
    const float ShowRange = 9f, HideRange = 11f; // bar shows within 9 units of the cat, hides past 11 (a gap so it doesn't flicker on the edge)
    static readonly Color Rim = new Color32(0x20, 0x19, 0x32, 0xff), Empty = new Color32(0x3f, 0x1a, 0x41, 0xff),
                          Red = new Color32(0xe0, 0x41, 0x4a, 0xff), Gold = new Color32(0xff, 0xd7, 0x5e, 0xff),
                          Trail = new Color32(0xff, 0xe8, 0xc8, 0xff);
    static Sprite white;

    Enemy enemy;
    SpriteRenderer rim, empty, trail, fill;
    float width, shown = 1f, trailValue = 1f, trailWait;
    bool visible = true; // far-away bars are hidden; this is the current show/hide state (hysteresis)

    public static EnemyHPBar Attach(Enemy e)
    {
        var go = new GameObject("HPBar_" + e.name);
        var bar = go.AddComponent<EnemyHPBar>();
        bar.enemy = e;
        // Keep it smaller than the body so groups do not become rows of red boxes.
        bar.width = Mathf.Clamp(Mathf.Round(e.BodyHalfWidth * 1.6f / Px), 10f, 22f) * Px;
        int order = e.Renderer != null ? e.Renderer.sortingOrder + 5 : 10;
        bar.rim = bar.Part("Rim", Rim, order);
        bar.empty = bar.Part("Empty", Empty, order + 1);
        bar.trail = bar.Part("Trail", Trail, order + 2);
        bar.fill = bar.Part("Fill", e.IsElite ? Gold : Red, order + 3);
        bar.LateUpdate();
        return bar;
    }

    SpriteRenderer Part(string name, Color c, int order)
    {
        if (white == null)
        {
            var tex = RuntimeGeneratedAssets.Texture(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            white = RuntimeGeneratedAssets.Sprite(tex, new Vector2(0f, 0.5f), 1f);
        }
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = white;
        sr.color = c;
        sr.sortingOrder = order;
        return sr;
    }

    void LateUpdate()
    {
        if (enemy == null || enemy.IsDead) { Destroy(gameObject); return; }
        // Encounter controllers deactivate future-room enemies, but this bar is deliberately
        // not their child. Hide it with its owner so sealed rooms do not reveal phantom HP bars.
        if (!enemy.gameObject.activeInHierarchy)
        {
            SetRenderers(false);
            return;
        }

        // Hide the bar while the cat is far away (declutters the screen); a show/hide gap stops it flickering
        // for an enemy sitting right on the edge. An elite stays visible a bit farther - it's worth tracking.
        var stats = PlayerStats.Instance;
        if (stats != null)
        {
            float dx = enemy.transform.position.x - stats.transform.position.x;
            float dy = enemy.transform.position.y - stats.transform.position.y;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float bonus = enemy.IsElite ? 3f : 0f;
            if (visible && dist > HideRange + bonus) visible = false;
            else if (!visible && dist < ShowRange + bonus) visible = true;
        }
        bool onCamera = true;
        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 centre = enemy.Renderer != null ? enemy.Renderer.bounds.center : enemy.transform.position;
            Vector3 viewport = cam.WorldToViewportPoint(centre);
            onCamera = viewport.z > 0f && viewport.x > 0.05f && viewport.x < 0.95f && viewport.y > 0.08f && viewport.y < 0.92f;
        }
        if (!visible || !onCamera)
        {
            SetRenderers(false);
            return;
        }
        SetRenderers(true);

        float frac = enemy.MaxHP > 0 ? Mathf.Clamp01((float)enemy.HP / enemy.MaxHP) : 0f;
        if (frac < shown) trailWait = TrailDelay; // a fresh hit: the trail waits, then drains
        shown = frac;
        if (trailValue < shown) trailValue = shown;
        if ((trailWait -= Time.deltaTime) <= 0f) trailValue = Mathf.MoveTowards(trailValue, shown, TrailSpeed * Time.deltaTime);

        // Above the head, centred on the body, snapped to the pixel grid.
        float x = Mathf.Round((enemy.BodyX - width * 0.5f) / Px) * Px;
        float y = Mathf.Round((enemy.BodyTopY + Above) / Px) * Px;
        transform.position = new Vector3(x, y, 0f);
        Set(rim, -Px, width + 2f * Px, Height + 2f * Px);
        Set(empty, 0f, width, Height);
        Set(trail, 0f, PixelWidth(trailValue), Height);
        Set(fill, 0f, PixelWidth(shown), Height);
        fill.color = enemy.IsElite ? Gold : GameSettings.Colorblind ? (Color)UiTheme.EnemyMark : Red;
    }

    void SetRenderers(bool enabled)
    {
        if (rim != null) rim.enabled = enabled;
        if (empty != null) empty.enabled = enabled;
        if (trail != null) trail.enabled = enabled;
        if (fill != null) fill.enabled = enabled;
    }

    // Whole pixels, and never zero while it's alive.
    float PixelWidth(float frac) => Mathf.Max(frac > 0f ? Px : 0f, Mathf.Round(width * frac / Px) * Px);

    static void Set(SpriteRenderer sr, float x, float w, float h)
    {
        sr.enabled = w > 0f;
        sr.transform.localPosition = new Vector3(x, 0f, 0f);
        sr.transform.localScale = new Vector3(w, h, 1f);
    }
}
