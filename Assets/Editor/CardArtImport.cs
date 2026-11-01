using UnityEditor;

// The hand-drawn skill icons (Resources/SkillIcons/<card id>.png) import the same way.
// The card-draft backgrounds (Assets/Resources/Cards/card_common|rare|epic.png, one per card grade -
// see CardPool.Tier / CardDraftUI) import as crisp, uncompressed single sprites.
public class CardArtImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Cards/") && !assetPath.StartsWith("Assets/Resources/SkillIcons/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100f;
        ti.filterMode = UnityEngine.FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.npotScale = TextureImporterNPOTScale.None;
    }
}
