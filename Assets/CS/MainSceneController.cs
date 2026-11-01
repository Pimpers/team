using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// MainScene (harbor village): the hub between the lobby and the stages. The mercenary
// picked in the lobby idles in the village square with its name above its head, and the
// top-left profile shows its face + name. The hammer button under the profile opens the
// blacksmith; the "출항하기!" button - or clicking the big ship at the pier - sets sail.
public class MainSceneController : MonoBehaviour
{
    [Tooltip("Same order as the lobby: 0 = Meow Knight, 1 = Meowolas.")]
    public CharacterDefinition[] characters;
    public SpriteRenderer catVisual;
    public TMP_Text nameTag;

    [Header("Top-left profile")]
    public Image portrait;
    public TMP_Text profileName;

    [Header("Buttons")]
    public Button departButton;
    public Button tavernButton;
    public Button blacksmithButton;
    public BlacksmithUI blacksmith;
    public Button settingsButton;
    public OptionsUI options;

    [Tooltip("World-space area of the big ship - clicking it also sets sail.")]
    public Rect shipArea;

    [Tooltip("Where setting sail goes: the CAT ISLAND world map.")]
    public string stageSceneName = "WorldMapScene";
    public string lobbySceneName = "LobbyScene";

    CharacterDefinition def;
    int frame;
    float timer;
    Camera cam;
    TMP_Text goldText;
    int shownGold = -1;

    void Start()
    {
        cam = Camera.main;
        BrightenIconLabels();
        int index = Mathf.Clamp(CharacterSelection.SelectedIndex, 0, characters.Length - 1);
        def = characters[index];
        string name = CharacterSelection.SelectedName;
        if (nameTag != null) nameTag.text = name;
        if (profileName != null) { profileName.text = name; FitNamePill(profileName); }
        if (portrait != null) portrait.sprite = def.idleFrames[0];
        ShowFrame(def.idleFrames[0]);

        departButton.onClick.AddListener(Depart);
        if (tavernButton != null) tavernButton.onClick.AddListener(() => SceneLoader.Load(lobbySceneName));
        if (blacksmithButton != null) blacksmithButton.onClick.AddListener(() => blacksmith.Open());
        if (settingsButton != null && options != null) settingsButton.onClick.AddListener(() => options.Open());
        CreateGoldDisplay();
        if (profileName != null && profileName.canvas != null)
            VillageGoals.Create(profileName.canvas.rootCanvas, profileName.font, def.ranged); // what the next runs open
        SetUpPadNavigation();
    }

    // Gamepad: the four icons (대장간 / 라운지 / 설정 / 출항하기) are UI buttons, so the d-pad moves between them
    // (automatic navigation) and A presses the selected one; a warm tint shows which. Update keeps something
    // selected - in the open popup while one is up, else the icons (출항하기 first).
    void SetUpPadNavigation()
    {
        foreach (var b in new[] { blacksmithButton, tavernButton, settingsButton, departButton })
        {
            if (b == null) continue;
            var nav = b.navigation; nav.mode = Navigation.Mode.Automatic; b.navigation = nav;
            var colors = b.colors; colors.selectedColor = new Color(1f, 0.86f, 0.45f); b.colors = colors;
        }
    }

    void KeepPadSelection()
    {
        if (KeyBindPanel.IsOpen) return; // it keeps its own selection
        if (blacksmith != null && blacksmith.panel != null && blacksmith.panel.activeInHierarchy) GameInput.KeepPadSelection(blacksmith.panel.transform);
        else if (blacksmith != null && blacksmith.IsOpen) return; // the blacksmith is talking: A moves the line on
        else if (options != null && options.IsOpen) GameInput.KeepPadSelection(options.panel.transform);
        else if (departButton != null) GameInput.KeepPadSelection(departButton.transform.parent, departButton);
    }

