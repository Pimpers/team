using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// ESC pause menu in the stage scenes, built at runtime by RunManager (no scene edits):
//   계속하기          - back to the fight (ESC again does the same)
//   포기하고 마을로    - press twice to confirm; ends the run like a defeat: the gold
//                      collected so far is deposited, then back to the village.
// Pausing holds the GameClock like the card draft, so the player, enemies and attacks
// all stop. Not available while the card draft or the result screen is up.
public class PauseMenu : MonoBehaviour
{
    GameObject root;
    TMP_Text giveUpLabel, quitLabel, statsText, cardsText;
    bool confirmGiveUp, confirmQuit;

    public static bool IsOpen { get; private set; }

    public static PauseMenu Create(Canvas canvas, TMP_FontAsset font)
    {
        var go = new GameObject("PauseMenu", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        var menu = go.AddComponent<PauseMenu>();
        menu.Build(font);
        return menu;
    }

    void Build(TMP_FontAsset font)
    {
        var skin = UISkin.Instance;
        var rt = (RectTransform)transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        transform.SetAsLastSibling(); // above the HUD and minimap

        // Dim the level behind; also blocks clicks from reaching anything under it.
        root = new GameObject("Blocker", typeof(RectTransform), typeof(Image));
        var blockRt = (RectTransform)root.transform;
        blockRt.SetParent(rt, false);
        blockRt.anchorMin = Vector2.zero;
        blockRt.anchorMax = Vector2.one;
        blockRt.offsetMin = blockRt.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        // An open travel book (BookUI): the cover behind two pages, controls on the left page, this run's cards on the
        // right. Drawn whole at 5 canvas units per art pixel (cover 1120 x 800, pages 520 x 735), the buttons below it.
        var holder = new GameObject("Book", typeof(RectTransform));
        var prt = (RectTransform)holder.transform;
        prt.SetParent(blockRt, false);
        prt.anchoredPosition = new Vector2(0f, 50f);
        prt.sizeDelta = new Vector2(1120f, 800f);
        BookUI.Piece(prt, "Cover", "BookCover01a", Vector2.zero, BookScale);
        BookUI.Piece(prt, "PageLeft", "BookPageLeft01a", new Vector2(-PageX, 0f), BookScale);
        BookUI.Piece(prt, "PageRight", "BookPageRight01a", new Vector2(PageX, 0f), BookScale);

        var ribbon = MakeImage(prt, "Banner", new Vector2(0f, 395f), new Vector2(520f, 104f));
        if (skin != null) UISkin.ApplyBanner(ribbon);
        var title = MakeText(ribbon.rectTransform, "Label", Loc.T("pause.title"), UISkin.FontTitle, UISkin.BannerText, font, Vector2.zero, Vector2.zero);
        UISkin.FitToRibbon(title.rectTransform, ribbon);

        BuildControls(prt, font);
        BuildCards(prt, font);

        var resume = resumeButton = MakeButton(prt, "ResumeButton", Loc.T("pause.resume"), new Vector2(-450f, -480f), skin != null ? skin.buttonRed : null, font, out _);
        resume.onClick.AddListener(Close);
        BookUI.ButtonIcon(resume, "IconPlay01a");
        var giveUp = MakeButton(prt, "GiveUpButton", Loc.T("pause.give_up"), new Vector2(0f, -480f), skin != null ? skin.buttonWood : null, font, out giveUpLabel);
        giveUp.onClick.AddListener(GiveUp);
        BookUI.ButtonIcon(giveUp, "IconHome01a");
        var quit = MakeButton(prt, "QuitButton", Loc.T("ui.quit"), new Vector2(450f, -480f), skin != null ? skin.buttonWood : null, font, out quitLabel);
        quit.onClick.AddListener(Quit);
        BookUI.ButtonIcon(quit, "IconCross01a");

        var hint = MakeText(prt, "Hint", Loc.T("pause.quit_note"), 22f, UISkin.InkSoft, font,
            new Vector2(-PageX, -292f), new Vector2(440f, 70f));
        hint.textWrappingMode = TextWrappingModes.Normal;

        root.SetActive(false);
    }

    // Left column: the controls, one row per key (key in ink-red, what it does beside it).
    // Built on first use, not as a static initializer: that can run inside Unity's object construction,
    // where the text table (Resources) can't be loaded yet.
    static string[,] controls;
    static string[,] Controls => controls ??= new string[,]
    {
        { "A / D", Loc.T("controls.move") },
        { "Space / W", Loc.T("controls.jump") },
        { "S", Loc.T("controls.drop") },
        { Loc.T("controls.right_click"), Loc.T("controls.dash") },
        { Loc.T("controls.left_click"), Loc.T("controls.attack") },
        { "Q / E / R", Loc.T("controls.skills") },
        { Loc.T("controls.air_click"), Loc.T("controls.slam") },
        { "ESC", Loc.T("pause.title") },
    };

    Button resumeButton;
    const float BookScale = 5f, PageX = 260f; // page centres at -260 / +260: they meet at the spine

    void BuildControls(RectTransform panel, TMP_FontAsset font)
    {
        MakeText(panel, "ControlsHeader", Loc.T("pause.controls"), UISkin.FontHeading, UISkin.InkTitle, font, new Vector2(-PageX, 285f), new Vector2(440f, 56f));
        for (int i = 0; i < Controls.GetLength(0); i++)
        {
            float y = 215f - i * 52f; // 8 rows on the left page, above the hint
            var key = MakeText(panel, "Key" + i, Controls[i, 0], 26f, new Color32(0xc0, 0x39, 0x2b, 0xff), font, new Vector2(-PageX - 135f, y), new Vector2(170f, 46f));
            key.alignment = TextAlignmentOptions.Left;
            key.enableAutoSizing = true; // "마우스 우클릭" is the longest key name
            key.fontSizeMax = 26f;
            key.fontSizeMin = UiTheme.TextMin;
            var desc = MakeText(panel, "Desc" + i, Controls[i, 1], 24f, UISkin.Ink, font, new Vector2(-PageX + 85f, y), new Vector2(260f, 46f));
            desc.alignment = TextAlignmentOptions.Left;
            desc.enableAutoSizing = true; // the longer lines shrink a little instead of overflowing
            desc.fontSizeMax = 24f;
            desc.fontSizeMin = 14f;
        }
    }

    // Right page: this run's stats and the skill cards taken so far (refreshed on open).
    void BuildCards(RectTransform panel, TMP_FontAsset font)
    {
        MakeText(panel, "CardsHeader", Loc.T("pause.cards"), UISkin.FontHeading, UISkin.InkTitle, font, new Vector2(PageX, 285f), new Vector2(440f, 56f));
        statsText = MakeText(panel, "Stats", "", 24f, UISkin.InkGold, font, new Vector2(PageX, 232f), new Vector2(440f, 40f));
        statsText.enableAutoSizing = true;
        statsText.fontSizeMax = 24f;
        statsText.fontSizeMin = UiTheme.TextMin;
        // Set bonuses at a fixed 24 (one auto-sized box for sets and cards dropped everything to an unreadable 12 as
        // soon as a bonus line was long); the cards below it are rows of icon, name and level (CardRow).
        cardsText = MakeText(panel, "Sets", "", 24f, UISkin.Ink, font, new Vector2(PageX, SetsTop), new Vector2(430f, 10f));
        cardsText.alignment = TextAlignmentOptions.TopLeft;
        cardsText.rectTransform.pivot = new Vector2(0.5f, 1f);
        cardsText.rectTransform.anchoredPosition = new Vector2(PageX, SetsTop);
        cardsText.textWrappingMode = TextWrappingModes.Normal;
        cardsPage = panel;
        cardsFont = font;
    }

    const float SetsTop = 205f, PageBottom = -320f, RowH = 54f;
    RectTransform cardsPage;
    TMP_FontAsset cardsFont;
    readonly System.Collections.Generic.List<CardRow> cardRows = new System.Collections.Generic.List<CardRow>();

    class CardRow { public RectTransform root; public Image icon; public TMP_Text name, level; }

    CardRow Row(int i)
    {
        while (cardRows.Count <= i)
        {
            var r = new CardRow();
            var go = new GameObject("CardRow" + cardRows.Count, typeof(RectTransform));
            r.root = (RectTransform)go.transform;
            r.root.SetParent(cardsPage, false);
            r.root.sizeDelta = new Vector2(430f, RowH);
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(r.root, false);
            r.icon = iconGo.GetComponent<Image>();
            r.icon.preserveAspect = true;
            r.icon.raycastTarget = false;
            r.icon.rectTransform.anchoredPosition = new Vector2(-190f, 0f);
            r.icon.rectTransform.sizeDelta = new Vector2(46f, 46f);
            r.name = MakeText(r.root, "Name", "", 24f, UISkin.Ink, cardsFont, new Vector2(-20f, 0f), new Vector2(290f, RowH));
            r.name.alignment = TextAlignmentOptions.Left;
            r.level = MakeText(r.root, "Level", "", 24f, UISkin.InkGold, cardsFont, new Vector2(165f, 0f), new Vector2(100f, RowH));
            r.level.alignment = TextAlignmentOptions.Right;
            cardRows.Add(r);
        }
        return cardRows[i];
    }

    void RefreshInfo()
    {
        var s = PlayerStats.Instance;
        if (s == null) { statsText.text = ""; cardsText.text = ""; return; }
        statsText.text = Loc.F("pause.stats", s.AttackPower, s.CurrentHP, s.MaxHP);

        var sb = new System.Text.StringBuilder();
        // Set bonuses first: the tags collected so far and what each one gives right now.
        int sets = 0;
        for (int t = 0; t < CardPool.TagNames.Length; t++)
        {
            int n = s.SetCount((CardPool.Tag)t);
            if (n <= 0) continue;
            sets++;
            int lvl = CardPool.SetLevel(n);
            string now = lvl == 0 ? "" : CardPool.SetText[t][0] + (lvl == 2 ? ", " + CardPool.SetText[t][1] : "");
            sb.Append($"<color={CardPool.TagColors[t]}>{CardPool.TagNames[t]} {n}</color>");
            sb.Append(lvl == 0 ? Loc.F("pause.set_next", CardPool.SetStep1, CardPool.SetText[t][0]) : $"<color={UiTheme.Html("ink")}>  {now}</color>\n");
        }
        int owned = 0;
        foreach (var c in CardPool.All) if (s.CardLevel(c.id) > 0) owned++;
        if (owned == 0) sb.Append(Loc.T("pause.no_cards"));
        cardsText.text = sb.ToString().TrimEnd('\n');

        // Card rows under the set lines, as many as the page holds; the rest are counted on the last row.
        float y = SetsTop - (cardsText.text.Length > 0 ? cardsText.preferredHeight + 18f : 0f) - RowH * 0.5f;
        int room = Mathf.Max(0, Mathf.FloorToInt((y - PageBottom) / RowH) + 1);
        int shown = 0;
        foreach (var c in CardPool.All)
        {
            int lv = s.CardLevel(c.id);
            if (lv <= 0) continue;
            if (shown >= room || (shown == room - 1 && owned > room)) break;
            var r = Row(shown);
            r.root.gameObject.SetActive(true);
            r.root.anchoredPosition = new Vector2(PageX, y - shown * RowH);
            r.icon.sprite = SkillHUD.Icon(c);
            r.icon.enabled = r.icon.sprite != null;
            r.name.text = c.name;
            r.level.text = c.maxLevel > 0 ? $"Lv.{lv}/{c.maxLevel}" : "";
            shown++;
        }
        if (owned > shown && room > 0)
        {
            var r = Row(shown);
            r.root.gameObject.SetActive(true);
            r.root.anchoredPosition = new Vector2(PageX, y - shown * RowH);
            r.icon.enabled = false;
            r.name.text = Loc.F("pause.more_cards", owned - shown);
            r.level.text = "";
            shown++;
        }
        for (int i = shown; i < cardRows.Count; i++) cardRows[i].root.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!GameInput.PausePressed) return; // ESC or the gamepad's Start
        if (!IsOpen && TutorialController.InTips) return; // the tutorial's tips: ESC skips them instead
        if (IsOpen) Close();
        else if (CanOpen()) Open();
    }

