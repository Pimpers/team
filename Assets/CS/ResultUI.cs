using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Shown when a run ends (boss defeated or player died). The run's gold has already been
// added to the saved wallet (MetaProgress) by RunManager; permanent upgrades are bought
// at the blacksmith in the harbor village. Besides the gold, the board looks back on the run (RunStats):
// how far it got, the play time, monsters and elites felled, damage dealt, what knocked the cat out, and the
// build - the cards taken and the sets they made. The old in-panel upgrade buttons are hidden.
public class ResultUI : MonoBehaviour
{
    public GameObject panel;
    public TMP_Text resultTitleText;
    public TMP_Text goldText;
    public TMP_Text attackButtonLabel;
    public TMP_Text speedButtonLabel;
    public TMP_Text hpButtonLabel;

    [Tooltip("Scene a new run starts from.")]
    public string retrySceneName = "MainScene";
    [Tooltip("\"바로 재도전\" starts a new run here, skipping the village.")]
    public string quickRetrySceneName = "Stage1";

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        AddQuickRetry();
        HangBoard();
    }

    // The result is a wooden notice board with iron corner studs (ToffeeCraft's medieval board, Resources/UIBundle): the
    // quest is done - or not - and the board says so. 6 canvas units per art pixel; light text on the wood. (Its chain is
    // left off: drawn for the board's own width, it can't reach the corners of a board stretched to the panel.)
    void HangBoard()
    {
        var img = panel != null ? panel.GetComponent<Image>() : null;
        if (img == null || BookUI.Get("UIBundle/MedievalBoard") == null) return;
        BookUI.Slice(img, "UIBundle/MedievalBoard", 6f);
        // The gold lines sit on the board's orange planks (light ground), so dark ink rather than the banner's cream.
        if (goldText != null) goldText.color = (Color)new Color32(0x3a, 0x1c, 0x0c, 0xff);
        boardStyled = true;
    }
    bool boardStyled;

    // A defeat greys the world out: a grey veil over the picture and under the board (which keeps its colours).
    // shortcut: a veil that mixes the picture toward grey, not true greyscale (that would need a post-process volume);
    // upgrade if the 2D renderer's post-processing is enabled.
    void GrayBehindBoard()
    {
        var parent = panel != null ? panel.transform.parent : null;
        if (parent == null || parent.Find("DeathGray") != null) return;
        var go = new GameObject("DeathGray", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.SetSiblingIndex(panel.transform.GetSiblingIndex()); // just under the board
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-4000f, -4000f); rt.offsetMax = new Vector2(4000f, 4000f); // any aspect ratio
        var img = go.GetComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.22f, 0.84f); // a dark grey: the picture is left dim and colourless
        img.raycastTarget = false;
    }

    // The outline needs the text set up, which an inactive panel's text isn't in Awake: done on Show.
    void OutlineOnBoard()
    {
        if (!boardStyled || goldText == null) return;
        goldText.outlineWidth = 0.22f;
        goldText.outlineColor = new Color32(40, 20, 12, 255);
    }

    // Two buttons side by side: "마을로 돌아가기" (wood) and "바로 재도전" (red, a copy of it
    // wired to QuickRetry) - so a retry when there's nothing to buy is one click.
    void AddQuickRetry()
    {
        var village = panel != null ? panel.transform.Find("RetryButton") as RectTransform : null;
        if (village == null || panel.transform.Find("QuickRetryButton") != null) return;

        var copy = Instantiate(village.gameObject, village.parent);
        copy.name = "QuickRetryButton";
        var btn = copy.GetComponent<Button>();
        btn.onClick = new Button.ButtonClickedEvent(); // drop the copied "go to village" call
        btn.onClick.AddListener(QuickRetry);
        foreach (var label in copy.GetComponentsInChildren<TMP_Text>(true)) label.text = Loc.T("result.retry");

        var rt = (RectTransform)copy.transform;
        Vector2 size = new Vector2(360f, village.sizeDelta.y);
        village.sizeDelta = rt.sizeDelta = size;
        village.anchoredPosition = new Vector2(-195f, village.anchoredPosition.y);
        rt.anchoredPosition = new Vector2(195f, village.anchoredPosition.y);

        var skin = UISkin.Instance;
        if (skin != null) UISkin.Apply(village.GetComponent<Image>(), skin.buttonWood, UISkin.SmallScale);
        foreach (var label in village.GetComponentsInChildren<TMP_Text>(true)) label.fontSize = Mathf.Min(label.fontSize, UISkin.FontButton);
        foreach (var label in copy.GetComponentsInChildren<TMP_Text>(true)) label.fontSize = Mathf.Min(label.fontSize, UISkin.FontButton);
        // Green: the retry is this screen's main way on (UiTheme's button rule); the trip back to the village is wood.
        if (skin != null) UISkin.Apply(copy.GetComponent<Image>(), skin.buttonRed, UISkin.SmallScale); // re-centres its label like the village one
        BookUI.ButtonIcon(village.GetComponent<Button>(), "IconHome01a");
        BookUI.ButtonIcon(btn, "IconRestart01a");
        ShareLabelSize(village, copy.transform);
        GameInput.SelectForPad(btn); // pad: A retries, d-pad left for the village
    }

    // One size for both button labels: the largest pixel-grid size (PixelText.Dot) at which every label still fits.
    // Auto-sized on their own, the short "바로 재도전" came out far bigger than "마을로 돌아가기" beside it.
    static void ShareLabelSize(params Transform[] buttons)
    {
        var labels = new System.Collections.Generic.List<TMP_Text>();
        foreach (var b in buttons) labels.AddRange(b.GetComponentsInChildren<TMP_Text>(true));
        float size = Mathf.Floor(UISkin.FontButton / PixelText.Dot) * PixelText.Dot;
        for (; size > PixelText.Dot; size -= PixelText.Dot)
        {
            bool fits = true;
            foreach (var l in labels)
            {
                l.enableAutoSizing = false;
                l.fontSize = size;
                if (l.GetPreferredValues(l.text).x > l.rectTransform.rect.width) { fits = false; break; }
            }
            if (fits) break;
        }
        foreach (var l in labels) { l.enableAutoSizing = false; l.fontSize = size; }
    }

    // The fight HUD goes with the fight: the skill bar and its 패시브 label sat on the board (and over its
    // 마을로 돌아가기 button), the hearts, map and counters showed round it. The run is over, nothing brings them back.
    static void HideRunHud()
    {
        var kinds = new[] { typeof(SkillHUD), typeof(HeartHUD), typeof(DashBar), typeof(StageMinimap), typeof(MonsterTracker), typeof(SetHUD), typeof(StageHudExtras) };
        foreach (var kind in kinds)
            foreach (var o in Object.FindObjectsByType(kind)) ((Component)o).gameObject.SetActive(false);
        var run = RunManager.Instance;
        if (run != null && run.hudText != null)
        {
            run.hudText.gameObject.SetActive(false);
            var backing = run.hudText.transform.parent != null ? run.hudText.transform.parent.Find("HudBacking") : null;
            if (backing != null) backing.gameObject.SetActive(false);
        }
    }

    // unlockedCorruption: the 타락도 tier this win just opened (-1 = none).
    public void Show(bool win, int goldEarned, int unlockedCorruption = -1)
    {
        StageBanner.ClearAll();
        HideRunHud();
        if (panel != null) panel.SetActive(true);
        if (!win) GrayBehindBoard();
        OutlineOnBoard();
        BuildSummary(win);
        if (resultTitleText != null)
        {
            resultTitleText.text = win ? Loc.T("result.win") : Loc.T("result.lose");
            resultTitleText.enableAutoSizing = true;
            resultTitleText.fontSizeMax = UISkin.FontTitle;
            resultTitleText.fontSizeMin = UISkin.FontBody;
            resultTitleText.textWrappingMode = TextWrappingModes.NoWrap;
            resultTitleText.overflowMode = TextOverflowModes.Truncate;
        }
        ApplyCrest(win);
        if (goldText != null)
        {
            // Two lines at most: a third ran into the buttons, so a newly opened tier takes the hint's place.
            string news = Unlocks(win, goldEarned, unlockedCorruption);
            goldText.text = Loc.F("result.gold", Currency.Icon, goldEarned, MetaProgress.Gold) +
                            (news.Length > 0 ? Loc.F("result.news", news) : Loc.T("result.gold_hint"));
            NewBest = false;
            // The fish-bone icon is taller than the text; grow the box so the two lines fit.
            // 36 at most (it was 48 and ran past the board's sides), shrinking to 24 inside a box the board's width.
            var rt = goldText.rectTransform;
            goldText.enableAutoSizing = true;
            goldText.fontSizeMax = UiTheme.TextLarge;
            goldText.fontSizeMin = UiTheme.TextSmall;
            goldText.textWrappingMode = TextWrappingModes.NoWrap;
            rt.sizeDelta = new Vector2(1000f, 150f); // the gold line and up to three news lines
        }

        // Upgrades moved to the village blacksmith.
        foreach (var label in new[] { attackButtonLabel, speedButtonLabel, hpButtonLabel })
            if (label != null && label.transform.parent != null) label.transform.parent.gameObject.SetActive(false);
    }

    // The laurel plate (Resources/UICats/ui_result_win gold / ui_result_lose grey) behind the title: its ribbon takes the
    // place of the wooden title plate, and the rest of the plate shows only round the title, the record sheets and the
    // build sheet being drawn over it. x6 (756 x 336); the ribbon's middle sits 14 art px under the plate's top.
    void ApplyCrest(bool win)
    {
        var board = panel != null ? panel.transform as RectTransform : null;
        var banner = board != null ? board.Find("ResultBanner") as RectTransform : null;
        var art = Resources.Load<Sprite>(win ? "UICats/ui_result_win" : "UICats/ui_result_lose");
        if (banner == null || art == null) return;
        var crestT = board.Find("ResultCrest") as RectTransform;
        if (crestT == null)
        {
            var go = new GameObject("ResultCrest", typeof(RectTransform), typeof(Image));
            crestT = (RectTransform)go.transform;
            crestT.SetParent(board, false);
            crestT.SetSiblingIndex(0); // behind the sheets, the buttons and the title
        }
        crestT.anchorMin = banner.anchorMin; crestT.anchorMax = banner.anchorMax; crestT.pivot = new Vector2(0.5f, 0.5f);
        crestT.sizeDelta = art.rect.size * 6f; // x6: the ribbon (58 art px wide) then holds the title at 36
        crestT.anchoredPosition = banner.anchoredPosition + new Vector2(0f, -14f * 6f);
        var img = crestT.GetComponent<Image>();
        img.sprite = art;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        img.raycastTarget = false;
        var plank = banner.GetComponent<Image>();
        if (plank != null) plank.enabled = false; // (its title text child stays)
        if (resultTitleText != null) resultTitleText.color = (Color)new Color32(0x3a, 0x1c, 0x0c, 0xff); // dark ink on the cream ribbon
    }

    // Set by RunManager when a win beats the best 타락도 cleared so far.
    public static bool NewBest;

    // What this run opened up, on one line: a new 타락도 tier, the first win / a new best, and the blacksmith
    // upgrades the gold just brought within reach (affordable now, not before the run).
    static string Unlocks(bool win, int goldEarned, int unlockedCorruption)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (unlockedCorruption > 0) parts.Add(Loc.F("result.unlock.corruption", unlockedCorruption));
        if (win && GameSave.Data.wins == 1) parts.Add(Loc.T("result.unlock.first_win"));
        else if (win && NewBest) parts.Add(Loc.T("result.unlock.best"));
        if (win) parts.Add(Loc.T("result.unlock.next_region")); // the boss beaten: what the map's "?" is
        int before = MetaProgress.Gold - goldEarned, ready = 0;
        foreach (MetaProgress.Upgrade u in System.Enum.GetValues(typeof(MetaProgress.Upgrade)))
            if (!MetaProgress.IsMaxed(u) && MetaProgress.Gold >= MetaProgress.Cost(u) && before < MetaProgress.Cost(u)) ready++;
        if (ready > 0) parts.Add(Loc.F("result.unlock.smith", ready));
        // one line each (at most three): run together they shrank to fit the board's width
        if (parts.Count > 3) parts.RemoveRange(3, parts.Count - 3);
        return string.Join("\n", parts);
    }

    // ---------------------------------------------------------------- the run, looked back on
    TextMeshProUGUI statsText, buildText;

    // The board grows to make room: banner on top, the record (left) and the build (right), the gold, the buttons.
    void BuildSummary(bool win)
    {
        if (panel == null) return;
        var board = (RectTransform)panel.transform;
        var font = goldText != null ? goldText.font : null;
        if (statsText == null)
        {
            statsText = Column(board, "RunRecord", new Vector2(-275f, 300f), font);
            buildText = Column(board, "RunBuild", new Vector2(275f, 300f), font);
        }

        int total = RunManager.Instance != null ? RunManager.Instance.totalStages : 5;
        string reached = win ? Loc.T("result.boss_down") : Loc.F("result.stage_of", Mathf.Max(1, RunStats.Stage), total);
        string cause = !win && !string.IsNullOrEmpty(RunStats.LastHitBy) ? Loc.F("result.last_hit", RunStats.LastHitBy) : "";
        string worst = !win && !string.IsNullOrEmpty(RunStats.MostDamageSource) ? Loc.F("result.worst_enemy", RunStats.MostDamageSource) : "";
        // A loss gets advice; a win the next goal (the next 타락도 tier, or that every tier is beaten).
        string goal = Corruption.Level < Corruption.MaxLevel ? Loc.F("result.next_goal", Mathf.Max(0, Corruption.Level) + 1) : Loc.T("result.all_clear");
        string tip = Loc.F("result.next_tip", win ? goal : RunStats.DefeatTip);
        string reroll = RunStats.Rerolls > 0 ? Loc.F("result.rerolls", RunStats.Rerolls, Currency.Icon, RunStats.GoldSpent) : "";
        statsText.text =
            Loc.T("result.title") +
            Loc.F("result.reached", reached) +
            Loc.F("result.time", RunStats.TimeText) +
            Loc.F("result.kills", RunStats.Kills) + (RunStats.EliteKills > 0 ? Loc.F("result.elite_kills", RunStats.EliteKills) : "") + "\n" +
            Loc.F("result.damage_dealt", RunStats.DamageDealt) +
            Loc.F("result.damage_taken", RunStats.DamageTaken) + cause + worst + tip + reroll;
        buildText.text = BuildLines();
        // 24 throughout: at 32 (which the pixel font draws as 36) the record ran out of its sheet.
        buildText.fontSize = UiTheme.TextSmall;
        statsText.fontSize = UiTheme.TextSmall;
        statsText.lineSpacing = 2f;

        // The two sheets are as tall as the longer record needs (300 to 380), and the board shrinks with them: the top
        // half of the board moves down and the bottom half up by half the saving, so nothing else changes its spacing.
        float need = statsText.GetPreferredValues(statsText.text, 476f, 0f).y + 40f; // (the sheet's 520 less its 22 px insets)
        float sheetH = Mathf.Clamp(need, 300f, 380f);
        float d = 380f - sheetH;
        foreach (var sheet in new[] { (RectTransform)statsText.transform.parent, (RectTransform)buildText.transform.parent })
        {
            sheet.sizeDelta = new Vector2(520f, sheetH);
            sheet.anchoredPosition = new Vector2(sheet.anchoredPosition.x, 300f - d * 0.5f);
        }
        board.sizeDelta = new Vector2(1160f, 900f - d);
        Place(board.Find("ResultBanner"), 385f - d * 0.5f);
        if (goldText != null) goldText.rectTransform.anchoredPosition = new Vector2(0f, -205f + d * 0.5f);
        foreach (var name in new[] { "RetryButton", "QuickRetryButton" })
        {
            var b = board.Find(name) as RectTransform;
            if (b != null) b.anchoredPosition = new Vector2(b.anchoredPosition.x, -350f + d * 0.5f);
        }
        BuildIcons((RectTransform)buildText.transform.parent, buildText); // (after the size: the strip sits right under the text)
    }

    // The build, short: how many cards and the sets reached. The cards themselves are the icon strip below, and the
    // full list is the Tab sheet during the run (BuildSheet).
    static string BuildLines()
    {
        var stats = PlayerStats.Instance;
        var sb = new System.Text.StringBuilder(Loc.T("result.build"));
        if (stats == null) return sb.ToString();
        int lines = stats.SkillOrder.Count;
        foreach (var c in CardPool.Stats) if (stats.CardLevel(c.id) > 0) lines++;
        sb.Append(lines == 0 ? Loc.T("result.no_cards") : Loc.F("result.card_count", lines));
        var sets = new System.Collections.Generic.List<string>();
        for (int t = 0; t < CardPool.TagNames.Length; t++)
        {
            int lv = stats.SetLv((CardPool.Tag)t);
            // (<nobr>: with five sets the line broke inside one - "사냥 / 세트")
            if (lv > 0) sets.Add("<nobr>" + (lv >= 2 ? Loc.F("result.set_complete", CardPool.TagNames[t]) : Loc.F("result.set", CardPool.TagNames[t])) + "</nobr>");
        }
        if (sets.Count > 0) sb.Append("<color=" + UiTheme.Html("tip") + ">").Append(string.Join(" · ", sets)).Append("</color>");
        return sb.ToString();
    }

    // The build at a glance: every card taken as its icon, along the bottom of the build sheet (skills in the order
    // taken, then the stat cards). The text above makes room for the strip.
    static void BuildIcons(RectTransform sheet, TextMeshProUGUI text)
    {
        const int MaxRows = 2;
        var old = sheet.Find("Icons");
        if (old != null) Destroy(old.gameObject);
        text.rectTransform.offsetMin = new Vector2(22f, 16f);
        var stats = PlayerStats.Instance;
        if (stats == null) return;
        var cards = new System.Collections.Generic.List<CardPool.Card>();
        foreach (var id in stats.SkillOrder) { var c = CardPool.Get(id); if (c != null) cards.Add(c); }
        foreach (var c in CardPool.Stats) if (stats.CardLevel(c.id) > 0) cards.Add(c);
        if (cards.Count == 0) return;

        // One strip 448 wide: a short build gets big icons (a few small ones left the sheet empty), a long one
        // eight a row; past two full rows the last cell says how many more.
        int perRow = cards.Count <= 5 ? 5 : cards.Count <= 14 ? 7 : 8;
        float Cell = 448f / perRow, IconSize = Cell - 10f;
        int PerRow = perRow;
        int rows = Mathf.Min(MaxRows, (cards.Count + PerRow - 1) / PerRow);
        int shown = Mathf.Min(cards.Count, rows * PerRow);
        int more = cards.Count - shown;
        if (more > 0) { shown--; more++; } // the last cell becomes "+N"
        var strip = new GameObject("Icons", typeof(RectTransform));
        var srt = (RectTransform)strip.transform;
        srt.SetParent(sheet, false);
        // Right under the text, not pinned to the sheet's bottom: a short build left a hole between them.
        float textH = text.GetPreferredValues(text.text, 476f, 0f).y;
        float maxTop = sheet.sizeDelta.y - rows * Cell - 16f; // never lower than the bottom edge it used to sit on
        srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0.5f, 1f);
        srt.anchoredPosition = new Vector2(0f, -Mathf.Min(textH + 36f, maxTop));
        srt.sizeDelta = new Vector2(PerRow * Cell, rows * Cell);
        for (int i = 0; i < shown + (more > 0 ? 1 : 0); i++)
        {
            int row = i / PerRow, col = i % PerRow;
            var slot = new GameObject("Slot", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)slot.transform;
            rt.SetParent(srt, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(Cell * (col + 0.5f), -Cell * (row + 0.5f));
            rt.sizeDelta = new Vector2(Cell - 6f, Cell - 6f);
            var bg = slot.GetComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.08f);
            bg.raycastTarget = false;
            if (i == shown)
            {
                var plus = new GameObject("More", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                plus.rectTransform.SetParent(rt, false);
                plus.rectTransform.sizeDelta = rt.sizeDelta;
                plus.font = text.font;
                plus.fontSize = UISkin.FontSmall;
                plus.alignment = TextAlignmentOptions.Center;
                plus.color = UiTheme.Cream;
                plus.raycastTarget = false;
                plus.text = "+" + more;
                continue;
            }
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            icon.rectTransform.SetParent(rt, false);
            icon.rectTransform.sizeDelta = new Vector2(IconSize, IconSize);
            icon.sprite = SkillHUD.Icon(cards[i]);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = icon.sprite != null;
        }
        text.rectTransform.offsetMin = new Vector2(22f, 16f + rows * Cell + 8f);
    }

    static void Place(Transform t, float y)
    {
        if (t is RectTransform rt) rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
    }

    // A text column on a dark sheet (the board's light wood behind light text would not read).
    static TextMeshProUGUI Column(RectTransform board, string name, Vector2 topCentre, TMP_FontAsset font)
    {
        var sheet = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)sheet.transform;
        rt.SetParent(board, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = topCentre;
        rt.sizeDelta = new Vector2(520f, 380f);
        var img = sheet.GetComponent<Image>();
        // A dark inset slot sunk into the board (UiPanels), not a flat dark rectangle laid on it.
        if (BookUI.Get("Slot01a") != null) BookUI.Slice(img, "Slot01a", UiPanels.InsetScale, new Color(1f, 1f, 1f, 0.96f));
        else img.color = new Color(0.09f, 0.06f, 0.07f, 0.9f);
        img.raycastTarget = false;
        var t = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.rectTransform.SetParent(rt, false);
        t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
        t.rectTransform.offsetMin = new Vector2(22f, 16f); t.rectTransform.offsetMax = new Vector2(-22f, -16f);
        if (font != null) t.font = font;
        t.fontSize = UiTheme.TextBody; // 32 / 48 / 16 px only (the pixel font's 16 dots); a long build drops to 16 (BuildSummary)
        t.enableAutoSizing = false;
        t.lineSpacing = 8f;
        t.alignment = TextAlignmentOptions.TopLeft;
        t.color = UiTheme.Cream;
        t.raycastTarget = false;
        t.outlineWidth = 0.15f;
        t.outlineColor = new Color32(24, 14, 10, 255);
        return t;
    }

    // Still referenced by the (now hidden) upgrade buttons' OnClick in the scenes.
    public void BuyAttack() { }
    public void BuySpeed() { }
    public void BuyHP() { }

    public void RetryRun()
    {
        SceneLoader.Load(retrySceneName);
    }

    public void QuickRetry()
    {
        SceneLoader.Load(quickRetrySceneName);
    }
}
