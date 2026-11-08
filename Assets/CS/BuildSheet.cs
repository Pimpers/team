using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Held Tab (pad: View / Select): the run so far on one sheet in the middle of the screen - the cat's numbers on the
// left (attack, hearts, speeds, crit, reach, dashes, jumps), the cards taken on the right (icon, name, level) and the
// sets under them. The game keeps running underneath: it is a glance, like a scoreboard, not a menu.
// Created by RunManager in the stage scenes (not the tutorial); hidden under the pause / draft / result popups.
public class BuildSheet : MonoBehaviour
{
    const float Width = 1120f, Height = 640f, Row = 48f, IconSize = 44f;
    const int RowsPerColumn = 8;

    public static bool ForceShow; // ScreenCaptureTool: the sheet without a key held
    public static bool Showing;   // the sheet is up: SkillHUD steps aside (its bar peeked out under the sheet)
    void OnDestroy() { Showing = false; } // a static outlives the scene (no domain reload on play)

    TMP_FontAsset font;
    CanvasGroup group;
    TextMeshProUGUI setsText, emptyText;
    // A card's art beside each number (attack, hearts, speed, attack speed, crit, reach, dashes, air jumps).
    static readonly string[] StatIcons = { "attack", "heart", "speed", "haste", "crit", "reach", "dash_more", "jump" };
    readonly TextMeshProUGUI[] statRows = new TextMeshProUGUI[StatIcons.Length];
    const float StatRow = 60f;
    RectTransform cardArea;
    string shownKey;
    TextMeshProUGUI moreHint; // "more cards come from clearing stages" under a short list
    int shownLevels = -1;

