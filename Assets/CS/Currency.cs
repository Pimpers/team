// The game's money is shown as a golden fish bone instead of the word "골드".
// `Icon` is a TextMeshPro inline sprite tag; the sprite lives in Assets/UI/GoldSprite.asset
// (made by the editor's CurrencyIcon builder and set as TMP's default sprite asset).
public static class Currency
{
    public const string Icon = "<sprite name=\"gold\">";

    // No space after the icon: the glyph already has a little gap built in, and the
    // monospace font's space would push the number far away.
    public static string Amount(int value) => $"{Icon}{value}";
}
