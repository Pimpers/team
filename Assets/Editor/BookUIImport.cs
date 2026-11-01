using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Import settings for the book-style UI sprites in Assets/Resources/UIBook (Crusenho's "Complete UI Book Styles Pack",
// free version - licence and credit in Assets/UI/Book/License.txt) and the pieces cut from ToffeeCraft's UI bundle in
// Assets/Resources/UIBundle (Assets/UI/Bundle/CREDIT.txt). Pixel art: point filter, no compression, 100 px/unit
// like the canvases' reference, and 9-slice borders for the pieces that stretch. The cursor is a Cursor texture.
// Applied on import, and once on load for files that came in before this script compiled.
public class BookUIImport : AssetPostprocessor
{
    static readonly string[] Dirs = { "Assets/Resources/UIBook/", "Assets/Resources/UIBundle/" };
    static bool Ours(string path) { foreach (var d in Dirs) if (path.StartsWith(d)) return true; return false; }

    // 9-slice border (left, bottom, right, top) in sprite pixels; pieces not listed are drawn whole.
    static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
    {
        { "Slot01a", new Vector4(3, 3, 3, 3) },
        { "Slot01b", new Vector4(3, 3, 3, 3) },
        { "Select01a", new Vector4(6, 6, 6, 6) },
        { "MedievalBoard", new Vector4(12, 12, 12, 12) },
        { "MedievalBarFrame", new Vector4(8, 4, 7, 4) },
    };

    void OnPreprocessTexture()
    {
        if (!Ours(assetPath)) return;
        Configure((TextureImporter)assetImporter, assetPath);
    }

    static bool Configure(TextureImporter ti, string path)
    {
        string name = System.IO.Path.GetFileNameWithoutExtension(path);
        bool cursor = name.StartsWith("Cursor");
        var border = Borders.TryGetValue(name, out var b) ? b : Vector4.zero;
        bool ok = ti.filterMode == FilterMode.Point && ti.textureCompression == TextureImporterCompression.Uncompressed && !ti.mipmapEnabled &&
                  (cursor ? ti.textureType == TextureImporterType.Cursor && ti.isReadable
                          : ti.textureType == TextureImporterType.Sprite && ti.spriteImportMode == SpriteImportMode.Single &&
                            Mathf.Approximately(ti.spritePixelsPerUnit, 100f) && ti.spriteBorder == border);
        if (ok) return false;
        ti.textureType = cursor ? TextureImporterType.Cursor : TextureImporterType.Sprite;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.npotScale = TextureImporterNPOTScale.None;
        if (cursor) ti.isReadable = true;
        else
        {
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 100f;
            ti.spriteBorder = border;
        }
        return true;
    }

    [InitializeOnLoadMethod]
    static void FixExisting()
    {
        EditorApplication.delayCall += () =>
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", System.Array.ConvertAll(Dirs, d => d.TrimEnd('/'))))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is TextureImporter ti && Configure(ti, path)) ti.SaveAndReimport();
            }
        };
    }
}
