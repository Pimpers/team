using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Applies the route picked after the previous stage's card. The safe route keeps low
// bridges; the hunt route adds a canopy path and puts enemies and its chest on that path.
public static class StageRouteLayout
{
    public static void Apply(StageMap map, List<Enemy> foes, bool hunt)
    {
        if (map == null || foes == null) return;
        if (map.verticalLayout)
        {
            List<Vector2> verticalSpots = AdjustVerticalRoute(map, hunt);
            RouteSign.Create(map.playerStart, hunt);
            MoveFoesVertical(map, foes, hunt, verticalSpots);
            if (hunt) RouteChest.Create(verticalSpots.Count > 0 ? verticalSpots[verticalSpots.Count - 1] : HighVerticalSpot(map));
            StageMinimap.Instance?.RefreshMap();
            return;
        }
        List<Vector2> routeSpots = AddRoutePlatforms(map, hunt);
        StageMinimap.Instance?.RefreshMap();
        RouteSign.Create(map.playerStart, hunt);
        MoveFoes(map, foes, hunt, routeSpots);
        if (hunt) RouteChest.Create(routeSpots.Count > 0 ? routeSpots[routeSpots.Count - 1] : HighRewardSpot(map));
    }

    static List<Vector2> AddRoutePlatforms(StageMap map, bool hunt)
    {
        var spots = new List<Vector2>();
        Tile tile = FirstPlatformTile(map);
        if (tile == null) return spots;

        int floor = Mathf.RoundToInt(map.playerStart.y);
        if (!hunt) RemoveHighPlatforms(map, floor + 4);

        int w = map.mapSize.x;
        int[] starts = hunt
            ? new[] { Mathf.RoundToInt(w * 0.2f), Mathf.RoundToInt(w * 0.45f), Mathf.RoundToInt(w * 0.7f) }
            : new[] { Mathf.RoundToInt(w * 0.24f), Mathf.RoundToInt(w * 0.52f), Mathf.RoundToInt(w * 0.78f) };
        int length = hunt ? 9 : 6;
        foreach (int start in starts)
        {
            int stand = Mathf.Min(map.mapSize.y - 3, Surface(map, start + length / 2) + (hunt ? 4 : 3));
            Vector2? spot = AddPlatform(map, tile, start, stand, length);
            if (spot.HasValue) spots.Add(spot.Value);
        }
        return spots;
    }

    static Tile FirstPlatformTile(StageMap map)
    {
        for (int y = 0; y < map.mapSize.y; y++)
            for (int x = 0; x < map.mapSize.x; x++)
            {
                var tile = map.platforms.GetTile<Tile>(new Vector3Int(x, y, 0));
                if (tile != null) return tile;
            }
        return null;
    }

    static int Surface(StageMap map, int x)
    {
        x = Mathf.Clamp(x, 1, map.mapSize.x - 2);
        int y = 0;
        while (y < map.mapSize.y && map.IsSolid(new Vector2(x + 0.5f, y + 0.5f))) y++;
        return y;
    }

    static void RemoveHighPlatforms(StageMap map, int maxStand)
    {
        for (int y = maxStand; y < map.mapSize.y; y++)
            for (int x = 0; x < map.mapSize.x; x++)
                map.platforms.SetTile(new Vector3Int(x, y, 0), null);
    }

    static Vector2? AddPlatform(StageMap map, Tile tile, int start, int standY, int length)
    {
        int tileY = standY - 1;
        if (tileY < 0 || tileY >= map.mapSize.y) return null;
        int first = -1, last = -1;
        for (int x = Mathf.Max(1, start); x < Mathf.Min(map.mapSize.x - 1, start + length); x++)
        {
            var cell = new Vector3Int(x, tileY, 0);
            Vector2 centre = new Vector2(x + 0.5f, tileY + 0.5f);
            if (map.IsSolid(centre)) continue;
            map.platforms.SetTile(cell, tile);
            if (first < 0) first = x;
            last = x;
        }
        if (first < 0 || last - first < 3) return null;
        return new Vector2((first + last + 1) * 0.5f, standY);
    }

