using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Top-right minimap for side-scrolling stages: the whole StageMap drawn once into a small
// texture (ground, grass tops, platforms), with live markers for the player (gold), the
// monsters (red, the boss bigger) and the area the camera currently shows. Built from code
// by RunManager, so no scene setup is needed.
public class StageMinimap : MonoBehaviour
{
    public static StageMinimap Instance { get; private set; }
    // Up to 6 px per tile, smaller for wide maps so the panel stays at most MaxWidth wide.
    const float MaxPixelsPerTile = 6f;
    const float MaxWidth = 420f;
    const float MaxHeight = 180f;
    const float Margin = 16f;
    const float Pad = 12f;

    static readonly Color32 Ground = new Color32(0x6b, 0x4a, 0x35, 0xff);
    static readonly Color32 GroundTop = new Color32(0x7c, 0xb3, 0x4f, 0xff);
    static readonly Color32 Platform = new Color32(0xd8, 0xb8, 0x7a, 0xff);
    static readonly Color32 Sky = new Color32(0x1c, 0x2a, 0x3a, 0xff);
    static Color PlayerColor => UiTheme.PlayerMark; // (색약 모드 swaps these: UiTheme)
    static Color EnemyColor => UiTheme.EnemyMark;
    static readonly Color BossColor = new Color(0.75f, 0.05f, 0.2f);
    static readonly Color ChestColor = new Color(1f, 0.75f, 0.12f);
    static readonly Color ViewColor = new Color(1f, 1f, 1f, 0.55f);

    StageMap map;
    Transform player;
    Camera cam;
    RectTransform mapRect;
    RectTransform playerMarker;
    RectTransform exitMarker;
    Image exitImage;
    static Color ExitColor => UiTheme.ExitMark;
    RectTransform[] viewEdges;
    RawImage mapImage;
    Texture2D mapTexture;
    readonly List<(Enemy enemy, RectTransform marker)> enemies = new List<(Enemy, RectTransform)>();
    readonly List<(RouteChest chest, RectTransform marker)> chests = new List<(RouteChest, RectTransform)>();
    readonly HashSet<Enemy> known = new HashSet<Enemy>();
    readonly HashSet<RouteChest> knownChests = new HashSet<RouteChest>();

