using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A translucent dark band behind a line of text that sits straight on the game picture (no panel of its own):
// HUD hints, the set banner's effect line, the title's "click to start". Add it with UiTheme.Backdrop(text).
// The band is a sibling just before the text (so it draws under it) and follows the text's drawn bounds each frame,
// so a changed string or language resizes it. The pixel font has no SDF, so a TMP outline never showed.
[RequireComponent(typeof(TMP_Text))]
public class TextBackdrop : MonoBehaviour
{
    TMP_Text text;
    RectTransform band;
    Image image;
    Vector2 pad;

    public static TextBackdrop Attach(TMP_Text text, Color colour, Vector2 pad)
    {
        var b = text.GetComponent<TextBackdrop>();
        if (b == null) b = text.gameObject.AddComponent<TextBackdrop>();
        b.text = text;
        b.pad = pad;
        if (b.band == null)
        {
            var go = new GameObject(text.name + "_Backdrop", typeof(RectTransform), typeof(Image));
            b.band = (RectTransform)go.transform;
            b.band.SetParent(text.transform.parent, false);
            b.band.SetSiblingIndex(text.transform.GetSiblingIndex()); // just before the text: drawn under it
            b.image = go.GetComponent<Image>();
            b.image.raycastTarget = false;
            var le = go.AddComponent<LayoutElement>();
            le.ignoreLayout = true; // never take a slot in a layout group the text is in
        }
        b.image.color = colour;
        b.LateUpdate();
        return b;
    }

    void LateUpdate()
    {
        if (text == null || band == null) return;
        bool show = text.isActiveAndEnabled && !string.IsNullOrEmpty(text.text);
        if (band.gameObject.activeSelf != show) band.gameObject.SetActive(show);
        if (!show) return;
        var rt = text.rectTransform;
        var bounds = text.textBounds; // in the text's local space, round its pivot
        band.anchorMin = rt.anchorMin;
        band.anchorMax = rt.anchorMax;
        band.pivot = new Vector2(0.5f, 0.5f);
        var scale = (Vector2)rt.localScale;
        band.anchoredPosition = rt.anchoredPosition + Vector2.Scale((Vector2)bounds.center, scale);
        band.sizeDelta = Vector2.Scale((Vector2)bounds.size, scale) + pad * 2f
            - (rt.anchorMax - rt.anchorMin) * ((RectTransform)band.parent).rect.size; // stretched anchors add the parent size
    }

    void OnDestroy()
    {
        if (band != null) Destroy(band.gameObject);
    }
}
