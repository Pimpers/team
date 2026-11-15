using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// The way from one stage to the next, through portals - stone arches (created by RunManager in the stage scenes;
// they were hollow logs until 2026-11-09, which is why the code still says "log" for the sprite):
//   Closed - from the start, the portals stand at the end of the stage, barred by chains or vines; walking into one
//            shakes it and says 적을 모두 물리치세요.
//   Open   - after the draft card is picked the vines blow off (dust, a creak), a wind swirls in the mouth and
//            fireflies string out a path to it; leaves near it are pulled in, an edge arrow (MonsterTracker) and the
//            minimap show the way. A stage where the end is too far (or a climbing one) gets a single log a few steps
//            away instead, popping up out of the ground.
//   Fork   - stages 1-3 end at a fork: two logs, the hard way (사냥 길: thorns, a skull, a red swirl - elites and
//            treasure next stage) and the easy way (안전 길: flowers, a green swirl - fewer monsters). Each has a sign
//            and a head icon (skull / sprout: shape as well as colour). Walking into one picks that route
//            (RouteProgress) and shuts the other at once.
//   In     - walking into an open mouth: the cat is locked, pulled to the mouth stretching, then shrinking and
//            spinning in; the camera leans in; the screen irises shut on the mouth (StageWipe) and the next stage
//            loads. About a second.
//   Out    - the next stage opens its iris on the cat inside a log at the start; it pops out, lands with a squash
//            and a puff of dust.
// The art is pxl (Tools/pxl/pxl_stage_exit.px.js) in Resources/StageExit at the tiles' 16 px per unit.
public class StageExit : MonoBehaviour
{
    public static StageExit Instance { get; private set; }
    // True while the cat is being pulled in: pause, the Tab sheet and the like keep out (PauseMenu, BuildSheet).
    public static bool Entering => Instance != null && Instance.entering;
    public static bool AutoEnter; // tests and the playtest harness: walking into a mouth is enough, no key
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetAutoEnter() => AutoEnter = false;

    const float HoleFromCentre = 1.25f; // the mouth: 20 px left of the middle of the 64 px log
    const float FarEnd = 22f;           // a closed log at the stage's end is used if the cat is no further than this
    const float SwirlFps = 10f, SuckFps = 24f;
    const float ClosedWordTime = 1.5f;  // 적을 모두 물리치세요 stays this long (a new bump restarts it, never a second copy)

    // One log: a way out. `kind` is its art ("" plain, "_crimson" boss, "_hunt" / "_safe" the fork).
    class Way
    {
        public SpriteRenderer log;
        public Sprite[] swirl;
        public string kind;
        public float holeX, floorY, holeSide; // holeSide: -1 = the mouth faces left (the cat comes from the left)
        public bool open, shut;               // shut: the other fork was taken
        public bool hunt;                     // the fork's hard way
        public float bumpCooldown, wordLeft;
        public TextMeshPro word;              // the closed message over it
        public Transform sign;                // the fork's sign and icon
        // The fork's two are portals (a standing arch, its opening's middle 1.6 up), the others logs (mouth at the cut end).
        public bool Portal => true; // every way out is a portal now (2026-11-09); the log geometry is kept for reference
        public float Offset => Portal ? 0f : holeSide * HoleFromCentre; // from the hole to the sprite's middle
        public Vector3 signHome;
        public SpriteRenderer rim;
        public Sprite[] rims;
        public Vector3 Mouth => Portal ? new Vector3(holeX, floorY + 1.6f, 0f) : new Vector3(holeX - holeSide * 0.2f, floorY + 1.3f, 0f);
    }

    readonly List<Way> ways = new List<Way>();
    Way taken;                 // the way being entered
    PlayerController2D body;
    string nextScene;
    System.Action<bool> chooseRoute; // the fork: true = 사냥 길
    bool open, entering, crimson, fork;
    float frameClock, pullIn;
    readonly List<Firefly> flies = new List<Firefly>();
    readonly List<Transform> drawn = new List<Transform>();
    Sprite[] flyFrames, leafSprites;
    static string arrivedKind = "_plain"; // the portal the cat left the last stage by: it pops out of the same kind

    class Firefly { public Transform tr; public SpriteRenderer sr; public Vector3 home; public float phase; public bool gone; }

    public static StageExit Create(int stageNumber, bool fork = false)
    {
        var go = new GameObject("StageExit");
        var s = go.AddComponent<StageExit>();
        s.stage = stageNumber;
        s.fork = fork;
        return s;
    }

    int stage;

    // The way out, once open (the minimap and the edge arrows mark each one).
    public bool IsOpen => open && !entering;
    public bool Opened => open; // the stage is done: traps switch off (TrapHazard)
    public bool IsFork => fork && ways.Count == 2;
    public Vector2 ExitPoint => ways.Count > 0 ? Point(ways[0]) : Vector2.zero;
    public int WayCount => ways.Count;
    public Vector2 WayPoint(int i) => Point(ways[i]);
    public bool WayOpen(int i) => open && !entering && ways[i].open && !ways[i].shut;
    // The way is on the map from the start (the minimap marks where it will be), open or not yet.
    public bool WayVisible(int i) => !entering && !ways[i].shut;
    public bool WayIsHunt(int i) => ways[i].hunt;
    // The fork's colours: red for the hard way, green for the easy one; 색약: vermilion and yellow (the shapes differ too).
    public Color WayColor(int i)
    {
        var w = ways[i];
        if (w.kind == "_hunt") return GameSettings.Colorblind ? new Color(0.93f, 0.39f, 0.04f) : new Color(0.9f, 0.21f, 0.31f);
        if (w.kind == "_safe") return GameSettings.Colorblind ? UiTheme.ExitMark : new Color(0.52f, 0.84f, 0.15f);
        return GameSettings.Colorblind ? UiTheme.ExitMark : new Color(0.35f, 0.9f, 1f);
    }
    // The fork's way for a route (the full-run bot and the tests walk in by it).
    public Vector2 RoutePoint(bool hunt)
    {
        foreach (var w in ways) if (w.hunt == hunt && (w.kind == "_hunt" || w.kind == "_safe")) return Point(w);
        return ExitPoint;
    }
    static Vector2 Point(Way w) => new Vector2(w.holeX, w.floorY + 0.5f);

    void Awake() { Instance = this; propsRead = false; decorMaps = null; } // a new scene: read its props and tiles afresh
    void OnDestroy() { if (Instance == this) Instance = null; StageWipe.Revealed -= PopOut; }

    void Start()
    {
        body = PlayerStats.Instance != null ? PlayerStats.Instance.Body : null;
        if (body == null) return;
        crimson = stage >= 4; // the way to the boss, and the boss room's way out
        if (StageWipe.ArrivedThroughExit) Arrive();
        PlaceClosed();
    }

    static Sprite Load(string kind, string state) => Resources.Load<Sprite>($"StageExit/exit_portal{kind}_{state}");
    // The single way on: plain (cyan), the way to the boss (stage 4: horns and a crown), the boss room's way to the
    // result (gold).
    string PlainKind => stage >= 5 ? "_victory" : stage == 4 ? "_boss" : "_plain";

    // ------------------------------------------------------------------ arriving
    void Arrive()
    {
        StageWipe.ArrivedThroughExit = false;
        // The cat waits inside a log at the start until the iris has opened, then pops out.
        var feet = body.Feet;
        // out of the same kind of portal it went in by
        var arrive = MakeLog(feet.x, feet.y, Load(arrivedKind, "open_0") ?? Load("_plain", "open_0"));
        arrive.flipX = true; // mouth on the right, at the cat
        arrive.sortingOrder = body.Renderer.sortingOrder - 1;
        body.skillLock = true;
        body.Renderer.enabled = false;
        StageWipe.Revealed += PopOut;
        if (!StageWipe.Busy) PopOut(); // (no iris running: straight out)
    }

    void PopOut()
    {
        StageWipe.Revealed -= PopOut;
        if (body == null) return;
        StartCoroutine(PopOutRoutine());
    }

