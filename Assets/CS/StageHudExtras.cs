using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Two small pieces of the fight HUD that used to be lines of the counter plate or only a banner:
//   - the fish-bone count (icon and number, no plate) at the top centre (the empty strip between the hearts and the minimap);
//   - "스테이지 1-1" small in the bottom-left corner, above the set chips, so the stage is known after its banner is gone.
// Created by RunManager in the stage scenes; RunHudOverlay hides / dims it with the rest of the HUD.
public class StageHudExtras : MonoBehaviour
{
    TextMeshProUGUI goldText, stageText;
    int shownGold = -1, shownStage = -1;
    int stageNumber;

    public static StageHudExtras Create(RectTransform canvasRoot, TMP_FontAsset font, int stageNumber)
    {
        var go = new GameObject("StageHudExtras", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvasRoot, false);
        rt.SetAsFirstSibling(); // under the popups (card draft, result board), like the set chips
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var extras = go.AddComponent<StageHudExtras>();
        extras.stageNumber = stageNumber;

        // The fish-bone count, top centre: just the small icon and the number, no box behind them. A dark rim round
        // the letters keeps them readable over the level instead.
        extras.goldText = Text(rt, "Gold", UiTheme.TextLarge, UiTheme.Gold, TextAlignmentOptions.Top, font);
        var grt = extras.goldText.rectTransform;
        grt.anchorMin = grt.anchorMax = grt.pivot = new Vector2(0.5f, 1f);
        // The boss's health bar hangs from the top centre (20 to 104 down): the count goes under it there.
        bool boss = RunManager.Instance != null && stageNumber >= RunManager.Instance.totalStages;
        grt.anchoredPosition = new Vector2(0f, boss ? -112f : -16f);
        grt.sizeDelta = new Vector2(190f, 48f);
        TextOutline.Attach(extras.goldText, new Color32(24, 14, 10, 255), 2f);

        // The stage, bottom-left, above the set chips (which start 24 px from the bottom and are 44 high).
        extras.stageText = Text(rt, "Stage", UiTheme.TextSmall, UiTheme.Cream, TextAlignmentOptions.BottomLeft, font);
        var srt = extras.stageText.rectTransform;
        srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0f, 0f);
        srt.anchoredPosition = new Vector2(24f, 78f);
        srt.sizeDelta = new Vector2(300f, 36f);
        UiTheme.Backdrop(extras.stageText);
        return extras;
    }

    static TextMeshProUGUI Text(RectTransform parent, string name, float size, Color color, TextAlignmentOptions align, TMP_FontAsset font)
    {
        var t = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.rectTransform.SetParent(parent, false);
        if (font != null) t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    void Update()
    {
        var stats = PlayerStats.Instance;
        // The fish bone at 60% of the number's size (the full-size one dwarfed the count now that it has no plate).
        if (stats != null && stats.Gold != shownGold) { shownGold = stats.Gold; goldText.text = $"<size=60%>{Currency.Icon}</size>{shownGold}"; }
        if (stageNumber != shownStage) { shownStage = stageNumber; stageText.text = Loc.F("stage.number", StageTitle.Chapter, stageNumber); }
    }
}
