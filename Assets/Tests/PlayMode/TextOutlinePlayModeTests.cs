using System.Collections;
using System.Diagnostics;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

// The cost of UiTheme.Outline (four dark copies per label): 60 outlined labels - more than any screen has (the
// card draft has 3) - kept up to date for 200 frames. A frame where nothing changed must not rebuild the copies'
// meshes, and the per-label cost of keeping them in step stays far under the frame budget.
public class TextOutlinePlayModeTests
{
    [UnityTest]
    public IEnumerator Outline_IsCheapAndRebuildsOnlyOnChange()
    {
        var canvas = new GameObject("Canvas", typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var labels = new TextMeshProUGUI[60];
        for (int i = 0; i < labels.Length; i++)
        {
            var go = new GameObject("L" + i, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(canvas.transform, false);
            labels[i] = go.GetComponent<TextMeshProUGUI>();
            labels[i].text = "희귀 " + i;
            UiTheme.Outline(labels[i]);
        }
        yield return null;
        yield return null;

        // after settling, an unchanged label leaves its copies alone
        var copy = canvas.transform.Find("L0_Edge0").GetComponent<TextMeshProUGUI>();
        yield return null;
        Assert.That(copy.havePropertiesChanged, Is.False, "the copies rebuilt with nothing changed");

        var outlines = canvas.GetComponentsInChildren<TextOutline>();
        var watch = Stopwatch.StartNew();
        for (int f = 0; f < 200; f++) foreach (var o in outlines) o.SendMessage("LateUpdate");
        watch.Stop();
        double perLabelUs = watch.Elapsed.TotalMilliseconds * 1000.0 / (200 * outlines.Length);
        UnityEngine.Debug.Log($"[TextOutline] {perLabelUs:F2} us per label per frame ({outlines.Length} labels)");
        Assert.That(perLabelUs, Is.LessThan(50.0), "keeping one outline in step costs too much");
        Object.Destroy(canvas.gameObject);
    }
}
