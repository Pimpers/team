using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The forest's scene change between stages (instead of the black fade): a gust of leaves sweeps in from the
// right, a dark curtain of foliage closing behind them; the next stage loads behind it, then the wind carries the
// curtain on off to the left and the stage opens. Leaves are pxl art in Resources/StageExit; the closer to the
// boss, the redder they get. Lives across the scene load (DontDestroyOnLoad), runs on unscaled time.
public class LeafWipe : MonoBehaviour
{
    const float CoverTime = 0.55f, RevealTime = 0.6f, LeafScale = 9f; // leaf art pixels -> canvas units

    static LeafWipe instance;
    // True from the start of a cover until the next scene is open again.
    public static bool Busy => instance != null && instance.busy;
    // The last scene was entered through the leaves (StageExit then has the cat pop out of a log).
    public static bool ArrivedThroughLeaves { get; set; }
    // Raised when the curtain has blown away and the new stage is in view.
    public static event Action Revealed;

    RectTransform canvasRt, curtain;
    Image curtainImg;
    Sprite[] leaves;
    readonly List<Leaf> flying = new List<Leaf>();
    readonly List<RectTransform> edge = new List<RectTransform>();
    bool busy, covered;

    class Leaf { public RectTransform rt; public Vector2 v; public float spin; }

    // tone: "green", "gold", "red" - or several, comma separated ("green,gold").
    public static void Cover(string tone, Action then)
    {
        if (Busy) return;
        if (instance == null) Create();
        instance.StartCoroutine(instance.CoverRoutine(tone, then));
    }

    // Tone for the stage being entered: green and gold in the forest, turning red on the way to the boss.
    public static string ToneFor(string scene) =>
        scene == "Stage5" ? "red" : scene == "Stage4" ? "red,gold" : scene == "Stage3" ? "gold,green" : "green,gold";

    static void Create()
    {
        var go = new GameObject("LeafWipe", typeof(RectTransform));
        DontDestroyOnLoad(go);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31990; // over every scene's UI, under the black ScreenFader
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        instance = go.AddComponent<LeafWipe>();
        instance.canvasRt = (RectTransform)go.transform;

        var c = new GameObject("Curtain", typeof(RectTransform), typeof(Image));
        instance.curtain = (RectTransform)c.transform;
        instance.curtain.SetParent(go.transform, false);
        instance.curtainImg = c.GetComponent<Image>();
        instance.curtain.gameObject.SetActive(false);
        SceneManager.sceneLoaded += instance.OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (instance == this) { SceneManager.sceneLoaded -= OnSceneLoaded; instance = null; }
    }

    void LoadSprites(string tone)
    {
        var list = new List<Sprite>();
        foreach (var t in tone.Split(','))
            foreach (var s in new[] { "a", "b", "c" })
            {
                var sp = Resources.Load<Sprite>($"StageExit/leaf_{t.Trim()}_{s}");
                if (sp != null) list.Add(sp);
            }
        leaves = list.ToArray();
        // The curtain: the dark of the foliage the leaves are torn from (deep green; plum on the crimson side).
        curtainImg.color = tone.StartsWith("red") ? new Color32(0x2c, 0x12, 0x2a, 0xff) : new Color32(0x15, 0x3a, 0x34, 0xff);
    }

    float Width => canvasRt.rect.width;
    float Height => canvasRt.rect.height;

    // The curtain covers [left, right] of the screen (0..1); its leading edge is ragged with pinned leaves.
    void SetCurtain(float left, float right, float edgeAt)
    {
        curtain.anchorMin = new Vector2(left, 0f);
        curtain.anchorMax = new Vector2(right, 1f);
        curtain.offsetMin = curtain.offsetMax = Vector2.zero;
        for (int i = 0; i < edge.Count; i++)
        {
            var rt = edge[i];
            float wobble = Mathf.Sin(Time.unscaledTime * 9f + i * 1.7f) * 14f;
            // Two staggered rows of big leaves, poking out of the curtain by different amounts: a ragged edge.
            float poke = (i % 3) * 26f + (i % 2 == 0 ? -70f : 0f);
            rt.anchoredPosition = new Vector2(edgeAt * Width - Width * 0.5f + wobble - poke,
                                              (i + 0.5f) / edge.Count * Height - Height * 0.5f);
            rt.localRotation = Quaternion.Euler(0f, 0f, i * 47f + wobble * 2f);
        }
    }

