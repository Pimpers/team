using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ESC in the village: the screen stops and a small board offers 계속하기 and 게임 종료 (quitting is here, not in the
// settings window). Built at runtime by MainSceneController; ESC again, the X or 계속하기 closes it.
public class VillageMenu : MonoBehaviour
{
    GameObject root;
    TMP_Text quitLabel;
    Button resumeButton;
    bool confirmQuit;

    // Set by the scene: false while a window of the village (settings, the blacksmith) is up, so ESC leaves it alone.
    public System.Func<bool> canOpen;

    public static bool IsOpen { get; private set; }

    public static VillageMenu Create(Canvas canvas, TMP_FontAsset font)
    {
        var go = new GameObject("VillageMenu", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        var menu = go.AddComponent<VillageMenu>();
        menu.Build(font);
        return menu;
    }

    void Build(TMP_FontAsset font)
    {
        var skin = UISkin.Instance;
        var rt = (RectTransform)transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        transform.SetAsLastSibling();

        root = new GameObject("Blocker", typeof(RectTransform), typeof(Image));
        var blockRt = (RectTransform)root.transform;
        blockRt.SetParent(rt, false);
        blockRt.anchorMin = Vector2.zero; blockRt.anchorMax = Vector2.one; blockRt.offsetMin = blockRt.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f); // dims the village and swallows clicks

        var board = new GameObject("Board", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        var brt = board.rectTransform;
        brt.SetParent(blockRt, false);
        brt.sizeDelta = new Vector2(560f, 440f);
        UiPanels.Board(board);
        UiPanels.Ribbon(brt, Loc.T("pause.title"), font, 400f);
        UiPanels.CloseButton(brt, font, Close, 28f, 72f);

        var wood = skin != null ? skin.buttonWood : null;
        var go = skin != null ? skin.buttonRed : null; // the green "go" sprite: the one way forward
        resumeButton = PauseMenu.MakeButton(brt, "ResumeButton", Loc.T("pause.resume"), new Vector2(0f, 35f), go != null ? go : wood, font, out _);
        resumeButton.onClick.AddListener(Close);
        var quit = PauseMenu.MakeButton(brt, "QuitButton", Loc.T("ui.quit"), new Vector2(0f, -100f), wood, font, out quitLabel);
        quit.onClick.AddListener(OnQuit);

        root.SetActive(false);
    }

    void Update()
    {
        if (!GameInput.PausePressed) return; // ESC or the gamepad's Start
        if (IsOpen) Close();
        else if (canOpen == null || canOpen()) Open();
    }

    void Open()
    {
        IsOpen = true;
        confirmQuit = false;
        quitLabel.text = Loc.T("ui.quit");
        transform.SetAsLastSibling();
        GameInput.SelectForPad(resumeButton);
        root.SetActive(true);
        GameClock.Pause("village_menu");
        Sfx.Play("ui_open");
    }

    void Close()
    {
        if (!IsOpen) return;
        Sfx.Play("ui_close");
        IsOpen = false;
        root.SetActive(false);
        GameClock.Resume("village_menu");
    }

    // Two clicks, so a stray one does not end the game (the gold is already banked in the village).
    void OnQuit()
    {
        if (!confirmQuit) { confirmQuit = true; quitLabel.text = Loc.T("ui.quit_confirm"); return; }
        GameSettings.Quit();
    }

    void OnDestroy()
    {
        if (IsOpen) GameClock.Resume("village_menu");
        IsOpen = false;
    }
}