    IEnumerator PopOutRoutine()
    {
        yield return new WaitForSeconds(0.1f);
        body.Renderer.enabled = true;
        body.FaceTowards(1f);
        body.Launch(8f);
        Sfx.Play("fx_pop");
        Vfx.Play("dust_small", body.Feet, 1f);
        var visual = body.visual != null ? body.visual : body.Renderer.transform;
        Vector3 scale = visual.localScale;
        for (float t = 0f; t < 0.25f; t += Time.deltaTime) // out, stretched tall, drifting right
        {
            visual.localScale = new Vector3(scale.x * 0.85f, scale.y * 1.2f, scale.z);
            body.TryMove(3.2f * Time.deltaTime);
            yield return null;
        }
        visual.localScale = scale;
        float until = Time.time + 1.2f;
        while (body.IsAirborne && Time.time < until) yield return null;
        // landing: a squash and a puff
        Vfx.Play("dust_mid", body.Feet, 1f);
        for (float t = 0f; t < 0.12f; t += Time.deltaTime)
        {
            float k = 1f - t / 0.12f;
            visual.localScale = new Vector3(scale.x * (1f + 0.25f * k), scale.y * (1f - 0.22f * k), scale.z);
            yield return null;
        }
        visual.localScale = scale;
        body.skillLock = false;
    }

    SpriteRenderer MakeLog(float x, float feetY, Sprite sprite)
    {
        var go = new GameObject("ExitLog");
        go.transform.position = new Vector3(x, feetY - 0.06f, 0f); // sits a pixel into the grass
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = -47; // with the bushes and rocks
        return sr;
    }

    Way AddWay(Vector2 spot, float holeSide, string kind, bool closed)
    {
        var w = new Way { kind = kind, holeX = spot.x, floorY = spot.y, holeSide = holeSide, hunt = kind == "_hunt" };
        w.log = MakeLog(spot.x - w.Offset, spot.y, Load(kind, closed ? "closed" : "open_0"));
        w.log.flipX = holeSide > 0f;
        w.log.sortingOrder = body.Renderer.sortingOrder - 4; // behind the fork's signpost (-2, its words -1) and the cat
        ways.Add(w);
        // still before a tree or bush (props or tiles: no clear spot was left): a dark silhouette behind it so the arch
        // reads against it
        // (a background tree - the parallax picture - can't be cleared or read; the plate keeps the arch apart from it)
        {
            var plate = new GameObject("Backdrop").AddComponent<SpriteRenderer>();
            plate.transform.SetParent(w.log.transform, false);
            plate.transform.localScale = new Vector3(1.3f, 1.06f, 1f);
            plate.sprite = w.log.sprite;
            plate.color = new Color(0.08f, 0.06f, 0.08f, 0.9f);
            plate.sortingOrder = w.log.sortingOrder - 1;
        }
        // the opening's inner edge, blinking between two colours once open (RimBlink)
        var rimGo = new GameObject("Rim");
        rimGo.transform.SetParent(w.log.transform, false);
        w.rim = rimGo.AddComponent<SpriteRenderer>();
        w.rim.sortingOrder = w.log.sortingOrder + 1;
        w.rims = new[] { Resources.Load<Sprite>($"StageExit/exit_portal{kind}_rim_0"), Resources.Load<Sprite>($"StageExit/exit_portal{kind}_rim_1") };
        w.rim.enabled = false;
        return w;
    }

    // ------------------------------------------------------------------ the closed logs
    // A side-scrolling stage shows its way out from the start, shut: one log, or the fork's two. (Climbing stages put
    // it near the card chest when the stage is done, so there is no fixed end to show.)
    void PlaceClosed()
    {
        var map = StageMap.Instance;
        if (map == null || map.verticalLayout || map.spawnSlots == null || map.spawnSlots.Length == 0) return;
        // a fork stage shows nothing until it is cleared: then its two portals rise beside the cat (OpenFork)
        if (fork) return;
        // the farthest roomy floor spot toward the stage's end (the last slot may sit on a ledge too short for it)
        bool found = false; Vector2 end = default;
        foreach (var s in map.spawnSlots)
            if ((!found || s.x > end.x) && Roomy(map, s)) { end = s; found = true; }
        if (!found) return;
        AddWay(end, -1f, PlainKind, closed: true);
    }

    // The fork's two logs among the roomy floor spots `near` allows: the hard way as high as it can go (위), the easy
    // way low (아래), far enough apart that one's pull never reaches the other. False if the place has no room for two.
    // The staged fork: safe portal to the left, hunt portal to the right, each "half" from a centre on the cat's floor
    // (or the nearest flat run that holds the whole stage), the signpost between them.
    float StageHalf()
    {
        var cam = Camera.main;
        float aspect = cam != null ? Mathf.Min(cam.aspect, 4f / 3f) : 4f / 3f; // narrowest screen we ship: 4:3
        float width = ViewHeight(cam) * aspect * 0.84f; // 8% margin each side
        return Mathf.Clamp(width * 0.5f - 1.25f - 0.25f, 2.6f, 5f);
    }

    // The view's height in units as the game draws it.
    // The game's view is fixed (16 px art drawn 7.8x on 1080 lines, the Pixel Perfect decision): 8.65 units tall at
    // every window size. A constant, not the camera's size - in a batch capture that reports the tiny batch window.
    static float ViewHeight(Camera cam) => 8.65f;

    // The card chest's place: the middle of where the fork will stand for a cat at `feet`, so the two portals rise
    // either side of the chest it has just opened (OpenFork). Null where the stage has no fork or no floor for one.
    Vector2 stageMid;
    Vector2? chestSpot;
    public Vector2? ForkCentre(Vector2 feet)
    {
        var map = StageMap.Instance;
        chestSpot = null;
        if (!fork || IsFork || map == null) return null;
        // a free flat run close by; else the nearest one, its low ledges to be cut when the portals rise
        if (PlaceStaged(map, feet, false, 9f, false) || PlaceStaged(map, feet, true, 30f, false)) chestSpot = stageMid;
        return chestSpot;
    }

    // cutLedges: one-way ledges through an arch don't count, and are taken out when the stage is set (CutLedges).
    // reach: how far from `feet` the stage's middle may be. place false: only finds the middle (stageMid).
    bool PlaceStaged(StageMap map, Vector2 feet, bool cutLedges = false, float reach = 30f, bool place = true)
    {
        float half = StageHalf();
        float floorY = FloorAt(map, feet.x, feet.y);
        if (float.IsNaN(floorY)) { Debug.Log($"[StageExit] staged: no floor under the cat at {feet}"); return false; }
        int nFloor = 0, nRoom = 0, nWalk = 0;
        for (float off = 0f; off <= reach; off += 0.5f) // nearest first, out to where a low ledge ends
            foreach (float sgn in new[] { 1f, -1f })
            {
                if (off == 0f && sgn < 0f) continue;
                float c = feet.x + sgn * off;
                float yl = FloorAt(map, c - half, floorY), yr = FloorAt(map, c + half, floorY), yc = FloorAt(map, c, floorY);
                if (float.IsNaN(yl) || float.IsNaN(yr) || float.IsNaN(yc)) continue;
                if (Mathf.Abs(yl - floorY) > 0.3f || Mathf.Abs(yr - floorY) > 0.3f || Mathf.Abs(yc - floorY) > 0.3f) { nFloor++; continue; }
                var a = new Vector2(c - half, floorY); var b = new Vector2(c + half, floorY);
                // on the staged floor a ledge may pass over a portal as long as the arch (3 tall) stands clear under it
                // (no HUD test here: the camera reframes on the stage as it opens, so where it is now says nothing)
                if (!PortalRoom(map, a, cutLedges) || !PortalRoom(map, b, cutLedges)) { nRoom++; continue; }
                if (!Walkable(map, a, b)) { nWalk++; continue; }
                stageMid = new Vector2(c, floorY);
                if (!place) return true;
                if (cutLedges) { CutLedges(map, a); CutLedges(map, b); }
                foreach (var w in ways) DestroyWay(w);
                ways.Clear();
                AddWay(b, -1f, "_hunt", false);
                AddWay(a, -1f, "_safe", false);
                foreach (var w in ways) MakeSign(w);
                return true;
            }
        Debug.Log($"[StageExit] staged: no stage near {feet} (half {half:0.0}; floor {nFloor}, room {nRoom}, walk {nWalk})");
        return false;
    }

