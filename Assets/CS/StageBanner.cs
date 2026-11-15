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
    static readonly System.Collections.Generic.List<GameObject> live = new System.Collections.Generic.List<GameObject>();
    static UnityEngine.SceneManagement.Scene closedScene;

    // Takes every banner down, the queued ones too, and lets no more up in this scene: the result board must not
    // get a boss's name over it (a boss killed before its roar ended still raised its banner after the board).
    // A banner on screen now (labels over the cat's head make way for it: DamagePopup.SpawnLabel).
    public static bool Up => live.Exists(g => g != null);

    public static void ClearAll()
    {
        foreach (var g in live) if (g != null) Object.Destroy(g);
        live.Clear();
        last = null;
        closedScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
    }

    public static void Show(Canvas canvas, TMP_FontAsset font, string title, string subtitle, float hold, bool compact = false)
    {
        if (canvas == null) return;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene() == closedScene) return;
        var go = new GameObject("StageBanner", typeof(RectTransform), typeof(CanvasGroup));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);
        rt.SetAsLastSibling();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = compact ? new Vector2(0f, 248f) : new Vector2(0f, 170f);
        rt.sizeDelta = compact ? new Vector2(540f, 112f) : new Vector2(760f, 200f);

        // A dark band under the whole banner (first child, so drawn first): over a lit platform or a busy tree the
        // ribbon's edge and the effect line lost their contrast. The banner itself stays at a fixed screen height.
        var shade = new GameObject("Shade", typeof(RectTransform), typeof(Image));
        var shadeRt = (RectTransform)shade.transform;
        shadeRt.SetParent(rt, false);
        shadeRt.anchorMin = Vector2.zero; shadeRt.anchorMax = Vector2.one;
        shadeRt.offsetMin = new Vector2(-16f, -10f); shadeRt.offsetMax = new Vector2(16f, 6f);
        var shadeImg = shade.GetComponent<Image>();
        shadeImg.color = UiTheme.TextBand;
        shadeImg.raycastTarget = false;

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
            bool big = GameSettings.LargeText; // 큰 글씨 option: the compact line goes 24 -> 36
            var sub = MakeText(rt, "Subtitle", subtitle, compact && !big ? 24f : 36f, UiTheme.Subtitle, font);
            sub.rectTransform.anchoredPosition = compact ? new Vector2(0f, big ? -40f : -34f) : new Vector2(0f, -55f);
            sub.rectTransform.sizeDelta = compact ? new Vector2(big ? 900f : 520f, big ? 50f : 38f) : new Vector2(900f, 60f);
            sub.outlineWidth = 0.2f;
            sub.outlineColor = new Color32(40, 20, 10, 255);
            // The effect line sits on the fight picture: the new gold-trimmed band (Resources/UICats/ui_band, 21 px at x2)
            // under it, as wide as the words need; the shared dark band when the art is missing.
            var bandArt = Resources.Load<Sprite>("UICats/ui_band");
            if (bandArt != null)
            {
                var bandGo = new GameObject("SubtitleBand", typeof(RectTransform), typeof(Image));
                var bandRt = (RectTransform)bandGo.transform;
                bandRt.SetParent(rt, false);
                bandRt.SetSiblingIndex(sub.transform.GetSiblingIndex()); // just under the words
                bandRt.anchorMin = bandRt.anchorMax = bandRt.pivot = new Vector2(0.5f, 0.5f);
                bandRt.anchoredPosition = sub.rectTransform.anchoredPosition;
                float words = sub.GetPreferredValues(subtitle).x;
                bandRt.sizeDelta = new Vector2(Mathf.Max(320f, words + 120f), bandArt.rect.height * 2f + 6f);
                var bandImg = bandGo.GetComponent<Image>();
                bandImg.sprite = bandArt;
                bandImg.type = Image.Type.Sliced;
                bandImg.pixelsPerUnitMultiplier = 0.5f; // 2 canvas units per art pixel
                bandImg.raycastTarget = false;
            }
            else UiTheme.Backdrop(sub);
        }

        var fade = go.AddComponent<FadeBanner>();
        fade.delay = last != null ? Mathf.Max(0f, lastEnds - Time.unscaledTime) : 0f;
        fade.hold = hold;
        fade.fadeIn = fade.fadeOut = UiTheme.BannerFade; // quick in, held, quick out (never half-faded over the fight)
        fade.slide = 12f;
        fade.sfx = "ui_open"; // a soft whoosh as the ribbon drops in (StageBanner waits its turn, so on show, not now)
        fade.sfxVolume = compact ? 0.2f : 0.3f;
        last = go;
        live.RemoveAll(g => g == null);
        live.Add(go);
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
