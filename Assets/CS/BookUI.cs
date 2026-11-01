using UnityEngine;
using UnityEngine.UI;

// The book-style UI pieces (Resources/UIBook, Crusenho's "Complete UI Book Styles Pack" - credit in
// Assets/UI/Book/License.txt), used where a book fits better than the Nature skin:
//   the pause menu is an open travel book, the Q / E / R and passive slots are the pack's inset slots,
//   a hovered card gets the corner brackets, the menu buttons carry its icons, and the mouse uses its cursor.
public static class BookUI
{
    // "Slot01a" = Resources/UIBook/Slot01a; a name with a folder ("UIBundle/MedievalBoard") is loaded as it is.
    public static Sprite Get(string name) => Resources.Load<Sprite>(name.Contains("/") ? name : "UIBook/" + name);

    // A whole (unsliced) piece drawn at `scale` canvas units per art pixel.
    public static Image Piece(RectTransform parent, string name, string sprite, Vector2 pos, float scale)
    {
        var s = Get(sprite);
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = s != null ? s.rect.size * scale : Vector2.one * 10f;
        var img = go.GetComponent<Image>();
        img.sprite = s;
        img.raycastTarget = false;
        return img;
    }

    // A 9-sliced piece (slot, select brackets) on an existing image, `scale` canvas units per art pixel.
    public static void Slice(Image img, string sprite, float scale, Color? tint = null)
    {
        var s = Get(sprite);
        if (img == null || s == null) return;
        img.sprite = s;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f / scale;
        img.color = tint ?? Color.white;
    }

    // An icon on the left of a button, the label moved over to make room.
    public static void ButtonIcon(Button button, string sprite, float scale = 4f)
    {
        if (button == null) return;
        var rt = (RectTransform)button.transform;
        var icon = Piece(rt, "Icon", sprite, Vector2.zero, scale);
        var irt = icon.rectTransform;
        irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
        irt.anchoredPosition = new Vector2(56f, 4f);
        foreach (Transform child in rt)
        {
            if (child == irt || child.GetComponent<TMPro.TMP_Text>() == null) continue;
            var label = child.GetComponent<TMPro.TMP_Text>();
            var lrt = (RectTransform)child;
            lrt.offsetMin = new Vector2(Mathf.Max(lrt.offsetMin.x, 92f), lrt.offsetMin.y);
            lrt.offsetMax = new Vector2(Mathf.Min(lrt.offsetMax.x, -18f), lrt.offsetMax.y);
            label.enableAutoSizing = true;
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = 24f;
            label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            label.overflowMode = TMPro.TextOverflowModes.Truncate;
        }
    }
}