    // The last resort of the fork: both portals go on the cat's own floor, a stage's half either side of it, whatever
    // stands there (ledges through the arches are taken out; solid ground is not). Two portals set into the scenery
    // are better than none - without them the stage had no way on but the old route window, which is gone.
    bool PlaceForced(StageMap map, Vector2 feet)
    {
        float half = StageHalf();
        float floorY = FloorAt(map, feet.x, feet.y);
        if (float.IsNaN(floorY)) floorY = feet.y;
        float yl = FloorAt(map, feet.x - half, floorY), yr = FloorAt(map, feet.x + half, floorY);
        var a = new Vector2(feet.x - half, float.IsNaN(yl) ? floorY : yl);
        var b = new Vector2(feet.x + half, float.IsNaN(yr) ? floorY : yr);
        foreach (var w in ways) DestroyWay(w);
        ways.Clear();
        CutLedges(map, a); CutLedges(map, b);
        AddWay(b, -1f, "_hunt", false);
        AddWay(a, -1f, "_safe", false);
        foreach (var w in ways) MakeSign(w);
        Debug.Log($"[StageExit] fork: no flat run near {feet}; portals set on the cat's floor");
        return true;
    }

    // Room for one portal (2.5 wide, 3 tall): ground under its step, nothing solid and no ledge through the arch (a
    // ledge just above it is fine). The 4-wide log test was stricter than a portal needs.
    // A ledge through a portal's arch is taken out whole, in a puff of dust. Stage 1's low branches left no flat run
    // free for two portals, so its fork always fell back to the route window; the stage is cleared by now, and the
    // scene reloads for the next one, so nothing needs the ledge.
    static void CutLedges(StageMap map, Vector2 p)
    {
        var tm = map.platforms;
        if (tm == null) return;
        for (float dx = -1.4f; dx <= 1.41f; dx += 0.7f)
            for (float dy = 0.4f; dy <= 3.2f; dy += 0.4f)
            {
                var c = tm.WorldToCell(new Vector3(p.x + dx, p.y + dy, 0f));
                if (!tm.HasTile(c)) continue;
                Vfx.Play("dust_mid", tm.GetCellCenterWorld(c), 1.2f);
                for (var r = c + Vector3Int.right; tm.HasTile(r); r += Vector3Int.right) tm.SetTile(r, null);
                for (var l = c; tm.HasTile(l); l += Vector3Int.left) tm.SetTile(l, null); // (c itself first)
            }
    }

    static bool PortalRoom(StageMap map, Vector2 p, bool ledgesOk = false)
    {
        for (float dx = -1.4f; dx <= 1.41f; dx += 0.7f)
        {
            for (float dy = 0.4f; dy <= 3.2f; dy += 0.4f)
            {
                var at = new Vector2(p.x + dx, p.y + dy);
                if (map.IsSolid(at) || (!ledgesOk && map.IsPlatform(at))) return false;
            }
            if (Mathf.Abs(dx) <= 1.21f)
            {
                var under = new Vector2(p.x + dx, p.y - 0.15f);
                if (!map.IsSolid(under) && !map.IsPlatform(under)) return false;
            }
        }
        return true;
    }

    // The floor surface at x near height y (the highest walkable top within 1 above to 3 below), NaN if none.
    static float FloorAt(StageMap map, float x, float y)
    {
        for (float t = y + 1f; t >= y - 3f; t -= 0.05f) // fine steps: the floor exactly (a cat set on it must not float)
        {
            var under = new Vector2(x, t - 0.15f);
            if ((map.IsSolid(under) || map.IsPlatform(under)) && !map.IsSolid(new Vector2(x, t + 0.4f))) return t;
        }
        return float.NaN;
    }

    // A spot whose portal would stand under the screen-fixed UI's corners at the current view (the minimap, the HUD).
    bool UnderHud(Vector2 spot)
    {
        var cam = Camera.main;
        if (cam == null) return false;
        Vector3 sp = cam.WorldToScreenPoint(new Vector3(spot.x, spot.y + 3f, 0f));
        float W = cam.pixelWidth, H = cam.pixelHeight, u = H / 1080f;
        if (sp.y < H - 360f * u) return false;
        return sp.x < 560f * u || sp.x > W - 480f * u;
    }

