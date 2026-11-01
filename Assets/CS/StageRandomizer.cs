using System.Collections.Generic;
using UnityEngine;

// Makes every run of a stage a little different (added to stage scenes by StageSceneBuilder):
//   - Awake, first: a mixed line-up - some of the stage's white rats become other monsters (spider, slime,
//     mushroom; a quarter in stage 1, up to 60% by stage 4), always the kind the stage has fewest of (EnemyRoster).
//   - Awake: the stage's monsters (not the boss) are moved to random spots picked from the
//     StageMap's spawn slots - all reachable floor with room for a body - kept away from
//     the player's start and spread out so they don't stack.
//   - Start: a few of them become elites (Enemy.MakeElite: bigger, golden, x3 HP, x4 gold).
// Runs before Enemy (Awake: so each enemy's patrol starts from its new spot; Start: after
// Enemy.Awake has set the base stats the elite multiplies).
[DefaultExecutionOrder(-50)]
public class StageRandomizer : MonoBehaviour
{
    public GameObject enemyGroup;
    [Tooltip("How many monsters become elites this run.")]
    public int eliteCount = 1;
    [Tooltip("No monster is placed closer than this (horizontally) to the player's start.")]
    public float safeRadius = 12f;
    [Tooltip("Monsters on about the same height stay at least this far apart.")]
    public float spacing = 4f;
    // Share of the stage's rats kept as rats (the rest become other monsters), by stage: the first stays gentle
    // (the spiders already shoot), the mix thickens as the forest gets deeper.
    static readonly float[] KeepRats = { 1f, 0.65f, 0.4f, 0.2f, 0.1f };

    readonly List<Enemy> monsters = new List<Enemy>();

    void Awake()
    {
        if (enemyGroup == null) return;
        var run = FindAnyObjectByType<RunManager>();
        int stage = run != null ? run.stageNumber : 0;
        var roster = EnemyRoster.Load();
        MixLineUp(stage, roster);
        foreach (var e in enemyGroup.GetComponentsInChildren<Enemy>(true))
            if (e.definition != null && !e.definition.isBoss) monsters.Add(e);

        var map = GetComponent<StageMap>();
        if (map == null || map.spawnSlots == null || map.spawnSlots.Length == 0) return;

        var slots = new List<Vector2>();
        foreach (var s in map.spawnSlots)
        {
            bool nearStart = map.verticalLayout
                ? Vector2.Distance(s, map.playerStart) < safeRadius
                : Mathf.Abs(s.x - map.playerStart.x) < safeRadius;
            if (!nearStart) slots.Add(s);
        }

        if (stage == 1 && roster != null && roster.rat != null &&
            map.horizontalGatePositions != null && map.horizontalGatePositions.Length > 0)
        {
            PlaceGentleStageOneOpening(map, slots, roster.rat);
            return;
        }

        Shuffle(slots);
        Shuffle(monsters);
        var used = new List<Vector2>();
        foreach (var enemy in monsters)
            PlaceClosest(enemy, slots, used, 0f, map.playerStart.y, false);
    }

    // Stage 1 always opens with three ground-level rats. Every other monster starts beyond
    // the first seal, so a run seed cannot turn the controls lesson into an early ranged fight.
    void PlaceGentleStageOneOpening(StageMap map, List<Vector2> slots, EnemyDefinition rat)
    {
        float firstEdge = map.horizontalGatePositions[0];
        float secondEdge = map.horizontalGatePositions.Length > 1 ? map.horizontalGatePositions[1] : map.mapSize.x * 0.5f;
        var openingSlots = slots.FindAll(s => s.x < firstEdge && s.y <= map.playerStart.y + 3.5f);
        var secondRoomSlots = slots.FindAll(s => s.x >= firstEdge && s.x < secondEdge && s.y <= map.playerStart.y + 4.5f);
        var laterSlots = slots.FindAll(s => s.x >= secondEdge);
        var rats = monsters.FindAll(e => e.definition == rat);
        Shuffle(openingSlots);
        Shuffle(secondRoomSlots);
        Shuffle(laterSlots);
        Shuffle(rats);

        const int OpeningCount = 3;
        // Stage 1 is authored with more than three rats. This fallback keeps the rule intact
        // if its authored line-up is changed later.
        if (rats.Count < OpeningCount)
        {
            var replacements = monsters.FindAll(e => e.definition != rat);
            Shuffle(replacements);
            while (rats.Count < OpeningCount && replacements.Count > 0)
            {
                var replacement = replacements[replacements.Count - 1];
                replacements.RemoveAt(replacements.Count - 1);
                replacement.definition = rat;
                replacement.name = rat.name;
                rats.Add(replacement);
            }
        }

        var opening = new List<Enemy>();
        for (int i = 0; i < Mathf.Min(OpeningCount, rats.Count); i++) opening.Add(rats[i]);
        var openingSet = new HashSet<Enemy>(opening);
        var available = monsters.FindAll(e => !openingSet.Contains(e));

        // Room 2 repeats the basic three-rat fight once on a wider piece of terrain.
        // Special enemies begin in room 3, after the player has learned the room rhythm.
        var roomTwo = new List<Enemy>();
        var remainingRats = available.FindAll(e => e.definition == rat);
        Shuffle(remainingRats);
        for (int i = 0; i < Mathf.Min(3, remainingRats.Count); i++) roomTwo.Add(remainingRats[i]);
        foreach (var enemy in available)
        {
            if (roomTwo.Count >= 3) break;
            if (!roomTwo.Contains(enemy))
            {
                enemy.definition = rat;
                enemy.name = rat.name;
                roomTwo.Add(enemy);
            }
        }
        Shuffle(roomTwo);
        var roomTwoSet = new HashSet<Enemy>(roomTwo);
        var later = available.FindAll(e => !roomTwoSet.Contains(e));
        Shuffle(later);
        // Rooms 3-4 must feel like a step up: at most half rats there.
        var roster = EnemyRoster.Load();
        if (roster != null && roster.others != null && roster.others.Length > 0)
        {
            int ratsLeft = later.FindAll(e => e.definition == rat).Count, next = RunRng.Range(0, roster.others.Length);
            foreach (var enemy in later)
            {
                if (ratsLeft * 2 <= later.Count) break;
                if (enemy.definition != rat) continue;
                var other = roster.others[next++ % roster.others.Length];
                if (other == null) continue;
                enemy.definition = other;
                enemy.name = other.name;
                ratsLeft--;
            }
        }

        var used = new List<Vector2>();
        float startX = Mathf.Max(map.playerStart.x + safeRadius, 0f);
        for (int i = 0; i < opening.Count; i++)
        {
            float targetX = Mathf.Lerp(startX, firstEdge - 2f, (i + 0.5f) / OpeningCount);
            PlaceClosest(opening[i], openingSlots, used, targetX, map.playerStart.y, true);
        }

        // Room 2 stays on the ground and low platforms; high-platform encounters begin
        // after the second seal, once the player has learned the room rhythm.
        used.Clear();
        for (int i = 0; i < roomTwo.Count; i++)
        {
            float targetX = Mathf.Lerp(firstEdge + 5f, secondEdge - 5f, (i + 0.5f) / Mathf.Max(1, roomTwo.Count));
            PlaceClosest(roomTwo[i], secondRoomSlots, used, targetX, map.playerStart.y, true);
        }

        // Rooms 3-4 keep the broader enemy and height variation.
        used.Clear();
        for (int i = 0; i < later.Count; i++)
        {
            float targetX = Mathf.Lerp(secondEdge + 4f, map.mapSize.x * 0.9f, (i + 0.5f) / Mathf.Max(1, later.Count));
            PlaceClosest(later[i], laterSlots, used, targetX, map.playerStart.y, true);
        }
        // The one furthest along becomes the stage's only elite: a small finale before the exit.
        foreach (var enemy in later)
            if (stageOneFinale == null || enemy.transform.position.x > stageOneFinale.transform.position.x)
                stageOneFinale = enemy;
    }

