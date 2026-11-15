using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Title screen extras: 설정 and 크레딧 above 게임 종료, so a first-time player can set the volume or read the credits
// before starting (both used to live only in the village). Built at runtime from the quit button, so the title scene
// itself is unchanged and the buttons share its look and click sound.
public static class TitleMenu
{
    const string TitleScene = "TitleScene";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        OnLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != TitleScene) return;
        var quit = Object.FindAnyObjectByType<QuitButton>();
        if (quit == null || quit.transform.parent.Find("CreditsButton") != null) return;
        var rt = (RectTransform)quit.transform;
        var canvas = quit.GetComponentInParent<Canvas>().rootCanvas;
        var label = quit.GetComponentInChildren<TMP_Text>(true);
        var font = label != null ? label.font : null;
        // The menu reads as a menu next to the big logo: body-size (36) labels rather than 24.
        if (label != null) { label.enableAutoSizing = false; label.fontSize = UiTheme.TextLarge; }
        // The decorated board the result and credits panels use, in place of the plain wood plank (it read as a
        // placeholder under the logo). Before the clones, so all three buttons carry it.
        var plank = quit.GetComponent<Image>();
        if (plank != null && BookUI.Get("UIBundle/MedievalBoard") != null) BookUI.Slice(plank, "UIBundle/MedievalBoard", 3f);
        Clone(rt, "CreditsButton", Loc.T("credits.title"), 1).onClick.AddListener(() => CreditsPanel.Show(canvas, font));
        Clone(rt, "SettingsButton", Loc.T("harbor.settings"), 2).onClick.AddListener(() => TitleSettings.Show(canvas, font));
        // Cream on the bright log board was low in contrast: the shared 1 px dark outline on all three labels
        // (after the clones, so each label gets its own copies).
        foreach (var b in rt.parent.GetComponentsInChildren<Button>())
            if (plank != null && b.GetComponent<Image>() != null && b.GetComponent<Image>().sprite == plank.sprite)
                foreach (var t in b.GetComponentsInChildren<TMP_Text>()) UiTheme.Outline(t);
        // Gamepad: the full-screen "click anywhere" button starts selected, so A starts the game.
        var start = GameObject.Find("ClickCatcher");
        if (start != null) GameInput.SelectForPad(start.GetComponent<Button>());
    }

    static Button Clone(RectTransform quit, string name, string text, int slot)
    {
        var go = Object.Instantiate(quit.gameObject, quit.parent);
        go.name = name;
        Object.Destroy(go.GetComponent<QuitButton>());
        var btn = go.GetComponent<Button>();
        btn.onClick = new Button.ButtonClickedEvent(); // drop the copied quit
        foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
        {
            var loc = t.GetComponent<LocText>();
            if (loc != null) Object.Destroy(loc);
            t.text = text;
        }
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = quit.anchoredPosition + Vector2.up * (quit.rect.height + 16f) * slot;
        return btn;
    }
}

// The title's settings: the volumes, screen shake and full screen (the village's OptionsUI is tied to its own scene
// panel). A plain panel built on demand; the values are GameSettings', so they match the village's settings.
public static class TitleSettings
{
    // The window that is up (for ESC in the pause menu: it closes this first, then the menu).
    static GameObject open;
    public static bool IsOpen => open != null;
    public static void Close() { if (open != null) Object.Destroy(open); open = null; }

    public static void Show(Canvas canvas, TMP_FontAsset font)
    {
        Close();
        var shade = open = new GameObject("TitleSettings", typeof(RectTransform), typeof(Image), typeof(Button));
        var srt = (RectTransform)shade.transform;
        srt.SetParent(canvas.transform, false);
        srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = srt.offsetMax = Vector2.zero;
        shade.GetComponent<Image>().color = UISkin.ModalShade;
        shade.GetComponent<Button>().onClick.AddListener(() => Object.Destroy(shade)); // click outside closes

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        var prt = (RectTransform)panel.transform;
        prt.SetParent(srt, false);
        prt.sizeDelta = new Vector2(700f, 660f);
        var pimg = panel.GetComponent<Image>();
        var skin = UISkin.Instance;
        if (skin != null && skin.buttonCream != null) UISkin.Apply(pimg, skin.buttonCream, 3f);
        else pimg.color = new Color(1f, 0.95f, 0.82f);
        panel.AddComponent<Button>().transition = Selectable.Transition.None; // clicks on the panel don't reach the shade

        const float dy = -93f; // the content is centred in the shorter panel
        Text(prt, Loc.T("harbor.settings"), UiTheme.TextTitle, new Vector2(0f, 340f + dy), new Vector2(600f, 64f), font, UISkin.InkTitle);
        // The same rows as the village's settings (GameSettings.Sliders / Toggles).
        for (int i = 0; i < GameSettings.Sliders.Length; i++)
        {
            var s = GameSettings.Sliders[i];
            Row(prt, Loc.T(s.labelKey), s.get(), s.set, 250f + dy - 100f * i, font);
        }
        Button firstToggle = null;
        for (int i = 0; i < GameSettings.Toggles.Length; i++)
        {
            var toggle = GameSettings.Toggles[i];
            // Two per row.
            var b = MakeButton(prt, "", new Vector2(-165f + 330f * (i % 2), -150f + dy - 85f * (i / 2)), new Vector2(310f, 72f), skin != null ? skin.buttonWood : null, font);
            var label = b.GetComponentInChildren<TMP_Text>();
            var bimg = b.GetComponent<Image>();
            void Refresh() { label.text = GameSettings.ToggleText(toggle); if (bimg != null) bimg.color = GameSettings.ToggleTint(toggle); }
            Refresh();
            b.onClick.AddListener(() => { toggle.set(!toggle.get()); Refresh(); });
            if (firstToggle == null) firstToggle = b;
        }
        // 닫기 is the X in the corner. (No 키 설정 or 게임 종료 here: the pause menu has the controls and the quit.)
        var close = UiPanels.CloseButton(prt, font, () => Object.Destroy(shade), 20f, 72f);
        GameInput.SelectForPad(firstToggle != null ? firstToggle : close);
    }

    static void Row(RectTransform panel, string label, float value, System.Action<float> set, float y, TMP_FontAsset font)
    {
        Text(panel, label, UiTheme.TextSmall, new Vector2(-150f, y + 26f), new Vector2(300f, 40f), font, UISkin.Ink).alignment = TextAlignmentOptions.Left;
        var go = DefaultControls.CreateSlider(new DefaultControls.Resources());
        var rt = (RectTransform)go.transform;
        rt.SetParent(panel, false);
        rt.anchoredPosition = new Vector2(0f, y - 14f);
        rt.sizeDelta = new Vector2(560f, 24f);
        foreach (var img in go.GetComponentsInChildren<Image>(true))
            img.color = img.name == "Fill" ? new Color(0.45f, 0.7f, 0.3f) : img.name == "Handle" ? new Color(0.3f, 0.2f, 0.12f) : new Color(0.55f, 0.45f, 0.35f);
        var slider = go.GetComponent<Slider>();
        slider.value = value;
        slider.onValueChanged.AddListener(v => set(v));
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

    static Button MakeButton(RectTransform parent, string label, Vector2 pos, Vector2 size, Sprite sprite, TMP_FontAsset font)
    {
        var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        if (sprite != null) UISkin.Apply(img, sprite, UISkin.SmallScale);
        var t = Text(rt, label, UiTheme.TextSmall, Vector2.zero, size, font, Color.white);
        t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one; t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
        return go.GetComponent<Button>();
    }
}
