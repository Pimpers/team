using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The game's UI look, made from Kenney's "UI Pack - Pixel Adventure" (Assets/UI, CC0):
// parchment panels with dark ink text, red ribbon banners for titles, red buttons for the
// main action and wooden ones for everything else, slate plates for the in-game overlays.
// The sprites are cut and assigned by the editor tool UISkinApplier (menu Cats Smash >
// Apply UI Skin), which also restyles the scenes; this asset lives in Resources so UI
// built at runtime (stage HUD plate, minimap, boss bar, blacksmith buttons) can use it.
[CreateAssetMenu(menuName = "Cats Smash/UI Skin")]
public class UISkin : ScriptableObject
{
    public Sprite panel;        // cream parchment, brown frame
    public Sprite slate;        // dark blue-grey plate (overlays on top of the level)
    public Sprite woodPanel;    // dark brown plate (HUD, minimap and boss-bar frames: warm, matches the wood buttons)
    public Sprite card;         // cream card with rivets (card draft)
    public Sprite banner;       // red ribbon (3 tiles wide)
    public Sprite buttonRed;
    public Sprite buttonWood;
    public Sprite buttonGrey;
    public Sprite buttonCream;
    public Sprite knob;         // round slider handle

    // Text colours for the parchment panels.
    public static readonly Color Ink = new Color32(0x4b, 0x2e, 0x1f, 0xff);
    public static readonly Color InkSoft = new Color32(0x86, 0x62, 0x42, 0xff);
    public static readonly Color InkTitle = new Color32(0x8a, 0x3b, 0x12, 0xff);
    public static readonly Color InkGold = new Color32(0xb0, 0x62, 0x10, 0xff);
    public static readonly Color InkWarning = new Color32(0xc0, 0x2a, 0x2a, 0xff);
    public static readonly Color BannerText = new Color32(0xff, 0xf1, 0xd6, 0xff);
    public static readonly Color OverlayText = new Color32(0xff, 0xf1, 0xd6, 0xff);
    public static readonly Color OverlayMuted = new Color32(0xc9, 0xb4, 0x8a, 0xff);
    public static readonly Color OverlayBack = new Color(0.14f, 0.085f, 0.05f, 0.93f); // dark wood (was near-black violet: see UiTheme.Plate)
    public static readonly Color ModalShade = new Color(0.02f, 0.015f, 0.03f, 0.78f);

    // On the pixel font's grid (PixelText.Dot = 12, see UiTheme): the old 40 / 20 / 18 were snapped to 36 / 24 / 24
    // anyway, so these say what is actually drawn.
    public const float FontTitle = UiTheme.TextTitle;
    public const float FontHeading = UiTheme.TextLarge;
    public const float FontButton = UiTheme.TextLarge;
    public const float FontBody = UiTheme.TextSmall;
    public const float FontHud = UiTheme.TextSmall;
    public const float FontSmall = UiTheme.TextSmall;

    public const float SpaceSmall = 8f;
    public const float Space = 16f;
    public const float SpaceLarge = 24f;

    // Canvas units per sprite pixel for the chunky pixel look.
    public const float LargeScale = 4f;  // 32px panels
    public const float SmallScale = 4f;  // 16px buttons
    const float GlyphLift = 3f;

    static UISkin instance;
    public static UISkin Instance
    {
        get
        {
            if (instance == null) instance = Resources.Load<UISkin>("UISkin");
            return instance;
        }
    }

