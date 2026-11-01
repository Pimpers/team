using UnityEngine;

// Location title card: fades in when the scene opens, stays briefly, then fades out and
// hides itself (e.g. the "항구" banner in the harbor village).
[RequireComponent(typeof(CanvasGroup))]
public class FadeBanner : MonoBehaviour
{
    public float delay = 0.2f;
    public float fadeIn = 0.4f;
    public float hold = 1f;
    public float fadeOut = 0.5f;
    [Tooltip("Slides down this many UI units while fading in.")]
    public float slide = 30f;

    CanvasGroup group;
    RectTransform rt;
    Vector2 basePos;
    float t;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        rt = transform as RectTransform;
        if (rt != null) basePos = rt.anchoredPosition;
    }

    void Update()
    {
        t += Time.unscaledDeltaTime;
        float u = t - delay;
        float a;
        if (u < 0f) a = 0f;
        else if (u < fadeIn) a = u / fadeIn;
        else if (u < fadeIn + hold) a = 1f;
        else if (u < fadeIn + hold + fadeOut) a = 1f - (u - fadeIn - hold) / fadeOut;
        else { gameObject.SetActive(false); return; }

        group.alpha = a;
        if (rt != null)
        {
            float k = u < fadeIn ? 1f - Mathf.Clamp01(u / fadeIn) : 0f;
            rt.anchoredPosition = basePos + Vector2.up * slide * k * k;
        }
    }
}
