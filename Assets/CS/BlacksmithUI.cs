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

    public bool IsOpen => panel != null && panel.activeSelf;

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

    public void Open()
    {
        panel.SetActive(true);
        Say(Loc.T("smith.greeting"));
        Refresh();
    }

    public void Close()
    {
        panel.SetActive(false);
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
