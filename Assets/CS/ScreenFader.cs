using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// A black overlay that hides scene switches. A single-mode scene load ends with a blocking garbage
// collection (measured ~0.27 s per switch in the editor), which froze the last frame of the stage and the
// rolling cat on the loading screen. SceneLoader / LoadingScreen fade to black first and switch behind it;
// every newly loaded scene fades back in once its heavy first frames are over. Lives across scenes.
public class ScreenFader : MonoBehaviour
{
    const float OutTime = 0.2f, InTime = 0.25f;
    static ScreenFader instance;
    Image overlay;
    bool switching;

    // True from the start of a fade-out until the next scene starts fading in (ignore repeat requests).
    public static bool Switching => instance != null && instance.switching;

    static ScreenFader Instance
    {
        get
        {
            if (instance != null) return instance;
            var go = new GameObject("ScreenFader", typeof(RectTransform));
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000; // above every scene's UI
            go.AddComponent<GraphicRaycaster>();
            var img = new GameObject("Black", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)img.transform;
            rt.SetParent(go.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            instance = go.AddComponent<ScreenFader>();
            instance.overlay = img.GetComponent<Image>();
            instance.Set(0f);
            SceneManager.sceneLoaded += instance.OnSceneLoaded;
            return instance;
        }
    }

    // Fades to black (unscaled time: works under a paused draft / result screen), then runs `then`.
    public static void FadeOut(Action then)
    {
        var f = Instance;
        f.switching = true;
        f.StopAllCoroutines();
        f.StartCoroutine(f.Out(then));
    }

    IEnumerator Out(Action then)
    {
        float a = overlay.color.a;
        while (a < 1f)
        {
            a += Time.unscaledDeltaTime / OutTime;
            Set(a);
            yield return null;
        }
        Set(1f);
        yield return null; // one frame fully black on screen before the hitch
        then?.Invoke();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (overlay.color.a <= 0f) return;
        StopAllCoroutines();
        StartCoroutine(In());
    }

    IEnumerator In()
    {
        // Let the load frame and the one after it (asset clean-up) pass while still black.
        yield return null;
        yield return null;
        switching = false;
        float a = overlay.color.a;
        while (a > 0f)
        {
            a -= Mathf.Min(Time.unscaledDeltaTime, 0.05f) / InTime; // a late frame doesn't skip the fade
            Set(a);
            yield return null;
        }
        Set(0f);
    }

    void Set(float a)
    {
        a = Mathf.Clamp01(a);
        overlay.color = new Color(0f, 0f, 0f, a);
        overlay.raycastTarget = a > 0f; // no clicks go through while the screen is (partly) black
        overlay.enabled = a > 0f;
    }

    void OnDestroy()
    {
        if (instance == this) { SceneManager.sceneLoaded -= OnSceneLoaded; instance = null; }
    }
}
