using UnityEditor;
using UnityEngine;

// Import settings for the stage-exit art in Assets/Resources/StageExit (drawn with pxl: Tools/pxl/pxl_stage_exit.px.js;
// the hollow log, the sweeping leaves, the firefly) and the enemies' effect sprites in
// Assets/Resources/EnemyFx (Tools/pxl/pxl_spider_web.px.js: the spider's web). Pixel art at the tiles' 16 px per unit,
// point filter, no compression; the logs stand on their bottom edge, the rest is centred.
// Applied on import, and once on load for files that came in before this script compiled.
public class StageExitImport : AssetPostprocessor
{
    static readonly string[] Dirs = { "Assets/Resources/StageExit/", "Assets/Resources/EnemyFx/" };
    static bool Ours(string path) { foreach (var d in Dirs) if (path.StartsWith(d)) return true; return false; }

    void OnPreprocessTexture()
    {
        if (!Ours(assetPath)) return;
        Configure((TextureImporter)assetImporter, assetPath);
    }

    static bool Configure(TextureImporter ti, string path)
    {
        bool log = System.IO.Path.GetFileNameWithoutExtension(path).StartsWith("exit_log") || System.IO.Path.GetFileNameWithoutExtension(path).StartsWith("exit_portal") || System.IO.Path.GetFileNameWithoutExtension(path).StartsWith("exit_signpole"); // stand on the floor
        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        int align = (int)(log ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
        bool ok = ti.textureType == TextureImporterType.Sprite && ti.spriteImportMode == SpriteImportMode.Single &&
                  Mathf.Approximately(ti.spritePixelsPerUnit, 16f) && ti.filterMode == FilterMode.Point &&
                  ti.textureCompression == TextureImporterCompression.Uncompressed && !ti.mipmapEnabled && s.spriteAlignment == align;
        if (ok) return false;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 16f;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.ReadTextureSettings(s);
        s.spriteAlignment = align;
        ti.SetTextureSettings(s);
        return true;
    }

    [InitializeOnLoadMethod]
    static void FixExisting()
    {
        EditorApplication.delayCall += () =>
        {
            foreach (var dir in Dirs)
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { dir.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is TextureImporter ti && Configure(ti, path)) ti.SaveAndReimport();
            }
        };
    }
}
