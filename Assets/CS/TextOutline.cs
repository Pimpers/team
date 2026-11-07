using TMPro;
using UnityEngine;

// A 1 px dark outline round a TMP text made of four dark copies one pixel out (left, right, up, down), drawn under
// it - the pixel font has no SDF, so TMP's own outlineWidth draws nothing. Add it with UiTheme.Outline(text).
// The copies follow the text, its font, size and alignment every frame (four extra text meshes: keep it to short
// labels such as the card grade).
[RequireComponent(typeof(TMP_Text))]
public class TextOutline : MonoBehaviour
{
    static readonly Vector2[] Dirs = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };
    TMP_Text text;
    readonly TextMeshProUGUI[] copies = new TextMeshProUGUI[4];
    Color ink;
    float px;

    public static void Attach(TMP_Text text, Color ink, float pixel)
    {
        var o = text.GetComponent<TextOutline>();
        if (o == null) o = text.gameObject.AddComponent<TextOutline>();
        o.text = text; o.ink = ink; o.px = pixel;
        for (int i = 0; i < o.copies.Length; i++)
        {
            if (o.copies[i] != null) continue;
            var go = new GameObject(text.name + "_Edge" + i, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(text.transform.parent, false);
            go.transform.SetSiblingIndex(text.transform.GetSiblingIndex()); // before the text: drawn under it
            o.copies[i] = go.GetComponent<TextMeshProUGUI>();
            o.copies[i].raycastTarget = false;
        }
        o.LateUpdate();
    }

    void LateUpdate()
    {
        if (text == null) return;
        var rt = text.rectTransform;
        for (int i = 0; i < copies.Length; i++)
        {
            var c = copies[i];
            if (c == null) continue;
            if (c.gameObject.activeSelf != text.isActiveAndEnabled) c.gameObject.SetActive(text.isActiveAndEnabled);
            if (c.text != text.text) c.text = text.text;
            c.font = text.font; c.fontSize = text.fontSize; c.fontStyle = text.fontStyle;
            c.alignment = text.alignment; c.textWrappingMode = text.textWrappingMode;
            c.color = new Color(ink.r, ink.g, ink.b, ink.a * text.color.a);
            var crt = c.rectTransform;
            crt.anchorMin = rt.anchorMin; crt.anchorMax = rt.anchorMax; crt.pivot = rt.pivot;
            crt.sizeDelta = rt.sizeDelta; crt.localScale = rt.localScale;
            crt.anchoredPosition = rt.anchoredPosition + Dirs[i] * px;
        }
    }

    void OnDestroy()
    {
        foreach (var c in copies) if (c != null) Destroy(c.gameObject);
    }
}
