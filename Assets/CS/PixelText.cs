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
    // the HUD, the Tab sheet, menus and dialogue alike, decided here once instead of screen by screen.
    // Checked only when something can have changed - the words (TMP's text event), the box (RectWatch below,
    // OnRectTransformDimensionsChange) or the setting (ApplyLargeText) - never per frame, and the measurement is
    // skipped while the words and width are the ones last measured. Hysteresis: a text that had to go back to 24
    // stays there until its words change (a box that wobbles a pixel must not flick it 36 / 24 / 36).
    const float Small = 24f, Large = 36f;
    class LargeState { public bool up, lockedSmall, measured; public string words; public float width; }
    static readonly Dictionary<TMP_Text, LargeState> large = new Dictionary<TMP_Text, LargeState>();

    static void TryRaise(TMP_Text t)
    {
        if (!GameSettings.LargeText) return;
        if (!large.TryGetValue(t, out var st))
        {
            if (!Mathf.Approximately(t.fontSize, Small)) return; // only the 24 px reading text
            st = new LargeState();
            large[t] = st;
        }
        var rect = t.rectTransform.rect;
        if (rect.width < 1f || rect.height < 1f) return; // a box that grows with its text: nothing to check against
        string words = t.text;
        bool wordsChanged = !ReferenceEquals(words, st.words) && words != st.words;
        if (st.lockedSmall && !wordsChanged) return;                                          // hysteresis
        if (st.measured && !wordsChanged && Mathf.Approximately(rect.width, st.width)) return; // cached
        bool was = fitting;
        fitting = true;
        t.fontSize = Large;
        Vector2 need = t.GetPreferredValues(words, t.textWrappingMode == TextWrappingModes.NoWrap ? 0f : rect.width, 0f);
        bool fits = need.x <= rect.width + 2f && need.y <= rect.height + 2f;
        t.fontSize = fits ? Large : Small;
        fitting = was;
        st.words = words; st.width = rect.width; st.measured = true;
        st.up = fits; st.lockedSmall = !fits;
        if (fits && t.GetComponent<RectWatch>() == null) t.gameObject.AddComponent<RectWatch>().text = t;
    }

    // The setting was switched: every text seen so far goes up where it fits, or back to 24.
    public static void ApplyLargeText()
    {
        bool was = fitting;
        fitting = true;
        foreach (var kv in large) if (kv.Key != null && kv.Value.up) kv.Key.fontSize = Small;
        large.Clear();
        if (GameSettings.LargeText) foreach (var t in seen) if (t != null && autoOf.ContainsKey(t) == false) TryRaise(t);
        fitting = was;
    }

    // On a raised text: its box changed size, so it is measured again (and lowered if it no longer fits).
    class RectWatch : MonoBehaviour
    {
        public TMP_Text text;
        void OnRectTransformDimensionsChange() { if (text != null && large.TryGetValue(text, out var st) && st.up) TryRaise(text); }
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

    static void OnLoaded(Scene scene, LoadSceneMode mode) { seen.Clear(); autos.Clear(); autoOf.Clear(); dirty.Clear(); large.Clear(); Scan(true); }

    static void OnTextChanged(Object obj)
    {
        if (!fitting && obj is TextMeshProUGUI t) dirty.Add(t);
    }

    static readonly List<TMP_Text> batch = new List<TMP_Text>();

    void LateUpdate()
    {
        if (dirty.Count == 0) return;
        batch.Clear();
        batch.AddRange(dirty); // (one list for every frame: nothing allocated here)
        dirty.Clear();
        foreach (var t in batch)
        {
            if (t == null) continue;
            if (seen.Add(t)) Consider((TextMeshProUGUI)t);
            else if (autoOf.TryGetValue(t, out var a) && t.isActiveAndEnabled && t.text != a.last) Fit(a);
            else if (large.ContainsKey(t)) TryRaise(t); // new words: measured again (and freed from a hysteresis 24)
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
