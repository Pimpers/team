using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// After a normal stage is cleared: 3 random cards from CardPool. Picking one you
// already own levels it up (the card shows "Lv.1 → 2"); maxed cards stop appearing.
// Each card is drawn on its grade's background (Resources/Cards: 일반 green, 희귀 blue, 전설 purple):
// the grade name in the top medallion, the card's effect sprite in the picture window, the text
// in the box underneath.
// The three buttons in the scene still call ChooseAttack / ChooseSpeed / ChooseHP from
// their OnClick - those now simply mean "slot 1 / 2 / 3".
// Under the cards, "다시 뽑기" deals three new ones for some of this run's gold (5, then 10, 15... over the
// whole run) - so the gold picked up in a run has a use there and then, at the price of less to bank at the end.
public class CardDraftUI : MonoBehaviour
{
    public GameObject panel;
    public TMP_Text card1Label;
    public TMP_Text card2Label;
    public TMP_Text card3Label;

    Action onChosenCallback;
    List<CardPool.Card> offered = new List<CardPool.Card>();

    void Awake()
    {
        // The cards auto-size their text anew with every deal: PixelText leaves them be.
        foreach (var l in new[] { card1Label, card2Label, card3Label })
            if (l != null && l.transform.parent != null) PixelText.Ignore(l.transform.parent);
        if (panel != null) panel.SetActive(false);
    }

    public void Show(Action onChosen)
    {
        onChosenCallback = onChosen;
        fastDeal = RunStats.CardDrafts > 0;
        Deal();
        ShowSetTip();
        if (card1Label != null) GameInput.SelectForPad(card1Label.GetComponentInParent<Button>()); // pad: A picks, d-pad moves
    }