    public static BuildSheet Create(Canvas canvas, TMP_FontAsset font)
    {
        var go = new GameObject("BuildSheet", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(Width, Height);
        var img = go.GetComponent<Image>();
        UiPanels.Board(img); // the riveted wooden board, like the credits and the result board
        img.raycastTarget = false;
        var sheet = go.AddComponent<BuildSheet>();
        sheet.font = font;
        sheet.group = go.AddComponent<CanvasGroup>();
        sheet.group.blocksRaycasts = false;
        sheet.group.alpha = 0f;
        sheet.Build(rt);
        return sheet;
    }

    TextMeshProUGUI Text(RectTransform parent, string name, float size, TextAlignmentOptions align, Vector2 pos, Vector2 box)
    {
        var t = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        var rt = t.rectTransform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        if (font != null) t.font = font;
        t.fontSize = size;
        t.color = UiTheme.Cream;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        t.outlineWidth = 0.15f;
        t.outlineColor = new Color32(24, 14, 10, 255);
        return t;
    }

    // Board layout: the red ribbon with the title over the top edge; a dark slot on the left with a row per number
    // (its card's icon, the label and the value); a dark slot on the right with the cards taken; a slot for the sets.
    void Build(RectTransform root)
    {
        UiPanels.Ribbon(root, Loc.T("sheet.title"), font, 440f);

        var statsSlot = (RectTransform)UiPanels.Inset(root, "StatsSlot", new Vector2(36f, -64f), new Vector2(400f, 540f)).transform;
        for (int i = 0; i < StatIcons.Length; i++)
        {
            float y = -24f - i * StatRow - StatRow * 0.5f;
            var card = CardPool.Get(StatIcons[i]);
            UiPanels.Icon(statsSlot, card != null ? card.Art : null, new Vector2(42f, y), 44f);
            statRows[i] = Text(statsSlot, "Stat" + i, UiTheme.TextSmall, TextAlignmentOptions.MidlineLeft, new Vector2(78f, y + StatRow * 0.5f), new Vector2(312f, StatRow));
        }

        var cardsSlot = (RectTransform)UiPanels.Inset(root, "CardsSlot", new Vector2(452f, -64f), new Vector2(632f, 456f)).transform;
        var cardsHead = Text(cardsSlot, "CardsHead", UiTheme.TextSmall, TextAlignmentOptions.TopLeft, new Vector2(24f, -16f), new Vector2(620f, 32f));
        cardsHead.text = Loc.T("sheet.cards_head");
        cardsHead.color = UiTheme.Gold;
        var area = new GameObject("Cards", typeof(RectTransform));
        cardArea = (RectTransform)area.transform;
        cardArea.SetParent(cardsSlot, false);
        cardArea.anchorMin = cardArea.anchorMax = cardArea.pivot = new Vector2(0f, 1f);
        cardArea.anchoredPosition = new Vector2(24f, -54f);
        cardArea.sizeDelta = new Vector2(590f, RowsPerColumn * Row);
        emptyText = Text(cardArea, "Empty", UiTheme.TextSmall, TextAlignmentOptions.TopLeft, Vector2.zero, new Vector2(620f, 32f));
        emptyText.text = Loc.T("sheet.no_cards");
        emptyText.color = UiTheme.DisabledText;

        var setsSlot = (RectTransform)UiPanels.Inset(root, "SetsSlot", new Vector2(452f, -532f), new Vector2(632f, 72f)).transform;
        setsText = Text(setsSlot, "Sets", UiTheme.TextSmall, TextAlignmentOptions.MidlineLeft, new Vector2(24f, 0f), new Vector2(590f, 72f));
    }

    void Update()
    {
        var stats = PlayerStats.Instance;
        bool popup = GameClock.Paused || (RunManager.Instance != null && RunManager.Instance.RunEnded);
        bool show = stats != null && !popup && (ForceShow || GameInput.BuildSheetHeld);
        group.alpha = show ? 1f : 0f;
        Showing = show;
        if (!show) return;
        transform.SetAsLastSibling(); // over the HUD bits made after it
        Refresh(stats);
    }

    // The numbers last shown: the stat text is rebuilt only when one changes (not every frame Tab is held).
    int shownStatsHash;

    void Refresh(PlayerStats s)
    {
        var body = s.Body;
        int dashes = body != null ? body.MaxDashes : 2 + s.ExtraDashes;
        int hash = System.HashCode.Combine(s.AttackPower, s.CurrentHP, s.MaxHP, s.MoveSpeed, s.AttackSpeedMultiplier, s.CritChance, s.ReachMultiplier, System.HashCode.Combine(dashes, s.ExtraAirJumps));
        if (hash != shownStatsHash)
        {
            shownStatsHash = hash;
            string all = Loc.F("sheet.stats",
                s.AttackPower,
                s.CurrentHP.ToString("0.#"), s.MaxHP.ToString("0.#"),
                (s.MoveSpeed / s.baseMoveSpeed * 100f).ToString("0"),
                (s.AttackSpeedMultiplier * 100f).ToString("0"),
                (s.CritChance * 100f).ToString("0"),
                (s.ReachMultiplier * 100f).ToString("0"),
                dashes,
                1 + s.ExtraAirJumps);
            var lines = all.Split('\n'); // one line of sheet.stats per row, beside its icon
            for (int i = 0; i < statRows.Length; i++) statRows[i].text = i < lines.Length ? lines[i] : "";
        }

        // The card rows and the set line only change with a pick: rebuilt when the levels do (checked by a count of
        // the levels, which only grows, so the list and key are built only then).
        int levels = 0;
        foreach (var kv in s.CardLevels) levels += kv.Value;
        if (levels == shownLevels) return;
        shownLevels = levels;
        var cards = new List<CardPool.Card>();
        foreach (var id in s.SkillOrder) { var c = CardPool.Get(id); if (c != null) cards.Add(c); }
        foreach (var c in CardPool.Stats) if (s.CardLevel(c.id) > 0) cards.Add(c);
        var key = new System.Text.StringBuilder();
        foreach (var c in cards) key.Append(c.id).Append(s.CardLevel(c.id)).Append(',');
        if (key.ToString() == shownKey) return;
        shownKey = key.ToString();

        for (int i = cardArea.childCount - 1; i >= 0; i--)
            if (cardArea.GetChild(i) != emptyText.transform) Destroy(cardArea.GetChild(i).gameObject);
        emptyText.gameObject.SetActive(cards.Count == 0);
        if (moreHint == null)
        {
            moreHint = Text(cardArea, "MoreHint", UiTheme.TextSmall, TextAlignmentOptions.TopLeft, Vector2.zero, new Vector2(560f, 32f));
            moreHint.text = Loc.T("sheet.more_hint");
            moreHint.color = UiTheme.DisabledText;
        }
        moreHint.gameObject.SetActive(cards.Count > 0 && cards.Count <= 4);
        moreHint.rectTransform.anchoredPosition = new Vector2(0f, -Mathf.Min(cards.Count, RowsPerColumn) * Row - 12f);
        int max = RowsPerColumn * 2;
        for (int i = 0; i < Mathf.Min(cards.Count, max); i++)
        {
            var c = cards[i];
            Vector2 pos = new Vector2(i / RowsPerColumn * 295f, -(i % RowsPerColumn) * Row);
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            icon.rectTransform.SetParent(cardArea, false);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = new Vector2(0f, 1f);
            icon.rectTransform.anchoredPosition = pos;
            icon.rectTransform.sizeDelta = new Vector2(IconSize, IconSize);
            icon.sprite = SkillHUD.Icon(c);
            icon.preserveAspect = true;
            icon.enabled = icon.sprite != null;
            icon.raycastTarget = false;
            int lv = s.CardLevel(c.id);
            var label = Text(cardArea, "Name", UiTheme.TextSmall, TextAlignmentOptions.MidlineLeft, pos + new Vector2(IconSize + 10f, 0f), new Vector2(cards.Count > RowsPerColumn ? 235f : 530f, IconSize)); // one column: the whole width
            label.text = c.maxLevel > 0 ? Loc.F("sheet.card", c.name, lv, c.maxLevel) : c.name;
            label.overflowMode = TextOverflowModes.Ellipsis;
        }

        var sets = new List<string>();
        for (int t = 0; t < CardPool.TagNames.Length; t++)
        {
            int have = s.SetCount((CardPool.Tag)t);
            if (have <= 0) continue;
            int goal = have >= CardPool.SetStep1 ? CardPool.SetStep2 : CardPool.SetStep1;
            sets.Add($"<color={UiTheme.TagOnDarkHex(t)}>#{CardPool.TagNames[t]} {Mathf.Min(have, goal)}/{goal}</color>");
        }
        setsText.text = sets.Count == 0 ? Loc.T("sheet.no_sets") : Loc.F("sheet.sets", string.Join("   ", sets));
    }
}
