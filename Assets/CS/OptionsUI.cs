using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Settings window opened by the gear icon: master / music / effects volume, screen shake, hit-stop, fullscreen,
// the credits (CreditsPanel) and quitting the game. Values are saved right away (GameSettings) and apply to every scene.
// The scene only contains the master-volume row and the close button; the other rows are
// cloned from those at startup (BuildExtras) so they share the same skin and fonts.
public class OptionsUI : MonoBehaviour
{
    public GameObject panel;
    public Slider volumeSlider;
    public TMP_Text volumeValue;
    public Button closeButton;

    Slider musicSlider, sfxSlider, shakeSlider;
    TMP_Text musicValue, sfxValue, shakeValue, fullscreenLabel, hitStopLabel, quitLabel;
    Button fullscreenButton, hitStopButton, quitButton, creditsButton;
    bool confirmQuit;

    public bool IsOpen => panel != null && panel.activeSelf;

    void Awake()
    {
        volumeSlider.minValue = 0f;
        volumeSlider.maxValue = 1f;
        volumeSlider.onValueChanged.AddListener(v => { GameSettings.MasterVolume = v; Show(volumeValue, v); });
        closeButton.onClick.AddListener(Close);
        BuildExtras();
        panel.SetActive(false);
    }

    public void Open()
    {
        confirmQuit = false;
        panel.SetActive(true);
        volumeSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
        Show(volumeValue, GameSettings.MasterVolume);
        if (musicSlider != null) { musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume); Show(musicValue, GameSettings.MusicVolume); }
        if (sfxSlider != null) { sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume); Show(sfxValue, GameSettings.SfxVolume); }
        if (shakeSlider != null) { shakeSlider.SetValueWithoutNotify(GameSettings.ShakeStrength); Show(shakeValue, GameSettings.ShakeStrength); }
        RefreshButtons();
    }

    public void Close()
    {
        panel.SetActive(false);
    }

    // ---------------------------------------------------------------- extra rows
    void BuildExtras()
    {
        var inner = volumeSlider.transform.parent as RectTransform;
        var frame = inner != null ? inner.parent as RectTransform : null;
        var labelT = inner != null ? inner.Find("VolumeLabel") as RectTransform : null;
        var valueT = volumeValue != null ? volumeValue.rectTransform : null;
        if (frame == null || labelT == null || valueT == null) return; // an older layout: leave it alone

        frame.sizeDelta = new Vector2(780f, 900f);
        inner.sizeDelta = new Vector2(768f, 888f);
        Move(inner.Find("Title"), 0f, 382f);
        Move(inner.Find("Divider"), 0f, 320f);

        // Master row (existing), then music, effects and screen shake cloned from it.
        // Rows 128 apart, each slider 58 under its label: the label of the next row no longer sits on the slider above.
        PlaceRow(labelT, valueT, volumeSlider.transform as RectTransform, 266f);
        musicSlider = CloneRow(inner, labelT, valueT, volumeSlider, "Music", Loc.T("options.music"), 138f, out musicValue);
        sfxSlider = CloneRow(inner, labelT, valueT, volumeSlider, "Sfx", Loc.T("options.sfx"), 10f, out sfxValue);
        shakeSlider = CloneRow(inner, labelT, valueT, volumeSlider, "Shake", Loc.T("options.shake"), -118f, out shakeValue);
        musicSlider.onValueChanged.AddListener(v => { GameSettings.MusicVolume = v; Show(musicValue, v); });
        sfxSlider.onValueChanged.AddListener(v => { GameSettings.SfxVolume = v; Show(sfxValue, v); });
        shakeSlider.onValueChanged.AddListener(v => { GameSettings.ShakeStrength = v; Show(shakeValue, v); if (v > 0f) CameraFollow.Shake(0.25f, 0.25f); }); // a taste of the new strength

        // Buttons are clones of "닫기" so they carry its look and click sound.
        var wood = UISkin.Instance != null ? UISkin.Instance.buttonWood : null;
        fullscreenButton = CloneButton("FullscreenButton", new Vector2(-190f, -272f), new Vector2(360f, 80f), wood, out fullscreenLabel);
        fullscreenButton.onClick.AddListener(() => { GameSettings.Fullscreen = !GameSettings.Fullscreen; RefreshButtons(); });
        hitStopButton = CloneButton("HitStopButton", new Vector2(190f, -272f), new Vector2(360f, 80f), wood, out hitStopLabel);
        hitStopButton.onClick.AddListener(() => { GameSettings.HitStopEnabled = !GameSettings.HitStopEnabled; RefreshButtons(); });
        quitButton = CloneButton("QuitButton", new Vector2(-250f, -372f), new Vector2(230f, 80f), wood, out quitLabel); // wood, not the green "go" button
        quitButton.onClick.AddListener(OnQuit);
        creditsButton = CloneButton("CreditsButton", new Vector2(0f, -372f), new Vector2(230f, 80f), wood, out var creditsLabel);
        if (creditsLabel != null) creditsLabel.text = Loc.T("credits.title");
        creditsButton.onClick.AddListener(() =>
        {
            var canvas = panel != null ? panel.GetComponentInParent<Canvas>() : null;
            if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
            CreditsPanel.Show(canvas, creditsLabel != null ? creditsLabel.font : null);
        });
        var close = (RectTransform)closeButton.transform;
        close.anchoredPosition = new Vector2(250f, -372f);
        close.sizeDelta = new Vector2(230f, 80f);
        RefreshButtons();
    }

    static void Move(Transform t, float x, float y)
    {
        if (t != null) ((RectTransform)t).anchoredPosition = new Vector2(x, y);
    }

    // Label + percentage above the slider, as in the original single row.
    static void PlaceRow(RectTransform label, RectTransform value, RectTransform slider, float y)
    {
        label.sizeDelta = new Vector2(300f, 50f);
        label.anchoredPosition = new Vector2(-150f, y);
        value.anchoredPosition = new Vector2(250f, y);
        slider.anchoredPosition = new Vector2(0f, y - 58f);
    }

    static Slider CloneRow(RectTransform inner, RectTransform label, RectTransform value, Slider slider, string id, string text, float y, out TMP_Text valueText)
    {
        var l = Instantiate(label.gameObject, inner, false);
        l.name = id + "Label";
        l.GetComponent<TMP_Text>().text = text;
        var v = Instantiate(value.gameObject, inner, false);
        v.name = id + "Value";
        valueText = v.GetComponent<TMP_Text>();
        var s = Instantiate(slider.gameObject, inner, false);
        s.name = id + "Slider";
        var clone = s.GetComponent<Slider>();
        clone.onValueChanged.RemoveAllListeners();
        PlaceRow((RectTransform)l.transform, (RectTransform)v.transform, (RectTransform)s.transform, y);
        return clone;
    }

    Button CloneButton(string name, Vector2 pos, Vector2 size, Sprite sprite, out TMP_Text label)
    {
        var go = Instantiate(closeButton.gameObject, closeButton.transform.parent, false);
        go.name = name;
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        if (sprite != null && img != null) UISkin.Apply(img, sprite, UISkin.SmallScale);
        var btn = go.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        label = go.GetComponentInChildren<TMP_Text>();
        return btn;
    }

    void OnQuit()
    {
        if (!confirmQuit) { confirmQuit = true; RefreshButtons(); return; }
        GameSettings.Quit();
    }

    void RefreshButtons()
    {
        if (fullscreenLabel != null) fullscreenLabel.text = GameSettings.Fullscreen ? Loc.T("options.fullscreen.on") : Loc.T("options.fullscreen.off");
        if (hitStopLabel != null) hitStopLabel.text = GameSettings.HitStopEnabled ? Loc.T("options.hitstop.on") : Loc.T("options.hitstop.off");
        if (quitLabel != null) quitLabel.text = confirmQuit ? Loc.T("ui.quit_confirm") : Loc.T("ui.quit");
    }

    static void Show(TMP_Text t, float v)
    {
        if (t != null) t.text = $"{Mathf.RoundToInt(v * 100f)}%";
    }
}
