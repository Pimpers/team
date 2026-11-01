using UnityEditor;
using UnityEngine;

// Danaida's climbing plants (Resources/WorldProps/Vines) are drawn at the tiles' 16 px per unit.
// Imports every file there as a crisp, uncompressed pixel sprite.
public class VineSpriteImporter : AssetPostprocessor
{
    const string Folder = "Assets/Resources/WorldProps/Vines/";

    // Unity.exe -batchmode -quit -projectPath <project> -executeMethod VineSpriteImporter.Reimport
    public static void Reimport()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder.TrimEnd('/') }))
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder)) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
    }
}
