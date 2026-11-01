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

    static readonly MetaProgress.Upgrade[] Order = { MetaProgress.Upgrade.Attack, MetaProgress.Upgrade.Speed, MetaProgress.Upgrade.Heart, MetaProgress.Upgrade.Reroll };
    // Built on first use, not as a static initializer: that can run inside Unity's object construction,
    // where the text table (Resources) can't be loaded yet.
    static string[] names, effects;
    static string[] Names => names ??= new[] { Loc.T("smith.attack"), Loc.T("smith.speed"), Loc.T("smith.heart"), Loc.T("smith.reroll") };
    static string[] Effects => effects ??= new[] { Loc.T("smith.attack.effect"), Loc.T("smith.speed.effect"), Loc.T("smith.heart.effect"), Loc.T("smith.reroll.effect") };

    // The scene has three rows; the fourth (행운의 방울) is a copy of the last, the panel growing by one row for it.
    void AddRerollRow()
    {
        if (rowLabels.Length >= Order.Length || buyButtons.Length < 3) return;
        var last = buyButtons[2].transform.parent as RectTransform; // Row2
        var prev = buyButtons[1].transform.parent as RectTransform;
        if (last == null || prev == null || last.parent == null) return;
        float step = last.anchoredPosition.y - prev.anchoredPosition.y; // negative: rows go down
        var row = Instantiate(last.gameObject, last.parent);
        row.name = "Row3";
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

        System.Array.Resize(ref rowLabels, Order.Length);
        System.Array.Resize(ref buyButtons, Order.Length);
        System.Array.Resize(ref buyLabels, Order.Length);
        buyButtons[3] = row.GetComponentInChildren<Button>(true);
        foreach (var t in row.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.transform.IsChildOf(buyButtons[3].transform)) buyLabels[3] = t;
            else rowLabels[3] = t;
        }
        buyButtons[3].onClick = new Button.ButtonClickedEvent();
    }

    static readonly Color BuyColor = UiTheme.Wine;
    static readonly Color DisabledColor = new Color(0.3f, 0.27f, 0.3f, 1f);
    static readonly Color MaxedColor = new Color(0.55f, 0.43f, 0.16f, 1f); // dull gold

    float messageTimer;

    public bool IsOpen => (panel != null && panel.activeSelf) || Talking; // talking counts: the village ignores clicks meanwhile

    void Awake()
    {
        AddRerollRow();
        for (int i = 0; i < buyButtons.Length; i++)
        {
            int index = i;
            buyButtons[i].onClick.AddListener(() => Buy(index));
            // "강화 <icon>80" must stay on one line inside the button; shrink instead of wrapping.
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
        BuildTalk();
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
        BuildTalk();
        portrait.gameObject.SetActive(true);
        talkBox.SetActive(false);
        panel.SetActive(true);
        Say(Loc.T("smith.greeting"));
        Refresh();
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

    // First visit: who he is. Later: what he sees (gold for something, not enough, nothing left to do).
    string[] TalkLines()
    {
        if (GameSave.Data.smithVisits == 0)
            return new[] { Loc.T("smith.talk.first1"), Loc.T("smith.talk.first2"), Loc.T("smith.talk.prompt") };
        bool anyLeft = false, canBuy = false;
        foreach (var u in Order)
        {
            if (MetaProgress.IsMaxed(u)) continue;
            anyLeft = true;
            if (MetaProgress.Gold >= MetaProgress.Cost(u)) canBuy = true;
        }
        if (!anyLeft) return new[] { Loc.T("smith.talk.maxed") };
        return new[] { Loc.T(canBuy ? "smith.talk.rich" : "smith.talk.poor"), Loc.T("smith.talk.prompt") };
    }

    void StartLine()
    {
        talkText.text = lines[line];
        talkText.maxVisibleCharacters = 0;
        shownChars = 0f;
        nextMark.gameObject.SetActive(false);
    }

    public void UpdateTalk()
    {
        int total = talkText.text.Length;
        bool done = shownChars >= total;
        if (!done)
        {
            shownChars += Time.unscaledDeltaTime * CharsPerSecond;
            talkText.maxVisibleCharacters = Mathf.Min(total, (int)shownChars);
        }
        nextMark.gameObject.SetActive(done && Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.5f);
        if (Time.frameCount == openedFrame || !GameInput.SubmitPressed) return;
        if (!done) { shownChars = total; talkText.maxVisibleCharacters = total; return; } // first press: the whole line
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
        prt.anchoredPosition = new Vector2(70f, 250f);
        prt.sizeDelta = new Vector2(48f * 7f, 34f * 7f); // the 48 x 34 drawing at x7
        portrait.sprite = Resources.Load<Sprite>("Village/blacksmith");
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;

        // Dialogue box along the bottom, with his name on a wood plate.
        talkBox = new GameObject("SmithTalk", typeof(RectTransform), typeof(Image));
        talkBox.AddComponent<TalkDriver>().owner = this; // runs the dialogue: this script sits on the panel, hidden meanwhile
        var brt = (RectTransform)talkBox.transform;
        brt.SetParent(canvas.transform, false);
        brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0f);
        brt.anchoredPosition = new Vector2(0f, 30f);
        brt.sizeDelta = new Vector2(1560f, 230f);
        var box = talkBox.GetComponent<Image>();
        if (skin != null && skin.buttonCream != null) UISkin.Apply(box, skin.buttonCream, 3f);
        else box.color = new Color(1f, 0.95f, 0.82f);

        var plate = new GameObject("NamePlate", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        plate.rectTransform.SetParent(brt, false);
        plate.rectTransform.anchorMin = plate.rectTransform.anchorMax = new Vector2(0f, 1f);
        plate.rectTransform.anchoredPosition = new Vector2(560f, 6f);
        plate.rectTransform.sizeDelta = new Vector2(380f, 64f);
        if (skin != null) UISkin.Apply(plate, skin.buttonWood, UISkin.SmallScale);
        var name = TalkText(plate.rectTransform, Loc.T("smith.name"), UiTheme.TextSmall, Color.white, font);
        name.alignment = TextAlignmentOptions.Center;

        talkText = TalkText(brt, "", UiTheme.TextLarge, UISkin.Ink, font);
        talkText.rectTransform.offsetMin = new Vector2(400f, 30f);   // right of the portrait
        talkText.rectTransform.offsetMax = new Vector2(-60f, -50f);
        talkText.alignment = TextAlignmentOptions.TopLeft;
        talkText.textWrappingMode = TextWrappingModes.Normal;

        nextMark = TalkText(brt, Loc.T("smith.talk.next"), UiTheme.TextSmall, UISkin.Ink, font);
        nextMark.rectTransform.offsetMin = new Vector2(0f, 18f);
        nextMark.rectTransform.offsetMax = new Vector2(-40f, -180f);
        nextMark.alignment = TextAlignmentOptions.BottomRight;

        portrait.transform.SetAsLastSibling(); // in front of the box
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

    void Buy(int i)
    {
        var u = Order[i];
        if (MetaProgress.IsMaxed(u))
            Say(Loc.F("smith.maxed_already", Names[i], Topic(Names[i])));
        else if (MetaProgress.TryBuy(u))
        {
            Sfx.Play("ui_buy");
            Sfx.Play("coin");
            Say(MetaProgress.IsMaxed(u)
                ? Loc.F("smith.maxed_now", Names[i], MetaProgress.Level(u))
                : Loc.F("smith.upgraded", Names[i], MetaProgress.Level(u)));
        }
        else
        {
            Sfx.Play("ui_error");
            Say(Loc.F("smith.no_gold", Currency.Icon));
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

    void Say(string text)
    {
        if (messageText == null) return;
        messageText.text = text;
        messageTimer = 3f;
    }

    void Update()
    {
        if (messageTimer > 0f)
        {
            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0f && messageText != null) messageText.text = Loc.T("smith.prompt");
        }
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
            rowLabels[i].text = $"<b>{Names[i]}</b>  Lv.{lv}/{max}\n<size=75%><color={UiTheme.Html("amber")}>{Effects[i]}</color></size>";
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
    void Update() { if (owner != null) owner.UpdateTalk(); }
}
