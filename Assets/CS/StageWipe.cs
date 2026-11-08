using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The scene change between stages (it replaced LeafWipe's sweep of leaves): an iris - the screen closes to a circle
// on the exit log's mouth as the cat is pulled in, stays shut while the next stage loads (however long that takes:
// it only opens once the new scene has loaded and had two frames to settle), then opens on the cat at the start.
// The hole is a 256 px pixel circle (point filtered), so its edge steps like the rest of the art. Black like
// ScreenFader. Lives across the load (DontDestroyOnLoad), runs on unscaled time.
public class StageWipe : MonoBehaviour
{
    const float CloseTime = 0.45f, OpenTime = 0.5f;

    static StageWipe instance;
    // True from the start of a close until the next stage is open again.
    public static bool Busy => instance != null && instance.busy;
    // The last scene was entered through an exit log (StageExit then has the cat pop out at the start).
    public static bool ArrivedThroughExit { get; set; }
    // Raised when the iris has opened on the new stage.
    public static event Action Revealed;

    RectTransform hole;
    readonly RectTransform[] sides = new RectTransform[4];
    bool busy, shut;

    // Closes on `screenPoint` (pixels, e.g. the log's mouth), then runs `then` (the scene load).
    public static void Close(Vector2 screenPoint, Action then)
    {
        if (Busy) return;
        if (instance == null) Create();
        instance.StartCoroutine(instance.CloseRoutine(screenPoint, then));
    }

    static void Create()
    {
        var go = new GameObject("StageWipe", typeof(RectTransform));
        DontDestroyOnLoad(go);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31990; // over every scene's UI, under the black ScreenFader
        go.AddComponent<GraphicRaycaster>();
        instance = go.AddComponent<StageWipe>();
        instance.hole = instance.Piece("Hole");
        instance.hole.GetComponent<Image>().sprite = HoleSprite();
        instance.hole.GetComponent<Image>().color = Color.white;
        for (int i = 0; i < 4; i++) instance.sides[i] = instance.Piece("Side" + i);
        instance.Show(false);
        SceneManager.sceneLoaded += instance.OnSceneLoaded;
    }

    RectTransform Piece(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = Vector2.zero; // positioned in screen pixels from the bottom-left
        rt.pivot = new Vector2(0.5f, 0.5f);
        var img = go.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = true; // nothing clicks through while it runs
        return rt;
    }

    // Black with a clear pixel circle in the middle (256 px).
    static Sprite HoleSprite()
    {
        const int N = 256;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[N * N];
        float c = (N - 1) * 0.5f, r2 = c * c;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = x - c, dy = y - c;
                px[y * N + x] = dx * dx + dy * dy <= r2 ? new Color32(0, 0, 0, 0) : new Color32(0, 0, 0, 255);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
    }

    void OnDestroy()
    {
        if (instance == this) { SceneManager.sceneLoaded -= OnSceneLoaded; instance = null; }
    }

    void Show(bool on)
    {
        hole.gameObject.SetActive(on);
        foreach (var s in sides) s.gameObject.SetActive(on);
    }

    // The circle of `radius` screen pixels round `centre`, black everywhere else. (No CanvasScaler: canvas units
    // are screen pixels.)
    void Set(Vector2 centre, float radius)
    {
        float w = Screen.width, h = Screen.height, d = Mathf.Max(radius, 0f);
        hole.anchoredPosition = centre;
        hole.sizeDelta = Vector2.one * d * 2f;
        hole.gameObject.SetActive(d > 0.5f);
        // left, right, below and above the hole's square (at radius 0 they cover everything)
        Rect(sides[0], 0f, 0f, centre.x - d, h);
        Rect(sides[1], centre.x + d, 0f, w, h);
        Rect(sides[2], centre.x - d, 0f, centre.x + d, centre.y - d);
        Rect(sides[3], centre.x - d, centre.y + d, centre.x + d, h);
    }

    static void Rect(RectTransform rt, float x0, float y0, float x1, float y1)
    {
        x0 = Mathf.Max(0f, x0); y0 = Mathf.Max(0f, y0); x1 = Mathf.Max(x0, x1); y1 = Mathf.Max(y0, y1);
        rt.anchoredPosition = new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);
        rt.sizeDelta = new Vector2(x1 - x0, y1 - y0);
    }

    static float Full => Mathf.Sqrt(Screen.width * (float)Screen.width + Screen.height * (float)Screen.height);

    IEnumerator CloseRoutine(Vector2 at, Action then)
    {
        busy = true;
        Show(true);
        float r0 = Full;
        for (float t = 0f; t < CloseTime; t += Time.unscaledDeltaTime)
        {
            float k = t / CloseTime;
            Set(at, r0 * (1f - k * k)); // eases in: slow first, snapping shut
            yield return null;
        }
        Set(at, 0f);
        shut = true;
        yield return null; // one frame fully shut before the load hitch
        then?.Invoke();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!shut) return;
        StopAllCoroutines();
        StartCoroutine(OpenRoutine());
    }

    IEnumerator OpenRoutine()
    {
        // the load frame and the one after it (asset clean-up) pass while still shut
        yield return null;
        yield return null;
        Vector2 at = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        var cam = Camera.main;
        if (cam != null && PlayerStats.Instance != null) at = cam.WorldToScreenPoint(PlayerStats.Instance.transform.position);
        float r1 = Full;
        for (float t = 0f; t < OpenTime; t += Mathf.Min(Time.unscaledDeltaTime, 0.05f))
        {
            float k = t / OpenTime;
            Set(at, r1 * (1f - (1f - k) * (1f - k))); // eases out
            yield return null;
        }
        Show(false);
        shut = false;
        busy = false;
        Revealed?.Invoke();
    }

    // For the capture tool: the iris held shut on the screen's centre (cleared by the next scene load).
    public static void HoldShutForCapture()
    {
        if (instance == null) Create();
        instance.busy = true;
        instance.shut = true; // the next scene load opens it as usual
        instance.Show(true);
        instance.Set(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), 0f);
    }
}
