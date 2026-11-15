using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The village blacksmith (MainScene). Spends the saved gold (MetaProgress) on permanent
// upgrades: attack +1, move speed +15%, max hearts +1 per level, each up to its cap
// (MetaProgress.MaxLevel - maxed rows show "최대"). Rows whose cost the
// player can't afford are greyed out. Buttons are hooked up in code.
public class BlacksmithUI : MonoBehaviour
{
    public GameObject panel;
    public TMP_Text goldText;
    public TMP_Text messageText;
    public Button closeButton;

    [Header("Rows: attack, speed, heart")]
    public TMP_Text[] rowLabels = new TMP_Text[3];
    public Button[] buyButtons = new Button[3];
    public TMP_Text[] buyLabels = new TMP_Text[3];

    static readonly MetaProgress.Upgrade[] Order = { MetaProgress.Upgrade.Attack, MetaProgress.Upgrade.Speed, MetaProgress.Upgrade.Heart, MetaProgress.Upgrade.Reroll, MetaProgress.Upgrade.Starter };
    // Built on first use, not as a static initializer: that can run inside Unity's object construction,
    // where the text table (Resources) can't be loaded yet.
    static string[] names, effects;
    static string[] Names => names ??= new[] { Loc.T("smith.attack"), Loc.T("smith.speed"), Loc.T("smith.heart"), Loc.T("smith.reroll"), Loc.T("smith.starter") };
    static string[] Effects => effects ??= new[] { Loc.T("smith.attack.effect"), Loc.T("smith.speed.effect"), Loc.T("smith.heart.effect"), Loc.T("smith.reroll.effect"), Loc.T("smith.starter.effect") };