    // Clear-out: as the portals rise, the decoration around them and the sign - decor tiles and small props, a
    // portal's width round each - goes under the dust (floor tiles, colliders, enemies, chests are never touched; the
    // scene reloads whole on a restart or the next stage, so nothing needs putting back).
    void ClearOut()
    {
        if (ways.Count != 2) return;
        float lo = Mathf.Min(ways[0].holeX, ways[1].holeX) - 1.25f - 2.5f, hi = Mathf.Max(ways[0].holeX, ways[1].holeX) + 1.25f + 2.5f;
        float bot = Mathf.Min(ways[0].floorY, ways[1].floorY), top = bot + 4f;
        foreach (var tm in DecorMaps())
            for (float x = lo; x <= hi; x += 0.5f)
                for (float y = bot + 0.1f; y <= top; y += 0.5f)
                {
                    var cell = tm.WorldToCell(new Vector3(x, y, 0f));
                    if (tm.HasTile(cell)) tm.SetTile(cell, null);
                }
        var root = GameObject.Find("Props");
        if (root != null)
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>())
            {
                var b = sr.bounds;
                if (b.max.x < lo || b.min.x > hi || b.max.y < bot || b.min.y > top) continue;
                if (sr.GetComponentInParent<Collider2D>() != null || sr.GetComponentInParent<Enemy>() != null) continue; // never what plays
                sr.gameObject.SetActive(false);
            }
        for (float x = lo + 1f; x < hi; x += 2.5f) Vfx.Play("dust_mid", new Vector3(x, bot + 0.6f, 0f), 1.2f);
    }

    // The fork's two portals among the roomy floor spots `near` allows, side by side so both fit one screen (a choice
    // is only a choice when both answers are in view): 7-10 units apart (one's pull never reaches the other), at most
    // a portal's height (3) apart in height, clear of big props (trees, bushes) so they stand out, toward the stage's
    // end. The hard way takes the farther one. False if the place has no such pair.
    bool PlaceFork(StageMap map, System.Func<Vector2, bool> near, bool closed)
    {
        var spots = new List<Vector2>();
        foreach (var s in map.spawnSlots) if (near(s) && Roomy(map, s)) spots.Add(s);
        if (spots.Count < 2) return false;
        float bestScore = float.MaxValue; Vector2 a = default, b = default;
        for (int i = 0; i < spots.Count; i++)
            for (int j = 0; j < spots.Count; j++)
            {
                if (i == j || spots[j].x <= spots[i].x) continue;
                float dx = spots[j].x - spots[i].x, dy = Mathf.Abs(spots[j].y - spots[i].y);
                // 6.2-10 apart, 6.5 best: the 5-wide signposts never touch, and the pair with its signs fits the screen
                // with margin at 6.5 (wider pairs, where the floor allows no closer one, still fit the portals)
                if (dx < 6.2f || dx > 10f || dy > 3f || !Walkable(map, spots[i], spots[j])) continue;
                float score = dy * 2f + Mathf.Abs(dx - 6.5f) - spots[j].x * 0.02f
                            + (PropsNear(spots[i]) ? 6f : 0f) + (PropsNear(spots[j]) ? 6f : 0f)
                            + (LedgeOver(map, spots[i], spots[j]) ? 5f : 0f);
                if (score < bestScore) { bestScore = score; a = spots[i]; b = spots[j]; }
            }
        if (bestScore == float.MaxValue) return false;
        foreach (var w in ways) DestroyWay(w);
        ways.Clear();
        AddWay(b, -1f, "_hunt", closed); // the farther one: the harder way
        AddWay(a, -1f, "_safe", closed);
        foreach (var w in ways) MakeSign(w); // the crossroads sign stands between the two
        return true;
    }

    // The cat can go from one portal to the other without a dash: walking the floor between them, any step up at most
    // 2.5 (a single jump reaches 14.5^2 / (2 * 32) = 3.3, so with room to spare) and any gap at most 2 wide (one jump
    // at the base speed 3 covers about 2.7). Read column by column, the highest floor near the two spots' heights.
    static bool Walkable(StageMap map, Vector2 a, Vector2 b)
    {
        float lo = Mathf.Min(a.y, b.y) - 1.5f, hi = Mathf.Max(a.y, b.y) + 0.6f;
        float last = a.y, gap = 0f;
        for (float x = a.x; x <= b.x; x += 0.5f)
        {
            float floor = float.NaN;
            for (float y = hi; y >= lo; y -= 0.25f)
            {
                var under = new Vector2(x, y - 0.15f);
                if ((map.IsSolid(under) || map.IsPlatform(under)) && !map.IsSolid(new Vector2(x, y + 0.4f))) { floor = y; break; }
            }
            if (float.IsNaN(floor)) { gap += 0.5f; if (gap > 2f) return false; continue; }
            if (Mathf.Abs(floor - last) > 2.5f) return false; // a step too high to jump back up (both ways)
            last = floor; gap = 0f;
        }
        return Mathf.Abs(last - b.y) <= 2.5f;
    }

    // A big floating ledge over the space between the two (3-6 above the floor): it pulls the eye to the middle when
    // the camera shows the pair. The level is not changed - such a pair just scores lower.
    static bool LedgeOver(StageMap map, Vector2 a, Vector2 b)
    {
        float floor = Mathf.Max(a.y, b.y);
        int hits = 0;
        for (float x = a.x + 1.5f; x <= b.x - 1.5f; x += 0.5f)
            for (float y = floor + 3f; y <= floor + 6f; y += 0.5f)
                if (map.IsSolid(new Vector2(x, y)) || map.IsPlatform(new Vector2(x, y))) { hits++; break; }
        return hits >= 4;
    }

    static UnityEngine.Tilemaps.Tilemap[] decorMaps;
    static UnityEngine.Tilemaps.Tilemap[] DecorMaps()
    {
        if (decorMaps != null && System.Array.TrueForAll(decorMaps, m => m != null)) return decorMaps;
        var list = new List<UnityEngine.Tilemaps.Tilemap>();
        foreach (var tm in Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>())
            if (tm.name == "Decor" || tm.name == "BackgroundProps") list.Add(tm);
        return decorMaps = list.ToArray();
    }

    // Big props (trees, bushes: the scene's Props) within a portal's width of the spot - a green portal in front of a
    // green bush is lost in it.
    static readonly List<Bounds> props = new List<Bounds>();
    static bool propsRead;
    bool PropsNear(Vector2 spot)
    {
        if (!propsRead)
        {
            propsRead = true;
            props.Clear();
            var root = GameObject.Find("Props");
            if (root != null)
                foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>())
                    if (sr.bounds.size.x > 1.2f || sr.bounds.size.y > 1.2f) props.Add(sr.bounds);
        }
        var zone = new Bounds(new Vector3(spot.x, spot.y + 1.5f, 0f), new Vector3(2.5f * 3f, 3f, 10f)); // the portal and a portal's width each side
        foreach (var bo in props) if (bo.Intersects(zone)) return true;
        // trees and bushes painted as tiles (the Decor and BackgroundProps tilemaps) count too
        foreach (var tm in DecorMaps())
            for (float x = spot.x - 2f; x <= spot.x + 2f; x += 0.5f)
                for (float y = spot.y + 0.3f; y <= spot.y + 3f; y += 0.5f)
                    if (tm.HasTile(tm.WorldToCell(new Vector3(x, y, 0f)))) return true;
        return false;
    }

    // The fork's sign over a log: the route's name and what it means, with its icon (skull / sprout).
    // The fork's crossroads signpost: one post between the two portals (covering neither) with an arrow-shaped arm
    // pointing at each - the hard way's above, the easy way's below - its icon and name on the arm (shrunk to fit, large
    // text too). Drawn behind the cat.
    void MakeSign(Way w)
    {
        float mid = ways.Count == 2 ? (ways[0].holeX + ways[1].holeX) * 0.5f : w.holeX;
        float floor = ways.Count == 2 ? Mathf.Min(ways[0].floorY, ways[1].floorY) : w.floorY;
        float dir = Mathf.Sign(w.holeX - mid); if (dir == 0f) dir = 1f;
        var root = new GameObject("ForkSign").transform;
        root.position = new Vector3(mid, floor - 0.06f, 0f);
        w.sign = root;
        w.signHome = root.position;
        int order = body.Renderer.sortingOrder;
        if (BuildArtSign(w, root, dir, order)) return;
        // at 60% of the drawn size (a sign, not a billboard): the post 1.65 tall, the arms near its top
        const float S = 0.6f;
        if (w.hunt)
        {
            var pole = new GameObject("Pole").AddComponent<SpriteRenderer>();
            pole.transform.SetParent(root, false);
            pole.transform.localScale = Vector3.one * S;
            pole.sprite = Resources.Load<Sprite>("StageExit/exit_signpole");
            pole.sortingOrder = order - 3;
        }
        float armY = w.hunt ? 1.45f : 0.92f;
        var arm = new GameObject("Arm").AddComponent<SpriteRenderer>();
        arm.transform.SetParent(root, false);
        arm.transform.localScale = Vector3.one * S;
        arm.sprite = Resources.Load<Sprite>("StageExit/exit_signarm");
        arm.flipX = dir < 0f;
        arm.sortingOrder = order - 2;
        // The arm fits its content (UI_STYLE.md): 4 px inside each end, the icon, 2 px, the name, then the arrow tip -
        // a sliced sprite, so the plank stretches and its ends and tip keep their pixels. The tip stops 4 px short of
        // the arch's outer edge; an arm that would be longer than that keeps the icon alone.
        const float Px = 1f / 16f;
        float px = S * Px, tip = 8f * px, pad = 4f * px, gap = 2f * px, iconW = 9f * px;
        float limit = Mathf.Abs(w.holeX - mid) - 1.25f - 0.25f;
        bool large = GameSettings.LargeText;
        var icon = new GameObject("Icon").AddComponent<SpriteRenderer>();
        icon.transform.SetParent(root, false);
        icon.transform.localScale = Vector3.one * S;
        icon.sprite = Resources.Load<Sprite>(w.hunt ? "StageExit/exit_icon_hunt" : "StageExit/exit_icon_safe");
        icon.sortingOrder = order - 1;
        // the name at no less than the small text size
        var text = MakeWord(root, Loc.T(w.hunt ? "route.hunt" : "route.safe"), new Color(0.98f, 0.92f, 0.78f), large ? 0.98f : 0.75f, order - 1, false);
        float wordsW = text.textBounds.size.x;
        float armLen = pad + iconW + gap + wordsW + pad + tip;
        if (armLen > limit + 0.6f) { Destroy(text.gameObject); text = null; armLen = pad + iconW + pad + tip; } // (the post side may take 0.6)
        arm.drawMode = SpriteDrawMode.Sliced;
        arm.size = new Vector2(armLen / S, 14f * Px);
        float centre = dir * (Mathf.Min(limit, armLen - 0.1f) - armLen * 0.5f);
        arm.transform.localPosition = new Vector3(centre, armY, 0f);
        float start = centre - dir * armLen * 0.5f; // the arm's post end
        float iconX = start + dir * (pad + iconW * 0.5f);
        icon.transform.localPosition = new Vector3(iconX, armY, 0f);
        if (text != null)
        {
            float wordsMid = start + dir * (pad + iconW + gap + wordsW * 0.5f);
            text.transform.localPosition = new Vector3(wordsMid - text.textBounds.center.x, armY - text.textBounds.center.y, 0f);
        }
    }

    // The new drawn signs (Resources/UICats/ui_sign_hunt red skull / ui_sign_safe green sprout: a vine-wrapped post, the
    // icon in a disc, a plank for the name, an arrow tip): one per way, 1.9 world units wide, the post at the root so the
    // hunt sign stands on the safe one's post, both pointing at their portals. The name is written on the plank (shrunk
    // to fit it). Returns false (the old arm and pole are built) when the art is missing.
    const float ArtSignWidth = 1.9f;
    static Sprite signHuntArt, signSafeArt;
    bool BuildArtSign(Way w, Transform root, float dir, int order)
    {
        var art = Resources.Load<Sprite>(w.hunt ? "UICats/ui_sign_hunt" : "UICats/ui_sign_safe");
        if (art == null) return false;
        ref Sprite cached = ref (w.hunt ? ref signHuntArt : ref signSafeArt);
        if (cached == null) cached = Sprite.Create(art.texture, art.rect, new Vector2(0.03f, 0f), art.rect.width / ArtSignWidth);
        float height = cached.bounds.size.y;
        float baseY = w.hunt ? height + 0.04f : 0f; // the hunt sign above the safe one on the same post
        var plate = new GameObject("SignArt").AddComponent<SpriteRenderer>();
        plate.transform.SetParent(root, false);
        plate.sprite = cached;
        plate.flipX = dir < 0f;
        plate.transform.localPosition = new Vector3(0f, baseY, 0f);
        plate.sortingOrder = order - 2;
        // the name on the plank (the art's plank runs from 0.30 to 0.86 of its width, at the middle of its height)
        bool large = GameSettings.LargeText;
        var text = MakeWord(root, Loc.T(w.hunt ? "route.hunt" : "route.safe"), new Color(0.98f, 0.92f, 0.78f), large ? 0.98f : 0.75f, order - 1, false);
        float room = ArtSignWidth * 0.52f;
        float wordsW = text.textBounds.size.x;
        if (wordsW > room * 1.3f)
        {
            // Far too long for the plank (a long translation): shrunk to it the name was unreadably small. It goes on a
            // dark strip at full size instead - above the upper sign, below the lower one - and the plank stays bare.
            string name = text.text; Color tint = text.color;
            Destroy(text.gameObject);
            // (order 56: the strip's backing is drawn at 54 - under it, at the sign's own order, the words hid behind the strip)
            var strip = MakeWord(root, name, tint, large ? 0.98f : 0.75f, 56, true, 0.92f);
            strip.transform.localPosition = new Vector3(dir * ArtSignWidth * 0.45f - strip.textBounds.center.x,
                                                         (w.hunt ? baseY + height + 0.34f : -0.38f) - strip.textBounds.center.y, 0f);
            return true;
        }
        if (wordsW > room) { text.fontSize *= room / wordsW; text.ForceMeshUpdate(); } // a little over: shrink to the plank
        Vector3 at = new Vector3(dir * ArtSignWidth * 0.55f, baseY + height * 0.5f, 0f);
        text.transform.localPosition = at - new Vector3(text.textBounds.center.x, text.textBounds.center.y, 0f);
        return true;
    }

    Color WayColorOf(Way w) => WayColor(ways.IndexOf(w) >= 0 ? ways.IndexOf(w) : 0);

    // A short line of world text on a dark strip (the fork's signs and the closed message).
    TextMeshPro MakeWord(Transform parent, string label, Color color, float scale, int order = 56, bool backing = true, float backAlpha = 0.85f)
    {
        var go = new GameObject("Word");
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshPro>();
        var hud = RunManager.Instance != null ? RunManager.Instance.hudText : null;
        if (hud != null) tmp.font = hud.font;
        tmp.text = label;
        tmp.fontSize = 3f * scale;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2(6f, 1f);
        tmp.sortingOrder = order;
        tmp.ForceMeshUpdate();
        if (!backing) return tmp; // on the signpost's plank: the wood is its backing
        var back = new GameObject("Back").AddComponent<SpriteRenderer>();
        back.transform.SetParent(go.transform, false);
        back.sprite = RuntimeGeneratedAssets.WhiteSprite(); // 1 unit square
        back.color = new Color(0.08f, 0.05f, 0.08f, backAlpha);
        back.sortingOrder = 54;
        var b = tmp.textBounds;
        back.transform.localPosition = new Vector3(b.center.x, b.center.y, 0f);
        back.transform.localScale = new Vector3(b.size.x + 0.4f, b.size.y + 0.2f, 1f);
        return tmp;
    }

    // ------------------------------------------------------------------ the way out
    // Called when the draft card is picked. Returns false if there is no stage map to put a log on (the caller then
    // just loads the next scene).
    // The boss room: the log leads to the result board (shown behind the iris by `show`) instead of a scene.
    System.Action toResult;
    public bool OpenToResult(System.Action show)
    {
        toResult = show;
        return Open("(result)");
    }

    // The fork: both logs open; walking into one calls `choose(hunt)` and goes on to `next`. False when this stage has
    // no room for two logs here (the caller falls back to the route window).
    public bool OpenFork(string next, System.Action<bool> choose)
    {
        var map = StageMap.Instance;
        if (open || body == null || map == null || string.IsNullOrEmpty(next)) return false;
        if (!IsFork)
        {
            // no closed fork (a climbing stage, or no room at the end): two logs near the cat, on its floor or close
            Vector2 at = body.Feet;
            // an authored stage, not a hunt for gaps: the two rise either side of the cat on its own floor, a fixed step
            // apart that fits the screen (4:3 too); a short floor moves the stage to the nearest flat run wide enough;
            // only where there is none does the old search run
            // first: either side of the card chest, where ForkCentre said the stage would stand
            if (chestSpot.HasValue && (PlaceStaged(map, chestSpot.Value, false, 0f) || PlaceStaged(map, chestSpot.Value, true, 0f))) { }
            else if (!PlaceStaged(map, at) && !PlaceFork(map, s => Mathf.Abs(s.x - at.x) <= 14f && Mathf.Abs(s.y - at.y) <= 6f && !UnderHud(s), closed: false)
                && !PlaceStaged(map, at, cutLedges: true)
                && !PlaceForced(map, at)) return false; // last: the nearest flat run with its ledges out; then no search at all
            ClearOut();
            foreach (var w in ways) StartCoroutine(Appear(w.log));
        }
        fork = true;
        chooseRoute = choose;
        OpenAll(next);
        // Frame both: the camera eases over to the pair's middle, holds a moment and comes back to the cat (position
        // only - the Pixel Perfect camera keeps its whole-number zoom; the controls stay live).
        if (ways.Count == 2) CameraFollow.Focus(FrameFork(), 2f); // eased in 0.45 s, about a second held, back in 0.55 s
        return true;
    }

    public bool Open(string next)
    {
        var map = StageMap.Instance;
        if (open || body == null || map == null || string.IsNullOrEmpty(next)) return false;
        Vector2 feet = body.Feet;
        if (IsFork || (ways.Count == 1 && Mathf.Abs(ways[0].holeX - feet.x) <= FarEnd))
        {
            // the closed log(s) at the end open where they are - a fork opened without a choice (OpenFork) keeps only
            // one log, the plain way
            if (IsFork) { for (int i = 1; i < ways.Count; i++) DestroyWay(ways[i]); ways.RemoveRange(1, ways.Count - 1); ways[0].kind = PlainKind; ways[0].hunt = false; if (ways[0].sign != null) Destroy(ways[0].sign.gameObject); }
        }
        else
        {
            foreach (var w in ways) DestroyWay(w);
            ways.Clear();
            Vector2 spot = PickSpot(map, feet);
            var way = AddWay(spot, feet.x <= spot.x ? -1f : 1f, PlainKind, closed: false); // the mouth faces the cat
            StartCoroutine(Appear(way.log));
        }
        OpenAll(next);
        return true;
    }

    void DestroyWay(Way w)
    {
        if (w.log != null) Destroy(w.log.gameObject);
        if (w.sign != null) Destroy(w.sign.gameObject);
        if (w.word != null) Destroy(w.word.gameObject);
    }

    void OpenAll(string next)
    {
        open = true;
        nextScene = next;
        foreach (var w in ways)
        {
            w.open = true;
            w.swirl = new[] { Load(w.kind, "open_0"), Load(w.kind, "open_1"), Load(w.kind, "open_2"), Load(w.kind, "open_3") };
            if (w.word != null) { Destroy(w.word.gameObject); w.word = null; }
            StartCoroutine(Unblock(w));
        }
        Vector2 feet = body.Feet;
        foreach (var w in ways) StartCoroutine(Gather(feet, w));
    }

    // The vines blow off: a puff of leaves and dust at the mouth, a creak, a little shudder, then the swirl.
    IEnumerator Unblock(Way w)
    {
        var mouth = new Vector3(w.holeX, w.floorY + 1.2f, 0f);
        Vfx.Play("dust_mid", mouth, 1.4f);
        Vfx.Play("smoke", mouth, 0.5f);
        Sfx.Play("land_heavy", 0.6f, 1.4f);
        Sfx.Play("fx_suck", 0.5f, 1.3f); // the wind starts up
        LeafBurst(w, mouth, 6);
        var tr = w.log.transform;
        Vector3 home = tr.position;
        for (float t = 0f; t < 0.25f; t += Time.deltaTime)
        {
            tr.position = home + Vector3.right * Mathf.Round(Mathf.Sin(t * 70f) * 1.5f) / 16f; // whole pixels
            yield return null;
        }
        tr.position = home;
        w.log.sprite = w.swirl[0];
        if (w.rim != null) w.rim.enabled = true;
        StartCoroutine(Burst(w));
    }

    // Where the camera looks as the fork opens: the middle of the cat, both portals and the sign, with an 8% margin;
    // if that is wider than the screen, the cat and the nearer portal (the other is shown by its edge arrow).
    Vector2 FrameFork()
    {
        Vector2 cat = (Vector2)body.Feet + new Vector2(0f, 0.6f), a = Point(ways[0]) + Vector2.up * 1f, b = Point(ways[1]) + Vector2.up * 1f;
        var cam = Camera.main;
        float width = ViewHeight(cam) * (cam != null ? cam.aspect : 16f / 9f) * 0.84f;
        float height = ViewHeight(cam) * 0.84f;
        float lo = Mathf.Min(cat.x, Mathf.Min(a.x, b.x) - 1.5f), hi = Mathf.Max(cat.x, Mathf.Max(a.x, b.x) + 1.5f);
        float bot = Mathf.Min(cat.y - 1f, Mathf.Min(a.y, b.y) - 1.5f), top = Mathf.Max(cat.y + 1f, Mathf.Max(a.y, b.y) + 2f);
        if (hi - lo <= width && top - bot <= height) return new Vector2((lo + hi) * 0.5f, (bot + top) * 0.5f);
        Vector2 near = Vector2.Distance(cat, a) < Vector2.Distance(cat, b) ? a : b;
        return (cat + near) * 0.5f;
    }

    // The moment a portal opens: one ring of light spreads from its opening (0.2 s), in the portal's colour.
    IEnumerator Burst(Way w)
    {
        var sprite = Resources.Load<Sprite>("StageExit/exit_portal_burst");
        if (sprite == null) yield break;
        var sr = new GameObject("PortalBurst").AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = w.log.sortingOrder + 2;
        sr.transform.position = w.Mouth;
        Color col = w.kind == "_hunt" ? new Color(1f, 0.6f, 0.3f) : w.kind == "_safe" ? new Color(0.75f, 0.95f, 0.45f)
                  : w.kind == "_boss" ? new Color(0.97f, 0.46f, 0.6f) : w.kind == "_victory" ? new Color(1f, 0.9f, 0.45f) : new Color(0.66f, 0.85f, 0.98f);
        for (float t = 0f; t < 0.2f; t += Time.deltaTime)
        {
            float k = t / 0.2f;
            sr.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.6f, k);
            sr.color = k < 0.5f ? col : new Color(col.r, col.g, col.b, 0.5f); // two steps, then gone
            yield return null;
        }
        Destroy(sr.gameObject);
    }

    // Where the log goes when the end is far: a floor spot a few steps ahead of the cat (or behind it), or near the
    // card chest on a climbing stage. Floor spots are the map's spawn slots.
    Vector2 PickSpot(StageMap map, Vector2 feet)
    {
        var slots = map.spawnSlots;
        if (slots == null || slots.Length == 0) return feet + Vector2.right * 4f;
        Vector2 end = slots[0];
        if (map.verticalLayout)
        {
            // The card chest was opened beside the player. Keep the exit on this floor,
            // a few steps away, instead of making the player climb to the top again.
            Vector2 nearby = feet;
            float nearbyScore = float.MaxValue;
            foreach (var s in slots)
            {
                float dx = Mathf.Abs(s.x - feet.x), dy = Mathf.Abs(s.y - feet.y);
                if (dx < 3.5f || dx > 8f || dy > 1.2f || !Roomy(map, s)) continue;
                float score = Mathf.Abs(dx - 5f) + dy * 3f;
                if (score < nearbyScore) { nearbyScore = score; nearby = s; }
            }
            if (nearbyScore < float.MaxValue) return nearby;

            // A narrow floor may have no roomy point. Use the closest roomy ledge.
            foreach (var s in slots)
            {
                if (!Roomy(map, s)) continue;
                float score = Vector2.Distance(s, feet) + Mathf.Abs(s.y - feet.y) * 2f;
                if (score < nearbyScore) { nearbyScore = score; nearby = s; }
            }
            if (nearbyScore < float.MaxValue) return nearby;
        }
        foreach (var s in slots) if (s.x > end.x) end = s;
        if (Mathf.Abs(end.x - feet.x) <= FarEnd && Roomy(map, end)) return end;
        float ahead = body.FacingSign;
        Vector2 best = end; float bestScore = float.MaxValue;
        foreach (var s in slots)
        {
            float dx = (s.x - feet.x) * ahead;
            if (Mathf.Abs(s.y - feet.y) > 3f || Mathf.Abs(dx) < 3.5f || !Roomy(map, s)) continue;
            float score = Mathf.Abs(Mathf.Abs(dx) - 6f) + (dx < 0f ? 8f : 0f) + Mathf.Abs(s.y - feet.y) * 2f;
            if (score < bestScore) { bestScore = score; best = s; }
        }
        return best;
    }

    // Room for the 4-unit log (or the 2.5-unit portal, 3 tall) on flat floor: both ends on ground, nothing solid through
    // its height, and open sky (no ground, no ledge) up to 1.5 times the portal's height - jammed under the floor above
    // it reads as stuck.
    const float Headroom = 3f * 1.5f;
    static bool Roomy(StageMap map, Vector2 p) => Roomy(map, p, Headroom);
    static bool Roomy(StageMap map, Vector2 p, float headroom)
    {
        for (float dx = -2f; dx <= 2.01f; dx += 0.8f)
        {
            if (map.IsSolid(new Vector2(p.x + dx, p.y + 0.4f)) || map.IsSolid(new Vector2(p.x + dx, p.y + 1.4f)) || map.IsSolid(new Vector2(p.x + dx, p.y + 2.3f))) return false;
            for (float dy = 2.6f; dy <= headroom; dy += 0.5f)
            {
                var above = new Vector2(p.x + dx, p.y + dy);
                if (map.IsSolid(above) || map.IsPlatform(above)) return false;
            }
            var under = new Vector2(p.x + dx, p.y - 0.15f);
            if (!map.IsSolid(under) && !map.IsPlatform(under)) return false;
        }
        return true;
    }

    // The log pops up out of the undergrowth: a puff of leaves and dust, a little squash and stretch.
    IEnumerator Appear(SpriteRenderer sr)
    {
        var tr = sr.transform;
        Vfx.Play("dust_mid", tr.position, 1.4f);
        Sfx.Play("land_heavy", 0.6f, 1.3f);
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            float k = t / 0.3f;
            float s = k < 0.6f ? Mathf.Lerp(0.2f, 1.15f, k / 0.6f) : Mathf.Lerp(1.15f, 1f, (k - 0.6f) / 0.4f);
            if (tr == null) yield break;
            tr.localScale = new Vector3(2f - s, s, 1f);
            yield return null;
        }
        if (tr != null) tr.localScale = Vector3.one;
    }

    // Fireflies rise around the cat, then string out along the way to a log's mouth.
    IEnumerator Gather(Vector2 from, Way w)
    {
        if (flyFrames == null)
        {
            var sheet = Resources.Load<Sprite>("StageExit/firefly");
            if (sheet == null) yield break;
            var tex = sheet.texture;
            flyFrames = new[]
            {
                Sprite.Create(tex, new Rect(0, 0, 5, 5), new Vector2(0.5f, 0.5f), 16f),
                Sprite.Create(tex, new Rect(5, 0, 5, 5), new Vector2(0.5f, 0.5f), 16f),
            };
        }
        Sfx.Play("card_pick", 0.5f, 1.6f);
        Vector2 hole = new Vector2(w.holeX, w.floorY + 0.8f);
        float dist = Vector2.Distance(hole, from);
        int n = Mathf.Clamp(Mathf.RoundToInt(dist / 1.1f), 4, ways.Count > 1 ? 10 : 18);
        for (int i = 0; i < n; i++)
        {
            float k = (i + 1f) / (n + 1f);
            // Along the way, floating at about the cat's chest height, a gentle wave in it.
            var home = new Vector3(Mathf.Lerp(from.x, hole.x, k), Mathf.Lerp(from.y + 1f, hole.y + 0.4f, k) + Mathf.Sin(k * 9f) * 0.25f, 0f);
            var go = new GameObject("Firefly");
            go.transform.position = (Vector3)from + new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(0.2f, 1.4f), 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = flyFrames[0];
            sr.sortingOrder = body.Renderer.sortingOrder - 3; // over the portal, under the signpost and the cat
            flies.Add(new Firefly { tr = go.transform, sr = sr, home = home, phase = Random.Range(0f, 6f) });
            if (i % 3 == 0) Sfx.Play("ui_click", 0.25f, 2.2f + i * 0.04f);
            yield return new WaitForSeconds(0.05f);
        }
    }

    // The fork's signs stay on screen: one over a portal on a high ledge is pulled down to the screen's top edge (and
    // in from the sides) instead of being cut off.
    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        // Measured on what the camera really draws into (its pixel rect: the window, or a capture's render texture),
        // never Screen.* - in a batch capture the "screen" is tiny while the picture is 1920x1080.
        float W = cam.pixelWidth, H = cam.pixelHeight;
        float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
        Vector3 c = cam.transform.position;
        foreach (var w in ways)
        {
            if (w.rim != null && w.rim.enabled && w.rims != null) w.rim.sprite = w.rims[(int)(Time.time / 0.25f) % 2]; // a slow two-colour blink
            if (w.sign == null) continue;
            var p = w.signHome;
            // (no sideways clamp: a portal off screen takes its sign with it - the edge arrow shows the way there; pulled in
            // from the side it landed on the other portal's sign)
            float topY = c.y + halfH - 0.9f;
            // pushed down by the screen's top it would sit on the arch: beside the portal instead (on the side the cat
            // comes from, at the opening's height), never over it or away from it
            if (p.y > topY) { p.y = Mathf.Min(w.floorY + 2.2f, topY); p.x = w.holeX + w.holeSide * 4.4f; }
            // the screen-fixed UI's corners (HUD top left, minimap and legend top right) are a safe area: a world sign
            // never goes under them (UI_STYLE.md)
            Vector3 sp = cam.WorldToScreenPoint(p);
            float u = H / 1080f;
            // the sign's own extent (icon on the left, words to the right), not just its middle
            float left = cam.WorldToScreenPoint(p - new Vector3(3.3f, 0f, 0f)).x, right = cam.WorldToScreenPoint(p + new Vector3(2.7f, 0f, 0f)).x;
            bool inHud = left < 560f * u && sp.y > H - 360f * u;
            bool inMap = right > W - 480f * u && sp.y > H - 260f * u;
            // a banner up (starter card, set bonus, stage clear) holds the top middle too while it lasts
            bool inBanner = StageBanner.Up && right > W * 0.5f - 400f * u && left < W * 0.5f + 400f * u
                            && sp.y > H * 0.5f + 40f * u && sp.y < H * 0.5f + 340f * u;
            // under the HUD, the minimap or a banner the sign stands down until the way is clear (pushed down instead it
            // landed on its own portal); the portal's keystone - skull or sprout - still says which way it is
            bool show = !w.shut && !(inHud || inMap || inBanner); // (the way not taken keeps its sign down)
            if (w.sign.gameObject.activeSelf != show) w.sign.gameObject.SetActive(show);
            w.sign.position = p;
        }
    }

    void Update()
    {
        if (body == null || ways.Count == 0) return;
        foreach (var w in ways) UpdateWord(w);
        if (!open) { foreach (var w in ways) Bump(w); return; }

        // the swirls in the mouths; faster while one swallows the cat
        frameClock += Time.deltaTime * (entering ? SuckFps : SwirlFps);
        foreach (var w in ways)
            if (w.swirl != null && !w.shut && w.log != null && w.log.transform.localScale == Vector3.one)
                w.log.sprite = w.swirl[(int)frameClock % w.swirl.Length];

        // Fireflies: drift to their place on the path, bob, and blink; the one the cat passes flies on into the log.
        Vector3 into0 = taken != null ? taken.Mouth : ways[0].Mouth;
        foreach (var f in flies)
        {
            if (f.tr == null) continue;
            f.phase += Time.deltaTime;
            Vector3 target = f.home + new Vector3(Mathf.Sin(f.phase * 1.3f) * 0.12f, Mathf.Sin(f.phase * 2.1f) * 0.15f, 0f);
            if (!f.gone && !entering && Mathf.Abs(body.transform.position.x - f.home.x) < 0.5f && Mathf.Abs(body.transform.position.y - f.home.y) < 2f)
                f.gone = true;
            if (f.gone || entering) target = entering ? into0 : NearestMouth(f.home);
            f.tr.position = Vector3.Lerp(f.tr.position, target, 1f - Mathf.Exp(-(f.gone ? 6f : 3.5f) * Time.deltaTime));
            bool bright = Mathf.Sin(f.phase * 4f) > -0.2f;
            f.sr.sprite = flyFrames[bright ? 0 : 1];
            f.sr.color = new Color(1f, 1f, 1f, bright ? 1f : 0.65f);
            if ((f.gone || entering) && Vector2.Distance(f.tr.position, target) < 0.15f) { Destroy(f.tr.gameObject); f.tr = null; }
        }

        // Near an open log, loose leaves are pulled into its mouth: the way in, without a word.
        foreach (var w in ways)
        {
            if (w.shut) continue;
            float near = Mathf.Abs(body.transform.position.x - w.holeX) + Mathf.Abs(body.transform.position.y - w.floorY) * 0.5f;
            if (near < 7f || (entering && w == taken))
            {
                pullIn -= Time.deltaTime / ways.Count;
                if (pullIn <= 0f) { pullIn = entering ? 0.04f : 0.22f; Drawn(w); }
            }
        }
        for (int i = drawn.Count - 1; i >= 0; i--)
        {
            var d = drawn[i];
            if (d == null) { drawn.RemoveAt(i); continue; }
            Vector3 to = NearestMouth(d.position);
            d.position = Vector3.MoveTowards(d.position, to, (entering ? 9f : 4f) * Time.deltaTime);
            d.Rotate(0f, 0f, 400f * Time.deltaTime);
            float left = Vector2.Distance(d.position, to);
            d.localScale = Vector3.one * Mathf.Clamp01(left / 1.5f + 0.2f);
            if (left < 0.1f) { Destroy(d.gameObject); drawn.RemoveAt(i); }
        }

        // Walking into an open mouth.
        if (entering) return;
        // W is the jump key too: the press that asks to go in has often already lifted the cat off the floor (which
        // script ran first decided it), and "airborne" then cancelled the entry - W at a log worked only now and then.
        // With W held the cat goes in from the hop as well; walking in without a key (AutoEnter) still needs the floor.
        bool up = GameInput.Move.y > 0.5f;
        if (body.IsAirborne && !up) return;
        Vector2 feet = body.Feet;
        foreach (var w in ways)
        {
            if (!w.open || w.shut) continue;
            float into = (feet.x - w.holeX) * -w.holeSide; // > 0 once past the opening, toward the log's middle
            // only at the mouth: a cat past the log (behind it, or beyond) is not drawn in
            if (into > -0.45f && into < 1.2f && Mathf.Abs(feet.y - w.floorY) < 0.8f)
            {
                // At the mouth, W / Up goes in. Touching it used to: a cat walking past a fork's log was taken down that way.
                if (AutoEnter || up) { StartCoroutine(Enter(w)); return; }
                if (w.word == null)
                {
                    var at = new GameObject("EnterWord").transform;
                    at.position = w.Mouth + Vector3.up * 1.5f;
                    w.word = MakeWord(at, Loc.T("exit.enter"), new Color(1f, 0.85f, 0.6f), GameSettings.LargeText ? 1.3f : 1f);
                }
                w.wordLeft = 0.3f; // gone a moment after stepping away
            }
        }
    }

    Vector3 NearestMouth(Vector3 p)
    {
        Way best = null; float bd = float.MaxValue;
        foreach (var w in ways)
        {
            if (w.shut) continue;
            float d = Vector2.Distance(p, w.Mouth);
            if (d < bd) { bd = d; best = w; }
        }
        return best != null ? best.Mouth : ways[0].Mouth;
    }

    // Walking into a closed log: it shudders and says why (one message; a new bump keeps it up longer).
    void Bump(Way w)
    {
        w.bumpCooldown -= Time.deltaTime;
        if (w.bumpCooldown > 0f || body.IsAirborne) return;
        Vector2 feet = body.Feet;
        if (Mathf.Abs(feet.x - w.holeX) > 0.6f || Mathf.Abs(feet.y - w.floorY) > 0.8f) return;
        w.bumpCooldown = 0.6f;
        StartCoroutine(Shudder(w));
        Sfx.Play("ui_error", 0.6f);
        if (w.word == null)
        {
            var at = new GameObject("ClosedWord").transform;
            at.position = w.Mouth + Vector3.up * 1.5f;
            w.word = MakeWord(at, Loc.T("exit.closed"), new Color(1f, 0.85f, 0.6f), GameSettings.LargeText ? 1.3f : 1f);
        }
        w.wordLeft = ClosedWordTime;
    }

    void UpdateWord(Way w)
    {
        if (w.word == null) return;
        w.wordLeft -= Time.deltaTime;
        float a = Mathf.Clamp01(w.wordLeft / 0.15f); // a quick fade at the end
        w.word.alpha = a;
        var back = w.word.GetComponentInChildren<SpriteRenderer>();
        if (back != null) back.color = new Color(0.08f, 0.05f, 0.08f, 0.85f * a);
        if (w.wordLeft <= 0f) { Destroy(w.word.transform.parent.gameObject); w.word = null; }
    }

    IEnumerator Shudder(Way w)
    {
        var tr = w.log.transform;
        Vector3 home = tr.position;
        for (float t = 0f; t < 0.25f; t += Time.deltaTime)
        {
            tr.position = home + Vector3.right * Mathf.Round(Mathf.Sin(t * 80f) * 1.5f) / 16f;
            yield return null;
        }
        tr.position = home;
    }

    // A leaf from round a log, about to be pulled in.
    void Drawn(Way w)
    {
        if (leafSprites == null)
        {
            var list = new List<Sprite>();
            foreach (var n in crimson ? new[] { "red_a", "red_b", "gold_a" } : new[] { "green_a", "green_b", "gold_a" })
            {
                var s = Resources.Load<Sprite>("StageExit/leaf_" + n);
                if (s != null) list.Add(s);
            }
            leafSprites = list.ToArray();
        }
        if (leafSprites.Length == 0) return;
        var go = new GameObject("DrawnLeaf");
        go.transform.position = w.Mouth + new Vector3(w.holeSide * Random.Range(1.2f, 3.4f), Random.Range(-1.0f, 1.6f), 0f); // from the open side
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = leafSprites[Random.Range(0, leafSprites.Length)];
        sr.sortingOrder = w.log.sortingOrder + 1;
        drawn.Add(go.transform);
    }

    void LeafBurst(Way w, Vector3 at, int n)
    {
        for (int i = 0; i < n; i++)
        {
            Drawn(w);
            if (drawn.Count > 0) drawn[drawn.Count - 1].position = at + new Vector3(Random.Range(-1f, 1f), Random.Range(-0.8f, 0.8f), 0f);
        }
    }

    // Pulled in: the controls lock, the cat is drawn to the mouth stretching toward it, then shrinks spinning in; the
    // camera leans in; the screen irises shut on the mouth and the next stage loads. The fork's other log shuts.
    IEnumerator Enter(Way w)
    {
        entering = true;
        taken = w;
        foreach (var other in ways)
        {
            if (other == w) continue;
            other.shut = true;
            if (other.log != null) other.log.sprite = Load(other.kind, "closed");
            if (other.sign != null) other.sign.gameObject.SetActive(false);
            if (other.rim != null) other.rim.enabled = false;
        }
        if (chooseRoute != null) chooseRoute(w.hunt);
        arrivedKind = w.kind;
        body.skillLock = true;
        body.skillHover = true;
        body.FaceTowards(-w.holeSide);
        Sfx.Play("fx_suck");
        var visual = body.visual != null ? body.visual : body.Renderer.transform;
        Vector3 scale = visual.localScale;
        Vector3 start = body.transform.position;
        Vector3 mouth = w.Mouth;
        // the cat's middle to the mouth's middle: lifted off the floor as it is drawn in
        Vector3 target = new Vector3(mouth.x, start.y + (mouth.y - 0.45f - body.Feet.y), start.z);
        var cam = Camera.main;
        var ppc = cam != null ? cam.GetComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>() : null;
        float size0 = cam != null ? cam.orthographicSize : 0f;
        if (ppc != null) { if (ppc.orthographicSize > 0f) size0 = ppc.orthographicSize; ppc.enabled = false; } // it would hold the size
        // shake off in the settings: only a gentle lean in
        float zoom = GameSettings.ShakeStrength > 0f ? 0.86f : 0.95f;
        CameraFollow.Focus(mouth, 1.2f);
        const float Pull = 1.0f;
        bool closing = false;
        for (float t = 0f; t < Pull; t += Time.deltaTime)
        {
            float k = t / Pull;
            body.transform.position = Vector3.Lerp(start, target, k * k);
            // stretch toward the mouth for the first half, then shrink and spin away
            float stretch = k < 0.5f ? k / 0.5f : 1f;
            float shrink = k < 0.5f ? 1f : 1f - (k - 0.5f) / 0.5f;
            visual.localScale = new Vector3(scale.x * (1f + 0.45f * stretch) * shrink, scale.y * (1f - 0.3f * stretch) * shrink, scale.z);
            visual.localRotation = Quaternion.Euler(0f, 0f, k < 0.5f ? 0f : (k - 0.5f) * 2f * 540f * w.holeSide);
            if (cam != null) cam.orthographicSize = Mathf.Lerp(size0, size0 * zoom, Mathf.SmoothStep(0f, 1f, k));
            if (!closing && k >= 0.55f)
            {
                closing = true;
                Vector2 mouthOnScreen = cam != null ? (Vector2)cam.WorldToScreenPoint(mouth) : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                if (toResult != null) StageWipe.CloseAndOpen(mouthOnScreen, toResult);
                else SceneLoader.LoadThroughExit(nextScene, mouthOnScreen);
            }
            yield return null;
        }
        body.Renderer.enabled = false; // inside now (the iris covers the screen by then)
        visual.localScale = scale;
        visual.localRotation = Quaternion.identity;
    }

    // For the capture tool: the log(s) open, or the cat put at a mouth (the fork's hard way when `hunt`) so it is
    // being pulled in.
    public void ForCapture(bool pull, bool hunt = false)
    {
        if (!open) Open("Stage3");
        if (!pull || body == null || ways.Count == 0) return;
        Way w = ways[0];
        foreach (var x in ways) if (x.hunt == hunt) w = x;
        body.transform.position += new Vector3(w.holeX + w.holeSide * 0.3f - body.Feet.x, w.floorY - body.Feet.y, 0f); // feet on the floor
    }
}
