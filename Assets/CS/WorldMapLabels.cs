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
    // Progressive reveal (UI_STYLE.md): one region open, the next one teased as a "?", the rest under cloud. Eight
    // "준비 중" plates read as a list of things the game does not have; one "?" reads as what comes next.
    public const string NextRegion = "caverns";
    static readonly Color Next = new Color(0.86f, 0.8f, 0.66f);
    static readonly Color Open = new Color(1f, 0.9f, 0.55f);    // brighter on the brown plate

    public static Vector3 WorldPos(string key, Vector2 mapSize)
    {
        foreach (var l in Labels) if (l.key == key) return new Vector3((l.x - 0.5f) * mapSize.x, (0.5f - l.y) * mapSize.y, 0f);
        return Vector3.zero;
    }

    // plate: the CHAPTER tag's plate, whose sliced sprite and font the labels share.
    public static void Build(Vector2 mapSize, SpriteRenderer plate, TMP_FontAsset font)
    {
        if (plate == null || font == null) return;
        var root = new GameObject("MapLabels").transform;
        var avoid = new[] { WorldPos(OpenRegion, mapSize), WorldPos(NextRegion, mapSize) }; // never under a cloud
        foreach (var l in Labels)
        {
            var at = new Vector3((l.x - 0.5f) * mapSize.x, (0.5f - l.y) * mapSize.y, 0f);
            bool hidden = !l.big && l.key != OpenRegion && l.key != NextRegion; // the realm too: it joins the title
            if (hidden)
            {
                // cloud over the region and its erased label (a flat patch under where its plate was)
                Fog(root, at, new Vector2(l.w * mapSize.x, l.h * mapSize.y), avoid);
                continue;
            }
            var go = new GameObject("Label_" + l.key);
            go.transform.SetParent(root, false);
            go.transform.position = at;

            var text = new GameObject("Text").AddComponent<TextMeshPro>();
            text.transform.SetParent(go.transform, false);
            text.font = font;
            bool next = l.key == NextRegion;
            text.fontSize = l.big ? 3.8f : next ? 2.2f : 2.8f; // (the plate keeps its box either way: under it the map is a flat patch)
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = next ? Next : Open;
            text.sortingOrder = 16;
            // The title carries the realm's name as its second line (a map title, not a place); the next region is "?".
            text.text = next ? Loc.T("worldmap.region.next") : l.big ? Loc.T("worldmap.region.title") + "\n<size=55%>" + Loc.T("worldmap.region.realm") + "</size>"
                              : Loc.T("worldmap.region." + l.key);
            text.lineSpacing = -12f;
            Vector2 need = text.GetPreferredValues(text.text);
            Vector2 size = new Vector2(Mathf.Max(l.w * mapSize.x, need.x + 0.9f + (next ? 1.1f : 0f)), Mathf.Max(l.h * mapSize.y, need.y + 0.12f)); // the next one: room for its lock
            text.rectTransform.sizeDelta = size;
            if (next) text.rectTransform.anchoredPosition = new Vector2(0.35f, 0f); // beside the lock

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = plate.sprite;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            sr.color = next ? plate.color * new Color(0.6f, 0.6f, 0.6f, 1f) : plate.color; // the "?": a duller plate
            sr.sortingOrder = 15; // over the map, under the forest's tag (19 / 20)
            // The new dark plank with its own padlock (Resources/UICats/ui_lock_plate) for the next region; the old plate and
            // a drawn padlock when the art is missing.
            // Every other plate (the title, the open region's "CHAPTER" tag) is the new vine-cornered dark plank
            // (ui_hud_plate). Both are cut at a scale from the plate's HEIGHT, so their borders keep their proportions.
            var art = Resources.Load<Sprite>(next ? "UICats/ui_lock_plate" : "UICats/ui_hud_plate");
            if (art != null)
            {
                sr.sprite = Sprite.Create(art.texture, art.rect, new Vector2(0.5f, 0.5f), art.rect.height / size.y, 0, SpriteMeshType.FullRect, art.border);
                sr.color = Color.white;
            }
            else if (next) Lock(go.transform, new Vector3(-size.x * 0.5f + 0.5f, 0f, 0f));
        }
    }

    // A cluster of pixel clouds (drawn here, Point filtered: 3 tones and a dark outline, like the map's own clouds)
    // big enough to cover a region's name box with room to spare.
    static Sprite cloud;
    // One big cloud close on the region's name (it hides the flat patch under it), with one or two small puffs round
    // it at uneven heights and gaps: a row of equal clouds read as a band. Puffs never land on the open region or the
    // next one's plate (avoid).
    static Sprite puff;
    static void Fog(Transform root, Vector3 at, Vector2 box, Vector3[] avoid)
    {
        if (cloud == null) cloud = NewCloud(0, 1.8f) ?? MakeCloud();
        if (puff == null) puff = NewCloud(2, 1.0f) ?? MakePuff();
        var rng = new System.Random(Mathf.RoundToInt(at.x * 100f) ^ Mathf.RoundToInt(at.y * 1000f)); // fixed per region
        float w = cloud.bounds.size.x, h = cloud.bounds.size.y;
        int bigs = Mathf.Max(1, Mathf.CeilToInt(box.x / (w * 0.9f))); // a wide name (the realm's) takes two
        for (int b = 0; b < bigs; b++)
        {
            float bx = (b - (bigs - 1) * 0.5f) * w * 0.75f;
            Put(root, cloud, at + new Vector3(bx + (float)(rng.NextDouble() - 0.5) * 0.2f, (b % 2 == 0 ? 1f : -1f) * 0.12f + (float)(rng.NextDouble() - 0.5) * 0.12f, 0f), rng, 14 + b);
        }
        int extra = 1 + rng.Next(2);
        for (int i = 0; i < extra; i++)
        {
            float side = rng.Next(2) == 0 ? -1f : 1f;
            var p = at + new Vector3(side * w * (0.45f + (float)rng.NextDouble() * 0.35f), (float)(rng.NextDouble() - 0.3) * h * 0.9f, 0f);
            bool clear = true;
            foreach (var a in avoid) if (((Vector2)(p - a)).magnitude < 1.6f) clear = false;
            if (clear) Put(root, puff, p, rng, 15 + i); // in front of the big one where they touch
        }
    }

    static void Put(Transform root, Sprite s, Vector3 at, System.Random rng, int order)
    {
        var go = new GameObject("Fog");
        go.transform.SetParent(root, false);
        go.transform.position = at;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.flipX = rng.Next(2) == 0;
        sr.sortingOrder = order;
    }

    // A small pixel padlock (iron, 3 tones, dark outline) on the next region's plate: a "?" alone read as a bug.
    static Sprite lockSprite;
    static void Lock(Transform plate, Vector3 at)
    {
        if (lockSprite == null)
        {
            string[] g =
            {
                "..kkkkk..",
                ".kjjjjjk.",
                ".kj...jk.",
                ".kj...jk.",
                "kkkkkkkkk",
                "khhhhhhhk",
                "khjjkjjjk",
                "khjjkjjjk",
                "kjiiiiiik",
                "kkkkkkkkk",
            };
            int W = g[0].Length, H = g.Length;
            var tex = RuntimeGeneratedAssets.Texture(W, H);
            var px = new Color[W * H];
            var c = new System.Collections.Generic.Dictionary<char, Color>
            {
                { 'k', new Color32(0x2a, 0x1a, 0x14, 0xff) }, { 'i', new Color32(0x6a, 0x5a, 0x40, 0xff) },
                { 'j', new Color32(0xb0, 0x90, 0x50, 0xff) }, { 'h', new Color32(0xe8, 0xc8, 0x70, 0xff) },
            };
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++) { char ch = g[H - 1 - y][x]; px[y * W + x] = c.TryGetValue(ch, out var col) ? col : Color.clear; }
            tex.SetPixels(px);
            tex.Apply();
            lockSprite = RuntimeGeneratedAssets.Sprite(tex, new Vector2(0.5f, 0.5f), 25f);
        }
        var go = new GameObject("Lock");
        go.transform.SetParent(plate, false);
        go.transform.localPosition = at;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = lockSprite;
        sr.sortingOrder = 17; // over the plate and its text
    }

    // A small puff (14 x 8) in the same tones, for the clusters' edges.
    static Sprite MakePuff()
    {
        string[] g =
        {
            "....kkkk......",
            "..kkwwwwkkkk..",
            ".kwwwwwwwwwwk.",
            "kwwwwwwwwwwwwk",
            "kswwwwwwwwwwsk",
            "kssswwwwwwsssk",
            ".kkssssssssk..",
            "...kkkkkkkk...",
        };
        return Grid(g);
    }

    static Sprite Grid(string[] g)
    {
        int W = g[0].Length, H = g.Length;
        var tex = RuntimeGeneratedAssets.Texture(W, H);
        var px = new Color[W * H];
        var k = new Color32(0x5a, 0x3c, 0x22, 0xff); var w = new Color32(0xee, 0xde, 0xb0, 0xff); var s = new Color32(0xd2, 0xb8, 0x80, 0xff);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                char ch = g[H - 1 - y][x];
                px[y * W + x] = ch == 'k' ? (Color)k : ch == 'w' ? (Color)w : ch == 's' ? (Color)s : Color.clear;
            }
        tex.SetPixels(px);
        tex.Apply();
        return RuntimeGeneratedAssets.Sprite(tex, new Vector2(0.5f, 0.5f), 12.5f);
    }

    // The new parchment clouds (Resources/UICats/ui_cloud_0..2), re-cut at a pixels-per-unit that makes them `width` world
    // units wide (the generated ones are 1.9 wide); null if the art is missing.
    static Sprite NewCloud(int index, float width)
    {
        var art = Resources.Load<Sprite>("UICats/ui_cloud_" + index);
        if (art == null) return null;
        var sprite = Sprite.Create(art.texture, art.rect, new Vector2(0.5f, 0.5f), art.rect.width / width);
        sprite.name = "NewCloud" + index;
        return sprite;
    }

    static Sprite MakeCloud()
    {
        string[] g =
        {
            "........kkkkk...........",
            "......kkwwwwwkk..kkkk...",
            ".....kwwwwwwwwwkkwwwwk..",
            "...kkwwwwwwwwwwwwwwwwwk.",
            "..kwwwwwwwwwwwwwwwwwwwwk",
            ".kwwwwwwwwwwwwwwwwwwwwwk",
            "kwwwwwwwwwwwwwwwwwwwwwsk",
            "kswwwwwwwwwwwwwwwwwwwssk",
            "kssswwwwwwwwwwwwwwwsssk.",
            ".kssssssswwwwwwssssssk..",
            "..kkssssssssssssssskk...",
            "....kkkkkkkkkkkkkkk.....",
        };
        int W = g[0].Length, H = g.Length;
        var tex = RuntimeGeneratedAssets.Texture(W, H);
        var px = new Color[W * H];
        // parchment tones from the map itself, the ink outline the map draws with (pure white with blue-grey edges sat
        // on it like a sticker)
        var k = new Color32(0x5a, 0x3c, 0x22, 0xff); var w = new Color32(0xee, 0xde, 0xb0, 0xff); var s = new Color32(0xd2, 0xb8, 0x80, 0xff);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                char ch = g[H - 1 - y][x];
                px[y * W + x] = ch == 'k' ? (Color)k : ch == 'w' ? (Color)w : ch == 's' ? (Color)s : Color.clear;
            }
        tex.SetPixels(px);
        tex.Apply();
        return RuntimeGeneratedAssets.Sprite(tex, new Vector2(0.5f, 0.5f), 12.5f); // the island's pixel size
    }
}
