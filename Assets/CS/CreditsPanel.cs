using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The credits window (opened from the settings window's "크레딧" button): everything in the game that wasn't made
// for it, by whom, under which licence - some of the packs require the credit (Crusenho's UI book pack), the rest
// appreciate it. Two columns on a dark wood board; click the corner X or anywhere outside to close. Built at runtime.
public class CreditsPanel : MonoBehaviour
{
    // The list itself lives in Resources/Credits.txt (one place to update when an asset is added):
    // "## " a section, "|||" the right-hand column, "> " a quieter line, "# " a note that isn't shown.
    static void LoadColumns(out string left, out string right)
    {
        var file = Resources.Load<TextAsset>("Credits");
        var cols = new[] { new System.Text.StringBuilder(), new System.Text.StringBuilder() };
        int col = 0;
        foreach (var raw in (file != null ? file.text : "").Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (line.StartsWith("|||")) { col = 1; continue; }
            if (line.StartsWith("## ")) { cols[col].Append("<size=200%><color=" + UiTheme.Html("gold") + ">").Append(line.Substring(3)).Append("</color></size>\n"); continue; }
            if (line.StartsWith("# ")) continue;
            if (line.StartsWith("> ")) { cols[col].Append("<color=" + UiTheme.Html("note") + ">").Append(line.Substring(2)).Append("</color>\n"); continue; }
            // "Asset name / maker (licence)": the name stays bright, the maker and licence go quieter, so the
            // list reads as names first. (Lines without " / " - notes, the sound packs - stay as written.)
            int slash = line.IndexOf(" / ", System.StringComparison.Ordinal);
            if (slash > 0) cols[col].Append(line, 0, slash).Append("<color=" + UiTheme.Html("note") + ">").Append(line, slash, line.Length - slash).Append("</color>\n");
            else cols[col].Append(line).Append('\n');
        }
        left = cols[0].ToString().TrimEnd();
        right = cols[1].ToString().TrimEnd();
    }

    static CreditsPanel open;

    public static void Show(Canvas canvas, TMP_FontAsset font)
    {
        if (canvas == null || open != null) return;
        var root = new GameObject("Credits", typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)root.transform;
        rt.SetParent(canvas.rootCanvas.transform, false);
        rt.SetAsLastSibling();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f); // dims the game; a click here closes
        open = root.AddComponent<CreditsPanel>();
        root.GetComponent<Button>().onClick.AddListener(open.Close);

        var board = new GameObject("Board", typeof(RectTransform), typeof(Image));
        var brt = (RectTransform)board.transform;
        brt.SetParent(rt, false);
        brt.sizeDelta = new Vector2(1700f, 1000f);
        var img = board.GetComponent<Image>();
        img.color = new Color(0.12f, 0.08f, 0.07f, 0.97f);
        if (UiPanels.NewBoard(img, 4f)) { }
        else if (BookUI.Get("UIBundle/MedievalBoard") != null) BookUI.Slice(img, "UIBundle/MedievalBoard", 6f);
        else if (UISkin.Instance != null) UISkin.Apply(img, UISkin.Instance.slate, 4f);
        board.AddComponent<Button>().transition = Selectable.Transition.None; // clicks on the board don't close it

        // A dark sheet inside the light wood, so the cream text reads.
        var sheet = new GameObject("Sheet", typeof(RectTransform), typeof(Image));
        var srt = (RectTransform)sheet.transform;
        srt.SetParent(brt, false);
        srt.anchoredPosition = new Vector2(0f, -10f);
        srt.sizeDelta = new Vector2(1560f, 760f);
        var simg = sheet.GetComponent<Image>();
        simg.color = new Color(0.09f, 0.06f, 0.07f, 0.94f);
        simg.raycastTarget = false;

        Text(brt, "Title", Loc.T("credits.title"), 64f, new Vector2(0f, 425f), new Vector2(1400f, 80f), TextAlignmentOptions.Center, font).color = UISkin.BannerText;
        LoadColumns(out string Left, out string Right);
        Text(srt, "Left", Left, 16f, new Vector2(-380f, 0f), new Vector2(740f, 720f), TextAlignmentOptions.TopLeft, font);
        Text(srt, "Right", Right, 16f, new Vector2(390f, 0f), new Vector2(720f, 720f), TextAlignmentOptions.TopLeft, font);

        // The X in the board's corner closes it (a click outside the board does too).
        UiPanels.CloseButton(brt, font, open.Close, 28f, 72f);
    }

    static TextMeshProUGUI Text(RectTransform parent, string name, string text, float size, Vector2 pos, Vector2 box, TextAlignmentOptions align, TMP_FontAsset font)
    {
        var t = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.rectTransform.SetParent(parent, false);
        t.rectTransform.anchoredPosition = pos;
        t.rectTransform.sizeDelta = box;
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.lineSpacing = 6f;
        t.color = UISkin.OverlayText;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        t.outlineWidth = 0.15f;
        t.outlineColor = new Color32(24, 14, 10, 255);
        return t;
    }

    void Close()
    {
        if (open == this) open = null;
        Destroy(gameObject);
    }
}
