using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The village's "다음 목표" board (right side, under the settings gear): what the next runs can open, so the
// unlocks aren't news only on the result board - the next blacksmith upgrade and its price (or the 행운의 방울
// still behind the counter), the next 타락도 tier and what wins it, and how many cards the chosen cat can draw.
// Rebuilt whenever the wallet or the visit count changes. Created by MainSceneController.
public class VillageGoals : MonoBehaviour
{
    TextMeshProUGUI text;
    int shownGold = -1, shownVisits = -1;

    public static VillageGoals Create(Canvas canvas, TMP_FontAsset font, bool ranged)
    {
        var go = new GameObject("VillageGoals", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-24f, -270f);
        rt.sizeDelta = new Vector2(500f, 196f); // the ribbon over the top, three lines in the slot (no wider: BlacksmithUI.RightColumn keeps the bubble clear of 560)
        var bg = go.GetComponent<Image>();
        UiPanels.Board(bg); // the riveted wooden board, the title on the red ribbon across its top
        bg.raycastTarget = false;
        UiPanels.Ribbon(rt, Loc.T("goals.title"), font, 280f, 56f);
        var slot = (RectTransform)UiPanels.Inset(rt, "Slot", new Vector2(18f, -40f), new Vector2(464f, 138f)).transform;

        var goals = go.AddComponent<VillageGoals>();
        goals.text = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        var trt = goals.text.rectTransform;
        trt.SetParent(slot, false);
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(18f, 10f); trt.offsetMax = new Vector2(-14f, -14f);
        if (font != null) goals.text.font = font;
        goals.text.fontSize = UiTheme.TextSmall;
        goals.text.color = UISkin.OverlayText;
        goals.text.alignment = TextAlignmentOptions.TopLeft;
        goals.text.textWrappingMode = TextWrappingModes.Normal; // two goals in a three-line slot: a long upgrade name wraps instead of running off the board
        goals.text.lineSpacing = 6f;
        goals.text.raycastTarget = false;
        goals.Refresh();
        return goals;
    }

    void Update()
    {
        if (MetaProgress.Gold != shownGold || GameSave.Data.smithVisits != shownVisits) Refresh();
    }

    void Refresh()
    {
        shownGold = MetaProgress.Gold;
        shownVisits = GameSave.Data.smithVisits;
        var sb = new System.Text.StringBuilder();
        // Goals, not facts: where to clear next, and how far the gear is upgraded.
        sb.Append(CorruptionLine());
        sb.Append(SmithLine());
        text.text = sb.ToString().TrimEnd('\n');
    }

    // The next gear upgrade as its own goal ("공격력 2레벨 강화"), the cheapest one still open: affordable now, or how
    // much gold is still missing. (It used to be every upgrade's levels added into one "12/12" count.) The 행운의 방울
    // behind the counter is its own goal until it opens.
    static string SmithLine()
    {
        if (BlacksmithUI.RerollLocked) return Loc.F("goals.smith_locked", BlacksmithUI.RerollUnlockVisit);
        int best = int.MaxValue;
        MetaProgress.Upgrade next = MetaProgress.Upgrade.Attack;
        foreach (MetaProgress.Upgrade u in System.Enum.GetValues(typeof(MetaProgress.Upgrade)))
        {
            if (MetaProgress.IsMaxed(u)) continue;
            int cost = MetaProgress.Cost(u);
            if (cost < best) { best = cost; next = u; }
        }
        if (best == int.MaxValue) return Loc.T("goals.smith_done");
        string name = Loc.T("smith." + next.ToString().ToLowerInvariant()); // smith.attack / speed / heart / reroll / starter
        int target = MetaProgress.Level(next) + 1;
        return MetaProgress.Gold >= best ? Loc.F("goals.smith_ready", name, target) : Loc.F("goals.smith_need", name, target, best - MetaProgress.Gold);
    }

    static string CorruptionLine()
    {
        int unlocked = Corruption.Unlocked;
        if (unlocked >= Corruption.MaxLevel) return Loc.T("goals.corruption_done");
        return unlocked == 0 ? Loc.T("goals.corruption_first") : Loc.F("goals.corruption_next", unlocked + 1, unlocked);
    }
}
