using UnityEngine;
using TMPro;

// Attach to a TextMeshProUGUI to fade its alpha up and down in a loop (e.g. "Touch to Start").
[RequireComponent(typeof(TMP_Text))]
public class BlinkingText : MonoBehaviour
{
    public float blinkSpeed = 2f;
    [Range(0f, 1f)] public float minAlpha = 0.6f;
    [Range(0f, 1f)] public float maxAlpha = 1f;

    TMP_Text text;

    void Awake()
    {
        text = GetComponent<TMP_Text>();
        // Light text over a busy forest: never fade below 0.6, and give it a dark outline so it reads at any alpha.
        minAlpha = Mathf.Max(minAlpha, 0.6f);
        text.outlineWidth = 0.22f;
        text.outlineColor = new Color32(30, 22, 16, 255);
        UiTheme.Backdrop(text); // (the outline does not draw with the pixel font: the band does the job)
    }

    void Update()
    {
        float t = (Mathf.Sin(Time.time * blinkSpeed) + 1f) / 2f;
        Color c = text.color;
        c.a = Mathf.Lerp(minAlpha, maxAlpha, t);
        text.color = c;
    }
}
