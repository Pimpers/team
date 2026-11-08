using UnityEngine;
using UnityEngine.UI;
using TMPro;

// World map panel for picking the 타락도 (difficulty tier) of the next run: "<  타락도 N  >"
// plus what the tier changes. Built at runtime by WorldMapController. Always there: below the normal tier sits
// 편하게 (Corruption.Easy), open from the start; the higher tiers appear once the chapter has been beaten.
public class CorruptionSelector : MonoBehaviour
{
    TMP_Text levelText, descText;
    Button prev, next;

    public static CorruptionSelector Create(Canvas canvas, TMP_FontAsset font)
    {
        if (canvas == null) return null;
        var skin = UISkin.Instance;

        var panel = new GameObject("CorruptionSelector", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)panel.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        // One strip in the top row, level with 항구: the 470 x 190 box covered the map's top-right corner once the
        // map filled the screen's width.
        rt.anchoredPosition = new Vector2(-24f, -10f);
        rt.sizeDelta = new Vector2(720f, 86f); // room between the arrows and the words (they touched at 660)
        UISkin.Apply(panel.GetComponent<Image>(), skin != null ? skin.panel : null, UISkin.LargeScale);

        var sel = panel.AddComponent<CorruptionSelector>();
        sel.levelText = Text(rt, "Level", "", 36f, UISkin.InkTitle, font, new Vector2(-150f, 0f), new Vector2(180f, 56f));
        sel.descText = Text(rt, "Desc", "", 24f, UISkin.Ink, font, new Vector2(96f, 0f), new Vector2(280f, 76f)); // UiTheme.TextSmall (24): the body size, not smaller
        sel.descText.textWrappingMode = TextWrappingModes.Normal;
        sel.descText.lineSpacing = 2f;
        sel.prev = ArrowButton(rt, "Prev", "<", new Vector2(-318f, 0f), font, () => sel.Step(-1));
        sel.next = ArrowButton(rt, "Next", ">", new Vector2(318f, 0f), font, () => sel.Step(1));
        sel.Refresh();
        return sel;
    }

    void Step(int d)
    {
        Corruption.Level = Corruption.Level + d;
        Refresh();
    }

    public void Refresh()
    {
        int lv = Corruption.Level;
        levelText.text = lv < 0 ? Loc.T("corruption.selector.easy") : lv == 0 ? Loc.T("corruption.selector.none") : Loc.F("corruption.selector.level", lv);
        descText.text = Corruption.Describe(lv);
        prev.interactable = lv > Corruption.Easy;
        next.interactable = lv < Corruption.Unlocked;
        if (Corruption.Unlocked > 0) OneTimeTip.Show("corruption", GetComponentInParent<Canvas>(), levelText.font); // the first tier on offer
    }

    static TMP_Text Text(RectTransform parent, string name, string text, float size, Color color, TMP_FontAsset font, Vector2 pos, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
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

    static Button ArrowButton(RectTransform parent, string name, string label, Vector2 pos, TMP_FontAsset font, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(64f, 64f); // inside the vine frame; over the 44 px touch / pad minimum
        var skin = UISkin.Instance;
        UISkin.Apply(go.GetComponent<Image>(), skin != null ? skin.buttonWood : null, UISkin.SmallScale);
        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
        btn.colors = colors;
        btn.onClick.AddListener(onClick);
        go.AddComponent<ButtonSound>();
        var t = Text(rt, "Label", label, 48f, Color.white, font, Vector2.zero, Vector2.zero);
        t.rectTransform.anchorMin = Vector2.zero;
        t.rectTransform.anchorMax = Vector2.one;
        t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
        return btn;
    }
}