    // The very first draft says what the # tags are for: the set system isn't taught in the tutorial.
    void ShowSetTip()
    {
        if (panel == null || card1Label == null) return;
        var tip = panel.transform.Find("SetTip");
        if (tip == null)
        {
            var t = new GameObject("SetTip", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            t.rectTransform.SetParent(panel.transform, false);
            t.rectTransform.sizeDelta = new Vector2(1200f, 48f);
            t.font = card1Label.font;
            t.fontSize = UiTheme.TextSmall;
            t.color = UiTheme.Gold;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.outlineWidth = 0.2f;
            t.outlineColor = new Color32(24, 14, 10, 255);
            t.raycastTarget = false;
            t.text = Loc.T("draft.set_tip");
            tip = t.transform;
        }
        ((RectTransform)tip).anchoredPosition = new Vector2(0f, CardsBottom - 180f); // under the reroll button
        bool first = !GameSave.Data.seenSetTip;
        tip.gameObject.SetActive(first);
        if (first) { GameSave.Data.seenSetTip = true; GameSave.Save(); }
    }

    // The bottom edge of the cards where they rest, in panel units: the cards sit lower than the panel's centre in the
    // scenes, so what goes under them (preview line, reroll, set tip) is placed from here, not from the centre.
    float CardsBottom
    {
        get
        {
            var card = card1Label != null ? card1Label.transform.parent as RectTransform : null;
            if (card == null) return -CardH * 0.5f;
            float y = homePos.TryGetValue(card, out var home) ? home.y : card.anchoredPosition.y;
            return y - CardH * 0.5f;
        }
    }

    // ---- Reroll
    const int RerollBase = 5;
    // Paid rerolls so far this run (the free ones first): the price climbs over the whole run, not anew each draft,
    // so rerolling is a spend to weigh rather than a free retry every stage.
    static int PaidRerolls => Mathf.Max(0, RunStats.Rerolls - MetaProgress.Level(MetaProgress.Upgrade.Reroll));
    Button rerollButton;
    TextMeshProUGUI rerollLabel;
    // 행운의 방울 (MetaProgress.Upgrade.Reroll): the run's first rerolls are free, one per level.
    static int FreeRerollsLeft => Mathf.Max(0, MetaProgress.Level(MetaProgress.Upgrade.Reroll) - RunStats.Rerolls);
    // Capped at 20: past the fourth paid reroll the price stops climbing, so late in a run it stays a choice.
    const int RerollMaxSteps = 4;
    int RerollCost => FreeRerollsLeft > 0 ? 0 : RerollBase * Mathf.Min(PaidRerolls + 1, RerollMaxSteps);

    void Reroll()
    {
        var stats = PlayerStats.Instance;
        if (stats == null || dealing != null || !stats.SpendGold(RerollCost)) { Sfx.Play("ui_error", 0.6f); return; }
        RunStats.Rerolls++;
        Sfx.Play("coin", 1f, 0.8f);
        Deal();
    }

    void BuildReroll()
    {
        if (rerollButton != null || panel == null) return;
        var go = new GameObject("RerollButton", typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform;
        rt.SetParent(panel.transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(440f, 78f); // room for "다시 뽑기  (gold)10  (보유 35)"
        var img = go.GetComponent<Image>();
        if (UISkin.Instance != null) UISkin.Apply(img, UISkin.Instance.buttonWood, UISkin.SmallScale);
        else img.color = UiTheme.Wood;
        rerollButton = go.GetComponent<Button>();
        var colors = rerollButton.colors;
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        rerollButton.colors = colors;
        rerollButton.onClick.AddListener(Reroll);
        go.AddComponent<ButtonSound>();
        rerollLabel = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        rerollLabel.rectTransform.SetParent(rt, false);
        rerollLabel.rectTransform.anchorMin = Vector2.zero; rerollLabel.rectTransform.anchorMax = Vector2.one;
        rerollLabel.rectTransform.offsetMin = rerollLabel.rectTransform.offsetMax = Vector2.zero;
        if (card1Label != null) rerollLabel.font = card1Label.font;
        rerollLabel.fontSize = UISkin.FontBody;
        rerollLabel.alignment = TextAlignmentOptions.Center;
        rerollLabel.textWrappingMode = TextWrappingModes.NoWrap;
        rerollLabel.raycastTarget = false;
        rerollLabel.outlineWidth = 0.18f;
        rerollLabel.outlineColor = new Color32(24, 14, 10, 255);
    }

    void RefreshReroll()
    {
        BuildReroll();
        if (rerollButton == null) return;
        ((RectTransform)rerollButton.transform).anchoredPosition = new Vector2(0f, CardsBottom - 110f); // under the preview line

        var stats = PlayerStats.Instance;
        bool afford = stats != null && stats.Gold >= RerollCost;
        rerollButton.interactable = afford;
        rerollLabel.text = FreeRerollsLeft > 0 ? Loc.F("draft.reroll_free", FreeRerollsLeft)
                         : Loc.F("draft.reroll", Currency.Icon, RerollCost, (stats != null ? stats.Gold : 0));
        rerollLabel.color = afford ? UiTheme.Cream : UiTheme.DisabledText;
    }

    void Deal()
    {
        offered = PlayerStats.Instance != null ? CardPool.Draw(PlayerStats.Instance, 3) : new List<CardPool.Card>();
        var labels = new[] { card1Label, card2Label, card3Label };
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] == null) continue;
            bool has = i < offered.Count;
            if (labels[i].transform.parent != null) labels[i].transform.parent.gameObject.SetActive(has);
            if (has) { Dress(labels[i], offered[i]); labels[i].text = CardText(offered[i]); }
        }
        if (offered.Count == 0) { onChosenCallback?.Invoke(); return; } // everything maxed

        if (panel != null)
        {
            panel.SetActive(true);
            // A quiet dark veil separates the paused choice from the live battlefield.
            var plate = panel.GetComponent<Image>();
            if (plate != null)
            {
                plate.enabled = true;
                plate.sprite = null;
                plate.color = new Color(0.025f, 0.018f, 0.035f, 0.72f);
                plate.raycastTarget = true;
            }
        }
        // Freeze the game while the player picks a card: the character stops where it is
        // (PlayerController2D/PlayerAttack also ignore input while timeScale is 0). UI
        // buttons still work since the EventSystem runs on unscaled time.
        GameClock.Pause("draft");

        if (dealing != null) StopCoroutine(dealing);
        var cards = new List<TMP_Text>();
        for (int i = 0; i < labels.Length && i < offered.Count; i++) if (labels[i] != null) cards.Add(labels[i]);
        dealing = StartCoroutine(DealIn(cards));
        RefreshReroll();
    }

