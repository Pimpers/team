using UnityEngine;

// What an enemy is about to do, as a small icon over its head when it notices the cat (Resources/EnemyFx/role_*.png:
// a "!" bubble = chases you, a target = shoots from afar, a shield = tough, ">>" = charges). It replaced the words
// 추격! / 원거리! / 단단함! / 돌진!, which piled up with the damage numbers and banners in a busy fight.
public class RoleIcon : MonoBehaviour
{
    // Bigger (0.6 was hard to pick out mid-fight), on a dark round plate so a busy background cannot swallow it, and
    // longer-lived; three at once so a pack noticing the cat shows more than two warnings.
    const float Life = 1.6f, Rise = 0.45f, Size = 0.8f, PlateScale = 1.45f;
    const int MaxLive = 3;
    SpriteRenderer plate;
    static Sprite plateSprite;
    static readonly System.Collections.Generic.Dictionary<EnemyDefinition.CombatRole, Sprite> sprites =
        new System.Collections.Generic.Dictionary<EnemyDefinition.CombatRole, Sprite>();

    static readonly System.Collections.Generic.List<RoleIcon> live = new System.Collections.Generic.List<RoleIcon>();
    SpriteRenderer sr;
    Vector3 start;
    float t;

    void OnEnable() => live.Add(this);
    void OnDisable() => live.Remove(this);

    public static void Show(Vector3 position, EnemyDefinition.CombatRole role)
    {
        var sprite = SpriteFor(role);
        if (sprite == null || live.Count >= MaxLive) return; // a pack noticing you at once shows a few, not a wall of icons
        var go = new GameObject("RoleIcon");
        go.transform.position = position + Vector3.up * 0.25f;
        var icon = go.AddComponent<RoleIcon>();
        icon.start = go.transform.position;
        icon.sr = go.AddComponent<SpriteRenderer>();
        icon.sr.sprite = sprite;
        icon.sr.sortingOrder = 30;
        var back = new GameObject("Plate");
        back.transform.SetParent(go.transform, false);
        back.transform.localScale = Vector3.one * PlateScale;
        icon.plate = back.AddComponent<SpriteRenderer>();
        icon.plate.sprite = PlateSprite();
        icon.plate.sortingOrder = 29;
        // A short chime, pitched by role (tough low, ranged and ambush high), so it is heard as well as seen.
        float pitch = role == EnemyDefinition.CombatRole.Ranged ? 1.26f : role == EnemyDefinition.CombatRole.Tank ? 0.8f
            : role == EnemyDefinition.CombatRole.Ambusher ? 1.5f : 1f;
        Sfx.Play("tip_chime", 0.45f, pitch);
    }

    // A 32 px pixel disc: dark fill with a one-pixel cream rim, drawn once (32 px per unit: 1 unit wide at scale 1).
    static Sprite PlateSprite()
    {
        if (plateSprite != null) return plateSprite;
        const int N = 32;
        var tex = RuntimeGeneratedAssets.Texture(N, N);
        tex.filterMode = FilterMode.Point;
        var px = new Color32[N * N];
        float c = (N - 1) * 0.5f;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                px[y * N + x] = d > 15.5f ? new Color32(0, 0, 0, 0)
                              : d > 14f ? new Color32(245, 230, 190, 255)
                              : new Color32(30, 18, 26, 235);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return plateSprite = RuntimeGeneratedAssets.Sprite(tex, new Vector2(0.5f, 0.5f), N);
    }

    static Sprite SpriteFor(EnemyDefinition.CombatRole role)
    {
        if (sprites.TryGetValue(role, out var s)) return s;
        string name = role == EnemyDefinition.CombatRole.Ranged ? "ranged" : role == EnemyDefinition.CombatRole.Tank ? "tank"
            : role == EnemyDefinition.CombatRole.Ambusher ? "ambusher" : "chaser";
        return sprites[role] = Resources.Load<Sprite>("EnemyFx/role_" + name);
    }

    void Update()
    {
        t += Time.deltaTime;
        float k = t / Life;
        // pops in a little big, floats up, fades out over the last third
        float pop = k < 0.12f ? Mathf.Lerp(0.4f, 1.15f, k / 0.12f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((k - 0.12f) / 0.1f));
        transform.localScale = Vector3.one * Size * pop;
        transform.position = start + Vector3.up * Rise * (1f - (1f - k) * (1f - k));
        var c = sr.color; c.a = k < 0.7f ? 1f : Mathf.Clamp01((1f - k) / 0.3f); sr.color = c;
        if (plate != null) plate.color = new Color(1f, 1f, 1f, c.a);
        if (t >= Life) Destroy(gameObject);
    }
}
