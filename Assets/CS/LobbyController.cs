using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Character select screen (LobbyScene). The cats wander around the tavern; clicking one
// zooms the camera in on it and opens the naming panel. "출정하기" saves the pick + name
// (CharacterSelection) and loads Stage 1 playing as that cat; "다시 고르기" zooms back out.
// Button clicks are hooked up here in code rather than via Inspector OnClick entries.
public class LobbyController : MonoBehaviour
{
    public LobbyCharacterDisplay[] displays;

    [Header("Overview UI (hidden while zoomed)")]
    public GameObject overviewGroup;

    [Header("Naming panel")]
    public GameObject namingPanel;
    public TMP_Text classText;
    public TMP_Text speciesText;
    public Image roleBadge;
    public TMP_Text roleText;
    public TMP_Text descriptionText;
    public TMP_InputField nameInput;
    public Button confirmButton;
    public Button cancelButton;
    public Button backButton;

    [Header("Camera")]
    public float overviewSize = 4.705f;
    public Vector2 overviewCenter = Vector2.zero;
    public float zoomSize = 1.75f;
    [Tooltip("Where the cat sits in the zoomed view: pushed left so the panel on the right doesn't cover it.")]
    public Vector2 zoomFocusOffset = new Vector2(1.8f, 0.75f);
    [Tooltip("Half size of the background picture, to keep the zoomed camera inside it.")]
    public Vector2 backgroundHalfSize = new Vector2(8.36f, 4.705f);
    public float zoomSpeed = 6f;

    [Tooltip("Where the lobby goes after naming: the harbor village hub.")]
    public string stageSceneName = "MainScene";
    public string titleSceneName = "TitleScene";


    [Tooltip("Small line under the name box: shows the length hint, or the warning when the name is empty.")]
    public TMP_Text nameHint;

    static readonly Color HintColor = UISkin.InkSoft;
    static readonly Color WarningColor = UISkin.InkWarning;

    int selected = -1;
    int padHover = -1; // gamepad: the cat the d-pad points at in the overview (A picks it)
    Camera cam;
    float warningShake;

    void Awake()
    {
        cam = Camera.main;
        confirmButton.onClick.AddListener(Confirm);
        cancelButton.onClick.AddListener(Cancel);
        if (backButton != null) backButton.onClick.AddListener(() => SceneManager.LoadScene(titleSceneName));
        if (nameHint == null)
        {
            var t = namingPanel.transform.Find("Inner/NameHint");
            if (t != null) nameHint = t.GetComponent<TMP_Text>();
        }

        // Korean IME + "select all on focus" + rich text makes TMP_InputField index into
        // stale character info and throw ArgumentOutOfRangeException while typing. Plain
        // text, no auto select-all, and the caret parked at the end avoids that path.
        nameInput.characterLimit = CharacterSelection.MaxNameLength;
        nameInput.onFocusSelectAll = false;
        nameInput.richText = false;
        nameInput.isRichTextEditingAllowed = true;
        if (nameInput.textComponent != null) nameInput.textComponent.richText = false;
        nameInput.onSubmit.AddListener(_ => Confirm());
        nameInput.onValueChanged.AddListener(_ => ShowHint(false));
        // The caret blinks in the middle of the centred placeholder ("이름|입력"): hide the placeholder while typing.
        if (nameInput.placeholder != null)
        {
            var ph = nameInput.placeholder;
            Color shown = ph.color;
            nameInput.onSelect.AddListener(_ => ph.color = new Color(shown.r, shown.g, shown.b, 0f));
            nameInput.onDeselect.AddListener(_ => ph.color = shown);
        }
        namingPanel.SetActive(false);
    }

    void ShowHint(bool warning)
    {
        if (nameHint == null) return;
        nameHint.text = warning ? Loc.T("lobby.name_prompt") : Loc.F("lobby.name_max", CharacterSelection.MaxNameLength);
        nameHint.color = warning ? WarningColor : HintColor;
        nameHint.fontSize = warning ? 30f : 24f;
        if (warning) warningShake = 0.35f;
    }

    void LateUpdate()
    {
        // Little horizontal shake on the warning so it's noticed.
        if (nameHint == null) return;
        var rt = nameHint.rectTransform;
        Vector2 p = rt.anchoredPosition;
        if (warningShake > 0f)
        {
            warningShake -= Time.deltaTime;
            p.x = Mathf.Sin(warningShake * 60f) * 10f * (warningShake / 0.35f);
        }
        else p.x = 0f;
        rt.anchoredPosition = p;
    }

    System.Collections.IEnumerator FocusNameInput()
    {
        // Activate a frame after the text was set, so TMP has rebuilt its character info.
        yield return null;
        nameInput.ActivateInputField();
        yield return null;
        nameInput.MoveTextEnd(false);
    }

