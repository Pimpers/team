using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// The new UI art (Assets/Resources/UICats/ui_*.png, cut from the 12 UI sheets): crisp, uncompressed single sprites at 100 PPU.
// The stretchable ones carry their 9-slice border here (left, bottom, right, top in art pixels); the rest are fixed.
// Also the three Nature buttons the skin uses (they were replaced by the new wood / green / grey buttons).
public class UiCatsImport : AssetPostprocessor
{
    static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
    {
        { "ui_button_wood", new Vector4(5, 5, 5, 5) }, { "ui_button_wood_down", new Vector4(5, 5, 5, 5) },
        { "ui_button_grey", new Vector4(5, 5, 5, 5) }, { "ui_button_grey_down", new Vector4(5, 5, 5, 5) },
        { "ui_button_green", new Vector4(5, 5, 5, 5) }, { "ui_button_green_down", new Vector4(5, 5, 5, 5) },
        { "nature_button_wood", new Vector4(5, 5, 5, 5) }, { "nature_button_grey", new Vector4(5, 5, 5, 5) }, { "nature_button_green", new Vector4(5, 5, 5, 5) },
        { "ui_ribbon", new Vector4(16, 8, 16, 8) }, { "ui_minimap", new Vector4(9, 9, 9, 9) }, { "ui_band", new Vector4(14, 4, 14, 4) },
        { "ui_result_win", new Vector4(28, 22, 28, 20) }, { "ui_result_lose", new Vector4(28, 22, 28, 20) },
        { "ui_tag", new Vector4(16, 18, 16, 16) }, { "ui_slot", new Vector4(16, 16, 16, 16) }, { "ui_slot_cd", new Vector4(16, 16, 16, 16) },
        { "ui_slider_a", new Vector4(6, 3, 6, 3) }, { "ui_slider_b", new Vector4(6, 3, 6, 3) },
        { "ui_slider_track", new Vector4(4, 3, 4, 3) }, { "ui_slider_fill", new Vector4(4, 3, 4, 3) },
    };

    void OnPreprocessTexture()
    {
        bool cats = assetPath.StartsWith("Assets/Resources/UICats/");
        string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        bool natureButton = assetPath.StartsWith("Assets/UI/Nature/nature_button_") && Borders.ContainsKey(name);
        if (!cats && !natureButton) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = natureButton ? 200f : 100f; // (the Nature sprites are 200 px per unit, see UISkinApplier)
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.spriteBorder = Borders.TryGetValue(name, out var b) ? b : Vector4.zero;
    }
}
