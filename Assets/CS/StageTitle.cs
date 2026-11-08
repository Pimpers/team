using System.Collections;
using UnityEngine;
using TMPro;

// The stage's title at the top centre when a stage is entered - "스테이지 1-2" with the place's name under it. It
// drops in as the screen opens (after the leaf sweep / fade), holds, then fades away. Placed under the boss's HP
// bar's spot so the two never cover each other. Started by RunManager; built at runtime, unscaled time.
public class StageTitle : MonoBehaviour
{
    const float FadeIn = 0.35f, Hold = 1.6f, FadeOut = 0.7f, Drop = 24f, Top = -120f;

    // The forest, a step deeper each stage (index = stage number).
    // Built on first use, not as a static initializer: that can run inside Unity's object construction,
    // where the text table (Resources) can't be loaded yet.
    static string[] names;
    public static string[] Names => names ??= new[] { "", Loc.T("stage.name.1"), Loc.T("stage.name.2"), Loc.T("stage.name.3"), Loc.T("stage.name.4"), Loc.T("stage.name.5") };
    public const int Chapter = 1;

    // A title is on screen (or waiting for the screen to open): the boss holds its entrance until it's gone.
    public static bool Showing => live > 0;
    static int live;
    void OnDestroy() => live--;

    public static void Show(Canvas canvas, TMP_FontAsset font, int stage)
    {
        if (canvas == null || stage <= 0) return;
        var go = new GameObject("StageTitle", typeof(RectTransform), typeof(CanvasGroup));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.rootCanvas.transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(900f, 160f);
        rt.anchoredPosition = new Vector2(0f, Top);
        var group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        // A soft dark band behind the words, so they read over bright sky as well as the dark crimson stages.
        var band = new GameObject("Band", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        var brt = (RectTransform)band.transform;
        brt.SetParent(rt, false);
        brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 1f);
        brt.anchoredPosition = new Vector2(0f, 6f);
        brt.sizeDelta = new Vector2(720f, 150f);
        var bimg = band.GetComponent<UnityEngine.UI.Image>();
        bimg.color = new Color(0.05f, 0.03f, 0.05f, 0.45f);
        bimg.raycastTarget = false;

        string name = stage < Names.Length ? Names[stage] : "";
        // 64 / 32 px: four and two times the Korean pixel font's 16 dots, so every dot stays square.
        Line(rt, "Title", Loc.F("stage.number", Chapter, stage), 64f, 0f, UISkin.BannerText, font);
        if (!string.IsNullOrEmpty(name)) Line(rt, "Name", name, 32f, -84f, UiTheme.Subtitle, font);
        var title = go.AddComponent<StageTitle>();
        live++;
        title.StartCoroutine(title.Play(group, rt));
    }

    static void Line(RectTransform parent, string id, string text, float size, float y, Color color, TMP_FontAsset font)
    {
        var t = new GameObject(id, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.rectTransform.SetParent(parent, false);
        t.rectTransform.anchorMin = t.rectTransform.anchorMax = t.rectTransform.pivot = new Vector2(0.5f, 1f);
        t.rectTransform.anchoredPosition = new Vector2(0f, y);
        t.rectTransform.sizeDelta = new Vector2(900f, size * 1.3f);
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        t.outlineWidth = 0.25f;
        t.outlineColor = new Color32(30, 16, 10, 255);
    }

    IEnumerator Play(CanvasGroup group, RectTransform rt)
    {
        // Wait for the screen to open (leaf sweep / black fade), then a beat.
        while (StageWipe.Busy || ScreenFader.Switching) yield return null;
        yield return new WaitForSecondsRealtime(0.35f);
        Sfx.Play("card_pick", 0.6f, 0.8f);
        for (float t = 0f; t < FadeIn; t += Time.unscaledDeltaTime)
        {
            float k = t / FadeIn;
            group.alpha = k;
            rt.anchoredPosition = new Vector2(0f, Top + (1f - k) * (1f - k) * Drop);
            yield return null;
        }
        group.alpha = 1f;
        rt.anchoredPosition = new Vector2(0f, Top);
        yield return new WaitForSecondsRealtime(Hold);
        for (float t = 0f; t < FadeOut; t += Time.unscaledDeltaTime)
        {
            group.alpha = 1f - t / FadeOut;
            yield return null;
        }
        Destroy(gameObject);
    }
}
