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
        if (goldText != null) goldText.color = UISkin.BannerText;
        boardStyled = true;
    }
    bool boardStyled;

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

    // unlockedCorruption: the 타락도 tier this win just opened (-1 = none).
    public void Show(bool win, int goldEarned, int unlockedCorruption = -1)
    {
        if (panel != null) panel.SetActive(true);
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
            rt.sizeDelta = new Vector2(1000f, 110f);
        }

        // Upgrades moved to the village blacksmith.
        foreach (var label in new[] { attackButtonLabel, speedButtonLabel, hpButtonLabel })
            if (label != null && label.transform.parent != null) label.transform.parent.gameObject.SetActive(false);
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
        int before = MetaProgress.Gold - goldEarned, ready = 0;
        foreach (MetaProgress.Upgrade u in System.Enum.GetValues(typeof(MetaProgress.Upgrade)))
            if (!MetaProgress.IsMaxed(u) && MetaProgress.Gold >= MetaProgress.Cost(u) && before < MetaProgress.Cost(u)) ready++;
        if (ready > 0) parts.Add(Loc.F("result.unlock.smith", ready));
        return string.Join("  ·  ", parts);
    }

    // ---------------------------------------------------------------- the run, looked back on
    TextMeshProUGUI statsText, buildText;

    // The board grows to make room: banner on top, the record (left) and the build (right), the gold, the buttons.
    void BuildSummary(bool win)
    {
        if (panel == null) return;
        var board = (RectTransform)panel.transform;
        board.sizeDelta = new Vector2(1160f, 900f);
        Place(board.Find("ResultBanner"), 385f);
        if (goldText != null) { goldText.rectTransform.anchoredPosition = new Vector2(0f, -205f); }
        foreach (var name in new[] { "RetryButton", "QuickRetryButton" })
        {
            var b = board.Find(name) as RectTransform;
            if (b != null) b.anchoredPosition = new Vector2(b.anchoredPosition.x, -350f);
        }
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
            Loc.F("result.seed", RunStats.Seed) +
            Loc.F("result.kills", RunStats.Kills) + (RunStats.EliteKills > 0 ? Loc.F("result.elite_kills", RunStats.EliteKills) : "") + "\n" +
            Loc.F("result.damage_dealt", RunStats.DamageDealt) +
            Loc.F("result.damage_taken", RunStats.DamageTaken) + cause + worst + tip + reroll;
        buildText.text = BuildLines();
        BuildIcons((RectTransform)buildText.transform.parent, buildText);
        // 24 throughout: at 32 (which the pixel font draws as 36) the record ran out of its sheet.
        buildText.fontSize = UiTheme.TextSmall;
        statsText.fontSize = UiTheme.TextSmall;
        statsText.lineSpacing = 2f;
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
            if (lv > 0) sets.Add(lv >= 2 ? Loc.F("result.set_complete", CardPool.TagNames[t]) : Loc.F("result.set", CardPool.TagNames[t]));
        }
        if (sets.Count > 0) sb.Append("<color=" + UiTheme.Html("tip") + ">").Append(string.Join(" · ", sets)).Append("</color>");
        return sb.ToString();
    }

    // The build at a glance: every card taken as its icon, along the bottom of the build sheet (skills in the order
    // taken, then the stat cards). The text above makes room for the strip.
    static void BuildIcons(RectTransform sheet, TextMeshProUGUI text)
    {
        const int PerRow = 7, MaxRows = 2;
        const float Cell = 64f, IconSize = 54f;
        var old = sheet.Find("Icons");
        if (old != null) Destroy(old.gameObject);
        text.rectTransform.offsetMin = new Vector2(22f, 16f);
        var stats = PlayerStats.Instance;
        if (stats == null) return;
        var cards = new System.Collections.Generic.List<CardPool.Card>();
        foreach (var id in stats.SkillOrder) { var c = CardPool.Get(id); if (c != null) cards.Add(c); }
        foreach (var c in CardPool.Stats) if (stats.CardLevel(c.id) > 0) cards.Add(c);
        if (cards.Count == 0) return;

        int rows = Mathf.Min(MaxRows, (cards.Count + PerRow - 1) / PerRow);
        var strip = new GameObject("Icons", typeof(RectTransform));
        var srt = (RectTransform)strip.transform;
        srt.SetParent(sheet, false);
        srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0.5f, 0f);
        srt.anchoredPosition = new Vector2(0f, 16f);
        srt.sizeDelta = new Vector2(PerRow * Cell, rows * Cell);
        for (int i = 0; i < Mathf.Min(cards.Count, rows * PerRow); i++)
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
        img.color = new Color(0.09f, 0.06f, 0.07f, 0.9f);
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