    void Awake() => Instance = this;
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (mapTexture != null) Destroy(mapTexture);
    }

    TMP_Text legend;
    float legendTimer = 5f;

    public static StageMinimap Create(Canvas canvas, StageMap map, Transform player, TMP_FontAsset font)
    {
        var go = new GameObject("Minimap", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsFirstSibling(); // behind any popup panels
        var minimap = go.AddComponent<StageMinimap>();
        minimap.Build(map, player, font);
        return minimap;
    }

    void Build(StageMap stageMap, Transform playerTransform, TMP_FontAsset font)
    {
        map = stageMap;
        player = playerTransform;
        cam = Camera.main;
        legendTimer = RunStats.Stage <= 1 ? 5f : 0f;

        float ppt = Mathf.Min(MaxPixelsPerTile,
            Mathf.Min(MaxWidth / Mathf.Max(1, map.mapSize.x), MaxHeight / Mathf.Max(1, map.mapSize.y)));
        Vector2 mapSize = new Vector2(map.mapSize.x, map.mapSize.y) * ppt;
        var panel = (RectTransform)transform;
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1f, 1f);
        panel.anchoredPosition = new Vector2(-Margin, -Margin);
        panel.sizeDelta = mapSize + Vector2.one * Pad * 2f;
        var bg = gameObject.AddComponent<Image>();
        bg.sprite = null;
        bg.color = UISkin.OverlayBack;
        bg.raycastTarget = false;
        BuildFrame(panel);

        var mapGo = new GameObject("Map", typeof(RectTransform));
        mapRect = (RectTransform)mapGo.transform;
        mapRect.SetParent(panel, false);
        mapRect.anchorMin = mapRect.anchorMax = mapRect.pivot = new Vector2(0f, 0f);
        mapRect.anchoredPosition = Vector2.one * Pad;
        mapRect.sizeDelta = mapSize;
        var raw = mapGo.AddComponent<RawImage>();
        mapImage = raw;
        mapTexture = DrawMap();
        raw.texture = mapTexture;
        raw.raycastTarget = false;

        viewEdges = new RectTransform[4];
        for (int i = 0; i < 4; i++) viewEdges[i] = Marker("View", ViewColor, Vector2.one);

        // Way out: a green diamond, hidden until the exit log appears.
        exitMarker = Marker("Exit", ExitColor, Vector2.one * 10f);
        exitMarker.localRotation = Quaternion.Euler(0f, 0f, 45f);
        exitImage = exitMarker.GetComponent<Image>();
        exitMarker.gameObject.SetActive(false);

        playerMarker = Marker("Player", PlayerColor, Vector2.one * 9f);
        BuildLegend(panel, font);
        LateUpdate(); // also adds a marker for every enemy already in the scene
    }

    void BuildFrame(RectTransform panel)
    {
        const float edge = 10f;
        FramePiece(panel, "Top", "top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, edge), true);
        FramePiece(panel, "Bottom", "bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, edge), true);
        FramePiece(panel, "Left", "left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(edge, 0f), true);
        FramePiece(panel, "Right", "right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(edge, 0f), true);
        FramePiece(panel, "TopLeft", "top_left", Vector2.up, Vector2.up, Vector2.up, Vector2.zero, Vector2.one * edge, false);
        FramePiece(panel, "TopRight", "top_right", Vector2.one, Vector2.one, Vector2.one, Vector2.zero, Vector2.one * edge, false);
        FramePiece(panel, "BottomLeft", "bottom_left", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one * edge, false);
        FramePiece(panel, "BottomRight", "bottom_right", Vector2.right, Vector2.right, Vector2.right, Vector2.zero, Vector2.one * edge, false);
    }

    static void FramePiece(RectTransform parent, string name, string resource,
                           Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
                           Vector2 position, Vector2 size, bool tiled)
    {
        var sprite = Resources.Load<Sprite>("ItchUI/MinimapFrame/" + resource);
        if (sprite == null) return;
        var go = new GameObject("Frame" + name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = tiled ? Image.Type.Tiled : Image.Type.Simple;
        image.pixelsPerUnitMultiplier = tiled ? 0.7f : 1f;
        image.raycastTarget = false;
    }

    void BuildLegend(RectTransform panel, TMP_FontAsset font)
    {
        var plateGo = new GameObject("LegendPlate", typeof(RectTransform), typeof(Image));
        var plate = (RectTransform)plateGo.transform;
        plate.SetParent(panel, false);
        plate.anchorMin = plate.anchorMax = new Vector2(1f, 0f);
        plate.pivot = new Vector2(1f, 1f);
        plate.anchoredPosition = new Vector2(0f, -6f);
        plate.sizeDelta = new Vector2(540f, 48f); // room for the legend at 36 (24 read small next to the map)
        var plateImage = plateGo.GetComponent<Image>();
        plateImage.color = UISkin.OverlayBack;
        plateImage.raycastTarget = false;

        legend = new GameObject("Legend", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        legend.rectTransform.SetParent(plate, false);
        legend.rectTransform.anchorMin = Vector2.zero;
        legend.rectTransform.anchorMax = Vector2.one;
        legend.rectTransform.offsetMin = new Vector2(8f, 0f);
        legend.rectTransform.offsetMax = new Vector2(-8f, 0f);
        if (font != null) legend.font = font;
        legend.fontSize = UiTheme.TextLarge;
        legend.alignment = TextAlignmentOptions.Right;
        legend.textWrappingMode = TextWrappingModes.NoWrap;
        legend.raycastTarget = false;
        legend.outlineWidth = 0.2f;
        legend.outlineColor = new Color32(20, 12, 10, 255);
        legend.text = Loc.T("minimap.legend");
    }

    // Enemies that appear mid-run (summons, spawners) get a marker too, the frame they show up.
    void TrackNewEnemies()
    {
        bool added = false;
        foreach (var enemy in Enemy.All)
        {
            if (enemy == null || known.Contains(enemy)) continue;
            known.Add(enemy);
            bool boss = enemy.isBoss;
            enemies.Add((enemy, Marker(boss ? "Boss" : "Monster", boss ? BossColor : EnemyColor, Vector2.one * (boss ? 11f : 8f))));
            added = true;
        }
        if (added && playerMarker != null) playerMarker.SetAsLastSibling(); // the player dot stays on top
    }

    // A hunt chest is a gold square on the map until the player opens it.
    void TrackNewChests()
    {
        foreach (var chest in RouteChest.All)
        {
            if (chest == null || knownChests.Contains(chest)) continue;
            knownChests.Add(chest);
            chests.Add((chest, Marker("Chest", ChestColor, Vector2.one * 10f)));
        }
    }

    // One texel per tile: ground (grass-coloured where open sky is above), platforms, sky.
    Texture2D DrawMap()
    {
        int w = map.mapSize.x, h = map.mapSize.y;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var c = new Vector2(x + 0.5f, y + 0.5f);
                Color32 col = Sky;
                if (map.IsSolid(c))
                    col = y + 1 < h && !map.IsSolid(c + Vector2.up) ? GroundTop : Ground;
                else if (map.IsPlatform(c))
                    col = Platform;
                px[y * w + x] = col;
            }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    // Route platforms are added after the stage opens, so redraw the small map as well.
    public void RefreshMap()
    {
        if (map == null || mapImage == null) return;
        if (mapTexture != null) Destroy(mapTexture);
        mapTexture = DrawMap();
        mapImage.texture = mapTexture;
    }

    RectTransform Marker(string name, Color color, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(mapRect, false);
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        if (name != "View")
        {
            // Dark edge so the dot reads on both sky and ground.
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1f, -1f);
        }
        return rt;
    }

    Vector2 ToMap(Vector2 world)
    {
        Vector2 s = mapRect.sizeDelta;
        return new Vector2(
            Mathf.Clamp(world.x / map.mapSize.x, 0f, 1f) * s.x,
            Mathf.Clamp(world.y / map.mapSize.y, 0f, 1f) * s.y);
    }

    void LateUpdate()
    {
        if (map == null) return;

        if (legend != null)
        {
            bool showLegend = !GameClock.Paused && legendTimer > 0f;
            legend.gameObject.SetActive(showLegend);
            // The dark plate goes with its text (hiding only the text left an empty bar under the map).
            var plate = legend.transform.parent;
            if (plate != null && plate.gameObject.activeSelf != showLegend) plate.gameObject.SetActive(showLegend);
            if (showLegend)
            {
                legendTimer -= Time.unscaledDeltaTime;
                legend.alpha = Mathf.Clamp01(legendTimer);
            }
        }

        if (player != null) playerMarker.anchoredPosition = ToMap(player.position);

        TrackNewEnemies();
        TrackNewChests();
        UpdateExitMarker();
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            var (enemy, marker) = enemies[i];
            if (enemy == null) // destroyed: drop its marker for good
            {
                if (marker != null) Destroy(marker.gameObject);
                known.Remove(enemy);
                enemies.RemoveAt(i);
                continue;
            }
            bool alive = !enemy.IsDead && enemy.gameObject.activeInHierarchy;
            marker.gameObject.SetActive(alive);
            if (!alive) continue;
            var sr = enemy.Renderer;
            Vector2 p = sr != null ? (Vector2)sr.bounds.center : (Vector2)enemy.transform.position;
            marker.anchoredPosition = ToMap(new Vector2(enemy.BodyX, p.y));
        }

        for (int i = chests.Count - 1; i >= 0; i--)
        {
            var (chest, marker) = chests[i];
            if (chest == null)
            {
                if (marker != null) Destroy(marker.gameObject);
                knownChests.Remove(chest);
                chests.RemoveAt(i);
                continue;
            }
            marker.anchoredPosition = ToMap(chest.transform.position);
        }

        if (cam != null && cam.orthographic)
        {
            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            Vector2 c = cam.transform.position;
            Vector2 min = ToMap(c - new Vector2(halfW, halfH)), max = ToMap(c + new Vector2(halfW, halfH));
            Vector2 size = max - min;
            SetEdge(viewEdges[0], new Vector2(min.x + size.x * 0.5f, max.y), new Vector2(size.x, 1f));
            SetEdge(viewEdges[1], new Vector2(min.x + size.x * 0.5f, min.y), new Vector2(size.x, 1f));
            SetEdge(viewEdges[2], new Vector2(min.x, min.y + size.y * 0.5f), new Vector2(1f, size.y));
            SetEdge(viewEdges[3], new Vector2(max.x, min.y + size.y * 0.5f), new Vector2(1f, size.y));
        }
    }

    // Shown only once the exit log is out; it pulses slowly when the cat is close.
    void UpdateExitMarker()
    {
        if (exitMarker == null) return;
        var exit = StageExit.Instance;
        bool show = exit != null && exit.IsOpen;
        if (exitMarker.gameObject.activeSelf != show)
        {
            exitMarker.gameObject.SetActive(show);
            if (show) exitMarker.SetAsLastSibling();
            if (show && playerMarker != null) playerMarker.SetAsLastSibling();
        }
        if (!show) return;
        Vector2 point = exit.ExitPoint;
        exitMarker.anchoredPosition = ToMap(point);
        bool near = player != null && Vector2.Distance(player.position, point) < 6f;
        float pulse = near ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f)) : 1f;
        exitImage.color = new Color(ExitColor.r, ExitColor.g, ExitColor.b, pulse);
        exitMarker.localScale = Vector3.one * (near ? 1f + 0.2f * pulse : 1f);
    }

    static void SetEdge(RectTransform edge, Vector2 pos, Vector2 size)
    {
        edge.anchoredPosition = pos;
        edge.sizeDelta = size;
    }
}
