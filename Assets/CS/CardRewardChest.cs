using System.Collections;
using TMPro;
using UnityEngine;

// One chest after every normal stage clear. Stage 1 takes two basic hits;
// later chests open in one hit, then the usual three-card draft appears.
public class CardRewardChest : MonoBehaviour
{
    const float BaseScale = 0.82f;
    const float HitScale = 0.94f;
    static Sprite chestSprite;
    static Sprite[] openSprites;
    PlayerStats player;
    System.Action onOpened;
    SpriteRenderer body;
    TextMeshPro prompt;
    bool opened;
    int hits;
    int hitsRequired = 2;
    public static CardRewardChest Instance { get; private set; }

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    public static bool Create(System.Action onOpened)
    {
        var player = PlayerStats.Instance;
        var map = StageMap.Instance;
        if (player == null || map == null) return false;

        var go = new GameObject("CardRewardChest");
        var chest = go.AddComponent<CardRewardChest>();
        chest.player = player;
        chest.onOpened = onOpened;
        var run = RunManager.Instance;
        chest.hitsRequired = run != null && run.stageNumber > 1 ? 1 : 2;
        Vector2 feet = player.Body != null ? player.Body.Feet : (Vector2)player.transform.position;
        // By the way out, where one already stands within reach (a stage with its closed portal at the end): the chest
        // came up anywhere round the cat, often the other way from the portal. A fork's portals rise beside the cat
        // once the card is picked - so beside the chest it has just opened.
        var exit = StageExit.Instance;
        Vector2 anchor = exit != null && exit.WayCount == 1 && Vector2.Distance(exit.ExitPoint, feet) < 22f ? exit.ExitPoint : feet;
        go.transform.position = FindSpot(map, anchor);
        go.transform.localScale = Vector3.one * BaseScale;
        chest.Build();
        WorldPropFx.PlayItemFeedback(go.transform.position + Vector3.up * 0.65f, 0.65f);
        return true;
    }

    static Vector2 FindSpot(StageMap map, Vector2 feet)
    {
        if (map.spawnSlots == null || map.spawnSlots.Length == 0) return feet + Vector2.right * 3f;
        Vector2 best = feet;
        float bestScore = float.MaxValue;
        foreach (var slot in map.spawnSlots)
        {
            float distance = Vector2.Distance(slot, feet);
            if (distance < 2.5f || distance > 9f) continue;
            float score = Mathf.Abs(distance - 4.5f) + Mathf.Abs(slot.y - feet.y) * 2f;
            if (score < bestScore) { bestScore = score; best = slot; }
        }
        if (bestScore < float.MaxValue) return best;
        foreach (var slot in map.spawnSlots)
        {
            float score = Vector2.Distance(slot, feet);
            if (score < bestScore) { bestScore = score; best = slot; }
        }
        return best;
    }

    void Build()
    {
        body = Part("Chest", ChestSprite, Vector3.zero, 14);
        prompt = new GameObject("Prompt").AddComponent<TextMeshPro>();
        prompt.transform.SetParent(transform, false);
        prompt.transform.localPosition = new Vector3(0f, 1.55f, 0f);
        prompt.rectTransform.sizeDelta = new Vector2(3.5f, 0.8f);
        prompt.text = Loc.F("chest.hits", 0, hitsRequired);
        prompt.fontSize = 3.4f;
        prompt.alignment = TextAlignmentOptions.Center;
        prompt.color = new Color(1f, 0.94f, 0.72f);
        prompt.outlineWidth = 0.18f;
        prompt.sortingOrder = 20;
        prompt.enabled = false;
        var hud = RunManager.Instance != null ? RunManager.Instance.hudText : null;
        if (hud != null) prompt.font = hud.font;
    }

    SpriteRenderer Part(string name, Sprite sprite, Vector3 local, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = local;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return renderer;
    }

    void Update()
    {
        if (opened || player == null) return;
        Vector2 feet = (player.Body != null ? player.Body.Feet : (Vector2)player.transform.position);
        prompt.enabled = Vector2.Distance(feet, transform.position) <= 2.2f;
    }

    public static bool TryMeleeHit(Vector2 attacker, float facing, float reach, float feet, float top)
    {
        var chest = Instance;
        if (chest == null || chest.opened) return false;
        Vector2 p = chest.transform.position;
        float dx = p.x - attacker.x;
        if (dx * facing < -0.2f || Mathf.Abs(dx) > reach + 0.9f) return false;
        if (p.y + 1.2f < feet || p.y > top) return false;
        chest.TakeHit();
        return true;
    }

    public static bool TryProjectileHit(Vector2 point)
    {
        var chest = Instance;
        if (chest == null || chest.opened) return false;
        if (Vector2.Distance(point, (Vector2)chest.transform.position + Vector2.up * 0.55f) > 0.85f) return false;
        chest.TakeHit();
        return true;
    }

    void TakeHit()
    {
        if (opened) return;
        hits++;
        prompt.text = Loc.F("chest.hits", hits, hitsRequired);
        Sfx.Play("hit_strong", 0.8f, 1.25f);
        Vfx.Play("spark_mid1", transform.position + Vector3.up * 0.55f, 0.7f, tint: UiTheme.Gold);
        transform.localScale = Vector3.one * HitScale;
        if (hits >= hitsRequired) StartCoroutine(Open());
    }

    void LateUpdate()
    {
        if (!opened) transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * BaseScale, 12f * Time.deltaTime);
    }

    IEnumerator Open()
    {
        opened = true;
        prompt.enabled = false;
        Sfx.Play("card_pick", 1f, 1.15f);
        WorldPropFx.PlayItemFeedback(transform.position + Vector3.up * 0.85f, 1.25f);
        var frames = OpenSprites;
        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null) body.sprite = frames[i];
            float elapsed = 0f;
            while (elapsed < 0.065f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        yield return new WaitForSecondsRealtime(0.12f);
        var callback = onOpened;
        Destroy(gameObject);
        callback?.Invoke();
    }

    static Sprite ChestSprite => chestSprite != null
        ? chestSprite
        : (chestSprite = Resources.Load<Sprite>("WorldProps/TreasureChest/idle"));

    static Sprite[] OpenSprites
    {
        get
        {
            if (openSprites != null) return openSprites;
            openSprites = new Sprite[8];
            for (int i = 0; i < openSprites.Length; i++)
                openSprites[i] = Resources.Load<Sprite>($"WorldProps/TreasureChest/open_{i + 1:D2}");
            return openSprites;
        }
    }
}
