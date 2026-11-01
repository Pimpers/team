using UnityEditor;
using UnityEngine;

// Imports the selected Kenney RPG UI sprites for scalable Unity UI panels and buttons.
public static class KenneyRpgUiAssets
{
    const string Dir = "Assets/Resources/KenneyRpgUI";

    [MenuItem("Cats Smash/Apply Kenney RPG UI")]
    public static void ApplyMenu() => Apply();

    public static void ApplyBatch()
    {
        try { Apply(); }
        catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static void Apply()
    {
        AssetDatabase.Refresh();
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Dir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            bool panel = path.Contains("panel_");
            bool button = path.Contains("buttonLong_") || path.Contains("buttonRound_") || path.Contains("buttonSquare_");
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
                importer.filterMode != FilterMode.Point || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.mipmapEnabled ||
                (panel || button) && importer.spriteBorder != new Vector4(8f, 8f, 8f, 8f))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                if (panel || button) importer.spriteBorder = new Vector4(8f, 8f, 8f, 8f);
                importer.SaveAndReimport();
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[KenneyRpgUiAssets] Imported Kenney RPG UI sprites.");
    }
}
