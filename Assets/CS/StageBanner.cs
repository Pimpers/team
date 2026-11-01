using UnityEngine;
using UnityEngine.UI;
using TMPro;

// A red ribbon title with a line under it that fades in, holds and fades out (FadeBanner),
// built at runtime - e.g. "스테이지 클리어!" / "하트 1칸 회복" when a stage's monsters are gone.
public static class StageBanner
{
    // Banners share one spot, so one raised while another is up waits its turn (a two-tag card can open two
    // sets at once). The last one is gone with its scene, so a new scene never waits on the old one.
    static GameObject last;
    static float lastEnds; // unscaled time when `last` has faded out

    public static void Show(Canvas canvas, TMP_FontAsset font, string title, string subtitle, float hold, bool compact = false)
    {
        if (canvas == null) return;
        var go = new GameObject("StageBanner", typeof(RectTransform), typeof(CanvasGroup));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);
        rt.SetAsLastSibling();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = compact ? new Vector2(0f, 248f) : new Vector2(0f, 170f);
        rt.sizeDelta = compact ? new Vector2(540f, 112f) : new Vector2(760f, 200f);

        var ribbonGo = new GameObject("Ribbon", typeof(RectTransform), typeof(Image));
        var ribbonRt = (RectTransform)ribbonGo.transform;
        ribbonRt.SetParent(rt, false);
        ribbonRt.anchoredPosition = compact ? new Vector2(0f, 22f) : new Vector2(0f, 40f);
        ribbonRt.sizeDelta = compact ? new Vector2(500f, 78f) : new Vector2(700f, 128f);
        var ribbon = ribbonGo.GetComponent<Image>();
        ribbon.raycastTarget = false;
        UISkin.ApplyBanner(ribbon);

        var titleText = MakeText(ribbonRt, "Title", title, compact ? 32f : 48f, UISkin.BannerText, font);
        UISkin.FitToRibbon(titleText.rectTransform, ribbon);

        if (!string.IsNullOrEmpty(subtitle))
        {
            var sub = MakeText(rt, "Subtitle", subtitle, compact ? 24f : 36f, UiTheme.Subtitle, font);
            sub.rectTransform.anchoredPosition = compact ? new Vector2(0f, -34f) : new Vector2(0f, -55f);
            sub.rectTransform.sizeDelta = compact ? new Vector2(520f, 38f) : new Vector2(700f, 60f);
            sub.outlineWidth = 0.2f;
            sub.outlineColor = new Color32(40, 20, 10, 255);
        }

        var fade = go.AddComponent<FadeBanner>();
        fade.delay = last != null ? Mathf.Max(0f, lastEnds - Time.unscaledTime) : 0f;
        fade.hold = hold;
        fade.sfx = "ui_open"; // a soft whoosh as the ribbon drops in (StageBanner waits its turn, so on show, not now)
        fade.sfxVolume = compact ? 0.2f : 0.3f;
        last = go;
        lastEnds = Time.unscaledTime + fade.delay + fade.fadeIn + hold + fade.fadeOut;
        Object.Destroy(go, fade.delay + fade.fadeIn + hold + fade.fadeOut + 0.1f);
    }

    static TextMeshProUGUI MakeText(RectTransform parent, string name, string text, float size, Color color, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }
}
