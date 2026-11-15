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
    Button creditsButton;

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

        frame.sizeDelta = new Vector2(780f, 850f);
        inner.sizeDelta = new Vector2(768f, 838f);
        Move(inner.Find("Title"), 0f, 357f);
        Move(inner.Find("Divider"), 0f, 295f);

        // Master row (existing), then music, effects and screen shake cloned from it.
        // Rows 128 apart, each slider 58 under its label: the label of the next row no longer sits on the slider above.
        PlaceRow(labelT, valueT, volumeSlider.transform as RectTransform, 241f);
        int n = GameSettings.Sliders.Length;
        System.Array.Resize(ref sliders, n);
        System.Array.Resize(ref values, n);
        for (int i = 1; i < n; i++)
        {
            sliders[i] = CloneRow(inner, labelT, valueT, volumeSlider, "Setting" + i, Loc.T(GameSettings.Sliders[i].labelKey), 241f - 128f * i, out values[i]);
            Hook(i);
        }

        // Buttons are clones of "닫기" so they carry its look and click sound.
        var wood = UISkin.Instance != null ? UISkin.Instance.buttonWood : null;
        toggleLabels = new TMP_Text[GameSettings.Toggles.Length];
        for (int i = 0; i < GameSettings.Toggles.Length; i++)
        {
            var toggle = GameSettings.Toggles[i];
            var b = CloneButton("Toggle" + i, GridSlot(i), new Vector2(240f, 80f), wood, out toggleLabels[i]);
            b.onClick.AddListener(() => { toggle.set(!toggle.get()); RefreshButtons(); });
        }
        // (No 키 설정 or 게임 종료 here: the controls are in the pause menu, quitting is on the village's ESC menu.)
        creditsButton = CloneButton("CreditsButton", new Vector2(0f, BottomRow), new Vector2(230f, 80f), wood, out var creditsLabel);
        if (creditsLabel != null) creditsLabel.text = Loc.T("credits.title");
        creditsButton.onClick.AddListener(() =>
        {
            var canvas = panel != null ? panel.GetComponentInParent<Canvas>() : null;
            if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
            CreditsPanel.Show(canvas, creditsLabel != null ? creditsLabel.font : null);
        });
        // 닫기 becomes the X in the window's top-right corner (it was cloned above, so the other buttons keep its look).
        var close = (RectTransform)closeButton.transform;
        close.anchoredPosition = new Vector2(334f, 369f); // 14 in from the 768 x 838 panel's corner
        close.sizeDelta = new Vector2(72f, 72f);
        var closeLabel = closeButton.GetComponentInChildren<TMP_Text>();
        if (closeLabel != null) { closeLabel.text = "X"; closeLabel.enableAutoSizing = false; closeLabel.fontSize = UiTheme.TextLarge; }
        RefreshButtons();
    }

    // The toggles fill a grid of two per row under the sliders; the credits button below it.
    const float GridTop = -275f, GridRow = 90f, BottomRow = -365f;
    static Vector2 GridSlot(int i) => new Vector2(-125f + 250f * (i % 2), GridTop - GridRow * (i / 2));

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

    // 36 when the text fits the button, else 24 - the pixel font is sharp only at 12 / 24 / 36 (auto-size would pick
    // a blurry size in between). "히트스톱: 켜짐" at 36 ran out of the 240-wide button.
    static void FitLabel(TMP_Text label)
    {
        label.enableAutoSizing = false;
        label.fontSize = UiTheme.TextLarge;
        float room = ((RectTransform)label.transform.parent).rect.width - 24f;
        if (label.GetPreferredValues(label.text).x > room) label.fontSize = UiTheme.TextSmall;
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

    void RefreshButtons()
    {
        if (toggleLabels != null)
            for (int i = 0; i < toggleLabels.Length; i++)
                if (toggleLabels[i] != null)
                {
                    toggleLabels[i].text = GameSettings.ToggleText(GameSettings.Toggles[i]);
                    FitLabel(toggleLabels[i]);
                    var img = toggleLabels[i].transform.parent.GetComponent<Image>();
                    if (img != null) img.color = GameSettings.ToggleTint(GameSettings.Toggles[i]);
                }
    }

    static void Show(TMP_Text t, float v)
    {
        if (t != null) t.text = $"{Mathf.RoundToInt(v * 100f)}%";
    }
}
