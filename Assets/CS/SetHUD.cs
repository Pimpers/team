using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The card-tag sets under the info plate (top left), so the build shows during play and not only in the pause menu:
// one chip per tag the cat has cards of - its name and cards / full set ("발톱 2/3"). A chip is dim until its first
// bonus is on (2 cards), then bright in the tag's colour, gold-edged at the full set (3); it bounces when a step is
// reached. Created by RunManager; RunManager places it under the plate.
public class SetHUD : MonoBehaviour
{
    // The tag colours on a dark plate (CardPool.TagColors are for the cream cards).
    static readonly Color[] Bright =
    {
        new Color32(0xff, 0x8a, 0x6a, 0xff), new Color32(0x6f, 0xd3, 0xf0, 0xff), new Color32(0xe8, 0xb8, 0x80, 0xff),
        new Color32(0xa6, 0xe8, 0x6e, 0xff), new Color32(0xff, 0xd7, 0x5e, 0xff),
    };
    const float ChipH = 44f, Gap = 8f, FontSize = UISkin.FontBody;

    class Chip { public RectTransform rt; public Image bg, icon; public TextMeshProUGUI text; public int shown = -1; public float punch; }
    readonly Chip[] chips = new Chip[System.Enum.GetValues(typeof(CardPool.Tag)).Length];
    TMP_FontAsset font;
    float poll;

    public static SetHUD Create(RectTransform parent, TMP_FontAsset font)
    {
        var go = new GameObject("SetHUD", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(700f, ChipH);
        var hud = go.AddComponent<SetHUD>();
        hud.font = font;
        return hud;
    }

    Chip Make(int i)
    {
        var go = new GameObject("Set_" + CardPool.TagNames[i], typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        var bg = go.GetComponent<Image>();
        bg.raycastTarget = false;
        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var iconRt = (RectTransform)iconGo.transform;
        iconRt.SetParent(rt, false);
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.anchoredPosition = new Vector2(20f, 0f);
        iconRt.sizeDelta = new Vector2(28f, 28f);
        var icon = iconGo.GetComponent<Image>();
        string[] iconNames = { "claw", "wind", "fur", "hunt", "treat" };
        icon.sprite = Resources.Load<Sprite>("ItchUI/SetIcons/" + iconNames[i]);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        var t = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.rectTransform.SetParent(rt, false);
        t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
        t.rectTransform.offsetMin = new Vector2(40f, 0f); t.rectTransform.offsetMax = new Vector2(-12f, 0f);
        if (font != null) t.font = font;
        t.fontSize = FontSize;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        t.outlineWidth = 0.2f;
        t.outlineColor = new Color32(20, 12, 10, 255);
        return new Chip { rt = rt, bg = bg, icon = icon, text = t };
    }

    void Update()
    {
        var stats = PlayerStats.Instance;
        if (stats == null) return;
        poll -= Time.unscaledDeltaTime;
        bool changed = false;
        if (poll <= 0f)
        {
            poll = 0.25f;
            for (int i = 0; i < chips.Length; i++)
            {
                int n = stats.SetCount((CardPool.Tag)i);
                if (chips[i] == null && n > 0) chips[i] = Make(i);
                var c = chips[i];
                if (c == null || c.shown == n) continue;
                if (c.shown >= 0 && CardPool.SetLevel(n) > CardPool.SetLevel(c.shown)) c.punch = 0.3f; // a set step reached
                c.shown = n;
                Style(c, i, n);
                changed = true;
            }
        }
        if (changed) Layout();
        foreach (var c in chips)
        {
            if (c == null || c.punch <= 0f) continue;
            c.punch -= Time.unscaledDeltaTime;
            float k = 1f + 0.18f * Mathf.Sin(Mathf.Clamp01(c.punch / 0.3f) * Mathf.PI);
            c.rt.localScale = new Vector3(k, k, 1f);
        }
    }

    void Style(Chip c, int tag, int n)
    {
        int lv = CardPool.SetLevel(n);
        int max = CardPool.SetStep2;
        Color col = lv > 0 ? Bright[tag] : Color.Lerp(Bright[tag], new Color(0.75f, 0.72f, 0.68f), 0.55f);
        string hex = ColorUtility.ToHtmlStringRGB(col);
        string count = lv >= 2 ? Loc.T("set.complete") : $"{Mathf.Min(n, max)}/{max}";
        c.text.text = $"<color=#{hex}>{CardPool.TagNames[tag]} {count}</color>";
        // Always a solid dark plate (it sits over bright sky); the full set gets a warm one.
        var plate = lv >= 2 ? new Color(0.36f, 0.24f, 0.08f, 1f) : new Color(0.1f, 0.07f, 0.09f, 0.95f);
        if (UISkin.Instance != null) UISkin.Apply(c.bg, UISkin.Instance.slate, 4f, plate);
        else c.bg.color = plate;
        c.text.ForceMeshUpdate();
        c.rt.sizeDelta = new Vector2(c.text.preferredWidth + 54f, ChipH);
    }

    void Layout()
    {
        float x = 0f;
        foreach (var c in chips)
        {
            if (c == null || c.shown <= 0) { if (c != null) c.rt.gameObject.SetActive(false); continue; }
            c.rt.gameObject.SetActive(true);
            c.rt.anchoredPosition = new Vector2(x, 0f);
            x += c.rt.sizeDelta.x + Gap;
        }
    }
}
