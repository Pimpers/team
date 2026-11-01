using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class VerticalEncounterController : StageEncounterController
{
    static Sprite white;
    static readonly Dictionary<int, Sprite> thornSprites = new Dictionary<int, Sprite>();

    public static void Create(StageMap map, GameObject enemyGroup)
    {
        if (map == null || !map.verticalLayout || map.verticalGateHeights == null || map.verticalGateHeights.Length == 0 || enemyGroup == null) return;
        var controller = new GameObject("VerticalEncounters").AddComponent<VerticalEncounterController>();
        controller.Configure(map, enemyGroup, map.verticalGateHeights);
    }

    protected override float Axis(Vector3 position) => position.y;
    protected override bool Passed(float edge) => player != null && player.Feet.y > edge + 0.6f;
    protected override void AddBarrier(float edge) => map.AddHorizontalBarrier(edge);
    protected override void RemoveBarrier(float edge) => map.RemoveHorizontalBarrier(edge);
    protected override Color GateEffectColor => new Color(1f, 0.45f, 0.42f);
    protected override Vector3 GateEffectPosition(float edge)
        => new Vector3(player != null ? player.transform.position.x : map.mapSize.x * 0.5f, edge, 0f);

    protected override Gate BuildGate(float edge)
    {
        var root = new GameObject("CrimsonVineGate");
        root.transform.SetParent(transform, false);
        root.transform.position = new Vector3(map.mapSize.x * 0.5f, edge, 0f);
        if (!BuildVineWall(root.transform, map.mapSize.x, true, new[] { "liana_5", "liana_4" }, 12))
        {
            var vine = root.AddComponent<SpriteRenderer>();
            vine.sprite = ThornSprite(map.mapSize.x);
            vine.sortingOrder = 12;
        }

        var plate = Part(root.transform, "LabelPlate", new Vector3(0f, -0.85f, 0f), new Vector3(4.2f, 0.68f, 1f),
                         UiTheme.Plate, 14);
        plate.enabled = false;
        var text = Label(root.transform, new Vector3(0f, -0.83f, 0f), new Color(1f, 0.72f, 0.62f));
        SpriteRenderer pointer = null;
        var arrow = Resources.Load<Sprite>("KenneyRpgUI/arrowBeige_left");
        if (arrow != null)
        {
            pointer = SpritePart(root.transform, "EnemyDirection", arrow, new Vector3(-2.65f, -0.83f, 0f),
                                 Vector3.one * 2f, new Color(1f, 0.55f, 0.42f), 16);
            pointer.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }
        text.gameObject.SetActive(false);
        if (pointer != null) pointer.gameObject.SetActive(false);
        return new Gate { edge = edge, root = root, plate = plate, label = text, pointer = pointer };
    }

    TextMeshPro Label(Transform parent, Vector3 local, Color color)
    {
        var text = new GameObject("GateLabel").AddComponent<TextMeshPro>();
        text.transform.SetParent(parent, false);
        text.transform.localPosition = local;
        text.rectTransform.sizeDelta = new Vector2(3.9f, 0.68f);
        text.fontSize = 3.4f; // world units: 1.35 drew the words 0.14 tall, a speck inside the plate
        text.enableAutoSizing = true; text.fontSizeMin = 2f; text.fontSizeMax = 3.4f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.outlineWidth = 0.16f;
        text.sortingOrder = 15;
        var hud = RunManager.Instance != null ? RunManager.Instance.hudText : null;
        if (hud != null) text.font = hud.font;
        return text;
    }

    static Sprite ThornSprite(int worldWidth)
    {
        if (thornSprites.TryGetValue(worldWidth, out var cached)) return cached;
        int width = Mathf.Max(16, worldWidth * 16), height = 12;
        var texture = RuntimeGeneratedAssets.Texture(width, height);
        var pixels = new Color32[width * height];
        var dark = new Color32(0x50, 0x16, 0x2d, 0xe8);
        var stem = new Color32(0xa3, 0x2d, 0x42, 0xec);
        var thorn = new Color32(0xe8, 0x55, 0x58, 0xec);
        for (int x = 0; x < width; x++)
        {
            int wave = (x / 8) % 4;
            int centre = wave == 0 ? 5 : wave == 1 ? 6 : wave == 2 ? 6 : 5;
            pixels[centre * width + x] = dark;
            if (centre + 1 < height) pixels[(centre + 1) * width + x] = stem;
            if (x % 30 == 8 && centre + 3 < height) pixels[(centre + 3) * width + x] = thorn;
            if (x % 30 == 22 && centre >= 2) pixels[(centre - 2) * width + x] = thorn;
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        cached = RuntimeGeneratedAssets.Sprite(texture, new Vector2(0.5f, 0.5f), 16f);
        thornSprites[worldWidth] = cached;
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
        thornSprites.Clear();
    }
}