    // ---- Deal-in animation: the cards come in face down (their grade's back: Resources/Cards/back_*),
    // spinning around their vertical axis while they grow and rise into place, one after another.
    // The last quarter turn flips each one over: it passes edge-on and comes round face up, with a
    // little pop and a chime (higher for rarer grades). Nothing can be clicked until all are up.
    // Runs on unscaled time (the game is paused under the draft).
    const float DealTime = 0.9f, DealStagger = 0.16f, DealTurns = 2f, DealRise = 90f, DealStartScale = 0.25f;
    // Turns spun face down, plus the final quarter turn that reveals the face.
    const float RevealPopTime = 0.18f;
    static readonly float[] RevealPitch = { 1f, 1.15f, 1.32f };
    Coroutine dealing;
    bool fastDeal;
    readonly Dictionary<RectTransform, Vector2> homePos = new Dictionary<RectTransform, Vector2>();

    System.Collections.IEnumerator DealIn(List<TMP_Text> labels)
    {
        var cards = new List<RectTransform>();
        var groups = new List<CanvasGroup>();
        float dealTime = fastDeal ? 0.55f : DealTime;
        float dealStagger = fastDeal ? 0.08f : DealStagger;
        float dealSpin = 360f * (fastDeal ? 1f : DealTurns) + 90f;
        foreach (var label in labels)
        {
            var card = label.transform.parent as RectTransform;
            if (card == null) continue;
            if (!homePos.ContainsKey(card)) homePos[card] = card.anchoredPosition;
            // (Explicit null check: Unity's "missing component" object is not C# null, so ?? would not add it.)
            var group = card.GetComponent<CanvasGroup>();
            if (group == null) group = card.gameObject.AddComponent<CanvasGroup>();
            // Clicks are blocked by not catching raycasts; staying "interactable" keeps the button
            // from greying the card with its disabled tint while it spins.
            group.interactable = true;
            group.blocksRaycasts = false;
            group.alpha = 0f;
            cards.Add(card);
            groups.Add(group);
            Pose(card, 0f, dealSpin);
        }

        float start = Time.unscaledTime;
        var revealedAt = new float[cards.Count];
        for (int i = 0; i < revealedAt.Length; i++) revealedAt[i] = -1f;
        bool done = false;
        while (!done)
        {
            done = true;
            float now = Time.unscaledTime;
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] == null) continue;
                float t = Mathf.Clamp01((now - start - i * dealStagger) / dealTime);
                groups[i].alpha = Mathf.Clamp01(t * 5f);
                bool faceUp = Pose(cards[i], t, dealSpin);
                if (faceUp && revealedAt[i] < 0f)
                {
                    revealedAt[i] = now;
                    int tier = i < offered.Count ? (int)offered[i].tier : 0;
                    Sfx.Play("card_pick", 0.75f, RevealPitch[tier]);
                    if (i < offered.Count && SetBadge(offered[i]) != null) Sfx.Play("coin", 0.3f, 1.5f); // a set badge glints
                }
                // Pop on the flip.
                float since = revealedAt[i] < 0f ? -1f : now - revealedAt[i];
                if (since >= 0f && since < RevealPopTime)
                    cards[i].localScale *= 1f + 0.07f * Mathf.Sin(since / RevealPopTime * Mathf.PI);
                if (t < 1f || since < RevealPopTime) done = false;
            }
            yield return null;
        }

        foreach (var g in groups) if (g != null) { g.interactable = true; g.blocksRaycasts = true; g.alpha = 1f; }
        dealing = null;
        RefreshReroll();
    }

    // t = 0 (small, low, spinning fast) .. 1 (at rest). Ease-out, with a little overshoot on the size.
    // Returns true once the card shows its face.
    bool Pose(RectTransform card, float t, float dealSpin)
    {
        float e = 1f - Mathf.Pow(1f - t, 3f);
        float angle = (1f - e) * dealSpin;
        card.localRotation = Quaternion.Euler(0f, angle, 0f);
        float overshoot = 1f + 0.08f * Mathf.Sin(t * Mathf.PI);
        float s = Mathf.Lerp(DealStartScale, 1f, e) * (t < 1f ? overshoot : 1f);
        card.localScale = new Vector3(s, s, 1f);
        card.anchoredPosition = homePos[card] + new Vector2(0f, -(1f - e) * DealRise);

        // Face down for the whole spin (both sides show the back), face up for the final quarter
        // turn - the switch happens edge-on, so it reads as the card flipping over.
        bool faceUp = angle < 90f;
        var own = card.GetComponent<Graphic>();
        if (own != null) own.canvasRenderer.SetAlpha(faceUp ? 1f : 0f);
        // (All graphics under the card, so TMP's fallback-font sub-meshes for Korean switch too.)
        foreach (var g in card.GetComponentsInChildren<Graphic>(true))
        {
            if (g.transform == card) continue;
            bool isBack = g.gameObject.name == "CardBack";
            g.canvasRenderer.SetAlpha(isBack != faceUp ? 1f : 0f);
        }
        return faceUp;
    }

    // Anything holding the game still (a draft, the route choice, the pause book, the result board).
    public static bool IsPaused => GameClock.Paused;

    // Card face: name, level line, effect. <nobr> keeps the name whole (Korean otherwise
    // breaks mid-word); the label auto-sizes down to fit instead.
    static string CardText(CardPool.Card card)
    {
        int lv = PlayerStats.Instance.CardLevel(card.id);
        // Skills all go to Lv.2, so a new one just says 신규 (keeps the line short on the card).
        string level = card.maxLevel == 0 ? Loc.T("draft.instant")
            : lv == 0 ? ""
            : $"Lv.{lv} → {lv + 1}" + (lv + 1 == card.maxLevel ? Loc.T("draft.max") : "");
        string kind = card.kind == CardPool.Kind.Active ? Loc.F("draft.kind.active", ActiveKey(card.id))
            // (passives get no kind line: one line less to read, and having no key or cooldown already says it)
            : card.kind == CardPool.Kind.Enhance ? Loc.F("draft.kind.enhance", card.tag) : "";
        // A new active's "대기 n초" line moves up into the kind line: with it as its own line the text ran
        // past the cream box onto the frame.
        string effect = card.effect(lv + 1);
        int cooldown = effect.IndexOf("\n" + Loc.T("card.cooldown_word"));
        if (cooldown >= 0 && !string.IsNullOrEmpty(kind)) { kind += " · " + effect.Substring(cooldown + 1); effect = effect.Substring(0, cooldown); }
        string meta = string.IsNullOrEmpty(level) ? kind : string.IsNullOrEmpty(kind) ? level : kind + "  " + level;
        // <nobr>: the short lines shrink (auto-size) rather than wrap into an extra line.
        string metaLine = string.IsNullOrEmpty(meta) ? "" : $"<size=76%><nobr>{meta}</nobr></size>\n";
        // The name sits on its own label over the frame (CardName, see Dress): this text box holds
        // only what fits inside the cream area, so nothing lands on the dark ornament.
        return metaLine +
               $"<size=72%><nobr>{TagLine(card)}</nobr></size>\n" +
               $"<size=82%>{CompactEffect(effect)}</size>";
    }

    // ---- Card face by grade (CardPool.Tier). Layout in card units (the card is 320 x 480, the
    // 512 x 768 art scaled by 0.625): medallion at the top, picture window, text box below it.
    const float CardW = 320f, CardH = 480f;
    public const float CardWidth = CardW, CardHeight = CardH;
    static readonly Vector2 MedallionPos = new Vector2(0f, 186f), MedallionSize = new Vector2(70f, 34f); // the Aether fronts' circle (art rows 44-130 of 768)
    // In the picture window (art rows ~130-440 of 768: +159 to -35), under the set badge at its top (+140).
    static readonly Vector2 PicturePos = new Vector2(0f, 56f), PictureSize = new Vector2(160f, 160f);
    const float IconSize = 128f; // the 64 px skill icons at exactly x2 (0.78 x 160 = 125 drew uneven pixels)
    // The text box is kept inside the cream area: the frame's corner ornaments cut into its sides and bottom,
    // and a box as wide as the frame let a long line or a fourth line run onto them.
    static readonly Vector2[] TextPos = { new Vector2(0f, -136f), new Vector2(0f, -136f), new Vector2(0f, -136f) }; // the text box: -65 to -172 on all three fronts (art rows 472-703)
    static readonly Vector2[] TextSize = { new Vector2(224f, 70f), new Vector2(224f, 70f), new Vector2(224f, 70f) };
    static readonly Vector2 NamePos = new Vector2(0f, -84f), NameSize = new Vector2(224f, 36f); // the box's top line (no name ribbon on these fronts)

    // Ink per grade, readable on that card's text box (cream / pale blue / lavender).
    static readonly (string name, string level, Color body, Color medallion)[] Inks =
    {
        ("#3e4a1c", "#6b7a2a", new Color32(0x3b, 0x3a, 0x22, 0xff), new Color32(0x4a, 0x55, 0x22, 0xff)),
        ("#1d3557", "#2f5d8a", new Color32(0x1f, 0x2a, 0x3d, 0xff), new Color32(0x1d, 0x35, 0x57, 0xff)),
        ("#3d1f63", "#6a3fa0", new Color32(0x2b, 0x1d, 0x40, 0xff), new Color32(0x3d, 0x1f, 0x63, 0xff)),
    };
    static readonly string[] ArtNames = { "card_common", "card_rare", "card_epic" };
    static readonly string[] BackNames = { "back_common", "back_rare", "back_epic" };
    static Sprite[] arts, backs;

    static Sprite Art(CardPool.Tier tier) => Load(ref arts, ArtNames)[(int)tier];
    static Sprite Back(CardPool.Tier tier) => Load(ref backs, BackNames)[(int)tier];

    static Sprite[] Load(ref Sprite[] cache, string[] names)
    {
        if (cache != null) return cache;
        cache = new Sprite[names.Length];
        for (int i = 0; i < names.Length; i++) cache[i] = Resources.Load<Sprite>("Cards/" + names[i]);
        return cache;
    }

    // Puts the grade background, picture and medallion on the label's card button (built once per button).
    void Dress(TMP_Text label, CardPool.Card card)
    {
        var button = label.transform.parent as RectTransform;
        if (button == null) return;
        int t = (int)card.tier;
        button.sizeDelta = new Vector2(CardW, CardH);

        var bg = button.GetComponent<Image>();
        var art = Art(card.tier);
        if (bg != null && art != null)
        {
            bg.sprite = art;
            bg.type = Image.Type.Simple;
            bg.preserveAspect = false;
            bg.color = Color.white;
        }

        var picture = Child<Image>(button, "CardPicture", PicturePos, PictureSize);
        var frames = !string.IsNullOrEmpty(card.icon) ? Vfx.Frames(card.icon) : null;
        picture.sprite = card.Art != null ? card.Art : frames == null ? null : frames[frames.Length == 1 ? 0 : frames.Length * 2 / 5];
        picture.enabled = picture.sprite != null;
        picture.preserveAspect = true;
        picture.raycastTarget = false;
        // The little 16 px props (trap, can...) are a quarter of an effect frame: draw them smaller.
        // Every card's picture in the same 128 box at the window's middle: the 64 px icons x2, a 32 px effect frame x4,
        // a 16 px prop x8 (they were 125 / 160 / 96 - three sizes, uneven pixels on the icons).
        picture.rectTransform.sizeDelta = Vector2.one * IconSize;

        var tier = Child<TextMeshProUGUI>(button, "CardTier", MedallionPos, MedallionSize);
        tier.font = label.font;
        tier.text = CardPool.TierNames[t];
        tier.fontSize = UISkin.FontBody;
        tier.alignment = TextAlignmentOptions.Center;
        tier.color = Inks[t].medallion;
        tier.raycastTarget = false;
        tier.textWrappingMode = TextWrappingModes.NoWrap;
        // A light rim, so the grade reads over the medallion's own engraving instead of sinking into it.
        tier.outlineWidth = 0.22f;
        tier.outlineColor = new Color32(0xf6, 0xea, 0xc8, 0xff);
        tier.fontStyle = FontStyles.Bold;

        // (Tags used to be their own label between the picture and the text box: it ran over the frame and, on the
        // blue card, the name. They are a line of the card text now - see CardText.)
        var oldTags = button.Find("CardTags");
        if (oldTags != null) oldTags.gameObject.SetActive(false);

        var lrt = label.rectTransform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
        lrt.pivot = new Vector2(0.5f, 0.5f);
        lrt.anchoredPosition = TextPos[t];
        lrt.sizeDelta = TextSize[t];
        label.textWrappingMode = TextWrappingModes.Normal;
        label.alignment = TextAlignmentOptions.Top;
        label.enableAutoSizing = true;
        label.fontSizeMin = UiTheme.TextMin;
        label.fontSizeMax = 28f;
        label.overflowMode = TextOverflowModes.Truncate;
        label.lineSpacing = -6f; // four lines (kind, tags, two of effect) fit the cream box above the corner curls

        // Card name: cream with a dark rim, so it reads on the dark frame between picture and text box.
        var nameLabel = Child<TextMeshProUGUI>(button, "CardName", NamePos, NameSize);
        nameLabel.font = label.font;
        nameLabel.text = card.name;
        nameLabel.color = UISkin.OverlayText;
        nameLabel.alignment = TextAlignmentOptions.Center;
        nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
        nameLabel.overflowMode = TextOverflowModes.Truncate;
        nameLabel.enableAutoSizing = true;
        nameLabel.fontSizeMax = 28f;
        nameLabel.fontSizeMin = 18f;
        nameLabel.outlineWidth = 0.28f;
        nameLabel.outlineColor = new Color32(24, 14, 10, 255);
        nameLabel.raycastTarget = false;
        // A dark plate behind the name: the frame's ornament runs through this band and showed through the letters.
        nameLabel.fontSize = nameLabel.fontSizeMax;
        float plateW = Mathf.Min(NameSize.x, nameLabel.GetPreferredValues(card.name).x + 28f);
        var plate = Child<Image>(button, "CardNamePlate", NamePos, new Vector2(plateW, NameSize.y - 2f));
        plate.color = UiTheme.Plate;
        plate.raycastTarget = false;
        plate.transform.SetSiblingIndex(nameLabel.transform.GetSiblingIndex());

        // Set badge over the top of the picture when this card would open or finish a set.
        string badge = SetBadge(card);
        var badgePlate = Child<Image>(button, "SetBadgePlate", new Vector2(0f, 140f), new Vector2(230f, 38f));
        var badgeText = Child<TextMeshProUGUI>(button, "SetBadge", new Vector2(0f, 140f), new Vector2(230f, 38f));
        badgePlate.color = UiTheme.Plate;
        badgePlate.raycastTarget = false;
        badgeText.font = label.font;
        badgeText.text = badge ?? "";
        badgeText.fontSize = UiTheme.TextSmall;
        badgeText.color = UiTheme.Gold;
        badgeText.alignment = TextAlignmentOptions.Center;
        badgeText.textWrappingMode = TextWrappingModes.NoWrap;
        badgeText.raycastTarget = false;
        badgePlate.gameObject.SetActive(badge != null);
        badgeText.gameObject.SetActive(badge != null);
        label.color = Inks[t].body;
        label.transform.SetAsLastSibling();

        // The book pack's corner brackets round the card under the mouse (CardHover).
        var corners = Child<Image>(button, "HoverCorners", Vector2.zero, new Vector2(CardW + 28f, CardH + 28f));
        BookUI.Slice(corners, "Select01a", 4f);
        corners.raycastTarget = false;
        corners.enabled = false;
        var hover = button.GetComponent<CardHover>();
        if (hover == null) hover = button.gameObject.AddComponent<CardHover>();
        hover.corners = corners;

        // What taking it changes ("공격력 5 → 6  #발톱 1/2 → 2/2 해금"), one line under the card, shown with the
        // corners while the card is under the mouse or selected with the pad.
        var preview = Child<TextMeshProUGUI>(button, "CardPreview", new Vector2(0f, -CardH * 0.5f - 30f), new Vector2(CardW + 120f, 40f));
        preview.font = label.font;
        preview.text = PreviewLine(card);
        preview.fontSize = UiTheme.TextSmall;
        preview.color = UiTheme.Cream;
        preview.alignment = TextAlignmentOptions.Center;
        preview.textWrappingMode = TextWrappingModes.NoWrap;
        preview.outlineWidth = 0.2f;
        preview.outlineColor = new Color32(24, 14, 10, 255);
        preview.raycastTarget = false;
        hover.preview = preview.gameObject;
        hover.Refresh();

        // The grade's card back, over everything; shown while the card is dealt face down (DealIn).
        var back = Child<Image>(button, "CardBack", Vector2.zero, new Vector2(CardW, CardH));
        back.sprite = Back(card.tier);
        back.enabled = back.sprite != null;
        back.preserveAspect = false;
        back.raycastTarget = false;
        back.transform.SetAsLastSibling();
    }

    // "#발톱 1/2  #간식 2/3": the tags of the card with this card counted in; a tag that reaches a set step is bold
    // (and says 세트), so the player sees which card completes a bonus.
    static string TagLine(CardPool.Card card)
    {
        var s = PlayerStats.Instance;
        var tags = CardPool.TagsOf(card.id);
        if (s == null || tags.Length == 0) return " ";
        bool isNew = s.CardLevel(card.id) == 0;
        var sb = new System.Text.StringBuilder();
        foreach (var t in tags)
        {
            int have = s.SetCount(t), after = have + (isNew ? 1 : 0);
            int goal = after >= CardPool.SetStep2 ? CardPool.SetStep2 : after >= CardPool.SetStep1 ? CardPool.SetStep2 : CardPool.SetStep1;
            string text = $"#{CardPool.TagNames[(int)t]} {Mathf.Min(after, goal)}/{goal}";
            bool step = isNew && CardPool.SetLevel(after) > CardPool.SetLevel(have);
            if (step) text = Loc.F("draft.set_complete", text);
            if (sb.Length > 0) sb.Append("  ");
            sb.Append($"<color={CardPool.TagColors[(int)t]}>{text}</color>");
        }
        return sb.ToString();
    }

    // "★ 발톱 세트 해금": the set step this card would reach if taken, or null. Shown as a badge over the picture -
    // the same news in the small tag line was easy to miss, and finishing sets is the build's main goal.
    static string SetBadge(CardPool.Card card)
    {
        var s = PlayerStats.Instance;
        if (s == null || s.CardLevel(card.id) > 0) return null;
        foreach (var t in CardPool.TagsOf(card.id))
        {
            int have = s.SetCount(t), lv = CardPool.SetLevel(have + 1);
            if (lv > CardPool.SetLevel(have))
                return Loc.F(lv >= 2 ? "draft.badge.complete" : "draft.badge.unlock", CardPool.TagNames[(int)t]);
        }
        // No step reached by this card: how far its set still is ("발톱 세트까지 1장"), so every pick shows its way there.
        foreach (var t in CardPool.TagsOf(card.id))
        {
            int after = s.SetCount(t) + 1;
            if (after >= CardPool.SetStep2) continue;
            int goal = after >= CardPool.SetStep1 ? CardPool.SetStep2 : CardPool.SetStep1;
            return Loc.F(goal >= CardPool.SetStep2 ? "draft.badge.to_complete" : "draft.badge.to_unlock", CardPool.TagNames[(int)t], goal - after);
        }
        return null;
    }

    // The draft's preview line: the numbers the card moves (before → after) and the set step it reaches.
    static string PreviewLine(CardPool.Card card)
    {
        var s = PlayerStats.Instance;
        if (s == null) return "";
        if (card.id == "heal")
            return Loc.F("draft.preview.hp", s.CurrentHP.ToString("0.#"), Mathf.Min(s.MaxHP, s.CurrentHP + 2f).ToString("0.#"));

        var parts = new List<string>();
        void Num(string key, float before, float after, string format)
        {
            if (Mathf.Abs(after - before) > 0.0001f) parts.Add(Loc.F(key, before.ToString(format), after.ToString(format)));
        }
        Num("draft.preview.attack", s.AttackPower, s.Preview(card.id, () => (float)s.AttackPower), "0");
        Num("draft.preview.maxhp", s.MaxHP, s.Preview(card.id, () => s.MaxHP), "0.#");
        Num("draft.preview.crit", s.CritChance * 100f, s.Preview(card.id, () => s.CritChance) * 100f, "0");
        Num("draft.preview.speed", s.MoveSpeed / s.baseMoveSpeed * 100f, s.Preview(card.id, () => s.MoveSpeed) / s.baseMoveSpeed * 100f, "0");
        Num("draft.preview.attack_speed", s.AttackSpeedMultiplier * 100f, s.Preview(card.id, () => s.AttackSpeedMultiplier) * 100f, "0");
        Num("draft.preview.reach", s.ReachMultiplier * 100f, s.Preview(card.id, () => s.ReachMultiplier) * 100f, "0");
        Num("draft.preview.jumps", s.ExtraAirJumps + 1, s.Preview(card.id, () => s.ExtraAirJumps) + 1, "0");
        Num("draft.preview.dashes", s.ExtraDashes + 2, s.Preview(card.id, () => s.ExtraDashes) + 2, "0");
        if (parts.Count > 2) parts.RemoveRange(2, parts.Count - 2); // one line under a 320-wide card

        // The set step a new card reaches (a level-up doesn't count again).
        if (s.CardLevel(card.id) == 0)
            foreach (var t in CardPool.TagsOf(card.id))
            {
                int have = s.SetCount(t), lv = CardPool.SetLevel(have + 1);
                if (lv <= CardPool.SetLevel(have)) continue;
                int goal = lv >= 2 ? CardPool.SetStep2 : CardPool.SetStep1;
                int was = have >= CardPool.SetStep1 ? CardPool.SetStep2 : CardPool.SetStep1;
                parts.Add(Loc.F(lv >= 2 ? "draft.preview.set_complete" : "draft.preview.set_unlock",
                    CardPool.TagColors[(int)t], CardPool.TagNames[(int)t], have, was, have + 1, goal));
                break;
            }

        if (parts.Count == 0)
        {
            int lv = s.CardLevel(card.id);
            if (card.kind == CardPool.Kind.Active && lv == 0) parts.Add(Loc.F("draft.preview.slot", ActiveKey(card.id)));
            else if (card.maxLevel > 0) parts.Add(Loc.F("draft.preview.level", lv, lv + 1));
        }
        return string.Join("   ", parts);
    }

    static T Child<T>(RectTransform parent, string name, Vector2 pos, Vector2 size) where T : Component
    {
        var existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(T));
        var rt = (RectTransform)go.transform;
        if (existing == null) rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return go.GetComponent<T>();
    }

    // The Q / E / R key an active has (or will get: the next free slot).
    static string ActiveKey(string id)
    {
        var actives = PlayerStats.Instance.ActiveSkills;
        int slot = actives.IndexOf(id);
        if (slot < 0) slot = actives.Count;
        return SkillSystem.SlotKeys[Mathf.Clamp(slot, 0, SkillSystem.SlotKeys.Length - 1)];
    }

    static string CompactEffect(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var lines = text.Split('\n');
        if (lines.Length <= 2) return text;
        // The joined pieces stay whole (<nobr>): if the line is too long it breaks between them, never mid-word
        // ("(10초마" / "다)").
        var second = new System.Text.StringBuilder("<nobr>" + lines[1]);
        for (int i = 2; i < lines.Length; i++) second.Append(" ·</nobr> <nobr>").Append(lines[i]);
        return lines[0] + "\n" + second + "</nobr>";
    }

    // Each written line stays whole; the label shrinks to fit instead of breaking words.
    static string NoBreak(string text) => "<nobr>" + text.Replace("\n", "</nobr>\n<nobr>") + "</nobr>";

    public void ChooseAttack() => Choose(0);
    public void ChooseSpeed() => Choose(1);
    public void ChooseHP() => Choose(2);

    void Choose(int slot)
    {
        if (slot >= offered.Count) return;
        Sfx.Play("card_pick");
        PlayerStats.Instance.ApplyCard(offered[slot].id);
        RunStats.CardDrafts++;
        if (panel != null) panel.SetActive(false);
        GameClock.Resume("draft");
        onChosenCallback?.Invoke();
        OneTimeTip.ShowInRun("sheet"); // after the first card: where to see the build
    }
}

