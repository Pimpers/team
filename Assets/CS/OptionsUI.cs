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

    // One slider / value per GameSettings.Sliders entry (the scene's own slider is the first, 전체 볼륨) and one
    // button per GameSettings.Toggles entry: the rows come from that shared list, as on the title's settings panel.
    Slider[] sliders;
    TMP_Text[] values;
    TMP_Text[] toggleLabels;
    TMP_Text quitLabel;
    Button quitButton, creditsButton;
    bool confirmQuit;

    public bool IsOpen => panel != null && panel.activeSelf;

    void Awake()
    {
        volumeSlider.minValue = 0f;
        volumeSlider.maxValue = 1f;
        sliders = new[] { volumeSlider };
        values = new[] { volumeValue };
        Hook(0);
        closeButton.onClick.AddListener(Close);
        BuildExtras();
        panel.SetActive(false);
    }

    void Hook(int i)
    {
        var s = GameSettings.Sliders[i];
        sliders[i].onValueChanged.AddListener(v => { s.set(v); Show(values[i], v); if (v > 0f) s.preview?.Invoke(); });
    }

    public void Open()
    {
        confirmQuit = false;
        panel.SetActive(true);
        for (int i = 0; i < sliders.Length; i++)
        {
            if (sliders[i] == null) continue;
            float v = GameSettings.Sliders[i].get();
            sliders[i].SetValueWithoutNotify(v);
            Show(values[i], v);
        }
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
        int n = GameSettings.Sliders.Length;
        System.Array.Resize(ref sliders, n);
        System.Array.Resize(ref values, n);
        for (int i = 1; i < n; i++)
        {
            sliders[i] = CloneRow(inner, labelT, valueT, volumeSlider, "Setting" + i, Loc.T(GameSettings.Sliders[i].labelKey), 266f - 128f * i, out values[i]);
            Hook(i);
        }

        // Buttons are clones of "닫기" so they carry its look and click sound.
        var wood = UISkin.Instance != null ? UISkin.Instance.buttonWood : null;
        toggleLabels = new TMP_Text[GameSettings.Toggles.Length];
        for (int i = 0; i < GameSettings.Toggles.Length; i++)
        {
            var toggle = GameSettings.Toggles[i];
            var b = CloneButton("Toggle" + i, new Vector2(-250f + 250f * i, -272f), new Vector2(240f, 80f), wood, out toggleLabels[i]);
            b.onClick.AddListener(() => { toggle.set(!toggle.get()); RefreshButtons(); });
        }
        // 키 설정 (KeyBindPanel), third in the toggles' row.
        var keys = CloneButton("KeysButton", new Vector2(250f, -272f), new Vector2(240f, 80f), wood, out var keysLabel);
        if (keysLabel != null) keysLabel.text = Loc.T("keys.title");
        keys.onClick.AddListener(() =>
        {
            var canvas = panel != null ? panel.GetComponentInParent<Canvas>() : null;
            KeyBindPanel.Show(canvas, keysLabel != null ? keysLabel.font : null);
        });
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
        if (toggleLabels != null)
            for (int i = 0; i < toggleLabels.Length; i++)
                if (toggleLabels[i] != null) toggleLabels[i].text = GameSettings.ToggleText(GameSettings.Toggles[i]);
        if (quitLabel != null) quitLabel.text = confirmQuit ? Loc.T("ui.quit_confirm") : Loc.T("ui.quit");
    }

    static void Show(TMP_Text t, float v)
    {
        if (t != null) t.text = $"{Mathf.RoundToInt(v * 100f)}%";
    }
}
