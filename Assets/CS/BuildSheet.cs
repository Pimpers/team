using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Held Tab (pad: View / Select): the run so far on one sheet in the middle of the screen, as two panels:
//   ① 캐릭터 능력치 - the cat's portrait, its name and job, then its numbers (hearts, attack, speeds, crit, reach, dashes, jumps);
//   ② 장착 스킬     - the active skills on Q / E / R, then the 패시브 효과 (passive skills and the stat cards taken),
//                    and under them the run's stage and the sets reached.
// The game keeps running underneath: it is a glance, like a scoreboard, not a menu.
// Created by RunManager in the stage scenes (not the tutorial); hidden under the pause / draft / result popups.
public class BuildSheet : MonoBehaviour
{
    const float Width = 1120f, Height = 640f;

    public static bool ForceShow; // ScreenCaptureTool: the sheet without a key held
    public static bool Showing;   // the sheet is up: SkillHUD steps aside (its bar peeked out under the sheet)
    void OnDestroy() { Showing = false; } // a static outlives the scene (no domain reload on play)

    TMP_FontAsset font;
    CanvasGroup group;
    TextMeshProUGUI setsText, nameText;
    Image portrait;
    // A card's art beside each number (attack, hearts, speed, attack speed, crit, reach, dashes, air jumps).
    static readonly string[] StatIcons = { "attack", "heart", "speed", "haste", "crit", "reach", "dash_more", "jump" };
    readonly TextMeshProUGUI[] statRows = new TextMeshProUGUI[StatIcons.Length];
    const float StatRow = 38f, StatTop = 226f;

    // ② the three active-skill rows (key chip, icon, name) and the passive list (two columns).
    const int SkillSlots = 3, PassiveRows = 4;
    readonly Image[] skillIcons = new Image[SkillSlots];
    readonly TextMeshProUGUI[] skillNames = new TextMeshProUGUI[SkillSlots];
    RectTransform passiveArea;
    TextMeshProUGUI noPassives;
    string shownKey;
    int shownLevels = -1, shownProgress = -1, shownStatsHash;

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

