using System.Collections.Generic;
using UnityEngine;

// The UI's shared palette and type sizes. Colours used in more than one place live here instead of as
// new Color(...) / "#rrggbb" literals in each script; one-off layout numbers (a panel's own size and offsets)
// stay next to the code that lays that panel out.
public static class UiTheme
{
    // ---- Colours (Color)
    public static readonly Color Gold = new Color(1f, 0.82f, 0.25f);          // gold, strong hits, rewards
    public static readonly Color Alert = new Color(1f, 0.35f, 0.3f);          // "!" and role warnings over enemies
    public static readonly Color Critical = new Color(1f, 0.38f, 0.2f);       // critical hit numbers
    public static readonly Color Cream = new Color(1f, 0.945f, 0.82f);        // light text on dark wood buttons
    public static readonly Color Subtitle = new Color(1f, 0.92f, 0.75f);      // banner / stage-title subtitles
    public static readonly Color DisabledText = new Color(0.6f, 0.55f, 0.5f); // a button you can't use yet
    public static readonly Color Wood = new Color(0.45f, 0.3f, 0.18f);        // fallback when a panel sprite is missing
    public static readonly Color Wine = new Color(0.62f, 0.16f, 0.22f, 1f);   // buy buttons, role badges
    public static readonly Color Plate = new Color(0.07f, 0.05f, 0.08f, 0.88f); // dark plate behind world labels
    public static readonly Color TextBand = new Color(0.06f, 0.04f, 0.05f, 0.7f); // band under text on the picture (0.55 left red keywords faint over the bright village)

    // Set-tag colours for text on a dark plate or the game picture (CardPool.TagColors are the dark inks for the cream
    // cards; on dark they sank - the Tab sheet's red #발톱). Rule: dark ground, these; cream ground, TagColors.
    public static readonly Color[] TagOnDark =
    {
        new Color32(0xff, 0x8a, 0x6a, 0xff), new Color32(0x6f, 0xd3, 0xf0, 0xff), new Color32(0xe8, 0xb8, 0x80, 0xff),
        new Color32(0xa6, 0xe8, 0x6e, 0xff), new Color32(0xff, 0xd7, 0x5e, 0xff),
    };
    public static string TagOnDarkHex(int tag) => "#" + ColorUtility.ToHtmlStringRGB(TagOnDark[tag]);

    // The shared style for text that sits straight on the game picture: a translucent dark band behind it.
    public static void Backdrop(TMPro.TMP_Text text) => TextBackdrop.Attach(text, TextBand, new Vector2(12f, 4f));

    // A 1 px dark outline for a short label (the pixel font has no SDF outline): four dark copies a pixel out.
    public static readonly Color OutlineInk = new Color(0.1f, 0.06f, 0.05f, 1f);
    public static void Outline(TMPro.TMP_Text text, float pixel = 2f) => TextOutline.Attach(text, OutlineInk, pixel);

    // ---- Rich-text colours (<color=#...>), also reachable from the text table as $name (Loc expands them).
    static readonly Dictionary<string, string> Hex = new Dictionary<string, string>
    {
        { "gold", "#ffd75e" },      // highlight on dark panels
        { "key", "#c0392b" },       // keys and warnings on parchment
        { "ink", "#8a3b12" },       // strong text on parchment
        { "muted", "#866242" },     // quiet text on parchment
        { "amber", "#b06210" },     // gold / level numbers on parchment
        { "label", "#e6d3a8" },     // a label next to a value on dark
        { "dimlabel", "#d8c49a" },  // a quieter label on dark
        { "note", "#c9b48a" },      // a closing note on dark
        { "good", "#2e7d32" },      // praise on parchment
        { "hurt", "#ff8070" },      // what hurt you, few enemies left
        { "danger", "#ff6b5a" },    // corruption on the HUD
        { "orange", "#ffb05e" },
        { "tip", "#a6e86e" },       // advice
        { "white", "#ffffff" },
        { "active", "#b0302a" },    // card kinds in the draft
        { "passive", "#1f6b5c" },
        { "enhance", "#6a3fa0" },
        { "map_player", "#ffd94d" }, // minimap legend (the player marker's gold)
        { "map_enemy", "#ff5555" }, // minimap legend
        { "map_chest", "#ffc21f" },
        { "map_exit", "#5aff73" },
    };

    // "#rrggbb" for a palette name (for code that builds rich text itself).
    public static string Html(string name) =>
        GameSettings.Colorblind && SafeHex.TryGetValue(name, out var s) ? s : Hex.TryGetValue(name, out var h) ? h : "#ff00ff";

    // 색약 모드 (GameSettings.Colorblind): Okabe-Ito colours that stay apart for red-green colour blindness -
    // enemies orange, the cat sky blue, the exit yellow.
    static readonly Dictionary<string, string> SafeHex = new Dictionary<string, string>
    {
        { "map_player", "#56b4e9" },
        { "map_enemy", "#e69f00" },
        { "map_exit", "#f0e442" },
    };
    public static Color EnemyMark => GameSettings.Colorblind ? new Color(0.9f, 0.62f, 0f) : new Color(0.95f, 0.25f, 0.25f);
    public static Color PlayerMark => GameSettings.Colorblind ? new Color(0.34f, 0.71f, 0.91f) : new Color(1f, 0.85f, 0.3f);
    public static Color ExitMark => GameSettings.Colorblind ? new Color(0.94f, 0.89f, 0.26f) : new Color(0.35f, 1f, 0.45f);

    // Replaces $name (letters, digits, _) with its hex; an unknown name is left as it is.
    public static string ExpandColours(string s)
    {
        if (s.IndexOf('$') < 0) return s;
        var sb = new System.Text.StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '$')
            {
                int j = i + 1;
                while (j < s.Length && (char.IsLetterOrDigit(s[j]) || s[j] == '_')) j++;
                string colour = j > i + 1 ? s.Substring(i + 1, j - i - 1) : null;
                if (colour != null && ((GameSettings.Colorblind && SafeHex.TryGetValue(colour, out var hex)) || Hex.TryGetValue(colour, out hex))) { sb.Append(hex); i = j - 1; continue; }
            }
            sb.Append(s[i]);
        }
        return sb.ToString();
    }

    public static IEnumerable<string> ColourNames => Hex.Keys;

    // ---- Buttons. One rule on every screen: the green "go" sprite (UISkin.buttonRed - the name is the Kenney
    // sheet's) for the screen's single main way forward (계속하기, 강화, 출항하기, 바로 재도전); wood for everything
    // else (back, close, settings, quit); grey for what can't be pressed yet.

    // ---- Type sizes. The pixel font has three crisp steps (PixelText.Dot = 12): small 24, body 36, title 48;
    // sizes asked for in between are snapped to these. Use the names, not new numbers.
    public const float TextSmall = 24f;
    public const float TextLarge = 36f;
    public const float TextTitle = 48f;
    // Banners (StageBanner, StageTitle): band and words come up together in this long, are held fully shown, and
    // go just as fast - no half-faded words over the fight (UI_STYLE.md, Motion).
    public const float BannerFade = 0.15f;
    public const float TextMin = 16f;   // auto-size floors (snapped to 12)
    public const float TextBody = 32f;  // older body size (snaps to 36)
}