    Enemy stageOneFinale;

    void PlaceClosest(Enemy enemy, List<Vector2> slots, List<Vector2> used,
                      float targetX, float targetY, bool scoreByPosition)
    {
        int pick = -1;
        float bestScore = float.MaxValue;
        for (int i = 0; i < slots.Count; i++)
        {
            bool free = true;
            foreach (var position in used)
                if (Mathf.Abs(position.x - slots[i].x) < spacing && Mathf.Abs(position.y - slots[i].y) < 2.5f)
                {
                    free = false;
                    break;
                }
            if (!free) continue;
            if (!scoreByPosition) { pick = i; break; }
            float score = Mathf.Abs(slots[i].x - targetX) + Mathf.Abs(slots[i].y - targetY) * 0.15f;
            if (score < bestScore) { bestScore = score; pick = i; }
        }
        if (pick < 0) return; // map too crowded: this one keeps its hand-placed spot
        enemy.transform.position = slots[pick];
        used.Add(slots[pick]);
        slots.RemoveAt(pick);
    }

    // Before the monsters' own Awake reads their definitions (this runs first: DefaultExecutionOrder -50).
    void MixLineUp(int stage, EnemyRoster roster)
    {
        if (roster == null || roster.rat == null || roster.others == null || roster.others.Length == 0) return;
        var rats = new List<Enemy>();
        var count = new Dictionary<EnemyDefinition, int>();
        foreach (var o in roster.others) if (o != null) count[o] = 0;
        foreach (var e in enemyGroup.GetComponentsInChildren<Enemy>(true))
        {
            if (e.definition == roster.rat) rats.Add(e);
            else if (e.definition != null && count.ContainsKey(e.definition)) count[e.definition]++;
        }
        Shuffle(rats);
        float keepRats = KeepRats[Mathf.Clamp(stage, 0, KeepRats.Length - 1)];
        int keep = Mathf.CeilToInt(rats.Count * keepRats);
        for (int i = keep; i < rats.Count; i++)
        {
            // The kind this stage has fewest of (a random one of those tied).
            EnemyDefinition pick = null; int least = int.MaxValue, ties = 0;
            foreach (var kv in count)
            {
                if (kv.Value < least) { least = kv.Value; pick = kv.Key; ties = 1; }
                else if (kv.Value == least && RunRng.Range(0, ++ties) == 0) pick = kv.Key;
            }
            if (pick == null) break;
            rats[i].definition = pick;
            rats[i].name = pick.name;
            count[pick]++;
        }
    }

    void Start()
    {
        // The first stage teaches the controls. Its base difficulty has no early elite;
        // corruption can still add one for experienced players.
        var run = FindAnyObjectByType<RunManager>();
        bool gentleFirstStage = run != null && run.stageNumber == 1 && Corruption.Level <= 0;
        if (gentleFirstStage && stageOneFinale != null) stageOneFinale.MakeElite();
        int elites = (gentleFirstStage ? 0 : eliteCount) + Corruption.ExtraElites(Corruption.Level);
        for (int i = 0; i < Mathf.Min(elites, monsters.Count); i++)
            monsters[i].MakeElite();
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = RunRng.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
