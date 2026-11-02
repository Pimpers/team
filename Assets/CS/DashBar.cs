using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The dash charges as a row of bars right under the hearts (one bar per dash, 2 to 4 with the 연속 대시
// card), labelled "대시". Each bar is a dark wood frame around a dark slot: a ready dash fills it bright
// gold with a light stripe on top; a used one stays dark and fills up sky blue, left to right, while it
// comes back, then flashes white as it turns gold again. Bars are as wide as a heart so the rows line up.
// Created by RunManager; reads PlayerController2D.
public class DashBar : MonoBehaviour
{
    // Bigger than the first version (40 x 20): a dash is the main dodge, its charges must read at a glance mid-fight.
    const float BarW = 72f, BarH = 36f, Gap = 8f, Pad = 8f; // inside the plate's wood rim (5 art pixels at 2 units each)
    static readonly Color Ready = new Color(1f, 0.8f, 0.22f), Shine = new Color(1f, 0.95f, 0.7f),
                          Charging = new Color(0.45f, 0.78f, 1f), Slot = new Color(0.13f, 0.09f, 0.11f, 0.95f);

    readonly List<Image> fills = new List<Image>();
    readonly List<Image> shines = new List<Image>();
    readonly List<RectTransform> bars = new List<RectTransform>();
    PlayerController2D body;
    TMP_Text label;
    TMP_FontAsset font;
    int lastLeft = -1;
    float flash;                 // a quick white flash on the bar that just came back
    int flashIndex = -1;
    static Sprite white;

    public static DashBar Create(Transform canvas, Vector2 topLeft, TMP_FontAsset font)
    {
        var go = new GameObject("DashBar", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = topLeft;
        rt.sizeDelta = new Vector2(4 * (BarW + Gap) + 90f, BarH);
        var bar = go.AddComponent<DashBar>();
        bar.font = font;
        return bar;
    }

    static Sprite White()
    {
        if (white != null) return white;
        var tex = RuntimeGeneratedAssets.Texture(4, 4);
        var px = new Color32[16];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px);
        tex.Apply();
        white = RuntimeGeneratedAssets.Sprite(tex, new Vector2(0.5f, 0.5f), 100f);
        return white;
    }

    Image MakeImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = White();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static void Inset(RectTransform rt, float left, float bottom, float right, float top)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    void Build(int count)
    {
        foreach (var b in bars) if (b != null) Destroy(b.gameObject);
        bars.Clear();
        fills.Clear();
        shines.Clear();
        for (int i = 0; i < count; i++)
        {
            // Wood frame (the HUD plates' sprite), a dark slot, the fill, and the shine stripe on top of it.
            var frame = MakeImage("Dash" + (i + 1), transform, new Color(0.07f, 0.05f, 0.08f, 0.9f));
            if (UISkin.Instance != null) UISkin.Apply(frame, UISkin.Instance.slate, 4f, Color.white); // plain wood rim (the vine plate broke up this small)
            var rt = frame.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(BarW, BarH);
            rt.anchoredPosition = new Vector2(i * (BarW + Gap), 0f);

            Inset(MakeImage("Slot", rt, Slot).rectTransform, Pad, Pad, Pad, Pad);
            var fill = MakeImage("Fill", rt, Ready);
            Inset(fill.rectTransform, Pad, Pad, Pad, Pad);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            var shine = MakeImage("Shine", rt, Shine);
            Inset(shine.rectTransform, Pad + 2f, BarH - Pad - 4f, Pad + 2f, Pad + 1f);

            bars.Add(rt);
            fills.Add(fill);
            shines.Add(shine);
        }

        if (label == null)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            label = go.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = Loc.T("hud.dash"); // "대시 · 우클릭": the bars are dash charges, and which button spends them
            label.fontSize = UISkin.FontBody;
            label.color = UISkin.OverlayText;
            label.outlineWidth = 0.25f;
            label.outlineColor = new Color32(20, 12, 10, 255);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            var lrt = label.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0f, 1f);
            lrt.pivot = new Vector2(0f, 1f);
            lrt.sizeDelta = new Vector2(420f, BarH); // "대시 · 우클릭   Tab 능력치" (also the Tab sheet's pointer)
        }
        label.rectTransform.anchoredPosition = new Vector2(count * (BarW + Gap) + 10f, 0f); // room for both Korean glyphs
        lastLeft = -1;
    }

    void Update()
    {
        if (body == null && PlayerStats.Instance != null) body = PlayerStats.Instance.Body;
        if (body == null) return;

        int max = body.MaxDashes, left = body.DashesLeft;
        if (bars.Count != max) Build(max);
        if (lastLeft >= 0 && left > lastLeft) { flashIndex = left - 1; flash = 0.25f; }
        lastLeft = left;
        if (flash > 0f) flash -= Time.unscaledDeltaTime;

        for (int i = 0; i < fills.Count; i++)
        {
            bool ready = i < left;
            fills[i].fillAmount = ready ? 1f : i == left ? body.DashRefillProgress : 0f;
            var c = ready ? Ready : Charging;
            if (ready && i == flashIndex && flash > 0f) c = Color.Lerp(c, Color.white, flash / 0.25f);
            fills[i].color = c;
            shines[i].enabled = ready; // only a full, ready bar gets the shine
        }
    }
}