    void BuildEdge()
    {
        foreach (var rt in edge) if (rt != null) Destroy(rt.gameObject);
        edge.Clear();
        int n = Mathf.CeilToInt(Height / 38f) + 1;
        for (int i = 0; i < n; i++) edge.Add(MakeLeaf(i % 2 == 0 ? 2f : 1.6f).rt);
    }

    Leaf MakeLeaf(float size = 1f)
    {
        var go = new GameObject("Leaf", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.sprite = leaves.Length > 0 ? leaves[UnityEngine.Random.Range(0, leaves.Length)] : null;
        Vector2 px = img.sprite != null ? img.sprite.rect.size : new Vector2(9f, 6f);
        rt.sizeDelta = px * LeafScale * size;
        return new Leaf { rt = rt };
    }

    // A loose leaf flying left with the wind from around x (canvas units from the centre).
    void Gust(float x, float speed)
    {
        var l = MakeLeaf(UnityEngine.Random.Range(0.7f, 1.2f));
        l.rt.anchoredPosition = new Vector2(x + UnityEngine.Random.Range(-40f, 120f), UnityEngine.Random.Range(-0.55f, 0.55f) * Height);
        l.v = new Vector2(-speed * UnityEngine.Random.Range(0.9f, 1.5f), UnityEngine.Random.Range(-260f, 260f));
        l.spin = UnityEngine.Random.Range(-720f, 720f);
        flying.Add(l);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        for (int i = flying.Count - 1; i >= 0; i--)
        {
            var l = flying[i];
            if (l.rt == null) { flying.RemoveAt(i); continue; }
            l.v.y += Mathf.Sin(Time.unscaledTime * 6f + i) * 600f * dt; // flutter
            l.rt.anchoredPosition += l.v * dt;
            l.rt.localRotation = Quaternion.Euler(0f, 0f, l.rt.localEulerAngles.z + l.spin * dt);
            if (l.rt.anchoredPosition.x < -Width * 0.5f - 200f) { Destroy(l.rt.gameObject); flying.RemoveAt(i); }
        }
    }

    IEnumerator CoverRoutine(string tone, Action then)
    {
        busy = true;
        LoadSprites(tone);
        curtain.gameObject.SetActive(true);
        curtain.SetAsFirstSibling();
        BuildEdge();
        Sfx.Play("dash", 0.7f, 0.6f); // the gust
        float gustIn = 0f;
        for (float t = 0f; t < CoverTime; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / CoverTime);
            SetCurtain(1f - p, 1f, 1f - p);
            if ((gustIn -= Time.unscaledDeltaTime) <= 0f) { Gust((1f - p) * Width - Width * 0.5f, 2200f); gustIn = 0.012f; }
            yield return null;
        }
        SetCurtain(0f, 1f, -0.1f);
        covered = true;
        yield return null; // one frame fully covered before the load hitch
        then?.Invoke();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!covered) return;
        StopAllCoroutines();
        StartCoroutine(RevealRoutine());
    }

    IEnumerator RevealRoutine()
    {
        // Let the load frame and the one after it (asset clean-up) pass while still covered.
        yield return null;
        yield return null;
        Sfx.Play("dash", 0.6f, 0.75f);
        float gustIn = 0f;
        for (float t = 0f; t < RevealTime; t += Mathf.Min(Time.unscaledDeltaTime, 0.05f))
        {
            float p = Mathf.SmoothStep(0f, 1f, t / RevealTime);
            SetCurtain(0f, 1f - p, 1f - p);
            if ((gustIn -= Time.unscaledDeltaTime) <= 0f) { Gust((1f - p) * Width - Width * 0.5f, 1800f); gustIn = 0.014f; }
            yield return null;
        }
        curtain.gameObject.SetActive(false);
        foreach (var rt in edge) if (rt != null) Destroy(rt.gameObject);
        edge.Clear();
        covered = false;
        busy = false;
        Revealed?.Invoke();
    }
}