    System.Collections.IEnumerator SelectConfirmNextFrame()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        yield return null;
        yield return null;
        GameInput.SelectForPad(confirmButton);
    }

    void Update()
    {
        UpdateCamera();

        if (selected >= 0) { PadPanel(); return; } // panel is open - only its buttons are clickable
        if (PadOverview()) return;
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (cam == null) return;

        Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
        for (int i = 0; i < displays.Length; i++)
            if (displays[i].Contains(world)) { PressFeedback.World(world); Select(i); break; }
    }

    // Gamepad in the overview: left / right walks along the cats (the one pointed at strikes its pose), A picks it,
    // B goes back to the title. True when the pad did something this frame.
    bool PadOverview()
    {
        int dx = GameInput.PadNavX;
        if (dx != 0 && displays.Length > 0)
        {
            if (padHover >= 0) displays[padHover].SetSelected(false);
            padHover = padHover < 0 ? (dx > 0 ? 0 : displays.Length - 1) : (padHover + dx + displays.Length) % displays.Length;
            displays[padHover].SetSelected(true);
            Sfx.Play("ui_click", 0.4f);
            return true;
        }
        if (GameInput.PadSubmitPressed)
        {
            int pick = padHover >= 0 ? padHover : 0;
            if (padHover >= 0) displays[padHover].SetSelected(false);
            padHover = -1;
            Select(pick);
            return true;
        }
        if (GameInput.PadBackPressed) { SceneManager.LoadScene(titleSceneName); return true; }
        return false;
    }

    // Gamepad on the naming panel: no on-screen keyboard, so an empty name becomes the cat's own name (still editable
    // from the keyboard); the d-pad moves between the name box and the buttons, B goes back to the cats.
    void PadPanel()
    {
        if (GameInput.PadBackPressed) { Cancel(); return; }
        if (!GameInput.UsingGamepad) return;
        if (string.IsNullOrWhiteSpace(nameInput.text) && !nameInput.isFocused)
            nameInput.text = displays[selected].character.displayName;
        GameInput.KeepPadSelection(namingPanel.transform, confirmButton);
    }

    void UpdateCamera()
    {
        if (cam == null) return;
        float size;
        Vector2 center;
        if (selected >= 0)
        {
            size = zoomSize;
            center = (Vector2)displays[selected].transform.position + zoomFocusOffset;
            // Keep the zoomed view inside the background picture.
            float halfH = size, halfW = size * cam.aspect;
            center.x = Mathf.Clamp(center.x, -backgroundHalfSize.x + halfW, backgroundHalfSize.x - halfW);
            center.y = Mathf.Clamp(center.y, -backgroundHalfSize.y + halfH, backgroundHalfSize.y - halfH);
        }
        else
        {
            size = overviewSize;
            center = overviewCenter;
        }

        float t = 1f - Mathf.Exp(-zoomSpeed * Time.deltaTime);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, size, t);
        Vector3 p = cam.transform.position;
        cam.transform.position = new Vector3(Mathf.Lerp(p.x, center.x, t), Mathf.Lerp(p.y, center.y, t), p.z);
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    public void PickForPlaytest(int index) => Select(index); // (screen captures: the naming panel)
#endif

    void Select(int index)
    {
        selected = index;
        var def = displays[index].character;
        for (int i = 0; i < displays.Length; i++)
        {
            displays[i].SetSelected(i == index);
            displays[i].ShowNameTag(false);
        }

        if (overviewGroup != null) overviewGroup.SetActive(false);
        namingPanel.SetActive(true);
        classText.text = string.IsNullOrEmpty(def.tagName) ? def.jobName : def.tagName;
        if (speciesText != null) speciesText.text = def.displayName;
        if (roleBadge != null) roleBadge.color = def.roleColor;
        roleText.text = Loc.F("lobby.role_group", def.roleGroup);
        // What actually differs in play, under the flavour text: it was only found out by playing (the knight's
        // extra hearts and parry showed nowhere).
        // (In place of def.role, "근접 · 검으로 베기": the badge above already says it, and a fourth line ran over the badge.)
        if (descriptionText != null) descriptionText.text = $"{def.description}\n<size=95%><color=#8a3f00>{Loc.T(def.ranged ? "lobby.traits.archer" : "lobby.traits.knight")}</color></size>";
        // The name given last time is already in the box: typed again only to change it (it started empty every visit).
        nameInput.text = CharacterSelection.GetName(index);
        ShowHint(false);
        if (GameInput.UsingGamepad) StartCoroutine(SelectConfirmNextFrame()); // the A that picked the cat must not press 출정
        else StartCoroutine(FocusNameInput());
    }

    void Cancel()
    {
        if (selected < 0) return;
        displays[selected].SetSelected(false);
        selected = -1;
        // (the floating name tags stay off: the cats are told apart by clicking them, not by plates over their heads)
        namingPanel.SetActive(false);
        if (overviewGroup != null) overviewGroup.SetActive(true);
    }

    void Confirm()
    {
        if (selected < 0) return;
        if (string.IsNullOrWhiteSpace(nameInput.text) || System.Linq.Enumerable.All(nameInput.text.Trim(), char.IsDigit)) // no blank or digits-only names ("111")
        {
            ShowHint(true);
            StartCoroutine(FocusNameInput());
            return;
        }
        CharacterSelection.SelectedIndex = selected;
        CharacterSelection.SetName(selected, nameInput.text);
        SceneLoader.Load(stageSceneName);
    }
}
