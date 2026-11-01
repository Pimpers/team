using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

// Builds the TextMeshPro sprite asset for the gold fish-bone icon (Assets/UI/FishBoneGold.png,
// the user's picture turned into 33x19 pixel art) and makes it TMP's default sprite asset,
// so any text can show it with Currency.Icon (<sprite name="gold">). The sprite asset has no
// own point size, so TMP scales the icon to the text's ascent - it always matches the text.
public static class CurrencyIcon
{
    const string TexturePath = "Assets/UI/FishBoneGold.png";
    const string AssetPath = "Assets/UI/GoldSprite.asset";

    public static void Ensure()
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        if (ti == null) throw new System.Exception($"{TexturePath} not found");
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.SaveAndReimport();
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);

        var sa = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(AssetPath);
        if (sa == null)
        {
            sa = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            sa.name = "GoldSprite";
            AssetDatabase.CreateAsset(sa, AssetPath);
        }

        // A sprite asset made from code has no version yet, which makes TMP try to "upgrade"
        // it from the legacy sprite list (null) and throw - mark it as current format.
        sa.spriteInfoList = new System.Collections.Generic.List<TMP_Sprite>();
        var vso = new SerializedObject(sa);
        var version = vso.FindProperty("m_Version");
        if (version != null && string.IsNullOrEmpty(version.stringValue))
        {
            version.stringValue = "1.1.0";
            vso.ApplyModifiedPropertiesWithoutUndo();
        }

        if (sa.material == null)
        {
            var mat = new Material(Shader.Find("TextMeshPro/Sprite")) { name = "GoldSprite Material" };
            AssetDatabase.AddObjectToAsset(mat, sa);
            sa.material = mat;
        }
        sa.material.SetTexture(ShaderUtilities.ID_MainTex, tex);
        sa.spriteSheet = tex;

        int w = tex.width, h = tex.height;
        sa.spriteGlyphTable.Clear();
        sa.spriteCharacterTable.Clear();
        // Drawn 1.6x the text height, dipping a bit below the baseline so it stays centred
        // on the text, with a little space after.
        var glyph = new TMP_SpriteGlyph(0, new GlyphMetrics(w, h, 0f, h * 0.78f, w + 3f), new GlyphRect(0, 0, w, h), 1f, 0);
        sa.spriteGlyphTable.Add(glyph);
        var character = new TMP_SpriteCharacter(0xE000, glyph) { name = "gold", scale = 1.6f };
        sa.spriteCharacterTable.Add(character);
        sa.UpdateLookupTables();
        EditorUtility.SetDirty(sa);

        var settings = Resources.Load<TMP_Settings>("TMP Settings");
        if (settings != null)
        {
            var so = new SerializedObject(settings);
            var prop = so.FindProperty("m_defaultSpriteAsset");
            if (prop != null && prop.objectReferenceValue != sa)
            {
                prop.objectReferenceValue = sa;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        AssetDatabase.SaveAssets();
    }
}
