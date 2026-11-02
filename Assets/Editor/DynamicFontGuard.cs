using UnityEditor;
using UnityEngine;
using TMPro;

// Keeps the dynamic TMP font asset (Galmuri11 Dynamic SDF) out of git churn. A dynamic asset adds every glyph a run
// draws - atlas pixels, glyph / character tables and kerning pairs - to the asset itself, so each play or batch run
// left a different file behind. The game needs none of it saved: ClearDynamicDataOnBuild already empties it for a
// build, and the editor rebuilds glyphs on demand. So:
//   - while playing, a save of the asset is skipped (clearing it mid-play would garble text on screen);
//   - back in edit mode (and before a batch run quits) its dynamic data is cleared and saved, which is exactly the
//     committed state - an empty 1x1 atlas and empty tables - so git sees no change.
// A static atlas was the other option, but the player types their cat's name with any Hangul syllable, which a
// static atlas can't cover without a dynamic fallback that would churn the same way.
[InitializeOnLoad]
public static class DynamicFontGuard
{
    // Every dynamic font asset in the project: Galmuri11 (the UI), DreamHeumulKR (TMP Settings' fallback for the
    // glyphs Galmuri lacks - it collected them the same way) and Departure Mono (not used, but dynamic too).
    public static readonly string[] Paths =
    {
        "Assets/font/Galmuri/Galmuri11 Dynamic SDF.asset",
        "Assets/font/DreamHeumulKR Dynamic SDF.asset",
        "Assets/font/DepartureMono Dynamic SDF.asset",
    };

    static DynamicFontGuard()
    {
        EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredEditMode) ClearAll(); };
        EditorApplication.quitting += ClearAll;
    }

    [MenuItem("Cats Smash/Clear Dynamic Font Data")]
    public static void ClearAll()
    {
        if (EditorApplication.isPlaying) return; // clearing mid-play would garble the text on screen
        foreach (var path in Paths)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null || font.atlasPopulationMode != AtlasPopulationMode.Dynamic) continue;
            bool hasData = font.glyphTable.Count > 0 || font.characterTable.Count > 0
                || font.fontFeatureTable.glyphPairAdjustmentRecords.Count > 0
                || (font.atlasTexture != null && font.atlasTexture.width > 1);
            if (!hasData) continue;
            font.ClearFontAssetData(true);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssetIfDirty(font);
        }
    }

    class SaveFilter : AssetModificationProcessor
    {
        static string[] OnWillSaveAssets(string[] paths)
        {
            if (!EditorApplication.isPlaying) return paths;
            return System.Array.FindAll(paths, p => System.Array.IndexOf(Paths, p) < 0);
        }
    }
}