    // Saved gold (golden fish bones) on a dark pill just left of the settings gear, styled
    // like the profile's name pill (same sprite and font, reused from it).
    void CreateGoldDisplay()
    {
        if (settingsButton == null) return;
        var gear = (RectTransform)settingsButton.transform;

        var go = new GameObject("GoldDisplay", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(gear.parent, false);
        rt.SetSiblingIndex(gear.GetSiblingIndex() + 1); // popups created later still cover it
        rt.anchorMin = rt.anchorMax = gear.anchorMin;
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(gear.anchoredPosition.x - gear.sizeDelta.x * 0.5f - 14f, gear.anchoredPosition.y);
        rt.sizeDelta = new Vector2(240f, 72f);

        var bg = go.AddComponent<Image>();
        var pill = profileName != null ? profileName.transform.parent.GetComponent<Image>() : null;
        if (pill != null && pill.sprite != null)
        {
            bg.sprite = pill.sprite;
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = pill.pixelsPerUnitMultiplier;
        }
        else bg.color = new Color(0.07f, 0.05f, 0.08f, 0.85f);
        bg.raycastTarget = false;

        var textGo = new GameObject("Gold", typeof(RectTransform));
        var trt = (RectTransform)textGo.transform;
        trt.SetParent(rt, false);
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(18f, 4f);
        trt.offsetMax = new Vector2(-18f, -4f);
        goldText = textGo.AddComponent<TextMeshProUGUI>();
        if (profileName != null) goldText.font = profileName.font;
        goldText.fontSize = UISkin.FontHeading;
        goldText.fontStyle = FontStyles.Bold;
        goldText.color = UISkin.OverlayText;
        goldText.alignment = TextAlignmentOptions.Center;
        goldText.textWrappingMode = TextWrappingModes.NoWrap;
        goldText.enableAutoSizing = true;
        goldText.fontSizeMin = 22f;
        goldText.fontSizeMax = 40f;
        goldText.raycastTarget = false;
        RefreshGold();
    }

    // Follows the wallet (e.g. after buying at the blacksmith).
    void RefreshGold()
    {
        if (goldText == null || MetaProgress.Gold == shownGold) return;
        shownGold = MetaProgress.Gold;
        goldText.text = Currency.Amount(shownGold);
    }

    void Update()
    {
        RefreshGold();
        KeepPadSelection();

        // Idle animation
        timer += Time.deltaTime;
        if (timer >= 1f / def.framesPerSecond)
        {
            timer = 0f;
            frame = (frame + 1) % def.idleFrames.Length;
            ShowFrame(def.idleFrames[frame]);
        }

        // Clicking the big ship sets sail too.
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame || cam == null) return;
        if (blacksmith != null && blacksmith.IsOpen) return;
        if (options != null && options.IsOpen) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
        if (shipArea.Contains(world)) Depart();
    }

    void Depart()
    {
        if (blacksmith != null && blacksmith.IsOpen) return;
        SceneLoader.Load(stageSceneName);
    }

    // Body-aligned like everywhere else; the cat object sits at the feet.
    void ShowFrame(Sprite f)
    {
        catVisual.sprite = f;
        float s = catVisual.transform.localScale.x;
        if (def.TryGetVisualOffset(f, catVisual.flipX, s, out Vector2 aligned))
            catVisual.transform.localPosition = new Vector3(aligned.x, aligned.y + def.alignReferenceSize.y * 0.5f * s, 0f);
    }

    // The profile pill was a fixed 370 px: a short name sat in a long empty bar. It now ends a little after the name,
    // growing to the right from where it starts (its left part is under the portrait).
    static void FitNamePill(TMP_Text name)
    {
        var pill = name.transform.parent as RectTransform;
        if (pill == null || pill.name != "NamePill") return;
        float left = pill.anchoredPosition.x - pill.sizeDelta.x * pill.pivot.x;
        float textOffset = pill.sizeDelta.x - name.rectTransform.rect.width; // the part of the pill the text doesn't use
        float width = Mathf.Clamp(name.GetPreferredValues(name.text).x + textOffset + 40f, 220f, pill.sizeDelta.x);
        pill.sizeDelta = new Vector2(width, pill.sizeDelta.y);
        pill.anchoredPosition = new Vector2(left + width * pill.pivot.x, pill.anchoredPosition.y);
    }

    // The small plates under the icons (대장간 / 라운지 / 설정 / 출항하기): gold on a brown pill was hard to read
    // over the busy village art - cream text with a dark outline instead. 출항하기 is the screen's main way on, so
    // its plate is the green "go" button (UiTheme: one colour rule for buttons).
    void BrightenIconLabels()
    {
        foreach (var plate in FindObjectsByType<Image>(FindObjectsInactive.Include))
        {
            if (plate.name != "LabelPlate") continue;
            var skin = UISkin.Instance;
            if (skin != null && plate.transform.parent != null && plate.transform.parent.name == "DepartButton")
                UISkin.Apply(plate, skin.buttonRed, 3f);
            var prt = plate.rectTransform; prt.sizeDelta = new Vector2(Mathf.Max(prt.sizeDelta.x, 170f), Mathf.Max(prt.sizeDelta.y, 52f));
            foreach (var t in plate.GetComponentsInChildren<TMP_Text>(true))
            {
                t.color = UISkin.OverlayText;
                t.enableAutoSizing = false;
                t.fontSize = Mathf.Max(t.fontSize, 34f); // 28 read as a smudge at 1080p
                t.outlineWidth = 0.18f;
                t.outlineColor = new Color32(40, 24, 12, 255);
            }
        }
    }
}
