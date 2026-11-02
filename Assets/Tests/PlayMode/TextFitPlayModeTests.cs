using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Text that runs out of its box, found without looking: the main screens are opened and every visible TMP label
// whose text is wider (one line, no auto-size) or taller (wrapping) than its rect fails the test, naming it.
// Truncated / ellipsis labels and boxes with no size (they grow with the text) are left out - they don't overflow.
public class TextFitPlayModeTests
{
    const float Slack = 6f; // outlines and rounding

    [TearDown]
    public void Unpause() { GameClock.ResetAll(); BuildSheet.ForceShow = false; }

    [UnityTest]
    public IEnumerator Village_AndItsSettings_TextFits()
    {
        SceneManager.LoadScene("MainScene");
        yield return new WaitForSecondsRealtime(1f);
        AssertFits("village");
        var options = Object.FindAnyObjectByType<OptionsUI>(FindObjectsInactive.Include);
        Assert.That(options, Is.Not.Null);
        options.Open();
        yield return new WaitForSecondsRealtime(0.3f);
        AssertFits("village settings");
    }

    [UnityTest]
    public IEnumerator Stage_HudPauseAndTabSheet_TextFits()
    {
        SceneManager.LoadScene("Stage2");
        yield return new WaitForSecondsRealtime(2.5f);
        AssertFits("stage HUD");
        BuildSheet.ForceShow = true;
        yield return new WaitForSecondsRealtime(0.2f);
        AssertFits("Tab sheet");
        BuildSheet.ForceShow = false;
        Object.FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include).OpenForPlaytest();
        yield return new WaitForSecondsRealtime(0.3f);
        AssertFits("pause");
    }

    static void AssertFits(string screen)
    {
        Canvas.ForceUpdateCanvases();
        var bad = new List<string>();
        foreach (var t in Object.FindObjectsByType<TextMeshProUGUI>())
        {
            if (!t.isActiveAndEnabled || string.IsNullOrEmpty(t.text) || Hidden(t)) continue;
            if (t.enableAutoSizing || t.overflowMode == TextOverflowModes.Truncate || t.overflowMode == TextOverflowModes.Ellipsis) continue;
            var rect = t.rectTransform.rect;
            Vector2 need = t.GetPreferredValues(t.text, rect.width > 1f ? rect.width : 0f, 0f);
            bool oneLine = t.textWrappingMode == TextWrappingModes.NoWrap;
            if (oneLine && rect.width > 1f && need.x > rect.width + Slack)
                bad.Add($"{Path(t)}: {need.x:0} wide in {rect.width:0} \"{t.GetParsedText()}\"");
            else if (!oneLine && rect.height > 1f && need.y > rect.height + Slack)
                bad.Add($"{Path(t)}: {need.y:0} tall in {rect.height:0} \"{t.GetParsedText()}\"");
        }
        Assert.That(bad, Is.Empty, screen + ": text out of its box\n" + string.Join("\n", bad));
    }

    static bool Hidden(Component c)
    {
        foreach (var g in c.GetComponentsInParent<CanvasGroup>()) if (g.alpha <= 0.01f) return true;
        return false;
    }

    static string Path(Component c)
    {
        var t = c.transform;
        string p = t.name;
        for (int i = 0; i < 3 && t.parent != null; i++) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }
}
