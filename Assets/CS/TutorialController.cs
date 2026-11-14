using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The tutorial stage (Tutorial.unity, built by StageSceneBuilder). Walks a first-time player
// through the controls one at a time - a prompt at the top of the screen, and the next one
// only once they've actually done it:
//   move -> jump a ledge -> double jump a wall -> drop through a platform (S) -> dash ->
//   3-hit combo on a dummy -> jump attack -> a real fight with two rats.
// What comes later in a run (sets, the route fork, Q/E/R, the Tab sheet, corruption) is no longer read out here in
// a row: each tip shows once when its moment first comes (OneTimeTip, CardDraftUI.ShowSetTip).
// The terrain forces the movement steps (the wall needs the double jump, the platform walk
// ends in a wall so S is the only way on). The player can't die here (PlayerStats.Immortal).
// Finishing or skipping marks it done (TutorialProgress) and starts the real run.
public class TutorialController : MonoBehaviour
{
    [Header("Goals (world x / heights) - set by StageSceneBuilder from the layout")]
    public float moveGoalX = 14f;
    public float jumpGoalX = 26f;
    public float wallGoalX = 33f;
    public float wallTopY = 8f;
    public float dropGoalX = 56f;

    public string nextSceneName = "Stage1";

    PlayerController2D player;
    TMP_Text promptText, counterText;
    RectTransform promptRect, canvasRect;
    int step;
    float doneTimer = -1f;   // > 0: showing "좋아요!" before the next step
    bool plunged, finished;
    readonly List<Enemy> dummies = new List<Enemy>();
    readonly List<Enemy> rats = new List<Enemy>();

    // Built on first use, not as a static initializer: that can run inside Unity's object construction,
    // where the text table (Resources) can't be loaded yet.
    static string[] prompts;
    static string[] Prompts => prompts ??= new[]
    {
        Loc.T("tutorial.step.move"),
        Loc.T("tutorial.step.jump"),
        Loc.T("tutorial.step.double_jump"),
        Loc.T("tutorial.step.drop"),
        Loc.T("tutorial.step.dash"),
        Loc.T("tutorial.step.attack"),
        Loc.T("tutorial.step.slam"),
        Loc.T("tutorial.step.fight"),
    };

    void Awake()
    {
        PlayerStats.Immortal = true;
    }

    void OnDestroy()
    {
        PlayerStats.Immortal = false;
        if (player != null) player.PlungeLanded -= OnPlunge;
    }

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.GetComponent<PlayerController2D>();
        if (player != null) player.PlungeLanded += OnPlunge;

        foreach (var e in FindObjectsByType<Enemy>())
            (e.definition != null && e.definition.name == "Dummy" ? dummies : rats).Add(e);
        dummies.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

        var hud = RunManager.Instance != null ? RunManager.Instance.hudText : null;
        var canvas = hud != null && hud.canvas != null ? hud.canvas.rootCanvas : FindAnyObjectByType<Canvas>();
        BuildUI(canvas, hud != null ? hud.font : null);
        ShowStep();
    }

    void OnPlunge() { if (step == 6) plunged = true; }

    void Update()
    {
        FitPrompt();
        if (finished || player == null) return;

        if (doneTimer > 0f)
        {
            doneTimer -= Time.unscaledDeltaTime;
            if (doneTimer <= 0f) { step++; ShowStep(); }
            return;
        }
        if (Done(step)) Complete();
    }

    bool Done(int s)
    {
        Vector3 pos = player.transform.position;
        float feet = pos.y - player.feetOffset;
        switch (s)
        {
            case 0: return pos.x >= moveGoalX;
            case 1: return pos.x >= jumpGoalX;
            case 2: return pos.x >= wallGoalX && feet >= wallTopY - 0.2f;
            case 3: return pos.x >= dropGoalX;
            case 4: return player.IsDashInvulnerable;
            case 5: return dummies.Count == 0 || dummies[0] == null || dummies[0].IsDead;
            case 6: return plunged;
            case 7: return rats.TrueForAll(r => r == null || r.IsDead);
        }
        return false;
    }

    void Complete()
    {
        Sfx.Play("card_pick");
        if (step >= Prompts.Length - 1) { Finish(); return; }
        promptText.text = Loc.T("tutorial.good");
        doneTimer = 0.8f;
    }

    // The panel sits between the HUD (top-left) and the minimap with its legend (top-right): on a narrow screen (4:3)
    // the canvas is narrower in units, so the panel is shortened to stay clear of both.
    void FitPrompt()
    {
        if (promptRect == null || canvasRect == null) return;
        float width = Mathf.Clamp(canvasRect.rect.width - 2f * 600f, 520f, 960f);
        if (!Mathf.Approximately(promptRect.sizeDelta.x, width)) promptRect.sizeDelta = new Vector2(width, promptRect.sizeDelta.y);
    }

    void ShowStep()
    {
        promptText.text = Prompts[step];
        counterText.text = $"{step + 1} / {Prompts.Length}";
        plunged = false;
    }

