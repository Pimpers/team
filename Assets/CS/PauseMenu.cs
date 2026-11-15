using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// ESC pause menu in the stage scenes, built at runtime by RunManager (no scene edits). A strip with the run so far,
// then five buttons; ESC again goes back to the fight (a window opened from the menu closes first):
//   계속하기          - back to the fight (same as ESC)
//   조작 방법          - the controls window (KeyBindPanel): what each key does
//   설정              - the settings window (TitleSettings)
//   마을로 돌아가기    - press twice to confirm; ends the run like a defeat: the gold
//                      collected so far is deposited, then back to the village.
//   게임 종료          - press twice to confirm.
// Pausing holds the GameClock like the card draft, so the player, enemies and attacks
// all stop. Not available while the card draft or the result screen is up.
public class PauseMenu : MonoBehaviour
{
    GameObject root;
    TMP_Text giveUpLabel, quitLabel, statsText;
    bool confirmGiveUp, confirmQuit;
    Button resumeButton; // the first button (계속하기): the pad's selection when the menu opens

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

        // One wooden board: the run so far on a dark strip (stage, time, kills), the one way forward in green, the
        // controls and the settings side by side, and below them the two ways out - back to the village, quit the
        // game. The X in the corner and ESC also go back to the fight.
        var board = MakeImage(blockRt, "Board", Vector2.zero, new Vector2(640f, 560f));
        UiPanels.Board(board);
        var prt = board.rectTransform;
        UiPanels.Ribbon(prt, Loc.T("pause.title"), font, 400f);
        UiPanels.CloseButton(prt, font, Close, 16f, 72f); // ends 88 down: clear of the strip at 92

        var strip = (RectTransform)UiPanels.Inset(prt, "RunStrip", new Vector2(30f, -92f), new Vector2(580f, 64f)).transform;
        statsText = MakeText(strip, "Stats", "", UiTheme.TextSmall, UISkin.OverlayText, font, Vector2.zero, new Vector2(560f, 56f));

        var wood = skin != null ? skin.buttonWood : null;
        var go = skin != null ? skin.buttonRed : null; // the green "go" sprite (UI_STYLE): the one way forward
        var big = new Vector2(580f, 100f);
        var half = new Vector2(282f, 84f);
        resumeButton = MakeButton(prt, "ResumeButton", Loc.T("pause.resume"), new Vector2(0f, 40f), go != null ? go : wood, font, out _, big);
        resumeButton.onClick.AddListener(Close);
        var controls = MakeButton(prt, "ControlsButton", Loc.T("pause.controls"), new Vector2(-149f, -66f), wood, font, out _, half, UiTheme.TextSmall);
        controls.onClick.AddListener(() => KeyBindPanel.Show(GetComponentInParent<Canvas>().rootCanvas, font));
        var settings = MakeButton(prt, "SettingsButton", Loc.T("pause.settings"), new Vector2(149f, -66f), wood, font, out _, half, UiTheme.TextSmall);
        settings.onClick.AddListener(() => TitleSettings.Show(GetComponentInParent<Canvas>().rootCanvas, font));
        var giveUp = MakeButton(prt, "GiveUpButton", Loc.T("pause.to_village"), new Vector2(-149f, -162f), wood, font, out giveUpLabel, half, UiTheme.TextSmall);
        giveUp.onClick.AddListener(GiveUp);
        var quit = MakeButton(prt, "QuitButton", Loc.T("ui.quit"), new Vector2(149f, -162f), wood, font, out quitLabel, half, UiTheme.TextSmall);
        quit.onClick.AddListener(Quit);
        // The hint sits on the new dark window (it was dark ink on the old orange planks): the muted cream.
        MakeText(prt, "Hint", Loc.T("pause.hint"), UiTheme.TextSmall, UISkin.OverlayMuted, font, new Vector2(0f, -230f), new Vector2(580f, 40f));

        root.SetActive(false);
    }

    void RefreshStats()
    {
        int total = RunManager.Instance != null ? RunManager.Instance.totalStages : 5;
        statsText.text = Loc.F("pause.stats", Mathf.Max(1, RunStats.Stage), total, RunStats.TimeText, RunStats.Kills);
    }

    void Update()
    {
        if (!GameInput.PausePressed) return; // ESC or the gamepad's Start
        // One window back per press: the key window, then the settings, then the menu itself (back to the fight).
        // ESC used to close only the menu underneath - the fight ran on with the settings window still up.
        if (IsOpen && KeyBindPanel.IsOpen) { KeyBindPanel.Escape(); return; }
        if (IsOpen && TitleSettings.IsOpen) { TitleSettings.Close(); return; }
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
        confirmGiveUp = confirmQuit = false;
        giveUpLabel.text = Loc.T("pause.to_village");
        quitLabel.text = Loc.T("ui.quit");
        RefreshStats();
        transform.SetAsLastSibling(); // the skill bar / monster tracker are created after this menu: stay above them
        GameInput.SelectForPad(resumeButton);
        root.SetActive(true);
        GameClock.Pause("pause");
        Sfx.Play("ui_open");
    }

    void Close()
    {
        if (IsOpen) Sfx.Play("ui_close");
        TitleSettings.Close(); // (a window left over the menu must not stay up over the fight)
        if (KeyBindPanel.IsOpen) KeyBindPanel.Escape();
        IsOpen = false;
        root.SetActive(false);
        GameClock.Resume("pause");
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
            confirmQuit = false;
            quitLabel.text = Loc.T("ui.quit");
            giveUpLabel.text = Loc.T("pause.give_up_confirm");
            return;
        }
        IsOpen = false;
        if (RunManager.Instance != null) RunManager.Instance.GiveUp();
    }

    // Quit the game: two presses, like giving up (the run's gold is banked by RunManager.OnApplicationQuit).
    void Quit()
    {
        if (!confirmQuit)
        {
            confirmQuit = true;
            confirmGiveUp = false;
            giveUpLabel.text = Loc.T("pause.to_village");
            quitLabel.text = Loc.T("ui.quit_confirm");
            return;
        }
        GameSettings.Quit();
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

    internal static Button MakeButton(RectTransform parent, string name, string label, Vector2 pos, Sprite sprite, TMP_FontAsset font, out TMP_Text text, Vector2? size = null, float fontSize = 0f)
    {
        var img = MakeImage(parent, name, pos, size ?? new Vector2(420f, 110f));
        if (sprite != null) UISkin.Apply(img, sprite, UISkin.SmallScale);
        var btn = img.gameObject.AddComponent<Button>();
        img.gameObject.AddComponent<ButtonSound>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(1f, 0.92f, 0.8f);
        colors.pressedColor = new Color(0.8f, 0.72f, 0.6f);
        btn.colors = colors;
        text = MakeText(img.rectTransform, "Label", label, fontSize > 0f ? fontSize : UISkin.FontButton, Color.white, font, Vector2.zero, Vector2.zero);
        var trt = text.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        return btn;
    }
}
