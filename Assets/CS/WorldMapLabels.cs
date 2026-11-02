using TMPro;
using UnityEngine;

// Korean names over the CAT ISLAND map's English ones: the PixelLab art has its labels painted in, so each gets a
// plate that covers it, with the region's name from the text table (worldmap.region.*). The open region (고대 숲) is
// gold like its CHAPTER tag; the rest are a quiet parchment, so the eye goes to where you can actually go.
public static class WorldMapLabels
{
    // Centre and size of each painted label, as a fraction of the map picture's width / height (from the top-left).
    struct Label
    {
        public string key; public float x, y, w, h; public bool big;
        public Label(string key, float x, float y, float w, float h, bool big = false) { this.key = key; this.x = x; this.y = y; this.w = w; this.h = h; this.big = big; }
    }

    static readonly Label[] Labels =
    {
        new Label("title", 0.5015f, 0.047f, 0.165f, 0.055f, true),
        new Label("realm", 0.134f, 0.441f, 0.145f, 0.078f),
        new Label("caverns", 0.289f, 0.318f, 0.095f, 0.055f),
        new Label("frost", 0.5565f, 0.191f, 0.085f, 0.055f),
        new Label("fortress", 0.669f, 0.211f, 0.085f, 0.055f),
        new Label("labyrinth", 0.37f, 0.479f, 0.07f, 0.055f),
        new Label("mountain", 0.6265f, 0.479f, 0.065f, 0.055f),
        new Label("volcano", 0.8275f, 0.538f, 0.075f, 0.055f),
        new Label("crypt", 0.6975f, 0.621f, 0.075f, 0.055f),
        new Label("forest", 0.309f, 0.675f, 0.09f, 0.061f),
        new Label("lagoon", 0.4465f, 0.665f, 0.08f, 0.055f),
        new Label("atoll", 0.9215f, 0.715f, 0.075f, 0.055f),
    };
    const string OpenRegion = "forest";
    static readonly Color Locked = new Color(0.85f, 0.77f, 0.6f);
    static readonly Color Open = new Color(1f, 0.82f, 0.4f);

    // plate: the CHAPTER tag's plate, whose sliced sprite and font the labels share.
    public static void Build(Vector2 mapSize, SpriteRenderer plate, TMP_FontAsset font)
    {
        if (plate == null || font == null) return;
        var root = new GameObject("MapLabels").transform;
        foreach (var l in Labels)
        {
            var go = new GameObject("Label_" + l.key);
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3((l.x - 0.5f) * mapSize.x, (0.5f - l.y) * mapSize.y, 0f);

            var text = new GameObject("Text").AddComponent<TextMeshPro>();
            text.transform.SetParent(go.transform, false);
            text.font = font;
            text.fontSize = l.big ? 3.4f : 2.2f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            bool locked = l.key != OpenRegion && !l.big && l.key != "realm";
            text.color = locked ? Locked : Open;
            text.sortingOrder = 16;
            // Closed regions say so: eleven names and one open forest otherwise read as content that isn't there.
            text.text = Loc.T("worldmap.region." + l.key) + (locked ? "\n" + Loc.T("worldmap.region.locked") : ""); // as big as the name (70 / 85% was too small on the map)
            text.lineSpacing = -12f;
            Vector2 need = text.GetPreferredValues(text.text);
            Vector2 size = new Vector2(Mathf.Max(l.w * mapSize.x, need.x + 0.4f), Mathf.Max(l.h * mapSize.y, need.y + 0.12f));
            text.rectTransform.sizeDelta = size;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = plate.sprite;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            sr.color = locked ? plate.color * new Color(0.7f, 0.7f, 0.7f, 1f) : plate.color; // closed: a duller plate
            sr.sortingOrder = 15; // over the map, under the forest's tag (19 / 20)
        }
    }
}
