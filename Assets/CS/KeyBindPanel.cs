using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The key-binding window (the settings' "키 설정" button, in the village's OptionsUI and the title's TitleSettings):
// one row per GameSettings.Bindings entry - click the key, then press the new key or mouse button (ESC cancels).
// Keyboard and mouse only; the gamepad's buttons stay fixed. Bindings are saved right away (GameInput.Rebind).
public class KeyBindPanel : MonoBehaviour
{
    static KeyBindPanel open;

    TMP_Text[] keyLabels;
    Button[] keyButtons;
    int waiting = -1;      // the row waiting for a key, or -1
    int waitingFrame;      // the frame the wait began: the click that started it isn't the new key

    public static bool IsOpen => open != null;

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
        root.GetComponent<Button>().onClick.AddListener(() => { if (open != null && open.waiting < 0) open.Close(); });

        int n = GameSettings.Bindings.Length;
        float rowH = 84f;
        var board = new GameObject("Board", typeof(RectTransform), typeof(Image));
        var brt = (RectTransform)board.transform;
        brt.SetParent(rt, false);
        // As big as the settings window it opens over (780 x 940), so none of that window shows round its edges.
        brt.sizeDelta = new Vector2(780f, Mathf.Max(940f, 260f + rowH * n));
        var img = board.GetComponent<Image>();
        var skin = UISkin.Instance;
        if (skin != null && skin.buttonCream != null) UISkin.Apply(img, skin.buttonCream, 3f);
        else img.color = new Color(1f, 0.95f, 0.82f);
        board.AddComponent<Button>().transition = Selectable.Transition.None; // clicks on the board don't close it

        float top = brt.sizeDelta.y * 0.5f;
        Text(brt, Loc.T("keys.title"), UiTheme.TextTitle, new Vector2(0f, top - 60f), new Vector2(680f, 64f), font, UISkin.InkTitle);
        open.keyLabels = new TMP_Text[n];
        open.keyButtons = new Button[n];
        for (int i = 0; i < n; i++)
        {
            float y = top - 150f - rowH * i;
            var label = Text(brt, Loc.T(GameSettings.Bindings[i].labelKey), UiTheme.TextSmall, new Vector2(-170f, y), new Vector2(300f, 60f), font, UISkin.Ink);
            label.alignment = TextAlignmentOptions.Left;
            int row = i;
            open.keyButtons[i] = MakeButton(brt, "", new Vector2(160f, y), new Vector2(320f, 70f), skin != null ? skin.buttonWood : null, font, out open.keyLabels[i]);
            open.keyButtons[i].onClick.AddListener(() => open.BeginWait(row));
        }
        float bottom = -top + 60f;
        MakeButton(brt, Loc.T("keys.reset"), new Vector2(-140f, bottom), new Vector2(240f, 72f), skin != null ? skin.buttonWood : null, font, out _)
            .onClick.AddListener(() => { GameInput.ResetBindings(); open.waiting = -1; open.Refresh(); });
        MakeButton(brt, Loc.T("ui.close"), new Vector2(140f, bottom), new Vector2(240f, 72f), skin != null ? skin.buttonWood : null, font, out _)
            .onClick.AddListener(() => open.Close());
        open.Refresh();
        GameInput.SelectForPad(open.keyButtons[0]);
    }

    void BeginWait(int row)
    {
        waiting = row;
        waitingFrame = Time.frameCount;
        Refresh();
    }

    void Update()
    {
        if (waiting < 0 || Time.frameCount <= waitingFrame + 1) return;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) { waiting = -1; Refresh(); return; }
        int code = GameInput.AnyBindablePressed();
        if (code == 0) return;
        GameInput.Rebind(GameSettings.Bindings[waiting].action, code);
        Sfx.Play("ui_click", 0.4f);
        waiting = -1;
        Refresh();
    }

    void Refresh()
    {
        for (int i = 0; i < keyLabels.Length; i++)
            keyLabels[i].text = i == waiting ? Loc.T("keys.press") : GameInput.BindingName(GameSettings.Bindings[i].action);
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

    static Button MakeButton(RectTransform parent, string label, Vector2 pos, Vector2 size, Sprite sprite, TMP_FontAsset font, out TMP_Text text)
    {
        var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        if (sprite != null) UISkin.Apply(img, sprite, UISkin.SmallScale);
        else img.color = UiTheme.Wood;
        go.AddComponent<ButtonSound>();
        text = Text(rt, label, UiTheme.TextSmall, Vector2.zero, size, font, Color.white);
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        return go.GetComponent<Button>();
    }
}
