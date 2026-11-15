using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The game's information boards, built one way: a riveted wooden board (UIBundle/MedievalBoard, the credits
// board's frame) around dark inset slots (UIBook/Slot01a) that hold the text, and the red ribbon (UISkin.banner)
// across the top for the title - the same wood, red and cream as the buttons and the pause book, instead of plain
// dark rectangles. Used by the Tab sheet (BuildSheet), the result board's two columns (ResultUI) and the
// village's goals board (VillageGoals).
public static class UiPanels
{
    public const float BoardScale = 4f, InsetScale = 3f;

    // The board itself on `img` (falls back to the dark plate when the sprite is missing).
    public static void Board(Image img)
    {
        if (NewBoard(img, 3f)) return;
        if (BookUI.Get("UIBundle/MedievalBoard") != null) BookUI.Slice(img, "UIBundle/MedievalBoard", BoardScale);
        else img.color = UiTheme.Plate;
    }

    // The new dark window (Resources/UICats/ui_panel_dark: a wooden frame with vine corners and a dark middle, 18 px
    // borders) at `scale` canvas units per art pixel. False when the art is missing (the old board is used then).
    public static bool NewBoard(Image img, float scale)
    {
        var board = Resources.Load<Sprite>("UICats/ui_panel_dark");
        if (img == null || board == null) return false;
        img.sprite = board;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f / scale;
        img.color = Color.white;
        return true;
    }

    // A full-screen darkening behind a panel that is smaller than the screen (the card draft's 1192 x 664 box left a
    // hard-edged rectangle with the game - and the cat - showing round and between the cards). It is the panel's first
    // child, so it comes and goes with the panel; it reaches far past every edge, so any aspect ratio is covered.
    public static void ScreenShade(RectTransform panel)
    {
        if (panel == null || panel.Find("ScreenShade") != null) return;
        var img = new GameObject("ScreenShade", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        var rt = img.rectTransform;
        rt.SetParent(panel, false);
        rt.SetAsFirstSibling();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-4000f, -4000f); rt.offsetMax = new Vector2(4000f, 4000f);
        img.color = UISkin.ModalShade;
        img.raycastTarget = true; // clicks outside the cards do not reach the fight
    }

    // A dark inset slot inside `parent`, top-left at `pos`, `size` big; text goes on top of it.
    public static Image Inset(RectTransform parent, string name, Vector2 pos, Vector2 size)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        var rt = img.rectTransform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        SkinInset(img);
        img.raycastTarget = false;
        return img;
    }

    // The new dark inset (Resources/UICats/ui_inset_dark, opaque, 6 px borders) on `img`; the old slot art when it is missing.
    public static void SkinInset(Image img)
    {
        var art = Resources.Load<Sprite>("UICats/ui_inset_dark");
        if (art != null)
        {
            img.sprite = art;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f / InsetScale;
            img.color = Color.white;
        }
        else if (BookUI.Get("Slot01a") != null) BookUI.Slice(img, "Slot01a", InsetScale, new Color(1f, 1f, 1f, 0.96f));
        else img.color = new Color(0.09f, 0.06f, 0.07f, 0.9f);
    }

    // The red title ribbon centred on the top edge of `parent`, its label inside.
    public static TMP_Text Ribbon(RectTransform parent, string title, TMP_FontAsset font, float width, float height = 64f)
    {
        var img = new GameObject("TitleRibbon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        var rt = img.rectTransform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -4f);
        rt.sizeDelta = new Vector2(width, height);
        img.raycastTarget = false;
        if (UISkin.Instance != null && UISkin.Instance.banner != null) UISkin.ApplyBanner(img);
        else img.color = UiTheme.Wine;

        var label = new GameObject("Title", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        label.rectTransform.SetParent(rt, false);
        if (font != null) label.font = font;
        label.text = title;
        label.fontSize = UiTheme.TextSmall;
        label.color = UISkin.BannerText;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        UISkin.FitToRibbon(label.rectTransform, img);
        return label;
    }

    // The close button every window shares: a small wooden "X" in the board's top-right corner, in place of a
    // "닫기" button along the bottom. `margin` is its distance from the corner.
    public static Button CloseButton(RectTransform board, TMP_FontAsset font, System.Action onClose, float margin = 24f, float size = 72f)
    {
        var go = new GameObject("CloseX", typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform;
        rt.SetParent(board, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-margin, -margin);
        rt.sizeDelta = new Vector2(size, size);
        var img = go.GetComponent<Image>();
        // The new round wooden button with its X drawn in (Resources/UICats/ui_close, pressed: ui_close_down), at x2.
        var closeSprite = Resources.Load<Sprite>("UICats/ui_close");
        var closeDown = Resources.Load<Sprite>("UICats/ui_close_down");
        if (closeSprite != null)
        {
            img.sprite = closeSprite;
            img.preserveAspect = true;
            rt.sizeDelta = closeSprite.rect.size * 2f;
            go.AddComponent<ButtonSound>();
            var round = go.GetComponent<Button>();
            round.targetGraphic = img;
            round.transition = Selectable.Transition.SpriteSwap;
            var states = round.spriteState;
            states.pressedSprite = closeDown;
            states.selectedSprite = closeDown;
            round.spriteState = states;
            round.onClick.AddListener(() => onClose?.Invoke());
            return round;
        }
        if (UISkin.Instance != null && UISkin.Instance.buttonWood != null) UISkin.Apply(img, UISkin.Instance.buttonWood, UISkin.SmallScale);
        else img.color = UiTheme.Wood;
        go.AddComponent<ButtonSound>();

        var label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        label.rectTransform.SetParent(rt, false);
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        if (font != null) label.font = font;
        label.text = "X";
        label.fontSize = UiTheme.TextLarge;
        label.color = UiTheme.Cream;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(() => onClose?.Invoke());
        return btn;
    }

    // A small icon (a card's art) at `pos` (its centre) inside `parent`.
    public static Image Icon(RectTransform parent, Sprite sprite, Vector2 pos, float size)
    {
        var img = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        var rt = img.rectTransform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size);
        img.sprite = sprite;
        img.preserveAspect = true;
        img.enabled = sprite != null;
        img.raycastTarget = false;
        return img;
    }
}
