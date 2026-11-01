using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class ItchTrapImporter
{
    const string Root = "Assets/ThirdParty/Itch/Traps/Foozle";
    const string RuntimeRoot = "Assets/Resources/Traps"; // the sheets the game loads (TrapHazard)

    readonly struct Sheet
    {
        public readonly string file;
        public readonly int width;
        public readonly int height;

        public Sheet(string file, int width, int height)
        {
            this.file = file;
            this.width = width;
            this.height = height;
        }
    }

    static readonly Sheet[] Sheets =
    {
        new Sheet("ceiling_spikes", 64, 64),
        new Sheet("saw", 64, 64),
        new Sheet("fire", 32, 64),
        new Sheet("toxic", 64, 64),
        new Sheet("arrow_trap", 32, 32),
    };

    [MenuItem("Cats Smash/Prepare Itch Trap Sprites")]
    public static void Prepare()
    {
        foreach (var sheet in Sheets) Slice(Root, sheet);
        foreach (var sheet in Sheets)
            if (System.IO.File.Exists($"{RuntimeRoot}/{sheet.file}.png")) Slice(RuntimeRoot, sheet);
        AssetDatabase.SaveAssets();
        Debug.Log("[ItchTrapImporter] Prepared Foozle trap animation sprites.");
    }

    static void Slice(string root, Sheet sheet)
    {
        string path = $"{root}/{sheet.file}.png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new System.Exception($"[ItchTrapImporter] Missing {path}");

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 32f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        int columns = texture.width / sheet.width;
        int rows = texture.height / sheet.height;
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        var oldIds = new Dictionary<string, GUID>();
        foreach (var old in provider.GetSpriteRects()) oldIds[old.name] = old.spriteID;
        var rects = new List<SpriteRect>();
        int frame = 0;
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                string name = $"{sheet.file}_{frame:00}";
                rects.Add(new SpriteRect
                {
                    name = name,
                    rect = new Rect(column * sheet.width, texture.height - (row + 1) * sheet.height,
                                    sheet.width, sheet.height),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    spriteID = oldIds.TryGetValue(name, out var id) ? id : GUID.Generate(),
                });
                frame++;
            }
        }

        provider.SetSpriteRects(rects.ToArray());
        var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (names != null)
        {
            var pairs = new List<SpriteNameFileIdPair>();
            foreach (var rect in rects) pairs.Add(new SpriteNameFileIdPair(rect.name, rect.spriteID));
            names.SetNameFileIdPairs(pairs);
        }
        provider.Apply();
        importer.SaveAndReimport();
    }
}