    // Puts a 9-sliced pixel sprite on an Image. `scale` = canvas units per sprite pixel
    // (sprites are imported at 100 PPU, the canvases' reference PPU).
    public static void Apply(Image img, Sprite sprite, float scale, Color? tint = null)
    {
        if (img == null || sprite == null) return;
        img.sprite = sprite;
        // The Nature pack's vine edges repeat instead of stretching (a stretched vine smears into a bar).
        bool vines = sprite.texture != null && sprite.texture.name.StartsWith("nature_") && !sprite.texture.name.StartsWith("nature_button"); // plain buttons stretch (tiling repeated their highlight pixel)
        img.type = sprite.border == Vector4.zero ? Image.Type.Simple : vines ? Image.Type.Tiled : Image.Type.Sliced;
        // The vine plank (banners, main buttons) is one board tall: size it to the rect's height so it only
        // repeats sideways - tiled at a fixed scale, a tall button stacked two or three boards.
        if (vines && sprite.texture.name == "nature_plank")
        {
            float h = img.rectTransform.rect.height;
            if (h <= 0f) h = img.rectTransform.sizeDelta.y;
            if (h > 0f) scale = h / sprite.rect.height * sprite.pixelsPerUnit / 100f;
            // A button's label goes on the board's flat face, below the leaves (as on the ribbons).
            if (img.GetComponent<Button>() != null)
                foreach (Transform child in img.transform)
                    if (child.GetComponent<TMP_Text>() != null) FitToRibbon((RectTransform)child, img);
        }
        img.pixelsPerUnitMultiplier = 1f / scale;
        if (img.type == Image.Type.Tiled) SnapToTiles(img, sprite, scale);
        // Plain Nature buttons: centre the label on the visible face. The pill's bottom 3 rows are its
        // shadow and it has a 5-6 px rim, so the face's middle sits above the rect's middle.
        if (sprite.texture != null && sprite.texture.name.StartsWith("nature_button") && img.GetComponent<Button>() != null)
        {
            float px = scale * 100f / sprite.pixelsPerUnit; // canvas units per art pixel
            foreach (Transform child in img.transform)
            {
                var label = child.GetComponent<TMP_Text>();
                if (label == null) continue;
                var rt = (RectTransform)child;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = new Vector2(6f * px, 3f * px - GlyphLift);
                rt.offsetMax = new Vector2(-6f * px, -1f * px - GlyphLift);
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.margin = Vector4.zero;
            }
        }
        img.color = tint ?? Color.white;
    }

    // A tiled 9-slice repeats its edges and middle in whole art tiles; a rect that isn't border + n tiles ends each
    // edge on a sliver of a tile (the vines stop mid-leaf and restart). Rounds a free-standing rect (not stretched
    // by its anchors) to the nearest whole number of tiles each way - a few canvas units at most.
    static void SnapToTiles(Image img, Sprite sprite, float scale)
    {
        var rt = img.rectTransform;
        if (rt.anchorMin != rt.anchorMax) return;
        float px = scale * 100f / sprite.pixelsPerUnit; // canvas units per art pixel
        Vector4 b = sprite.border;
        float tileW = (sprite.rect.width - b.x - b.z) * px, tileH = (sprite.rect.height - b.y - b.w) * px;
        float edgeW = (b.x + b.z) * px, edgeH = (b.y + b.w) * px;
        var size = rt.sizeDelta;
        if (tileW > 0f && size.x > edgeW) size.x = edgeW + Mathf.Max(1f, Mathf.Round((size.x - edgeW) / tileW)) * tileW;
        if (tileH > 0f && size.y > edgeH && sprite.texture.name != "nature_plank") size.y = edgeH + Mathf.Max(1f, Mathf.Round((size.y - edgeH) / tileH)) * tileH;
        rt.sizeDelta = size;
    }

    // Banners only stretch sideways: scale them so the ribbon fills the rect's height.
    public static void ApplyBanner(Image img)
    {
        var skin = Instance;
        // The new scroll-ended ribbon (Resources/UICats/ui_ribbon, vine leaves, 16 px ends): sideways stretch only.
        var ribbon = Resources.Load<Sprite>("UICats/ui_ribbon");
        if (img != null && ribbon != null)
        {
            float rh = img.rectTransform.rect.height;
            if (rh <= 0f) rh = img.rectTransform.sizeDelta.y;
            Apply(img, ribbon, Mathf.Max(0.5f, rh / ribbon.rect.height * ribbon.pixelsPerUnit / 100f));
            return;
        }
        if (img == null || skin == null || skin.banner == null) return;
        float h = img.rectTransform.rect.height;
        if (h <= 0f) h = img.rectTransform.sizeDelta.y;
        // `scale` counts 100 px/unit pixels; the Nature sprites are 200 px/unit, so convert.
        Apply(img, skin.banner, Mathf.Max(0.5f, h / skin.banner.rect.height * skin.banner.pixelsPerUnit / 100f));
    }

    // Keeps every title centred inside the clean sliced banner.
    public static void FitToRibbon(RectTransform label, Image ribbon)
    {
        if (label == null || ribbon == null) return;
        label.anchorMin = Vector2.zero;
        label.anchorMax = Vector2.one;
        label.pivot = new Vector2(0.5f, 0.5f);
        label.offsetMin = new Vector2(22f, 8f);
        label.offsetMax = new Vector2(-22f, -8f);
        var text = label.GetComponent<TMP_Text>();
        if (text != null)
        {
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            text.margin = Vector4.zero;
            text.enableAutoSizing = true;
            text.fontSizeMax = Mathf.Max(28f, text.fontSize);
            text.fontSizeMin = 24f;
        }
    }

    public static void Text(TMP_Text text, Color color)
    {
        if (text != null) text.color = color;
    }
}
