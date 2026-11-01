using UnityEngine;

// Small code-driven effects for the run skills, on top of the sprite sheets in Vfx:
//   Ghost  - a fading copy of a sprite (dash afterimages, the wind trail behind arrows).
//   Burst  - a handful of sprites flung out and fading (hearts, goo, sparkles).
//   Pop    - a sprite that springs up, holds and fades (the hiss "!").
//   Bar    - a thin stretched line between two points (the bell wire).
// Sizes: the cats are drawn at 100 px/unit x8 (1 px = 0.08 units), the skill props at 16 px/unit,
// so a prop drawn at PropScale has the same pixel size as the cat.
public static class SkillFx
{
    public const float PropScale = 1.3f;
    static Sprite pixel;

    // A copy of what `src` shows right now, tinted: an afterimage in the pixel-art way - fully opaque, one step
    // darker for its second half, then gone (no smooth alpha fade, which blurs the pixels). `tint.a` is ignored.
    public static void Ghost(SpriteRenderer src, Color tint, float life = 0.25f)
    {
        if (src == null || src.sprite == null) return;
        tint.a = 1f;
        var go = new GameObject("Ghost");
        go.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
        go.transform.localScale = src.transform.lossyScale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = src.sprite;
        sr.flipX = src.flipX;
        sr.sortingLayerID = src.sortingLayerID;
        sr.sortingOrder = src.sortingOrder - 1;
        sr.color = tint;
        var mover = go.AddComponent<SkillFxMover>();
        mover.Init(Vector2.zero, 0f, life, 0f);
        mover.stepFade = true;
    }

    // `count` copies of `sprite` thrown out from `at` (mostly upward), falling and fading.
    public static void Burst(Sprite sprite, Vector3 at, int count, float speed, float life, float scale = PropScale,
                             float gravity = 4f, Color? tint = null, int order = 9)
    {
        if (sprite == null) return;
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Burst");
            go.transform.position = at + (Vector3)(Random.insideUnitCircle * 0.25f);
            go.transform.localScale = Vector3.one * scale * Random.Range(0.75f, 1.1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.color = tint ?? Color.white;
            float a = Random.Range(25f, 155f) * Mathf.Deg2Rad;
            var v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed * Random.Range(0.6f, 1.1f);
            go.AddComponent<SkillFxMover>().Init(v, gravity, life * Random.Range(0.8f, 1.15f), Random.Range(-90f, 90f));
        }
    }

    // Springs up to full size, holds, then fades. Follows `parent` if given.
    public static GameObject Pop(Sprite sprite, Vector3 at, float life, float scale = PropScale, Transform parent = null, int order = 10)
    {
        if (sprite == null) return null;
        var go = new GameObject("Pop");
        go.transform.position = at;
        if (parent != null) go.transform.SetParent(parent, true);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        go.AddComponent<SkillFxPop>().Init(scale, life);
        return go;
    }

    // A straight line of the given thickness from `a` to `b`.
    public static SpriteRenderer Bar(Vector3 a, Vector3 b, float thickness, Color color, int order = 4)
    {
        if (pixel == null)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
        var go = new GameObject("Bar");
        Vector3 d = b - a;
        go.transform.position = (a + b) * 0.5f;
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        go.transform.localScale = new Vector3(d.magnitude, thickness, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = pixel;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }
}

// Drifts, falls, spins and fades, then removes itself (Ghost / Burst).
public class SkillFxMover : MonoBehaviour
{
    Vector2 velocity;
    float gravity, life, age, spin, startAlpha;
    SpriteRenderer sr;
    [System.NonSerialized] public bool stepFade; // afterimage: opaque, a darker tone for the last half, then gone
    Color startColor;

    public void Init(Vector2 velocity, float gravity, float life, float spin)
    {
        this.velocity = velocity;
        this.gravity = gravity;
        this.life = Mathf.Max(0.01f, life);
        this.spin = spin;
        sr = GetComponent<SpriteRenderer>();
        startAlpha = sr != null ? sr.color.a : 1f;
        startColor = sr != null ? sr.color : Color.white;
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= life) { Destroy(gameObject); return; }
        velocity.y -= gravity * Time.deltaTime;
        transform.position += (Vector3)(velocity * Time.deltaTime);
        if (spin != 0f) transform.Rotate(0f, 0f, spin * Time.deltaTime);
        if (sr != null && stepFade)
        {
            float k = age < life * 0.5f ? 1f : 0.55f;
            sr.color = new Color(startColor.r * k, startColor.g * k, startColor.b * k, 1f);
        }
        else if (sr != null)
        {
            var c = sr.color;
            c.a = startAlpha * (1f - age / life);
            sr.color = c;
        }
    }
}

// Scale 0 -> 1.25 -> 1 in the first 0.15 s, hold, fade over the last 0.2 s (SkillFx.Pop).
public class SkillFxPop : MonoBehaviour
{
    float scale, life, age;
    SpriteRenderer sr;

    public void Init(float scale, float life)
    {
        this.scale = scale;
        this.life = Mathf.Max(0.3f, life);
        sr = GetComponent<SpriteRenderer>();
        transform.localScale = Vector3.zero;
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= life) { Destroy(gameObject); return; }
        float k = age < 0.1f ? Mathf.Lerp(0f, 1.25f, age / 0.1f)
                : age < 0.15f ? Mathf.Lerp(1.25f, 1f, (age - 0.1f) / 0.05f) : 1f;
        transform.localScale = Vector3.one * scale * k;
        float left = life - age;
        if (sr != null && left < 0.2f) { var c = sr.color; c.a = left / 0.2f; sr.color = c; }
    }
}
