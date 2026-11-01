using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Menu: Cats Smash > Link Scene Texts To Table
//
// Ties the Korean texts saved in the scenes to the text table (Resources/Strings/ko.txt): each TMP text whose
// line is a table value gets a LocText with that key, and a text that already has one is refreshed from the
// table (so an edited line shows in the editor too). UISkinApplier.ApplyAndSave runs it, so rebuilt scenes
// stay linked. A Korean text with no matching table line is listed in the console - add the line to ko.txt.
public static class SceneTextLinker
{
    [MenuItem("Cats Smash/Link Scene Texts To Table")]
    public static void LinkBuildScenes()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var missing = new List<string>();
        int linked = 0;
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (!s.enabled || !System.IO.File.Exists(s.path)) continue;
            var scene = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
            int n = Link(scene, missing);
            if (n > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            linked += n;
        }
        Report(linked, missing);
    }

    // Returns how many texts were linked or refreshed; texts with no table line go into `missing`.
    public static int Link(Scene scene, List<string> missing)
    {
        Loc.Reload();
        var keyOf = new Dictionary<string, string>();
        foreach (var pair in Loc.All) if (!keyOf.ContainsKey(pair.Value)) keyOf[pair.Value] = pair.Key; // first key wins
        int changed = 0;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                var loc = t.GetComponent<LocText>();
                if (loc != null && Loc.Has(loc.key))
                {
                    string line = Loc.T(loc.key);
                    if (t.text == loc.source && line != loc.source)
                    {
                        Undo.RecordObjects(new Object[] { t, loc }, "Refresh scene text");
                        t.text = line; loc.source = line; changed++;
                    }
                    continue;
                }
                if (!HasHangul(t.text)) continue;
                if (!keyOf.TryGetValue(t.text, out var key)) { missing.Add($"{scene.name}: {PathOf(t.transform)} \"{t.text}\""); continue; }
                if (loc == null) loc = Undo.AddComponent<LocText>(t.gameObject);
                loc.key = key;
                loc.source = t.text;
                changed++;
            }
        return changed;
    }

    public static void Report(int linked, List<string> missing)
    {
        if (missing.Count > 0)
            Debug.LogWarning($"[SceneTextLinker] {missing.Count} scene text(s) have no line in Resources/Strings/ko.txt:\n" + string.Join("\n", missing));
        Debug.Log($"[SceneTextLinker] linked or refreshed {linked} scene text(s).");
    }

    static bool HasHangul(string s) { foreach (char c in s ?? "") if (c >= '가' && c <= '힣') return true; return false; }

    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
