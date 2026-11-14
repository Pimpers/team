using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The controls window ("조작 방법", opened from the pause menu): what each key does, grouped as moving, fighting and
// looking things up, the key on a chip and its use beside it. A read-only list - the keys are not rebound here.
// The X in the corner, a click outside or ESC closes it. (Class name kept from when it was the rebinding window.)
public class KeyBindPanel : MonoBehaviour
{
    static KeyBindPanel open;

    public static bool IsOpen => open != null;

    // ESC from the pause menu closes the window.
    public static void Escape()
    {
        if (open != null) open.Close();
    }

    public static void Show(Canvas canvas, TMP_FontAsset font)
    {
        if (canvas == null || open != null) return;
        var root = new GameObject("KeyBindings", typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)root.transform;
        rt.SetParent(canvas.rootCanvas.transform, false);
        rt.SetAsLastSibling();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = UISkin.ModalShade;
        open = root.AddComponent<KeyBindPanel>();
        root.GetComponent<Button>().onClick.AddListener(() => { if (open != null) open.Close(); });

        var board = new GameObject("Board", typeof(RectTransform), typeof(Image));
        var brt = (RectTransform)board.transform;
        brt.SetParent(rt, false);
        brt.sizeDelta = new Vector2(780f, 900f);
        UiPanels.Board(board.GetComponent<Image>());
        board.AddComponent<Button>().transition = Selectable.Transition.None; // clicks on the board don't close it
        UiPanels.Ribbon(brt, Loc.T("controls.title"), font, 420f);
        var x = UiPanels.CloseButton(brt, font, () => { if (open != null) open.Close(); }, 16f, 72f); // ends 88 down: clear of the list at 96

        // A dark slot holds the list, so the cream text and the gold keys read on it.
        const float ListW = 720f, ListH = 760f;
        var list = (RectTransform)UiPanels.Inset(brt, "List", new Vector2(30f, -96f), new Vector2(ListW, ListH)).transform;
        float y = ListH * 0.5f - 22f;

        Section(list, Loc.T("controls.sec_move"), ref y, font);
        Row(list, "A / D", Loc.T("controls.move"), ref y, font);
        Row(list, GameInput.BindingName(GameInput.Action.Jump) + " / W", Loc.T("controls.jump"), ref y, font);
        Row(list, "S", Loc.T("controls.drop"), ref y, font);

        Section(list, Loc.T("controls.sec_combat"), ref y, font);
        // (The saved bindings, not fixed names: keys rebound in an older build still apply.)
        Row(list, GameInput.BindingName(GameInput.Action.Attack), Loc.T("controls.attack"), ref y, font);
        Row(list, GameInput.BindingName(GameInput.Action.Dash), Loc.T("controls.dash"), ref y, font);
        Row(list, Loc.T("controls.air_click"), Loc.T("controls.slam"), ref y, font);
        Row(list, GameInput.BindingName(GameInput.Action.Skill1) + " / " + GameInput.BindingName(GameInput.Action.Skill2) + " / " +
                  GameInput.BindingName(GameInput.Action.Skill3), Loc.T("controls.skills"), ref y, font);

        Section(list, Loc.T("controls.sec_info"), ref y, font);
        Row(list, "Tab", Loc.T("controls.stats"), ref y, font);
        Row(list, "ESC", Loc.T("pause.title"), ref y, font);

        GameInput.SelectForPad(x);
    }

    const float SectionH = 56f, RowH = 62f;

    // A section heading, left-aligned in gold. `y` is the next free line (from the list's centre) and moves down.
    static void Section(RectTransform list, string title, ref float y, TMP_FontAsset font)
    {
        var t = Text(list, title, UiTheme.TextLarge, new Vector2(0f, y - SectionH * 0.5f), new Vector2(660f, SectionH), font, UiTheme.Gold);
        t.alignment = TextAlignmentOptions.Left;
        y -= SectionH;
    }

    // One line: the key on a dark chip, what it does beside it.
    static void Row(RectTransform list, string key, string what, ref float y, TMP_FontAsset font)
    {
        float cy = y - RowH * 0.5f;
        var chip = new GameObject("KeyChip", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        var crt = chip.rectTransform;
        crt.SetParent(list, false);
        crt.anchoredPosition = new Vector2(-190f, cy);
        crt.sizeDelta = new Vector2(310f, 50f);
        chip.raycastTarget = false;
        if (BookUI.Get("Slot01b") != null) BookUI.Slice(chip, "Slot01b", 2f, new Color(1f, 1f, 1f, 0.9f));
        else chip.color = new Color(0.25f, 0.16f, 0.12f, 1f);
        var k = Text(crt, key, UiTheme.TextSmall, Vector2.zero, new Vector2(290f, 46f), font, UiTheme.Gold);
        k.enableAutoSizing = false;

        var d = Text(list, what, UiTheme.TextSmall, new Vector2(160f, cy), new Vector2(340f, 50f), font, UISkin.OverlayText);
        d.alignment = TextAlignmentOptions.Left;
        y -= RowH;
    }

    void Close()
    {
        if (open == this) open = null;
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (open == this) open = null;
    }

    static TextMeshProUGUI Text(RectTransform parent, string text, float size, Vector2 pos, Vector2 box, TMP_FontAsset font, Color color)
    {
        var t = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.rectTransform.SetParent(parent, false);
        t.rectTransform.anchoredPosition = pos;
        t.rectTransform.sizeDelta = box;
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
