using UnityEngine;
using TMPro;

// A tip shown once, when its moment first comes, instead of all of them in a row at the end of the tutorial:
// the route fork when the first fork opens, Q/E/R when the first active skill is taken, the Tab sheet after the
// first card, 타락도 when the world map first offers a tier. (The # tag sets have their own line on the first
// draft - CardDraftUI.ShowSetTip.) Seen tips are kept in the save (SaveData.seenTips); the text is the
// tutorial's tutorial.tip.<id> line.
public static class OneTimeTip
{
    public static bool Seen(string id) => ("," + GameSave.Data.seenTips + ",").Contains("," + id + ",");

    public static void Show(string id, Canvas canvas, TMP_FontAsset font)
    {
        if (canvas == null || Seen(id)) return;
        GameSave.Data.seenTips = string.IsNullOrEmpty(GameSave.Data.seenTips) ? id : GameSave.Data.seenTips + "," + id;
        GameSave.Save();
        Sfx.Play("tip_chime");
        StageBanner.Show(canvas.rootCanvas, font, Loc.T("tip.title"), Loc.T("tutorial.tip." + id), 3.5f, compact: true);
    }

    // In a stage: on the HUD's canvas (none in the tutorial stage: no draft, fork or skill happens there).
    public static void ShowInRun(string id)
    {
        var run = RunManager.Instance;
        if (run == null || run.tutorial || run.hudText == null || run.hudText.canvas == null) return;
        Show(id, run.hudText.canvas, run.hudText.font);
    }
}