    // Board layout: the red ribbon with the title over the top edge; a dark slot on the left (① the cat) and one on
    // the right (② its skills); a slot under the right one for the run's stage and sets.
    void Build(RectTransform root)
    {
        UiPanels.Ribbon(root, Loc.T("sheet.title"), font, 440f);

        // ① the cat
        var statsSlot = (RectTransform)UiPanels.Inset(root, "StatsSlot", new Vector2(36f, -64f), new Vector2(400f, 540f)).transform;
        var head = Text(statsSlot, "Head1", UiTheme.TextSmall, TextAlignmentOptions.TopLeft, new Vector2(24f, -16f), new Vector2(352f, 32f));
        head.text = Loc.T("sheet.head_cat");
        head.color = UiTheme.Gold;
        portrait = UiPanels.Icon(statsSlot, null, new Vector2(200f, -112f), 104f);
        nameText = Text(statsSlot, "CatName", UiTheme.TextLarge, TextAlignmentOptions.Center, new Vector2(24f, -170f), new Vector2(352f, 44f));
        for (int i = 0; i < StatIcons.Length; i++)
        {
            float top = -StatTop - i * StatRow;
            var card = CardPool.Get(StatIcons[i]);
            UiPanels.Icon(statsSlot, card != null ? card.Art : null, new Vector2(46f, top - StatRow * 0.5f), 32f);
            statRows[i] = Text(statsSlot, "Stat" + i, UiTheme.TextSmall, TextAlignmentOptions.MidlineLeft, new Vector2(78f, top), new Vector2(312f, StatRow));
        }

        // ② the skills
        var skillsSlot = (RectTransform)UiPanels.Inset(root, "SkillsSlot", new Vector2(452f, -64f), new Vector2(632f, 456f)).transform;
        var head2 = Text(skillsSlot, "Head2", UiTheme.TextSmall, TextAlignmentOptions.TopLeft, new Vector2(24f, -14f), new Vector2(584f, 32f));
        head2.text = Loc.T("sheet.head_skills");
        head2.color = UiTheme.Gold;
        var sub = Text(skillsSlot, "Sub2", UiTheme.TextSmall, TextAlignmentOptions.TopLeft, new Vector2(24f, -46f), new Vector2(584f, 32f));
        sub.text = Loc.T("sheet.skills_sub");
        sub.color = UiTheme.DisabledText;
        var keys = new[] { GameInput.Action.Skill1, GameInput.Action.Skill2, GameInput.Action.Skill3 };
        for (int i = 0; i < SkillSlots; i++)
        {
            float top = -84f - i * 58f;
            var chip = new GameObject("KeyChip", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var crt = chip.rectTransform;
            crt.SetParent(skillsSlot, false);
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0f, 1f);
            crt.anchoredPosition = new Vector2(24f, top);
            crt.sizeDelta = new Vector2(64f, 48f);
            chip.raycastTarget = false;
            if (BookUI.Get("Slot01b") != null) BookUI.Slice(chip, "Slot01b", 2f, new Color(1f, 1f, 1f, 0.9f));
            else chip.color = new Color(0.25f, 0.16f, 0.12f, 1f);
            var k = Text(crt, "Key", UiTheme.TextSmall, TextAlignmentOptions.Center, Vector2.zero, crt.sizeDelta);
            k.text = GameInput.BindingName(keys[i]);
            k.color = UiTheme.Gold;
            skillIcons[i] = UiPanels.Icon(skillsSlot, null, new Vector2(124f, top - 24f), 44f);
            skillNames[i] = Text(skillsSlot, "Skill" + i, UiTheme.TextSmall, TextAlignmentOptions.MidlineLeft, new Vector2(160f, top), new Vector2(450f, 48f));
        }
        var head3 = Text(skillsSlot, "Head3", UiTheme.TextSmall, TextAlignmentOptions.TopLeft, new Vector2(24f, -270f), new Vector2(584f, 32f));
        head3.text = Loc.T("sheet.head_passive");
        head3.color = UiTheme.Gold;
        var area = new GameObject("Passives", typeof(RectTransform));
        passiveArea = (RectTransform)area.transform;
        passiveArea.SetParent(skillsSlot, false);
        passiveArea.anchorMin = passiveArea.anchorMax = passiveArea.pivot = new Vector2(0f, 1f);
        passiveArea.anchoredPosition = new Vector2(24f, -308f);
        passiveArea.sizeDelta = new Vector2(584f, PassiveRows * 34f);
        noPassives = Text(passiveArea, "None", UiTheme.TextSmall, TextAlignmentOptions.TopLeft, Vector2.zero, new Vector2(584f, 32f));
        noPassives.text = Loc.T("sheet.no_cards");
        noPassives.color = UiTheme.DisabledText;

        // the run: stage and kills, then the sets reached
        var setsSlot = (RectTransform)UiPanels.Inset(root, "SetsSlot", new Vector2(452f, -532f), new Vector2(632f, 72f)).transform;
        setsText = Text(setsSlot, "Sets", UiTheme.TextSmall, TextAlignmentOptions.MidlineLeft, new Vector2(24f, 0f), new Vector2(590f, 72f));
        setsText.textWrappingMode = TextWrappingModes.Normal; // four or five sets on one line ran off the board
        setsText.enableAutoSizing = true;
        setsText.fontSizeMax = UiTheme.TextSmall;
        setsText.fontSizeMin = 14f;

        // This run's 타락도 rules on a line under the board: they were shown only on the world map, and a rule like
        // the death burst is forgotten by stage 3.
        string rules = Corruption.Rules(Corruption.Level);
        if (rules.Length > 0)
        {
            var line = Text(root, "CorruptionRules", UiTheme.TextSmall, TextAlignmentOptions.Center, new Vector2(0f, -Height - 10f), new Vector2(Width, 36f));
            line.text = Loc.F("sheet.corruption", Corruption.Level, rules);
            line.enableAutoSizing = true; // five rules at tier 5: shrinks rather than runs past the board
            line.fontSizeMax = UiTheme.TextSmall;
            line.fontSizeMin = PixelText.Dot;
            UiTheme.Backdrop(line);
        }
    }

    void Update()
    {
        var stats = PlayerStats.Instance;
        bool popup = GameClock.Paused; // (not RunEnded: after the boss the cat still walks to the log, and Tab then showed only the minimap's legend)
        bool show = stats != null && !popup && (ForceShow || GameInput.BuildSheetHeld);
        group.alpha = show ? 1f : 0f;
        Showing = show;
        if (!show) return;
        transform.SetAsLastSibling(); // over the HUD bits made after it
        Refresh(stats);
    }

