using UnityEngine;
using UnityEngine.Tilemaps;

// Attach to the Grid that holds a side-scrolling stage's tilemaps. Scene singleton
// that PlayerController2D asks for collision:
//   - solid:     ground/walls - block from every side
//   - platforms: one-way - only landed on from above, can jump up through them
// The map occupies cells (0,0) .. (mapSize-1); outside the left/right/bottom edges
// counts as solid so the player can't leave the map. No Collider2D/Rigidbody2D -
// same lookup-based approach as the rest of this project.
public class StageMap : MonoBehaviour
{
    public static StageMap Instance { get; private set; }

    public Tilemap solid;
    public Tilemap platforms;
    public Vector2Int mapSize;
    [Tooltip("Vertical rooms progress toward the top; exits and map UI use height as the main axis.")]
    public bool verticalLayout;
    public float[] verticalGateHeights;
    [Tooltip("Horizontal room boundaries in world X coordinates.")]
    public float[] horizontalGatePositions;
    readonly System.Collections.Generic.List<float> activeHorizontalBarriers = new System.Collections.Generic.List<float>();
    readonly System.Collections.Generic.List<float> activeVerticalBarriers = new System.Collections.Generic.List<float>();

    [Tooltip("Floor points (feet positions) an enemy can be moved to - every standable, reachable spot with room for its body. Filled by StageSceneBuilder; StageRandomizer picks from these each run.")]
    public Vector2[] spawnSlots;
    [Tooltip("Where the player starts; enemies are never placed right next to it.")]
    public Vector2 playerStart;

    public Bounds WorldBounds => new Bounds(
        new Vector3(mapSize.x * 0.5f, mapSize.y * 0.5f, 0f),
        new Vector3(mapSize.x, mapSize.y, 0f));

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void AddHorizontalBarrier(float y)
    {
        if (!activeHorizontalBarriers.Contains(y)) activeHorizontalBarriers.Add(y);
    }

    public void RemoveHorizontalBarrier(float y) => activeHorizontalBarriers.Remove(y);

    public void AddVerticalBarrier(float x)
    {
        if (!activeVerticalBarriers.Contains(x)) activeVerticalBarriers.Add(x);
    }

    public void RemoveVerticalBarrier(float x) => activeVerticalBarriers.Remove(x);

    public bool CrossesHorizontalBarrier(float fromY, float toY)
    {
        foreach (float y in activeHorizontalBarriers)
            if ((fromY < y && toY > y) || (fromY > y && toY < y)) return true;
        return false;
    }

    public bool IsHorizontalBarrier(Vector2 worldPoint)
    {
        if (!verticalLayout || worldPoint.x < 0f || worldPoint.x >= mapSize.x) return false;
        foreach (float y in activeHorizontalBarriers)
            if (Mathf.Abs(worldPoint.y - y) <= 0.38f) return true;
        return false;
    }

    public bool IsVerticalBarrier(Vector2 worldPoint)
    {
        if (verticalLayout || worldPoint.y < 0f || worldPoint.y >= mapSize.y) return false;
        foreach (float x in activeVerticalBarriers)
            if (Mathf.Abs(worldPoint.x - x) <= 0.38f) return true;
        return false;
    }

    public bool IsSolid(Vector2 worldPoint)
    {
        if (IsHorizontalBarrier(worldPoint) || IsVerticalBarrier(worldPoint)) return true;
        if (worldPoint.x < 0f || worldPoint.x >= mapSize.x || worldPoint.y < 0f) return true;
        if (solid == null) return false;
        return solid.HasTile(solid.WorldToCell(worldPoint));
    }

    public bool IsPlatform(Vector2 worldPoint)
    {
        if (platforms == null) return false;
        return platforms.HasTile(platforms.WorldToCell(worldPoint));
    }
}
