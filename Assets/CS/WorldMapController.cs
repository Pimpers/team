using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// WorldMapScene: the CAT ISLAND map shown after "출항" in the harbor village. Only the
// Ancient Forest (고대 숲, chapter 1) is open: a pulsing gold ring marks it, and clicking
// inside it sets sail to Stage 1. Clicking anywhere else on the map just says the region
// can't be reached yet. "< 항구" goes back to the village.
public class WorldMapController : MonoBehaviour
{
    [Header("Ancient Forest hotspot (world space ellipse)")]
    public Vector2 forestCenter;
    public Vector2 forestRadius = new Vector2(1.5f, 0.95f);
    public SpriteRenderer ring;
    public SpriteRenderer glow;
    public Transform marker;

    [Header("UI")]
    public TMP_Text messageText;
    public Button backButton;

    public string stageSceneName = "Stage1";
    public string villageSceneName = "MainScene";

    static readonly Color RingColor = new Color(1f, 0.82f, 0.4f, 1f);

    Camera cam;
    Vector3 ringScale;
    Vector3 markerPos;
    float messageTimer;
    bool leaving;

    [Tooltip("World size of the map picture; the camera zooms out so all of it fits on any screen.")]
    public Vector2 mapSize = new Vector2(19.2f, 8.85f);

    void Start()
    {
        cam = Camera.main;
        // Fit the whole (very wide) map: by width on 16:9 and narrower screens, else by height.
        if (cam != null && cam.orthographic)
            cam.orthographicSize = Mathf.Max(mapSize.y * 0.5f, mapSize.x * 0.5f / cam.aspect);
        if (ring != null) ringScale = ring.transform.localScale;
        if (marker != null) markerPos = marker.localPosition;
        if (messageText != null) messageText.gameObject.SetActive(false);
        if (backButton != null) backButton.onClick.AddListener(() => SceneLoader.Load(villageSceneName));
        // The builder's 30 snaps down to 24 on the pixel grid - smaller than the lobby's "< 뒤로" (36). Match it.
        var backLabel = backButton != null ? backButton.GetComponentInChildren<TMP_Text>(true) : null;
        if (backLabel != null) { backLabel.enableAutoSizing = false; backLabel.fontSize = 36f; }
        // The map art's region names are English: cover them with Korean plates in the CHAPTER tag's style.
        var tagPlate = marker != null ? marker.GetComponentInChildren<SpriteRenderer>(true) : null;
        var tagText = marker != null ? marker.GetComponentInChildren<TMP_Text>(true) : null;
        WorldMapLabels.Build(mapSize, tagPlate, tagText != null ? tagText.font : null);
        // After the first clear: pick the 타락도 (difficulty) of the next run.
        var uiCanvas = backButton != null ? backButton.GetComponentInParent<Canvas>() : null; // messageText is hidden by now
        if (uiCanvas != null) CorruptionSelector.Create(uiCanvas.rootCanvas, messageText != null ? messageText.font : null);
        if (uiCanvas != null && TutorialProgress.Done) AddTutorialButton(uiCanvas.rootCanvas, messageText != null ? messageText.font : null);
    }

    void Update()
    {
        var mouse = Mouse.current;
        Vector2 world = (mouse != null && cam != null) ? (Vector2)cam.ScreenToWorldPoint(mouse.position.ReadValue()) : Vector2.zero;
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        bool hover = !overUI && InsideForest(world);

        // Pulse: the ring breathes, glows brighter and grows a little under the mouse.
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
        if (ring != null)
        {
            float s = (hover ? 1.08f : 1f) + pulse * 0.05f;
            ring.transform.localScale = ringScale * s;
            ring.color = new Color(RingColor.r, RingColor.g, RingColor.b, hover ? 1f : 0.55f + pulse * 0.45f);
        }
        if (glow != null) glow.color = new Color(1f, 0.85f, 0.45f, (hover ? 0.32f : 0.12f) + pulse * 0.1f);
        if (marker != null) marker.localPosition = markerPos + Vector3.up * Mathf.Sin(Time.time * 3f) * 0.08f;

        if (messageTimer > 0f)
        {
            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0f && messageText != null) messageText.gameObject.SetActive(false);
        }

        if (leaving || mouse == null || !mouse.leftButton.wasPressedThisFrame || overUI) return;
        if (InsideForest(world))
        {
            leaving = true;
            // First voyage: the tutorial first (it starts the real run when finished or skipped).
            SceneLoader.Load(TutorialProgress.Done ? stageSceneName : TutorialProgress.SceneName);
        }
        else ShowMessage(Loc.T("worldmap.locked"));
    }

    bool InsideForest(Vector2 p)
    {
        Vector2 d = p - forestCenter;
        float nx = d.x / forestRadius.x, ny = d.y / forestRadius.y;
        return nx * nx + ny * ny <= 1f;
    }

    // Bottom-left "튜토리얼" button to replay it (only after it has been done once - before
    // that, setting sail goes there anyway).
    void AddTutorialButton(Canvas canvas, TMP_FontAsset font)
    {
        var go = new GameObject("TutorialButton", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(30f, 30f);
        rt.sizeDelta = new Vector2(260f, 80f);
        var skin = UISkin.Instance;
        UISkin.Apply(go.GetComponent<Image>(), skin != null ? skin.buttonWood : null, UISkin.SmallScale);
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(() => { leaving = true; SceneLoader.Load(TutorialProgress.SceneName); });
        go.AddComponent<ButtonSound>();
        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var lrt = (RectTransform)labelGo.transform;
        lrt.SetParent(rt, false);
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var t = labelGo.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = Loc.T("hud.tutorial_name");
        t.fontSize = UiTheme.TextBody;
        t.color = Color.white;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
    }

    void ShowMessage(string text)
    {
        if (messageText == null) return;
        messageText.text = text;
        messageText.gameObject.SetActive(true);
        messageTimer = 1.8f;
    }
}
