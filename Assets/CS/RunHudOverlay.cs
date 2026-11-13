using System.Collections.Generic;
using UnityEngine;

// One rule for the fight HUD under every panel (UI_STYLE.md, "Overlays"):
//   - a panel that takes the whole screen and stops the fight (card draft, pause, route choice, result: the ones that
//     hold GameClock) hides the HUD entirely;
//   - a quick look that leaves the fight running (the Tab sheet, BuildSheet.Showing) dims it to Dim;
//   - otherwise it is fully there.
// The HUD pieces (hearts, dash bars, the counter plate, minimap, monster arrows, set badges) each get a CanvasGroup
// set from here; SkillHUD keeps its own group (it also fades when it covers the cat) and asks Alpha. Created by
// RunManager in the stage scenes.
public class RunHudOverlay : MonoBehaviour
{
    public const float Dim = 0.35f;
    static RunHudOverlay instance;
    readonly List<CanvasGroup> groups = new List<CanvasGroup>();
    float rescan;

    // 0 under a full panel, Dim under the Tab sheet, else 1.
    public static float Alpha => GameClock.Paused ? 0f : BuildSheet.Showing ? Dim : 1f;

    public static void Ensure()
    {
        if (instance != null) return;
        instance = new GameObject("RunHudOverlay").AddComponent<RunHudOverlay>();
    }

    void OnDestroy() { if (instance == this) instance = null; }

    void LateUpdate()
    {
        // the pieces appear at different moments (the minimap after the map, arrows on demand): look again now and then
        if ((rescan -= Time.unscaledDeltaTime) <= 0f) { rescan = 0.5f; Scan(); }
        float a = Alpha;
        foreach (var g in groups) if (g != null) { g.alpha = a; g.blocksRaycasts = a > 0f; }
    }

    void Scan()
    {
        groups.Clear();
        Add<HeartHUD>(); Add<DashBar>(); Add<StageMinimap>(); Add<MonsterTracker>(); Add<SetHUD>();
        var run = RunManager.Instance;
        if (run != null && run.hudText != null)
        {
            Group(run.hudText.gameObject);
            var backing = run.hudText.transform.parent != null ? run.hudText.transform.parent.Find("HudBacking") : null;
            if (backing != null) Group(backing.gameObject);
        }
    }

    void Add<T>() where T : Component
    {
        foreach (var c in FindObjectsByType<T>()) Group(c.gameObject);
    }

    void Group(GameObject go)
    {
        var g = go.GetComponent<CanvasGroup>();
        if (g == null) g = go.AddComponent<CanvasGroup>();
        groups.Add(g);
    }
}
