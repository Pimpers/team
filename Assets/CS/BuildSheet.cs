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
    const float Width = 1120f, Height = 640f, Row = 52f, IconSize = 44f;
    const int RowsPerColumn = 8;

    public static bool ForceShow; // ScreenCaptureTool: the sheet without a key held

    TMP_FontAsset font;
    CanvasGroup group;
    TextMeshProUGUI statsText, setsText, emptyText;
    RectTransform cardArea;
    string shownKey;
    int shownLevels = -1;

    public static BuildSheet Create(Canvas canvas, TMP_FontAsset font)
    {
        var go = new GameObject("BuildSheet", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(Width, Height);
        var img = go.GetComponent<Image>();
        img.color = UiTheme.Plate;
        if (UISkin.Instance != null) UISkin.Apply(img, UISkin.Instance.slate, 4f, Color.white);
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

    void Build(RectTransform root)
    {
        var title = Text(root, "Title", UiTheme.TextLarge, TextAlignmentOptions.Center, new Vector2(0f, -22f), new Vector2(Width, 48f));
        title.text = Loc.T("sheet.title");
        title.color = UiTheme.Gold;

        var statsHead = Text(root, "StatsHead", UiTheme.TextSmall, TextAlignmentOptions.TopLeft, new Vector2(48f, -92f), new Vector2(340f, 32f));
        statsHead.text = Loc.T("sheet.stats_head");
        statsHead.color = UiTheme.Gold;
        statsText = Text(root, "Stats", UiTheme.TextSmall, TextAlignmentOptions.TopLeft, new Vector2(48f, -134f), new Vector2(340f, 440f));
        statsText.lineSpacing = 18f;

        var cardsHead = Text(root, "CardsHead", UiTheme.TextSmall, TextAlignmentOptions.TopLeft, new Vector2(420f, -92f), new Vector2(640f, 32f));
        cardsHead.text = Loc.T("sheet.cards_head");
        cardsHead.color = UiTheme.Gold;
        var area = new GameObject("Cards", typeof(RectTransform));
        cardArea = (RectTransform)area.transform;
        cardArea.SetParent(root, false);
        cardArea.anchorMin = cardArea.anchorMax = cardArea.pivot = new Vector2(0f, 1f);
        cardArea.anchoredPosition = new Vector2(420f, -132f);
        cardArea.sizeDelta = new Vector2(660f, RowsPerColumn * Row);
        emptyText = Text(cardArea, "Empty", UiTheme.TextSmall, TextAlignmentOptions.TopLeft, Vector2.zero, new Vector2(640f, 32f));
        emptyText.text = Loc.T("sheet.no_cards");
        emptyText.color = UiTheme.DisabledText;

        setsText = Text(root, "Sets", UiTheme.TextSmall, TextAlignmentOptions.TopLeft, new Vector2(48f, -Height + 84f), new Vector2(Width - 96f, 32f));
    }

    void Update()
    {
        var stats = PlayerStats.Instance;
        bool popup = GameClock.Paused || (RunManager.Instance != null && RunManager.Instance.RunEnded);
        bool show = stats != null && !popup && (ForceShow || GameInput.BuildSheetHeld);
        group.alpha = show ? 1f : 0f;
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
            statsText.text = Loc.F("sheet.stats",
                s.AttackPower,
                s.CurrentHP.ToString("0.#"), s.MaxHP.ToString("0.#"),
                (s.MoveSpeed / s.baseMoveSpeed * 100f).ToString("0"),
                (s.AttackSpeedMultiplier * 100f).ToString("0"),
                (s.CritChance * 100f).ToString("0"),
                (s.ReachMultiplier * 100f).ToString("0"),
                dashes,
                1 + s.ExtraAirJumps);
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
        int max = RowsPerColumn * 2;
        for (int i = 0; i < Mathf.Min(cards.Count, max); i++)
        {
            var c = cards[i];
            Vector2 pos = new Vector2(i / RowsPerColumn * 330f, -(i % RowsPerColumn) * Row);
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
            var label = Text(cardArea, "Name", UiTheme.TextSmall, TextAlignmentOptions.MidlineLeft, pos + new Vector2(IconSize + 10f, 0f), new Vector2(cards.Count > RowsPerColumn ? 270f : 590f, IconSize)); // one column: the whole width
            label.text = c.maxLevel > 0 ? Loc.F("sheet.card", c.name, lv, c.maxLevel) : c.name;
            label.overflowMode = TextOverflowModes.Ellipsis;
        }

        var sets = new List<string>();
        for (int t = 0; t < CardPool.TagNames.Length; t++)
        {
            int have = s.SetCount((CardPool.Tag)t);
            if (have <= 0) continue;
            int goal = have >= CardPool.SetStep1 ? CardPool.SetStep2 : CardPool.SetStep1;
            sets.Add($"<color={CardPool.TagColors[t]}>#{CardPool.TagNames[t]} {Mathf.Min(have, goal)}/{goal}</color>");
        }
        setsText.text = sets.Count == 0 ? Loc.T("sheet.no_sets") : Loc.F("sheet.sets", string.Join("   ", sets));
    }
}
