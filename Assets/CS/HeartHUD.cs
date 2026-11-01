using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Shows the player's HP as a row of hearts (Simple Heart asset sprites,
// Assets/HealthHeartSystem/Graphics/mini-heart.png). 1 heart = 1 HP; half hearts are
// drawn by filling the red heart horizontally over the empty one, like the asset's own
// HealthBarController. Hearts are created/updated whenever HP or max HP changes, and the
// heart that just lost HP gives a little "pop".
public class HeartHUD : MonoBehaviour
{
    public Sprite fullHeart;
    public Sprite emptyHeart;
    public Vector2 heartSize = new Vector2(40f, 35f);
    public float spacing = 6f;
    static readonly Color EmptyTint = new Color(0.42f, 0.4f, 0.44f, 0.7f);

    readonly List<RectTransform> slots = new List<RectTransform>();
    readonly List<Image> fills = new List<Image>();
    float lastHP = -1f;
    float lastMax = -1f;
    RectTransform popping;
    float popTimer;
    float heartOffset;

    void Awake()
    {
        var portrait = Resources.Load<Sprite>("ItchUI/cat_portrait");
        if (portrait == null) return;
        heartOffset = 46f;
        var go = new GameObject("CatPortrait", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(40f, 40f); // as tall as a heart row: stays clear of the dash bar below
        rt.anchoredPosition = new Vector2(20f, -17f);
        var image = go.GetComponent<Image>();
        image.sprite = portrait;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    void Update()
    {
        var stats = PlayerStats.Instance;
        if (stats != null && (stats.CurrentHP != lastHP || stats.MaxHP != lastMax))
        {
            int hitIndex = stats.CurrentHP < lastHP ? Mathf.Clamp(Mathf.CeilToInt(lastHP) - 1, 0, 999) : -1;
            Refresh(stats.CurrentHP, stats.MaxHP);
            if (hitIndex >= 0 && hitIndex < slots.Count) Pop(slots[hitIndex]);
            lastHP = stats.CurrentHP;
            lastMax = stats.MaxHP;
        }

        if (popping != null)
        {
            popTimer -= Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(popTimer / 0.25f);
            popping.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(t * Mathf.PI));
            if (popTimer <= 0f)
            {
                popping.localScale = Vector3.one;
                popping = null;
            }
        }
    }

    void Refresh(float hp, float max)
    {
        int count = Mathf.CeilToInt(max);
        while (slots.Count < count) CreateSlot();
        for (int i = 0; i < slots.Count; i++)
        {
            slots[i].gameObject.SetActive(i < count);
            fills[i].fillAmount = Mathf.Clamp01(hp - i);
        }
    }

    void Pop(RectTransform slot)
    {
        if (popping != null) popping.localScale = Vector3.one;
        popping = slot;
        popTimer = 0.25f;
    }

    void CreateSlot()
    {
        int i = slots.Count;
        var slot = new GameObject($"Heart{i + 1}", typeof(RectTransform));
        slot.transform.SetParent(transform, false);
        var rt = (RectTransform)slot.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = heartSize;
        rt.anchoredPosition = new Vector2(heartOffset + heartSize.x * 0.5f + i * (heartSize.x + spacing), -heartSize.y * 0.5f);
        var empty = slot.AddComponent<Image>();
        empty.sprite = emptyHeart;
        empty.preserveAspect = true;
        empty.raycastTarget = false;
        // The asset's empty heart is a dark red that reads as "full" at a glance: grey it out and let it
        // show a little of the level through, so lost HP is obvious next to the bright red full hearts.
        empty.color = EmptyTint;

        var fillGo = new GameObject("HeartFill", typeof(RectTransform));
        fillGo.transform.SetParent(slot.transform, false);
        var frt = (RectTransform)fillGo.transform;
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = frt.offsetMax = Vector2.zero;
        var fill = fillGo.AddComponent<Image>();
        fill.sprite = fullHeart;
        fill.preserveAspect = true;
        fill.raycastTarget = false;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;

        slots.Add(rt);
        fills.Add(fill);
    }
}