    // Not over the card draft / result screen (they pause or end the game themselves).
    static bool CanOpen()
    {
        if (GameClock.Paused) return false;
        var rm = RunManager.Instance;
        return rm == null || !rm.RunEnded;
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    public void OpenForPlaytest()
    {
        if (!IsOpen && CanOpen()) Open();
    }
#endif

    void Open()
    {
        IsOpen = true;
        confirmGiveUp = false;
        giveUpLabel.text = Loc.T("pause.give_up");
        confirmQuit = false;
        quitLabel.text = Loc.T("ui.quit");
        transform.SetAsLastSibling(); // the skill bar / monster tracker are created after this menu: stay above them
        RefreshInfo();
        GameInput.SelectForPad(resumeButton); // pad: A resumes, d-pad reaches the other two
        root.SetActive(true);
        GameClock.Pause("pause");
        Sfx.Play("ui_open");
    }

    void Close()
    {
        if (IsOpen) Sfx.Play("ui_close");
        IsOpen = false;
        root.SetActive(false);
        GameClock.Resume("pause");
    }

    // Two clicks, like giving up. The gold of this run is banked by RunManager.OnApplicationQuit.
    void Quit()
    {
        if (!confirmQuit)
        {
            confirmQuit = true;
            quitLabel.text = Loc.T("ui.quit_confirm");
            return;
        }
        GameSettings.Quit();
    }

    // Alt-tabbing away mid-fight pauses the game instead of letting the monsters keep going.
    // (Not in batch mode, where there is no window to lose focus.)
    void OnApplicationFocus(bool focused)
    {
        if (!focused && !Application.isBatchMode && !IsOpen && root != null && CanOpen()) Open();
    }

    void GiveUp()
    {
        if (!confirmGiveUp)
        {
            confirmGiveUp = true;
            giveUpLabel.text = Loc.T("pause.give_up_confirm");
            return;
        }
        IsOpen = false;
        if (RunManager.Instance != null) RunManager.Instance.GiveUp();
    }

    void OnDestroy()
    {
        IsOpen = false;
    }

    // ---------------------------------------------------------------- building helpers
    static Image MakeImage(RectTransform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return go.GetComponent<Image>();
    }

    static TMP_Text MakeText(RectTransform parent, string name, string text, float size, Color color, TMP_FontAsset font, Vector2 pos, Vector2 box)
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

    static Button MakeButton(RectTransform parent, string name, string label, Vector2 pos, Sprite sprite, TMP_FontAsset font, out TMP_Text text)
    {
        var img = MakeImage(parent, name, pos, new Vector2(420f, 110f));
        if (sprite != null) UISkin.Apply(img, sprite, UISkin.SmallScale);
        var btn = img.gameObject.AddComponent<Button>();
        img.gameObject.AddComponent<ButtonSound>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(1f, 0.92f, 0.8f);
        colors.pressedColor = new Color(0.8f, 0.72f, 0.6f);
        btn.colors = colors;
        text = MakeText(img.rectTransform, "Label", label, UISkin.FontButton, Color.white, font, Vector2.zero, Vector2.zero);
        var trt = text.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        return btn;
    }
}