    // The scene has three rows; each one after (행운의 방울, 든든한 출발) is a copy of the last, the panel growing by a row for it.
    void AddRerollRow()
    {
        int n = rowLabels.Length; // the row being added
        if (n >= Order.Length || buyButtons.Length < 3) return;
        var last = buyButtons[n - 1].transform.parent as RectTransform;
        var prev = buyButtons[n - 2].transform.parent as RectTransform;
        if (last == null || prev == null || last.parent == null) return;
        float step = last.anchoredPosition.y - prev.anchoredPosition.y; // negative: rows go down
        var row = Instantiate(last.gameObject, last.parent);
        row.name = "Row" + n;
        var rt = (RectTransform)row.transform;
        rt.anchoredPosition = last.anchoredPosition + new Vector2(0f, step);
        // Make room: everything in the panel moves up half a row, the frame grows a row, and what sat below the
        // rows (the close button) moves down with the new bottom.
        var inner = (RectTransform)last.parent;
        foreach (RectTransform child in inner)
        {
            bool below = child.anchoredPosition.y < last.anchoredPosition.y - 1f && child != rt;
            child.anchoredPosition += new Vector2(0f, below ? step * 0.5f : -step * 0.5f);
        }
        for (var t = inner; t != null && t != panel.transform; t = t.parent as RectTransform)
            t.sizeDelta += new Vector2(0f, -step);

        System.Array.Resize(ref rowLabels, n + 1);
        System.Array.Resize(ref buyButtons, n + 1);
        System.Array.Resize(ref buyLabels, n + 1);
        buyButtons[n] = row.GetComponentInChildren<Button>(true);
        foreach (var t in row.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.transform.IsChildOf(buyButtons[n].transform)) buyLabels[n] = t;
            else rowLabels[n] = t;
        }
        buyButtons[n].onClick = new Button.ButtonClickedEvent();
    }

    static readonly Color BuyColor = UiTheme.Wine;
    static readonly Color DisabledColor = new Color(0.3f, 0.27f, 0.3f, 1f);
    static readonly Color MaxedColor = new Color(0.55f, 0.43f, 0.16f, 1f); // dull gold

    float messageTimer;

    public bool IsOpen => (panel != null && panel.activeSelf) || Talking; // talking counts: the village ignores clicks meanwhile

    void Awake()
    {
        for (int i = rowLabels.Length; i < Order.Length; i++) AddRerollRow();
        for (int i = 0; i < buyButtons.Length; i++)
        {
            int index = i;
            buyButtons[i].onClick.AddListener(() => Buy(index));
            // "<icon>80" must stay on one line inside the button; shrink instead of wrapping. (Only the price: with
            // "강화" before it a three-digit price no longer fitted at 24 and PixelText dropped it to 12.)
            var label = buyLabels[i];
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = label.fontSize * 0.6f;
            label.margin = new Vector4(12f, 0f, 12f, 0f); // "강화" was touching the button's left edge
            rowLabels[i].lineSpacing = 14f; // the effect line touched the name line above it
            rowLabels[i].verticalAlignment = VerticalAlignmentOptions.Middle;
        }
        closeButton.onClick.AddListener(Close);
        panel.SetActive(false);
    }

    // The blacksmith talks first: his portrait (Resources/Village/blacksmith, drawn with pxl) and a dialogue box at
    // the bottom of the screen, a line at a time (click / Space / Enter / pad A: finish the line, then the next).
    // After the last line the upgrade panel opens, his portrait staying beside it.
    public void Open()
    {
        SharpTitle();
        BuildTalk();
        remark = false;
        lines = TalkLines();
        line = 0;
        GameSave.Data.smithVisits++;
        GameSave.Save();
        panel.SetActive(false);
        portrait.gameObject.SetActive(true);
        talkBox.SetActive(true);
        openedFrame = Time.frameCount; // the click that opened it is not a "next"
        StartLine();
    }

    // Straight to the shop (screen captures, tests): the portrait beside it, no dialogue.
    public void OpenShop()
    {
        SharpTitle();
        BuildTalk();
        portrait.gameObject.SetActive(true);
        talkBox.SetActive(false);
        panel.SetActive(true);
        Say(Loc.T("smith.greeting"));
        Refresh();
    }

    // The board's title (the scene's "대장간") is bold at 64: the pixel font is sharp only at 12 / 24 / 36 / 48 and
    // a faked bold smears it. Plain at 48, the font's own weight.
    void SharpTitle()
    {
        if (panel == null) return;
        string title = Loc.T("harbor.blacksmith");
        foreach (var t in panel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.text != title) continue;
            t.fontStyle = FontStyles.Normal;
            t.enableAutoSizing = false;
            t.fontSize = 48f;
        }
    }

    public void Close()
    {
        panel.SetActive(false);
        if (talkBox != null) talkBox.SetActive(false);
        if (portrait != null) portrait.gameObject.SetActive(false);
    }

    GameObject talkBox;
    Image portrait;
    TMP_Text talkText, nextMark;
    string[] lines;
    int line, openedFrame = -1;
    float shownChars;
    const float CharsPerSecond = 32f;

    bool Talking => talkBox != null && talkBox.activeSelf;
    public bool IsTalking => Talking && !remark; // (a shop remark in the bubble is not a conversation)
    public bool BubbleUp => Talking;
    // For tests: which line is up and whether it has finished typing.
    public int LineIndex => line;
    public bool LineTyped => shownChars >= lineChars;
    public RectTransform BubbleRect => bubble != null ? bubble.rectTransform : null;
    Mood[] lineMoods;

    // First visit: who he is. Later: what he sees (gold for something, not enough, nothing left to do).
    string[] TalkLines()
    {
        if (GameSave.Data.smithVisits == 0)
        {
            lineMoods = new[] { Mood.Happy, Mood.Neutral, Mood.Happy };
            return new[] { Loc.T("smith.talk.first1"), Loc.T("smith.talk.first2"), Loc.T("smith.talk.prompt") };
        }
        // Milestones (once, on that visit): the lucky bell comes out on the second visit; a regular gets a word at the tenth.
        int visit = GameSave.Data.smithVisits + 1;
        if (visit == RerollUnlockVisit && MetaProgress.Level(MetaProgress.Upgrade.Reroll) == 0)
        {
            lineMoods = new[] { Mood.Happy, Mood.Neutral };
            return new[] { Loc.T("smith.talk.reroll_unlock"), Loc.T("smith.talk.prompt") };
        }
        if (visit == RegularVisit)
        {
            lineMoods = new[] { Mood.Happy, Mood.Neutral };
            return new[] { Loc.T("smith.talk.regular"), Loc.T("smith.talk.prompt") };
        }
        bool anyLeft = false, canBuy = false;
        foreach (var u in Order)
        {
            if (MetaProgress.IsMaxed(u) || Locked(u)) continue;
            anyLeft = true;
            if (MetaProgress.Gold >= MetaProgress.Cost(u)) canBuy = true;
        }
        if (!anyLeft) { lineMoods = new[] { Mood.Happy }; return new[] { Loc.T("smith.talk.maxed") }; }
        // A regular: one line and straight to the shop (two lines to click through every visit got tedious).
        lineMoods = new[] { canBuy ? Mood.Happy : Mood.Sad };
        return new[] { Loc.T(canBuy ? "smith.talk.rich" : "smith.talk.poor") };
    }

    // The bubble: bottom-left corner up and right of the portrait's mouth; padding round the text (room for the
    // name plate on top and the "click" hint under it); never wider than BubbleMaxWidth - longer lines wrap.
    static readonly Vector2 BubbleCorner = new Vector2(310f, 430f);
    // With the shop open the bubble sits over his head instead, in the strip left of the shop board (it covered the
    // upgrade rows), narrower and wrapping sooner.
    static readonly Vector2 RemarkCorner = new Vector2(24f, 480f);

    // The shop board's left edge in the canvas's units, from its bottom-left.
    float ShopLeft(RectTransform canvas)
    {
        var corners = new Vector3[4];
        ((RectTransform)panel.transform).GetWorldCorners(corners);
        return canvas.InverseTransformPoint(corners[0]).x - canvas.rect.xMin;
    }
    const float BubbleScale = 4f; // canvas units per art pixel of the board and the tail (whole numbers only: pixel snap)
    const float BubbleMaxWidth = 820f, BubbleMinWidth = 420f, BubblePadX = 34f, BubblePadTop = 46f, BubblePadBottom = 58f;
    const float PopTime = 0.09f;
    Image bubble;
    float popUntil;
    int lineChars;

    // The widest the bubble may be on this screen: up to BubbleMaxWidth, but on a narrower canvas (16:10, 4:3) only
    // what is left between its corner and the village's right-hand column (the goals board and the buttons, about
    // RightColumn wide) - so with 큰 글씨 on a 4:3 screen it wraps sooner instead of running under them.
    const float RightColumn = 560f;
    float MaxBubbleWidth()
    {
        var canvas = (RectTransform)talkBox.transform.parent;
        if (remark) return Mathf.Max(300f, ShopLeft(canvas) - RemarkCorner.x - 16f);
        float room = canvas.rect.width - BubbleCorner.x - RightColumn;
        return Mathf.Clamp(room, 360f, BubbleMaxWidth);
    }

    // Fits the bubble to the whole line (measured at its full length, so it doesn't grow while the letters type).
    void SizeBubble(string text)
    {
        float maxWidth = MaxBubbleWidth();
        float maxInner = maxWidth - BubblePadX * 2f;
        Vector2 need = talkText.GetPreferredValues(text, maxInner, 0f);
        float w = Mathf.Clamp(need.x + BubblePadX * 2f + 8f, Mathf.Min(BubbleMinWidth, maxWidth), maxWidth);
        float h = need.y + BubblePadTop + BubblePadBottom;
        bubble.rectTransform.sizeDelta = new Vector2(Mathf.Ceil(w / 4f) * 4f, Mathf.Ceil(h / 4f) * 4f); // whole art pixels (3 units) round up to 4
    }

    void StartLine()
    {
        // Each word kept whole (<nobr>): a long line wraps between words, never inside one ("있겠 / 다냥").
        ((RectTransform)talkBox.transform).anchoredPosition = remark ? RemarkCorner : BubbleCorner;
        string words = "<nobr>" + lines[line].Replace(" ", "</nobr> <nobr>") + "</nobr>";
        talkText.text = words;
        SizeBubble(words);
        talkText.ForceMeshUpdate();
        lineChars = talkText.textInfo.characterCount; // the letters to type (the <nobr> tags aren't any)
        // Pop in: half size for a moment, then full (only 0.5 and 1: in-between scales smear the pixel art).
        popUntil = Time.unscaledTime + PopTime;
        talkBox.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
        Speak(lines[line], lineMoods != null && line < lineMoods.Length ? lineMoods[line] : Mood.Neutral);
        talkText.maxVisibleCharacters = 0;
        shownChars = 0f;
        nextMark.gameObject.SetActive(false);
    }

    public void UpdateTalk()
    {
        if (talkBox.transform.localScale.x < 1f && Time.unscaledTime >= popUntil) talkBox.transform.localScale = Vector3.one;
        int total = lineChars;
        bool done = shownChars >= total;
        if (!done)
        {
            int before = (int)shownChars;
            shownChars += Time.unscaledDeltaTime * CharsPerSecond;
            talkText.maxVisibleCharacters = Mathf.Min(total, (int)shownChars);
            // a soft blip every few letters, his "voice" (a high, quiet card flick)
            if ((int)shownChars / 3 > before / 3) Sfx.Play("voice_blip", 1f, Random.Range(0.92f, 1.1f)); // his own chatter (SynthSfx), not a card flip
        }
        else talkUntil = 0f; // the whole line is out: mouth shut
        nextMark.gameObject.SetActive(!remark && done && Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.5f);
        if (remark) return; // a shop remark: no "next", it just stays up
        if (Time.frameCount == openedFrame || !GameInput.SubmitPressed) return;
        if (!done) { shownChars = total; talkText.maxVisibleCharacters = total; talkUntil = 0f; return; } // first press: the whole line
        if (++line < lines.Length) { StartLine(); return; }
        talkBox.SetActive(false); // last line read: the shop
        panel.SetActive(true);
        Say(Loc.T("smith.greeting"));
        Refresh();
    }

    void BuildTalk()
    {
        if (talkBox != null) return;
        var canvas = panel.GetComponentInParent<Canvas>(true).rootCanvas;
        var font = messageText != null ? messageText.font : null;
        var skin = UISkin.Instance;

        // Portrait, bottom-left, standing on the dialogue box (and beside the panel while shopping).
        portrait = new GameObject("SmithPortrait", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        var prt = portrait.rectTransform;
        prt.SetParent(canvas.transform, false);
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0f, 0f);
        prt.anchoredPosition = new Vector2(70f, 190f);
        prt.sizeDelta = new Vector2(120f * 2.25f, 126f * 2.25f); // the 120 x 126 frames (smith_0..7)
        for (int i = 0; i < faces.Length; i++) faces[i] = Resources.Load<Sprite>("Village/smith_" + i);
        portrait.sprite = faces[0];
        portrait.gameObject.AddComponent<TalkDriver>().owner = this; // ticks the dialogue and the mouth (this script sits on the panel, hidden meanwhile)
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;

        // A speech bubble up and to the right of the portrait, its tail down at his mouth: icelivin's pixel speech
        // bubble (UIBook/SpeechBoard, 9-sliced; cut by Tools/bubble_from_icelivin.ps1), sized to each line (SizeBubble)
        // up to MaxBubbleWidth, growing up and right from its bottom-left corner. The tail is the sheet's left tail
        // (UIBook/SpeechTail, 5 x 5 art pixels, never rotated), its top row laid over the bubble's bottom rim. His
        // name on a small wood plate on the bubble's top edge.
        talkBox = new GameObject("SmithBubble", typeof(RectTransform));
        var brt = (RectTransform)talkBox.transform;
        brt.SetParent(canvas.transform, false);
        brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0f, 0f);
        brt.anchoredPosition = BubbleCorner;

        bubble = new GameObject("Bubble", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        bubble.rectTransform.SetParent(brt, false);
        bubble.rectTransform.anchorMin = bubble.rectTransform.anchorMax = bubble.rectTransform.pivot = new Vector2(0f, 0f);
        bubble.raycastTarget = false;
        // The new bubble art (UICats/ui_bubble) has its tail built in at the bottom-left; the old board + loose tail stay as fallback.
        var art = Resources.Load<Sprite>("UICats/ui_bubble");
        if (art != null)
        {
            bubble.sprite = art;
            bubble.type = Image.Type.Sliced;
            bubble.pixelsPerUnitMultiplier = 1f / BubbleScale;
        }
        else BookUI.Slice(bubble, "SpeechBoard", BubbleScale);
        if (bubble.sprite == null) bubble.color = new Color(0.91f, 0.84f, 0.68f);

        // 5 x 5 art pixels at the bubble's units per pixel, where the sheet has it (6 px in from the board's left);
        // drawn over the bubble so its top (fill) row hides the rim there and the two read as one shape.
        var tail = new GameObject("Tail", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        tail.rectTransform.SetParent(brt, false);
        tail.rectTransform.anchorMin = tail.rectTransform.anchorMax = Vector2.zero; // at the bubble's bottom-left corner
        tail.rectTransform.pivot = new Vector2(0f, 1f);
        tail.rectTransform.anchoredPosition = new Vector2(6f * BubbleScale, BubbleScale); // at the left end: its point lands by his mouth
        tail.rectTransform.sizeDelta = new Vector2(5f * BubbleScale, 5f * BubbleScale);
        tail.sprite = BookUI.Get("SpeechTail");
        tail.enabled = tail.sprite != null && art == null;
        tail.raycastTarget = false;

        var plate = new GameObject("NamePlate", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        plate.rectTransform.SetParent(bubble.rectTransform, false);
        plate.rectTransform.anchorMin = plate.rectTransform.anchorMax = new Vector2(0f, 1f);
        plate.rectTransform.pivot = new Vector2(0f, 0.5f);
        plate.rectTransform.anchoredPosition = new Vector2(24f, 4f);
        plate.rectTransform.sizeDelta = new Vector2(300f, 56f);
        if (skin != null) UISkin.Apply(plate, skin.buttonWood, UISkin.SmallScale);
        var name = TalkText(plate.rectTransform, Loc.T("smith.name"), UiTheme.TextSmall, Color.white, font);
        name.alignment = TextAlignmentOptions.Center;

        talkText = TalkText(bubble.rectTransform, "", UiTheme.TextLarge, UISkin.Ink, font);
        talkText.rectTransform.offsetMin = new Vector2(BubblePadX, BubblePadBottom);
        talkText.rectTransform.offsetMax = new Vector2(-BubblePadX, -BubblePadTop);
        talkText.alignment = TextAlignmentOptions.TopLeft;
        talkText.textWrappingMode = TextWrappingModes.Normal;

        nextMark = TalkText(bubble.rectTransform, Loc.T("smith.talk.next"), UiTheme.TextSmall, UISkin.Ink, font);
        nextMark.rectTransform.anchorMin = new Vector2(0f, 0f); nextMark.rectTransform.anchorMax = new Vector2(1f, 0f);
        nextMark.rectTransform.pivot = new Vector2(1f, 0f);
        nextMark.rectTransform.offsetMin = new Vector2(0f, 12f);
        nextMark.rectTransform.offsetMax = new Vector2(-BubblePadX, 46f);
        nextMark.alignment = TextAlignmentOptions.BottomRight;

        portrait.transform.SetAsLastSibling(); // in front of the bubble's tail
        talkBox.SetActive(false);
        portrait.gameObject.SetActive(false);
    }

    static TMP_Text TalkText(RectTransform parent, string text, float size, Color color, TMP_FontAsset font)
    {
        var t = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.rectTransform.SetParent(parent, false);
        t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
        t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    // The 행운의 방울 is kept behind the counter until the second visit (unless it was already bought).
    public const int RerollUnlockVisit = 2;
    const int RegularVisit = 10;
    static bool Locked(MetaProgress.Upgrade u) =>
        u == MetaProgress.Upgrade.Reroll && GameSave.Data.smithVisits < RerollUnlockVisit && MetaProgress.Level(u) == 0;
    public static bool RerollLocked => Locked(MetaProgress.Upgrade.Reroll); // (the village goals board says so)

    void Buy(int i)
    {
        var u = Order[i];
        if (Locked(u)) { Sfx.Play("ui_error"); Say(Loc.F("smith.locked_say", RerollUnlockVisit)); return; }
        if (MetaProgress.IsMaxed(u))
            Say(Loc.F("smith.maxed_already", Names[i], Topic(Names[i])));
        else if (MetaProgress.TryBuy(u))
        {
            Sfx.Play("ui_buy");
            Sfx.Play("coin");
            Say(MetaProgress.IsMaxed(u)
                ? Loc.F("smith.maxed_now", Names[i], MetaProgress.Level(u))
                : Loc.F("smith.upgraded", Names[i], MetaProgress.Level(u)), Mood.Happy);
        }
        else
        {
            Sfx.Play("ui_error");
            Say(Loc.F("smith.no_gold", Currency.Icon), Mood.Sad);
        }
        Refresh();
    }

    // Korean topic particle: 은 after a final consonant (공격력은), 는 otherwise (하트는).
    static string Topic(string word)
    {
        char last = word[word.Length - 1];
        bool hasFinal = last >= '가' && last <= '힣' && (last - '가') % 28 != 0;
        return hasFinal ? Loc.T("grammar.topic_after_final") : Loc.T("grammar.topic");
    }

    // In the shop he still talks in his bubble (an upgrade, not enough gold, ...): the line types out and stays until
    // the next one; nothing to click through, the clicks are for the shop. The old text line on the panel is hidden.
    void Say(string text, Mood m = Mood.Neutral)
    {
        if (messageText != null) messageText.gameObject.SetActive(false);
        if (talkBox == null) { Speak(text, m); return; }
        remark = true;
        lines = new[] { text };
        lineMoods = new[] { m };
        line = 0;
        talkBox.SetActive(true);
        StartLine();
    }
    bool remark;

    // ---- His face (Resources/Village/smith_0..7): 0 / 4 plain, 1 / 2 mouth open, 3 / 7 beaming, 5 smiling, 6 surprised.
    // While a line is being said the mouth flaps through the mood's frames; then he holds the mood's resting face.
    public enum Mood { Neutral, Happy, Sad }
    readonly Sprite[] faces = new Sprite[8];
    static readonly int[][] TalkFrames = { new[] { 0, 1, 2, 1 }, new[] { 5, 3, 7, 3 }, new[] { 4, 1, 4, 6 } };
    static readonly int[] RestFrame = { 5, 5, 4 };
    Mood mood;
    float talkUntil;
    const float MouthFps = 9f;

    void Speak(string text, Mood m)
    {
        mood = m;
        talkUntil = Time.unscaledTime + Mathf.Max(0.6f, text.Length / CharsPerSecond);
    }

    public void UpdatePortrait()
    {
        if (portrait == null) return;
        bool talking = Time.unscaledTime < talkUntil;
        int frame = talking ? TalkFrames[(int)mood][(int)(Time.unscaledTime * MouthFps) % 4] : RestFrame[(int)mood];
        var face = faces[frame];
        if (face != null) portrait.sprite = face;
    }

    void Update()
    {
        if (messageTimer > 0f)
        {
            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0f && messageText != null) messageText.text = Loc.T("smith.prompt");
        }
    }

    // The row's second line: what the cat has now and what the next level makes it ("공격력 4 → 5"), not only the
    // step ("공격력 +1"). Maxed: the value alone. The starter draft has no number: its effect text.
    static readonly string[] ValueKeys = { "smith.value.attack", "smith.value.speed", "smith.value.heart", "smith.value.reroll", null };
    static string Change(int i, int lv, bool maxed)
    {
        if (ValueKeys[i] == null) return Effects[i];
        string Value(int l) => Loc.F(ValueKeys[i], i == 0 ? PlayerStats.BaseAttack + l : i == 1 ? 100 + 15 * l : i == 2 ? 5 + l : l); // (5: PlayerStats.baseMaxHP)
        return maxed ? Value(lv) : $"{Value(lv)} → {Value(lv + 1)}";
    }

    void Refresh()
    {
        int gold = MetaProgress.Gold;
        goldText.text = Loc.F("smith.owned", Currency.Amount(gold));
        for (int i = 0; i < Order.Length; i++)
        {
            var u = Order[i];
            int lv = MetaProgress.Level(u), max = MetaProgress.MaxLevel(u), cost = MetaProgress.Cost(u);
            bool maxed = lv >= max;
            if (Locked(u))
            {
                rowLabels[i].text = Loc.F("smith.locked", RerollUnlockVisit);
                buyLabels[i].text = "-";
                var lockedImg = (Image)buyButtons[i].targetGraphic;
                if (UISkin.Instance != null && lockedImg.sprite != null) UISkin.Apply(lockedImg, UISkin.Instance.buttonGrey, UISkin.SmallScale);
                else lockedImg.color = DisabledColor;
                continue;
            }
            rowLabels[i].text = $"<b>{Names[i]}</b>  Lv.{lv}/{max}\n<size=75%><color={UiTheme.Html("ink")}>{Change(i, lv, maxed)}</color></size>"; // (ink: amber was faint on the tan row)
            buyLabels[i].text = maxed ? Loc.T("smith.max") : Loc.F("smith.cost", Currency.Amount(cost));
            bool available = !maxed && gold >= cost;
            var img = (Image)buyButtons[i].targetGraphic;
            var skin = UISkin.Instance;
            if (skin != null && img.sprite != null)
            {
                // Red = can buy, grey = can't afford, wood = maxed out.
                UISkin.Apply(img, maxed ? skin.buttonWood : available ? skin.buttonRed : skin.buttonGrey, UISkin.SmallScale);
            }
            else img.color = maxed ? MaxedColor : available ? BuyColor : DisabledColor;
        }
    }
}

// Ticks the blacksmith's dialogue from the dialogue box itself (active exactly while he talks).
public class TalkDriver : MonoBehaviour
{
    public BlacksmithUI owner;
    void Update()
    {
        if (owner == null) return;
        if (owner.BubbleUp) owner.UpdateTalk(); // a conversation or a shop remark typing out
        owner.UpdatePortrait();
    }
}
