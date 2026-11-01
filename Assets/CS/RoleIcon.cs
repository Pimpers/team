using UnityEngine;

// What an enemy is about to do, as a small icon over its head when it notices the cat (Resources/EnemyFx/role_*.png:
// a "!" bubble = chases you, a target = shoots from afar, a shield = tough, ">>" = charges). It replaced the words
// 추격! / 원거리! / 단단함! / 돌진!, which piled up with the damage numbers and banners in a busy fight.
public class RoleIcon : MonoBehaviour
{
    const float Life = 1.1f, Rise = 0.45f, Size = 0.6f;
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
        if (sprite == null || live.Count >= 2) return; // a pack noticing you at once shows two, not a wall of icons
        var go = new GameObject("RoleIcon");
        go.transform.position = position + Vector3.up * 0.25f;
        var icon = go.AddComponent<RoleIcon>();
        icon.start = go.transform.position;
        icon.sr = go.AddComponent<SpriteRenderer>();
        icon.sr.sprite = sprite;
        icon.sr.sortingOrder = 30;
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
        var c = sr.color; c.a = k < 0.66f ? 1f : Mathf.Clamp01((1f - k) / 0.34f); sr.color = c;
        if (t >= Life) Destroy(gameObject);
    }
}