    void Refresh(PlayerStats s)
    {
        var body = s.Body;
        // ① the cat: portrait and "name · job" (set once - they do not change during a run)
        if (nameText != null && string.IsNullOrEmpty(nameText.text))
        {
            var def = body != null ? body.character : null;
            if (def != null && def.idleFrames != null && def.idleFrames.Length > 0) { portrait.sprite = def.idleFrames[0]; portrait.enabled = portrait.sprite != null; }
            string given = CharacterSelection.SelectedName;
            string job = def != null ? (!string.IsNullOrEmpty(def.jobName) ? def.jobName : def.displayName) : "";
            string who = !string.IsNullOrEmpty(given) ? given : def != null ? def.tagName : "";
            nameText.text = string.IsNullOrEmpty(job) ? who : $"{who} · {job}";
        }

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

        // ② the skills and passives only change with a pick: rebuilt when the levels do (a count of the levels,
        // which only grows, so the lists are built only then).
        int progressKey = RunStats.Stage * 100000 + RunStats.Kills; // the line's text is built only when one of them changes
        int levels = 0;
        foreach (var kv in s.CardLevels) levels += kv.Value;
        if (levels == shownLevels && progressKey == shownProgress) return;
        shownLevels = levels;
        shownProgress = progressKey;
        string progress = Loc.F("sheet.progress", Mathf.Max(1, RunStats.Stage), RunManager.Instance != null ? RunManager.Instance.totalStages : 5, RunStats.Kills);

        var actives = new List<CardPool.Card>();
        var passives = new List<CardPool.Card>();
        foreach (var id in s.SkillOrder)
        {
            var c = CardPool.Get(id);
            if (c == null) continue;
            (c.kind == CardPool.Kind.Active ? actives : passives).Add(c);
        }
        foreach (var c in CardPool.Stats) if (s.CardLevel(c.id) > 0) passives.Add(c);
        var key = new System.Text.StringBuilder();
        foreach (var c in actives) key.Append(c.id).Append(s.CardLevel(c.id)).Append(',');
        key.Append('|');
        foreach (var c in passives) key.Append(c.id).Append(s.CardLevel(c.id)).Append(',');
        if (key.ToString() != shownKey)
        {
            shownKey = key.ToString();
            for (int i = 0; i < SkillSlots; i++)
            {
                var c = i < actives.Count ? actives[i] : null;
                skillIcons[i].sprite = c != null ? SkillHUD.Icon(c) : null;
                skillIcons[i].enabled = skillIcons[i].sprite != null;
                skillNames[i].text = c == null ? Loc.T("sheet.slot_empty") : CardLine(c, s.CardLevel(c.id));
                skillNames[i].color = c == null ? UiTheme.DisabledText : UiTheme.Cream;
            }

            for (int i = passiveArea.childCount - 1; i >= 0; i--)
                if (passiveArea.GetChild(i) != noPassives.transform) Destroy(passiveArea.GetChild(i).gameObject);
            noPassives.gameObject.SetActive(passives.Count == 0);
            // Eight places: with more passives than that, the last place says how many are not listed.
            int places = Mathf.Min(passives.Count, PassiveRows * 2);
            for (int i = 0; i < places; i++)
            {
                var c = passives[i];
                var t = Text(passiveArea, "Passive", UiTheme.TextSmall, TextAlignmentOptions.MidlineLeft, new Vector2(i / PassiveRows * 296f, -(i % PassiveRows) * 34f), new Vector2(288f, 34f));
                bool rest = passives.Count > places && i == places - 1;
                t.text = rest ? Loc.F("sheet.more", passives.Count - places + 1) : CardLine(c, s.CardLevel(c.id));
                t.overflowMode = TextOverflowModes.Ellipsis;
                t.enableAutoSizing = true; // a long name shrinks (to 12, PixelText) instead of ending in …
                t.fontSizeMax = UiTheme.TextSmall;
                t.fontSizeMin = PixelText.Dot;
            }
        }

        var sets = new List<string>();
        for (int t = 0; t < CardPool.TagNames.Length; t++)
        {
            int have = s.SetCount((CardPool.Tag)t);
            if (have <= 0) continue;
            int goal = have >= CardPool.SetStep1 ? CardPool.SetStep2 : CardPool.SetStep1;
            sets.Add($"<color={UiTheme.TagOnDarkHex(t)}>#{CardPool.TagNames[t]} {Mathf.Min(have, goal)}/{goal}</color>");
        }
        setsText.text = progress + "\n" + (sets.Count == 0 ? Loc.T("sheet.no_sets") : Loc.F("sheet.sets", string.Join("   ", sets)));
    }

    static string CardLine(CardPool.Card c, int lv) => c.maxLevel > 0 ? Loc.F("sheet.card", c.name, lv, c.maxLevel) : c.name;
}
