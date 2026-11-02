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
    TMP_Text giveUpLabel, quitLabel;
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

    // Right page: settings, not the run's numbers and cards - those are the Tab sheet's (BuildSheet), a glance held
    // mid-fight; the pause book is for stopping: the controls (left), the settings window and the way out.
    void BuildCards(RectTransform panel, TMP_FontAsset font)
    {
        MakeText(panel, "MenuHeader", Loc.T("pause.settings"), UISkin.FontHeading, UISkin.InkTitle, font, new Vector2(PageX, 285f), new Vector2(440f, 56f));
        var skin = UISkin.Instance;
        var open = MakeButton(panel, "SettingsButton", Loc.T("pause.open_settings"), new Vector2(PageX, 150f), skin != null ? skin.buttonWood : null, font, out _);
        open.onClick.AddListener(() => TitleSettings.Show(panel.GetComponentInParent<Canvas>().rootCanvas, font));
        var hint = MakeText(panel, "TabHint", Loc.T("pause.tab_hint"), 24f, UISkin.Ink, font, new Vector2(PageX, -10f), new Vector2(440f, 150f));
        hint.textWrappingMode = TextWrappingModes.Normal;
    }

    void Update()
    {
        if (!GameInput.PausePressed) return; // ESC or the gamepad's Start
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