#if UNITY_EDITOR
    // ScreenCaptureTool: shows step `s` without playing up to it.
    public void JumpToStepForCapture(int s) { step = Mathf.Clamp(s, 0, Prompts.Length - 1); doneTimer = -1f; ShowStep(); }
#endif

    void Finish()
    {
        finished = true;
        promptText.text = Loc.T("tutorial.done_rich");
        TutorialProgress.Done = true;
        var hud = RunManager.Instance != null ? RunManager.Instance.hudText : null;
        if (hud != null) StageBanner.Show(hud.canvas.rootCanvas, hud.font, Loc.T("tutorial.done"), Loc.T("tutorial.done_sub"), 1.4f);
        Invoke(nameof(Leave), 2.6f);
    }

    void Leave() => SceneLoader.Load(nextSceneName);

    void Skip()
    {
        if (finished) return;
        finished = true;
        TutorialProgress.Done = true;
        SceneLoader.Load(nextSceneName);
    }

    // ---------------------------------------------------------------- UI
    void BuildUI(Canvas canvas, TMP_FontAsset font)
    {
        if (canvas == null) return;
        var skin = UISkin.Instance;

        var panel = new GameObject("TutorialPrompt", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)panel.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -24f);
        rt.sizeDelta = new Vector2(960f, 120f); // clear of the HUD (top-left) and minimap (top-right)
        promptRect = rt;
        canvasRect = (RectTransform)canvas.transform;
        var img = panel.GetComponent<Image>();
        img.raycastTarget = false;
        UISkin.Apply(img, skin != null ? skin.panel : null, UISkin.LargeScale);

        promptText = Text(rt, "Prompt", 36f, UISkin.Ink, font, TextAlignmentOptions.Center);
        promptText.enableAutoSizing = true; // a narrower panel shrinks the text instead of clipping it
        promptText.fontSizeMax = 36f;
        promptText.fontSizeMin = 22f;
        promptText.textWrappingMode = TextWrappingModes.Normal; // a long step takes two lines rather than running out of the panel
        var prt = promptText.rectTransform;
        prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
        prt.offsetMin = new Vector2(140f, 10f); prt.offsetMax = new Vector2(-40f, -10f); // left: clear of the step counter

        counterText = Text(rt, "Counter", 26f, UISkin.Ink, font, TextAlignmentOptions.Left); // (InkSoft was faint on the cream panel)
        var crt = counterText.rectTransform;
        crt.anchorMin = new Vector2(0f, 0f); crt.anchorMax = new Vector2(0f, 1f); crt.pivot = new Vector2(0f, 0.5f);
        crt.anchoredPosition = new Vector2(34f, 0f); crt.sizeDelta = new Vector2(90f, 0f);

        // Skip, bottom-right (the minimap owns the top-right corner).
        var skipGo = new GameObject("SkipTutorial", typeof(RectTransform), typeof(Image));
        var srt = (RectTransform)skipGo.transform;
        srt.SetParent(canvas.transform, false);
        srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(1f, 0f);
        srt.anchoredPosition = new Vector2(-30f, 30f);
        srt.sizeDelta = new Vector2(360f, 90f);
        UISkin.Apply(skipGo.GetComponent<Image>(), skin != null ? skin.buttonWood : null, UISkin.SmallScale);
        var btn = skipGo.AddComponent<Button>();
        btn.onClick.AddListener(Skip);
        skipGo.AddComponent<ButtonSound>();
        var label = Text(srt, "Label", 34f, Color.white, font, TextAlignmentOptions.Center);
        label.text = Loc.T("tutorial.skip");
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
    }

    static TMP_Text Text(RectTransform parent, string name, float size, Color color, TMP_FontAsset font, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }
}

// Whether the tutorial has been finished or skipped.
public static class TutorialProgress
{
    public const string SceneName = "Tutorial";

    public static bool Done
    {
        get => GameSave.Data.tutorialDone;
        set { GameSave.Data.tutorialDone = value; GameSave.Save(); }
    }
}
