using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// A short decision before each normal stage. It pauses the stage until the player chooses
// a quiet path (fewer enemies) or a hunting path (one more elite and richer rewards).
public class StageRouteChoiceUI : MonoBehaviour
{
    RunManager run;
    Canvas canvas;
    TMP_FontAsset font;
    CanvasGroup group;
    System.Action<bool> onChoice;

    public static void Show(RunManager run, Canvas canvas, TMP_FontAsset font, System.Action<bool> onChoice)
    {
        if (run == null || canvas == null) return;
        var go = new GameObject("StageRouteChoice", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(canvas.transform, false);
        var ui = go.AddComponent<StageRouteChoiceUI>();
        ui.run = run;
        ui.canvas = canvas;
        ui.font = font;
        ui.onChoice = onChoice;
        ui.group = go.GetComponent<CanvasGroup>();
        ui.group.alpha = 0f;
        ui.group.blocksRaycasts = false;
        GameClock.Pause("route");
        ui.StartCoroutine(ui.Open());
    }

    IEnumerator Open()
    {
        while (StageTitle.Showing || LeafWipe.Busy || ScreenFader.Switching) yield return null;
        Build();
        for (float t = 0f; t < 0.18f; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Clamp01(t / 0.18f);
            yield return null;
        }
        group.alpha = 1f;
        group.blocksRaycasts = true;
    }

    void Build()
    {
        var root = (RectTransform)transform;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;

        var shade = gameObject.AddComponent<Image>();
        shade.color = UISkin.ModalShade;
        shade.raycastTarget = true;

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        var panelRt = (RectTransform)panel.transform;
        panelRt.SetParent(root, false);
        panelRt.anchorMin = panelRt.anchorMax = panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(1040f, 480f);
        var panelImage = panel.GetComponent<Image>();
        ApplyRpgSkin(panelImage, "panel_brown", new Color(0.12f, 0.075f, 0.08f, 1f));

        MakeText(panelRt, "Title", Loc.T("route.choice.title"), UISkin.FontTitle, new Vector2(0f, 150f), new Vector2(900f, 80f), UISkin.BannerText);
        MakeText(panelRt, "Hint", Loc.T("route.choice.subtitle"), UISkin.FontBody, new Vector2(0f, 96f), new Vector2(900f, 50f), UISkin.OverlayMuted);
        MakeButton(panelRt, "Safe", Loc.T("route.safe"), Loc.T("route.choice.safe"), new Vector2(-250f, -55f), false, new Color(0.22f, 0.42f, 0.28f));
        // Every second hunt route makes the next card draft a guaranteed rare: say so, and say when this one is it.
        string rare = RouteProgress.HuntChoices % 2 == 1 ? Loc.T("route.choice.rare_now")
                                                         : Loc.F("route.choice.rare_progress", RouteProgress.HuntChoices % 2);
        MakeButton(panelRt, "Hunt", Loc.T("route.hunt"), Loc.F("route.choice.hunt", rare), new Vector2(250f, -55f), true, new Color(0.55f, 0.18f, 0.14f));
    }

    void MakeButton(RectTransform parent, string name, string title, string detail, Vector2 pos, bool hunt, Color fallback)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(430f, 230f);
        var image = go.GetComponent<Image>();
        ApplyRpgSkin(image, hunt ? "buttonLong_blue" : "buttonLong_brown", fallback);
        var button = go.GetComponent<Button>();
        button.onClick.AddListener(() => Choose(hunt));
        go.AddComponent<ButtonSound>();
        MakeText(rt, "Title", title, UISkin.FontHeading, new Vector2(0f, 55f), new Vector2(400f, 64f), UISkin.OverlayText);
        MakeText(rt, "Detail", detail, UISkin.FontBody, new Vector2(0f, -42f), new Vector2(400f, 110f), UISkin.OverlayText);
    }

    // Kenney RPG UI: a nine-sliced brown panel and blue/brown route buttons.
    static void ApplyRpgSkin(Image image, string name, Color fallback)
    {
        var sprite = Resources.Load<Sprite>("KenneyRpgUI/" + name);
        if (sprite == null) { image.color = fallback; return; }
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
    }

    void MakeText(RectTransform parent, string name, string value, float size, Vector2 pos, Vector2 box, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var text = go.GetComponent<TextMeshProUGUI>();
        text.rectTransform.SetParent(parent, false);
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        text.rectTransform.anchoredPosition = pos;
        text.rectTransform.sizeDelta = box;
        text.text = value;
        text.font = font;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color32(30, 16, 10, 255);
        text.raycastTarget = false;
    }

    void Choose(bool hunt)
    {
        group.blocksRaycasts = false;
        GameClock.Resume("route");
        onChoice?.Invoke(hunt);
        Destroy(gameObject);
    }
}