// Corner brackets round the card under the mouse, breathing a little (unscaled time: the draft pauses the game).
public class CardHover : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler,
    UnityEngine.EventSystems.ISelectHandler, UnityEngine.EventSystems.IDeselectHandler
{
    [HideInInspector] public Image corners;
    [HideInInspector] public GameObject preview; // the "what changes" line under the card
    bool over, selected;

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { over = true; Refresh(); }
    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { over = false; Refresh(); }
    public void OnSelect(UnityEngine.EventSystems.BaseEventData e) { selected = true; Refresh(); }
    public void OnDeselect(UnityEngine.EventSystems.BaseEventData e) { selected = false; Refresh(); }
    void OnDisable() { over = selected = false; Refresh(); }

    public void Refresh()
    {
        bool on = over || selected;
        // A light tick when the preview line comes up (not on the way down, nor while the card is being dealt).
        if (on && preview != null && !preview.activeSelf && isActiveAndEnabled) Sfx.Play("preview_tick");
        if (corners != null) corners.enabled = on;
        if (preview != null) preview.SetActive(on);
    }

    void Update()
    {
        if (!(over || selected) || corners == null) return;
        // whole-pixel steps (4 units = one art pixel), out and back twice a second
        float k = Mathf.Repeat(Time.unscaledTime * 2f, 1f) < 0.5f ? 0f : 8f;
        corners.rectTransform.sizeDelta = new Vector2(CardDraftUI.CardWidth + 28f + k, CardDraftUI.CardHeight + 28f + k);
    }
}
