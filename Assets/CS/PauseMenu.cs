using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// ESC pause menu in the stage scenes, built at runtime by RunManager (no scene edits). Four buttons; ESC again
// goes back to the fight (a window opened from the menu closes first):
//   계속하기          - back to the fight (same as ESC)
//   조작키            - the key window (KeyBindPanel): what each key does, and rebinding
//   설정              - the settings window (TitleSettings)
//   포기하고 마을로    - press twice to confirm; ends the run like a defeat: the gold
//                      collected so far is deposited, then back to the village.
// Pausing holds the GameClock like the card draft, so the player, enemies and attacks
// all stop. Not available while the card draft or the result screen is up.
public class PauseMenu : MonoBehaviour
{
    GameObject root;
    TMP_Text giveUpLabel;
    bool confirmGiveUp;
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

        // A small wooden board, the title on its ribbon, and three buttons - nothing else: the controls (the key
        // window), give the run up, the settings window. ESC goes back to the fight. (It was an open book with the
        // controls, hints, the run's numbers, a resume and a quit button.)
        var board = MakeImage(blockRt, "Board", Vector2.zero, new Vector2(560f, 650f));
        UiPanels.Board(board);
        var prt = board.rectTransform;
        UiPanels.Ribbon(prt, Loc.T("pause.title"), font, 400f);

        // The one way forward (UI_STYLE: the green "go" sprite) on top, the way out last and apart from it.
        var wood = skin != null ? skin.buttonWood : null;
        var go = skin != null ? skin.buttonRed : null;
        resumeButton = MakeButton(prt, "ResumeButton", Loc.T("pause.resume"), new Vector2(0f, 185f), go ?? wood, font, out _);
        resumeButton.onClick.AddListener(Close);
        var controls = MakeButton(prt, "ControlsButton", Loc.T("pause.controls"), new Vector2(0f, 50f), wood, font, out _);
        controls.onClick.AddListener(() => KeyBindPanel.Show(GetComponentInParent<Canvas>().rootCanvas, font));
        var settings = MakeButton(prt, "SettingsButton", Loc.T("pause.settings"), new Vector2(0f, -85f), wood, font, out _);
        settings.onClick.AddListener(() => TitleSettings.Show(GetComponentInParent<Canvas>().rootCanvas, font));
        var giveUp = MakeButton(prt, "GiveUpButton", Loc.T("pause.give_up"), new Vector2(0f, -220f), wood, font, out giveUpLabel);
        giveUp.onClick.AddListener(GiveUp);

        root.SetActive(false);
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
        confirmGiveUp = false;
        giveUpLabel.text = Loc.T("pause.give_up");
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
