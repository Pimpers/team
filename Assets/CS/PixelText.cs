using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// Keeps Galmuri11 on its 12-dot pixel grid. Screen UI uses 12 / 24 / 36 / 48 / 60 px;
// auto-sized text takes the largest 12-pixel step that fits. Card faces keep their own sizing.
// Event-driven: texts are checked when TMP reports them changed (TEXT_CHANGED), not by searching on a timer.
public class PixelText : MonoBehaviour
{
    // Galmuri11 is a 12-dot Korean pixel font. Keep screen sizes on 12-pixel steps.
    public const float Dot = 12f;

    class Auto { public TMP_Text text; public float min, max; public string last; }
    static readonly HashSet<TMP_Text> seen = new HashSet<TMP_Text>();
    static readonly List<Auto> autos = new List<Auto>();
    static readonly List<Transform> ignored = new List<Transform>();

    // UI that sizes its own text (the draft's cards: auto-sized again with every deal) - registered from its Awake.
    public static void Ignore(Transform root) { if (root != null && !ignored.Contains(root)) ignored.Add(root); }

    static bool IsIgnored(Transform t)
    {
        for (int i = ignored.Count - 1; i >= 0; i--)
        {
            if (ignored[i] == null) { ignored.RemoveAt(i); continue; }
            if (t.IsChildOf(ignored[i])) return true;
        }
        return false;
    }
    // Texts TMP reported as changed since the last frame (TMPro's TEXT_CHANGED event): new texts, and auto-sized
    // ones whose words changed. Handled in LateUpdate - nothing is searched for on a timer.
    static readonly HashSet<TMP_Text> dirty = new HashSet<TMP_Text>();
    static readonly Dictionary<TMP_Text, Auto> autoOf = new Dictionary<TMP_Text, Auto>();
    static bool fitting; // our own size changes raise the event too: ignore those

    // 큰 글씨 (GameSettings.LargeText): reading text one grid step up, 24 -> 36, wherever it still fits its box -
    // the HUD, the Tab sheet, menus and dialogue alike, decided here once instead of screen by screen. A text
    // raised to 36 is checked again whenever its words change and goes back to 24 if the new words don't fit.
    const float Small = 24f, Large = 36f;
    static readonly HashSet<TMP_Text> raised = new HashSet<TMP_Text>();

    static void TryRaise(TMP_Text t)
    {
        if (!GameSettings.LargeText) return;
        if (!Mathf.Approximately(t.fontSize, Small) && !(raised.Contains(t) && Mathf.Approximately(t.fontSize, Large))) return;
        var rect = t.rectTransform.rect;
        if (rect.width < 1f || rect.height < 1f) return; // a box that grows with its text: nothing to check against
        t.fontSize = Large;
        Vector2 need = t.GetPreferredValues(t.text, t.textWrappingMode == TextWrappingModes.NoWrap ? 0f : rect.width, 0f);
        bool fits = need.x <= rect.width + 2f && need.y <= rect.height + 2f;
        t.fontSize = fits ? Large : Small;
        if (fits) raised.Add(t); else raised.Remove(t);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        var go = new GameObject("PixelText");
        DontDestroyOnLoad(go);
        go.AddComponent<PixelText>();
        Scan(true);
    }

    static void OnLoaded(Scene scene, LoadSceneMode mode) { seen.Clear(); autos.Clear(); autoOf.Clear(); dirty.Clear(); raised.Clear(); Scan(true); }

    static void OnTextChanged(Object obj)
    {
        if (!fitting && obj is TextMeshProUGUI t) dirty.Add(t);
    }

    void LateUpdate()
    {
        if (dirty.Count == 0) return;
        var batch = new List<TMP_Text>(dirty);
        dirty.Clear();
        foreach (var t in batch)
        {
            if (t == null) continue;
            if (seen.Add(t)) Consider((TextMeshProUGUI)t);
            else if (autoOf.TryGetValue(t, out var a) && t.isActiveAndEnabled && t.text != a.last) Fit(a);
            else if (raised.Contains(t)) { fitting = true; t.fontSize = Small; TryRaise(t); fitting = false; } // new words: fit again
        }
        for (int i = autos.Count - 1; i >= 0; i--) if (autos[i].text == null) { autoOf.Remove(autos[i].text); autos.RemoveAt(i); }
    }

    // Everything in the freshly loaded scene, active or not.
    static void Scan(bool includeInactive)
    {
        foreach (var t in FindObjectsByType<TextMeshProUGUI>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude))
            if (seen.Add(t)) Consider(t);
    }

    static void Consider(TextMeshProUGUI t)
    {
        if (!OnScreenPixelText(t) || IsIgnored(t.transform)) return;
        fitting = true;
        if (t.enableAutoSizing)
        {
            var a = new Auto { text = t, min = t.fontSizeMin, max = t.fontSizeMax, last = null };
            autos.Add(a);
            autoOf[t] = a;
            if (t.isActiveAndEnabled) Fit(a);
            else { t.enableAutoSizing = false; t.fontSize = Mathf.Max(Dot, Mathf.Floor(a.max / Dot + 0.001f) * Dot); }
        }
        else { t.fontSize = Mathf.Max(Dot, Mathf.Round(t.fontSize / Dot) * Dot); TryRaise(t); }
        fitting = false;
    }

    // Lets TMP fit the text in its original range, then rounds that down to the pixel grid.
    static void Fit(Auto a)
    {
        bool outer = !fitting;
        fitting = true;
        try { FitInner(a); } finally { if (outer) fitting = false; }
    }

    static void FitInner(Auto a)
    {
        var t = a.text;
        a.last = t.text;
        t.enableAutoSizing = true;
        t.fontSizeMin = a.min;
        t.fontSizeMax = GameSettings.LargeText && a.max <= Small + 4f ? Large : a.max; // 큰 글씨: room to grow to 36
        t.ForceMeshUpdate();
        float fitted = t.fontSize;
        t.enableAutoSizing = false;
        t.fontSize = Mathf.Max(Dot, Mathf.Floor(fitted / Dot + 0.001f) * Dot);
    }

    public static bool IsPixelFont(TMP_FontAsset f) =>
        f != null && (f.name.StartsWith("Galmuri11") || f.name.StartsWith("DepartureMono") || f.name.StartsWith("DreamHeumul"));

    // Screen UI in the pixel fonts only: world-space text (the name over the cat, the lobby's labels) is sized in
    // world units and left alone.
    static bool OnScreenPixelText(TMP_Text t)
    {
        if (t == null || !IsPixelFont(t.font) || t.fontSize < 8f) return false;
        var canvas = t.canvas != null ? t.canvas.rootCanvas : t.GetComponentInParent<Canvas>(true);
        return canvas != null && canvas.renderMode != RenderMode.WorldSpace;
    }
}