    static List<Vector2> AdjustVerticalRoute(StageMap map, bool hunt)
    {
        var branches = new List<Vector2>();
        Tile tile = FirstPlatformTile(map);
        if (tile == null) return branches;
        var rows = new List<int>();
        for (int y = 0; y < map.mapSize.y; y++)
            for (int x = 0; x < map.mapSize.x; x++)
                if (map.platforms.HasTile(new Vector3Int(x, y, 0))) { rows.Add(y); break; }

        for (int i = 0; i < rows.Count; i++)
        {
            int y = rows[i], left = map.mapSize.x, right = -1;
            for (int x = 0; x < map.mapSize.x; x++)
                if (map.platforms.HasTile(new Vector3Int(x, y, 0))) { left = Mathf.Min(left, x); right = Mathf.Max(right, x); }
            if (right < left) continue;

            if (!hunt)
            {
                // Safe route: two extra landing tiles on each side.
                for (int x = Mathf.Max(1, left - 2); x <= Mathf.Min(map.mapSize.x - 2, right + 2); x++)
                    if (!map.IsSolid(new Vector2(x + 0.5f, y + 0.5f))) map.platforms.SetTile(new Vector3Int(x, y, 0), tile);
                if (i >= 2 && i % 4 == 2) RouteMarker.Create(new Vector2((left + right + 1) * 0.5f, y + 1), false);
            }
            else if (i >= 2 && i % 4 == 2)
            {
                // Hunt route: an exposed side branch every few floors. Enemies use the
                // first branches; the final one holds the route treasure.
                bool extendLeft = (i / 4) % 2 == 0;
                int from = extendLeft ? Mathf.Max(1, left - 6) : right + 1;
                int to = extendLeft ? left - 1 : Mathf.Min(map.mapSize.x - 2, right + 6);
                if (from > to) continue;
                for (int x = from; x <= to; x++)
                    if (!map.IsSolid(new Vector2(x + 0.5f, y + 0.5f))) map.platforms.SetTile(new Vector3Int(x, y, 0), tile);
                Vector2 branch = new Vector2((from + to + 1) * 0.5f, y + 1);
                branches.Add(branch);
                RouteMarker.Create(branch, true);
            }
        }
        return branches;
    }

    static Vector2 HighVerticalSpot(StageMap map)
    {
        Vector2 best = map.playerStart;
        if (map.spawnSlots == null) return best;
        foreach (var slot in map.spawnSlots)
            if (slot.y > best.y) best = slot;
        return best;
    }

    static void MoveFoesVertical(StageMap map, List<Enemy> foes, bool hunt, List<Vector2> routeSpots)
    {
        if (map.spawnSlots == null || map.spawnSlots.Length == 0) return;
        int reserved = hunt ? Mathf.Min(Mathf.Max(0, routeSpots.Count - 1), foes.Count) : 0;
        for (int i = 0; i < reserved; i++) foes[i].Relocate(routeSpots[i]);

        var slots = new List<Vector2>();
        foreach (var slot in map.spawnSlots)
            if (slot.y >= map.playerStart.y + 4f) slots.Add(slot);
        slots.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
        PlaceEvenly(foes, reserved, foes.Count - reserved, slots.Count > 0 ? slots : new List<Vector2>(map.spawnSlots));
    }

    static Vector2 HighRewardSpot(StageMap map)
    {
        Vector2 best = map.playerStart + Vector2.right * 12f;
        if (map.spawnSlots == null) return best;
        foreach (var slot in map.spawnSlots)
            if (slot.x > best.x && slot.y >= map.playerStart.y + 3f) best = slot;
        return best;
    }

    static void MoveFoes(StageMap map, List<Enemy> foes, bool hunt, List<Vector2> routeSpots)
    {
        if (map.spawnSlots == null || map.spawnSlots.Length == 0 || foes.Count == 0) return;
        float floor = map.playerStart.y;
        var low = new List<Vector2>();
        var high = new List<Vector2>(routeSpots);
        foreach (var slot in map.spawnSlots)
        {
            if (Mathf.Abs(slot.x - map.playerStart.x) < 12f) continue;
            if (slot.y <= floor + 2.2f) low.Add(slot);
            if (slot.y >= floor + 3f && !high.Contains(slot)) high.Add(slot);
        }
        low.Sort((a, b) => a.x.CompareTo(b.x));
        high.Sort((a, b) => a.x.CompareTo(b.x));

        if (!hunt)
        {
            PlaceEvenly(foes, 0, foes.Count, low.Count > 0 ? low : new List<Vector2>(map.spawnSlots));
            return;
        }

        // Put one foe on each new branch except the last one, which holds the chest.
        int reserved = Mathf.Min(Mathf.Max(0, routeSpots.Count - 1), foes.Count);
        for (int i = 0; i < reserved; i++)
        {
            foes[i].Relocate(routeSpots[i]);
            high.Remove(routeSpots[i]);
        }

        int remaining = foes.Count - reserved;
        int highCount = Mathf.Min(remaining, Mathf.CeilToInt(remaining * 0.65f));
        PlaceEvenly(foes, reserved, highCount, high.Count > 0 ? high : new List<Vector2>(map.spawnSlots));
        PlaceEvenly(foes, reserved + highCount, remaining - highCount, low.Count > 0 ? low : new List<Vector2>(map.spawnSlots));
    }

    static void PlaceEvenly(List<Enemy> foes, int foeStart, int count, List<Vector2> slots)
    {
        if (count <= 0 || slots == null || slots.Count == 0) return;
        for (int i = 0; i < count; i++)
        {
            int index = Mathf.Clamp(Mathf.RoundToInt((slots.Count - 1) * (i + 1f) / (count + 1f)), 0, slots.Count - 1);
            foes[foeStart + i].Relocate(slots[index]);
        }
    }
}
