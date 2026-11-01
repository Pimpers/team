using UnityEditor;
using UnityEngine;

// Imports the selected CC0 world props on the same 16-pixel grid as the game.
public class WorldPropImport : AssetPostprocessor
{
    const string Root = "Assets/Resources/WorldProps/";

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root)) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        bool grounded = assetPath.Contains("/TreasureChest/") || assetPath.EndsWith("/SunnyLand/sign.png");
        settings.spriteAlignment = grounded ? (int)SpriteAlignment.BottomCenter : (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);
    }
}
