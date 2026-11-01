using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class HorizontalEncounterController : StageEncounterController
{
    static Sprite white;
    static readonly Dictionary<int, Sprite> vineSprites = new Dictionary<int, Sprite>();

    public static void Create(StageMap map, GameObject enemyGroup)
    {
        if (map == null || map.verticalLayout || map.mapSize.x < 80 || enemyGroup == null) return;
        var controller = new GameObject("HorizontalEncounters").AddComponent<HorizontalEncounterController>();
        float[] edges = map.horizontalGatePositions != null && map.horizontalGatePositions.Length > 0
            ? map.horizontalGatePositions
            : new[] { map.mapSize.x * 0.25f, map.mapSize.x * 0.5f, map.mapSize.x * 0.75f };
        controller.Configure(map, enemyGroup, edges);
    }

    protected override float Axis(Vector3 position) => position.x;
    protected override bool Passed(float edge) => player != null && player.transform.position.x > edge + 0.6f;
    protected override void AddBarrier(float edge) => map.AddVerticalBarrier(edge);
    protected override void RemoveBarrier(float edge) => map.RemoveVerticalBarrier(edge);
    protected override Color GateEffectColor => new Color(0.62f, 1f, 0.38f);
    protected override Vector3 GateEffectPosition(float edge)
        => new Vector3(edge, player != null ? player.Feet.y + 1f : map.playerStart.y + 1f, 0f);

    protected override void OnRoomOpened()
    {
        if (PlayerStats.Instance != null) PlayerStats.Instance.Heal(0.5f);
        if (player != null)
            DamagePopup.SpawnLabel(player.transform.position + Vector3.up * 2.1f,
                                   Loc.T("encounter.area_clear_heal"), GateEffectColor, 0.65f);
    }

    protected override Gate BuildGate(float edge)
    {
        var root = new GameObject("ForestVineGate");
        root.transform.SetParent(transform, false);
        root.transform.position = new Vector3(edge, map.mapSize.y * 0.5f, 0f);
        if (!BuildVineWall(root.transform, map.mapSize.y, false, new[] { "liana_2", "liana_7" }, 12))
        {
            var vine = root.AddComponent<SpriteRenderer>();
            vine.sprite = VineSprite(map.mapSize.y);
            vine.sortingOrder = 12;
        }

        float localY = map.playerStart.y + 3.5f - map.mapSize.y * 0.5f;
        var plate = Part(root.transform, "LabelPlate", new Vector3(-2.1f, localY, 0f), new Vector3(3.6f, 0.62f, 1f),
                         UiTheme.Plate, 14);
        plate.enabled = false;
        var text = Label(root.transform, new Vector3(-2.1f, localY + 0.02f, 0f), new Color(0.85f, 1f, 0.68f));
        SpriteRenderer pointer = null;
        var arrow = Resources.Load<Sprite>("KenneyRpgUI/arrowBeige_left");
        if (arrow != null)
            pointer = SpritePart(root.transform, "EnemyDirection", arrow, new Vector3(-4.2f, localY, 0f),
                                 Vector3.one * 2f, new Color(0.72f, 1f, 0.45f), 16);
        text.gameObject.SetActive(false);
        if (pointer != null) pointer.gameObject.SetActive(false);
        return new Gate { edge = edge, root = root, plate = plate, label = text, pointer = pointer };
    }

    TextMeshPro Label(Transform parent, Vector3 local, Color color)
    {
        var text = new GameObject("GateLabel").AddComponent<TextMeshPro>();
        text.transform.SetParent(parent, false);
        text.transform.localPosition = local;
        text.rectTransform.sizeDelta = new Vector2(3.3f, 0.62f);
        text.fontSize = 3.2f; // world units: 1.2 drew the words 0.12 tall, a speck inside the 0.62-tall plate
        text.enableAutoSizing = true; text.fontSizeMin = 2f; text.fontSizeMax = 3.2f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.outlineWidth = 0.16f;
        text.sortingOrder = 15;
        var hud = RunManager.Instance != null ? RunManager.Instance.hudText : null;
        if (hud != null) text.font = hud.font;
        return text;
    }

    static Sprite VineSprite(int worldHeight)
    {
        if (vineSprites.TryGetValue(worldHeight, out var cached)) return cached;
        int width = 12, height = Mathf.Max(16, worldHeight * 16);
        var texture = RuntimeGeneratedAssets.Texture(width, height);
        var pixels = new Color32[width * height];
        var stemDark = new Color32(0x28, 0x3b, 0x19, 0xe8);
        var stem = new Color32(0x4c, 0x73, 0x29, 0xe8);
        var leaf = new Color32(0x68, 0x9d, 0x35, 0xec);
        for (int y = 0; y < height; y++)
        {
            int wave = (y / 8) % 4;
            int centre = wave == 0 ? 5 : wave == 1 ? 6 : wave == 2 ? 6 : 5;
            pixels[y * width + centre] = stemDark;
            pixels[y * width + centre + 1] = stem;
            if (y % 28 == 9)
            {
                for (int dx = 1; dx <= 3; dx++) pixels[y * width + centre - dx] = leaf;
                if (y + 1 < height) pixels[(y + 1) * width + centre - 2] = leaf;
            }
            if (y % 28 == 22)
            {
                for (int dx = 2; dx <= 4 && centre + dx < width; dx++) pixels[y * width + centre + dx] = leaf;
                if (y + 1 < height && centre + 3 < width) pixels[(y + 1) * width + centre + 3] = leaf;
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        cached = RuntimeGeneratedAssets.Sprite(texture, new Vector2(0.5f, 0.5f), 16f);
        vineSprites[worldHeight] = cached;
        return cached;
    }

    static SpriteRenderer Part(Transform parent, string name, Vector3 local, Vector3 scale, Color color, int order)
        => SpritePart(parent, name, White, local, scale, color, order);

    static SpriteRenderer SpritePart(Transform parent, string name, Sprite sprite, Vector3 local, Vector3 scale, Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        go.transform.localScale = scale;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = order;
        return renderer;
    }

    static Sprite White => white != null ? white : (white = RuntimeGeneratedAssets.WhiteSprite());

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCache()
    {
        white = null;
        vineSprites.Clear();
    }
}
